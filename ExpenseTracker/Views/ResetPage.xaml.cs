using ExpenseTracker.ViewModels;

namespace ExpenseTracker.Views
{
    public partial class ResetPage : ContentPage
    {
        public ResetPage(ResetViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}