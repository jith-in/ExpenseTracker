using ExpenseTracker.Models;
using System;
using System.Text.RegularExpressions;

namespace ExpenseTracker.Services.Parsers
{
    public class CanaraParser : IBankParser
    {
        public string BankName => "Canara Bank";

        public bool CanHandle(string body)
        {
            return body.Contains("Canara", StringComparison.OrdinalIgnoreCase);
        }

        public ImportedTransaction Parse(string body, DateTime receivedDate)
        {
            var transaction = new ImportedTransaction();

            // Mapping the Two-Date Architecture
            transaction.SmsReceivedDate = receivedDate; // Metadata from Android
            transaction.TransactionDate = ParserToolkit.ExtractDate(body); // Nullable from SMS text

            // Parsing logic
            transaction.Amount = ParserToolkit.ExtractAmount(body);
            transaction.TransactionType = ParserToolkit.ClassifyTransactionType(body, out _);
            transaction.SuggestedPaymentMethod = ParserToolkit.ParseChannelFormat(body, "Net Banking");

            // Extract Reference Number
            var refMatch = Regex.Match(body, @"(?:Ref|Txn|ID)[:\s\-]*([A-Za-z0-9]+)", RegexOptions.IgnoreCase);
            transaction.ReferenceNumber = refMatch.Success ? refMatch.Groups[1].Value.Trim() : string.Empty;

            // Pattern for Canara specific notification text templates
            var merchantMatch = Regex.Match(body, @"(?:towards|at|from)\s+([A-Za-z0-9\s&.-]+?)(?=\s+(?:on|via|\.|$))", RegexOptions.IgnoreCase);
            transaction.Merchant = merchantMatch.Success ? merchantMatch.Groups[1].Value.Trim() : string.Empty;

            // Confidence Logic
            transaction.Confidence = transaction.Amount > 0 && transaction.TransactionType != "Unknown" ? 95 : 60;

            return transaction;
        }
    }
}