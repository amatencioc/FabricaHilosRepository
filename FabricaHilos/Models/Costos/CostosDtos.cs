namespace FabricaHilos.Models.Costos;

/// <summary>Resultado completo de la búsqueda: cabecera de la cotización + ítems (contexto) + el
/// último cálculo real ejecutado por PKG_COS_COSTEO.SP_CALCULAR_COTIZACION (si existe).</summary>
public class CotizacionCascadaDto
{
    public string  Tipodoc      { get; set; } = "";
    public int     Serie        { get; set; }
    public int     Numero       { get; set; }
    public DateTime Fecha       { get; set; }
    public string  CodCliente   { get; set; } = "";
    public string? NombreCliente { get; set; }
    public string? CodVende     { get; set; }
    public string? Estado       { get; set; }
    /// <summary>Descripción de <see cref="Estado"/> vía H_TPROD (TABLA='64', "ESTADO DE COTIZACION") —
    /// ver _MEMORIA_LEGACY_COTIZACIONES.md §3.9. Null si el código no está en el catálogo.</summary>
    public string? EstadoDescripcion { get; set; }
    public string? Moneda       { get; set; }
    public string? Observaciones { get; set; }

    public List<CotizacionItemContextoDto> Items { get; set; } = new();

    /// <summary>Último cálculo (COS_COTIZACION_CALC + _D) — null si nunca se ha calculado esta cotización.</summary>
    public CosCalcResultDto? Calc { get; set; }

    /// <summary>Costo/precio calculado POR ÍTEM (derivado de Calc, nunca de COTIZACION_D/COTIZACION_P) —
    /// es la fuente para la pantalla "Cotización" estilo legacy. Vacío si Calc es null.</summary>
    public List<ItemCosteoCalculadoDto> ItemsCalculados { get; set; } = new();

    /// <summary>COTIZACION_G.MONEDA está NULL en ~1/3 de las cotizaciones reales (dato histórico
    /// incompleto, no es un bug de esta app) — si falta, se usa la moneda con la que el motor de
    /// costeo realmente calculó (Calc.Moneda), que siempre está poblada.</summary>
    public string? MonedaEfectiva => !string.IsNullOrWhiteSpace(Moneda) ? Moneda : Calc?.Moneda;
}

/// <summary>Datos de un ítem (COTIZACION_D) — solo contexto para mostrar junto al cálculo,
/// el motor de costeo (SP_CALCULAR_COTIZACION) es quien decide la ruta/proyección a usar.</summary>
public class CotizacionItemContextoDto
{
    public int     Item        { get; set; }
    public string? CodServ     { get; set; }
    public string? Titulo      { get; set; }
    public string? TituloDescripcion { get; set; }
    public string? Proceso     { get; set; }
    public string? ProcesoDescripcion { get; set; }
    public string? Fibra1      { get; set; }
    public string? Fibra1Descripcion { get; set; }
    public string? Fibra2      { get; set; }
    public string? Fibra2Descripcion { get; set; }
    public string? ColorDet    { get; set; }
    public string? Intensidad  { get; set; }
    public decimal? Cantidad   { get; set; }
    public decimal? Precio     { get; set; }
    public decimal? PrecioSugerido { get; set; }
    public decimal? PrecioMax  { get; set; }
    public string? Estado      { get; set; }

    public List<CotizacionRangoDto> Rangos { get; set; } = new();
}

/// <summary>Detalle por rango de kg (COTIZACION_P) — precio/costo/rentabilidad histórico por tramo.</summary>
public class CotizacionRangoDto
{
    public int      Rango        { get; set; }
    public decimal? Precio       { get; set; }
    public decimal? Costo        { get; set; }
    public decimal? PorcRent     { get; set; }
    public decimal? PrecioElegido{ get; set; }
    public decimal? PrecioMax    { get; set; }
    public decimal? PorcRentMax  { get; set; }
}

/// <summary>Igual a <see cref="CotizacionRangoDto"/> pero con el ITEM al que pertenece —
/// usado solo internamente en CostosService para agrupar por ítem tras el query único.</summary>
public class CotizacionRangoItemDto : CotizacionRangoDto
{
    public int ItemRef { get; set; }
}

/// <summary>Cabecera del último cálculo real (COS_COTIZACION_CALC) hecho por el motor de costeo.</summary>
public class CosCalcResultDto
{
    public int      IdCalc       { get; set; }
    public int      NroVersion   { get; set; }
    public DateTime FecCalculo   { get; set; }
    public string   UsuarioCalculo { get; set; } = "";
    public decimal  PctMargen    { get; set; }

    public decimal  CostoHilanderia { get; set; }
    public decimal  CostoTintoreria { get; set; }
    public decimal  CostoMoi     { get; set; }
    public decimal  CostoCif     { get; set; }
    public decimal  CostoGastosOpfin { get; set; }
    public decimal  CostoGas     { get; set; }
    public decimal  CostoAgua    { get; set; }
    public decimal  CostoEnel    { get; set; }
    public decimal  CostoTotal   { get; set; }
    public decimal  PrecioFinal  { get; set; }

