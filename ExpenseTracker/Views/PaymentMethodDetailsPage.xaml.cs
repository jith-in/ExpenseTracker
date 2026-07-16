using ExpenseTracker.ViewModels;
using Microsoft.Maui.Controls;

namespace ExpenseTracker.Views
{
    public partial class PaymentMethodDetailsPage : ContentPage
    {
        public PaymentMethodDetailsPage(PaymentMethodDetailsViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}