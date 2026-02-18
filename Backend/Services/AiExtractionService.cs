using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Backend.Models;
using Microsoft.Extensions.Configuration;

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
            // Validating API Key availability
            if (string.IsNullOrEmpty(_apiKey))
            {
                return null;
            }

            var prompt = @"
            You are a legal data extraction assistant. Analyze the following judicial auction edict text and extract structured data.
            
            CRITICAL: ALL FIELDS ARE REQUIRED. DO NOT OMIT ANY FIELD.
            If a value is not explicit, INFER IT from the context. Only use 'N/A' if absolutely impossible to determine.

            Fields to extract:
            - Expediente: File number (e.g. 21-000123-1207-CJ). REQUIRED.
            - Tipo: ""Vehiculo"" or ""Propiedad"". REQUIRED.
            - Titulo: Short title (e.g. ""Casa en San José"" or ""Toyota Corolla 2015""). REQUIRED.
            - Demandado: Defendant name(s). REQUIRED.
            - Juzgado: Court name. REQUIRED.
            - Area: Property size in sqm/hectares (e.g. ""154 m2"") or ""N/A"" for vehicles. REQUIRED.
            - Fechas: Array of auction dates (1st, 2nd, 3rd). Extract Date, Time, and Base Price for each. REQUIRED.
            - Detalles: A simple key-value object with specific details found. REQUIRED.
                For Vehicles: Placa, Marca, Modelo, Estilo, Color, Motor, Serie, VIN
                For Properties: Matricula (Finca ID), Naturaleza, Ubicacion, Colindantes, Gravamenes, Plano

            Return ONLY valid JSON in this format:
            {
                ""expediente"": ""string"",
                ""tipo"": ""Vehiculo"" or ""Propiedad"",
                ""titulo"": ""string"",
                ""demandado"": ""string"",
                ""juzgado"": ""string"",
                ""area"": ""string"",
                ""fechas"": [
                    { ""orden"": 1, ""fecha"": ""YYYY-MM-DD HH:MM"", ""precioBase"": 0.00 },
                    { ""orden"": 2, ""fecha"": ""YYYY-MM-DD HH:MM"", ""precioBase"": 0.00 },
                    { ""orden"": 3, ""fecha"": ""YYYY-MM-DD HH:MM"", ""precioBase"": 0.00 }
                ],
                ""detalles"": {
                    ""matricula"": ""string"",
                    ""placa"": ""string"",
                    ""marca"": ""string"",
                    ""modelo"": ""string"",
                    ""color"": ""string"",
                    ""ubicacion"": ""string"",
                    ""naturaleza"": ""string""
                }
            }

            Text:
            " + text;

            Console.WriteLine($"\n--- GEMINI INPUT TEXT ({text.Length} chars) ---");
            Console.WriteLine(text.Substring(Math.Max(0, text.Length - 200))); // Show last 200 chars to check for cutoff
            Console.WriteLine("---------------------------------\n");

            var requestBody = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = prompt } } }
                },
                generationConfig = new
                {
                    temperature = 0.1,
                    responseMimeType = "application/json"
                }
            };

            var url = $"{GeminiUrl}?key={_apiKey}";
            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync(url, jsonContent);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(responseString);
                var candidates = doc.RootElement.GetProperty("candidates");
                if (candidates.GetArrayLength() == 0) return null;

                var content = candidates[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();

                if (string.IsNullOrEmpty(content)) return null;

                // Sanitize content: remove markdown formatting if present
                content = content.Replace("```json", "").Replace("```", "").Trim();

                Console.WriteLine("\n--- GEMINI JSON RESPONSE ---");
                Console.WriteLine(content);
                Console.WriteLine("----------------------------\n");

                var extracted = JsonSerializer.Deserialize<AiRemateDto>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (extracted == null) return null;

                var remate = new Remate
                {
                    Expediente = extracted.Expediente ?? "",
                    Tipo = extracted.Tipo ?? "Desconocido",
                    Titulo = extracted.Titulo ?? "Remate AI",
                    Demandado = extracted.Demandado ?? "",
                    Juzgado = extracted.Juzgado ?? "",
                    Area = extracted.Area ?? "",
                    Detalles = new Dictionary<string, string>()
                };

                // Map Dates
                if (extracted.Fechas != null && extracted.Fechas.Count > 0)
                {
                    remate.Remates = new List<RemateFecha>();
                    foreach (var fecha in extracted.Fechas)
                    {
                        remate.Remates.Add(new RemateFecha
                        {
                            Label = $"{fecha.Orden}° Remate",
                            Fecha = fecha.Fecha ?? "N/A",
                            Precio = fecha.PrecioBase,
                            PrecioDisplay = $"⚡ {fecha.PrecioBase:N2}"
                        });

                        // Set main base price from first auction
                        if (fecha.Orden == 1)
                        {
                            remate.PrecioBase = fecha.PrecioBase;
                            remate.PrecioBaseDisplay = $"⚡ {fecha.PrecioBase:N2}";
                        }
                    }
                }

                // Map dictionary
                if (extracted.Detalles != null)
                {
                    foreach (var kvp in extracted.Detalles)
                    {
                        if (!string.IsNullOrEmpty(kvp.Value))
                        {
                            remate.Detalles[kvp.Key] = kvp.Value;
                        }
                    }

                    // Specific fix for UI keys
                    if (extracted.Detalles.ContainsKey("matricula")) remate.Detalles["Matricula"] = extracted.Detalles["matricula"];
                    if (extracted.Detalles.ContainsKey("placa")) remate.Detalles["Placa"] = extracted.Detalles["placa"];
                }

                // If type is unknown but we have data
                if (remate.Tipo == "Desconocido")
                {
                    if (remate.Detalles.ContainsKey("Placa")) remate.Tipo = "Vehiculo";
                    else if (remate.Detalles.ContainsKey("Matricula")) remate.Tipo = "Propiedad";
                }

                return remate;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calling Gemini: {ex.Message}");
                return null;
            }
        }

        private class AiRemateDto
        {
            public string? Expediente { get; set; }
            public string? Tipo { get; set; }
            public string? Titulo { get; set; }
            public string? Demandado { get; set; }
            public string? Juzgado { get; set; }
            public string? Area { get; set; }
            public List<AiFechaDto>? Fechas { get; set; }
            public Dictionary<string, string>? Detalles { get; set; }
        }

        private class AiFechaDto
        {
            public int Orden { get; set; }
            public string? Fecha { get; set; }
            public decimal PrecioBase { get; set; }
        }
    }
}
