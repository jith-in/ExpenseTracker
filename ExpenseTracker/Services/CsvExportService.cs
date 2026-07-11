using ExpenseTracker.Interfaces;
using ExpenseTracker.Models;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ExpenseTracker.Services
{
    public class CsvExportService
    {
        private readonly IFileService _fileService;

        public CsvExportService(IFileService fileService)
        {
            _fileService = fileService;
        }

        public Task<string> ExportExpensesAsync(IEnumerable<Expense> expenses)
        {
            var fileName = $"expenses_export_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
            var filePath = _fileService.GetExportFilePath(fileName);
            var csvLines = new List<string>
            {
                "Id,Amount,Category,PaymentMethod,Date,Note,IsImported,Merchant,TransactionType,ReferenceNumber"
            };

            foreach (var expense in expenses)
            {
                csvLines.Add($"{expense.Id},{expense.Amount},{Escape(expense.Category)},{Escape(expense.PaymentMethod)},{expense.Date:O},{Escape(expense.Note)},{expense.IsImported},{Escape(expense.Merchant)},{Escape(expense.TransactionType)},{Escape(expense.ReferenceNumber)}");
            }

            File.WriteAllLines(filePath, csvLines, Encoding.UTF8);
            return Task.FromResult(filePath);
        }

        private static string Escape(string value)
        {
            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                return '"' + value.Replace("\"", "\"\"") + '"';
            }

            return value;
        }
    }
}
