using Xunit;
using Backend.Services;
using System.Linq;

namespace Backend.Tests
{
    public class PdfParserServiceTests
    {
        [Fact]
        public void ParseText_ShouldIdentifyEdictAndExtractIdStartAndEnd()
        {
            // Arrange
            var service = new PdfParserService();
            // Simulating content similar to parrafo.py expectations
            // "En este Despacho" start ... "publicación número: 12345" end
            string text = @"
                Algún texto irrelevante previo.
                En este Despacho del Juzgado Civil se vende la finca.
                Base 10000 colones. Expediente: 25-123456-CIVIL.
                publicación número: 12345 
                En este Despacho otro remate de vehículo.
                Base 5000 colones. Expediente: 99-000999-COBRO.
                Referencia N° 999.
            ";

            // Act
            var remates = service.ParseText(text);

            // Assert
            Assert.Equal(2, remates.Count);

            // Check First Remate
            Assert.Equal(1, remates[0].Id);
            Assert.Contains("En este Despacho del Juzgado Civil se vende la finca", remates[0].TextoOriginal);
            Assert.Contains("publicación número: 12345", remates[0].TextoOriginal);
            // Verify cut-off: Should NOT contain next edict logic
            Assert.DoesNotContain("otro remate", remates[0].TextoOriginal);

            // Check Second Remate
            Assert.Equal(2, remates[1].Id);
            Assert.Contains("otro remate de vehículo", remates[1].TextoOriginal);
            Assert.Contains("Referencia N° 999", remates[1].TextoOriginal);
        }

        [Fact]
        public void ParseText_ShouldExtractKeyInformation()
        {
            // Arrange
            var service = new PdfParserService();
            // Sample extracted from typical format
            string text = @"
                En este Despacho se saca a remate el Vehiculo Marca: Toyota Estilo: Corolla.
                Base de CINCO MILLONES DE COLONES.
                Expediente: 24-001234-CIVIL.
                Se señalan las diez horas del quince de marzo de dos mil veintiséis.
                publicación número: 100
            ";

            // Act
            var remates = service.ParseText(text);

            // Assert
            Assert.Single(remates);
            var r = remates[0];

            Assert.Equal(1, r.Id);
            Assert.Equal("Vehiculo", r.Tipo);
            Assert.Equal("Toyota Corolla", r.Titulo);
            Assert.Equal("24-001234-CIVIL", r.Expediente);
            Assert.Equal(5000000, r.PrecioBase);
            Assert.Single(r.Remates);
            Assert.Equal("15/03/2026 10:00", r.Remates[0].Fecha);
        }

        [Fact]
        public void ParseText_ShouldHandleVariousDateFormats()
        {
            var service = new PdfParserService();
            string text = @"
                En este Despacho se saca a remate Varias Fechas.
                Primero: señalan las nueve horas del cinco de enero de dos mil veinticinco.
                Segundo: señala fecha del quince de agosto del año dos mil veinticuatro.
                Tercero: para el 14 de febrero del 2026.
                Cuarto: fecha tope 10/10/2026.
                Expediente: 24-000001-CIVIL
                Base 10000 colones.
                publicación número: 101
            ";

            var remates = service.ParseText(text);

            Assert.Single(remates);
            var r = remates[0];

            Assert.Equal(4, r.Remates.Count);

            // 1. Verbose: "nueve horas ... cinco de enero ... dos mil veinticinco" -> 05/01/2025 09:00
            Assert.Equal("05/01/2025 09:00", r.Remates[0].Fecha);

            // 2. Verbose with extras: "quince de agosto ... dos mil veinticuatro" -> 15/08/2024
            Assert.Equal("15/08/2024", r.Remates[1].Fecha.Trim());

            // 3. Semi-Numeric: "14 de febrero del 2026"
            Assert.Equal("14/02/2026", r.Remates[2].Fecha.Trim());

            // 4. Strict Numeric: "10/10/2026"
            Assert.Equal("10/10/2026", r.Remates[3].Fecha.Trim());


            // 5. OCR Failures: "l0/l0/2O26" (l=1, O=0)
            Assert.Equal("10/10/2026", new PdfParserService().ParseText("\nEn este Despacho fecha l0/l0/2O26.\npublicación número: 2")[0].Remates[0].Fecha.Trim());
        }

        [Fact]
        public void ParseText_ShouldHandleOcrConcatenatedWords()
        {
            var service = new PdfParserService();

            // Real-world case 1: "lasdiez horas cero minutos del veinticuatro de marzo de dos mil veintiséis"
            string text1 = @"
                En este Despacho se saca a remate un bien.
                Se señalan lasdiez horas cero minutos del veinticuatro de marzo de dos mil veintiséis.
                Base 10000 colones.
                Expediente: 24-000001-CIVIL
                publicación número: 101
            ";

            var remates1 = service.ParseText(text1);
            Assert.Single(remates1);
            Assert.Single(remates1[0].Remates);
            Assert.Equal("24/03/2026 10:00", remates1[0].Remates[0].Fecha);

            // Real-world case 2: "tercer remate se señalan las nueve horas treinta minutos del diecinueve de marzo de dos milveintiséis"
            string text2 = @"
                En este Despacho se saca a remate un bien.
                tercer remate se señalan las nueve horas treinta minutos del diecinueve de marzo de dos milveintiséis.
                Base 5000 colones.
                Expediente: 24-000002-CIVIL
                publicación número: 102
            ";

            var remates2 = service.ParseText(text2);
            Assert.Single(remates2);
            Assert.Single(remates2[0].Remates);
            Assert.Equal("19/03/2026 09:30", remates2[0].Remates[0].Fecha);
        }
    }
}
