namespace FabricaHilos.Models.Produccion.Planeamiento;

/// <summary>Catálogo AREA_RESP/MOTIVO (PLN_CAT_MOTIVO) para los combos dependientes
/// de la columna "Área Resp." en SeguimientoTintoreria.cshtml.</summary>
public class PlnCatMotivo
{
    public string AreaResp { get; set; } = "";
    public string Motivo   { get; set; } = "";
    public int    Orden    { get; set; }
}
