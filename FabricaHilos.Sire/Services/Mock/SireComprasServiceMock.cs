using System.Text;
using FabricaHilos.Sire.Interfaces;
using FabricaHilos.Sire.Models;

namespace FabricaHilos.Sire.Services.Mock;

public sealed class SireComprasServiceMock : ISireComprasService
{
    private static readonly string[] _meses = ["Enero","Febrero","Marzo","Abril","Mayo","Junio","Julio","Agosto","Septiembre","Octubre","Noviembre","Diciembre"];

    public Task<IReadOnlyList<PropuestaDto>> ObtenerPeriodosAsync(CancellationToken cancellationToken = default)
    {
        var hoy = DateTime.Today;
        var list = new List<PropuestaDto>();
        for (int i = 1; i <= 18; i++)
        {
            var d = new DateTime(hoy.Year, hoy.Month, 1).AddMonths(-i);
            var per = $"{d.Year}{d.Month:D2}";
            var desc = $"{_meses[d.Month - 1]} {d.Year}";
            var estado = i == 1 ? "PROPUESTA_DISPONIBLE" : i == 2 ? "EN_PROCESO" : "CERRADO";
            list.Add(new() { Periodo = per, Descripcion = desc, Estado = estado });
        }
        return Task.FromResult<IReadOnlyList<PropuestaDto>>(list);
    }

    public Task<IReadOnlyList<RegistroCompra>> ObtenerPropuestaAsync(string periodo, CancellationToken cancellationToken = default)
    {
        static RegistroCompra Compra(string per, string cuo, string seq, string fecha, string venc,
            string tipo, string serie, string numero, string tdoc, string ndoc, string razon,
            decimal baseGrav, decimal igv, decimal total, decimal tc = 0m,
            string ncFecha = "", string ncTipo = "", string ncSerie = "", string ncNum = "",
            string detraccion = "", string retencion = "0", string clasif = "") => new()
        {
            PeriodoTributario = per, Cuo = cuo, CorrelativoAsiento = seq,
            FechaEmision = fecha, FechaVencimientoPago = venc,
            TipoComprobante = tipo, SerieComprobante = serie, AnioDuaDsi = "", NumeroComprobante = numero,
            TipoDocIdentidadProveedor = tdoc, NumeroDocIdentidadProveedor = ndoc, RazonSocialProveedor = razon,
            BaseImponibleGravadaDestinoGravadas = baseGrav, IgvDestinoGravadas = igv,
            BaseImponibleGravadaDestinoMixtas = 0m, IgvDestinoMixtas = 0m,
            BaseImponibleGravadaDestinoNoGravadas = 0m, IgvDestinoNoGravadas = 0m,
            ValorAdquisicionesNoGravadas = 0m, Isc = 0m, Icbper = 0m, OtrosTributosCargos = 0m,
            ImporteTotal = total, TipoCambio = tc,
            FechaEmisionDocModificado = ncFecha, TipoDocModificado = ncTipo,
            SerieDocModificado = ncSerie, CodigoDependenciaAduanera = "", NumeroDocModificado = ncNum,
            NumeroConstanciaDetraccion = detraccion, IndicadorSujetoRetencion = retencion,
            ClasificacionBienesServicios = clasif, IdentificacionContrato = "", CodigoErrorTipo1 = "", Estado = "1"
        };

        var yr = periodo.Length == 6 ? periodo[..4] : "2025";
        var mm = periodo.Length == 6 ? periodo[4..] : "04";
        string D(int d) => $"{d:D2}/{mm}/{yr}";
        string V(int d) => $"{d + 30:D2}/{mm}/{yr}";

        var data = new List<RegistroCompra>
        {
            Compra($"{yr}/{mm}", $"{periodo}-C001","001",D(2), V(2), "01","F100","00012345","6","20111111111","PROVEEDOR INDUSTRIAL SAC",       1500m,  270m,  1770m),
            Compra($"{yr}/{mm}", $"{periodo}-C002","002",D(3), V(3), "01","E001","00000123","6","20888777666","ENERGIA ELECTRICA SA",            4200m,  756m,  4956m),
            Compra($"{yr}/{mm}", $"{periodo}-C003","003",D(5), D(5), "03","B200","00056789","6","20444444444","SERVICIOS GENERALES EIRL",         850m,  153m,  1003m, detraccion:"NCD-00012", retencion:"1", clasif:"01"),
            Compra($"{yr}/{mm}", $"{periodo}-C004","004",D(8), V(8), "01","F500","00000078","6","20555666778","INSUMOS QUIMICOS SAC",            3800m,  684m,  4484m),
            Compra($"{yr}/{mm}", $"{periodo}-C005","005",D(10),V(10),"01","F010","00000234","6","20777888999","IMPORTACIONES ALFA SAC",          6500m, 1170m,  7670m, tc:3.82m),
            Compra($"{yr}/{mm}", $"{periodo}-C006","006",D(12),V(12),"01","F100","00012400","6","20111111111","PROVEEDOR INDUSTRIAL SAC",        2300m,  414m,  2714m),
            Compra($"{yr}/{mm}", $"{periodo}-C007","007",D(15),D(15),"03","B050","00000890","6","20321654987","REPUESTOS Y MECANICA SRL",         480m,   86.4m, 566.4m),
            Compra($"{yr}/{mm}", $"{periodo}-C008","008",D(18),V(18),"01","F300","00001100","6","20147258369","FIBRA TEXTIL DEL SUR SAC",        9100m, 1638m, 10738m, detraccion:"NCD-00045"),
            Compra($"{yr}/{mm}", $"{periodo}-C009","009",D(20),V(20),"07","NC01","00000015","6","20999999999","INSUMOS TEXTILES SAC",            -200m,  -36m,  -236m,
                ncFecha:D(3), ncTipo:"01", ncSerie:"F500", ncNum:"00000078"),
            Compra($"{yr}/{mm}", $"{periodo}-C010","010",D(22),V(22),"01","F220","00004567","6","20654321098","TRANSPORTES LOGISTICA SA",         750m,  135m,   885m, clasif:"02"),
            Compra($"{yr}/{mm}", $"{periodo}-C011","011",D(25),V(25),"01","F100","00012500","6","20111111111","PROVEEDOR INDUSTRIAL SAC",        1100m,  198m,  1298m),
            Compra($"{yr}/{mm}", $"{periodo}-C012","012",D(28),V(28),"01","F800","00009870","6","20456123789","LUBRICANTES Y ACEITES SAC",        320m,   57.6m, 377.6m),
        };

        return Task.FromResult<IReadOnlyList<RegistroCompra>>(data);
    }

