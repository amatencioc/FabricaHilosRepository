using FabricaHilos.Alertas.Config;
using FabricaHilos.Alertas.Data;
using FabricaHilos.Alertas.Workers;
using FabricaHilos.Notificaciones.Extensions;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Rutas ANCLADAS a la carpeta del ejecutable (AppContext.BaseDirectory), no relativas:
// como Windows Service el directorio de trabajo del proceso NO es la carpeta de
// publicación (suele ser System32) -- con rutas relativas los logs terminaban
// escribiéndose (o fallando por permisos) fuera de la carpeta esperada.
var carpetaLogs = Path.Combine(AppContext.BaseDirectory, "Logs");

// ─── Configuración de Serilog ────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: Path.Combine(carpetaLogs, "alertas-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger);

// ─── Excepciones no controladas a nivel de proceso ───────────────────────
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

// ─── Windows Service ──────────────────────────────────────────────────────
builder.Services.AddWindowsService(options =>
    options.ServiceName = "FabricaHilos Alertas");

// Dar tiempo suficiente para que el worker cierre conexiones y termine el envío en curso.
builder.Services.Configure<HostOptions>(opts =>
    opts.ShutdownTimeout = TimeSpan.FromSeconds(30));

// ─── Configuración tipada ─────────────────────────────────────────────────
builder.Services.Configure<AlertaTurnoDescansoOptions>(
    builder.Configuration.GetSection(AlertaTurnoDescansoOptions.SeccionConfig));

// ─── Notificaciones (correo) ──────────────────────────────────────────────
builder.Services.AddNotificaciones(builder.Configuration);

// ─── Repositorios ──────────────────────────────────────────────────────────
builder.Services.AddTransient<IAlertaTurnoDescansoRepository, AlertaTurnoDescansoRepository>();

// ─── Workers (Hosted Services) ─────────────────────────────────────────────
builder.Services.AddHostedService<AlertaTurnoDescansoWorker>();

var host = builder.Build();
await host.RunAsync();
