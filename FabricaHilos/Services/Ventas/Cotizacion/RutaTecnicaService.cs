using System.Data;
using Oracle.ManagedDataAccess.Client;
using FabricaHilos.Models.Ventas.Cotizacion;

namespace FabricaHilos.Services.Ventas.Cotizacion;

public interface IRutaTecnicaService
{
    /// <summary>Agrupa un IntensidadCod ('0'..'5', ver PKG_COT.F_INTENSIDAD_TONOS) en la tonalidad
    /// que usa Preparatoria en sus fichas técnicas ('CRUDO'|'BLANCO'|'CLARO'|'MEDIO'|'OSCURO_INTENSO').</summary>
    string MapearTonalidad(string? intensidadCod);

    /// <summary>Ficha técnica VIGENTE (la más reciente mantenida por Preparatoria) para un título+tonalidad.
    /// <paramref name="tituloCod"/> es el código real de H_TITULOS.TITULO (mismo valor que
    /// CotizacionParametros.TituloCod), NO el texto libre TITULO_ROUTE.
    /// Fallback: exacto (título+tonalidad) → título con TONALIDAD='TODOS' → cualquier tonalidad del título.
    /// Usado en Simular.cshtml (aún no guardada) y para tomar el snapshot al guardar.</summary>
    Task<RutaTecnicaCabDto?> ObtenerVigenteAsync(string? tituloCod, string? intensidadCod);

    /// <summary>Listado de cabeceras (activas e inactivas) para la pantalla de mantenimiento.</summary>
    Task<List<RutaTecnicaCabDto>> ListarCabecerasAsync(string? buscar);

    /// <summary>Cabecera + detalle completos, para el formulario de edición.</summary>
    Task<RutaTecnicaCabDto?> ObtenerPorIdAsync(long idCab);

    /// <summary>Crea o actualiza una ficha técnica completa (cabecera + reemplazo total del detalle).</summary>
    Task<long> GuardarAsync(RutaTecnicaCabDto dto, string usuario);

    Task EliminarAsync(long idCab, string usuario);
    Task RestaurarAsync(long idCab, string usuario);
}

/// <summary>
/// Mantenimiento de la ficha técnica de ruta (COT_RUTA_TECNICA_CAB/DET) que reemplaza el Excel
/// manual de Preparatoria ("1_DATOS_BASE_...xlsx"). Ver 04_COT_RUTA_TECNICA.sql (SIG) para el DDL
/// y las reglas de negocio (por qué varias columnas de detalle son texto libre).
/// </summary>
public class RutaTecnicaService : OracleServiceBase, IRutaTecnicaService
{
    public RutaTecnicaService(IConfiguration configuration, IHttpContextAccessor httpContextAccessor)
        : base(configuration, httpContextAccessor) { }

    private static string? GetStr(OracleDataReader r, string col) => r[col] == DBNull.Value ? null : r[col]?.ToString();
    private static decimal? GetNullDec(OracleDataReader r, string col) => r[col] == DBNull.Value ? null : Convert.ToDecimal(r[col]);
    private static int? GetNullInt(OracleDataReader r, string col) => r[col] == DBNull.Value ? null : Convert.ToInt32(r[col]);
    private static long GetLong(OracleDataReader r, string col) => r[col] == DBNull.Value ? 0L : Convert.ToInt64(r[col]);
    private static DateTime? GetDt(OracleDataReader r, string col) => r[col] == DBNull.Value ? null : Convert.ToDateTime(r[col]);

    public string MapearTonalidad(string? intensidadCod) => (intensidadCod ?? "3").Trim() switch
    {
        "0" => "CRUDO",
        "5" => "BLANCO",
        "1" => "CLARO",
        "2" => "MEDIO",
        "3" or "4" => "OSCURO_INTENSO",
        _ => "TODOS",
    };

