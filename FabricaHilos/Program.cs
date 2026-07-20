using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using FabricaHilos.Data;
using FabricaHilos.Models;
using FabricaHilos.Models.Ventas;
using FabricaHilos.Services;
using FabricaHilos.Services.Produccion;
using FabricaHilos.Services.RecursosHumanos;
using FabricaHilos.Services.Sgc;
using FabricaHilos.Services.Sgc.AnalisisReclamo;
using FabricaHilos.Services.Logistica;
using FabricaHilos.Services.Ventas;
using FabricaHilos.Services.Seguridad.Inspeccion;
using FabricaHilos.Config;
using FabricaHilos.Notificaciones.Extensions;
using Serilog;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using QuestPDF.Infrastructure;
using FabricaHilos.Services.CreditosCobranza;
using FabricaHilos.Services.Facturacion;
using FabricaHilos.Services.Sistemas;
using FabricaHilos.Services.Produccion.Planeamiento;
using FabricaHilos.Services.Contabilidad;
using FabricaHilos.Services.Sire;
using FabricaHilos.Sire.Interfaces;
using FabricaHilos.Sire.Options;
using FabricaHilos.Sire.Services;
using FabricaHilos.Sire.Services.Mock;
using FabricaHilos.Sire.Helpers;

// ══════════════════════════════════════════════════════════════════════════════
// FORZAR TLS 1.2+ PARA SUNAT SIRE: Requerido por APIs de seguridad SUNAT
// ══════════════════════════════════════════════════════════════════════════════
// NOTA: TLS 1.2+ para SUNAT SIRE ya no requiere configuración manual.
// ServicePointManager está obsoleto (SYSLIB0014) y no afecta a HttpClient.

// Dapper: mapear columnas Oracle con guión bajo a propiedades PascalCase
// Ej: ID_RUBRO → IdRubro, PTS_MAX → PtsMax, COD_ITEM → CodItem
Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

var builder = WebApplication.CreateBuilder(args);

// Aumentar límite de Kestrel para subida de archivos (máx. 500 MB por request)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 524_288_000; // 500 MB
});

// ══════════════════════════════════════════════════════════════════════════════
// SERILOG: Configurar logging estructurado con persistencia en archivos
// ══════════════════════════════════════════════════════════════════════════════
// Crear carpeta de logs relativa al directorio de despliegue (funciona en cualquier unidad/ruta)
var logPath = Path.Combine(AppContext.BaseDirectory, "Logs", "log-.txt");
var logDirectory = Path.GetDirectoryName(logPath);
if (!string.IsNullOrEmpty(logDirectory) && !Directory.Exists(logDirectory))
{
    Directory.CreateDirectory(logDirectory);
}

// Eliminar providers por defecto para evitar duplicación con Serilog Console sink
builder.Logging.ClearProviders();

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.File(
        path: logPath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}"
    ));


// Configurar EF Core con SQLite
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configurar ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    // Protección contra fuerza bruta en login local (Identity)
    options.Lockout.AllowedForNewUsers   = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(10);
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Tiempo de inactividad unificado para web y sesión Oracle (1 turno = 8 horas)
const int sessionHours = 8;

// Configurar cookies de autenticación web (Identity)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath           = "/Account/Login";
    options.LogoutPath          = "/Account/Logout";
    options.AccessDeniedPath    = "/Account/AccesoDenegado";
    options.ExpireTimeSpan      = TimeSpan.FromHours(sessionHours); // expira tras inactividad
    options.SlidingExpiration   = true;  // se renueva en cada request activo
    options.Cookie.HttpOnly     = true;
    options.Cookie.IsEssential  = true;
    options.Cookie.SameSite     = SameSiteMode.Lax;                      // compatibilidad móvil
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;      // permite HTTP y HTTPS
});