    public Task<TicketEstado> AceptarPropuestaAsync(string periodo, CancellationToken cancellationToken = default)
        => Task.FromResult(new TicketEstado { Ticket = $"RCE-ACEPTAR-{periodo}", Estado = "COMPLETADO", Mensaje = "Propuesta RCE aceptada" });

    public Task<TicketEstado> ReemplazarPropuestaAsync(string periodo, Stream contenidoArchivo, string nombreArchivo, CancellationToken cancellationToken = default)
        => Task.FromResult(new TicketEstado { Ticket = $"RCE-REEMPLAZO-{periodo}", Estado = "COMPLETADO", Mensaje = $"Archivo {nombreArchivo} procesado" });

    public Task<TicketEstado> CerrarPeriodoAsync(string periodo, CancellationToken cancellationToken = default)
        => Task.FromResult(new TicketEstado { Ticket = $"RCE-CIERRE-{periodo}", Estado = "COMPLETADO", Mensaje = "Periodo RCE cerrado" });

    public Task<TicketEstado> ConsultarTicketAsync(string numTicket, string periodo, CancellationToken cancellationToken = default)
        => Task.FromResult(new TicketEstado { Ticket = numTicket, Estado = "COMPLETADO", Mensaje = "[MOCK] Ticket RCE procesado" });

    public Task<ConstanciaCierre> DescargarConstanciaAsync(string periodo, CancellationToken cancellationToken = default)
    {
        var text = $"CONSTANCIA RCE MOCK\nPeriodo: {periodo}\nFecha: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
        return Task.FromResult(new ConstanciaCierre
        {
            NombreArchivo = $"RCE_Constancia_{periodo}.txt",
            ContentType = "text/plain",
            Contenido = Encoding.UTF8.GetBytes(text)
        });
    }
}
