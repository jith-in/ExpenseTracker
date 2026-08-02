using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Interfaces;
using ExpenseTracker.Models;
using ExpenseTracker.Repositories;
using ExpenseTracker.Services;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace ExpenseTracker.ViewModels
{
    public partial class NewTransactionsViewModel : BaseViewModel
    {
        private readonly ISmsReaderService _smsReaderService;
        private readonly ISmsImportService _smsImportService;
        private readonly IExpenseRepository _repository;
        private readonly IAiService _aiService;
        private readonly IBudgetAlertService _budgetAlertService; // 🎯 1. Budget Service Reference

        // ==========================================
        // Observable Properties
        // ==========================================

        [ObservableProperty]
        private ObservableCollection<TransactionGroup> groupedTransactions = new(); // 🎯 Unified Grouped Collection Source

        [ObservableProperty]
        private bool isRefreshing;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        [ObservableProperty]
        private bool isAiAnalyzing;

        // ==========================================
        // Constructor
        // ==========================================

        public NewTransactionsViewModel(
            ISmsReaderService smsReaderService,
            ISmsImportService smsImportService,
            IExpenseRepository repository,
            IAiService aiService,
            IBudgetAlertService budgetAlertService) // 🎯 2. Injected Budget Alert Service
        {
            _smsReaderService = smsReaderService;
            _smsImportService = smsImportService;
            _repository = repository;
            _aiService = aiService;
            _budgetAlertService = budgetAlertService;
            Title = "New Transactions";
        }

        // ==========================================
        // Commands & Core Business Logic
        // ==========================================

        [RelayCommand]
        public async Task LoadNewTransactionsAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            IsRefreshing = true;
            StatusMessage = string.Empty;

            try
            {
                // 🔐 Security check handles permission requests on the Main Thread safely
                if (!await _smsReaderService.CheckSmsPermissionAsync())
                {
                    var requestResult = await _smsReaderService.RequestSmsPermissionAsync();
                    if (!requestResult)
                    {
                        StatusMessage = "SMS permission is required.";
                        return;
                    }
                }

                // 🚀 STEP 1: Background processing for local records
                var processedData = await Task.Run(async () =>
                {
                    var smsBodies = await _smsReaderService.GetRecentSmsBodiesAsync();
                    await _smsImportService.ParseIncomingMessagesAsync(smsBodies);

                    var allImportedTransactions = await _repository.GetImportedTransactionsAsync();

                    DateTime today = DateTime.Today;
                    DateTime startDate = today.Day < 20
                        ? new DateTime(today.Year, today.Month, 20).AddMonths(-1)
                        : new DateTime(today.Year, today.Month, 20);
                    DateTime endDate = startDate.AddMonths(1);

                    var unprocessedBacklog = allImportedTransactions
                        .Where(t => !t.IsProcessed && (t.TransactionDate ?? t.SmsReceivedDate) >= startDate && (t.TransactionDate ?? t.SmsReceivedDate) < endDate)
                        .ToList();

                    var rawAiStagingItems = unprocessedBacklog
                        .Where(t => t.SuggestedCategory == "Pending AI Analysis")
                        .ToList();

                    return new { unprocessedBacklog, rawAiStagingItems };
                });

                // 🚀 STEP 2: Handle live Gemini analysis with strict type sanitation
                if (processedData.rawAiStagingItems.Any() && Connectivity.Current.NetworkAccess == Microsoft.Maui.Networking.NetworkAccess.Internet)
                {
                    StatusMessage = "Gemini is analyzing unknown text structures...";
                    IsAiAnalyzing = true;

                    try
                    {
                        var currentBatchSlices = processedData.rawAiStagingItems.Select(r => new Expense { Id = r.Id, Note = r.SmsContent }).ToList();
                        var aiResponse = await _aiService.ParseBatchAsync(currentBatchSlices);

                        if (aiResponse?.ProcessedTransactions != null)
                        {
                            await Task.Run(async () =>
                            {
                                foreach (var aiItem in aiResponse.ProcessedTransactions)
                                {
                                    var localMatch = processedData.rawAiStagingItems.FirstOrDefault(x => x.Id == aiItem.Id);
                                    if (localMatch != null)
                                    {
                                        // 🛑 RULE 1: Automatically filter out telecom warnings, spam alerts, or OTP message logs
                                        if (!aiItem.IsTransaction)
                                        {
                                            localMatch.IsProcessed = true; // Flags it so future queries ignore it entirely
                                            await _repository.SaveImportedTransactionAsync(localMatch);
                                            continue;
                                        }

                                        // 🟢 RULE 2: Hydrate transaction entity schemas safely using structural checks
                                        localMatch.Amount = aiItem.Amount ?? 0.00m; // Safeguards against completely redacted text flags
                                        localMatch.Merchant = !string.IsNullOrWhiteSpace(aiItem.MerchantOrEntity) ? aiItem.MerchantOrEntity : "Unknown Entity";
                                        localMatch.SuggestedCategory = !string.IsNullOrWhiteSpace(aiItem.Category) ? aiItem.Category : "Miscellaneous";
                                        localMatch.ReferenceNumber = aiItem.TransactionId;

                                        // Enforce uniform type validation casing context checks 
                                        localMatch.TransactionType = NormalizeTransactionDirection(aiItem.TransactionType, localMatch.SmsContent);
                                        localMatch.SuggestedPaymentMethod = "AI_STAGED";

                                        await _repository.SaveImportedTransactionAsync(localMatch);
                                    }
                                }
                            });
                        }
                    }
                    finally
                    {
                       // LogSmsToVisualStudioConsole(processedData.unprocessedBacklog);
                        IsAiAnalyzing = false;
                    }
                }

                // 🚀 STEP 3: Sanitize and segment all entries on a background thread
                var structuralGroups = await Task.Run(async () =>
                {
                    // Clean up historical local regex entries and persist normalized direction to SQLite
                    foreach (var item in processedData.unprocessedBacklog)
                    {
                        string normalizedType = NormalizeTransactionDirection(item.TransactionType, item.SmsContent);
                        if (item.TransactionType != normalizedType)
                        {
                            item.TransactionType = normalizedType;
                            await _repository.SaveImportedTransactionAsync(item);
                        }
                    }

                    // Exclude items handled as non-transactions in Step 2 to keep layout lists pristine
                    var activeBacklog = processedData.unprocessedBacklog.Where(t => !t.IsProcessed).ToList();

                    var standardItems = activeBacklog
                        .Where(t => t.SuggestedCategory != "Pending AI Analysis" && t.SuggestedPaymentMethod != "AI_STAGED")
                        .OrderBy(t => t.TransactionDate ?? t.SmsReceivedDate)
                        .ToList();

                    var finalizedAiItems = activeBacklog
                        .Where(t => t.SuggestedCategory == "Pending AI Analysis" || t.SuggestedPaymentMethod == "AI_STAGED")
                        .OrderBy(t => t.SmsReceivedDate)
                        .ToList();

                    var groupsList = new List<TransactionGroup>();

                    if (standardItems.Any())
                        groupsList.Add(new TransactionGroup("Parsed Transactions", "Green", standardItems));

                    if (finalizedAiItems.Any())
                        groupsList.Add(new TransactionGroup("AI Generated Transactions (Review Required)", "#6C3483", finalizedAiItems));

                    return groupsList;
                });

                // 🚀 STEP 4: Render onto UI thread atomically
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    GroupedTransactions = new ObservableCollection<TransactionGroup>(structuralGroups);
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error dividing pipeline structures: {ex}");
                StatusMessage = "Failed to synchronize staging logs.";
            }
            finally
            {
                StatusMessage = string.Empty;
                IsBusy = false;
                IsRefreshing = false;
            }
        }

        /// <summary>
        /// 🎯 THE TRANSACTION NORMALIZER ENGINE
        /// Enforces strict architectural type safety across your database boundaries
        /// </summary>
        private string NormalizeTransactionDirection(string rawType, string smsContent)
        {
            if (string.IsNullOrWhiteSpace(smsContent))
                return "Debit";

            // 1. Convert completely to lowercase immediately to guarantee case-insensitive matching
            string text = smsContent.ToLowerInvariant();

            // 🎯 2. Custom Income Pattern Rule (Treats NEFT/Salary credits as true Income/Credits)
            if (text.Contains("credited") && (text.Contains("neft") || text.Contains("salary")))
            {
                return "Credit";
            }

            // 🎯 3. ICICI Outbound UPI Fix (Catch "debited for ... credited" pattern before generic credit checks)
            if (text.Contains("debited for") || text.Contains("debited rs") || (text.Contains("acc xx") && text.Contains("debited")))
            {
                return "Debit";
            }

            // 4. 🎯 High-Priority Explicit Merchant/Telecom Expense Overrides
            if (text.Contains("recharge successful") ||
                text.Contains("bill paid") ||
                text.Contains("spent at") ||
                text.Contains("prepaid re") ||
                text.Contains("prepaid rec") ||
                text.Contains("airtel") ||
                text.Contains("jio") ||
                text.Contains("bsnl"))
            {
                return "Debit";
            }

            // 5. Fallback explicit validation from the cloud layer
            if (!string.IsNullOrWhiteSpace(rawType))
            {
                if (string.Equals(rawType, "Credit", StringComparison.OrdinalIgnoreCase)) return "Credit";
                if (string.Equals(rawType, "Debit", StringComparison.OrdinalIgnoreCase)) return "Debit";
            }

            // 6. Standard Direction Parsing
            if (text.Contains("credited") || text.Contains("received") || text.Contains("refunded") || text.Contains("cashback"))
            {
                return "Credit";
            }

            return "Debit";
        }

        [RelayCommand]
        public async Task ViewFullMessageAsync(ImportedTransaction transaction)
        {
            if (transaction == null) return;
            await Shell.Current.DisplayAlert("Full Message", transaction.SmsContent, "Close");
        }

        [RelayCommand]
        public async Task EditTransactionAsync(ImportedTransaction transaction)
        {
            if (transaction == null || IsBusy) return;

            var effectiveDate = transaction.TransactionDate ?? transaction.SmsReceivedDate;

            var navigationParameters = new Dictionary<string, object>
            {
                { "amount", transaction.Amount.ToString() },
                { "merchant", transaction.Merchant },
                { "date", effectiveDate.ToString("o") },
                { "category", transaction.SuggestedCategory },
                { "type", transaction.TransactionType },
                { "importId", transaction.Id.ToString() }
            };

            await Shell.Current.GoToAsync("///AddExpensePage", navigationParameters);
        }

        [RelayCommand]
        public async Task AcceptTransactionAsync(ImportedTransaction transaction)
        {
            if (transaction == null || IsBusy) return;

            IsBusy = true;
            try
            {
                // Re-evaluate direction before saving
                string direction = NormalizeTransactionDirection(transaction.TransactionType, transaction.SmsContent);

                var expense = new Expense
                {
                    Amount = Math.Abs(transaction.Amount), // Store positive magnitude
                    Category = transaction.SuggestedCategory,
                    Merchant = transaction.Merchant,
                    TransactionType = direction,
                    ReferenceNumber = transaction.ReferenceNumber,
                    Date = transaction.TransactionDate ?? transaction.SmsReceivedDate,
                    Note = transaction.SmsContent,
                    IsImported = true,
                    PaymentMethod = (string.IsNullOrWhiteSpace(transaction.SuggestedPaymentMethod) || transaction.SuggestedPaymentMethod == "AI_STAGED")
                        ? "Net Banking"
                        : transaction.SuggestedPaymentMethod
                };

                await _repository.SaveExpenseAsync(expense);

                if (!string.IsNullOrWhiteSpace(transaction.SuggestedCategory) && transaction.Merchant != "Unparsed Financial SMS")
                {
                    await _repository.SaveMerchantCategoryMappingAsync(new MerchantCategoryMapping
                    {
                        Merchant = transaction.Merchant,
                        Category = transaction.SuggestedCategory
                    });
                }

                transaction.IsProcessed = true;
                await _repository.SaveImportedTransactionAsync(transaction);

                RemoveTransactionFromUI(transaction);

                // 🎯 3. Check and display budget alert if limit/threshold is reached
                await _budgetAlertService.CheckAndShowBudgetAlertAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        // 🌟 Flattened Bulk Processor supporting virtualized grouping
        [RelayCommand]
        public async Task LogAllTransactionsAsync()
        {
            var totalVisibleBacklog = GroupedTransactions.SelectMany(group => group).ToList();

            if (IsBusy || !totalVisibleBacklog.Any()) return;

            bool isConfirmed = await Shell.Current.DisplayAlert(
                "Log All Transactions",
                $"Are you sure you want to approve and log all {totalVisibleBacklog.Count} pending entries?",
                "Log All",
                "Cancel");

            if (!isConfirmed) return;

            IsBusy = true;
            try
            {
                var expensesToInsert = new List<Expense>();
                var transactionsToUpdate = new List<ImportedTransaction>();

                foreach (var trans in totalVisibleBacklog)
                {
                    // Re-evaluate direction before batch save
                    string direction = NormalizeTransactionDirection(trans.TransactionType, trans.SmsContent);

                    expensesToInsert.Add(new Expense
                    {
                        Amount = Math.Abs(trans.Amount), // Always absolute amount
                        Category = trans.SuggestedCategory ?? "Others",
                        Date = trans.TransactionDate ?? trans.SmsReceivedDate,
                        Merchant = trans.Merchant,
                        TransactionType = direction,
                        ReferenceNumber = trans.ReferenceNumber,
                        Note = trans.SmsContent,
                        IsImported = true,
                        PaymentMethod = (string.IsNullOrWhiteSpace(trans.SuggestedPaymentMethod) || trans.SuggestedPaymentMethod == "AI_STAGED") ? "Net Banking" : trans.SuggestedPaymentMethod
                    });

                    trans.IsProcessed = true;
                    transactionsToUpdate.Add(trans);
                }

                await _repository.BulkLogTransactionsAsync(expensesToInsert, transactionsToUpdate);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    GroupedTransactions.Clear();
                });

                // 🎯 4. Check and display budget alert after bulk logging
                await _budgetAlertService.CheckAndShowBudgetAlertAsync();

                await Shell.Current.DisplayAlert("Success", "All transactions have been integrated successfully.", "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during bulk upload execution: {ex}");
                await Shell.Current.DisplayAlert("Import Error", "Failed to batch process transaction imports.", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task IgnoreTransactionAsync(ImportedTransaction transaction)
        {
            if (transaction == null) return;
            transaction.IsProcessed = true;
            await _repository.SaveImportedTransactionAsync(transaction);
            RemoveTransactionFromUI(transaction);
        }

        private void RemoveTransactionFromUI(ImportedTransaction targetItem)
        {
            foreach (var group in GroupedTransactions.ToList())
            {
                if (group.Contains(targetItem))
                {
                    group.Remove(targetItem);

                    if (!group.Any())
                    {
                        GroupedTransactions.Remove(group);
                    }
                    break;
                }
            }
        }

        private void LogSmsToVisualStudioConsole(IEnumerable<ImportedTransaction> transactions)
        {
            var logList = transactions.ToList();

            Debug.WriteLine("\n==================================================");
            Debug.WriteLine($"  SMS IMPORT DEBUG LOG - {DateTime.Now:dd MMM yyyy HH:mm:ss}");
            Debug.WriteLine($"  Total Messages Logged: {logList.Count}");
            Debug.WriteLine("==================================================\n");

            foreach (var item in logList)
            {
                Debug.WriteLine($"[ID: {item.Id}]");
                Debug.WriteLine($"Type:           {item.TransactionType}");
                Debug.WriteLine($"Merchant:       {item.Merchant}");
                Debug.WriteLine($"Amount:         ₹{item.Amount:F2}");
                Debug.WriteLine($"Category:       {item.SuggestedCategory}");
                Debug.WriteLine($"Received Date:  {item.SmsReceivedDate:yyyy-MM-dd HH:mm:ss}");
                Debug.WriteLine($"SMS Content:    {item.SmsContent}");
                Debug.WriteLine("--------------------------------------------------");
            }

            Debug.WriteLine("=================== END LOG ======================\n");
        }
    }
}