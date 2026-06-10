namespace FabricaHilos.Controllers.Contabilidad;

using FabricaHilos.Sire.Models;

/// <summary>
/// ViewModel para vistas de RVIE/RCE con períodos y registros
/// </summary>
public class SireRegistrosViewModel
{
    public IReadOnlyList<PropuestaDto> Periodos { get; set; } = Array.Empty<PropuestaDto>();
    public string PeriodoSeleccionado { get; set; } = string.Empty;
    public IReadOnlyList<RegistroVenta> RegistrosVentas { get; set; } = Array.Empty<RegistroVenta>();
    public IReadOnlyList<RegistroCompra> RegistrosCompras { get; set; } = Array.Empty<RegistroCompra>();
    public bool EsMock { get; set; }
    public TipoRegistro Tipo { get; set; }
    public string? MensajeInfo { get; set; }
    public string Ruc { get; set; } = string.Empty;
}

public enum TipoRegistro
{
    Ventas,
    Compras
}
