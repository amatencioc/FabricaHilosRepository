using Oracle.ManagedDataAccess.Client;
using System.Data;
using FabricaHilos.Models.Sgc;

namespace FabricaHilos.Services.Sgc
{
    /// <summary>
    /// Servicio para el módulo "Cargar TC Fibras" (certificados de compra de algodón
    /// orgánico, GOTS/OCS). Reutiliza las mismas tablas genéricas SIG.REQ_CERT /
    /// SIG.REQ_CERT_D que el módulo CargaTc (certificados de venta), filtrando siempre
    /// TIPO = 'C' (Compra). Para TIPO='C', REQ_CERT_D.TIPODOC/SERIE/NUMERO referencian
    /// una REQUISICION (TIPDOC='80'), no un DOCUVENT/factura de venta como en TIPO='V'.
    ///
    /// Cadena de resolución del proveedor y artículo:
    /// REQ_CERT_D (TIPO='C') --NUMERO--> REQUISICION (TIPDOC='80')
    ///     --> DESP_ITEMREQ (TIP_DOC_REF='82') --> ORDEN_DE_COMPRA (TIPO_DOCTO='82')
    ///     --> PROVEED (COD_PROVEED) para Razón Social / RUC del proveedor
    ///     --> ARTICUL (COD_ART) para la descripción del artículo
    ///
    /// Reemplaza al diseño anterior basado en KARDEX_G + PKG_SGC_TC_ALGODON
    /// (ver d:\Development\Database\VCODE_WorkSpace\SIG\SGC\TRAZABILIDAD_TC_FIBRAS\).
    /// Esos objetos Oracle quedaron obsoletos y deben eliminarse (DROP).
    ///
    /// IMPORTANTE: REQ_CERT / REQ_CERT_D tienen PK compuesta (TIPO, NUM_REQ) y NUM_REQ
    /// se reutiliza entre TIPO='V' y TIPO='C'. TODA consulta/actualización sobre estas
    /// tablas DEBE filtrar por TIPO='C' además de NUM_REQ, nunca NUM_REQ solo.
    /// </summary>
    public interface ICargaTcFibraService
    {
        Task<(List<ReqCertDto> Items, int TotalCount)> ObtenerRequerimientosAsync(string? buscar, DateTime? fechaInicio, DateTime? fechaFin, int page = 1, int pageSize = 10);
        Task<Dictionary<string, string>> ObtenerTodosProveedoresAsync();
        Task<ReqCertDto?> ObtenerRequerimientoAsync(int numReq);
        Task<List<ReqCertFibraDocDto>> ObtenerDetalleRequerimientoAsync(int numReq);
        Task<bool> ActualizarCertificadoAsync(ActualizarCertificadoDto modelo, string usuario);
        Task<string> GenerarRutaPdfCertificado(string ruc, string numCer);
        Task<int?> RegistrarRequerimientoCertificadoAsync(RegistrarRequerimientoCertDto modelo, string usuario);
    }

    public class CargaTcFibraService : OracleServiceBase, ICargaTcFibraService
    {
        // Artículo fijo usado siempre para solicitar el servicio de Certificado Digital
        // (GOTS/OCS), tal como lo usa hoy SGC vía la pantalla de Requerimiento normal.
        private const string CodArtCertificadoDigital = "X02018";

        private readonly ILogger<CargaTcFibraService> _logger;

        public CargaTcFibraService(IConfiguration configuration, ILogger<CargaTcFibraService> logger, IHttpContextAccessor httpContextAccessor)
            : base(configuration, httpContextAccessor)
        {
            _logger = logger;
        }

        private static int? SafeGetInt32(OracleDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);

            if (reader.IsDBNull(ordinal))
                return null;

            try
            {
                // Intenta obtenerlo como decimal primero (el tipo más común en Oracle para NUMBER)
                return Convert.ToInt32(reader.GetDecimal(ordinal));
            }
            catch
            {
                try
                {
                    // Si falla, intenta obtenerlo directamente como int
                    return reader.GetInt32(ordinal);
                }
                catch
                {
                    try
                    {
                        // Como último recurso, intenta convertir desde string
                        var value = reader.GetValue(ordinal);
                        return Convert.ToInt32(value);
                    }
                    catch
                    {
                        return null;
                    }
                }
            }
        }

