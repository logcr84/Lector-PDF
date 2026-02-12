using Backend.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using System.Text.RegularExpressions;
using System.Text;

namespace Backend.Services
{
    /// <summary>
    /// Interfaz para el servicio de análisis de documentos PDF.
    /// Proporciona funcionalidad para extraer información de remates judiciales desde archivos PDF.
    /// </summary>
    public interface IPdfParserService
    {
        /// <summary>
        /// Analiza un archivo PDF para extraer información de remates judiciales.
        /// </summary>
        /// <param name="pdfStream">Flujo de datos del archivo PDF a analizar.</param>
        /// <returns>Lista de objetos <see cref="Remate"/> con la información extraída de cada remate.</returns>
        List<Remate> ParsePdf(Stream pdfStream);
    }

    /// <summary>
    /// Servicio para el análisis y extracción de información de remates judiciales desde documentos PDF.
    /// Implementa lógica avanzada para reconocer y extraer fechas, montos, números de expediente y otros datos relevantes
    /// de documentos en español, incluyendo manejo de errores comunes de OCR.
    /// </summary>
    public class PdfParserService : IPdfParserService
    {
        /// <summary>
        /// Mapeo de números en español (palabras) a sus valores numéricos.
        /// Utilizado para analizar fechas y horas expresadas en formato textual.
        /// Incluye variantes con y sin tildes para mayor robustez en el análisis OCR.
        /// </summary>
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

        /// <summary>
        /// Mapeo de meses en español a sus valores numéricos (1-12).
        /// Incluye variantes comunes como "setiembre" y "septiembre" para el mes 9.
        /// </summary>
        private static readonly Dictionary<string, int> SpanishMonths = new()
        {
            {"enero", 1}, {"febrero", 2}, {"marzo", 3}, {"abril", 4}, {"mayo", 5}, {"junio", 6},
            {"julio", 7}, {"agosto", 8}, {"septiembre", 9}, {"setiembre", 9},
            {"octubre", 10}, {"noviembre", 11}, {"diciembre", 12}
        };

        /// <summary>
        /// Normaliza errores comunes de OCR en texto de fechas separando palabras concatenadas.
        /// Ejemplos de correcciones:
        /// - "lasdiez" → "las diez"
        /// - "milveintiséis" → "mil veintiséis"
        /// - "doshoras" → "dos horas"
        /// - "cerominutos" → "cero minutos"
        /// - "dosmil" → "dos mil"
        /// - "delagosto" → "del agosto"
        /// </summary>
        /// <param name="text">Texto de fecha con posibles errores de OCR.</param>
        /// <returns>Texto normalizado con palabras separadas correctamente.</returns>
        private string NormalizeOcrDateText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            text = text.ToLower();

            // Common hour concatenations
            text = Regex.Replace(text, @"\blas(diez|once|doce|trece|catorce|quince|dieciséis|dieciseis|diecisiete|dieciocho|diecinueve|veinte|veintiuno|una|dos|tres|cuatro|cinco|seis|siete|ocho|nueve)\b", "las $1", RegexOptions.IgnoreCase);

            // "Xhoras" -> "X horas"
            text = Regex.Replace(text, @"\b(un|una|dos|tres|cuatro|cinco|seis|siete|ocho|nueve|diez|once|doce|trece|catorce|quince|dieciséis|dieciseis|diecisiete|dieciocho|diecinueve|veinte|veintiuno)horas?\b", "$1 horas", RegexOptions.IgnoreCase);

            // "Xminutos" -> "X minutos"
            text = Regex.Replace(text, @"\b(cero|un|una|dos|tres|cuatro|cinco|seis|siete|ocho|nueve|diez|once|doce|trece|catorce|quince|dieciséis|dieciseis|veinte|veinticinco|treinta|cuarenta|cincuenta)minutos?\b", "$1 minutos", RegexOptions.IgnoreCase);

            // "dosmil" -> "dos mil" (year concatenation)
            text = Regex.Replace(text, @"\b(dos)mil\b", "$1 mil", RegexOptions.IgnoreCase);

            // "milveintiséis" -> "mil veintiséis", "milveinticinco" -> "mil veinticinco", etc.
            text = Regex.Replace(text, @"\bmil(veinti[a-záéíóú]+|treinta|cuarenta|cincuenta|sesenta|setenta|ochenta|noventa|diez|once|doce|trece|catorce|quince|dieciséis|dieciseis|diecisiete|dieciocho|diecinueve|uno|dos|tres|cuatro|cinco|seis|siete|ocho|nueve)\b", "mil $1", RegexOptions.IgnoreCase);

