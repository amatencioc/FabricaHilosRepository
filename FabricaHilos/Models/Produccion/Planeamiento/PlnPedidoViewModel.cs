using FabricaHilos.Models.Produccion.Planeamiento;

namespace FabricaHilos.Models.Produccion.Planeamiento;

/// <summary>ViewModel compuesto para la vista de trazabilidad de un pedido.</summary>
public class PlnPedidoViewModel
{
    public long   NumPed { get; set; }
    public int    Serie  { get; set; }

    public IEnumerable<PlnSeguimiento>  Items    { get; set; } = [];
    public IEnumerable<PlnLogEvento>    Eventos  { get; set; } = [];
    public IEnumerable<PlnAlerta>       Alertas  { get; set; } = [];
    public IEnumerable<PlnEstadoCodigo> Pasos    { get; set; } = [];

    /// <summary>
    /// Detalle completo de Tintorería por partida.
    /// Clave: PARTIDA.NUMERO (PlnSeguimiento.NumPartida).
    /// Incluye baños ejecutados, secado, CC TT y validación de laboratorio.
    /// </summary>
    public Dictionary<long, PlnDetalleTt> DetalleTt { get; set; } = [];
}
