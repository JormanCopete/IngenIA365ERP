using System.Globalization;
using IngenIA365ERP.Application.Accounting.Documents;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports;

/// <summary>
/// Impresión de un comprobante contable (feature 009, US3; <c>GET /api/accounting/documents/{id}/print</c>).
/// Reescrito sobre <see cref="ComprobanteDto"/>: origen (módulo y documento), quién registró y
/// quién contabilizó, reversión en ambos sentidos y tercero, documento cruce, centro y sucursal
/// por línea.
/// <para>
/// 2026-11-16 (pedido del dueño): carta <b>vertical</b> y más clara. Recuadro con tipo, número, fecha y estado; cinco columnas
/// —cuenta (código y nombre), tercero (nombre y documento), detalle (con documento cruce, centro, sucursal y base debajo, en letra
/// pequeña), débito y crédito—; encabezado de tabla sombreado que se repite en cada página, filas alternas, totales en recuadro y
/// firmas de quien elaboró, contabilizó y revisó. Un borrador lleva la marca de agua «BORRADOR» (antes sólo lo decía el texto).
/// </para>
/// </summary>
public static class VoucherPrintReport
{
    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");
    private const float AnchoValor = 78;
    private static readonly Color Linea = Colors.Grey.Lighten1;
    private static readonly Color Sombra = Colors.Grey.Lighten3;
    private static readonly Color Alterna = Colors.Grey.Lighten5;
    private static readonly Color Tenue = Colors.Grey.Darken1;

