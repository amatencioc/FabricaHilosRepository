using Dapper;
using FabricaHilos.Models.SaludOcupacional;
using Oracle.ManagedDataAccess.Client;

namespace FabricaHilos.Services.SaludOcupacional;

// ─────────────────────────────────────────────────────────────────────────────
// Interface
// ─────────────────────────────────────────────────────────────────────────────

public interface ISoInspeccionComService
{
    // Catálogo
    Task<IReadOnlyList<SoComedor>>    ObtenerCOMEDORESAsync();
    Task<IReadOnlyList<SoInspRubro>>  ObtenerRUBROSConItemsAsync();

    // Inspecciones
    Task<IReadOnlyList<SoInspeccion>> ListarInspeccionesAsync(int? idCom = null, string? estado = null, int top = 50);
    Task<SoInspeccion?>               ObtenerPorIdAsync(long idInsp);
    Task<long>                        CrearBorradorAsync(SoInspeccion insp, string usuario);
    Task                              ActualizarEncabezadoAsync(SoInspeccion insp, string usuario);
    Task                              CerrarInspeccionAsync(long idInsp, string usuario);
    Task                              AnularInspeccionAsync(long idInsp, string usuario);

    // Detalle checklist
    Task<IReadOnlyList<SoInspDetalle>> ObtenerDetalleAsync(long idInsp);
    Task                               GuardarDetalleAsync(SoInspDetalle detalle, string usuario);
    Task                               GuardarDetallesLoteAsync(IEnumerable<SoInspDetalle> detalles, string usuario);

    // Evidencias
    Task<IReadOnlyList<SoInspEvidencia>> ObtenerEvidenciasAsync(long idInsp);
    Task<long>                           AgregarEvidenciaAsync(SoInspEvidencia evidencia);
    Task                                 EliminarEvidenciaAsync(long idEvidencia);

    // Acciones correctivas
    Task<IReadOnlyList<SoInspAccion>> ObtenerAccionesInspeccionAsync(long idInsp);
    Task<IReadOnlyList<SoInspAccion>> ObtenerAccionesAbiertasAsync(int? idCom = null);
    Task<IReadOnlyList<SoInspAccion>> ObtenerAccionesResueltasAsync(int? idCom = null);
    Task<long>                        CrearAccionAsync(SoInspAccion accion, string usuario);
    Task                              ActualizarEstadoAccionAsync(long idAccion, string nuevoEstado, string? observacion, string usuario);

    // Dashboard KPIs
    Task<SoDashboardViewModel>        ObtenerDashboardAsync();
}

// ─────────────────────────────────────────────────────────────────────────────
// Implementación
// ─────────────────────────────────────────────────────────────────────────────

public class SoInspeccionComService : OracleServiceBase, ISoInspeccionComService
{
    private readonly ILogger<SoInspeccionComService> _logger;

    public SoInspeccionComService(
        IConfiguration       configuration,
        IHttpContextAccessor httpContextAccessor,
        ILogger<SoInspeccionComService> logger)
        : base(configuration, httpContextAccessor)
    {
        _logger = logger;
    }

    // ── Catálogo ─────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SoComedor>> ObtenerCOMEDORESAsync()
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        var rows = await conn.QueryAsync<SoComedor>($@"
            SELECT c.ID_COM, c.NOMBRE, c.UBICACION, c.ID_CONC,
                   k.NOMBRE AS NOMBRE_CONC, c.CAPACIDAD, c.TIPO, c.ESTADO
            FROM   {S}SO_COMEDOR c
            LEFT JOIN {S}SO_CONCESIONARIA k ON k.ID_CONC = c.ID_CONC
            WHERE  c.ESTADO = 'A'
            ORDER  BY c.NOMBRE");
        return rows.ToList();
    }

