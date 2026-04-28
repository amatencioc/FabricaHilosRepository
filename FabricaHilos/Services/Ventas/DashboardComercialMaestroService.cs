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

        // ── SQL principal — query agrupado por cliente/asesor ──────────────────
        //  IMPORTANTE: el RUC y NOMBRE se traen en una capa EXTERIOR para evitar
        //  que duplicados en CLIENTES (RUC/NOMBRE inconsistentes para el mismo
        //  COD_CLIENTE) inflen los montos al participar en el GROUP BY interno.
        private string BuildSql() => $@"
SELECT A.COD_CLIENTE,
       CLL.RUC,
       CLL.NOMBRE,
       A.GIRO,
       A.DESC_GIRO,
       A.COD_ASESOR,
       A.ASESOR,
       NVL(C.NRODOC,  0)  NRODOC,
       NVL(C.TOTUNID, 0)  TOTUNID,
       (A.SOLES  - NVL(B.SOLES_ANT,  0)) SOLES,
       (A.DOLAR  - NVL(B.DOLAR_ANT,  0)) DOLAR
  FROM (SELECT DECODE(C.GRUPO_REL, NULL, V.COD_CLIENTE, GRP.MIN_CLIENTE) AS COD_CLIENTE,
               C.GIRO,
               T2.ABREVIADA DESC_GIRO,
               C.VENDEDOR  COD_ASESOR,
               T.DESCRIPCION ASESOR,
               SUM(DECODE(:P_OPCION, 'TODOS', V.SOLES,   V.SOLES_SINANT))   SOLES,
               SUM(DECODE(:P_OPCION, 'TODOS', V.DOLARES, V.DOLARES_SINANT)) DOLAR
          FROM V_DOCUVEN V
          LEFT JOIN CLIENTES C            ON  C.COD_CLIENTE = V.COD_CLIENTE
          LEFT JOIN TABLAS_AUXILIARES T   ON  T.CODIGO  = C.VENDEDOR
                                          AND T.TIPO    = 29
          LEFT JOIN TABLAS_AUXILIARES T2  ON  T2.CODIGO = C.GIRO
                                          AND T2.TIPO   = 27
          LEFT JOIN (SELECT GRUPO, MIN(COD_CLIENTE) AS MIN_CLIENTE
                       FROM {S}CLIENTE_RELACION
                      GROUP BY GRUPO) GRP ON  GRP.GRUPO = C.GRUPO_REL
         WHERE V.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
         GROUP BY DECODE(C.GRUPO_REL, NULL, V.COD_CLIENTE, GRP.MIN_CLIENTE),
                  C.GIRO,
                  T2.ABREVIADA,
                  C.VENDEDOR,
                  T.DESCRIPCION) A
  LEFT JOIN (SELECT COD_CLIENTE, MIN(RUC) RUC, MIN(NOMBRE) NOMBRE
               FROM CLIENTES
              GROUP BY COD_CLIENTE) CLL ON CLL.COD_CLIENTE = A.COD_CLIENTE
  LEFT JOIN (SELECT DECODE(C.GRUPO_REL, NULL, D.COD_CLIENTE, GRP.MIN_CLIENTE) AS COD_CLIENTE,
                    C.VENDEDOR COD_ASESOR,
                    SUM(DECODE(D.MONEDA,
                               'S', I.IMP_VVTA,
                               ROUND(I.IMP_VVTA * D.IMPORT_CAM, 2))) SOLES_ANT,
                    SUM(DECODE(D.MONEDA,
                               'D', I.IMP_VVTA,
                               ROUND(I.IMP_VVTA / NULLIF(D.IMPORT_CAM, 0), 2))) DOLAR_ANT
               FROM DOCUVENT D
               JOIN ITEMDOCU I              ON  I.TIPODOC = D.TIPODOC
                                            AND I.SERIE   = D.SERIE
                                            AND I.NUMERO  = D.NUMERO
               LEFT JOIN CLIENTES C         ON  C.COD_CLIENTE = D.COD_CLIENTE
               LEFT JOIN (SELECT GRUPO, MIN(COD_CLIENTE) AS MIN_CLIENTE
                            FROM {S}CLIENTE_RELACION
                           GROUP BY GRUPO) GRP ON GRP.GRUPO = C.GRUPO_REL
              WHERE :P_OPCION <> 'TODOS'
                AND D.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
                AND D.ESTADO <> '9'
                AND I.COD_ART IN ('9300049997',
                                  '9300049999',
                                  '930004999A',
                                  '9300049998')
              GROUP BY DECODE(C.GRUPO_REL, NULL, D.COD_CLIENTE, GRP.MIN_CLIENTE),
                       C.VENDEDOR) B
    ON  B.COD_CLIENTE = A.COD_CLIENTE
    AND B.COD_ASESOR  = A.COD_ASESOR
  LEFT JOIN (SELECT DECODE(C.GRUPO_REL, NULL, D.COD_CLIENTE, GRP.MIN_CLIENTE) AS COD_CLIENTE,
                    C.VENDEDOR COD_ASESOR,
                    COUNT(DISTINCT D.TIPODOC || '-' || D.SERIE || '-' || D.NUMERO) NRODOC,
                    SUM(I.CANTIDAD * E.FACTOR) TOTUNID
               FROM DOCUVENT D
               JOIN ITEMDOCU I              ON  I.TIPODOC = D.TIPODOC
                                            AND I.SERIE   = D.SERIE
                                            AND I.NUMERO  = D.NUMERO
               LEFT JOIN EQUIVALENCIA E     ON  E.COD_ART = I.COD_ART
                                            AND E.UNIDAD  = 'KG'
               LEFT JOIN CLIENTES C         ON  C.COD_CLIENTE = D.COD_CLIENTE
               LEFT JOIN (SELECT GRUPO, MIN(COD_CLIENTE) AS MIN_CLIENTE
                            FROM {S}CLIENTE_RELACION
                           GROUP BY GRUPO) GRP ON GRP.GRUPO = C.GRUPO_REL
              WHERE D.FECHA BETWEEN :P_FECHA1 AND :P_FECHA2
                AND D.ESTADO <> '9'
                AND (:P_OPCION = 'TODOS'
                     OR I.COD_ART NOT IN ('9300049997',
                                          '9300049999',
                                          '930004999A',
                                          '9300049998'))
              GROUP BY DECODE(C.GRUPO_REL, NULL, D.COD_CLIENTE, GRP.MIN_CLIENTE),
                       C.VENDEDOR) C
    ON  C.COD_CLIENTE = A.COD_CLIENTE
    AND C.COD_ASESOR  = A.COD_ASESOR
 ORDER BY A.COD_ASESOR, A.COD_CLIENTE";

        // ── Cargar filas desde Oracle ───────────────────────────────────────────
        private async Task<List<DcmFilaRawDto>> CargarFilasAsync(
            DateTime fechaInicio, DateTime fechaFin, string opcion = "CON VENDEDOR")
        {
            var connStr = GetOracleConnectionString();
            var filas   = new List<DcmFilaRawDto>();
            if (string.IsNullOrEmpty(connStr)) return filas;

            try
            {
                using var conn   = new OracleConnection(connStr);
                await conn.OpenAsync();

                using var cmd    = new OracleCommand(BuildSql(), conn) { BindByName = true };
                cmd.Parameters.Add("P_OPCION", OracleDbType.Varchar2).Value = opcion;
                cmd.Parameters.Add("P_FECHA1", OracleDbType.Date).Value     = fechaInicio.Date;
                cmd.Parameters.Add("P_FECHA2", OracleDbType.Date).Value     = fechaFin.Date;

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
                        Soles      = GetDec(reader, "SOLES"),
                        Dolar      = GetDec(reader, "DOLAR"),
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cargar datos del Dashboard Comercial Maestro");
                return filas;
            }

            _logger.LogInformation("[DCM] Filas cargadas: {N} | Opcion: {Op} | Fechas: {F1:dd/MM/yyyy}-{F2:dd/MM/yyyy}",
                filas.Count, opcion, fechaInicio, fechaFin);

            return filas;
        }

        // ── Importe e IGV según moneda seleccionada ─────────────────────────────
        private static decimal Imp(DcmFilaRawDto f, string moneda) =>
            moneda.Equals("S", StringComparison.OrdinalIgnoreCase) ? f.Soles : f.Dolar;

        // ── Total — el nuevo query devuelve el importe neto directamente (sin IGV separado)
        private static decimal ImpTotal(DcmFilaRawDto f, string moneda) =>
            Imp(f, moneda);

        // ── Proyectar una fila raw al DTO de cliente maestro ────────────────────
        private static DcmClienteMaestroDto ToClienteDto(DcmFilaRawDto f, string mon)
        {
            var imp = Imp(f, mon);
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
                Total       = imp,
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

            // El query devuelve una fila por (COD_CLIENTE, COD_ASESOR) — sin duplicados.
            var filasConsolidadas = filas;

            // ── 1. Todos los clientes (tabla maestra, ranking y exportación) ────
            dto.ClientesTodos = filasConsolidadas
                .Where(f => !string.Equals(f.Asesor, "OFICINA", StringComparison.OrdinalIgnoreCase)
                         && ImpTotal(f, mon) > 0)
                .Select(f => ToClienteDto(f, mon))
                .OrderBy(x => x.Asesor).ThenByDescending(x => x.Total)
                .ToList();

            // ── 2. Top N clientes por Asesor (Importe y KG) ─────────────────────
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
                        Total       = imp,
                    };
                })
                .Where(x => x.Total > 0)
                .OrderByDescending(x => x.Total)
                .ToList();
        }

            }
        }
