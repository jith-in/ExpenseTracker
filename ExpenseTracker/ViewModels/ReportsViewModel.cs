using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
            if (IsBusy)
            {
                return;
            }

            Debug.WriteLine("Startup: ReportsViewModel.LoadReportsAsync begin");
            IsBusy = true;
            StatusMessage = string.Empty;

            try
            {
                // Fetch consolidated data rows from the repository layer
                var categories = await _repository.GetCategoryReportAsync();
                var monthly = await _repository.GetMonthlyReportAsync(SelectedYear);
                var methods = await _repository.GetPaymentMethodReportAsync();

                // 1. Separate Category Items (Positive totals = Debit, Negative totals = Credit)
                DebitCategoryItems = new ObservableCollection<CategoryReportItem>(categories.Where(x => x.Total > 0));
                CreditCategoryItems = new ObservableCollection<CategoryReportItem>(categories.Where(x => x.Total < 0));

                // 2. Separate Monthly Items
                DebitMonthlyItems = new ObservableCollection<MonthlyReportItem>(monthly.Where(x => x.Total > 0));
                CreditMonthlyItems = new ObservableCollection<MonthlyReportItem>(monthly.Where(x => x.Total < 0));

                // 3. Separate Payment Method Items
                DebitPaymentMethodItems = new ObservableCollection<PaymentMethodReportItem>(methods.Where(x => x.Total > 0));
                CreditPaymentMethodItems = new ObservableCollection<PaymentMethodReportItem>(methods.Where(x => x.Total < 0));

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

            // Package the date filters to pass to the target transaction detail view
            var navigationParameters = new Dictionary<string, object>
    {
        { "month", selectedMonth.Month },
        { "year", selectedMonth.Year }
    };

            // Routes the navigation query targeting the registered monthly details container
            await Shell.Current.GoToAsync("MonthlyDetailsPage", navigationParameters);
        }

        [RelayCommand]
        public async Task ViewPaymentMethodDetailsAsync(PaymentMethodReportItem selectedMethod)
        {
            if (selectedMethod == null) return;

            // Package the payment method name and selected year filter for the details page
            var navigationParameters = new Dictionary<string, object>
    {
        { "paymentMethod", selectedMethod.PaymentMethod },
        { "year", SelectedYear }
    };

            // Route the navigation request to the payment method details container
            await Shell.Current.GoToAsync("PaymentMethodDetailsPage", navigationParameters);
        }

    }
}