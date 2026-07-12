using SQLite;
using System;

namespace ExpenseTracker.Models
{
    [Table("ImportedTransactions")]
    public class ImportedTransaction
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public decimal Amount { get; set; }
        public string Merchant { get; set; } = string.Empty;

        // 1. Android Metadata Date: Always accurate.
        public DateTime SmsReceivedDate { get; set; } = DateTime.Now;

        // 2. Transaction Date: Extracted from SMS text (Nullable).
        public DateTime? TransactionDate { get; set; }

        // Future-proofing fields
        public string BankName { get; set; } = string.Empty;
        public string SenderId { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public bool IsCredit { get; set; }
        public bool IsParsedSuccessfully { get; set; } = true;
        public string ParserUsed { get; set; } = string.Empty;

        public string TransactionType { get; set; } = string.Empty;
        public string ReferenceNumber { get; set; } = string.Empty;
        public string SmsContent { get; set; } = string.Empty;
        public string SuggestedCategory { get; set; } = string.Empty;
        public bool IsProcessed { get; set; }
        public string SuggestedPaymentMethod { get; set; } = "Net Banking";
        public int Confidence { get; set; } = 100;
        [Ignore] // Tell SQLite to ignore this field (it's computed)
        public DateTime Date
        {
            get => TransactionDate ?? SmsReceivedDate;
            set { /* You can choose to ignore this or assign it */ }
        }
    }
}