        // SELECT base reutilizado por el listado y por la consulta de un único requerimiento.
        // Siempre filtra TIPO='C'. Resuelve el proveedor y la 1ra requisición asociada
        // (solo para mostrar en el listado; el detalle completo se obtiene con
        // ObtenerDetalleRequerimientoAsync, que trae TODAS las requisiciones/ítems).
        private string BuildBaseSelectSql() => $@"
                SELECT
                    rc.NUM_REQ,
                    rc.FECHA,
                    rc.NUM_CER,
                    rc.ESTADO,
                    rc.OBSERVACION,
                    rc.A_ADUSER,
                    rc.A_ADFECHA,
                    rc.A_MDUSER,
                    rc.A_MDFECHA,
                    prd.NUM_REQUISICION,
                    NVL(prov.NOMBRE, rq.PROVEEDORES) AS RAZON_SOCIAL,
                    prov.RUC,
                    ocs.OCS
                FROM {S}REQ_CERT rc
                LEFT JOIN (
                    SELECT rd.NUM_REQ, MIN(rd.NUMERO) AS NUM_REQUISICION
                    FROM {S}REQ_CERT_D rd
                    WHERE rd.TIPO = 'C' AND rd.TIPODOC = '80'
                    GROUP BY rd.NUM_REQ
                ) prd ON prd.NUM_REQ = rc.NUM_REQ
                LEFT JOIN {S}REQUISICION rq ON rq.TIPDOC = '80' AND rq.SERIE = '1' AND rq.NUMREQ = prd.NUM_REQUISICION
                LEFT JOIN (
                    SELECT di.NUMREQ, MIN(di.NRO_DOC_REF) AS NRO_DOC_REF, MIN(di.SER_DOC_REF) AS SER_DOC_REF
                    FROM {S}DESP_ITEMREQ di
                    WHERE di.TIPDOC = '80' AND di.SERIE = '1' AND di.TIP_DOC_REF = '82'
                    GROUP BY di.NUMREQ
                ) pdi ON pdi.NUMREQ = rq.NUMREQ
                LEFT JOIN {S}ORDEN_DE_COMPRA oc ON oc.TIPO_DOCTO = '82' AND oc.SERIE = pdi.SER_DOC_REF AND oc.NUM_PED = pdi.NRO_DOC_REF
                LEFT JOIN {S}PROVEED prov ON prov.COD_PROVEED = oc.COD_PROVEED
                -- Sin OC aún (requisición de servicio recién enlazada, sin DESP_ITEMREQ/ORDEN_DE_COMPRA
                -- todavía) → usar el proveedor de texto libre capturado en REQUISICION.PROVEEDORES.
                -- Todas las Órdenes de Compra distintas del requerimiento (puede tener varias
                -- requisiciones/ítems referenciando OCs distintas). Se agrupa con LISTAGG sobre
                -- un DISTINCT previo porque Oracle 11.2.0.4 no soporta LISTAGG(DISTINCT ...).
                LEFT JOIN (
                    SELECT NUM_REQ, LISTAGG(NRO_DOC_REF, ',') WITHIN GROUP (ORDER BY NRO_DOC_REF) AS OCS
                    FROM (
                        SELECT DISTINCT rd2.NUM_REQ, di2.NRO_DOC_REF
                        FROM {S}REQ_CERT_D rd2
                        JOIN {S}DESP_ITEMREQ di2 ON di2.TIPDOC = rd2.TIPODOC AND di2.SERIE = rd2.SERIE
                                                  AND di2.NUMREQ = rd2.NUMERO AND di2.TIP_DOC_REF = '82'
                        WHERE rd2.TIPO = 'C' AND di2.NRO_DOC_REF IS NOT NULL
                    )
                    GROUP BY NUM_REQ
                ) ocs ON ocs.NUM_REQ = rc.NUM_REQ";

        private static ReqCertDto MapReqCertRow(OracleDataReader reader) => new()
        {
            NumReq = Convert.ToInt32(reader["NUM_REQ"]),
            Tipo = "C",
            Fecha = reader["FECHA"] == DBNull.Value ? null : Convert.ToDateTime(reader["FECHA"]),
            NumCer = reader["NUM_CER"] == DBNull.Value ? null : reader["NUM_CER"]?.ToString(),
            Estado = SafeGetInt32(reader, "ESTADO"),
            Observacion = reader["OBSERVACION"] == DBNull.Value ? null : reader["OBSERVACION"]?.ToString(),
            AAduser = reader["A_ADUSER"] == DBNull.Value ? null : reader["A_ADUSER"]?.ToString(),
            AAdfecha = reader["A_ADFECHA"] == DBNull.Value ? null : Convert.ToDateTime(reader["A_ADFECHA"]),
            AMduser = reader["A_MDUSER"] == DBNull.Value ? null : reader["A_MDUSER"]?.ToString(),
            AMdfecha = reader["A_MDFECHA"] == DBNull.Value ? null : Convert.ToDateTime(reader["A_MDFECHA"]),
            NumRequisicion = reader["NUM_REQUISICION"] == DBNull.Value ? null : Convert.ToDecimal(reader["NUM_REQUISICION"]),
            RazonSocial = reader["RAZON_SOCIAL"] == DBNull.Value ? null : reader["RAZON_SOCIAL"]?.ToString(),
            Ruc = reader["RUC"] == DBNull.Value ? null : reader["RUC"]?.ToString(),
            Ocs = reader["OCS"] == DBNull.Value ? null : reader["OCS"]?.ToString()
        };

