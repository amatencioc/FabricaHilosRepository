namespace FabricaHilos.Helpers
{
    public static class ProduccionLookups
    {
        public static readonly Dictionary<string, string> Turnos = new()
        {
            { "1", "1er Turno" },
            { "2", "2do Turno" },
            { "3", "3er Turno" },
            { "4", "12 Hrs Día" },
            { "5", "12 Hrs Noche" }
        };

        public static readonly Dictionary<string, string> Pasos = new()
        {
            { "1", "1ER" },
            { "2", "2DO" },
            { "3", "3ER" },
            { "PRE", "PRE" },
            { "POST", "POST" }
        };

        public static string GetTurno(string? codigo) =>
            !string.IsNullOrEmpty(codigo) && Turnos.TryGetValue(codigo, out var desc) ? desc : (codigo ?? "-");

        public static string GetPaso(string? codigo) =>
            !string.IsNullOrEmpty(codigo) && Pasos.TryGetValue(codigo, out var desc) ? desc : (codigo ?? "-");
    }
}
