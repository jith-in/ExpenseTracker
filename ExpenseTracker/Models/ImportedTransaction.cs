using SQLite;

namespace ExpenseTracker.Models
{
    [Table("ImportedTransactions")]
    public class ImportedTransaction
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public decimal Amount { get; set; }

        public string Merchant { get; set; } = string.Empty;

        public DateTime Date { get; set; } = DateTime.UtcNow;

        public string TransactionType { get; set; } = string.Empty;

        public string ReferenceNumber { get; set; } = string.Empty;

        public string SmsContent { get; set; } = string.Empty;

        public string SuggestedCategory { get; set; } = string.Empty;

        public bool IsProcessed { get; set; }
    }
}