// Habilitar acceso al HttpContext desde servicios y sesión por usuario
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout         = TimeSpan.FromHours(sessionHours); // sincronizado con Identity
    options.Cookie.HttpOnly     = true;
    options.Cookie.IsEssential  = true;
    options.Cookie.Name         = ".FabricaHilos.Session";
    options.Cookie.SameSite     = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // permite HTTP y HTTPS
});

// Persistir claves de Data Protection en disco, fuera del folder de publicación,
// para que las cookies de autenticación sobrevivan reinicios de IIS y nuevas publicaciones.
var keysRelativePath = builder.Configuration["DataProtection:KeysPath"] ?? "DataProtectionKeys";
var keysFolder = Path.IsPathRooted(keysRelativePath)
    ? new DirectoryInfo(keysRelativePath)
    : new DirectoryInfo(Path.GetFullPath(
          Path.Combine(builder.Environment.ContentRootPath, keysRelativePath)));
keysFolder.Create();
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(keysFolder)
    .SetApplicationName("FabricaHilos");

// Registrar tema de empresa (singleton: no cambia en tiempo de ejecución)
builder.Services.Configure<FabricaHilos.Config.EmpresaTemaOptions>(
    builder.Configuration.GetSection(FabricaHilos.Config.EmpresaTemaOptions.SectionName));
builder.Services.AddScoped<IEmpresaTemaService, EmpresaTemaService>();

// Registrar opciones de acceso por red (soporta hot-reload vía IOptionsMonitor)
builder.Services.Configure<FabricaHilos.Config.RedInternaOptions>(
    builder.Configuration.GetSection(FabricaHilos.Config.RedInternaOptions.SectionName));

// Registrar servicios de negocio
builder.Services.AddScoped<IRecetaService, RecetaService>();
builder.Services.AddScoped<IParoService, ParoService>();
builder.Services.AddScoped<ISgcService, SgcService>();
builder.Services.AddScoped<ICargaTcService, CargaTcService>();
builder.Services.AddScoped<IAnalisisReclamoService, AnalisisReclamoService>();
builder.Services.AddScoped<IIndicadoresComercialesService, IndicadoresComercialesService>();
builder.Services.AddScoped<IIndicadorComercialMaestroService, IndicadorComercialMaestroService>();
builder.Services.AddScoped<IVentasPorMercadoService, VentasPorMercadoService>();
builder.Services.AddScoped<FabricaHilos.Services.Ventas.Cotizacion.ICotizacionService,
                           FabricaHilos.Services.Ventas.Cotizacion.CotizacionService>();
builder.Services.AddScoped<FabricaHilos.Services.Ventas.Cotizacion.IRutaTecnicaService,
                           FabricaHilos.Services.Ventas.Cotizacion.RutaTecnicaService>();
