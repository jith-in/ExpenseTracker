using ExpenseTracker.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ExpenseTracker.Interfaces
{
    public interface ISmsImportService
    {
        Task<IEnumerable<ImportedTransaction>> ParseIncomingMessagesAsync(IEnumerable<SmsMessageData> messages);
        Task<bool> IsDuplicateImportAsync(ImportedTransaction transaction);
    }
}
