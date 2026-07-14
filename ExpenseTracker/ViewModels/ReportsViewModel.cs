using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Models;
using ExpenseTracker.Repositories;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ExpenseTracker.ViewModels
{
    public partial class ReportsViewModel : BaseViewModel
    {
        private readonly IExpenseRepository _repository;
        private int _selectedYear;

        [ObservableProperty]
        private ObservableCollection<CategoryReportItem> categoryReportItems = new();

        [ObservableProperty]
        private ObservableCollection<MonthlyReportItem> monthlyReportItems = new();

        [ObservableProperty]
        private ObservableCollection<PaymentMethodReportItem> paymentMethodReportItems = new();

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
                var categories = await _repository.GetCategoryReportAsync();
                var monthly = await _repository.GetMonthlyReportAsync(SelectedYear);
                var methods = await _repository.GetPaymentMethodReportAsync();

                CategoryReportItems = new ObservableCollection<CategoryReportItem>(categories);
                MonthlyReportItems = new ObservableCollection<MonthlyReportItem>(monthly);
                PaymentMethodReportItems = new ObservableCollection<PaymentMethodReportItem>(methods);

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

            // Executes shell pipeline navigation targeting registered configuration definitions
            await Shell.Current.GoToAsync("CategoryDetailsPage", navigationParameters);
        }
    }
}