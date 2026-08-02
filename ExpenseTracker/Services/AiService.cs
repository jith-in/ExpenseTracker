using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ExpenseTracker.Models;
using System.Diagnostics;
namespace ExpenseTracker.Services
{
    public interface IAiService
    {
        Task<BatchExpenseResponse?> ParseBatchAsync(List<Expense> pendingExpenses);
    }

    public class AiService : IAiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _geminiUrl;

        public AiService(HttpClient httpClient)
        {
            _httpClient = httpClient;

            // 🎯 FIX: Move the dynamic URL initialization safely inside the constructor execution path
            _geminiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash:generateContent?key={Secrets.GeminiApiKey}";
        }

        public async Task<BatchExpenseResponse?> ParseBatchAsync(List<Expense> pendingExpenses)
        {
            // 🎯 Step 1: Validate live API key configurations directly
            if (string.IsNullOrEmpty(Secrets.GeminiApiKey) ||
                Secrets.GeminiApiKey == "YOUR_API_KEY_GOES_HERE" ||
                Secrets.GeminiApiKey == "AI_STUDIO_KEY_HERE")
            {
                throw new InvalidOperationException("Please verify that your active Gemini API key is correctly inserted inside Secrets.cs.");
            }

            if (pendingExpenses == null || !pendingExpenses.Any())
            {
                return new BatchExpenseResponse();
            }

            // 🎯 Step 2: Format the raw data list safely into a clean JSON data context context array
            var transactionsData = pendingExpenses.Select(e => new
            {
                id = e.Id,
                smsText = e.Note
            }).ToList();

            string dataContextJson = JsonSerializer.Serialize(transactionsData);

            // 🎯 Step 3: Embed your high-precision structural prompt rules
            string systemPrompt = @"You are a high-precision financial transaction parsing and classification engine. Analyze the raw text records in the provided batch array, normalize the entities, filter out non-transactional items, and map properties.

## Allowed Categories
If is_transaction is true, evaluate the narrative context and map category into exactly one of these string tokens:
- Income: Salary credits, interest credited, peer credits.
- Utilities: Phone/Internet recharges, broadband, streaming bills.
- Food: Restaurant orders and food delivery apps.
- Investment: Long-term wealth creation instruments. Must contain clear structural phrases like 'SIP', 'Mutual Fund', 'Fund House', 'Folio No', 'NAV', 'Units Allotted', or explicit asset managers (ITIMF, SBIMF, TATA MF).
- Medical: Pharmacy, hospital bills, diagnostic labs.
- Leisure: Retail shopping, streaming platforms, fashion outlets.
- Miscellaneous: Retail jewelry savings schemes (e.g., Bhima Saving Scheme installments), generic P2P transfers with no clear intent context, ATM cash operations, or internal bank transfers.

## Operational Pipeline Rules
1. Non-Transaction Filter: If an item contains no numeric currency debit/credit information (e.g., Missed Call Alerts, Telecom spam, general operator fraud warnings, OTP tokens), set is_transaction to false and all other fields to null.
2. Language Agnosticism: If the text body is in a regional Indian language (like Malayalam), parse its behavioral intent. If it is a non-transactional awareness broadcast, mark is_transaction: false.
3. Gold Scheme Exception: Explicitly classify installments for jewelry saving programs (e.g., 'Bhima Saving Scheme') as Miscellaneous, NOT Investment.
4. Token Normalization: Maintain the exact input matching numerical 'id' for every record entry pass.";

            string userPrompt = $"Process this JSON array of raw transaction messages:\n{dataContextJson}";

            // 🎯 Step 4: Build request container mapping payload variables 
            var requestBody = new GeminiApiRequest
            {
                Contents = new List<GeminiContent>
        {
            new GeminiContent
            {
                Parts = new List<GeminiPart>
                {
                    new GeminiPart { Text = $"{systemPrompt}\n\n{userPrompt}" }
                }
            }
        },
                GenerationConfig = new GeminiGenerationConfig
                {
                    ResponseMimeType = "application/json",
                    ResponseSchema = GetStructuredOutputSchema() // Enforces properties matches at engine level
                }
            };

            Debug.WriteLine($"[Gemini API Request] Transmitting batch payload string: {dataContextJson}");

            // 🎯 Step 5: Post data parameters out to the server endpoints
            var response = await _httpClient.PostAsJsonAsync(_geminiUrl, requestBody);
            if (!response.IsSuccessStatusCode)
            {
                string errorLog = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Gemini API Error ({response.StatusCode}): {errorLog}");
            }

            var geminiEnvelope = await response.Content.ReadFromJsonAsync<GeminiApiResponseEnvelope>();

            // 🎯 Step 6: Safe parse extraction parameters out from nested string response block
            string? innerJsonResult = geminiEnvelope?.Candidates?.FirstOrDefault()?
                                        .Content?.Parts?.FirstOrDefault()?.Text;

            Debug.WriteLine($"[Gemini API Response] Raw Structured JSON Received:\n{innerJsonResult}");

            if (string.IsNullOrWhiteSpace(innerJsonResult))
            {
                throw new JsonException("The model generated an empty or structurally invalid parsing candidate response framework.");
            }

            return JsonSerializer.Deserialize<BatchExpenseResponse>(innerJsonResult);
        }

