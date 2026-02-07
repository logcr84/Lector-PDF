using Backend.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using System.Text.RegularExpressions;
using System.Text;

namespace Backend.Services
{
    public interface IPdfParserService
    {
        List<Remate> ParsePdf(Stream pdfStream);
    }

    public class PdfParserService : IPdfParserService
    {
        // Spanish number mappings for date parsing
        private static readonly Dictionary<string, int> SpanishNumbers = new()
        {
            {"cero", 0}, {"un", 1}, {"una", 1}, {"uno", 1}, {"dos", 2}, {"tres", 3}, {"cuatro", 4},
            {"cinco", 5}, {"seis", 6}, {"siete", 7}, {"ocho", 8}, {"nueve", 9}, {"diez", 10},
            {"once", 11}, {"doce", 12}, {"trece", 13}, {"catorce", 14}, {"quince", 15},
            {"dieciséis", 16}, {"dieciseis", 16}, {"diecisiete", 17}, {"dieciocho", 18},
            {"diecinueve", 19}, {"veinte", 20}, {"veintiuno", 21}, {"veintidós", 22}, {"veintidos", 22},
            {"veintitrés", 23}, {"veintitres", 23}, {"veinticuatro", 24}, {"veinticinco", 25},
            {"veintiséis", 26}, {"veintiseis", 26}, {"veintisiete", 27}, {"veintiocho", 28},
            {"veintinueve", 29}, {"treinta", 30}, {"treinta y uno", 31}
        };

        private static readonly Dictionary<string, int> SpanishMonths = new()
        {
            {"enero", 1}, {"febrero", 2}, {"marzo", 3}, {"abril", 4}, {"mayo", 5}, {"junio", 6},
            {"julio", 7}, {"agosto", 8}, {"septiembre", 9}, {"setiembre", 9},
            {"octubre", 10}, {"noviembre", 11}, {"diciembre", 12}
        };

        /// <summary>
        /// Parses Spanish verbose date text into formatted date string.
        /// Example: "catorce horas treinta minutos del tres de febrero de dos mil veintiséis" 
        /// → "03/02/2026 14:30"
        /// </summary>
        private string ParseSpanishDate(string rawDateText)
        {
            try
            {
                var text = rawDateText.ToLower().Trim();

                // Extract hours (e.g., "catorce horas" → 14)
                int hours = 0;
                var hoursMatch = Regex.Match(text, @"([\wáéíóúñ\s]+)\s+horas?", RegexOptions.IgnoreCase);
                if (hoursMatch.Success)
                {
                    var hourText = hoursMatch.Groups[1].Value.Trim();
                    hours = ParseSpanishNumber(hourText);
                }

                // Extract minutes (e.g., "treinta minutos" → 30)
                int minutes = 0;
                var minutesMatch = Regex.Match(text, @"([\wáéíóúñ\s]+)\s+minutos?", RegexOptions.IgnoreCase);
                if (minutesMatch.Success)
                {
                    var minuteText = minutesMatch.Groups[1].Value.Trim();
                    minutes = ParseSpanishNumber(minuteText);
                }

                // Extract day (e.g., "tres de febrero" → 3)
                int day = 0;
                var dayMatch = Regex.Match(text, @"del?\s+([\wáéíóúñ\s]+?)\s+de\s+([\wáéíóú]+)", RegexOptions.IgnoreCase);
                if (dayMatch.Success)
                {
                    var dayText = dayMatch.Groups[1].Value.Trim();
                    day = ParseSpanishNumber(dayText);
                }

                // Extract month (e.g., "de febrero de" → 2)
                int month = 0;
                if (dayMatch.Success)
                {
                    var monthText = dayMatch.Groups[2].Value.Trim();
                    if (SpanishMonths.TryGetValue(monthText, out int m))
                    {
                        month = m;
                    }
                }

                // Extract year (e.g., "dos mil veintiséis" → 2026)
                int year = 0;
                var yearMatch = Regex.Match(text, @"de\s+(dos\s+mil[\wáéíóúñ\s]*)", RegexOptions.IgnoreCase);
                if (yearMatch.Success)
                {
                    var yearText = yearMatch.Groups[1].Value.Trim();
                    year = ParseSpanishYear(yearText);
                }

                // Validate all components were extracted
                if (day > 0 && month > 0 && year > 0)
                {
                    var timeStr = hours > 0 || minutes > 0 ? $" {hours:D2}:{minutes:D2}" : "";
                    return $"{day:D2}/{month:D2}/{year}{timeStr}";
                }

                // Fallback: return original text if parsing incomplete
                return rawDateText;
            }
            catch
            {
                // On any error, return original text
                return rawDateText;
            }
        }

