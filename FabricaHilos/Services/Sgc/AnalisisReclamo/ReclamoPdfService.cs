using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using FabricaHilos.Models.Sgc;

namespace FabricaHilos.Services.Sgc.AnalisisReclamo;

public interface IReclamoPdfService
{
    /// <summary>
    /// Genera un PDF del reclamo aprobado con todos los detalles,
    /// descargos, archivos y firma de gerencia.
    /// </summary>
    byte[] GenerarPdf(ReclamoImpresionDto datos, string logoPath = "");
}

public class ReclamoPdfService : IReclamoPdfService
{
    private const string ColorPrimario = "#0066cc";
    private const string ColorExito = "#28a745";

    public byte[] GenerarPdf(ReclamoImpresionDto datos, string logoPath = "")
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(25, Unit.Point);
                page.MarginVertical(20, Unit.Point);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily(Fonts.Arial));

                page.Content().Column(col =>
                {
                    // ══════════════ ENCABEZADO ══════════════
                    col.Item().Row(row =>
                    {
                        // Logo + nombre empresa
                        row.RelativeItem(4).Padding(6).Row(logoRow =>
                        {
                            if (!string.IsNullOrEmpty(logoPath) && File.Exists(logoPath))
                            {
                                logoRow.RelativeItem(3).MaxHeight(50).Image(logoPath).FitArea();
                                logoRow.ConstantItem(6);
                            }
                            logoRow.RelativeItem(2).AlignMiddle().Column(c =>
                            {
                                c.Item().Text("La Colonial").Bold().FontSize(12);
                                c.Item().Text("FABRICA DE HILOS S.A.").FontSize(8);
                            });
                        });

                        // Datos empresa
                        row.RelativeItem(6).Padding(8).Column(info =>
                        {
                            info.Item().AlignCenter()
                                .Text($"REPORTE DE ANÁLISIS DE RECLAMO")
                                .Bold().FontSize(11);
                            info.Item().AlignCenter()
                                .Text($"Reclamo Nº {datos.Reclamo.IdReclamo}")
                                .Bold().FontSize(10);
                        });
                    });

                    col.Item().Padding(4).Background(ColorPrimario);

                    // ══════════════ INFORMACIÓN GENERAL ══════════════
                    col.Item().PaddingTop(10).Column(sec =>
                    {
                        sec.Item().Text("INFORMACIÓN GENERAL DEL RECLAMO").Bold().FontSize(10);
                        sec.Item().Padding(2).Background(ColorPrimario);

                        sec.Item().PaddingTop(5).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Cliente:").Bold();
                                c.Item().Text(datos.Reclamo.NomCliente ?? datos.Reclamo.CodCliente).FontSize(10);

                                c.Item().PaddingTop(5).Text("RUC:").Bold();
                                c.Item().Text(datos.Reclamo.RucCliente ?? "-").FontSize(10);
                            });

                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Teléfono:").Bold();
                                c.Item().Text(datos.Reclamo.Telefono).FontSize(10);

                                c.Item().PaddingTop(5).Text("Contacto:").Bold();
                                c.Item().Text(datos.Reclamo.Contacto).FontSize(10);
                            });

                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("Asunto:").Bold();
                                c.Item().Text(datos.Reclamo.Asunto).FontSize(10);

                                c.Item().PaddingTop(5).Text("Estado:").Bold();
                                c.Item().Text("APROBADO").FontSize(10).FontColor(ColorExito);
                            });
                        });
                    });

                    // ══════════════ CRONOLOGÍA ══════════════
                    col.Item().PaddingTop(15).Column(sec =>
                    {
                        sec.Item().Text("CRONOLOGÍA DE EVENTOS").Bold().FontSize(10);
                        sec.Item().Padding(2).Background(ColorPrimario);

                        sec.Item().PaddingTop(5).Row(row =>
                        {
                            row.ConstantItem(15).Text("•").FontSize(8);
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text($"Creación: {datos.Reclamo.FchCreacion:dd/MM/yyyy HH:mm:ss}").FontSize(9);
                                c.Item().Text($"Vendedor: {datos.Reclamo.UsuVendedor}").FontSize(8).FontColor("#666");
                            });
                        });

                        if (!string.IsNullOrEmpty(datos.Reclamo.UsuAnalista))
                        {
                            sec.Item().PaddingTop(3).Row(row =>
                            {
                                row.ConstantItem(15).Text("•").FontSize(8);
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text($"Análisis: {datos.Reclamo.FchAnalisis:dd/MM/yyyy HH:mm:ss}").FontSize(9);
                                    c.Item().Text($"Analista: {datos.Reclamo.UsuAnalista}").FontSize(8).FontColor("#666");
                                });
                            });
                        }

                        if (!string.IsNullOrEmpty(datos.Reclamo.UsuGerente))
                        {
                            sec.Item().PaddingTop(3).Row(row =>
                            {
                                row.ConstantItem(15).Text("•").FontSize(8);
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text($"Aprobación: {datos.Reclamo.FchAprobacion:dd/MM/yyyy HH:mm:ss}").FontSize(9);
                                    c.Item().Text($"Gerente: {datos.Reclamo.UsuGerente}").FontSize(8).FontColor("#666");
                                });
                            });
                        }
                    });

                    // ══════════════ DESCARGOS ══════════════
                    if (datos.Descargos.Any())
                    {
                        col.Item().PaddingTop(15).Column(sec =>
                        {
                            sec.Item().Text("DESCARGOS Y ANÁLISIS").Bold().FontSize(10);
                            sec.Item().Padding(2).Background(ColorPrimario);

                            foreach (var d in datos.Descargos)
                            {
                                sec.Item().PaddingTop(5).Padding(5).Border(1).BorderColor("#ddd").Column(c =>
                                {
                                    c.Item().Row(r =>
                                    {
                                        r.RelativeItem().Text($"{d.Usuario} ({d.RolTexto})").Bold().FontSize(9);
                                        r.ConstantItem(100).AlignRight().Text($"{d.FchRegistro:dd/MM/yyyy HH:mm}").FontSize(8).FontColor("#666");
                                    });

                                    c.Item().PaddingTop(3).Text(d.Descripcion).FontSize(9).LineHeight(1.4f);
                                });
                            }
                        });
                    }

                    // ══════════════ ANÁLISIS DE CAUSA ══════════════
                    if (!string.IsNullOrWhiteSpace(datos.Reclamo.AnalisisCausa))
                    {
                        col.Item().PaddingTop(15).Column(sec =>
                        {
                            sec.Item().Text("ANÁLISIS DE CAUSA").Bold().FontSize(10);
                            sec.Item().Padding(2).Background(ColorPrimario);

                            sec.Item().PaddingTop(5).Padding(5).Border(1).BorderColor("#ddd").Background("#f9f9f9")
                                .Text(datos.Reclamo.AnalisisCausa).FontSize(9).LineHeight(1.5f);
                        });
                    }

                    // ══════════════ DECISIÓN FINAL ══════════════
                    if (!string.IsNullOrWhiteSpace(datos.Reclamo.DecisionFinal))
                    {
                        col.Item().PaddingTop(15).Column(sec =>
                        {
                            sec.Item().Text("DECISIÓN FINAL").Bold().FontSize(10);
                            sec.Item().Padding(2).Background(ColorExito);

                            sec.Item().PaddingTop(5).Padding(5).Border(2).BorderColor(ColorExito).Background("#e8f5e9")
                                .Text(datos.Reclamo.DecisionFinal).FontSize(9).LineHeight(1.5f);
                        });
                    }

                    // ══════════════ ARCHIVOS ADJUNTOS ══════════════
                    if (datos.Archivos.Any())
                    {
                        col.Item().PaddingTop(15).Column(sec =>
                        {
                            sec.Item().Text("ARCHIVOS ADJUNTOS").Bold().FontSize(10);
                            sec.Item().Padding(2).Background(ColorPrimario);

                            sec.Item().PaddingTop(5).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(4);
                                    columns.RelativeColumn(1.5f);
                                    columns.RelativeColumn(1.5f);
                                    columns.RelativeColumn(1.5f);
                                });

                                // Headers
                                table.Header(header =>
                                {
                                    header.Cell().Background(ColorPrimario).Padding(4)
                                        .Text("Archivo").FontColor("white").Bold().FontSize(8);
                                    header.Cell().Background(ColorPrimario).Padding(4)
                                        .Text("Rol").FontColor("white").Bold().FontSize(8);
                                    header.Cell().Background(ColorPrimario).Padding(4)
                                        .AlignCenter().Text("Tamaño").FontColor("white").Bold().FontSize(8);
                                    header.Cell().Background(ColorPrimario).Padding(4)
                                        .AlignRight().Text("Fecha").FontColor("white").Bold().FontSize(8);
                                });

                                // Rows
                                foreach (var a in datos.Archivos.OrderBy(x => x.FchCarga))
                                {
                                    table.Cell().Padding(4).Text(a.NombreOrig).FontSize(8);
                                    table.Cell().Padding(4).Text(a.RolTexto).FontSize(8);
                                    table.Cell().Padding(4).AlignCenter().Text(a.TamanioTexto).FontSize(8);
                                    table.Cell().Padding(4).AlignRight().Text(a.FchCarga.ToString("dd/MM/yyyy HH:mm")).FontSize(8);
                                }
                            });
                        });
                    }

                    col.Item().PaddingTop(20).Background("#ddd");
                });

                // ══════════════ PIE DE PÁGINA ══════════════
                page.Footer().Row(row =>
                {
                    row.RelativeItem().AlignLeft()
                        .Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm:ss}")
                        .FontSize(8).FontColor("#666");

                    row.ConstantItem(200).AlignRight()
                        .Text("Sistema de Gestión de Calidad (SGC)")
                        .FontSize(8).FontColor("#666");
                });
            });

        }).GeneratePdf();
    }
}
