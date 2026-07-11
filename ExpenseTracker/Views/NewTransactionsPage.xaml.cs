using ExpenseTracker.ViewModels;
using System.Diagnostics;

namespace ExpenseTracker.Views
{
    public partial class NewTransactionsPage : ContentPage
    {
        public NewTransactionsPage()
        {
            Debug.WriteLine("Startup: NewTransactionsPage ctor begin");
            try
            {
                InitializeComponent();
                BindingContext = App.Services.GetRequiredService<NewTransactionsViewModel>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup: NewTransactionsPage ctor failed: {ex}");
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
            Debug.WriteLine("Startup: NewTransactionsPage ctor end");
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            try
            {
                await ((NewTransactionsViewModel)BindingContext).LoadNewTransactionsAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup: NewTransactionsPage OnAppearing failed: {ex}");
            }
        }
    }
}
