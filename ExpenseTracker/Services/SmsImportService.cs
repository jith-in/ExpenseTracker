using ExpenseTracker.Interfaces;
using ExpenseTracker.Models;
using ExpenseTracker.Repositories;
using ExpenseTracker.Services.Parsers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            { "dmart", "Groceries" }, { "bazaar", "Groceries" }, { "groceries", "Groceries" },
            { "reliance", "Shopping" }, { "amazon", "Shopping" }, { "flipkart", "Shopping" }, { "myntra", "Shopping" }, { "lulu", "Shopping" },
            { "fuel", "Fuel" }, { "petroleum", "Fuel" }, { "bpc", "Fuel" }, { "iocl", "Fuel" },
            { "medical", "Medical" }, { "hospital", "Medical" }, { "pharmacy", "Medical" }, { "lab", "Medical" },
            { "kseb", "Bills" }, { "electricity", "Bills" }, { "recharge", "Bills" }, { "airtel", "Bills" }, { "jio", "Bills" }, { "bsnl", "Bills" },
            { "mutual fund", "Investment" }, { "sip", "Investment" }, { "iti", "Investment" }, { "tata", "Investment" }, { "sbi mf", "Investment" }, { "fixed deposit", "Investment" }, { "fd clos", "Investment" }, { "fd closure", "Investment" }, { "ach", "Investment" },
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

            var hashLookup = new HashSet<string>(
                databaseLog.Select(x => x.ReferenceNumber).Where(r => !string.IsNullOrWhiteSpace(r))!
            );
            var contentLookup = new HashSet<string>(
                databaseLog.Select(x => ComputeSha256(Normalize(x.SmsContent)))
            );

            foreach (var message in messages)
            {
                var parsed = ParseSms(message.Body, message.ReceivedDate);

                if (parsed == null) continue;

                if (!string.IsNullOrWhiteSpace(parsed.ReferenceNumber) && hashLookup.Contains(parsed.ReferenceNumber)) continue;
                if (contentLookup.Contains(ComputeSha256(parsed.SmsContent))) continue;

                var learnedMapping = await _expenseRepository.GetMerchantCategoryMappingAsync(parsed.Merchant).ConfigureAwait(false);
                parsed.SuggestedCategory = learnedMapping != null ? learnedMapping.Category : InferCategoryFromKeywords(parsed.Merchant, parsed.SmsContent);

                await _expenseRepository.SaveImportedTransactionAsync(parsed).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(parsed.ReferenceNumber)) hashLookup.Add(parsed.ReferenceNumber);
                contentLookup.Add(ComputeSha256(parsed.SmsContent));

                importedList.Add(parsed);
            }

            return importedList.OrderBy(x => x.TransactionDate ?? x.SmsReceivedDate);
        }

        public ImportedTransaction? ParseSms(string body, DateTime receivedDate)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;

            body = Normalize(body);

            if (IsPromotional(body)) return null;
            if (IsPendingTransaction(body)) return null;

            var activeParser = _bankParsers.FirstOrDefault(p => p.CanHandle(body)) ?? _bankParsers.Last();
            var transaction = activeParser.Parse(body, receivedDate);

            if (transaction == null || transaction.Amount <= 0 || transaction.TransactionType == "Unknown")
            {
                return null;
            }

            // Intercept engine: Captures automated clearings, terminal card swipes, and P2P transfers
            if (string.IsNullOrWhiteSpace(transaction.Merchant) ||
                transaction.Merchant.Equals("Unknown Merchant", StringComparison.OrdinalIgnoreCase))
            {
                // 1. Automated Clearing House processing loop
                if (body.Contains("ACH*", StringComparison.OrdinalIgnoreCase))
                {
                    string token = body.Contains("Info ACH*", StringComparison.OrdinalIgnoreCase) ? "Info ACH*" : "ACH*";
                    int achIndex = body.IndexOf(token, StringComparison.OrdinalIgnoreCase) + token.Length;
                    string rawDetails = body.Substring(achIndex).Trim();

                    if (rawDetails.Contains(".")) rawDetails = rawDetails.Split('.')[0].Trim();

                    string[] balanceKeywords = { "Available Balance", "Avl Bal", "View Full" };
                    foreach (var keyword in balanceKeywords)
                    {
                        int index = rawDetails.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
                        if (index >= 0) rawDetails = rawDetails.Substring(0, index).Trim();
                    }

                    rawDetails = rawDetails.TrimEnd('.', ' ');
                    if (!string.IsNullOrWhiteSpace(rawDetails))
                    {
                        transaction.Merchant = $"ACH - {rawDetails}";
                    }
                }
                // 2. Fixed Deposit Closure Intercept Engine
                else if (Regex.IsMatch(body, @"\bFD\s+clos", RegexOptions.IgnoreCase) || body.Contains("Fixed Deposit", StringComparison.OrdinalIgnoreCase))
                {
                    var fdNumMatch = Regex.Match(body, @"\b(Info\s+)?(\d{4,})\s+FD\s+clos", RegexOptions.IgnoreCase);

                    if (fdNumMatch.Success)
                    {
                        transaction.Merchant = $"FD Closure - {fdNumMatch.Groups[2].Value}";
                    }
                    else
                    {
                        transaction.Merchant = "Fixed Deposit Closure";
                    }
                }
                // 3. Point of Sale / Card terminal transaction merchant capture check (with 'at')
                else if (Regex.IsMatch(body, @"\bat\s+", RegexOptions.IgnoreCase))
                {
                    var terminalMatch = Regex.Match(body, @"\bat\s+([A-Z0-9][A-Z0-9\s\-\*&'\.]+?)(?=\.|\bAvl\b|\bBalance\b|\bLmt\b|\bTo\b|$)", RegexOptions.IgnoreCase);

                    if (terminalMatch.Success)
                    {
                        string extractedIdentifier = terminalMatch.Groups[1].Value.Trim();
                        extractedIdentifier = Regex.Replace(extractedIdentifier, @"\s+", " ");

                        if (!string.IsNullOrWhiteSpace(extractedIdentifier) && extractedIdentifier.Length > 2)
                        {
                            transaction.Merchant = extractedIdentifier;
                        }
                    }
                }
                // 4. Date-Trailing Merchant Intercept Engine (e.g., "on 07-Jul-26 VPS*LULU INTE.")
                else if (Regex.IsMatch(body, @"\bon\s+\d{2}-[A-Za-z]{3}-\d{2}\s+", RegexOptions.IgnoreCase))
                {
                    var dateTrailingMatch = Regex.Match(body, @"\bon\s+\d{2}-[A-Za-z]{3}-\d{2}\s+([A-Z0-9\*][A-Z0-9\s\-\*&'\.]+?)(?=\.|\bBal\b|\bBalance\b|\bAvailable\b|$)", RegexOptions.IgnoreCase);

                    if (dateTrailingMatch.Success)
                    {
                        string extractedIdentifier = dateTrailingMatch.Groups[1].Value.Trim();
                        extractedIdentifier = Regex.Replace(extractedIdentifier, @"\s+", " ");

                        if (!string.IsNullOrWhiteSpace(extractedIdentifier) && extractedIdentifier.Length > 2)
                        {
                            transaction.Merchant = extractedIdentifier;
                        }
                    }
                }
                // ✅ 5. ADDED: Recharge & Utility Intercept Engine
                else if (body.Contains("Recharge", StringComparison.OrdinalIgnoreCase) || body.Contains("Prepaid no", StringComparison.OrdinalIgnoreCase))
                {
                    if (body.Contains("bsnl", StringComparison.OrdinalIgnoreCase))
                        transaction.Merchant = "BSNL Recharge";
                    else if (body.Contains("airtel", StringComparison.OrdinalIgnoreCase))
                        transaction.Merchant = "Airtel Recharge";
                    else if (body.Contains("jio", StringComparison.OrdinalIgnoreCase))
                        transaction.Merchant = "Jio Recharge";
                    else if (body.Contains("vi ", StringComparison.OrdinalIgnoreCase) || body.Contains("vodafone", StringComparison.OrdinalIgnoreCase))
                        transaction.Merchant = "Vi Recharge";
                    else
                        transaction.Merchant = "Prepaid Recharge";
                }
                // 6. Peer-to-Peer Transfer name capture check
                else
                {
                    var p2pMatch = Regex.Match(body, @"\b(from|to)\s+([A-Z][A-Z\s]+?)(?=\.|\bUPI\b|\bRs\b|\bAccount\b|$)", RegexOptions.IgnoreCase);

                    if (p2pMatch.Success)
                    {
                        string extractedIdentifier = p2pMatch.Groups[2].Value.Trim();
                        extractedIdentifier = Regex.Replace(extractedIdentifier, @"\s+", " ");

                        if (!string.IsNullOrWhiteSpace(extractedIdentifier) && extractedIdentifier.Length > 2)
                        {
                            transaction.Merchant = extractedIdentifier;
                        }
                    }
                }
            }

            // Post-Processing validation pipeline continues as normal...
            transaction.Merchant = CleanMerchantName(transaction.Merchant);
            transaction.SmsContent = body;
            transaction.SmsReceivedDate = receivedDate;
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

        private bool IsPendingTransaction(string body)
        {
            return Regex.IsMatch(body,
                @"\b(will be debited|will be credited|debited shortly|credited shortly|scheduled|due for debit|due for payment|is due on)\b",
                RegexOptions.IgnoreCase);
        }

        private string CleanMerchantName(string merchant)
        {
            if (string.IsNullOrWhiteSpace(merchant)) return "Unknown Merchant";

            if (merchant.StartsWith("ACH -", StringComparison.OrdinalIgnoreCase))
            {
                return merchant;
            }

            if (merchant.StartsWith("FD Closure", StringComparison.OrdinalIgnoreCase))
            {
                return merchant;
            }

            // ✅ PROTECT EXTRACTED RECHARGE STRINGS FROM STRIPPING
            if (merchant.EndsWith("Recharge", StringComparison.OrdinalIgnoreCase))
            {
                return merchant;
            }

            string workingName = Regex.Replace(merchant, @"\s+(credited|debited|spent|paid|towards|completed|successful|using|via)\b.*", "", RegexOptions.IgnoreCase);

            var tokens = workingName.Split(new[] { ' ', '*', '-' }, StringSplitOptions.RemoveEmptyEntries);
            var cleanTokens = tokens.Where(t => !MerchantCleaners.ContainsKey(t));

            workingName = string.Join(" ", cleanTokens).Trim();
            workingName = Regex.Replace(workingName, @"\s+", " ");

            workingName = workingName.TrimEnd('.');

            return string.IsNullOrWhiteSpace(workingName) ? "Bank Transaction" : workingName;
        }

        private string InferCategoryFromKeywords(string merchant, string text)
        {
            var combinedScope = $"{merchant} {text}".ToLowerInvariant();
            foreach (var rule in CategoryKeywordMatrix)
            {
                string pattern = $@"\b{Regex.Escape(rule.Key.ToLowerInvariant())}\b";

                if (Regex.IsMatch(combinedScope, pattern))
                    return rule.Value;
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