        /// <summary>
        /// Parses Spanish number words to integers
        /// </summary>
        private int ParseSpanishNumber(string numberText)
        {
            numberText = numberText.ToLower().Trim();

            // Try direct lookup
            if (SpanishNumbers.TryGetValue(numberText, out int value))
            {
                return value;
            }

            // Handle "treinta y uno" style
            if (numberText.Contains(" y "))
            {
                var parts = numberText.Split(" y ");
                if (parts.Length == 2 &&
                    SpanishNumbers.TryGetValue(parts[0].Trim(), out int tens) &&
                    SpanishNumbers.TryGetValue(parts[1].Trim(), out int ones))
                {
                    return tens + ones;
                }
            }

            // Try parsing as numeric
            if (int.TryParse(numberText, out int num))
            {
                return num;
            }

            return 0;
        }

        /// <summary>
        /// Parses Spanish year text (e.g., "dos mil veintiséis" → 2026)
        /// </summary>
        private int ParseSpanishYear(string yearText)
        {
            yearText = yearText.ToLower().Trim();

            // Handle "dos mil XXX" pattern
            if (yearText.StartsWith("dos mil"))
            {
                var remaining = yearText.Replace("dos mil", "").Trim();

                if (string.IsNullOrEmpty(remaining))
                {
                    return 2000;
                }

                var lastTwo = ParseSpanishNumber(remaining);
                return 2000 + lastTwo;
            }

            // Try parsing as numeric
            if (int.TryParse(yearText, out int year))
            {
                return year;
            }

            return 0;
        }

