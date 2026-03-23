using FabricaHilos.DocumentExtractor.Services;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddScoped<IDocumentExtractorService, PdfExtractorService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();
