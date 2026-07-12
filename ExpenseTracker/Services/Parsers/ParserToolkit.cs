using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Diagnostics;
namespace ExpenseTracker.Services.Parsers
{
    public static class ParserToolkit
    {
        public static decimal ExtractAmount(string body)
        {
            // Layer 3: Comprehensive currency evaluation map (Supports Rs, Rs., INR, ₹ with or without spacing and Lakhs formatting)
            var match = Regex.Match(body, @"(?:Rs\.?|INR|₹)\s*([0-9]+(?:,[0-9]{2,3})*(?:\.[0-9]{1,2})?)", RegexOptions.IgnoreCase);
            if (!match.Success) return 0;

            var rawValue = match.Groups[1].Value.Replace(",", "");
            return decimal.TryParse(rawValue, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsedValue) ? parsedValue : 0;
        }

        public static string ClassifyTransactionType(string body, out string refinedSubtype)
        {
            var text = body.ToLowerInvariant();

            // 1. Explicit Credit Classification Tree
            if (text.Contains("salary")) { refinedSubtype = "Salary"; return "Credit"; }
            if (text.Contains("interest")) { refinedSubtype = "Interest"; return "Credit"; }
            if (text.Contains("refund")) { refinedSubtype = "Refund"; return "Credit"; }
            if (text.Contains("cashback")) { refinedSubtype = "Cashback"; return "Credit"; }
            if (text.Contains("dividend")) { refinedSubtype = "Dividend"; return "Credit"; }
            if (text.Contains("credited") || text.Contains("received")) { refinedSubtype = "Transfer In"; return "Credit"; }

            // 2. Explicit Debit Classification Tree
            if (text.Contains("cash wdl") || text.Contains("withdrawn at atm") || text.Contains("atm withdrawal")) { refinedSubtype = "Cash Withdrawal"; return "Debit"; }
            if (text.Contains("debited") || text.Contains("spent") || text.Contains("paid") || text.Contains("transferred")) { refinedSubtype = "Debit"; return "Debit"; }

            refinedSubtype = "Unknown";
            return "Unknown";
        }

        public static string ParseChannelFormat(string body, string defaultChannelType)
        {
            var text = body.ToLowerInvariant();

            // Layer 4: Deep channel tracking matrix
            if (Regex.IsMatch(text, @"\b(upi|vpa|gpay|phonepe|paytm|okicici)\b")) return "UPI";
            if (Regex.IsMatch(text, @"\b(credit card|crd card|cc)\b")) return "Credit Card";
            if (Regex.IsMatch(text, @"\b(debit card|dc|pos|card ending)\b")) return "Debit Card";
            if (text.Contains("imps")) return "IMPS";
            if (text.Contains("neft")) return "NEFT";
            if (text.Contains("rtgs")) return "RTGS";
            if (text.Contains("fastag")) return "FASTag";
            if (text.Contains("nach") || text.Contains("ach")) return "ACH/NACH";
            if (text.Contains("si due") || text.Contains("standing instruction") || text.Contains("autopay")) return "Standing Instruction";
            if (text.Contains("cheque") || text.Contains("chq")) return "Cheque";
            if (text.Contains("wallet")) return "Wallet";

            return defaultChannelType; // Fallback to bank-specific default profile channel layout
        }

        public static DateTime ExtractDate(string body)
        {
            var dateRegex = new Regex(@"(\d{1,2}[-/ ](?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[-/ ]\d{2,4}|\d{1,2}[-/]\d{1,2}[-/]\d{2,4})", RegexOptions.IgnoreCase);
            var match = dateRegex.Match(body);

            if (match.Success)
            {
                if (DateTime.TryParse(match.Value, out var parsedDate))
                {
                    return parsedDate;
                }
                Debug.WriteLine($"PARSER: Matched '{match.Value}' but failed to parse to date.");
            }
            else
            {
                // THIS IS LIKELY HAPPENING TO YOU
                Debug.WriteLine($"PARSER: No date pattern matched in SMS: {body}");
            }

            return DateTime.Today; // Fallback
        }
    }
}