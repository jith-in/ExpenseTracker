using ExpenseTracker.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ExpenseTracker.Repositories
{
    public interface IExpenseRepository
    {
        Task<List<Expense>> GetExpensesAsync();
        Task<List<Category>> GetCategoriesAsync();
        Task<List<PaymentMethod>> GetPaymentMethodsAsync();
        Task<List<ImportedTransaction>> GetImportedTransactionsAsync();
        Task<List<ImportedTransaction>> GetAllImportedTransactionsAsync();
        Task<MerchantCategoryMapping?> GetMerchantCategoryMappingAsync(string merchant);
        Task<int> SaveMerchantCategoryMappingAsync(MerchantCategoryMapping mapping);
        Task<int> SaveExpenseAsync(Expense expense);
        Task<int> DeleteExpenseAsync(Expense expense);
        Task<Expense?> GetExpenseByIdAsync(int id);
        Task<int> SaveImportedTransactionAsync(ImportedTransaction transaction);
        Task<int> MarkImportedTransactionProcessedAsync(ImportedTransaction transaction);
        Task<List<CategoryReportItem>> GetCategoryReportAsync();
        Task<List<MonthlyReportItem>> GetMonthlyReportAsync(int year);
        Task<List<PaymentMethodReportItem>> GetPaymentMethodReportAsync();
        Task<List<int>> GetAvailableExpenseYearsAsync();
        Task<int> DeleteAllImportedTransactionsAsync();
        Task<int> DeleteAllUnprocessedTransactionsAsync();
    
        Task<List<Expense>> GetExpensesByCategoryAsync(string categoryName);
        Task ClearAllDataAsync();
        Task BulkLogTransactionsAsync(List<Expense> expenses, List<ImportedTransaction> transactions);
    }
}
