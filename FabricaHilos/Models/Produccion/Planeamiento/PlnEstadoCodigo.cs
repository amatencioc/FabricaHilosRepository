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
        "03" or "04"          => "Hilandería",
        "05"                  => "Laboratorio",
        "06" or "07" or "08" or "9R" => "Tintorería",
        "09"  => "Calidad",
        "09B" => "Acabados",
        "10"  => "Devanado",
        "11"  => "Calidad",
        "12" or "13"          => "Almacén PT",
        "14"  => "Despacho",
        _     => ""
    };
}
