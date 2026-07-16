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
        string? buscar, string? clase, string? estado, int page, int pageSize,
        bool? soloSistemas = null)
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
            // "0"  → activos (ESTADO='0'), con o sin F_OPERA
            // "0P" → activos pendientes de activar: ESTADO='0' AND F_OPERA IS NULL
            // "0C" → activos confirmados:           ESTADO='0' AND F_OPERA IS NOT NULL
            if (estado == "0")
                where.Add("af.ESTADO = '0'");
            else if (estado == "0P")
                where.Add("(af.ESTADO = '0' AND af.F_OPERA IS NULL)");
            else if (estado == "0C")
                where.Add("(af.ESTADO = '0' AND af.F_OPERA IS NOT NULL)");
            else
                where.Add("af.ESTADO = :estado");
        }

        // Filtro de área: SISTEMAS (CCOSTO='250') vs. el resto
        if (soloSistemas == true)
            where.Add("af.CCOSTO = '250'");
        else if (soloSistemas == false)
            where.Add("(af.CCOSTO != '250' OR af.CCOSTO IS NULL)");

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
                   OBS_ALTA, OBS_BAJA,
                   CLASE_DESC,
                   NOMBRE_RESPONSABLE, EMAIL_RESPONSABLE
            FROM (
                SELECT af.CLASE, af.CODIGO, af.NUMERO,
                       af.DESCRIPCION, af.MODELO, af.MARCA, af.SERIE,
                       af.F_OPERA, af.F_BAJA, af.F_INGRESO,
                       af.CCOSTO, af.UBICACION, af.COD_PROVEED,
                       af.VALOR_ADQ_S, af.VALOR_NETO_S, af.VAL_RESID_S,
                       af.VIDA_UTIL, af.TASA_DEPREC, af.MESES_DEP,
                       af.ESTADO, af.SITUACION,
                       af.USER_ALTA, af.USER_BAJA,
                       af.OBS_ALTA, af.OBS_BAJA,
                       af.CLASE_DESC,
                       af.NOMBRE_RESPONSABLE, af.EMAIL_RESPONSABLE,
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
                           af.OBS_ALTA, af.OBS_BAJA,
                           cl.DESCRIPCION AS CLASE_DESC,
                           usr.C_NOMBRE   AS NOMBRE_RESPONSABLE,
                           anx.EMAIL      AS EMAIL_RESPONSABLE
                    FROM   {S}ACTIVO_FIJO af
                    LEFT   JOIN {S}AF_CLASE cl ON cl.CODIGO = af.CLASE
                    LEFT   JOIN {S}CENTRO_DE_COSTOS cc ON cc.CENTRO_COSTO = af.CCOSTO
                    LEFT   JOIN {S}TABLAS_AUXILIARES ta ON ta.TIPO = 83
                                                       AND ta.CODIGO = cc.GRAN_CCOSTO
                    LEFT   JOIN {S}CS_USER  usr ON usr.C_CODIGO = '03' || TO_CHAR(ta.VALOR1)
                    LEFT   JOIN {S}CS_ANEXO anx ON anx.C_CODIGO = usr.C_CODIGO
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
            if (!string.IsNullOrWhiteSpace(estado) && estado != "0" && estado != "0P" && estado != "0C")
                cmdCount.Parameters.Add("estado", OracleDbType.Varchar2).Value = estado;
            total = Convert.ToInt32(await cmdCount.ExecuteScalarAsync() ?? 0);
        }

        if (total > 0)
        {
            await using var cmdData = new OracleCommand(sqlData, conn) { BindByName = true };
            if (!string.IsNullOrWhiteSpace(buscar))  cmdData.Parameters.Add("buscar",   OracleDbType.Varchar2).Value = buscarParam;
            if (!string.IsNullOrWhiteSpace(clase))   cmdData.Parameters.Add("clase",    OracleDbType.Varchar2).Value = clase;
            if (!string.IsNullOrWhiteSpace(estado) && estado != "0" && estado != "0P" && estado != "0C")
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
                    ObsAlta            = GetStr(rdr, "OBS_ALTA"),
                    UserBaja           = GetStr(rdr, "USER_BAJA"),
                    ObsBaja            = GetStr(rdr, "OBS_BAJA"),
                    ClaseDescripcion   = GetStr(rdr, "CLASE_DESC"),
                    NombreResponsable  = GetStr(rdr, "NOMBRE_RESPONSABLE"),
                    EmailResponsable   = GetStr(rdr, "EMAIL_RESPONSABLE"),
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
                   af.VISADO_ALTA, af.VISADO_ALTA_POR, af.VISADO_ALTA_FECHA, af.VISADO_ALTA_OBS,
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
        cmd.Parameters.Add("usuario", OracleDbType.Varchar2, 15).Value = usuario;
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
        string? estadoBaja = null, DateTime? fBaja = null, bool fBajaEnviada = false,
        string? cSestado = null, DateTime? fOpera = null, bool fOperaEnviada = false)
    {
        await using var conn = await AbrirConexionAsync();

        var campo = tipo == "alta" ? "OBS_ALTA" : "OBS_BAJA";

        // Para tipo ALTA, actualizar F_OPERA siempre que el campo haya sido enviado
        // (incluye el caso de fecha borrada → fOpera=null → se guarda NULL en Oracle)
        var setCamposAlta = tipo == "alta" && fOperaEnviada
            ? ", F_OPERA = :fOpera"
            : "";

        // Para tipo BAJA, actualizar ESTADO, F_BAJA y C_SESTADO solo cuando FBajaEnviada=true
        // (igual que FOperaEnviada en alta: permite guardar NULL si el usuario borra el campo)
        var setCamposBaja = tipo == "baja" && fBajaEnviada
            ? ", ESTADO = :estadoBaja, F_BAJA = :fBaja, C_SESTADO = :cSestado"
            : (tipo == "baja" ? ", ESTADO = :estadoBaja, C_SESTADO = :cSestado" : "");

        var sql = $@"UPDATE {S}ACTIVO_FIJO SET {campo} = :obs{setCamposAlta}{setCamposBaja},
                     A_MDUSER = :aMduser, A_MDFECHA = SYSDATE
                     WHERE CLASE = :clase AND CODIGO = :codigo AND NUMERO = :numero";

        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        cmd.Parameters.Add("obs",     OracleDbType.Varchar2, 1000).Value = string.IsNullOrEmpty(obs) ? (object)DBNull.Value : obs;
        if (tipo == "alta" && fOperaEnviada)
            cmd.Parameters.Add("fOpera", OracleDbType.Date).Value = fOpera.HasValue ? (object)fOpera.Value : DBNull.Value;
        if (tipo == "baja")
        {
            cmd.Parameters.Add("estadoBaja", OracleDbType.Varchar2, 6).Value = string.IsNullOrEmpty(estadoBaja) ? (object)DBNull.Value : estadoBaja;
            if (fBajaEnviada)
                cmd.Parameters.Add("fBaja", OracleDbType.Date).Value = fBaja.HasValue ? (object)fBaja.Value : DBNull.Value;
            cmd.Parameters.Add("cSestado", OracleDbType.Varchar2, 1).Value = string.IsNullOrEmpty(cSestado) ? (object)DBNull.Value : cSestado;
        }
        cmd.Parameters.Add("aMduser", OracleDbType.Varchar2,  20).Value = usuario;
        cmd.Parameters.Add("clase",   OracleDbType.Varchar2,   3).Value = clase;
        cmd.Parameters.Add("codigo",  OracleDbType.Varchar2,  10).Value = codigo;
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

    private async Task<FirmaAfDto?> CargarFirmaDto(OracleConnection conn, string? loginUsuario, string rolEtiqueta)
    {
        if (string.IsNullOrWhiteSpace(loginUsuario)) return null;

        // ── Paso 1: resolver login → C_CODIGO + C_NOMBRE (igual que OC resuelve COD_APROB → datos del aprobador) ──
        string? cCodigo        = null;
        string  nombreCompleto = "";

        var sqlCs = $"SELECT C_CODIGO, C_NOMBRE FROM {S}CS_USER WHERE C_USER = :usr AND ROWNUM = 1";
        await using (var cmdCs = new OracleCommand(sqlCs, conn) { BindByName = true })
        {
            cmdCs.Parameters.Add("usr", OracleDbType.Varchar2).Value = loginUsuario.ToUpperInvariant();
            await using var rCs = (OracleDataReader)await cmdCs.ExecuteReaderAsync();
            if (await rCs.ReadAsync())
            {
                cCodigo        = GetStr(rCs, "C_CODIGO")?.Trim();
                nombreCompleto = GetStr(rCs, "C_NOMBRE")?.Trim() ?? "";
            }
        }

        // Fallback nombre desde RH_PERSONAS si CS_USER no tenía C_NOMBRE
        if (string.IsNullOrWhiteSpace(nombreCompleto) && !string.IsNullOrWhiteSpace(cCodigo))
        {
            var sqlRh = $"SELECT APELLIDO_PATERNO || ' ' || APELLIDO_MATERNO || ', ' || NOMBRES FROM {S}RH_PERSONAS WHERE C_CODIGO = :cod AND ROWNUM = 1";
            await using var cmdRh = new OracleCommand(sqlRh, conn) { BindByName = true };
            cmdRh.Parameters.Add("cod", OracleDbType.Varchar2).Value = cCodigo;
            var v = await cmdRh.ExecuteScalarAsync();
            if (v != null && v != DBNull.Value) nombreCompleto = v.ToString()?.Trim() ?? "";
        }

        if (string.IsNullOrWhiteSpace(nombreCompleto)) nombreCompleto = loginUsuario;

        // ── Paso 2: cargo del empleado (igual que OC: RH_PERSONAL + T_CARGO) ──
        string cargo = "";
        if (!string.IsNullOrWhiteSpace(cCodigo))
        {
            var sqlCargo = $"SELECT NVL(tc.DESCRIPCION,'') FROM {S}RH_PERSONAL pr LEFT JOIN {S}T_CARGO tc ON tc.C_CARGO = pr.C_CARGO WHERE pr.C_CODIGO = :cod AND ROWNUM = 1";
            await using var cmdCargo = new OracleCommand(sqlCargo, conn) { BindByName = true };
            cmdCargo.Parameters.Add("cod", OracleDbType.Varchar2).Value = cCodigo;
            var v = await cmdCargo.ExecuteScalarAsync();
            if (v != null && v != DBNull.Value) cargo = v.ToString()?.Trim() ?? "";
        }

        var dto = new FirmaAfDto
        {
            Codigo         = cCodigo ?? loginUsuario,
            NombreCompleto = nombreCompleto,
            Cargo          = cargo,
            RolEtiqueta    = rolEtiqueta,
            Firma          = null
        };

        // ── Paso 3: leer LONG RAW desde RH_FIRMAS ── EXACTAMENTE igual que OC (CargarFirma) ──
        if (string.IsNullOrWhiteSpace(cCodigo)) return dto;
        try
        {
            await using var cmdF = new OracleCommand(
                $"SELECT FIRMA FROM {S}RH_FIRMAS WHERE C_CODIGO = :cod", conn)
            {
                InitialLONGFetchSize = -1
            };
            cmdF.Parameters.Add("cod", OracleDbType.Varchar2, 20).Value = cCodigo;
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
                    else
                        _logger.LogWarning("Firma de {Codigo}: formato de imagen no soportado.", cCodigo);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo leer firma de RH_FIRMAS para {Codigo}", cCodigo);
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
            VisadoAlta         = GetStr(r, "VISADO_ALTA"),
            VisadoAltaPor      = GetStr(r, "VISADO_ALTA_POR"),
            VisadoAltaFecha    = GetDt(r,  "VISADO_ALTA_FECHA"),
            VisadoAltaObs      = GetStr(r, "VISADO_ALTA_OBS"),
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

    public async Task<FirmaAfDto?> ObtenerFirmaJefaturaAsync(string? cCodigo)
    {
        if (string.IsNullOrWhiteSpace(cCodigo)) return null;
        await using var conn = await AbrirConexionAsync();
        return await CargarFirmaDtoPorCodigo(conn, cCodigo, "JEFATURA");
    }

    private async Task<FirmaAfDto?> CargarFirmaDtoPorCodigo(
        OracleConnection conn, string cCodigo, string rolEtiqueta)
    {
        cCodigo = cCodigo.Trim();

        // Nombre desde CS_USER
        string nombreCompleto = "";
        var sqlCs = $"SELECT C_NOMBRE FROM {S}CS_USER WHERE C_CODIGO = :cod AND ROWNUM = 1";
        await using (var cmdCs = new OracleCommand(sqlCs, conn) { BindByName = true })
        {
            cmdCs.Parameters.Add("cod", OracleDbType.Varchar2).Value = cCodigo;
            var v = await cmdCs.ExecuteScalarAsync();
            if (v != null && v != DBNull.Value) nombreCompleto = v.ToString()?.Trim() ?? "";
        }

        // Fallback desde RH_PERSONAS
        if (string.IsNullOrWhiteSpace(nombreCompleto))
        {
            var sqlRh = $"SELECT APELLIDO_PATERNO || ' ' || APELLIDO_MATERNO || ', ' || NOMBRES FROM {S}RH_PERSONAS WHERE C_CODIGO = :cod AND ROWNUM = 1";
            await using var cmdRh = new OracleCommand(sqlRh, conn) { BindByName = true };
            cmdRh.Parameters.Add("cod", OracleDbType.Varchar2).Value = cCodigo;
            var v = await cmdRh.ExecuteScalarAsync();
            if (v != null && v != DBNull.Value) nombreCompleto = v.ToString()?.Trim() ?? "";
        }

        if (string.IsNullOrWhiteSpace(nombreCompleto)) nombreCompleto = cCodigo;

        // Cargo
        string cargo = "";
        var sqlCargo = $"SELECT NVL(tc.DESCRIPCION,'') FROM {S}RH_PERSONAL pr LEFT JOIN {S}T_CARGO tc ON tc.C_CARGO = pr.C_CARGO WHERE pr.C_CODIGO = :cod AND ROWNUM = 1";
        await using (var cmdCargo = new OracleCommand(sqlCargo, conn) { BindByName = true })
        {
            cmdCargo.Parameters.Add("cod", OracleDbType.Varchar2).Value = cCodigo;
            var v = await cmdCargo.ExecuteScalarAsync();
            if (v != null && v != DBNull.Value) cargo = v.ToString()?.Trim() ?? "";
        }

        var dto = new FirmaAfDto
        {
            Codigo         = cCodigo,
            NombreCompleto = nombreCompleto,
            Cargo          = cargo,
            RolEtiqueta    = rolEtiqueta,
            Firma          = null
        };

        try
        {
            await using var cmdF = new OracleCommand(
                $"SELECT FIRMA FROM {S}RH_FIRMAS WHERE C_CODIGO = :cod", conn)
            {
                InitialLONGFetchSize = -1
            };
            cmdF.Parameters.Add("cod", OracleDbType.Varchar2, 20).Value = cCodigo;
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
                    else
                        _logger.LogWarning("Firma jefatura {Codigo}: formato no soportado.", cCodigo);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo leer firma de RH_FIRMAS para jefatura {Codigo}", cCodigo);
        }

        return dto;
    }

    // ── VISADO DE ALTA ────────────────────────────────────────────────────────

    /// <summary>
    /// Genera un token SHA-256 de un solo uso, lo persiste en BD con expiración 30 días,
    /// y devuelve los datos necesarios para armar el email de visado.
    /// </summary>
    public async Task<VisadoAltaEmailData?> PrepararEnvioVisadoAsync(
        string clase, string codigo, int numero, string baseUrl)
    {
        await using var conn = await AbrirConexionAsync();

        // 1 — Leer datos del activo
        var sqlAf = $@"
            SELECT af.DESCRIPCION, af.CCOSTO, af.F_OPERA, af.F_INGRESO,
                   af.VALOR_ADQ_S, af.OBS_ALTA, af.USER_ALTA,
                   cl.DESCRIPCION AS CLASE_DESC,
                   cc.NOMBRE      AS NOMBRE_CC
            FROM   {S}ACTIVO_FIJO af
            LEFT   JOIN {S}AF_CLASE          cl ON cl.CODIGO        = af.CLASE
            LEFT   JOIN {S}CENTRO_DE_COSTOS  cc ON cc.CENTRO_COSTO  = af.CCOSTO
            WHERE  af.CLASE = :clase AND af.CODIGO = :codigo AND af.NUMERO = :numero";

        string? descripcion = null, ccosto = null, nombreCc = null, claseDesc = null;
        string? userAlta = null, obsAlta = null;
        DateTime? fOpera = null, fIngreso = null;
        decimal?  valorAdq = null;

        await using (var cmdAf = new OracleCommand(sqlAf, conn) { BindByName = true })
        {
            cmdAf.Parameters.Add("clase",  OracleDbType.Varchar2,  3).Value = clase;
            cmdAf.Parameters.Add("codigo", OracleDbType.Varchar2, 10).Value = codigo;
            cmdAf.Parameters.Add("numero", OracleDbType.Int32).Value        = numero;
            await using var r = (OracleDataReader)await cmdAf.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return null;
            descripcion = GetStr(r, "DESCRIPCION");
            ccosto      = GetStr(r, "CCOSTO");
            nombreCc    = GetStr(r, "NOMBRE_CC");
            claseDesc   = GetStr(r, "CLASE_DESC");
            fOpera      = GetDt(r,  "F_OPERA");
            fIngreso    = GetDt(r,  "F_INGRESO");
            valorAdq    = GetDec(r, "VALOR_ADQ_S");
            userAlta    = GetStr(r, "USER_ALTA");
            obsAlta     = GetStr(r, "OBS_ALTA");
        }

        // 2 — Resolver nombre del registrador (USER_ALTA → CS_USER)
        string nomRegistrador = userAlta ?? "Sistema";
        if (!string.IsNullOrWhiteSpace(userAlta))
        {
            var sqlNom = $"SELECT C_NOMBRE FROM {S}CS_USER WHERE C_USER = :u AND ROWNUM = 1";
            await using var cmdNom = new OracleCommand(sqlNom, conn) { BindByName = true };
            cmdNom.Parameters.Add("u", OracleDbType.Varchar2, 15).Value = userAlta.ToUpperInvariant();
            var v = await cmdNom.ExecuteScalarAsync();
            if (v != null && v != DBNull.Value) nomRegistrador = v.ToString()!.Trim();
        }

        // 3 — Resolver responsable del C.Costo: misma lógica que el listado
        //     CENTRO_DE_COSTOS.GRAN_CCOSTO → TABLAS_AUXILIARES(tipo=83) → '03'||VALOR1 → CS_USER → CS_ANEXO.EMAIL
        string? correoPor = null, nomPor = null;
        if (!string.IsNullOrWhiteSpace(ccosto))
        {
            var sqlResp = $@"
                SELECT usr.C_NOMBRE AS NOMBRE,
                       anx.EMAIL   AS EMAIL
                FROM   {S}CENTRO_DE_COSTOS  cc
                JOIN   {S}TABLAS_AUXILIARES ta  ON ta.TIPO   = 83
                                               AND ta.CODIGO = cc.GRAN_CCOSTO
                JOIN   {S}CS_USER           usr ON usr.C_CODIGO = '03' || TO_CHAR(ta.VALOR1)
                LEFT   JOIN {S}CS_ANEXO     anx ON anx.C_CODIGO  = usr.C_CODIGO
                WHERE  cc.CENTRO_COSTO = :cc
                AND    ROWNUM = 1";
            await using var cmdResp = new OracleCommand(sqlResp, conn) { BindByName = true };
            cmdResp.Parameters.Add("cc", OracleDbType.Varchar2, 15).Value = ccosto;
            await using var rResp = (OracleDataReader)await cmdResp.ExecuteReaderAsync();
            if (await rResp.ReadAsync())
            {
                nomPor    = GetStr(rResp, "NOMBRE");
                correoPor = GetStr(rResp, "EMAIL");
            }
        }

        if (string.IsNullOrWhiteSpace(correoPor)) return null;  // sin destinatario no se puede enviar

        // 4 — Generar token SHA-256 único
        var tokenRaw  = $"{clase}|{codigo}|{numero}|{Guid.NewGuid()}|{DateTime.UtcNow.Ticks}";
        var tokenHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(tokenRaw))).ToLowerInvariant();

        var expira = DateTime.Today.AddDays(30);

        // 5 — Persistir token + estado 'P' en la BD
        var sqlUpd = $@"
            UPDATE {S}ACTIVO_FIJO
            SET    VISADO_ALTA     = 'P',
                   VISADO_ALTA_POR = NULL,
                   VISADO_ALTA_FECHA = NULL,
                   VISADO_ALTA_OBS  = NULL,
                   TOKEN_ALTA      = :tok,
                   TOKEN_ALTA_EXP  = TO_DATE(:exp, 'DD/MM/YYYY')
            WHERE  CLASE = :clase AND CODIGO = :codigo AND NUMERO = :numero";

        await using var cmdUpd = new OracleCommand(sqlUpd, conn) { BindByName = true };
        cmdUpd.Parameters.Add("tok",    OracleDbType.Varchar2, 64).Value = tokenHash;
        cmdUpd.Parameters.Add("exp",    OracleDbType.Varchar2, 10).Value = expira.ToString("dd/MM/yyyy");
        cmdUpd.Parameters.Add("clase",  OracleDbType.Varchar2,  3).Value = clase;
        cmdUpd.Parameters.Add("codigo", OracleDbType.Varchar2, 10).Value = codigo;
        cmdUpd.Parameters.Add("numero", OracleDbType.Int32).Value        = numero;
        await cmdUpd.ExecuteNonQueryAsync();

        // 6 — Construir URLs
        var urlBase   = baseUrl.TrimEnd('/');
        var urlAprobar = $"{urlBase}/Contabilidad/ActivoFijo/Visar?token={tokenHash}&accion=aprobar";
        var urlObservar= $"{urlBase}/Contabilidad/ActivoFijo/Visar?token={tokenHash}&accion=observar";
        var urlFicha   = $"{urlBase}/Contabilidad/ActivoFijo/Ficha?clase={clase}&codigo={codigo}&numero={numero}&tipo=alta";

        return new VisadoAltaEmailData
        {
            CorreoAprobador  = correoPor,
            NombreAprobador  = nomPor ?? correoPor,
            UrlAprobar       = urlAprobar,
            UrlObservar      = urlObservar,
            UrlFicha         = urlFicha,
            CodigoActivo     = codigo,
            ClaseActivo      = claseDesc ?? clase,
            Descripcion      = descripcion ?? "",
            CCosto           = ccosto ?? "",
            NombreCC         = nombreCc ?? "",
            ValorAdquisicion = valorAdq.HasValue ? $"S/ {valorAdq.Value:N2}" : "—",
            FechaRecepcion   = fIngreso.HasValue ? fIngreso.Value.ToString("dd/MM/yyyy") : "—",
            NombreRegistrador= nomRegistrador,
            FechaRegistro    = DateTime.Today.ToString("dd/MM/yyyy"),
            ObsAlta          = obsAlta,
            FechaOperacion   = fOpera.HasValue ? fOpera.Value.ToString("dd/MM/yyyy") : null,
            FechaExpira      = expira.ToString("dd/MM/yyyy"),
        };
    }

    /// <summary>
    /// Valida el token y aplica la acción (aprobar / observar).
    /// El token se invalida al usarse (se borra de la BD).
    /// </summary>
    public async Task<VisadoResultado> ProcesarVisadoAsync(
        string token, string accion, string? observacion, string ipRemota)
    {
        await using var conn = await AbrirConexionAsync();

        // 1 — Buscar activo por token (sin filtrar por estado para detectar si ya fue aprobado)
        var sqlFind = $@"
            SELECT CLASE, CODIGO, NUMERO, DESCRIPCION, CCOSTO, TOKEN_ALTA_EXP, VISADO_ALTA
            FROM   {S}ACTIVO_FIJO
            WHERE  TOKEN_ALTA = :tok
            AND    ROWNUM = 1";

        string? clase = null, codigoAf = null, descripcion = null, ccosto = null, visadoEstado = null;
        int     numero = 0;
        DateTime? exp = null;

        await using (var cmdF = new OracleCommand(sqlFind, conn) { BindByName = true })
        {
            cmdF.Parameters.Add("tok", OracleDbType.Varchar2, 64).Value = token.ToLowerInvariant();
            await using var r = (OracleDataReader)await cmdF.ExecuteReaderAsync();
            if (!await r.ReadAsync())
                return new VisadoResultado { Ok = false, Error = "El enlace no es válido o no corresponde a ningún activo." };
            clase        = GetStr(r, "CLASE");
            codigoAf     = GetStr(r, "CODIGO");
            numero       = GetInt(r, "NUMERO");
            descripcion  = GetStr(r, "DESCRIPCION");
            ccosto       = GetStr(r, "CCOSTO");
            exp          = GetDt(r, "TOKEN_ALTA_EXP");
            visadoEstado = GetStr(r, "VISADO_ALTA");
        }

        // Validar que el visado aún esté pendiente
        if (visadoEstado == "A")
            return new VisadoResultado { Ok = false, Error = "Este activo ya fue aprobado previamente. No es posible modificar el visado." };

        if (visadoEstado != "P")
            return new VisadoResultado { Ok = false, Error = "El enlace ya fue utilizado o el visado se encuentra en un estado que no permite modificaciones." };

        if (exp.HasValue && DateTime.Today > exp.Value)
            return new VisadoResultado { Ok = false, Error = "El enlace ha expirado. Solicite al registrador que reenvíe el visado." };

        var esAprobar = string.Equals(accion, "aprobar", StringComparison.OrdinalIgnoreCase);
        var nuevoEstado = esAprobar ? "A" : "R";

        // 2 — Resolver C_CODIGO y nombre del aprobador (buscando por C.Costo del activo → jefe área → C_CODIGO)
        //     Necesario para que ObtenerFirmaJefaturaAsync pueda cargar la firma digital correcta,
        //     y para poder informar el nombre del aprobador en el correo de confirmación.
        string? cCodigoAprobador = null;
        string? nombreAprobador  = null;
        try
        {
            var sqlCodSimple = $@"
                SELECT usr.C_CODIGO, usr.C_NOMBRE
                FROM   {S}ACTIVO_FIJO     af
                JOIN   {S}CENTRO_DE_COSTOS cc  ON cc.CENTRO_COSTO = af.CCOSTO
                JOIN   {S}TABLAS_AUXILIARES ta  ON ta.TIPO = 83 AND ta.CODIGO = cc.GRAN_CCOSTO
                JOIN   {S}CS_USER          usr ON usr.C_CODIGO = '03' || TO_CHAR(ta.VALOR1)
                WHERE  af.CLASE = :clase AND af.CODIGO = :codigo AND af.NUMERO = :numero
                AND    ROWNUM = 1";
            await using var cmdCod = new OracleCommand(sqlCodSimple, conn) { BindByName = true };
            cmdCod.Parameters.Add("clase",  OracleDbType.Varchar2,  3).Value = clase!;
            cmdCod.Parameters.Add("codigo", OracleDbType.Varchar2, 10).Value = codigoAf!;
            cmdCod.Parameters.Add("numero", OracleDbType.Int32).Value        = numero;
            await using var rCod = (OracleDataReader)await cmdCod.ExecuteReaderAsync();
            if (await rCod.ReadAsync())
            {
                cCodigoAprobador = GetStr(rCod, "C_CODIGO");
                nombreAprobador  = GetStr(rCod, "C_NOMBRE");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo resolver C_CODIGO del aprobador para activo {Codigo}. Se usará IP como referencia.", codigoAf);
        }

        // 3 — Resolver nombre del centro de costo (para el correo de confirmación)
        string? nombreCc = null;
        if (!string.IsNullOrWhiteSpace(ccosto))
        {
            var sqlCc = $"SELECT NOMBRE FROM {S}CENTRO_DE_COSTOS WHERE CENTRO_COSTO = :cc AND ROWNUM = 1";
            await using var cmdCc = new OracleCommand(sqlCc, conn) { BindByName = true };
            cmdCc.Parameters.Add("cc", OracleDbType.Varchar2, 15).Value = ccosto;
            var vCc = await cmdCc.ExecuteScalarAsync();
            if (vCc != null && vCc != DBNull.Value) nombreCc = vCc.ToString()!.Trim();
        }

        // Actualizar BD: registrar resultado e invalidar token
        var sqlUpd = $@"
            UPDATE {S}ACTIVO_FIJO
            SET    VISADO_ALTA       = :estado,
                   VISADO_ALTA_POR  = :por,
                   VISADO_ALTA_FECHA= SYSDATE,
                   VISADO_ALTA_OBS  = :obs,
                   TOKEN_ALTA       = NULL,
                   TOKEN_ALTA_EXP   = NULL
            WHERE  CLASE = :clase AND CODIGO = :codigo AND NUMERO = :numero";

        await using var cmdU = new OracleCommand(sqlUpd, conn) { BindByName = true };
        cmdU.Parameters.Add("estado", OracleDbType.Varchar2,   1).Value = nuevoEstado;
        cmdU.Parameters.Add("por",    OracleDbType.Varchar2,  15).Value = (object?)(cCodigoAprobador ?? ipRemota) ?? DBNull.Value;
        cmdU.Parameters.Add("obs",    OracleDbType.Varchar2, 500).Value = string.IsNullOrWhiteSpace(observacion)
            ? (object)DBNull.Value : observacion.Trim();
        cmdU.Parameters.Add("clase",  OracleDbType.Varchar2,   3).Value = clase!;
        cmdU.Parameters.Add("codigo", OracleDbType.Varchar2,  10).Value = codigoAf!;
        cmdU.Parameters.Add("numero", OracleDbType.Int32).Value         = numero;
        await cmdU.ExecuteNonQueryAsync();

        var urlFicha = $"/Contabilidad/ActivoFijo/Ficha?clase={clase}&codigo={codigoAf}&numero={numero}&tipo=alta";

        return new VisadoResultado
        {
            Ok              = true,
            CodigoActivo    = codigoAf,
            Descripcion     = descripcion,
            Accion          = esAprobar ? "APROBADO" : "DEVUELTO CON OBSERVACIÓN",
            UrlFicha        = urlFicha,
            CCosto          = ccosto,
            NombreCC        = nombreCc,
            NombreAprobador = nombreAprobador,
            FechaVisado     = DateTime.Now,
        };
    }

    // ── CCOSTO DEL USUARIO ORACLE ─────────────────────────────────────────────

    /// Devuelve el C_COSTO del usuario Oracle activo (leído de CS_USER.C_COSTO).
    public async Task<string?> ObtenerCcostoUsuarioAsync(string cUser)
    {
        if (string.IsNullOrWhiteSpace(cUser)) return null;

        await using var conn = await AbrirConexionAsync();
        const string sql = "SELECT C_COSTO FROM SIG.CS_USER WHERE UPPER(C_USER) = UPPER(:cUser) AND ROWNUM = 1";
        await using var cmd = new OracleCommand(sql, conn) { BindByName = true };
        cmd.Parameters.Add("cUser", OracleDbType.Varchar2, 30).Value = cUser;

        var result = await cmd.ExecuteScalarAsync();
        return result == DBNull.Value || result is null ? null : result.ToString();
    }
}

