using ExpenseTracker.Views;
using System.Diagnostics;

namespace ExpenseTracker
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            Debug.WriteLine("Startup: AppShell ctor begin");
            try
            {
                InitializeComponent();

                // Registered routes for application navigation
                Routing.RegisterRoute(nameof(EditExpensePage), typeof(EditExpensePage));
                Routing.RegisterRoute(nameof(CategoryDetailsPage), typeof(CategoryDetailsPage));
                Routing.RegisterRoute(nameof(ResetPage), typeof(ResetPage));
                Routing.RegisterRoute(nameof(Views.MonthlyDetailsPage), typeof(Views.MonthlyDetailsPage));
                Routing.RegisterRoute(nameof(Views.PaymentMethodDetailsPage), typeof(Views.PaymentMethodDetailsPage));
                Routing.RegisterRoute("NewTransactionsPage", typeof(Views.NewTransactionsPage));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup: AppShell ctor failed: {ex}");
                Items.Add(new ShellContent
                {
                    Content = new ContentPage
                    {
                        Content = new StackLayout
                        {
                            Padding = 20,
                            Children =
                            {
                                new Label { Text = "Startup failed", FontAttributes = FontAttributes.Bold, FontSize = 24 },
                                new Label { Text = ex.Message }
                            }
                        }
                    }
                });
            }
            Debug.WriteLine("Startup: AppShell ctor end");
        }
    }
}