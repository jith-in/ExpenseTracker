using ExpenseTracker.ViewModels;
using Microsoft.Maui.Controls;

namespace ExpenseTracker.Views
{
    public partial class MonthlyDetailsPage : ContentPage
    {
        public MonthlyDetailsPage(MonthlyDetailsViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}