namespace FabricaHilos.Models.Produccion.Planeamiento;

public class PlnCargaDiaria
{
    public DateTime Fecha           { get; set; }
    public string   CodMaq          { get; set; } = "";
    public string?  NombreMaq       { get; set; }
    public string   TpMaq           { get; set; } = "";  // 'H'=Hilandería, 'T'=Tintorería
    public double   HorasCapacidad  { get; set; }
    public decimal  KgCapacidad     { get; set; }
    public double   HorasAsignadas  { get; set; }
    public decimal  KgAsignados     { get; set; }
    public int      NroPedidos      { get; set; }
    public double   HorasReal       { get; set; }   // horas_real (V_PLN_CARGA_MAQUINAS §8.5)
    public decimal  KgReal          { get; set; }   // kg_real    (V_PLN_CARGA_MAQUINAS §8.5)
    public double   PctUtilizacion  { get; set; }
    public double   PctCarga        { get; set; }
    public string   IndSobrecargada { get; set; } = "N";
    public string   EstadoCarga     { get; set; } = "";  // SOBRECARGADA/CARGA_ALTA/CARGA_MEDIA/DISPONIBLE

    // ORA-00904: AREA no existe en PLN_CARGA_DIARIA. Se deriva de TP_MAQ (§2.6 PKG_PLN.sql).
    public string Area => TpMaq == "H" ? "Hilandería" : TpMaq == "T" ? "Tintorería" : "";

    public decimal CapacidadKgDia  => KgCapacidad;
    public decimal DiferenciaKg    => KgAsignados - KgCapacidad;
    public bool    EstaSobrecargada => IndSobrecargada == "S";

    public string ColorSemaforo => PctCarga switch
    {
        > 95 => "#dc3545",
        > 80 => "#fd7e14",
        > 50 => "#ffc107",
        _    => "#198754"
    };
}
