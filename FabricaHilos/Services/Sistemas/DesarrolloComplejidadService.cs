using System.Globalization;
using FabricaHilos.Models.Sistemas;
using Oracle.ManagedDataAccess.Client;

namespace FabricaHilos.Services.Sistemas
{
    /// <summary>
    /// Agrupa los requerimientos de desarrollo por nivel de Complejidad
    /// (campo PRIORIDAD de CS_SOPCOMP, decodificado via CS_TABLAS TIPO='4':
    ///   01 = BAJA · 02 = MEDIA · 03 = ALTA).
    /// Los gráficos usan COMPLEJIDAD como dimensión de peso.
    /// </summary>
    public class DesarrolloComplejidadService : OracleServiceBase, IDesarrolloComplejidadService
    {
        private readonly ILogger<DesarrolloComplejidadService> _logger;

        public DesarrolloComplejidadService(
            IConfiguration configuration,
            ILogger<DesarrolloComplejidadService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(configuration, httpContextAccessor)
        {
            _logger = logger;
        }

        // ── Helpers ─────────────────────────────────────────────────────────────
        private static string?   GetStr (OracleDataReader r, string c) => r[c] == DBNull.Value ? null : r[c]?.ToString();
        private static DateTime? GetDate(OracleDataReader r, string c) => r[c] == DBNull.Value ? null : Convert.ToDateTime(r[c]);

        // ── SQL  (ind_desarrollo_complejidad.sql) ──────────────────────────────
        // Verificado en producción 30/06/2026:
        //   - CS_SOPCOMP.COMPLEJIDAD: VARCHAR2(2), códigos '01'/'02'/'03'.
        //     231 registros ya tienen COMPLEJIDAD cargado (01=41, 02=135, 03=55).
        //   - CS_SOPCOMP.PRIORIDAD: campo histórico con mismos códigos '01'/'02'/'03'.
        //   - Decodificación: NVL(COMPLEJIDAD, PRIORIDAD) → CS_TABLAS TIPO='4'
        //     → BAJA / MEDIA / ALTA (mantiene labels del frontend sin cambios).
        //   - Pendientes (ESTADO='1' y sin F_TERMINO) → SIN filtro de fecha.
        //   - Entregados (ESTADO='2') → F_TERMINO BETWEEN :P_fecini AND :P_fecfin.
        private const string SqlComplejidad = @"
SELECT S.NUMERO,
       S.C_NOMBRE,
       C.C_COSTO,
       X.NOMBRE                                          AS NOMBRE,
       TRUNC(S.FECHA)                                    AS FECHA,
       TRUNC(S.F_APROBACION)                             AS APROBADO,
       REPLACE(S.REQUERIMIENTO, CHR(10), ' ')            AS REQUERIMIENTO,
       REPLACE(S.SOLUCION,      CHR(10), ' ')            AS SOLUCION,
       S.F_SOLUCION_INI                                  AS F_INICIO,
       NVL(S.F_TEST_INI, S.F_SOLUCION)                  AS F_TERMINO,
       S.ESTADO,
       -- FIX: decodificar COMPLEJIDAD (VARCHAR2 '01'/'02'/'03') con CASE;
       -- si no está cargado, caer en PRIORIDAD decodificada via CS_TABLAS TIPO='4'.
       NVL(
           CASE S.COMPLEJIDAD
               WHEN '03' THEN 'ALTA'
               WHEN '02' THEN 'MEDIA'
               WHEN '01' THEN 'BAJA'
               ELSE NULL
           END,
           NVL(TP.DESCRIPCION, '(Sin clasificar)')
       )                                                 AS COMPLEJIDAD,
       NVL(S.COMPLEJIDAD, NVL(S.PRIORIDAD, '00'))        AS COD_COMPLEJIDAD
  FROM CS_SOPCOMP S
  LEFT JOIN CS_TABLAS T  ON T.TIPO  = '6' AND T.CODIGO = S.USER_SOPORTE
  LEFT JOIN CS_TABLAS TP ON TP.TIPO = '4' AND TP.CODIGO = S.PRIORIDAD
  JOIN T_CCOSTO         C ON C.C_CODIGO     = S.C_CODIGO
  JOIN CENTRO_DE_COSTOS X ON X.CENTRO_COSTO = C.C_COSTO
 WHERE S.TIPODOC = 'S'
   AND S.MOTIVO  IN ('11','16')
   AND (
         (S.ESTADO = '1'  AND NVL(S.F_TEST_INI, S.F_SOLUCION) IS NULL)
      OR (S.ESTADO IN ('2') AND NVL(S.F_TEST_INI, S.F_SOLUCION) BETWEEN :P_fecini AND :P_fecfin)
       )";

        // Orden fijo para mostrar en gráficos (ALTA primero = mayor peso)
        private static readonly string[] OrdenComplejidad = ["ALTA", "MEDIA", "BAJA", "(Sin clasificar)"];

        private int PesoComplejidad(string c) => c switch
        {
            "ALTA"             => 3,
            "MEDIA"            => 2,
            "BAJA"             => 1,
            _                  => 0
        };

        // ── Cargar filas crudas desde Oracle ───────────────────────────────────
        private async Task<List<DevCompFilaRawDto>> CargarFilasAsync(DateTime fechaInicio, DateTime fechaFin)
        {
            var connStr = GetOracleConnectionString();
            var filas   = new List<DevCompFilaRawDto>();
            if (string.IsNullOrEmpty(connStr)) return filas;

            try
            {
                await using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();

                await using var cmd = new OracleCommand(SqlComplejidad, conn) { BindByName = true };
                cmd.Parameters.Add("P_fecini", OracleDbType.Date).Value = fechaInicio.Date;
                cmd.Parameters.Add("P_fecfin", OracleDbType.Date).Value = fechaFin.Date;

                await using var reader = await cmd.ExecuteReaderAsync();
                var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                while (await reader.ReadAsync())
                {
                    var numero = GetStr(reader, "NUMERO") ?? "";
                    if (!vistos.Add(numero)) continue;

                    filas.Add(new DevCompFilaRawDto
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
                        Complejidad     = GetStr(reader, "COMPLEJIDAD"),
                        CodComplejidad  = GetStr(reader, "COD_COMPLEJIDAD"),
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error al cargar datos de DesarrolloComplejidad ({F1:dd/MM/yyyy} - {F2:dd/MM/yyyy})",
                    fechaInicio, fechaFin);
                return filas;
            }

            _logger.LogInformation(
                "[DEV-COMPL] Filas cargadas: {N} | Fechas: {F1:dd/MM/yyyy}-{F2:dd/MM/yyyy}",
                filas.Count, fechaInicio, fechaFin);

            return filas;
        }

        // ════════════════════════════════════════════════════════════════════════
        //  ObtenerDashboardAsync
        // ════════════════════════════════════════════════════════════════════════
        public async Task<DevCompDashboardDto> ObtenerDashboardAsync(DateTime fechaInicio, DateTime fechaFin)
        {
            var filas = await CargarFilasAsync(fechaInicio, fechaFin);

            var dto = new DevCompDashboardDto
            {
                FechaInicio = fechaInicio,
                FechaFin    = fechaFin,
            };

            if (filas.Count == 0) return dto;

            // ── Columnas de años ───────────────────────────────────────────────
            var anosData  = filas.Where(f => f.Fecha.HasValue).Select(f => f.Fecha!.Value.Year);
            var anosRango = Enumerable.Range(fechaInicio.Year,
                Math.Max(1, fechaFin.Year - fechaInicio.Year + 1));

            dto.Anos = anosData.Union(anosRango).OrderBy(a => a).ToList();

            // ── Lista de complejidades presentes, ordenadas por peso ────────────
            string CompKey(DevCompFilaRawDto f) =>
                string.IsNullOrWhiteSpace(f.Complejidad) ? "(Sin clasificar)" : f.Complejidad!;

            dto.Complejidades = filas
                .Select(CompKey)
                .Distinct()
                .OrderByDescending(PesoComplejidad)
                .ThenBy(c => c)
                .ToList();

            // ── Filas del pivot: COMPLEJIDAD × AÑO ────────────────────────────
            foreach (var comp in dto.Complejidades)
            {
                var fila = new DevCompFilaComplejidadDto { Complejidad = comp };
                var fl   = filas.Where(f => CompKey(f) == comp).ToList();

                foreach (var ano in dto.Anos)
                {
                    var enAno = fl.Where(f => f.Fecha.HasValue && f.Fecha!.Value.Year == ano).ToList();
                    fila.Anos.Add(new DevCompCeldaAnoDto
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
            dto.TotalesAno = dto.Anos.Select(ano => new DevCompCeldaAnoDto
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
            dto.TotalComplejos  = filas.Count(f => f.Complejidad == "ALTA");

            // ── Dataset: totales por complejidad ───────────────────────────────
            dto.PorComplejidad = dto.Filas
                .Select(f => new DevCompTotalDto
                {
                    Complejidad    = f.Complejidad,
                    TotalPendiente = f.TotalPendiente,
                    TotalEntregado = f.TotalEntregado,
                })
                .ToList();

            // ── Dataset: totales por año ───────────────────────────────────────
            dto.PorAno = dto.TotalesAno
                .Select(t => new DevCompAnoTotalDto
                {
                    Ano       = t.Ano,
                    Pendiente = t.Pendiente,
                    Entregado = t.Entregado,
                })
                .ToList();

            // ── Detalle mensual × complejidad (año más reciente del rango) ─────
            var culture = CultureInfo.GetCultureInfo("es-PE");
            int anoAt   = fechaFin.Year;
            int mIni    = fechaInicio.Year == anoAt ? fechaInicio.Month : 1;
            int mFin    = fechaFin.Month;

            dto.AnoAtencion = anoAt;
            var mensual     = new List<DevCompAtencionMesDto>();
            var mesDetalle  = new List<DevCompMesDetalleDto>();

            for (int m = mIni; m <= mFin; m++)
            {
                var delMes = filas.Where(f =>
                    f.Fecha.HasValue &&
                    f.Fecha!.Value.Year  == anoAt &&
                    f.Fecha!.Value.Month == m).ToList();

                if (delMes.Count == 0) continue;

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

                // Peso acumulado: cada ítem pesa según su complejidad (ALTA=3, MEDIA=2, BAJA=1)
                double peso = delMes.Sum(f => PesoComplejidad(CompKey(f)));

                mensual.Add(new DevCompAtencionMesDto
                {
                    Mes         = m,
                    Etiqueta    = culture.DateTimeFormat.GetMonthName(m).ToUpper(),
                    Recibidos   = rec,
                    AtMismoMes  = mismo,
                    AtSigMes    = sig,
                    Pendientes  = pend,
                    PctMismoMes = rec > 0 ? Math.Round((double)mismo / rec * 100, 0) : 0,
                    PesoTotal   = peso,
                });

                // Desagregado por complejidad para barras apiladas
                var detalle = new DevCompMesDetalleDto
                {
                    Mes      = m,
                    Etiqueta = culture.DateTimeFormat.GetMonthName(m).ToUpper(),
                };
                foreach (var comp in dto.Complejidades)
                    detalle.PorComplejidad[comp] = delMes.Count(f => CompKey(f) == comp);
                mesDetalle.Add(detalle);
            }

            dto.AtencionMes = mensual;
            dto.MesDetalle  = mesDetalle;

            int totRec = mensual.Sum(r => r.Recibidos);
            int totAt  = mensual.Sum(r => r.AtMismoMes + r.AtSigMes);
            dto.PctAtencionAno = totRec > 0 ? Math.Round((double)totAt / totRec * 100, 0) : 0;

            return dto;
        }
    }
}
