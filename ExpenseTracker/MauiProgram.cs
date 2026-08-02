using CommunityToolkit.Maui;
using ExpenseTracker.Database;
using ExpenseTracker.Interfaces;
using ExpenseTracker.Repositories;
using ExpenseTracker.Services;
using ExpenseTracker.ViewModels;
using ExpenseTracker.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

#if ANDROID
using Plugin.Fingerprint;
#endif

namespace ExpenseTracker
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            Debug.WriteLine("Startup: CreateMauiApp begin");

#if ANDROID
            // 🎯 Register the Android Activity resolver at startup to prevent "Resolver not set" errors
            CrossFingerprint.SetCurrentActivityResolver(() => Microsoft.Maui.ApplicationModel.Platform.CurrentActivity);
#endif

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // Services & Repositories
            builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
            builder.Services.AddSingleton<IFileService, FileService>();
            builder.Services.AddSingleton<ExpenseDatabase>();
            builder.Services.AddSingleton<IExpenseRepository, ExpenseRepository>();
            builder.Services.AddSingleton<ISmsImportService, SmsImportService>();
            builder.Services.AddSingleton<ISmsReaderService, SmsReaderService>();
            builder.Services.AddSingleton<CsvExportService>();
            builder.Services.AddSingleton<BackupService>();
            builder.Services.AddSingleton<HttpClient>();
            builder.Services.AddTransient<IAiService, AiService>();

            // ViewModels
            builder.Services.AddTransient<DashboardViewModel>();
            builder.Services.AddTransient<AddExpenseViewModel>();
            builder.Services.AddTransient<ExpenseHistoryViewModel>();
            builder.Services.AddTransient<EditExpenseViewModel>();
            builder.Services.AddTransient<NewTransactionsViewModel>();
            builder.Services.AddTransient<ReportsViewModel>();
            builder.Services.AddTransient<SettingsViewModel>();
            builder.Services.AddTransient<CategoryDetailsViewModel>();
            builder.Services.AddTransient<ResetViewModel>();
            builder.Services.AddTransient<ViewModels.MonthlyDetailsViewModel>();
            builder.Services.AddTransient<ViewModels.PaymentMethodDetailsViewModel>();

            // Views / Pages (Explicitly Registered for MAUI DI Routing)
            builder.Services.AddTransient<DashboardPage>();
            builder.Services.AddTransient<AddExpensePage>();
            builder.Services.AddTransient<ExpenseHistoryPage>();
            builder.Services.AddTransient<EditExpensePage>();
            builder.Services.AddTransient<NewTransactionsPage>();
            builder.Services.AddTransient<ReportsPage>();
            builder.Services.AddTransient<SettingsPage>();
            builder.Services.AddTransient<CategoryDetailsPage>();
            builder.Services.AddTransient<ResetPage>();
            builder.Services.AddTransient<Views.MonthlyDetailsPage>();
            builder.Services.AddTransient<Views.PaymentMethodDetailsPage>();
            builder.Services.AddSingleton<IBudgetAlertService, BudgetAlertService>();
            builder.Services.AddTransient<PinLockViewModel>();
            builder.Services.AddTransient<PinLockPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            var app = builder.Build();
            Debug.WriteLine("Startup: CreateMauiApp built app");

            var database = app.Services.GetRequiredService<ExpenseDatabase>();
            _ = InitializeDatabaseAsync(database);

            Debug.WriteLine("Startup: CreateMauiApp end");
            return app;
        }

        private static async Task InitializeDatabaseAsync(ExpenseDatabase database)
        {
            try
            {
                Debug.WriteLine("Startup: ExpenseDatabase initialization begin");
                await database.InitializeAsync().ConfigureAwait(false);
                Debug.WriteLine("Startup: ExpenseDatabase initialization end");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Startup: ExpenseDatabase initialization failed: {ex}");
            }
        }
    }
}