builder.Services.AddScoped<_IDashboardComercialService, DashboardComercialService>();
builder.Services.AddScoped<IDashboardComercialMaestroService, DashboardComercialMaestroService>();
builder.Services.AddScoped<IDashboardGerencialService, DashboardGerencialService>();
builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddScoped<IRedInternaService, RedInternaService>();
builder.Services.AddScoped<IMarcacionesService, MarcacionesService>();
builder.Services.AddScoped<ICompensacionDiaDiaService, CompensacionDiaDiaService>();
builder.Services.AddScoped<ICompensacionDdcService, CompensacionDdcService>();
builder.Services.AddScoped<IAuthHorasService, AuthHorasService>();
builder.Services.AddScoped<IPlanillaMensualService, PlanillaMensualService>();
builder.Services.AddScoped<IHorasExtrasService, HorasExtrasService>();
builder.Services.AddScoped<ICostoSalarialHorasExtrasService, CostoSalarialHorasExtrasService>();
builder.Services.AddScoped<IComparativoCostoLaboralService, ComparativoCostoLaboralService>();
builder.Services.AddHostedService<CompensacionTxCleanupService>();
builder.Services.AddSingleton<DepuracionJobService>();
builder.Services.AddSingleton<IDepuracionJobService>(sp => sp.GetRequiredService<DepuracionJobService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<DepuracionJobService>());
builder.Services.AddScoped<IInspeccionService, InspeccionService>();
builder.Services.AddScoped<IRequisicionService, RequisicionService>();
builder.Services.AddScoped<IOrdenCompraService, OrdenCompraService>();
builder.Services.AddScoped<IIndLogisticaService, IndLogisticaService>();
builder.Services.AddScoped<INivelMorosidadService, NivelMorosidadService>();
builder.Services.AddScoped<INivelTiempoService, NivelTiempoService>();
builder.Services.AddScoped<IDesarrolloService, DesarrolloService>();
builder.Services.AddScoped<DesarrolloExcelService>();
builder.Services.AddScoped<IDesarrolloComplejidadService, DesarrolloComplejidadService>();
builder.Services.AddScoped<DesarrolloComplejidadExcelService>();
builder.Services.AddScoped<IIncidenciaService, IncidenciaService>();
builder.Services.AddScoped<ISeguimientoDevService, SeguimientoDevService>();
builder.Services.AddScoped<IAnularDocumentoService, AnularDocumentoService>();
builder.Services.AddSingleton<AnularDocumentoJobManager>();
builder.Services.AddSingleton<ISalidaInternaPdfService, SalidaInternaPdfService>();
builder.Services.AddSingleton<IReclamoPdfService, ReclamoPdfService>();
builder.Services.AddSingleton<INavTokenService, NavTokenService>();
builder.Services.AddScoped<AcuerdoCompHeDocxService>();

// Monitor de usuarios activos en tiempo real (Sistemas > Usuarios Activos)
builder.Services.AddSingleton<FabricaHilos.Services.Sistemas.UsuarioActivoStore>();
builder.Services.AddHostedService<FabricaHilos.Services.Sistemas.CleanupUsuariosActivosWorker>();

// Salud Ocupacional
builder.Services.AddScoped<FabricaHilos.Services.SaludOcupacional.ISoInspeccionComService,
                           FabricaHilos.Services.SaludOcupacional.SoInspeccionComService>();
builder.Services.AddScoped<FabricaHilos.Services.SaludOcupacional.ISoInspeccionPdfService,
                           FabricaHilos.Services.SaludOcupacional.SoInspeccionPdfService>();

// Planeamiento
builder.Services.AddScoped<IPlnRegistroService, PlnRegistroService>();
builder.Services.AddScoped<IPlnSeguimientoService, PlnSeguimientoService>();
builder.Services.AddScoped<IPlnAlertaService, PlnAlertaService>();
builder.Services.AddScoped<IPlnKpiService, PlnKpiService>();
builder.Services.AddScoped<IPlnParamService, PlnParamService>();
builder.Services.AddScoped<IPlnReporteService, PlnReporteService>();
builder.Services.AddScoped<IPlnPendientesService, PlnPendientesService>();

// Servicio centralizado de archivos (usado por todos los módulos)
builder.Services.AddScoped<FabricaHilos.Services.Archivos.IProcesadorArchivoService,
                           FabricaHilos.Services.Archivos.ProcesadorArchivoService>();

// Contabilidad
builder.Services.AddScoped<IActivoFijoService, ActivoFijoService>();
builder.Services.AddScoped<FabricaHilos.Services.Contabilidad.ProcesadorImagenActivoFijo>();

// Capacitación (LMS)
builder.Services.AddScoped<FabricaHilos.Services.Capacitacion.ICapacitacionService,
                           FabricaHilos.Services.Capacitacion.CapacitacionService>();
builder.Services.AddScoped<FabricaHilos.Services.Capacitacion.IExamenService,
                           FabricaHilos.Services.Capacitacion.ExamenService>();
builder.Services.AddScoped<FabricaHilos.Services.Capacitacion.ICertificadoService,
                           FabricaHilos.Services.Capacitacion.CertificadoService>();
