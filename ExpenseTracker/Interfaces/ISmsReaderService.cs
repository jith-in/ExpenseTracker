using System.Collections.Generic;
using System.Threading.Tasks;
using ExpenseTracker.Models; // Ensure this is imported for SmsMessageData

namespace ExpenseTracker.Interfaces
{
    public interface ISmsReaderService
    {
        Task<bool> CheckSmsPermissionAsync();
        Task<bool> RequestSmsPermissionAsync();

        // Ensure this return type matches your service implementation
        Task<IEnumerable<SmsMessageData>> GetRecentSmsBodiesAsync();
    }
}