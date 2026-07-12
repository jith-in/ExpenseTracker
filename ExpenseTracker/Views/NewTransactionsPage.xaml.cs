using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Models;
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
            Debug.WriteLine("Startup: NewTransactionsPage OnAppearing called.");
            try
            {
                await ((NewTransactionsViewModel)BindingContext).LoadNewTransactionsAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup: NewTransactionsPage OnAppearing failed: {ex}");
            }
        }
        [RelayCommand]
        public async Task ViewFullMessageAsync(ImportedTransaction transaction)
        {
            if (transaction == null)
            {
                Debug.WriteLine("ViewFullMessage: Transaction was null.");
                return;
            }

            Debug.WriteLine($"ViewFullMessage: Displaying content for {transaction.Merchant}");
            await Shell.Current.DisplayAlert("Full Message", transaction.SmsContent, "Close");
        }
    }
}
