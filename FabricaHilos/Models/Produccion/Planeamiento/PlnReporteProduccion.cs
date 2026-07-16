namespace FabricaHilos.Models.Produccion.Planeamiento;

/// <summary>
/// Resultado de PKG_PLN.SP_PLN_SEG_PROG_TINTORERIA.
/// 70 columnas en orden exacto del SP (v3.3).
/// Cols 0    = COLORHEXA (color de fila).
/// Cols 1-50 = hoja DT del Excel SEGUIMIENTO_PARTIDAS_TINTORERIA_KAREN.xlsm.
/// Cols 51-70 = columnas adicionales web (no en DT).
/// </summary>
public class PlnReporteProduccion
{
    // ── Col 0: Color de fila ───────────────────────────────────────────────────
    public string?   ColorHexa       { get; set; }   // 0  COLORHEXA

    // ── Cols 1-4: Dimensiones de tiempo
    public string?   Mes             { get; set; }   // 1  MES
    public string?   MesTex          { get; set; }   // 2  MES_TEX
    public string?   Ano             { get; set; }   // 3  ANO
    public string?   Sem             { get; set; }   // 4  SEM

    // ── Cols 5-12: Identificación del ítem ────────────────────────────────
    public string?   Partida         { get; set; }   // 5  PARTIDA
    public string?   Cliente         { get; set; }   // 6  CLIENTE
    public string?   Material        { get; set; }   // 7  MATERIAL
    public string?   Est             { get; set; }   // 8  EST
    public string?   Ne              { get; set; }   // 9  NE  (título/contaje)
    public string?   Mat             { get; set; }   // 10 MAT (tipo fibra)
    public string?   Lote            { get; set; }   // 11 LOTE
    public DateTime? FchPedido       { get; set; }   // 12 FCH_PEDIDO

    // ── Cols 13-15: 1er Rodete ────────────────────────────────────────────
    public DateTime? EstimaRod       { get; set; }   // 13 ESTIMA_ROD
    public DateTime? EntregRod       { get; set; }   // 14 ENTREG_ROD
    public decimal?  DiasRod         { get; set; }   // 15 DIAS_ROD
    public decimal?  XRod            { get; set; }   // 15b X_ROD   (semáforo rodete)

    // ── Cols 16-18: Material Hilandería ───────────────────────────────────
    public DateTime? EstimaMat       { get; set; }   // 16 ESTIMA_MAT
    public DateTime? EntregMat       { get; set; }   // 17 ENTREG_MAT
    public decimal?  DiasMh          { get; set; }   // 18 DIAS_MH

    // ── Col 19: Fecha guía ────────────────────────────────────────────────
    public DateTime? FchaGuia        { get; set; }   // 19 FCHA_GUIA

    // ── Cols 20-23: Receta TT ─────────────────────────────────────────────
    public DateTime? EstimaReceta    { get; set; }   // 20 ESTIMA_RECETA
    public DateTime? EntregReceta    { get; set; }   // 21 ENTREG_RECETA
    public decimal?  DiasRec         { get; set; }   // 22 DIAS_REC
    public decimal?  X               { get; set; }   // 23 X

    // ── Cols 24-28: Programa Tintorería ───────────────────────────────────
    public DateTime? FchPrograma     { get; set; }   // 24 FCH_PROGRAMA
    public string?   MaqTen          { get; set; }   // 25 MAQ_TEN
    public DateTime? EstimaTenido    { get; set; }   // 26 ESTIMA_TENIDO
    public DateTime? EntregTenido    { get; set; }   // 27 ENTREG_TENIDO
    public decimal?  DiasTenido      { get; set; }   // 28 DIAS_TENIDO
    public decimal?  XTenido         { get; set; }   // 28b X_TENIDO (semáforo tenido)

    // ── Cols 29-34: Fechas reales de producción ───────────────────────────
    public DateTime? FchPartida      { get; set; }   // 29 FCH_PARTIDA
    public DateTime? FchReceta       { get; set; }   // 30 FCH_RECETA
    public DateTime? FchSecRodete    { get; set; }   // 31 FCH_SEC_RODETE  (secado rodete  S01/S03)
    public DateTime? FchSecMadeja    { get; set; }   // 32 FCH_SEC_MADEJA  (secado madeja  S02/S04)
    public DateTime? FchAprobCal     { get; set; }   // 33 FCH_APROB_CAL
    public decimal?  TimeAprov       { get; set; }   // 34 TIME_APROV

