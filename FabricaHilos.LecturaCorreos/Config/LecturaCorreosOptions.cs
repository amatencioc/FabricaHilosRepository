namespace FabricaHilos.LecturaCorreos.Config;

public class LecturaCorreosOptions
{
    public const string SeccionConfig = "LecturaCorreos";

    public int  IntervaloMinutos   { get; set; } = 10;
    public int  MaxCorreosPorCiclo { get; set; } = 50;

    /// <summary>
    /// Ruta base donde se guardarán los documentos descargados organizados por RUC/año/mes/día.
    /// Se configura en appsettings.json → LecturaCorreos:RutaArchivos.
    /// Si está vacío, no se guarda nada en disco.
    /// </summary>
    public string RutaArchivos { get; set; } = string.Empty;

    /// <summary>RUC de la empresa receptora. Se usa como primer nivel de carpeta.</summary>
    public string RucEmpresa   { get; set; } = "20100096260";

    /// <summary>
    /// SOLO PRUEBAS. Si es true, elimina todos los registros de las tablas del proceso
    /// antes de iniciar el primer ciclo de lectura de correos.
    /// Nunca activar en producción.
    /// </summary>
    public bool LimpiarBdAlIniciar { get; set; } = false;

    public List<CuentaCorreoOptions> Cuentas { get; set; } = [];
}

public class CuentaCorreoOptions
{
    public string Nombre            { get; set; } = string.Empty;
    public bool   Activa            { get; set; } = true;
    public string Proveedor         { get; set; } = "Office365"; // "Office365" | "Gmail" | "Office365OAuth2"
    public string ImapHost          { get; set; } = string.Empty;
    public int    ImapPort          { get; set; } = 993;
    public bool   UsarSsl           { get; set; } = true;
    public string Usuario           { get; set; } = string.Empty;
    public string Contrasena        { get; set; } = string.Empty;
    public string Carpeta           { get; set; } = "INBOX";
    public bool   MarcarLeido       { get; set; } = true;
    public bool   MoverProcesado    { get; set; } = false;
    public string CarpetaProcesados { get; set; } = "Procesados";

    // ── OAuth2 (Office365OAuth2) ───────────────────────────────────────────────
    public string TenantId     { get; set; } = string.Empty;
    public string ClientId     { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string AuthUrl      { get; set; } = string.Empty;
    public string Scope        { get; set; } = string.Empty;
}
