namespace ExpenseTracker.Models
{
    public class MonthlyReportItem
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal Total { get; set; }

        public string MonthName => System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(Month);
    }
}
