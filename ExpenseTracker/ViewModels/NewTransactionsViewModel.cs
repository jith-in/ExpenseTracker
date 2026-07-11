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
            Debug.WriteLine("Startup: NewTransactionsViewModel ctor begin");
            _smsReaderService = smsReaderService;
            _smsImportService = smsImportService;
            _repository = repository;
            Title = "New Transactions";
            Debug.WriteLine("Startup: NewTransactionsViewModel ctor end");
        }

        [RelayCommand]
        public async Task LoadNewTransactionsAsync()
        {
            if (IsBusy)
            {
                return;
            }

            Debug.WriteLine("Startup: NewTransactionsViewModel.LoadNewTransactionsAsync begin");
            IsBusy = true;
            StatusMessage = string.Empty;

            try
            {
                Debug.WriteLine("SmsReaderService: User initiated SMS import.");

                if (!await _smsReaderService.CheckSmsPermissionAsync())
                {
                    var requestResult = await _smsReaderService.RequestSmsPermissionAsync();
                    if (!requestResult)
                    {
                        StatusMessage = "SMS permission is required to import new transactions.";
                        Debug.WriteLine("SmsReaderService: READ_SMS permission denied by user.");
                        return;
                    }
                }

                var smsBodies = await _smsReaderService.GetRecentSmsBodiesAsync();
                var newTransactions = await _smsImportService.ParseIncomingMessagesAsync(smsBodies);
                ImportedTransactions = new ObservableCollection<ImportedTransaction>(newTransactions);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup: NewTransactionsViewModel.LoadNewTransactionsAsync failed: {ex}");
                StatusMessage = "Failed to load new transactions.";
            }
            finally
            {
                IsBusy = false;
                Debug.WriteLine("Startup: NewTransactionsViewModel.LoadNewTransactionsAsync end");
            }
        }

        [RelayCommand]
        public async Task EditTransactionAsync(ImportedTransaction transaction)
        {
            if (transaction == null || IsBusy)
            {
                return;
            }

            try
            {
                var route = $"AddExpensePage?amount={transaction.Amount}&merchant={Uri.EscapeDataString(transaction.Merchant)}&date={Uri.EscapeDataString(transaction.Date.ToString("o"))}&category={Uri.EscapeDataString(transaction.SuggestedCategory)}&importId={transaction.Id}";
                await Shell.Current.GoToAsync(route);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup: NewTransactionsViewModel.EditTransactionAsync failed: {ex}");
                throw;
            }
        }

        [RelayCommand]
        public async Task AcceptTransactionAsync(ImportedTransaction transaction)
        {
            if (transaction == null || IsBusy)
            {
                return;
            }

            Debug.WriteLine("Startup: NewTransactionsViewModel.AcceptTransactionAsync begin");
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
                    Date = transaction.Date,
                    Note = transaction.SmsContent,
                    IsImported = true,
                    PaymentMethod = "UPI"
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
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup: NewTransactionsViewModel.AcceptTransactionAsync failed: {ex}");
                throw;
            }
            finally
            {
                IsBusy = false;
                Debug.WriteLine("Startup: NewTransactionsViewModel.AcceptTransactionAsync end");
            }
        }

        [RelayCommand]
        public async Task IgnoreTransactionAsync(ImportedTransaction transaction)
        {
            if (transaction == null)
            {
                return;
            }

            try
            {
                transaction.IsProcessed = true;
                await _repository.SaveImportedTransactionAsync(transaction);
                ImportedTransactions.Remove(transaction);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup: NewTransactionsViewModel.IgnoreTransactionAsync failed: {ex}");
                throw;
            }
        }
    }
}
