using System.Diagnostics;

namespace ExpenseTracker
{
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; } = null!;

        public App(IServiceProvider services)
        {
            Debug.WriteLine("Startup: App ctor begin");
            Services = services;
            InitializeComponent();
            Debug.WriteLine("Startup: App ctor end");
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            Debug.WriteLine("Startup: App.CreateWindow begin");
            try
            {
                var window = new Window(new AppShell());
                Debug.WriteLine("Startup: App.CreateWindow end");
                return window;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup: App.CreateWindow failed: {ex}");
                return new Window(new ContentPage
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
                });
            }
        }
    }
}