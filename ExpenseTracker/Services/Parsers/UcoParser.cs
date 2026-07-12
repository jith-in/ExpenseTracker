using ExpenseTracker.Models;
using System;
using System.Text.RegularExpressions;

namespace ExpenseTracker.Services.Parsers
{
    public class UcoParser : IBankParser
    {
        public string BankName => "UCO Bank";

        public bool CanHandle(string body)
        {
            return body.Contains("UCO", StringComparison.OrdinalIgnoreCase);
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
            transaction.TransactionType = ParserToolkit.ClassifyTransactionType(body, out _);
            transaction.SuggestedPaymentMethod = ParserToolkit.ParseChannelFormat(body, "Net Banking");

            // 3. Extract references safely
            var refMatch = Regex.Match(body, @"(?:Ref|Txn|ID)[:\s\-]*([A-Za-z0-9]+)", RegexOptions.IgnoreCase);
            transaction.ReferenceNumber = refMatch.Success ? refMatch.Groups[1].Value.Trim() : string.Empty;

            // 4. Extract merchant descriptions (UCO specific patterns)
            var merchantMatch = Regex.Match(body, @"(?:by self|to self|by|to)\s+([A-Za-z0-9\s&.-]+?)(?=\s+(?:by|on|via|\.|$))", RegexOptions.IgnoreCase);
            transaction.Merchant = merchantMatch.Success ? merchantMatch.Groups[1].Value.Trim() : string.Empty;

            // 5. Confidence Score
            transaction.Confidence = transaction.Amount > 0 && transaction.TransactionType != "Unknown" ? 95 : 60;

            return transaction;
        }
    }
}