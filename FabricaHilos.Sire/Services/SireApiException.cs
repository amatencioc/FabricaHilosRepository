using System.Net;

namespace FabricaHilos.Sire.Services;

public sealed class SireApiException : Exception
{
    public HttpStatusCode? StatusCode { get; }

    public SireApiException(string message, HttpStatusCode? statusCode = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}
