using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Models;
using ExpenseTracker.Repositories;
using Microsoft.Maui.Controls;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace ExpenseTracker.ViewModels
{
    public partial class EditExpenseViewModel : BaseViewModel
    {
        private readonly IExpenseRepository _repository;
        private Expense? _currentExpense;

        [ObservableProperty]
        private string amountText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<Category> categories = new();

        [ObservableProperty]
        private ObservableCollection<PaymentMethod> paymentMethods = new();

        [ObservableProperty]
        private Category? selectedCategory;

        [ObservableProperty]
        private PaymentMethod? selectedPaymentMethod;

        [ObservableProperty]
        private DateTime date = DateTime.Today;

        [ObservableProperty]
        private string note = string.Empty;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        public EditExpenseViewModel(IExpenseRepository repository)
        {
            Debug.WriteLine("Startup: EditExpenseViewModel ctor begin");
            _repository = repository;
            Title = "Edit Expense";
            Debug.WriteLine("Startup: EditExpenseViewModel ctor end");
        }

        public async Task LoadExpenseAsync(int expenseId)
        {
            if (IsBusy)
            {
                return;
            }

            Debug.WriteLine("Startup: EditExpenseViewModel.LoadExpenseAsync begin");
            IsBusy = true;
            try
            {
                await LoadOptionsAsync();
                _currentExpense = await _repository.GetExpenseByIdAsync(expenseId);
                if (_currentExpense == null)
                {
                    StatusMessage = "Expense not found.";
                    return;
                }

                AmountText = _currentExpense.Amount.ToString("F2");
                SelectedCategory = Categories.FirstOrDefault(c => c.Name == _currentExpense.Category) ?? Categories.FirstOrDefault();
                SelectedPaymentMethod = PaymentMethods.FirstOrDefault(p => p.Name == _currentExpense.PaymentMethod) ?? PaymentMethods.FirstOrDefault();
                Date = _currentExpense.Date;
                Note = _currentExpense.Note;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup: EditExpenseViewModel.LoadExpenseAsync failed: {ex}");
                throw;
            }
            finally
            {
                IsBusy = false;
                Debug.WriteLine("Startup: EditExpenseViewModel.LoadExpenseAsync end");
            }
        }

        public async Task LoadOptionsAsync()
        {
            if (Categories.Any() && PaymentMethods.Any())
            {
                return;
            }

            Debug.WriteLine("Startup: EditExpenseViewModel.LoadOptionsAsync begin");
            try
            {
                var categories = await _repository.GetCategoriesAsync();
                var paymentMethods = await _repository.GetPaymentMethodsAsync();
                Categories = new ObservableCollection<Category>(categories);
                PaymentMethods = new ObservableCollection<PaymentMethod>(paymentMethods);
                SelectedCategory = Categories.FirstOrDefault();
                SelectedPaymentMethod = PaymentMethods.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup: EditExpenseViewModel.LoadOptionsAsync failed: {ex}");
                throw;
            }
            finally
            {
                Debug.WriteLine("Startup: EditExpenseViewModel.LoadOptionsAsync end");
            }
        }

        [RelayCommand]
        public async Task SaveExpenseAsync()
        {
            if (IsBusy)
            {
                return;
            }

            if (_currentExpense == null)
            {
                StatusMessage = "No expense loaded.";
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

            Debug.WriteLine("Startup: EditExpenseViewModel.SaveExpenseAsync begin");
            IsBusy = true;
            try
            {
                _currentExpense.Amount = amount;
                _currentExpense.Category = SelectedCategory.Name;
                _currentExpense.PaymentMethod = SelectedPaymentMethod.Name;
                _currentExpense.Date = Date;
                _currentExpense.Note = Note?.Trim() ?? string.Empty;

                await _repository.SaveExpenseAsync(_currentExpense);
                StatusMessage = "Expense updated successfully.";
                await Shell.Current.GoToAsync("..", false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup: EditExpenseViewModel.SaveExpenseAsync failed: {ex}");
                throw;
            }
            finally
            {
                IsBusy = false;
                Debug.WriteLine("Startup: EditExpenseViewModel.SaveExpenseAsync end");
            }
        }
    }
}
