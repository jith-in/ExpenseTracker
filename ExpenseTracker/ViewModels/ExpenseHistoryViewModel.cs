using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Models;
using ExpenseTracker.Repositories;
using Microsoft.Maui.Controls;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace ExpenseTracker.ViewModels
{
    public partial class ExpenseHistoryViewModel : BaseViewModel
    {
        private readonly IExpenseRepository _repository;

        [ObservableProperty]
        private ObservableCollection<Expense> expenses = new();

        [ObservableProperty]
        private ObservableCollection<Expense> filteredExpenses = new();

        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        private bool isRefreshing;

        public ExpenseHistoryViewModel(IExpenseRepository repository)
        {
            Debug.WriteLine("Startup: ExpenseHistoryViewModel ctor begin");
            _repository = repository;
            Title = "Expense History";
            Debug.WriteLine("Startup: ExpenseHistoryViewModel ctor end");
        }

        partial void OnSearchTextChanged(string value)
        {
            ApplySearchFilter();
        }

        [RelayCommand]
        public async Task LoadExpensesAsync()
        {
            if (IsBusy)
            {
                return;
            }

            Debug.WriteLine("Startup: ExpenseHistoryViewModel.LoadExpensesAsync begin");
            IsBusy = true;
            try
            {
                var expenses = await _repository.GetExpensesAsync();
                Expenses = new ObservableCollection<Expense>(expenses);
                ApplySearchFilter();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup: ExpenseHistoryViewModel.LoadExpensesAsync failed: {ex}");
                throw;
            }
            finally
            {
                IsBusy = false;
                Debug.WriteLine("Startup: ExpenseHistoryViewModel.LoadExpensesAsync end");
            }
        }

        [RelayCommand]
        public async Task RefreshAsync()
        {
            IsRefreshing = true;
            try
            {
                await LoadExpensesAsync();
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        [RelayCommand]
        public async Task DeleteExpenseAsync(Expense expense)
        {
            if (expense == null || IsBusy)
            {
                return;
            }

            Debug.WriteLine("Startup: ExpenseHistoryViewModel.DeleteExpenseAsync begin");
            IsBusy = true;
            try
            {
                await _repository.DeleteExpenseAsync(expense);
                await LoadExpensesAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup: ExpenseHistoryViewModel.DeleteExpenseAsync failed: {ex}");
                throw;
            }
            finally
            {
                IsBusy = false;
                Debug.WriteLine("Startup: ExpenseHistoryViewModel.DeleteExpenseAsync end");
            }
        }

        [RelayCommand]
        public async Task EditExpenseAsync(Expense expense)
        {
            if (expense == null)
            {
                return;
            }

            try
            {
                await Shell.Current.GoToAsync($"EditExpensePage?expenseId={expense.Id}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup: ExpenseHistoryViewModel.EditExpenseAsync failed: {ex}");
                throw;
            }
        }

        private void ApplySearchFilter()
        {
            var query = SearchText?.Trim().ToLowerInvariant() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(query))
            {
                FilteredExpenses = new ObservableCollection<Expense>(Expenses);
                return;
            }

            var filtered = Expenses
                .Where(expense =>
                    expense.Category.ToLowerInvariant().Contains(query) ||
                    expense.PaymentMethod.ToLowerInvariant().Contains(query) ||
                    expense.Note.ToLowerInvariant().Contains(query) ||
                    expense.Merchant.ToLowerInvariant().Contains(query) ||
                    expense.ReferenceNumber.ToLowerInvariant().Contains(query))
                .ToList();

            FilteredExpenses = new ObservableCollection<Expense>(filtered);
        }
    }
}
