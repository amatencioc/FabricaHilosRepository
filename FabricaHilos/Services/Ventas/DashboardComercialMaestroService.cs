using Oracle.ManagedDataAccess.Client;
using System.Data;
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

        // ── Helpers de lectura ─────────────────────────────────────────────────
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

        // ── Fila cabecera (P_TIPO='C'): DESCRIPCION, PERIODO, TOTUNID, MONTO ──
        private sealed class FilaCabeceraRaw
        {
            public string? Asesor  { get; set; }
            public decimal TotUnid { get; set; }
            public decimal Monto   { get; set; }
        }

        // ── Fila detalle (P_TIPO='D'): por cliente/mes ─────────────────────────
        private sealed class FilaDetalleRaw
        {
            public string? CodCliente { get; set; }
            public string? Nombre     { get; set; }
            public string? Ruc        { get; set; }
            public string? Giro       { get; set; }
            public string? Asesor     { get; set; }
            public decimal TotUnid    { get; set; }
            public decimal Monto      { get; set; }
        }

        // ── Llama al paquete con el P_TIPO indicado ────────────────────────────
        private async Task<List<T>> EjecutarPaqueteAsync<T>(
            DateTime fechaInicio, DateTime fechaFin, string moneda, string tipo,
            Func<OracleDataReader, T> mapper)
        {
            var connStr = GetOracleConnectionString();
            var filas   = new List<T>();
            if (string.IsNullOrEmpty(connStr)) return filas;

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();

                using var cmd = new OracleCommand(
                    $"{S}PKG_VEND_GRUPO_MAESTROCLIENTE.SP_REPORTE", conn)
                {
                    CommandType = CommandType.StoredProcedure,
                    BindByName  = true
                };

                cmd.Parameters.Add("P_FECHA1", OracleDbType.Date).Value      = fechaInicio.Date;
                cmd.Parameters.Add("P_FECHA2", OracleDbType.Date).Value      = fechaFin.Date;
                cmd.Parameters.Add("P_MON",    OracleDbType.Varchar2).Value  = moneda;
                cmd.Parameters.Add("P_TIPO",   OracleDbType.Char).Value      = tipo;
                cmd.Parameters.Add("P_CURSOR", OracleDbType.RefCursor).Direction = ParameterDirection.Output;

                using var reader = await cmd.ExecuteReaderAsync();
                bool logged = false;
                while (await reader.ReadAsync())
                {
                    if (!logged)
                    {
                        var cols = new string[reader.FieldCount];
                        for (int i = 0; i < reader.FieldCount; i++) cols[i] = reader.GetName(i);
                        _logger.LogInformation("[DCM] P_TIPO={Tipo} columnas: {Cols}", tipo, string.Join(", ", cols));
                        logged = true;
                    }
                    filas.Add(mapper(reader));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DCM] Error PKG_VEND_GRUPO_MAESTROCLIENTE.SP_REPORTE P_TIPO={Tipo}", tipo);
            }

            _logger.LogInformation("[DCM] P_TIPO={Tipo} filas={N} | {F1:dd/MM/yyyy}-{F2:dd/MM/yyyy} | {Mon}",
                tipo, filas.Count, fechaInicio, fechaFin, moneda);
            return filas;
        }

        // ── Proyectar al DTO de cliente maestro ────────────────────────────────
        private static DcmClienteMaestroDto ToClienteDto(
            string asesor, string? codCliente, string? ruc, string? nombre, string? giro,
            decimal monto, decimal totUnid)
        {
            return new DcmClienteMaestroDto
            {
                Asesor      = asesor,
                CodCliente  = codCliente,
                Ruc         = ruc,
                RazonSocial = nombre,
                Giro        = string.IsNullOrEmpty(giro) ? "SIN GIRO" : giro,
                CantidadKg  = totUnid,
                Importe     = monto,
                Total       = monto,
            };
        }

        // ════════════════════════════════════════════════════════════════════════
        // ObtenerDashboardAsync
        //   · Cabecera (P_TIPO='C') → gráfico "Cartera por Asesor"
        //   · Detalle  (P_TIPO='D') → gráficos "Top N Clientes por Asesor"
        // ════════════════════════════════════════════════════════════════════════
        public async Task<DcmDashboardDto> ObtenerDashboardAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda, int top = 3)
        {
            var mon = string.IsNullOrEmpty(moneda) ? "D" : moneda.ToUpperInvariant();
            var dto = new DcmDashboardDto();

            // Ambas llamadas en paralelo para no duplicar el tiempo de espera
            var taskCab = EjecutarPaqueteAsync(
                fechaInicio, fechaFin, mon, "C",
                r => new FilaCabeceraRaw
                {
                    Asesor  = GetStr(r, "DESCRIPCION"),
                    TotUnid = GetDec(r, "TOTUNID"),
                    Monto   = GetDec(r, "MONTO"),
                });

            var taskDet = EjecutarPaqueteAsync(
                fechaInicio, fechaFin, mon, "D",
                r => new FilaDetalleRaw
                {
                    Asesor     = GetStr(r, "DESCRIPCION"),
                    CodCliente = GetStr(r, "COD_CLIENTE"),
                    Nombre     = GetStr(r, "NOMBRE"),
                    Ruc        = GetStr(r, "RUC"),
                    Giro       = GetStr(r, "GIRO"),
                    TotUnid    = GetDec(r, "TOTUNID"),
                    Monto      = GetDec(r, "MONTO"),
                });

            await Task.WhenAll(taskCab, taskDet);

            var filasCab = taskCab.Result;
            var filasDet = taskDet.Result;

            // ── 1. Cartera completa de clientes por Asesor (para la grilla y el export) ──
            // Se construye desde filasDet para conservar Ruc, RazonSocial, CantidadKg e Importe
            // por cada combinación (Asesor, Cliente).
            var consolidado = filasDet
                .GroupBy(f => new { f.Asesor, f.CodCliente, f.Nombre, f.Ruc, f.Giro })
                .Select(g => new FilaDetalleRaw
                {
                    Asesor     = g.Key.Asesor,
                    CodCliente = g.Key.CodCliente,
                    Nombre     = g.Key.Nombre,
                    Ruc        = g.Key.Ruc,
                    Giro       = g.Key.Giro,
                    TotUnid    = g.Sum(f => f.TotUnid),
                    Monto      = g.Sum(f => f.Monto),
                })
                .ToList();

            dto.ClientesTodos = consolidado
                .Select(f => new DcmClienteMaestroDto
                {
                    Asesor      = f.Asesor ?? "Sin Asesor",
                    Ruc         = f.Ruc,
                    RazonSocial = f.Nombre,
                    CantidadKg  = f.TotUnid,
                    Importe     = f.Monto,
                    Total       = f.Monto,
                })
                .OrderBy(x => x.Asesor)
                .ThenByDescending(x => x.Importe)
                .ToList();

            // ── 2. Top N clientes por Asesor — a nivel cliente (detalle) ────────

            // ── 3. Conteo de clientes distintos por asesor (pie chart) ───────────
            // Solo clientes con ventas reales (igual que la grilla: Total > 0)
            dto.ClientesPorAsesor = consolidado
                .Where(f => f.Monto > 0)
                .GroupBy(f => f.Asesor ?? "Sin Asesor")
                .Select(g => new DcmClientesCountDto
                {
                    Asesor        = g.Key,
                    TotalClientes = g.Count(),
                })
                .OrderBy(x => x.Asesor)
                .ToList();

            // ── 4. Ventas por asesor (ranking + participación) ───────────────────
            // Importe desde detalle; KG desde cabecera (P_TIPO='C') para
            // coincidir con el Indicador Comercial Maestro (mismo paquete, mismo tipo).
            var kgPorAsesor = filasCab
                .GroupBy(f => f.Asesor ?? "Sin Asesor")
                .ToDictionary(g => g.Key, g => g.Sum(f => f.TotUnid));

            dto.VentasPorAsesor = consolidado
                .GroupBy(f => f.Asesor ?? "Sin Asesor")
                .Select(g => new DcmVentaAsesorDto
                {
                    Asesor     = g.Key,
                    Importe    = g.Sum(f => f.Monto),
                    CantidadKg = kgPorAsesor.TryGetValue(g.Key, out var kg) ? kg : 0m,
                })
                .OrderBy(x => x.Asesor)
                .ToList();

            var topImp = consolidado
                .Where(f => f.Monto > 0)
                .GroupBy(f => f.Asesor)
                .SelectMany(g => g.OrderByDescending(f => f.Monto).Take(top)
                    .Select(f => new DcmTopClienteAsesorDto
                    {
                        Asesor      = f.Asesor,
                        CodCliente  = f.CodCliente,
                        RazonSocial = f.Nombre,
                        CantidadKg  = f.TotUnid,
                        Importe     = f.Monto,
                        TopType     = "importe",
                    }))
                .ToList();

            var topKg = consolidado
                .Where(f => f.TotUnid > 0)
                .GroupBy(f => f.Asesor)
                .SelectMany(g => g.OrderByDescending(f => f.TotUnid).Take(top)
                    .Select(f => new DcmTopClienteAsesorDto
                    {
                        Asesor      = f.Asesor,
                        CodCliente  = f.CodCliente,
                        RazonSocial = f.Nombre,
                        CantidadKg  = f.TotUnid,
                        Importe     = f.Monto,
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
        // ObtenerClientesPorAsesorAsync — detalle clientes (P_TIPO='D')
        // ════════════════════════════════════════════════════════════════════════
        public async Task<List<DcmClienteMaestroDto>> ObtenerClientesPorAsesorAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda, string asesor)
        {
            var mon = string.IsNullOrEmpty(moneda) ? "D" : moneda.ToUpperInvariant();

            var filas = await EjecutarPaqueteAsync(
                fechaInicio, fechaFin, mon, "D",
                r => new FilaDetalleRaw
                {
                    Asesor     = GetStr(r, "DESCRIPCION"),
                    CodCliente = GetStr(r, "COD_CLIENTE"),
                    Nombre     = GetStr(r, "NOMBRE"),
                    Ruc        = GetStr(r, "RUC"),
                    Giro       = GetStr(r, "GIRO"),
                    TotUnid    = GetDec(r, "TOTUNID"),
                    Monto      = GetDec(r, "MONTO"),
                });

            return filas
                .Where(f => string.Equals(f.Asesor, asesor, StringComparison.OrdinalIgnoreCase))
                .GroupBy(f => new { f.CodCliente, f.Ruc, f.Nombre, f.Giro })
                .Select(g => ToClienteDto(asesor, g.Key.CodCliente, g.Key.Ruc,
                                          g.Key.Nombre, g.Key.Giro,
                                          g.Sum(f => f.Monto), g.Sum(f => f.TotUnid)))
                .Where(x => x.Total > 0)
                .OrderByDescending(x => x.Total)
                .ToList();
        }
    }
}
