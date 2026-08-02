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
            var transaction = new ImportedTransaction
            {
                SmsReceivedDate = receivedDate,
                TransactionDate = ParserToolkit.ExtractDate(body)
            };

            // 🎯 1. ICICI Outbound UPI Pattern (e.g., "ICICI Bank Acct XX480 debited for Rs 500.00 on 25-Jul-26; Ashtami Raj credited. UPI:620608715687")
            var iciciUpiOutboundMatch = Regex.Match(
                body,
                @"Acct?\s+(\w+)\s+debited\s+(?:for\s+)?Rs\.?\s*([\d,]+\.?\d*)\s+on\s+([\d\w\-]+);\s*(.*?)\s+credited",
                RegexOptions.IgnoreCase);

            if (iciciUpiOutboundMatch.Success)
            {
                if (decimal.TryParse(iciciUpiOutboundMatch.Groups[2].Value.Replace(",", ""), out decimal parsedAmount))
                {
                    transaction.Amount = parsedAmount;
                }

                transaction.Merchant = iciciUpiOutboundMatch.Groups[4].Value.Trim();
                transaction.TransactionType = "Debit"; // 🟢 Force Debit explicitly
                transaction.SuggestedPaymentMethod = "UPI";
                transaction.ReferenceNumber = ExtractIciciReferenceNumber(body);
                transaction.Confidence = CalculateConfidence(transaction, body);

                return transaction;
            }

            // 🎯 2. ICICI Credit Card Payment Received Pattern (e.g., "Payment of INR 5,539.00 has been received on your ICICI Bank Credit Card Account 4xxx7007")
            var ccPaymentMatch = Regex.Match(
                body,
                @"Payment\s+of\s+(?:INR|Rs\.?)\s*([\d,]+\.?\d*)\s+has\s+been\s+received\s+on\s+your\s+ICICI\s+Bank\s+Credit\s+Card",
                RegexOptions.IgnoreCase);

            if (ccPaymentMatch.Success)
            {
                if (decimal.TryParse(ccPaymentMatch.Groups[1].Value.Replace(",", ""), out decimal parsedAmount))
                {
                    transaction.Amount = parsedAmount;
                }

                transaction.Merchant = "ICICI Credit Card Payment";
                // 🎯 Changed from "Credit" to "Transfer" to avoid inflating Income metrics
                transaction.TransactionType = "Transfer";
                transaction.SuggestedPaymentMethod = "Net Banking";
                transaction.ReferenceNumber = ExtractIciciReferenceNumber(body);
                transaction.Confidence = CalculateConfidence(transaction, body);

                return transaction;
            }

            // 3. Fallback to toolkit extraction for other standard formats (POS, Card Swipes, NEFT)
            transaction.Amount = ParserToolkit.ExtractAmount(body);
            transaction.TransactionType = ParserToolkit.ClassifyTransactionType(body, out _);
            transaction.SuggestedPaymentMethod = ParserToolkit.ParseChannelFormat(body, "Net Banking");
            transaction.ReferenceNumber = ExtractIciciReferenceNumber(body);
            transaction.Merchant = ExtractIciciMerchant(body);
            transaction.Confidence = CalculateConfidence(transaction, body);

            return transaction;
        }

        private string ExtractIciciMerchant(string body)
        {
            // Card layout checks
            var cardMerchant = Regex.Match(body, @"card\s+XX\d{4}\s+on\s+[^_]+?\s+on\s+([A-Za-z0-9\s&.-]+?)(?=\.\s*Avl|\.\s*If|$)", RegexOptions.IgnoreCase);
            if (cardMerchant.Success) return cardMerchant.Groups[1].Value.Trim();

            // Direct sequence checkpoints (e.g. "; Ashtami Raj credited")
            var directMerchant = Regex.Match(body, @";\s*([A-Za-z0-9\s&.-]+?)\s+(?:credited|debited)", RegexOptions.IgnoreCase);
            if (directMerchant.Success) return directMerchant.Groups[1].Value.Trim();

            // NEFT/IMPS Merchant extraction
            var neftMatch = Regex.Match(body, @"Info\s+(?:NEFT|IMPS)-[A-Za-z0-9]+-([A-Za-z0-9\s.-]+)", RegexOptions.IgnoreCase);
            if (neftMatch.Success) return neftMatch.Groups[1].Value.Trim();

            return string.Empty;
        }

        private string ExtractIciciReferenceNumber(string body)
        {
            // 1. UPI Ref No
            var upiRefMatch = Regex.Match(body, @"UPI:?\s*(\d+)", RegexOptions.IgnoreCase);
            if (upiRefMatch.Success) return upiRefMatch.Groups[1].Value.Trim();

            // 2. NEFT / IMPS / Txn Ref
            var refMatch = Regex.Match(body, @"(?:Ref|Txn|IMPS|NEFT)[:\s\.-]*([A-Za-z0-9]+)", RegexOptions.IgnoreCase);
            if (refMatch.Success) return refMatch.Groups[1].Value.Trim();

            return string.Empty;
        }

        private int CalculateConfidence(ImportedTransaction t, string body)
        {
            int score = 0;
            if (t.Amount > 0) score += 30;
            if (t.TransactionType != "Unknown") score += 30;
            if (!string.IsNullOrWhiteSpace(t.ReferenceNumber)) score += 20;
            if (!string.IsNullOrWhiteSpace(t.Merchant) && t.Merchant != $"{t.TransactionType} Transaction") score += 18;

            return Math.Clamp(score, 0, 100);
        }
    }
}