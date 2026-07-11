using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using FabricaHilos.Models.Contabilidad;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace FabricaHilos.Services.Contabilidad;

public class ActivoFijoService : OracleServiceBase, IActivoFijoService
{
    private readonly ILogger<ActivoFijoService> _logger;

    public ActivoFijoService(
        IConfiguration       configuration,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ActivoFijoService> logger)
        : base(configuration, httpContextAccessor)
    {
        _logger = logger;
    }

    // ── LISTADO ───────────────────────────────────────────────────────────────

    public async Task<(IEnumerable<ActivoFijoDto> Items, int Total)> ObtenerActivosAsync(
        string? buscar, string? clase, string? estado, int page, int pageSize)
    {
        await using var conn = await AbrirConexionAsync();

        // WHERE dinámico
        var where = new List<string>();
        if (!string.IsNullOrWhiteSpace(buscar))
            where.Add("(UPPER(af.CODIGO) LIKE :buscar OR UPPER(af.DESCRIPCION) LIKE :buscar OR UPPER(af.MARCA) LIKE :buscar OR UPPER(af.SERIE) LIKE :buscar)");
        if (!string.IsNullOrWhiteSpace(clase))
            where.Add("af.CLASE = :clase");
        if (!string.IsNullOrWhiteSpace(estado))
        {
            // "0"  → pendientes de activar: ESTADO='0' AND F_OPERA IS NULL
            // "0C" → activos confirmados:    ESTADO='0' AND F_OPERA IS NOT NULL
            if (estado == "0")
                where.Add("(af.ESTADO = '0' AND af.F_OPERA IS NULL)");
            else if (estado == "0C")
                where.Add("(af.ESTADO = '0' AND af.F_OPERA IS NOT NULL)");
            else
                where.Add("af.ESTADO = :estado");
        }

        var whereClause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
        var buscarParam = "%" + (buscar ?? "").ToUpperInvariant() + "%";

        var sqlCount = $"SELECT COUNT(*) FROM {S}ACTIVO_FIJO af {whereClause}";
        var sqlData  = $@"
            SELECT CLASE, CODIGO, NUMERO,
                   DESCRIPCION, MODELO, MARCA, SERIE,
                   F_OPERA, F_BAJA, F_INGRESO,
                   CCOSTO, UBICACION, COD_PROVEED,
                   VALOR_ADQ_S, VALOR_NETO_S, VAL_RESID_S,
                   VIDA_UTIL, TASA_DEPREC, MESES_DEP,
                   ESTADO, SITUACION,
                   USER_ALTA, USER_BAJA,
                   CLASE_DESC
            FROM (
                SELECT af.CLASE, af.CODIGO, af.NUMERO,
                       af.DESCRIPCION, af.MODELO, af.MARCA, af.SERIE,
                       af.F_OPERA, af.F_BAJA, af.F_INGRESO,
                       af.CCOSTO, af.UBICACION, af.COD_PROVEED,
                       af.VALOR_ADQ_S, af.VALOR_NETO_S, af.VAL_RESID_S,
                       af.VIDA_UTIL, af.TASA_DEPREC, af.MESES_DEP,
                       af.ESTADO, af.SITUACION,
                       af.USER_ALTA, af.USER_BAJA,
                       af.CLASE_DESC,
                       ROWNUM AS RN
                FROM (
                    SELECT af.CLASE, af.CODIGO, af.NUMERO,
                           af.DESCRIPCION, af.MODELO, af.MARCA, af.SERIE,
                           af.F_OPERA, af.F_BAJA, af.F_INGRESO,
                           af.CCOSTO, af.UBICACION, af.COD_PROVEED,
                           af.VALOR_ADQ_S, af.VALOR_NETO_S, af.VAL_RESID_S,
                           af.VIDA_UTIL, af.TASA_DEPREC, af.MESES_DEP,
                           af.ESTADO, af.SITUACION,
                           af.USER_ALTA, af.USER_BAJA,
                           cl.DESCRIPCION AS CLASE_DESC
                    FROM   {S}ACTIVO_FIJO af
                    LEFT   JOIN {S}AF_CLASE cl ON cl.CODIGO = af.CLASE
                    {whereClause}
                    ORDER  BY CASE WHEN af.F_INGRESO IS NULL THEN 1 ELSE 0 END,
                              af.F_INGRESO DESC, af.CLASE, af.CODIGO
                ) af
                WHERE ROWNUM <= :rowMax
            )
            WHERE RN > :offset";

        int total = 0;
        var items = new List<ActivoFijoDto>();

        await using (var cmdCount = new OracleCommand(sqlCount, conn) { BindByName = true })
        {
            if (!string.IsNullOrWhiteSpace(buscar))  cmdCount.Parameters.Add("buscar", OracleDbType.Varchar2).Value = buscarParam;
            if (!string.IsNullOrWhiteSpace(clase))   cmdCount.Parameters.Add("clase",  OracleDbType.Varchar2).Value = clase;
            if (!string.IsNullOrWhiteSpace(estado) && estado != "0" && estado != "0C")
                cmdCount.Parameters.Add("estado", OracleDbType.Varchar2).Value = estado;
            total = Convert.ToInt32(await cmdCount.ExecuteScalarAsync() ?? 0);
        }

        if (total > 0)
        {
            await using var cmdData = new OracleCommand(sqlData, conn) { BindByName = true };
            if (!string.IsNullOrWhiteSpace(buscar))  cmdData.Parameters.Add("buscar",   OracleDbType.Varchar2).Value = buscarParam;
            if (!string.IsNullOrWhiteSpace(clase))   cmdData.Parameters.Add("clase",    OracleDbType.Varchar2).Value = clase;
            if (!string.IsNullOrWhiteSpace(estado) && estado != "0" && estado != "0C")
                cmdData.Parameters.Add("estado", OracleDbType.Varchar2).Value = estado;
            cmdData.Parameters.Add("offset",  OracleDbType.Int32).Value = (page - 1) * pageSize;
            cmdData.Parameters.Add("rowMax",   OracleDbType.Int32).Value = page * pageSize;

            await using var rdr = (OracleDataReader)await cmdData.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                items.Add(new ActivoFijoDto
                {
                    Clase              = GetStr(rdr, "CLASE")       ?? "",
                    Codigo             = GetStr(rdr, "CODIGO")      ?? "",
                    Numero             = GetInt(rdr, "NUMERO"),
                    Descripcion        = GetStr(rdr, "DESCRIPCION"),
                    Modelo             = GetStr(rdr, "MODELO"),
                    Marca              = GetStr(rdr, "MARCA"),
                    Serie              = GetStr(rdr, "SERIE"),
                    FOpera             = GetDt(rdr,  "F_OPERA"),
                    FBaja              = GetDt(rdr,  "F_BAJA"),
                    FIngreso           = GetDt(rdr,  "F_INGRESO"),
                    CCosto             = GetStr(rdr, "CCOSTO"),
                    Ubicacion          = GetStr(rdr, "UBICACION"),
                    CodProveed         = GetStr(rdr, "COD_PROVEED"),
                    ValorAdqS          = GetDec(rdr, "VALOR_ADQ_S"),
                    ValorNetoS         = GetDec(rdr, "VALOR_NETO_S"),
                    ValResidS          = GetDec(rdr, "VAL_RESID_S"),
                    VidaUtil           = GetInt(rdr, "VIDA_UTIL"),
                    TasaDeprec         = GetDec(rdr, "TASA_DEPREC"),
                    MesesDep           = GetInt(rdr, "MESES_DEP"),
                    Estado             = GetStr(rdr, "ESTADO"),
                    Situacion          = GetStr(rdr, "SITUACION"),
                    UserAlta           = GetStr(rdr, "USER_ALTA"),
                    UserBaja           = GetStr(rdr, "USER_BAJA"),
                    ClaseDescripcion   = GetStr(rdr, "CLASE_DESC"),
                });
            }
        }

