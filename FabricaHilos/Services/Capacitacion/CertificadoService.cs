using Dapper;
using FabricaHilos.Models.Capacitacion;
using Oracle.ManagedDataAccess.Client;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FabricaHilos.Services.Capacitacion;

public class CertificadoService : OracleServiceBase, ICertificadoService
{
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration      _config;

    public CertificadoService(IConfiguration cfg, IHttpContextAccessor http, IWebHostEnvironment env)
        : base(cfg, http)
    {
        _env    = env;
        _config = cfg;
    }

    public async Task<CapCertificado?> GetAsync(int idCertificado, string codUsuario)
    {
        await using var db = new OracleConnection(GetOracleConnectionString());
        return await db.QueryFirstOrDefaultAsync<CapCertificado>(
            $@"SELECT * FROM {S}CAP_CERTIFICADO
               WHERE ID_CERTIFICADO = :id AND COD_USUARIO = :usr AND ESTADO <> 'X'",
            new { id = idCertificado, usr = codUsuario });
    }

    public async Task<CapCertificado?> GetByCodigoAsync(string codigoVerif)
    {
        await using var db = new OracleConnection(GetOracleConnectionString());
        return await db.QueryFirstOrDefaultAsync<CapCertificado>(
            $"SELECT * FROM {S}CAP_CERTIFICADO WHERE CODIGO_VERIF = :cod",
            new { cod = codigoVerif });
    }

    public async Task<CapCertificado?> EmitirAsync(long idIntento, long idInscripcion, string codUsuario)
    {
        await using var db = new OracleConnection(GetOracleConnectionString());
        await db.OpenAsync();

        await using var cmd = db.CreateCommand();
        cmd.CommandText =
            $"BEGIN {S}PKG_CAP.SP_CAP_EMITIR_CERT(:p_int, :p_insc, :p_usr, :p_id, :p_cod, :p_res); END;";

        cmd.Parameters.Add(new OracleParameter("p_int",  OracleDbType.Decimal)        { Value     = idIntento     });
        cmd.Parameters.Add(new OracleParameter("p_insc", OracleDbType.Decimal)        { Value     = idInscripcion });
        cmd.Parameters.Add(new OracleParameter("p_usr",  OracleDbType.Varchar2, 50)   { Value     = codUsuario    });
        cmd.Parameters.Add(new OracleParameter("p_id",   OracleDbType.Decimal)         { Direction = System.Data.ParameterDirection.Output });
        cmd.Parameters.Add(new OracleParameter("p_cod",  OracleDbType.Varchar2, 40)   { Direction = System.Data.ParameterDirection.Output });
        cmd.Parameters.Add(new OracleParameter("p_res",  OracleDbType.Varchar2, 20)   { Direction = System.Data.ParameterDirection.Output });

        await cmd.ExecuteNonQueryAsync();

        var resultado = cmd.Parameters["p_res"].Value?.ToString() ?? "ERROR";
        if (resultado is "ERROR" or "NO_APROBADO" or "NO_ENCONTRADO")
            return null;

        var idOut  = cmd.Parameters["p_id"].Value is Oracle.ManagedDataAccess.Types.OracleDecimal od && !od.IsNull
                     ? Convert.ToInt32(od.Value) : 0;
        var codigo = cmd.Parameters["p_cod"].Value?.ToString() ?? "";

        // Leer el certificado recién creado para devolver el modelo completo
        return await GetAsync(idOut, codUsuario)
            ?? new CapCertificado { IdCertificado = idOut, CodigoVerif = codigo, Estado = "V" };
    }

    public async Task<byte[]?> GenerarPdfAsync(int idCertificado)
    {
        await using var db = new OracleConnection(GetOracleConnectionString());
        var cert = await db.QueryFirstOrDefaultAsync<CapCertificado>(
            $"SELECT * FROM {S}CAP_CERTIFICADO WHERE ID_CERTIFICADO = :id", new { id = idCertificado });

        if (cert == null) return null;

        // Generación PDF con QuestPDF 2024
        try
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var pdf = Document.Create(doc =>
            {
                doc.Page(page =>
                {
                    // A4 apaisado (297×210 mm)
                    page.Size(new PageSize(
                        QuestPDF.Helpers.PageSizes.A4.Height,
                        QuestPDF.Helpers.PageSizes.A4.Width));
                    page.Margin(30, Unit.Point);
                    page.PageColor(Color.FromHex("#FFFDF0"));

                    page.Content().Column(col =>
                    {
                        col.Item().AlignCenter().Text("CERTIFICADO DE CAPACITACIÓN")
                            .FontSize(28).Bold().FontColor(Color.FromHex("#8B6914"));
                        col.Item().Height(20);
                        col.Item().AlignCenter().Text("Se certifica que:")
                            .FontSize(14).FontColor(Color.FromHex("#555"));
                        col.Item().Height(10);
                        col.Item().AlignCenter().Text(cert.NombreUsuario)
                            .FontSize(22).Bold().FontColor(Color.FromHex("#1a1a1a"));
                        col.Item().Height(10);
                        col.Item().AlignCenter().Text($"completó satisfactoriamente el curso:")
                            .FontSize(14).FontColor(Color.FromHex("#555"));
                        col.Item().Height(10);
                        col.Item().AlignCenter().Text($"\"{cert.TituloCurso}\"")
                            .FontSize(20).Bold().Italic().FontColor(Color.FromHex("#0d6efd"));
                        col.Item().Height(20);
                        col.Item().AlignCenter().Text($"Nota obtenida: {cert.PuntajeObt:F1} / 100")
                            .FontSize(14);
                        col.Item().Height(10);
                        col.Item().AlignCenter().Text($"Emitido el {cert.FchEmision:dd/MM/yyyy}")
                            .FontSize(12).FontColor(Color.FromHex("#777"));
                        col.Item().Height(30);
                        col.Item().AlignCenter().Text($"Código de verificación: {cert.CodigoVerif}")
                            .FontSize(9).FontColor(Color.FromHex("#999"));
                    });
                });
            }).GeneratePdf();

            return pdf;
        }
        catch
        {
            return null;
        }
    }
}
