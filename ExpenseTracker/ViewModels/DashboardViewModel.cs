using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Models;
using ExpenseTracker.Repositories;
using Microsoft.Maui.Dispatching;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace ExpenseTracker.ViewModels
{
    public partial class DashboardViewModel : BaseViewModel
    {
        private readonly IExpenseRepository _repository;
        private ObservableCollection<Expense> _recentExpenses = new();
        private ObservableCollection<CategorySummary> _topCategories = new();
        private decimal _todayTotal;
        private decimal _monthTotal;
        private decimal _yearTotal;
        private int _pendingImports;

        public DashboardViewModel(IExpenseRepository repository)
        {
            Debug.WriteLine("Startup: DashboardViewModel ctor begin");
            _repository = repository;
            Title = "Dashboard";
            RecentExpenses = new ObservableCollection<Expense>();
            TopCategories = new ObservableCollection<CategorySummary>();
            Debug.WriteLine("Startup: DashboardViewModel ctor end");
        }

        public ObservableCollection<Expense> RecentExpenses
        {
            get => _recentExpenses;
            set => SetProperty(ref _recentExpenses, value);
        }

        public ObservableCollection<CategorySummary> TopCategories
        {
            get => _topCategories;
            set => SetProperty(ref _topCategories, value);
        }

        public decimal TodayTotal
        {
            get => _todayTotal;
            set => SetProperty(ref _todayTotal, value);
        }

        public decimal MonthTotal
        {
            get => _monthTotal;
            set => SetProperty(ref _monthTotal, value);
        }

        public decimal YearTotal
        {
            get => _yearTotal;
            set => SetProperty(ref _yearTotal, value);
        }

        public int PendingImports
        {
            get => _pendingImports;
            set => SetProperty(ref _pendingImports, value);
        }

        [RelayCommand]
        public async Task LoadDashboardAsync()
        {
            if (IsBusy)
            {
                return;
            }

            Debug.WriteLine("Startup: DashboardViewModel.LoadDashboardAsync begin");
            IsBusy = true;

            try
            {
                var expenses = await _repository.GetExpensesAsync();
                var imported = await _repository.GetImportedTransactionsAsync();

                var now = DateTime.UtcNow;
                TodayTotal = expenses.Where(x => x.Date.Date == now.Date).Sum(x => x.Amount);
                MonthTotal = expenses.Where(x => x.Date.Year == now.Year && x.Date.Month == now.Month).Sum(x => x.Amount);
                YearTotal = expenses.Where(x => x.Date.Year == now.Year).Sum(x => x.Amount);
                PendingImports = imported.Count;

                RecentExpenses.Clear();
                foreach (var expense in expenses.Take(5))
                {
                    RecentExpenses.Add(expense);
                }

                TopCategories.Clear();
                foreach (var categoryGroup in expenses
                    .GroupBy(x => x.Category)
                    .OrderByDescending(g => g.Sum(x => x.Amount))
                    .Take(5))
                {
                    TopCategories.Add(new CategorySummary
                    {
                        Category = categoryGroup.Key,
                        Total = categoryGroup.Sum(x => x.Amount)
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup: DashboardViewModel.LoadDashboardAsync failed: {ex}");
                throw;
            }
            finally
            {
                IsBusy = false;
                Debug.WriteLine("Startup: DashboardViewModel.LoadDashboardAsync end");
            }
        }
    }

    public class CategorySummary
    {
        public string Category { get; set; } = string.Empty;
        public decimal Total { get; set; }
    }
}
