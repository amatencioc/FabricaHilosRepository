using FabricaHilos.Models.RecursosHumanos;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Data;

namespace FabricaHilos.Services.RecursosHumanos;

public interface IProyeccionAsistenciaService
{
    Task<(bool Ok, string? Mensaje, List<ProyeccionResumenDto> Resumen, List<ProyeccionEmpleadoDto> Detalle)> ConsultarAsync(
        DateTime fecha, string? codEmpresa);
}

/// <summary>
/// v1.0 — Invoca AQUARIUS.SP_AQ_PROYECCION_ASISTENCIA: pronóstico de cuántos y qué
/// empleados activos vendrían a trabajar en una fecha específica, derivado del
/// horario/turno vigente de cada uno (SCA_HORARIO_PERSONAL/CAB/DET) y descartando
/// a quienes tengan un evento activo ese día (SIG.RH_EVENTOS: vacaciones, descanso
/// médico, permiso, etc.). 100% lectura, no modifica datos.
/// </summary>
public class ProyeccionAsistenciaService : IProyeccionAsistenciaService
{
    private readonly string _connectionString;
    private readonly ILogger<ProyeccionAsistenciaService> _logger;

    public ProyeccionAsistenciaService(IConfiguration configuration, ILogger<ProyeccionAsistenciaService> logger)
    {
        _connectionString = configuration.GetConnectionString("AquariusConnection")
            ?? throw new InvalidOperationException("AquariusConnection connection string not found.");
        _logger = logger;
    }

    public async Task<(bool Ok, string? Mensaje, List<ProyeccionResumenDto> Resumen, List<ProyeccionEmpleadoDto> Detalle)> ConsultarAsync(
        DateTime fecha, string? codEmpresa)
    {
        var resumen = new List<ProyeccionResumenDto>();
        var detalle = new List<ProyeccionEmpleadoDto>();

        try
        {
            await using var conn = new OracleConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = "SP_AQ_PROYECCION_ASISTENCIA";
            cmd.BindByName = true;

            cmd.Parameters.Add(new OracleParameter("p_fecha", OracleDbType.Date) { Value = fecha.Date });
            cmd.Parameters.Add(new OracleParameter("p_cod_empresa", OracleDbType.Varchar2)
            {
                Value = string.IsNullOrWhiteSpace(codEmpresa) ? DBNull.Value : codEmpresa
            });
            cmd.Parameters.Add(new OracleParameter("p_cur_resumen", OracleDbType.RefCursor) { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_cur_detalle", OracleDbType.RefCursor) { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_error", OracleDbType.Varchar2, 500) { Direction = ParameterDirection.Output });

            await cmd.ExecuteNonQueryAsync();

            var error = cmd.Parameters["p_error"].Value is OracleString os && !os.IsNull ? os.Value : null;
            if (!string.IsNullOrEmpty(error))
                return (false, error, resumen, detalle);

            static string? Col(OracleDataReader r, string name)
            {
                int idx;
                try { idx = r.GetOrdinal(name); }
                catch (IndexOutOfRangeException) { return null; }
                return r.IsDBNull(idx) ? null : r.GetValue(idx).ToString();
            }

            if (cmd.Parameters["p_cur_resumen"].Value is OracleRefCursor resCursor)
            {
                using var resReader = resCursor.GetDataReader();
                while (resReader.Read())
                {
                    var cantidadStr = Col(resReader, "CANTIDAD");
                    resumen.Add(new ProyeccionResumenDto
                    {
                        Estado   = Col(resReader, "ESTADO"),
                        Cantidad = int.TryParse(cantidadStr, out var n) ? n : 0,
                    });
                }
            }

            if (cmd.Parameters["p_cur_detalle"].Value is OracleRefCursor detCursor)
            {
                using var detReader = detCursor.GetDataReader();
                while (detReader.Read())
                {
                    var horasNumStr = Col(detReader, "HORAS_TRABAJO_NUM");
                    detalle.Add(new ProyeccionEmpleadoDto
                    {
                        CodPersonal        = Col(detReader, "COD_PERSONAL"),
                        CodSpring          = Col(detReader, "COD_SPRING"),
                        NombreCompleto     = Col(detReader, "NOMBRE_COMPLETO"),
                        Empresa            = Col(detReader, "EMPRESA"),
                        HorarioDescripcion = Col(detReader, "HORARIO_DESCRIPCION"),
                        Turno              = Col(detReader, "TURNO"),
                        HoraIngresoTeorica = Col(detReader, "HORA_INGRESO_TEORICA"),
                        HoraSalidaTeorica  = Col(detReader, "HORA_SALIDA_TEORICA"),
                        HorasTrabajo       = Col(detReader, "HORAS_TRABAJO"),
                        HorasTrabajoNum    = decimal.TryParse(horasNumStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var hn) ? hn : null,
                        Ccosto             = Col(detReader, "CCOSTO"),
                        CcostoNombre       = Col(detReader, "CCOSTO_NOMBRE"),
                        GranCcosto         = Col(detReader, "GRAN_CCOSTO"),
                        GranCcostoNombre   = Col(detReader, "GRAN_CCOSTO_NOMBRE"),
                        EncargadoNombre    = Col(detReader, "ENCARGADO_NOMBRE"),
                        Estado             = Col(detReader, "ESTADO"),
                        EventoDescripcion  = Col(detReader, "EVENTO_DESCRIPCION"),
                        Feriado            = Col(detReader, "FERIADO"),
                    });
                }
            }

            return (true, null, resumen, detalle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al consultar proyección de asistencia para {Fecha} ({Empresa})", fecha, codEmpresa);
            return (false, "Error al consultar la proyección de asistencia en Aquarius.", resumen, detalle);
        }
    }
}
