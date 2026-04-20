var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
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

// Cache de archivos estáticos: 30 días para producción
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (!app.Environment.IsDevelopment())
            ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=2592000");
    }
});

app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

