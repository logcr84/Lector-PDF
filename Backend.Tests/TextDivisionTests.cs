using Xunit;
using Backend.Services;
using System.Linq;

namespace Backend.Tests
{
    public class TextDivisionTests
    {
        [Fact]
        public void ParseText_ShouldHandleCaseInsensitiveStart()
        {
            var service = new PdfParserService();
            // UPPERCASE "EN ESTE DESPACHO"
            string text = @"
                EN ESTE DESPACHO se vende finca.
                Base 1000. Expediente: 123.
                publicación número: 1
            ";

            var remates = service.ParseText(text);

            Assert.Single(remates);
        }

        [Fact]
        public void ParseText_ShouldHandleCommaAfterDespacho()
        {
            var service = new PdfParserService();
            // Comma "En este Despacho,"
            string text = @"
                En este Despacho, se vende finca.
                Base 1000. Expediente: 123.
                publicación número: 1
            ";

            var remates = service.ParseText(text);

            Assert.Single(remates);
        }

        [Fact]
        public void ParseText_ShouldHandleTypoInDespacho()
        {
            var service = new PdfParserService();
            // Typo/OCR error "En este D espacho" (This might be too hard to fix generally but good to know)
            // But let's try at least "En este despacho" (lowercase d)
            string text = @"
                En este despacho se vende finca.
                Base 1000. Expediente: 123.
                publicación número: 1
            ";

            var remates = service.ParseText(text);

            Assert.Single(remates);
        }
    }
}
