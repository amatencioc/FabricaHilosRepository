using System.Globalization;
using FabricaHilos.Models.Sistemas;
using Oracle.ManagedDataAccess.Client;

namespace FabricaHilos.Services.Sistemas
{
    public class SeguimientoDevService : OracleServiceBase, ISeguimientoDevService
    {
        private readonly ILogger<SeguimientoDevService> _logger;

        public SeguimientoDevService(
            IConfiguration configuration,
            ILogger<SeguimientoDevService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(configuration, httpContextAccessor)
        {
            _logger = logger;
        }

        // ── Helpers ─────────────────────────────────────────────────────────────
        private static string?   GetStr (OracleDataReader r, string c) => r[c] == DBNull.Value ? null : r[c]?.ToString();
        private static DateTime? GetDate(OracleDataReader r, string c) => r[c] == DBNull.Value ? null : Convert.ToDateTime(r[c]);

        // ── SQL (ind_seguimientoDev.sql) ─────────────────────────────────────────
        //  Solo ESTADO='2' (entregados) con F_TERMINO en el rango dado.
        private const string SqlSeguimientoDev = @"
SELECT NUMERO, C_NOMBRE, C.C_COSTO, X.NOMBRE,
       TRUNC(FECHA) FECHA, TRUNC(F_APROBACION) APROBADO,
       REPLACE(REQUERIMIENTO,CHR(10),' ') REQUERIMIENTO,
       REPLACE(SOLUCION,CHR(10),' ')      SOLUCION,
       F_SOLUCION_INI                     F_INICIO,
       NVL(S.F_TEST_INI,S.F_SOLUCION)     F_TERMINO,
       S.ESTADO,
       T.DESCRIPCION                      USER_SISTEMA,
       MOTIVO
  FROM CS_SOPCOMP S, CS_TABLAS T, T_CCOSTO C, CENTRO_DE_COSTOS X
 WHERE TIPODOC = 'S'   
   AND S.ESTADO = '2'
   AND NVL(S.F_TEST_INI,S.F_SOLUCION) BETWEEN :P_fecini AND :P_fecfin
   AND T.TIPO(+)      = '6'
   AND T.CODIGO(+)    = S.USER_SOPORTE
   AND C.C_CODIGO     = S.C_CODIGO
   AND X.CENTRO_COSTO = C.C_COSTO";

        // ── Cargar filas crudas desde Oracle ───────────────────────────────────
        private async Task<List<SdFilaRawDto>> CargarFilasAsync(DateTime fechaInicio, DateTime fechaFin)
        {
            var connStr = GetOracleConnectionString();
            var filas   = new List<SdFilaRawDto>();
            if (string.IsNullOrEmpty(connStr)) return filas;

            try
            {
                await using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();

                await using var cmd = new OracleCommand(SqlSeguimientoDev, conn) { BindByName = true };
                cmd.Parameters.Add("P_fecini", OracleDbType.Date).Value = fechaInicio.Date;
                cmd.Parameters.Add("P_fecfin", OracleDbType.Date).Value = fechaFin.Date;

                await using var reader = await cmd.ExecuteReaderAsync();
                var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                while (await reader.ReadAsync())
                {
                    var numero = GetStr(reader, "NUMERO") ?? "";
                    if (!vistos.Add(numero)) continue;

                    filas.Add(new SdFilaRawDto
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
                        UserSistema     = GetStr(reader, "USER_SISTEMA"),
                        Motivo          = GetStr(reader, "MOTIVO"),
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar datos de SeguimientoDev ({F1:dd/MM/yyyy} - {F2:dd/MM/yyyy})", fechaInicio, fechaFin);
                return filas;
            }

            _logger.LogInformation("[SEGDEV] Filas cargadas: {N} | Fechas: {F1:dd/MM/yyyy}-{F2:dd/MM/yyyy}",
                filas.Count, fechaInicio, fechaFin);

            return filas;
        }

        // ════════════════════════════════════════════════════════════════════════
        //  ObtenerDashboardAsync — punto de entrada principal
        //
        //  Pivot:
        //   - Filas    = RESPONSABLE (T.DESCRIPCION = USER_SISTEMA); NULL → "(sin asignar)".
        //   - Columnas = AÑOS de F_TERMINO (entrega), agrupados.
        //   - Solo items con ESTADO='2' (entregados).
        // ════════════════════════════════════════════════════════════════════════
        public async Task<SdDashboardDto> ObtenerDashboardAsync(DateTime fechaInicio, DateTime fechaFin, string? responsable = null, string? tipoMotivo = null)
        {
            var todasFilas = await CargarFilasAsync(fechaInicio, fechaFin);

            // ── Lista completa de responsables (para el selector del cliente) ──
            const string SinResponsable = "(sin asignar)";
            const string SinArea        = "(en blanco)";
            string RespKey(SdFilaRawDto f) => string.IsNullOrWhiteSpace(f.UserSistema) ? SinResponsable : f.UserSistema!;
            string AreaKey(SdFilaRawDto f) => string.IsNullOrWhiteSpace(f.Area)        ? SinArea        : f.Area!;

            var listaResponsables = todasFilas
                .Select(RespKey)
                .Where(r => r != SinResponsable)
                .Distinct()
                .OrderBy(r => r)
                .ToList();

            // ── Clasificación por motivo ──────────────────────────────────
            static bool EsDesarrollo(SdFilaRawDto f) =>
                f.Motivo == "11" || f.Motivo == "16";

            // ── Filtro por responsable ─────────────────────────────────
            var filas = string.IsNullOrWhiteSpace(responsable)
                ? todasFilas
                : todasFilas.Where(f => string.Equals(f.UserSistema, responsable, StringComparison.OrdinalIgnoreCase)).ToList();

            // ── Filtro por tipo motivo ──────────────────────────────
            if (tipoMotivo == "desarrollo")
                filas = filas.Where(EsDesarrollo).ToList();
            else if (tipoMotivo == "incidencia")
                filas = filas.Where(f => !EsDesarrollo(f)).ToList();

            var dto = new SdDashboardDto
            {
                FechaInicio  = fechaInicio,
                FechaFin     = fechaFin,
                Responsables = listaResponsables,
            };

            if (filas.Count == 0) return dto;

            // ── Columnas de años ───────────────────────────────────────────────
            var anosData = filas
                .Where(f => f.FechaTermino.HasValue)
                .Select(f => f.FechaTermino!.Value.Year);

            var anosRango = Enumerable.Range(
                fechaInicio.Year,
                Math.Max(1, fechaFin.Year - fechaInicio.Year + 1));

            dto.Anos = anosData
                .Union(anosRango)
                .OrderBy(a => a)
                .ToList();

            // ── Pivot por responsable ──────────────────────────────────────────
            var responsables = filas
                .Select(RespKey)
                .Distinct()
                .OrderBy(r => r == SinResponsable)
                .ThenBy(r => r)
                .ToList();

            foreach (var resp in responsables)
            {
                var fila = new SdFilaResponsableDto { Responsable = resp };
                var fl   = filas.Where(f => RespKey(f) == resp).ToList();

                foreach (var ano in dto.Anos)
                {
                    var enAno = fl.Where(f => f.FechaTermino.HasValue && f.FechaTermino!.Value.Year == ano).ToList();
                    fila.Anos.Add(new SdCeldaAnoDto
                    {
                        Ano       = ano,
                        Entregado = enAno.Count,
                    });
                }

                fila.TotalEntregado = fila.Anos.Sum(c => c.Entregado);
                dto.Filas.Add(fila);
            }

            // ── Totales por columna ────────────────────────────────────────────
            dto.TotalesAno = dto.Anos.Select(ano => new SdCeldaAnoDto
            {
                Ano       = ano,
                Entregado = dto.Filas.Sum(f => f.Anos.First(a => a.Ano == ano).Entregado),
            }).ToList();

            dto.GTEntregado = dto.Filas.Sum(f => f.TotalEntregado);

            // ── KPI cards ──────────────────────────────────────────────────────
            dto.TotalEntregados   = filas.Count;
            dto.TotalDesarrollo   = filas.Count(EsDesarrollo);
            dto.TotalIncidencia   = filas.Count(f => !EsDesarrollo(f));
            dto.TotalResponsables = filas.Select(RespKey).Distinct().Count(r => r != SinResponsable);
            dto.TotalAreas        = filas.Select(AreaKey).Distinct().Count(a => a != SinArea);

            // ── Por área (para gráficos) ───────────────────────────────────────
            var allAreas = filas.Select(AreaKey).Distinct().OrderBy(a => a).ToList();

            dto.PorArea = filas
                .GroupBy(AreaKey)
                .Select(g => new SdAreaTotalDto { Area = g.Key, TotalEntregado = g.Count() })
                .OrderByDescending(a => a.TotalEntregado)
                .ToList();

            dto.PorAreaDesarrollo = filas.Where(EsDesarrollo)
                .GroupBy(AreaKey)
                .Select(g => new SdAreaTotalDto { Area = g.Key, TotalEntregado = g.Count() })
                .ToList();

            dto.PorAreaIncidencia = filas.Where(f => !EsDesarrollo(f))
                .GroupBy(AreaKey)
                .Select(g => new SdAreaTotalDto { Area = g.Key, TotalEntregado = g.Count() })
                .ToList();

            // ── Por responsable (para gráficos) ───────────────────────────────
            dto.PorResponsable = dto.Filas
                .Select(f => new SdResponsableTotalDto { Responsable = f.Responsable, TotalEntregado = f.TotalEntregado })
                .OrderByDescending(r => r.TotalEntregado)
                .ToList();

            var respLabels = dto.PorResponsable.Select(r => r.Responsable).ToList();

            dto.PorResponsableDesarrollo = respLabels
                .Select(resp => new SdResponsableTotalDto
                {
                    Responsable    = resp,
                    TotalEntregado = filas.Count(f => RespKey(f) == resp && EsDesarrollo(f)),
                }).ToList();

            dto.PorResponsableIncidencia = respLabels
                .Select(resp => new SdResponsableTotalDto
                {
                    Responsable    = resp,
                    TotalEntregado = filas.Count(f => RespKey(f) == resp && !EsDesarrollo(f)),
                }).ToList();

            // ── Entrega mes a mes ──────────────────────────────────────────────
            var culture = CultureInfo.GetCultureInfo("es-PE");
            int anoAt   = fechaFin.Year;
            int mIni    = fechaInicio.Year == anoAt ? fechaInicio.Month : 1;
            int mFin    = fechaFin.Month;

            dto.AnoAtencion = anoAt;
            var mensual     = new List<SdEntregaMesDto>();
            var mensualDev  = new List<SdEntregaMesDto>();
            var mensualInc  = new List<SdEntregaMesDto>();

            for (int m = mIni; m <= mFin; m++)
            {
                Func<SdFilaRawDto, bool> enMes = f =>
                    f.FechaTermino.HasValue &&
                    f.FechaTermino!.Value.Year  == anoAt &&
                    f.FechaTermino!.Value.Month == m;

                int ent  = filas.Count(enMes);
                int eDev = filas.Count(f => enMes(f) && EsDesarrollo(f));
                int eInc = filas.Count(f => enMes(f) && !EsDesarrollo(f));
                string etiq = culture.DateTimeFormat.GetMonthName(m).ToUpper();

                if (ent > 0)
                    mensual.Add(new SdEntregaMesDto { Mes = m, Etiqueta = etiq, Entregados = ent });
                if (eDev > 0 || eInc > 0)
                {
                    mensualDev.Add(new SdEntregaMesDto { Mes = m, Etiqueta = etiq, Entregados = eDev });
                    mensualInc.Add(new SdEntregaMesDto { Mes = m, Etiqueta = etiq, Entregados = eInc });
                }
            }
            dto.EntregaMes            = mensual;
            dto.EntregaMesDesarrollo  = mensualDev;
            dto.EntregaMesIncidencia  = mensualInc;

            // ── Totales por año split ─────────────────────────────────────────
            dto.TotalesAnoDesarrollo = dto.Anos.Select(ano => new SdCeldaAnoDto
            {
                Ano       = ano,
                Entregado = filas.Count(f => f.FechaTermino.HasValue && f.FechaTermino!.Value.Year == ano && EsDesarrollo(f)),
            }).ToList();

            dto.TotalesAnoIncidencia = dto.Anos.Select(ano => new SdCeldaAnoDto
            {
                Ano       = ano,
                Entregado = filas.Count(f => f.FechaTermino.HasValue && f.FechaTermino!.Value.Year == ano && !EsDesarrollo(f)),
            }).ToList();

            return dto;
        }
    }
}