            // "delveinti" -> "del veinti", "delagosto" -> "del agosto"
            text = Regex.Replace(text, @"\bdel(veinti|enero|febrero|marzo|abril|mayo|junio|julio|agosto|septiembre|setiembre|octubre|noviembre|diciembre)", "del $1", RegexOptions.IgnoreCase);

            // "deaño" -> "de año"
            text = Regex.Replace(text, @"\bde(año)\b", "de $1", RegexOptions.IgnoreCase);

            return text;
        }

        /// <summary>
        /// Analiza texto de fechas en español (verboso o numérico) y lo convierte a formato estándar.
        /// Soporta tres estrategias de análisis en orden de prioridad:
        /// 1. Formato numérico estricto: dd/MM/yyyy o dd-MM-yyyy
        /// 2. Formato semi-numérico: "14 de febrero del 2026"
        /// 3. Formato verboso completo: "catorce horas treinta minutos del tres de febrero de dos mil veintiséis"
        /// </summary>
        /// <param name="rawDateText">Texto de fecha en español, puede incluir hora.</param>
        /// <returns>Fecha formateada como "dd/MM/yyyy HH:mm". Si no se encuentra hora en el texto, retorna "dd/MM/yyyy 00:00". Retorna el texto original si no se puede analizar.</returns>
        /// <example>
        /// ParseSpanishDate("catorce horas treinta minutos del tres de febrero de dos mil veintiséis") → "03/02/2026 14:30"
        /// ParseSpanishDate("14 de febrero del 2026") → "14/02/2026 00:00"
        /// ParseSpanishDate("03/02/2026") → "03/02/2026 00:00"
        /// </example>
        private string ParseSpanishDate(string rawDateText)
        {
            try
            {
                // First, normalize OCR errors
                var text = NormalizeOcrDateText(rawDateText);
                text = text.ToLower().Trim();

                // Strategy 1: strict numeric (dd/MM/yyyy) or (dd-MM-yyyy)
                // regex look for independent date-like patterns
                var numericMatch = Regex.Match(text, @"\b(\d{1,2})[\/\-\.](\d{1,2})[\/\-\.](\d{2,4})\b");
                if (numericMatch.Success)
                {
                    if (int.TryParse(numericMatch.Groups[1].Value, out int d) &&
                        int.TryParse(numericMatch.Groups[2].Value, out int m) &&
                        int.TryParse(numericMatch.Groups[3].Value, out int y))
                    {
                        // Fix short year
                        if (y < 100) y += 2000;

                        // Try to find time (always include HH:MM format)
                        var timeMatch = Regex.Match(text, @"(\d{1,2})[:\.](\d{2})");
                        string timePart = timeMatch.Success
                            ? $" {int.Parse(timeMatch.Groups[1].Value):D2}:{int.Parse(timeMatch.Groups[2].Value):D2}"
                            : " 00:00";

                        return $"{d:D2}/{m:D2}/{y}{timePart}";
                    }
                }

                // Strategy 2: Semi-numeric (14 de febrero del 2026)
                // This often appears as "el 14 de febrero del 2026"
                var semiNumericMatch = Regex.Match(text, @"(\d{1,2})\s+de\s+([a-z]+)\s+(?:de|del|del a[ñn]o)?\s+(\d{4})");
                if (semiNumericMatch.Success)
                {
                    int d = int.Parse(semiNumericMatch.Groups[1].Value);
                    string monthName = semiNumericMatch.Groups[2].Value;
                    int y = int.Parse(semiNumericMatch.Groups[3].Value);

                    int m = 0;
                    foreach (var kvp in SpanishMonths)
                    {
                        if (monthName.StartsWith(kvp.Key))
                        {
                            m = kvp.Value;
                            break;
                        }
                    }

                    if (m > 0)
                    {
                        // Try to find time (always include HH:MM format)
                        var timeMatch = Regex.Match(text, @"(\d{1,2})[:\.](\d{2})");
                        string timePart = timeMatch.Success
                            ? $" {int.Parse(timeMatch.Groups[1].Value):D2}:{int.Parse(timeMatch.Groups[2].Value):D2}"
                            : " 00:00";

                        return $"{d:D2}/{m:D2}/{y}{timePart}";
                    }
                }

                // Strategy 3: Verbose Spanish (Original Logic)
                return ParseVerboseSpanishDate(text, rawDateText);
            }
            catch
            {
                // On any error, return original text
                return rawDateText;
            }
        }

