using Oracle.ManagedDataAccess.Client;
using System.Data;
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

        // ── Helpers ──────────────────────────────────────────────────────────
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

        private static string[] GetColumnNames(OracleDataReader r)
        {
            var names = new string[r.FieldCount];
            for (int i = 0; i < r.FieldCount; i++)
                names[i] = r.GetName(i);
            return names;
        }

        // ── Fila cruda del paquete PKG_VEND_GRUPO_MAESTROCLIENTE (cabecera) ───
        private sealed class FilaCabecera
        {
            public string? Asesor   { get; set; }   // DESCRIPCION
            public string? Mes      { get; set; }   // PERIODO  (YYYY/MM)
            public decimal TotUnid  { get; set; }   // TOTUNID
            public decimal Monto    { get; set; }   // MONTO (neto, en la moneda P_MON)
        }

        // ── Llama a PKG_VEND_GRUPO_MAESTROCLIENTE.SP_REPORTE con P_TIPO='C' ──
        private async Task<List<FilaCabecera>> CargarCabeceraAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda)
        {
            var connStr = GetOracleConnectionString();
            var filas   = new List<FilaCabecera>();
            if (string.IsNullOrEmpty(connStr)) return filas;

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();

                using var cmd = new OracleCommand($"{S}PKG_VEND_GRUPO_MAESTROCLIENTE.SP_REPORTE", conn)
                {
                    CommandType = CommandType.StoredProcedure,
                    BindByName  = true
                };

                cmd.Parameters.Add("P_FECHA1", OracleDbType.Date).Value        = fechaInicio.Date;
                cmd.Parameters.Add("P_FECHA2", OracleDbType.Date).Value        = fechaFin.Date;
                cmd.Parameters.Add("P_MON",    OracleDbType.Varchar2).Value    = moneda;
                cmd.Parameters.Add("P_TIPO",   OracleDbType.Char).Value        = "C";
                cmd.Parameters.Add("P_CURSOR", OracleDbType.RefCursor).Direction = ParameterDirection.Output;

                using var reader = await cmd.ExecuteReaderAsync();
                bool columnNamesLogged = false;
                while (await reader.ReadAsync())
                {
                    if (!columnNamesLogged)
                    {
                        var cols = GetColumnNames(reader);
                        _logger.LogInformation("[ICM] Columnas recibidas del paquete: {Cols}", string.Join(", ", cols));
                        columnNamesLogged = true;
                    }
                    filas.Add(new FilaCabecera
                    {
                        Asesor  = GetStr(reader, "DESCRIPCION"),
                        Mes     = GetStr(reader, "PERIODO"),
                        TotUnid = GetDec(reader, "TOTUNID"),
                        Monto   = GetDec(reader, "MONTO"),
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ICM] Error al ejecutar PKG_VEND_GRUPO_MAESTROCLIENTE.SP_REPORTE");
            }

            _logger.LogDebug("[ICM] Filas cabecera: {N} | {F1:dd/MM/yyyy}-{F2:dd/MM/yyyy} | {Mon}",
                filas.Count, fechaInicio, fechaFin, moneda);
            return filas;
        }

        // ════════════════════════════════════════════════════════════════════════
        // ObtenerTodosAsync — un solo viaje Oracle, importe + KG
        // ════════════════════════════════════════════════════════════════════════
        public async Task<IcmTodosDto> ObtenerTodosAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda)
        {
            var mon   = string.IsNullOrEmpty(moneda) ? "D" : moneda.ToUpperInvariant();
            var filas = await CargarCabeceraAsync(fechaInicio, fechaFin, mon);

            var importe = filas
                .Select(f => new IcmImporteAsesorMesDto
                {
                    Asesor  = f.Asesor ?? "Sin Asesor",
                    Mes     = f.Mes    ?? "",
                    Importe = f.Monto,
                })
                .OrderBy(x => x.Asesor).ThenBy(x => x.Mes)
                .ToList();

            var kg = filas
                .Select(f => new IcmKgAsesorMesDto
                {
                    Asesor     = f.Asesor ?? "Sin Asesor",
                    Mes        = f.Mes    ?? "",
                    CantidadKg = f.TotUnid,
                })
                .OrderBy(x => x.Asesor).ThenBy(x => x.Mes)
                .ToList();

            return new IcmTodosDto { Importe = importe, Kg = kg, Clientes = new() };
        }

        public async Task<List<IcmImporteAsesorMesDto>> ObtenerImportePorAsesorMesAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda)
        {
            var result = await ObtenerTodosAsync(fechaInicio, fechaFin, moneda);
            return result.Importe;
        }

        public async Task<List<IcmKgAsesorMesDto>> ObtenerKgPorAsesorMesAsync(
            DateTime fechaInicio, DateTime fechaFin)
        {
            var result = await ObtenerTodosAsync(fechaInicio, fechaFin, "D");
            return result.Kg;
        }

        public Task<List<IcmClientesAsesorMesDto>> ObtenerClientesPorAsesorMesAsync(
            DateTime fechaInicio, DateTime fechaFin)
        {
            return Task.FromResult(new List<IcmClientesAsesorMesDto>());
        }
    }
}
