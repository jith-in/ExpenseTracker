using ExpenseTracker.Models;

namespace ExpenseTracker.Services.Parsers
{
    public interface IBankParser
    {
        string BankName { get; }
        bool CanHandle(string body);
        ImportedTransaction Parse(string body, DateTime receivedDate);
    }
}