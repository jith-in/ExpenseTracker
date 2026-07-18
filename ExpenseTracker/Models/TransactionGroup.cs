using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ExpenseTracker.Models
{
    // 🎯 FIX: Inheriting from ObservableCollection enables child cards to animate out gracefully 
    public class TransactionGroup : ObservableCollection<ImportedTransaction>
    {
        public string Title { get; set; }
        public string HeaderColor { get; set; }

        public TransactionGroup(string title, string headerColor, List<ImportedTransaction> items) : base(items)
        {
            Title = title;
            HeaderColor = headerColor;
        }
    }
}