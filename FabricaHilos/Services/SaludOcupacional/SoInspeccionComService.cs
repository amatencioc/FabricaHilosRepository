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
    Task                              GuardarObservacionAsync(long idInsp, string? observacion, string usuario);

    // Detalle checklist
    Task<IReadOnlyList<SoInspDetalle>> ObtenerDetalleAsync(long idInsp);
    Task                               GuardarDetalleAsync(SoInspDetalle detalle, string usuario);
    Task                               GuardarDetallesLoteAsync(IEnumerable<SoInspDetalle> detalles, string usuario);

    // Evidencias
    Task<IReadOnlyList<SoInspEvidencia>> ObtenerEvidenciasAsync(long idInsp);
    Task<SoInspEvidencia?>               ObtenerEvidenciaPorIdAsync(long idEvidencia);
    Task<long>                           AgregarEvidenciaAsync(SoInspEvidencia evidencia);
    Task                                 EliminarEvidenciaAsync(long idEvidencia);

    // Acciones correctivas
    Task<IReadOnlyList<SoInspAccion>> ObtenerAccionesInspeccionAsync(long idInsp);
    Task<IReadOnlyList<SoInspAccion>> ObtenerAccionesAbiertasAsync(int? idCom = null);
    Task<IReadOnlyList<SoInspAccion>> ObtenerAccionesResueltasAsync(int? idCom = null);
    Task<long>                        CrearAccionAsync(SoInspAccion accion, string usuario);
    Task                              ActualizarEstadoAccionAsync(long idAccion, string nuevoEstado, string? observacion, string usuario);
    Task                              ActualizarEstadoHallazgoAsync(long idHallazgo, string nuevoEstado, string? observacion, string usuario);

    // Hallazgos e Informe
    Task<IReadOnlyList<SoHallazgo>> ObtenerHallazgosAsync(long idInsp);
    Task<long>                      GuardarHallazgoAsync(SoHallazgo h, string usuario);
    Task                            ActualizarHallazgoAsync(SoHallazgo h, string usuario);
    Task                            EliminarHallazgoAsync(long idHallazgo);
    Task<long>                      AgregarImgHallazgoAsync(SoHallazgoImg img);
    Task<SoHallazgoImg?>            ObtenerImgHallazgoPorIdAsync(long idImg);
    Task                            EliminarImgHallazgoAsync(long idImg);

    // Dashboard KPIs
    Task<SoDashboardViewModel>        ObtenerDashboardAsync();

    // Clasificación de hallazgos + Personal notificado (Mantenimiento / Servicios Generales / Orden y Limpieza)
    Task<IReadOnlyList<SoPersonalClasif>> ObtenerPersonalClasifAsync(string? codClasif = null, bool soloActivos = true);
    Task<long>                            AsignarPersonalAsync(SoPersonalClasif p, string usuario);
    Task                                  QuitarPersonalAsync(long idPersonal, string usuario);
    Task<List<SoEmpleadoBusqueda>>        BuscarEmpleadosAsync(string term, int take = 20);
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
                       k.NOMBRE AS NOMBRE_CONC, k.CONTACTO AS CONTACTO_CONC,
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
                   k.NOMBRE AS NOMBRE_CONC, k.CONTACTO AS CONTACTO_CONC,
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
        await conn.OpenAsync();
        var id = await conn.ExecuteScalarAsync<long>(
            $"SELECT {S}SO_INSPECCION_SEQ.NEXTVAL FROM DUAL");

        // Todo el borrador (encabezado + ítems de checklist) debe ser atómico.
        // Si falla cualquier INSERT de detalle se hace ROLLBACK del encabezado.
        await using var tx = conn.BeginTransaction();
        try
        {
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
                }, tx);

            // Pre-inicializar detalle con todos los ítems activos en puntaje 0.
            // Se inserta fila por fila para evitar ORA-04091 (tabla mutante) que
            // se produce cuando el trigger TRG_SO_DET_UPD_INSP lee SO_INSP_DETALLE
            // dentro de un INSERT-SELECT masivo sobre esa misma tabla.
            var items = (await conn.QueryAsync<long>(
                $"SELECT ID_ITEM FROM {S}SO_INSP_ITEM WHERE ESTADO = 'A'",
                transaction: tx)).AsList();

            foreach (var idItem in items)
            {
                var idDetalle = await conn.ExecuteScalarAsync<long>(
                    $"SELECT {S}SO_DETALLE_SEQ.NEXTVAL FROM DUAL", transaction: tx);

                await conn.ExecuteAsync(
                    $"INSERT INTO {S}SO_INSP_DETALLE (ID_DETALLE, ID_INSP, ID_ITEM, PUNTAJE) " +
                    $"VALUES (:idDet, :idInsp, :idItem, 0)",
                    new { idDet = idDetalle, idInsp = id, idItem }, tx);
            }

            tx.Commit();
            _logger.LogInformation("[SO] Inspección {Id} creada como borrador por {User} ({N} ítems)", id, usuario, items.Count);
            return id;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
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

    public async Task GuardarObservacionAsync(long idInsp, string? observacion, string usuario)
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.ExecuteAsync($@"
            UPDATE {S}SO_INSPECCION
            SET    OBSERVACIONES = :obs,
                   FCH_MOD       = SYSDATE,
                   USR_MOD       = :usr
            WHERE  ID_INSP = :id AND ESTADO = 'B'",
            new { obs = observacion, usr = usuario, id = idInsp });
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
        await using var conn = new OracleConnection(GetOracleConnectionString());

        // El PTS_MAX no viaja de forma confiable desde el front (solo Puntaje/IdDetalle/IdItem),
        // así que se obtiene el máximo real del ítem en BD para validar correctamente
        // (evita aceptar, por ejemplo, Puntaje=4 en un ítem cuyo máximo es 2).
        int? ptsMax = detalle.IdDetalle > 0
            ? await conn.ExecuteScalarAsync<int?>($@"
                SELECT t.PTS_MAX
                FROM   {S}SO_INSP_DETALLE d
                JOIN   {S}SO_INSP_ITEM    t ON t.ID_ITEM = d.ID_ITEM
                WHERE  d.ID_DETALLE = :id",
                new { id = detalle.IdDetalle })
            : detalle.IdItem > 0
                ? await conn.ExecuteScalarAsync<int?>(
                    $"SELECT PTS_MAX FROM {S}SO_INSP_ITEM WHERE ID_ITEM = :idItem",
                    new { idItem = detalle.IdItem })
                : null;

        if (ptsMax is null)
            throw new InvalidOperationException(
                "No se pudo validar el puntaje: el ítem no existe.");

        if (detalle.Puntaje != 0 && detalle.Puntaje != ptsMax)
            throw new ArgumentOutOfRangeException(nameof(detalle),
                $"El puntaje solo puede ser 0 o {ptsMax}.");

        int affected;

        if (detalle.IdDetalle > 0)
        {
            // Ruta normal: actualizar por ID_DETALLE
            affected = await conn.ExecuteAsync($@"
                UPDATE {S}SO_INSP_DETALLE d
                SET    d.PUNTAJE     = :puntaje,
                       d.HALLAZGO    = NVL(:hallazgo, d.HALLAZGO),
                       d.RESPONSABLE = NVL(:resp, d.RESPONSABLE)
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
        }
        else if (detalle.IdInsp > 0 && detalle.IdItem > 0)
        {
            // Fallback: el front no tenía IdDetalle aún; usar clave natural ID_INSP+ID_ITEM
            affected = await conn.ExecuteAsync($@"
                UPDATE {S}SO_INSP_DETALLE d
                SET    d.PUNTAJE     = :puntaje,
                       d.HALLAZGO    = NVL(:hallazgo, d.HALLAZGO),
                       d.RESPONSABLE = NVL(:resp, d.RESPONSABLE)
                WHERE  d.ID_INSP     = :idInsp
                  AND  d.ID_ITEM     = :idItem
                  AND  EXISTS (
                           SELECT 1 FROM {S}SO_INSPECCION i
                           WHERE  i.ID_INSP = d.ID_INSP AND i.ESTADO = 'B')",
                new
                {
                    puntaje  = detalle.Puntaje,
                    hallazgo = detalle.Hallazgo,
                    resp     = detalle.Responsable,
                    idInsp   = detalle.IdInsp,
                    idItem   = detalle.IdItem
                });

            // Si no existía la fila (borrador creado antes de que el ítem fuera añadido),
            // intentar insertar el detalle faltante.
            if (affected == 0)
            {
                // Verificar que la inspección exista y esté en Borrador
                var inspExiste = await conn.ExecuteScalarAsync<int>($@"
                    SELECT COUNT(*) FROM {S}SO_INSPECCION
                    WHERE  ID_INSP = :idInsp AND ESTADO = 'B'",
                    new { idInsp = detalle.IdInsp });

                if (inspExiste == 0)
                    throw new InvalidOperationException(
                        "No se pudo guardar el puntaje: la inspección no existe o no está en estado Borrador.");

                var idDetNuevo = await conn.ExecuteScalarAsync<long>(
                    $"SELECT {S}SO_DETALLE_SEQ.NEXTVAL FROM DUAL");

                await conn.ExecuteAsync(
                    $"INSERT INTO {S}SO_INSP_DETALLE (ID_DETALLE, ID_INSP, ID_ITEM, PUNTAJE) " +
                    $"VALUES (:idDet, :idInsp, :idItem, :puntaje)",
                    new
                    {
                        idDet   = idDetNuevo,
                        idInsp  = detalle.IdInsp,
                        idItem  = detalle.IdItem,
                        puntaje = detalle.Puntaje
                    });
                affected = 1;
                _logger.LogWarning("[SO] Detalle faltante insertado: IdInsp={IdInsp} IdItem={IdItem} IdDetalle={IdDet}",
                    detalle.IdInsp, detalle.IdItem, idDetNuevo);
            }
        }
        else
        {
            throw new InvalidOperationException(
                "No se puede guardar el puntaje: se requiere IdDetalle > 0 o bien IdInsp + IdItem > 0.");
        }

        if (affected == 0)
            throw new InvalidOperationException(
                "No se pudo guardar el puntaje: el ítem no existe o la inspección no está en estado Borrador.");
    }

    public async Task GuardarDetallesLoteAsync(IEnumerable<SoInspDetalle> detalles, string usuario)
    {
        var lista = detalles.ToList();
        if (lista.Count == 0) return;

        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();

        // Obtener en un solo viaje el PTS_MAX real de cada ítem involucrado (por IdItem
        // o, si no viene, resolviéndolo a partir del IdDetalle) para validar correctamente
        // en vez del hardcode 0/2/4, que no contempla ítems con distinto puntaje máximo.
        var idsItem = lista.Where(d => d.IdItem > 0).Select(d => (long)d.IdItem).Distinct().ToList();
        var idsDetalle = lista.Where(d => d.IdItem <= 0 && d.IdDetalle > 0).Select(d => d.IdDetalle).Distinct().ToList();

        var ptsMaxPorItem = idsItem.Count == 0
            ? new Dictionary<long, int>()
            : (await conn.QueryAsync<(long IdItem, int PtsMax)>(
                $"SELECT ID_ITEM, PTS_MAX FROM {S}SO_INSP_ITEM WHERE ID_ITEM IN :ids",
                new { ids = idsItem }))
                .ToDictionary(x => x.IdItem, x => x.PtsMax);

        var ptsMaxPorDetalle = idsDetalle.Count == 0
            ? new Dictionary<long, int>()
            : (await conn.QueryAsync<(long IdDetalle, int PtsMax)>($@"
                SELECT d.ID_DETALLE, t.PTS_MAX
                FROM   {S}SO_INSP_DETALLE d
                JOIN   {S}SO_INSP_ITEM    t ON t.ID_ITEM = d.ID_ITEM
                WHERE  d.ID_DETALLE IN :ids",
                new { ids = idsDetalle }))
                .ToDictionary(x => x.IdDetalle, x => x.PtsMax);

        foreach (var d in lista)
        {
            int? ptsMax = d.IdItem > 0
                ? ptsMaxPorItem.GetValueOrDefault(d.IdItem, -1) is var pm && pm >= 0 ? pm : null
                : ptsMaxPorDetalle.TryGetValue(d.IdDetalle, out var pd) ? pd : null;

            if (ptsMax is null)
                throw new InvalidOperationException(
                    $"No se pudo validar el puntaje: el ítem del detalle {d.IdDetalle} no existe.");

            if (d.Puntaje != 0 && d.Puntaje != ptsMax)
                throw new ArgumentOutOfRangeException(nameof(detalles),
                    $"Puntaje inválido ({d.Puntaje}) en detalle {d.IdDetalle}. Solo se permite 0 o {ptsMax}.");
        }

        await using var tx = conn.BeginTransaction();
        try
        {
            foreach (var d in lista)
            {
                if (d.IdDetalle > 0)
                {
                    await conn.ExecuteAsync($@"
                        UPDATE {S}SO_INSP_DETALLE dd
                        SET    dd.PUNTAJE     = :puntaje,
                               dd.HALLAZGO    = NVL(:hallazgo, dd.HALLAZGO),
                               dd.RESPONSABLE = NVL(:resp, dd.RESPONSABLE)
                        WHERE  dd.ID_DETALLE  = :id
                          AND  EXISTS (
                                   SELECT 1 FROM {S}SO_INSPECCION i
                                   WHERE  i.ID_INSP = dd.ID_INSP AND i.ESTADO = 'B')",
                        new
                        {
                            puntaje  = d.Puntaje,
                            hallazgo = d.Hallazgo,
                            resp     = d.Responsable,
                            id       = d.IdDetalle
                        }, tx);
                }
                else if (d.IdInsp > 0 && d.IdItem > 0)
                {
                    // Fila aún no cargada en el front: usar IdInsp+IdItem como clave
                    int n = await conn.ExecuteAsync($@"
                        UPDATE {S}SO_INSP_DETALLE dd
                        SET    dd.PUNTAJE     = :puntaje,
                               dd.HALLAZGO    = NVL(:hallazgo, dd.HALLAZGO),
                               dd.RESPONSABLE = NVL(:resp, dd.RESPONSABLE)
                        WHERE  dd.ID_INSP     = :idInsp
                          AND  dd.ID_ITEM     = :idItem
                          AND  EXISTS (
                                   SELECT 1 FROM {S}SO_INSPECCION i
                                   WHERE  i.ID_INSP = dd.ID_INSP AND i.ESTADO = 'B')",
                        new
                        {
                            puntaje  = d.Puntaje,
                            hallazgo = d.Hallazgo,
                            resp     = d.Responsable,
                            idInsp   = d.IdInsp,
                            idItem   = d.IdItem
                        }, tx);

                    // Si la fila no existía, insertarla (detalle faltante en borrador antiguo)
                    if (n == 0)
                    {
                        var idDetNuevo = await conn.ExecuteScalarAsync<long>(
                            $"SELECT {S}SO_DETALLE_SEQ.NEXTVAL FROM DUAL", transaction: tx);
                        await conn.ExecuteAsync(
                            $"INSERT INTO {S}SO_INSP_DETALLE (ID_DETALLE, ID_INSP, ID_ITEM, PUNTAJE) " +
                            $"VALUES (:idDet, :idInsp, :idItem, :puntaje)",
                            new { idDet = idDetNuevo, idInsp = d.IdInsp, idItem = d.IdItem, puntaje = d.Puntaje },
                            tx);
                    }
                }
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

    public async Task<SoInspEvidencia?> ObtenerEvidenciaPorIdAsync(long idEvidencia)
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        return await conn.QueryFirstOrDefaultAsync<SoInspEvidencia>($@"
            SELECT ID_EVIDENCIA, ID_DETALLE, ID_INSP,
                   NOMBRE_ARCH, RUTA_ARCH, DESCRIPCION, FCH_CARGA, USUARIO
            FROM   {S}SO_INSP_EVIDENCIA
            WHERE  ID_EVIDENCIA = :id",
            new { id = idEvidencia });
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
            SELECT ID_ACCION, ID_DETALLE, ID_INSP,
                   DESCRIPCION, RESPONSABLE, FCH_LIMITE, FCH_CIERRE,
                   ESTADO, OBSERVACION, FCH_CREA,
                   COD_ITEM, DESC_ITEM, NOMBRE_COMEDOR, FECHA_INSP,
                   ORD_ESTADO
            FROM (
                SELECT a.ID_ACCION, a.ID_DETALLE, a.ID_INSP,
                       a.DESCRIPCION, a.RESPONSABLE, a.FCH_LIMITE, a.FCH_CIERRE,
                       a.ESTADO, a.OBSERVACION, a.FCH_CREA,
                       t.COD_ITEM, t.DESCRIPCION AS DESC_ITEM,
                       c.NOMBRE AS NOMBRE_COMEDOR, i.FECHA_INSP,
                       DECODE(a.ESTADO,'P',1,'E',2,3) AS ORD_ESTADO
                FROM   {S}SO_INSP_ACCION  a
                JOIN   {S}SO_INSP_DETALLE d ON d.ID_DETALLE = a.ID_DETALLE
                JOIN   {S}SO_INSP_ITEM    t ON t.ID_ITEM    = d.ID_ITEM
                JOIN   {S}SO_INSPECCION   i ON i.ID_INSP    = a.ID_INSP
                JOIN   {S}SO_COMEDOR      c ON c.ID_COM     = i.ID_COM
                WHERE  a.ESTADO IN ('P','E')
                  AND  (:idCom IS NULL OR i.ID_COM = :idCom)
                UNION ALL
                SELECT h.ID_HALLAZGO, 0, h.ID_INSP,
                       h.ACCION_CORR, NULL, h.FCH_LIMITE, NULL,
                       h.ESTADO, h.OBS_SEGUIM, h.FCH_CREA,
                       NULL, h.DESCRIPCION AS DESC_ITEM,
                       c.NOMBRE AS NOMBRE_COMEDOR, i.FECHA_INSP,
                       DECODE(h.ESTADO,'P',1,'E',2,3) AS ORD_ESTADO
                FROM   {S}SO_INSP_HALLAZGO h
                JOIN   {S}SO_INSPECCION    i ON i.ID_INSP = h.ID_INSP
                JOIN   {S}SO_COMEDOR       c ON c.ID_COM  = i.ID_COM
                WHERE  h.ACCION_CORR IS NOT NULL
                  AND  h.ESTADO IN ('P','E')
                  AND  (:idCom IS NULL OR i.ID_COM = :idCom)
            )
            ORDER BY 14 DESC, 15, 10",
            new { idCom });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<SoInspAccion>> ObtenerAccionesResueltasAsync(int? idCom = null)
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        var rows = await conn.QueryAsync<SoInspAccion>($@"
            SELECT ID_ACCION, ID_DETALLE, ID_INSP,
                   DESCRIPCION, RESPONSABLE, FCH_LIMITE, FCH_CIERRE,
                   ESTADO, OBSERVACION, FCH_CREA,
                   COD_ITEM, DESC_ITEM, NOMBRE_COMEDOR, FECHA_INSP
            FROM (
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
                WHERE  a.ESTADO = 'R'
                  AND  (:idCom IS NULL OR i.ID_COM = :idCom)
                UNION ALL
                SELECT h.ID_HALLAZGO, 0, h.ID_INSP,
                       h.ACCION_CORR, NULL, h.FCH_LIMITE, h.FCH_RESOL,
                       h.ESTADO, h.OBS_SEGUIM, h.FCH_CREA,
                       NULL, h.DESCRIPCION AS DESC_ITEM,
                       c.NOMBRE AS NOMBRE_COMEDOR, i.FECHA_INSP
                FROM   {S}SO_INSP_HALLAZGO h
                JOIN   {S}SO_INSPECCION    i ON i.ID_INSP = h.ID_INSP
                JOIN   {S}SO_COMEDOR       c ON c.ID_COM  = i.ID_COM
                WHERE  h.ACCION_CORR IS NOT NULL
                  AND  h.ESTADO IN ('R','V')
                  AND  (:idCom IS NULL OR i.ID_COM = :idCom)
            )
            ORDER BY 14 DESC, 10",
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

    public async Task ActualizarEstadoHallazgoAsync(
        long idHallazgo, string nuevoEstado, string? observacion, string usuario)
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.ExecuteAsync($@"
            UPDATE {S}SO_INSP_HALLAZGO
            SET    ESTADO      = :estado,
                   OBS_SEGUIM  = :obs,
                   FCH_RESOL   = CASE WHEN :estado IN ('R','V') THEN SYSDATE ELSE NULL END
            WHERE  ID_HALLAZGO = :id",
            new { estado = nuevoEstado, obs = observacion, id = idHallazgo });
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    public async Task<SoDashboardViewModel> ObtenerDashboardAsync()
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();

        var ultimas = (await conn.QueryAsync<SoInspeccion>($@"
            SELECT * FROM (
                SELECT i.ID_INSP, i.ID_COM, c.NOMBRE AS NOMBRE_COMEDOR,
                       k.NOMBRE AS NOMBRE_CONC, k.CONTACTO AS CONTACTO_CONC,
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
            SELECT ID_ACCION, ID_DETALLE, ID_INSP,
                   DESCRIPCION, RESPONSABLE, FCH_LIMITE, FCH_CIERRE,
                   ESTADO, OBSERVACION, FCH_CREA,
                   COD_ITEM, DESC_ITEM,
                   NOMBRE_COMEDOR, FECHA_INSP
            FROM (
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
                UNION ALL
                SELECT h.ID_HALLAZGO, 0, h.ID_INSP,
                       h.ACCION_CORR, NULL, h.FCH_LIMITE, NULL,
                       h.ESTADO, h.OBS_SEGUIM, h.FCH_CREA,
                       NULL, h.DESCRIPCION AS DESC_ITEM,
                       c.NOMBRE AS NOMBRE_COMEDOR, i.FECHA_INSP
                FROM   {S}SO_INSP_HALLAZGO h
                JOIN   {S}SO_INSPECCION    i ON i.ID_INSP = h.ID_INSP
                JOIN   {S}SO_COMEDOR       c ON c.ID_COM  = i.ID_COM
                WHERE  h.ACCION_CORR IS NOT NULL
            )
            ORDER BY 14 DESC, 6 NULLS LAST")).ToList();
        var totalAbiertas = acciones.Count(a => a.Estado == "P");

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
            AccionesRecientes     = acciones,
            Comedores             = comedores,
            TotalInspecciones     = (int)(kpis.TOTAL_INSP ?? 0),
            InspeccionesEsteAno   = (int)(kpis.INSP_ANO  ?? 0),
            AccionesPend          = totalAbiertas,
            AccionesVencidas      = acciones.Count(a => a.EsVencida),
            UltimoPctCumpl        = ultima?.PctCumpl,
            UltimaCalificacion    = ultima?.Calificacion
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Hallazgos
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SoHallazgo>> ObtenerHallazgosAsync(long idInsp)
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        var hallazgos = (await conn.QueryAsync<SoHallazgo>($@"
            SELECT h.ID_HALLAZGO, h.ID_INSP, h.CORRELATIVO,
                   h.DESCRIPCION, h.ACCION_CORR, h.OBS_SEGUIM,
                   h.ESTADO, h.FCH_LIMITE, h.FCH_RESOL, h.COD_CLASIF,
                   h.USR_CREA, h.FCH_CREA
            FROM   {S}SO_INSP_HALLAZGO h
            WHERE  h.ID_INSP = :idInsp
            ORDER  BY h.CORRELATIVO",
            new { idInsp })).ToList();

        if (hallazgos.Count == 0) return hallazgos;

        // Defensa contra columnas Oracle CHAR(n) de longitud fija: si COD_CLASIF (u otros
        // campos de texto) viene con relleno de espacios, una comparación en memoria
        // (LINQ) como h.CodClasif == codClasif fallaría aunque el valor "lógico" coincida.
        foreach (var h in hallazgos)
        {
            h.CodClasif = string.IsNullOrWhiteSpace(h.CodClasif) ? null : h.CodClasif.Trim();
            h.Estado    = h.Estado?.Trim() ?? h.Estado;
        }

        var ids = hallazgos.Select(x => x.IdHallazgo).ToList();
        var imgs = (await conn.QueryAsync<SoHallazgoImg>($@"
            SELECT ID_IMG, ID_HALLAZGO, TIPO, RUTA_ARCH, DESCRIPCION, FCH_CREA
            FROM   {S}SO_HALLAZGO_IMG
            WHERE  ID_HALLAZGO IN :ids
            ORDER  BY ID_HALLAZGO, TIPO, FCH_CREA",
            new { ids })).ToList();

        foreach (var h in hallazgos)
            h.Imgs = imgs.Where(i => i.IdHallazgo == h.IdHallazgo).ToList();

        return hallazgos;
    }

    public async Task<long> GuardarHallazgoAsync(SoHallazgo h, string usuario)
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        var id = await conn.ExecuteScalarAsync<long>(
            $"SELECT {S}SO_HALLAZGO_SEQ.NEXTVAL FROM DUAL");

        await conn.ExecuteAsync($@"
            INSERT INTO {S}SO_INSP_HALLAZGO
                   (ID_HALLAZGO, ID_INSP, CORRELATIVO, DESCRIPCION, ACCION_CORR,
                    OBS_SEGUIM, ESTADO, FCH_LIMITE, COD_CLASIF, FCH_CREA, USR_CREA)
            VALUES (:id, :idInsp, 0, :descripcion, :accion,
                    :obs, :estado, :fchLim, :codClasif, SYSDATE, :usr)",
            new
            {
                id,
                idInsp      = h.IdInsp,
                descripcion = h.Descripcion,
                accion  = h.AccionCorr,
                obs     = h.ObsSeguim,
                estado  = h.Estado,
                fchLim  = h.FchLimite,
                codClasif = h.CodClasif,
                usr     = usuario
            });
        return id;
    }

    public async Task ActualizarHallazgoAsync(SoHallazgo h, string usuario)
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());

        // Si la descripción viene vacía/nula no la sobreescribimos (campo NOT NULL en BD)
        var setDesc = string.IsNullOrWhiteSpace(h.Descripcion) ? "" : "DESCRIPCION = :descripcion,";

        await conn.ExecuteAsync($@"
            UPDATE {S}SO_INSP_HALLAZGO
            SET    {setDesc}
                   ACCION_CORR = :accion,
                   OBS_SEGUIM  = :obs,
                   ESTADO      = :estado,
                   FCH_LIMITE  = :fchLim,
                   FCH_RESOL   = :fchResol,
                   COD_CLASIF  = :codClasif,
                   FCH_MOD     = SYSDATE,
                   USR_MOD     = :usr
            WHERE  ID_HALLAZGO = :id",
            new
            {
                descripcion = string.IsNullOrWhiteSpace(h.Descripcion) ? null! : (object)h.Descripcion,
                accion  = h.AccionCorr,
                obs     = h.ObsSeguim,
                estado  = h.Estado,
                fchLim  = h.FchLimite,
                fchResol= h.FchResol,
                codClasif = h.CodClasif,
                usr     = usuario,
                id      = h.IdHallazgo
            });
    }

    public async Task EliminarHallazgoAsync(long idHallazgo)
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var tx = conn.BeginTransaction();
        try
        {
            await conn.ExecuteAsync(
                $"DELETE FROM {S}SO_HALLAZGO_IMG   WHERE ID_HALLAZGO = :id",
                new { id = idHallazgo }, tx);
            await conn.ExecuteAsync(
                $"DELETE FROM {S}SO_INSP_HALLAZGO  WHERE ID_HALLAZGO = :id",
                new { id = idHallazgo }, tx);
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task<long> AgregarImgHallazgoAsync(SoHallazgoImg img)
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        var id = await conn.ExecuteScalarAsync<long>(
            $"SELECT {S}SO_HALLAZGO_IMG_SEQ.NEXTVAL FROM DUAL");

        await conn.ExecuteAsync($@"
            INSERT INTO {S}SO_HALLAZGO_IMG
                   (ID_IMG, ID_HALLAZGO, TIPO, RUTA_ARCH, DESCRIPCION, FCH_CREA, USR_CREA)
            VALUES (:id, :idHallazgo, :tipo, :ruta, :descripcion, SYSDATE, :usr)",
            new
            {
                id,
                idHallazgo = img.IdHallazgo,
                tipo       = img.Tipo,
                ruta       = img.RutaArch,
                descripcion = img.Descripcion,
                usr        = img.UsrCrea
            });
        return id;
    }

    public async Task<SoHallazgoImg?> ObtenerImgHallazgoPorIdAsync(long idImg)
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        return await conn.QueryFirstOrDefaultAsync<SoHallazgoImg>($@"
            SELECT ID_IMG, ID_HALLAZGO, TIPO, RUTA_ARCH, DESCRIPCION, FCH_CREA, USR_CREA
            FROM   {S}SO_HALLAZGO_IMG
            WHERE  ID_IMG = :id",
            new { id = idImg });
    }

    public async Task EliminarImgHallazgoAsync(long idImg)
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.ExecuteAsync(
            $"DELETE FROM {S}SO_HALLAZGO_IMG WHERE ID_IMG = :id",
            new { id = idImg });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Clasificación de hallazgos + Personal notificado (SO_PERSONAL_CLASIF)
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SoPersonalClasif>> ObtenerPersonalClasifAsync(
        string? codClasif = null, bool soloActivos = true)
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        var rows = await conn.QueryAsync<SoPersonalClasif>($@"
            SELECT ID_PERSONAL, COD_CLASIF, C_CODIGO, NOMBRE, EMAIL,
                   ESTADO, USR_CREA, FCH_CREA
            FROM   {S}SO_PERSONAL_CLASIF
            WHERE  (:codClasif IS NULL OR COD_CLASIF = :codClasif)
              AND  (:soloActivos = 0 OR ESTADO = 'A')
            ORDER  BY COD_CLASIF, NOMBRE",
            new { codClasif, soloActivos = soloActivos ? 1 : 0 });
        return rows.ToList();
    }

    public async Task<long> AsignarPersonalAsync(SoPersonalClasif p, string usuario)
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();

        // Si ya existe (activo o inactivo) para esa clasificación + código, reactivar en vez de duplicar
        var idExistente = await conn.ExecuteScalarAsync<long?>($@"
            SELECT ID_PERSONAL FROM {S}SO_PERSONAL_CLASIF
            WHERE  COD_CLASIF = :codClasif AND C_CODIGO = :ccodigo AND ROWNUM = 1",
            new { codClasif = p.CodClasif, ccodigo = p.CCodigo });

        if (idExistente is > 0)
        {
            await conn.ExecuteAsync($@"
                UPDATE {S}SO_PERSONAL_CLASIF
                SET    ESTADO = 'A', NOMBRE = :nombre, EMAIL = :email,
                       USR_MOD = :usr, FCH_MOD = SYSDATE
                WHERE  ID_PERSONAL = :id",
                new { nombre = p.Nombre, email = p.Email, usr = usuario, id = idExistente.Value });
            return idExistente.Value;
        }

        var id = await conn.ExecuteScalarAsync<long>(
            $"SELECT {S}SO_PERSONAL_SEQ.NEXTVAL FROM DUAL");

        await conn.ExecuteAsync($@"
            INSERT INTO {S}SO_PERSONAL_CLASIF
                   (ID_PERSONAL, COD_CLASIF, C_CODIGO, NOMBRE, EMAIL, ESTADO, FCH_CREA, USR_CREA)
            VALUES (:id, :codClasif, :ccodigo, :nombre, :email, 'A', SYSDATE, :usr)",
            new
            {
                id,
                codClasif = p.CodClasif,
                ccodigo   = p.CCodigo,
                nombre    = p.Nombre,
                email     = p.Email,
                usr       = usuario
            });

        _logger.LogInformation("[SO] Personal {Ccodigo} ({Nombre}) asignado a clasificación {Clasif} por {User}",
            p.CCodigo, p.Nombre, p.CodClasif, usuario);
        return id;
    }

    public async Task QuitarPersonalAsync(long idPersonal, string usuario)
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.ExecuteAsync($@"
            UPDATE {S}SO_PERSONAL_CLASIF
            SET    ESTADO = 'I', USR_MOD = :usr, FCH_MOD = SYSDATE
            WHERE  ID_PERSONAL = :id",
            new { usr = usuario, id = idPersonal });
    }

    public async Task<List<SoEmpleadoBusqueda>> BuscarEmpleadosAsync(string term, int take = 20)
    {
        await using var conn = new OracleConnection(GetOracleConnectionString());
        var like = "%" + term.ToUpperInvariant() + "%";
        // Correo institucional: CS_ANEXO.EMAIL enlazado vía CS_USER.C_CODIGO
        // (vp.EMAIL de V_PERSONAL es el correo personal registrado en RRHH, no el corporativo).
        var sql = $@"SELECT * FROM (
                        SELECT vp.C_CODIGO, vp.NOMBRE_CORTO AS NOMBRE, ca.EMAIL,
                               cc.DESC_GRAN_CCOSTO AS DESC_AREA
                        FROM   {S}V_PERSONAL vp
                        LEFT JOIN (
                                 SELECT pc.C_CODIGO, pc.C_COSTO,
                                        ROW_NUMBER() OVER (PARTITION BY pc.C_CODIGO ORDER BY pc.NUM_PLA DESC) AS RN
                                 FROM   {S}PLA_COSTO pc
                               ) ult ON ult.C_CODIGO = vp.C_CODIGO AND ult.RN = 1
                        LEFT JOIN {S}V_CENTRO_DE_COSTOS cc ON cc.CCOSTO_DET = ult.C_COSTO
                        LEFT JOIN {S}CS_USER  cu ON cu.C_CODIGO = vp.C_CODIGO
                        LEFT JOIN {S}CS_ANEXO ca ON ca.C_CODIGO = cu.C_CODIGO
                        WHERE  vp.SITUACION = '1'
                          AND  (UPPER(vp.NOMBRE_CORTO) LIKE :term
                                OR UPPER(vp.C_CODIGO)   LIKE :term)
                        ORDER  BY vp.NOMBRE_CORTO
                     ) WHERE ROWNUM <= :take";
        var rows = await conn.QueryAsync<SoEmpleadoBusqueda>(sql, new { term = like, take });
        return rows.ToList();
    }
}