        public List<Remate> ParsePdf(Stream pdfStream)
        {
            var remates = new List<Remate>();

            try
            {
                using var document = PdfDocument.Open(pdfStream);
                var fullTextBuilder = new StringBuilder();
                foreach (var page in document.GetPages())
                {
                    // Basic text extraction; layout analysis might be needed for complex columns
                    // For now, we append text line by line.
                    fullTextBuilder.AppendLine(page.Text);
                }

                string fullText = fullTextBuilder.ToString();

                // Strategy adapted from parrafo.py:
                // Find blocks starting with "En este Despacho" and ending with "publicación número:" or similar metadata.
                // Since PDF text might not be perfectly paragraphed like HTML <p> tags, we'll try to split by known distinct markers or just scan through.

                // Let's normalize spaces first
                fullText = Regex.Replace(fullText, @"\s+", " ");

                // We split by "En este Despacho" to get candidate blocks
                var rawBlocks = fullText.Split(new[] { "En este Despacho" }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var rawBlock in rawBlocks)
                {
                    // Validate if it looks like a valid edict (too short might be noise)
                    if (rawBlock.Length < 50) continue;

                    var remate = new Remate();
                    // Reconstruct context slightly
                    remate.TextoOriginal = "En este Despacho " + rawBlock.Trim();

                    var blockText = rawBlock;

                    // Note: parrafo.py accumulates text until "publicación número" or "Referencia N°".
                    // The Split approach implicitly grabs everything until the next "En este Despacho". 
                    // To be more precise we could truncate at "publicación número" if present to avoid trailing garbage.
                    var endMarkerMatch = Regex.Match(blockText, @"(publicaci[óo]n n[úu]mero:|Referencia N°)", RegexOptions.IgnoreCase);
                    if (endMarkerMatch.Success)
                    {
                        // Keep text only up to the marker + some chars? 
                        // parrafo.py includes the marker line.
                        blockText = blockText.Substring(0, endMarkerMatch.Index + endMarkerMatch.Length + 20); // +20 to capture the number
                    }

                    // --- Extraction Logic ---

                    // 1. Tipo (Vehículo / Propiedad)
                    // Simple heuristic keywords
                    if (Regex.IsMatch(blockText, @"\b(veh[íi]culo|carro|moto|bus|camion|placa)\b", RegexOptions.IgnoreCase))
                    {
                        remate.Tipo = "Vehiculo";
                    }
                    else if (Regex.IsMatch(blockText, @"\b(finca|lote|terreno|propiedad|inmueble)\b", RegexOptions.IgnoreCase))
                    {
                        remate.Tipo = "Propiedad";
                    }
                    else
                    {
                        // Heuristic: default to Propiedad if unsure, or check if it mentions 'Area'
                        remate.Tipo = "Propiedad";
                    }

                    // 2. Expediente
                    // Scan for pattern like 25-006327- or similar
                    var expedienteMatch = Regex.Match(blockText, @"(?:Expediente|EXP|autos?)\s*[:Nn°]*\s*(\d+[\d\-]+[\w]*)", RegexOptions.IgnoreCase);
                    if (expedienteMatch.Success)
                    {
                        remate.Expediente = expedienteMatch.Groups[1].Value;
                    }

                    // 3. Precio Base
                    // "base de X colones" or symbols.
                    var precioMatch = Regex.Match(blockText, @"(?:base de|valor de|precio)\s*([₡$¢]\s?[\d,.]+)");
                    if (precioMatch.Success)
                    {
                        remate.PrecioBaseDisplay = precioMatch.Groups[1].Value;
                    }
                    else
                    {
                        // Fallback: look for just currency
                        var simplePrice = Regex.Match(blockText, @"[₡$¢]\s?[\d,.]+");
                        if (simplePrice.Success) remate.PrecioBaseDisplay = simplePrice.Value;
                    }

                    // 4. Titulo (Resumen)
                    if (remate.Tipo == "Vehiculo")
                    {
                        // Try to find Make/Model: "Marca: ... Estilo: ..."
                        var marcaMatch = Regex.Match(blockText, @"Marca[:\s]+(\w+)", RegexOptions.IgnoreCase);
                        var estiloMatch = Regex.Match(blockText, @"Estilo[:\s]+(\w+)", RegexOptions.IgnoreCase);
                        var marca = marcaMatch.Success ? marcaMatch.Groups[1].Value : "Vehículo";
                        var estilo = estiloMatch.Success ? estiloMatch.Groups[1].Value : "";
                        remate.Titulo = $"{marca} {estilo}".Trim();
                    }
                    else
                    {
                        // Propiedad: Try to find location "Distrito ... Cantón ..."
                        var locMatch = Regex.Match(blockText, @"Distrito\s+([\w\s]+?)[,.]\s*Cant[óo]n\s+([\w\s]+)", RegexOptions.IgnoreCase);
                        if (locMatch.Success)
                        {
                            remate.Titulo = $"Distrito {locMatch.Groups[1].Value.Trim()}, Cantón {locMatch.Groups[2].Value.Trim()}";
                        }
                        else
                        {
                            // Fallback title to something from text
                            remate.Titulo = "Propiedad en Remate";
                        }
                    }

                    // 5. Dates (Remates)
                    // Look for "señalan las..."
                    var dateMatches = Regex.Matches(blockText, @"señalan las\s+([^\.]+?)(?:\.|;|,)", RegexOptions.IgnoreCase);
                    int count = 1;
                    foreach (Match dm in dateMatches)
                    {
                        if (count > 3) break;

                        var rawDate = dm.Groups[1].Value.Trim();
                        var formattedDate = ParseSpanishDate(rawDate);

                        remate.Remates.Add(new RemateFecha
                        {
                            Label = $"{count}° Remate",
                            Fecha = formattedDate,
                            PrecioDisplay = count == 1 ? remate.PrecioBaseDisplay : (count == 2 ? "75% Base" : "25% Base")
                        });
                        count++;
                    }

                    // Only add if we have at least some meaningful data
                    bool hasExpediente = !string.IsNullOrWhiteSpace(remate.Expediente);
                    bool hasPrecio = !string.IsNullOrWhiteSpace(remate.PrecioBaseDisplay);
                    bool hasValidTitle = !string.IsNullOrWhiteSpace(remate.Titulo) && remate.Titulo != "Propiedad en Remate";
                    bool hasDates = remate.Remates.Count > 0;

                    if (hasExpediente || hasPrecio || hasValidTitle || hasDates)
                    {
                        remates.Add(remate);
                        Console.WriteLine($"✓ Extracted remate: {remate.Titulo} (Expediente: {remate.Expediente ?? "N/A"})");
                    }
                    else
                    {
                        Console.WriteLine($"⚠ Skipped incomplete block (no critical fields found)");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error parsing PDF: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }


            if (remates.Count == 0)
            {
                Console.WriteLine($"⚠ No valid remate data found in PDF");
            }
            else
            {
                Console.WriteLine($"✓ Successfully extracted {remates.Count} remate(s) from PDF");
            }

            return remates;
        }
    }
}
