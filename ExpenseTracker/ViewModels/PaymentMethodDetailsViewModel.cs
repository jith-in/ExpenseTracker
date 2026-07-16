using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Models;
using ExpenseTracker.Repositories;
using Java.Time;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ExpenseTracker.ViewModels
{
    [QueryProperty(nameof(PaymentMethod), "paymentMethod")]
    [QueryProperty(nameof(Year), "year")]
    public partial class PaymentMethodDetailsViewModel : BaseViewModel
    {
        private readonly IExpenseRepository _repository;

        [ObservableProperty]
        private string paymentMethod = string.Empty;

        [ObservableProperty]
        private int year;

        [ObservableProperty]
        private string targetMethodName = string.Empty;

        [ObservableProperty]
        private ObservableCollection<Expense> methodExpenses = new();

        public PaymentMethodDetailsViewModel(IExpenseRepository repository)
        {
            _repository = repository;
        }

        [RelayCommand]
        public async Task LoadMethodTransactionsAsync()
        {
            if (IsBusy || string.IsNullOrEmpty(PaymentMethod)) return;
            IsBusy = true;

            try
            {
                TargetMethodName = $"{PaymentMethod} ({Year})";

                // Fetch data rows from repository
                var allExpenses = await _repository.GetExpensesAsync();

                // Filter down by year and specific payment method metrics
                var filtered = allExpenses
                    .Where(x => x.Date.Year == Year &&
                                string.Equals(x.PaymentMethod, PaymentMethod, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(x => x.Date)
                    .ToList();

                MethodExpenses = new ObservableCollection<Expense>(filtered);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load payment method details: {ex}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // Auto-refresh layout list entries as soon as parameters are received
        partial void OnPaymentMethodChanged(string value) => _ = LoadMethodTransactionsAsync();
        partial void OnYearChanged(int value) => _ = LoadMethodTransactionsAsync();
    }
}