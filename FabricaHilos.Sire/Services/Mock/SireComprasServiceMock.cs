using System.Text;
using FabricaHilos.Sire.Interfaces;
using FabricaHilos.Sire.Models;

namespace FabricaHilos.Sire.Services.Mock;

public sealed class SireComprasServiceMock : ISireComprasService
{
    public Task<IReadOnlyList<PropuestaDto>> ObtenerPeriodosAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PropuestaDto>>([
            new() { Periodo = "202504", Descripcion = "Abril 2025", Estado = "PROPUESTA_DISPONIBLE" },
            new() { Periodo = "202503", Descripcion = "Marzo 2025", Estado = "CERRADO" },
            new() { Periodo = "202502", Descripcion = "Febrero 2025", Estado = "CERRADO" }
        ]);

    public Task<IReadOnlyList<RegistroCompra>> ObtenerPropuestaAsync(string periodo, CancellationToken cancellationToken = default)
    {
        var data = new List<RegistroCompra>
        {
            new()
            {
                PeriodoTributario = "2025/04", Cuo = "C001", CorrelativoAsiento = "001", FechaEmision = "03/04/2025", FechaVencimientoPago = "03/04/2025",
                TipoComprobante = "01", SerieComprobante = "F100", AnioDuaDsi = "", NumeroComprobante = "00012345", TipoDocIdentidadProveedor = "6",
                NumeroDocIdentidadProveedor = "20111111111", RazonSocialProveedor = "PROVEEDOR INDUSTRIAL SAC",
                BaseImponibleGravadaDestinoGravadas = 1500m, IgvDestinoGravadas = 270m, BaseImponibleGravadaDestinoMixtas = 0m,
                IgvDestinoMixtas = 0m, BaseImponibleGravadaDestinoNoGravadas = 0m, IgvDestinoNoGravadas = 0m,
                ValorAdquisicionesNoGravadas = 0m, Isc = 0m, Icbper = 0m, OtrosTributosCargos = 0m,
                ImporteTotal = 1770m, TipoCambio = 0m, FechaEmisionDocModificado = "", TipoDocModificado = "", SerieDocModificado = "",
                CodigoDependenciaAduanera = "", NumeroDocModificado = "", NumeroConstanciaDetraccion = "",
                IndicadorSujetoRetencion = "0", ClasificacionBienesServicios = "", IdentificacionContrato = "", CodigoErrorTipo1 = "", Estado = "1"
            },
            new()
            {
                PeriodoTributario = "2025/04", Cuo = "C002", CorrelativoAsiento = "002", FechaEmision = "12/04/2025", FechaVencimientoPago = "12/04/2025",
                TipoComprobante = "03", SerieComprobante = "B200", AnioDuaDsi = "", NumeroComprobante = "00056789", TipoDocIdentidadProveedor = "6",
                NumeroDocIdentidadProveedor = "20444444444", RazonSocialProveedor = "SERVICIOS GENERALES EIRL",
                BaseImponibleGravadaDestinoGravadas = 850m, IgvDestinoGravadas = 153m, BaseImponibleGravadaDestinoMixtas = 0m,
                IgvDestinoMixtas = 0m, BaseImponibleGravadaDestinoNoGravadas = 0m, IgvDestinoNoGravadas = 0m,
                ValorAdquisicionesNoGravadas = 0m, Isc = 0m, Icbper = 0m, OtrosTributosCargos = 0m,
                ImporteTotal = 1003m, TipoCambio = 0m, FechaEmisionDocModificado = "", TipoDocModificado = "", SerieDocModificado = "",
                CodigoDependenciaAduanera = "", NumeroDocModificado = "", NumeroConstanciaDetraccion = "NCD-00012",
                IndicadorSujetoRetencion = "1", ClasificacionBienesServicios = "01", IdentificacionContrato = "", CodigoErrorTipo1 = "", Estado = "1"
            },
            new()
            {
                PeriodoTributario = "2025/04", Cuo = "C003", CorrelativoAsiento = "003", FechaEmision = "20/04/2025", FechaVencimientoPago = "20/04/2025",
                TipoComprobante = "07", SerieComprobante = "NC01", AnioDuaDsi = "", NumeroComprobante = "00000015", TipoDocIdentidadProveedor = "6",
                NumeroDocIdentidadProveedor = "20999999999", RazonSocialProveedor = "INSUMOS TEXTILES SAC",
                BaseImponibleGravadaDestinoGravadas = -200m, IgvDestinoGravadas = -36m, BaseImponibleGravadaDestinoMixtas = 0m,
                IgvDestinoMixtas = 0m, BaseImponibleGravadaDestinoNoGravadas = 0m, IgvDestinoNoGravadas = 0m,
                ValorAdquisicionesNoGravadas = 0m, Isc = 0m, Icbper = 0m, OtrosTributosCargos = 0m,
                ImporteTotal = -236m, TipoCambio = 0m, FechaEmisionDocModificado = "03/04/2025", TipoDocModificado = "01", SerieDocModificado = "F100",
                CodigoDependenciaAduanera = "", NumeroDocModificado = "00012345", NumeroConstanciaDetraccion = "",
                IndicadorSujetoRetencion = "0", ClasificacionBienesServicios = "", IdentificacionContrato = "", CodigoErrorTipo1 = "", Estado = "1"
            }
        };

        return Task.FromResult<IReadOnlyList<RegistroCompra>>(data);
    }

    public Task<TicketEstado> AceptarPropuestaAsync(string periodo, CancellationToken cancellationToken = default)
        => Task.FromResult(new TicketEstado { Ticket = $"RCE-ACEPTAR-{periodo}", Estado = "COMPLETADO", Mensaje = "Propuesta RCE aceptada" });

    public Task<TicketEstado> ReemplazarPropuestaAsync(string periodo, Stream contenidoArchivo, string nombreArchivo, CancellationToken cancellationToken = default)
        => Task.FromResult(new TicketEstado { Ticket = $"RCE-REEMPLAZO-{periodo}", Estado = "COMPLETADO", Mensaje = $"Archivo {nombreArchivo} procesado" });

    public Task<TicketEstado> CerrarPeriodoAsync(string periodo, CancellationToken cancellationToken = default)
        => Task.FromResult(new TicketEstado { Ticket = $"RCE-CIERRE-{periodo}", Estado = "COMPLETADO", Mensaje = "Periodo RCE cerrado" });

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
