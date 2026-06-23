using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using FabricaHilos.Models.SaludOcupacional;

namespace FabricaHilos.Services.SaludOcupacional;

public interface ISoInspeccionPdfService
{
    byte[] Generar(SoDetalleInspeccionViewModel datos, string logoPath);
}

public class SoInspeccionPdfService : ISoInspeccionPdfService
{
    // ── Paleta corporativa teal/sanitaria ────────────────────────────────────
    private static readonly string C_TEAL       = "#0f5132";
    private static readonly string C_TEAL_MID   = "#198754";
    private static readonly string C_TEAL_LIGHT = "#d1e7dd";
    private static readonly string C_WARN_BG    = "#fff3cd";
    private static readonly string C_WARN_BORD  = "#664d03";
    private static readonly string C_DANGER_BG  = "#f8d7da";
    private static readonly string C_DANGER_BORD= "#842029";
    private static readonly string C_GRAY_HDR   = "#f8f9fa";
    private static readonly string C_GRAY_BORD  = "#dee2e6";

    public byte[] Generar(SoDetalleInspeccionViewModel datos, string logoPath)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var insp    = datos.Inspeccion;
        var rubros  = datos.Rubros;
        var acciones = datos.Acciones;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(28, Unit.Point);
                page.MarginTop(22, Unit.Point);
                page.MarginBottom(28, Unit.Point);
                page.DefaultTextStyle(x => x.FontSize(8.5f).FontFamily(Fonts.Arial));

                // ── Encabezado del documento ────────────────────────────────
                page.Header().Column(hdr =>
                {
                    hdr.Item().Row(row =>
                    {
                        // Logo
                        row.ConstantItem(90).AlignMiddle().Column(c =>
                        {
                            if (File.Exists(logoPath))
                                c.Item().MaxHeight(55).Image(logoPath).FitArea();
                            else
                                c.Item().Text("La Colonial\nFABRICA DE HILOS S.A.")
                                        .Bold().FontSize(9).LineHeight(1.3f);
                        });

                        row.RelativeItem().AlignMiddle().AlignCenter().Column(c =>
                        {
                            c.Item().Text("INFORME DE INSPECCIÓN")
                                    .Bold().FontSize(13).FontColor(C_TEAL);
                            c.Item().Text("COMEDOR Y COCINA")
                                    .Bold().FontSize(11).FontColor(C_TEAL);
                        });

                        row.ConstantItem(90).AlignMiddle().AlignRight().Column(c =>
                        {
                            c.Item().Text($"Fecha: {insp.FechaInsp:dd/MM/yyyy}")
                                    .FontSize(8).FontColor("#555");
                            if (!string.IsNullOrEmpty(insp.HoraInsp))
                                c.Item().Text($"Hora: {insp.HoraInsp}")
                                        .FontSize(8).FontColor("#555");
                        });
                    });

                    hdr.Item().Height(4);
                    hdr.Item().LineHorizontal(1.5f).LineColor(C_TEAL);
                    hdr.Item().Height(5);
                });

