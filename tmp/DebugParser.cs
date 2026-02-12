using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;

// Paste dependent classes here to be self-contained or reference them if building
namespace DebugParser
{
    class Program
    {
        static void Main(string[] args)
        {
            var parser = new PdfParserService();

            string edicto1 = @"En este Despacho, con una base de CUARENTA Y DOS MILLONES DOSCIENTOS QUINCE MIL CINCUENTA Y NUEVE COLONES
CON CUARENTA Y OCHO CÉNTIMOS (¢42.215.059,48), soportando hipoteca de primer grado citas: 2010-246052-01-0005-001 y
RESERVAS DE LEY DE AGUAS Y LEY DE CAMINOS PÚBLICOS CITAS: 416-04829-01-0297-001, sáquese a remate la finca del
partido de Alajuela, matrícula número 469228, derecho 000, para lo cual se señalan las NUEVE HORAS DEL DOCE DE FEBRERO
DEL DOS MIL VEINTISÉIS (9:00 AM DEL 12-02-2026).";

            string edicto2 = @"En este Despacho, con una base de VEINTISÉIS MIL OCHOCIENTOS SESENTA Y TRES DÓLARES EXACTOS, libre de gravámenes prendarios... sáquese a remate el vehículo PLACA: CBY660...";

            string edicto3 = @"En este Despacho, Con una base de TRECE MILLONES SESENTA MIL COLONES EXACTOS, libre de gravámenes hipotecarios... sáquese a remate la finca del partido de SAN JOSÉ...";

            Console.WriteLine("--- Testing Edicto 1 ---");
            PrintRemates(parser.ParseText(edicto1));

            Console.WriteLine("\n--- Testing Edicto 2 ---");
            PrintRemates(parser.ParseText(edicto2));

            Console.WriteLine("\n--- Testing Edicto 3 ---");
            PrintRemates(parser.ParseText(edicto3));
        }

        static void PrintRemates(List<Remate> remates)
        {
            if (remates.Count == 0) Console.WriteLine("No remates found.");
            foreach (var r in remates)
            {
                Console.WriteLine($"Tipo: {r.Tipo}");
                Console.WriteLine($"Expediente: {r.Expediente}");
                Console.WriteLine($"Precio: {r.PrecioBase} ({r.PrecioBaseDisplay})");
                Console.WriteLine($"Titulo: {r.Titulo}");
            }
        }
    }

    // MOCK MODELS
    public class Remate
    {
        public int Id { get; set; }
        public string Expediente { get; set; }
        public string Tipo { get; set; }
        public decimal PrecioBase { get; set; }
        public string PrecioBaseDisplay { get; set; }
        public string TextoOriginal { get; set; }
        public string Titulo { get; set; }
        public string Demandado { get; set; }
        public string Juzgado { get; set; }
        public string Area { get; set; }
        public Dictionary<string, string> Detalles { get; set; } = new Dictionary<string, string>();
        public List<RemateFecha> Remates { get; set; } = new List<RemateFecha>();
    }
    public class RemateFecha
    {
        public string Label { get; set; }
        public string Fecha { get; set; }
        public decimal Precio { get; set; }
        public string PrecioDisplay { get; set; }
    }

    // PASTE SERVICE LOGIC HERE (Simplified for single file run)
    public class PdfParserService
    {
        // ... (I will need to copy the FULL service logic here to test it)
        // For brevity in this tool call, I will include the critical updated methods.

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

        private static readonly Dictionary<string, decimal> SpanishAmountValues = new()
        {
            {"uno", 1}, {"un", 1}, {"una", 1}, {"dos", 2}, {"tres", 3}, {"cuatro", 4}, {"cinco", 5},
            {"seis", 6}, {"siete", 7}, {"ocho", 8}, {"nueve", 9}, {"diez", 10},
            {"once", 11}, {"doce", 12}, {"trece", 13}, {"catorce", 14}, {"quince", 15},
            {"dieciséis", 16}, {"dieciseis", 16}, {"diecisiete", 17}, {"dieciocho", 18},
            {"diecinueve", 19}, {"veintiuno", 21}, {"veintiún", 21}, {"veintidós", 22}, {"veintidos", 22},
            {"veintitrés", 23}, {"veintitres", 23}, {"veinticuatro", 24}, {"veinticinco", 25},
            {"veintiséis", 26}, {"veintiseis", 26}, {"veintisiete", 27}, {"veintiocho", 28},
            {"veintinueve", 29},
            {"veinte", 20}, {"treinta", 30}, {"cuarenta", 40}, {"cincuenta", 50},
            {"sesenta", 60}, {"setenta", 70}, {"ochenta", 80}, {"noventa", 90},
            {"cien", 100}, {"ciento", 100}, {"doscientos", 200}, {"trescientos", 300},
            {"cuatrocientos", 400}, {"quinientos", 500}, {"seiscientos", 600},
            {"setecientos", 700}, {"ochocientos", 800}, {"novecientos", 900},
            {"mil", 1000}, {"millón", 1000000}, {"millon", 1000000}, {"millones", 1000000}
        };

