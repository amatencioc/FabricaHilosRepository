using FabricaHilos.OrgatexSync.Config;
using FabricaHilos.OrgatexSync.Data;
using FabricaHilos.OrgatexSync.Logging;
using FabricaHilos.OrgatexSync.Workers;
using Serilog;
using Serilog.Filters;

var builder = Host.CreateApplicationBuilder(args);

// ─── Configuración de Serilog ────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: "Logs/orgatexSync-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    // Log dedicado: una línea por cada llamada a PKG_ORGATEX.SP_MERGE_FILA (OK o error),
    // separado del log general del servicio. Ver OrgatexCallLogger.NombreCategoria.
    .WriteTo.Logger(lc => lc
        .Filter.ByIncludingOnly(Matching.FromSource(OrgatexCallLogger.NombreCategoria))
        .WriteTo.File(
            path: "Logs/OrgatexCalls/orgatex-calls-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 90,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"))
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
    options.ServiceName = "FabricaHilos OrgatexSync");

// Dar tiempo suficiente para que el worker cierre conexiones y termine el MERGE en curso.
builder.Services.Configure<HostOptions>(opts =>
    opts.ShutdownTimeout = TimeSpan.FromSeconds(30));

// ─── Configuración tipada ─────────────────────────────────────
builder.Services.Configure<OrgatexOptions>(
    builder.Configuration.GetSection(OrgatexOptions.SeccionConfig));

// ─── Repositorio ───────────────────────────────────────────────
builder.Services.AddTransient<IOrgatexRepository, OrgatexRepository>();

// ─── Worker (Hosted Service) ───────────────────────────────────
builder.Services.AddHostedService<OrgatexSyncWorker>();

var host = builder.Build();
await host.RunAsync();
