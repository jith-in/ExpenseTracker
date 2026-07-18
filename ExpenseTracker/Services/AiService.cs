using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ExpenseTracker.Models;

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
            // 🎯 FIX: Validate your live key status directly against the tracking value inside Secrets.cs
            if (string.IsNullOrEmpty(Secrets.GeminiApiKey) ||
                Secrets.GeminiApiKey == "YOUR_API_KEY_GOES_HERE" ||
                Secrets.GeminiApiKey == "AI_STUDIO_KEY_HERE")
            {
                throw new InvalidOperationException("Please verify that your active Gemini API key is correctly inserted inside Secrets.cs.");
            }

            // 1. Format the data records safely into a clean text list for the model prompt context
            var transactionsData = pendingExpenses.Select(e => new
            {
                id = e.Id,
                smsText = e.Note
            }).ToList();

            string dataContextJson = JsonSerializer.Serialize(transactionsData);

            string systemPrompt = "You are an expert personal finance parsing assistant. Analyze the provided array of text messages. " +
                                  "Extract the transaction components, clean up cryptic merchant names into readable notes (e.g., convert 'VPS*LULU INTE' to 'Lulu Hypermarket'), " +
                                  "and assign one exact category: [Food, Groceries, Shopping, Fuel, Bills, Travel, Income, Entertainment, Miscellaneous]. " +
                                  "Maintain the matching numerical 'id' identifier for each record.";

            string userPrompt = $"Process this JSON array of raw transaction messages:\n{dataContextJson}";

            // 2. Build the exact payload schema signature requested by the Google REST API specification
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
                    ResponseMimeType = "application/json", // Force JSON mode constraint engine
                    ResponseSchema = GetStructuredOutputSchema() // Set structural array schemas
                }
            };
            System.Diagnostics.Debug.WriteLine($"[Gemini API Request] Transmitting batch payload string: {dataContextJson}");
            // 3. Dispatch payload out to the server endpoints using our newly defined instance field
            var response = await _httpClient.PostAsJsonAsync(_geminiUrl, requestBody);
            if (!response.IsSuccessStatusCode)
            {
                string errorLog = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Gemini API Error ({response.StatusCode}): {errorLog}");
            }

            var geminiEnvelope = await response.Content.ReadFromJsonAsync<GeminiApiResponseEnvelope>();

            // 4. Extract the nested string result from Google's response object
            string? innerJsonResult = geminiEnvelope?.Candidates?.FirstOrDefault()?
                                            .Content?.Parts?.FirstOrDefault()?.Text;
            System.Diagnostics.Debug.WriteLine($"[Gemini API Response] Raw Structured JSON Received:\n{innerJsonResult}");
            if (string.IsNullOrWhiteSpace(innerJsonResult))
            {
                throw new JsonException("The model generated an empty or structurally invalid parsing candidate response framework.");
            }

            // 5. Deserialize the inner clean JSON string block directly into your C# domain classes
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
                    processedTransactions = new
                    {
                        type = "ARRAY",
                        description = "Array containing processed and categorized transaction details matching the source entries.",
                        items = new
                        {
                            type = "OBJECT",
                            properties = new
                            {
                                id = new { type = "INTEGER", description = "The exact unique matching integer database row identifier tracking identity back." },
                                amount = new { type = "NUMBER", description = "Pure floating point absolute monetary transactional value parsed out clean." },
                                category = new { type = "STRING", description = "Assigned transactional asset grouping." },
                                transactionType = new { type = "STRING", description = "Must evaluate strictly to 'Credit' or 'Debit'." },
                                note = new { type = "STRING", description = "Cleaned up highly readable human merchant naming label context description string." }
                            },
                            required = new[] { "id", "amount", "category", "transactionType", "note" }
                        }
                    }
                },
                required = new[] { "processedTransactions" }
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