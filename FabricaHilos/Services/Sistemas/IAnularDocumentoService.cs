namespace FabricaHilos.Services.Sistemas;

public interface IAnularDocumentoService
{
    /// <summary>Busca el documento en DOCUVENT y recupera datos de MOVGLOS → NRODOC → NROLIBR.</summary>
    Task<AnularDocumentoResultDto> BuscarDocumentoAsync(string tipoDoc, string serie, string numero);

    /// <summary>Paso 1: DELETE en DOCUVENT. Retorna inmediatamente.</summary>
    Task<RestablecerPasoDto> Paso1DeleteDocumentAsync(string tipoDoc, string serie, string numero);

    /// <summary>Paso 2: Espera hasta que MOVGLOS.ESTADO = 9, luego hace DELETE en MOVGLOS.</summary>
    Task<RestablecerPasoDto> Paso2EsperarYDeleteMovGlosAsync(string tipoDoc, string serie, string numero, int timeoutSegundos = 60);

    /// <summary>Paso 3: UPDATE NRODOC SET NUMERO = numeroBusqueda.</summary>
    Task<RestablecerPasoDto> Paso3UpdateNroDocAsync(string tipoDoc, string serie, string numeroBusqueda);

    /// <summary>Paso 4: UPDATE NROLIBR SET NUMERO = voucherBusqueda.</summary>
    Task<RestablecerPasoDto> Paso4UpdateNroLibrAsync(string ano, string mes, string libro, string voucherBusqueda);

    /// <summary>Revierte: UPDATE NRODOC y NROLIBR a los valores anteriores (antes de la restauración).</summary>
    Task<RestablecerPasoDto> RevertirAsync(string tipoDoc, string serie, string numeroAnterior,
                                           string ano, string mes, string libro, string voucherAnterior);
}
