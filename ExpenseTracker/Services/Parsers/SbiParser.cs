using ExpenseTracker.Models;
using System;
using System.Text.RegularExpressions;

namespace ExpenseTracker.Services.Parsers
{
    public class SbiParser : IBankParser
    {
        public string BankName => "SBI";

        public bool CanHandle(string body)
        {
            return body.Contains("SBI", StringComparison.OrdinalIgnoreCase);
        }

        public ImportedTransaction Parse(string body, DateTime receivedDate)
        {
            var transaction = new ImportedTransaction();

            // 1. Metadata & Transaction Date Mapping
            // Always trust the Android system metadata (SmsReceivedDate)
            transaction.SmsReceivedDate = receivedDate;

            // Only set TransactionDate if a date exists in the text (will be null otherwise)
            transaction.TransactionDate = ParserToolkit.ExtractDate(body);

            // 2. Core metric extractions
            transaction.Amount = ParserToolkit.ExtractAmount(body);
            transaction.TransactionType = ParserToolkit.ClassifyTransactionType(body, out var subType);
            transaction.SuggestedPaymentMethod = ParserToolkit.ParseChannelFormat(body, "Net Banking");

            // 3. Extract references safely
            var refMatch = Regex.Match(body, @"(?:Ref\s*No|Txn\s*ID|UPI\s*ID|RRN)[:\s\-]*([A-Za-z0-9]+)", RegexOptions.IgnoreCase);
            transaction.ReferenceNumber = refMatch.Success ? refMatch.Groups[1].Value.Trim() : string.Empty;

            // 4. Extract merchant descriptions
            transaction.Merchant = ExtractSbiMerchant(body);

            // 5. Confidence Score
            transaction.Confidence = CalculateConfidence(transaction);

            return transaction;
        }

        private string ExtractSbiMerchant(string body)
        {
            // Pattern A: Trailing assignment structures (e.g., "debited for Rs 500... to Swiggy")
            var actionMatch = Regex.Match(body, @"(?:paid to|transferred to|spent at|to|towards)\s+([A-Za-z0-9\s&.-]+?)(?=\s+(?:on|via|ref|txn|for|using|link|\.|$))", RegexOptions.IgnoreCase);
            if (actionMatch.Success && !string.IsNullOrWhiteSpace(actionMatch.Groups[1].Value))
                return actionMatch.Groups[1].Value.Trim();

            // Pattern B: Leading/Inline statement identifiers (e.g., "Thank you for shopping at DMART")
            var shoppingMatch = Regex.Match(body, @"(?:shopping at|at)\s+([A-Za-z0-9\s&.-]+?)(?=\s+(?:on|via|ref|\.|$))", RegexOptions.IgnoreCase);
            if (shoppingMatch.Success && !string.IsNullOrWhiteSpace(shoppingMatch.Groups[1].Value))
                return shoppingMatch.Groups[1].Value.Trim();

            return string.Empty;
        }

        private int CalculateConfidence(ImportedTransaction t)
        {
            int score = 0;
            if (t.Amount > 0) score += 30;
            if (t.TransactionType != "Unknown") score += 30;
            if (!string.IsNullOrWhiteSpace(t.ReferenceNumber)) score += 20;
            if (!string.IsNullOrWhiteSpace(t.Merchant) && t.Merchant != $"{t.TransactionType} Transaction") score += 20;

            return Math.Clamp(score, 0, 100);
        }
    }
}