using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Repositories;
using Microsoft.Maui.Controls;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ExpenseTracker.ViewModels
{
    public partial class ResetViewModel : BaseViewModel
    {
        private readonly IExpenseRepository _repository;

        public ResetViewModel(IExpenseRepository repository)
        {
            _repository = repository;
            Title = "Reset Application";
        }

        [RelayCommand]
        private async Task ResetApplicationDataAsync()
        {
            if (IsBusy) return;

           
            bool isConfirmed = await Shell.Current.DisplayAlert(
                "Confirm Total Reset",
                "Are you absolutely sure you want to clear all data? This will permanently delete all tracked expenses, imported transactions, and personalized categories. This action cannot be undone.",
                "Delete Everything",
                "Cancel");

            if (!isConfirmed) return;

       
            IsBusy = true;
            try
            {
                await _repository.ClearAllDataAsync();

                await Shell.Current.DisplayAlert("Data Cleared", "The application database has been completely reset.", "OK");

                // 3. Hot Route Redirect back to the starting view context state
                await Shell.Current.GoToAsync("//DashboardPage");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during full application reset operation: {ex}");
                await Shell.Current.DisplayAlert("Reset Failed", "An error occurred while wiping data tables.", "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}