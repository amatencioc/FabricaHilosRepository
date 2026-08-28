using Dapper;
using FabricaHilos.Models.Costos;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Data;

namespace FabricaHilos.Services.Costos;

public interface ICostosService
{
    /// <summary>
    /// Busca una cotización (COTIZACION_G/D/P) por número y adjunta el último cálculo real
    /// (COS_COTIZACION_CALC/_D) hecho por PKG_COS_COSTEO.SP_CALCULAR_COTIZACION, si existe.
    /// Devuelve null si el número no existe. Lanza <see cref="InvalidOperationException"/> con un
    /// mensaje ya limpio (sin traza Oracle) si la BD falla al consultar.
    /// </summary>
    Task<CotizacionCascadaDto?> BuscarCotizacionAsync(int numero);

    /// <summary>
    /// Ejecuta (o re-ejecuta, quedando como nueva versión) el cálculo real de costeo para la
    /// cotización, invocando PKG_COS_COSTEO.SP_CALCULAR_COTIZACION (ruta vigente → proyectada →
    /// regresión → mezclas: TODO resuelto por el motor, no por esta capa). Devuelve
    /// (IdCalc, Error): IdCalc=0 y Error informado si la BD rechaza el cálculo.
    /// </summary>
    Task<(int IdCalc, string? Error)> CalcularAsync(int numero, decimal pctMargen, string usuario);
}

public class CostosService : OracleServiceBase, ICostosService
{
    private readonly ILogger<CostosService> _logger;

    // COTIZACION_G/D/P y COS_COTIZACION_CALC solo tienen un valor real de TIPODOC/SERIE en toda la BD.
    private const string Tipodoc = "CT";
    private const int    Serie   = 1;

    public CostosService(IConfiguration cfg, IHttpContextAccessor http, ILogger<CostosService> logger)
        : base(cfg, http) { _logger = logger; }

    public async Task<CotizacionCascadaDto?> BuscarCotizacionAsync(int numero)
    {
        try
        {
            return await BuscarCotizacionInternalAsync(numero);
        }
        catch (OracleException ex)
        {
            _logger.LogError(ex, "[Costos] Error al buscar cotización {Numero}", numero);
            throw new InvalidOperationException(LimpiarMensajeOracle(ex.Message), ex);
        }
    }

    private async Task<CotizacionCascadaDto?> BuscarCotizacionInternalAsync(int numero)
    {
        await using var db = new OracleConnection(GetOracleConnectionString());

        var cabecera = await db.QueryFirstOrDefaultAsync<CotizacionCascadaDto>(
            $@"SELECT g.tipodoc AS Tipodoc, g.serie AS Serie, g.numero AS Numero, g.fecha AS Fecha,
                      g.cod_cliente AS CodCliente, c.nombre AS NombreCliente,
                      g.cod_vende AS CodVende, g.estado AS Estado, te.descripcion AS EstadoDescripcion,
                      g.moneda AS Moneda, g.observaciones AS Observaciones
               FROM   {S}COTIZACION_G g
               LEFT JOIN {S}CLIENTES c ON c.cod_cliente = g.cod_cliente
               LEFT JOIN {S}H_TPROD te ON te.tabla = '64' AND te.codigo = g.estado
               WHERE  g.tipodoc = :tipodoc AND g.serie = :serie AND g.numero = :numero",
            new { tipodoc = Tipodoc, serie = Serie, numero });

        if (cabecera == null)
            return null;

        cabecera.Items = (await db.QueryAsync<CotizacionItemContextoDto>(
            $@"SELECT d.item AS Item, d.cod_serv AS CodServ,
                      d.titulo AS Titulo, ht.descripcion AS TituloDescripcion,
                      d.proceso AS Proceso, hp.descripcion AS ProcesoDescripcion,
                      d.fibra1 AS Fibra1, vf1.descripcion AS Fibra1Descripcion,
                      d.fibra2 AS Fibra2, vf2.descripcion AS Fibra2Descripcion,
                      d.color_det AS ColorDet, d.intensidad AS Intensidad,
                      d.cantidad AS Cantidad, d.precio AS Precio,
                      d.precio_sugerido AS PrecioSugerido, d.precio_max AS PrecioMax, d.estado AS Estado
               FROM   {S}COTIZACION_D d
               LEFT JOIN {S}H_TITULOS ht ON ht.titulo = d.titulo
               LEFT JOIN {S}H_PROCESOS hp ON hp.proceso = d.proceso
               LEFT JOIN {S}V_FIBRA vf1 ON vf1.fibra = d.fibra1
               LEFT JOIN {S}V_FIBRA vf2 ON vf2.fibra = d.fibra2
               WHERE  d.tipodoc = :tipodoc AND d.serie = :serie AND d.numero = :numero
               ORDER  BY d.item",
            new { tipodoc = Tipodoc, serie = Serie, numero })).ToList();

        var rangos = (await db.QueryAsync<CotizacionRangoItemDto>(
            $@"SELECT p.item AS ItemRef, p.rango AS Rango, p.precio AS Precio, p.costo AS Costo,
                      p.porc_rent AS PorcRent, p.precio_elegido AS PrecioElegido,
                      p.precio_max AS PrecioMax, p.porc_rent_max AS PorcRentMax
               FROM   {S}COTIZACION_P p
               WHERE  p.tipodoc = :tipodoc AND p.serie = :serie AND p.numero = :numero
               ORDER  BY p.item, p.rango",
            new { tipodoc = Tipodoc, serie = Serie, numero })).ToList();

        foreach (var item in cabecera.Items)
            item.Rangos = rangos.Where(r => r.ItemRef == item.Item).Cast<CotizacionRangoDto>().ToList();

        cabecera.Calc = await ObtenerUltimoCalcAsync(db, numero);
        cabecera.ItemsCalculados = CalcularItems(cabecera.Calc, cabecera.Items);
        return cabecera;
    }

