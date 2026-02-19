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
        // Gemini 2.0 Flash endpoint (updated from deprecated 1.5-flash)
        private const string GeminiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent";

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

            string prompt = @"
            You are a legal data extraction assistant. Analyze the following judicial auction edict text and extract structured data.
            
            Instructions:
            1. Identify the TYPE of asset (Vehiculo vs Propiedad).
            2. Extract all relevant details for that type.
            3. If a value is not explicit, INFER IT from the context.
            4. Return 'N/A' only if the field is applicable but the value is missing.
            5. OMIT fields that are NOT applicable to the specific type (e.g. do not include 'Placa' for a Property).

            Fields to focus on:
            - Expediente: File number (e.g. 21-000123-1207-CJ). REQUIRED.
            - Tipo: ""Vehiculo"" or ""Propiedad"". REQUIRED.
            - Titulo: Short title (e.g. ""Casa en San José"" or ""Toyota Corolla 2015""). REQUIRED.
            - Demandado: Defendant name(s). REQUIRED.
            - Juzgado: Court name. REQUIRED.
            - Area: Property size (e.g. ""154 m2""); for vehicles, omit or use ""N/A"".
            - Fechas: Array of auction dates (1st, 2nd, 3rd). Extract Date, Time, and Base Price.
              *IMPORTANT*: If text says ""base: avalúo"", search for the monetary value. 
              ALWAYS try to find a numeric Base Price.

            - Detalles: Key-value object with specific details found. 
                For Vehicles: Placa, Marca, Modelo, Estilo, Color, Motor, Serie, VIN, Traccion, Carroceria, Capacidad, Peso, Categoria, Anio, Chasis, Cilindrada, Combustible
                For Properties: Matricula (Finca ID), Naturaleza, Ubicacion, Colindantes, Gravamenes, Plano

            Return ONLY valid JSON in this format:
            {
                ""expediente"": ""string"",
                ""tipo"": ""Vehiculo"", 
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
                    ""naturaleza"": ""string"",
                    ""gravamenes"": ""string"",
                    ""colindantes"": ""string"",
                    ""demandante"": ""string"",
                    ""medida"": ""string"",
                    ""traccion"": ""string"",
                    ""carroceria"": ""string"",
                    ""capacidad"": ""string"",
                    ""peso"": ""string"",
                    ""categoria"": ""string"",
                    ""anio"": ""string"",
                    ""chasis"": ""string"",
                    ""cilindrada"": ""string"",
                    ""combustible"": ""string""
                }
            }
            
            Text:
             " + text;

            Console.WriteLine($"\n--- GEMINI INPUT TEXT ({text.Length} chars) ---");
            Console.WriteLine(text.AsSpan(Math.Max(0, text.Length - 200))); // Show last 200 chars to check for cutoff
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

                    // Normalize VIN/Serie
                    if (extracted.Detalles.ContainsKey("serie")) remate.Detalles["Serie"] = extracted.Detalles["serie"];
                    if (extracted.Detalles.ContainsKey("vin")) remate.Detalles["Serie"] = extracted.Detalles["vin"];

                    // Normalize Motor
                    if (extracted.Detalles.ContainsKey("numero de motor")) remate.Detalles["Motor"] = extracted.Detalles["numero de motor"];
                    if (extracted.Detalles.ContainsKey("num motor")) remate.Detalles["Motor"] = extracted.Detalles["num motor"];
                    if (extracted.Detalles.ContainsKey("motor")) remate.Detalles["Motor"] = extracted.Detalles["motor"];

                    // Normalize other common keys just in case
                    if (extracted.Detalles.ContainsKey("color")) remate.Detalles["Color"] = extracted.Detalles["color"];
                    if (extracted.Detalles.ContainsKey("estilo")) remate.Detalles["Estilo"] = extracted.Detalles["estilo"];
                    if (extracted.Detalles.ContainsKey("modelo")) remate.Detalles["Modelo"] = extracted.Detalles["modelo"];
                    if (extracted.Detalles.ContainsKey("marca")) remate.Detalles["Marca"] = extracted.Detalles["marca"];

                    // Normalize extended vehicle details
                    if (extracted.Detalles.ContainsKey("traccion")) remate.Detalles["Traccion"] = extracted.Detalles["traccion"];
                    if (extracted.Detalles.ContainsKey("carroceria")) remate.Detalles["Carroceria"] = extracted.Detalles["carroceria"];
                    if (extracted.Detalles.ContainsKey("capacidad")) remate.Detalles["Capacidad"] = extracted.Detalles["capacidad"];
                    if (extracted.Detalles.ContainsKey("peso")) remate.Detalles["Peso"] = extracted.Detalles["peso"];

                    // Normalize new fields (Category, Year, Chasis, Cilindrada)
                    if (extracted.Detalles.ContainsKey("categoria")) remate.Detalles["Categoria"] = extracted.Detalles["categoria"];
                    if (extracted.Detalles.ContainsKey("anio")) remate.Detalles["Anio"] = extracted.Detalles["anio"];
                    if (extracted.Detalles.ContainsKey("chasis")) remate.Detalles["Chasis"] = extracted.Detalles["chasis"];
                    if (extracted.Detalles.ContainsKey("cilindrada")) remate.Detalles["Cilindrada"] = extracted.Detalles["cilindrada"];
                    if (extracted.Detalles.ContainsKey("combustible")) remate.Detalles["Combustible"] = extracted.Detalles["combustible"];

                    // Normalize property-specific fields
                    if (extracted.Detalles.ContainsKey("gravamenes") && !string.IsNullOrEmpty(extracted.Detalles["gravamenes"]))
                        remate.Detalles["Gravamenes"] = extracted.Detalles["gravamenes"];
                    if (extracted.Detalles.ContainsKey("colindantes") && !string.IsNullOrEmpty(extracted.Detalles["colindantes"]))
                        remate.Detalles["Colindantes"] = extracted.Detalles["colindantes"];
                    if (extracted.Detalles.ContainsKey("demandante") && !string.IsNullOrEmpty(extracted.Detalles["demandante"]))
                        remate.Detalles["Demandante"] = extracted.Detalles["demandante"];
                    if (extracted.Detalles.ContainsKey("medida") && !string.IsNullOrEmpty(extracted.Detalles["medida"]))
                        remate.Detalles["Medida"] = extracted.Detalles["medida"];
                    if (extracted.Detalles.ContainsKey("ubicacion") && !string.IsNullOrEmpty(extracted.Detalles["ubicacion"]))
                        remate.Detalles["Ubicacion"] = extracted.Detalles["ubicacion"];
                    if (extracted.Detalles.ContainsKey("naturaleza") && !string.IsNullOrEmpty(extracted.Detalles["naturaleza"]))
                        remate.Detalles["Naturaleza"] = extracted.Detalles["naturaleza"];
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
