using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using FabricaHilos.Models.Ventas;

namespace FabricaHilos.Services.Ventas
{
    public class DashboardGerencialService : OracleServiceBase, IDashboardGerencialService
    {
        private readonly ILogger<DashboardGerencialService> _logger;

        public DashboardGerencialService(
            IConfiguration configuration,
            ILogger<DashboardGerencialService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(configuration, httpContextAccessor)
        {
            _logger = logger;
        }

        private static string? GetStr(OracleDataReader r, string col) =>
            r[col] == DBNull.Value ? null : r[col]?.ToString();

        private static decimal GetDec(OracleDataReader r, string col)
        {
            var ordinal = r.GetOrdinal(col);
            if (r.IsDBNull(ordinal)) return 0m;
            var oraVal = r.GetOracleDecimal(ordinal);
            // Truncar a la precisión de .NET decimal (28 dígitos) para evitar OverflowException
            oraVal = OracleDecimal.SetPrecision(oraVal, 28);
            return oraVal.Value;
        }

        // ─────────────────────────────────────────────────────────
        // Ventas agrupadas por Mercado: Perú / Latam / Global
        // ─────────────────────────────────────────────────────────
        public async Task<List<DgVentaMercadoDto>> ObtenerVentasPorMercadoAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DgVentaMercadoDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(
                    $"{S}PKG_VEND_GRUPO_MAESTROCLIENTE.SP_VENTAS_POR_MERCADO", conn)
                {
                    CommandType = System.Data.CommandType.StoredProcedure,
                    BindByName  = true
                };
                cmd.Parameters.Add("P_FECHA1", OracleDbType.Date).Value          = fechaInicio.Date;
                cmd.Parameters.Add("P_FECHA2", OracleDbType.Date).Value          = fechaFin.Date;
                cmd.Parameters.Add("P_MON",    OracleDbType.Varchar2).Value      = string.IsNullOrEmpty(moneda) ? "D" : moneda.ToUpperInvariant();
                cmd.Parameters.Add("P_CURSOR", OracleDbType.RefCursor).Direction = System.Data.ParameterDirection.Output;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new DgVentaMercadoDto
                    {
                        Mercado = GetStr(reader, "MERCADO"),
                        Importe = GetDec(reader, "IMPORTE")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener Ventas por Mercado (Dashboard Gerencial)");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Detalle por País (drill-down desde el donut)
        // ─────────────────────────────────────────────────────────
        public async Task<List<DgVentaMercadoPaisDto>> ObtenerDetallePorPaisAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda, string? mercado)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DgVentaMercadoPaisDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(
                    $"{S}PKG_VEND_GRUPO_MAESTROCLIENTE.SP_DETALLE_POR_PAIS", conn)
                {
                    CommandType = System.Data.CommandType.StoredProcedure,
                    BindByName  = true
                };
                cmd.Parameters.Add("P_FECHA1",  OracleDbType.Date).Value          = fechaInicio.Date;
                cmd.Parameters.Add("P_FECHA2",  OracleDbType.Date).Value          = fechaFin.Date;
                cmd.Parameters.Add("P_MON",     OracleDbType.Varchar2).Value      = string.IsNullOrEmpty(moneda) ? "D" : moneda.ToUpperInvariant();
                cmd.Parameters.Add("P_MERCADO", OracleDbType.Varchar2).Value      = string.IsNullOrEmpty(mercado) ? (object)DBNull.Value : mercado;
                cmd.Parameters.Add("P_CURSOR",  OracleDbType.RefCursor).Direction = System.Data.ParameterDirection.Output;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new DgVentaMercadoPaisDto
                    {
                        Mercado    = GetStr(reader, "MERCADO"),
                        CodigoPais = GetStr(reader, "CODIGO_PAIS"),
                        Pais       = GetStr(reader, "PAIS_NOMBRE"),
                        Importe    = GetDec(reader, "IMPORTE")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener detalle por País (Dashboard Gerencial)");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Detalle por Departamento (solo Perú, UBIGEO.PAIS='01')
        // ─────────────────────────────────────────────────────────
        public async Task<List<DgVentaMercadoDepartamentoDto>> ObtenerDetallePorDepartamentoAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DgVentaMercadoDepartamentoDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(
                    $"{S}PKG_VEND_GRUPO_MAESTROCLIENTE.SP_DETALLE_POR_DPTO", conn)
                {
                    CommandType = System.Data.CommandType.StoredProcedure,
                    BindByName  = true
                };
                cmd.Parameters.Add("P_FECHA1", OracleDbType.Date).Value          = fechaInicio.Date;
                cmd.Parameters.Add("P_FECHA2", OracleDbType.Date).Value          = fechaFin.Date;
                cmd.Parameters.Add("P_MON",    OracleDbType.Varchar2).Value      = string.IsNullOrEmpty(moneda) ? "D" : moneda.ToUpperInvariant();
                cmd.Parameters.Add("P_CURSOR", OracleDbType.RefCursor).Direction = System.Data.ParameterDirection.Output;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new DgVentaMercadoDepartamentoDto
                    {
                        Departamento = GetStr(reader, "DEPARTAMENTO"),
                        Importe      = GetDec(reader, "IMPORTE")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener detalle por Departamento (Dashboard Gerencial)");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Detalle por Distrito dentro de un Departamento (Perú)
        // ─────────────────────────────────────────────────────────
        public async Task<List<DgVentaMercadoDistritoDto>> ObtenerDetallePorDistritoAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda, string departamento)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DgVentaMercadoDistritoDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(
                    $"{S}PKG_VEND_GRUPO_MAESTROCLIENTE.SP_DETALLE_POR_DISTRITO", conn)
                {
                    CommandType = System.Data.CommandType.StoredProcedure,
                    BindByName  = true
                };
                cmd.Parameters.Add("P_FECHA1", OracleDbType.Date).Value          = fechaInicio.Date;
                cmd.Parameters.Add("P_FECHA2", OracleDbType.Date).Value          = fechaFin.Date;
                cmd.Parameters.Add("P_MON",    OracleDbType.Varchar2).Value      = string.IsNullOrEmpty(moneda) ? "D" : moneda.ToUpperInvariant();
                cmd.Parameters.Add("P_DPTO",   OracleDbType.Varchar2).Value      = departamento ?? "";
                cmd.Parameters.Add("P_CURSOR", OracleDbType.RefCursor).Direction = System.Data.ParameterDirection.Output;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new DgVentaMercadoDistritoDto
                    {
                        Departamento = GetStr(reader, "DEPARTAMENTO"),
                        Distrito     = GetStr(reader, "DISTRITO"),
                        Importe      = GetDec(reader, "IMPORTE")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener detalle por Distrito del departamento {Departamento} (Dashboard Gerencial)", departamento);
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Detalle de Ciudades por País Extranjero
        // ─────────────────────────────────────────────────────────
        public async Task<List<DgVentaMercadoCiudadPaisDto>> ObtenerCiudadesPorPaisAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda, string codigoPais)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DgVentaMercadoCiudadPaisDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(
                    $"{S}PKG_VEND_GRUPO_MAESTROCLIENTE.SP_CIUDADES_POR_PAIS", conn)
                {
                    CommandType = System.Data.CommandType.StoredProcedure,
                    BindByName  = true
                };
                cmd.Parameters.Add("P_FECHA1", OracleDbType.Date).Value          = fechaInicio.Date;
                cmd.Parameters.Add("P_FECHA2", OracleDbType.Date).Value          = fechaFin.Date;
                cmd.Parameters.Add("P_MON",    OracleDbType.Varchar2).Value      = string.IsNullOrEmpty(moneda) ? "D" : moneda.ToUpperInvariant();
                cmd.Parameters.Add("P_PAIS",   OracleDbType.Varchar2).Value      = codigoPais ?? "";
                cmd.Parameters.Add("P_CURSOR", OracleDbType.RefCursor).Direction = System.Data.ParameterDirection.Output;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new DgVentaMercadoCiudadPaisDto
                    {
                        Pais    = GetStr(reader, "PAIS_NOMBRE"),
                        Ciudad  = GetStr(reader, "CIUDAD"),
                        Importe = GetDec(reader, "IMPORTE")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener ciudades del país {Pais} (Dashboard Gerencial)", codigoPais);
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Evolución mensual por mercado
        // ─────────────────────────────────────────────────────────
        public async Task<List<DgVentaMercadoEvolucionDto>> ObtenerEvolucionMensualAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DgVentaMercadoEvolucionDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(
                    $"{S}PKG_VEND_GRUPO_MAESTROCLIENTE.SP_EVOLUCION_MENSUAL", conn)
                {
                    CommandType = System.Data.CommandType.StoredProcedure,
                    BindByName  = true
                };
                cmd.Parameters.Add("P_FECHA1", OracleDbType.Date).Value          = fechaInicio.Date;
                cmd.Parameters.Add("P_FECHA2", OracleDbType.Date).Value          = fechaFin.Date;
                cmd.Parameters.Add("P_MON",    OracleDbType.Varchar2).Value      = string.IsNullOrEmpty(moneda) ? "D" : moneda.ToUpperInvariant();
                cmd.Parameters.Add("P_CURSOR", OracleDbType.RefCursor).Direction = System.Data.ParameterDirection.Output;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new DgVentaMercadoEvolucionDto
                    {
                        Periodo = GetStr(reader, "PERIODO"),
                        Mercado = GetStr(reader, "MERCADO"),
                        Importe = GetDec(reader, "IMPORTE")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener evolución mensual por Mercado (Dashboard Gerencial)");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Mapeo de países BD → ISO (TABLAS_AUXILIARES TIPO=25)
        // ─────────────────────────────────────────────────────────
        public async Task<List<DgPaisIsoDto>> ObtenerPaisesIsoAsync()
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DgPaisIsoDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(
                    $"{S}PKG_VEND_GRUPO_MAESTROCLIENTE.SP_PAISES_ISO", conn)
                {
                    CommandType = System.Data.CommandType.StoredProcedure,
                    BindByName  = true
                };
                cmd.Parameters.Add("P_CURSOR", OracleDbType.RefCursor).Direction = System.Data.ParameterDirection.Output;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new DgPaisIsoDto
                    {
                        CodigoBD    = GetStr(reader, "CODIGO"),
                        CodigoISO   = GetStr(reader, "INDICADOR2"),
                        Descripcion = GetStr(reader, "DESCRIPCION")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener mapeo de países ISO (Dashboard Gerencial)");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Cantidad KG mensual (sin filtro de asesor)
        // ─────────────────────────────────────────────────────────
        public async Task<List<DgKgMensualDto>> ObtenerKgMensualAsync(
            DateTime fechaInicio, DateTime fechaFin)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DgKgMensualDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();
                using var cmd = new OracleCommand(
                    $"{S}PKG_VEND_GRUPO_MAESTROCLIENTE.SP_KG_MENSUAL", conn)
                {
                    CommandType = System.Data.CommandType.StoredProcedure,
                    BindByName  = true
                };
                cmd.Parameters.Add("P_FECHA1", OracleDbType.Date).Value          = fechaInicio.Date;
                cmd.Parameters.Add("P_FECHA2", OracleDbType.Date).Value          = fechaFin.Date;
                cmd.Parameters.Add("P_CURSOR", OracleDbType.RefCursor).Direction = System.Data.ParameterDirection.Output;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new DgKgMensualDto
                    {
                        Periodo    = GetStr(reader, "PERIODO"),
                        CantidadKg = GetDec(reader, "CANTIDAD_KG")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener KG mensual (Dashboard Gerencial)");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Top Hilados por Importe (agrupado por familia TFAMLIN)
        // ─────────────────────────────────────────────────────────
        public async Task<List<DgTopHiladoImporteDto>> ObtenerTopHiladosImporteAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda, int top)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DgTopHiladoImporteDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();

                using var cmd = new OracleCommand(
                    $"{S}PKG_VEND_GRUPO_MAESTROCLIENTE.SP_TOP_HILADOS_IMPORTE", conn)
                {
                    CommandType = System.Data.CommandType.StoredProcedure,
                    BindByName  = true
                };

                cmd.Parameters.Add("P_FECHA1", OracleDbType.Date).Value      = fechaInicio.Date;
                cmd.Parameters.Add("P_FECHA2", OracleDbType.Date).Value      = fechaFin.Date;
                cmd.Parameters.Add("P_MON",    OracleDbType.Varchar2).Value  = string.IsNullOrEmpty(moneda) ? "D" : moneda.ToUpperInvariant();
                cmd.Parameters.Add("P_TOP",    OracleDbType.Int32).Value     = top;
                cmd.Parameters.Add("P_CURSOR", OracleDbType.RefCursor).Direction = System.Data.ParameterDirection.Output;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new DgTopHiladoImporteDto
                    {
                        Familia = GetStr(reader, "FAMILIA"),
                        Importe = GetDec(reader, "IMPORTE")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener Top Hilados por Importe (Dashboard Gerencial)");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Ventas por Giro de Cliente
        // ─────────────────────────────────────────────────────────
        public async Task<List<DgVentaPorGiroDto>> ObtenerVentasPorGiroAsync(
            DateTime fechaInicio, DateTime fechaFin, string moneda)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DgVentaPorGiroDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();

                using var cmd = new OracleCommand(
                    $"{S}PKG_VEND_GRUPO_MAESTROCLIENTE.SP_VENTAS_POR_GIRO", conn)
                {
                    CommandType = System.Data.CommandType.StoredProcedure,
                    BindByName  = true
                };

                cmd.Parameters.Add("P_FECHA1", OracleDbType.Date).Value          = fechaInicio.Date;
                cmd.Parameters.Add("P_FECHA2", OracleDbType.Date).Value          = fechaFin.Date;
                cmd.Parameters.Add("P_MON",    OracleDbType.Varchar2).Value      = string.IsNullOrEmpty(moneda) ? "D" : moneda.ToUpperInvariant();
                cmd.Parameters.Add("P_CURSOR", OracleDbType.RefCursor).Direction = System.Data.ParameterDirection.Output;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new DgVentaPorGiroDto
                    {
                        CodigoGiro = GetStr(reader, "CODIGO_GIRO"),
                        DescGiro   = GetStr(reader, "DESC_GIRO"),
                        Importe    = GetDec(reader, "IMPORTE")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener Ventas por Giro (Dashboard Gerencial)");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────
        // Top Hilados por KG (agrupado por descripción artículo)
        // ─────────────────────────────────────────────────────────
        public async Task<List<DgTopHiladoKgDto>> ObtenerTopHiladosKgAsync(
            DateTime fechaInicio, DateTime fechaFin, int top)
        {
            var connStr = GetOracleConnectionString();
            var result  = new List<DgTopHiladoKgDto>();
            if (string.IsNullOrEmpty(connStr)) return result;

            try
            {
                using var conn = new OracleConnection(connStr);
                await conn.OpenAsync();

                using var cmd = new OracleCommand(
                    $"{S}PKG_VEND_GRUPO_MAESTROCLIENTE.SP_TOP_HILADOS_KG", conn)
                {
                    CommandType = System.Data.CommandType.StoredProcedure,
                    BindByName  = true
                };

                cmd.Parameters.Add("P_FECHA1", OracleDbType.Date).Value      = fechaInicio.Date;
                cmd.Parameters.Add("P_FECHA2", OracleDbType.Date).Value      = fechaFin.Date;
                cmd.Parameters.Add("P_TOP",    OracleDbType.Int32).Value     = top;
                cmd.Parameters.Add("P_CURSOR", OracleDbType.RefCursor).Direction = System.Data.ParameterDirection.Output;

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    result.Add(new DgTopHiladoKgDto
                    {
                        Familia = GetStr(reader, "FAMILIA"),
                        Kilos   = GetDec(reader, "KILOS")
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener Top Hilados por KG (Dashboard Gerencial)");
            }

            return result;
        }
    }
}
