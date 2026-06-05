using System.Text;
using FabricaHilos.Sire.Interfaces;
using FabricaHilos.Sire.Models;

namespace FabricaHilos.Sire.Services.Mock;

public sealed class SireVentasServiceMock : ISireVentasService
{
    private static readonly string[] _meses = ["Enero","Febrero","Marzo","Abril","Mayo","Junio","Julio","Agosto","Septiembre","Octubre","Noviembre","Diciembre"];

    public Task<IReadOnlyList<PropuestaDto>> ObtenerPeriodosAsync(CancellationToken cancellationToken = default)
    {
        var hoy = DateTime.Today;
        var list = new List<PropuestaDto>();
        // Meses hacia atrás desde el mes anterior al actual
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

    public Task<IReadOnlyList<RegistroVenta>> ObtenerPropuestaAsync(string periodo, CancellationToken cancellationToken = default)
    {
        static RegistroVenta Venta(string per, string cuo, string seq, string fecha, string tipo, string serie, string numero,
            string tdoc, string ndoc, string razon, decimal base_, decimal igv, decimal total, decimal tc = 0m,
            string modFecha = "", string modTipo = "", string modSerie = "", string modNum = "", string cancelado = "1") => new()
        {
            PeriodoTributario = per, Cuo = cuo, CorrelativoAsiento = seq, FechaEmision = fecha, FechaVencimientoPago = "",
            TipoComprobante = tipo, SerieComprobante = serie, AnioDuaDsi = "", NumeroComprobante = numero, NumeroFinalComprobante = "",
            TipoDocIdentidadCliente = tdoc, NumeroDocIdentidadCliente = ndoc, RazonSocialCliente = razon,
            BaseImponibleGravada = base_, BaseImponibleGravadaTasaDiferenciada = 0m, IgvTasaDiferenciada = 0m,
            BaseImponibleIsc = 0m, Isc = 0m, BaseImponibleIvap = 0m, Ivap = 0m,
            OperacionesExoneradas = 0m, OperacionesInafectas = 0m, Igv = igv, Icbper = 0m,
            OtrosTributosCargos = 0m, ImporteTotal = total, TipoCambio = tc,
            FechaEmisionDocModificado = modFecha, TipoDocModificado = modTipo,
            SerieDocModificado = modSerie, NumeroDocModificado = modNum,
            CodigoErrorTipo1 = "", IndicadorComprobanteCancelado = cancelado, Estado = "1"
        };

        // Periodo base para el formato dd/MM/yyyy del mes del periodo solicitado
        var yr = periodo.Length == 6 ? periodo[..4] : "2025";
        var mm = periodo.Length == 6 ? periodo[4..] : "04";
        string D(int d) => $"{d:D2}/{mm}/{yr}";

        var data = new List<RegistroVenta>
        {
            Venta($"{yr}/{mm}", $"{periodo}-V001", "001", D(2),  "01","F001","00000001","6","20100096260","CLIENTE INDUSTRIAL SAC",         4500m,  810m,  5310m),
            Venta($"{yr}/{mm}", $"{periodo}-V002", "002", D(5),  "01","F001","00000002","6","20123456789","TEXTILES DEL PERU SAC",          3200m,  576m,  3776m),
            Venta($"{yr}/{mm}", $"{periodo}-V003", "003", D(8),  "03","B001","00000215","1","73456789",   "PERSONA NATURAL",                 350m,   63m,   413m),
            Venta($"{yr}/{mm}", $"{periodo}-V004", "004", D(10), "01","F001","00000003","6","20456789123","IMPORTACIONES SOTO SAC",         8900m, 1602m, 10502m),
            Venta($"{yr}/{mm}", $"{periodo}-V005", "005", D(11), "01","F001","00000004","6","20789123456","DISTRIBUIDORA NORTE SRL",        2100m,  378m,  2478m),
            Venta($"{yr}/{mm}", $"{periodo}-V006", "006", D(14), "03","B001","00000216","1","41234567",   "CONSUMIDOR FINAL",                180m,   32m,   212m,  3.75m),
            Venta($"{yr}/{mm}", $"{periodo}-V007", "007", D(16), "01","F001","00000005","6","20222333444","EXPORTADORA COLONIAL SAC",       6750m, 1215m,  7965m),
            Venta($"{yr}/{mm}", $"{periodo}-V008", "008", D(18), "01","F002","00000001","6","20333444555","GRUPO TEXTIL ANDINO SAC",        5400m,  972m,  6372m),
            Venta($"{yr}/{mm}", $"{periodo}-V009", "009", D(22), "07","FC01","00000001","6","20123456789","TEXTILES DEL PERU SAC",          -320m,  -57.6m, -377.6m,
                modFecha: D(5), modTipo:"01", modSerie:"F001", modNum:"00000002"),
            Venta($"{yr}/{mm}", $"{periodo}-V010", "010", D(25), "01","F001","00000006","6","20555666777","HILANDERIA MODELO SAC",          1800m,  324m,  2124m),
            Venta($"{yr}/{mm}", $"{periodo}-V011", "011", D(28), "01","F001","00000007","6","20100096260","CLIENTE INDUSTRIAL SAC",         2950m,  531m,  3481m),
            Venta($"{yr}/{mm}", $"{periodo}-V012", "012", D(30), "03","B002","00000045", "0","-",         "VENTA MOSTRADOR",                 520m,   93.6m, 613.6m),
        };

        return Task.FromResult<IReadOnlyList<RegistroVenta>>(data);
    }

    public Task<TicketEstado> AceptarPropuestaAsync(string periodo, CancellationToken cancellationToken = default)
        => Task.FromResult(new TicketEstado { Ticket = $"RVIE-ACEPTAR-{periodo}", Estado = "COMPLETADO", Mensaje = "Propuesta RVIE aceptada" });

    public Task<TicketEstado> ReemplazarPropuestaAsync(string periodo, Stream contenidoArchivo, string nombreArchivo, CancellationToken cancellationToken = default)
        => Task.FromResult(new TicketEstado { Ticket = $"RVIE-REEMPLAZO-{periodo}", Estado = "COMPLETADO", Mensaje = $"Archivo {nombreArchivo} procesado" });

    public Task<TicketEstado> CerrarPeriodoAsync(string periodo, CancellationToken cancellationToken = default)
        => Task.FromResult(new TicketEstado { Ticket = $"RVIE-CIERRE-{periodo}", Estado = "COMPLETADO", Mensaje = "Periodo RVIE cerrado" });

    public Task<TicketEstado> ConsultarTicketAsync(string numTicket, string periodo, CancellationToken cancellationToken = default)
        => Task.FromResult(new TicketEstado { Ticket = numTicket, Estado = "COMPLETADO", Mensaje = "[MOCK] Ticket RVIE procesado" });

    public Task<ConstanciaCierre> DescargarConstanciaAsync(string periodo, CancellationToken cancellationToken = default)
    {
        var text = $"CONSTANCIA RVIE MOCK\nPeriodo: {periodo}\nFecha: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
        return Task.FromResult(new ConstanciaCierre
        {
            NombreArchivo = $"RVIE_Constancia_{periodo}.txt",
            ContentType = "text/plain",
            Contenido = Encoding.UTF8.GetBytes(text)
        });
    }
}
