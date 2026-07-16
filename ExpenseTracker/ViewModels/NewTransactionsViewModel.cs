using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Interfaces;
using ExpenseTracker.Models;
using ExpenseTracker.Repositories;
using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
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

        // ==========================================
        // Observable Private Fields
        // (The source generator uses these to create the upper-case public properties)
        // ==========================================

        [ObservableProperty]
        private ObservableCollection<ImportedTransaction> importedTransactions = new();

        [ObservableProperty]
        private bool isRefreshing;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        // ==========================================
        // Constructor
        // ==========================================

        public NewTransactionsViewModel(
            ISmsReaderService smsReaderService,
            ISmsImportService smsImportService,
            IExpenseRepository repository)
        {
            _smsReaderService = smsReaderService;
            _smsImportService = smsImportService;
            _repository = repository;
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
                if (!await _smsReaderService.CheckSmsPermissionAsync())
                {
                    var requestResult = await _smsReaderService.RequestSmsPermissionAsync();
                    if (!requestResult)
                    {
                        StatusMessage = "SMS permission is required.";
                        return;
                    }
                }

                var smsBodies = await _smsReaderService.GetRecentSmsBodiesAsync();
                await _smsImportService.ParseIncomingMessagesAsync(smsBodies);

                var allImportedTransactions = await _repository.GetImportedTransactionsAsync();

                if (allImportedTransactions.Any())
                {
                    var minDate = allImportedTransactions.Min(t => t.TransactionDate ?? t.SmsReceivedDate);
                    var maxDate = allImportedTransactions.Max(t => t.TransactionDate ?? t.SmsReceivedDate);
                    Debug.WriteLine($"Debug: Oldest: {minDate:yyyy-MM-dd}, Newest: {maxDate:yyyy-MM-dd}");
                }

                DateTime today = DateTime.Today;
                DateTime startDate = today.Day < 20
                    ? new DateTime(today.Year, today.Month, 20).AddMonths(-1)
                    : new DateTime(today.Year, today.Month, 20);

                DateTime endDate = startDate.AddMonths(1);

                var unprocessed = allImportedTransactions
                    .Where(t => !t.IsProcessed && (t.TransactionDate ?? t.SmsReceivedDate) >= startDate && (t.TransactionDate ?? t.SmsReceivedDate) < endDate)
                    .OrderBy(t => t.TransactionDate ?? t.SmsReceivedDate)
                    .ToList();

                ImportedTransactions = new ObservableCollection<ImportedTransaction>(unprocessed);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading transactions: {ex}");
                StatusMessage = "Failed to load new transactions.";
            }
            finally
            {
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
                // Assign correct mathematical sign depending on internal metadata flags
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
                    PaymentMethod = string.IsNullOrWhiteSpace(transaction.SuggestedPaymentMethod)
                        ? "Net Banking"
                        : transaction.SuggestedPaymentMethod
                };

                await _repository.SaveExpenseAsync(expense);

                if (!string.IsNullOrWhiteSpace(transaction.SuggestedCategory))
                {
                    await _repository.SaveMerchantCategoryMappingAsync(new MerchantCategoryMapping
                    {
                        Merchant = transaction.Merchant,
                        Category = transaction.SuggestedCategory
                    });
                }

                transaction.IsProcessed = true;
                await _repository.SaveImportedTransactionAsync(transaction);
                ImportedTransactions.Remove(transaction);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task LogAllTransactionsAsync()
        {
            if (IsBusy || ImportedTransactions == null || !ImportedTransactions.Any())
                return;

            bool isConfirmed = await Shell.Current.DisplayAlert(
                "Log All Transactions",
                $"Are you sure you want to approve and log all {ImportedTransactions.Count} new transactions?",
                "Log All",
                "Cancel");

            if (!isConfirmed) return;

            IsBusy = true;
            try
            {
                var expensesToInsert = new List<Expense>();
                var transactionsToUpdate = new List<ImportedTransaction>();

                foreach (var trans in ImportedTransactions.ToList())
                {
                    // Enforce mathematical signs to differentiate debits vs credits
                    decimal adjustedAmount = string.Equals(trans.TransactionType, "Credit", StringComparison.OrdinalIgnoreCase)
                        ? -Math.Abs(trans.Amount)
                        : Math.Abs(trans.Amount);

                    expensesToInsert.Add(new Expense
                    {
                        Amount = adjustedAmount,
                        Category = trans.SuggestedCategory ?? "Others",
                        Date = trans.TransactionDate ?? trans.SmsReceivedDate,
                        Merchant = trans.Merchant,
                        TransactionType = trans.TransactionType,
                        ReferenceNumber = trans.ReferenceNumber,
                        Note = trans.SmsContent,
                        IsImported = true,
                        PaymentMethod = string.IsNullOrWhiteSpace(trans.SuggestedPaymentMethod)
                            ? "Net Banking"
                            : trans.SuggestedPaymentMethod
                    });

                    trans.IsProcessed = true;
                    transactionsToUpdate.Add(trans);
                }

                // Call the repository batch execution pipeline method
                await _repository.BulkLogTransactionsAsync(expensesToInsert, transactionsToUpdate);

                ImportedTransactions.Clear();

                await Shell.Current.DisplayAlert("Success", "All new transactions have been integrated successfully.", "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during transaction bulk upload execution: {ex}");
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
            ImportedTransactions.Remove(transaction);
        }
    }
}

