using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using FabricaHilos.Models.Sgc;

namespace FabricaHilos.Services.Sgc
{
    public interface ISalidaInternaPdfService
    {
        byte[] Generar(SalidaInternaDto datos, string rucEmpresa, string logoPath);
    }

    public class SalidaInternaPdfService : ISalidaInternaPdfService
    {
        public byte[] Generar(SalidaInternaDto datos, string rucEmpresa, string logoPath)
        {
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.MarginHorizontal(25, Unit.Point);
                    page.MarginVertical(20, Unit.Point);
                    page.DefaultTextStyle(x => x.FontSize(8).FontFamily(Fonts.Arial));

                    page.Content().Column(col =>
                    {
                        // ── ENCABEZADO ────────────────────────────────────────────
                        col.Item().Row(row =>
                        {
                            // Logo + nombre empresa
                            row.RelativeItem(4).Padding(6).Row(logoRow =>
                               {
                                   if (File.Exists(logoPath))
                                   {
                                       logoRow.RelativeItem(3).MaxHeight(60).Image(logoPath).FitArea();
                                       logoRow.ConstantItem(6);
                                   }
                                   logoRow.RelativeItem(2).AlignMiddle().Column(c =>
                                   {
                                       c.Item().Text("La Colonial").Bold().FontSize(11);
                                       c.Item().Text("FABRICA DE HILOS S.A.").FontSize(7.5f);
                                   });
                               });

                            // Datos empresa + número
                            row.RelativeItem(6).Padding(8).Column(info =>
                            {
                                info.Item().AlignCenter()
                                    .Text($"RUC N° {rucEmpresa}").Bold().FontSize(10);
                                info.Item().AlignCenter()
                                    .Text("SALIDA INTERNA").Bold().FontSize(13);
                                info.Item().Height(6);
                                info.Item().AlignCenter()
                                    .Text($"{datos.Serie:D3}-{datos.Numero:D8}").Bold().FontSize(12);
                            });
                        });

                        col.Item().Height(4);

                        // ── CUERPO: datos en dos columnas ────────────────────────
                        col.Item().Row(row =>
                        {
                            // Columna izquierda
                            row.RelativeItem().Padding(5).Column(left =>
                               {
                                   Fila(left, "Razón Social",    datos.Nombre);
                                   Fila(left, "Fecha emisión",   datos.FchTransac?.ToString("dd/MM/yyyy"));
                                   Fila(left, "Partida",         datos.DirPartida);
                                   Fila(left, "Destinatario",    datos.Nombre);
                                   Fila(left, "Mod. de Traslado", datos.ModTraslado);
                                   Fila(left, "Transportista",
                                       string.IsNullOrWhiteSpace(datos.NomTranspor) ? null
                                       : $"{datos.NomTranspor} {datos.NroTranspor}".Trim());
                                   Fila(left, "Vehículo",        datos.NomVehiculo);

                                   left.Item().Height(2);
                                   left.Item().Row(r =>
                                   {
                                       r.RelativeItem().Text(t =>
                                       {
                                           t.Span("Peso: ").Bold();
                                           t.Span($"{datos.PesoTotal:N3}   KG.");
                                       });
                                       r.RelativeItem().Text(t =>
                                       {
                                           t.Span("Nro Bultos: ").Bold();
                                           t.Span(datos.NroBultos?.ToString() ?? "-");
                                       });
                                   });
                               });

                            // Columna derecha
                            row.RelativeItem().Padding(5).Column(right =>
                            {
                                right.Item().Row(r =>
                                {
                                    r.RelativeItem().Text(t =>
                                    {
                                        t.Span("Fecha Traslado: ").Bold();
                                        t.Span(datos.FchTransac?.ToString("dd/MM/yyyy") ?? "-");
                                    });
                                    r.RelativeItem().Text(t =>
                                    {
                                        t.Span("Fecha de Entrega: ").Bold();
                                        t.Span(datos.FchEntrega?.ToString("dd/MM/yyyy") ?? "-");
                                    });
                                });
                                right.Item().Height(2);
                                Fila(right, "Punto de llegada",   datos.DirLlegada);
                                Fila(right, "Ruc Destinatario",   datos.Ruc);
                                Fila(right, "Motivo Traslado",    datos.Motivo);
                                Fila(right, "Comprobante",        BuildComprobante(datos));
                                Fila(right, "Observación",        datos.Glosa);
                            });
                        });

                        col.Item().Height(6);

                        // ── TABLA DE ARTÍCULOS ────────────────────────────────────
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(3);   // COD_ART
                                cols.RelativeColumn(8);   // DESCRIPCION
                                cols.RelativeColumn(2);   // UNID
                                cols.RelativeColumn(2);   // CANTIDAD
                            });

                            // Cabecera
                            table.Header(header =>
                            {
                                HeaderCell(header, "ARTÍCULOS");
                                HeaderCell(header, "DESCRIPCIÓN");
                                HeaderCell(header, "UNID");
                                HeaderCell(header, "CANTIDAD");
                            });

                            // Filas
                            foreach (var item in datos.Items)
                            {
                                DataCell(table, item.CodArt ?? string.Empty, false);
                                DataCell(table, item.Descripcion ?? string.Empty, false);
                                DataCell(table, item.Unidad ?? string.Empty, true);
                                DataCell(table, item.Cantidad?.ToString("N2") ?? string.Empty, true);
                            }
                        });
                    });
                });
            }).GeneratePdf();
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static void Fila(ColumnDescriptor col, string label, string? valor)
        {
            col.Item().Row(r =>
            {
                r.ConstantItem(90).Text(t => t.Span($"{label}:").Bold());
                r.RelativeItem().Text(valor ?? "-");
            });
            col.Item().Height(1);
        }

        private static void HeaderCell(TableCellDescriptor header, string text)
        {
            header.Cell().Background(Colors.Grey.Darken3).Padding(4)
                  .Text(text).FontColor(Colors.White).Bold().FontSize(7.5f);
        }

        private static void DataCell(TableDescriptor table, string text, bool center)
        {
            var cell = table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                            .PaddingVertical(3).PaddingHorizontal(4);
            if (center)
                cell.AlignCenter().Text(text).FontSize(7.5f);
            else
                cell.Text(text).FontSize(7.5f);
        }

        private static string BuildComprobante(SalidaInternaDto d)
        {
            var ref1 = string.Join("-",
                new[] { d.TipRef, d.SerRef, d.NroRef }
                .Where(x => !string.IsNullOrWhiteSpace(x)));

            var fecha = d.FchTransac?.ToString("dd/MM/yyyy") ?? string.Empty;
            var ped   = string.IsNullOrWhiteSpace(d.NroDocRef) ? string.Empty : $" O/P. {d.NroDocRef.Trim()}";

            var partes = new List<string>();
            if (!string.IsNullOrEmpty(ref1))  partes.Add(ref1);
            if (!string.IsNullOrEmpty(fecha)) partes.Add(fecha);
            var result = string.Join(" - ", partes) + ped;
            return string.IsNullOrWhiteSpace(result) ? "-" : result;
        }
    }
}
