namespace FabricaHilos.Models.Produccion.Planeamiento;

/// <summary>
/// Fila de PLN_PARAM (§2.1 PKG_PLN). Tabla de configuración clave-valor del módulo.
/// Permite ajustar umbrales de alertas, horas de turno y buffers sin tocar PL/SQL.
/// Modificar con: UPDATE PLN_PARAM SET VALOR_NUM=x WHERE COD_PARAM='NOMBRE';
/// </summary>
public class PlnParam
{
    public string    CodParam    { get; set; } = "";
    public string    Descripcion { get; set; } = "";
    public decimal?  ValorNum    { get; set; }
    public string?   ValorText   { get; set; }
    public DateTime? ValorDate   { get; set; }
    public string?   AMduser     { get; set; }
    public DateTime? AMdfecha    { get; set; }

    // ── Valores por defecto (para mostrar en UI sin ir a BD) ────────────────
    public static readonly IReadOnlyDictionary<string, (decimal Valor, string Descripcion)> Defaults =
        new Dictionary<string, (decimal, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["HRS_HILANDERIA"]    = (22,  "Horas/día operativas hilandería"),
            ["HRS_TINTORERIA"]    = (24,  "Horas/día operativas tintorería"),
            ["HRS_SECADO"]        = (8,   "Horas buffer post-secado"),
            ["DIAS_BUFFER_LAB"]   = (1,   "Días laboratorio antes de TT"),
            ["DIAS_BUFFER_QC"]    = (1,   "Días control calidad post-secado"),
            ["DIAS_BUFFER_DESP"]  = (1,   "Días para preparar despacho"),
            ["DIAS_ALERTA_CRIT"]  = (7,   "Días retraso → alerta CRÍTICA"),
            ["DIAS_ALERTA_ALTA"]  = (3,   "Días retraso → alerta ALTA"),
            ["DIAS_ALERTA_MEDIA"] = (1,   "Días retraso → alerta MEDIA"),
        };
}
