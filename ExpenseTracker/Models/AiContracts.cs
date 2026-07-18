using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ExpenseTracker.Models
{
    // The Request payload envelope sent up to the cloud proxy
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

    // The Response payload envelope returned by the cloud proxy
    public class BatchExpenseResponse
    {
        [JsonPropertyName("processedTransactions")]
        public List<AiParsedTransaction> ProcessedTransactions { get; set; } = new();
    }

    public class AiParsedTransaction
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }

        [JsonPropertyName("category")]
        public string Category { get; set; } = "Unknown";

        [JsonPropertyName("transactionType")]
        public string TransactionType { get; set; } = "Debit";

        [JsonPropertyName("note")]
        public string Note { get; set; } = string.Empty;
    }
}