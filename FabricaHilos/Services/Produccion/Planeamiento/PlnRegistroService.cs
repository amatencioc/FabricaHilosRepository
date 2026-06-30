using System.Data;
using FabricaHilos.Models.Produccion.Planeamiento;
using Oracle.ManagedDataAccess.Client;

namespace FabricaHilos.Services.Produccion.Planeamiento;

public class PlnRegistroService : OracleServiceBase, IPlnRegistroService
{
    private const int TimeoutSeconds = 120;

    public PlnRegistroService(
        IConfiguration       configuration,
        IHttpContextAccessor httpContextAccessor)
        : base(configuration, httpContextAccessor) { }

    // ── helpers ──────────────────────────────────────────────────────────────
    private static DateTime? SafeDate(object? val) =>
        val == null || val == DBNull.Value ? null : Convert.ToDateTime(val);

    private static string SafeStr(object? val) =>
        val == null || val == DBNull.Value ? "" : val.ToString()!.Trim();

    private static decimal SafeDec(object? val) =>
        val == null || val == DBNull.Value ? 0m :
        decimal.TryParse(val.ToString(), out var d) ? d : 0m;

    private static long? SafeLong(object? val) =>
        val == null || val == DBNull.Value ? null :
        long.TryParse(val.ToString(), out var l) ? l : null;

    // Devuelve DBNull.Value si el string es nulo/vacío → el parámetro llega NULL al SP
    private static object NullOrValue(string? v) =>
        string.IsNullOrEmpty(v) ? DBNull.Value : (object)v;

