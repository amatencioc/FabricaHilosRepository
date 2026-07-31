using FabricaHilos.Models.RecursosHumanos;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Data;

namespace FabricaHilos.Services.RecursosHumanos;

public interface IFindEmpleadoService
{
    Task<(bool Ok, string? Mensaje, EmpleadoConEventoRealDto? Data)> BuscarAsync(
        string busqueda, string tipoBusqueda, DateTime? fechaDesde = null, DateTime? fechaHasta = null);

    Task<List<SugerenciaEmpleadoDto>> SugerirNombresAsync(string texto);
}

/// <summary>
/// Invoca AQUARIUS.SP_FIND_EMPLEADO_EVENTO_REAL: búsqueda flexible (CODIGO/DNI/NOMBRE)
/// + asistencia de HOY (AQUARIUS) + evento activo EN VIVO (SIG.RH_EVENTOS).
/// No modifica datos — 100% lectura.
/// </summary>
public class FindEmpleadoService : IFindEmpleadoService
{
    private readonly string _connectionString;
    private readonly ILogger<FindEmpleadoService> _logger;

    public FindEmpleadoService(IConfiguration configuration, ILogger<FindEmpleadoService> logger)
    {
        _connectionString = configuration.GetConnectionString("AquariusConnection")
            ?? throw new InvalidOperationException("AquariusConnection connection string not found.");
        _logger = logger;
    }

