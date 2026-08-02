using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Models;
using ExpenseTracker.Repositories;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace ExpenseTracker.ViewModels
{
    public partial class ReportsViewModel : BaseViewModel
    {
        private readonly IExpenseRepository _repository;

        [ObservableProperty]
        private string periodLabel = string.Empty;

        [ObservableProperty]
        private decimal totalExpenses;

        [ObservableProperty]
        private decimal totalIncome;

        [ObservableProperty]
        private ObservableCollection<Expense> expenseTransactions = new();

        [ObservableProperty]
        private ObservableCollection<Expense> incomeTransactions = new();

        [ObservableProperty]
        private bool isExpensesListVisible;

        [ObservableProperty]
        private bool isIncomeListVisible;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        public ReportsViewModel(IExpenseRepository repository)
        {
            _repository = repository;
            Title = "Period Report";
        }

        [RelayCommand]
        public async Task LoadReportsAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            StatusMessage = string.Empty;

            try
            {
                var now = DateTime.Today;

                // Compute rolling 20th to 20th statement cycle dates
                DateTime startDate = now.Day < 20
                    ? new DateTime(now.Year, now.Month, 20).AddMonths(-1)
                    : new DateTime(now.Year, now.Month, 20);
                DateTime endDate = startDate.AddMonths(1);

                PeriodLabel = $"Period: {startDate:dd MMM yyyy} – {endDate.AddDays(-1):dd MMM yyyy}";

                var allExpenses = await _repository.GetExpensesAsync();

                // Filter transactions falling within active period
                var periodItems = allExpenses
                    .Where(x => x.Date >= startDate && x.Date < endDate)
                    .OrderByDescending(x => x.Date)
                    .ToList();

                // Separate Debits and Credits
                var debits = periodItems
                    .Where(x => string.Equals(x.TransactionType, "Debit", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(x.TransactionType))
                    .ToList();

                var credits = periodItems
                    .Where(x => string.Equals(x.TransactionType, "Credit", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                ExpenseTransactions = new ObservableCollection<Expense>(debits);
                IncomeTransactions = new ObservableCollection<Expense>(credits);

                TotalExpenses = debits.Sum(x => Math.Abs(x.Amount));
                TotalIncome = credits.Sum(x => Math.Abs(x.Amount));
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
                Debug.WriteLine($"Error loading period report: {ex}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void ToggleExpenses()
        {
            IsExpensesListVisible = !IsExpensesListVisible;
        }

        [RelayCommand]
        public void ToggleIncome()
        {
            IsIncomeListVisible = !IsIncomeListVisible;
        }
    }
}