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
                        remate.Remates.Add(new RemateFecha
                        {
                            Label = $"{count}° Remate",
                            Fecha = dm.Groups[1].Value.Trim(),
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
