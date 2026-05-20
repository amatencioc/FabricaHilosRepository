namespace FabricaHilos.Models.Produccion.Planeamiento;

public class PlnEstadoCodigo
{
    public string CodPaso     { get; set; } = "";
    public string NombrePaso  { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public int    OrdenPaso   { get; set; }
    public string ColorUi     { get; set; } = "#6c757d";
    public string EsFinal     { get; set; } = "N";

    // ORA-00904: AREA no existe en PLN_ESTADO_CODIGO (§2.2 PKG_PLN.sql).
    // Se deriva del CodPaso según el flujo de producción documentado.
    public string Area => CodPaso switch
    {
        "01"  => "Ventas",
        "02"  => "Planeamiento",
        "03"                  => "Laboratorio",   // v2.1: L_VALIDA_RECETA
        "04" or "05"          => "Hilandería",    // v2.1: PARTIDA / H_RPRODUC
        "06" or "07" or "08" or "9R" => "Tintorería",
        "09"  => "Calidad",
        "09B" => "Acabados",
        "10"  => "Devanado",
        "11"  => "Calidad",
        "12" or "13"          => "Almacén PT",
        "14"  => "Despacho",
        _     => ""
    };

    /// <summary>Texto de ayuda contextual para mostrar en el combobox de filtro.</summary>
    public string Tooltip => CodPaso switch
    {
        "01"  => "Área: Ventas — Pedido registrado, aún sin planificar.",
        "02"  => "Área: Planeamiento — Ítem planificado, asignado a programa de producción.",
        "03"  => "Área: Laboratorio — Receta de tintorería validada en laboratorio (L_VALIDA_RECETA ESTADO='3'). Paso previo a Hilandería.",
        "04"  => "Área: Hilandería — Lote asignado a producción (PARTIDA INSERT, NROPROG asignado). Inicio de proceso de hilado.",
        "05"  => "Área: Hilandería — Lote de hilo disponible (H_RPRODUC INSERT; dead code en sistema 2026, PASO '04' activo).",
        "06"  => "Área: Tintorería — Lote ingresado a tintorería, tintura en curso.",
        "07"  => "Área: Tintorería — Tenido completado, pendiente de secado.",
        "08"  => "Área: Tintorería — En proceso de secado post-tintura.",
        "09"  => "Área: Calidad — Control de calidad tintorería aprobado (CC TT OK).",
        "09B" => "Área: Acabados — Gaseado (solo proceso 24). Aplica exclusivamente a hilo PROCESO='24'.",
        "9R"  => "Área: Tintorería — CC TT rechazado; ítem en reproceso de tintura. Incrementa NRO_CICLO.",
        "10"  => "Área: Devanado — Transferencia de lotes a conos/madejas para despacho.",
        "11"  => "Área: Calidad — Revisión final antes de ingresar a almacén PT.",
        "12"  => "Área: Almacén PT — Ingresado a almacén de producto terminado.",
        "13"  => "Área: Almacén PT — Listo para despacho (o stock disponible para entrega).",
        "14"  => "Área: Despacho — Despachado/Cerrado. Despacho parcial retrocede a etapa 13.",
        _     => ""
    };
}