    public async Task<RutaTecnicaCabDto?> ObtenerVigenteAsync(string? tituloCod, string? intensidadCod)
    {
        if (string.IsNullOrWhiteSpace(tituloCod)) return null;
        var tonalidad = MapearTonalidad(intensidadCod);

        using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();

        // Fallback en 3 niveles: exacto (título+tonalidad) → título con ficha 'TODOS' (aplica a
        // cualquier tonalidad) → cualquier ficha activa del mismo título (la más reciente).
        // Match por TITULO_COD (código real de H_TITULOS), NUNCA por TITULO_ROUTE (texto libre).
        var sql = $@"
            SELECT * FROM (
                SELECT ID_CAB, 1 AS PRIORIDAD FROM {S}COT_RUTA_TECNICA_CAB
                WHERE TITULO_COD = :titulo AND TONALIDAD = :tonalidad AND ESTADO = 'A'
                UNION ALL
                SELECT ID_CAB, 2 FROM {S}COT_RUTA_TECNICA_CAB
                WHERE TITULO_COD = :titulo AND TONALIDAD = 'TODOS' AND ESTADO = 'A'
                UNION ALL
                SELECT ID_CAB, 3 FROM {S}COT_RUTA_TECNICA_CAB
                WHERE TITULO_COD = :titulo AND ESTADO = 'A'
                ORDER BY 2, 1 DESC
            ) WHERE ROWNUM = 1";

        long idCab;
        using (var cmd = new OracleCommand(sql, conn))
        {
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter(":titulo", OracleDbType.Varchar2, tituloCod.Trim(), ParameterDirection.Input));
            cmd.Parameters.Add(new OracleParameter(":tonalidad", OracleDbType.Varchar2, tonalidad, ParameterDirection.Input));
            var result = await cmd.ExecuteScalarAsync();
            if (result is null || result == DBNull.Value) return null;
            idCab = Convert.ToInt64(result);
        }

