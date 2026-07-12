using ExpenseTracker.Models;
using System;
using System.Text.RegularExpressions;

namespace ExpenseTracker.Services.Parsers
{
    public class HdfcParser : IBankParser
    {
        public string BankName => "HDFC Bank";

        public bool CanHandle(string body)
        {
            return body.Contains("HDFC", StringComparison.OrdinalIgnoreCase);
        }

        public ImportedTransaction Parse(string body, DateTime receivedDate)
        {
            var transaction = new ImportedTransaction();

            // 1. Map the dates
            transaction.SmsReceivedDate = receivedDate; // Metadata from Android
            transaction.TransactionDate = ParserToolkit.ExtractDate(body); // Nullable from SMS text

            // 2. Map standard fields
            transaction.Amount = ParserToolkit.ExtractAmount(body);
            transaction.TransactionType = ParserToolkit.ClassifyTransactionType(body, out _);
            transaction.SuggestedPaymentMethod = ParserToolkit.ParseChannelFormat(body, "Net Banking");

            // 3. Extract Reference Number
            // Note: 'RRN' here refers to the 'Retrieval Reference Number', a standard banking term.
            var refMatch = Regex.Match(body, @"(?:Ref|Txn|RRN)[:\s\-]*([A-Za-z0-9]+)", RegexOptions.IgnoreCase);
            transaction.ReferenceNumber = refMatch.Success ? refMatch.Groups[1].Value.Trim() : string.Empty;

            // 4. Pattern for HDFC notification routing architectures
            var merchantMatch = Regex.Match(body, @"(?:at|to|towards)\s+([A-Za-z0-9\s&.-]+?)(?=\s+(?:on|via|ref|\.|$))", RegexOptions.IgnoreCase);
            transaction.Merchant = merchantMatch.Success ? merchantMatch.Groups[1].Value.Trim() : string.Empty;

            // 5. Confidence Logic
            transaction.Confidence = transaction.Amount > 0 && transaction.TransactionType != "Unknown" ? 95 : 60;

            return transaction;
        }
    }
}