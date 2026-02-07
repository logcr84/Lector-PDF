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
                Detalles de venta.
                publicación número: 12345 
                En este Despacho otro remate de vehículo.
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
    }
}
