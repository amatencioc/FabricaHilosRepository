using Oracle.ManagedDataAccess.Client;
using FabricaHilos.Models.Ventas;

namespace FabricaHilos.Services.Ventas
{
    public class DashboardComercialMaestroService : OracleServiceBase, IDashboardComercialMaestroService
    {
        private readonly ILogger<DashboardComercialMaestroService> _logger;

        public DashboardComercialMaestroService(
            IConfiguration configuration,
            ILogger<DashboardComercialMaestroService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(configuration, httpContextAccessor)
        {
            _logger = logger;
        }

        // ── Helpers de lectura ──────────────────────────────────────────────────
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

        private static int GetInt(OracleDataReader r, string col) =>
            r[col] == DBNull.Value ? 0 : Convert.ToInt32(r[col]);

        // ── SQL principal — query agrupado por cliente/asesor/moneda ────────────
        private string BuildSql() => $@"
SELECT DECODE(C.GRUPO_REL, NULL, A.COD_CLIENTE, GRP.MIN_CLIENTE) AS COD_CLIENTE,
       CLL.RUC,
       CLL.NOMBRE,
       C.GIRO,
       T2.ABREVIADA DESC_GIRO,
       C.VENDEDOR COD_ASESOR,
       T.DESCRIPCION ASESOR,
       COUNT(A.NUMERO) NRODOC,
       SUM(I.CANTIDAD * E.FACTOR) TOTUNID,
       A.MONEDA,
       SUM(DECODE(A.MONEDA,
              'S',
              (I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)),
              ((I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)) * A.IMPORT_CAM))) SOLES,
       SUM(DECODE(A.MONEDA,
              'D',
              (I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)),
              ((I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)) / NULLIF(A.IMPORT_CAM, 0)))) DOLAR,
       SUM(DECODE(A.MONEDA,
              'S',
              (((I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)) * I.P_IGV)),
              (((I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)) * I.P_IGV)) * A.IMPORT_CAM)) IGV_SOLES,
       SUM(DECODE(A.MONEDA,
              'D',
              (((I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)) * I.P_IGV)),
              (((I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)) * I.P_IGV)) / NULLIF(A.IMPORT_CAM, 0))) IGV_DOLAR
  FROM {S}DOCUVENT A
       LEFT JOIN {S}ITEMDOCU I           ON  I.TIPODOC = A.TIPODOC
                                         AND I.SERIE   = A.SERIE
                                         AND I.NUMERO  = A.NUMERO
       LEFT JOIN {S}EQUIVALENCIA E       ON  E.COD_ART = I.COD_ART
                                         AND E.UNIDAD  = 'KG'
       LEFT JOIN {S}ARTICUL M            ON  M.COD_ART = I.COD_ART
       LEFT JOIN {S}CLIENTES C           ON  C.COD_CLIENTE = A.COD_CLIENTE
       LEFT JOIN {S}TABLAS_AUXILIARES T  ON  T.CODIGO  = C.VENDEDOR
                                         AND T.TIPO    = 29
       LEFT JOIN {S}TABLAS_AUXILIARES T2 ON  T2.CODIGO = C.GIRO
                                         AND T2.TIPO   = 27
       LEFT JOIN (SELECT GRUPO, MIN(COD_CLIENTE) AS MIN_CLIENTE
                    FROM {S}CLIENTE_RELACION
                   GROUP BY GRUPO) GRP   ON  GRP.GRUPO = C.GRUPO_REL
       LEFT JOIN {S}CLIENTES CLL         ON  CLL.COD_CLIENTE = DECODE(C.GRUPO_REL, NULL, A.COD_CLIENTE, GRP.MIN_CLIENTE)
 WHERE A.FECHA BETWEEN :FECHA1 AND :FECHA2
   AND NVL(A.ESTADO, '0') <> '9'
   AND NVL(A.ORIGEN, '0') <> 'A'
   AND M.TP_ART IN ('T', 'S')
 GROUP BY A.COD_CLIENTE, CLL.NOMBRE, CLL.RUC, T.DESCRIPCION,
          C.GIRO, T2.ABREVIADA, C.VENDEDOR, A.MONEDA, C.GRUPO_REL, GRP.MIN_CLIENTE
 ORDER BY C.VENDEDOR, C.GRUPO_REL ASC";

        // ── Cargar filas desde Oracle ───────────────────────────────────────────
        private async Task<List<DcmFilaRawDto>> CargarFilasAsync(
            DateTime fechaInicio, DateTime fechaFin)
        {
            var connStr = GetOracleConnectionString();
            var filas   = new List<DcmFilaRawDto>();
            if (string.IsNullOrEmpty(connStr)) return filas;

            try
            {
                using var conn   = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd    = new OracleCommand(BuildSql(), conn) { BindByName = true };
                cmd.Parameters.Add("FECHA1", OracleDbType.Date).Value = fechaInicio.Date;
                cmd.Parameters.Add("FECHA2", OracleDbType.Date).Value = fechaFin.Date;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    filas.Add(new DcmFilaRawDto
                    {
                        CodCliente = GetStr(reader, "COD_CLIENTE"),
                        Ruc        = GetStr(reader, "RUC"),
                        Nombre     = GetStr(reader, "NOMBRE"),
                        Giro       = GetStr(reader, "GIRO"),
                        DescGiro   = GetStr(reader, "DESC_GIRO"),
                        CodAsesor  = GetStr(reader, "COD_ASESOR"),
                        Asesor     = GetStr(reader, "ASESOR"),
                        NroDoc     = GetInt(reader, "NRODOC"),
                        TotUnid    = GetDec(reader, "TOTUNID"),
                        Moneda     = GetStr(reader, "MONEDA"),
                        Soles      = GetDec(reader, "SOLES"),
                        Dolar      = GetDec(reader, "DOLAR"),
                        IgvSoles   = GetDec(reader, "IGV_SOLES"),
                        IgvDolar   = GetDec(reader, "IGV_DOLAR"),
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar datos del Dashboard Comercial Maestro");
            }

            return filas;
        }

        // ── Importe e IGV según moneda seleccionada ─────────────────────────────
        private static decimal Imp(DcmFilaRawDto f, string moneda) =>
            moneda.Equals("S", StringComparison.OrdinalIgnoreCase) ? f.Soles : f.Dolar;

        private static decimal Igv(DcmFilaRawDto f, string moneda) =>
            moneda.Equals("S", StringComparison.OrdinalIgnoreCase) ? f.IgvSoles : f.IgvDolar;

        // ── Total (base + IGV) — es el valor que se muestra en todos los reportes
        private static decimal ImpTotal(DcmFilaRawDto f, string moneda) =>
            Imp(f, moneda) + Igv(f, moneda);

        // ── Proyectar una fila raw al DTO de cliente maestro ────────────────────
        private static DcmClienteMaestroDto ToClienteDto(DcmFilaRawDto f, string mon)
        {
            var imp = Imp(f, mon);
            var igv = Igv(f, mon);
            return new DcmClienteMaestroDto
            {
                Asesor      = f.Asesor,
                CodAsesor   = f.CodAsesor,
                CodCliente  = f.CodCliente,
                Ruc         = f.Ruc,
                RazonSocial = f.Nombre,
                Giro        = string.IsNullOrEmpty(f.DescGiro) ? "SIN GIRO" : f.DescGiro,
                NroDoc      = f.NroDoc,
                CantidadKg  = f.TotUnid,
                Importe     = imp,
                Igv         = igv,
                Total       = imp + igv,
            };
        }

        // ════════════════════════════════════════════════════════════════════════
        // ObtenerDashboardAsync — punto de entrada principal
        // ════════════════════════════════════════════════════════════════════════
        public async Task<DcmDashboardDto> ObtenerDashboardAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda, int top = 3)
        {
            var mon   = string.IsNullOrEmpty(moneda) ? "D" : moneda.ToUpperInvariant();
            var filas = await CargarFilasAsync(fechaInicio, fechaFin);
            var dto   = new DcmDashboardDto();

            if (filas.Count == 0) return dto;

            // El query puede retornar dos filas por cliente (una 'S' y una 'D').
            // Consolidamos sumando Importe y KG por cliente según la moneda elegida.
            var filasConsolidadas = filas
                .GroupBy(f => new { f.CodAsesor, f.Asesor, f.CodCliente, f.Ruc, f.Nombre, f.DescGiro })
                .Select(g => new DcmFilaRawDto
                {
                    CodCliente = g.Key.CodCliente,
                    Ruc        = g.Key.Ruc,
                    Nombre     = g.Key.Nombre,
                    DescGiro   = g.Key.DescGiro,
                    CodAsesor  = g.Key.CodAsesor,
                    Asesor     = g.Key.Asesor,
                    NroDoc     = g.Sum(f => f.NroDoc),
                    TotUnid    = g.Sum(f => f.TotUnid),
                    Soles      = g.Sum(f => f.Soles),
                    Dolar      = g.Sum(f => f.Dolar),
                    IgvSoles   = g.Sum(f => f.IgvSoles),
                    IgvDolar   = g.Sum(f => f.IgvDolar),
                })
                .ToList();

            // ── 1. Importe + KG + NroDoc por Asesor ─────────────────────────────
            dto.Asesores = filasConsolidadas
                .GroupBy(f => new { f.CodAsesor, f.Asesor })
                .Select(g => new DcmImporteAsesorDto
                {
                    CodAsesor = g.Key.CodAsesor,
                    Asesor    = g.Key.Asesor,
                    Importe   = g.Sum(f => ImpTotal(f, mon)),
                    Kilos     = g.Sum(f => f.TotUnid),
                    NroDoc    = g.Sum(f => f.NroDoc),
                })
                .Where(x => x.Importe > 0)
                .OrderByDescending(x => x.Importe)
                .ToList();

            // ── 2. Nro. Clientes distintos por Asesor ───────────────────────────
            dto.Clientes = filasConsolidadas
                .Where(f => !string.Equals(f.Asesor, "OFICINA", StringComparison.OrdinalIgnoreCase)
                         && ImpTotal(f, mon) > 0)
                .GroupBy(f => f.Asesor)
                .Select(g => new DcmNroClientesAsesorDto
                {
                    Asesor      = g.Key,
                    NroClientes = g.Select(f => f.CodCliente).Distinct().Count()
                })
                .OrderBy(x => x.Asesor)
                .ToList();

            // ── 3. Todos los clientes (tabla maestra, ranking y exportación) ────
            dto.ClientesTodos = filasConsolidadas
                .Where(f => !string.Equals(f.Asesor, "OFICINA", StringComparison.OrdinalIgnoreCase)
                         && ImpTotal(f, mon) > 0)
                .Select(f => ToClienteDto(f, mon))
                .OrderBy(x => x.Asesor).ThenByDescending(x => x.Total)
                .ToList();

            // ── 4. Top N clientes por Asesor (Importe y KG) ─────────────────────
            var topImp = filasConsolidadas
                .Where(f => !string.Equals(f.Asesor, "OFICINA", StringComparison.OrdinalIgnoreCase)
                         && ImpTotal(f, mon) > 0)
                .GroupBy(f => f.Asesor)
                .SelectMany(g => g.OrderByDescending(f => ImpTotal(f, mon)).Take(top)
                    .Select(f => new DcmTopClienteAsesorDto
                    {
                        Asesor      = f.Asesor,
                        CodCliente  = f.CodCliente,
                        RazonSocial = f.Nombre,
                        CantidadKg  = f.TotUnid,
                        Importe     = ImpTotal(f, mon),
                        NroDoc      = f.NroDoc,
                        TopType     = "importe",
                    }))
                .ToList();

            var topKg = filasConsolidadas
                .Where(f => !string.Equals(f.Asesor, "OFICINA", StringComparison.OrdinalIgnoreCase)
                         && f.TotUnid > 0)
                .GroupBy(f => f.Asesor)
                .SelectMany(g => g.OrderByDescending(f => f.TotUnid).Take(top)
                    .Select(f => new DcmTopClienteAsesorDto
                    {
                        Asesor      = f.Asesor,
                        CodCliente  = f.CodCliente,
                        RazonSocial = f.Nombre,
                        CantidadKg  = f.TotUnid,
                        Importe     = ImpTotal(f, mon),
                        NroDoc      = f.NroDoc,
                        TopType     = "kg",
                    }))
                .ToList();

            var keysBoth = topImp
                .Select(r => (r.Asesor, r.CodCliente))
                .Intersect(topKg.Select(r => (r.Asesor, r.CodCliente)))
                .ToHashSet();

            dto.TopClientes = topImp
                .Union(topKg)
                .DistinctBy(r => (r.Asesor, r.CodCliente))
                .Select(r =>
                {
                    if (keysBoth.Contains((r.Asesor, r.CodCliente))) r.TopType = "both";
                    return r;
                })
                .ToList();

            return dto;
        }

        // ════════════════════════════════════════════════════════════════════════
        // ObtenerClientesPorAsesorAsync — clientes de un asesor (filtrado en memoria)
        // ════════════════════════════════════════════════════════════════════════
        public async Task<List<DcmClienteMaestroDto>> ObtenerClientesPorAsesorAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda, string asesor)
        {
            var mon   = string.IsNullOrEmpty(moneda) ? "D" : moneda.ToUpperInvariant();
            var filas = await CargarFilasAsync(fechaInicio, fechaFin);

            return filas
                .Where(f => string.Equals(f.Asesor, asesor, StringComparison.OrdinalIgnoreCase))
                .GroupBy(f => new { f.CodCliente, f.Ruc, f.Nombre, f.DescGiro })
                .Select(g =>
                {
                    var imp = g.Sum(f => Imp(f, mon));
                    var igv = g.Sum(f => Igv(f, mon));
                    return new DcmClienteMaestroDto
                    {
                        Asesor      = asesor,
                        CodCliente  = g.Key.CodCliente,
                        Ruc         = g.Key.Ruc,
                        RazonSocial = g.Key.Nombre,
                        Giro        = string.IsNullOrEmpty(g.Key.DescGiro) ? "SIN GIRO" : g.Key.DescGiro,
                        NroDoc      = g.Sum(f => f.NroDoc),
                        CantidadKg  = g.Sum(f => f.TotUnid),
                        Importe     = imp,
                        Igv         = igv,
                        Total       = imp + igv,
                    };
                })
                .Where(x => x.Total > 0)
                .OrderByDescending(x => x.Total)
                .ToList();
        }

        // ════════════════════════════════════════════════════════════════════════
        // DiagnosticoFilasAsync
        // ════════════════════════════════════════════════════════════════════════
        public async Task<int> DiagnosticoFilasAsync(DateTime fechaInicio, DateTime fechaFin)
        {
            var connStr = GetOracleConnectionString();
            var schema  = S;
            int count   = 0;

            _logger.LogWarning("[DIAG] Schema usado: '{Schema}'", schema);
            _logger.LogWarning("[DIAG] FechaInicio: {FI:yyyy-MM-dd}  FechaFin: {FF:yyyy-MM-dd}", fechaInicio, fechaFin);

            if (string.IsNullOrEmpty(connStr))
            {
                _logger.LogWarning("[DIAG] Sin connection string.");
                return -1;
            }

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();

                var sqlCount = $@"
SELECT COUNT(*) FROM (
SELECT DECODE(C.GRUPO_REL, NULL, A.COD_CLIENTE, GRP.MIN_CLIENTE) AS COD_CLIENTE, A.MONEDA
  FROM {schema}DOCUVENT A
       LEFT JOIN {schema}ITEMDOCU I    ON  I.TIPODOC = A.TIPODOC AND I.SERIE = A.SERIE AND I.NUMERO = A.NUMERO
       LEFT JOIN {schema}ARTICUL M     ON  M.COD_ART = I.COD_ART
       LEFT JOIN {schema}CLIENTES C    ON  C.COD_CLIENTE = A.COD_CLIENTE
       LEFT JOIN (SELECT GRUPO, MIN(COD_CLIENTE) AS MIN_CLIENTE
                    FROM {schema}CLIENTE_RELACION GROUP BY GRUPO) GRP ON GRP.GRUPO = C.GRUPO_REL
 WHERE A.FECHA BETWEEN :FECHA1 AND :FECHA2
   AND NVL(A.ESTADO,'0') <> '9'
   AND NVL(A.ORIGEN, '0') <> 'A'
   AND M.TP_ART IN ('T','S')
 GROUP BY DECODE(C.GRUPO_REL, NULL, A.COD_CLIENTE, GRP.MIN_CLIENTE), C.VENDEDOR, A.MONEDA, C.GRUPO_REL, GRP.MIN_CLIENTE)";

                using var cmd = new OracleCommand(sqlCount, conn) { BindByName = true };
                cmd.Parameters.Add("FECHA1", OracleDbType.Date).Value = fechaInicio.Date;
                cmd.Parameters.Add("FECHA2", OracleDbType.Date).Value = fechaFin.Date;

                var result = await cmd.ExecuteScalarAsync();
                count = Convert.ToInt32(result);

                _logger.LogWarning("[DIAG] COUNT(*) directo Oracle: {N}", count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DIAG] Error en DiagnosticoFilasAsync");
                return -1;
            }

            return count;
        }
    }
}
