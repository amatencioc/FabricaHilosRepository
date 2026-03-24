namespace FabricaHilos.LecturaCorreos.Config;

public class LecturaCorreosOptions
{
    public const string SeccionConfig = "LecturaCorreos";

    public int  IntervaloMinutos   { get; set; } = 10;
    public int  MaxCorreosPorCiclo { get; set; } = 50;

    /// <summary>
    /// Intervalo en minutos entre ciclos de consulta de CDR a SUNAT (SunatCdrWorker).
    /// </summary>
    public int IntervaloConsultaMinutos { get; set; } = 15;

    /// <summary>
    /// Ruta base donde se guardarán los documentos descargados organizados por RUC/año/mes/día.
    /// Se configura en appsettings.json → LecturaCorreos:RutaArchivos.
    /// Si está vacío, no se guarda nada en disco.
    /// </summary>
    public string RutaArchivos { get; set; } = string.Empty;

    /// <summary>
    /// [Legado] RUC único de empresa. Se aplica a las cuentas de <see cref="Cuentas"/> planas.
    /// Para multi-empresa usar <see cref="Empresas"/> con el RUC dentro de cada entrada.
    /// </summary>
    public string RucEmpresa { get; set; } = string.Empty;

    /// <summary>
    /// Habilita o deshabilita el worker de lectura de correos (LecturaCorreosSunatCdrWorker).
    /// Cuando es false el servicio arranca pero el worker no procesa ningún ciclo.
    /// </summary>
    public bool WorkerCorreosActivo { get; set; } = true;

    /// <summary>
    /// Habilita o deshabilita el worker de consulta de CDR a SUNAT (SunatCdrWorker).
    /// Cuando es false el servicio arranca pero el worker no procesa ningún ciclo.
    /// </summary>
    public bool WorkerSunatActivo { get; set; } = true;

    /// <summary>
    /// Habilita o deshabilita el worker de notificación de PDFs en limbo (NotificacionPdfLimboWorker).
    /// Cuando es false el servicio arranca pero el worker no procesa ningún ciclo.
    /// </summary>
    public bool WorkerNotificacionPdfActivo { get; set; } = true;

    /// <summary>
    /// Intervalo en minutos entre ciclos del NotificacionPdfLimboWorker.
    /// </summary>
    public int IntervaloNotificacionPdfMinutos { get; set; } = 5;

    /// <summary>
    /// SOLO PRUEBAS. Si es true, elimina todos los registros de las tablas del proceso
    /// antes de iniciar el primer ciclo de lectura de correos.
    /// Nunca activar en producción.
    /// </summary>
    public bool LimpiarBdAlIniciar { get; set; } = false;

    /// <summary>
    /// [Legado] Cuentas directamente bajo LecturaCorreos (compatibilidad con configuración anterior).
    /// Heredan el RUC de <see cref="RucEmpresa"/>. Para multi-empresa usar <see cref="Empresas"/>.
    /// </summary>
    public List<CuentaCorreoOptions> Cuentas { get; set; } = [];

    /// <summary>
    /// Lista de empresas. Cada empresa agrupa sus propias cuentas de correo y credenciales SUNAT.
    /// Permite procesar múltiples RUCs en paralelo en el mismo servicio.
    /// </summary>
    public List<EmpresaOptions> Empresas { get; set; } = [];

    /// <summary>
    /// Todas las cuentas a procesar: combina las de <see cref="Empresas"/> (con su RUC propio)
    /// más las de <see cref="Cuentas"/> legadas (con <see cref="RucEmpresa"/> global).
    /// Es la lista que usa el worker en cada ciclo.
    /// </summary>
    public IEnumerable<CuentaCorreoOptions> TodasLasCuentas
    {
        get
        {
            foreach (var empresa in Empresas.Where(e => e.Activa))
                foreach (var cuenta in empresa.Cuentas
                    .Where(c => c.Activa && !string.IsNullOrWhiteSpace(c.ImapHost)))
                {
                    cuenta.RucEmpresa    = empresa.Ruc;
                    cuenta.NombreEmpresa = empresa.Nombre;
                    yield return cuenta;
                }

            foreach (var cuenta in Cuentas
                .Where(c => c.Activa && !string.IsNullOrWhiteSpace(c.ImapHost)))
            {
                if (string.IsNullOrEmpty(cuenta.RucEmpresa))
                    cuenta.RucEmpresa = RucEmpresa;
                yield return cuenta;
            }
        }
    }
}

/// <summary>
/// Agrupa la identidad fiscal de una empresa con sus cuentas de correo
/// y sus credenciales SUNAT para consulta de CDR.
/// </summary>
public class EmpresaOptions
{
    /// <summary>RUC de 11 dígitos de la empresa receptora.</summary>
    public string Ruc    { get; set; } = string.Empty;

    /// <summary>Nombre descriptivo (ej. "EmpresaPrincipal", "Subsidiaria1").</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// Habilita o deshabilita el procesamiento de esta empresa.
    /// Cuando es false, ninguna de sus cuentas se procesa en el ciclo.
    /// </summary>
    public bool Activa { get; set; } = true;

    /// <summary>Cuentas de correo IMAP que pertenecen a esta empresa.</summary>
    public List<CuentaCorreoOptions> Cuentas { get; set; } = [];

    /// <summary>
    /// Credenciales SOL de SUNAT para verificación de CDR de esta empresa.
    /// Si es null, se usarán las credenciales globales de la sección Sunat en appsettings.
    /// </summary>
    public SunatEmpresaOptions? Sunat { get; set; }
}

/// <summary>
/// Credenciales SOL de SUNAT específicas por empresa.
/// </summary>
public class SunatEmpresaOptions
{
    public string UsuarioSol          { get; set; } = string.Empty;
    public string ClaveSol            { get; set; } = string.Empty;
    public string EndpointConsultaCdr { get; set; } = string.Empty;
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

    /// <summary>
    /// RUC de la empresa dueña de esta cuenta. Se propaga automáticamente desde
    /// <see cref="EmpresaOptions.Ruc"/> o desde <see cref="LecturaCorreosOptions.RucEmpresa"/>.
    /// No se configura manualmente en appsettings.json.
    /// </summary>
    public string RucEmpresa { get; set; } = string.Empty;

    /// <summary>
    /// Nombre descriptivo de la empresa dueña de esta cuenta. Propagado automáticamente
    /// desde <see cref="EmpresaOptions.Nombre"/>. No se configura manualmente en appsettings.json.
    /// </summary>
    public string NombreEmpresa { get; set; } = string.Empty;
}
