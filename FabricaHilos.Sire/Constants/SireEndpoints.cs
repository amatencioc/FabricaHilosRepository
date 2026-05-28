namespace FabricaHilos.Sire.Constants;

public static class SireEndpoints
{
    public const string ProdAuthBaseUrl = "https://api-seguridad.sunat.gob.pe/v1/clientessol";
    public const string ProdApiBaseUrl = "https://api.sunat.gob.pe/v1/contribuyente/gem";
    public const string MockApiBaseUrl = "https://mock.local.sunat/sire";

    public static string RviePeriodos => "/sire/rvie/periodos";
    public static string RviePropuesta(string periodo) => $"/sire/rvie/propuesta/{periodo}";
    public static string RvieReemplazo(string periodo) => $"/sire/rvie/propuesta/{periodo}/reemplazo";
    public static string RvieAceptar(string periodo) => $"/sire/rvie/propuesta/{periodo}/aceptar";
    public static string RvieCierre(string periodo) => $"/sire/rvie/cierre/{periodo}";
    public static string RvieConstancia(string periodo) => $"/sire/rvie/cierre/{periodo}/constancia";
    public static string RvieTicket(string periodo, string ticket) => $"/sire/rvie/propuesta/{periodo}/ticket/{ticket}";

    public static string RcePeriodos => "/sire/rce/periodos";
    public static string RcePropuesta(string periodo) => $"/sire/rce/propuesta/{periodo}";
    public static string RceReemplazo(string periodo) => $"/sire/rce/propuesta/{periodo}/reemplazo";
    public static string RceAceptar(string periodo) => $"/sire/rce/propuesta/{periodo}/aceptar";
    public static string RceCierre(string periodo) => $"/sire/rce/cierre/{periodo}";
    public static string RceConstancia(string periodo) => $"/sire/rce/cierre/{periodo}/constancia";
    public static string RceTicket(string periodo, string ticket) => $"/sire/rce/propuesta/{periodo}/ticket/{ticket}";
}