    public async Task<(bool Ok, string? Mensaje, EmpleadoConEventoRealDto? Data)> BuscarAsync(
        string busqueda, string tipoBusqueda, DateTime? fechaDesde = null, DateTime? fechaHasta = null)
    {
        try
        {
            await using var conn = new OracleConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = "SP_FIND_EMPLEADO_EVENTO_REAL";
            cmd.BindByName = true;

            cmd.Parameters.Add(new OracleParameter("p_busqueda",      OracleDbType.Varchar2) { Value = busqueda });
            cmd.Parameters.Add(new OracleParameter("p_tipo_busqueda", OracleDbType.Varchar2) { Value = tipoBusqueda });
            cmd.Parameters.Add(new OracleParameter("p_fecha_desde", OracleDbType.Date) { Value = (object?)fechaDesde ?? DBNull.Value });
            cmd.Parameters.Add(new OracleParameter("p_fecha_hasta", OracleDbType.Date) { Value = (object?)fechaHasta ?? DBNull.Value });

            cmd.Parameters.Add(new OracleParameter("p_cod_aquarius", OracleDbType.Varchar2, 10)  { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_cod_sig",      OracleDbType.Varchar2, 10)  { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_nombre",       OracleDbType.Varchar2, 200) { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_dni",          OracleDbType.Varchar2, 20)  { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_empresa",      OracleDbType.Varchar2, 10)  { Direction = ParameterDirection.Output });

            cmd.Parameters.Add(new OracleParameter("p_estado_hoy",   OracleDbType.Varchar2, 20)  { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_entrada",      OracleDbType.Varchar2, 10)  { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_salida",       OracleDbType.Varchar2, 10)  { Direction = ParameterDirection.Output });

            cmd.Parameters.Add(new OracleParameter("p_evento_desc",   OracleDbType.Varchar2, 500)  { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_evento_tipo",   OracleDbType.Varchar2, 5)    { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_evento_fechas", OracleDbType.Varchar2, 30)   { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_evento_obs",    OracleDbType.Varchar2, 500)  { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_evento_fuente", OracleDbType.Varchar2, 50)   { Direction = ParameterDirection.Output });

            cmd.Parameters.Add(new OracleParameter("p_vig_estado",  OracleDbType.Varchar2, 30)  { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_vig_entrada", OracleDbType.Varchar2, 10)  { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_vig_salida",  OracleDbType.Varchar2, 10)  { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_vig_alcohol", OracleDbType.Varchar2, 20)  { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_vig_celular", OracleDbType.Varchar2, 5)   { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_vig_fuente",  OracleDbType.Varchar2, 50)  { Direction = ParameterDirection.Output });

            cmd.Parameters.Add(new OracleParameter("p_rango_vig_desde", OracleDbType.Varchar2, 15) { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_rango_vig_hasta", OracleDbType.Varchar2, 15) { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_rango_evt_desde", OracleDbType.Varchar2, 15) { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_rango_evt_hasta", OracleDbType.Varchar2, 15) { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_cur_vigilancia", OracleDbType.RefCursor) { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_cur_eventos",    OracleDbType.RefCursor) { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_cur_compensaciones", OracleDbType.RefCursor) { Direction = ParameterDirection.Output });

            cmd.Parameters.Add(new OracleParameter("p_nota_sync", OracleDbType.Varchar2, 200) { Direction = ParameterDirection.Output });
            cmd.Parameters.Add(new OracleParameter("p_error",     OracleDbType.Varchar2, 500) { Direction = ParameterDirection.Output });

            await cmd.ExecuteNonQueryAsync();

            string? GetOut(string name) =>
                cmd.Parameters[name].Value is OracleString os && !os.IsNull ? os.Value : null;

            static string? Col(OracleDataReader r, string name)
            {
                int idx;
                try
                {
                    idx = r.GetOrdinal(name);
                }
                catch (IndexOutOfRangeException)
                {
                    // Columna no existe en el resultset (ej. SP desplegado no coincide aún
                    // con la versión esperada por este código) — no debe tumbar la búsqueda.
                    return null;
                }
                return r.IsDBNull(idx) ? null : r.GetValue(idx).ToString();
            }

            var error = GetOut("p_error");
            if (!string.IsNullOrEmpty(error))
                return (false, error, null);

            var codAquarius = GetOut("p_cod_aquarius");
            if (string.IsNullOrEmpty(codAquarius))
                return (false, $"Empleado no encontrado: {busqueda}", null);

            var vigilanciaRegistros = new List<VigilanciaRegistroDto>();
            if (cmd.Parameters["p_cur_vigilancia"].Value is OracleRefCursor vigCursor)
            {
                using var vigReader = vigCursor.GetDataReader();
                while (vigReader.Read())
                {
                    vigilanciaRegistros.Add(new VigilanciaRegistroDto
                    {
                        Tipo             = Col(vigReader, "TIPO"),
                        CodSig           = Col(vigReader, "C_CODIGO"),
                        DocId            = Col(vigReader, "DOCID"),
                        Nombre           = Col(vigReader, "NOMBRE"),
                        DniRuc           = Col(vigReader, "DNI_RUC"),
                        CentroCosto      = Col(vigReader, "C_COSTO"),
                        TipoCp           = Col(vigReader, "TIPO_CP"),
                        FechaIngreso     = Col(vigReader, "FECHAI"),
                        FechaSalida      = Col(vigReader, "FECHAF"),
                        TraeCelular      = Col(vigReader, "TRAE_CELULAR"),
                        GuardaCelular    = Col(vigReader, "GUARDA_CELULAR"),
                        NroBlock         = Col(vigReader, "NRO_BLOCK"),
                        TestAlcohol      = Col(vigReader, "TEST_ALCOHOL"),
                        ResultadoAlcohol = Col(vigReader, "RESULTADO_ALCOHOL"),
                        Observacion      = Col(vigReader, "OBSERVACION"),
                    });
                }
            }

            var eventosHistorial = new List<EventoHistorialDto>();
            if (cmd.Parameters["p_cur_eventos"].Value is OracleRefCursor evtCursor)
            {
                using var evtReader = evtCursor.GetDataReader();
                while (evtReader.Read())
                {
                    eventosHistorial.Add(new EventoHistorialDto
                    {
                        TipoCodigo   = Col(evtReader, "EVENTO_TIPO_CODIGO"),
                        Descripcion  = Col(evtReader, "EVENTO_DESCRIPCION"),
                        FechaInicio  = Col(evtReader, "FECHA_INICIO"),
                        FechaFinal   = Col(evtReader, "FECHA_FINAL"),
                        Observacion  = Col(evtReader, "OBSERVACION"),
                        NoSincroniza = Col(evtReader, "NO_SINCRONIZA") == "S",
                    });
                }
            }

            var compensaciones = new List<CompensacionDto>();
            if (cmd.Parameters["p_cur_compensaciones"].Value is OracleRefCursor compCursor)
            {
                using var compReader = compCursor.GetDataReader();
                while (compReader.Read())
                {
                    compensaciones.Add(new CompensacionDto
                    {
                        IdCompen             = Col(compReader, "ID_COMPEN"),
                        FechaOrigen          = Col(compReader, "FECHAORIGEN"),
                        TipoOrigen           = Col(compReader, "TIPOORIGEN"),
                        TipoOrigenDesc       = Col(compReader, "TIPOORIGEN_DESC"),
                        FechaDestino         = Col(compReader, "FECHADESTINO"),
                        TipoCompensacion     = Col(compReader, "TIPOCOMPENSACION"),
                        TipoCompensacionDesc = Col(compReader, "TIPOCOMPENSACION_DESC"),
                        TiempoHhMm           = Col(compReader, "TIEMPO_HHMM"),
                        Aux1                 = Col(compReader, "AUX1"),
                        Descripcion          = Col(compReader, "DESCRIPCION"),
                    });
                }
            }

            // v1.8 — Horario/Turno vigente. Consulta directa (no vía el SP grande, para no
            // depender de un nuevo redeploy) contra SCA_HORARIO_PERSONAL + SCA_HORARIO_CAB,
            // resolviendo el horario vigente a HOY mediante el patrón MAX(fec_vigencia).
            string? horarioDesc = null, horarioTurno = null;
            var codEmpresaVal = GetOut("p_empresa");
            if (!string.IsNullOrEmpty(codEmpresaVal))
            {
                try
                {
                    await using var cmdHor = conn.CreateCommand();
                    cmdHor.BindByName = true;
                    cmdHor.CommandText =
                        "SELECT hc.hordes, hc.hortur " +
                        "FROM SCA_HORARIO_PERSONAL hp " +
                        "JOIN SCA_HORARIO_CAB hc ON hc.horid = hp.horid " +
                        "WHERE hp.cod_empresa = :emp AND hp.cod_personal = :cod " +
                        "  AND hp.fec_vigencia = (SELECT MAX(fec_vigencia) FROM SCA_HORARIO_PERSONAL " +
                        "                          WHERE cod_empresa = :emp AND cod_personal = :cod AND fec_vigencia <= SYSDATE)";
                    cmdHor.Parameters.Add(new OracleParameter("emp", OracleDbType.Varchar2) { Value = codEmpresaVal });
                    cmdHor.Parameters.Add(new OracleParameter("cod", OracleDbType.Varchar2) { Value = codAquarius });

                    await using var horReader = await cmdHor.ExecuteReaderAsync();
                    if (await horReader.ReadAsync())
                    {
                        horarioDesc  = horReader["hordes"] is DBNull ? null : horReader["hordes"].ToString()?.Trim();
                        horarioTurno = horReader["hortur"] is DBNull ? null : horReader["hortur"].ToString()?.Trim();
                    }
                }
                catch (Exception exHor)
                {
                    // No debe tumbar la búsqueda principal si esta consulta adicional falla.
                    _logger.LogWarning(exHor, "No se pudo resolver horario/turno vigente para {Cod}", codAquarius);
                }
            }

            var dto = new EmpleadoConEventoRealDto
            {
                CodAquarius          = codAquarius,
                CodSig               = GetOut("p_cod_sig"),
                NombreCompleto       = GetOut("p_nombre"),
                Dni                  = GetOut("p_dni"),
                Empresa              = codEmpresaVal,
                HorarioDescripcion   = horarioDesc,
                HorarioTurno         = horarioTurno,
                EstadoAsistenciaHoy  = GetOut("p_estado_hoy"),
                HoraEntrada          = GetOut("p_entrada"),
                HoraSalida           = GetOut("p_salida"),
                EventoDescripcion    = GetOut("p_evento_desc"),
                EventoTipoCodigo     = GetOut("p_evento_tipo"),
                EventoFechas         = GetOut("p_evento_fechas"),
                EventoObservacion    = GetOut("p_evento_obs"),
                FuenteEvento         = GetOut("p_evento_fuente") ?? "SIG.RH_EVENTOS (EN VIVO)",
                VigilanciaEstado     = GetOut("p_vig_estado"),
                VigilanciaEntrada    = GetOut("p_vig_entrada"),
                VigilanciaSalida     = GetOut("p_vig_salida"),
                VigilanciaAlcohol    = GetOut("p_vig_alcohol"),
                VigilanciaCelular    = GetOut("p_vig_celular"),
                VigilanciaFuente     = GetOut("p_vig_fuente") ?? "SIG.SI_REGPERS (VIGILANCIA - TIEMPO REAL)",
                RangoVigilanciaDesde = GetOut("p_rango_vig_desde"),
                RangoVigilanciaHasta = GetOut("p_rango_vig_hasta"),
                RangoEventosDesde    = GetOut("p_rango_evt_desde"),
                RangoEventosHasta    = GetOut("p_rango_evt_hasta"),
                VigilanciaRegistros  = vigilanciaRegistros,
                EventosHistorial     = eventosHistorial,
                Compensaciones       = compensaciones,
                NotaSincronizacion   = GetOut("p_nota_sync"),
            };

            return (true, null, dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al buscar empleado: {Busqueda} ({Tipo})", busqueda, tipoBusqueda);
            return (false, "Error al consultar el empleado en Aquarius.", null);
        }
    }

    /// <summary>
    /// v1.7 — Autocompletado de búsqueda por Nombre. Consulta directa (no vía el SP grande)
    /// contra PLA_PERSONAL, mismo criterio que AQUARIUS.sp_SCA_Read_Personal_AutCom:
    /// solo activos (tip_estado='AC'), LIKE sobre "APELLIDOS, Nombre", máx. 20 filas.
    /// </summary>
    public async Task<List<SugerenciaEmpleadoDto>> SugerirNombresAsync(string texto)
    {
        var resultado = new List<SugerenciaEmpleadoDto>();

        await using var conn = new OracleConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.BindByName = true;
        cmd.CommandText =
            "SELECT * FROM ( " +
            "  SELECT p.cod_personal, p.cod_spring, " +
            "         LTRIM(RTRIM(p.ape_paterno)) || ' ' || LTRIM(RTRIM(p.ape_materno)) || ', ' || LTRIM(RTRIM(p.nom_trabajador)) AS nombre " +
            "  FROM PLA_PERSONAL p " +
            "  WHERE UPPER(LTRIM(RTRIM(p.ape_paterno)) || ' ' || LTRIM(RTRIM(p.ape_materno)) || ', ' || LTRIM(RTRIM(p.nom_trabajador))) LIKE '%' || UPPER(:texto) || '%' " +
            "    AND p.tip_estado = 'AC' " +
            "  ORDER BY LTRIM(RTRIM(p.ape_paterno)) || ' ' || LTRIM(RTRIM(p.ape_materno)) || ', ' || LTRIM(RTRIM(p.nom_trabajador)) " +
            ") WHERE ROWNUM <= 20";
        cmd.Parameters.Add(new OracleParameter("texto", OracleDbType.Varchar2) { Value = texto });

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            resultado.Add(new SugerenciaEmpleadoDto
            {
                CodPersonal = reader["cod_personal"] is DBNull ? null : reader["cod_personal"].ToString(),
                CodSpring   = reader["cod_spring"]   is DBNull ? null : reader["cod_spring"].ToString(),
                Nombre      = reader["nombre"]       is DBNull ? null : reader["nombre"].ToString(),
            });
        }

        return resultado;
    }
}