builder.Services.AddScoped<FabricaHilos.Services.Capacitacion.ContenidoMediaService>();
// Habilitar subida de archivos grandes (video hasta 500 MB)
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 600_000_000; // 600 MB
});
builder.WebHost.ConfigureKestrel(o =>
{
    o.Limits.MaxRequestBodySize = 600_000_000;
});

// Registrar servicios de notificaciones
builder.Services.AddNotificaciones(builder.Configuration);

// Health checks requeridos por app.MapHealthChecks("/health") y "/health/sire"
builder.Services.AddHealthChecks();

// ══════════════════════════════════════════════════════════════════════════════
// SIRE LAZY INITIALIZATION: Defer SIRE services until Contabilidad module is accessed
// ══════════════════════════════════════════════════════════════════════════════
var sireOptions = builder.Configuration.GetSection("Sire").Get<SireOptions>() ?? new SireOptions();
builder.Services.Configure<SireOptions>(builder.Configuration.GetSection("Sire"));
builder.Services.Configure<FabricaHilos.Services.Sire.SireReporteComprasOptions>(
    builder.Configuration.GetSection("SireReporteCompras"));

if (sireOptions.UseMock)
{
    builder.Services.AddSingleton<ISireAuthService, SireAuthServiceMock>();
    builder.Services.AddSingleton<ISireVentasService, SireVentasServiceMock>();
    builder.Services.AddSingleton<ISireComprasService, SireComprasServiceMock>();
    builder.Services.AddSingleton<ITusUploadService, TusUploadServiceMock>();
}
else
{
    builder.Services.AddHttpClient<ISireAuthService, SireAuthService>()
        .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromMinutes(5));
    builder.Services.AddHttpClient<ISireVentasService, SireVentasService>()
        .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromMinutes(5));
    builder.Services.AddHttpClient<ISireComprasService, SireComprasService>()
        .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromMinutes(5));
    builder.Services.AddHttpClient<ITusUploadService, TusUploadService>()
        .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromMinutes(5));
}
builder.Services.AddScoped<TicketPollingHelper>();
builder.Services.AddSingleton<ILazySireInitializer, LazySireInitializer>();

// Repositorio Oracle para persistencia SIRE (reemplaza SQLite para jobs/health/logs)
builder.Services.AddSingleton<ISireOracleRepository, SireOracleRepository>();

// Cola y worker de exportación asíncrona SIRE
builder.Services.AddSingleton<ISireExportacionQueue, SireExportacionQueue>();
builder.Services.AddScoped<SireValidaService>();
builder.Services.AddScoped<SirePropuestaZipService>();
builder.Services.AddScoped<FabricaHilos.Services.Sire.SireReporteComprasService>();
// Validez de comprobantes (API Consulta Integrada SUNAT — clientesextranet)
builder.Services.AddSingleton<IConsultaValidezService, ConsultaValidezService>();
builder.Services.AddHttpClient("sunat-validez");
// Descarga automática del padrón SSCO desde el portal público de SUNAT
builder.Services.AddHttpClient("sunat-ssco", client =>
{
    client.Timeout = TimeSpan.FromMinutes(3);
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0 Safari/537.36");
});
builder.Services.AddHostedService<SireExportacionWorker>();
// Fase 2: Watcher de tickets SUNAT (polling cada WatcherIntervalMin minutos)
builder.Services.AddHostedService<SireTicketWatcherWorker>();

// NOTE: SireMonitoringService eliminado — health checks de SUNAT removidos.

// Licencia QuestPDF (Community: proyectos con ingresos < $1M USD)
QuestPDF.Settings.License = LicenseType.Community;

// Registrar cliente HTTP para la API de extracción de documentos
builder.Services.AddHttpClient<DocumentExtractorClient>(client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["DocumentExtractor:BaseUrl"] ?? "https://localhost:7200/");
    client.Timeout = TimeSpan.FromSeconds(60);
});

