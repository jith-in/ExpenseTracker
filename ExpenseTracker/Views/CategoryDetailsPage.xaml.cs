using System;
using Microsoft.Maui.Controls;
using ExpenseTracker.ViewModels; // Resolves the missing namespace error

namespace ExpenseTracker.Views
{
    public partial class CategoryDetailsPage : ContentPage
    {
        // This must be the ONLY constructor defined in this file
        public CategoryDetailsPage(CategoryDetailsViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}