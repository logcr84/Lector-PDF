namespace Backend.Models
{
    public class Remate
    {
        public int Id { get; set; }
        public string Tipo { get; set; } = string.Empty; // "Vehiculo" or "Propiedad"
        public string Titulo { get; set; } = string.Empty;
        public string Area { get; set; } = string.Empty;
        public decimal PrecioBase { get; set; }
        public string PrecioBaseDisplay { get; set; } = string.Empty;

        public List<RemateFecha> Remates { get; set; } = new List<RemateFecha>();

        public string Expediente { get; set; } = string.Empty;
        public Dictionary<string, string> Detalles { get; set; } = new Dictionary<string, string>();
        public string TextoOriginal { get; set; } = string.Empty;
        public string Demandado { get; set; } = string.Empty;
        public string Juzgado { get; set; } = string.Empty;
    }

    public class RemateFecha
    {
        public string Label { get; set; } = string.Empty; // "Primer Remate", etc.
        public decimal Precio { get; set; }
        public string PrecioDisplay { get; set; } = string.Empty;
        public string Fecha { get; set; } = string.Empty;
    }
}
