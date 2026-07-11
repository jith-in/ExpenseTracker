using ExpenseTracker.ViewModels;
using System.Diagnostics;

namespace ExpenseTracker.Views
{
    public partial class ReportsPage : ContentPage
    {
        public ReportsPage()
        {
            Debug.WriteLine("Startup: ReportsPage ctor begin");
            try
            {
                InitializeComponent();
                BindingContext = App.Services.GetRequiredService<ReportsViewModel>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup: ReportsPage ctor failed: {ex}");
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
            Debug.WriteLine("Startup: ReportsPage ctor end");
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            try
            {
                await ((ReportsViewModel)BindingContext).LoadReportsAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup: ReportsPage OnAppearing failed: {ex}");
            }
        }
    }
}
