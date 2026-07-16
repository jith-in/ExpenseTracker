using ExpenseTracker.Interfaces;
using ExpenseTracker.Models;
using SQLite;
using System;
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

        public async Task<List<Expense>> GetExpensesAsync()
        {
            await EnsureInitializedAsync();
            return await _db.Table<Expense>().OrderByDescending(x => x.Date).ToListAsync();
        }

        public async Task<Expense?> GetExpenseByIdAsync(int id)
        {
            await EnsureInitializedAsync();
            return await _db.FindAsync<Expense>(id).ConfigureAwait(false);
        }

        public async Task<int> SaveExpenseAsync(Expense expense)
        {
            await EnsureInitializedAsync();
            if (expense.Id != 0)
            {
                return await _db.UpdateAsync(expense);
            }

            return await _db.InsertAsync(expense);
        }

        public async Task<int> DeleteExpenseAsync(Expense expense)
        {
            await EnsureInitializedAsync();
            return await _db.DeleteAsync(expense);
        }

        public async Task<List<Category>> GetCategoriesAsync()
        {
            await EnsureInitializedAsync();
            return await _db.Table<Category>().ToListAsync();
        }

        public async Task<List<PaymentMethod>> GetPaymentMethodsAsync()
        {
            await EnsureInitializedAsync();
            return await _db.Table<PaymentMethod>().ToListAsync();
        }

        public async Task<List<ImportedTransaction>> GetImportedTransactionsAsync()
        {
            await EnsureInitializedAsync();

            var transactions = await _db.Table<ImportedTransaction>()
                .Where(x => !x.IsProcessed)
                .ToListAsync()
                .ConfigureAwait(false);

            return transactions
                .OrderByDescending(x => x.TransactionDate ?? x.SmsReceivedDate)
                .ToList();
        }

        public async Task<List<ImportedTransaction>> GetAllImportedTransactionsAsync()
        {
            await EnsureInitializedAsync();

            var transactions = await _db.Table<ImportedTransaction>()
                .ToListAsync()
                .ConfigureAwait(false);

            return transactions
                .OrderByDescending(x => x.TransactionDate ?? x.SmsReceivedDate)
                .ToList();
        }

        public async Task<int> SaveImportedTransactionAsync(ImportedTransaction importedTransaction)
        {
            await EnsureInitializedAsync();
            if (importedTransaction.Id != 0)
            {
                return await _db.UpdateAsync(importedTransaction);
            }

            return await _db.InsertAsync(importedTransaction);
        }

        public async Task<int> MarkImportedTransactionProcessedAsync(ImportedTransaction importedTransaction)
        {
            await EnsureInitializedAsync();
            importedTransaction.IsProcessed = true;
            return await _db.UpdateAsync(importedTransaction);
        }

        public async Task<MerchantCategoryMapping?> GetMerchantCategoryMappingAsync(string merchant)
        {
            await EnsureInitializedAsync();
            var result = await _db.Table<MerchantCategoryMapping>()
                .Where(x => x.Merchant == merchant)
                .FirstOrDefaultAsync().ConfigureAwait(false);

            return result;
        }

        public async Task<int> SaveMerchantCategoryMappingAsync(MerchantCategoryMapping mapping)
        {
            await EnsureInitializedAsync();
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
            await EnsureInitializedAsync();
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
            await EnsureInitializedAsync();

            var startDate = new DateTime(year, 1, 1, 0, 0, 0);
            var endDate = new DateTime(year, 12, 31, 23, 59, 59, 999);

            var expenses = await _db.Table<Expense>()
                .Where(x => x.Date >= startDate && x.Date <= endDate)
                .ToListAsync()
                .ConfigureAwait(false);

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
            await EnsureInitializedAsync();
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
            await EnsureInitializedAsync();
            var expenses = await _db.Table<Expense>().ToListAsync().ConfigureAwait(false);
            return expenses
                .Select(x => x.Date.Year)
                .Distinct()
                .OrderByDescending(x => x)
                .ToList();
        }

        public async Task<int> DeleteAllImportedTransactionsAsync()
        {
            await EnsureInitializedAsync();
            return await _db.DeleteAllAsync<ImportedTransaction>().ConfigureAwait(false);
        }

        public async Task<int> DeleteAllUnprocessedTransactionsAsync()
        {
            await EnsureInitializedAsync();

            var unprocessedItems = await _db.Table<ImportedTransaction>()
                                            .Where(x => !x.IsProcessed)
                                            .ToListAsync()
                                            .ConfigureAwait(false);

            int deletedCount = 0;

            foreach (var transaction in unprocessedItems)
            {
                deletedCount += await _db.DeleteAsync(transaction).ConfigureAwait(false);
            }

            return deletedCount;
        }

        public async Task<List<Expense>> GetExpensesByCategoryAsync(string categoryName)
        {
            await EnsureInitializedAsync();

            return await _db.Table<Expense>()
                .Where(x => x.Category == categoryName)
                .OrderByDescending(x => x.Date)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task ClearAllDataAsync()
        {
            await EnsureInitializedAsync();

            await _db.DeleteAllAsync<Expense>().ConfigureAwait(false);
            await _db.DeleteAllAsync<ImportedTransaction>().ConfigureAwait(false);
            await _db.DeleteAllAsync<MerchantCategoryMapping>().ConfigureAwait(false);
        }

        
        public async Task BulkLogTransactionsAsync(List<Expense> expenses, List<ImportedTransaction> transactions)
        {
            await EnsureInitializedAsync();

            // Runs both tracking inserts and processing updates inside a single secure atomic block
            await _db.RunInTransactionAsync(connection =>
            {
                foreach (var expense in expenses)
                {
                    connection.Insert(expense);
                }

                foreach (var trans in transactions)
                {
                    connection.Update(trans);
                }
            }).ConfigureAwait(false);
        }
    }
}