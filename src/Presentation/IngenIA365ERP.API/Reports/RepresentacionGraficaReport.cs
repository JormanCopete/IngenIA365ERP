using System.Globalization;
using IngenIA365ERP.Application.ElectronicInvoicing;
using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports;

/// <summary>
/// La representación gráfica de un documento electrónico (feature 012, I4, T750; contracts/dian.md §13.1): <b>una sola plantilla del ERP</b>
/// para todos los canales y para el modo propio —cambiar de proveedor no cambia documentos (FR-064)—, en carta (factura, notas y documento
/// soporte) y en tirilla de 80 mm (documento equivalente POS y su nota de ajuste). Todo sale del canónico, así que la contraparte es la de la
/// copia fiscal con que se numeró, nunca el maestro de hoy (FR-011). El QR es el <c>QrContent</c> que devolvió el canal, tal cual; sin él no
/// se pinta (un documento de papel en contingencia 03 no lo tiene). Las leyendas por estado (tipo 04 pendiente de validación, papel 03,
/// «SIN VALIDEZ FISCAL» en pruebas) las decide <see cref="LeyendasDeRepresentacion"/> y aquí sólo se pintan, arriba y bien visibles.
/// (nuevo)
/// </summary>
public static class RepresentacionGraficaReport
{
    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    /// <summary>El ancho de la tirilla del POS, en milímetros.</summary>
    private const float AnchoDeTirillaMm = 80f;

    public static byte[] Generate(SolicitudDeRepresentacion solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        ArgumentNullException.ThrowIfNull(solicitud.Canonico);
        var qr = Qr(solicitud.QrContent);
        return solicitud.Formato == FormatoDeRepresentacion.Tirilla80mm ? Tirilla(solicitud, qr) : Carta(solicitud, qr);
    }

    // ---------------------------------------------------------------------------------------------------------- carta --

