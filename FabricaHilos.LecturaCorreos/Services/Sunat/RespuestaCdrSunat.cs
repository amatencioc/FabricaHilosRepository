namespace FabricaHilos.LecturaCorreos.Services.Sunat;

public class RespuestaCdrSunat
{
    public bool    Exitoso          { get; set; }
    public string  CodigoRespuesta  { get; set; } = string.Empty;
    public string  MensajeRespuesta { get; set; } = string.Empty;
    public bool    EstaAceptado     => CodigoRespuesta == "0";
    public bool    EstaRechazado    => !string.IsNullOrEmpty(CodigoRespuesta)
                                    && (CodigoRespuesta.StartsWith("2", StringComparison.Ordinal)
                                    ||  CodigoRespuesta.StartsWith("4", StringComparison.Ordinal));
    public byte[]? CdrZip           { get; set; }
    public string? ErrorDetalle     { get; set; }
}
