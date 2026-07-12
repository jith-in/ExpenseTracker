using ExpenseTracker.Models;
using System;
using System.Text.RegularExpressions;

namespace ExpenseTracker.Services.Parsers
{
    public class GenericParser : IBankParser
    {
        public string BankName => "Generic Provider";

        // This catch-all returns true if it hits any fallback criteria
        public bool CanHandle(string body)
        {
            return true;
        }

        public ImportedTransaction Parse(string body, DateTime receivedDate)
        {
            var transaction = new ImportedTransaction();

            // 1. Metadata & Transaction Date Mapping
            // Always trust the Android system metadata (SmsReceivedDate)
            transaction.SmsReceivedDate = receivedDate;

            // Only set TransactionDate if a date exists in the text (will be null otherwise)
            transaction.TransactionDate = ParserToolkit.ExtractDate(body);

            // 2. Core metric extractions via Toolkit helpers
            transaction.Amount = ParserToolkit.ExtractAmount(body);
            transaction.TransactionType = ParserToolkit.ClassifyTransactionType(body, out var subType);
            transaction.SuggestedPaymentMethod = ParserToolkit.ParseChannelFormat(body, "Net Banking");

            // 3. Extract standard references safely
            // (Note: Your regex correctly includes RRN as 'Retrieval Reference Number', which is safe to use in a banking context)
            var refMatch = Regex.Match(body, @"(?:Ref|TXN|Txn|ID|RRN|IMPS)[:\s\-]*([A-Za-z0-9]+)", RegexOptions.IgnoreCase);
            transaction.ReferenceNumber = refMatch.Success ? refMatch.Groups[1].Value.Trim() : string.Empty;

            // 4. Extract basic merchant strings safely
            transaction.Merchant = ExtractGenericMerchant(body);

            // 5. Calculate Confidence Score
            transaction.Confidence = CalculateConfidence(transaction);

            return transaction;
        }

        private string ExtractGenericMerchant(string body)
        {
            // Fallback lookup: Scrape keywords bordered by standard execution connectors
            var actionMatch = Regex.Match(body, @"(?:paid to|transferred to|spent at|towards|to merchant|at)\s+([A-Za-z0-9\s&.-]+?)(?=\s+(?:on|via|ref|txn|for|using|link|\.|$))", RegexOptions.IgnoreCase);
            if (actionMatch.Success && !string.IsNullOrWhiteSpace(actionMatch.Groups[1].Value))
                return actionMatch.Groups[1].Value.Trim();

            return string.Empty;
        }

        private int CalculateConfidence(ImportedTransaction t)
        {
            int score = 0;
            if (t.Amount > 0) score += 25;
            if (t.TransactionType != "Unknown") score += 25;
            if (!string.IsNullOrWhiteSpace(t.ReferenceNumber)) score += 15;
            if (!string.IsNullOrWhiteSpace(t.Merchant)) score += 15;

            // Caps at an 80% maximum boundary to tell the UI it's an unverified/generic guess
            return Math.Clamp(score, 0, 80);
        }
    }
}