        /// <summary>
        /// Analiza fechas en formato verboso completo en español.
        /// Extrae componentes individuales (horas, minutos, día, mes, año) del texto y los ensambla en formato estándar.
        /// Este método es llamado como última estrategia cuando los formatos numéricos y semi-numéricos no funcionan.
        /// </summary>
        /// <param name="text">Texto normalizado de la fecha en formato verboso.</param>
        /// <param name="rawDateText">Texto original sin normalizar (usado como fallback si el análisis falla).</param>
        /// <returns>Fecha formateada o el texto original si no se pueden extraer todos los componentes.</returns>
        /// <example>
        /// Input: "catorce horas treinta minutos del tres de febrero de dos mil veintiséis"
        /// Output: "03/02/2026 14:30"
        /// </example>
        private string ParseVerboseSpanishDate(string text, string rawDateText)
        {
            // Extract hours (e.g., "catorce horas" -> 14 or "once horas" -> 11, or "quincehoras")
            int hours = 0;
            // Changed \s+ to \s* to handle "quincehoras"
            var hoursMatch = Regex.Match(text, @"((?:un(?:a|o)?|dos|tres|cuatro|cinco|seis|siete|ocho|nueve|diez|once|doce|trece|catorce|quince|dieciséis|dieciseis|diecisiete|dieciocho|diecinueve|veinte|veintiún|veintiuno|veintidós|veintidos|veintitrés|veintitres|\d+))\s*horas?", RegexOptions.IgnoreCase);
            if (hoursMatch.Success)
            {
                var hourText = hoursMatch.Groups[1].Value.Trim();
                hours = ParseSpanishNumber(hourText);
            }

            // Extract minutes (e.g., "treinta minutos" -> 30 or "cerominutos" -> 0)
            int minutes = 0;
            // Try "XXminutos" pattern first (e.g., "cerominutos")
            var minutesMatch = Regex.Match(text, @"(cero|un(?:a|o)?|dos|tres|cuatro|cinco|seis|siete|ocho|nueve|diez|once|doce|trece|catorce|quince|(?:dieci)?(?:seis|siete|ocho|nueve)|veinte|veinticinco|treinta|cuarenta|cincuenta|\d+)minutos?", RegexOptions.IgnoreCase);
            if (!minutesMatch.Success)
            {
                // Try "XX minutos" pattern (with space)
                minutesMatch = Regex.Match(text, @"(cero|un(?:a|o)?|dos|tres|cuatro|cinco|seis|siete|ocho|nueve|diez|once|doce|trece|catorce|quince|(?:dieci)?(?:seis|siete|ocho|nueve)|veinte|veinticinco|treinta|cuarenta|cincuenta|\d+)\s+minutos?", RegexOptions.IgnoreCase);
            }
            if (minutesMatch.Success)
            {
                var minuteText = minutesMatch.Groups[1].Value.Trim();
                minutes = ParseSpanishNumber(minuteText);
            }

            // Extract day (e.g., "tres de febrero" -> 3)
            int day = 0;
            // Mejorado: regex más específico que acepta "del" o "el" al inicio
            // y captura correctamente día (con espacios para "treinta y tres") y mes
            var dayMatch = Regex.Match(text, @"(?:del?|el)\s+([a-záéíóúñ\s]+?)\s+de\s+([a-záéíóúñ]+)\s+(?:de|del)", RegexOptions.IgnoreCase);
            if (dayMatch.Success)
            {
                var dayText = dayMatch.Groups[1].Value.Trim();
                var monthText = dayMatch.Groups[2].Value.Trim();

                // Debug mejorado para ver qué capturó el regex
                Console.WriteLine($"DEBUG DAY_MONTH CAPTURE: dayText='{dayText}' monthText='{monthText}'");

                // Limpiar texto del día (normalizar espacios múltiples)
                dayText = Regex.Replace(dayText, @"\s+", " ");

                day = ParseSpanishNumber(dayText);
            }

            // Extract month (e.g., "de febrero de" -> 2, or "de agostode" -> 8)
            int month = 0;
            if (dayMatch.Success)
            {
                var monthText = dayMatch.Groups[2].Value.Trim().ToLower();

                // Handle concatenated month+de (e.g., "agostode" -> "agosto")
                foreach (var monthName in SpanishMonths.Keys)
                {
                    if (monthText.StartsWith(monthName))
                    {
                        month = SpanishMonths[monthName];
                        Console.WriteLine($"DEBUG MONTH MATCHED: '{monthText}' -> {month}");
                        break;
                    }
                }
            }

            // Extract year (e.g., "del dos mil veintiséis" → 2026, "de dos mil veintiséis" → 2026, "del año dos mil" → 2026)
            int year = 0;
            var yearMatch = Regex.Match(text, @"(?:de|del)\s+(?:año\s+)?(dos\s?mil[\wáéíóúñ\s]*)", RegexOptions.IgnoreCase);
            if (yearMatch.Success)
            {
                var yearText = yearMatch.Groups[1].Value.Trim();

                // Clean attached noise words (e.g., "veintiséiscon" -> "veintiséis")
                // This happens when OCR misses the space between the date and the next sentence ("con la base...")
                // CAUTION: Do NOT remove "y" because it's needed for "treinta y tres" etc.
                yearText = Regex.Replace(yearText, @"(con|base|suma|por|en|del|al)\s*.*$", "", RegexOptions.IgnoreCase); // Remove trailing sequence tokens
                yearText = Regex.Replace(yearText, @"(con|base|suma|por|en)$", "", RegexOptions.IgnoreCase); // Remove robust attached suffix

                year = ParseSpanishYear(yearText);
            }

            // Extra debug for verbose
            Console.WriteLine($"DEBUG VERBOSE DATE: '{rawDateText}' -> H:{hours} M:{minutes} D:{day} Mo:{month} Y:{year}");

            // Validate all components were extracted
            if (day > 0 && month > 0 && year > 0)
            {
                // Always include time in HH:MM format
                var timeStr = $" {hours:D2}:{minutes:D2}";
                return $"{day:D2}/{month:D2}/{year}{timeStr}";
            }

            // Fallback: return original text if parsing incomplete
            return rawDateText;
        }

