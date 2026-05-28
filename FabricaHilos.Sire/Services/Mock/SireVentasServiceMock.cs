using System.Text;
using FabricaHilos.Sire.Interfaces;
using FabricaHilos.Sire.Models;

namespace FabricaHilos.Sire.Services.Mock;

public sealed class SireVentasServiceMock : ISireVentasService
{
    public Task<IReadOnlyList<PropuestaDto>> ObtenerPeriodosAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PropuestaDto>>([
            new() { Periodo = "202504", Descripcion = "Abril 2025", Estado = "PROPUESTA_DISPONIBLE" },
            new() { Periodo = "202503", Descripcion = "Marzo 2025", Estado = "CERRADO" },
            new() { Periodo = "202502", Descripcion = "Febrero 2025", Estado = "CERRADO" }
        ]);

    public Task<IReadOnlyList<RegistroVenta>> ObtenerPropuestaAsync(string periodo, CancellationToken cancellationToken = default)
    {
        var data = new List<RegistroVenta>
        {
            new()
            {
                PeriodoTributario = "2025/04", Cuo = "M001", CorrelativoAsiento = "001", FechaEmision = "02/04/2025", FechaVencimientoPago = "",
                TipoComprobante = "01", SerieComprobante = "F001", AnioDuaDsi = "", NumeroComprobante = "00000001", NumeroFinalComprobante = "",
                TipoDocIdentidadCliente = "6", NumeroDocIdentidadCliente = "20123456789", RazonSocialCliente = "EMPRESA SAC",
                BaseImponibleGravada = 1000m, BaseImponibleGravadaTasaDiferenciada = 0m, IgvTasaDiferenciada = 0m,
                BaseImponibleIsc = 0m, Isc = 0m, BaseImponibleIvap = 0m, Ivap = 0m,
                OperacionesExoneradas = 0m, OperacionesInafectas = 0m, Igv = 180m, Icbper = 0m,
                OtrosTributosCargos = 0m, ImporteTotal = 1180m, TipoCambio = 0m,
                FechaEmisionDocModificado = "", TipoDocModificado = "", SerieDocModificado = "", NumeroDocModificado = "",
                CodigoErrorTipo1 = "", IndicadorComprobanteCancelado = "1", Estado = "1"
            },
            new()
            {
                PeriodoTributario = "2025/04", Cuo = "M002", CorrelativoAsiento = "002", FechaEmision = "10/04/2025", FechaVencimientoPago = "",
                TipoComprobante = "03", SerieComprobante = "B001", AnioDuaDsi = "", NumeroComprobante = "00000127", NumeroFinalComprobante = "",
                TipoDocIdentidadCliente = "1", NumeroDocIdentidadCliente = "72123456", RazonSocialCliente = "CLIENTE FINAL",
                BaseImponibleGravada = 250m, BaseImponibleGravadaTasaDiferenciada = 0m, IgvTasaDiferenciada = 0m,
                BaseImponibleIsc = 0m, Isc = 0m, BaseImponibleIvap = 0m, Ivap = 0m,
                OperacionesExoneradas = 0m, OperacionesInafectas = 0m, Igv = 45m, Icbper = 0m,
                OtrosTributosCargos = 0m, ImporteTotal = 295m, TipoCambio = 0m,
                FechaEmisionDocModificado = "", TipoDocModificado = "", SerieDocModificado = "", NumeroDocModificado = "",
                CodigoErrorTipo1 = "", IndicadorComprobanteCancelado = "1", Estado = "1"
            },
            new()
            {
                PeriodoTributario = "2025/04", Cuo = "M003", CorrelativoAsiento = "003", FechaEmision = "15/04/2025", FechaVencimientoPago = "",
                TipoComprobante = "07", SerieComprobante = "FC01", AnioDuaDsi = "", NumeroComprobante = "00000009", NumeroFinalComprobante = "",
                TipoDocIdentidadCliente = "6", NumeroDocIdentidadCliente = "20555666777", RazonSocialCliente = "DISTRIBUIDORA SRL",
                BaseImponibleGravada = -100m, BaseImponibleGravadaTasaDiferenciada = 0m, IgvTasaDiferenciada = 0m,
                BaseImponibleIsc = 0m, Isc = 0m, BaseImponibleIvap = 0m, Ivap = 0m,
                OperacionesExoneradas = 0m, OperacionesInafectas = 0m, Igv = -18m, Icbper = 0m,
                OtrosTributosCargos = 0m, ImporteTotal = -118m, TipoCambio = 0m,
                FechaEmisionDocModificado = "02/04/2025", TipoDocModificado = "01", SerieDocModificado = "F001", NumeroDocModificado = "00000001",
                CodigoErrorTipo1 = "", IndicadorComprobanteCancelado = "1", Estado = "1"
            }
        };

        return Task.FromResult<IReadOnlyList<RegistroVenta>>(data);
    }

    public Task<TicketEstado> AceptarPropuestaAsync(string periodo, CancellationToken cancellationToken = default)
        => Task.FromResult(new TicketEstado { Ticket = $"RVIE-ACEPTAR-{periodo}", Estado = "COMPLETADO", Mensaje = "Propuesta RVIE aceptada" });

    public Task<TicketEstado> ReemplazarPropuestaAsync(string periodo, Stream contenidoArchivo, string nombreArchivo, CancellationToken cancellationToken = default)
        => Task.FromResult(new TicketEstado { Ticket = $"RVIE-REEMPLAZO-{periodo}", Estado = "COMPLETADO", Mensaje = $"Archivo {nombreArchivo} procesado" });

    public Task<TicketEstado> CerrarPeriodoAsync(string periodo, CancellationToken cancellationToken = default)
        => Task.FromResult(new TicketEstado { Ticket = $"RVIE-CIERRE-{periodo}", Estado = "COMPLETADO", Mensaje = "Periodo RVIE cerrado" });

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
