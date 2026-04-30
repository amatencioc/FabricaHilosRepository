using System.Globalization;
using FabricaHilos.Models.Sistemas;
using Oracle.ManagedDataAccess.Client;

namespace FabricaHilos.Services.Sistemas
{
    public class DesarrolloService : OracleServiceBase, IDesarrolloService
    {
        private readonly ILogger<DesarrolloService> _logger;

        public DesarrolloService(
            IConfiguration configuration,
            ILogger<DesarrolloService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(configuration, httpContextAccessor)
        {
            _logger = logger;
        }

        // ── Helpers ─────────────────────────────────────────────────────────────
        private static string?   GetStr (OracleDataReader r, string c) => r[c] == DBNull.Value ? null : r[c]?.ToString();
        private static DateTime? GetDate(OracleDataReader r, string c) => r[c] == DBNull.Value ? null : Convert.ToDateTime(r[c]);

        // ── SQL  (ind_desarrollo.sql)  ─────────────────────────────────────────
        //   - Pendientes (ESTADO='1' y sin F_TERMINO) → SIN filtro de fecha
        //   - Entregados (ESTADO='2') → F_TERMINO BETWEEN :P_fecini AND :P_fecfin
        private const string SqlDesarrollo = @"
SELECT NUMERO, C_NOMBRE, C.C_COSTO, X.NOMBRE,
       TRUNC(FECHA) FECHA, TRUNC(F_APROBACION) APROBADO,
       REPLACE(REQUERIMIENTO,CHR(10),' ') REQUERIMIENTO,
       REPLACE(SOLUCION,CHR(10),' ')      SOLUCION,
       F_SOLUCION_INI                     F_INICIO,
       NVL(S.F_TEST_INI,S.F_SOLUCION)     F_TERMINO,
       S.ESTADO
  FROM CS_SOPCOMP S, CS_TABLAS T, T_CCOSTO C, CENTRO_DE_COSTOS X
 WHERE TIPODOC = 'S'
   AND MOTIVO  IN ('11','16')
   AND ((S.ESTADO = '1' AND NVL(S.F_TEST_INI,S.F_SOLUCION) IS NULL)
     OR (S.ESTADO IN ('2') AND NVL(S.F_TEST_INI,S.F_SOLUCION) BETWEEN :P_fecini AND :P_fecfin))
   AND T.TIPO(+)      = '6'
   AND T.CODIGO(+)    = S.USER_SOPORTE
   AND C.C_CODIGO     = S.C_CODIGO
   AND X.CENTRO_COSTO = C.C_COSTO";

        // ── Cargar filas crudas desde Oracle ───────────────────────────────────
        private async Task<List<DevFilaRawDto>> CargarFilasAsync(DateTime fechaInicio, DateTime fechaFin)
        {
            var connStr = GetOracleConnectionString();
            var filas   = new List<DevFilaRawDto>();
            if (string.IsNullOrEmpty(connStr)) return filas;

            try
            {
                await using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();

                await using var cmd = new OracleCommand(SqlDesarrollo, conn) { BindByName = true };
                cmd.Parameters.Add("P_fecini", OracleDbType.Date).Value = fechaInicio.Date;
                cmd.Parameters.Add("P_fecfin", OracleDbType.Date).Value = fechaFin.Date;

                await using var reader = await cmd.ExecuteReaderAsync();
                var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                while (await reader.ReadAsync())
                {
                    var numero = GetStr(reader, "NUMERO") ?? "";
                    // Los joins externos pueden duplicar la misma boleta; nos quedamos con la primera fila.
                    if (!vistos.Add(numero)) continue;

                    filas.Add(new DevFilaRawDto
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
                _logger.LogError(ex, "Error al cargar datos de Desarrollo ({F1:dd/MM/yyyy} - {F2:dd/MM/yyyy})", fechaInicio, fechaFin);
                return filas;
            }

            _logger.LogInformation("[DEV] Filas cargadas: {N} | Fechas: {F1:dd/MM/yyyy}-{F2:dd/MM/yyyy}",
                filas.Count, fechaInicio, fechaFin);

            return filas;
        }

        // ════════════════════════════════════════════════════════════════════════
        //  ObtenerDashboardAsync — punto de entrada principal
        //
        //  Réplica del pivot Excel:
        //   - Filas    = ÁREA (NOMBRE de CENTRO_DE_COSTOS); NULL → "(en blanco)".
        //   - Columnas = AÑOS de FECHA (creación), agrupados.
        //   - PENDIENTE año X = todos los items con FECHA.Year == X (cualquier estado).
        //   - ENTREGADO año X = items con FECHA.Year == X y ESTADO == '2'.
        //   - Total PEND  = total de filas devueltas por el query (incluye abiertos + entregados).
        //   - Total ENTR  = items con ESTADO == '2'.
        // ════════════════════════════════════════════════════════════════════════
        public async Task<DevDashboardDto> ObtenerDashboardAsync(DateTime fechaInicio, DateTime fechaFin)
        {
            var filas = await CargarFilasAsync(fechaInicio, fechaFin);

            var dto = new DevDashboardDto
            {
                FechaInicio = fechaInicio,
                FechaFin    = fechaFin,
            };

            if (filas.Count == 0) return dto;

            // ── Columnas de años (los que aparecen en FECHA + el rango consultado) ──
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
            string AreaKey(DevFilaRawDto f) => string.IsNullOrWhiteSpace(f.Area) ? SinArea : f.Area!;

            var areas = filas
                .Select(AreaKey)
                .Distinct()
                .OrderBy(a => a == SinArea)   // (en blanco) al final
                .ThenBy(a => a)
                .ToList();

            foreach (var area in areas)
            {
                var fila = new DevFilaAreaDto { Area = area };
                var fl   = filas.Where(f => AreaKey(f) == area).ToList();

                foreach (var ano in dto.Anos)
                {
                    var enAno = fl.Where(f => f.Fecha.HasValue && f.Fecha!.Value.Year == ano).ToList();
                    fila.Anos.Add(new DevCeldaAnoDto
                    {
                        Ano       = ano,
                        Pendiente = enAno.Count,                              // = recibidos en el año
                        Entregado = enAno.Count(f => f.EsEntregado),
                    });
                }

                fila.TotalPendiente = fila.Anos.Sum(c => c.Pendiente);
                fila.TotalEntregado = fila.Anos.Sum(c => c.Entregado);
                dto.Filas.Add(fila);
            }

            // ── Totales por columna ────────────────────────────────────────────
            dto.TotalesAno = dto.Anos.Select(ano => new DevCeldaAnoDto
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
            dto.TotalPendientes = filas.Count(f => f.EsPendiente);  // abiertos sin F_TERMINO

            // ── Datasets para gráficos ─────────────────────────────────────────
            dto.PorArea = dto.Filas
                .Select(f => new DevAreaTotalDto
                {
                    Area           = f.Area,
                    TotalPendiente = f.TotalPendiente,
                    TotalEntregado = f.TotalEntregado,
                })
                .OrderByDescending(a => a.TotalPendiente)
                .ToList();

            dto.PorAno = dto.TotalesAno
                .Select(t => new DevAnoTotalDto
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
            var mensual = new List<DevAtencionMesDto>();

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

                mensual.Add(new DevAtencionMesDto
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