    private static byte[] Carta(SolicitudDeRepresentacion s, byte[]? qr)
    {
        var c = s.Canonico;
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(32);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(emisor =>
                        {
                            emisor.Item().Text(c.Issuer.Name).Bold().FontSize(13);
                            emisor.Item().Text(Identificacion(c.Issuer));
                            if (Direccion(c.Issuer) is { } direccion) emisor.Item().Text(direccion);
                            if (!string.IsNullOrWhiteSpace(c.Issuer.Email)) emisor.Item().Text(c.Issuer.Email);
                            if (Resolucion(c) is { } resolucion) emisor.Item().PaddingTop(2).Text(resolucion).FontSize(7);
                        });
                        row.ConstantItem(230).AlignRight().Column(doc =>
                        {
                            doc.Item().AlignRight().Text(LeyendasDeRepresentacion.Nombre(c.Kind)).SemiBold().FontSize(12);
                            doc.Item().AlignRight().Text($"No. {c.Number.Full}").Bold().FontSize(12);
                            doc.Item().AlignRight().Text($"Expedición {Fecha(c.IssuedAt)}");
                            if (c.DueDate is { } vence) doc.Item().AlignRight().Text($"Vencimiento {vence:yyyy-MM-dd}");
                            doc.Item().AlignRight().Text($"Forma de pago {FormaDePago(c.PaymentForm)}");
                        });
                    });
                    foreach (var leyenda in s.Leyendas)
                        col.Item().PaddingTop(4).AlignCenter().Text(leyenda).Bold().FontSize(11);
                    col.Item().PaddingTop(6).Text($"{Rol(c)}: {c.Counterparty.Name} · {Identificacion(c.Counterparty)}");
                    if (Direccion(c.Counterparty) is { } dirContraparte) col.Item().Text(dirContraparte);
                    if (c.References.Corrected is { } corregido)
                        col.Item().Text($"Corrige el documento {corregido.Number} del {corregido.IssueDate:yyyy-MM-dd}"
                                        + (string.IsNullOrWhiteSpace(corregido.UniqueCode) ? string.Empty : $" · {corregido.UniqueCode}"));
                    col.Item().PaddingTop(4).PaddingBottom(4).LineHorizontal(1);
                });

                page.Content().Column(col =>
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(d =>
                        {
                            d.ConstantColumn(24);
                            d.ConstantColumn(60);
                            d.RelativeColumn(3);
                            d.ConstantColumn(55);
                            d.ConstantColumn(35);
                            d.ConstantColumn(70);
                            d.ConstantColumn(60);
                            d.ConstantColumn(75);
                        });
                        table.Header(h =>
                        {
                            foreach (var (titulo, derecha) in new[]
                                     {
                                         ("#", false), ("Código", false), ("Descripción", false), ("Cantidad", true), ("Unidad", false),
                                         ("Precio", true), ("Descuento", true), ("Total", true),
                                     })
                            {
                                var celda = h.Cell().BorderBottom(1).Padding(2);
                                if (derecha) celda.AlignRight().Text(titulo).Bold(); else celda.Text(titulo).Bold();
                            }
                        });
                        foreach (var l in c.Lines)
                        {
                            table.Cell().Padding(2).Text(l.LineNumber.ToString(CultureInfo.InvariantCulture));
                            table.Cell().Padding(2).Text(l.ProductCode);
                            table.Cell().Padding(2).Text(l.Description);
                            table.Cell().Padding(2).AlignRight().Text(l.Quantity.ToString("0.####", Co));
                            table.Cell().Padding(2).Text(l.UnitCode);
                            table.Cell().Padding(2).AlignRight().Text(Pesos(l.UnitPrice));
                            var descuento = l.Allowances.Sum(a => a.Amount);
                            table.Cell().Padding(2).AlignRight().Text(descuento == 0m ? string.Empty : Pesos(descuento));
                            table.Cell().Padding(2).AlignRight().Text(Pesos(l.LineExtension));
                        }
                    });

                    col.Item().PaddingTop(8).Row(row =>
                    {
                        row.RelativeItem().Column(izq =>
                        {
                            Impuestos(izq, c);
                            Retenciones(izq, c);
                            Pagos(izq, c);
                            foreach (var nota in c.Notes) izq.Item().PaddingTop(4).Text(nota).FontSize(8);
                        });
                        row.ConstantItem(210).Column(der => Totales(der, c));
                    });

                    col.Item().PaddingTop(10).Row(row =>
                    {
                        if (qr is not null) row.ConstantItem(96).Image(qr);
                        row.RelativeItem().PaddingLeft(qr is null ? 0 : 8).Column(pie => CodigoYContingencia(pie, s));
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span($"{LeyendasDeRepresentacion.Nombre(c.Kind)} {c.Number.Full} · página ").FontSize(7);
                    x.CurrentPageNumber().FontSize(7);
                    x.Span(" de ").FontSize(7);
                    x.TotalPages().FontSize(7);
                });
            });
        }).GeneratePdf();
    }

    // -------------------------------------------------------------------------------------------------------- tirilla --

    private static byte[] Tirilla(SolicitudDeRepresentacion s, byte[]? qr)
    {
        var c = s.Canonico;
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.ContinuousSize(AnchoDeTirillaMm, Unit.Millimetre);
                page.Margin(6);
                page.DefaultTextStyle(x => x.FontSize(7));

                page.Content().Column(col =>
                {
                    col.Item().AlignCenter().Text(c.Issuer.Name).Bold().FontSize(9);
                    col.Item().AlignCenter().Text(Identificacion(c.Issuer));
                    if (Direccion(c.Issuer) is { } direccion) col.Item().AlignCenter().Text(direccion);
                    if (Resolucion(c) is { } resolucion) col.Item().AlignCenter().Text(resolucion).FontSize(6);
                    col.Item().PaddingTop(3).AlignCenter().Text(LeyendasDeRepresentacion.Nombre(c.Kind)).SemiBold();
                    col.Item().AlignCenter().Text($"No. {c.Number.Full}").Bold().FontSize(9);
                    col.Item().AlignCenter().Text(Fecha(c.IssuedAt));
                    if (c.Pos is { } pos)
                    {
                        if (!string.IsNullOrWhiteSpace(pos.CashRegisterPlate)) col.Item().AlignCenter().Text($"Caja {pos.CashRegisterPlate}");
                        if (!string.IsNullOrWhiteSpace(pos.CashierName)) col.Item().AlignCenter().Text($"Cajero {pos.CashierName}");
                    }
                    foreach (var leyenda in s.Leyendas) col.Item().PaddingTop(2).AlignCenter().Text(leyenda).Bold();
                    col.Item().PaddingTop(3).Text($"{Rol(c)}: {c.Counterparty.Name}");
                    col.Item().Text(Identificacion(c.Counterparty));
                    if (c.References.Corrected is { } corregido) col.Item().Text($"Corrige {corregido.Number} del {corregido.IssueDate:yyyy-MM-dd}");
                    col.Item().PaddingVertical(2).LineHorizontal(0.5f);

                    foreach (var l in c.Lines)
                    {
                        col.Item().Text($"{l.ProductCode} {l.Description}");
                        col.Item().Row(r =>
                        {
                            r.RelativeItem().Text($"{l.Quantity.ToString("0.####", Co)} {l.UnitCode} × {Pesos(l.UnitPrice)}");
                            r.ConstantItem(70).AlignRight().Text(Pesos(l.LineExtension));
                        });
                    }
                    col.Item().PaddingVertical(2).LineHorizontal(0.5f);
                    Totales(col, c);
                    Impuestos(col, c);
                    Retenciones(col, c);
                    Pagos(col, c);
                    col.Item().PaddingTop(4).Column(pie => CodigoYContingencia(pie, s));
                    if (qr is not null) col.Item().PaddingTop(4).AlignCenter().Width(90).Image(qr);
                    if (c.Pos?.Software is { } software)
                        col.Item().PaddingTop(3).AlignCenter().Text($"Software {software.Name} · {software.ManufacturerName} NIT {software.ManufacturerTaxId}").FontSize(6);
                });
            });
        }).GeneratePdf();
    }

    // ------------------------------------------------------------------------------------------------------- bloques --

    private static void Totales(ColumnDescriptor col, DocumentoElectronicoCanonico c)
    {
        Total(col, "Subtotal", c.Totals.LineExtension);
        if (c.Totals.Allowances != 0m) Total(col, "Descuentos", c.Totals.Allowances);
        Total(col, "Impuestos", c.Totals.Taxes);
        if (c.Totals.Charges != 0m) Total(col, "Cargos", c.Totals.Charges);
        if (c.Totals.Rounding != 0m) Total(col, "Redondeo", c.Totals.Rounding);
        Total(col, "Total", c.Totals.Payable, negrita: true);
        if (c.Totals.Withholdings != 0m) Total(col, "Retenciones", c.Totals.Withholdings);
        if (c.Totals.AmountDue != c.Totals.Payable) Total(col, "A pagar", c.Totals.AmountDue, negrita: true);
    }

    private static void Impuestos(ColumnDescriptor col, DocumentoElectronicoCanonico c)
    {
        var impuestos = c.Lines.SelectMany(l => l.Taxes)
            .GroupBy(t => (t.DianTaxCode, t.Rate, t.AmountPerUnit))
            .Select(g => (g.Key.DianTaxCode, g.Key.Rate, g.Key.AmountPerUnit, Base: g.Sum(t => t.Base), Valor: g.Sum(t => t.Amount)))
            .ToList();
        if (impuestos.Count == 0) return;
        col.Item().PaddingTop(4).Text("Impuestos").Bold();
        foreach (var i in impuestos)
            col.Item().Text($"{Tarifa(i.DianTaxCode, i.Rate, i.AmountPerUnit)}: base {Pesos(i.Base)} · valor {Pesos(i.Valor)}");
    }

    private static void Retenciones(ColumnDescriptor col, DocumentoElectronicoCanonico c)
    {
        if (c.Withholdings.Count == 0) return;
        col.Item().PaddingTop(4).Text("Retenciones").Bold();
        foreach (var r in c.Withholdings)
            col.Item().Text($"{Tarifa(r.DianTaxCode, r.Rate, null)}: base {Pesos(r.Base)} · valor {Pesos(r.Amount)}");
    }

    private static void Pagos(ColumnDescriptor col, DocumentoElectronicoCanonico c)
    {
        if (c.Payments.Count == 0) return;
        col.Item().PaddingTop(4).Text("Medios de pago").Bold();
        foreach (var p in c.Payments)
            col.Item().Text($"Medio {p.DianPaymentMeansCode}{(string.IsNullOrWhiteSpace(p.Reference) ? string.Empty : $" · {p.Reference}")}: {Pesos(p.Amount)}");
    }

    private static void CodigoYContingencia(ColumnDescriptor col, SolicitudDeRepresentacion s)
    {
        if (!string.IsNullOrWhiteSpace(s.UniqueCode))
        {
            col.Item().Text(NombreDelCodigo(s.Canonico.Kind)).Bold();
            col.Item().Text(s.UniqueCode).FontSize(7);
        }
        if (s.Canonico.Contingency is { } contingencia)
            col.Item().PaddingTop(2).Text($"Expedido en contingencia: papel {contingencia.PaperNumber} del {Fecha(contingencia.PaperIssuedAt)}");
        col.Item().PaddingTop(2).Text($"Estado ante la DIAN: {Estado(s.Estado)}").FontSize(7);
    }

    private static void Total(ColumnDescriptor c, string rotulo, decimal valor, bool negrita = false) =>
        c.Item().Row(r =>
        {
            var izquierda = r.RelativeItem().Text(rotulo);
            var derecha = r.ConstantItem(90).AlignRight().Text(Pesos(valor));
            if (negrita)
            {
                izquierda.Bold();
                derecha.Bold();
            }
        });

    // ------------------------------------------------------------------------------------------------------- textos --

    private static string Identificacion(ParteCanonica p) =>
        string.IsNullOrWhiteSpace(p.CheckDigit) ? $"Identificación {p.TaxId}" : $"NIT {p.TaxId}-{p.CheckDigit}";

    private static string? Direccion(ParteCanonica p)
    {
        var partes = new[] { p.Address.Line, p.Address.CityDaneCode is { Length: > 0 } ciudad ? $"municipio {ciudad}" : null, p.Phone }
            .Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        return partes.Count == 0 ? null : string.Join(" · ", partes);
    }

    private static string? Resolucion(DocumentoElectronicoCanonico c) =>
        c.Resolution is not { } r
            ? null
            : $"Resolución DIAN {r.Number} del {r.Date:yyyy-MM-dd}, prefijo {c.Number.Prefix} del {r.RangeFrom} al {r.RangeTo}, " +
              $"vigente del {r.ValidFrom:yyyy-MM-dd} al {r.ValidTo:yyyy-MM-dd}";

    private static string Rol(DocumentoElectronicoCanonico c) =>
        c.Kind is ElectronicDocumentKind.SupportDocument or ElectronicDocumentKind.SupportDocumentAdjustmentNote ? "Vendedor" : "Adquirente";

    private static string NombreDelCodigo(ElectronicDocumentKind tipo) => tipo switch
    {
        ElectronicDocumentKind.Invoice => "CUFE",
        ElectronicDocumentKind.SupportDocument or ElectronicDocumentKind.SupportDocumentAdjustmentNote => "CUDS",
        _ => "CUDE",
    };

    private static string FormaDePago(string forma) => forma switch
    {
        "Credit" => "crédito",
        "Cash" => "contado",
        _ => forma,
    };

    private static string Estado(ElectronicDocumentStatus estado) => estado switch
    {
        ElectronicDocumentStatus.Pending => "pendiente de envío",
        ElectronicDocumentStatus.Sent => "enviado, sin respuesta",
        ElectronicDocumentStatus.Validated => "validado",
        ElectronicDocumentStatus.ValidatedWithNotices => "validado con notificaciones",
        ElectronicDocumentStatus.Rejected => "rechazado",
        ElectronicDocumentStatus.IssuerContingency => "expedido en contingencia del facturador",
        ElectronicDocumentStatus.DianContingency => "pendiente de validación de la DIAN",
        ElectronicDocumentStatus.CancelledWithoutReplacement => "anulado sin reemplazo",
        _ => estado.ToString(),
    };

    private static string Tarifa(string codigo, decimal? tarifa, decimal? porUnidad) =>
        tarifa is { } t ? $"Tributo {codigo} al {(t * 100m).ToString("0.##", Co)} %"
        : porUnidad is { } u ? $"Tributo {codigo} de {Pesos(u)} por unidad"
        : $"Tributo {codigo}";

    private static string Fecha(DateTimeOffset momento) => momento.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);

    private static string Pesos(decimal v) => v.ToString("N2", Co);

    /// <summary>El PNG del QR; sin contenido, ninguno (el papel 03 no lleva QR hasta transmitirse).</summary>
    private static byte[]? Qr(string? contenido)
    {
        if (string.IsNullOrWhiteSpace(contenido)) return null;
        using var generador = new QRCodeGenerator();
        using var datos = generador.CreateQrCode(contenido, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(datos).GetGraphic(8);
    }
}

/// <summary><see cref="IRepresentacionGraficaRenderer"/> con QuestPDF y QRCoder (T750). (nuevo)</summary>
public sealed class RepresentacionGraficaRenderer : IRepresentacionGraficaRenderer
{
    public byte[] Renderizar(SolicitudDeRepresentacion solicitud) => RepresentacionGraficaReport.Generate(solicitud);
}
