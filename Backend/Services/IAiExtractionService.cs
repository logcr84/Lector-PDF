using Backend.Models;

namespace Backend.Services
{
    public interface IAiExtractionService
    {
        /// <summary>
        /// Extracts missing fields from a raw text block using AI.
        /// </summary>
        /// <param name="text">The raw text of the edict.</param>
        /// <returns>A dictionary of extracted fields or a partial Remate object.</returns>
        Task<Remate?> ExtractMissingFieldsAsync(string text);
    }
}
