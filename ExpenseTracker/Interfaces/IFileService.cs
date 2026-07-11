namespace ExpenseTracker.Interfaces
{
    public interface IFileService
    {
        string GetBackupFilePath(string fileName);
        string GetExportFilePath(string fileName);
    }
}
