namespace FabricaHilos.Models.Produccion.Planeamiento;

/// <summary>
/// Resumen de actividad reciente de una máquina de hilandería (H_RPRODUC).
/// Agrupa los registros de las últimas 24h por tp_maq + cod_maq.
/// </summary>
public class PlnResumenHilanderia
{
    public string    TpMaq          { get; set; } = "";
    public string    CodMaq         { get; set; } = "";
    public string    Lote           { get; set; } = "";
    public string    Titulo         { get; set; } = "";
    public string    Proceso        { get; set; } = "";
    /// <summary>Estado máximo visto en las últimas 24h: '1'/'2' = en proceso, '3' = terminado.</summary>
    public string    EstadoMax      { get; set; } = "";
    public decimal   KgTotal        { get; set; }
    public int       HusosMax       { get; set; }
    public double    Velocidad      { get; set; }
    public DateTime? UltimaFechaIni { get; set; }
    public int       Registros      { get; set; }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// Activa si tiene estado '1' ó '2'
    public bool EsActiva => EstadoMax == "1" || EstadoMax == "2";

    /// Nombre descriptivo del tipo de máquina
    public string DescripcionTipo => TpMaq switch
    {
        "A" => "Automática",
        "B" => "Batán",
        "C" => "Continua (Ring)",
        "E" => "Peinadora",
        "G" => "Gaseadora",
        "J" => "Madeja",
        "L" => "Laminador",
        "M" => "Manuar",
        "P" => "Pabilera",
        "R" => "Reunidora",
        "T" => "Retorcedora",
        _   => TpMaq
    };

    /// Nombre del proceso hilandería
    public string NombreProceso => Proceso switch
    {
        "01" => "Cardado",
        "20" => "Peinado",
        "21" => "Peinado semi",
        "24" => "Peinado Gaseado",
        "33" => "Open End",
        "37" => "Carda+Peinado",
        _    => string.IsNullOrEmpty(Proceso) ? "—" : Proceso
    };

    /// Clase Bootstrap para el badge de estado
    public string BadgeClase => EsActiva ? "bg-success" : "bg-secondary";

    /// Color del ícono/acento por tipo de máquina
    public string ColorTipo => TpMaq switch
    {
        "A" => "#0d6efd",
        "C" => "#6610f2",
        "T" => "#20c997",
        "R" => "#fd7e14",
        "P" => "#0dcaf0",
        "M" => "#198754",
        "L" => "#6c757d",
        "E" => "#d63384",
        "G" => "#ffc107",
        "B" => "#495057",
        "J" => "#17a2b8",
        _   => "#6c757d"
    };
}
