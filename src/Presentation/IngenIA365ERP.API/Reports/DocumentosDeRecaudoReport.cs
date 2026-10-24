using System.Globalization;
using IngenIA365ERP.Application.Receipts.Sessions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports;

/// <summary>
/// Los documentos de la caja de Recaudo (feature 015, T0096; FR-033, FR-156): el cierre de la sesión, el cierre del día del punto y el acta del arqueo
/// sorpresivo o diferido, cada uno con su número y su código de verificación en el encabezado y el pie, y las firmas. (nuevo)
/// </summary>
public static class DocumentosDeRecaudoReport
{
    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    public static byte[] CierreDeSesion(CierreDeSesionModel m)
    {
        ArgumentNullException.ThrowIfNull(m);
        var detalle = $"Cajero: {m.Cashier} · apertura {Hora(m.OpenedAt)}" + (m.ClosedAt is { } c ? $" · cierre {Hora(c)}" : string.Empty)
                      + $" · base {Pesos(m.OpeningBase)}" + (m.Blind ? " · arqueo ciego" : string.Empty)
                      + (m.Deferred ? $" · arqueo diferido con testigo {m.Witness}" : string.Empty);
        return Documento(m.Company, "Cierre de sesión de caja", m.Number, m.VerificationCode, $"{m.CollectionPoint} · caja {m.CashBox}",
            $"Día {m.BusinessDate:yyyy-MM-dd} · {m.State}", detalle, col => Lineas(col, m.Lines),
            row => { Firma(row, "Cajero", m.Cashier); row.ConstantItem(40); Firma(row, m.Deferred ? "Testigo" : "Supervisor", m.Witness ?? string.Empty); });
    }

    public static byte[] CierreDelDia(CierreDelDiaModel m)
    {
        ArgumentNullException.ThrowIfNull(m);
        var detalle = $"Sesiones {m.Sessions} · recibos {m.Receipts} · anulaciones {m.Voidings} · cerró {m.ClosedBy}"
                      + (m.ByDelegate ? " (suplente)" : string.Empty) + $" · {Hora(m.ClosedAt)}";
        return Documento(m.Company, "Cierre del día del punto", m.Number, m.VerificationCode, m.CollectionPoint, $"Día {m.BusinessDate:yyyy-MM-dd}", detalle,
            col => col.Item().Table(t =>
            {
                t.ColumnsDefinition(c => { c.RelativeColumn(2); c.ConstantColumn(90); c.ConstantColumn(90); c.ConstantColumn(90); });
                t.Header(h =>
                {
                    h.Cell().BorderBottom(1).Padding(2).Text("Medio").Bold();
                    foreach (var titulo in new[] { "Esperado", "Contado", "Diferencia" }) h.Cell().BorderBottom(1).Padding(2).AlignRight().Text(titulo).Bold();
                });
                foreach (var l in m.Lines)
                {
                    t.Cell().Padding(2).Text(l.MeansCode);
                    t.Cell().Padding(2).AlignRight().Text(Pesos(l.Expected));
                    t.Cell().Padding(2).AlignRight().Text(Pesos(l.Counted));
                    t.Cell().Padding(2).AlignRight().Text(Pesos(l.Difference));
                }
                t.Cell().BorderTop(1).Padding(2).Text("Total").Bold();
                t.Cell().BorderTop(1).Padding(2).AlignRight().Text(Pesos(m.Lines.Sum(l => l.Expected))).Bold();
                t.Cell().BorderTop(1).Padding(2).AlignRight().Text(Pesos(m.Lines.Sum(l => l.Counted))).Bold();
                t.Cell().BorderTop(1).Padding(2).AlignRight().Text(Pesos(m.Lines.Sum(l => l.Difference))).Bold();
            }),
            row => Firma(row, "Supervisor del punto", m.ClosedBy));
    }

