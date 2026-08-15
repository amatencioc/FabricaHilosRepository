using System.Data;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using FabricaHilos.Models.Ventas;

namespace FabricaHilos.Services.Ventas
{
    public interface IReporteReclamosService
    {
        Task<List<ReclamoMesDto>> ObtenerPorMesAsync(DateTime fechaIni, DateTime fechaFin, string? cliente = null, string? vendedor = null, string? estado = null);
        Task<List<ReclamoFamiliaDto>> ObtenerPorFamiliaAsync(DateTime fechaIni, DateTime fechaFin, string? cliente = null, string? vendedor = null, string? estado = null);
        Task<List<ReclamoClienteDto>> ObtenerPorClienteAsync(DateTime fechaIni, DateTime fechaFin, string? cliente = null, string? vendedor = null, string? estado = null);
        Task<ReclamoIndicadoresDto> ObtenerIndicadoresAsync(DateTime fechaIni, DateTime fechaFin, decimal? kgAtendidos = null, string? cliente = null, string? vendedor = null, string? estado = null);
        Task<List<ReclamoMotivoDto>> ObtenerMotivosAsync(DateTime fechaIni, DateTime fechaFin, string? cliente = null, string? vendedor = null, string? estado = null);
        Task<List<ReclamoListadoDto>> ObtenerListadoAsync(DateTime fechaIni, DateTime fechaFin, string? cliente, string? vendedor, string? estado);
        Task<List<ReclamoComboItemDto>> ObtenerParametrosComboAsync(string tipo, DateTime? fechaIni = null, DateTime? fechaFin = null);
    }

    public class ReporteReclamosService : OracleServiceBase, IReporteReclamosService
    {
        private readonly ILogger<ReporteReclamosService> _logger;

