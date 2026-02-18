using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Backend.Models;

namespace Backend.Services
{
    public class AiExtractionService : IAiExtractionService
    {
        private readonly HttpClient _httpClient;
        private readonly string? _apiKey;
        // Gemini 1.5 Flash endpoint
        private const string GeminiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent";

        public AiExtractionService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["GoogleAi:ApiKey"];
        }

        public async Task<Remate?> ExtractMissingFieldsAsync(string text)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                return null;
            }

            var prompt = @"
            You are a data extraction assistant. Analyze the following judicial auction edict text and extract:
            - Expediente (File Number)
            - PrecioBase (Base Price as a decimal number, 0 if not found)
            - Tipo (Vehicle or Property)
            - Matricula (Property ID or Vehicle Plate)
            - Titulo (A short descriptive title)

            Return ONLY valid JSON in this format:
            {
                ""expediente"": ""string"",
                ""precioBase"": 0.00,
                ""tipo"": ""Vehiculo"" or ""Propiedad"",
                ""matricula"": ""string"",
                ""titulo"": ""string""
            }

            Text:
            " + text;

            // Gemini Request Format
            var requestBody = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = prompt } } }
                },
                generationConfig = new
                {
                    temperature = 0.1,
                    responseMimeType = "application/json" // Gemini supports JSON mode natively
                }
            };

            var url = $"{GeminiUrl}?key={_apiKey}";
            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(url, jsonContent);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();

                // Gemini Response Parsing
                using var doc = JsonDocument.Parse(responseString);
                var candidates = doc.RootElement.GetProperty("candidates");
                if (candidates.GetArrayLength() == 0) return null;

                var content = candidates[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();

                if (string.IsNullOrEmpty(content)) return null;

                var extracted = JsonSerializer.Deserialize<AiRemateDto>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (extracted == null) return null;

                return new Remate
                {
                    Expediente = extracted.Expediente ?? "",
                    PrecioBase = extracted.PrecioBase,
                    Tipo = extracted.Tipo ?? "Desconocido",
                    Titulo = extracted.Titulo ?? "Remate AI",
                    Detalles = new Dictionary<string, string> { { "Matricula", extracted.Matricula ?? "" } }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calling OpenAI: {ex.Message}");
                return null;
            }
        }

        private class AiRemateDto
        {
            public string? Expediente { get; set; }
            public decimal PrecioBase { get; set; }
            public string? Tipo { get; set; }
            public string? Matricula { get; set; }
            public string? Titulo { get; set; }
        }
    }
}
