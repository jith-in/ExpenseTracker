using ExpenseTracker.Interfaces;
using Microsoft.Maui.Storage;
using System.IO;

namespace ExpenseTracker.Services
{
    public class FileService : IFileService
    {
        private const string BackupFolderName = "Backup";
        private const string ExportFolderName = "Export";

        public string GetBackupFilePath(string fileName)
        {
            var backupFolder = Path.Combine(FileSystem.AppDataDirectory, BackupFolderName);
            Directory.CreateDirectory(backupFolder);
            return Path.Combine(backupFolder, fileName);
        }

        public string GetExportFilePath(string fileName)
        {
            var exportFolder = Path.Combine(FileSystem.AppDataDirectory, ExportFolderName);
            Directory.CreateDirectory(exportFolder);
            return Path.Combine(exportFolder, fileName);
        }
    }
}
