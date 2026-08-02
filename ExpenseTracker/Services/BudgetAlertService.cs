using ExpenseTracker.Interfaces;
using ExpenseTracker.Repositories;
using Microsoft.Maui.Controls;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ExpenseTracker.Services
{
    public interface IBudgetAlertService
    {
        Task CheckAndShowBudgetAlertAsync();
    }

    public class BudgetAlertService : IBudgetAlertService
    {
        private readonly IExpenseRepository _repository;

        public BudgetAlertService(IExpenseRepository repository)
        {
            _repository = repository;
        }

        public async Task CheckAndShowBudgetAlertAsync()
        {
            // 🎯 Reads the dynamically configured user budget
            decimal monthlyBudget = await _repository.GetMonthlyBudgetAsync();
            if (monthlyBudget <= 0) return; // Skip if no budget is configured

            var allExpenses = await _repository.GetExpensesAsync();
            var now = DateTime.Today;

            // Sum up all DEBIT transactions for the current month
            decimal currentMonthDebit = allExpenses
                .Where(x => x.Date.Year == now.Year &&
                            x.Date.Month == now.Month &&
                            (string.Equals(x.TransactionType, "Debit", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(x.TransactionType)))
                .Sum(x => Math.Abs(x.Amount));

            // Set alert threshold (e.g. 95% of budget or within ₹3,000 of reaching it)
            decimal warningThreshold = Math.Min(monthlyBudget - 3000.00m, monthlyBudget * 0.95m);

            if (currentMonthDebit >= monthlyBudget)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Shell.Current.DisplayAlert(
                        "⚠️ Budget Exceeded!",
                        $"You have spent ₹{currentMonthDebit:N2} this month, exceeding your monthly budget of ₹{monthlyBudget:N2}.",
                        "OK");
                });
            }
            else if (currentMonthDebit >= warningThreshold)
            {
                decimal remaining = monthlyBudget - currentMonthDebit;
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Shell.Current.DisplayAlert(
                        "⚠️ Budget Limit Approaching",
                        $"Your total spending for this month has reached ₹{currentMonthDebit:N2}.\n\nYou are approaching your budget of ₹{monthlyBudget:N2} (Only ₹{remaining:N2} remaining).",
                        "OK");
                });
            }
        }
    }
}