        // Proveedores activos (SIG.PROVEED) para el autocompletar de texto libre del modal
        // "Registrar Requerimiento de Certificado Digital" (mismo patrón que Logistica/OrdenCompra).
        public async Task<Dictionary<string, string>> ObtenerTodosProveedoresAsync()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var conn = new OracleConnection(GetOracleConnectionString());
                await conn.OpenAsync();

                var sql = $"SELECT COD_PROVEED, NOMBRE FROM {S}PROVEED WHERE ESTADO = '0' ORDER BY NOMBRE";
                using var cmd = new OracleCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync() as OracleDataReader
                    ?? throw new InvalidOperationException("OracleDataReader expected");

                while (await reader.ReadAsync())
                {
                    var cod    = reader["COD_PROVEED"] == DBNull.Value ? null : reader["COD_PROVEED"].ToString();
                    var nombre = reader["NOMBRE"]      == DBNull.Value ? null : reader["NOMBRE"].ToString();
                    if (!string.IsNullOrEmpty(cod) && !string.IsNullOrEmpty(nombre))
                        result[cod] = nombre;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener lista de proveedores para autocompletar");
            }
            return result;
        }

        public async Task<(List<ReqCertDto> Items, int TotalCount)> ObtenerRequerimientosAsync(
            string? buscar, DateTime? fechaInicio, DateTime? fechaFin, int page = 1, int pageSize = 10)
        {
            var items = new List<ReqCertDto>();
            int totalCount = 0;

            try
            {
                using var conn = new OracleConnection(GetOracleConnectionString());
                await conn.OpenAsync();

                int startRow = (page - 1) * pageSize + 1;
                int endRow   = page * pageSize;

                bool hasBuscar   = !string.IsNullOrWhiteSpace(buscar);
                bool hasFechaIni = fechaInicio.HasValue;
                bool hasFechaFin = fechaFin.HasValue;

                string buscarFilter = hasBuscar
                    ? "\n                          AND (rc.NUM_CER LIKE '%' || :buscar || '%' OR prov.NOMBRE LIKE '%' || :buscar || '%' OR prov.RUC LIKE '%' || :buscar || '%'" +
                      " OR TO_CHAR(rc.NUM_REQ) LIKE '%' || :buscar || '%' OR TO_CHAR(rq.NUMREQ) LIKE '%' || :buscar || '%' OR ocs.OCS LIKE '%' || :buscar || '%')"
                    : string.Empty;

                string fechaFilter = string.Empty;
                if (hasFechaIni && hasFechaFin)
                    fechaFilter = "\n                          AND TRUNC(rc.FECHA) BETWEEN TRUNC(:fechaInicio) AND TRUNC(:fechaFin)";
                else if (hasFechaIni)
                    fechaFilter = "\n                          AND TRUNC(rc.FECHA) >= TRUNC(:fechaInicio)";
                else if (hasFechaFin)
                    fechaFilter = "\n                          AND TRUNC(rc.FECHA) <= TRUNC(:fechaFin)";

                string sql = $@"
                    SELECT RN, TOTAL_COUNT,
                           NUM_REQ, FECHA, NUM_CER, ESTADO, OBSERVACION,
                           A_ADUSER, A_ADFECHA, A_MDUSER, A_MDFECHA,
                           NUM_REQUISICION, RAZON_SOCIAL, RUC, OCS
                    FROM (
                        SELECT ROW_NUMBER() OVER (ORDER BY Q.NUM_REQ ASC) AS RN,
                               COUNT(*) OVER() AS TOTAL_COUNT,
                               Q.*
                        FROM (
                            {BuildBaseSelectSql()}
                            WHERE rc.TIPO = 'C'{buscarFilter}{fechaFilter}
                        ) Q
                    )
                    WHERE RN BETWEEN :startRow AND :endRow";

                using var cmd = new OracleCommand(sql, conn);
                cmd.BindByName = true;

                if (hasBuscar)
                    cmd.Parameters.Add(new OracleParameter(":buscar", OracleDbType.Varchar2, buscar!.Trim(), ParameterDirection.Input));
                if (hasFechaIni)
                    cmd.Parameters.Add(new OracleParameter(":fechaInicio", OracleDbType.Date, fechaInicio!.Value.Date, ParameterDirection.Input));
                if (hasFechaFin)
                    cmd.Parameters.Add(new OracleParameter(":fechaFin", OracleDbType.Date, fechaFin!.Value.Date, ParameterDirection.Input));

                cmd.Parameters.Add(new OracleParameter(":startRow", OracleDbType.Int32, startRow, ParameterDirection.Input));
                cmd.Parameters.Add(new OracleParameter(":endRow", OracleDbType.Int32, endRow, ParameterDirection.Input));

                using var reader = await cmd.ExecuteReaderAsync() as OracleDataReader
                    ?? throw new InvalidOperationException("OracleDataReader expected");

                while (await reader.ReadAsync())
                {
                    if (items.Count == 0)
                        totalCount = reader["TOTAL_COUNT"] == DBNull.Value ? 0 : Convert.ToInt32(reader["TOTAL_COUNT"]);

                    items.Add(MapReqCertRow(reader));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener requerimientos de certificados de fibra (TIPO=C)");
                throw;
            }

            return (items, totalCount);
        }

        public async Task<ReqCertDto?> ObtenerRequerimientoAsync(int numReq)
        {
            try
            {
                using var conn = new OracleConnection(GetOracleConnectionString());
                await conn.OpenAsync();

                var sql = $@"
                    {BuildBaseSelectSql()}
                    WHERE rc.TIPO = 'C' AND rc.NUM_REQ = :NumReq";

                using var cmd = new OracleCommand(sql, conn);
                cmd.Parameters.Add(new OracleParameter("NumReq", numReq));

                using var reader = await cmd.ExecuteReaderAsync() as OracleDataReader
                    ?? throw new InvalidOperationException("OracleDataReader expected");

                if (await reader.ReadAsync())
                    return MapReqCertRow(reader);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener requerimiento de fibra {NumReq}", numReq);
                throw;
            }

            return null;
        }

        public async Task<List<ReqCertFibraDocDto>> ObtenerDetalleRequerimientoAsync(int numReq)
        {
            var items = new List<ReqCertFibraDocDto>();

            try
            {
                using var conn = new OracleConnection(GetOracleConnectionString());
                await conn.OpenAsync();

                // Ingreso de almacén (KARDEX_G/KARDEX_D, TP_TRANSAC='11'): el operario registra
                // en KARDEX_G.NUM_OCOMPRA el Nº de OC si existe, o el Nº de requisición si el
                // ingreso se hizo directo sin OC formal (ver Logistica/_MEMORIA_INGRESO_ALMACEN.md).
                // NOTA: la correlación con la fila externa (di.COD_ART, oc.NUM_PED, rq.NUMREQ) debe
                // ir en el WHERE de la subquery, no en el ON del JOIN (ORA-00904 en Oracle 11g si se
                // pone ahí).
                var sql = $@"
                    SELECT
                        rd.NUM_REQ,
                        rd.TIPODOC,
                        rd.SERIE,
                        rd.NUMERO AS NUM_REQUISICION,
                        rq.FECHA AS FECHA_REQ,
                        rq.ESTADO AS ESTADO_REQ,
                        rq.OBSERVACION AS OBSERVACION_REQ,
                        ir.ORDEN,
                        ir.COD_ART,
                        ar.DESCRIPCION AS ARTICULO,
                        NVL(di.CANTIDAD, ir.CANTIDAD) AS CANTIDAD,
                        ir.UNIDAD,
                        ir.MONEDA,
                        oc.NUM_PED AS OC,
                        oc.FECHA AS FECHA_OC,
                        oc.DETALLE AS OBSERVACION_OC,
                        oc.COD_PROVEED,
                        NVL(prov.NOMBRE, rq.PROVEEDORES) AS PROVEEDOR,
                        prov.RUC,
                        king_art.CANTIDAD_INGRESADA,
                        king_oc.FECHA_INGRESO
                    FROM {S}REQ_CERT_D rd
                    LEFT JOIN {S}REQUISICION rq ON rq.TIPDOC = rd.TIPODOC AND rq.SERIE = rd.SERIE AND rq.NUMREQ = rd.NUMERO
                    -- ITEMREQ/ARTICUL se leen directo desde la REQUISICION (no dependen de que ya
                    -- exista una OC despachada), para que un ítem de servicio recién enlazado
                    -- (sin DESP_ITEMREQ/ORDEN_DE_COMPRA todavía) muestre Artículo/Cantidad igual.
                    LEFT JOIN {S}ITEMREQ ir ON ir.TIPDOC = rq.TIPDOC AND ir.SERIE = rq.SERIE AND ir.NUMREQ = rq.NUMREQ
                    LEFT JOIN {S}ARTICUL ar ON ar.COD_ART = ir.COD_ART
                    LEFT JOIN {S}DESP_ITEMREQ di ON di.TIPDOC = ir.TIPDOC AND di.SERIE = ir.SERIE AND di.NUMREQ = ir.NUMREQ AND di.ORDEN = ir.ORDEN AND di.TIP_DOC_REF = '82'
                    LEFT JOIN {S}ORDEN_DE_COMPRA oc ON oc.TIPO_DOCTO = '82' AND oc.SERIE = di.SER_DOC_REF AND oc.NUM_PED = di.NRO_DOC_REF
                    LEFT JOIN {S}PROVEED prov ON prov.COD_PROVEED = oc.COD_PROVEED
                    -- Ingresos de almac\u00e9n agregados por (NUM_OCOMPRA, COD_ART): reemplaza el antiguo subselect
                    -- correlacionado (ejecutado una vez por fila) por un JOIN a una agregaci\u00f3n calculada
                    -- una sola vez para todo el resultado.
                    LEFT JOIN (
                        SELECT kg2.NUM_OCOMPRA, kd.COD_ART, SUM(kd.CANTIDAD) AS CANTIDAD_INGRESADA
                        FROM {S}KARDEX_G kg2
                        JOIN {S}KARDEX_D kd ON kd.COD_ALM = kg2.COD_ALM AND kd.TP_TRANSAC = kg2.TP_TRANSAC
                                            AND kd.SERIE = kg2.SERIE AND kd.NUMERO = kg2.NUMERO
                        WHERE kg2.TP_TRANSAC = '11'
                        GROUP BY kg2.NUM_OCOMPRA, kd.COD_ART
                    ) king_art ON king_art.NUM_OCOMPRA = NVL(oc.NUM_PED, rq.NUMREQ) AND king_art.COD_ART = ir.COD_ART
                    -- Fecha del \u00faltimo ingreso por NUM_OCOMPRA (cualquier art\u00edculo), tambi\u00e9n agregada una sola vez.
                    LEFT JOIN (
                        SELECT kg2.NUM_OCOMPRA, MAX(kg2.FCH_TRANSAC) AS FECHA_INGRESO
                        FROM {S}KARDEX_G kg2
                        WHERE kg2.TP_TRANSAC = '11'
                        GROUP BY kg2.NUM_OCOMPRA
                    ) king_oc ON king_oc.NUM_OCOMPRA = NVL(oc.NUM_PED, rq.NUMREQ)
                    WHERE rd.TIPO = 'C' AND rd.NUM_REQ = :NumReq
                    ORDER BY rq.NUMREQ, ir.ORDEN";

                using var cmd = new OracleCommand(sql, conn);
                cmd.Parameters.Add(new OracleParameter("NumReq", numReq));

                using var reader = await cmd.ExecuteReaderAsync() as OracleDataReader
                    ?? throw new InvalidOperationException("OracleDataReader expected");

                while (await reader.ReadAsync())
                {
                    items.Add(new ReqCertFibraDocDto
                    {
                        NumReq = Convert.ToInt32(reader["NUM_REQ"]),
                        TipoDoc = reader["TIPODOC"] == DBNull.Value ? null : reader["TIPODOC"]?.ToString(),
                        Serie = reader["SERIE"] == DBNull.Value ? null : reader["SERIE"]?.ToString(),
                        NumRequisicion = reader["NUM_REQUISICION"] == DBNull.Value ? null : Convert.ToDecimal(reader["NUM_REQUISICION"]),
                        FechaReq = reader["FECHA_REQ"] == DBNull.Value ? null : Convert.ToDateTime(reader["FECHA_REQ"]),
                        EstadoReq = reader["ESTADO_REQ"] == DBNull.Value ? null : reader["ESTADO_REQ"]?.ToString(),
                        ObservacionReq = reader["OBSERVACION_REQ"] == DBNull.Value ? null : reader["OBSERVACION_REQ"]?.ToString(),
                        Orden = reader["ORDEN"] == DBNull.Value ? null : Convert.ToInt32(reader["ORDEN"]),
                        CodArt = reader["COD_ART"] == DBNull.Value ? null : reader["COD_ART"]?.ToString(),
                        Articulo = reader["ARTICULO"] == DBNull.Value ? null : reader["ARTICULO"]?.ToString(),
                        Cantidad = reader["CANTIDAD"] == DBNull.Value ? null : Convert.ToDecimal(reader["CANTIDAD"]),
                        Unidad = reader["UNIDAD"] == DBNull.Value ? null : reader["UNIDAD"]?.ToString(),
                        Moneda = reader["MONEDA"] == DBNull.Value ? null : reader["MONEDA"]?.ToString(),
                        Oc = reader["OC"] == DBNull.Value ? null : Convert.ToDecimal(reader["OC"]),
                        FechaOc = reader["FECHA_OC"] == DBNull.Value ? null : Convert.ToDateTime(reader["FECHA_OC"]),
                        ObservacionOc = reader["OBSERVACION_OC"] == DBNull.Value ? null : reader["OBSERVACION_OC"]?.ToString(),
                        CodProveed = reader["COD_PROVEED"] == DBNull.Value ? null : reader["COD_PROVEED"]?.ToString(),
                        Proveedor = reader["PROVEEDOR"] == DBNull.Value ? null : reader["PROVEEDOR"]?.ToString(),
                        Ruc = reader["RUC"] == DBNull.Value ? null : reader["RUC"]?.ToString(),
                        CantidadIngresada = reader["CANTIDAD_INGRESADA"] == DBNull.Value ? null : Convert.ToDecimal(reader["CANTIDAD_INGRESADA"]),
                        FechaIngreso = reader["FECHA_INGRESO"] == DBNull.Value ? null : Convert.ToDateTime(reader["FECHA_INGRESO"])
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener detalle del requerimiento de fibra {NumReq}", numReq);
                throw;
            }

            return items;
        }

        public async Task<bool> ActualizarCertificadoAsync(ActualizarCertificadoDto modelo, string usuario)
        {
            try
            {
                using var conn = new OracleConnection(GetOracleConnectionString());
                await conn.OpenAsync();
                using var transaction = conn.BeginTransaction();

                try
                {
                    // 1. Bloquear el registro antes de actualizar (FOR UPDATE NOWAIT). SIEMPRE
                    //    con TIPO='C' — REQ_CERT tiene PK compuesta (TIPO, NUM_REQ) y NUM_REQ
                    //    se reutiliza entre TIPO='V' y TIPO='C'.
                    var querySel = $"SELECT NUM_CER FROM {S}REQ_CERT WHERE TIPO = 'C' AND NUM_REQ = :NumReq FOR UPDATE NOWAIT";

                    using (var cmdSel = new OracleCommand(querySel, conn))
                    {
                        cmdSel.Transaction = transaction;
                        cmdSel.Parameters.Add(new OracleParameter("NumReq", modelo.NumReq));

                        using var reader = await cmdSel.ExecuteReaderAsync();
                        if (!await reader.ReadAsync())
                        {
                            _logger.LogWarning("No se encontró el registro TIPO=C NUM_REQ={NumReq} a actualizar", modelo.NumReq);
                            await transaction.RollbackAsync();
                            return false;
                        }
                    }

                    _logger.LogDebug("Registro TIPO=C NUM_REQ={NumReq} bloqueado correctamente", modelo.NumReq);

                    // 2. Ejecutar el UPDATE
                    var sql = $@"
                        UPDATE {S}REQ_CERT 
                        SET 
                            NUM_CER = :NumCer,
                            OBSERVACION = :Observacion,
                            A_MDUSER = :Usuario,
                            A_MDFECHA = SYSDATE
                        WHERE TIPO = 'C' AND NUM_REQ = :NumReq";

                    using var cmd = new OracleCommand(sql, conn);
                    cmd.Transaction = transaction;
                    cmd.Parameters.Add(new OracleParameter("NumCer", modelo.NumCer));
                    cmd.Parameters.Add(new OracleParameter("Observacion", modelo.Observacion));
                    cmd.Parameters.Add(new OracleParameter("Usuario", usuario));
                    cmd.Parameters.Add(new OracleParameter("NumReq", modelo.NumReq));

                    var rowsAffected = await cmd.ExecuteNonQueryAsync();

                    // 3. COMMIT de la transacción
                    await transaction.CommitAsync();
                    _logger.LogInformation("✅ Certificado de fibra actualizado correctamente. TIPO=C, NUM_REQ={NumReq}, Filas={Rows}", modelo.NumReq, rowsAffected);
                    return rowsAffected > 0;
                }
                catch (OracleException oraEx) when (oraEx.Number == 54) // ORA-00054: resource busy
                {
                    _logger.LogWarning("El certificado TIPO=C NUM_REQ={NumReq} está siendo modificado por otro usuario. Intente nuevamente.", modelo.NumReq);
                    await transaction.RollbackAsync();
                    return false;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar certificado de fibra para NUM_REQ {NumReq}", modelo.NumReq);
                throw;
            }
        }

        public async Task<string> GenerarRutaPdfCertificado(string ruc, string numCer)
        {
            // Carpeta propia de fibra (SGC\CargaTC_Fibra\Certificados), mismo server que CargaTc.
            var rutaBase = _configuration["RutaCertificadosFibra"] ?? @"\\10.0.7.14\FabricaHilos\SGC\CargaTC_Fibra\Certificados";
            var año = DateTime.Now.Year.ToString();

            // Autenticarse en el recurso de red antes de crear directorios
            var username = _configuration["NetworkShare:Username"];
            var password = _configuration["NetworkShare:Password"];
            var domain = _configuration["NetworkShare:Domain"];

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    FabricaHilos.Helpers.NetworkShareHelper.Connect(rutaBase, username, password, domain);
                    _logger.LogInformation("Autenticación exitosa en el recurso de red para crear carpetas");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al autenticarse en el recurso de red");
                throw new InvalidOperationException($"No se pudo conectar al recurso de red: {ex.Message}", ex);
            }

            // Crear estructura de carpetas: RutaBase\RUC\AÑO (RUC del proveedor)
            var rutaCarpeta = Path.Combine(rutaBase, ruc, año);

            // Crear carpetas si no existen
            await Task.Run(() =>
            {
                if (!Directory.Exists(rutaCarpeta))
                {
                    Directory.CreateDirectory(rutaCarpeta);
                    _logger.LogInformation("Carpeta creada: {RutaCarpeta}", rutaCarpeta);
                }
            });

            // Nombre del archivo: NUM_CER.pdf
            var nombreArchivo = $"{numCer}.pdf";
            var rutaCompleta = Path.Combine(rutaCarpeta, nombreArchivo);

            return rutaCompleta;
        }

        // Registra (un-click) un nuevo requerimiento (REQUISICION + ITEMREQ, artículo fijo
        // X02018) y lo enlaza como una fila NUEVA en el detalle (REQ_CERT_D) de un certificado
        // (REQ_CERT) YA EXISTENTE — un mismo certificado acumula varias requisiciones/ítems en
        // su detalle a lo largo del tiempo, por eso NO se crea un REQ_CERT nuevo cada vez.
        // Devuelve REQ_CERT.NUM_REQ (el mismo recibido) para redirigir al detalle, o null si ese
        // certificado no existe.
        public async Task<int?> RegistrarRequerimientoCertificadoAsync(RegistrarRequerimientoCertDto modelo, string usuario)
        {
            try
            {
                using var conn = new OracleConnection(GetOracleConnectionString());
                await conn.OpenAsync();
                using var transaction = conn.BeginTransaction();

                try
                {
                    // 0. El certificado al que se enlazará este nuevo requerimiento debe existir
                    //    de antes; se bloquea para serializar altas concurrentes de detalle.
                    var sqlLockCert0 = $"SELECT NUM_REQ FROM {S}REQ_CERT WHERE TIPO = 'C' AND NUM_REQ = :NumReq FOR UPDATE";
                    using (var cmdLockCert0 = new OracleCommand(sqlLockCert0, conn))
                    {
                        cmdLockCert0.Transaction = transaction;
                        cmdLockCert0.BindByName = true;
                        cmdLockCert0.Parameters.Add(new OracleParameter("NumReq", modelo.NumReq));

                        using var readerCert0 = await cmdLockCert0.ExecuteReaderAsync();
                        if (!await readerCert0.ReadAsync())
                        {
                            _logger.LogWarning("No se encontró el certificado TIPO=C NUM_REQ={NumReq} para enlazar el nuevo requerimiento", modelo.NumReq);
                            await transaction.RollbackAsync();
                            return null;
                        }
                    }

                    // 1. No existe secuencia Oracle para REQUISICION.NUMREQ — la app legacy de
                    //    Logística lo numera con MAX+1 manual. Se bloquea (FOR UPDATE) la fila
                    //    con el NUMREQ máximo actual para serializar el cálculo del siguiente
                    //    valor frente a inserciones concurrentes (propias o del sistema legacy).
                    var sqlLock = $@"
                        SELECT NUMREQ FROM {S}REQUISICION
                        WHERE TIPDOC = '80' AND SERIE = 1
                          AND NUMREQ = (SELECT MAX(NUMREQ) FROM {S}REQUISICION WHERE TIPDOC = '80' AND SERIE = 1)
                        FOR UPDATE";

                    int nuevoNumReq;
                    using (var cmdLock = new OracleCommand(sqlLock, conn))
                    {
                        cmdLock.Transaction = transaction;
                        var maxActual = await cmdLock.ExecuteScalarAsync();
                        nuevoNumReq = Convert.ToInt32(maxActual) + 1;
                    }

                    // 2. Cabecera REQUISICION (patrón real SGC para solicitar el servicio de
                    //    Certificado Digital, tomado de los últimos requerimientos con X02018).
                    var sqlReq = $@"
                        INSERT INTO {S}REQUISICION
                            (TIPDOC, SERIE, NUMREQ, CENTRO_COSTO, PROVEEDORES, FECHA, F_ENTREGA,
                             RESPONSABLE, PRIORIDAD, OBSERVACION, IMPSTO, IND_SERV, AFECTO_IGV,
                             AFECTO_IRENTA, DESTINO, ESTADO, A_ADUSER, A_ADFECHA)
                        VALUES
                            ('80', 1, :NumReq, '230', :Proveedor, SYSDATE, TRUNC(SYSDATE),
                             '034685', '02', :Observacion, 0.18, 'S', 'S',
                             'N', '00', '0', :Usuario, SYSDATE)";

                    using (var cmdReq = new OracleCommand(sqlReq, conn))
                    {
                        cmdReq.Transaction = transaction;
                        cmdReq.BindByName = true;
                        cmdReq.Parameters.Add(new OracleParameter("NumReq", nuevoNumReq));
                        cmdReq.Parameters.Add(new OracleParameter("Proveedor", modelo.Proveedor.Trim()));
                        cmdReq.Parameters.Add(new OracleParameter("Observacion", modelo.Observacion.Trim()));
                        cmdReq.Parameters.Add(new OracleParameter("Usuario", usuario));
                        await cmdReq.ExecuteNonQueryAsync();
                    }

                    // 3. Único ítem: artículo fijo X02018.
                    var sqlItem = $@"
                        INSERT INTO {S}ITEMREQ
                            (TIPDOC, SERIE, NUMREQ, ORDEN, COD_ART, CANTIDAD, SALDO, UNIDAD,
                             TP_DESTINO, DESTINO, COD_SOLICITA, MONEDA, PRECIO, OBSERVACIONES,
                             A_ADUSER, A_ADFECHA)
                        VALUES
                            ('80', 1, :NumReq, 1, :CodArt, :Cantidad, :Cantidad, 'UND',
                             'U', '230', '034685', 'D', 0, :Observacion,
                             :Usuario, SYSDATE)";

                    using (var cmdItem = new OracleCommand(sqlItem, conn))
                    {
                        cmdItem.Transaction = transaction;
                        cmdItem.BindByName = true;
                        cmdItem.Parameters.Add(new OracleParameter("NumReq", nuevoNumReq));
                        cmdItem.Parameters.Add(new OracleParameter("CodArt", CodArtCertificadoDigital));
                        cmdItem.Parameters.Add(new OracleParameter("Cantidad", modelo.Cantidad));
                        cmdItem.Parameters.Add(new OracleParameter("Observacion", modelo.Observacion.Trim()));
                        cmdItem.Parameters.Add(new OracleParameter("Usuario", usuario));
                        await cmdItem.ExecuteNonQueryAsync();
                    }

                    // 4. Enlace: nueva fila en REQ_CERT_D bajo el REQ_CERT EXISTENTE recibido en
                    //    modelo.NumReq (validado y bloqueado en el paso 0). NO se crea un REQ_CERT
                    //    nuevo — un mismo certificado acumula varias requisiciones/ítems en su detalle.
                    using (var cmdCertD = new OracleCommand(
                        $"INSERT INTO {S}REQ_CERT_D (TIPO, NUM_REQ, TIPODOC, SERIE, NUMERO, A_ADUSER, A_ADFECHA) VALUES ('C', :NumReq, '80', '1', :NumRequisicion, :Usuario, SYSDATE)", conn))
                    {
                        cmdCertD.Transaction = transaction;
                        cmdCertD.BindByName = true;
                        cmdCertD.Parameters.Add(new OracleParameter("NumReq", modelo.NumReq));
                        cmdCertD.Parameters.Add(new OracleParameter("NumRequisicion", nuevoNumReq));
                        cmdCertD.Parameters.Add(new OracleParameter("Usuario", usuario));
                        await cmdCertD.ExecuteNonQueryAsync();
                    }

                    await transaction.CommitAsync();
                    _logger.LogInformation(
                        "✅ Nuevo requerimiento (REQUISICION.NUMREQ={NumRequisicion}) enlazado al certificado TIPO=C NUM_REQ={NumReq}",
                        nuevoNumReq, modelo.NumReq);
                    return modelo.NumReq;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar requerimiento de Certificado Digital");
                throw;
            }
        }
    }
}
