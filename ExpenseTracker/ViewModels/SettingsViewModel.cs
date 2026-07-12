using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Repositories;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ExpenseTracker.ViewModels
{
    public partial class SettingsViewModel : BaseViewModel
    {
        private readonly IExpenseRepository _repository;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        public SettingsViewModel(IExpenseRepository repository)
        {
            Debug.WriteLine("Startup: SettingsViewModel ctor begin");
            _repository = repository;
            Title = "Settings";
            Debug.WriteLine("Startup: SettingsViewModel ctor end");
        }

        [RelayCommand]
        public async Task ClearSmsMessagesAsync()
        {
            Debug.WriteLine("Startup: SettingsViewModel.ClearSmsMessagesAsync begin");
            
            try
            {
                var result = await Application.Current!.MainPage!.DisplayAlert(
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
                var result = await Application.Current!.MainPage!.DisplayAlert(
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
