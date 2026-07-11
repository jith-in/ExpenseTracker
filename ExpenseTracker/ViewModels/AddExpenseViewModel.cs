using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Models;
using ExpenseTracker.Repositories;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ExpenseTracker.ViewModels
{
    public partial class AddExpenseViewModel : BaseViewModel
    {
        private readonly IExpenseRepository _repository;
        private string _amountText = string.Empty;
        private ObservableCollection<Category> _categories = new();
        private ObservableCollection<PaymentMethod> _paymentMethods = new();
        private Category? _selectedCategory;
        private PaymentMethod? _selectedPaymentMethod;
        private DateTime _date = DateTime.Today;
        private string _note = string.Empty;
        private string _statusMessage = string.Empty;
        private int _pendingImportId;
        private string _pendingCategoryName = string.Empty;
        private string _pendingMerchantName = string.Empty;

        public AddExpenseViewModel(IExpenseRepository repository)
        {
            _repository = repository;
            Title = "Add Expense";
            SaveExpenseCommand = new AsyncRelayCommand(SaveExpenseAsync);
        }

        public string AmountText
        {
            get => _amountText;
            set => SetProperty(ref _amountText, value);
        }

        public ObservableCollection<Category> Categories
        {
            get => _categories;
            set => SetProperty(ref _categories, value);
        }

        public ObservableCollection<PaymentMethod> PaymentMethods
        {
            get => _paymentMethods;
            set => SetProperty(ref _paymentMethods, value);
        }

        public Category? SelectedCategory
        {
            get => _selectedCategory;
            set => SetProperty(ref _selectedCategory, value);
        }

        public PaymentMethod? SelectedPaymentMethod
        {
            get => _selectedPaymentMethod;
            set => SetProperty(ref _selectedPaymentMethod, value);
        }

        public DateTime Date
        {
            get => _date;
            set => SetProperty(ref _date, value);
        }

        public string Note
        {
            get => _note;
            set => SetProperty(ref _note, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public IAsyncRelayCommand SaveExpenseCommand { get; }

        public async Task LoadOptionsAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;

            try
            {
                var categories = await _repository.GetCategoriesAsync();
                var paymentMethods = await _repository.GetPaymentMethodsAsync();

                Categories = new ObservableCollection<Category>(categories);
                PaymentMethods = new ObservableCollection<PaymentMethod>(paymentMethods);

                if (!string.IsNullOrWhiteSpace(_pendingCategoryName))
                {
                    SelectedCategory = Categories.FirstOrDefault(c => c.Name == _pendingCategoryName) ?? Categories.FirstOrDefault();
                    _pendingCategoryName = string.Empty;
                }
                else
                {
                    SelectedCategory = Categories.FirstOrDefault();
                }

                if (!string.IsNullOrWhiteSpace(_pendingMerchantName) && string.IsNullOrWhiteSpace(Note))
                {
                    Note = _pendingMerchantName;
                }

                SelectedPaymentMethod = PaymentMethods.FirstOrDefault();
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("amount", out var amount))
            {
                AmountText = amount.ToString() ?? string.Empty;
            }

            if (query.TryGetValue("merchant", out var merchant))
            {
                _pendingMerchantName = merchant.ToString() ?? string.Empty;
                Note = _pendingMerchantName;
            }

            if (query.TryGetValue("date", out var date) && DateTime.TryParse(date.ToString(), out var parsedDate))
            {
                Date = parsedDate;
            }

            if (query.TryGetValue("category", out var category))
            {
                _pendingCategoryName = category?.ToString() ?? string.Empty;
            }

            if (query.TryGetValue("importId", out var importId) && int.TryParse(importId.ToString(), out var id))
            {
                _pendingImportId = id;
            }
        }

        private async Task SaveExpenseAsync()
        {
            if (IsBusy)
            {
                return;
            }

            if (!decimal.TryParse(AmountText?.Replace(",", string.Empty), out var amount) || amount <= 0)
            {
                StatusMessage = "Please enter a valid amount.";
                return;
            }

            if (SelectedCategory == null)
            {
                StatusMessage = "Please choose a category.";
                return;
            }

            if (SelectedPaymentMethod == null)
            {
                StatusMessage = "Please choose a payment method.";
                return;
            }

            IsBusy = true;

            try
            {
                var expense = new Expense
                {
                    Amount = amount,
                    Category = SelectedCategory.Name,
                    PaymentMethod = SelectedPaymentMethod.Name,
                    Date = Date,
                    Note = Note?.Trim() ?? string.Empty,
                    IsImported = _pendingImportId > 0,
                };

                await _repository.SaveExpenseAsync(expense);

                if (_pendingImportId > 0)
                {
                    // Update merchant learning
                    await _repository.SaveMerchantCategoryMappingAsync(new MerchantCategoryMapping
                    {
                        Merchant = Note?.Trim() ?? string.Empty,
                        Category = SelectedCategory.Name
                    });

                    // Mark as processed
                    var imports = await _repository.GetImportedTransactionsAsync();
                    var match = imports.FirstOrDefault(x => x.Id == _pendingImportId);
                    if (match != null)
                    {
                        await _repository.MarkImportedTransactionProcessedAsync(match);
                    }
                }

                StatusMessage = "Expense saved successfully.";

                if (_pendingImportId > 0)
                {
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    AmountText = string.Empty;
                    Note = string.Empty;
                    Date = DateTime.Today;
                    SelectedCategory = Categories.FirstOrDefault();
                    SelectedPaymentMethod = PaymentMethods.FirstOrDefault();
                    _pendingImportId = 0;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