    public static byte[] Generate(ComprobanteDto d, EmpresaParaImpresion empresa)
    {
        var esBorrador = string.Equals(d.Status, "Draft", StringComparison.OrdinalIgnoreCase);
        var cuadra = d.TotalDebit == d.TotalCredit;
        var documento = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(32);
                page.DefaultTextStyle(x => x.FontSize(8.5f));

                if (esBorrador)
                    page.Background().AlignCenter().AlignMiddle().Rotate(-30).Text("BORRADOR").FontSize(80).Bold().FontColor(Colors.Grey.Lighten3);

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().PaddingRight(12).Column(c =>
                        {
                            c.Item().Text(empresa.Name).Bold().FontSize(13);
                            if (!string.IsNullOrWhiteSpace(empresa.TaxId)) c.Item().Text($"NIT {empresa.TaxId}").FontSize(8.5f).FontColor(Tenue);
                            c.Item().PaddingTop(6).Text("COMPROBANTE DE CONTABILIDAD").SemiBold().FontSize(9).FontColor(Tenue);
                        });
                        row.ConstantItem(200).Border(1).BorderColor(Linea).Column(c =>
                        {
                            c.Item().Background(Sombra).PaddingVertical(3).PaddingHorizontal(6).AlignCenter()
                                .Text($"{d.VoucherTypeName} ({d.VoucherTypeCode})").SemiBold().FontSize(9);
                            c.Item().PaddingVertical(4).AlignCenter().Text(esBorrador ? "BORRADOR" : $"N.º {d.Referencia}").Bold().FontSize(14);
                            c.Item().BorderTop(1).BorderColor(Linea).Row(r =>
                            {
                                r.RelativeItem().BorderRight(1).BorderColor(Linea).Padding(3).Column(x =>
                                {
                                    x.Item().AlignCenter().Text("Fecha").FontSize(7).FontColor(Tenue);
                                    x.Item().AlignCenter().Text(d.Date.ToString("dd/MM/yyyy", Co)).SemiBold();
                                });
                                r.RelativeItem().Padding(3).Column(x =>
                                {
                                    x.Item().AlignCenter().Text("Estado").FontSize(7).FontColor(Tenue);
                                    x.Item().AlignCenter().Text(Estado(d.Status)).SemiBold();
                                });
                            });
                        });
                    });

                    col.Item().PaddingTop(8).Border(1).BorderColor(Linea).Padding(6).Column(c =>
                    {
                        c.Item().Text(t =>
                        {
                            t.Span("Concepto: ").SemiBold();
                            t.Span(string.IsNullOrWhiteSpace(d.Description) ? "—" : d.Description);
                        });
                        if (d.Origin.Module != "CNT")
                            c.Item().PaddingTop(2).Text($"Origen: {d.Origin.ModuleName}{(d.Origin.SourceType is null ? string.Empty : $" · {d.Origin.SourceType}")}").FontSize(8).FontColor(Tenue);
                        if (d.Reversal.ReversesPublicId is not null)
                            c.Item().PaddingTop(2).Text($"Reversa el comprobante {d.Reversal.ReversesNumber}. Motivo: {d.Reversal.Reason}").FontSize(8).Italic();
                        if (d.Reversal.ReversedByPublicId is not null)
                            c.Item().PaddingTop(2).Text($"REVERSADO por el comprobante {d.Reversal.ReversedByNumber}. Motivo: {d.Reversal.Reason}").FontSize(8).Bold().FontColor(Colors.Red.Darken2);
                    });
                    col.Item().Height(8);
                });

                page.Content().Column(col =>
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(24);         // #
                            cols.RelativeColumn(2.3f);       // cuenta
                            cols.RelativeColumn(2f);         // tercero
                            cols.RelativeColumn(2.3f);       // detalle
                            cols.ConstantColumn(AnchoValor); // débito
                            cols.ConstantColumn(AnchoValor); // crédito
                        });

                        table.Header(h =>
                        {
                            foreach (var (titulo, derecha) in new[] { ("#", false), ("Cuenta", false), ("Tercero", false), ("Detalle", false), ("Débito", true), ("Crédito", true) })
                            {
                                var celda = h.Cell().Background(Sombra).BorderBottom(1).BorderColor(Linea).PaddingVertical(4).PaddingHorizontal(3);
                                if (derecha) celda.AlignRight().Text(titulo).SemiBold(); else celda.Text(titulo).SemiBold();
                            }
                        });

                        var n = 0;
                        foreach (var l in d.Lines)
                        {
                            // Filas alternas, salvo en el borrador: el fondo taparía la marca de agua.
                            var alterna = n++ % 2 == 1 && !esBorrador;
                            IContainer Celda()
                            {
                                IContainer celda = table.Cell();
                                if (alterna) celda = celda.Background(Alterna);
                                return celda.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).PaddingHorizontal(3);
                            }

                            Celda().Text(l.LineNumber.ToString(Co)).FontColor(Tenue);
                            Celda().Column(c =>
                            {
                                c.Item().Text(l.AccountCode).SemiBold();
                                c.Item().Text(l.AccountName).FontSize(7.5f);
                            });
                            Celda().Column(c =>
                            {
                                if (l.PersonName is null) { c.Item().Text("—").FontColor(Tenue); return; }
                                c.Item().Text(l.PersonName);
                                if (!string.IsNullOrWhiteSpace(l.PersonTaxId)) c.Item().Text(l.PersonTaxId).FontSize(7.5f).FontColor(Tenue);
                            });
                            Celda().Column(c =>
                            {
                                if (!string.IsNullOrWhiteSpace(l.Detail)) c.Item().Text(l.Detail);
                                var extra = Datos(l);
                                if (extra.Length > 0) c.Item().Text(extra).FontSize(7).FontColor(Tenue);
                            });
                            Celda().AlignRight().Text(l.Debit > 0 ? Pesos(l.Debit) : string.Empty);
                            Celda().AlignRight().Text(l.Credit > 0 ? Pesos(l.Credit) : string.Empty);
                        }
                    });

                    col.Item().BorderTop(1.5f).BorderBottom(1.5f).BorderColor(Colors.Grey.Darken2).Background(Sombra).PaddingVertical(4).PaddingHorizontal(3).Row(row =>
                    {
                        row.RelativeItem().Text(t =>
                        {
                            t.Span("TOTALES").Bold();
                            t.Span($"   {d.Lines.Count} {(d.Lines.Count == 1 ? "línea" : "líneas")}").FontSize(7.5f).FontColor(Tenue);
                            if (!cuadra) t.Span($"   Descuadre: {Pesos(d.TotalDebit - d.TotalCredit)}").Bold().FontColor(Colors.Red.Darken2);
                        });
                        row.ConstantItem(AnchoValor).AlignRight().Text(Pesos(d.TotalDebit)).Bold();
                        row.ConstantItem(AnchoValor).AlignRight().Text(Pesos(d.TotalCredit)).Bold();
                    });

                    col.Item().PaddingTop(40).ShowEntire().Row(row =>
                    {
                        Firma(row, "Elaboró", d.RegisteredBy, d.RegisteredAt);
                        row.ConstantItem(24);
                        Firma(row, "Contabilizó", d.PostedBy ?? string.Empty, d.PostedAt);
                        row.ConstantItem(24);
                        Firma(row, "Revisó", string.Empty, null);
                    });
                });

                page.Footer().PaddingTop(6).BorderTop(0.5f).BorderColor(Linea).Row(row =>
                {
                    row.RelativeItem().Text($"{d.VoucherTypeCode} {(esBorrador ? "borrador" : d.Referencia)} · impreso el {DateTime.UtcNow.AddHours(-5):dd/MM/yyyy HH:mm} (hora de Colombia)")
                        .FontSize(7).FontColor(Tenue);
                    row.ConstantItem(90).AlignRight().Text(x =>
                    {
                        x.Span("Página ").FontSize(7).FontColor(Tenue);
                        x.CurrentPageNumber().FontSize(7).FontColor(Tenue);
                        x.Span(" de ").FontSize(7).FontColor(Tenue);
                        x.TotalPages().FontSize(7).FontColor(Tenue);
                    });
                });
            });
        });

        return documento.GeneratePdf();
    }

    /// <summary>Lo secundario de la línea, en una sola línea pequeña bajo el detalle: documento cruce, centro, sucursal y base.</summary>
    private static string Datos(LineaDeComprobanteDto l)
    {
        var partes = new List<string>();
        if (l.CrossDocumentType is not null || l.CrossDocumentNumber is not null) partes.Add($"Doc. {l.CrossDocumentType} {l.CrossDocumentNumber}".Trim());
        if (!string.IsNullOrWhiteSpace(l.CostCenterName)) partes.Add($"C. costo {l.CostCenterName}");
        if (!string.IsNullOrWhiteSpace(l.BranchName)) partes.Add($"Suc. {l.BranchName}");
        if (l.TaxBase is { } b) partes.Add($"Base {Pesos(b)}");
        return string.Join(" · ", partes);
    }

    private static void Firma(RowDescriptor row, string rotulo, string quien, DateTime? cuando) =>
        row.RelativeItem().Column(c =>
        {
            c.Item().Height(14).AlignCenter().AlignBottom().Text(quien).FontSize(8);
            c.Item().LineHorizontal(0.75f).LineColor(Colors.Grey.Darken1);
            c.Item().AlignCenter().Text(rotulo).FontSize(8).SemiBold();
            if (cuando is { } f) c.Item().AlignCenter().Text(f.AddHours(-5).ToString("dd/MM/yyyy HH:mm", Co)).FontSize(7).FontColor(Tenue);
        });

    private static string Estado(string status) => status switch
    {
        "Draft" => "Borrador",
        "Posted" => "Contabilizado",
        "Reversed" => "Reversado",
        _ => status,
    };

    private static string Pesos(decimal v) => v.ToString("N2", Co);
}

/// <summary><see cref="IVoucherPdfRenderer"/> con QuestPDF. Registrado en la API, la única capa que conoce la librería.</summary>
public sealed class VoucherPdfRenderer : IVoucherPdfRenderer
{
    public byte[] Render(ComprobanteDto comprobante, EmpresaParaImpresion empresa) => VoucherPrintReport.Generate(comprobante, empresa);
}
