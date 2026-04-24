using Oracle.ManagedDataAccess.Client;
using FabricaHilos.Models.Ventas;

namespace FabricaHilos.Services.Ventas
{
    public class DashboardComercialService : OracleServiceBase, IDashboardComercialService
    {
        private readonly ILogger<DashboardComercialService> _logger;

        public DashboardComercialService(
            IConfiguration configuration,
            ILogger<DashboardComercialService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(configuration, httpContextAccessor)
        {
            _logger = logger;
        }

        // ── Helpers de lectura ──────────────────────────────────────────────────
        private static string?  GetStr(OracleDataReader r, string col) =>
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
        private static int      GetInt(OracleDataReader r, string col) =>
            r[col] == DBNull.Value ? 0  : Convert.ToInt32(r[col]);

        // ── SQL principal (único query de toda la pantalla) ─────────────────────
        private string BuildSql() => $@"
SELECT D.COD_CLIENTE,
       C.NOMBRE,
       C.RUC,
       C.GIRO,
       T2.ABREVIADA DESC_GIRO,
       D.COD_VENDE COD_ASESOR,
       T.DESCRIPCION ASESOR,
       I.TIPODOC,
       I.SERIE,
       I.NUMERO NUMDOC,
       G.COD_ALM,
       D.FECHA,
       A.TP_ART,
       A.COD_FAM,
       A.COD_LIN,
       I.COD_ART,
       DECODE(A.DESCRIPCION,
              'VARIOS',
              I.DETALLE,
              A.DESCRIPCION || ' (' || I.COLOR_DET || ')') DESCRIPCION,
       A.UNIDAD,
       (I.CANTIDAD * E.FACTOR) KILOS,
       I.CANTIDAD,
       I.IMP_VVTA,
       DECODE(D.MONEDA, 'S', 'S', 'D') MON,
       G.NRO_DOC_REF NUMERO1,
       TO_CHAR(D.FECHA, 'YYYY/MM') FEC,
       I.VVTU,
       DECODE(D.MONEDA,
              'S',
              I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000),
              (I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)) *
              D.IMPORT_CAM) SOLES,
       DECODE(D.MONEDA,
              'D',
              I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000),
              (I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)) /
              NULLIF(D.IMPORT_CAM, 0)) DOLAR,
       DECODE(D.MONEDA,
              'S',
              (I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)) * I.P_IGV,
              ((I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)) * I.P_IGV) *
              D.IMPORT_CAM) IGV_SOLES,
       DECODE(D.MONEDA,
              'D',
              (I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)) * I.P_IGV,
              ((I.IMP_VVTA * ((100 - I.POR_DESC1) * (100 - I.POR_DESC2) / 10000)) * I.P_IGV) /
              NULLIF(D.IMPORT_CAM, 0)) IGV_DOLAR,
       I.POR_DESC1,
       I.POR_DESC2
  FROM {S}ITEMDOCU          I,
       {S}DOCUVENT          D,
       {S}KARDEX_G          G,
       {S}EQUIVALENCIA      E,
       {S}ARTICUL           A,
       {S}CLIENTES          C,
       {S}TABLAS_AUXILIARES T,
       {S}TABLAS_AUXILIARES T2
 WHERE D.TIPODOC = I.TIPODOC
   AND D.SERIE = I.SERIE
   AND D.NUMERO = I.NUMERO
   AND D.FECHA BETWEEN :FECHAI AND :FECHAF
   AND NVL(D.ESTADO, '0') <> '9'
   AND NVL(D.ORIGEN, '0') <> 'A'
   AND G.TP_TRANSAC(+) = D.TIP_DOC_REF
   AND TO_CHAR(G.SERIE(+)) = D.SER_DOC_REF
   AND G.NUMERO(+) = D.NRO_DOC_REF
   AND G.COD_RELACION(+) = D.COD_CLIENTE
   AND E.COD_ART(+) = I.COD_ART
   AND E.UNIDAD(+) = 'KG'
   AND A.COD_ART = I.COD_ART
   AND C.COD_CLIENTE = D.COD_CLIENTE
   AND T.TIPO(+) = 29
   AND T.CODIGO(+) = D.COD_VENDE
   AND T2.TIPO(+) = 27
   AND T2.CODIGO(+) = C.GIRO
   --AND A.COD_FAM NOT IN ('ANT','Z01','ZVA')
   AND TP_ART IN ('T','S')
 ORDER BY C.NOMBRE, T.DESCRIPCION, I.TIPODOC, I.SERIE, I.NUMERO";

        // ── Cargar filas crudas ─────────────────────────────────────────────────
        private async Task<List<DcFilaRawDto>> CargarFilasAsync(
            DateTime fechaInicio, DateTime fechaFin)
        {
            var connStr = GetOracleConnectionString();
            var filas   = new List<DcFilaRawDto>();
            if (string.IsNullOrEmpty(connStr)) return filas;

            try
            {
                using var conn   = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd    = new OracleCommand(BuildSql(), conn) { BindByName = true };
                cmd.Parameters.Add("FECHAI", OracleDbType.Date).Value = fechaInicio.Date;
                cmd.Parameters.Add("FECHAF", OracleDbType.Date).Value = fechaFin.Date;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var fec = GetStr(reader, "FEC");
                    filas.Add(new DcFilaRawDto
                    {
                        CodCliente  = GetStr(reader, "COD_CLIENTE"),
                        Nombre      = GetStr(reader, "NOMBRE"),
                        DescGiro    = GetStr(reader, "DESC_GIRO"),
                        CodAsesor   = GetStr(reader, "COD_ASESOR"),
                        Asesor      = GetStr(reader, "ASESOR"),
                        TipoDoc     = GetStr(reader, "TIPODOC"),
                        Serie       = GetStr(reader, "SERIE"),
                        NumDoc      = GetStr(reader, "NUMDOC"),
                        CodAlm      = GetStr(reader, "COD_ALM"),
                        TpArt       = GetStr(reader, "TP_ART"),
                        CodFam      = GetStr(reader, "COD_FAM"),
                        CodLin      = GetStr(reader, "COD_LIN"),
                        CodArt      = GetStr(reader, "COD_ART"),
                        Descripcion = GetStr(reader, "DESCRIPCION"),
                        Unidad      = GetStr(reader, "UNIDAD"),
                        Cantidad    = GetDec(reader, "CANTIDAD"),
                        ImpVvta     = GetDec(reader, "IMP_VVTA"),
                        Mon         = GetStr(reader, "MON"),
                        Numero1     = GetStr(reader, "NUMERO1"),
                        Fec         = fec,
                        Anio        = fec != null && fec.Length >= 4 ? int.Parse(fec[..4]) : 0,
                        Vvtu        = GetDec(reader, "VVTU"),
                        PorDesc1    = GetDec(reader, "POR_DESC1"),
                        PorDesc2    = GetDec(reader, "POR_DESC2"),
                        Kilos       = GetDec(reader, "KILOS"),
                        Soles       = GetDec(reader, "SOLES"),
                        Dolar       = GetDec(reader, "DOLAR"),
                        IgvSoles    = GetDec(reader, "IGV_SOLES"),
                        IgvDolar    = GetDec(reader, "IGV_DOLAR"),
                        Ruc         = GetStr(reader, "RUC"),
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar datos del Dashboard Comercial");
            }

            return filas;
        }

        // ── Importe según moneda ────────────────────────────────────────────────
        private static decimal Imp(DcFilaRawDto f, string moneda) =>
            moneda.Equals("S", StringComparison.OrdinalIgnoreCase) ? f.Soles : f.Dolar;

        private static decimal Igv(DcFilaRawDto f, string moneda) =>
            moneda.Equals("S", StringComparison.OrdinalIgnoreCase) ? f.IgvSoles : f.IgvDolar;

        private static decimal ImpTotal(DcFilaRawDto f, string moneda) =>
            Imp(f, moneda) + Igv(f, moneda);

        // ════════════════════════════════════════════════════════════════════════
        // ObtenerDashboardAsync — punto de entrada principal
        // ════════════════════════════════════════════════════════════════════════
        public async Task<DcDashboardDto> ObtenerDashboardAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda, int top = 3)
        {
            var mon   = string.IsNullOrEmpty(moneda) ? "D" : moneda.ToUpperInvariant();
            var filas = await CargarFilasAsync(fechaInicio, fechaFin);
            var dto   = new DcDashboardDto();

            if (filas.Count == 0) return dto;

            // ── 1. Importe por Asesor / Mes ─────────────────────────────────────
            dto.Importe = filas
                .GroupBy(f => new { f.CodAsesor, f.Asesor, f.Fec })
                .Select(g => new DcImporteAsesorMesDto
                {
                    CodAsesor = g.Key.CodAsesor,
                    Asesor    = g.Key.Asesor,
                    Mes       = g.Key.Fec,
                    Importe   = g.Sum(f => Imp(f, mon))
                })
                .OrderBy(x => x.Asesor).ThenBy(x => x.Mes)
                .ToList();

            // ── 2. KG por Asesor / Mes ───────────────────────────────────────────
            dto.Kg = filas
                .GroupBy(f => new { f.Asesor, f.Fec })
                .Select(g => new DcCantidadKgAsesorMesDto
                {
                    Asesor     = g.Key.Asesor,
                    Mes        = g.Key.Fec,
                    CantidadKg = g.Sum(f => f.Kilos)
                })
                .OrderBy(x => x.Asesor).ThenBy(x => x.Mes)
                .ToList();

            // ── 3. Nro. Clientes distintos por Asesor ───────────────────────────
            dto.Clientes = filas
                .Where(f => !string.Equals(f.Asesor, "OFICINA", StringComparison.OrdinalIgnoreCase))
                .GroupBy(f => new { f.Asesor, f.CodCliente })
                .Select(g => new { g.Key.Asesor, g.Key.CodCliente, Imp = g.Sum(f => Imp(f, mon)) })
                .Where(x => x.Imp > 0)
                .GroupBy(x => x.Asesor)
                .Select(g => new DcNroClientesAsesorMesDto
                {
                    Asesor      = g.Key,
                    Mes         = null,
                    NroClientes = g.Count()
                })
                .OrderBy(x => x.Asesor)
                .ToList();

            // ── 4. Todos los clientes (para Ranking, Participación y Export) ────
            dto.ClientesTodos = filas
                .Where(f => !string.Equals(f.Asesor, "OFICINA", StringComparison.OrdinalIgnoreCase))
                .GroupBy(f => new { f.Asesor, f.CodCliente, f.Ruc, f.Nombre, f.DescGiro })
                .Select(g => new DcClienteImporteTodosDto
                {
                    Asesor      = g.Key.Asesor,
                    CodCliente  = g.Key.CodCliente,
                    Ruc         = g.Key.Ruc,
                    RazonSocial = g.Key.Nombre,
                    Giro        = string.IsNullOrEmpty(g.Key.DescGiro) ? "SIN GIRO" : g.Key.DescGiro,
                    Moneda      = mon,
                    Importe     = g.Sum(f => Imp(f, mon)),
                    Igv         = g.Sum(f => Igv(f, mon)),
                    Total       = g.Sum(f => ImpTotal(f, mon)),
                    CantidadKg  = g.Sum(f => f.Kilos)
                })
                .Where(x => x.Total > 0)
                .OrderBy(x => x.Asesor).ThenByDescending(x => x.Total)
                .ToList();

            // ── 5. Top N clientes por Asesor / Año ──────────────────────────────
            var porAsesorAnio = filas
                .Where(f => !string.Equals(f.Asesor, "OFICINA", StringComparison.OrdinalIgnoreCase))
                .GroupBy(f => new { f.Asesor, f.Anio, f.CodCliente, f.Nombre })
                .Select(g => new DcTopClienteAsesorDto
                {
                    Asesor      = g.Key.Asesor,
                    RazonSocial = g.Key.Nombre,
                    Anio        = g.Key.Anio,
                    Importe     = g.Sum(f => Imp(f, mon)),
                    CantidadKg  = g.Sum(f => f.Kilos),
                    TopType     = "both"
                })
                .Where(x => x.Asesor != null)
                .ToList();

            var topImp = porAsesorAnio
                .GroupBy(r => new { r.Asesor, r.Anio })
                .SelectMany(g => g.OrderByDescending(r => r.Importe).Take(top)
                                  .Select(r => { r.TopType = "importe"; return r; }))
                .ToList();

            var topKg = porAsesorAnio
                .GroupBy(r => new { r.Asesor, r.Anio })
                .SelectMany(g => g.OrderByDescending(r => r.CantidadKg).Take(top)
                                  .Select(r => { r.TopType = "kg"; return r; }))
                .ToList();

            var keysBoth = topImp
                .Select(r => (r.Asesor, r.Anio, r.RazonSocial))
                .Intersect(topKg.Select(r => (r.Asesor, r.Anio, r.RazonSocial)))
                .ToHashSet();

            dto.TopClientes = topImp
                .Union(topKg)
                .DistinctBy(r => (r.Asesor, r.Anio, r.RazonSocial))
                .Select(r =>
                {
                    if (keysBoth.Contains((r.Asesor, r.Anio, r.RazonSocial))) r.TopType = "both";
                    return r;
                })
                .ToList();

            return dto;
        }

        // ════════════════════════════════════════════════════════════════════════
        // ObtenerClientesPorAsesorAsync — detalle de un asesor específico
        // ════════════════════════════════════════════════════════════════════════
        public async Task<List<DcClienteImporteAsesorDto>> ObtenerClientesPorAsesorAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda, string asesor)
        {
            var mon   = string.IsNullOrEmpty(moneda) ? "D" : moneda.ToUpperInvariant();
            var filas = await CargarFilasAsync(fechaInicio, fechaFin);

            return filas
                .Where(f => string.Equals(f.Asesor, asesor, StringComparison.OrdinalIgnoreCase))
                .GroupBy(f => new { f.CodCliente, f.Nombre, f.DescGiro, f.Unidad })
                .Select(g => new DcClienteImporteAsesorDto
                {
                    CodCliente  = g.Key.CodCliente,
                    RazonSocial = g.Key.Nombre,
                    Giro        = string.IsNullOrEmpty(g.Key.DescGiro) ? "SIN GIRO" : g.Key.DescGiro,
                    Unidad      = g.Key.Unidad,
                    CantidadKg  = g.Sum(f => f.Kilos),
                    Importe     = g.Sum(f => Imp(f, mon)),
                })
                .Where(x => x.Importe > 0)
                .OrderByDescending(x => x.Importe)
                .ToList();
        }

        // ════════════════════════════════════════════════════════════════════════
        // DiagnosticoFilasAsync — COUNT(*) directo para comparar con Toad
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
SELECT COUNT(*) FROM {schema}ITEMDOCU I, {schema}DOCUVENT D, {schema}KARDEX_G G,
       {schema}EQUIVALENCIA E, {schema}ARTICUL A, {schema}CLIENTES C,
       {schema}TABLAS_AUXILIARES T, {schema}TABLAS_AUXILIARES T2
 WHERE D.TIPODOC = I.TIPODOC AND D.SERIE = I.SERIE AND D.NUMERO = I.NUMERO
   AND D.FECHA BETWEEN :FECHAI AND :FECHAF
   AND NVL(D.ESTADO, '0') <> '9'
   AND NVL(D.ORIGEN, '0') <> 'A'
   AND G.TP_TRANSAC(+) = D.TIP_DOC_REF AND TO_CHAR(G.SERIE(+)) = D.SER_DOC_REF
   AND G.NUMERO(+) = D.NRO_DOC_REF AND G.COD_RELACION(+) = D.COD_CLIENTE
   AND E.COD_ART(+) = I.COD_ART AND E.UNIDAD(+) = 'KG'
   AND A.COD_ART = I.COD_ART AND C.COD_CLIENTE = D.COD_CLIENTE
   AND T.TIPO(+) = 29 AND T.CODIGO(+) = D.COD_VENDE
   AND T2.TIPO(+) = 27 AND T2.CODIGO(+) = C.GIRO";

                using var cmd = new OracleCommand(sqlCount, conn) { BindByName = true };
                cmd.Parameters.Add("FECHAI", OracleDbType.Date).Value = fechaInicio.Date;
                cmd.Parameters.Add("FECHAF", OracleDbType.Date).Value = fechaFin.Date;

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