        /// <summary>
        /// Convierte palabras numéricas en español a valores enteros.
        /// Soporta números del 0 al 31, incluyendo formas compuestas con "y" (ej. "treinta y uno").
        /// Si el texto ya es numérico, lo convierte directamente.
        /// </summary>
        /// <param name="numberText">Texto con el número en español o dígitos.</param>
        /// <returns>Valor entero del número, o 0 si no se puede analizar.</returns>
        /// <example>
        /// ParseSpanishNumber("treinta y uno") → 31
        /// ParseSpanishNumber("quince") → 15
        /// ParseSpanishNumber("25") → 25
        /// </example>
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
        /// Convierte texto de año en español a valor numérico.
        /// Maneja el patrón "dos mil XXX" (con o sin espacios) para años del 2000 en adelante.
        /// También acepta años en formato numérico directo.
        /// </summary>
        /// <param name="yearText">Texto del año en español o dígitos.</param>
        /// <returns>Año como entero (ej. 2026), o 0 si no se puede analizar.</returns>
        /// <example>
        /// ParseSpanishYear("dos mil veintiséis") → 2026
        /// ParseSpanishYear("dosmil veintiséis") → 2026
        /// ParseSpanishYear("dos mil") → 2000
        /// ParseSpanishYear("2026") → 2026
        /// </example>
        private int ParseSpanishYear(string yearText)
        {
            yearText = yearText.ToLower().Trim();

            // Handle "dos mil XXX" or "dosmil XXX" pattern
            if (yearText.StartsWith("dos mil") || yearText.StartsWith("dosmil"))
            {
                // Remove both variants to get the remaining number
                var remaining = yearText.Replace("dos mil", "").Replace("dosmil", "").Trim();

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

        /// <summary>
        /// Mapeo de palabras numéricas en español a valores decimales para montos monetarios.
        /// Incluye números básicos (0-99), centenas, miles y millones.
        /// Soporta variantes con y sin tildes para mayor robustez.
        /// </summary>
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

        /// <summary>
        /// Convierte texto de montos en español a valores decimales.
        /// Procesa palabras numéricas compuestas, manejando miles, millones y centenas correctamente.
        /// Normaliza el texto eliminando "y", comas y puntos antes del análisis.
        /// </summary>
        /// <param name="text">Texto del monto en español (ej. "cinco millones doscientos mil").</param>
        /// <returns>Valor decimal del monto, o 0 si no se puede analizar o se encuentra una palabra no numérica.</returns>
        /// <example>
        /// ConvertSpanishTextToDecimal("cinco millones doscientos mil") → 5200000
        /// ConvertSpanishTextToDecimal("tres mil quinientos") → 3500
        /// ConvertSpanishTextToDecimal("cien mil") → 100000
        /// </example>
        private decimal ConvertSpanishTextToDecimal(string text)
        {
            try
            {
                // Normalize text: lowercase, remove " y ", remove punctuation
                var cleanText = text.ToLower()
                    .Replace(" y ", " ")
                    .Replace(",", "")
                    .Replace(".", "");

                var words = cleanText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                decimal totalValue = 0;
                decimal currentChunk = 0;

                foreach (var word in words)
                {
                    if (SpanishAmountValues.TryGetValue(word, out decimal val))
                    {
                        if (val == 1000) // mil
                        {
                            // "cinco mil" -> 5 * 1000
                            // "mil" -> 1 * 1000
                            if (currentChunk == 0) currentChunk = 1;
                            currentChunk *= 1000;
                            totalValue += currentChunk;
                            currentChunk = 0;
                        }
                        else if (val == 1000000) // millón/millones
                        {
                            // "cinco millones" -> 5 * 1000000
                            // "un millón" -> 1 * 1000000
                            if (currentChunk == 0) currentChunk = 1;
                            currentChunk *= 1000000;
                            totalValue += currentChunk;
                            currentChunk = 0;
                        }
                        else if (val >= 100) // Hundreds (cien, doscientos...)
                        {
                            currentChunk += val;
                        }
                        else // 0-99
                        {
                            currentChunk += val;
                        }
                    }
                    else
                    {
                        // Stop parsing if we hit a word that isn't a number (e.g., "señala", "fecha")
                        // This prevents merging "diez mil" with "quince" from a date following it.
                        break;
                    }
                }

                // Add remaining chunk (e.g., "... quinientos")
                totalValue += currentChunk;

                return totalValue;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Analiza un archivo PDF para extraer información de remates judiciales.
        /// Lee todas las páginas del PDF, extrae el texto palabra por palabra (similar a PyMuPDF),
        /// y luego procesa el texto completo para identificar y extraer datos de remates.
        /// </summary>
        /// <param name="pdfStream">Flujo de datos del archivo PDF a analizar.</param>
        /// <returns>Lista de objetos <see cref="Remate"/> con la información extraída. Retorna lista vacía si hay error al leer el PDF.</returns>
        /// <remarks>
        /// Este método utiliza PdfPig para extraer palabras individuales y las une con espacios,
        /// asegurando mejor separación que usar page.Text directamente.
        /// Luego delega el análisis del texto completo al método <see cref="ParseText"/>.
        /// </remarks>
        public List<Remate> ParsePdf(Stream pdfStream)
        {
            var fullTextBuilder = new StringBuilder();

            try
            {
                using var document = PdfDocument.Open(pdfStream);
                foreach (var page in document.GetPages())
                {
                    // Similar a parrafo.py que usa get_text(" ", strip=True)
                    // Extraemos palabras individuales y las unimos con espacios
                    // Esto asegura mejor separación que page.Text
                    var words = page.GetWords();
                    var pageText = string.Join(" ", words.Select(w => w.Text.Trim()));
                    fullTextBuilder.AppendLine(pageText);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error reading PDF stream: {ex.Message}");
                return new List<Remate>();
            }

            return ParseText(fullTextBuilder.ToString());
        }
        /// <summary>
        /// Procesa texto completo extraído de un PDF para identificar y extraer información de remates judiciales.
        /// Divide el texto en bloques que comienzan con "En este Despacho" y extrae datos estructurados de cada bloque.
        /// </summary>
        /// <param name="fullText">Texto completo del documento PDF.</param>
        /// <returns>Lista de objetos <see cref="Remate"/> con la información extraída de cada remate identificado.</returns>
        /// <remarks>
        /// Para cada bloque de texto, este método extrae:
        /// - Tipo de remate (Vehículo o Propiedad)
        /// - Número de expediente
        /// - Precio base (en texto o numérico)
        /// - Título descriptivo
        /// - Fechas de remates (hasta 3)
        /// 
        /// Solo se agregan remates que tengan al menos uno de estos campos: expediente, precio, título válido o fechas.
        /// La estrategia está adaptada del script Python parrafo.py para coherencia en el análisis.
        /// </remarks>
        public List<Remate> ParseText(string fullText)
        {
            var remates = new List<Remate>();

            try
            {
                // Strategy adapted from parrafo.py:
                // Find blocks starting with "En este Despacho" and ending with "publicación número:" or similar metadata.
                // Since PDF text might not be perfectly paragraphed like HTML <p> tags, we'll try to split by known distinct markers or just scan through.

                // Let's normalize spaces first
                fullText = Regex.Replace(fullText, @"\s+", " ");

                // We split by "En este Despacho" (case insensitive, ignoring punctuation/extra spaces) to get candidate blocks
                var rawBlocks = Regex.Split(fullText, @"En\s+este\s+Despacho[,\.\s]*", RegexOptions.IgnoreCase)
                                     .Where(x => !string.IsNullOrWhiteSpace(x))
                                     .ToArray();

                foreach (var rawBlock in rawBlocks)
                {
                    // Validate if it looks like a valid edict (too short might be noise)
                    if (rawBlock.Length < 50) continue;

                    var remate = new Remate();
                    remate.Id = remates.Count + 1;

                    // Reconstruct context slightly
                    remate.TextoOriginal = "En este Despacho " + rawBlock.Trim();

                    // Normalize whitespace for regex matching: replace parsing artifacts like newlines within sentences
                    var blockText = Regex.Replace(rawBlock, @"\s+", " ").Trim();

                    Console.WriteLine($"--- Block Preview: {blockText.Substring(0, Math.Min(100, blockText.Length))}... ---");


                    // Note: parrafo.py accumulates text until "publicación número" or "Referencia N°".
                    // The Split approach implicitly grabs everything until the next "En este Despacho". 
                    // To be more precise we could truncate at "publicación número" if present to avoid trailing garbage.
                    var endMarkerMatch = Regex.Match(blockText, @"(publicaci[óo]n n[úu]mero:|Referencia N°)\s*([\w\d\-]+)?", RegexOptions.IgnoreCase);
                    if (endMarkerMatch.Success)
                    {
                        // Keep text only up to the marker + the number captured
                        // parrafo.py includes the marker line.
                        // We capture the marker and the potential number following it
                        int lengthToKeep = endMarkerMatch.Index + endMarkerMatch.Length;
                        blockText = blockText.Substring(0, Math.Min(lengthToKeep, blockText.Length));

                        // Update TextoOriginal to reflect the clean cut
                        remate.TextoOriginal = "En este Despacho " + blockText;
                    }

                    // --- Extraction Logic ---

                    // --- Extraction Logic ---

                    // 1. Tipo (Vehículo / Propiedad) & Detalles Específicos
                    remate.Tipo = "Propiedad"; // Default

                    // Vehicle detection keywords
                    if (Regex.IsMatch(blockText, @"\b(veh[íi]culo|carro|moto|bus|camion|placa|marca|estilo)\b", RegexOptions.IgnoreCase) &&
                        !Regex.IsMatch(blockText, @"\b(finca|lote|terreno)\b", RegexOptions.IgnoreCase))
                    {
                        remate.Tipo = "Vehiculo";

                        // Extract Vehicle Details
                        ExtractRegexGroup(blockText, @"Marca[:\s]+([\w\-\s]+?)(?:,|;|\.|$)", "Marca", remate.Detalles);
                        ExtractRegexGroup(blockText, @"Estilo[:\s]+([\w\-\s]+?)(?:,|;|\.|$)", "Estilo", remate.Detalles);
                        ExtractRegexGroup(blockText, @"Modelo[:\s]+([\w\-\s]+?)(?:,|;|\.|$)", "Modelo", remate.Detalles);
                        ExtractRegexGroup(blockText, @"Color[:\s]+([\w\-\s]+?)(?:,|;|\.|$)", "Color", remate.Detalles);
                        ExtractRegexGroup(blockText, @"Placa[:\s]+(\w+)", "Placa", remate.Detalles);
                        ExtractRegexGroup(blockText, @"Motor[:\s]+([\w\-\s]+?)(?:,|;|\.|$)", "Motor", remate.Detalles);
                        ExtractRegexGroup(blockText, @"(Serie|VIN)[:\s]+([\w\-\s]+?)(?:,|;|\.|$)", "Serie", remate.Detalles);

                        // Construct Title
                        var marca = remate.Detalles.ContainsKey("Marca") ? remate.Detalles["Marca"] : "Vehículo";
                        var estilo = remate.Detalles.ContainsKey("Estilo") ? remate.Detalles["Estilo"] : "";
                        var modelo = remate.Detalles.ContainsKey("Modelo") ? remate.Detalles["Modelo"] : "";
                        remate.Titulo = $"{marca} {estilo} {modelo}".Trim();
                    }
                    else
                    {
                        remate.Tipo = "Propiedad";

                        // Extract Property Details
                        ExtractRegexGroup(blockText, @"Finca[:\s]+(\d+)", "Matricula", remate.Detalles);
                        ExtractRegexGroup(blockText, @"MIDE[:\s]+(.*?)(\.|PLANO|COLINDA)", "Medida", remate.Detalles);
                        ExtractRegexGroup(blockText, @"Naturaleza[:\s]+(.*?)(?:Si|situada)", "Naturaleza", remate.Detalles);
                        ExtractRegexGroup(blockText, @"Situada en (.*?)(?:,|;|\.|$)", "Ubicacion", remate.Detalles);
                        ExtractRegexGroup(blockText, @"COLINDA[:\s]+(.*?)(?:MIDE|\.|$)", "Colindantes", remate.Detalles);

                        // If "Medida" contains a number, use it for Area
                        if (remate.Detalles.ContainsKey("Medida"))
                        {
                            remate.Area = remate.Detalles["Medida"].Trim();
                        }

                        // Construct Title
                        if (remate.Detalles.ContainsKey("Ubicacion"))
                        {
                            remate.Titulo = remate.Detalles["Ubicacion"].Trim();
                        }
                        else
                        {
                            remate.Titulo = "Propiedad en Remate";
                        }
                    }

                    // 2. Expediente, Demandado, Juzgado
                    var expedienteMatch = Regex.Match(blockText, @"(?:Expediente|EXP|autos?)\s*[:Nn°]*\s*(\d+[\d\-]+[\w]*)", RegexOptions.IgnoreCase);
                    if (expedienteMatch.Success) remate.Expediente = expedienteMatch.Groups[1].Value;

                    ExtractRegexGroup(blockText, @"contra\s+(.*?)(?:\s+EXP|\.|$)", "Demandado", remate.Detalles);
                    if (remate.Detalles.ContainsKey("Demandado")) remate.Demandado = remate.Detalles["Demandado"];

                    ExtractRegexGroup(blockText, @"JUZGADO\s+(.*?)(?:\.|:|$)", "Juzgado", remate.Detalles);
                    if (remate.Detalles.ContainsKey("Juzgado")) remate.Juzgado = remate.Detalles["Juzgado"];


                    // 3. Precios y Fechas (1°, 2°, 3° Remate)
                    // Logic to extract specific blocks for each auction

                    // 1st Auction (Base Price)
                    decimal price1 = 0;
                    var baseMatch = Regex.Match(blockText, @"(?:base|suma|servirá)\s+(?:de\s+)?(?:la suma de\s+)?([a-zA-ZáéíóúñÁÉÍÓÚÑ\s]+?)(?:\s+(?:colones|d[óo]lares|exactos)|\s*(?:,|\.|$))", RegexOptions.IgnoreCase);
                    if (baseMatch.Success && baseMatch.Groups[1].Value.Length > 5)
                    {
                        price1 = ConvertSpanishTextToDecimal(baseMatch.Groups[1].Value);
                    }

                    if (price1 == 0) // Try digits
                    {
                        var digitMatch = Regex.Match(blockText, @"base\s+(?:de\s+)?[¢$]?\s?([\d,.]+)");
                        if (digitMatch.Success)
                        {
                            decimal.TryParse(digitMatch.Groups[1].Value.Replace(",", "").Replace(".", ""), out price1); // very rough
                                                                                                                        // Better to let a helper handle robust digit parsing if needed, sticking to text for now as primary manual fallback
                        }
                    }

                    remate.PrecioBase = price1;
                    remate.PrecioBaseDisplay = price1 > 0 ? $"₡{price1:N2}" : "Ver Texto"; // Default currency assumption

                    // Extract Dates and specific prices for subsequent auctions
                    var dateMatches = Regex.Matches(blockText, @"(?:(primer|segundo|tercer)[oa]?\s+)?(?:remate|subasta)\s+(?:se\s+)?(?:señalan|fijan)\s+(?:las|para el)\s+(.*?)(?:\.|;|,|\scon\b)", RegexOptions.IgnoreCase);

                    // This simple regex loop is okay, but often 2nd/3rd auctions are separate sentences with their own prices.
                    // Let's look for specific sentences.

                    // 1st
                    string date1 = "";
                    var match1 = Regex.Match(blockText, @"(?:primer)?\s*remate.*?señalan\s+(?:las|para)\s+(.*?)(?:\.|;|,)", RegexOptions.IgnoreCase);
                    if (match1.Success) date1 = ParseSpanishDate(match1.Groups[1].Value);

                    if (!string.IsNullOrEmpty(date1))
                    {
                        remate.Remates.Add(new RemateFecha
                        {
                            Label = "1° Remate",
                            Fecha = date1,
                            Precio = price1,
                            PrecioDisplay = remate.PrecioBaseDisplay
                        });
                    }

                    // 2nd (Base 75%)
                    string date2 = "";
                    decimal price2 = price1 > 0 ? price1 * 0.75m : 0;
                    // Look for "segundo remate... base de X"
                    var match2Block = Regex.Match(blockText, @"segundo\s+remate.*?(?:\.|;|$)", RegexOptions.IgnoreCase);
                    if (match2Block.Success)
                    {
                        var segText = match2Block.Value;
                        // Extract date
                        var d2 = Regex.Match(segText, @"señalan\s+(?:las|para)\s+(.*?)(?:\.|;|,|con|base)");
                        if (d2.Success) date2 = ParseSpanishDate(d2.Groups[1].Value);

                        // Extract specific price if mentioned
                        var p2Match = Regex.Match(segText, @"base\s+(?:de\s+)?(?:la suma de\s+)?([a-zA-Z\s]+)");
                        if (p2Match.Success)
                        {
                            var p2Val = ConvertSpanishTextToDecimal(p2Match.Groups[1].Value);
                            if (p2Val > 0) price2 = p2Val;
                        }
                    }

                    if (!string.IsNullOrEmpty(date2))
                    {
                        remate.Remates.Add(new RemateFecha
                        {
                            Label = "2° Remate",
                            Fecha = date2,
                            Precio = price2,
                            PrecioDisplay = price2 > 0 ? $"₡{price2:N2}" : "75% Base"
                        });
                    }

                    // 3rd (Base 25%)
                    string date3 = "";
                    decimal price3 = price1 > 0 ? price1 * 0.25m : 0;
                    var match3Block = Regex.Match(blockText, @"tercer\s+remate.*?(?:\.|;|$)", RegexOptions.IgnoreCase);
                    if (match3Block.Success)
                    {
                        var terText = match3Block.Value;
                        var d3 = Regex.Match(terText, @"señalan\s+(?:las|para)\s+(.*?)(?:\.|;|,|con|base)");
                        if (d3.Success) date3 = ParseSpanishDate(d3.Groups[1].Value);

                        var p3Match = Regex.Match(terText, @"base\s+(?:de\s+)?(?:la suma de\s+)?([a-zA-Z\s]+)");
                        if (p3Match.Success)
                        {
                            var p3Val = ConvertSpanishTextToDecimal(p3Match.Groups[1].Value);
                            if (p3Val > 0) price3 = p3Val;
                        }
                    }

                    if (!string.IsNullOrEmpty(date3))
                    {
                        remate.Remates.Add(new RemateFecha
                        {
                            Label = "3° Remate",
                            Fecha = date3,
                            Precio = price3,
                            PrecioDisplay = price3 > 0 ? $"₡{price3:N2}" : "25% Base"
                        });
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
                Console.WriteLine($"❌ Error parsing text: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }


            if (remates.Count == 0)
            {
                Console.WriteLine($"⚠ No valid remate data found in Text");
            }
            else
            {
                Console.WriteLine($"✓ Successfully extracted {remates.Count} remate(s) from Text");
            }

            return remates;
        }

        private void ExtractRegexGroup(string input, string pattern, string key, Dictionary<string, string> targetDict)
        {
            var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                string value = match.Groups[1].Value.Trim();
                if (!string.IsNullOrEmpty(value))
                {
                    targetDict[key] = value;
                }
            }
        }
    }
}
