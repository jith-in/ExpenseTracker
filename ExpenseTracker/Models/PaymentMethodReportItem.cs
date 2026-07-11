namespace ExpenseTracker.Models
{
    public class PaymentMethodReportItem
    {
        public string PaymentMethod { get; set; } = string.Empty;
        public decimal Total { get; set; }
    }
}
