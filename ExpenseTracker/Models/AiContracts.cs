using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ExpenseTracker.Models
{
    // ==========================================
    // Request Payload Envelope
    // ==========================================
    public class BatchExpenseRequest
    {
        [JsonPropertyName("transactions")]
        public List<SmsPayload> Transactions { get; set; } = new();
    }

    public class SmsPayload
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    // ==========================================
    // Response Payload Envelope (🎯 SYNCHRONIZED TO GEMINI SCHEMA)
    // ==========================================
    public class BatchExpenseResponse
    {
        [JsonPropertyName("processed_transactions")] // 🎯 Synchronized snake_case property
        public List<AiParsedTransaction> ProcessedTransactions { get; set; } = new();
    }

    public class AiParsedTransaction
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("is_transaction")] // 🌟 Handles spam/alert filtering checks
        public bool IsTransaction { get; set; }

        // 🟢 FIX CS8618: Declared as nullable string? initialized with string.Empty
        [JsonPropertyName("transaction_id")] // 🌟 Captures UPI Ref, IMPS Ref, or UTR sequence hashes
        public string? TransactionId { get; set; } = string.Empty;

        // 🟢 FIX CS8618: Declared as nullable string? initialized with string.Empty
        [JsonPropertyName("account_masked")] // 🌟 Extracts card/bank sequence tracks (e.g. XX7007)
        public string? AccountMasked { get; set; } = string.Empty;

        [JsonPropertyName("amount")]
        public decimal? Amount { get; set; } // 🎯 Crucial: Nullable to safely absorb spam records with null quantities

        [JsonPropertyName("currency")] // 🌟 Standardizes currency tracking strings
        public string Currency { get; set; } = "INR";

        [JsonPropertyName("transaction_type")] // 🎯 Direction type: Debit / Credit
        public string TransactionType { get; set; } = "Debit";

        [JsonPropertyName("merchant_or_entity")] // 🎯 Replaces old generic 'note' tag to map cleanly to models
        public string MerchantOrEntity { get; set; } = string.Empty;

        [JsonPropertyName("category")]
        public string Category { get; set; } = "Miscellaneous";
    }
}