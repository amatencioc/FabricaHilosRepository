using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace FabricaHilos.Services.Seguridad.Inspeccion
{
    public class ResponsableDto
    {
        public string Codigo { get; set; } = string.Empty;
        public string NombreCorto { get; set; } = string.Empty;
        public string TextoCompleto => $"{Codigo} - {NombreCorto}";
    }

    public class CentroCostoDto
    {
        public string CentroCosto { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string TextoCompleto => $"{CentroCosto} - {Nombre}";
    }

    public class InspeccionListDto
    {
        public int Numero { get; set; }
        public string CentroCosto { get; set; } = string.Empty;
        public string NombreCentroCosto { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string ResponsableInspeccion { get; set; } = string.Empty;
        public string NombreRespInspeccion { get; set; } = string.Empty;
        public string ResponsableArea { get; set; } = string.Empty;
        public string NombreRespArea { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string? RutaFotoH { get; set; }
        public DateTime? FechaFotoH { get; set; }
        public string? UbicaFotoH { get; set; }
        public string? RutaFotoAc { get; set; }
        public DateTime? FechaFotoAc { get; set; }
        public string? UbicaFotoAc { get; set; }
        public bool TieneAccionCorrectiva => !string.IsNullOrEmpty(UbicaFotoAc);
    }

    public interface IInspeccionService
    {
        Task<List<ResponsableDto>> ObtenerResponsablesAreaAsync();
        Task<List<ResponsableDto>> ObtenerResponsablesInspeccionAsync();
        Task<List<CentroCostoDto>> ObtenerCentrosCostoAsync();
        Task<int> ObtenerSiguienteNumeroInspeccionAsync();
        Task RegistrarHallazgoAsync(InspeccionRegistroDto inspeccion, string usuario);
        Task<List<InspeccionListDto>> ObtenerInspeccionesAsync(string? buscar = null, string? tipo = null);
        Task<InspeccionListDto?> ObtenerInspeccionPorNumeroAsync(int numero);
        Task RegistrarAccionCorrectivaAsync(int numero, string rutaFoto, string ubicaFoto, string usuario);
    }

    public class InspeccionRegistroDto
    {
        public int NumeroInspeccion { get; set; }
        public string CentroCosto { get; set; } = string.Empty;
        public string TipoInspeccion { get; set; } = string.Empty;
        public string ResponsableInspeccion { get; set; } = string.Empty;
        public string ResponsableArea { get; set; } = string.Empty;
        public string RutaFoto { get; set; } = string.Empty;
        public string UbicaFoto { get; set; } = string.Empty;
    }

    public class InspeccionService : IInspeccionService
    {
        private readonly string _connectionString;
        private readonly ILogger<InspeccionService> _logger;

        public InspeccionService(IConfiguration configuration, ILogger<InspeccionService> logger)
        {
            _connectionString = configuration.GetConnectionString("OracleConnection")
                ?? throw new InvalidOperationException("Oracle connection string not found.");
            _logger = logger;
        }

        public async Task<List<ResponsableDto>> ObtenerResponsablesAreaAsync()
        {
            var resultado = new List<ResponsableDto>();

            const string query = @"
                SELECT C_CODIGO, NOMBRE_CORTO
                FROM V_PERSONAL
                WHERE SITUACION = '1'
                ORDER BY 2";

            try
            {
                using var connection = new OracleConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new OracleCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    resultado.Add(new ResponsableDto
                    {
                        Codigo = reader.GetString(0),
                        NombreCorto = reader.GetString(1)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener responsables de área");
                throw;
            }

            return resultado;
        }

        public async Task<List<ResponsableDto>> ObtenerResponsablesInspeccionAsync()
        {
            var resultado = new List<ResponsableDto>();

            const string query = @"
                SELECT C_CODIGO, NOMBRE_CORTO
                FROM V_PERSONAL
                WHERE SITUACION = '1'
                  AND C_CARGO IN (SELECT C_CARGO FROM T_CARGO WHERE CCOSTO = '280' AND ESTADO <> '9')
                ORDER BY 2";

            try
            {
                using var connection = new OracleConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new OracleCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    resultado.Add(new ResponsableDto
                    {
                        Codigo = reader.GetString(0),
                        NombreCorto = reader.GetString(1)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener responsables de inspección");
                throw;
            }

            return resultado;
        }

        public async Task<List<CentroCostoDto>> ObtenerCentrosCostoAsync()
        {
            var resultado = new List<CentroCostoDto>();

            const string query = @"
                SELECT CENTRO_COSTO, SUBSTR(NOMBRE, 1, 30) NOMBRE
                FROM CENTRO_DE_COSTOS
                WHERE TIPO = 'D'
                  AND ESTADO <> '9'
                ORDER BY 2";

            try
            {
                using var connection = new OracleConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new OracleCommand(query, connection);
                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    resultado.Add(new CentroCostoDto
                    {
                        CentroCosto = reader.GetString(0),
                        Nombre = reader.GetString(1)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener centros de costo");
                throw;
            }

            return resultado;
        }

        public async Task<List<InspeccionListDto>> ObtenerInspeccionesAsync(string? buscar = null, string? tipo = null)
        {
            var resultado = new List<InspeccionListDto>();

            var query = @"
                SELECT 
                    i.NUMERO,
                    i.CCOSTO,
                    NVL(c.NOMBRE, '') AS NOMBRE_CCOSTO,
                    i.FECHA,
                    i.TIPO,
                    i.RESP_INSPECCION,
                    NVL(vp1.NOMBRE_CORTO, '') AS NOMBRE_RESP_INSPECCION,
                    i.RESP_AREA,
                    NVL(vp2.NOMBRE_CORTO, '') AS NOMBRE_RESP_AREA,
                    i.ESTADO,
                    i.RUTA_FOTO_H,
                    i.FCH_FOTO_H,
                    i.UBICA_FOTO_H,
                    i.RUTA_FOTO_AC,
                    i.FCH_FOTO_AC,
                    i.UBICA_FOTO_AC
                FROM SI_INSPECCION i
                LEFT JOIN CENTRO_DE_COSTOS c ON i.CCOSTO = c.CENTRO_COSTO
                LEFT JOIN V_PERSONAL vp1 ON i.RESP_INSPECCION = vp1.C_CODIGO
                LEFT JOIN V_PERSONAL vp2 ON i.RESP_AREA = vp2.C_CODIGO
                WHERE 1=1";

            if (!string.IsNullOrWhiteSpace(buscar))
            {
                query += " AND (UPPER(i.CCOSTO) LIKE '%' || :buscar || '%' OR UPPER(c.NOMBRE) LIKE '%' || :buscar || '%')";
            }

            if (!string.IsNullOrWhiteSpace(tipo))
            {
                query += " AND i.TIPO = :tipo";
            }

            query += " ORDER BY i.NUMERO DESC";

            try
            {
                using var connection = new OracleConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new OracleCommand(query, connection);

                if (!string.IsNullOrWhiteSpace(buscar))
                {
                    command.Parameters.Add("buscar", OracleDbType.Varchar2).Value = buscar.ToUpperInvariant();
                }

                if (!string.IsNullOrWhiteSpace(tipo))
                {
                    command.Parameters.Add("tipo", OracleDbType.Varchar2).Value = tipo;
                }

                using var reader = await command.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    resultado.Add(new InspeccionListDto
                    {
                        Numero = reader.GetInt32(0),
                        CentroCosto = reader.GetString(1),
                        NombreCentroCosto = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        Fecha = reader.GetDateTime(3),
                        Tipo = reader.GetString(4),
                        ResponsableInspeccion = reader.GetString(5),
                        NombreRespInspeccion = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                        ResponsableArea = reader.GetString(7),
                        NombreRespArea = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                        Estado = reader.GetString(9),
                        RutaFotoH = reader.IsDBNull(10) ? null : reader.GetString(10),
                        FechaFotoH = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
                        UbicaFotoH = reader.IsDBNull(12) ? null : reader.GetString(12),
                        RutaFotoAc = reader.IsDBNull(13) ? null : reader.GetString(13),
                        FechaFotoAc = reader.IsDBNull(14) ? null : reader.GetDateTime(14),
                        UbicaFotoAc = reader.IsDBNull(15) ? null : reader.GetString(15)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener listado de inspecciones");
                throw;
            }

            return resultado;
        }

        public async Task<InspeccionListDto?> ObtenerInspeccionPorNumeroAsync(int numero)
        {
            const string query = @"
                SELECT 
                    i.NUMERO,
                    i.CCOSTO,
                    NVL(c.NOMBRE, '') AS NOMBRE_CCOSTO,
                    i.FECHA,
                    i.TIPO,
                    i.RESP_INSPECCION,
                    NVL(vp1.NOMBRE_CORTO, '') AS NOMBRE_RESP_INSPECCION,
                    i.RESP_AREA,
                    NVL(vp2.NOMBRE_CORTO, '') AS NOMBRE_RESP_AREA,
                    i.ESTADO,
                    i.RUTA_FOTO_H,
                    i.FCH_FOTO_H,
                    i.UBICA_FOTO_H,
                    i.RUTA_FOTO_AC,
                    i.FCH_FOTO_AC,
                    i.UBICA_FOTO_AC
                FROM SI_INSPECCION i
                LEFT JOIN CENTRO_DE_COSTOS c ON i.CCOSTO = c.CENTRO_COSTO
                LEFT JOIN V_PERSONAL vp1 ON i.RESP_INSPECCION = vp1.C_CODIGO
                LEFT JOIN V_PERSONAL vp2 ON i.RESP_AREA = vp2.C_CODIGO
                WHERE i.NUMERO = :numero";

            try
            {
                using var connection = new OracleConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new OracleCommand(query, connection);
                command.Parameters.Add("numero", OracleDbType.Int32).Value = numero;

                using var reader = await command.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    return new InspeccionListDto
                    {
                        Numero = reader.GetInt32(0),
                        CentroCosto = reader.GetString(1),
                        NombreCentroCosto = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        Fecha = reader.GetDateTime(3),
                        Tipo = reader.GetString(4),
                        ResponsableInspeccion = reader.GetString(5),
                        NombreRespInspeccion = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                        ResponsableArea = reader.GetString(7),
                        NombreRespArea = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                        Estado = reader.GetString(9),
                        RutaFotoH = reader.IsDBNull(10) ? null : reader.GetString(10),
                        FechaFotoH = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
                        UbicaFotoH = reader.IsDBNull(12) ? null : reader.GetString(12),
                        RutaFotoAc = reader.IsDBNull(13) ? null : reader.GetString(13),
                        FechaFotoAc = reader.IsDBNull(14) ? null : reader.GetDateTime(14),
                        UbicaFotoAc = reader.IsDBNull(15) ? null : reader.GetString(15)
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener inspección {Numero}", numero);
                throw;
            }

            return null;
        }

        public async Task<int> ObtenerSiguienteNumeroInspeccionAsync()
        {
            const string query = "SELECT NUMERO FROM NRODOC WHERE TIPODOC = 'IN'";

            try
            {
                using var connection = new OracleConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new OracleCommand(query, connection);
                var result = await command.ExecuteScalarAsync();
                return result != null ? Convert.ToInt32(result) : 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener siguiente número de inspección");
                throw;
            }
        }

        public async Task RegistrarHallazgoAsync(InspeccionRegistroDto inspeccion, string usuario)
        {
            using var connection = new OracleConnection(_connectionString);
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();

            try
            {
                // 1. Insertar en SI_INSPECCION
                const string queryInsertar = @"
                    INSERT INTO SI_INSPECCION 
                    (NUMERO, CCOSTO, FECHA, TIPO, RESP_INSPECCION, RESP_AREA, ESTADO, 
                     RUTA_FOTO_H, FCH_FOTO_H, UBICA_FOTO_H, A_ADUSER, A_ADFECHA)
                    VALUES 
                    (:numero, :ccosto, SYSDATE, :tipo, :respInspeccion, :respArea, '1',
                     :rutaFoto, SYSDATE, :ubicaFoto, :usuario, SYSDATE)";

                using (var cmdInsertar = new OracleCommand(queryInsertar, connection))
                {
                    cmdInsertar.Transaction = transaction;
                    cmdInsertar.Parameters.Add("numero", OracleDbType.Int32).Value = inspeccion.NumeroInspeccion;
                    cmdInsertar.Parameters.Add("ccosto", OracleDbType.Varchar2).Value = inspeccion.CentroCosto;
                    cmdInsertar.Parameters.Add("tipo", OracleDbType.Varchar2).Value = inspeccion.TipoInspeccion;
                    cmdInsertar.Parameters.Add("respInspeccion", OracleDbType.Varchar2).Value = inspeccion.ResponsableInspeccion;
                    cmdInsertar.Parameters.Add("respArea", OracleDbType.Varchar2).Value = inspeccion.ResponsableArea;
                    cmdInsertar.Parameters.Add("rutaFoto", OracleDbType.Varchar2).Value = inspeccion.RutaFoto;
                    cmdInsertar.Parameters.Add("ubicaFoto", OracleDbType.Varchar2).Value = inspeccion.UbicaFoto;
                    cmdInsertar.Parameters.Add("usuario", OracleDbType.Varchar2).Value = usuario;

                    await cmdInsertar.ExecuteNonQueryAsync();
                }

                // 2. Actualizar el correlativo en NRODOC (+1)
                const string queryActualizarCorrelativo = @"
                    UPDATE NRODOC 
                    SET NUMERO = NUMERO + 1 
                    WHERE TIPODOC = 'IN'";

                using (var cmdActualizar = new OracleCommand(queryActualizarCorrelativo, connection))
                {
                    cmdActualizar.Transaction = transaction;
                    await cmdActualizar.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
                _logger.LogInformation("Hallazgo (Inspección) registrado exitosamente. Número: {Numero}", inspeccion.NumeroInspeccion);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error al registrar hallazgo (inspección)");
                throw;
            }
        }

        public async Task RegistrarAccionCorrectivaAsync(int numero, string rutaFoto, string ubicaFoto, string usuario)
        {
            const string query = @"
                UPDATE SI_INSPECCION
                SET RUTA_FOTO_AC = :rutaFoto,
                    FCH_FOTO_AC = SYSDATE,
                    UBICA_FOTO_AC = :ubicaFoto,
                    A_MDUSER = :usuario,
                    A_MDFECHA = SYSDATE
                WHERE NUMERO = :numero";

            try
            {
                using var connection = new OracleConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new OracleCommand(query, connection);
                command.Parameters.Add("rutaFoto", OracleDbType.Varchar2).Value = rutaFoto;
                command.Parameters.Add("ubicaFoto", OracleDbType.Varchar2).Value = ubicaFoto;
                command.Parameters.Add("usuario", OracleDbType.Varchar2).Value = usuario;
                command.Parameters.Add("numero", OracleDbType.Int32).Value = numero;

                await command.ExecuteNonQueryAsync();
                _logger.LogInformation("Acción correctiva registrada para inspección {Numero}", numero);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar acción correctiva para inspección {Numero}", numero);
                throw;
            }
        }
    }
}
