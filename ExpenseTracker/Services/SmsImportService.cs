using ExpenseTracker.Interfaces;
using ExpenseTracker.Models;
using ExpenseTracker.Repositories;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ExpenseTracker.Services
{
    public class SmsImportService : ISmsImportService
    {
        private readonly IExpenseRepository _expenseRepository;

        public SmsImportService(IExpenseRepository expenseRepository)
        {
            _expenseRepository = expenseRepository;
        }

        public async Task<IEnumerable<ImportedTransaction>> ParseIncomingMessagesAsync(IEnumerable<string> messageBodies)
        {
            var transactions = new List<ImportedTransaction>();
            var existingTransactions = await _expenseRepository.GetImportedTransactionsAsync().ConfigureAwait(false);

            // local memory lookups for highly efficient batch imports
            var existingLookup = new HashSet<string>(
                existingTransactions.Select(x => $"{x.Amount:F2}_{x.Merchant.ToLower().Trim()}_{x.Date.Date:yyyyMMdd}_{x.ReferenceNumber}")
            );

            foreach (var body in messageBodies)
            {
                var parsed = ParseSms(body);
                if (parsed == null) continue;

                var currentKey = $"{parsed.Amount:F2}_{parsed.Merchant.ToLower().Trim()}_{parsed.Date.Date:yyyyMMdd}_{parsed.ReferenceNumber}";
                if (existingLookup.Contains(currentKey)) continue;

                var mapping = await _expenseRepository.GetMerchantCategoryMappingAsync(parsed.Merchant).ConfigureAwait(false);
                if (mapping != null)
                {
                    parsed.SuggestedCategory = mapping.Category;
                }

                await _expenseRepository.SaveImportedTransactionAsync(parsed).ConfigureAwait(false);
                existingLookup.Add(currentKey);
                transactions.Add(parsed);
            }

            return transactions;
        }

        public async Task<bool> IsDuplicateImportAsync(ImportedTransaction transaction)
        {
            var existingTransactions = await _expenseRepository.GetImportedTransactionsAsync().ConfigureAwait(false);

            return existingTransactions.Any(x =>
                x.Amount == transaction.Amount &&
                string.Equals(x.Merchant.Trim(), transaction.Merchant.Trim(), StringComparison.OrdinalIgnoreCase) &&
                x.Date.Date == transaction.Date.Date &&
                x.ReferenceNumber == transaction.ReferenceNumber);
        }

        private ImportedTransaction? ParseSms(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;

            var amount = ParseAmount(body);
            var transactionType = ParseTransactionType(body);
            var merchant = ParseMerchant(body);
            var date = ParseDate(body);
            var referenceNumber = ParseReference(body);

            // Hard Guard: Must have a valid currency amount to process
            if (amount <= 0) return null;

            // Fallback Guard: Never drop a valid transaction just because the merchant layout is unique
            if (string.IsNullOrWhiteSpace(merchant))
            {
                merchant = !string.IsNullOrWhiteSpace(transactionType)
                    ? $"{transactionType} Transaction"
                    : "Bank Transaction";
            }

            return new ImportedTransaction
            {
                Amount = amount,
                Merchant = merchant,
                Date = date,
                ReferenceNumber = referenceNumber,
                SmsContent = body,
                TransactionType = transactionType,
                SuggestedCategory = string.Empty,
                IsProcessed = false,
            };
        }

        private decimal ParseAmount(string body)
        {
            // Explicitly catches major Indian currency markers: Rs, Rs., INR, or ₹
            var amountMatch = Regex.Match(body, @"(?:Rs\.?|INR|₹)\s*([0-9]+(?:,[0-9]{3})*(?:\.[0-9]{1,2})?)", RegexOptions.IgnoreCase);
            if (!amountMatch.Success) return 0;

            var normalized = amountMatch.Groups[1].Value.Replace(",", string.Empty);
            return decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) ? value : 0;
        }

        private string ParseMerchant(string body)
        {
            // Pattern 1: ICICI Style structural balance sequences (e.g., "; RELIANCE RETAIL credited.")
            var iciciMatch = Regex.Match(body, @";\s*([A-Za-z0-9\s&.-]+?)\s+(?:credited|debited)", RegexOptions.IgnoreCase);
            if (iciciMatch.Success && !string.IsNullOrWhiteSpace(iciciMatch.Groups[1].Value))
                return iciciMatch.Groups[1].Value.Trim();

            // Pattern 2: Action indicator routing constructs (e.g., "spent at...", "paid to...", "transferred to...")
            var actionMatch = Regex.Match(body, @"(?:paid to|transferred to|spent at|towards|to merchant)\s+([A-Za-z0-9\s&.-]+?)(?=\s+(?:on|via|ref|txn|for|using|link|\.|$))", RegexOptions.IgnoreCase);
            if (actionMatch.Success && !string.IsNullOrWhiteSpace(actionMatch.Groups[1].Value))
                return actionMatch.Groups[1].Value.Trim();

            // Pattern 3: Explicit UPI / Wallet transfer destinations (e.g., "Sent Rs.500 to Swiggy")
            var walletMatch = Regex.Match(body, @"(?:sent|paid)\s+(?:Rs\.?|INR|₹)\s*[0-9,.]+\s+to\s+([A-Za-z0-9\s&.-]+?)(?=\s+(?:from|via|ref|txn|upi|using|\.|$))", RegexOptions.IgnoreCase);
            if (walletMatch.Success && !string.IsNullOrWhiteSpace(walletMatch.Groups[1].Value))
                return walletMatch.Groups[1].Value.Trim();

            // Pattern 4: Legacy structural fallbacks (e.g., "at XYZ", "from ABC", "via PQR")
            var fallbackMatch = Regex.Match(body, @"(?:at|from|via)\s+([A-Za-z0-9\s&.-]+?)(?=\s+(?:on|via|ref|txn|for|\.|$))", RegexOptions.IgnoreCase);
            if (fallbackMatch.Success && !string.IsNullOrWhiteSpace(fallbackMatch.Groups[1].Value))
                return fallbackMatch.Groups[1].Value.Trim();

            return string.Empty;
        }

        private DateTime ParseDate(string body)
        {
            // Captures both purely numeric (11-07-2026) and literal month expressions (11-Jul-26, 11 July 2026)
            var dateMatch = Regex.Match(body, @"\b\d{1,2}[-/\s](?:[A-Za-z]{3,9}|\d{1,2})[-/\s]\d{2,4}\b");
            if (dateMatch.Success)
            {
                var dateStr = dateMatch.Value.Trim();

                // Matrix of standard string variations utilized across Indian banking notifications
                string[] formats = {
                    "d-MMM-yy", "dd-MMM-yy", "d-MMM-yyyy", "dd-MMM-yyyy",
                    "d MMM yy", "dd MMM yy", "d MMM yyyy", "dd MMM yyyy",
                    "d/MM/yyyy", "dd/MM/yyyy", "d-MM-yyyy", "dd-MM-yyyy",
                    "yyyy-MM-dd", "dd/MM/yy", "dd-MM-yy"
                };

                // Change this line inside ParseDate():
                if (DateTime.TryParseExact(dateStr, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                {
                    return parsedDate;
                }
            }

            return DateTime.Now; // Device local fallback execution framework context
        }

        private string ParseReference(string body)
        {
            // Identifies common high-frequency transaction IDs (UPI, RRN, TXN, Ref)
            var refMatch = Regex.Match(body, @"(?:Ref|TXN|Txn|ID|UPI\s?ID|RRN)[:\s]*([A-Za-z0-9]+)", RegexOptions.IgnoreCase);
            return refMatch.Success ? refMatch.Groups[1].Value.Trim() : string.Empty;
        }

        private string ParseTransactionType(string body)
        {
            if (Regex.IsMatch(body, @"\b(credit|credited|received|refund)\b", RegexOptions.IgnoreCase))
            {
                return "Credit";
            }
            if (Regex.IsMatch(body, @"\b(debit|debited|spent|paid|transferred|withdrawn)\b", RegexOptions.IgnoreCase))
            {
                return "Debit";
            }
            return "Unknown";
        }
    }
}