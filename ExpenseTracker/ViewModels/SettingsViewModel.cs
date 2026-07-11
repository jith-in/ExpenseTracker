using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Repositories;
using ExpenseTracker.Services;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ExpenseTracker.ViewModels
{
    public partial class SettingsViewModel : BaseViewModel
    {
        private readonly IExpenseRepository _repository;
        private readonly CsvExportService _csvExportService;
        private readonly BackupService _backupService;

        [ObservableProperty]
        private bool isDarkModeEnabled;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        public SettingsViewModel(IExpenseRepository repository, CsvExportService csvExportService, BackupService backupService)
        {
            Debug.WriteLine("Startup: SettingsViewModel ctor begin");
            _repository = repository;
            _csvExportService = csvExportService;
            _backupService = backupService;
            Title = "Settings";
            IsDarkModeEnabled = Application.Current?.RequestedTheme == AppTheme.Dark;
            Debug.WriteLine("Startup: SettingsViewModel ctor end");
        }

        [RelayCommand]
        public async Task ExportCsvAsync()
        {
            if (IsBusy)
            {
                return;
            }

            Debug.WriteLine("Startup: SettingsViewModel.ExportCsvAsync begin");
            IsBusy = true;
            try
            {
                var expenses = await _repository.GetExpensesAsync();
                var exportPath = await _csvExportService.ExportExpensesAsync(expenses);
                StatusMessage = $"Export saved to {exportPath}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup: SettingsViewModel.ExportCsvAsync failed: {ex}");
                throw;
            }
            finally
            {
                IsBusy = false;
                Debug.WriteLine("Startup: SettingsViewModel.ExportCsvAsync end");
            }
        }

        [RelayCommand]
        public async Task CreateBackupAsync()
        {
            if (IsBusy)
            {
                return;
            }

            Debug.WriteLine("Startup: SettingsViewModel.CreateBackupAsync begin");
            IsBusy = true;
            try
            {
                var backupPath = await _backupService.CreateBackupAsync();
                StatusMessage = $"Backup created at {backupPath}";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup: SettingsViewModel.CreateBackupAsync failed: {ex}");
                throw;
            }
            finally
            {
                IsBusy = false;
                Debug.WriteLine("Startup: SettingsViewModel.CreateBackupAsync end");
            }
        }

        [RelayCommand]
        public async Task RestoreBackupAsync()
        {
            if (IsBusy)
            {
                return;
            }

            Debug.WriteLine("Startup: SettingsViewModel.RestoreBackupAsync begin");
            try
            {
                var result = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Select SQLite backup file",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.Android, new[] { "application/x-sqlite3", "application/octet-stream" } },
                        { DevicePlatform.WinUI, new[] { ".db3", ".sqlite", ".sqlite3" } },
                        { DevicePlatform.MacCatalyst, new[] { ".db3", ".sqlite", ".sqlite3" } }
                    })
                });

                if (result?.FullPath == null)
                {
                    StatusMessage = "Restore cancelled.";
                    return;
                }

                var filePath = result.FullPath;
                IsBusy = true;
                try
                {
                    var restorePath = await _backupService.RestoreBackupAsync(filePath);
                    StatusMessage = $"Database restored from {restorePath}";
                }
                finally
                {
                    IsBusy = false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup: SettingsViewModel.RestoreBackupAsync failed: {ex}");
                throw;
            }
            finally
            {
                Debug.WriteLine("Startup: SettingsViewModel.RestoreBackupAsync end");
            }
        }

        partial void OnIsDarkModeEnabledChanged(bool value)
        {
            if (Application.Current != null)
            {
                Application.Current.UserAppTheme = value ? AppTheme.Dark : AppTheme.Light;
            }

            StatusMessage = value ? "Dark mode enabled." : "Light mode enabled.";
        }
    }
}