        /// <summary>
        /// Defines the structural schema matrix required by Gemini's schema validation engine.
        /// </summary>
        private object GetStructuredOutputSchema()
        {
            return new
            {
                type = "OBJECT",
                properties = new
                {
                    processed_transactions = new
                    {
                        type = "ARRAY",
                        description = "List of parsed financial records matching input ids.",
                        items = new
                        {
                            type = "OBJECT",
                            properties = new
                            {
                                id = new { type = "INTEGER", description = "The exact unique matching integer database row identifier tracking identity back." },
                                is_transaction = new { type = "BOOLEAN", description = "True if a valid financial transaction, false for spam/alerts/OTPs." },
                                transaction_id = new { type = "STRING", description = "UPI Ref, IMPS Ref, UTR, or text reference ID number if present." },
                                account_masked = new { type = "STRING", description = "Masked card or account tracking sequence information string." },
                                amount = new { type = "NUMBER", description = "The numerical transaction monetary float value parsed out clean." },
                                currency = new { type = "STRING", description = "Detected currency token code code layout, default INR." },
                                transaction_type = new { type = "STRING", description = "Must evaluate strictly to 'Debit', 'Credit', 'Due_Bill', or 'Statement'." },
                                merchant_or_entity = new { type = "STRING", description = "Cleaned up normalized highly readable business or entity title." },
                                category = new { type = "STRING", description = "Strictly assigned to: Income, Utilities, Food, Investment, Medical, Leisure, or Miscellaneous." }
                            },
                            required = new[] { "id", "is_transaction", "transaction_type", "merchant_or_entity", "category" }
                        }
                    }
                },
                required = new[] { "processed_transactions" }
            };
        }
    }

    // =================================================================
    // 🏛️ INTERNAL GOOGLE GEMINI NATIVE API CONTRACT SCHEMAS
    // =================================================================

    public class GeminiApiRequest
    {
        [JsonPropertyName("contents")]
        public List<GeminiContent> Contents { get; set; } = new();

        [JsonPropertyName("generationConfig")]
        public GeminiGenerationConfig GenerationConfig { get; set; } = new();
    }

    public class GeminiContent
    {
        [JsonPropertyName("parts")]
        public List<GeminiPart> Parts { get; set; } = new();
    }

    public class GeminiPart
    {
        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;
    }

    public class GeminiGenerationConfig
    {
        [JsonPropertyName("responseMimeType")]
        public string ResponseMimeType { get; set; } = "application/json";

        [JsonPropertyName("responseSchema")]
        public object? ResponseSchema { get; set; }
    }

    public class GeminiApiResponseEnvelope
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate>? Candidates { get; set; }
    }

    public class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent? Content { get; set; }
    }
}