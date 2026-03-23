using FabricaHilos.Data;
using FabricaHilos.Models.Facturacion;
using FabricaHilos.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FabricaHilos.Controllers;

[Authorize]
public class FacturacionController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly DocumentExtractorClient _extractorClient;

    public FacturacionController(ApplicationDbContext context, DocumentExtractorClient extractorClient)
    {
        _context = context;
        _extractorClient = extractorClient;
    }

    // GET: /Facturacion/Index
    public IActionResult Index() => View();

    // GET: /Facturacion/ImportarFacturas
    [HttpGet]
    public IActionResult ImportarFacturas() => View();

    // POST: /Facturacion/ImportarFacturas
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportarFacturas(IFormFile archivoPdf)
    {
        if (archivoPdf == null || archivoPdf.Length == 0)
        {
            TempData["Error"] = "Por favor seleccione un archivo PDF, PNG o JPEG.";
            return View();
        }

        try
        {
            var extraido = await _extractorClient.ExtraerAsync(archivoPdf);
            if (extraido == null)
            {
                TempData["Error"] = "No se pudo extraer información del documento.";
                return View();
            }

            if (extraido.MensajeError != null)
                TempData["Warning"] = $"Extracción con advertencias: {extraido.MensajeError}";

            return View("VistaPrevia", extraido);
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error al procesar el archivo: {ex.Message}";
            return View();
        }
    }

    // POST: /Facturacion/ConfirmarImportacion
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,Gerencia,Supervisor")]
    public async Task<IActionResult> ConfirmarImportacion(DocumentoExtraido modelo)
    {
        try
        {
            var documento = new FhLcDocumento
            {
                NombreArchivo = modelo.NombreArchivo,
                TipoDocumento = modelo.TipoDocumento,
                Serie = modelo.Serie,
                Correlativo = modelo.Correlativo,
                NumeroDocumento = modelo.NumeroDocumento,
                FechaEmision = modelo.FechaEmision,
                HoraEmision = modelo.HoraEmision,
                FechaVencimiento = modelo.FechaVencimiento,
                RucEmisor = modelo.RucEmisor,
                RazonSocialEmisor = modelo.RazonSocialEmisor,
                NombreComercialEmisor = modelo.NombreComercialEmisor,
                DireccionEmisor = modelo.DireccionEmisor,
                RucReceptor = modelo.RucReceptor,
                RazonSocialReceptor = modelo.RazonSocialReceptor,
                DireccionReceptor = modelo.DireccionReceptor,
                Moneda = modelo.Moneda,
                BaseImponible = modelo.BaseImponible,
                TotalIgv = modelo.TotalIgv,
                TotalExonerado = modelo.TotalExonerado,
                TotalInafecto = modelo.TotalInafecto,
                TotalGratuito = modelo.TotalGratuito,
                TotalDescuento = modelo.TotalDescuento,
                TotalCargo = modelo.TotalCargo,
                TotalAnticipos = modelo.TotalAnticipos,
                TotalPagar = modelo.TotalPagar,
                FormaPago = modelo.FormaPago,
                MontoNetoPendiente = modelo.MontoNetoPendiente,
                TieneDetraccion = modelo.TieneDetraccion,
                PctDetraccion = modelo.PctDetraccion,
                MontoDetraccion = modelo.MontoDetraccion,
                NumeroPedido = modelo.NumeroPedido,
                NumeroGuia = modelo.NumeroGuia,
                NumeroDocRef = modelo.NumeroDocRef,
                ModalidadTraslado = modelo.ModalidadTraslado,
                MotivoTraslado = modelo.MotivoTraslado,
                ModoTransporte = modelo.ModoTransporte,
                PesoBruto = modelo.PesoBruto,
                UnidadPeso = modelo.UnidadPeso,
                FechaInicioTraslado = modelo.FechaInicioTraslado,
                FechaFinTraslado = modelo.FechaFinTraslado,
                RucTransportista = modelo.RucTransportista,
                RazonSocTransportista = modelo.RazonSocTransportista,
                NombreConductor = modelo.NombreConductor,
                LicenciaConductor = modelo.LicenciaConductor,
                PlacaVehiculo = modelo.PlacaVehiculo,
                MarcaVehiculo = modelo.MarcaVehiculo,
                NroDocConductor = modelo.NroDocConductor,
                UbigeoOrigen = modelo.UbigeoOrigen,
                DirOrigen = modelo.DirOrigen,
                UbigeoDestino = modelo.UbigeoDestino,
                DirDestino = modelo.DirDestino,
                Vendedor = modelo.Vendedor,
                Estado = modelo.Estado,
                FechaProcesamiento = DateTime.Now,
                Observaciones = modelo.Observaciones,
                FuenteExtraccion = modelo.FuenteExtraccion,
                Confianza = modelo.Confianza,
                MensajeError = modelo.MensajeError
            };

            _context.Set<FhLcDocumento>().Add(documento);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Documento {documento.NumeroDocumento ?? documento.NombreArchivo} registrado correctamente.";
            return RedirectToAction(nameof(ListaDocumentos));
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error al registrar el documento: {ex.Message}";
            return View("VistaPrevia", modelo);
        }
    }

    // GET: /Facturacion/ListaDocumentos
    public async Task<IActionResult> ListaDocumentos()
    {
        var documentos = await _context.Set<FhLcDocumento>()
            .OrderByDescending(d => d.FechaProcesamiento)
            .ToListAsync();
        return View(documentos);
    }
}