// Agregar MVC con vistas y registrar ubicación de vistas anidadas bajo Produccion
builder.Services.AddControllersWithViews()
    .AddRazorRuntimeCompilation()   // ← recarga .cshtml sin reiniciar en Development
    .AddRazorOptions(options =>
    {
        // Permite que Views/Produccion/{Controller}/{Action}.cshtml sea encontrado automáticamente
        options.ViewLocationFormats.Add("/Views/Produccion/{1}/{0}.cshtml");
        // Permite que Views/Sgc/{Controller}/{Action}.cshtml sea encontrado automáticamente
        options.ViewLocationFormats.Add("/Views/Sgc/{1}/{0}.cshtml");
        // Permite que Views/Ventas/{Controller}/{Action}.cshtml sea encontrado automáticamente
        options.ViewLocationFormats.Add("/Views/Ventas/{1}/{0}.cshtml");
        // Permite que Views/RecursosHumanos/Aquarius/{Controller}/{Action}.cshtml sea encontrado
        options.ViewLocationFormats.Add("/Views/RecursosHumanos/Aquarius/{1}/{0}.cshtml");
        // Permite que Views/Seguridad/{Controller}/{Action}.cshtml sea encontrado automáticamente
        options.ViewLocationFormats.Add("/Views/Seguridad/{1}/{0}.cshtml");
        // Permite que Views/CreditosCobranza/{Controller}/{Action}.cshtml sea encontrado automáticamente
        options.ViewLocationFormats.Add("/Views/CreditosCobranza/{1}/{0}.cshtml");
        // Permite que Views/Sistemas/{Controller}/{Action}.cshtml sea encontrado automáticamente
        options.ViewLocationFormats.Add("/Views/Sistemas/{1}/{0}.cshtml");
    });

// Rate Limiting: protege /Account/Login contra fuerza bruta
// Máximo 10 intentos por IP en una ventana de 5 minutos
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("login", policy =>
    {
        policy.PermitLimit         = 10;
        policy.Window              = TimeSpan.FromMinutes(5);
        policy.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        policy.QueueLimit          = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// Compresión HTTP: reduce hasta 70-80% el tamaño de respuestas HTML/JSON grandes
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
    options.MimeTypes = Microsoft.AspNetCore.ResponseCompression.ResponseCompressionDefaults.MimeTypes
        .Concat(new[] { "text/html", "application/json", "text/css", "application/javascript" });
});
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(o =>
    o.Level = System.IO.Compression.CompressionLevel.Fastest);
builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(o =>
    o.Level = System.IO.Compression.CompressionLevel.Fastest);

// Visibilidad de menús del sidebar (configurable en appsettings.json)
builder.Services.Configure<MenuOptions>(
    builder.Configuration.GetSection(MenuOptions.Seccion));

// Antiforgery: habilita validación por header para los endpoints AJAX que reciben
// JSON en el body (ej. Cotización — Simular/Guardar). Los formularios tradicionales
// (form-urlencoded) siguen validando por campo oculto sin cambios.
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");

var app = builder.Build();

// ══════════════════════════════════════════════════════════════════════════════
// SERILOG: Logging de requests HTTP (opcional, para diagnóstico de rendimiento)
// ══════════════════════════════════════════════════════════════════════════════
app.UseSerilogRequestLogging(options =>
{
    // Personalizar el log de cada request HTTP
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} respondió {StatusCode} en {Elapsed:0.0000} ms";
});

// Inicializar base de datos y seed data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await InicializarBD(services);
}

// Configure pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    // En desarrollo se puede redirigir a HTTPS con el certificado de desarrollo de .NET
    app.UseHttpsRedirection();
}
else
{
    // En producción sin certificado SSL válido no redirigir a HTTPS:
    // UseHttpsRedirection() causaba que iOS Safari mostrara un diálogo de descarga
    // al no poder resolver HTTPS en una IP sin certificado válido.
    // HSTS también desactivado: el servidor corre en HTTP puro (IP sin dominio/cert).
    app.UseExceptionHandler("/Home/Error");
}

