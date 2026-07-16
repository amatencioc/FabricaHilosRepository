namespace FabricaHilos.Models.Produccion.Planeamiento;

/// <summary>DTO unificado para guardar COLORHEXA + OBSERVACIONES via SP_PLN_UPD_ITEM_OBS_COLOR.</summary>
public class PlnSaveColorDto
{
    public decimal?  NroProg   { get; set; }   // surrogate key (preferido)
    public decimal?  NumPed    { get; set; }
    public decimal?  Nro       { get; set; }
    public decimal?  NumDet    { get; set; }
    public string?   Reproceso { get; set; }
    public DateTime? FchProg   { get; set; }
    public string?   ColorHexa { get; set; }
}

/// <summary>DTO unificado para guardar OBSERVACIONES + COLORHEXA via SP_PLN_UPD_ITEM_OBS_COLOR.</summary>
public class PlnSaveObsDto
{
    public decimal?  NroProg      { get; set; }   // surrogate key (preferido)
    public decimal?  NumPed       { get; set; }
    public decimal?  Nro          { get; set; }
    public decimal?  NumDet       { get; set; }
    public string?   Reproceso    { get; set; }
    public DateTime? FchProg      { get; set; }
    public string?   Observaciones { get; set; }
}

/// <summary>DTO para guardar AREA_RESP / MOTIVO / DESCRIPCION via SP_PLN_UPD_ITEM_MOTIVO.</summary>
public class PlnSaveMotivoDto
{
    public decimal?  NumPed      { get; set; }
    public decimal?  Nro         { get; set; }
    public decimal?  NumDet      { get; set; }
    public string?   Reproceso   { get; set; }
    public string?   AreaResp    { get; set; }
    public string?   Motivo      { get; set; }
    public string?   Descripcion { get; set; }
}

