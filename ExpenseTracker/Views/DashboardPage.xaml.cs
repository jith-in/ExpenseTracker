using ExpenseTracker.ViewModels;
using System.Diagnostics;

namespace ExpenseTracker.Views
{
    public partial class DashboardPage : ContentPage
    {
        public DashboardPage()
        {
            Debug.WriteLine("Startup: DashboardPage ctor begin");
            try
            {
                InitializeComponent();
                BindingContext = App.Services.GetRequiredService<DashboardViewModel>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup: DashboardPage ctor failed: {ex}");
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
            Debug.WriteLine("Startup: DashboardPage ctor end");
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            try
            {
                await ((DashboardViewModel)BindingContext).LoadDashboardAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup: DashboardPage OnAppearing failed: {ex}");
            }
        }
    }
}
