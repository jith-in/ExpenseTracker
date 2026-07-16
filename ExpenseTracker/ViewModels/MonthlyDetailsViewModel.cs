using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Models;
using ExpenseTracker.Repositories;
using Java.Time;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace ExpenseTracker.ViewModels
{
    [QueryProperty(nameof(Month), "month")]
    [QueryProperty(nameof(Year), "year")]
    public partial class MonthlyDetailsViewModel : BaseViewModel
    {
        private readonly IExpenseRepository _repository;

        [ObservableProperty]
        private int month;

        [ObservableProperty]
        private int year;

        [ObservableProperty]
        private string targetMonthName = string.Empty;

        [ObservableProperty]
        private ObservableCollection<Expense> monthlyExpenses = new();

        public MonthlyDetailsViewModel(IExpenseRepository repository)
        {
            _repository = repository;
        }

        [RelayCommand]
        public async Task LoadMonthlyTransactionsAsync()
        {
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                // Set the display header text using local localization definitions
                TargetMonthName = $"{DateTimeFormatInfo.CurrentInfo.GetMonthName(Month)} {Year}";

                // Fetch consolidated data rows from the repository layer
                var allExpenses = await _repository.GetExpensesAsync();

                // Filter down by specific month and year metrics in memory
                var filtered = allExpenses
                    .Where(x => x.Date.Year == Year && x.Date.Month == Month)
                    .ToList();

                MonthlyExpenses = new ObservableCollection<Expense>(filtered);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load monthly details: {ex}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // Trigger loading automatically once navigation arguments are received
        partial void OnMonthChanged(int value) => _ = LoadMonthlyTransactionsAsync();
        partial void OnYearChanged(int value) => _ = LoadMonthlyTransactionsAsync();
    }
}