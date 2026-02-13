using System;
using System.Linq;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

// Read PDF
using var doc = PdfDocument.Open("/Users/jalfaro/Lector PDF/test.pdf");
var fullText = string.Join(" ", doc.GetPages().Select(p => p.Text));

// Normalize
fullText = Regex.Replace(fullText, @"\s+", " ");

// Split into blocks
var rawBlocks = Regex.Split(fullText, @"En\s+este\s+Despacho[,\.\s]*", RegexOptions.IgnoreCase)
                     .Where(x => !string.IsNullOrWhiteSpace(x) && x.Length >= 50)
                     .ToArray();

Console.WriteLine($"Total blocks: {rawBlocks.Length}\n");

// Print first 5 blocks (first 500 chars each) to understand patterns
for (int i = 0; i < Math.Min(5, rawBlocks.Length); i++)
{
    var block = rawBlocks[i];
    var preview = block.Substring(0, Math.Min(600, block.Length));

    // Check what patterns are present
    bool hasMatricula = Regex.IsMatch(block, @"matrícula|matricula|Finca|folio real", RegexOptions.IgnoreCase);
    bool hasExpediente = Regex.IsMatch(block, @"Expediente|EXP|autos", RegexOptions.IgnoreCase);
    bool hasBase = Regex.IsMatch(block, @"base|suma|servirá", RegexOptions.IgnoreCase);
    bool hasSeñalan = Regex.IsMatch(block, @"señalan", RegexOptions.IgnoreCase);
    bool hasRemate = Regex.IsMatch(block, @"remate|subasta", RegexOptions.IgnoreCase);

    Console.WriteLine($"=== BLOCK {i + 1} ===");
    Console.WriteLine($"Length: {block.Length}");
    Console.WriteLine($"Patterns: matrícula={hasMatricula}, expediente={hasExpediente}, base={hasBase}, señalan={hasSeñalan}, remate={hasRemate}");
    Console.WriteLine($"Text: {preview}");
    Console.WriteLine();
}
