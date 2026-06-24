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
    private static readonly string C_TEAL        = "#0f5132";
    private static readonly string C_TEAL_MID    = "#198754";
    private static readonly string C_TEAL_LIGHT  = "#d1e7dd";
    private static readonly string C_WARN_BG     = "#fff3cd";
    private static readonly string C_WARN_BORD   = "#664d03";
    private static readonly string C_DANGER_BG   = "#f8d7da";
    private static readonly string C_DANGER_BORD = "#842029";
    private static readonly string C_GRAY_HDR    = "#f8f9fa";
    private static readonly string C_GRAY_BORD   = "#dee2e6";

    public byte[] Generar(SoDetalleInspeccionViewModel datos, string logoPath)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var insp     = datos.Inspeccion;
        var rubros   = datos.Rubros;
        var acciones = datos.Acciones;
        const float IMG_HEIGHT = 75f;
        const float IMG_THUMB  = 70f;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(28, Unit.Point);
                page.MarginTop(20, Unit.Point);
                page.MarginBottom(20, Unit.Point);
                page.DefaultTextStyle(x => x.FontSize(8.5f).FontFamily(Fonts.Arial));
                page.Header().Height(0);
                page.Footer().Height(0);

                page.Content().Column(col =>
                {
                    col.Item().ShowOnce().Column(hdr =>
                    {
                        hdr.Item().Row(row =>
                        {
                            row.ConstantItem(90).AlignMiddle().Column(c =>
                            {
                                if (File.Exists(logoPath))
                                    c.Item().MaxHeight(55).Image(logoPath).FitArea();
                                else
                                    c.Item().Text("La Colonial\nFABRICA DE HILOS S.A.").Bold().FontSize(9).LineHeight(1.3f);
                            });
                            row.RelativeItem().AlignMiddle().AlignCenter().Column(c =>
                            {
                                c.Item().Text("INFORME DE INSPECCION").Bold().FontSize(13).FontColor(C_TEAL);
                                c.Item().Text("COMEDOR Y COCINA").Bold().FontSize(11).FontColor(C_TEAL);
                            });
                            row.ConstantItem(90).AlignMiddle().AlignRight().Column(c =>
                            {
                                c.Item().Text($"Fecha: {insp.FechaInsp:dd/MM/yyyy}").FontSize(8).FontColor("#555");
                                if (!string.IsNullOrEmpty(insp.HoraInsp))
                                    c.Item().Text($"Hora: {insp.HoraInsp}").FontSize(8).FontColor("#555");
                            });
                        });
                        hdr.Item().Height(4);
                        hdr.Item().LineHorizontal(1.5f).LineColor(C_TEAL);
                        hdr.Item().Height(8);
                    });

                    col.Item().Border(0.5f).BorderColor(C_GRAY_BORD).Background(C_GRAY_HDR).Padding(8).Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            FilaDato(left, "Comedor",       insp.NombreComedor);
                            FilaDato(left, "Concesionaria", insp.NombreConc ?? "---");
                            FilaDato(left, "Encargada",     insp.ContactoConc ?? insp.Encargada ?? "---");
                        });
                        row.ConstantItem(10);
                        row.RelativeItem().Column(right =>
                        {
                            FilaDato(right, "Inspector",  insp.Inspector ?? "---");
                            FilaDato(right, "Medico SSO", insp.Medico    ?? "---");
                            FilaDato(right, "Registrado", insp.UsrCrea   ?? "---");
                        });
                    });

                    col.Item().Height(8);

                    string calColor = insp.Calificacion switch { "ACEPTABLE" => C_TEAL_MID, "CON OBSERVACION" => C_WARN_BORD, _ => C_DANGER_BORD };
                    string calBg    = insp.Calificacion switch { "ACEPTABLE" => C_TEAL_LIGHT, "CON OBSERVACION" => C_WARN_BG, _ => C_DANGER_BG };

                    col.Item().Background(calBg).Border(0.5f).BorderColor(C_GRAY_BORD).Padding(8).Row(row =>
                    {
                        row.RelativeItem().Column(c => { c.Item().Text("RESULTADO GLOBAL").Bold().FontSize(9).FontColor(C_TEAL); });
                        row.RelativeItem().AlignCenter().Column(c => { c.Item().AlignCenter().Text($"{insp.PtsObtenidos:0} / {insp.PtsMaximo:0} pts").Bold().FontSize(10).FontColor(calColor); });
                        row.RelativeItem().AlignRight().Column(c => { c.Item().AlignRight().Text($"{insp.PctCumpl:0.0}% -- {insp.Calificacion ?? "---"}").Bold().FontSize(10).FontColor(calColor); });
                    });

                    col.Item().Height(10);
                    col.Item().Text("I. RESUMEN POR RUBRO").Bold().FontSize(9.5f).FontColor(C_TEAL);
                    col.Item().Height(3);

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols => { cols.RelativeColumn(8); cols.RelativeColumn(2); cols.RelativeColumn(2); cols.RelativeColumn(3); });
                        table.Header(header => { ThCell(header.Cell(), "Rubro"); ThCell(header.Cell(), "Pts"); ThCell(header.Cell(), "%"); ThCell(header.Cell(), "Nivel"); });
                        foreach (var r in rubros)
                        {
                            double pct = r.PtsMaximoRubro > 0 ? r.PtsObtenidosRubro * 100.0 / r.PtsMaximoRubro : 0;
                            string nTxt   = pct >= 75 ? "Aceptable" : pct >= 51 ? "Con Observacion" : "No Aceptable";
                            string nColor = pct >= 75 ? C_TEAL_MID : pct >= 51 ? C_WARN_BORD : C_DANGER_BORD;
                            TdCell(table.Cell(), r.Rubro.Nombre);
                            TdCell(table.Cell(), $"{r.PtsObtenidosRubro}/{r.PtsMaximoRubro}", center: true);
                            TdCell(table.Cell(), $"{pct:0.0}%", center: true);
                            table.Cell().BorderBottom(0.4f).BorderColor(C_GRAY_BORD).PaddingVertical(3).PaddingHorizontal(4).Text(nTxt).FontSize(7.5f).FontColor(nColor).Bold();
                        }
                    });

                    col.Item().Height(12);
                    col.Item().Text("II. DETALLE DEL CHECKLIST").Bold().FontSize(9.5f).FontColor(C_TEAL);
                    col.Item().Height(3);

                    foreach (var r in rubros)
                    {
                        col.Item().Background(C_TEAL).Padding(4).Row(row =>
                        {
                            row.RelativeItem().Text(r.Rubro.Nombre).Bold().FontSize(8.5f).FontColor("#FFFFFF");
                            row.ConstantItem(70).AlignRight().Text($"{r.PtsObtenidosRubro}/{r.PtsMaximoRubro} pts").Bold().FontSize(8).FontColor("#d1e7dd");
                        });
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols => { cols.RelativeColumn(1); cols.RelativeColumn(10); cols.ConstantColumn(35); });
                            int idx = 1;
                            foreach (var det in r.Items)
                            {
                                string bg = idx % 2 == 0 ? "#f8f9fa" : "#ffffff";
                                table.Cell().Background(bg).BorderBottom(0.4f).BorderColor(C_GRAY_BORD).PaddingVertical(3).PaddingHorizontal(4).Text($"{idx}").FontSize(7.5f).FontColor("#888");
                                table.Cell().Background(bg).BorderBottom(0.4f).BorderColor(C_GRAY_BORD).PaddingVertical(3).PaddingHorizontal(4).Column(c =>
                                {
                                    c.Item().Text(det.Descripcion).FontSize(8);
                                    if (!string.IsNullOrEmpty(det.Hallazgo)) { c.Item().Height(1); c.Item().Text($"! {det.Hallazgo}").FontSize(7.5f).FontColor(C_DANGER_BORD).Italic(); }
                                });
                                string pc = det.Puntaje == 4 ? C_TEAL_MID : det.Puntaje == 2 ? C_WARN_BORD : C_DANGER_BORD;
                                table.Cell().Background(bg).BorderBottom(0.4f).BorderColor(C_GRAY_BORD).AlignMiddle().AlignCenter().PaddingVertical(3).Column(c =>
                                {
                                    c.Item().AlignCenter().Text($"{det.Puntaje}").Bold().FontSize(9).FontColor(pc);
                                    c.Item().AlignCenter().Text($"/ {det.PtsMax}").FontSize(7).FontColor("#aaa");
                                });
                                idx++;
                            }
                        });
                        col.Item().Height(4);
                    }

                    var hallazgos = datos.Hallazgos.OrderBy(h => h.Correlativo).ToList();
                    if (hallazgos.Any())
                    {
                        col.Item().Height(10);
                        col.Item().Text("III. INFORME DE HALLAZGOS").Bold().FontSize(9.5f).FontColor(C_TEAL);
                        col.Item().Height(3);
                        col.Item().Text("Se realizo la inspeccion donde se encontraron los siguientes hallazgos:").FontSize(8.5f).Italic().FontColor("#555");
                        col.Item().Height(5);

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols => { cols.ConstantColumn(22); cols.RelativeColumn(4); cols.RelativeColumn(3); });
                            table.Header(header => { ThCell(header.Cell(), "N"); ThCell(header.Cell(), "HALLAZGO / ACCION CORRECTIVA"); ThCell(header.Cell(), "EVIDENCIA FOTOGRAFICA"); });
                            foreach (var h in hallazgos)
                            {
                                table.Cell().BorderBottom(0.4f).BorderColor(C_GRAY_BORD).PaddingVertical(4).PaddingHorizontal(4).AlignMiddle().Text($"{h.Correlativo}").Bold().FontSize(9).FontColor(C_TEAL);
                                table.Cell().BorderBottom(0.4f).BorderColor(C_GRAY_BORD).PaddingVertical(4).PaddingHorizontal(5).MinHeight(IMG_HEIGHT).AlignMiddle().Column(c =>
                                {
                                    c.Item().Text(h.Descripcion).FontSize(8);
                                    if (!string.IsNullOrWhiteSpace(h.AccionCorr)) { c.Item().Height(3); c.Item().Text("Accion: " + h.AccionCorr).FontSize(7.5f).FontColor(C_TEAL_MID).Italic(); }
                                });
                                var fH = h.Imgs.Where(i => i.Tipo == "H" && !string.IsNullOrEmpty(i.RutaFisica) && File.Exists(i.RutaFisica)).ToList();
                                var cF = table.Cell().BorderBottom(0.4f).BorderColor(C_GRAY_BORD).Padding(3).MinHeight(IMG_HEIGHT);
                                if (fH.Count == 0) cF.AlignMiddle().AlignCenter().Text("Sin imagen").FontSize(7).FontColor("#aaa");
                                else if (fH.Count == 1) cF.MaxHeight(IMG_HEIGHT).Image(fH[0].RutaFisica!).FitWidth();
                                else cF.Row(ir => { foreach (var f in fH) { ir.ConstantItem(IMG_THUMB).MaxHeight(IMG_HEIGHT).Image(f.RutaFisica!).FitWidth(); ir.ConstantItem(3); } });
                            }
                        });

                        var seg = hallazgos.Where(h => !string.IsNullOrWhiteSpace(h.ObsSeguim) || h.Imgs.Any(i => i.Tipo == "S" && !string.IsNullOrEmpty(i.RutaFisica) && File.Exists(i.RutaFisica))).ToList();
                        if (seg.Any())
                        {
                            col.Item().Height(10);
                            col.Item().Text("IV. SEGUIMIENTO DE ACCIONES CORRECTIVAS").Bold().FontSize(9.5f).FontColor(C_TEAL);
                            col.Item().Height(3);
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cols => { cols.ConstantColumn(22); cols.RelativeColumn(4); cols.RelativeColumn(3); });
                                table.Header(header => { ThCell(header.Cell(), "N"); ThCell(header.Cell(), "OBSERVACION DE SEGUIMIENTO"); ThCell(header.Cell(), "EVIDENCIA"); });
                                foreach (var h in seg)
                                {
                                    table.Cell().BorderBottom(0.4f).BorderColor(C_GRAY_BORD).PaddingVertical(4).PaddingHorizontal(4).AlignMiddle().Text($"{h.Correlativo}").Bold().FontSize(9).FontColor(C_TEAL);
                                    table.Cell().BorderBottom(0.4f).BorderColor(C_GRAY_BORD).PaddingVertical(4).PaddingHorizontal(5).MinHeight(IMG_HEIGHT).AlignMiddle().Column(c =>
                                    {
                                        c.Item().Text(h.ObsSeguim ?? "---").FontSize(8);
                                        string badge = h.Estado == "R" ? "Resuelto" : h.Estado == "V" ? "Verificado" : "Pendiente";
                                        string bc    = h.Estado == "R" ? C_TEAL_MID : h.Estado == "V" ? "#084298" : "#664d03";
                                        c.Item().PaddingTop(2).Text(badge).FontSize(7).FontColor(bc).Bold();
                                    });
                                    var fS = h.Imgs.Where(i => i.Tipo == "S" && !string.IsNullOrEmpty(i.RutaFisica) && File.Exists(i.RutaFisica)).ToList();
                                    var cS = table.Cell().BorderBottom(0.4f).BorderColor(C_GRAY_BORD).Padding(3).MinHeight(IMG_HEIGHT);
                                    if (fS.Count == 0) cS.AlignMiddle().AlignCenter().Text("Sin imagen").FontSize(7).FontColor("#aaa");
                                    else if (fS.Count == 1) cS.MaxHeight(IMG_HEIGHT).Image(fS[0].RutaFisica!).FitWidth();
                                    else cS.Row(ir => { foreach (var f in fS) { ir.ConstantItem(IMG_THUMB).MaxHeight(IMG_HEIGHT).Image(f.RutaFisica!).FitWidth(); ir.ConstantItem(3); } });
                                }
                            });
                        }
                    }

                    var evidencias = datos.Evidencias.Where(e => !string.IsNullOrEmpty(e.RutaArch)).ToList();
                    if (evidencias.Any())
                    {
                        col.Item().Height(8);
                        col.Item().Text("V. EVIDENCIAS FOTOGRAFICAS").Bold().FontSize(9.5f).FontColor(C_TEAL);
                        col.Item().Height(5);
                        foreach (var chunk in evidencias.Chunk(3))
                        {
                            col.Item().Row(row =>
                            {
                                foreach (var ev in chunk)
                                {
                                    row.RelativeItem().Column(cell =>
                                    {
                                        if (!string.IsNullOrEmpty(ev.RutaFisica) && File.Exists(ev.RutaFisica)) cell.Item().MaxHeight(90).Image(ev.RutaFisica).FitArea();
                                        else cell.Item().Height(90).Background("#e9ecef").AlignMiddle().AlignCenter().Text("Sin imagen").FontSize(7).FontColor("#888");
                                        if (!string.IsNullOrEmpty(ev.Descripcion)) cell.Item().AlignCenter().Text(ev.Descripcion).FontSize(7).FontColor("#555").Italic();
                                    });
                                    row.ConstantItem(6);
                                }
                                for (int i = chunk.Length; i < 3; i++) row.RelativeItem();
                            });
                            col.Item().Height(6);
                        }
                    }

                    if (acciones.Any())
                    {
                        col.Item().Height(8);
                        string seccion = evidencias.Any() ? "VI" : "V";
                        col.Item().Text($"{seccion}. ACCIONES CORRECTIVAS").Bold().FontSize(9.5f).FontColor(C_TEAL);
                        col.Item().Height(3);
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols => { cols.RelativeColumn(7); cols.RelativeColumn(3); cols.ConstantColumn(60); cols.ConstantColumn(55); });
                            table.Header(header => { ThCell(header.Cell(), "Accion correctiva"); ThCell(header.Cell(), "Responsable"); ThCell(header.Cell(), "Plazo"); ThCell(header.Cell(), "Estado"); });
                            foreach (var ac in acciones)
                            {
                                TdCell(table.Cell(), ac.Descripcion);
                                TdCell(table.Cell(), ac.Responsable ?? "---");
                                TdCell(table.Cell(), ac.FchLimite?.ToString("dd/MM/yyyy") ?? "---", center: true);
                                string el = ac.Estado switch { "P" => "Pendiente", "E" => "En proceso", "R" => "Resuelta", _ => ac.Estado };
                                string ec = ac.Estado switch { "R" => C_TEAL_MID, "E" => "#084298", _ => ac.EsVencida ? C_DANGER_BORD : "#555" };
                                table.Cell().BorderBottom(0.4f).BorderColor(C_GRAY_BORD).PaddingVertical(3).PaddingHorizontal(4).Text(el).FontSize(7.5f).FontColor(ec).Bold();
                            }
                        });
                    }

                    col.Item().Height(24);
                    col.Item().Row(row => { FirmaBloque(row.RelativeItem(), insp.Inspector ?? "---", "Inspector"); row.ConstantItem(80); FirmaBloque(row.RelativeItem(), insp.Medico ?? "---", "Medico SSO"); });
                    col.Item().Height(12);
                    col.Item().LineHorizontal(0.5f).LineColor(C_GRAY_BORD);
                    col.Item().Height(4);
                    col.Item().AlignCenter().Text(txt =>
                    {
                        txt.Span("La Colonial -- Salud Ocupacional | ").FontSize(7).FontColor("#888");
                        txt.Span($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}").FontSize(7).FontColor("#888");
                    });
                });
            });
        }).GeneratePdf();
    }

    private static void ThCell(IContainer cell, string text) => cell.Background(C_TEAL).PaddingVertical(4).PaddingHorizontal(5).Text(text).Bold().FontSize(8).FontColor("#FFFFFF");
    private static void TdCell(IContainer cell, string text, bool center = false)
    {
        var t = cell.BorderBottom(0.4f).BorderColor(C_GRAY_BORD).PaddingVertical(3).PaddingHorizontal(4);
        var txt = t.Text(text).FontSize(8);
        if (center) txt.AlignCenter();
    }
    private static void FilaDato(ColumnDescriptor col, string label, string? value)
    {
        col.Item().Row(r => { r.ConstantItem(70).Text(label + ":").Bold().FontSize(8).FontColor("#555"); r.RelativeItem().Text(value ?? "---").FontSize(8); });
        col.Item().Height(2);
    }
    private static void FirmaBloque(IContainer container, string nombre, string rol)
    {
        container.Column(c => { c.Item().Height(28).BorderBottom(0.8f).BorderColor("#555"); c.Item().Height(2); c.Item().AlignCenter().Text(nombre).Bold().FontSize(7.5f); c.Item().AlignCenter().Text(rol).FontSize(7).FontColor("#777"); });
    }
}