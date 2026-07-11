using System.Collections.Generic;
using System.Threading.Tasks;

namespace ExpenseTracker.Interfaces
{
    public interface ISmsReaderService
    {
        Task<bool> CheckSmsPermissionAsync();
        Task<bool> RequestSmsPermissionAsync();
        Task<IEnumerable<string>> GetRecentSmsBodiesAsync(int maxMessages = 100);
    }
}
