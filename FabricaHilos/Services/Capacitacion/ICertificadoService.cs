using FabricaHilos.Models.Capacitacion;

namespace FabricaHilos.Services.Capacitacion;

public interface ICertificadoService
{
    Task<CapCertificado?>  GetAsync(int idCertificado, string codUsuario);
    Task<CapCertificado?>  GetByCodigoAsync(string codigoVerif);
    Task<CapCertificado?>  EmitirAsync(long idIntento, long idInscripcion, string codUsuario);
    Task<byte[]?>          GenerarPdfAsync(int idCertificado);
}
