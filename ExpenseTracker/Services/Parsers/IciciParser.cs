using ExpenseTracker.Models;
using System;
using System.Text.RegularExpressions;

namespace ExpenseTracker.Services.Parsers
{
    public class IciciParser : IBankParser
    {
        public string BankName => "ICICI Bank";

        public bool CanHandle(string body)
        {
            return body.Contains("ICICI Bank", StringComparison.OrdinalIgnoreCase);
        }

        public ImportedTransaction Parse(string body, DateTime receivedDate)
        {
            var transaction = new ImportedTransaction();

            // 1. Metadata & Transaction Date Mapping
            // Always trust the Android receivedDate as the primary timestamp
            transaction.SmsReceivedDate = receivedDate;

            // Only set TransactionDate if a date exists in the text (will be null otherwise)
            transaction.TransactionDate = ParserToolkit.ExtractDate(body);

            // 2. Core metric extractions via Toolkit helpers
            transaction.Amount = ParserToolkit.ExtractAmount(body);
            transaction.TransactionType = ParserToolkit.ClassifyTransactionType(body, out var subType);
            transaction.SuggestedPaymentMethod = ParserToolkit.ParseChannelFormat(body, "Net Banking");

            // 3. Extract references safely
            var refMatch = Regex.Match(body, @"(?:Ref|Txn|IMPS)[:\s\.no]*([A-Za-z0-9]+)", RegexOptions.IgnoreCase);
            transaction.ReferenceNumber = refMatch.Success ? refMatch.Groups[1].Value.Trim() : string.Empty;

            // 4. Extract exact target merchant descriptions
            transaction.Merchant = ExtractIciciMerchant(body);

            // Layer 10: Structural Confidence Calculations
            transaction.Confidence = CalculateConfidence(transaction, body);

            return transaction;
        }

        private string ExtractIciciMerchant(string body)
        {
            // Card layout checks
            var cardMerchant = Regex.Match(body, @"card\s+XX\d{4}\s+on\s+[^_]+?\s+on\s+([A-Za-z0-9\s&.-]+?)(?=\.\s*Avl|\.\s*If|$)", RegexOptions.IgnoreCase);
            if (cardMerchant.Success) return cardMerchant.Groups[1].Value;

            // Direct sequence checkpoints
            var directMerchant = Regex.Match(body, @";\s*([A-Za-z0-9\s&.-]+?)\s+(?:credited|debited)", RegexOptions.IgnoreCase);
            if (directMerchant.Success) return directMerchant.Groups[1].Value;

            return string.Empty;
        }

        private int CalculateConfidence(ImportedTransaction t, string body)
        {
            int score = 0;
            if (t.Amount > 0) score += 30;
            if (t.TransactionType != "Unknown") score += 30;
            if (!string.IsNullOrWhiteSpace(t.ReferenceNumber)) score += 20;
            if (!string.IsNullOrWhiteSpace(t.Merchant) && t.Merchant != $"{t.TransactionType} Transaction") score += 18;

            return Math.Clamp(score, 0, 100); // Guarantees score metrics land cleanly inside bounds
        }
    }
}