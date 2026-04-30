using System.Globalization;
using FabricaHilos.Models.Sistemas;
using Oracle.ManagedDataAccess.Client;

namespace FabricaHilos.Services.Sistemas
{
    public class IncidenciaService : OracleServiceBase, IIncidenciaService
    {
        private readonly ILogger<IncidenciaService> _logger;

        public IncidenciaService(
            IConfiguration configuration,
            ILogger<IncidenciaService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(configuration, httpContextAccessor)
        {
            _logger = logger;
        }

        // ── Helpers ─────────────────────────────────────────────────────────────
        private static string?   GetStr (OracleDataReader r, string c) => r[c] == DBNull.Value ? null : r[c]?.ToString();
        private static DateTime? GetDate(OracleDataReader r, string c) => r[c] == DBNull.Value ? null : Convert.ToDateTime(r[c]);

        // ── SQL 1 (ind_incidencias.sql — detalle) ──────────────────────────────
        //   - Pendientes (ESTADO='1' y sin F_TERMINO)  → SIN filtro de fecha
        //   - Resueltos  (ESTADO IN ('2','9'))          → F_TERMINO BETWEEN :P_fecini AND :P_fecfin
        private const string SqlIncidencias = @"
SELECT NUMERO, C_NOMBRE, C.C_COSTO, X.NOMBRE,
       TRUNC(FECHA) FECHA, TRUNC(F_APROBACION) APROBADO,
       REPLACE(REQUERIMIENTO,CHR(10),' ') REQUERIMIENTO,
       REPLACE(SOLUCION,CHR(10),' ')      SOLUCION,
       F_SOLUCION_INI                     F_INICIO,
       NVL(S.F_TEST_INI,S.F_SOLUCION)    F_TERMINO,
       S.ESTADO
  FROM CS_SOPCOMP S, CS_TABLAS T, T_CCOSTO C, CENTRO_DE_COSTOS X
 WHERE TIPODOC = 'S'
   AND MOTIVO NOT IN ('11','16')
   AND ((S.ESTADO = '1' AND NVL(S.F_TEST_INI,S.F_SOLUCION) IS NULL)
     OR (S.ESTADO IN ('2','9') AND NVL(S.F_TEST_INI,S.F_SOLUCION) BETWEEN :P_fecini AND :P_fecfin))
   AND T.TIPO(+)      = '6'
   AND T.CODIGO(+)    = S.USER_SOPORTE
   AND C.C_CODIGO     = S.C_CODIGO
   AND X.CENTRO_COSTO = C.C_COSTO";

        // ── SQL 2 (ind_incidencias.sql — promedio minutos por mes) ─────────────
        // Filtra filas donde ambas fechas de cálculo son NOT NULL para evitar AVG = NULL
        private const string SqlMinutosMes = @"
SELECT TO_CHAR(S.FECHA, 'MM') MES,
       AVG(ROUND(((NVL(S.F_TEST_INI,S.F_SOLUCION) - S.F_SOLUCION_INI) * 24 * 60),2)) MINUTOS
  FROM CS_SOPCOMP S
 WHERE TO_CHAR(S.FECHA, 'YYYY') = :P_ANO
   AND S.ESTADO <> '9'
   AND S.MOTIVO NOT IN ('11', '16')
   AND S.F_SOLUCION_INI                IS NOT NULL
   AND NVL(S.F_TEST_INI, S.F_SOLUCION) IS NOT NULL
 GROUP BY TO_CHAR(S.FECHA, 'MM')
 ORDER BY TO_CHAR(S.FECHA, 'MM')";

        // ── Cargar filas crudas desde Oracle (query 1) ─────────────────────────
        private async Task<List<IncFilaRawDto>> CargarFilasAsync(DateTime fechaInicio, DateTime fechaFin)
        {
            var connStr = GetOracleConnectionString();
            var filas   = new List<IncFilaRawDto>();
            if (string.IsNullOrEmpty(connStr)) return filas;

            try
            {
                await using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();

                await using var cmd = new OracleCommand(SqlIncidencias, conn) { BindByName = true };
                cmd.Parameters.Add("P_fecini", OracleDbType.Date).Value = fechaInicio.Date;
                cmd.Parameters.Add("P_fecfin", OracleDbType.Date).Value = fechaFin.Date;

                await using var reader = await cmd.ExecuteReaderAsync();
                var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                while (await reader.ReadAsync())
                {
                    var numero = GetStr(reader, "NUMERO") ?? "";
                    if (!vistos.Add(numero)) continue;

                    filas.Add(new IncFilaRawDto
                    {
                        Numero          = numero,
                        ClienteNombre   = GetStr(reader, "C_NOMBRE"),
                        CCosto          = GetStr(reader, "C_COSTO"),
                        Area            = GetStr(reader, "NOMBRE"),
                        Fecha           = GetDate(reader, "FECHA"),
                        FechaAprobacion = GetDate(reader, "APROBADO"),
                        Requerimiento   = GetStr(reader, "REQUERIMIENTO"),
                        Solucion        = GetStr(reader, "SOLUCION"),
                        FechaInicio     = GetDate(reader, "F_INICIO"),
                        FechaTermino    = GetDate(reader, "F_TERMINO"),
                        Estado          = GetStr(reader, "ESTADO"),
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar datos de Incidencias ({F1:dd/MM/yyyy} - {F2:dd/MM/yyyy})", fechaInicio, fechaFin);
                return filas;
            }

            _logger.LogInformation("[INC] Filas cargadas: {N} | Fechas: {F1:dd/MM/yyyy}-{F2:dd/MM/yyyy}",
                filas.Count, fechaInicio, fechaFin);

            return filas;
        }

        // ── Cargar promedio de minutos por mes (query 2) ───────────────────────
        private async Task<List<IncMinutosMesDto>> CargarMinutosMesAsync(int ano)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<IncMinutosMesDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            var culture = CultureInfo.GetCultureInfo("es-PE");

            try
            {
                await using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();

                await using var cmd = new OracleCommand(SqlMinutosMes, conn) { BindByName = true };
                cmd.Parameters.Add("P_ANO", OracleDbType.Varchar2).Value = ano.ToString();

                await using var reader = await cmd.ExecuteReaderAsync();
                int ordMes = reader.GetOrdinal("MES");
                int ordMin = reader.GetOrdinal("MINUTOS");
                while (await reader.ReadAsync())
                {
                    if (reader.IsDBNull(ordMes)) continue;
                    if (!int.TryParse(reader.GetString(ordMes), out int mes)) continue;
                    if (reader.IsDBNull(ordMin)) continue;

                    // AVG en Oracle 10g devuelve NUMBER de alta precisión → usar OracleDecimal
                    var oraVal  = reader.GetOracleDecimal(ordMin);
                    var oraRnd  = Oracle.ManagedDataAccess.Types.OracleDecimal.SetPrecision(oraVal, 10);
                    double minutos = Math.Round((double)oraRnd, 2);
                    if (minutos <= 0) continue;
                    result.Add(new IncMinutosMesDto
                    {
                        Mes             = mes,
                        Etiqueta        = culture.DateTimeFormat.GetMonthName(mes).ToUpper(),
                        PromedioMinutos = minutos,
                    });
                }
                _logger.LogInformation("[INC] Minutos por mes: {N} filas | Año: {ANO}", result.Count, ano);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar minutos promedio de Incidencias (año {ANO})", ano);
            }

            return result;
        }

        // ════════════════════════════════════════════════════════════════════════
        //  ObtenerDashboardAsync — punto de entrada principal
        // ════════════════════════════════════════════════════════════════════════
        public async Task<IncDashboardDto> ObtenerDashboardAsync(DateTime fechaInicio, DateTime fechaFin)
        {
            var filas = await CargarFilasAsync(fechaInicio, fechaFin);

            var dto = new IncDashboardDto
            {
                FechaInicio = fechaInicio,
                FechaFin    = fechaFin,
            };

            if (filas.Count == 0) return dto;

            // ── Columnas de años ───────────────────────────────────────────────
            var anosData = filas
                .Where(f => f.Fecha.HasValue)
                .Select(f => f.Fecha!.Value.Year);

            var anosRango = Enumerable.Range(
                fechaInicio.Year,
                Math.Max(1, fechaFin.Year - fechaInicio.Year + 1));

            dto.Anos = anosData
                .Union(anosRango)
                .OrderBy(a => a)
                .ToList();

            // ── Filas por área ─────────────────────────────────────────────────
            const string SinArea = "(en blanco)";
            string AreaKey(IncFilaRawDto f) => string.IsNullOrWhiteSpace(f.Area) ? SinArea : f.Area!;

            var areas = filas
                .Select(AreaKey)
                .Distinct()
                .OrderBy(a => a == SinArea)
                .ThenBy(a => a)
                .ToList();

            foreach (var area in areas)
            {
                var fila = new IncFilaAreaDto { Area = area };
                var fl   = filas.Where(f => AreaKey(f) == area).ToList();

                foreach (var ano in dto.Anos)
                {
                    var enAno = fl.Where(f => f.Fecha.HasValue && f.Fecha!.Value.Year == ano).ToList();
                    fila.Anos.Add(new IncCeldaAnoDto
                    {
                        Ano       = ano,
                        Pendiente = enAno.Count,
                        Entregado = enAno.Count(f => f.EsEntregado),
                    });
                }

                fila.TotalPendiente = fila.Anos.Sum(c => c.Pendiente);
                fila.TotalEntregado = fila.Anos.Sum(c => c.Entregado);
                dto.Filas.Add(fila);
            }

            // ── Totales por columna ────────────────────────────────────────────
            dto.TotalesAno = dto.Anos.Select(ano => new IncCeldaAnoDto
            {
                Ano       = ano,
                Pendiente = dto.Filas.Sum(f => f.Anos.First(a => a.Ano == ano).Pendiente),
                Entregado = dto.Filas.Sum(f => f.Anos.First(a => a.Ano == ano).Entregado),
            }).ToList();

            dto.GTPendiente = dto.Filas.Sum(f => f.TotalPendiente);
            dto.GTEntregado = dto.Filas.Sum(f => f.TotalEntregado);

            // ── KPI cards ──────────────────────────────────────────────────────
            dto.TotalRecibidos  = filas.Count;
            dto.TotalEntregados = filas.Count(f => f.EsEntregado);
            dto.TotalPendientes = filas.Count(f => f.EsPendiente);

            // ── Datasets para gráficos ─────────────────────────────────────────
            dto.PorArea = dto.Filas
                .Select(f => new IncAreaTotalDto
                {
                    Area           = f.Area,
                    TotalPendiente = f.TotalPendiente,
                    TotalEntregado = f.TotalEntregado,
                })
                .OrderByDescending(a => a.TotalPendiente)
                .ToList();

            dto.PorAno = dto.TotalesAno
                .Select(t => new IncAnoTotalDto
                {
                    Ano       = t.Ano,
                    Pendiente = t.Pendiente,
                    Entregado = t.Entregado,
                })
                .ToList();

            // ── Atención mes a mes (año más reciente del rango) ────────────────
            var culture = CultureInfo.GetCultureInfo("es-PE");
            int anoAt   = fechaFin.Year;
            int mIni    = fechaInicio.Year == anoAt ? fechaInicio.Month : 1;
            int mFin    = fechaFin.Month;

            dto.AnoAtencion = anoAt;

            // Query 2 — promedio de minutos independiente (puede tener meses distintos al query 1)
            dto.MinutosMes = await CargarMinutosMesAsync(anoAt);

            var mensual = new List<IncAtencionMesDto>();

            for (int m = mIni; m <= mFin; m++)
            {
                var delMes = filas.Where(f =>
                    f.Fecha.HasValue &&
                    f.Fecha!.Value.Year  == anoAt &&
                    f.Fecha!.Value.Month == m).ToList();

                int rec   = delMes.Count;
                int mismo = delMes.Count(f => f.EsEntregado &&
                                              f.FechaTermino.HasValue &&
                                              f.FechaTermino!.Value.Year  == anoAt &&
                                              f.FechaTermino!.Value.Month == m);
                int sig   = delMes.Count(f => f.EsEntregado &&
                                              f.FechaTermino.HasValue &&
                                              (f.FechaTermino!.Value.Year > anoAt ||
                                              (f.FechaTermino.Value.Year == anoAt &&
                                               f.FechaTermino.Value.Month > m)));
                int pend  = delMes.Count(f => f.EsPendiente);

                if (rec == 0 && mismo == 0 && sig == 0 && pend == 0) continue;

                mensual.Add(new IncAtencionMesDto
                {
                    Mes         = m,
                    Etiqueta    = culture.DateTimeFormat.GetMonthName(m).ToUpper(),
                    Recibidos   = rec,
                    AtMismoMes  = mismo,
                    AtSigMes    = sig,
                    Pendientes  = pend,
                    PctMismoMes = rec > 0 ? Math.Round((double)mismo / rec * 100, 0) : 0,
                });
            }
            dto.AtencionMes = mensual;

            int totRec = mensual.Sum(r => r.Recibidos);
            int totAt  = mensual.Sum(r => r.AtMismoMes + r.AtSigMes);
            dto.PctAtencionAno = totRec > 0 ? Math.Round((double)totAt / totRec * 100, 0) : 0;

            return dto;
        }
    }
}