    // ── Cols 34-38: Acabado, enconado, revisado
    public string?   TipoAcabado     { get; set; }   // 34  TIPO_ACABADO  (REDINA/CONERA)
    public string?   Acabado         { get; set; }   // 34b ACABADO      (RODETE/MADEJA/null)
    public DateTime? FchEnconado     { get; set; }   // 35 FCH_ENCONADO
    public DateTime? FchRevisado     { get; set; }   // 36 FCH_REVISADO
    public string?   EvEncon         { get; set; }   // 37 EV_ENCON
    public string?   Calificacion     { get; set; }   // 37b CALIFICACION (TT_RPRODUC último baño)

    // ── Cols 38-42: Entrega y días de espera ──────────────────────────────
    public DateTime? FchEntrega      { get; set; }   // 38 FCH_ENTREGA
    public DateTime? IngAlmpt        { get; set; }   // 39 ING_ALMPT
    public decimal?  DiasEnEspera    { get; set; }   // 40 DIAS_EN_ESPERA
    public decimal?  De              { get; set; }   // 41 DE
    public decimal?  DeCopia         { get; set; }   // 42 DE_COPIA

    // ── Cols 43-47: Kilogramos y tolerancia
    public decimal?  KgPedido        { get; set; }   // 43  KG_PEDIDO (kg pedidos cliente)
    public decimal?  KgProg          { get; set; }   // 44 KG_PROG
    public decimal?  KgDespa         { get; set; }   // 45 KG_DESPA
    public decimal?  Gap             { get; set; }   // 46 GAP
    public decimal?  PctToleran      { get; set; }   // 47 PCT_TOLERAN

    // ── Cols 47-48: Estado del flujo ──────────────────────────────────────
    public string?   EstadoFlujo     { get; set; }   // 47 ESTADO_FLUJO
    public string?   EstadoDespacho  { get; set; }   // 48 ESTADO_DESPACHO

    // ── Cols 49-50: Columnas de apoyo (NULL en el SP) ─────────────────────
    public string?   AreaResponsable { get; set; }   // 49 AREA_RESPONSABLE (legado, solo lectura)
    public string?   Bp              { get; set; }   // 50 BP

    // ── Área responsable / motivo de retraso (combos dependientes) + descripción libre
    public string?   AreaResp           { get; set; }   // AREA_RESP        (combo 1)
    public string?   MotivoRetraso      { get; set; }   // MOTIVO_RETRASO   (combo 2, depende de AreaResp)
    public string?   DescripcionMotivo  { get; set; }   // DESCRIPCION_MOTIVO (texto libre)

    // ── Cols 51-65: Columnas adicionales — no en DT Excel ─────────────────
    public decimal?  PesoNeto        { get; set; }   // 51 PESO_NETO
    public string?   Rmc             { get; set; }   // 52 RMC
    public string?   NroRmc          { get; set; }   // 53 NRO_RMC
    public string?   Titulo          { get; set; }   // 54 TITULO
    public string?   TituloTexto     { get; set; }   // 55 TITULO_TEXTO
    public string?   Referencia      { get; set; }   // 56 REFERENCIA
    public string?   ProcesoTt       { get; set; }   // 57 PROCESO_TT
    public string?   PartMatiz       { get; set; }   // 58 PART_MATIZ
    public string?   EstEvaluacion   { get; set; }   // 59 EST_EVALUACION
    public string?   Defecto         { get; set; }   // 60 DEFECTO
    public string?   Resultado       { get; set; }   // 61 RESULTADO
    public string?   LaboVal         { get; set; }   // 62 LABO_VAL
    public string?   AcaMad          { get; set; }   // 63 ACA_MAD
    public decimal?  DiasRetraso     { get; set; }   // 64 DIAS_RETRASO

    // ── Campos clave (edit/save)
    public decimal?  NroProg         { get; set; }   // NROPROG_DET
    public decimal?  NumPedKey       { get; set; }   // NUM_PED_KEY
    public decimal?  NroKey          { get; set; }   // NRO_KEY
    public decimal?  NumDetKey       { get; set; }   // NUM_DET_KEY
    public string?   ReprocesoKey    { get; set; }   // REPROCESO_KEY
    public DateTime? FchProgKey      { get; set; }   // FCH_PROG_KEY

    // ── Observaciones (guardado manual por usuario, no viene del SP) ────────
    public string?   Observaciones   { get; set; }   // campo editable en tabla

    // ── Helpers
    /// <summary>Indica si el ítem está vencido según el semáforo de despacho.</summary>
    public bool EstaRetrasado => EstadoDespacho == "VENCIDO";

    /// <summary>Clase CSS para colorear el semáforo de despacho.</summary>
    public string SemaforoClass => EstaRetrasado ? "text-danger fw-semibold" : "text-success";
}
