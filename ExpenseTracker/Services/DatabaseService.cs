using ExpenseTracker.Interfaces;
using System.IO;
using Microsoft.Maui.Storage;

namespace ExpenseTracker.Services
{
    public class DatabaseService : IDatabaseService
    {
        private const string DatabaseFileName = "expenses.db3";

        public string GetDatabasePath()
        {
            return Path.Combine(FileSystem.AppDataDirectory, DatabaseFileName);
        }
    }
}