    public async Task<IReadOnlyList<SoInspRubro>> ObtenerRUBROSConItemsAsync()
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());

        var rubros = (await conn.QueryAsync<SoInspRubro>($@"
            SELECT ID_RUBRO, COD_RUBRO, NOMBRE, ORDEN, ICONO_BI
            FROM   {S}SO_INSP_RUBRO
            WHERE  ESTADO = 'A'
            ORDER  BY ORDEN")).ToList();

        var items = (await conn.QueryAsync<SoInspItem>($@"
            SELECT ID_ITEM, ID_RUBRO, COD_ITEM, DESCRIPCION, PTS_MAX, ORDEN
            FROM   {S}SO_INSP_ITEM
            WHERE  ESTADO = 'A'
            ORDER  BY ORDEN")).ToList();

        foreach (var r in rubros)
            r.Items = items.Where(i => i.IdRubro == r.IdRubro).ToList();

        return rubros;
    }

    // ── Inspecciones ─────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SoInspeccion>> ListarInspeccionesAsync(
        int? idCom = null, string? estado = null, int top = 50)
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        var sql = $@"
            SELECT * FROM (
                SELECT i.ID_INSP, i.ID_COM, c.NOMBRE AS NOMBRE_COMEDOR,
                       k.NOMBRE AS NOMBRE_CONC,
                       i.FECHA_INSP, i.HORA_INSP, i.ENCARGADA, i.INSPECTOR, i.MEDICO,
                       i.PTS_OBTENIDOS, i.PTS_MAXIMO, i.PCT_CUMPL, i.CALIFICACION,
                       i.ESTADO, i.FCH_CREA, i.USR_CREA, i.FCH_CIERRE
                FROM   {S}SO_INSPECCION i
                JOIN   {S}SO_COMEDOR        c ON c.ID_COM  = i.ID_COM
                LEFT JOIN {S}SO_CONCESIONARIA k ON k.ID_CONC = c.ID_CONC
                WHERE  (:idCom  IS NULL OR i.ID_COM = :idCom)
                  AND  (:estado IS NULL OR i.ESTADO  = :estado)
                ORDER  BY i.FECHA_INSP DESC, i.ID_INSP DESC
            ) WHERE ROWNUM <= :top";
        var rows = await conn.QueryAsync<SoInspeccion>(sql,
            new { idCom, estado, top });
        return rows.ToList();
    }

    public async Task<SoInspeccion?> ObtenerPorIdAsync(long idInsp)
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        return await conn.QueryFirstOrDefaultAsync<SoInspeccion>($@"
            SELECT i.ID_INSP, i.ID_COM, c.NOMBRE AS NOMBRE_COMEDOR,
                   k.NOMBRE AS NOMBRE_CONC,
                   i.FECHA_INSP, i.HORA_INSP, i.ENCARGADA, i.INSPECTOR, i.MEDICO,
                   i.PTS_OBTENIDOS, i.PTS_MAXIMO, i.PCT_CUMPL, i.CALIFICACION,
                   i.OBSERVACIONES, i.ESTADO, i.FCH_CREA, i.USR_CREA, i.FCH_CIERRE
            FROM   {S}SO_INSPECCION i
            JOIN   {S}SO_COMEDOR        c ON c.ID_COM  = i.ID_COM
            LEFT JOIN {S}SO_CONCESIONARIA k ON k.ID_CONC = c.ID_CONC
            WHERE  i.ID_INSP = :idInsp",
            new { idInsp });
    }

    public async Task<long> CrearBorradorAsync(SoInspeccion insp, string usuario)
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        var id = await conn.ExecuteScalarAsync<long>(
            $"SELECT {S}SO_INSPECCION_SEQ.NEXTVAL FROM DUAL");

        await conn.ExecuteAsync($@"
            INSERT INTO {S}SO_INSPECCION
                (ID_INSP, ID_COM, FECHA_INSP, HORA_INSP, ENCARGADA,
                 INSPECTOR, MEDICO, OBSERVACIONES, ESTADO, FCH_CREA, USR_CREA)
            VALUES
                (:id, :idCom, TO_DATE(:fecha,'DD/MM/YYYY'), :hora, :encargada,
                 :inspector, :medico, :obs, 'B', SYSDATE, :usr)",
            new
            {
                id,
                idCom      = insp.IdCom,
                fecha      = insp.FechaInsp.ToString("dd/MM/yyyy"),
                hora       = insp.HoraInsp,
                encargada  = insp.Encargada,
                inspector  = insp.Inspector,
                medico     = insp.Medico,
                obs        = insp.Observaciones,
                usr        = usuario
            });

        // Pre-inicializar detalle con todos los ítems activos en puntaje 0
        await conn.ExecuteAsync($@"
            INSERT INTO {S}SO_INSP_DETALLE (ID_DETALLE, ID_INSP, ID_ITEM, PUNTAJE)
            SELECT {S}SO_DETALLE_SEQ.NEXTVAL, :id, ID_ITEM, 0
            FROM   {S}SO_INSP_ITEM WHERE ESTADO = 'A'",
            new { id });

        _logger.LogInformation("[SO] Inspección {Id} creada como borrador por {User}", id, usuario);
        return id;
    }

    public async Task ActualizarEncabezadoAsync(SoInspeccion insp, string usuario)
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        var affected = await conn.ExecuteAsync($@"
            UPDATE {S}SO_INSPECCION
            SET    ID_COM       = :idCom,
                   FECHA_INSP   = TO_DATE(:fecha,'DD/MM/YYYY'),
                   HORA_INSP    = :hora,
                   ENCARGADA    = :encargada,
                   INSPECTOR    = :inspector,
                   MEDICO       = :medico,
                   OBSERVACIONES= :obs,
                   FCH_MOD      = SYSDATE,
                   USR_MOD      = :usr
            WHERE  ID_INSP = :id AND ESTADO = 'B'",
            new
            {
                idCom     = insp.IdCom,
                fecha     = insp.FechaInsp.ToString("dd/MM/yyyy"),
                hora      = insp.HoraInsp,
                encargada = insp.Encargada,
                inspector = insp.Inspector,
                medico    = insp.Medico,
                obs       = insp.Observaciones,
                usr       = usuario,
                id        = insp.IdInsp
            });

        if (affected == 0)
            throw new InvalidOperationException(
                "La inspección no existe o no está en estado Borrador y no puede modificarse.");
    }

    public async Task CerrarInspeccionAsync(long idInsp, string usuario)
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        // Verificar que todos los ítems con puntaje 0 y hallazgo tengan acción correctiva
        var sinAccion = await conn.ExecuteScalarAsync<int>($@"
            SELECT COUNT(*) FROM {S}SO_INSP_DETALLE d
            WHERE  d.ID_INSP  = :idInsp
              AND  d.PUNTAJE   = 0
              AND  d.HALLAZGO IS NOT NULL
              AND  d.TIENE_ACCION = 'N'",
            new { idInsp });

        if (sinAccion > 0)
            throw new InvalidOperationException(
                $"Hay {sinAccion} hallazgo(s) sin acción correctiva asignada. No se puede cerrar la inspección.");

        await conn.ExecuteAsync($@"
            UPDATE {S}SO_INSPECCION
            SET    ESTADO     = 'C',
                   FCH_CIERRE = SYSDATE,
                   USR_CIERRE = :usr,
                   FCH_MOD    = SYSDATE,
                   USR_MOD    = :usr
            WHERE  ID_INSP = :idInsp AND ESTADO = 'B'",
            new { usr = usuario, idInsp });

        _logger.LogInformation("[SO] Inspección {Id} cerrada por {User}", idInsp, usuario);
    }

    public async Task AnularInspeccionAsync(long idInsp, string usuario)
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        var affected = await conn.ExecuteAsync($@"
            UPDATE {S}SO_INSPECCION
            SET    ESTADO  = 'A',
                   FCH_MOD = SYSDATE,
                   USR_MOD = :usr
            WHERE  ID_INSP = :idInsp AND ESTADO = 'B'",
            new { usr = usuario, idInsp });

        if (affected == 0)
            throw new InvalidOperationException(
                "La inspección no existe o no está en estado Borrador y no puede anularse.");
    }

    // ── Detalle checklist ─────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SoInspDetalle>> ObtenerDetalleAsync(long idInsp)
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        var rows = await conn.QueryAsync<SoInspDetalle>($@"
            SELECT d.ID_DETALLE, d.ID_INSP, d.ID_ITEM,
                   t.COD_ITEM, t.DESCRIPCION, t.PTS_MAX,
                   d.PUNTAJE, d.HALLAZGO, d.RESPONSABLE, d.TIENE_ACCION,
                   r.ID_RUBRO, r.COD_RUBRO
            FROM   {S}SO_INSP_DETALLE d
            JOIN   {S}SO_INSP_ITEM    t ON t.ID_ITEM  = d.ID_ITEM
            JOIN   {S}SO_INSP_RUBRO   r ON r.ID_RUBRO = t.ID_RUBRO
            WHERE  d.ID_INSP = :idInsp
            ORDER  BY t.ORDEN",
            new { idInsp });
        return rows.ToList();
    }

    public async Task GuardarDetalleAsync(SoInspDetalle detalle, string usuario)
    {
        if (detalle.Puntaje is not (0 or 2 or 4))
            throw new ArgumentOutOfRangeException(nameof(detalle),
                "El puntaje solo puede ser 0, 2 o 4.");

        await using var conn = new OracleConnection(GetOracleConnectionString());
        var affected = await conn.ExecuteAsync($@"
            UPDATE {S}SO_INSP_DETALLE d
            SET    d.PUNTAJE     = :puntaje,
                   d.HALLAZGO    = :hallazgo,
                   d.RESPONSABLE = :resp
            WHERE  d.ID_DETALLE  = :id
              AND  EXISTS (
                       SELECT 1 FROM {S}SO_INSPECCION i
                       WHERE  i.ID_INSP = d.ID_INSP AND i.ESTADO = 'B')",
            new
            {
                puntaje  = detalle.Puntaje,
                hallazgo = detalle.Hallazgo,
                resp     = detalle.Responsable,
                id       = detalle.IdDetalle
            });

        if (affected == 0)
            throw new InvalidOperationException(
                "No se pudo guardar el puntaje: el ítem no existe o la inspección no está en estado Borrador.");
    }

    public async Task GuardarDetallesLoteAsync(IEnumerable<SoInspDetalle> detalles, string usuario)
    {
        var lista = detalles.ToList();
        foreach (var d in lista)
            if (d.Puntaje is not (0 or 2 or 4))
                throw new ArgumentOutOfRangeException(nameof(detalles),
                    $"Puntaje inválido ({d.Puntaje}) en detalle {d.IdDetalle}. Solo se permiten 0, 2 o 4.");

        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();
        try
        {
            foreach (var d in lista)
            {
                await conn.ExecuteAsync($@"
                    UPDATE {S}SO_INSP_DETALLE d
                    SET    d.PUNTAJE     = :puntaje,
                           d.HALLAZGO    = :hallazgo,
                           d.RESPONSABLE = :resp
                    WHERE  d.ID_DETALLE  = :id
                      AND  EXISTS (
                               SELECT 1 FROM {S}SO_INSPECCION i
                               WHERE  i.ID_INSP = d.ID_INSP AND i.ESTADO = 'B')",
                    new
                    {
                        puntaje  = d.Puntaje,
                        hallazgo = d.Hallazgo,
                        resp     = d.Responsable,
                        id       = d.IdDetalle
                    }, tx);
            }
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    // ── Evidencias ────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SoInspEvidencia>> ObtenerEvidenciasAsync(long idInsp)
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        var rows = await conn.QueryAsync<SoInspEvidencia>($@"
            SELECT ID_EVIDENCIA, ID_DETALLE, ID_INSP,
                   NOMBRE_ARCH, RUTA_ARCH, DESCRIPCION, FCH_CARGA, USUARIO
            FROM   {S}SO_INSP_EVIDENCIA
            WHERE  ID_INSP = :idInsp
            ORDER  BY FCH_CARGA",
            new { idInsp });
        return rows.ToList();
    }

    public async Task<long> AgregarEvidenciaAsync(SoInspEvidencia evidencia)
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        var id = await conn.ExecuteScalarAsync<long>(
            $"SELECT {S}SO_EVIDENCIA_SEQ.NEXTVAL FROM DUAL");

        await conn.ExecuteAsync($@"
            INSERT INTO {S}SO_INSP_EVIDENCIA
                (ID_EVIDENCIA, ID_DETALLE, ID_INSP, NOMBRE_ARCH, RUTA_ARCH,
                 DESCRIPCION, FCH_CARGA, USUARIO)
            VALUES
                (:id, :detalle, :insp, :nombre, :ruta,
                 :desc, SYSDATE, :usr)",
            new
            {
                id      = id,
                detalle = evidencia.IdDetalle,
                insp    = evidencia.IdInsp,
                nombre  = evidencia.NombreArch,
                ruta    = evidencia.RutaArch,
                desc    = evidencia.Descripcion,
                usr     = evidencia.Usuario
            });

        return id;
    }

    public async Task EliminarEvidenciaAsync(long idEvidencia)
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.ExecuteAsync(
            $"DELETE FROM {S}SO_INSP_EVIDENCIA WHERE ID_EVIDENCIA = :id",
            new { id = idEvidencia });
    }

    // ── Acciones Correctivas ──────────────────────────────────────────────────

    public async Task<IReadOnlyList<SoInspAccion>> ObtenerAccionesInspeccionAsync(long idInsp)
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        var rows = await conn.QueryAsync<SoInspAccion>($@"
            SELECT a.ID_ACCION, a.ID_DETALLE, a.ID_INSP,
                   a.DESCRIPCION, a.RESPONSABLE, a.FCH_LIMITE, a.FCH_CIERRE,
                   a.ESTADO, a.OBSERVACION, a.USUARIO_CIERRE, a.FCH_CREA,
                   t.COD_ITEM, t.DESCRIPCION AS DESC_ITEM,
                   c.NOMBRE AS NOMBRE_COMEDOR, i.FECHA_INSP
            FROM   {S}SO_INSP_ACCION    a
            JOIN   {S}SO_INSP_DETALLE   d ON d.ID_DETALLE = a.ID_DETALLE
            JOIN   {S}SO_INSP_ITEM      t ON t.ID_ITEM    = d.ID_ITEM
            JOIN   {S}SO_INSPECCION     i ON i.ID_INSP    = a.ID_INSP
            JOIN   {S}SO_COMEDOR        c ON c.ID_COM     = i.ID_COM
            WHERE  a.ID_INSP = :idInsp
            ORDER  BY a.FCH_CREA",
            new { idInsp });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<SoInspAccion>> ObtenerAccionesAbiertasAsync(int? idCom = null)
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        var rows = await conn.QueryAsync<SoInspAccion>($@"
            SELECT a.ID_ACCION, a.ID_DETALLE, a.ID_INSP,
                   a.DESCRIPCION, a.RESPONSABLE, a.FCH_LIMITE, a.FCH_CIERRE,
                   a.ESTADO, a.OBSERVACION, a.FCH_CREA,
                   t.COD_ITEM, t.DESCRIPCION AS DESC_ITEM,
                   c.NOMBRE AS NOMBRE_COMEDOR, i.FECHA_INSP
            FROM   {S}SO_INSP_ACCION  a
            JOIN   {S}SO_INSP_DETALLE d ON d.ID_DETALLE = a.ID_DETALLE
            JOIN   {S}SO_INSP_ITEM    t ON t.ID_ITEM    = d.ID_ITEM
            JOIN   {S}SO_INSPECCION   i ON i.ID_INSP    = a.ID_INSP
            JOIN   {S}SO_COMEDOR      c ON c.ID_COM     = i.ID_COM
            WHERE  a.ESTADO IN ('P','E')
              AND  (:idCom IS NULL OR i.ID_COM = :idCom)
            ORDER  BY DECODE(a.ESTADO,'P',1,'E',2), a.FCH_LIMITE NULLS LAST",
            new { idCom });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<SoInspAccion>> ObtenerAccionesResueltasAsync(int? idCom = null)
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        var rows = await conn.QueryAsync<SoInspAccion>($@"
            SELECT a.ID_ACCION, a.ID_DETALLE, a.ID_INSP,
                   a.DESCRIPCION, a.RESPONSABLE, a.FCH_LIMITE, a.FCH_CIERRE,
                   a.ESTADO, a.OBSERVACION, a.USUARIO_CIERRE, a.FCH_CREA,
                   t.COD_ITEM, t.DESCRIPCION AS DESC_ITEM,
                   c.NOMBRE AS NOMBRE_COMEDOR, i.FECHA_INSP
            FROM   {S}SO_INSP_ACCION  a
            JOIN   {S}SO_INSP_DETALLE d ON d.ID_DETALLE = a.ID_DETALLE
            JOIN   {S}SO_INSP_ITEM    t ON t.ID_ITEM    = d.ID_ITEM
            JOIN   {S}SO_INSPECCION   i ON i.ID_INSP    = a.ID_INSP
            JOIN   {S}SO_COMEDOR      c ON c.ID_COM     = i.ID_COM
            WHERE  a.ESTADO = 'R'
              AND  (:idCom IS NULL OR i.ID_COM = :idCom)
            ORDER  BY a.FCH_CIERRE DESC NULLS LAST",
            new { idCom });
        return rows.ToList();
    }

    public async Task<long> CrearAccionAsync(SoInspAccion accion, string usuario)
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        var id = await conn.ExecuteScalarAsync<long>(
            $"SELECT {S}SO_ACCION_SEQ.NEXTVAL FROM DUAL");

        await conn.ExecuteAsync($@"
            INSERT INTO {S}SO_INSP_ACCION
                (ID_ACCION, ID_DETALLE, ID_INSP, DESCRIPCION, RESPONSABLE,
                 FCH_LIMITE, ESTADO, FCH_CREA, USR_CREA)
            VALUES
                (:id, :detalle, :insp, :desc, :resp,
                 TO_DATE(:limite,'DD/MM/YYYY'), 'P', SYSDATE, :usr)",
            new
            {
                id      = id,
                detalle = accion.IdDetalle,
                insp    = accion.IdInsp,
                desc    = accion.Descripcion,
                resp    = accion.Responsable,
                limite  = accion.FchLimite?.ToString("dd/MM/yyyy"),
                usr     = usuario
            });

        _logger.LogInformation("[SO] Acción correctiva {Id} creada para inspección {Insp} por {User}",
            id, accion.IdInsp, usuario);
        return id;
    }

    public async Task ActualizarEstadoAccionAsync(
        long idAccion, string nuevoEstado, string? observacion, string usuario)
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.ExecuteAsync($@"
            UPDATE {S}SO_INSP_ACCION
            SET    ESTADO         = :estado,
                   OBSERVACION    = :obs,
                   FCH_CIERRE     = CASE WHEN :estado = 'R' THEN SYSDATE ELSE NULL END,
                   USUARIO_CIERRE = CASE WHEN :estado = 'R' THEN :usr    ELSE NULL END
            WHERE  ID_ACCION = :id",
            new { estado = nuevoEstado, obs = observacion, usr = usuario, id = idAccion });
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    public async Task<SoDashboardViewModel> ObtenerDashboardAsync()
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();

        var ultimas = (await conn.QueryAsync<SoInspeccion>($@"
            SELECT * FROM (
                SELECT i.ID_INSP, i.ID_COM, c.NOMBRE AS NOMBRE_COMEDOR,
                       k.NOMBRE AS NOMBRE_CONC,
                       i.FECHA_INSP, i.INSPECTOR, i.PTS_OBTENIDOS,
                       i.PTS_MAXIMO, i.PCT_CUMPL, i.CALIFICACION,
                       i.ESTADO, i.FCH_CREA
                FROM   {S}SO_INSPECCION   i
                JOIN   {S}SO_COMEDOR      c ON c.ID_COM  = i.ID_COM
                LEFT JOIN {S}SO_CONCESIONARIA k ON k.ID_CONC = c.ID_CONC
                WHERE  i.ESTADO IN ('C','B')
                ORDER  BY i.FECHA_INSP DESC
            ) WHERE ROWNUM <= 10")).ToList();

        var acciones = (await conn.QueryAsync<SoInspAccion>($@"
            SELECT a.ID_ACCION, a.ID_DETALLE, a.ID_INSP,
                   a.DESCRIPCION, a.RESPONSABLE, a.FCH_LIMITE, a.FCH_CIERRE,
                   a.ESTADO, a.OBSERVACION, a.FCH_CREA,
                   t.COD_ITEM, t.DESCRIPCION AS DESC_ITEM,
                   c.NOMBRE AS NOMBRE_COMEDOR, i.FECHA_INSP
            FROM   {S}SO_INSP_ACCION  a
            JOIN   {S}SO_INSP_DETALLE d ON d.ID_DETALLE = a.ID_DETALLE
            JOIN   {S}SO_INSP_ITEM    t ON t.ID_ITEM    = d.ID_ITEM
            JOIN   {S}SO_INSPECCION   i ON i.ID_INSP    = a.ID_INSP
            JOIN   {S}SO_COMEDOR      c ON c.ID_COM     = i.ID_COM
            WHERE  a.ESTADO IN ('P','E')
            ORDER  BY DECODE(a.ESTADO,'P',1,'E',2), a.FCH_LIMITE NULLS LAST")).ToList();

        var comedores = (await conn.QueryAsync<SoComedor>($@"
            SELECT c.ID_COM, c.NOMBRE, c.UBICACION, c.ID_CONC,
                   k.NOMBRE AS NOMBRE_CONC, c.CAPACIDAD, c.TIPO, c.ESTADO
            FROM   {S}SO_COMEDOR c
            LEFT JOIN {S}SO_CONCESIONARIA k ON k.ID_CONC = c.ID_CONC
            WHERE  c.ESTADO = 'A'
            ORDER  BY c.NOMBRE")).ToList();

        var kpis = await conn.QueryFirstAsync($@"
            SELECT SUM(1) AS TOTAL_INSP,
                   SUM(CASE WHEN EXTRACT(YEAR FROM FECHA_INSP) = EXTRACT(YEAR FROM SYSDATE)
                            THEN 1 ELSE 0 END) AS INSP_ANO
            FROM   {S}SO_INSPECCION
            WHERE  ESTADO != 'A'");

        var ultima = ultimas.FirstOrDefault(x => x.Estado == "C");

        return new SoDashboardViewModel
        {
            UltimasInspecciones   = ultimas,
            AccionesPendientes    = acciones,
            Comedores             = comedores,
            TotalInspecciones     = (int)(kpis.TOTAL_INSP ?? 0),
            InspeccionesEsteAno   = (int)(kpis.INSP_ANO  ?? 0),
            AccionesPend          = acciones.Count(a => a.Estado == "P"),
            AccionesVencidas      = acciones.Count(a => a.EsVencida),
            UltimoPctCumpl        = ultima?.PctCumpl,
            UltimaCalificacion    = ultima?.Calificacion
        };
    }
}