        return await ObtenerPorIdInternalAsync(conn, idCab);
    }

    public async Task<List<RutaTecnicaCabDto>> ListarCabecerasAsync(string? buscar)
    {
        using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();

        var filtro = (buscar ?? "").Trim().ToUpperInvariant();
        var sql = $@"
            SELECT c.ID_CAB, c.TITULO_COD, c.TITULO_ROUTE, c.TONALIDAD, c.CLIENTE_REF, c.PRODUCTO_DESC, c.FCH_ACTUALIZADO,
                   c.PEDIDO_MIN_KG, c.PEDIDO_MAX_KG, c.LINEA_ALIM_PCT, c.LINEA_ALIM_DESC,
                   c.NOTA_PEDIDO_MIN, c.NOTA_PARTIDA, c.FIBRA1, c.FIBRA2, c.FIBRA3, c.VALPF, c.PROCESO, c.CLASE_COLOR,
                   c.ESTADO, ht.DESCRIPCION AS TITULO_DESC
            FROM {S}COT_RUTA_TECNICA_CAB c
            LEFT JOIN {S}H_TITULOS ht ON ht.TITULO = c.TITULO_COD
            WHERE (:filtro IS NULL OR UPPER(c.TITULO_ROUTE) LIKE '%'||:filtro||'%' OR UPPER(c.PRODUCTO_DESC) LIKE '%'||:filtro||'%'
                   OR UPPER(c.TITULO_COD) LIKE '%'||:filtro||'%' OR UPPER(ht.DESCRIPCION) LIKE '%'||:filtro||'%')
            ORDER BY c.TITULO_ROUTE, c.TONALIDAD";
        using var cmd = new OracleCommand(sql, conn);
        cmd.BindByName = true;
        cmd.Parameters.Add(new OracleParameter(":filtro", OracleDbType.Varchar2, string.IsNullOrEmpty(filtro) ? null : filtro, ParameterDirection.Input));

        var lista = new List<RutaTecnicaCabDto>();
        using var r = await cmd.ExecuteReaderAsync() as OracleDataReader ?? throw new InvalidOperationException("OracleDataReader esperado.");
        while (await r.ReadAsync())
            lista.Add(MapCab(r));
        return lista;
    }

    public async Task<RutaTecnicaCabDto?> ObtenerPorIdAsync(long idCab)
    {
        using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        return await ObtenerPorIdInternalAsync(conn, idCab);
    }

    private async Task<RutaTecnicaCabDto?> ObtenerPorIdInternalAsync(OracleConnection conn, long idCab)
    {
        RutaTecnicaCabDto? cab;
        using (var cmd = new OracleCommand($@"
            SELECT c.ID_CAB, c.TITULO_COD, c.TITULO_ROUTE, c.TONALIDAD, c.CLIENTE_REF, c.PRODUCTO_DESC, c.FCH_ACTUALIZADO,
                   c.PEDIDO_MIN_KG, c.PEDIDO_MAX_KG, c.LINEA_ALIM_PCT, c.LINEA_ALIM_DESC,
                   c.NOTA_PEDIDO_MIN, c.NOTA_PARTIDA, c.FIBRA1, c.FIBRA2, c.FIBRA3, c.VALPF, c.PROCESO, c.CLASE_COLOR,
                   c.ESTADO, ht.DESCRIPCION AS TITULO_DESC
            FROM {S}COT_RUTA_TECNICA_CAB c
            LEFT JOIN {S}H_TITULOS ht ON ht.TITULO = c.TITULO_COD
            WHERE c.ID_CAB = :id", conn))
        {
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter(":id", OracleDbType.Int64, idCab, ParameterDirection.Input));
            using var r = await cmd.ExecuteReaderAsync() as OracleDataReader ?? throw new InvalidOperationException("OracleDataReader esperado.");
            if (!await r.ReadAsync()) return null;
            cab = MapCab(r);
        }

        using (var cmd = new OracleCommand($@"
            SELECT ID_DET, ORDEN, SECCION, PCT_MERMA, NRO_H, KG_H_MAQ, KG_H_MAQ_TEORICO, NE, PCT_EFIC, OPER, M_MIN, OBS
            FROM {S}COT_RUTA_TECNICA_DET WHERE ID_CAB = :id ORDER BY ORDEN", conn))
        {
            cmd.BindByName = true;
            cmd.Parameters.Add(new OracleParameter(":id", OracleDbType.Int64, idCab, ParameterDirection.Input));
            using var r = await cmd.ExecuteReaderAsync() as OracleDataReader ?? throw new InvalidOperationException("OracleDataReader esperado.");
            while (await r.ReadAsync())
            {
                cab.Detalle.Add(new RutaTecnicaDetDto
                {
                    IdDet = GetLong(r, "ID_DET"),
                    Orden = GetNullInt(r, "ORDEN") ?? 0,
                    Seccion = GetStr(r, "SECCION") ?? "",
                    PctMerma = GetNullDec(r, "PCT_MERMA"),
                    NroH = GetNullInt(r, "NRO_H"),
                    KgHMaq = GetStr(r, "KG_H_MAQ"),
                    KgHMaqTeorico = GetStr(r, "KG_H_MAQ_TEORICO"),
                    Ne = GetStr(r, "NE"),
                    PctEfic = GetStr(r, "PCT_EFIC"),
                    Oper = GetStr(r, "OPER"),
                    MMin = GetStr(r, "M_MIN"),
                    Obs = GetStr(r, "OBS"),
                });
            }
        }
        return cab;
    }

    private static RutaTecnicaCabDto MapCab(OracleDataReader r) => new()
    {
        IdCab = GetLong(r, "ID_CAB"),
        TituloCod = GetStr(r, "TITULO_COD") ?? "",
        TituloDesc = GetStr(r, "TITULO_DESC"),
        TituloRoute = GetStr(r, "TITULO_ROUTE") ?? "",
        Tonalidad = GetStr(r, "TONALIDAD") ?? "TODOS",
        ClienteRef = GetStr(r, "CLIENTE_REF"),
        ProductoDesc = GetStr(r, "PRODUCTO_DESC"),
        FchActualizado = GetDt(r, "FCH_ACTUALIZADO"),
        PedidoMinKg = GetNullDec(r, "PEDIDO_MIN_KG"),
        PedidoMaxKg = GetNullDec(r, "PEDIDO_MAX_KG"),
        LineaAlimPct = GetStr(r, "LINEA_ALIM_PCT"),
        LineaAlimDesc = GetStr(r, "LINEA_ALIM_DESC"),
        NotaPedidoMin = GetStr(r, "NOTA_PEDIDO_MIN"),
        NotaPartida = GetStr(r, "NOTA_PARTIDA"),
        Fibra1 = GetStr(r, "FIBRA1"),
        Fibra2 = GetStr(r, "FIBRA2"),
        Fibra3 = GetStr(r, "FIBRA3"),
        Valpf = GetStr(r, "VALPF"),
        Proceso = GetStr(r, "PROCESO"),
        ClaseColor = GetStr(r, "CLASE_COLOR") ?? "CRUDO",
        Estado = GetStr(r, "ESTADO") ?? "A",
    };

    public async Task<long> GuardarAsync(RutaTecnicaCabDto dto, string usuario)
    {
        if (string.IsNullOrWhiteSpace(dto.TituloCod))
            throw new ArgumentException("El código de título (H_TITULOS) es obligatorio: sin él, la ficha nunca podrá emparejarse con ninguna cotización.");

        using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();

        long idCab;
        try
        {
            if (dto.IdCab <= 0)
            {
                using var cmdSeq = new OracleCommand($"SELECT {S}COT_RUTA_TEC_CAB_SEQ.NEXTVAL FROM DUAL", conn) { Transaction = tx };
                idCab = Convert.ToInt64(await cmdSeq.ExecuteScalarAsync());

                using var cmdIns = new OracleCommand($@"
                    INSERT INTO {S}COT_RUTA_TECNICA_CAB(
                        ID_CAB, TITULO_COD, TITULO_ROUTE, TONALIDAD, CLIENTE_REF, PRODUCTO_DESC, FCH_ACTUALIZADO,
                        PEDIDO_MIN_KG, PEDIDO_MAX_KG, LINEA_ALIM_PCT, LINEA_ALIM_DESC,
                        NOTA_PEDIDO_MIN, NOTA_PARTIDA, FIBRA1, FIBRA2, FIBRA3, VALPF, PROCESO, CLASE_COLOR,
                        ESTADO, A_ADUSER, A_ADFECHA)
                    VALUES(:id, :tituloCod, :titulo, :tonalidad, :cliente, :producto, :fchAct,
                        :pedMin, :pedMax, :lineaPct, :lineaDesc, :notaPed, :notaPart,
                        :fibra1, :fibra2, :fibra3, :valpf, :proceso, :claseColor, 'A', :usuario, SYSDATE)", conn)
                { Transaction = tx };
                AgregarParamsCab(cmdIns, idCab, dto, usuario, esInsert: true);
                await cmdIns.ExecuteNonQueryAsync();
            }
            else
            {
                idCab = dto.IdCab;
                using var cmdUpd = new OracleCommand($@"
                    UPDATE {S}COT_RUTA_TECNICA_CAB SET
                        TITULO_COD=:tituloCod, TITULO_ROUTE=:titulo, TONALIDAD=:tonalidad, CLIENTE_REF=:cliente, PRODUCTO_DESC=:producto,
                        FCH_ACTUALIZADO=:fchAct, PEDIDO_MIN_KG=:pedMin, PEDIDO_MAX_KG=:pedMax,
                        LINEA_ALIM_PCT=:lineaPct, LINEA_ALIM_DESC=:lineaDesc,
                        NOTA_PEDIDO_MIN=:notaPed, NOTA_PARTIDA=:notaPart,
                        FIBRA1=:fibra1, FIBRA2=:fibra2, FIBRA3=:fibra3, VALPF=:valpf, PROCESO=:proceso, CLASE_COLOR=:claseColor,
                        A_MDUSER=:usuario, A_MDFECHA=SYSDATE
                    WHERE ID_CAB=:id", conn)
                { Transaction = tx };
                AgregarParamsCab(cmdUpd, idCab, dto, usuario, esInsert: false);
                await cmdUpd.ExecuteNonQueryAsync();

                // Reemplazo total del detalle (más simple/seguro que un diff fila-por-fila,
                // y el detalle no tiene FKs propias entrantes).
                using var cmdDel = new OracleCommand($"DELETE FROM {S}COT_RUTA_TECNICA_DET WHERE ID_CAB=:id", conn) { Transaction = tx };
                cmdDel.BindByName = true;
                cmdDel.Parameters.Add(new OracleParameter(":id", OracleDbType.Int64, idCab, ParameterDirection.Input));
                await cmdDel.ExecuteNonQueryAsync();
            }

            foreach (var det in dto.Detalle)
            {
                using var cmdSeqDet = new OracleCommand($"SELECT {S}COT_RUTA_TEC_DET_SEQ.NEXTVAL FROM DUAL", conn) { Transaction = tx };
                var idDet = Convert.ToInt64(await cmdSeqDet.ExecuteScalarAsync());

                using var cmdDet = new OracleCommand($@"
                    INSERT INTO {S}COT_RUTA_TECNICA_DET(
                        ID_DET, ID_CAB, ORDEN, SECCION, PCT_MERMA, NRO_H,
                        KG_H_MAQ, KG_H_MAQ_TEORICO, NE, PCT_EFIC, OPER, M_MIN, OBS)
                    VALUES(:idDet, :idCab, :orden, :seccion, :pctMerma, :nroH,
                        :kgHMaq, :kgHMaqTeo, :ne, :pctEfic, :oper, :mMin, :obs)", conn)
                { Transaction = tx };
                cmdDet.BindByName = true;
                cmdDet.Parameters.Add(new OracleParameter(":idDet", OracleDbType.Int64, idDet, ParameterDirection.Input));
                cmdDet.Parameters.Add(new OracleParameter(":idCab", OracleDbType.Int64, idCab, ParameterDirection.Input));
                cmdDet.Parameters.Add(new OracleParameter(":orden", OracleDbType.Int32, det.Orden, ParameterDirection.Input));
                cmdDet.Parameters.Add(new OracleParameter(":seccion", OracleDbType.Varchar2, det.Seccion, ParameterDirection.Input));
                cmdDet.Parameters.Add(new OracleParameter(":pctMerma", OracleDbType.Decimal, (object?)det.PctMerma ?? DBNull.Value, ParameterDirection.Input));
                cmdDet.Parameters.Add(new OracleParameter(":nroH", OracleDbType.Int32, (object?)det.NroH ?? DBNull.Value, ParameterDirection.Input));
                cmdDet.Parameters.Add(new OracleParameter(":kgHMaq", OracleDbType.Varchar2, (object?)det.KgHMaq ?? DBNull.Value, ParameterDirection.Input));
                cmdDet.Parameters.Add(new OracleParameter(":kgHMaqTeo", OracleDbType.Varchar2, (object?)det.KgHMaqTeorico ?? DBNull.Value, ParameterDirection.Input));
                cmdDet.Parameters.Add(new OracleParameter(":ne", OracleDbType.Varchar2, (object?)det.Ne ?? DBNull.Value, ParameterDirection.Input));
                cmdDet.Parameters.Add(new OracleParameter(":pctEfic", OracleDbType.Varchar2, (object?)det.PctEfic ?? DBNull.Value, ParameterDirection.Input));
                cmdDet.Parameters.Add(new OracleParameter(":oper", OracleDbType.Varchar2, (object?)det.Oper ?? DBNull.Value, ParameterDirection.Input));
                cmdDet.Parameters.Add(new OracleParameter(":mMin", OracleDbType.Varchar2, (object?)det.MMin ?? DBNull.Value, ParameterDirection.Input));
                cmdDet.Parameters.Add(new OracleParameter(":obs", OracleDbType.Varchar2, (object?)det.Obs ?? DBNull.Value, ParameterDirection.Input));
                await cmdDet.ExecuteNonQueryAsync();
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
        return idCab;
    }

    private void AgregarParamsCab(OracleCommand cmd, long idCab, RutaTecnicaCabDto dto, string usuario, bool esInsert)
    {
        cmd.BindByName = true;
        if (esInsert) cmd.Parameters.Add(new OracleParameter(":id", OracleDbType.Int64, idCab, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":tituloCod", OracleDbType.Varchar2, dto.TituloCod.Trim(), ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":titulo", OracleDbType.Varchar2, dto.TituloRoute, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":tonalidad", OracleDbType.Varchar2, dto.Tonalidad, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":cliente", OracleDbType.Varchar2, (object?)dto.ClienteRef ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":producto", OracleDbType.Varchar2, (object?)dto.ProductoDesc ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":fchAct", OracleDbType.Date, (object?)dto.FchActualizado ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":pedMin", OracleDbType.Decimal, (object?)dto.PedidoMinKg ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":pedMax", OracleDbType.Decimal, (object?)dto.PedidoMaxKg ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":lineaPct", OracleDbType.Varchar2, (object?)dto.LineaAlimPct ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":lineaDesc", OracleDbType.Varchar2, (object?)dto.LineaAlimDesc ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":notaPed", OracleDbType.Varchar2, (object?)dto.NotaPedidoMin ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":notaPart", OracleDbType.Varchar2, (object?)dto.NotaPartida ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":fibra1", OracleDbType.Varchar2, (object?)dto.Fibra1 ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":fibra2", OracleDbType.Varchar2, (object?)dto.Fibra2 ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":fibra3", OracleDbType.Varchar2, (object?)dto.Fibra3 ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":valpf", OracleDbType.Varchar2, (object?)dto.Valpf ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":proceso", OracleDbType.Varchar2, (object?)dto.Proceso ?? DBNull.Value, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":claseColor", OracleDbType.Varchar2, dto.ClaseColor, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":usuario", OracleDbType.Varchar2, usuario, ParameterDirection.Input));
        if (!esInsert) cmd.Parameters.Add(new OracleParameter(":id", OracleDbType.Int64, idCab, ParameterDirection.Input));
    }

    public async Task EliminarAsync(long idCab, string usuario)
    {
        using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        using var cmd = new OracleCommand($"UPDATE {S}COT_RUTA_TECNICA_CAB SET ESTADO='X', A_MDUSER=:usuario, A_MDFECHA=SYSDATE WHERE ID_CAB=:id", conn);
        cmd.BindByName = true;
        cmd.Parameters.Add(new OracleParameter(":usuario", OracleDbType.Varchar2, usuario, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":id", OracleDbType.Int64, idCab, ParameterDirection.Input));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task RestaurarAsync(long idCab, string usuario)
    {
        using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        using var cmd = new OracleCommand($"UPDATE {S}COT_RUTA_TECNICA_CAB SET ESTADO='A', A_MDUSER=:usuario, A_MDFECHA=SYSDATE WHERE ID_CAB=:id", conn);
        cmd.BindByName = true;
        cmd.Parameters.Add(new OracleParameter(":usuario", OracleDbType.Varchar2, usuario, ParameterDirection.Input));
        cmd.Parameters.Add(new OracleParameter(":id", OracleDbType.Int64, idCab, ParameterDirection.Input));
        await cmd.ExecuteNonQueryAsync();
    }
}
