using FabricaHilos.LecturaCorreos.Config;

namespace FabricaHilos.LecturaCorreos.Services.Sunat;

public interface ISunatService
{
    Task<RespuestaCdrSunat> ConsultarCdrAsync(
        string        ruc,
        string        tipoComprobante,
        string        serie,
        int           correlativo,
        EmpresaOptions empresa);
}
