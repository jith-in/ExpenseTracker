using ExpenseTracker.ViewModels;
using System.Diagnostics;

namespace ExpenseTracker.Views
{
    public partial class SettingsPage : ContentPage
    {
        public SettingsPage()
        {
            Debug.WriteLine("Startup: SettingsPage ctor begin");
            try
            {
                InitializeComponent();
                BindingContext = App.Services.GetRequiredService<SettingsViewModel>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup: SettingsPage ctor failed: {ex}");
                Content = new StackLayout
                {
                    Padding = 20,
                    Children =
                    {
                        new Label { Text = "Startup failed", FontAttributes = FontAttributes.Bold, FontSize = 24 },
                        new Label { Text = ex.Message }
                    }
                };
            }
            Debug.WriteLine("Startup: SettingsPage ctor end");
        }
    }
}
