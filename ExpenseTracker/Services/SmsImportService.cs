using ExpenseTracker.Interfaces;
using ExpenseTracker.Models;
using ExpenseTracker.Repositories;
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

            foreach (var body in messageBodies)
            {
                var parsed = ParseSms(body);
                if (parsed == null)
                {
                    continue;
                }

                var isDuplicate = existingTransactions.Any(x =>
                    x.Amount == parsed.Amount &&
                    x.Merchant == parsed.Merchant &&
                    x.Date.Date == parsed.Date.Date &&
                    x.ReferenceNumber == parsed.ReferenceNumber);

                if (isDuplicate)
                {
                    continue;
                }

                var mapping = await _expenseRepository.GetMerchantCategoryMappingAsync(parsed.Merchant).ConfigureAwait(false);
                if (mapping != null)
                {
                    parsed.SuggestedCategory = mapping.Category;
                }

                await _expenseRepository.SaveImportedTransactionAsync(parsed).ConfigureAwait(false);
                existingTransactions.Add(parsed);
                transactions.Add(parsed);
            }

            return transactions;
        }

        public async Task<bool> IsDuplicateImportAsync(ImportedTransaction transaction)
        {
            var existingTransactions = await _expenseRepository.GetImportedTransactionsAsync().ConfigureAwait(false);

            return existingTransactions.Any(x =>
                x.Amount == transaction.Amount &&
                x.Merchant == transaction.Merchant &&
                x.Date.Date == transaction.Date.Date &&
                x.ReferenceNumber == transaction.ReferenceNumber);
        }

        private ImportedTransaction? ParseSms(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            var amount = ParseAmount(body);
            var merchant = ParseMerchant(body);
            var date = ParseDate(body);
            var referenceNumber = ParseReference(body);
            var transactionType = ParseTransactionType(body);

            if (amount <= 0 || string.IsNullOrWhiteSpace(merchant))
            {
                return null;
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
            var amountMatch = Regex.Match(body, @"[₹RS]?\s?([0-9]+(?:,[0-9]{3})*(?:\.[0-9]{1,2})?)", RegexOptions.IgnoreCase);
            if (!amountMatch.Success)
            {
                return 0;
            }

            var normalized = amountMatch.Groups[1].Value.Replace(",", string.Empty);
            if (decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }

            return 0;
        }

        private string ParseMerchant(string body)
        {
            var merchantMatch = Regex.Match(body, @"at\s+([A-Za-z0-9 &.-]+)|from\s+([A-Za-z0-9 &.-]+)|via\s+([A-Za-z0-9 &.-]+)", RegexOptions.IgnoreCase);
            return merchantMatch.Groups.Cast<Group>().Skip(1).Select(g => g.Value).FirstOrDefault(g => !string.IsNullOrWhiteSpace(g))?.Trim() ?? string.Empty;
        }

        private DateTime ParseDate(string body)
        {
            var dateMatch = Regex.Match(body, @"(\d{1,2}[/-]\d{1,2}[/-]\d{2,4})");
            if (dateMatch.Success && DateTime.TryParse(dateMatch.Groups[1].Value, out var parsedDate))
            {
                return parsedDate;
            }

            return DateTime.UtcNow;
        }

        private string ParseReference(string body)
        {
            var refMatch = Regex.Match(body, @"(Ref|TXN|Txn|UPI\s?ID|RRN)[:\s]*([A-Za-z0-9]+)", RegexOptions.IgnoreCase);
            return refMatch.Success ? refMatch.Groups[2].Value.Trim() : string.Empty;
        }

        private string ParseTransactionType(string body)
        {
            if (Regex.IsMatch(body, "credit|debit|spent|paid", RegexOptions.IgnoreCase))
            {
                return Regex.Match(body, "credit|debit|spent|paid", RegexOptions.IgnoreCase).Value;
            }

            return string.Empty;
        }
    }
}
