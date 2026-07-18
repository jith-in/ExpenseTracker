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

                // 1. Calculate Today's Pure Volume
                TodayTotal = expenses.Where(x => x.Date.Date == now.Date).Sum(x => x.Amount);

                // 2. Aggregate and segment "This Year" Data Rows
                var yearTransactions = expenses.Where(x => x.Date.Year == now.Year).ToList();

                ThisYearCredit = yearTransactions
                    .Where(x => string.Equals(x.TransactionType, "Credit", StringComparison.OrdinalIgnoreCase))
                    .Sum(x => x.Amount);

                ThisYearDebit = yearTransactions
                    .Where(x => string.Equals(x.TransactionType, "Debit", StringComparison.OrdinalIgnoreCase))
                    .Sum(x => x.Amount);

                YearTotal = ThisYearCredit - ThisYearDebit;

                // 3. Aggregate and segment "This Month" Data Rows
                var monthTransactions = yearTransactions.Where(x => x.Date.Month == now.Month).ToList();

                ThisMonthCredit = monthTransactions
                    .Where(x => string.Equals(x.TransactionType, "Credit", StringComparison.OrdinalIgnoreCase))
                    .Sum(x => x.Amount);

                ThisMonthDebit = monthTransactions
                    .Where(x => string.Equals(x.TransactionType, "Debit", StringComparison.OrdinalIgnoreCase))
                    .Sum(x => x.Amount);

                MonthTotal = ThisMonthCredit - ThisMonthDebit;

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

                // Keep the pending review indicator bar accurately updated
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

            // Toggles the visibility of the dashboard alert notification bar instantly
            HasPendingAiTransactions = pendingCount > 0;
        }

        [RelayCommand]
        public async Task ResolvePendingWithAiAsync()
        {
            if (IsBusy) return;

            // 1. Guard against dead network zones before hitting the API engine
            if (Connectivity.Current.NetworkAccess != Microsoft.Maui.Networking.NetworkAccess.Internet)
            {
                await Shell.Current.DisplayAlert("No Connection", "You need an active internet connection to verify items via the cloud.", "OK");
                return;
            }

            IsBusy = true;
            System.Diagnostics.Debug.WriteLine("[Dashboard AI] Initiating chunked batch classification pipeline...");

            try
            {
                var allExpenses = await _repository.GetExpensesAsync();
                var pendingItems = allExpenses.Where(x => x.ProcessingStatus == "PendingAiReview").ToList();

                if (!pendingItems.Any()) return;

                // 🎯 Strategy 2 Implementation: Partition backlog rows into chunks of 50 max
                var messageChunks = pendingItems.Chunk(50).ToList();

                for (int i = 0; i < messageChunks.Count; i++)
                {
                    var currentChunk = messageChunks[i].ToList();
                    System.Diagnostics.Debug.WriteLine($"[Dashboard AI] Processing chunk {i + 1} of {messageChunks.Count} ({currentChunk.Count} messages)...");

                    // Transmit the slice payload to Gemini REST engine via single POST request
                    var aiResponse = await _aiService.ParseBatchAsync(currentChunk);

                    if (aiResponse?.ProcessedTransactions != null)
                    {
                        // 📝 LOG TRACKING: Initialize counter for this specific batch partition run
                        int matchedCount = 0;

                        foreach (var aiItem in aiResponse.ProcessedTransactions)
                        {
                            // Match the cloud properties back to the local records within the current chunk boundaries
                            var localMatch = currentChunk.FirstOrDefault(x => x.Id == aiItem.Id);
                            if (localMatch != null)
                            {
                                localMatch.Amount = aiItem.Amount;
                                localMatch.Category = aiItem.Category;
                                localMatch.TransactionType = aiItem.TransactionType;
                                localMatch.Note = aiItem.Note;
                                localMatch.ProcessingStatus = "AiResolved"; // Flip processing state flag

                                // Save updates directly to the physical SQLite cache layer
                                await _repository.SaveExpenseAsync(localMatch);

                                // Increment tracking index loop status metric
                                matchedCount++;
                            }
                        }

                        // 📝 LOG INTEGRATION: Print out exactly how many matching entity items Gemini successfully parsed back
                        System.Diagnostics.Debug.WriteLine($"[Dashboard AI Batch] Successfully matched and updated {matchedCount} out of {currentChunk.Count} local database rows for chunk {i + 1}.");
                    }

                    // ⏳ Defensive Throttling: Sleep for 3 seconds between chunks to stay well under the RPM limits
                    if (i < messageChunks.Count - 1)
                    {
                        System.Diagnostics.Debug.WriteLine("[Dashboard AI] Resting for 3 seconds to protect free tier rate limits...");
                        await Task.Delay(3000);
                    }
                }

                // Force UI layout grids to recalculate metric values based on fresh allocations
                await LoadDashboardAsync();
                await Shell.Current.DisplayAlert("Success", "All pending transactions have been processed dynamically.", "OK");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Dashboard AI Failure]: {ex.Message}");
                await Shell.Current.DisplayAlert("Sync Failed", "Could not auto-categorize items right now. They will remain in queue.", "OK");
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

            // 🚀 Uses Shell to route to your NewTransactionsPage
            // Note: If you registered it as a tab route, you may need "///NewTransactionsPage"
            await Shell.Current.GoToAsync("NewTransactionsPage");
        }
    }

    public class CategorySummary
    {
        public string Category { get; set; } = string.Empty;
        public decimal Total { get; set; }
    }
}