                // ── Contenido principal ──────────────────────────────────────
                page.Content().Column(col =>
                {
                    // ── Ficha de la inspección ──────────────────────────────
                    col.Item().Border(0.5f).BorderColor(C_GRAY_BORD)
                       .Background(C_GRAY_HDR).Padding(8).Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            FilaDato(left, "Comedor",       insp.NombreComedor);
                            FilaDato(left, "Concesionaria", insp.NombreConc ?? "—");
                            FilaDato(left, "Encargada",     insp.Encargada  ?? "—");
                        });
                        row.ConstantItem(10);
                        row.RelativeItem().Column(right =>
                        {
                            FilaDato(right, "Inspector",  insp.Inspector  ?? "—");
                            FilaDato(right, "Médico SSO", insp.Medico      ?? "—");
                            FilaDato(right, "Registrado", insp.UsrCrea     ?? "—");
                        });
                    });

                    col.Item().Height(8);

                    // ── Resultado global ────────────────────────────────────
                    string calColor = insp.Calificacion switch
                    {
                        "ACEPTABLE"       => C_TEAL_MID,
                        "CON OBSERVACION" => C_WARN_BORD,
                        _                 => C_DANGER_BORD
                    };
                    string calBg = insp.Calificacion switch
                    {
                        "ACEPTABLE"       => C_TEAL_LIGHT,
                        "CON OBSERVACION" => C_WARN_BG,
                        _                 => C_DANGER_BG
                    };

                    col.Item().Background(calBg).Border(0.5f).BorderColor(C_GRAY_BORD)
                       .Padding(8).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("RESULTADO GLOBAL").Bold().FontSize(9)
                                    .FontColor(C_TEAL);
                        });
                        row.RelativeItem().AlignCenter().Column(c =>
                        {
                            c.Item().AlignCenter()
                                    .Text($"{insp.PtsObtenidos:0} / {insp.PtsMaximo:0} pts")
                                    .Bold().FontSize(10).FontColor(calColor);
                        });
                        row.RelativeItem().AlignRight().Column(c =>
                        {
                            c.Item().AlignRight()
                                    .Text($"{insp.PctCumpl:0.0}% — {insp.Calificacion ?? "—"}")
                                    .Bold().FontSize(10).FontColor(calColor);
                        });
                    });

                    col.Item().Height(10);

                    // ── Resumen por rubro ───────────────────────────────────
                    col.Item().Text("I. RESUMEN POR RUBRO")
                              .Bold().FontSize(9.5f).FontColor(C_TEAL);
                    col.Item().Height(3);

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(8);  // nombre rubro
                            cols.RelativeColumn(2);  // pts
                            cols.RelativeColumn(2);  // pct
                            cols.RelativeColumn(3);  // barra visual
                        });

                        // Cabecera
                        table.Header(header =>
                        {
                            ThCell(header.Cell(), "Rubro");
                            ThCell(header.Cell(), "Pts");
                            ThCell(header.Cell(), "%");
                            ThCell(header.Cell(), "Nivel");
                        });

                        foreach (var r in rubros)
                        {
                            double pct = r.PtsMaximoRubro > 0
                                ? r.PtsObtenidosRubro * 100.0 / r.PtsMaximoRubro
                                : 0;
                            string nivelTxt = pct >= 75 ? "Aceptable"
                                            : pct >= 51 ? "Con Observación"
                                            :             "No Aceptable";
                            string nivelColor = pct >= 75 ? C_TEAL_MID
                                              : pct >= 51 ? C_WARN_BORD
                                              :             C_DANGER_BORD;

                            TdCell(table.Cell(), r.Rubro.Nombre);
                            TdCell(table.Cell(), $"{r.PtsObtenidosRubro}/{r.PtsMaximoRubro}", center: true);
                            TdCell(table.Cell(), $"{pct:0.0}%", center: true);
                            table.Cell().BorderBottom(0.4f).BorderColor(C_GRAY_BORD)
                                        .PaddingVertical(3).PaddingHorizontal(4)
                                        .Text(nivelTxt).FontSize(7.5f).FontColor(nivelColor).Bold();
                        }
                    });

                    col.Item().Height(12);

                    // ── Detalle del checklist ───────────────────────────────
                    col.Item().Text("II. DETALLE DEL CHECKLIST")
                              .Bold().FontSize(9.5f).FontColor(C_TEAL);
                    col.Item().Height(3);

                    foreach (var r in rubros)
                    {
                        // Cabecera de rubro
                        col.Item().Background(C_TEAL).Padding(4).Row(row =>
                        {
                            row.RelativeItem().Text(r.Rubro.Nombre)
                               .Bold().FontSize(8.5f).FontColor("#FFFFFF");
                            row.ConstantItem(70).AlignRight()
                               .Text($"{r.PtsObtenidosRubro}/{r.PtsMaximoRubro} pts")
                               .Bold().FontSize(8).FontColor("#d1e7dd");
                        });

                        // Ítems del rubro
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(1);  // nro
                                cols.RelativeColumn(10); // descripción + hallazgo
                                cols.ConstantColumn(35); // puntaje
                            });

                            int idx = 1;
                            foreach (var det in r.Items)
                            {
                                bool bg = idx % 2 == 0;
                                string bgColor = bg ? "#f8f9fa" : "#ffffff";

                                table.Cell().Background(bgColor)
                                     .BorderBottom(0.4f).BorderColor(C_GRAY_BORD)
                                     .PaddingVertical(3).PaddingHorizontal(4)
                                     .Text($"{idx}").FontSize(7.5f).FontColor("#888");

                                // Descripción + hallazgo
                                var hayHallazgo = !string.IsNullOrEmpty(det.Hallazgo);
                                table.Cell().Background(bgColor)
                                     .BorderBottom(0.4f).BorderColor(C_GRAY_BORD)
                                     .PaddingVertical(3).PaddingHorizontal(4)
                                     .Column(c =>
                                     {
                                         c.Item().Text(det.Descripcion)
                                                 .FontSize(8);
                                         if (hayHallazgo)
                                         {
                                             c.Item().Height(1);
                                             c.Item().Text($"⚠ {det.Hallazgo}")
                                                     .FontSize(7.5f).FontColor(C_DANGER_BORD)
                                                             .Italic();
                                         }
                                     });

                                // Puntaje badge
                                string ptsColor = det.Puntaje == 4 ? C_TEAL_MID
                                                : det.Puntaje == 2 ? C_WARN_BORD
                                                :                     C_DANGER_BORD;
                                table.Cell().Background(bgColor)
                                     .BorderBottom(0.4f).BorderColor(C_GRAY_BORD)
                                     .AlignMiddle().AlignCenter()
                                     .PaddingVertical(3)
                                     .Text($"{det.Puntaje}")
                                     .Bold().FontSize(9).FontColor(ptsColor);

                                idx++;
                            }
                        });

                        col.Item().Height(4);
                    }

                    // ── Evidencias fotográficas ─────────────────────────────
                    var evidencias = datos.Evidencias
                        .Where(e => !string.IsNullOrEmpty(e.RutaArch))
                        .ToList();

                    if (evidencias.Any())
                    {
                        col.Item().Height(8);
                        col.Item().Text("III. EVIDENCIAS FOTOGRÁFICAS")
                                  .Bold().FontSize(9.5f).FontColor(C_TEAL);
                        col.Item().Height(5);

                        // Grilla 3 fotos por fila
                        foreach (var chunk in evidencias.Chunk(3))
                        {
                            col.Item().Row(row =>
                            {
                                foreach (var ev in chunk)
                                {
                                    row.RelativeItem().Column(cell =>
                                    {
                                                        if (!string.IsNullOrEmpty(ev.RutaFisica) && File.Exists(ev.RutaFisica))
                                        {
                                            cell.Item().MaxHeight(90).Image(ev.RutaFisica).FitArea();
                                        }
                                        else
                                        {
                                            cell.Item().Height(90).Background("#e9ecef")
                                                .AlignMiddle().AlignCenter()
                                                .Text("Sin imagen").FontSize(7).FontColor("#888");
                                        }
                                        if (!string.IsNullOrEmpty(ev.Descripcion))
                                            cell.Item().AlignCenter()
                                                       .Text(ev.Descripcion)
                                                       .FontSize(7).FontColor("#555").Italic();
                                    });
                                    row.ConstantItem(6);
                                }
                                // Completar si quedan celdas vacías
                                for (int i = chunk.Length; i < 3; i++)
                                    row.RelativeItem();
                            });
                            col.Item().Height(6);
                        }
                    }

                    // ── Acciones correctivas ────────────────────────────────
                    if (acciones.Any())
                    {
                        col.Item().Height(8);
                        var seccion = evidencias.Any() ? "IV" : "III";
                        col.Item().Text($"{seccion}. ACCIONES CORRECTIVAS")
                                  .Bold().FontSize(9.5f).FontColor(C_TEAL);
                        col.Item().Height(3);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(7);  // descripción
                                cols.RelativeColumn(3);  // responsable
                                cols.ConstantColumn(60); // plazo
                                cols.ConstantColumn(55); // estado
                            });

                            // Cabecera
                            table.Header(header =>
                            {
                                ThCell(header.Cell(), "Acción correctiva");
                                ThCell(header.Cell(), "Responsable");
                                ThCell(header.Cell(), "Plazo");
                                ThCell(header.Cell(), "Estado");
                            });

                            foreach (var ac in acciones)
                            {
                                TdCell(table.Cell(), ac.Descripcion);
                                TdCell(table.Cell(), ac.Responsable ?? "—");
                                TdCell(table.Cell(), ac.FchLimite?.ToString("dd/MM/yyyy") ?? "—", center: true);

                                string estLabel = ac.Estado switch
                                {
                                    "P" => "Pendiente",
                                    "E" => "En proceso",
                                    "R" => "Resuelta",
                                    _   => ac.Estado
                                };
                                string estColor = ac.Estado switch
                                {
                                    "R" => C_TEAL_MID,
                                    "E" => "#084298",
                                    _   => ac.EsVencida ? C_DANGER_BORD : "#555"
                                };
                                table.Cell().BorderBottom(0.4f).BorderColor(C_GRAY_BORD)
                                            .PaddingVertical(3).PaddingHorizontal(4)
                                            .Text(estLabel).FontSize(7.5f).FontColor(estColor).Bold();
                            }
                        });
                    }

                    // ── Firmas ──────────────────────────────────────────────
                    col.Item().Height(24);
                    col.Item().Row(row =>
                    {
                        FirmaBloque(row.RelativeItem(), insp.Inspector ?? "—", "Inspector");
                        row.ConstantItem(40);
                        FirmaBloque(row.RelativeItem(), insp.Medico ?? "—", "Médico SSO");
                        row.ConstantItem(40);
                        FirmaBloque(row.RelativeItem(), insp.Encargada ?? "—", "Encargado/a Comedor");
                    });
                });

                // ── Pie de página ────────────────────────────────────────────
                page.Footer().AlignCenter().Text(txt =>
                {
                    txt.Span("La Colonial — Salud Ocupacional | ")
                       .FontSize(7).FontColor("#888");
                    txt.Span($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}  |  Pág. ")
                       .FontSize(7).FontColor("#888");
                    txt.CurrentPageNumber().FontSize(7).FontColor("#888");
                    txt.Span(" de ").FontSize(7).FontColor("#888");
                    txt.TotalPages().FontSize(7).FontColor("#888");
                });
            });
        }).GeneratePdf();
    }

    // ── Helpers de celdas ───────────────────────────────────────────────────
    private static void ThCell(IContainer cell, string text)
    {
        cell.Background(C_TEAL).PaddingVertical(4).PaddingHorizontal(5)
            .Text(text).Bold().FontSize(8).FontColor("#FFFFFF");
    }

    private static void TdCell(IContainer cell, string text, bool center = false)
    {
        var t = cell.BorderBottom(0.4f).BorderColor(C_GRAY_BORD)
                    .PaddingVertical(3).PaddingHorizontal(4);
        var txt = t.Text(text).FontSize(8);
        if (center) txt.AlignCenter();
    }

    private static void FilaDato(ColumnDescriptor col, string label, string? value)
    {
        col.Item().Row(r =>
        {
            r.ConstantItem(70).Text(label + ":").Bold().FontSize(8).FontColor("#555");
            r.RelativeItem().Text(value ?? "—").FontSize(8);
        });
        col.Item().Height(2);
    }

    private static void FirmaBloque(IContainer container, string nombre, string rol)
    {
        container.Column(c =>
        {
            c.Item().Height(28).BorderBottom(0.8f).BorderColor("#555");
            c.Item().Height(2);
            c.Item().AlignCenter().Text(nombre).Bold().FontSize(7.5f);
            c.Item().AlignCenter().Text(rol).FontSize(7).FontColor("#777");
        });
    }
}
