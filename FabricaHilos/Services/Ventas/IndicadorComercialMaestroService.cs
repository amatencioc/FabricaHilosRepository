using Oracle.ManagedDataAccess.Client;
using FabricaHilos.Models.Ventas;

namespace FabricaHilos.Services.Ventas
{
    public class IndicadorComercialMaestroService : OracleServiceBase, IIndicadorComercialMaestroService
    {
        private readonly ILogger<IndicadorComercialMaestroService> _logger;

        public IndicadorComercialMaestroService(
            IConfiguration configuration,
            ILogger<IndicadorComercialMaestroService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(configuration, httpContextAccessor)
        {
            _logger = logger;
        }

        // ── Helpers idénticos a DashboardComercialMaestroService ──────────────
        private static string? GetStr(OracleDataReader r, string col) =>
            r[col] == DBNull.Value ? null : r[col]?.ToString();

        private static decimal GetDec(OracleDataReader r, string col)
        {
            try
            {
                if (r[col] == DBNull.Value) return 0m;
                var od = r.GetOracleDecimal(r.GetOrdinal(col));
                od = Oracle.ManagedDataAccess.Types.OracleDecimal.SetPrecision(od, 28);
                return (decimal)od;
            }
            catch { return 0m; }
        }

        // ── SQL con dimensión MES ─────────────────────────────────────────────
        // Estructura idéntica a DashboardComercialMaestroService.BuildSql() con
        // TO_CHAR(FECHA,'YYYY/MM') añadido a los tres subqueries.
        // - Subquery A : V_DOCUVEN + GRUPO_REL + SOLES_SINANT / DOLARES_SINANT
        // - Subquery B : Artículos de servicio a descontar (IN list)
        // - Subquery C : KG por EQUIVALENCIA (misma exclusión por COD_ART)
        private string BuildSqlMes() => $@"
SELECT A.COD_CLIENTE_K,
       A.COD_ASESOR,
       A.ASESOR,
       A.MES,
       NVL(C.TOTUNID, 0)                          TOTUNID,
       (A.SOLES - NVL(B.SOLES_ANT,  0))           SOLES,
       (A.DOLAR - NVL(B.DOLAR_ANT,  0))           DOLAR
  FROM (SELECT DECODE(CL.GRUPO_REL, NULL, V.COD_CLIENTE, GRP.MIN_CLIENTE) COD_CLIENTE_K,
               CL.VENDEDOR                                                  COD_ASESOR,
               T.DESCRIPCION                                                ASESOR,
               TO_CHAR(V.FECHA, 'YYYY/MM')                                 MES,
               SUM(V.SOLES_SINANT)                                          SOLES,
               SUM(V.DOLARES_SINANT)                                        DOLAR
          FROM {S}V_DOCUVEN V
          LEFT JOIN {S}CLIENTES CL         ON CL.COD_CLIENTE = V.COD_CLIENTE
          LEFT JOIN {S}TABLAS_AUXILIARES T ON T.CODIGO = CL.VENDEDOR
                                         AND T.TIPO   = 29
          LEFT JOIN (SELECT GRUPO, MIN(COD_CLIENTE) AS MIN_CLIENTE
                       FROM {S}CLIENTE_RELACION GROUP BY GRUPO) GRP
                 ON GRP.GRUPO = CL.GRUPO_REL
         WHERE V.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
         GROUP BY DECODE(CL.GRUPO_REL, NULL, V.COD_CLIENTE, GRP.MIN_CLIENTE),
                  CL.VENDEDOR,
                  T.DESCRIPCION,
                  TO_CHAR(V.FECHA, 'YYYY/MM')) A
  LEFT JOIN (SELECT DECODE(CL.GRUPO_REL, NULL, D.COD_CLIENTE, GRP.MIN_CLIENTE) COD_CLIENTE_K,
                    CL.VENDEDOR                     COD_ASESOR,
                    TO_CHAR(D.FECHA, 'YYYY/MM')     MES,
                    SUM(DECODE(D.MONEDA, 'S', I.IMP_VVTA,
                               ROUND(I.IMP_VVTA * D.IMPORT_CAM, 2)))            SOLES_ANT,
                    SUM(DECODE(D.MONEDA, 'D', I.IMP_VVTA,
                               ROUND(I.IMP_VVTA / NULLIF(D.IMPORT_CAM, 0), 2))) DOLAR_ANT
               FROM {S}DOCUVENT D
               JOIN {S}ITEMDOCU I         ON I.TIPODOC = D.TIPODOC
                                         AND I.SERIE   = D.SERIE
                                         AND I.NUMERO  = D.NUMERO
               LEFT JOIN {S}CLIENTES CL  ON CL.COD_CLIENTE = D.COD_CLIENTE
               LEFT JOIN (SELECT GRUPO, MIN(COD_CLIENTE) AS MIN_CLIENTE
                            FROM {S}CLIENTE_RELACION GROUP BY GRUPO) GRP
                      ON GRP.GRUPO = CL.GRUPO_REL
              WHERE D.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
                AND D.ESTADO <> '9'
                AND I.COD_ART IN ('9300049997', '9300049999',
                                  '930004999A', '9300049998')
              GROUP BY DECODE(CL.GRUPO_REL, NULL, D.COD_CLIENTE, GRP.MIN_CLIENTE),
                       CL.VENDEDOR,
                       TO_CHAR(D.FECHA, 'YYYY/MM')) B
    ON  B.COD_CLIENTE_K = A.COD_CLIENTE_K
    AND B.COD_ASESOR    = A.COD_ASESOR
    AND B.MES           = A.MES
  LEFT JOIN (SELECT DECODE(CL.GRUPO_REL, NULL, D.COD_CLIENTE, GRP.MIN_CLIENTE) COD_CLIENTE_K,
                    CL.VENDEDOR                     COD_ASESOR,
                    TO_CHAR(D.FECHA, 'YYYY/MM')     MES,
                    SUM(I.CANTIDAD * E.FACTOR)       TOTUNID
               FROM {S}DOCUVENT D
               JOIN {S}ITEMDOCU I          ON I.TIPODOC = D.TIPODOC
                                          AND I.SERIE   = D.SERIE
                                          AND I.NUMERO  = D.NUMERO
               LEFT JOIN {S}EQUIVALENCIA E ON E.COD_ART = I.COD_ART
                                          AND E.UNIDAD  = 'KG'
               LEFT JOIN {S}CLIENTES CL   ON CL.COD_CLIENTE = D.COD_CLIENTE
               LEFT JOIN (SELECT GRUPO, MIN(COD_CLIENTE) AS MIN_CLIENTE
                            FROM {S}CLIENTE_RELACION GROUP BY GRUPO) GRP
                      ON GRP.GRUPO = CL.GRUPO_REL
              WHERE D.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
                AND D.ESTADO <> '9'
                AND I.COD_ART NOT IN ('9300049997', '9300049999',
                                      '930004999A', '9300049998')
              GROUP BY DECODE(CL.GRUPO_REL, NULL, D.COD_CLIENTE, GRP.MIN_CLIENTE),
                       CL.VENDEDOR,
                       TO_CHAR(D.FECHA, 'YYYY/MM')) C
    ON  C.COD_CLIENTE_K = A.COD_CLIENTE_K
    AND C.COD_ASESOR    = A.COD_ASESOR
    AND C.MES           = A.MES
 ORDER BY A.COD_ASESOR, A.MES";

        // ── Fila interna (un registro por cliente+asesor+mes) ─────────────────
        private sealed class FilaMesRaw
        {
            public string? CodCliente { get; set; }
            public string? CodAsesor  { get; set; }
            public string? Asesor     { get; set; }
            public string? Mes        { get; set; }
            public decimal TotUnid    { get; set; }
            public decimal Soles      { get; set; }
            public decimal Dolar      { get; set; }
        }

        // ── Carga desde Oracle — un registro por (cliente, asesor, mes) ───────
        private async Task<List<FilaMesRaw>> CargarFilasMesAsync(DateTime fechaInicio, DateTime fechaFin)
        {
            var connStr = GetOracleConnectionString();
            var filas   = new List<FilaMesRaw>();
            if (string.IsNullOrEmpty(connStr)) return filas;

            try
            {
                using var conn   = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd    = new OracleCommand(BuildSqlMes(), conn) { BindByName = true };
                cmd.Parameters.Add("P_FECHA1", OracleDbType.Date).Value = fechaInicio.Date;
                cmd.Parameters.Add("P_FECHA2", OracleDbType.Date).Value = fechaFin.Date;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    filas.Add(new FilaMesRaw
                    {
                        CodCliente = GetStr(reader, "COD_CLIENTE_K"),
                        CodAsesor  = GetStr(reader, "COD_ASESOR"),
                        Asesor     = GetStr(reader, "ASESOR"),
                        Mes        = GetStr(reader, "MES"),
                        TotUnid    = GetDec(reader, "TOTUNID"),
                        Soles      = GetDec(reader, "SOLES"),
                        Dolar      = GetDec(reader, "DOLAR"),
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ICM] Error al cargar filas por Asesor/Mes");
            }

            _logger.LogDebug("[ICM] Filas cargadas: {N} | Fechas: {F1:dd/MM/yyyy}-{F2:dd/MM/yyyy}",
                filas.Count, fechaInicio, fechaFin);
            return filas;
        }

        // Selecciona la moneda igual que DashboardComercialMaestroService.Imp()
        private static decimal Imp(FilaMesRaw f, string moneda) =>
            moneda.Equals("S", StringComparison.OrdinalIgnoreCase) ? f.Soles : f.Dolar;

        // ════════════════════════════════════════════════════════════════════════
        // ObtenerTodosAsync — un solo viaje Oracle, tres resultados
        // ════════════════════════════════════════════════════════════════════════
        public async Task<IcmTodosDto> ObtenerTodosAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda)
        {
            var mon   = string.IsNullOrEmpty(moneda) ? "D" : moneda.ToUpperInvariant();
            var filas = await CargarFilasMesAsync(fechaInicio, fechaFin);

            var sinOficina = filas
                .Where(f => !string.Equals(f.Asesor, "OFICINA", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var importe = sinOficina
                .GroupBy(f => new { Asesor = f.Asesor ?? "Sin Asesor", CodAsesor = f.CodAsesor, Mes = f.Mes ?? "" })
                .Select(g => new IcmImporteAsesorMesDto
                {
                    CodAsesor = g.Key.CodAsesor,
                    Asesor    = g.Key.Asesor,
                    Mes       = g.Key.Mes,
                    Importe   = g.Sum(f => Imp(f, mon)),
                })
                .OrderBy(x => x.Asesor).ThenBy(x => x.Mes)
                .ToList();

            var kg = sinOficina
                .GroupBy(f => new { Asesor = f.Asesor ?? "Sin Asesor", Mes = f.Mes ?? "" })
                .Select(g => new IcmKgAsesorMesDto
                {
                    Asesor     = g.Key.Asesor,
                    Mes        = g.Key.Mes,
                    CantidadKg = g.Sum(f => f.TotUnid),
                })
                .OrderBy(x => x.Asesor).ThenBy(x => x.Mes)
                .ToList();

            var clientes = sinOficina
                .GroupBy(f => new { Asesor = f.Asesor ?? "Sin Asesor", Mes = f.Mes ?? "" })
                .Select(g => new IcmClientesAsesorMesDto
                {
                    Asesor      = g.Key.Asesor,
                    Mes         = g.Key.Mes,
                    NroClientes = g.Select(f => f.CodCliente).Distinct().Count(),
                })
                .OrderBy(x => x.Asesor).ThenBy(x => x.Mes)
                .ToList();

            return new IcmTodosDto { Importe = importe, Kg = kg, Clientes = clientes };
        }

        // ════════════════════════════════════════════════════════════════════════
        // Importe neto por Asesor / Mes
        // Carga una sola vez y agrupa en C# — idéntico a cómo lo hace el dashboard
        // ════════════════════════════════════════════════════════════════════════
        public async Task<List<IcmImporteAsesorMesDto>> ObtenerImportePorAsesorMesAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda)
        {
            var mon   = string.IsNullOrEmpty(moneda) ? "D" : moneda.ToUpperInvariant();
            var filas = await CargarFilasMesAsync(fechaInicio, fechaFin);

            return filas
                .Where(f => !string.Equals(f.Asesor, "OFICINA", StringComparison.OrdinalIgnoreCase))
                .GroupBy(f => new
                {
                    Asesor    = f.Asesor    ?? "Sin Asesor",
                    CodAsesor = f.CodAsesor,
                    Mes       = f.Mes       ?? "",
                })
                .Select(g => new IcmImporteAsesorMesDto
                {
                    CodAsesor = g.Key.CodAsesor,
                    Asesor    = g.Key.Asesor,
                    Mes       = g.Key.Mes,
                    Importe   = g.Sum(f => Imp(f, mon)),
                })
                .OrderBy(x => x.Asesor).ThenBy(x => x.Mes)
                .ToList();
        }

        // ════════════════════════════════════════════════════════════════════════
        // KG por Asesor / Mes  (reutiliza la misma carga)
        // ════════════════════════════════════════════════════════════════════════
        public async Task<List<IcmKgAsesorMesDto>> ObtenerKgPorAsesorMesAsync(
            DateTime fechaInicio, DateTime fechaFin)
        {
            var filas = await CargarFilasMesAsync(fechaInicio, fechaFin);

            return filas
                .Where(f => !string.Equals(f.Asesor, "OFICINA", StringComparison.OrdinalIgnoreCase))
                .GroupBy(f => new
                {
                    Asesor = f.Asesor ?? "Sin Asesor",
                    Mes    = f.Mes    ?? "",
                })
                .Select(g => new IcmKgAsesorMesDto
                {
                    Asesor     = g.Key.Asesor,
                    Mes        = g.Key.Mes,
                    CantidadKg = g.Sum(f => f.TotUnid),
                })
                .OrderBy(x => x.Asesor).ThenBy(x => x.Mes)
                .ToList();
        }

        // ════════════════════════════════════════════════════════════════════════
        // Clientes distintos por Asesor / Mes  (derivado de la misma carga)
        // Cada fila de CargarFilasMesAsync ya representa un cliente distinto
        // dentro de su asesor+mes gracias al GRUPO_REL del SQL.
        // ════════════════════════════════════════════════════════════════════════
        public async Task<List<IcmClientesAsesorMesDto>> ObtenerClientesPorAsesorMesAsync(
            DateTime fechaInicio, DateTime fechaFin)
        {
            var filas = await CargarFilasMesAsync(fechaInicio, fechaFin);

            return filas
                .Where(f => !string.Equals(f.Asesor, "OFICINA", StringComparison.OrdinalIgnoreCase))
                .GroupBy(f => new
                {
                    Asesor = f.Asesor ?? "Sin Asesor",
                    Mes    = f.Mes    ?? "",
                })
                .Select(g => new IcmClientesAsesorMesDto
                {
                    Asesor      = g.Key.Asesor,
                    Mes         = g.Key.Mes,
                    NroClientes = g.Select(f => f.CodCliente).Distinct().Count(),
                })
                .OrderBy(x => x.Asesor).ThenBy(x => x.Mes)
                .ToList();
        }
    }
}