// Middleware de diagnóstico: intercepta excepciones durante el render de vistas
// ANTES de que el compresor Brotli/Gzip empiece a escribir bytes en el stream.
// Sin esto, una excepción mid-render produce ERR_CONTENT_DECODING_FAILED porque
// el stream comprimido queda truncado y el browser no puede descomprimirlo.
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var log = context.RequestServices.GetRequiredService<ILogger<Program>>();
        log.LogError(ex, "[RenderError] Excepción durante render de {Method} {Path}",
            context.Request.Method, context.Request.Path);

        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = 500;
            var msg = app.Environment.IsDevelopment()
                ? $"Error interno del servidor.\n[{ex.GetType().Name}] {ex.Message}\n{ex.StackTrace}"
                : $"Error interno del servidor. [{ex.GetType().Name}] {ex.Message}";
            await context.Response.WriteAsync(msg);
        }
    }
});

// Security headers: previene clickjacking, MIME sniffing, XSS y fuga de Referer
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"]  = "nosniff";
    context.Response.Headers["X-Frame-Options"]         = "SAMEORIGIN";
    context.Response.Headers["X-XSS-Protection"]        = "1; mode=block";
    context.Response.Headers["Referrer-Policy"]         = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"]      = "geolocation=(), microphone=()";
    await next();
});

// Interceptar HTTP 400 causado por cookies corruptas o indescifrables (DataProtection key rotation).
// Sin este middleware el navegador muestra "Esta página no funciona - HTTP ERROR 400" de forma
// permanente hasta que el usuario borre manualmente las cookies, lo cual es difícil en móvil.
// La solución: expirar todas las cookies conocidas y redirigir al login para que el usuario
// obtenga cookies frescas en el siguiente request.
app.Use(async (context, next) =>
{
    await next();
    if (context.Response.StatusCode == 400
        && !context.Response.HasStarted
        && context.Request.Cookies.Count > 0)
    {
        // Las peticiones AJAX (fetch/JSON) deben recibir 400 directo, no un redirect.
        // El redirect haría que el fetch reciba HTML del login y falle al parsear JSON.
        var contentType = context.Request.ContentType ?? string.Empty;
        var isAjax      = contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase)
                       || context.Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        if (isAjax)
        {
            // Dejar pasar el 400 original para que el JS lo maneje correctamente
            return;
        }

        var logger = context.RequestServices
            .GetRequiredService<ILogger<Program>>();
        logger.LogWarning(
            "HTTP 400 con cookies presentes en {Path}; expirando cookies y redirigiendo al login.",
            context.Request.Path);

        // Expirar todas las cookies que envió el navegador
        foreach (var cookie in context.Request.Cookies.Keys)
        {
            context.Response.Cookies.Delete(cookie, new CookieOptions
            {
                SameSite = SameSiteMode.Lax,
                Secure   = context.Request.IsHttps
            });
        }
        context.Response.StatusCode  = 302;
        context.Response.Headers["Location"] = "/Account/Login?fresh=true";
    }
});
if (!app.Environment.IsDevelopment())
{
    app.UseResponseCompression();
}
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseSession();
app.UseMiddleware<FabricaHilos.Middleware.NetworkAccessMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<FabricaHilos.Middleware.ActivityTrackingMiddleware>();

// Log de diagnóstico: grupos de rutas descubiertos dinámicamente
{
    var startupLog = app.Services.GetRequiredService<ILoggerFactory>()
        .CreateLogger("FabricaHilos.Config.RouteGroups");
    var map = FabricaHilos.Config.RouteGroups.GetExpansionMap();
    if (map.Count == 0)
    {
        startupLog.LogInformation("[RouteGroups] No se encontraron grupos de rutas expandibles.");
    }
    else
    {
        foreach (var (canonical, routes) in map)
            startupLog.LogInformation(
                "[RouteGroups] Grupo descubierto: {Canonical} → [{Routes}]",
                canonical, string.Join(", ", routes));
    }
}

