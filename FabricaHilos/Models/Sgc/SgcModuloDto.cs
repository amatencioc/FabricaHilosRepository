namespace FabricaHilos.Models.Sgc
{
    public class SgcModuloDto
    {
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Icono { get; set; } = string.Empty;
        public string ColorClase { get; set; } = string.Empty;
        public string? Controller { get; set; }
        public string? Action { get; set; }
        public List<SgcSubModuloDto> SubModulos { get; set; } = new();
        public bool TieneSubModulos => SubModulos.Any();
    }

    public class SgcSubModuloDto
    {
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Icono { get; set; } = string.Empty;
        public string Controller { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
    }
}