        private decimal ConvertSpanishTextToDecimal(string text)
        {
            try
            {
                var cleanText = text.ToLower().Replace(",", "").Replace(".", "");
                string integerPartText = cleanText;
                string decimalPartText = "";

                if (Regex.IsMatch(cleanText, @"\s+con\s+.*?(c[ée]ntimos|centavos)"))
                {
                    var parts = Regex.Split(cleanText, @"\s+con\s+");
                    integerPartText = parts[0];
                    if (parts.Length > 1)
                        decimalPartText = Regex.Replace(parts[1], @"\s*(c[ée]ntimos|centavos).*", "").Trim();
                }

                decimal integerValue = ParseSpanishNumberText(integerPartText);
                decimal decimalValue = 0;

                if (!string.IsNullOrEmpty(decimalPartText))
                {
                    decimalValue = ParseSpanishNumberText(decimalPartText);
                    if (decimalValue > 0) decimalValue /= 100m;
                }

                return integerValue + decimalValue;
            }
            catch { return 0; }
        }

        private decimal ParseSpanishNumberText(string text)
        {
            text = Regex.Replace(text, @"\b(colones|d[óo]lares|exactos)\b", "", RegexOptions.IgnoreCase);
            text = text.Replace(" y ", " ");
            var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            decimal totalValue = 0;
            decimal currentChunk = 0;
            foreach (var word in words)
            {
                if (SpanishAmountValues.TryGetValue(word, out decimal val))
                {
                    if (val == 1000) { if (currentChunk == 0) currentChunk = 1; currentChunk *= 1000; totalValue += currentChunk; currentChunk = 0; }
                    else if (val == 1000000) { if (currentChunk == 0) currentChunk = 1; currentChunk *= 1000000; totalValue += currentChunk; currentChunk = 0; }
                    else if (val >= 100) { currentChunk += val; }
                    else { currentChunk += val; }
                }
            }
            totalValue += currentChunk;
            return totalValue;
        }

        public List<Remate> ParseText(string fullText)
        {
            var remates = new List<Remate>();
            fullText = Regex.Replace(fullText, @"\s+", " ");
            var rawBlocks = Regex.Split(fullText, @"En\s+este\s+Despacho[,\.\s]*", RegexOptions.IgnoreCase)
                                     .Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();

            foreach (var rawBlock in rawBlocks)
            {
                if (rawBlock.Length < 50) continue;
                var remate = new Remate();
                var blockText = rawBlock.Trim();

                remate.Tipo = "Propiedad";
                if (Regex.IsMatch(blockText, @"\b(veh[íi]culo|carro|moto|bus|camion|placa|marca|estilo)\b", RegexOptions.IgnoreCase) &&
                    !Regex.IsMatch(blockText, @"\b(finca|lote|terreno)\b", RegexOptions.IgnoreCase))
                {
                    remate.Tipo = "Vehiculo";
                }

                decimal price1 = 0;
                string currencySymbol = "₡";

                var baseMatch = Regex.Match(blockText, @"(?:base|suma|servirá)\s+(?:de\s+)?(?:la suma de\s+)?([a-zA-ZáéíóúñÁÉÍÓÚÑ\s]+?)(?:\s+(colones|d[óo]lares)(?:\s+con\s+[a-zA-ZáéíóúñÁÉÍÓÚÑ\s]+(?:c[ée]ntimos|centavos))?|\s*(?:,|\.|$))", RegexOptions.IgnoreCase);
                if (baseMatch.Success)
                {
                    var robustMatch = Regex.Match(blockText, @"(?:base|suma)\s+(?:de\s+)?(?:la suma de\s+)?([a-zA-ZáéíóúñÁÉÍÓÚÑ\s]+(?:colones|d[óo]lares)(?:\s+con\s+[a-zA-ZáéíóúñÁÉÍÓÚÑ\s]+(?:c[ée]ntimos|centavos))?)", RegexOptions.IgnoreCase);
                    if (robustMatch.Success)
                    {
                        var fullAmountString = robustMatch.Groups[1].Value;
                        if (Regex.IsMatch(fullAmountString, "d[óo]lares", RegexOptions.IgnoreCase)) currencySymbol = "$";
                        price1 = ConvertSpanishTextToDecimal(fullAmountString);
                    }
                    else if (baseMatch.Groups[1].Value.Length > 5)
                    {
                        price1 = ConvertSpanishTextToDecimal(baseMatch.Groups[1].Value);
                        if (Regex.IsMatch(baseMatch.Value, "d[óo]lares", RegexOptions.IgnoreCase)) currencySymbol = "$";
                    }
                }

                remate.PrecioBase = price1;
                remate.PrecioBaseDisplay = price1 > 0 ? $"{currencySymbol}{price1:N2}" : "Ver Texto";
                remates.Add(remate);
            }
            return remates;
        }

        private void ExtractRegexGroup(string input, string pattern, string key, Dictionary<string, string> targetDict)
        {
            var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1) targetDict[key] = match.Groups[1].Value.Trim();
        }
    }
}