    // Tasas ($/kg) realmente usadas por el motor — necesarias para prorratear moi/cif/gof/
    // gas/agua/enel POR ÍTEM (misma fórmula que el motor, solo con kg del ítem en vez del total).
    public decimal? CostoMoiUsado  { get; set; }
    public decimal? CostoCifUsado  { get; set; }
    public decimal? CostoGofUsado  { get; set; }
    public decimal? CostoGasUsado  { get; set; }
    public decimal? CostoAguaUsado { get; set; }
    public decimal? CostoEnelUsado { get; set; }

    public decimal? CostoMateriaPrima { get; set; }
    public string   IndMateriaPrimaCompleta { get; set; } = "S";
    public decimal? CostoTotalConMp  { get; set; }
    public decimal? PrecioFinalConMp { get; set; }

    public string   Moneda       { get; set; } = "";
    public string   Estado       { get; set; } = "A";
    public string?  Observaciones { get; set; }

    public string   IndTieneEstimados { get; set; } = "N";
    public string?  NivelProyeccionPeor { get; set; }
    public decimal? PctConfiabilidadPeor { get; set; }
    public string?  DescripcionNivelPeor { get; set; }

    /// <summary>'N' si alguna etapa quedó sin poder costearse por config incompleta (hoy: CICLO_FIJO
    /// sin HORAS_CICLO) — ver PKG_COS_COSTEO v2.4 / 31_COS_ALTER_ETAPA_SIN_CONFIG.sql.</summary>
    public string   IndEtapasCompletas { get; set; } = "S";

    public List<CosCalcLineaDto> Lineas { get; set; } = new();

    public IEnumerable<IGrouping<int, CosCalcLineaDto>> LineasPorItem => Lineas.GroupBy(l => l.Item);

    /// <summary>Suma de las etapas de químicos/colorantes de tintorería (concepto COS_TASA
    /// 'COSTO_QUIM_TIN', agregado 25/08/2026) — YA está incluida dentro de CostoTintoreria, esto
    /// es solo el desglose informativo, no se debe sumar de nuevo al total. Se identifica por
    /// NOMBRE_ETAPA (contiene "QUIMIC") porque COS_COTIZACION_CALC_D no guarda el
    /// COD_CONCEPTO_TASA de cada línea, solo el nombre de la etapa.</summary>
    public decimal CostoQuimicos => Lineas.Where(l => l.NombreEtapa.IndexOf("QUIMIC", StringComparison.OrdinalIgnoreCase) >= 0).Sum(l => l.MontoParcial);
}

/// <summary>Una línea de detalle (COS_COTIZACION_CALC_D): una etapa real costeada, o una fila de
/// materia prima (IND_ES_MATERIA_PRIMA='S', ORDEN_ETAPA negativo, sin relación con COS_RUTA_ETAPA).</summary>
public class CosCalcLineaDto
{
    public int      Item          { get; set; }
    public int      OrdenEtapa    { get; set; }
    public string   NombreEtapa   { get; set; } = "";
    public string?  CodMaquina    { get; set; }
    public string?  ComponenteFibra { get; set; }
    public decimal  CantidadKg    { get; set; }
    public decimal? PctMermaAplicado { get; set; }
    public decimal? TasaAplicada  { get; set; }
    public string?  UnidadTasa    { get; set; }
    public decimal  MontoParcial  { get; set; }
    public string   IndEstimado   { get; set; } = "N";
    public string?  NivelProyeccion { get; set; }
    public decimal? PctConfiabilidad { get; set; }
    public string?  DescripcionNivel { get; set; }
    public string   IndEsMateriaPrima { get; set; } = "N";
    public string?  IndMpSinCosto { get; set; }
    public string?  IndEtapaSinConfig { get; set; }
    public string?  Observaciones { get; set; }

    public bool EsMateriaPrima => IndEsMateriaPrima == "S";
    public bool EsEstimado     => IndEstimado == "S";
    public bool EsSinConfig    => IndEtapaSinConfig == "S";

    /// <summary>CantidadKg/MontoParcial normalizados a 1 kg de producto terminado (mismo criterio
    /// con el que se arman las fichas Excel — ver V_COS_COTIZACION_CALC_DET.CANTIDAD_KG_X1KG).
    /// CantidadKg/MontoParcial crudos quedan sin cambios (base del "Simulador de precio por
    /// cantidad" y de COSTO_TOTAL/PRECIO_FINAL, que amortizan bien las etapas CICLO_FIJO sobre
    /// el lote real). cantidadItem viene de COTIZACION_D.CANTIDAD del ítem (nunca de CantidadKg,
    /// que varía por etapa por la merma en cascada).</summary>
    public decimal? CantidadKgX1Kg(decimal? cantidadItem) => cantidadItem > 0 ? Math.Round(CantidadKg / cantidadItem.Value, 6) : null;
    public decimal? MontoParcialX1Kg(decimal? cantidadItem) => cantidadItem > 0 ? Math.Round(MontoParcial / cantidadItem.Value, 6) : null;
}

