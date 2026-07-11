using ExpenseTracker.ViewModels;
using System.Collections.Generic;
using Microsoft.Maui.Controls;
using System.Diagnostics;

namespace ExpenseTracker.Views
{
    public partial class AddExpensePage : ContentPage, IQueryAttributable
    {
        private readonly AddExpenseViewModel _viewModel = null!;

        public AddExpensePage()
        {
            Debug.WriteLine("Startup: AddExpensePage ctor begin");
            try
            {
                InitializeComponent();
                _viewModel = App.Services.GetRequiredService<AddExpenseViewModel>();
                BindingContext = _viewModel;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup: AddExpensePage ctor failed: {ex}");
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
            Debug.WriteLine("Startup: AddExpensePage ctor end");
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            try
            {
                await _viewModel.LoadOptionsAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup: AddExpensePage OnAppearing failed: {ex}");
            }
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            try
            {
                _viewModel.ApplyQueryAttributes(query);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup: AddExpensePage ApplyQueryAttributes failed: {ex}");
            }
        }
    }
}
