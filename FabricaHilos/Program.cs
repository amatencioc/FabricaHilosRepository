using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using FabricaHilos.Data;
using FabricaHilos.Models;
using FabricaHilos.Models.Inventario;
using FabricaHilos.Models.Ventas;
using FabricaHilos.Models.RecursosHumanos;
using FabricaHilos.Services;
using FabricaHilos.Services.Produccion;
using FabricaHilos.Services.Sgc;
using QuestPDF.Infrastructure;
using FabricaHilos.Config;

var builder = WebApplication.CreateBuilder(args);

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
    options.LoginPath          = "/Account/Login";
    options.LogoutPath         = "/Account/Logout";
    options.AccessDeniedPath   = "/Account/AccesoDenegado";
    options.ExpireTimeSpan     = TimeSpan.FromHours(sessionHours); // expira tras inactividad
    options.SlidingExpiration  = true;  // se renueva en cada request activo
    options.Cookie.HttpOnly    = true;
    options.Cookie.IsEssential = true;
});

// Habilitar acceso al HttpContext desde servicios y sesión por usuario
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout        = TimeSpan.FromHours(sessionHours); // sincronizado con Identity
    options.Cookie.HttpOnly    = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name        = ".FabricaHilos.Session";
    options.Cookie.SameSite    = SameSiteMode.Lax;
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

// Registrar servicios de negocio
builder.Services.AddScoped<IRecetaService, RecetaService>();
builder.Services.AddScoped<IParoService, ParoService>();
builder.Services.AddScoped<ISgcService, SgcService>();
builder.Services.AddScoped<IDashboardSgcService, DashboardSgcService>();
builder.Services.AddScoped<ICargaTcService, CargaTcService>();
builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddSingleton<ISalidaInternaPdfService, SalidaInternaPdfService>();
builder.Services.AddSingleton<INavTokenService, NavTokenService>();

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
    .AddRazorOptions(options =>
    {
        // Permite que Views/Produccion/{Controller}/{Action}.cshtml sea encontrado automáticamente
        options.ViewLocationFormats.Add("/Views/Produccion/{1}/{0}.cshtml");
        // Permite que Views/Sgc/{Controller}/{Action}.cshtml sea encontrado automáticamente
        options.ViewLocationFormats.Add("/Views/Sgc/{1}/{0}.cshtml");
    });

// Visibilidad de menús del sidebar (configurable en appsettings.json)
builder.Services.Configure<MenuOptions>(
    builder.Configuration.GetSection(MenuOptions.Seccion));

var app = builder.Build();

// Inicializar base de datos y seed data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await InicializarBD(services);
}

// Configure pipeline HTTP
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Landing}/{id?}");

app.Run();

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
        string[] roles = { "Admin", "Gerencia", "Supervisor", "Trabajador" };
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

        // Seed de materias primas
        if (!context.MateriasPrimas.Any())
        {
            context.MateriasPrimas.AddRange(
                new MateriaPrima { Nombre = "Algodón Pima", Tipo = "Fibra Natural", UnidadMedida = "kg", CantidadDisponible = 500, StockMinimo = 100, Proveedor = "Textiles del Norte SAC", FechaUltimoIngreso = DateTime.Now.AddDays(-5) },
                new MateriaPrima { Nombre = "Poliéster Reciclado", Tipo = "Fibra Sintética", UnidadMedida = "kg", CantidadDisponible = 300, StockMinimo = 150, Proveedor = "Industrias Químicas Lima", FechaUltimoIngreso = DateTime.Now.AddDays(-10) },
                new MateriaPrima { Nombre = "Nylon 66", Tipo = "Fibra Sintética", UnidadMedida = "kg", CantidadDisponible = 80, StockMinimo = 100, Proveedor = "Importadora Textil SRL", FechaUltimoIngreso = DateTime.Now.AddDays(-15) },
                new MateriaPrima { Nombre = "Lana Merino", Tipo = "Fibra Natural", UnidadMedida = "kg", CantidadDisponible = 200, StockMinimo = 50, Proveedor = "Ganadería Andina EIRL", FechaUltimoIngreso = DateTime.Now.AddDays(-3) },
                new MateriaPrima { Nombre = "Colorante Azul Naval", Tipo = "Colorante", UnidadMedida = "lt", CantidadDisponible = 40, StockMinimo = 20, Proveedor = "QuimiColor SAC", FechaUltimoIngreso = DateTime.Now.AddDays(-7) }
            );
            await context.SaveChangesAsync();
            logger.LogInformation("Materias primas de prueba creadas");
        }

        // Seed de empleados
        if (!context.Empleados.Any())
        {
            context.Empleados.AddRange(
                new Empleado { NombreCompleto = "Carlos Ramírez Torres", Dni = "45678901", Cargo = "Supervisor de Producción", Area = "Producción", FechaIngreso = new DateTime(2020, 3, 15), Salario = 3500, Activo = true, Telefono = "987654321" },
                new Empleado { NombreCompleto = "María Quispe Huanca", Dni = "34567890", Cargo = "Operaria de Hilandería", Area = "Producción", FechaIngreso = new DateTime(2019, 6, 1), Salario = 1800, Activo = true, Telefono = "976543210" },
                new Empleado { NombreCompleto = "Jorge Mendoza Cano", Dni = "56789012", Cargo = "Jefe de Ventas", Area = "Ventas", FechaIngreso = new DateTime(2021, 1, 10), Salario = 4200, Activo = true, Telefono = "965432109" }
            );
            await context.SaveChangesAsync();
            logger.LogInformation("Empleados de prueba creados");
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
