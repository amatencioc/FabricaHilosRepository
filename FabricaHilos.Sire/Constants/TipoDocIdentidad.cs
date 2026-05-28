namespace FabricaHilos.Sire.Constants;

public static class TipoDocIdentidad
{
    public static readonly IReadOnlyDictionary<string, string> Tabla2 =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["0"] = "Sin documento",
            ["1"] = "DNI",
            ["4"] = "Carnet de Extranjería",
            ["6"] = "RUC",
            ["7"] = "Pasaporte",
            ["A"] = "Cédula Diplomática"
        };
}
