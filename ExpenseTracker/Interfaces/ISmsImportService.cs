using ExpenseTracker.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ExpenseTracker.Interfaces
{
    public interface ISmsImportService
    {
        Task<IEnumerable<ImportedTransaction>> ParseIncomingMessagesAsync(IEnumerable<string> messageBodies);
        Task<bool> IsDuplicateImportAsync(ImportedTransaction transaction);
    }
}
