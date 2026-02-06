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
                return Ok(remates);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}
