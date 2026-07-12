using ExpenseTracker.Database;
using ExpenseTracker.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ExpenseTracker.Repositories
{
    public class ExpenseRepository : IExpenseRepository
    {
        private readonly ExpenseDatabase _database;

        public ExpenseRepository(ExpenseDatabase database)
        {
            _database = database;
        }

        public Task<List<Expense>> GetExpensesAsync()
        {
            return _database.GetExpensesAsync();
        }

        public Task<List<Category>> GetCategoriesAsync()
        {
            return _database.GetCategoriesAsync();
        }

        public Task<List<PaymentMethod>> GetPaymentMethodsAsync()
        {
            return _database.GetPaymentMethodsAsync();
        }

        public Task<List<ImportedTransaction>> GetImportedTransactionsAsync()
        {
            return _database.GetImportedTransactionsAsync();
        }

        public Task<List<ImportedTransaction>> GetAllImportedTransactionsAsync()
        {
            return _database.GetAllImportedTransactionsAsync();
        }

        public Task<int> SaveExpenseAsync(Expense expense)
        {
            return _database.SaveExpenseAsync(expense);
        }

        public Task<int> DeleteExpenseAsync(Expense expense)
        {
            return _database.DeleteExpenseAsync(expense);
        }

        public Task<MerchantCategoryMapping?> GetMerchantCategoryMappingAsync(string merchant)
        {
            return _database.GetMerchantCategoryMappingAsync(merchant);
        }

        public Task<int> SaveMerchantCategoryMappingAsync(MerchantCategoryMapping mapping)
        {
            return _database.SaveMerchantCategoryMappingAsync(mapping);
        }

        public Task<int> SaveImportedTransactionAsync(ImportedTransaction transaction)
        {
            return _database.SaveImportedTransactionAsync(transaction);
        }

        public Task<int> MarkImportedTransactionProcessedAsync(ImportedTransaction transaction)
        {
            return _database.MarkImportedTransactionProcessedAsync(transaction);
        }

        public Task<Expense?> GetExpenseByIdAsync(int id)
        {
            return _database.GetExpenseByIdAsync(id);
        }

        public Task<List<CategoryReportItem>> GetCategoryReportAsync()
        {
            return _database.GetCategoryReportAsync();
        }

        public Task<List<MonthlyReportItem>> GetMonthlyReportAsync(int year)
        {
            return _database.GetMonthlyReportAsync(year);
        }

        public Task<List<PaymentMethodReportItem>> GetPaymentMethodReportAsync()
        {
            return _database.GetPaymentMethodReportAsync();
        }

        public Task<List<int>> GetAvailableExpenseYearsAsync()
        {
            return _database.GetAvailableExpenseYearsAsync();
        }
        public Task<int> DeleteAllImportedTransactionsAsync()
        {
         return _database.DeleteAllImportedTransactionsAsync();
        }

        public Task<int> DeleteAllUnprocessedTransactionsAsync()
        {
        return _database.DeleteAllUnprocessedTransactionsAsync();
        }
    }
}
