using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Models;
using ExpenseTracker.Repositories;
using ExpenseTracker.Services;
using Microsoft.Maui.Devices;
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
        private readonly IAiService _aiService;

        private ObservableCollection<Expense> _recentExpenses = new();
        private ObservableCollection<CategorySummary> _topCategories = new();
        private decimal _todayTotal;
        private decimal _monthTotal;
        private decimal _yearTotal;
        private int _pendingImports;

        // Backing Fields for Credit/Debit Breakdowns
        private decimal _thisMonthCredit;
        private decimal _thisMonthDebit;
        private decimal _thisYearCredit;
        private decimal _thisYearDebit;

        // Backing Fields for UI Inline Expansion Panels
        private bool _isMonthBreakdownVisible;
        private bool _isYearBreakdownVisible;
        private bool _hasPendingAiTransactions;

        // Backing Fields for Dynamic Budget Tile
        private decimal _monthlyBudget;
        private decimal _remainingBudget;
        private bool _hasBudgetSet;

        public DashboardViewModel(IExpenseRepository repository, IAiService aiService)
        {
            Debug.WriteLine("Startup: DashboardViewModel ctor begin");
            _repository = repository;
            _aiService = aiService;

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

        // ================= CREDIT / DEBIT METRIC PROPERTIES =================

        public decimal ThisMonthCredit
        {
            get => _thisMonthCredit;
            set => SetProperty(ref _thisMonthCredit, value);
        }

        public decimal ThisMonthDebit
        {
            get => _thisMonthDebit;
            set => SetProperty(ref _thisMonthDebit, value);
        }

        public decimal ThisYearCredit
        {
            get => _thisYearCredit;
            set => SetProperty(ref _thisYearCredit, value);
        }

        public decimal ThisYearDebit
        {
            get => _thisYearDebit;
            set => SetProperty(ref _thisYearDebit, value);
        }

        // ================= BUDGET METRIC PROPERTIES =================

        public decimal MonthlyBudget
        {
            get => _monthlyBudget;
            set => SetProperty(ref _monthlyBudget, value);
        }

        public decimal RemainingBudget
        {
            get => _remainingBudget;
            set => SetProperty(ref _remainingBudget, value);
        }

        public bool HasBudgetSet
        {
            get => _hasBudgetSet;
            set => SetProperty(ref _hasBudgetSet, value);
        }

        // ================= INTERACTIVE VISIBILITY FLAGS =================

        public bool IsMonthBreakdownVisible
        {
            get => _isMonthBreakdownVisible;
            set => SetProperty(ref _isMonthBreakdownVisible, value);
        }

        public bool IsYearBreakdownVisible
        {
            get => _isYearBreakdownVisible;
            set => SetProperty(ref _isYearBreakdownVisible, value);
        }

        public bool HasPendingAiTransactions
        {
            get => _hasPendingAiTransactions;
            set => SetProperty(ref _hasPendingAiTransactions, value);
        }

        // ================= TOGGLE INTERACTION COMMANDS =================

        [RelayCommand]
        public void ToggleMonthBreakdown()
        {
            IsMonthBreakdownVisible = !IsMonthBreakdownVisible;
            Debug.WriteLine($"[Dashboard] Month breakdown toggle clicked. Visible = {IsMonthBreakdownVisible}");
        }

        [RelayCommand]
        public void ToggleYearBreakdown()
        {
            IsYearBreakdownVisible = !IsYearBreakdownVisible;
            Debug.WriteLine($"[Dashboard] Year breakdown toggle clicked. Visible = {IsYearBreakdownVisible}");
        }

        // ================= CORE DATA REFRESH LOGIC =================

        [RelayCommand]
        public async Task LoadDashboardAsync()
        {
            if (IsBusy) return;

            Debug.WriteLine("Startup: DashboardViewModel.LoadDashboardAsync begin");
            IsBusy = true;

            try
            {
                var expenses = await _repository.GetExpensesAsync();
                var imported = await _repository.GetImportedTransactionsAsync();
                var now = DateTime.Today;

                // 1. Calculate Today's Pure Volume using absolute metrics
                TodayTotal = expenses.Where(x => x.Date.Date == now.Date).Sum(x => Math.Abs(x.Amount));

                // 2. Aggregate and segment "This Year" Data Rows safely
                var yearTransactions = expenses.Where(x => x.Date.Year == now.Year).ToList();

                ThisYearCredit = yearTransactions
                    .Where(x => string.Equals(x.TransactionType, "Credit", StringComparison.OrdinalIgnoreCase))
                    .Sum(x => Math.Abs(x.Amount));

                ThisYearDebit = yearTransactions
                    .Where(x => string.Equals(x.TransactionType, "Debit", StringComparison.OrdinalIgnoreCase)
                             || string.IsNullOrWhiteSpace(x.TransactionType))
                    .Sum(x => Math.Abs(x.Amount));

                YearTotal = ThisYearCredit - ThisYearDebit; // Clean net balance

                // 🎯 3. Calculate rolling monthly statement period (20th to 20th window)
                DateTime monthStartDate = now.Day < 20
                    ? new DateTime(now.Year, now.Month, 20).AddMonths(-1)
                    : new DateTime(now.Year, now.Month, 20);
                DateTime monthEndDate = monthStartDate.AddMonths(1);

                // Filter transactions falling inside the active statement cycle
                var monthTransactions = expenses
                    .Where(x => x.Date >= monthStartDate && x.Date < monthEndDate)
                    .ToList();

                ThisMonthCredit = monthTransactions
                    .Where(x => string.Equals(x.TransactionType, "Credit", StringComparison.OrdinalIgnoreCase))
                    .Sum(x => Math.Abs(x.Amount));

                ThisMonthDebit = monthTransactions
                    .Where(x => string.Equals(x.TransactionType, "Debit", StringComparison.OrdinalIgnoreCase)
                             || string.IsNullOrWhiteSpace(x.TransactionType))
                    .Sum(x => Math.Abs(x.Amount));

                MonthTotal = ThisMonthDebit; // Set to total spending magnitude or net balance (ThisMonthCredit - ThisMonthDebit)

                // 4. Compute Remaining Budget Metrics
                MonthlyBudget = await _repository.GetMonthlyBudgetAsync();
                if (MonthlyBudget > 0)
                {
                    HasBudgetSet = true;
                    RemainingBudget = MonthlyBudget - ThisMonthDebit;
                }
                else
                {
                    HasBudgetSet = false;
                    RemainingBudget = 0;
                }

                PendingImports = imported.Count;

                RecentExpenses.Clear();
                foreach (var expense in expenses.Take(5))
                {
                    RecentExpenses.Add(expense);
                }

                TopCategories.Clear();
                foreach (var categoryGroup in expenses
                    .GroupBy(x => x.Category)
                    .OrderByDescending(g => g.Sum(x => Math.Abs(x.Amount)))
                    .Take(5))
                {
                    TopCategories.Add(new CategorySummary
                    {
                        Category = categoryGroup.Key,
                        Total = categoryGroup.Sum(x => Math.Abs(x.Amount))
                    });
                }

                await CheckPendingTransactionsAsync();
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

        public async Task CheckPendingTransactionsAsync()
        {
            var expenses = await _repository.GetExpensesAsync();
            int pendingCount = expenses.Count(x => x.ProcessingStatus == "PendingAiReview");

            HasPendingAiTransactions = pendingCount > 0;
        }

        [RelayCommand]
        public async Task ResolvePendingWithAiAsync()
        {
            if (IsBusy) return;

            if (Connectivity.Current.NetworkAccess != Microsoft.Maui.Networking.NetworkAccess.Internet)
            {
                await Shell.Current.DisplayAlert("No Connection", "You need an active internet connection to verify items via the cloud.", "OK");
                return;
            }

            IsBusy = true;
            Debug.WriteLine("[Dashboard AI] Initiating chunked batch classification pipeline...");

            try
            {
                var allExpenses = await _repository.GetExpensesAsync();
                var pendingItems = allExpenses.Where(x => x.ProcessingStatus == "PendingAiReview").ToList();

                if (!pendingItems.Any()) return;

                var messageChunks = pendingItems.Chunk(50).ToList();

                for (int i = 0; i < messageChunks.Count; i++)
                {
                    var currentChunk = messageChunks[i].ToList();
                    var aiResponse = await _aiService.ParseBatchAsync(currentChunk);

                    if (aiResponse?.ProcessedTransactions != null)
                    {
                        int matchedCount = 0;

                        foreach (var aiItem in aiResponse.ProcessedTransactions)
                        {
                            var localMatch = currentChunk.FirstOrDefault(x => x.Id == aiItem.Id);
                            if (localMatch != null)
                            {
                                localMatch.Amount = Math.Abs(aiItem.Amount ?? 0m);
                                localMatch.Category = aiItem.Category;
                                localMatch.TransactionType = aiItem.TransactionType;
                                localMatch.Note = aiItem.MerchantOrEntity;
                                localMatch.ProcessingStatus = "AiResolved";

                                await _repository.SaveExpenseAsync(localMatch);
                                matchedCount++;
                            }
                        }
                        Debug.WriteLine($"[Dashboard AI Batch] Updated {matchedCount} rows for chunk {i + 1}.");
                    }

                    if (i < messageChunks.Count - 1)
                    {
                        await Task.Delay(3000);
                    }
                }

                await LoadDashboardAsync();
                await Shell.Current.DisplayAlert("Success", "All pending transactions have been processed dynamically.", "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Dashboard AI Failure]: {ex.Message}");
                await Shell.Current.DisplayAlert("Sync Failed", "Could not auto-categorize items right now.", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task NavigateToNewTransactionsAsync()
        {
            Debug.WriteLine("[Dashboard] Navigating to New Transactions page...");
            await Shell.Current.GoToAsync("NewTransactionsPage");
        }
    }

    public class CategorySummary
    {
        public string Category { get; set; } = string.Empty;
        public decimal Total { get; set; }
    }
}