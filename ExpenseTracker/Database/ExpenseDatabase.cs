using ExpenseTracker.Interfaces;
using ExpenseTracker.Models;
using SQLite;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;

namespace ExpenseTracker.Database
{
    public class ExpenseDatabase
    {
        private readonly SQLiteAsyncConnection _db;
        private readonly Task _initializationTask;

        public ExpenseDatabase(IDatabaseService databaseService)
        {
            _db = new SQLiteAsyncConnection(databaseService.GetDatabasePath());
            _initializationTask = CreateTablesAsync();
        }

        public Task InitializeAsync()
        {
            return _initializationTask;
        }

        private async Task EnsureInitializedAsync()
        {
            try
            {
                await _initializationTask.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup: ExpenseDatabase EnsureInitializedAsync failed: {ex}");
                throw;
            }
        }

        private async Task CreateTablesAsync()
        {
            await _db.CreateTableAsync<Expense>().ConfigureAwait(false);
            await _db.CreateTableAsync<Category>().ConfigureAwait(false);
            await _db.CreateTableAsync<PaymentMethod>().ConfigureAwait(false);
            await _db.CreateTableAsync<ImportedTransaction>().ConfigureAwait(false);
            await _db.CreateTableAsync<MerchantCategoryMapping>().ConfigureAwait(false);
            await SeedDefaultsAsync().ConfigureAwait(false);
        }

        private async Task SeedDefaultsAsync()
        {
            if (await _db.Table<Category>().CountAsync().ConfigureAwait(false) == 0)
            {
                var categories = new List<Category>
                {
                    new() { Name = "Food" },
                    new() { Name = "Fuel" },
                    new() { Name = "Shopping" },
                    new() { Name = "Medical" },
                    new() { Name = "Bills" },
                    new() { Name = "Child" },
                    new() { Name = "Investment" },
                    new() { Name = "Travel" },
                    new() { Name = "Entertainment" },
                    new() { Name = "Groceries" },
                    new() { Name = "Others" },
                };

                await _db.InsertAllAsync(categories).ConfigureAwait(false);
            }

            if (await _db.Table<PaymentMethod>().CountAsync().ConfigureAwait(false) == 0)
            {
                var paymentMethods = new List<PaymentMethod>
                {
                    new() { Name = "Cash" },
                    new() { Name = "UPI" },
                    new() { Name = "Credit Card" },
                    new() { Name = "Debit Card" },
                    new() { Name = "Net Banking" },
                };

                await _db.InsertAllAsync(paymentMethods).ConfigureAwait(false);
            }
        }

        public Task<List<Expense>> GetExpensesAsync()
        {
            return _db.Table<Expense>().OrderByDescending(x => x.Date).ToListAsync();
        }

        public async Task<Expense?> GetExpenseByIdAsync(int id)
        {
            return await _db.FindAsync<Expense>(id).ConfigureAwait(false);
        }

        public Task<int> SaveExpenseAsync(Expense expense)
        {
            if (expense.Id != 0)
            {
                return _db.UpdateAsync(expense);
            }

            return _db.InsertAsync(expense);
        }

        public Task<int> DeleteExpenseAsync(Expense expense)
        {
            return _db.DeleteAsync(expense);
        }

        public Task<List<Category>> GetCategoriesAsync()
        {
            return _db.Table<Category>().ToListAsync();
        }

        public Task<List<PaymentMethod>> GetPaymentMethodsAsync()
        {
            return _db.Table<PaymentMethod>().ToListAsync();
        }

        public Task<List<ImportedTransaction>> GetImportedTransactionsAsync()
        {
            return _db.Table<ImportedTransaction>().Where(x => !x.IsProcessed).OrderByDescending(x => x.Date).ToListAsync();
        }

        public Task<List<ImportedTransaction>> GetAllImportedTransactionsAsync()
        {
            return _db.Table<ImportedTransaction>().OrderByDescending(x => x.Date).ToListAsync();
        }

        public Task<int> SaveImportedTransactionAsync(ImportedTransaction importedTransaction)
        {
            if (importedTransaction.Id != 0)
            {
                return _db.UpdateAsync(importedTransaction);
            }

            return _db.InsertAsync(importedTransaction);
        }

        public Task<int> MarkImportedTransactionProcessedAsync(ImportedTransaction importedTransaction)
        {
            importedTransaction.IsProcessed = true;
            return _db.UpdateAsync(importedTransaction);
        }

        public async Task<MerchantCategoryMapping?> GetMerchantCategoryMappingAsync(string merchant)
        {
            var result = await _db.Table<MerchantCategoryMapping>()
                .Where(x => x.Merchant == merchant)
                .FirstOrDefaultAsync().ConfigureAwait(false);

            return result;
        }

        public async Task<int> SaveMerchantCategoryMappingAsync(MerchantCategoryMapping mapping)
        {
            var merchant = mapping.Merchant?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(merchant))
            {
                return 0;
            }

            mapping.Merchant = merchant;
            var existing = await GetMerchantCategoryMappingAsync(merchant).ConfigureAwait(false);
            if (existing != null)
            {
                existing.Category = mapping.Category;
                return await _db.UpdateAsync(existing).ConfigureAwait(false);
            }

            return await _db.InsertAsync(mapping).ConfigureAwait(false);
        }

        public async Task<List<CategoryReportItem>> GetCategoryReportAsync()
        {
            var expenses = await _db.Table<Expense>().ToListAsync().ConfigureAwait(false);
            return expenses
                .GroupBy(x => x.Category)
                .Select(g => new CategoryReportItem
                {
                    Category = g.Key,
                    Total = g.Sum(x => x.Amount)
                })
                .OrderByDescending(x => x.Total)
                .ToList();
        }

        public async Task<List<MonthlyReportItem>> GetMonthlyReportAsync(int year)
        {
            var expenses = await _db.Table<Expense>()
                .Where(x => x.Date.Year == year)
                .ToListAsync().ConfigureAwait(false);

            return expenses
                .GroupBy(x => x.Date.Month)
                .Select(g => new MonthlyReportItem
                {
                    Year = year,
                    Month = g.Key,
                    Total = g.Sum(x => x.Amount)
                })
                .OrderBy(x => x.Month)
                .ToList();
        }

        public async Task<List<PaymentMethodReportItem>> GetPaymentMethodReportAsync()
        {
            var expenses = await _db.Table<Expense>().ToListAsync().ConfigureAwait(false);
            return expenses
                .GroupBy(x => x.PaymentMethod)
                .Select(g => new PaymentMethodReportItem
                {
                    PaymentMethod = g.Key,
                    Total = g.Sum(x => x.Amount)
                })
                .OrderByDescending(x => x.Total)
                .ToList();
        }

        public async Task<List<int>> GetAvailableExpenseYearsAsync()
        {
            var expenses = await _db.Table<Expense>().ToListAsync().ConfigureAwait(false);
            return expenses
                .Select(x => x.Date.Year)
                .Distinct()
                .OrderByDescending(x => x)
                .ToList();
        }
    }
}
