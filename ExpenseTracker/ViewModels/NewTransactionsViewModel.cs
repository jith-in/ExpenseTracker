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
            IAiService aiService)
        {
            _smsReaderService = smsReaderService;
            _smsImportService = smsImportService;
            _repository = repository;
            _aiService = aiService;
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

                // 🚀 STEP 1: Offload heavy SMS loading, Regex parsing, and DB reads entirely to a background thread
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

                    // Filter out Track 2 (AI Staging) entries that need immediate Gemini parsing
                    var rawAiStagingItems = unprocessedBacklog
                        .Where(t => t.SuggestedCategory == "Pending AI Analysis")
                        .ToList();

                    return new { unprocessedBacklog, rawAiStagingItems };
                });

                // 🚀 STEP 2: Handle live Gemini analysis if any pending items exist
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
                            // Offload database save execution to background thread pool
                            await Task.Run(async () =>
                            {
                                foreach (var aiItem in aiResponse.ProcessedTransactions)
                                {
                                    var localMatch = processedData.rawAiStagingItems.FirstOrDefault(x => x.Id == aiItem.Id);
                                    if (localMatch != null)
                                    {
                                        localMatch.Amount = aiItem.Amount;
                                        localMatch.Merchant = aiItem.Note;
                                        localMatch.SuggestedCategory = aiItem.Category;
                                        localMatch.TransactionType = aiItem.TransactionType;
                                        localMatch.SuggestedPaymentMethod = "AI_STAGED"; // Fast classification tag flag

                                        await _repository.SaveImportedTransactionAsync(localMatch);
                                    }
                                }
                            });
                        }
                    }
                    finally
                    {
                        IsAiAnalyzing = false;
                    }
                }

                // 🚀 STEP 3: Sort and partition the finalized entries into Section Groups on a background thread
                var structuralGroups = await Task.Run(() =>
                {
                    var standardItems = processedData.unprocessedBacklog
                        .Where(t => t.SuggestedCategory != "Pending AI Analysis" && t.SuggestedPaymentMethod != "AI_STAGED")
                        .OrderBy(t => t.TransactionDate ?? t.SmsReceivedDate)
                        .ToList();

                    var finalizedAiItems = processedData.unprocessedBacklog
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
                decimal adjustedAmount = string.Equals(transaction.TransactionType, "Credit", StringComparison.OrdinalIgnoreCase)
                    ? -Math.Abs(transaction.Amount)
                    : Math.Abs(transaction.Amount);

                var expense = new Expense
                {
                    Amount = adjustedAmount,
                    Category = transaction.SuggestedCategory,
                    Merchant = transaction.Merchant,
                    TransactionType = transaction.TransactionType,
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

                // Clean item from virtualized group
                RemoveTransactionFromUI(transaction);
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

                    // If a section category drops to zero records, remove the header panel completely
                    if (!group.Any())
                    {
                        GroupedTransactions.Remove(group);
                    }
                    break;
                }
            }
        }
    }
}