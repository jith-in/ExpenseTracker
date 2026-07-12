using ExpenseTracker.Interfaces;
using ExpenseTracker.Models;
using ExpenseTracker.Repositories;
using ExpenseTracker.Services.Parsers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ExpenseTracker.Services
{
    public class SmsImportService : ISmsImportService
    {
        private readonly IExpenseRepository _expenseRepository;
        private readonly List<IBankParser> _bankParsers;

        // Tier 1: True Marketing, Ads, Security, and System Authentication Noise
        private readonly string[] PromotionalKeywords = {
            "otp", "one time password", "do not share", "kyc", "update pan",
            "reward points", "loan approved", "credit limit", "insurance renewal",
            "policy", "advertisement", "eligible", "apply now", "click here",
            "will be delivered", "open box delivery", "wrong mpin", "verify new limit",
            "സഞ്ചാർ സാഥി", "സിമ്മുകളുടെ എണ്ണം", "സൈബർ സുരക്ഷിതരായിരിക്കുക", "corruption"
        };

        private readonly Dictionary<string, string> MerchantCleaners = new(StringComparer.OrdinalIgnoreCase)
        {
            { "LIMITED", "" }, { "PVT", "" }, { "LTD", "" }, { "RETAIL", "" },
            { "SERVICES", "" }, { "COMMUNICATIONS", "" }, { "PAYMENTS", "" },
            { "TRADERS", "" }, { "MARKETS", "" }, { "STORES", "" }, { "SUPERMARKET", "" }
        };

        private readonly Dictionary<string, string> CategoryKeywordMatrix = new(StringComparer.OrdinalIgnoreCase)
        {
            { "swiggy", "Food" }, { "zomato", "Food" }, { "restaurant", "Food" }, { "hotel", "Food" },
            { "dmart", "Groceries" }, { "bazaar", "Groceries" }, { "groceries", "Groceries" }, { "reliance", "Shopping" },
            { "amazon", "Shopping" }, { "flipkart", "Shopping" }, { "myntra", "Shopping" },
            { "fuel", "Fuel" }, { "petroleum", "Fuel" }, { "bpc", "Fuel" }, { "iocl", "Fuel" },
            { "medical", "Medical" }, { "hospital", "Medical" }, { "pharmacy", "Medical" }, { "lab", "Medical" },
            { "kseb", "Bills" }, { "electricity", "Bills" }, { "recharge", "Bills" }, { "airtel", "Bills" }, { "jio", "Bills" }, { "bsnl", "Bills" },
            { "mutual fund", "Investment" }, { "sip", "Investment" }, { "iti", "Investment" }, { "tata", "Investment" }, { "sbi mf", "Investment" },
            { "irctc", "Travel" }, { "uber", "Travel" }, { "ola", "Travel" }, { "fastag", "Travel" }, { "kvip", "Travel" },
            { "netflix", "Entertainment" }, { "prime", "Entertainment" }, { "hotstar", "Entertainment" }
        };

        public SmsImportService(IExpenseRepository expenseRepository)
        {
            _expenseRepository = expenseRepository;

            _bankParsers = new List<IBankParser>
            {
                new IciciParser(),
                new SbiParser(),
                new HdfcParser(),
                new CanaraParser(),
                new UcoParser(),
                new GenericParser()
            };
        }

        public async Task<IEnumerable<ImportedTransaction>> ParseIncomingMessagesAsync(IEnumerable<SmsMessageData> messages)
        {
            var importedList = new List<ImportedTransaction>();
            var databaseLog = await _expenseRepository.GetAllImportedTransactionsAsync().ConfigureAwait(false);

            // Keep your existing lookup logic
            var hashLookup = new HashSet<string>(
                databaseLog.Select(x => x.ReferenceNumber).Where(r => !string.IsNullOrWhiteSpace(r))!
            );
            var contentLookup = new HashSet<string>(
                databaseLog.Select(x => ComputeSha256(Normalize(x.SmsContent)))
            );

            foreach (var message in messages)
            {
                // 1. Pass both body and system metadata (ReceivedDate) to your parser
                var parsed = ParseSms(message.Body, message.ReceivedDate);

                if (parsed == null) continue;

                // 2. Deduplication using existing logic
                if (!string.IsNullOrWhiteSpace(parsed.ReferenceNumber) && hashLookup.Contains(parsed.ReferenceNumber)) continue;
                if (contentLookup.Contains(ComputeSha256(parsed.SmsContent))) continue;

                // 3. Category Inference
                var learnedMapping = await _expenseRepository.GetMerchantCategoryMappingAsync(parsed.Merchant).ConfigureAwait(false);
                parsed.SuggestedCategory = learnedMapping != null ? learnedMapping.Category : InferCategoryFromKeywords(parsed.Merchant, parsed.SmsContent);

                // 4. Save to repository
                await _expenseRepository.SaveImportedTransactionAsync(parsed).ConfigureAwait(false);

                // 5. Update lookups
                if (!string.IsNullOrWhiteSpace(parsed.ReferenceNumber)) hashLookup.Add(parsed.ReferenceNumber);
                contentLookup.Add(ComputeSha256(parsed.SmsContent));

                importedList.Add(parsed);
            }

            // 6. Better Sorting: Use the TransactionDate if it exists, otherwise fall back to received date
            return importedList.OrderBy(x => x.TransactionDate ?? x.SmsReceivedDate);
        }
        public ImportedTransaction? ParseSms(string body, DateTime receivedDate)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;

            body = Normalize(body);

            // Tier 1: Identity & Promo Guard
            if (IsPromotional(body)) return null;

            // Tier 2: Future Intent Guard
            if (IsPendingTransaction(body)) return null;

            // Tier 3: Completed Parser Routing
            // Ensure IBankParser.Parse(string, DateTime) is updated in your interface
            var activeParser = _bankParsers.FirstOrDefault(p => p.CanHandle(body)) ?? _bankParsers.Last();

            // Pass the receivedDate into the specific parser
            var transaction = activeParser.Parse(body, receivedDate);

            if (transaction == null || transaction.Amount <= 0 || transaction.TransactionType == "Unknown")
            {
                return null;
            }

            // Post-Processing
            transaction.Merchant = CleanMerchantName(transaction.Merchant);
            transaction.SmsContent = body;

            // Ensure the metadata is captured
            transaction.SmsReceivedDate = receivedDate;

            // Note: If the specific bank parser didn't extract a TransactionDate, 
            // it will be null, which is exactly what we want.

            transaction.IsProcessed = false;

            return transaction;
        }

        private string Normalize(string body)
        {
            return body
                .Replace("\r", " ")
                .Replace("\n", " ")
                .Replace("Rs.", "Rs ")
                .Replace("INR.", "INR ")
                .Replace("  ", " ")
                .Trim();
        }

        private bool IsPromotional(string body)
        {
            var normalizedLower = body.ToLowerInvariant();
            return PromotionalKeywords.Any(normalizedLower.Contains);
        }

        // Tier 2: Pure State-Intent Guard Engine
        private bool IsPendingTransaction(string body)
        {
            return Regex.IsMatch(body,
                @"\b(will be debited|will be credited|debited shortly|credited shortly|scheduled|due for debit|due for payment|is due on)\b",
                RegexOptions.IgnoreCase);
        }

        private string CleanMerchantName(string merchant)
        {
            if (string.IsNullOrWhiteSpace(merchant)) return "Unknown Merchant";

            string workingName = Regex.Replace(merchant, @"\s+(credited|debited|spent|paid|towards|completed|successful|using|via)\b.*", "", RegexOptions.IgnoreCase);

            var tokens = workingName.Split(new[] { ' ', '*', '-' }, StringSplitOptions.RemoveEmptyEntries);
            var cleanTokens = tokens.Where(t => !MerchantCleaners.ContainsKey(t));

            workingName = string.Join(" ", cleanTokens).Trim();
            workingName = Regex.Replace(workingName, @"\s+", " ");

            return string.IsNullOrWhiteSpace(workingName) ? "Bank Transaction" : workingName;
        }

        private string InferCategoryFromKeywords(string merchant, string text)
        {
            var combinedScope = $"{merchant} {text}".ToLowerInvariant();
            foreach (var rule in CategoryKeywordMatrix)
            {
                if (combinedScope.Contains(rule.Key)) return rule.Value;
            }
            return "Others";
        }

        private string ComputeSha256(string rawData)
        {
            using SHA256 sha256Hash = SHA256.Create();
            byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            StringBuilder builder = new();
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(bytes[i].ToString("x2"));
            }
            return builder.ToString();
        }

        public async Task<bool> IsDuplicateImportAsync(ImportedTransaction transaction)
        {
            var log = await _expenseRepository.GetAllImportedTransactionsAsync().ConfigureAwait(false);
            var contextHash = ComputeSha256(Normalize(transaction.SmsContent));
            return log.Any(x => ComputeSha256(Normalize(x.SmsContent)) == contextHash);
        }
    }
}