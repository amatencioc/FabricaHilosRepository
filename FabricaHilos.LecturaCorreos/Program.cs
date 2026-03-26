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

// ─── Windows Service ─────────────────────────────────────────
builder.Services.AddWindowsService(options =>
    options.ServiceName = "FabricaHilos LecturaCorreos CDR");

// ─── Configuración tipada ─────────────────────────────────────
builder.Services.Configure<LecturaCorreosOptions>(
    builder.Configuration.GetSection(LecturaCorreosOptions.SeccionConfig));

// ─── HttpClient ───────────────────────────────────────────────
builder.Services.AddHttpClient("OAuth2Token");
builder.Services.AddHttpClient<ISunatService, SunatService>(client =>
    client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddHttpClient<IPortalDescargaService, BizlinksPortalService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
});

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
// Portal de descarga (Bizlinks / JSF): crea su propio HttpClient con cookies por sesión.
builder.Services.AddTransient<IPortalDescargaService, BizlinksPortalService>();
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