/// <summary>Costo/precio calculado de UN ítem, derivado 100% de <see cref="CosCalcLineaDto"/> +
/// las tasas ($/kg) de <see cref="CosCalcResultDto"/> — misma fórmula que usa el motor para el
/// total de la cotización (COSTO_TOTAL/PRECIO_FINAL), aplicada con el kg del ítem en vez del kg
/// total. Nunca lee COTIZACION_D.PRECIO/PRECIO_SUGERIDO/PRECIO_MAX ni COTIZACION_P (esos son
/// datos históricos de BD, no del motor de costeo). Suma de todos los ítems = totales de Calc.</summary>
public class ItemCosteoCalculadoDto
{
    public int     Item              { get; set; }
    public decimal CantidadKg        { get; set; }
    public decimal CostoTransformacion { get; set; } // suma de etapas (hilandería + tintorería)
    public decimal CostoMateriaPrima { get; set; }
    public decimal CostoMoi          { get; set; }
    public decimal CostoCif          { get; set; }
    public decimal CostoGastosOpfin  { get; set; }
    public decimal CostoGas          { get; set; }
    public decimal CostoAgua         { get; set; }
    public decimal CostoEnel         { get; set; }
    public bool    TieneEstimados    { get; set; }

    /// <summary>Parte de CostoTransformacion que NO escala con el kg (etapas UNIDAD_TASA='CICLO_FIJO':
    /// horas_ciclo*tasa, fijo por corrida) — necesario para simular precio a otra cantidad.</summary>
    public decimal CostoFijo         { get; set; }

    public decimal CostoTotal       => CostoTransformacion + CostoMoi + CostoCif + CostoGastosOpfin + CostoGas + CostoAgua + CostoEnel;
    public decimal CostoTotalConMp  => CostoTotal + CostoMateriaPrima;
    public decimal? PrecioCalculado(decimal pctMargen) => CantidadKg > 0 ? Math.Round(CostoTotal * (1 + pctMargen / 100) / CantidadKg, 6) : null;
    public decimal? PrecioCalculadoConMp(decimal pctMargen) => CantidadKg > 0 ? Math.Round(CostoTotalConMp * (1 + pctMargen / 100) / CantidadKg, 6) : null;

    /// <summary>Simula el costo/precio a OTRA cantidad hipotética: la parte fija (CostoFijo) no
    /// cambia, el resto escala proporcional al kg (mismo criterio que el motor: horas/kg, tasas $/kg,
    /// materia prima $/kg). Ninguno de estos valores viene de COTIZACION_P/COTIZACION_RANGO (legacy);
    /// se deriva 100% de las líneas ya calculadas por PKG_COS_COSTEO para este ítem.</summary>
    public decimal? SimularCostoTotal(decimal kgSimulado) =>
        CantidadKg > 0 && kgSimulado > 0 ? CostoFijo + (CostoTotal - CostoFijo) * (kgSimulado / CantidadKg) : null;

    public decimal? SimularCostoTotalConMp(decimal kgSimulado) =>
        CantidadKg > 0 && kgSimulado > 0 ? CostoFijo + (CostoTotalConMp - CostoFijo) * (kgSimulado / CantidadKg) : null;

    public decimal? SimularPrecio(decimal kgSimulado, decimal pctMargen) =>
        SimularCostoTotal(kgSimulado) is decimal c ? Math.Round(c * (1 + pctMargen / 100) / kgSimulado, 6) : null;

    public decimal? SimularPrecioConMp(decimal kgSimulado, decimal pctMargen) =>
        SimularCostoTotalConMp(kgSimulado) is decimal c ? Math.Round(c * (1 + pctMargen / 100) / kgSimulado, 6) : null;

    /// <summary>Costo/precio de referencia "a 1 kg" — igual a como la hoja Excel de la contadora
    /// expresa el costo por kilo (unitario) ANTES de multiplicarlo por la cantidad real del
    /// pedido. Simple caso particular de <see cref="SimularCostoTotal"/> con kg=1; null si
    /// CantidadKg del último cálculo es 0 (no se puede derivar una tasa unitaria sin un cálculo
    /// real con kg&gt;0 detrás).</summary>
    public decimal? CostoUnitario1Kg      => SimularCostoTotal(1);
    public decimal? CostoUnitario1KgConMp => SimularCostoTotalConMp(1);
    public decimal? PrecioUnitario1Kg(decimal pctMargen)      => SimularPrecio(1, pctMargen);
    public decimal? PrecioUnitario1KgConMp(decimal pctMargen) => SimularPrecioConMp(1, pctMargen);
}