        public ReporteReclamosService(
            IConfiguration configuration,
            ILogger<ReporteReclamosService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(configuration, httpContextAccessor)
        {
            _logger = logger;
        }

        private static string? GetStr(OracleDataReader r, string col) =>
            r[col] == DBNull.Value ? null : r[col]?.ToString();

        private static decimal GetDec(OracleDataReader r, string col) =>
            r[col] == DBNull.Value ? 0m : Convert.ToDecimal(r[col]);

        private static int GetInt(OracleDataReader r, string col) =>
            r[col] == DBNull.Value ? 0 : Convert.ToInt32(r[col]);

        private static int? GetIntNullable(OracleDataReader r, string col) =>
            r[col] == DBNull.Value ? null : Convert.ToInt32(r[col]);

        private static DateTime? GetDateNullable(OracleDataReader r, string col) =>
            r[col] == DBNull.Value ? null : Convert.ToDateTime(r[col]);

        public async Task<List<ReclamoMesDto>> ObtenerPorMesAsync(DateTime fechaIni, DateTime fechaFin, string? cliente = null, string? vendedor = null, string? estado = null)
        {
            var result = new List<ReclamoMesDto>();
            try
            {
                using var conn = await AbrirConexionAsync();
                using var cmd = new OracleCommand($"{S}PKG_RECLAMO_DASH.SP_RECLAMOS_POR_MES", conn)
                {
                    CommandType = CommandType.StoredProcedure,
                    BindByName = true
                };
                cmd.Parameters.Add("p_fecha_ini", OracleDbType.Date).Value = fechaIni.Date;
                cmd.Parameters.Add("p_fecha_fin", OracleDbType.Date).Value = fechaFin.Date;
                cmd.Parameters.Add("p_cliente", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(cliente) ? "%" : cliente;
                cmd.Parameters.Add("p_vendedor", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(vendedor) ? "%" : vendedor;
                cmd.Parameters.Add("p_estado", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(estado) ? "%" : estado;
                cmd.Parameters.Add("p_cursor", OracleDbType.RefCursor, ParameterDirection.Output);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new ReclamoMesDto
                    {
                        Periodo = GetStr(reader, "PERIODO"),
                        Cantidad = GetInt(reader, "CANTIDAD")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener Reclamos por Mes");
            }
            return result;
        }

        public async Task<List<ReclamoFamiliaDto>> ObtenerPorFamiliaAsync(DateTime fechaIni, DateTime fechaFin, string? cliente = null, string? vendedor = null, string? estado = null)
        {
            var result = new List<ReclamoFamiliaDto>();
            try
            {
                using var conn = await AbrirConexionAsync();
                using var cmd = new OracleCommand($"{S}PKG_RECLAMO_DASH.SP_RECLAMOS_POR_FAMILIA", conn)
                {
                    CommandType = CommandType.StoredProcedure,
                    BindByName = true
                };
                cmd.Parameters.Add("p_fecha_ini", OracleDbType.Date).Value = fechaIni.Date;
                cmd.Parameters.Add("p_fecha_fin", OracleDbType.Date).Value = fechaFin.Date;
                cmd.Parameters.Add("p_cliente", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(cliente) ? "%" : cliente;
                cmd.Parameters.Add("p_vendedor", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(vendedor) ? "%" : vendedor;
                cmd.Parameters.Add("p_estado", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(estado) ? "%" : estado;
                cmd.Parameters.Add("p_cursor", OracleDbType.RefCursor, ParameterDirection.Output);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new ReclamoFamiliaDto
                    {
                        CodFamilia = GetStr(reader, "COD_FAMILIA"),
                        Cantidad = GetInt(reader, "CANTIDAD")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener Reclamos por Familia");
            }
            return result;
        }

        public async Task<List<ReclamoClienteDto>> ObtenerPorClienteAsync(DateTime fechaIni, DateTime fechaFin, string? cliente = null, string? vendedor = null, string? estado = null)
        {
            var result = new List<ReclamoClienteDto>();
            try
            {
                using var conn = await AbrirConexionAsync();
                using var cmd = new OracleCommand($"{S}PKG_RECLAMO_DASH.SP_RECLAMOS_POR_CLIENTE", conn)
                {
                    CommandType = CommandType.StoredProcedure,
                    BindByName = true
                };
                cmd.Parameters.Add("p_fecha_ini", OracleDbType.Date).Value = fechaIni.Date;
                cmd.Parameters.Add("p_fecha_fin", OracleDbType.Date).Value = fechaFin.Date;
                cmd.Parameters.Add("p_cliente", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(cliente) ? "%" : cliente;
                cmd.Parameters.Add("p_vendedor", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(vendedor) ? "%" : vendedor;
                cmd.Parameters.Add("p_estado", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(estado) ? "%" : estado;
                cmd.Parameters.Add("p_cursor", OracleDbType.RefCursor, ParameterDirection.Output);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new ReclamoClienteDto
                    {
                        CodCliente = GetStr(reader, "COD_CLIENTE"),
                        NombreCliente = GetStr(reader, "NOMBRE_CLIENTE"),
                        Cantidad = GetInt(reader, "CANTIDAD"),
                        KgReclamados = GetDec(reader, "KG_RECLAMADOS")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener Reclamos por Cliente");
            }
            return result;
        }

        /// <summary>
        /// SP_INDICADORES_DASH devuelve un RECORD PL/SQL por OUT, no soportado directamente por ODP.NET.
        /// Se envuelve la llamada dentro de un bloque anónimo PL/SQL que expone los campos como
        /// parámetros escalares de salida.
        /// </summary>
        public async Task<ReclamoIndicadoresDto> ObtenerIndicadoresAsync(DateTime fechaIni, DateTime fechaFin, decimal? kgAtendidos = null, string? cliente = null, string? vendedor = null, string? estado = null)
        {
            var dto = new ReclamoIndicadoresDto();
            try
            {
                using var conn = await AbrirConexionAsync();
                var sql = $@"
DECLARE
    v_ind {S}PKG_RECLAMO_DASH.REC_INDICADOR;
BEGIN
    {S}PKG_RECLAMO_DASH.SP_INDICADORES_DASH(
        p_fecha_ini => :p_fecha_ini,
        p_fecha_fin => :p_fecha_fin,
        p_ind => v_ind,
        p_kg_atendidos => :p_kg_atendidos,
        p_cliente => :p_cliente,
        p_vendedor => :p_vendedor,
        p_estado => :p_estado);
    :o_total_reclamos      := v_ind.total_reclamos;
    :o_total_kg            := v_ind.total_kg_reclamados;
    :o_lead_time           := v_ind.lead_time_promedio;
    :o_pct_reclamos        := v_ind.pct_reclamos;
    :o_pct_reposicion      := v_ind.pct_reposicion;
    :o_pct_reproceso       := v_ind.pct_reproceso;
    :o_reclamos_pendientes := v_ind.reclamos_pendientes;
    :o_reclamos_en_proceso := v_ind.reclamos_en_proceso;
END;";
                using var cmd = new OracleCommand(sql, conn) { BindByName = true };
                cmd.Parameters.Add("p_fecha_ini", OracleDbType.Date).Value = fechaIni.Date;
                cmd.Parameters.Add("p_fecha_fin", OracleDbType.Date).Value = fechaFin.Date;
                cmd.Parameters.Add("p_kg_atendidos", OracleDbType.Decimal).Value = kgAtendidos.HasValue ? kgAtendidos.Value : (object)DBNull.Value;
                cmd.Parameters.Add("p_cliente", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(cliente) ? "%" : cliente;
                cmd.Parameters.Add("p_vendedor", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(vendedor) ? "%" : vendedor;
                cmd.Parameters.Add("p_estado", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(estado) ? "%" : estado;
                var oTotal = cmd.Parameters.Add("o_total_reclamos", OracleDbType.Int32, ParameterDirection.Output);
                var oKg = cmd.Parameters.Add("o_total_kg", OracleDbType.Decimal, ParameterDirection.Output);
                var oLead = cmd.Parameters.Add("o_lead_time", OracleDbType.Decimal, ParameterDirection.Output);
                var oPctRec = cmd.Parameters.Add("o_pct_reclamos", OracleDbType.Decimal, ParameterDirection.Output);
                var oPctRepo = cmd.Parameters.Add("o_pct_reposicion", OracleDbType.Decimal, ParameterDirection.Output);
                var oPctRepro = cmd.Parameters.Add("o_pct_reproceso", OracleDbType.Decimal, ParameterDirection.Output);
                var oPend = cmd.Parameters.Add("o_reclamos_pendientes", OracleDbType.Int32, ParameterDirection.Output);
                var oProc = cmd.Parameters.Add("o_reclamos_en_proceso", OracleDbType.Int32, ParameterDirection.Output);

                await cmd.ExecuteNonQueryAsync();

                static decimal ToDecimalSafe(OracleParameter p)
                {
                    var dec = (OracleDecimal)p.Value;
                    return dec.IsNull ? 0m : Convert.ToDecimal(dec.Value);
                }

                static decimal? ToDecimalNullable(OracleParameter p)
                {
                    var dec = (OracleDecimal)p.Value;
                    return dec.IsNull ? (decimal?)null : Convert.ToDecimal(dec.Value);
                }

                static int ToInt32Safe(OracleParameter p)
                {
                    var dec = (OracleDecimal)p.Value;
                    return dec.IsNull ? 0 : Convert.ToInt32(dec.Value);
                }

                dto.TotalReclamos = ToInt32Safe(oTotal);
                dto.TotalKgReclamados = ToDecimalSafe(oKg);
                dto.LeadTimePromedio = ToDecimalSafe(oLead);
                dto.PctReclamos = ToDecimalNullable(oPctRec);
                dto.PctReposicion = ToDecimalSafe(oPctRepo);
                dto.PctReproceso = ToDecimalSafe(oPctRepro);
                dto.ReclamosPendientes = ToInt32Safe(oPend);
                dto.ReclamosEnProceso = ToInt32Safe(oProc);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener Indicadores de Reclamos");
            }

            return dto;
        }

        public async Task<List<ReclamoMotivoDto>> ObtenerMotivosAsync(DateTime fechaIni, DateTime fechaFin, string? cliente = null, string? vendedor = null, string? estado = null)
        {
            var result = new List<ReclamoMotivoDto>();
            try
            {
                using var conn = await AbrirConexionAsync();
                using var cmd = new OracleCommand($"{S}PKG_RECLAMO_DASH.SP_MOTIVOS_RECLAMOS", conn)
                {
                    CommandType = CommandType.StoredProcedure,
                    BindByName = true
                };
                cmd.Parameters.Add("p_fecha_ini", OracleDbType.Date).Value = fechaIni.Date;
                cmd.Parameters.Add("p_fecha_fin", OracleDbType.Date).Value = fechaFin.Date;
                cmd.Parameters.Add("p_cliente", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(cliente) ? "%" : cliente;
                cmd.Parameters.Add("p_vendedor", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(vendedor) ? "%" : vendedor;
                cmd.Parameters.Add("p_estado", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(estado) ? "%" : estado;
                cmd.Parameters.Add("p_cursor", OracleDbType.RefCursor, ParameterDirection.Output);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new ReclamoMotivoDto
                    {
                        Problema = GetStr(reader, "PROBLEMA"),
                        Motivo = GetStr(reader, "MOTIVO"),
                        Cantidad = GetInt(reader, "CANTIDAD"),
                        Porcentaje = GetDec(reader, "PORCENTAJE")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener Motivos de Reclamos");
            }
            return result;
        }

        public async Task<List<ReclamoListadoDto>> ObtenerListadoAsync(DateTime fechaIni, DateTime fechaFin, string? cliente, string? vendedor, string? estado)
        {
            var result = new List<ReclamoListadoDto>();
            try
            {
                using var conn = await AbrirConexionAsync();
                using var cmd = new OracleCommand($"{S}PKG_RECLAMO_DASH.SP_LISTADO_RECLAMOS", conn)
                {
                    CommandType = CommandType.StoredProcedure,
                    BindByName = true
                };
                cmd.Parameters.Add("p_fecha_ini", OracleDbType.Date).Value = fechaIni.Date;
                cmd.Parameters.Add("p_fecha_fin", OracleDbType.Date).Value = fechaFin.Date;
                cmd.Parameters.Add("p_cliente", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(cliente) ? "%" : cliente;
                cmd.Parameters.Add("p_vendedor", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(vendedor) ? "%" : vendedor;
                cmd.Parameters.Add("p_estado", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(estado) ? "%" : estado;
                cmd.Parameters.Add("p_cursor", OracleDbType.RefCursor, ParameterDirection.Output);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new ReclamoListadoDto
                    {
                        Nrorec = GetInt(reader, "NROREC"),
                        Fecrec = GetDateNullable(reader, "FECREC"),
                        Codcli = GetStr(reader, "CODCLI"),
                        Descli = GetStr(reader, "DESCLI"),
                        Desven = GetStr(reader, "DESVEN"),
                        Codart = GetStr(reader, "CODART"),
                        Desart = GetStr(reader, "DESART"),
                        Cantidad = GetDec(reader, "CANTIDAD"),
                        Motivo = GetStr(reader, "MOTIVO"),
                        Procede = GetStr(reader, "PROCEDE"),
                        AtencionCli = GetStr(reader, "ATENCION_CLI"),
                        EstadoDesc = GetStr(reader, "ESTADO_DESC"),
                        LeadTime = GetIntNullable(reader, "LEAD_TIME")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener Listado de Reclamos");
            }
            return result;
        }

        public async Task<List<ReclamoComboItemDto>> ObtenerParametrosComboAsync(string tipo, DateTime? fechaIni = null, DateTime? fechaFin = null)
        {
            var result = new List<ReclamoComboItemDto>();
            try
            {
                using var conn = await AbrirConexionAsync();
                using var cmd = new OracleCommand($"{S}PKG_RECLAMO_DASH.SP_GET_PARAMETROS_COMBO", conn)
                {
                    CommandType = CommandType.StoredProcedure,
                    BindByName = true
                };
                cmd.Parameters.Add("p_tipo", OracleDbType.Varchar2).Value = tipo;
                var pFechaIni = cmd.Parameters.Add("p_fecha_ini", OracleDbType.Date);
                pFechaIni.Value = fechaIni.HasValue ? fechaIni.Value.Date : (object)DBNull.Value;
                var pFechaFin = cmd.Parameters.Add("p_fecha_fin", OracleDbType.Date);
                pFechaFin.Value = fechaFin.HasValue ? fechaFin.Value.Date : (object)DBNull.Value;
                cmd.Parameters.Add("p_cursor", OracleDbType.RefCursor, ParameterDirection.Output);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new ReclamoComboItemDto
                    {
                        Codigo = GetStr(reader, "CODIGO"),
                        Descripcion = GetStr(reader, "DESCRIPCION")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener Parámetros de Combo ({Tipo})", tipo);
            }
            return result;
        }
    }
}
