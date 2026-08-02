using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Plugin.Fingerprint;
using Plugin.Fingerprint.Abstractions;
using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace ExpenseTracker.ViewModels
{
    public partial class PinLockViewModel : BaseViewModel
    {
        [ObservableProperty]
        private string errorMessage = string.Empty;

        [RelayCommand]
        public async Task AuthenticateAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                var isAvailable = await CrossFingerprint.Current.IsAvailableAsync(allowAlternativeAuthentication: true);

                if (!isAvailable)
                {
                    // If device PIN or biometrics are not configured on phone, bypass lock screen
                    NavigateToAppShell();
                    return;
                }

                var request = new AuthenticationRequestConfiguration(
                    "Expense Tracker Locked",
                    "Authenticate using your phone PIN, Pattern, or Biometrics.")
                {
                    AllowAlternativeAuthentication = true // 👈 Fall back to Phone PIN / Pattern / Password
                };

                var result = await CrossFingerprint.Current.AuthenticateAsync(request);

                if (result.Authenticated)
                {
                    NavigateToAppShell();
                }
                else
                {
                    ErrorMessage = "Authentication failed. Tap 'Unlock Now' to retry.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Lock Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void NavigateToAppShell()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // 🎯 Modern .NET 9 root page transition
                if (Application.Current?.Windows.Count > 0)
                {
                    Application.Current.Windows[0].Page = new AppShell();
                }
            });
        }
    }
}