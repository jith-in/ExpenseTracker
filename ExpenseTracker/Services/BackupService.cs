using ExpenseTracker.Interfaces;
using ExpenseTracker.Models;
using System.IO;
using System.Threading.Tasks;

namespace ExpenseTracker.Services
{
    public class BackupService
    {
        private readonly IFileService _fileService;
        private readonly IDatabaseService _databaseService;

        public BackupService(IFileService fileService, IDatabaseService databaseService)
        {
            _fileService = fileService;
            _databaseService = databaseService;
        }

        public Task<string> CreateBackupAsync()
        {
            var sourcePath = _databaseService.GetDatabasePath();
            var backupPath = _fileService.GetBackupFilePath($"backup_{DateTime.UtcNow:yyyyMMddHHmmss}.db3");

            File.Copy(sourcePath, backupPath, overwrite: true);
            return Task.FromResult(backupPath);
        }

        public Task<string> RestoreBackupAsync(string backupFilePath)
        {
            var sourcePath = _databaseService.GetDatabasePath();
            File.Copy(backupFilePath, sourcePath, overwrite: true);
            return Task.FromResult(sourcePath);
        }
    }
}
