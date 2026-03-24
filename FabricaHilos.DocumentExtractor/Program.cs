using FabricaHilos.DocumentExtractor.Services;

var builder = WebApplication.CreateBuilder(args);

// Allow up to 30 MB globally for multipart/form-data uploads
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 30 * 1024 * 1024;
});
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 30 * 1024 * 1024);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "FabricaHilos - Document Extractor API",
        Version = "v1",
        Description = "API para extracción de datos de PDFs y documentos peruanos (SUNAT)"
    });
});

// Singleton: PdfExtractorService has no mutable instance state — all methods are static
// and create a new TesseractEngine per call. Singleton avoids repeated DI overhead.
builder.Services.AddSingleton<IDocumentExtractorService, PdfExtractorService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();
