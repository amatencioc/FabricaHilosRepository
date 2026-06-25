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
    // ── Paleta corporativa ──────────────────────────────────────────────────────
    private static readonly string C_PRIMARY     = "#1B4332";
    private static readonly string C_PRIMARY_MID = "#2D6A4F";
    private static readonly string C_PRIMARY_LT  = "#D8F3DC";
    private static readonly string C_WARN_BG     = "#FFF8E7";
    private static readonly string C_WARN_TEXT   = "#92400E";
    private static readonly string C_DANGER_BG   = "#FEF2F2";
    private static readonly string C_DANGER_TEXT = "#991B1B";
    private static readonly string C_ROW_ALT     = "#F5FAF7";
    private static readonly string C_BORDER      = "#C6D9CE";
    private static readonly string C_TXT         = "#111827";
    private static readonly string C_TXT_MUTED   = "#6B7280";

    public byte[] Generar(SoDetalleInspeccionViewModel datos, string logoPath)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var insp     = datos.Inspeccion;
        var rubros   = datos.Rubros;
        var acciones = datos.Acciones;
        const float IMG_H  = 82f;
        const float IMG_TH = 76f;

        var hallazgos  = datos.Hallazgos.OrderBy(h => h.Correlativo).ToList();
        var seg        = hallazgos
            .Where(h => !string.IsNullOrWhiteSpace(h.ObsSeguim) ||
                        h.Imgs.Any(i => i.Tipo == "S" && !string.IsNullOrEmpty(i.RutaFisica) && File.Exists(i.RutaFisica)))
            .ToList();
        var evidencias = datos.Evidencias.Where(e => !string.IsNullOrEmpty(e.RutaArch)).ToList();

        int sn = 2;
        string? secHall = hallazgos.Any()              ? Roman(++sn) : null;
        string? secSeg  = secHall != null && seg.Any() ? Roman(++sn) : null;
        string? secEvi  = evidencias.Any()             ? Roman(++sn) : null;
        string? secAcc  = acciones.Any()               ? Roman(++sn) : null;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(30, Unit.Point);
                page.MarginTop(24, Unit.Point);
                page.MarginBottom(24, Unit.Point);
                page.DefaultTextStyle(x => x.FontSize(9f).FontFamily(Fonts.Arial).FontColor(C_TXT));
                page.Header().Height(0);
                page.Footer().Height(0);

                page.Content().Column(col =>
                {
                    // ── ENCABEZADO ──────────────────────────────────────────────
                    col.Item().ShowOnce().Column(hdr =>
                    {
                        hdr.Item().Row(row =>
                        {
                            row.ConstantItem(82).AlignMiddle().Column(c =>
                            {
                                if (File.Exists(logoPath))
                                    c.Item().MaxHeight(52).Image(logoPath).FitArea();
                                else
                                    c.Item().Text("La Colonial\nFABRICA DE HILOS S.A.").Bold().FontSize(9f).LineHeight(1.4f);
                            });
                            row.RelativeItem().AlignMiddle().AlignCenter().Column(c =>
                            {
                                c.Item().Text("INFORME DE INSPECCION").Bold().FontSize(14f).FontColor(C_PRIMARY);
                                c.Item().Height(2);
                                c.Item().Text("COMEDOR Y COCINA").Bold().FontSize(10.5f).FontColor(C_PRIMARY_MID);
                            });
                            row.ConstantItem(95).AlignMiddle().AlignRight().Column(c =>
                            {
                                c.Item().Text($"Fecha:   {insp.FechaInsp:dd/MM/yyyy}").FontSize(9f).FontColor(C_TXT_MUTED);
                                if (!string.IsNullOrEmpty(insp.HoraInsp))
                                {
                                    c.Item().Height(2);
                                    c.Item().Text($"Hora:    {insp.HoraInsp}").FontSize(9f).FontColor(C_TXT_MUTED);
                                }
                            });
                        });
                        hdr.Item().Height(6);
                        hdr.Item().LineHorizontal(2f).LineColor(C_PRIMARY);
                        hdr.Item().Height(10);
                    });

                    // ── DATOS GENERALES ──────────────────────────────────────────
                    col.Item().Border(0.5f).BorderColor(C_BORDER).Background(C_ROW_ALT).Padding(10).Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            FilaDato(left, "Comedor",       insp.NombreComedor);
                            FilaDato(left, "Concesionaria", insp.NombreConc ?? "---");
                            FilaDato(left, "Encargada",     insp.ContactoConc ?? insp.Encargada ?? "---");
                        });
                        row.ConstantItem(14);
                        row.RelativeItem().Column(right =>
                        {
                            FilaDato(right, "Inspector",  insp.Inspector ?? "---");
                            FilaDato(right, "Medico SSO", insp.Medico    ?? "---");
                            FilaDato(right, "Registrado", insp.UsrCrea   ?? "---");
                        });
                    });

                    col.Item().Height(7);

                    // ── RESULTADO GLOBAL ─────────────────────────────────────────
                    string calColor = insp.Calificacion switch
                    {
                        "ACEPTABLE"       => C_PRIMARY_MID,
                        "CON OBSERVACION" => C_WARN_TEXT,
                        _                 => C_DANGER_TEXT
                    };
                    string calBg = insp.Calificacion switch
                    {
                        "ACEPTABLE"       => C_PRIMARY_LT,
                        "CON OBSERVACION" => C_WARN_BG,
                        _                 => C_DANGER_BG
                    };

                    col.Item().Background(calBg).Border(0.5f).BorderColor(C_BORDER).Padding(10).Row(row =>
                    {
                        row.RelativeItem().AlignMiddle()
                           .Text("RESULTADO GLOBAL").Bold().FontSize(10f).FontColor(C_PRIMARY);
                        row.ConstantItem(115).AlignMiddle().AlignCenter()
                           .Text($"{insp.PtsObtenidos:0} / {insp.PtsMaximo:0} pts")
                           .Bold().FontSize(11.5f).FontColor(calColor);
                        row.ConstantItem(140).AlignMiddle().AlignRight()
                           .Text($"{insp.PctCumpl:0.0}%   {insp.Calificacion ?? "---"}")
                           .Bold().FontSize(11.5f).FontColor(calColor);
                    });

                    col.Item().Height(14);

                    // ── I. RESUMEN POR RUBRO ─────────────────────────────────────
                    SeccionTitulo(col, "I", "RESUMEN POR RUBRO");
                    col.Item().Height(5);

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn();   // Rubro (toma el espacio restante)
                            cols.ConstantColumn(65); // Puntaje Obt/Max
                            cols.ConstantColumn(52); // %
                            cols.ConstantColumn(100); // Nivel
                        });
                        table.Header(header =>
                        {
                            TableHdr(header.Cell(), "RUBRO");
                            TableHdr(header.Cell(), "PUNTAJE", center: true);
                            TableHdr(header.Cell(), "%", center: true);
                            TableHdr(header.Cell(), "NIVEL", center: true);
                        });
                        int ri = 0;
                        foreach (var r in rubros)
                        {
                            string bg   = ri++ % 2 == 0 ? "#FFFFFF" : C_ROW_ALT;
                            double pct  = r.PtsMaximoRubro > 0 ? r.PtsObtenidosRubro * 100.0 / r.PtsMaximoRubro : 0;
                            string nTxt = pct >= 75 ? "Aceptable" : pct >= 51 ? "Con Observacion" : "No Aceptable";
                            string nClr = pct >= 75 ? C_PRIMARY_MID : pct >= 51 ? C_WARN_TEXT : C_DANGER_TEXT;
                            string nBg  = pct >= 75 ? C_PRIMARY_LT  : pct >= 51 ? C_WARN_BG   : C_DANGER_BG;
                            TableCell(table.Cell(), bg, r.Rubro.Nombre);
                            TableCell(table.Cell(), bg, $"{r.PtsObtenidosRubro} / {r.PtsMaximoRubro}", center: true);
                            TableCell(table.Cell(), bg, $"{pct:0.0}%", center: true);
                            var txt = table.Cell().Background(nBg).BorderBottom(0.4f).BorderColor(C_BORDER)
                                          .PaddingVertical(5).PaddingHorizontal(6).AlignMiddle()
                                          .Text(nTxt).Bold().FontSize(8.5f).FontColor(nClr);
                            txt.AlignCenter();
                        }
                    });

                    col.Item().Height(14);

                    // ── II. DETALLE DEL CHECKLIST ────────────────────────────────
                    SeccionTitulo(col, "II", "DETALLE DEL CHECKLIST");
                    col.Item().Height(5);

                    foreach (var r in rubros)
                    {
                        col.Item().Background(C_PRIMARY).PaddingVertical(5).PaddingHorizontal(8).Row(rrow =>
                        {
                            rrow.RelativeItem().AlignMiddle()
                                .Text(r.Rubro.Nombre).Bold().FontSize(9f).FontColor("#FFFFFF");
                            rrow.ConstantItem(88).AlignMiddle().AlignRight()
                                .Text($"{r.PtsObtenidosRubro} / {r.PtsMaximoRubro} pts")
                                .Bold().FontSize(8.5f).FontColor(C_PRIMARY_LT);
                        });
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.ConstantColumn(26); // N
                                cols.RelativeColumn();   // Descripcion
                                cols.ConstantColumn(50); // Puntaje
                            });
                            table.Header(header =>
                            {
                                TableHdr(header.Cell(), "N", center: true, small: true);
                                TableHdr(header.Cell(), "Item evaluado", small: true);
                                TableHdr(header.Cell(), "Ptje.", center: true, small: true);
                            });
                            int idx = 1;
                            foreach (var det in r.Items)
                            {
                                string bg = idx % 2 == 0 ? C_ROW_ALT : "#FFFFFF";
                                string pc = det.Puntaje == 4 ? C_PRIMARY_MID : det.Puntaje == 2 ? C_WARN_TEXT : C_DANGER_TEXT;

                                table.Cell().Background(bg).BorderBottom(0.4f).BorderColor(C_BORDER)
                                     .PaddingVertical(5).PaddingHorizontal(4).AlignMiddle().AlignCenter()
                                     .Text($"{idx}").FontSize(8.5f).FontColor(C_TXT_MUTED);

                                table.Cell().Background(bg).BorderBottom(0.4f).BorderColor(C_BORDER)
                                     .PaddingVertical(5).PaddingHorizontal(7).Column(c =>
                                {
                                    c.Item().Text(det.Descripcion).FontSize(9f);
                                    if (!string.IsNullOrEmpty(det.Hallazgo))
                                    {
                                        c.Item().Height(2);
                                        c.Item().Text($"> {det.Hallazgo}").FontSize(8f).FontColor(C_DANGER_TEXT).Italic();
                                    }
                                });

                                table.Cell().Background(bg).BorderBottom(0.4f).BorderColor(C_BORDER)
                                     .PaddingVertical(5).AlignMiddle().AlignCenter()
                                     .Text($"{det.Puntaje} / {det.PtsMax}").Bold().FontSize(9f).FontColor(pc);
                                idx++;
                            }
                        });
                        col.Item().Height(5);
                    }

                    // ── III. INFORME DE HALLAZGOS ────────────────────────────────
                    if (secHall != null)
                    {
                        col.Item().Height(10);
                        SeccionTitulo(col, secHall, "INFORME DE HALLAZGOS");
                        col.Item().Height(3);
                        col.Item().Text("Se realizo la inspeccion, encontrandose los siguientes hallazgos:")
                                  .FontSize(9f).Italic().FontColor(C_TXT_MUTED);
                        col.Item().Height(5);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.ConstantColumn(26);
                                cols.RelativeColumn(4);
                                cols.RelativeColumn(3);
                            });
                            table.Header(header =>
                            {
                                TableHdr(header.Cell(), "N", center: true);
                                TableHdr(header.Cell(), "HALLAZGO / ACCION CORRECTIVA");
                                TableHdr(header.Cell(), "EVIDENCIA FOTOGRAFICA", center: true);
                            });
                            foreach (var h in hallazgos)
                            {
                                table.Cell().BorderBottom(0.4f).BorderColor(C_BORDER)
                                     .PaddingVertical(5).PaddingHorizontal(4).AlignMiddle().AlignCenter()
                                     .Text($"{h.Correlativo}").Bold().FontSize(10.5f).FontColor(C_PRIMARY);

                                table.Cell().BorderBottom(0.4f).BorderColor(C_BORDER)
                                     .PaddingVertical(5).PaddingHorizontal(7)
                                     .MinHeight(IMG_H).AlignMiddle().Column(c =>
                                {
                                    c.Item().Text(h.Descripcion).FontSize(9f);
                                    if (!string.IsNullOrWhiteSpace(h.AccionCorr))
                                    {
                                        c.Item().Height(3);
                                        c.Item().Text("Accion: " + h.AccionCorr).FontSize(8.5f).FontColor(C_PRIMARY_MID).Italic();
                                    }
                                });

                                var fH = h.Imgs.Where(i => i.Tipo == "H" && !string.IsNullOrEmpty(i.RutaFisica) && File.Exists(i.RutaFisica)).ToList();
                                var cF = table.Cell().BorderBottom(0.4f).BorderColor(C_BORDER).Padding(4).MinHeight(IMG_H);
                                if (fH.Count == 0)
                                    cF.AlignMiddle().AlignCenter().Text("Sin imagen").FontSize(8f).FontColor(C_TXT_MUTED);
                                else if (fH.Count == 1)
                                    cF.MaxHeight(IMG_H).Image(fH[0].RutaFisica!).FitArea();
                                else
                                    cF.Row(ir => { foreach (var f in fH) { ir.ConstantItem(IMG_TH).MaxHeight(IMG_H).Image(f.RutaFisica!).FitArea(); ir.ConstantItem(3); } });
                            }
                        });
                    }

                    // ── IV. SEGUIMIENTO ──────────────────────────────────────────
                    if (secSeg != null)
                    {
                        col.Item().Height(12);
                        SeccionTitulo(col, secSeg, "SEGUIMIENTO DE ACCIONES CORRECTIVAS");
                        col.Item().Height(4);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.ConstantColumn(26);
                                cols.RelativeColumn(4);
                                cols.RelativeColumn(3);
                            });
                            table.Header(header =>
                            {
                                TableHdr(header.Cell(), "N", center: true);
                                TableHdr(header.Cell(), "OBSERVACION DE SEGUIMIENTO");
                                TableHdr(header.Cell(), "EVIDENCIA", center: true);
                            });
                            foreach (var h in seg)
                            {
                                table.Cell().BorderBottom(0.4f).BorderColor(C_BORDER)
                                     .PaddingVertical(5).PaddingHorizontal(4).AlignMiddle().AlignCenter()
                                     .Text($"{h.Correlativo}").Bold().FontSize(10.5f).FontColor(C_PRIMARY);

                                table.Cell().BorderBottom(0.4f).BorderColor(C_BORDER)
                                     .PaddingVertical(5).PaddingHorizontal(7)
                                     .MinHeight(IMG_H).AlignMiddle().Column(c =>
                                {
                                    c.Item().Text(h.ObsSeguim ?? "---").FontSize(9f);
                                    string badge = h.Estado == "R" ? "Resuelto" : h.Estado == "V" ? "Verificado" : "Pendiente";
                                    string bc    = h.Estado == "R" ? C_PRIMARY_MID : h.Estado == "V" ? "#1D4ED8" : C_WARN_TEXT;
                                    c.Item().PaddingTop(3).Text(badge).Bold().FontSize(8.5f).FontColor(bc);
                                });

                                var fS = h.Imgs.Where(i => i.Tipo == "S" && !string.IsNullOrEmpty(i.RutaFisica) && File.Exists(i.RutaFisica)).ToList();
                                var cS = table.Cell().BorderBottom(0.4f).BorderColor(C_BORDER).Padding(4).MinHeight(IMG_H);
                                if (fS.Count == 0)
                                    cS.AlignMiddle().AlignCenter().Text("Sin imagen").FontSize(8f).FontColor(C_TXT_MUTED);
                                else if (fS.Count == 1)
                                    cS.MaxHeight(IMG_H).Image(fS[0].RutaFisica!).FitArea();
                                else
                                    cS.Row(ir => { foreach (var f in fS) { ir.ConstantItem(IMG_TH).MaxHeight(IMG_H).Image(f.RutaFisica!).FitArea(); ir.ConstantItem(3); } });
                            }
                        });
                    }

                    // ── V. EVIDENCIAS FOTOGRAFICAS ───────────────────────────────
                    if (secEvi != null)
                    {
                        col.Item().Height(12);
                        SeccionTitulo(col, secEvi, "EVIDENCIAS FOTOGRAFICAS");
                        col.Item().Height(6);
                        foreach (var chunk in evidencias.Chunk(3))
                        {
                            col.Item().Row(row =>
                            {
                                foreach (var ev in chunk)
                                {
                                    row.RelativeItem().Border(0.5f).BorderColor(C_BORDER).Column(cell =>
                                    {
                                        if (!string.IsNullOrEmpty(ev.RutaFisica) && File.Exists(ev.RutaFisica))
                                            cell.Item().MaxHeight(96).Image(ev.RutaFisica).FitArea();
                                        else
                                            cell.Item().Height(96).Background("#E9ECEF")
                                                .AlignMiddle().AlignCenter()
                                                .Text("Sin imagen").FontSize(8f).FontColor(C_TXT_MUTED);
                                        if (!string.IsNullOrEmpty(ev.Descripcion))
                                            cell.Item().Background(C_ROW_ALT).PaddingVertical(2).PaddingHorizontal(4)
                                                .AlignCenter().Text(ev.Descripcion).FontSize(7.5f).FontColor(C_TXT_MUTED).Italic();
                                    });
                                    row.ConstantItem(6);
                                }
                                for (int i = chunk.Length; i < 3; i++) row.RelativeItem();
                            });
                            col.Item().Height(6);
                        }
                    }

                    // ── VI. ACCIONES CORRECTIVAS ─────────────────────────────────
                    if (secAcc != null)
                    {
                        col.Item().Height(12);
                        SeccionTitulo(col, secAcc, "ACCIONES CORRECTIVAS");
                        col.Item().Height(4);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(6);
                                cols.RelativeColumn(3);
                                cols.ConstantColumn(67);
                                cols.ConstantColumn(62);
                            });
                            table.Header(header =>
                            {
                                TableHdr(header.Cell(), "ACCION CORRECTIVA");
                                TableHdr(header.Cell(), "RESPONSABLE");
                                TableHdr(header.Cell(), "PLAZO", center: true);
                                TableHdr(header.Cell(), "ESTADO", center: true);
                            });
                            int ri = 0;
                            foreach (var ac in acciones)
                            {
                                string bg = ri++ % 2 == 0 ? "#FFFFFF" : C_ROW_ALT;
                                TableCell(table.Cell(), bg, ac.Descripcion);
                                TableCell(table.Cell(), bg, ac.Responsable ?? "---");
                                TableCell(table.Cell(), bg, ac.FchLimite?.ToString("dd/MM/yyyy") ?? "---", center: true);
                                string el = ac.Estado switch { "P" => "Pendiente", "E" => "En proceso", "R" => "Resuelta", _ => ac.Estado };
                                string ec = ac.Estado switch { "R" => C_PRIMARY_MID, "E" => "#1D4ED8", _ => ac.EsVencida ? C_DANGER_TEXT : C_TXT_MUTED };
                                var estTxt = table.Cell().Background(bg).BorderBottom(0.4f).BorderColor(C_BORDER)
                                                 .PaddingVertical(5).PaddingHorizontal(6).AlignMiddle()
                                                 .Text(el).Bold().FontSize(8.5f).FontColor(ec);
                                estTxt.AlignCenter();
                            }
                        });
                    }

                    // ── FIRMAS ────────────────────────────────────────────────────
                    col.Item().Height(30);
                    col.Item().Row(row =>
                    {
                        FirmaBloque(row.RelativeItem(), insp.Inspector ?? "---", "Inspector SSO");
                        row.ConstantItem(90);
                        FirmaBloque(row.RelativeItem(), insp.Medico ?? "---", "Medico SSO");
                    });
                    col.Item().Height(18);
                    col.Item().LineHorizontal(0.5f).LineColor(C_BORDER);
                    col.Item().Height(4);
                    col.Item().AlignCenter().Text(txt =>
                    {
                        txt.Span("La Colonial - Salud Ocupacional  |  ").FontSize(7.5f).FontColor(C_TXT_MUTED);
                        txt.Span($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(7.5f).FontColor(C_TXT_MUTED);
                    });
                });
            });
        }).GeneratePdf();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static void SeccionTitulo(ColumnDescriptor col, string numero, string titulo)
    {
        col.Item().PaddingBottom(4).BorderBottom(1.5f).BorderColor(C_PRIMARY).Text(txt =>
        {
            txt.Span($"{numero}.  ").Bold().FontSize(10.5f).FontColor(C_PRIMARY);
            txt.Span(titulo).Bold().FontSize(10.5f).FontColor(C_PRIMARY);
        });
    }

    private static void TableHdr(IContainer cell, string text, bool center = false, bool small = false)
    {
        float fs = small ? 8f : 8.5f;
        var   b  = cell.Background(C_PRIMARY).PaddingVertical(6).PaddingHorizontal(6);
        var   t  = b.Text(text).Bold().FontSize(fs).FontColor("#FFFFFF");
        if (center) t.AlignCenter();
    }

    private static void TableCell(IContainer cell, string bg, string text, bool center = false)
    {
        var t = cell.Background(bg).BorderBottom(0.4f).BorderColor(C_BORDER)
                    .PaddingVertical(5).PaddingHorizontal(6).Text(text).FontSize(9f);
        if (center) t.AlignCenter();
    }

    private static void FilaDato(ColumnDescriptor col, string label, string? value)
    {
        col.Item().Row(r =>
        {
            r.ConstantItem(82).Text(label + ":").Bold().FontSize(8.5f).FontColor(C_TXT_MUTED);
            r.RelativeItem().Text(value ?? "---").FontSize(9f);
        });
        col.Item().Height(2);
    }

    private static void FirmaBloque(IContainer container, string nombre, string rol)
    {
        container.Column(c =>
        {
            c.Item().Height(32).BorderBottom(1f).BorderColor("#888888");
            c.Item().Height(3);
            c.Item().AlignCenter().Text(nombre).Bold().FontSize(9f);
            c.Item().AlignCenter().Text(rol).FontSize(8f).FontColor(C_TXT_MUTED);
        });
    }

    private static string Roman(int n) => n switch
    {
        1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V",
        6 => "VI", 7 => "VII", 8 => "VIII", _ => n.ToString()
    };
}
