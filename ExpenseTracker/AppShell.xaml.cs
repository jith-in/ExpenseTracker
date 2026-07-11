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
                Routing.RegisterRoute(nameof(EditExpensePage), typeof(EditExpensePage));
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
