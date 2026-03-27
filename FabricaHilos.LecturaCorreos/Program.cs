using FabricaHilos.LecturaCorreos.Config;
using FabricaHilos.LecturaCorreos.Data;
using FabricaHilos.LecturaCorreos.Services;
using FabricaHilos.LecturaCorreos.Services.Archivos;
using FabricaHilos.LecturaCorreos.Services.Email;
using FabricaHilos.LecturaCorreos.Services.Email.Conexion;
using FabricaHilos.LecturaCorreos.Services.Email.Lectores;
using FabricaHilos.LecturaCorreos.Services.Email.Portales;
using FabricaHilos.LecturaCorreos.Services.Parsers;
using FabricaHilos.LecturaCorreos.Services.Signals;
using FabricaHilos.LecturaCorreos.Services.Sunat;
using FabricaHilos.LecturaCorreos.Workers;
using FabricaHilos.Notificaciones.Extensions;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// ─── Configuración de Serilog ────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: "Logs/lecturaCorreos-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger);

// ─── Excepciones no controladas a nivel de proceso ────────────
AppDomain.CurrentDomain.UnhandledException += (_, args) =>
{
    Log.Fatal(args.ExceptionObject as Exception,
        "Excepción no controlada en el dominio de la aplicación. IsTerminating={IsTerminating}",
        args.IsTerminating);
    Log.CloseAndFlush();
};

TaskScheduler.UnobservedTaskException += (_, args) =>
{
    Log.Error(args.Exception,
        "Excepción no observada en tarea. Se marca como observada para evitar cierre del proceso.");
    args.SetObserved();
};

// ─── Windows Service ─────────────────────────────────────────
builder.Services.AddWindowsService(options =>
    options.ServiceName = "FabricaHilos LecturaCorreos CDR");

// Dar tiempo suficiente para que los workers cierren conexiones IMAP y completen escrituras en BD.
// El valor por defecto (5s) es demasiado corto para operaciones de red en curso.
builder.Services.Configure<HostOptions>(opts =>
    opts.ShutdownTimeout = TimeSpan.FromSeconds(30));

// ─── Configuración tipada ─────────────────────────────────────
builder.Services.Configure<LecturaCorreosOptions>(
    builder.Configuration.GetSection(LecturaCorreosOptions.SeccionConfig));

// ─── HttpClient ───────────────────────────────────────────────
builder.Services.AddHttpClient("OAuth2Token");
builder.Services.AddHttpClient<ISunatService, SunatService>(client =>
    client.Timeout = TimeSpan.FromSeconds(30));
// Portales de descarga: cada servicio crea su propio HttpClient con CookieContainer.
// El orquestador enruta segun el tipo de correo (efacturacion.pe / bizlinks.la / asaduanas.com).

// ─── Repositorios Oracle ──────────────────────────────────────
builder.Services.AddTransient<ILecturaCorreosRepository, LecturaCorreosRepository>();
builder.Services.AddTransient<ILogisticaRepository, LogisticaRepository>();

// ─── Servicios ────────────────────────────────────────────────
// Conexión IMAP / OAuth2
builder.Services.AddTransient<IImapConexionService, ImapConexionService>();
// Lectores especializados
builder.Services.AddTransient<ILectorAdjuntoXml, LectorAdjuntoXml>();
builder.Services.AddTransient<ILectorAdjuntoPdf, LectorAdjuntoPdf>();
builder.Services.AddTransient<ILectorAdjuntoZip, LectorAdjuntoZip>();
// Portales de descarga por proveedor (efacturacion.pe, bizlinks.la, asaduanas.com/softpad)
builder.Services.AddTransient<EfacturacionPortalService>();
builder.Services.AddTransient<BizlinksPortalService>();
builder.Services.AddTransient<AsaduanasPortalService>();
builder.Services.AddTransient<IPortalDescargaService, PortalDescargaOrquestador>();
// Orquestador de correo
builder.Services.AddTransient<IEmailReaderService, ImapEmailReaderService>();
builder.Services.AddTransient<IXmlParserService, UblXmlParserService>();
builder.Services.AddSingleton<ILimpiezaSignal, LimpiezaSignal>();
// Circuit breaker: Singleton para mantener el estado de fallos entre ciclos.
builder.Services.AddSingleton<ICuentaCircuitBreaker, CuentaCircuitBreaker>();
// Archivos en disco: organiza documentos por RUC/año/mes/día.
builder.Services.AddTransient<IArchivoDocumentoService, ArchivoDocumentoService>();
// PDF Limbo: notificación por correo de adjuntos huérfanos.
builder.Services.AddTransient<IPdfLimboRepository, PdfLimboRepository>();

// ─── Notificaciones ───────────────────────────────────────────
builder.Services.AddNotificaciones(builder.Configuration);

// ─── Workers (Hosted Services) ────────────────────────────────
builder.Services.AddHostedService<LecturaCorreosSunatCdrWorker>();
builder.Services.AddHostedService<SunatCdrWorker>();
builder.Services.AddHostedService<NotificacionPdfLimboWorker>();

try
{
    Log.Information("Iniciando FabricaHilos.LecturaCorreos...");
    var host = builder.Build();
    ValidarConfiguracionAlIniciar(host.Services);
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "El servicio terminó de forma inesperada.");
}
finally
{
    Log.CloseAndFlush();
}

static void ValidarConfiguracionAlIniciar(IServiceProvider services)
{
    var config   = services.GetRequiredService<IConfiguration>();
    var opciones = new LecturaCorreosOptions();
    config.GetSection(LecturaCorreosOptions.SeccionConfig).Bind(opciones);

    if (opciones.IntervaloMinutos <= 0)
        Log.Error("\u26a0\ufe0f Config inv\u00e1lida \u2014 LecturaCorreos:IntervaloMinutos={V} debe ser > 0.", opciones.IntervaloMinutos);
    if (opciones.MaxCorreosPorCiclo <= 0)
        Log.Error("\u26a0\ufe0f Config inv\u00e1lida \u2014 LecturaCorreos:MaxCorreosPorCiclo={V} debe ser > 0.", opciones.MaxCorreosPorCiclo);
    if (opciones.IntervaloConsultaMinutos <= 0)
        Log.Error("\u26a0\ufe0f Config inv\u00e1lida \u2014 LecturaCorreos:IntervaloConsultaMinutos={V} debe ser > 0.", opciones.IntervaloConsultaMinutos);
    if (string.IsNullOrWhiteSpace(config.GetConnectionString("OracleConnection")))
        Log.Error("\u26a0\ufe0f Config inv\u00e1lida \u2014 Cadena de conexi\u00f3n 'OracleConnection' no configurada. El servicio no puede conectarse a BD.");
    if (string.IsNullOrWhiteSpace(opciones.RutaArchivos))
        Log.Warning("\u26a0\ufe0f LecturaCorreos:RutaArchivos no configurada \u2014 los documentos NO se guardar\u00e1n en disco.");
    if (opciones.WorkerCorreosActivo && !opciones.TodasLasCuentas.Any())
        Log.Warning("\u26a0\ufe0f WorkerCorreosActivo=true pero no hay cuentas activas configuradas \u2014 el worker de correos no procesar\u00e1 nada.");
}

