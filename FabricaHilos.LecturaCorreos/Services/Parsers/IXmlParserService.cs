namespace FabricaHilos.LecturaCorreos.Services.Parsers;

using FabricaHilos.LecturaCorreos.Models;

public interface IXmlParserService
{
    /// <summary>
    /// Detecta el tipo de documento XML UBL y parsea todos sus campos.
    /// Siempre retorna un <see cref="ResultadoParseo"/> que indica el estado exacto:
    /// éxito, CDR omitido intencionalmente, XML inválido o tipo no reconocido.
    /// </summary>
    ResultadoParseo Parsear(string xmlContenido, string nombreArchivo, string cuentaCorreo,
                            string asunto, string remitente, DateTime fechaCorreo);
}
