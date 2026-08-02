using System.Text.Json.Serialization;
using System.Collections.Generic;

namespace ExpenseTracker.Models
{
    public class GeminiTransactionResponse
    {
        // 🎯 Maps directly to your unique prompt ID tracking key
        public int Id { get; set; } 

        [JsonPropertyName("is_transaction")]
        public bool IsTransaction { get; set; }

        [JsonPropertyName("transaction_id")]
        public string TransactionId { get; set; }

        [JsonPropertyName("account_masked")]
        public string AccountMasked { get; set; }

        [JsonPropertyName("amount")]
        public decimal? Amount { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; }

        [JsonPropertyName("transaction_type")]
        public string TransactionType { get; set; }

        [JsonPropertyName("merchant_or_entity")]
        public string MerchantOrEntity { get; set; }

        [JsonPropertyName("category")]
        public string Category { get; set; }
    }

    // Wrapper matching your ViewModel's batch processing structure
    public class GeminiBatchWrapper
    {
        [JsonPropertyName("processed_transactions")]
        public List<GeminiTransactionResponse> ProcessedTransactions { get; set; } = new();
    }
}