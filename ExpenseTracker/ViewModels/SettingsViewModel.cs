using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Repositories;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Maui.Controls; // Ensure this namespace is present for AppTheme

namespace ExpenseTracker.ViewModels
{
    public partial class SettingsViewModel : BaseViewModel
    {
        private readonly IExpenseRepository _repository;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        // 1. Define the backing field for the switch binding
        [ObservableProperty]
        private bool _isDarkModeEnabled;

        public SettingsViewModel(IExpenseRepository repository)
        {
            Debug.WriteLine("Startup: SettingsViewModel ctor begin");
            _repository = repository;
            Title = "Settings";

            // 2. Sync the switch position with the current active theme on load
            var currentTheme = Application.Current!.UserAppTheme;
            if (currentTheme == AppTheme.Unspecified)
            {
                currentTheme = Application.Current.RequestedTheme;
            }
            _isDarkModeEnabled = currentTheme == AppTheme.Dark;

            Debug.WriteLine("Startup: SettingsViewModel ctor end");
        }

        // 3. This runs automatically whenever IsDarkModeEnabled changes
        partial void OnIsDarkModeEnabledChanged(bool value)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Application.Current!.UserAppTheme = value ? AppTheme.Dark : AppTheme.Light;
            });
        }

        [RelayCommand]
        public async Task ClearSmsMessagesAsync()
        {
            Debug.WriteLine("Startup: SettingsViewModel.ClearSmsMessagesAsync begin");

            try
            {
                var result = await Shell.Current.DisplayAlert(
                 "Clear SMS Messages",
                 "Are you sure you want to delete all imported SMS messages? This cannot be undone.",
                "Yes, Delete",
                "Cancel"
 );

                if (!result)
                {
                    Debug.WriteLine("User cancelled SMS deletion.");
                    return;
                }

                StatusMessage = "Clearing SMS messages...";
                var deletedCount = await _repository.DeleteAllImportedTransactionsAsync();
                StatusMessage = $"Deleted {deletedCount} SMS messages successfully.";
                Debug.WriteLine($"Deleted {deletedCount} imported transactions.");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error deleting messages: {ex.Message}";
                Debug.WriteLine($"Error clearing SMS messages: {ex}");
            }
            finally
            {
                Debug.WriteLine("Startup: SettingsViewModel.ClearSmsMessagesAsync end");
            }
        }

        [RelayCommand]
        public async Task ClearUnprocessedMessagesAsync()
        {
            Debug.WriteLine("Startup: SettingsViewModel.ClearUnprocessedMessagesAsync begin");

            try
            {
                var result = await Shell.Current.DisplayAlert(
                 "Clear Pending Messages",
                "Are you sure you want to delete all pending SMS messages?",
                "Yes, Delete",
                 "Cancel"
);

                if (!result)
                {
                    Debug.WriteLine("User cancelled unprocessed message deletion.");
                    return;
                }

                StatusMessage = "Clearing pending messages...";
                var deletedCount = await _repository.DeleteAllUnprocessedTransactionsAsync();
                StatusMessage = $"Deleted {deletedCount} pending messages successfully.";
                Debug.WriteLine($"Deleted {deletedCount} unprocessed transactions.");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error deleting messages: {ex.Message}";
                Debug.WriteLine($"Error clearing unprocessed messages: {ex}");
            }
            finally
            {
                Debug.WriteLine("Startup: SettingsViewModel.ClearUnprocessedMessagesAsync end");
            }
        }
    }
}