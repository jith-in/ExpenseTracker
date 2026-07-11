using ExpenseTracker.ViewModels;
using System.Diagnostics;

namespace ExpenseTracker.Views
{
    public partial class ExpenseHistoryPage : ContentPage
    {
        public ExpenseHistoryPage()
        {
            Debug.WriteLine("Startup: ExpenseHistoryPage ctor begin");
            try
            {
                InitializeComponent();
                BindingContext = App.Services.GetRequiredService<ExpenseHistoryViewModel>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup: ExpenseHistoryPage ctor failed: {ex}");
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
            Debug.WriteLine("Startup: ExpenseHistoryPage ctor end");
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            try
            {
                await ((ExpenseHistoryViewModel)BindingContext).LoadExpensesAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup: ExpenseHistoryPage OnAppearing failed: {ex}");
            }
        }
    }
}
