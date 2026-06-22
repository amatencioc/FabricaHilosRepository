namespace FabricaHilos.Controllers.Contabilidad;

using FabricaHilos.Models.Sire;
using FabricaHilos.Sire.Models;

/// <summary>
/// ViewModel para vistas de RVIE/RCE con períodos y registros
/// </summary>
public class SireRegistrosViewModel
{
    public IReadOnlyList<PropuestaDto> Periodos { get; set; } = Array.Empty<PropuestaDto>();
    public string PeriodoSeleccionado { get; set; } = string.Empty;
    public IReadOnlyList<RegistroVenta>  RegistrosVentas  { get; set; } = Array.Empty<RegistroVenta>();
    public IReadOnlyList<RegistroCompra> RegistrosCompras { get; set; } = Array.Empty<RegistroCompra>();

    /// <summary>Registros del período seleccionado almacenados en SIRE_PROPUESTA (propuesta SUNAT).</summary>
    public IReadOnlyList<SireValidaRegistro> RegistrosPropuesta { get; set; }
        = new List<SireValidaRegistro>();

    /// <summary>Registros ERP del período seleccionado almacenados en SIRE_LEGACY.</summary>
    public IReadOnlyList<SireLegacyRegistro> RegistrosLegacy { get; set; }
        = new List<SireLegacyRegistro>();

    /// <summary>Detalle cruzado fila a fila del período almacenado en SIRE_CONCIL.</summary>
    public IReadOnlyList<SireConcilDetalle> RegistrosConcil { get; set; }
        = new List<SireConcilDetalle>();

    /// <summary>Resumen de TODOS los períodos descargados en SIRE_PROPUESTA para este tipo.</summary>
    public IReadOnlyList<PropuestaPeriodoResumen> PropuestasResumen { get; set; }
        = new List<PropuestaPeriodoResumen>();

    public bool EsMock { get; set; }
    public TipoRegistro Tipo { get; set; }
    public string? MensajeInfo { get; set; }
    public string Ruc { get; set; } = string.Empty;

    /// <summary>true si existe un ZIP local ya descargado para el período seleccionado.</summary>
    public bool TieneZipLocal { get; set; }
    /// <summary>Nombre del archivo ZIP local más reciente, para mostrarlo en el modal.</summary>
    public string? NombreZipLocal { get; set; }
    /// <summary>NombreArchivo del job completado para la descarga de constancia SUNAT. Null si no hubo exportación.</summary>
    public string? NombreArchivoConstancia { get; set; }
    /// <summary>Resumen de última conciliación ejecutada para el período seleccionado. Null si nunca se concilió.</summary>
    public SireConcilResumen? ConcilResumen { get; set; }

    // ── SSCO ─────────────────────────────────────────────────────────────────
    /// <summary>
    /// Set de RUCs presentes en SSCO_LISTA (Sujetos Sin Capacidad Operativa).
    /// Vacío si la tabla no tiene datos o la carga aún no se ha realizado.
    /// </summary>
    public HashSet<string> RucsEnSsco { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Fecha de la última carga del padrón SSCO. Null si nunca se cargó.</summary>
    public DateTime? SscoFchUltimaCarga { get; set; }
    /// <summary>Período YYYYMM de la última carga del padrón SSCO.</summary>
    public int? SscoPeriodoCarga { get; set; }
    /// <summary>
    /// Número de registros Legacy del período cuyo RUC figura en la lista SSCO.
    /// Calculado en el controller para evitar el scan LINQ en la vista.
    /// </summary>
    public int SscoHits { get; set; }
    /// <summary>
    /// Lista completa del padrón SSCO_LISTA (todas las columnas del Excel de SUNAT).
    /// </summary>
    public List<FabricaHilos.Models.Sire.SscoListaEntry> SscoLista { get; set; } = new();
}

public enum TipoRegistro
{
    Ventas,
    Compras
}
