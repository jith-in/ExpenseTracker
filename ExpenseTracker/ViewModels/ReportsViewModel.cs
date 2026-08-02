using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Models;
using ExpenseTracker.Repositories;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace ExpenseTracker.ViewModels
{
    public partial class ReportsViewModel : BaseViewModel
    {
        private readonly IExpenseRepository _repository;
        private int _selectedYear;

        // Split Category Report Collections
        [ObservableProperty]
        private ObservableCollection<CategoryReportItem> debitCategoryItems = new();

        [ObservableProperty]
        private ObservableCollection<CategoryReportItem> creditCategoryItems = new();

        // Split Monthly Report Collections
        [ObservableProperty]
        private ObservableCollection<MonthlyReportItem> debitMonthlyItems = new();

        [ObservableProperty]
        private ObservableCollection<MonthlyReportItem> creditMonthlyItems = new();

        // Split Payment Method Report Collections
        [ObservableProperty]
        private ObservableCollection<PaymentMethodReportItem> debitPaymentMethodItems = new();

        [ObservableProperty]
        private ObservableCollection<PaymentMethodReportItem> creditPaymentMethodItems = new();

        [ObservableProperty]
        private ObservableCollection<int> availableYears = new();

        [ObservableProperty]
        private string statusMessage = string.Empty;

        public ReportsViewModel(IExpenseRepository repository)
        {
            Debug.WriteLine("Startup: ReportsViewModel ctor begin");
            _repository = repository;
            Title = "Reports";
            SelectedYear = DateTime.Today.Year;
            Debug.WriteLine("Startup: ReportsViewModel ctor end");
        }

        public int SelectedYear
        {
            get => _selectedYear;
            set
            {
                if (SetProperty(ref _selectedYear, value))
                {
                    _ = LoadReportsAsync();
                }
            }
        }

        [RelayCommand]
        public async Task LoadReportsAsync()
        {
            if (IsBusy) return;

            Debug.WriteLine("Startup: ReportsViewModel.LoadReportsAsync begin");
            IsBusy = true;
            StatusMessage = string.Empty;

            try
            {
                // Fetch all raw expense rows directly to ensure precise filtering by TransactionType
                var allExpenses = await _repository.GetExpensesAsync();
                var yearExpenses = allExpenses.Where(x => x.Date.Year == SelectedYear).ToList();

                // 🎯 1. CATEGORIES SPLIT BY TRANSACTION TYPE
                var debitCats = yearExpenses
                    .Where(x => string.Equals(x.TransactionType, "Debit", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(x.TransactionType))
                    .GroupBy(x => string.IsNullOrWhiteSpace(x.Category) ? "Others" : x.Category)
                    .Select(g => new CategoryReportItem { Category = g.Key, Total = g.Sum(x => Math.Abs(x.Amount)) })
                    .OrderByDescending(x => x.Total)
                    .ToList();

                var creditCats = yearExpenses
                    .Where(x => string.Equals(x.TransactionType, "Credit", StringComparison.OrdinalIgnoreCase))
                    .GroupBy(x => string.IsNullOrWhiteSpace(x.Category) ? "Others" : x.Category)
                    .Select(g => new CategoryReportItem { Category = g.Key, Total = g.Sum(x => Math.Abs(x.Amount)) })
                    .OrderByDescending(x => x.Total)
                    .ToList();

                DebitCategoryItems = new ObservableCollection<CategoryReportItem>(debitCats);
                CreditCategoryItems = new ObservableCollection<CategoryReportItem>(creditCats);

               
                // 🎯 2. MONTHLY WISE SPLIT BY TRANSACTION TYPE
                var debitMonths = yearExpenses
                    .Where(x => string.Equals(x.TransactionType, "Debit", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(x.TransactionType))
                    .GroupBy(x => x.Date.Month)
                    .Select(g => new MonthlyReportItem
                    {
                        Year = SelectedYear,
                        Month = g.Key, // 🟢 FIXED: Assign int g.Key directly (1..12) instead of month name string
                        Total = g.Sum(x => Math.Abs(x.Amount))
                    })
                    .OrderBy(x => x.Month)
                    .ToList();

                var creditMonths = yearExpenses
                    .Where(x => string.Equals(x.TransactionType, "Credit", StringComparison.OrdinalIgnoreCase))
                    .GroupBy(x => x.Date.Month)
                    .Select(g => new MonthlyReportItem
                    {
                        Year = SelectedYear,
                        Month = g.Key, // 🟢 FIXED: Assign int g.Key directly (1..12) instead of month name string
                        Total = g.Sum(x => Math.Abs(x.Amount))
                    })
                    .OrderBy(x => x.Month)
                    .ToList();

                DebitMonthlyItems = new ObservableCollection<MonthlyReportItem>(debitMonths);
                CreditMonthlyItems = new ObservableCollection<MonthlyReportItem>(creditMonths);

                // 🎯 3. PAYMENT METHOD WISE SPLIT BY TRANSACTION TYPE
                var debitMethods = yearExpenses
                    .Where(x => string.Equals(x.TransactionType, "Debit", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(x.TransactionType))
                    .GroupBy(x => string.IsNullOrWhiteSpace(x.PaymentMethod) ? "Net Banking" : x.PaymentMethod)
                    .Select(g => new PaymentMethodReportItem { PaymentMethod = g.Key, Total = g.Sum(x => Math.Abs(x.Amount)) })
                    .OrderByDescending(x => x.Total)
                    .ToList();

                var creditMethods = yearExpenses
                    .Where(x => string.Equals(x.TransactionType, "Credit", StringComparison.OrdinalIgnoreCase))
                    .GroupBy(x => string.IsNullOrWhiteSpace(x.PaymentMethod) ? "NEFT" : x.PaymentMethod)
                    .Select(g => new PaymentMethodReportItem { PaymentMethod = g.Key, Total = g.Sum(x => Math.Abs(x.Amount)) })
                    .OrderByDescending(x => x.Total)
                    .ToList();

                DebitPaymentMethodItems = new ObservableCollection<PaymentMethodReportItem>(debitMethods);
                CreditPaymentMethodItems = new ObservableCollection<PaymentMethodReportItem>(creditMethods);

                // Update timeline year selectors
                var years = await _repository.GetAvailableExpenseYearsAsync();
                AvailableYears = new ObservableCollection<int>(years);
                if (!AvailableYears.Contains(SelectedYear) && AvailableYears.Count > 0)
                {
                    SelectedYear = AvailableYears[0];
                }
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
                Debug.WriteLine($"Startup: ReportsViewModel.LoadReportsAsync failed: {ex}");
            }
            finally
            {
                IsBusy = false;
                Debug.WriteLine("Startup: ReportsViewModel.LoadReportsAsync end");
            }
        }

        [RelayCommand]
        public async Task ViewCategoryDetailsAsync(CategoryReportItem selectedCategory)
        {
            if (selectedCategory == null) return;

            var navigationParameters = new Dictionary<string, object>
            {
                { "categoryName", selectedCategory.Category }
            };

            await Shell.Current.GoToAsync("CategoryDetailsPage", navigationParameters);
        }

        [RelayCommand]
        public async Task ViewMonthlyDetailsAsync(MonthlyReportItem selectedMonth)
        {
            if (selectedMonth == null) return;

            var navigationParameters = new Dictionary<string, object>
            {
                { "month", selectedMonth.Month },
                { "year", selectedMonth.Year }
            };

            await Shell.Current.GoToAsync("MonthlyDetailsPage", navigationParameters);
        }

        [RelayCommand]
        public async Task ViewPaymentMethodDetailsAsync(PaymentMethodReportItem selectedMethod)
        {
            if (selectedMethod == null) return;

            var navigationParameters = new Dictionary<string, object>
            {
                { "paymentMethod", selectedMethod.PaymentMethod },
                { "year", SelectedYear }
            };

            await Shell.Current.GoToAsync("PaymentMethodDetailsPage", navigationParameters);
        }
    }
}