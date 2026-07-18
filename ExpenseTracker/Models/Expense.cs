using SQLite;

namespace ExpenseTracker.Models
{
    [Table("Expenses")]
    public class Expense
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public decimal Amount { get; set; }

        public string Category { get; set; } = string.Empty;

        public string PaymentMethod { get; set; } = string.Empty;

        public DateTime Date { get; set; } = DateTime.UtcNow;

        public string Note { get; set; } = string.Empty;

        public bool IsImported { get; set; }

        public string Merchant { get; set; } = string.Empty;

        public string TransactionType { get; set; } = string.Empty;

        public string ReferenceNumber { get; set; } = string.Empty;

        public string ProcessingStatus { get; set; } = "Confirmed";
    }
}