        return (items, total);
    }

    // ── DETALLE ───────────────────────────────────────────────────────────────

    public async Task<ActivoFijoDto?> ObtenerActivoAsync(string clase, string codigo, int numero)
    {
        await using var conn = await AbrirConexionAsync();

        var sql = $@"
            SELECT af.CLASE, af.CODIGO, af.NUMERO, af.TIPO_COMP,
                   af.DESCRIPCION, af.MODELO, af.MARCA, af.SERIE, af.COLOR,
                   af.F_OPERA, af.F_BAJA, af.F_INGRESO, af.F_ADQUISI, af.F_FABRICA, af.F_INVENTA,
                   af.CCOSTO, af.RESCOD, af.UBICACION, af.COD_PROVEED,
                   af.SERIE_CMP, af.ORDEN_CMP,
                   af.TIPO_DOC, af.SERIE_DOC, af.NUM_DOC,
                   af.CONDI_TEC, af.FORMA_ADQ, af.MONEDA_ADQ,
                   af.VALOR_ADQ_S, af.VALOR_ADQ_D, af.TIPCAM_ADQ,
                   af.VALOR_INI_S, af.VALOR_INI_D, af.TIPCAM_INI,
                   af.VALOR_IDX_S, af.VALOR_IDX_D,
                   af.VALOR_NETO_S, af.VALOR_NETO_D,
                   af.DEPRE_MES_S, af.DEPRE_MES_D,
                   af.DEPRE_ACUM_S, af.DEPRE_ACUM_D,
                   af.DEPRE_RACUM_S,
                   af.INDEX_ACUM_S, af.INDEX_ACUM_D,
                   af.REVAL_ACUM_S, af.REVAL_ACUM_D,
                   af.VAL_RESID_S, af.VAL_RESID_D,
                   af.VAL_TOT_EXT,
                   af.VIDA_UTIL, af.TASA_DEPREC, af.MESES_DEP,
                   af.IND_DEPREC, af.TANGIBLE,
                   af.CUENTA, af.CUENTA_REV, af.CUENTA_DEP, af.CUENTA_RDEP,
                   af.CUENTA_IDX, af.CUENTA_IDX_DEP,
                   af.SITUACION, af.ESTADO, af.CARACTER,
                   af.C_SESTADO, af.ARRENDADO, af.IND_MANTENIMIENTO,
                   af.POTENCIA, af.MEJORA,
                   af.NIIF_VUTIL, af.NIIF_TASA, af.NIIF_VNETOANT,
                   af.NIIF_VTASADO, af.NIIF_VREVALUADO, af.NIIF_VDETERIORO,
                   af.NIIF_VRESIDUAL, af.NIIF_VDEPRECIADO,
                   af.HIST_TASA,
                   af.OBS_ALTA, af.USER_ALTA,
                   af.OBS_BAJA, af.USER_BAJA,
                   af.A_ADUSER, af.A_ADFECHA, af.A_MDUSER, af.A_MDFECHA,
                   af.TIPO_COMPRA,
                   cl.DESCRIPCION AS CLASE_DESC
            FROM   {S}ACTIVO_FIJO af
            LEFT   JOIN {S}AF_CLASE cl ON cl.CODIGO = af.CLASE
            WHERE  af.CLASE = :clase AND af.CODIGO = :codigo AND af.NUMERO = :numero";

        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        cmd.Parameters.Add("clase",  OracleDbType.Varchar2).Value = clase;
        cmd.Parameters.Add("codigo", OracleDbType.Varchar2).Value = codigo;
        cmd.Parameters.Add("numero", OracleDbType.Int32).Value    = numero;

        await using var rdr = (OracleDataReader)await cmd.ExecuteReaderAsync();
        if (!await rdr.ReadAsync()) return null;

        return MapActivoFull(rdr);
    }

    // ── ACTUALIZAR ────────────────────────────────────────────────────────────

    public async Task ActualizarActivoAsync(ActivoFijoDto dto, string usuario)
    {
        await using var conn = await AbrirConexionAsync();

        var sql = $@"
            UPDATE {S}ACTIVO_FIJO SET
                MODELO        = :modelo,
                MARCA         = :marca,
                SERIE         = :serie,
                COLOR         = :color,
                OBS_ALTA      = :obsAlta,
                OBS_BAJA      = :obsBaja,
                A_MDUSER      = :aMduser,
                A_MDFECHA     = SYSDATE
            WHERE CLASE  = :clase
              AND CODIGO = :codigo
              AND NUMERO = :numero";

        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        cmd.Parameters.Add("modelo",      OracleDbType.Varchar2,   60).Value = (object?)dto.Modelo   ?? DBNull.Value;
        cmd.Parameters.Add("marca",       OracleDbType.Varchar2,   60).Value = (object?)dto.Marca    ?? DBNull.Value;
        cmd.Parameters.Add("serie",       OracleDbType.Varchar2,   20).Value = (object?)dto.Serie    ?? DBNull.Value;
        cmd.Parameters.Add("color",       OracleDbType.Varchar2,   40).Value = (object?)dto.Color    ?? DBNull.Value;
        cmd.Parameters.Add("obsAlta",     OracleDbType.Varchar2, 1000).Value = (object?)dto.ObsAlta  ?? DBNull.Value;
        cmd.Parameters.Add("obsBaja",     OracleDbType.Varchar2, 1000).Value = (object?)dto.ObsBaja  ?? DBNull.Value;
        cmd.Parameters.Add("aMduser",     OracleDbType.Varchar2,   15).Value = usuario;
        cmd.Parameters.Add("clase",       OracleDbType.Varchar2,    3).Value = dto.Clase;
        cmd.Parameters.Add("codigo",      OracleDbType.Varchar2,   10).Value = dto.Codigo;
        cmd.Parameters.Add("numero",      OracleDbType.Int32).Value          = dto.Numero;

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ActualizarUsuarioAltaBajaAsync(string clase, string codigo, int numero, string tipo, string usuario)
    {
        await using var conn = await AbrirConexionAsync();

        var campo = tipo == "alta" ? "USER_ALTA" : "USER_BAJA";
        var sql   = $@"UPDATE {S}ACTIVO_FIJO SET {campo} = :usuario WHERE CLASE = :clase AND CODIGO = :codigo AND NUMERO = :numero";

        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        cmd.Parameters.Add("usuario", OracleDbType.Varchar2,  8).Value = usuario;
        cmd.Parameters.Add("clase",   OracleDbType.Varchar2,  3).Value = clase;
        cmd.Parameters.Add("codigo",  OracleDbType.Varchar2, 10).Value = codigo;
        cmd.Parameters.Add("numero",  OracleDbType.Int32).Value        = numero;

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task LimpiarUsuarioAltaBajaAsync(string clase, string codigo, int numero, string tipo)
    {
        await using var conn = await AbrirConexionAsync();

        var campo = tipo == "alta" ? "USER_ALTA" : "USER_BAJA";
        var sql   = $@"UPDATE {S}ACTIVO_FIJO SET {campo} = NULL WHERE CLASE = :clase AND CODIGO = :codigo AND NUMERO = :numero";

        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        cmd.Parameters.Add("clase",  OracleDbType.Varchar2,  3).Value = clase;
        cmd.Parameters.Add("codigo", OracleDbType.Varchar2, 10).Value = codigo;
        cmd.Parameters.Add("numero", OracleDbType.Int32).Value        = numero;

        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ActualizarObservacionesAsync(
        string clase, string codigo, int numero, string tipo, string obs, string usuario,
        string? estadoBaja = null, DateTime? fBaja = null, string? cSestado = null,
        DateTime? fOpera = null, bool fOperaEnviada = false)
    {
        await using var conn = await AbrirConexionAsync();

        var campo = tipo == "alta" ? "OBS_ALTA" : "OBS_BAJA";

        // Para tipo ALTA, actualizar F_OPERA siempre que el campo haya sido enviado
        // (incluye el caso de fecha borrada → fOpera=null → se guarda NULL en Oracle)
        var setCamposAlta = tipo == "alta" && fOperaEnviada
            ? ", F_OPERA = :fOpera"
            : "";

        // Para tipo BAJA, también actualizar ESTADO, F_BAJA y C_SESTADO si se proporcionaron
        var setCamposExtra = tipo == "baja"
            ? ", ESTADO = NVL(:estadoBaja, ESTADO), F_BAJA = NVL(:fBaja, F_BAJA), C_SESTADO = NVL(:cSestado, C_SESTADO)"
            : "";

        var sql = $@"UPDATE {S}ACTIVO_FIJO SET {campo} = :obs{setCamposAlta}{setCamposExtra},
                     A_MDUSER = :aMduser, A_MDFECHA = SYSDATE
                     WHERE CLASE = :clase AND CODIGO = :codigo AND NUMERO = :numero";

        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        cmd.Parameters.Add("obs",     OracleDbType.Varchar2, 1000).Value = string.IsNullOrEmpty(obs) ? (object)DBNull.Value : obs;
        if (tipo == "alta" && fOperaEnviada)
            cmd.Parameters.Add("fOpera", OracleDbType.Date).Value = fOpera.HasValue ? (object)fOpera.Value : DBNull.Value;
        if (tipo == "baja")
        {
            cmd.Parameters.Add("estadoBaja", OracleDbType.Varchar2, 6).Value = string.IsNullOrEmpty(estadoBaja) ? (object)DBNull.Value : estadoBaja;
            cmd.Parameters.Add("fBaja",      OracleDbType.Date).Value         = fBaja.HasValue ? (object)fBaja.Value : DBNull.Value;
            cmd.Parameters.Add("cSestado",   OracleDbType.Varchar2, 1).Value  = string.IsNullOrEmpty(cSestado)   ? (object)DBNull.Value : cSestado;
        }
        cmd.Parameters.Add("aMduser", OracleDbType.Varchar2,   15).Value = usuario;
        cmd.Parameters.Add("clase",   OracleDbType.Varchar2,    3).Value = clase;
        cmd.Parameters.Add("codigo",  OracleDbType.Varchar2,   10).Value = codigo;
        cmd.Parameters.Add("numero",  OracleDbType.Int32).Value          = numero;

        await cmd.ExecuteNonQueryAsync();
    }

    // ── CLASES ────────────────────────────────────────────────────────────────

    public async Task<IEnumerable<AfClaseDto>> ObtenerClasesAsync()
    {
        await using var conn = await AbrirConexionAsync();
        var list = new List<AfClaseDto>();

        await using var cmd = new OracleCommand(
            $"SELECT CODIGO, DESCRIPCION, V_UTIL, TASA FROM {S}AF_CLASE ORDER BY CODIGO", conn);
        await using var rdr = (OracleDataReader)await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            list.Add(new AfClaseDto
            {
                Codigo      = GetStr(rdr, "CODIGO")      ?? "",
                Descripcion = GetStr(rdr, "DESCRIPCION") ?? "",
                VUtil       = GetInt(rdr, "V_UTIL"),
                Tasa        = GetDec(rdr, "TASA") ?? 0
            });
        return list;
    }

    // ── NOMBRES PROVEEDORES ────────────────────────────────────────────────────

    public async Task<Dictionary<string, string>> ObtenerNombresProveedoresAsync(
        IEnumerable<string> codigos)
    {
        var lista = codigos.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!lista.Any()) return result;

        await using var conn = await AbrirConexionAsync();
        var inList = string.Join(",", lista.Select((_, i) => $":p{i}"));
        var sql = $"SELECT COD_PROVEED, NOMBRE FROM {S}PROVEED WHERE COD_PROVEED IN ({inList})";
        await using var cmd = new OracleCommand(sql, conn);
        for (int i = 0; i < lista.Count; i++)
            cmd.Parameters.Add($"p{i}", OracleDbType.Varchar2).Value = lista[i];
        await using var rdr = (OracleDataReader)await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
        {
            var cod  = GetStr(rdr, "COD_PROVEED") ?? "";
            var nom  = GetStr(rdr, "NOMBRE")      ?? cod;
            if (!string.IsNullOrWhiteSpace(cod)) result[cod] = nom;
        }
        return result;
    }

    // ── CENTROS DE COSTO ──────────────────────────────────────────────────────

    public async Task<Dictionary<string, string>> ObtenerDescripcionesCCostosAsync(
        IEnumerable<string> codigos)
    {
        var lista = codigos.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!lista.Any()) return result;

        await using var conn = await AbrirConexionAsync();
        var inList = string.Join(",", lista.Select((_, i) => $":p{i}"));
        var sql = $"SELECT CENTRO_COSTO, NOMBRE FROM {S}CENTRO_DE_COSTOS WHERE CENTRO_COSTO IN ({inList})";
        await using var cmd = new OracleCommand(sql, conn);
        for (int i = 0; i < lista.Count; i++)
            cmd.Parameters.Add($"p{i}", OracleDbType.Varchar2).Value = lista[i];
        await using var rdr = (OracleDataReader)await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
        {
            var cod  = GetStr(rdr, "CENTRO_COSTO") ?? "";
            var desc = GetStr(rdr, "NOMBRE")       ?? cod;
            if (!string.IsNullOrWhiteSpace(cod)) result[cod] = desc;
        }
        return result;
    }

    // ── NOMBRE EMPLEADO ───────────────────────────────────────────────────────

    public async Task<string?> ObtenerNombreEmpleadoAsync(string codEmpleado)
    {
        if (string.IsNullOrWhiteSpace(codEmpleado)) return null;
        await using var conn = await AbrirConexionAsync();

        // Fuente principal: CS_USER (C_USER = login, C_NOMBRE = nombre completo)
        var sql = $@"SELECT C_NOMBRE FROM {S}CS_USER
                     WHERE C_USER = :cod AND ROWNUM = 1";
        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        cmd.Parameters.Add("cod", OracleDbType.Varchar2).Value = codEmpleado.ToUpperInvariant();
        var result = await cmd.ExecuteScalarAsync();
        return result is DBNull || result == null ? null : result.ToString()?.Trim();
    }

    // ── FIRMAS PARA FICHA ─────────────────────────────────────────────────────

    public async Task<(FirmaAfDto? Alta, FirmaAfDto? Baja)> ObtenerFirmasAsync(
        string? userAlta, string? userBaja)
    {
        FirmaAfDto? alta = null;
        FirmaAfDto? baja = null;

        try
        {
            await using var conn = await AbrirConexionAsync();

            alta = await CargarFirmaDto(conn, userAlta, "RESPONSABLE DE ALTA");
            baja = await CargarFirmaDto(conn, userBaja, "RESPONSABLE DE BAJA");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudieron obtener firmas para activo Alta={Alta} Baja={Baja}",
                userAlta, userBaja);
        }

        return (alta, baja);
    }

    private async Task<FirmaAfDto?> CargarFirmaDto(OracleConnection conn, string? codigo, string rolEtiqueta)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return null;

        // ── Paso 1: nombre desde CS_USER (fuente principal para usuarios del sistema) ──
        string nombreCompleto = "";
        var sqlCs = $@"SELECT C_NOMBRE FROM {S}CS_USER
                       WHERE C_USER = :cod AND ROWNUM = 1";
        await using (var cmdCs = new OracleCommand(sqlCs, conn) { BindByName = true })
        {
            cmdCs.Parameters.Add("cod", OracleDbType.Varchar2).Value = codigo.ToUpperInvariant();
            var r = await cmdCs.ExecuteScalarAsync();
            if (r != null && r != DBNull.Value)
                nombreCompleto = r.ToString()?.Trim() ?? "";
        }

        // ── Paso 2: cargo desde RH_PERSONAL / T_CARGO (si el usuario es empleado) ──
        string cargo = "";
        var sqlCargo = $@"SELECT NVL(tc.DESCRIPCION, '') AS CARGO
                          FROM   {S}RH_PERSONAL pr
                          LEFT   JOIN {S}T_CARGO tc ON tc.C_CARGO = pr.C_CARGO
                          WHERE  pr.C_CODIGO = :cod AND ROWNUM = 1";
        await using (var cmdCargo = new OracleCommand(sqlCargo, conn) { BindByName = true })
        {
            cmdCargo.Parameters.Add("cod", OracleDbType.Varchar2).Value = codigo;
            var r = await cmdCargo.ExecuteScalarAsync();
            if (r != null && r != DBNull.Value)
                cargo = r.ToString()?.Trim() ?? "";
        }

        // Si CS_USER no tenía nombre, intentar RH_PERSONAS como último recurso
        if (string.IsNullOrWhiteSpace(nombreCompleto))
        {
            var sqlRh = $@"SELECT ps.APELLIDO_PATERNO || ' ' || ps.APELLIDO_MATERNO || ', ' || ps.NOMBRES
                           FROM {S}RH_PERSONAS ps WHERE ps.C_CODIGO = :cod AND ROWNUM = 1";
            await using var cmdRh = new OracleCommand(sqlRh, conn) { BindByName = true };
            cmdRh.Parameters.Add("cod", OracleDbType.Varchar2).Value = codigo;
            var r = await cmdRh.ExecuteScalarAsync();
            if (r != null && r != DBNull.Value)
                nombreCompleto = r.ToString()?.Trim() ?? "";
        }

        // Si no encontramos nada, usar el código como nombre
        if (string.IsNullOrWhiteSpace(nombreCompleto))
            nombreCompleto = codigo;

        var dto = new FirmaAfDto
        {
            Codigo         = codigo,
            NombreCompleto = nombreCompleto,
            Cargo          = cargo,
            RolEtiqueta    = rolEtiqueta,
            Firma          = null
        };

        // Leer firma LONG RAW desde RH_FIRMAS
        try
        {
            await using var cmdF = new OracleCommand(
                $"SELECT FIRMA FROM {S}RH_FIRMAS WHERE C_CODIGO = :cod", conn)
            {
                InitialLONGFetchSize = -1,
                BindByName           = true
            };
            cmdF.Parameters.Add("cod", OracleDbType.Varchar2, 20).Value = codigo;
            await using var rdr = (OracleDataReader)await cmdF.ExecuteReaderAsync();
            if (await rdr.ReadAsync() && !rdr.IsDBNull(0))
            {
                byte[]? bytes = null;
                var val = rdr.GetValue(0);
                if (val is byte[] b && b.Length > 0)
                    bytes = b;
                else if (val is OracleBinary ob && !ob.IsNull)
                    bytes = ob.Value;

                if (bytes != null && bytes.Length > 0)
                {
                    var mime = DetectImageMimeType(bytes);
                    if (mime == "image/tiff")
                    {
                        bytes = ConvertirTiffAPng(bytes);
                        mime  = "image/png";
                    }
                    if (mime != null)
                        dto.Firma = bytes;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo leer firma de RH_FIRMAS para {Codigo}", codigo);
        }

        return dto;
    }

    // ── HELPERS ───────────────────────────────────────────────────────────────

    private static ActivoFijoDto MapActivoFull(OracleDataReader r)
    {
        return new ActivoFijoDto
        {
            Clase              = GetStr(r, "CLASE")           ?? "",
            Codigo             = GetStr(r, "CODIGO")          ?? "",
            Numero             = GetInt(r, "NUMERO"),
            TipoComp           = GetStr(r, "TIPO_COMP"),
            Descripcion        = GetStr(r, "DESCRIPCION"),
            Modelo             = GetStr(r, "MODELO"),
            Marca              = GetStr(r, "MARCA"),
            Serie              = GetStr(r, "SERIE"),
            Color              = GetStr(r, "COLOR"),
            FOpera             = GetDt(r,  "F_OPERA"),
            FBaja              = GetDt(r,  "F_BAJA"),
            FAdquisi           = GetDt(r,  "F_ADQUISI"),
            FFabrica           = GetDt(r,  "F_FABRICA"),
            FIngreso           = GetDt(r,  "F_INGRESO"),
            FInventa           = GetDt(r,  "F_INVENTA"),
            CCosto             = GetStr(r, "CCOSTO"),
            Rescod             = GetStr(r, "RESCOD"),
            Ubicacion          = GetStr(r, "UBICACION"),
            CodProveed         = GetStr(r, "COD_PROVEED"),
            SerieCmp           = GetStr(r, "SERIE_CMP"),
            OrdenCmp           = GetStr(r, "ORDEN_CMP"),
            TipoDoc            = GetStr(r, "TIPO_DOC"),
            SerieDoc           = GetStr(r, "SERIE_DOC"),
            NumDoc             = GetStr(r, "NUM_DOC"),
            CondiTec           = GetStr(r, "CONDI_TEC"),
            FormaAdq           = GetStr(r, "FORMA_ADQ"),
            MonedaAdq          = GetStr(r, "MONEDA_ADQ"),
            ValorAdqS          = GetDec(r, "VALOR_ADQ_S"),
            ValorAdqD          = GetDec(r, "VALOR_ADQ_D"),
            TipcamAdq          = GetDec(r, "TIPCAM_ADQ"),
            ValorIniS          = GetDec(r, "VALOR_INI_S"),
            ValorIdxS          = GetDec(r, "VALOR_IDX_S"),
            ValorNetoS         = GetDec(r, "VALOR_NETO_S"),
            ValResidS          = GetDec(r, "VAL_RESID_S"),
            ValResidD          = GetDec(r, "VAL_RESID_D"),
            DepreMesS          = GetDec(r, "DEPRE_MES_S"),
            DepreAcumS         = GetDec(r, "DEPRE_ACUM_S"),
            RevalAcumS         = GetDec(r, "REVAL_ACUM_S"),
            Mejora             = GetDec(r, "MEJORA"),
            Potencia           = GetDec(r, "POTENCIA"),
            VidaUtil           = GetInt(r, "VIDA_UTIL"),
            TasaDeprec         = GetDec(r, "TASA_DEPREC"),
            MesesDep           = GetInt(r, "MESES_DEP"),
            IndDeprec          = GetInt(r, "IND_DEPREC"),
            Cuenta             = GetStr(r, "CUENTA"),
            CuentaRev          = GetStr(r, "CUENTA_REV"),
            CuentaDep          = GetStr(r, "CUENTA_DEP"),
            CuentaRdep         = GetStr(r, "CUENTA_RDEP"),
            CuentaIdx          = GetStr(r, "CUENTA_IDX"),
            CuentaIdxDep       = GetStr(r, "CUENTA_IDX_DEP"),
            Tangible           = GetStr(r, "TANGIBLE"),
            Situacion          = GetStr(r, "SITUACION"),
            TipoCompra         = GetStr(r, "TIPO_COMPRA"),
            Caracter           = GetStr(r, "CARACTER"),
            Estado             = GetStr(r, "ESTADO"),
            CSestado           = GetStr(r, "C_SESTADO"),
            Arrendado          = GetStr(r, "ARRENDADO"),
            IndMantenimiento   = GetStr(r, "IND_MANTENIMIENTO"),
            NiifVutil          = GetInt(r, "NIIF_VUTIL"),
            NiifTasa           = GetDec(r, "NIIF_TASA"),
            NiifVnetoant       = GetDec(r, "NIIF_VNETOANT"),
            NiifVtasado        = GetDec(r, "NIIF_VTASADO"),
            NiifVrevaluado     = GetDec(r, "NIIF_VREVALUADO"),
            NiifVdeterioro     = GetDec(r, "NIIF_VDETERIORO"),
            NiifVresidual      = GetDec(r, "NIIF_VRESIDUAL"),
            NiifVdepreciado    = GetDec(r, "NIIF_VDEPRECIADO"),
            HistTasa           = GetDec(r, "HIST_TASA"),
            AAduser            = GetStr(r, "A_ADUSER"),
            AAdfecha           = GetDt(r,  "A_ADFECHA"),
            AMduser            = GetStr(r, "A_MDUSER"),
            AMdfecha           = GetDt(r,  "A_MDFECHA"),
            ObsAlta            = GetStr(r, "OBS_ALTA"),
            UserAlta           = GetStr(r, "USER_ALTA"),
            ObsBaja            = GetStr(r, "OBS_BAJA"),
            UserBaja           = GetStr(r, "USER_BAJA"),
            ClaseDescripcion   = TryGetStr(r, "CLASE_DESC"),
        };
    }

    // ── Readers con safe null ─────────────────────────────────────────────────

    private static string? GetStr(OracleDataReader r, string col)
    {
        try
        {
            int ord = r.GetOrdinal(col);
            return r.IsDBNull(ord) ? null : r.GetString(ord)?.Trim();
        }
        catch { return null; }
    }

    private static string? TryGetStr(OracleDataReader r, string col)
    {
        try
        {
            int ord = r.GetOrdinal(col);
            return r.IsDBNull(ord) ? null : r.GetString(ord)?.Trim();
        }
        catch { return null; }
    }

    private static DateTime? GetDt(OracleDataReader r, string col)
    {
        try
        {
            int ord = r.GetOrdinal(col);
            return r.IsDBNull(ord) ? null : r.GetDateTime(ord);
        }
        catch { return null; }
    }

    private static decimal? GetDec(OracleDataReader r, string col)
    {
        try
        {
            int ord = r.GetOrdinal(col);
            return r.IsDBNull(ord) ? null : r.GetDecimal(ord);
        }
        catch { return null; }
    }

    private static int GetInt(OracleDataReader r, string col)
    {
        try
        {
            int ord = r.GetOrdinal(col);
            return r.IsDBNull(ord) ? 0 : Convert.ToInt32(r.GetValue(ord));
        }
        catch { return 0; }
    }

    // ── Helpers de imagen (mirrors de OrdenCompraService) ─────────────────────

    public static string? DetectImageMimeType(byte[] data)
    {
        if (data == null || data.Length < 4) return null;
        if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47) return "image/png";
        if (data[0] == 0xFF && data[1] == 0xD8)                                         return "image/jpeg";
        if (data[0] == 0x42 && data[1] == 0x4D)                                         return "image/bmp";
        if (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46)                     return "image/gif";
        if ((data[0] == 0x49 && data[1] == 0x49) || (data[0] == 0x4D && data[1] == 0x4D)) return "image/tiff";
        return null;
    }

    private static byte[] ConvertirTiffAPng(byte[] tiffBytes)
    {
        try
        {
            using var input  = new System.IO.MemoryStream(tiffBytes);
            using var image  = Image.Load(input);
            using var output = new System.IO.MemoryStream();
            image.Save(output, new PngEncoder());
            return output.ToArray();
        }
        catch
        {
            return tiffBytes;
        }
    }
    // -- MEMORANDO -----------------------------------------------------------------------

    public async Task<List<MemorandoItemDto>> ObtenerActivosParaMemoAsync(
        IEnumerable<(string Clase, string Codigo, int Numero)> claves)
    {
        var result = new List<MemorandoItemDto>();
        var lista  = claves.ToList();
        if (lista.Count == 0) return result;

        await using var conn = await AbrirConexionAsync();

        foreach (var (clase, codigo, numero) in lista)
        {
            var sql = $@"SELECT CODIGO, DESCRIPCION, F_INGRESO, VALOR_ADQ_S
                         FROM {S}ACTIVO_FIJO
                         WHERE CLASE = :cls AND CODIGO = :cod AND NUMERO = :num";
            await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
            cmd.Parameters.Add("cls", OracleDbType.Varchar2, 10).Value  = clase;
            cmd.Parameters.Add("cod", OracleDbType.Varchar2, 20).Value  = codigo;
            cmd.Parameters.Add("num", OracleDbType.Int32).Value         = numero;

            await using var rdr = (OracleDataReader)await cmd.ExecuteReaderAsync();
            if (await rdr.ReadAsync())
            {
                var fIngreso = GetDt(rdr, "F_INGRESO");
                int anios    = fIngreso.HasValue
                    ? (int)((DateTime.Today - fIngreso.Value).TotalDays / 365.25)
                    : 0;
                result.Add(new MemorandoItemDto
                {
                    Codigo      = GetStr(rdr, "CODIGO") ?? codigo,
                    Descripcion = GetStr(rdr, "DESCRIPCION"),
                    FIngreso    = fIngreso,
                    AniosAnt    = anios,
                    PrecioRef   = GetDec(rdr, "VALOR_ADQ_S") ?? 0m
                });
            }
        }
        return result;
    }

    public async Task<FirmaAfDto?> ObtenerFirmaUsuarioAsync(string codigoUsuario)
    {
        if (string.IsNullOrWhiteSpace(codigoUsuario)) return null;
        await using var conn = await AbrirConexionAsync();
        return await CargarFirmaDto(conn, codigoUsuario, "Firmante");
    }
}

