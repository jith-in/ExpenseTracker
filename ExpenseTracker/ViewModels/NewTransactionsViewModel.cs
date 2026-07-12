using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Interfaces;
using ExpenseTracker.Models;
using ExpenseTracker.Repositories;
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

        [ObservableProperty]
        private ObservableCollection<ImportedTransaction> importedTransactions = new();

        [ObservableProperty]
        private bool isRefreshing;

        [ObservableProperty]
        private string statusMessage = string.Empty;

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
                    // Use the null-coalescing operator to determine the "effective" date
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

        // ✅ ADDED: This method generates the ViewFullMessageCommand automatically
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

            // Use the effective date for editing
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
                var expense = new Expense
                {
                    Amount = transaction.Amount,
                    Category = transaction.SuggestedCategory,
                    Merchant = transaction.Merchant,
                    TransactionType = transaction.TransactionType,
                    ReferenceNumber = transaction.ReferenceNumber,
                    // Assign the effective date here
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
        public async Task IgnoreTransactionAsync(ImportedTransaction transaction)
        {
            if (transaction == null) return;
            transaction.IsProcessed = true;
            await _repository.SaveImportedTransactionAsync(transaction);
            ImportedTransactions.Remove(transaction);
        }
    }
}