    // ── query principal ─────────────────────────────────────────────────────
    public async Task<IReadOnlyList<RegistroPedidoItem>> GetRegistroDiarioAsync(
        DateTime? fchDesde,
        DateTime? fchHasta,
        string   filtroServ         = "",
        string   filtroCliente      = "",
        string   filtroProceso      = "",
        string   filtroEstado       = "",
        string   filtroTfibra       = "",
        string   filtroPasoActual   = "",
        DateTime? fchEntDesde       = null,
        DateTime? fchEntHasta       = null)
    {
        var result = new List<RegistroPedidoItem>(256);

        await using var conn = new OracleConnection(GetOracleConnectionString());
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText    = $"{S}PKG_PLN.SP_PLN_REGISTRO_DIARIO";
        cmd.CommandType    = CommandType.StoredProcedure;
        cmd.BindByName     = true;
        cmd.CommandTimeout = TimeoutSeconds;

        cmd.Parameters.Add("p_fch_desde",     OracleDbType.Date).Value     = (object?)fchDesde     ?? DBNull.Value;
        cmd.Parameters.Add("p_fch_hasta",     OracleDbType.Date).Value     = (object?)fchHasta     ?? DBNull.Value;
        cmd.Parameters.Add("p_cod_serv",      OracleDbType.Varchar2).Value = NullOrValue(filtroServ);
        cmd.Parameters.Add("p_cod_cliente",   OracleDbType.Varchar2).Value = NullOrValue(filtroCliente);
        cmd.Parameters.Add("p_proceso",       OracleDbType.Varchar2).Value = NullOrValue(filtroProceso);
        cmd.Parameters.Add("p_estado",        OracleDbType.Varchar2).Value = NullOrValue(filtroEstado);
        cmd.Parameters.Add("p_tipo_fibra",    OracleDbType.Varchar2).Value = NullOrValue(filtroTfibra);
        cmd.Parameters.Add("p_paso_actual",   OracleDbType.Varchar2).Value = NullOrValue(filtroPasoActual);
        cmd.Parameters.Add("p_fch_ent_desde", OracleDbType.Date).Value     = (object?)fchEntDesde ?? DBNull.Value;
        cmd.Parameters.Add("p_fch_ent_hasta", OracleDbType.Date).Value     = (object?)fchEntHasta ?? DBNull.Value;
        var pCursor = cmd.Parameters.Add("p_cursor", OracleDbType.RefCursor);
        pCursor.Direction = ParameterDirection.Output;

        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync();
        // Reducir round-trips a Oracle leyendo filas en bloques de 1 MB
        reader.FetchSize = 1_048_576;

        // Pre-calcular ordinals una sola vez para evitar O(n×cols) lookups por nombre
        var oCodCliente       = reader.GetOrdinal("cod_cliente");
        var oNombreCliente    = reader.GetOrdinal("nombre_cliente");
        var oCodVende         = reader.GetOrdinal("cod_vende");
        var oNombreVende      = reader.GetOrdinal("nombre_vende");
        var oGiro             = reader.GetOrdinal("giro");
        var oNumPed           = reader.GetOrdinal("num_ped");
        var oSerie            = reader.GetOrdinal("serie");
        var oNro              = reader.GetOrdinal("nro");
        var oFchPedido        = reader.GetOrdinal("fch_pedido");
        var oFchAprobacion    = reader.GetOrdinal("f_aprobacion");
        var oFMaxPed          = reader.GetOrdinal("f_maxped");
        var oEstadoPed        = reader.GetOrdinal("estado_ped");
        var oEstadoItem       = reader.GetOrdinal("estado_item");
        var oCodArt           = reader.GetOrdinal("cod_art");
        var oDescArt          = reader.GetOrdinal("desc_art");
        var oTipoFibra        = reader.GetOrdinal("tipo_fibra");
        var oDescFibra        = reader.GetOrdinal("desc_fibra");
        var oTitulo           = reader.GetOrdinal("titulo");
        var oProceso          = reader.GetOrdinal("proceso");
        var oDescProceso      = reader.GetOrdinal("desc_proceso");
        var oCodServ          = reader.GetOrdinal("cod_serv");
        var oNroRmc           = reader.GetOrdinal("nro_rmc");
        var oRmc              = reader.GetOrdinal("rmc");
        var oDescRmc          = reader.GetOrdinal("desc_rmc");
        var oLote             = reader.GetOrdinal("lote");
        var oColor            = reader.GetOrdinal("color");
        var oColorDet         = reader.GetOrdinal("color_det");
        var oIntensidad       = reader.GetOrdinal("intensidad");
        var oIntensidadAbrev  = reader.GetOrdinal("intensidad_abrev");
        var oPresentacion     = reader.GetOrdinal("presentacion");
        var oCantidad         = reader.GetOrdinal("cantidad");
        var oPrecio           = reader.GetOrdinal("precio");
        var oFhcEntrega       = reader.GetOrdinal("fhc_entrega");
        var oFchEntregaComp   = reader.GetOrdinal("fch_entrega_comp");
        var oPasoActual       = reader.GetOrdinal("paso_actual");
        var oCodPasoAct       = reader.GetOrdinal("cod_paso_act");
        var oPasoColor        = reader.GetOrdinal("paso_color");
        var oDiasRetraso      = reader.GetOrdinal("dias_retraso");
        var oIndRetraso       = reader.GetOrdinal("ind_retraso");
        var oIndUrgente       = reader.GetOrdinal("ind_urgente");
        var oLeadTime         = reader.GetOrdinal("lead_time");
        var oUrgente          = reader.GetOrdinal("urgente");
        var oNroprog          = reader.GetOrdinal("nroprog");
        var oNumPartida       = reader.GetOrdinal("num_partida");
        var oKgPendientes     = reader.GetOrdinal("kg_pendientes");
        var oEstadoProg       = reader.GetOrdinal("estado_prog");
        var oSoloDespacho     = reader.GetOrdinal("solo_despacho");
        var oDetalle          = reader.GetOrdinal("detalle");
        var oObservaciones    = reader.GetOrdinal("observaciones");
        var oUnidad           = reader.GetOrdinal("unidad");
        var oEnconado         = reader.GetOrdinal("enconado");
        var oParafina         = reader.GetOrdinal("parafina");
        var oCodFam           = reader.GetOrdinal("cod_fam");
        var oDescFamilia      = reader.GetOrdinal("desc_familia");
        var oCodLin           = reader.GetOrdinal("cod_lin");
        var oDescLinea        = reader.GetOrdinal("desc_linea");

        while (await reader.ReadAsync())
        {
            result.Add(new RegistroPedidoItem
            {
                // ─ Pedido / cliente ───────────────────────────────────────────────────
                CodCliente      = SafeStr(reader.GetValue(oCodCliente)),
                NombreCliente   = SafeStr(reader.GetValue(oNombreCliente)),
                CodVende        = SafeStr(reader.GetValue(oCodVende)),
                NombreVende     = SafeStr(reader.GetValue(oNombreVende)),
                Giro            = SafeStr(reader.GetValue(oGiro)),
                NumPed          = reader.GetInt64(oNumPed),
                Serie           = reader.GetInt32(oSerie),
                Nro             = reader.GetInt32(oNro),
                FchPedido       = reader.GetDateTime(oFchPedido),
                FchAprobacion   = SafeDate(reader.GetValue(oFchAprobacion)),
                FMaxPed         = SafeDate(reader.GetValue(oFMaxPed)),
                EstadoPed       = SafeStr(reader.GetValue(oEstadoPed)),
                EstadoItem      = SafeStr(reader.GetValue(oEstadoItem)),
                // ─ Artículo / material ──────────────────────────────────────────────
                CodArt          = SafeStr(reader.GetValue(oCodArt)),
                DescArt         = SafeStr(reader.GetValue(oDescArt)),
                TipoFibra       = SafeStr(reader.GetValue(oTipoFibra)),
                DescTfibra      = SafeStr(reader.GetValue(oDescFibra)),
                Titulo          = SafeStr(reader.GetValue(oTitulo)),
                Proceso         = SafeStr(reader.GetValue(oProceso)),
                NombreProcesoDb = SafeStr(reader.GetValue(oDescProceso)),
                CodServ         = SafeStr(reader.GetValue(oCodServ)),
                // ─ Hilandería ──────────────────────────────────────────────────────
                NroRmc          = SafeLong(reader.GetValue(oNroRmc)),
                Rmc             = SafeStr(reader.GetValue(oRmc)),
                DescRmc         = SafeStr(reader.GetValue(oDescRmc)),
                Lote            = SafeStr(reader.GetValue(oLote)),
                // ─ Color / presentación ─────────────────────────────────────────────
                Color           = SafeStr(reader.GetValue(oColor)),
                ColorDet        = SafeStr(reader.GetValue(oColorDet)),
                Intensidad      = SafeStr(reader.GetValue(oIntensidad)),
                IntensidadAbrev = SafeStr(reader.GetValue(oIntensidadAbrev)),
                Presentacion    = SafeStr(reader.GetValue(oPresentacion)),
                // ─ Cantidades ─────────────────────────────────────────────────────────
                Cantidad        = SafeDec(reader.GetValue(oCantidad)),
                Precio          = SafeDec(reader.GetValue(oPrecio)),
                // ─ Fechas ────────────────────────────────────────────────────────────
                FhcEntrega      = SafeDate(reader.GetValue(oFhcEntrega)),
                FchEntregaComp  = SafeDate(reader.GetValue(oFchEntregaComp)),
                // ─ Estado PLN ───────────────────────────────────────────────────────
                PasoActual      = SafeStr(reader.GetValue(oPasoActual)),
                CodPasoAct      = SafeStr(reader.GetValue(oCodPasoAct)),
                PasoActualColor = SafeStr(reader.GetValue(oPasoColor)),
                DiasRetraso     = (int)SafeDec(reader.GetValue(oDiasRetraso)),
                IndRetraso      = SafeStr(reader.GetValue(oIndRetraso)),
                IndUrgente      = SafeStr(reader.GetValue(oIndUrgente)),
                LeadTime        = reader.IsDBNull(oLeadTime) ? null : (int?)Convert.ToInt32(reader.GetValue(oLeadTime)),
                Urgente         = SafeStr(reader.GetValue(oUrgente)),
                Nroprog         = SafeLong(reader.GetValue(oNroprog)),
                NumPartida      = SafeLong(reader.GetValue(oNumPartida)),
                KgPendientes    = SafeDec(reader.GetValue(oKgPendientes)),
                EstadoProg      = SafeStr(reader.GetValue(oEstadoProg)),
                // ─ Flags / familias ────────────────────────────────────────────────
                SoloDespacho    = SafeStr(reader.GetValue(oSoloDespacho)),
                Detalle         = SafeStr(reader.GetValue(oDetalle)),
                Observaciones   = SafeStr(reader.GetValue(oObservaciones)),
                Unidad          = SafeStr(reader.GetValue(oUnidad)),
                Enconado        = SafeStr(reader.GetValue(oEnconado)),
                Parafina        = SafeStr(reader.GetValue(oParafina)),
                CodFam          = SafeStr(reader.GetValue(oCodFam)),
                DescFamilia     = SafeStr(reader.GetValue(oDescFamilia)),
                CodLin          = SafeStr(reader.GetValue(oCodLin)),
                DescLinea       = SafeStr(reader.GetValue(oDescLinea)),
            });
        }

        return result.AsReadOnly();
    }
}
