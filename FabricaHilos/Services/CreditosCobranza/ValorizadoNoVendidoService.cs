using System.Data;
using Oracle.ManagedDataAccess.Client;
using FabricaHilos.Models.CreditosCobranza;

namespace FabricaHilos.Services.CreditosCobranza;

public class ValorizadoNoVendidoService : OracleServiceBase, IValorizadoNoVendidoService
{
    private readonly ILogger<ValorizadoNoVendidoService> _logger;

    public ValorizadoNoVendidoService(
        IConfiguration configuration,
        ILogger<ValorizadoNoVendidoService> logger,
        IHttpContextAccessor httpContextAccessor)
        : base(configuration, httpContextAccessor)
    {
        _logger = logger;
    }

    public async Task<List<ValorizadoNoVendidoDto>> ObtenerValorizadoNoVendidoAsync(DateTime fechaInicio, DateTime fechaFin)
    {
        var result  = new List<ValorizadoNoVendidoDto>();
        var connStr = GetOracleConnectionString();
        if (string.IsNullOrEmpty(connStr)) return result;

        try
        {
            // El paquete devuelve 12 filas (una por mes) para el a\u00f1o indicado en p_anio,
            // por eso se invoca una vez por cada a\u00f1o presente en el rango solicitado.
            var anios = Enumerable.Range(fechaInicio.Year, fechaFin.Year - fechaInicio.Year + 1);

            await using var conn = new OracleConnection(connStr);
            await conn.OpenAsync();

            foreach (var anio in anios)
            {
                await using var cmd = new OracleCommand($"{S}PKG_NO_FACTURADO.SP_VALORIZADO_ANUAL", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.BindByName  = true;
                cmd.Parameters.Add(new OracleParameter("p_anio",          OracleDbType.Int32) { Value = anio });
                cmd.Parameters.Add(new OracleParameter("p_can_toneladas", OracleDbType.Decimal) { Value = 120 });
                cmd.Parameters.Add(new OracleParameter("p_cursor",        OracleDbType.RefCursor, ParameterDirection.Output));

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var mes = Convert.ToInt32(reader["MES"]);

                    // Filtrar meses fuera del rango solicitado
                    var fecha = new DateTime(anio, mes, 1);
                    if (fecha < new DateTime(fechaInicio.Year, fechaInicio.Month, 1)) continue;
                    if (fecha > new DateTime(fechaFin.Year, fechaFin.Month, 1)) continue;

                    result.Add(new ValorizadoNoVendidoDto
                    {
                        Ano                  = anio,
                        Mes                  = mes,
                        NombreMes            = reader["NOMBRE_MES"] == DBNull.Value ? string.Empty : Convert.ToString(reader["NOMBRE_MES"]) ?? string.Empty,
                        KgVendidos           = reader["KG_VENDIDOS"]           == DBNull.Value ? 0m : Convert.ToDecimal(reader["KG_VENDIDOS"]),
                        Valorizado           = reader["VALORIZADO"]           == DBNull.Value ? 0m : Convert.ToDecimal(reader["VALORIZADO"]),
                        Promedio             = reader["PROMEDIO"]             == DBNull.Value ? 0m : Convert.ToDecimal(reader["PROMEDIO"]),
                        CanToneladasKg       = reader["CAN_TONELADAS_KG"]     == DBNull.Value ? 0m : Convert.ToDecimal(reader["CAN_TONELADAS_KG"]),
                        DiferenciaKg         = reader["DIFERENCIA_KG"]        == DBNull.Value ? 0m : Convert.ToDecimal(reader["DIFERENCIA_KG"]),
                        ValorizadoNoVendido  = reader["VALORIZADO_NO_VENDIDO"] == DBNull.Value ? 0m : Convert.ToDecimal(reader["VALORIZADO_NO_VENDIDO"]),
                    });
                }
            }

            result = result.OrderBy(x => x.Ano).ThenBy(x => x.Mes).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener valorizado no vendido");
        }

        return result;
    }
}