    /// <summary>Deriva el costo/precio por ítem a partir de las líneas del cálculo (COS_COTIZACION_CALC_D)
    /// y las tasas $/kg de la cabecera — nunca de COTIZACION_D.PRECIO/PRECIO_SUGERIDO/PRECIO_MAX ni
    /// COTIZACION_P. El kg de cada ítem se toma de COTIZACION_D.CANTIDAD (mismo valor que el motor
    /// acumula en v_total_kg — NUNCA de COS_COTIZACION_CALC_D.CANTIDAD_KG, que varía por etapa por
    /// la merma en cascada). Ver comentario de <see cref="ItemCosteoCalculadoDto"/>.</summary>
    private static List<ItemCosteoCalculadoDto> CalcularItems(CosCalcResultDto? calc, List<CotizacionItemContextoDto> items)
    {
        if (calc == null) return new();

        return calc.Lineas
            .GroupBy(l => l.Item)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var kg = items.FirstOrDefault(i => i.Item == g.Key)?.Cantidad ?? 0;
                return new ItemCosteoCalculadoDto
                {
                    Item = g.Key,
                    CantidadKg = kg,
                    CostoTransformacion = g.Where(l => !l.EsMateriaPrima).Sum(l => l.MontoParcial),
                    CostoFijo = g.Where(l => !l.EsMateriaPrima && l.UnidadTasa == "CICLO_FIJO").Sum(l => l.MontoParcial),
                    CostoMateriaPrima = g.Where(l => l.EsMateriaPrima).Sum(l => l.MontoParcial),
                    CostoMoi = kg * (calc.CostoMoiUsado ?? 0),
                    CostoCif = kg * (calc.CostoCifUsado ?? 0),
                    CostoGastosOpfin = kg * (calc.CostoGofUsado ?? 0),
                    CostoGas = kg * (calc.CostoGasUsado ?? 0),
                    CostoAgua = kg * (calc.CostoAguaUsado ?? 0),
                    CostoEnel = kg * (calc.CostoEnelUsado ?? 0),
                    TieneEstimados = g.Any(l => l.EsEstimado)
                };
            })
            .ToList();
    }

    public async Task<(int IdCalc, string? Error)> CalcularAsync(int numero, decimal pctMargen, string usuario)
    {
        await using var db = new OracleConnection(GetOracleConnectionString());
        await db.OpenAsync();
        using var tran = db.BeginTransaction();
        try
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = $"BEGIN {S}PKG_COS_COSTEO.SP_CALCULAR_COTIZACION(:tipodoc,:serie,:numero,:margen,:usuario,:idcalc); END;";
            // La ruta vigente/proyectada/regresión/mezclas puede tardar en cotizaciones con muchos
            // ítems — el timeout por defecto de OracleCommand (15s) podía cortar el cálculo a mitad
            // de camino sin avisar claramente al usuario (bug silencioso).
            cmd.CommandTimeout = 180;
            cmd.Parameters.Add("tipodoc", OracleDbType.Varchar2, Tipodoc, ParameterDirection.Input);
            cmd.Parameters.Add("serie", OracleDbType.Int32, Serie, ParameterDirection.Input);
            cmd.Parameters.Add("numero", OracleDbType.Int32, numero, ParameterDirection.Input);
            cmd.Parameters.Add("margen", OracleDbType.Decimal, pctMargen, ParameterDirection.Input);
            cmd.Parameters.Add("usuario", OracleDbType.Varchar2, usuario, ParameterDirection.Input);
            cmd.Parameters.Add("idcalc", OracleDbType.Int32, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();
            tran.Commit();

            var idCalcOra = (OracleDecimal)cmd.Parameters["idcalc"].Value;
            return (idCalcOra.IsNull ? 0 : idCalcOra.ToInt32(), null);
        }
        catch (OracleException ex)
        {
            tran.Rollback();
            _logger.LogWarning(ex, "[Costos] Error al calcular cotización {Numero}", numero);
            return (0, LimpiarMensajeOracle(ex.Message));
        }
    }

    /// <summary>
    /// Limpia el mensaje devuelto por Oracle antes de mostrarlo al usuario:
    /// - Corrige el mojibake típico cuando el mensaje UTF-8 del RAISE_APPLICATION_ERROR
    ///   llega mal interpretado como Windows-1252 (p.ej. "Ã­tem" en vez de "ítem",
    ///   "â€”" en vez de "—"). El rango 0x80-0x9F de Windows-1252 difiere de ISO-8859-1,
    ///   por eso se mapea explícitamente en vez de usar Encoding.Latin1.
    /// - Se queda solo con la primera línea (el texto del ORA-XXXXX de negocio),
    ///   descartando la traza "ORA-06512: en ..." y el link de documentación.
    /// </summary>
    private static string LimpiarMensajeOracle(string mensaje)
    {
        if (string.IsNullOrWhiteSpace(mensaje))
            return mensaje;

        mensaje = FixMojibake(mensaje);

        var primeraLinea = mensaje.Split('\n', '\r')[0].Trim();

        var match = System.Text.RegularExpressions.Regex.Match(primeraLinea, @"ORA-\d+:\s*(.+)");
        return match.Success ? match.Groups[1].Value.Trim() : primeraLinea;
    }

    // Mapa inverso de los caracteres especiales de Windows-1252 (0x80-0x9F) a su byte original
    // — mismo mapeo usado en PlnReporteService.FixMojibake, reutilizado aquí para reparar los
    // mensajes de error de RAISE_APPLICATION_ERROR que llegan con encoding mal interpretado.
    private static readonly System.Collections.Generic.Dictionary<char, byte> Cp1252Extended = new()
    {
        ['\u20AC']=0x80, ['\u201A']=0x82, ['\u0192']=0x83, ['\u201E']=0x84, ['\u2026']=0x85,
        ['\u2020']=0x86, ['\u2021']=0x87, ['\u02C6']=0x88, ['\u2030']=0x89, ['\u0160']=0x8A,
        ['\u2039']=0x8B, ['\u0152']=0x8C, ['\u017D']=0x8E, ['\u2018']=0x91, ['\u2019']=0x92,
        ['\u201C']=0x93, ['\u201D']=0x94, ['\u2022']=0x95, ['\u2013']=0x96, ['\u2014']=0x97,
        ['\u02DC']=0x98, ['\u2122']=0x99, ['\u0161']=0x9A, ['\u203A']=0x9B, ['\u0153']=0x9C,
        ['\u017E']=0x9E, ['\u0178']=0x9F,
    };

    private static string FixMojibake(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        if (s.IndexOf('Ã') < 0 && s.IndexOf('Â') < 0 && s.IndexOf('â') < 0) return s;

        var bytes = new byte[s.Length];
        for (int i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c <= 0xFF) bytes[i] = (byte)c;
            else if (Cp1252Extended.TryGetValue(c, out var b)) bytes[i] = b;
            else return s; // carácter fuera de rango: no es el mojibake esperado, no tocar
        }

        var reparado = System.Text.Encoding.UTF8.GetString(bytes);
        return reparado.IndexOf('\uFFFD') < 0 && !string.Equals(reparado, s, StringComparison.Ordinal)
            ? reparado
            : s;
    }

    private async Task<CosCalcResultDto?> ObtenerUltimoCalcAsync(OracleConnection db, int numero)
    {
        var calc = await db.QueryFirstOrDefaultAsync<CosCalcResultDto>(
            $@"SELECT * FROM (
                   SELECT id_calc AS IdCalc, nro_version AS NroVersion, fec_calculo AS FecCalculo,
                          usuario_calculo AS UsuarioCalculo, pct_margen AS PctMargen,
                          costo_hilanderia AS CostoHilanderia, costo_tintoreria AS CostoTintoreria,
                          costo_moi AS CostoMoi, costo_cif AS CostoCif,
                          costo_gastos_opfin AS CostoGastosOpfin, costo_gas AS CostoGas,
                          costo_agua AS CostoAgua, costo_enel AS CostoEnel,
                          costo_moi_usado AS CostoMoiUsado, costo_cif_usado AS CostoCifUsado,
                          costo_gof_usado AS CostoGofUsado, costo_gas_usado AS CostoGasUsado,
                          costo_agua_usado AS CostoAguaUsado, costo_enel_usado AS CostoEnelUsado,
                          costo_total AS CostoTotal, precio_final AS PrecioFinal,
                          costo_materia_prima AS CostoMateriaPrima,
                          ind_materia_prima_completa AS IndMateriaPrimaCompleta,
                          costo_total_con_mp AS CostoTotalConMp, precio_final_con_mp AS PrecioFinalConMp,
                          moneda AS Moneda, estado AS Estado, observaciones AS Observaciones,
                          ind_tiene_estimados AS IndTieneEstimados,
                          nivel_proyeccion_peor AS NivelProyeccionPeor,
                          pct_confiabilidad_peor AS PctConfiabilidadPeor,
                          descripcion_nivel_peor AS DescripcionNivelPeor,
                          ind_etapas_completas AS IndEtapasCompletas
                   FROM   {S}COS_COTIZACION_CALC
                   WHERE  tipodoc = :tipodoc AND serie = :serie AND numero = :numero
                   ORDER  BY nro_version DESC
               ) WHERE ROWNUM = 1",
            new { tipodoc = Tipodoc, serie = Serie, numero });

        if (calc == null)
            return null;

        calc.Lineas = (await db.QueryAsync<CosCalcLineaDto>(
            $@"SELECT item AS Item, orden_etapa AS OrdenEtapa, nombre_etapa AS NombreEtapa,
                      cod_maquina AS CodMaquina, componente_fibra AS ComponenteFibra,
                      cantidad_kg AS CantidadKg, pct_merma_aplicado AS PctMermaAplicado,
                      tasa_aplicada AS TasaAplicada, unidad_tasa AS UnidadTasa,
                      monto_parcial AS MontoParcial, ind_estimado AS IndEstimado,
                      nivel_proyeccion AS NivelProyeccion, pct_confiabilidad AS PctConfiabilidad,
                      descripcion_nivel AS DescripcionNivel,
                      ind_es_materia_prima AS IndEsMateriaPrima, ind_mp_sin_costo AS IndMpSinCosto,
                      ind_etapa_sin_config AS IndEtapaSinConfig,
                      observaciones AS Observaciones
               FROM   {S}COS_COTIZACION_CALC_D
               WHERE  id_calc = :idCalc
               ORDER  BY item, orden_etapa",
            new { idCalc = calc.IdCalc })).ToList();

        return calc;
    }
}

