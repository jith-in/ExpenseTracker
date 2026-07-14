using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Helpers;
using ExpenseTracker.Models;
using ExpenseTracker.Repositories;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ExpenseTracker.ViewModels
{
    public partial class CategoryDetailsViewModel : BaseViewModel, IQueryAttributable
    {
        private readonly IExpenseRepository _repository;

        [ObservableProperty]
        private string categoryName = string.Empty;

        [ObservableProperty]
        private decimal totalDebit;

        [ObservableProperty]
        private decimal totalCredit;

        [ObservableProperty]
        private decimal netExpense;

        [ObservableProperty]
        private int transactionCount;

        public ObservableCollection<Grouping<DateTime, Expense>> GroupedTransactions { get; } = new();

        public CategoryDetailsViewModel(IExpenseRepository repository)
        {
            _repository = repository;
        }

        public async void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            if (query.TryGetValue("categoryName", out var name) && name is string catName)
            {
                CategoryName = catName;
                Title = catName;
                await LoadCategoryDataAsync();
            }
        }

        [RelayCommand]
        private async Task LoadCategoryDataAsync()
        {
            if (IsBusy) return;
            IsBusy = true;

            try
            {
                var expenses = await _repository.GetExpensesByCategoryAsync(CategoryName);

                // Summary processing calculations
                TransactionCount = expenses.Count;
                TotalDebit = expenses.Where(x => x.Amount > 0).Sum(x => x.Amount);
                TotalCredit = expenses.Where(x => x.Amount < 0).Sum(x => Math.Abs(x.Amount));
                NetExpense = TotalDebit - TotalCredit;

                // Group structural records uniformly by clear date boundaries
                var groups = expenses
                    .GroupBy(x => x.Date.Date)
                    .Select(g => new Grouping<DateTime, Expense>(g.Key, g))
                    .ToList();

                GroupedTransactions.Clear();
                foreach (var group in groups)
                {
                    GroupedTransactions.Add(group);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading category drilldown details: {ex}");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}