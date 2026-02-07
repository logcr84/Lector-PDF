using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RematesController : ControllerBase
    {
        private readonly IPdfParserService _pdfParserService;

        public RematesController(IPdfParserService pdfParserService)
        {
            _pdfParserService = pdfParserService;
        }

        [HttpPost("upload")]
        public IActionResult UploadPdf(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                return BadRequest("Only PDF files are allowed.");

            try
            {
                using var stream = file.OpenReadStream();
                var remates = _pdfParserService.ParsePdf(stream);

                if (remates.Count == 0)
                {
                    return BadRequest(new
                    {
                        message = "No se encontraron datos de remates en el archivo PDF. Asegúrese de que sea un boletín oficial de remates.",
                        details = "El archivo podría ser de otro tipo o no contener el formato esperado ('En este Despacho...', expedientes, precios, etc.)"
                    });
                }

                if (remates.Count > 0)
                {
                    var debugJson = System.Text.Json.JsonSerializer.Serialize(remates[0]);
                    Console.WriteLine($"DEBUG JSON (First Remate): {debugJson}");
                }

                return Ok(remates);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Error al procesar el archivo PDF",
                    details = ex.Message
                });
            }
        }
    }
}
