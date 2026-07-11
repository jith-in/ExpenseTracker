using ExpenseTracker.ViewModels;
using Microsoft.Maui.Controls;
using System.Collections.Generic;
using System.Diagnostics;

namespace ExpenseTracker.Views
{
    public partial class EditExpensePage : ContentPage, IQueryAttributable
    {
        private readonly EditExpenseViewModel _viewModel = null!;

        public EditExpensePage()
        {
            Debug.WriteLine("Startup: EditExpensePage ctor begin");
            try
            {
                InitializeComponent();
                _viewModel = App.Services.GetRequiredService<EditExpenseViewModel>();
                BindingContext = _viewModel;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup: EditExpensePage ctor failed: {ex}");
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
            Debug.WriteLine("Startup: EditExpensePage ctor end");
        }

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            try
            {
                if (query.TryGetValue("expenseId", out var expenseIdValue) && int.TryParse(expenseIdValue?.ToString(), out var expenseId))
                {
                    _ = _viewModel.LoadExpenseAsync(expenseId);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup: EditExpensePage ApplyQueryAttributes failed: {ex}");
            }
        }
    }
}
