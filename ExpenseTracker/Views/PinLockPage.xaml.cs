using ExpenseTracker.ViewModels;

namespace ExpenseTracker.Views
{
    public partial class PinLockPage : ContentPage
    {
        public PinLockPage(PinLockViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Give Android Activity context a brief moment to attach
            await Task.Delay(200);

            if (BindingContext is PinLockViewModel vm)
            {
                await vm.AuthenticateAsync();
            }
        }
    }
}