// ══════════════════════════════════════════════════════════════════════════════
// HEALTH CHECKS: Endpoints de monitoreo
// ══════════════════════════════════════════════════════════════════════════════
// Endpoint general: /health (todos los health checks registrados)
app.MapHealthChecks("/health");

// Endpoint específico SIRE: /health/sire (solo para monitoreo SUNAT)
app.MapHealthChecks("/health/sire", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("sire"),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            duration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds,
                data = e.Value.Data,
                exception = e.Value.Exception?.Message
            })
        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        await context.Response.WriteAsync(result);
    }
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Landing}/{id?}");

// ══════════════════════════════════════════════════════════════════════════════
// SERILOG: Asegurar que todos los logs se escriban antes de terminar la app
// ══════════════════════════════════════════════════════════════════════════════
try
{
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "La aplicación terminó inesperadamente");
    throw;
}
finally
{
    Log.CloseAndFlush(); // Cierra Serilog y escribe todos los logs pendientes
}


// Método de inicialización de datos
static async Task InicializarBD(IServiceProvider services)
{
    var context = services.GetRequiredService<ApplicationDbContext>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        await context.Database.MigrateAsync();

        // Crear roles
        string[] roles = { "Admin", "Trabajador", "Gerencia", "Supervisor" };
        foreach (var rol in roles)
        {
            if (!await roleManager.RoleExistsAsync(rol))
            {
                await roleManager.CreateAsync(new IdentityRole(rol));
                logger.LogInformation("Rol '{Rol}' creado", rol);
            }
        }

        // Usuarios de prueba por defecto
        var usuariosSeed = new[]
        {
            (Email: "admin@fabricahilos.com",      Password: "Admin123!",       Nombre: "Administrador del Sistema", Cargo: "Admin",      Rol: "Admin"),
            (Email: "gerencia@fabricahilos.com",   Password: "Gerencia123!",    Nombre: "Gerente General",           Cargo: "Gerencia",   Rol: "Gerencia"),
            (Email: "supervisor@fabricahilos.com", Password: "Supervisor123!",  Nombre: "Supervisor de Planta",      Cargo: "Supervisor", Rol: "Supervisor"),
            (Email: "trabajador@fabricahilos.com", Password: "Trabajador123!",  Nombre: "Operario de Producción",    Cargo: "Trabajador", Rol: "Trabajador"),
        };

        foreach (var u in usuariosSeed)
        {
            if (await userManager.FindByEmailAsync(u.Email) == null)
            {
                var nuevoUsuario = new ApplicationUser
                {
                    UserName = u.Email,
                    Email = u.Email,
                    NombreCompleto = u.Nombre,
                    Cargo = u.Cargo,
                    EmailConfirmed = true
                };
                var resultado = await userManager.CreateAsync(nuevoUsuario, u.Password);
                if (resultado.Succeeded)
                {
                    await userManager.AddToRoleAsync(nuevoUsuario, u.Rol);
                    logger.LogInformation("Usuario '{Rol}' creado: {Email}", u.Rol, u.Email);
                }
            }
        }

        // Seed de clientes
        if (!context.Clientes.Any())
        {
            context.Clientes.AddRange(
                new Cliente { Nombre = "Textiles Arequipa SAC", RucDni = "20456789012", Direccion = "Av. Industrial 234, Arequipa", Telefono = "054-234567", Correo = "ventas@textilesarequipa.com", Activo = true },
                new Cliente { Nombre = "Confecciones Lima SRL", RucDni = "20345678901", Direccion = "Jr. Comercio 567, Lima", Telefono = "01-5678901", Correo = "pedidos@confeccioneslima.com", Activo = true }
            );
            await context.SaveChangesAsync();
            logger.LogInformation("Clientes de prueba creados");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error al inicializar la base de datos");
    }
}
