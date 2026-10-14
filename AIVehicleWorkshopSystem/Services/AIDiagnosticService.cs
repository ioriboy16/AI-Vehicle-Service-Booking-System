using AIVehicleWorkshopSystem.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AIVehicleWorkshopSystem.Services
{
    public class AIDiagnosticService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public AIDiagnosticService(HttpClient httpClient)
        {
            _httpClient = httpClient;

            _apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                ?? throw new InvalidOperationException(
                    "GEMINI_API_KEY environment variable is not configured.");
        }

        public async Task<AIDiagnosticResult> AnalyseVehicleAsync(
            Vehicle vehicle,
            string symptoms,
            string serviceHistory)
        {
            var prompt = $"""
                You are an AI assistant for a vehicle workshop management system.

                Your role is to provide an AI-assisted diagnostic recommendation
                based on the customer's reported symptoms, vehicle information,
                and available service history.

                VEHICLE INFORMATION
                Brand: {vehicle.Brand}
                Model: {vehicle.Model}
                Manufacturing Year: {vehicle.ManufacturingYear}
                Current Mileage: {vehicle.CurrentMileage?.ToString() ?? "Not provided"}

                CUSTOMER-REPORTED SYMPTOMS
                {symptoms}

                RELEVANT SERVICE AND REPAIR HISTORY
                {serviceHistory}

                Provide:
                1. A possible issue based on the information provided.
                2. Recommended inspection steps for a qualified mechanic.
                3. A suitable maintenance suggestion.
                4. Priority level: Low, Medium, or High.

                Important:
                - This is an AI-assisted recommendation, not a confirmed diagnosis.
                - Do not claim certainty.
                - A qualified mechanic must physically inspect the vehicle.
                - Keep the response concise and useful for a vehicle workshop.
                """;

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new
                            {
                                text = prompt
                            }
                        }
                    }
                },

                generationConfig = new
                {
                    responseMimeType = "application/json",

                    responseSchema = new
                    {
                        type = "OBJECT",

                        properties = new
                        {
                            possibleIssue = new
                            {
                                type = "STRING"
                            },

                            recommendedInspection = new
                            {
                                type = "STRING"
                            },

                            maintenanceSuggestion = new
                            {
                                type = "STRING"
                            },

                            priority = new
                            {
                                type = "STRING",
                                @enum = new[]
                                {
                                    "Low",
                                    "Medium",
                                    "High"
                                }
                            }
                        },

                        required = new[]
                        {
                            "possibleIssue",
                            "recommendedInspection",
                            "maintenanceSuggestion",
                            "priority"
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.6-flash:generateContent");

            request.Headers.Add("x-goog-api-key", _apiKey);

            request.Content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.SendAsync(request);

            var responseContent =
                await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Gemini API request failed: " +
                    $"{response.StatusCode}. {responseContent}");
            }

            using var document =
                JsonDocument.Parse(responseContent);

            var root = document.RootElement;

            if (!root.TryGetProperty("candidates", out var candidates) ||
                candidates.GetArrayLength() == 0)
            {
                throw new InvalidOperationException(
                    "Gemini API returned no diagnostic result.");
            }

            var candidate = candidates[0];

            var aiText = candidate
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrWhiteSpace(aiText))
            {
                throw new InvalidOperationException(
                    "Gemini API returned an empty diagnostic result.");
            }

            var result =
                JsonSerializer.Deserialize<AIDiagnosticResult>(
                    aiText,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            if (result == null)
            {
                throw new InvalidOperationException(
                    "Unable to read the AI diagnostic result.");
            }

            return result;
        }
    }

    public class AIDiagnosticResult
    {
        public string PossibleIssue { get; set; } = string.Empty;

        public string RecommendedInspection { get; set; } =
            string.Empty;

        public string MaintenanceSuggestion { get; set; } =
            string.Empty;

        public string Priority { get; set; } = string.Empty;
    }
}