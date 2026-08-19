namespace FabricaHilos.Alertas.Data;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;
using FabricaHilos.Alertas.Models;

/// <summary>
/// Acceso a AQUARIUS (Oracle) para el proceso semanal de alertas de tareo
/// (ver Alertas_Turno_Descanso/V_SCA_ALERTA_TAREO_DETALLE.sql y
/// PKG_SCA_ALERTAS_TAREO.sql). Solo lee la vista y marca como notificadas las
/// alertas ya enviadas; la generaci�n (GENERAR_ALERTAS) la hace el job Oracle.
/// </summary>
public sealed class AlertaTurnoDescansoRepository : IAlertaTurnoDescansoRepository
{
    private const string SqlPendientes = """
        SELECT id_alerta, cod_empresa, cod_personal, nombre_empleado, tip_alerta, tip_alerta_desc,
               fecini_semana, fecfin_semana, turno_cod, turno_descripcion, horario_desc,
               hora_ingreso_teorica, hora_salida_teorica, cod_c_costos, centro_costo_nombre,
               cod_area, area_nombre, encargado_nombre, dias_descanso, detalle,
               fec_deteccion, estado, notificado, fec_notificacion
        FROM AQUARIUS.V_SCA_ALERTA_TAREO_DETALLE
        WHERE notificado = 'N'
        ORDER BY tip_alerta, cod_empresa, nombre_empleado
        """;

    private const string SqlMarcarNotificado = """
        BEGIN
          AQUARIUS.PKG_SCA_ALERTAS_TAREO.MARCAR_NOTIFICADO(p_id_alerta => :p_id_alerta);
        END;
        """;

    private readonly string _oracleConnStr;
    private readonly ILogger<AlertaTurnoDescansoRepository> _logger;

    public AlertaTurnoDescansoRepository(IConfiguration configuration, ILogger<AlertaTurnoDescansoRepository> logger)
    {
        _oracleConnStr = configuration.GetConnectionString("LaColonialConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:LaColonialConnection no configurada.");
        _logger = logger;
    }

    public async Task<IReadOnlyList<AlertaTurnoDescansoDetalle>> ObtenerPendientesAsync(CancellationToken ct)
    {
        var lista = new List<AlertaTurnoDescansoDetalle>();

        return await OracleRetry.EjecutarAsync(async () =>
        {
            lista.Clear();

            await using var conn = new OracleConnection(_oracleConnStr);
            await conn.OpenAsync(ct);

            await using var cmd = new OracleCommand(SqlPendientes, conn) { CommandTimeout = 60 };
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                lista.Add(new AlertaTurnoDescansoDetalle
                {
                    IdAlerta           = reader.GetInt64(reader.GetOrdinal("id_alerta")),
                    CodEmpresa         = LeerString(reader, "cod_empresa"),
                    CodPersonal        = LeerString(reader, "cod_personal"),
                    NombreEmpleado     = LeerString(reader, "nombre_empleado"),
                    TipAlerta          = LeerString(reader, "tip_alerta"),
                    TipAlertaDesc      = LeerString(reader, "tip_alerta_desc"),
                    FecIniSemana       = reader.GetDateTime(reader.GetOrdinal("fecini_semana")),
                    FecFinSemana       = reader.GetDateTime(reader.GetOrdinal("fecfin_semana")),
                    TurnoCod           = LeerStringNullable(reader, "turno_cod"),
                    TurnoDescripcion   = LeerStringNullable(reader, "turno_descripcion"),
                    HorarioDesc        = LeerStringNullable(reader, "horario_desc"),
                    HoraIngresoTeorica = LeerStringNullable(reader, "hora_ingreso_teorica"),
                    HoraSalidaTeorica  = LeerStringNullable(reader, "hora_salida_teorica"),
                    CodCCostos         = LeerStringNullable(reader, "cod_c_costos"),
                    CentroCostoNombre  = LeerStringNullable(reader, "centro_costo_nombre"),
                    CodArea            = LeerStringNullable(reader, "cod_area"),
                    AreaNombre         = LeerStringNullable(reader, "area_nombre"),
                    EncargadoNombre    = LeerStringNullable(reader, "encargado_nombre"),
                    DiasDescanso       = LeerIntNullable(reader, "dias_descanso"),
                    Detalle            = LeerStringNullable(reader, "detalle"),
                    FecDeteccion       = reader.GetDateTime(reader.GetOrdinal("fec_deteccion")),
                    Estado             = LeerString(reader, "estado"),
                    Notificado         = LeerString(reader, "notificado"),
                    FecNotificacion    = LeerDateTimeNullable(reader, "fec_notificacion"),
                });
            }

            _logger.LogInformation("[ALERTAS-TAREO] {Cantidad} alerta(s) pendiente(s) de notificar en V_SCA_ALERTA_TAREO_DETALLE.", lista.Count);

            return (IReadOnlyList<AlertaTurnoDescansoDetalle>)lista;
        }, _logger, nameof(ObtenerPendientesAsync), ct);
    }

    public async Task MarcarNotificadoAsync(long idAlerta, CancellationToken ct)
    {
        await OracleRetry.EjecutarAsync(async () =>
        {
            await using var conn = new OracleConnection(_oracleConnStr);
            await conn.OpenAsync(ct);

            await using var cmd = new OracleCommand(SqlMarcarNotificado, conn) { CommandTimeout = 30 };
            cmd.Parameters.Add(new OracleParameter("p_id_alerta", idAlerta));
            await cmd.ExecuteNonQueryAsync(ct);

            return true;
        }, _logger, nameof(MarcarNotificadoAsync), ct);
    }

    private static string LeerString(OracleDataReader reader, string columna)
    {
        var ordinal = reader.GetOrdinal(columna);
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
    }

    private static string? LeerStringNullable(OracleDataReader reader, string columna)
    {
        var ordinal = reader.GetOrdinal(columna);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static int? LeerIntNullable(OracleDataReader reader, string columna)
    {
        var ordinal = reader.GetOrdinal(columna);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static DateTime? LeerDateTimeNullable(OracleDataReader reader, string columna)
    {
        var ordinal = reader.GetOrdinal(columna);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }
}