    public static byte[] Acta(ActaDeArqueoModel m)
    {
        ArgumentNullException.ThrowIfNull(m);
        var detalle = $"Cajero: {m.Cashier} · contó {m.CountedBy}" + (m.Witness is { } t ? $" · testigo {t}" : string.Empty) + $" · {Hora(m.CountedAt)}";
        return Documento(m.Company, m.Title, m.Number, m.VerificationCode, $"{m.CollectionPoint} · caja {m.CashBox}", string.Empty, detalle,
            col => Lineas(col, m.Lines),
            row => { Firma(row, "Cajero", m.Cashier); row.ConstantItem(30); Firma(row, "Quien arquea", m.CountedBy); row.ConstantItem(30); Firma(row, "Testigo", m.Witness ?? string.Empty); });
    }

    /// <summary>
    /// El recibo de caja (fase 6, T0118; FR-044, FR-045, FR-177, FR-190): en tirilla de 80 mm o carta, «COPIA» al reimprimir, el código de verificación, las
    /// aplicaciones con el comprobante de su módulo, la elección del deudor y la aceptación del aviso, la firmeza, la imputación por medio, quien efectúa y la
    /// declaración de origen de fondos. Con pagador tercero el producto va enmascarado y sin saldos.
    /// </summary>
    public static byte[] Recibo(Application.Receipts.CashDesk.ReciboModel m)
    {
        ArgumentNullException.ThrowIfNull(m);
        var termica = m.Format == Application.Receipts.CashDesk.FormatoDeRecibo.Thermal;
        return Document.Create(container => container.Page(page =>
        {
            if (termica)
            {
                page.ContinuousSize(80, Unit.Millimetre);
                page.Margin(4, Unit.Millimetre);
                page.DefaultTextStyle(x => x.FontSize(7.5f));
            }
            else
            {
                page.Size(PageSizes.Letter);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9));
            }
            page.Content().Column(col =>
            {
                col.Item().AlignCenter().Text(m.Company.Name).Bold();
                if (!string.IsNullOrWhiteSpace(m.Company.Nit)) col.Item().AlignCenter().Text($"NIT {m.Company.Nit}");
                col.Item().AlignCenter().Text($"Recibo de caja N.º {m.Number}").SemiBold();
                if (m.Copy) col.Item().AlignCenter().Text("COPIA").Bold();
                if (m.Voided) col.Item().AlignCenter().Text("ANULADO").Bold();
                col.Item().Text($"{m.Office}{(m.CollectionPoint is null ? "" : $" · {m.CollectionPoint}")}{(m.CashBox is null ? "" : $" · {m.CashBox}")}");
                col.Item().Text($"Fecha valor {m.ValueDate:yyyy-MM-dd} · registrado {Hora(m.RegisteredAt)}");
                col.Item().Text($"Pagador: {m.Payer}{(m.PayerIsThirdParty ? " (tercero)" : "")}");
                if (m.DeliveredBy is not null) col.Item().Text($"Efectúa: {m.DeliveredBy}");
                if (m.Cashier is not null) col.Item().Text($"Cajero: {m.Cashier}");
                col.Item().PaddingVertical(3).LineHorizontal(0.5f);
                foreach (var l in m.Lines)
                {
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Text($"{l.Ordinal}. {l.Description}{(l.Product is null ? "" : $" {l.Product}")}");
                        r.ConstantItem(termica ? 70 : 110).AlignRight().Text(Pesos(l.Amount));
                    });
                    if (l.ModuleVoucher is not null) col.Item().PaddingLeft(8).Text($"Comprobante {l.ModuleVoucher}").FontSize(termica ? 6.5f : 8);
                    if (l.Choice is not null) col.Item().PaddingLeft(8).Text($"Elección del deudor: {l.Choice}").FontSize(termica ? 6.5f : 8);
                    if (l.NoticeAccepted) col.Item().PaddingLeft(8).Text("Aceptó el aviso del pago anticipado").FontSize(termica ? 6.5f : 8);
                    if (l.FinalityUntil is { } f) col.Item().PaddingLeft(8).Text($"En firme desde {f:yyyy-MM-dd}").FontSize(termica ? 6.5f : 8);
                }
                col.Item().PaddingVertical(3).LineHorizontal(0.5f);
                col.Item().Row(r => { r.RelativeItem().Text("Total aplicado").Bold(); r.ConstantItem(termica ? 70 : 110).AlignRight().Text(Pesos(m.TotalApplied)).Bold(); });
                foreach (var medio in m.Means)
                {
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Text($"{medio.Code}{(medio.Reference is null ? "" : $" ref. {medio.Reference}")}");
                        r.ConstantItem(termica ? 70 : 110).AlignRight().Text(Pesos(medio.Amount));
                    });
                    if (medio.Covers.Count > 1)
                        col.Item().PaddingLeft(8).Text(string.Join(" · ", medio.Covers.Select(c => $"{c.Aplicacion}: {Pesos(c.Valor)}"))).FontSize(termica ? 6.5f : 8);
                }
                if (m.ChangeGiven > 0) col.Item().Row(r => { r.RelativeItem().Text("Vueltas"); r.ConstantItem(termica ? 70 : 110).AlignRight().Text(Pesos(m.ChangeGiven)); });
                if (m.SourceOfFunds is not null) col.Item().PaddingTop(3).Text($"Origen de fondos declarado: {m.SourceOfFunds}");
                col.Item().PaddingTop(6).AlignCenter().Text($"Código de verificación {m.VerificationCode}").FontSize(termica ? 6.5f : 8);
            });
        })).GeneratePdf();
    }

    private static byte[] Documento(EmpresaDelDocumentoDto empresa, string titulo, string numero, string codigo, string lugar, string subtitulo, string detalle,
        Action<ColumnDescriptor> contenido, Action<RowDescriptor> firmas) =>
        Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(30);
            page.DefaultTextStyle(x => x.FontSize(8.5f));
            page.Header().Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(empresa.Name).Bold().FontSize(12);
                        if (!string.IsNullOrWhiteSpace(empresa.Nit)) c.Item().Text($"NIT {empresa.Nit}");
                    });
                    row.ConstantItem(260).AlignRight().Column(c =>
                    {
                        c.Item().Text(titulo).SemiBold().FontSize(12);
                        c.Item().Text($"N.º {numero}");
                        c.Item().Text(lugar);
                        if (!string.IsNullOrWhiteSpace(subtitulo)) c.Item().Text(subtitulo);
                    });
                });
                col.Item().PaddingTop(3).Text(detalle);
                col.Item().PaddingTop(3).PaddingBottom(4).LineHorizontal(1);
            });
            page.Content().Column(col =>
            {
                contenido(col);
                col.Item().PaddingTop(40).Row(firmas);
            });
            page.Footer().AlignCenter().Text(x =>
            {
                x.Span($"{titulo} {numero} · código de verificación {codigo} · página ").FontSize(7);
                x.CurrentPageNumber().FontSize(7);
                x.Span(" de ").FontSize(7);
                x.TotalPages().FontSize(7);
            });
        })).GeneratePdf();

    /// <summary>
    /// El documento de anulación de un recibo (fase 10, T0179; FR-156): número y código de verificación, el recibo original, el motivo, quién pidió y quién aprobó,
    /// cada aplicación reversada u omitida (la que el módulo ya había reversado), y si el dinero quedó como saldo a favor del pagador.
    /// </summary>
    public static byte[] Anulacion(Application.Receipts.Voidings.AnulacionModel m)
    {
        ArgumentNullException.ThrowIfNull(m);
        var detalle = $"Recibo {m.ReceiptNumber} del {m.ReceiptValueDate:yyyy-MM-dd} · pagador {m.Payer} · {Pesos(m.Amount)} · motivo: {m.Reason}"
                      + (m.SessionClosed ? " · el dinero quedó como saldo a favor del pagador" : string.Empty);
        return Documento(m.Company, "Anulación de recibo", m.Number, m.VerificationCode, $"Recibo {m.ReceiptNumber}",
            m.DecidedAt is { } d ? $"Aplicada {Hora(d)}" : string.Empty, detalle,
            col => col.Item().Table(t =>
            {
                t.ColumnsDefinition(c => { c.ConstantColumn(30); c.RelativeColumn(2); c.RelativeColumn(1.5f); c.ConstantColumn(90); c.RelativeColumn(2); });
                t.Header(h =>
                {
                    foreach (var titulo in new[] { "#", "Aplicación", "Producto", "Valor", "Resultado" }) h.Cell().BorderBottom(1).Padding(2).Text(titulo).Bold();
                });
                foreach (var l in m.Lines)
                {
                    t.Cell().Padding(2).Text(l.Ordinal.ToString(Co));
                    t.Cell().Padding(2).Text(l.Description);
                    t.Cell().Padding(2).Text(l.Product ?? string.Empty);
                    t.Cell().Padding(2).AlignRight().Text(Pesos(l.Amount));
                    t.Cell().Padding(2).Text(l.Outcome);
                }
            }),
            row => { Firma(row, "Pidió", m.RequestedBy); row.ConstantItem(40); Firma(row, "Aprobó", m.ApprovedBy ?? string.Empty); });
    }

    private static void Lineas(ColumnDescriptor col, IReadOnlyList<LineaDelDocumentoDto> lineas) =>
        col.Item().Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2.2f); c.ConstantColumn(70); c.ConstantColumn(70); c.ConstantColumn(70); c.ConstantColumn(60); c.RelativeColumn(1.8f);
            });
            t.Header(h =>
            {
                h.Cell().BorderBottom(1).Padding(2).Text("Medio").Bold();
                foreach (var titulo in new[] { "Esperado", "Contado", "Diferencia", "Tolerancia" }) h.Cell().BorderBottom(1).Padding(2).AlignRight().Text(titulo).Bold();
                h.Cell().BorderBottom(1).Padding(2).Text("Tratamiento / motivo").Bold();
            });
            foreach (var l in lineas)
            {
                t.Cell().Padding(2).Text($"{l.MeansCode} · {l.MeansName}");
                t.Cell().Padding(2).AlignRight().Text(Pesos(l.Expected));
                t.Cell().Padding(2).AlignRight().Text(Pesos(l.Counted));
                t.Cell().Padding(2).AlignRight().Text(Pesos(l.Difference));
                t.Cell().Padding(2).AlignRight().Text(Pesos(l.Tolerance));
                t.Cell().Padding(2).Text(string.Join(" · ", new[] { l.Difference == 0m ? null : l.Treatment, l.Reason, l.Approved ? "aprobada" : null }
                    .Where(x => !string.IsNullOrWhiteSpace(x))));
            }
        });

    private static void Firma(RowDescriptor row, string rotulo, string quien) =>
        row.RelativeItem().Column(c =>
        {
            c.Item().LineHorizontal(0.5f);
            c.Item().AlignCenter().Text(rotulo).Bold();
            c.Item().AlignCenter().Text(quien);
        });

    private static string Hora(DateTime utc) => utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", Co);

    private static string Pesos(decimal v) => v.ToString("N2", Co);
}

/// <summary><see cref="IDocumentosDeRecaudoEnPdf"/> con QuestPDF. (nuevo)</summary>
public sealed class DocumentosDeRecaudoPdfRenderer : IDocumentosDeRecaudoEnPdf
{
    public byte[] CierreDeSesion(CierreDeSesionModel modelo) => DocumentosDeRecaudoReport.CierreDeSesion(modelo);

    public byte[] CierreDelDia(CierreDelDiaModel modelo) => DocumentosDeRecaudoReport.CierreDelDia(modelo);

    public byte[] Acta(ActaDeArqueoModel modelo) => DocumentosDeRecaudoReport.Acta(modelo);

    public byte[] Recibo(Application.Receipts.CashDesk.ReciboModel modelo) => DocumentosDeRecaudoReport.Recibo(modelo);

    public byte[] Anulacion(Application.Receipts.Voidings.AnulacionModel modelo) => DocumentosDeRecaudoReport.Anulacion(modelo);
}
