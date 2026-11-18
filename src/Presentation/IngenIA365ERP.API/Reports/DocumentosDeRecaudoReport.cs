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
    /// 2026-11-17 (pedido del dueño): pensado para el asociado. Cada pago dice qué abonó a cada rubro (capital, interés corriente, interés de mora,
    /// seguro…) o a qué períodos (aportes), qué cuotas tocó y cómo quedó el producto (para cancelar, vencido, próxima cuota, cancelado); las formas de pago
    /// van con su nombre, su referencia y lo que cubrió cada una; y al final lo recibido, las vueltas y la firma del cajero.
    /// </summary>
    public static byte[] Recibo(Application.Receipts.CashDesk.ReciboModel m)
    {
        ArgumentNullException.ThrowIfNull(m);
        var termica = m.Format == Application.Receipts.CashDesk.FormatoDeRecibo.Thermal;
        var chica = termica ? 6.5f : 8f;
        var ancho = termica ? 68f : 110f;
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
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(9.5f));
            }
            page.Content().Column(col =>
            {
                col.Spacing(termica ? 1 : 2);
                col.Item().AlignCenter().Text(m.Company.Name).Bold().FontSize(termica ? 9 : 13);
                if (!string.IsNullOrWhiteSpace(m.Company.Nit)) col.Item().AlignCenter().Text($"NIT {m.Company.Nit}");
                col.Item().PaddingTop(3).AlignCenter().Text($"RECIBO DE CAJA N.º {m.Number}").Bold().FontSize(termica ? 8.5f : 12);
                if (m.Copy) col.Item().AlignCenter().Text("COPIA").Bold();
                if (m.Voided) col.Item().AlignCenter().Text("ANULADO").Bold();
                col.Item().PaddingTop(3).Text(t =>
                {
                    t.Span("Fecha: ").SemiBold();
                    t.Span($"{m.ValueDate:dd/MM/yyyy} · registrado {m.RegisteredAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Co)}");
                });
                col.Item().Text(t =>
                {
                    t.Span("Oficina: ").SemiBold();
                    t.Span($"{m.Office}{(m.CollectionPoint is null ? "" : $" · {m.CollectionPoint}")}{(m.CashBox is null ? "" : $" · caja {m.CashBox}")}");
                });
                col.Item().Text(t =>
                {
                    t.Span("Recibido de: ").SemiBold();
                    t.Span($"{m.Payer}{(m.PayerTaxId is { Length: > 0 } doc ? $" · {doc}" : "")}{(m.PayerIsThirdParty ? " (tercero)" : "")}");
                });
                if (m.DeliveredBy is not null) col.Item().Text($"Entregado por: {m.DeliveredBy}");

                // --- lo que pagó ---
                col.Item().PaddingTop(4).Text("DETALLE DEL PAGO").Bold();
                col.Item().LineHorizontal(0.5f);
                foreach (var l in m.Lines)
                {
                    col.Item().PaddingTop(2).Row(r =>
                    {
                        r.RelativeItem().Text($"{l.Ordinal}. {l.Description}{(l.Product is null ? "" : $" {l.Product}")}").SemiBold();
                        r.ConstantItem(ancho).AlignRight().Text(Pesos(l.Amount)).SemiBold();
                    });
                    var detalle = new List<string>();
                    if (l.Modality is not null) detalle.Add(l.Modality);
                    if (l.Choice is not null) detalle.Add($"elección del deudor: {l.Choice}");
                    if (l.Breakdown is { Installments.Count: > 0 } conCuotas)
                        detalle.Add(conCuotas.Installments.Count == 1 ? $"cuota {conCuotas.Installments[0]}" : $"cuotas {Rango(conCuotas.Installments)}");
                    if (detalle.Count > 0) col.Item().PaddingLeft(8).Text(string.Join(" · ", detalle)).FontSize(chica);
                    if (l.Breakdown is { } b)
                    {
                        foreach (var rubro in b.Concepts)
                            Renglon(col, rubro.Name, rubro.Amount, ancho, chica);
                        foreach (var periodo in b.Periods)
                            Renglon(col, $"Cuota del período {periodo.Period}", periodo.Amount, ancho, chica);
                        if (b.Advance > 0m) Renglon(col, "Queda como anticipo de cuotas futuras", b.Advance, ancho, chica);
                        if (b.CreditBalance > 0m) Renglon(col, "Queda como saldo a favor", b.CreditBalance, ancho, chica);
                        if (Queda(b) is { } queda) col.Item().PaddingLeft(8).Text(queda).Italic().FontSize(chica);
                    }
                    if (l.ModuleVoucher is not null) col.Item().PaddingLeft(8).Text($"Comprobante {l.ModuleVoucher}").FontSize(chica);
                    if (l.NoticeAccepted) col.Item().PaddingLeft(8).Text("Aceptó el aviso del pago anticipado").FontSize(chica);
                    if (l.FinalityUntil is { } f) col.Item().PaddingLeft(8).Text($"En firme desde {f:dd/MM/yyyy}").FontSize(chica);
                }
                col.Item().PaddingTop(2).LineHorizontal(0.5f);
                col.Item().Row(r => { r.RelativeItem().Text("TOTAL PAGADO").Bold(); r.ConstantItem(ancho).AlignRight().Text(Pesos(m.TotalApplied)).Bold(); });

                // --- cómo pagó ---
                col.Item().PaddingTop(4).Text("FORMAS DE PAGO").Bold();
                col.Item().LineHorizontal(0.5f);
                foreach (var medio in m.Means)
                {
                    var referencia = string.Join(" · ", new[] { medio.Last4 is { Length: > 0 } u ? $"****{u}" : null, medio.Reference is { Length: > 0 } x ? $"ref. {x}" : null }
                        .Where(x => x is not null));
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Text($"{medio.Name ?? medio.Code}{(referencia.Length > 0 ? $" ({referencia})" : "")}");
                        r.ConstantItem(ancho).AlignRight().Text(Pesos(medio.Amount));
                    });
                    if (medio.Covers.Count > 1)
                        col.Item().PaddingLeft(8).Text("Cubre: " + string.Join(" · ", medio.Covers.Select(c => $"{c.Aplicacion}. {Pesos(c.Valor)}"))).FontSize(chica);
                }
                col.Item().Row(r => { r.RelativeItem().Text("Total recibido").SemiBold(); r.ConstantItem(ancho).AlignRight().Text(Pesos(m.TotalMeans)).SemiBold(); });
                if (m.ChangeGiven > 0) col.Item().Row(r => { r.RelativeItem().Text("Vueltas"); r.ConstantItem(ancho).AlignRight().Text(Pesos(m.ChangeGiven)); });
                if (m.SourceOfFunds is not null) col.Item().PaddingTop(3).Text($"Origen de fondos declarado: {m.SourceOfFunds}").FontSize(chica);

                if (m.Cashier is not null)
                {
                    if (termica) col.Item().PaddingTop(6).Text($"Atendió: {m.Cashier}");
                    else col.Item().PaddingTop(30).Row(r => { Firma(r, "Cajero", m.Cashier); r.ConstantItem(60); Firma(r, "Recibí conforme", m.Payer); });
                }
                col.Item().PaddingTop(6).AlignCenter().Text("Conserve este recibo: es el soporte de su pago.").FontSize(chica);
                col.Item().AlignCenter().Text($"Código de verificación {m.VerificationCode}").FontSize(chica);
            });
        })).GeneratePdf();
    }

    private static void Renglon(ColumnDescriptor col, string texto, decimal valor, float ancho, float tamano) =>
        col.Item().PaddingLeft(8).Row(r =>
        {
            r.RelativeItem().Text(texto).FontSize(tamano);
            r.ConstantItem(ancho).AlignRight().Text(Pesos(valor)).FontSize(tamano);
        });

    /// <summary>Cómo quedó el producto después del pago, en una frase para el asociado.</summary>
    private static string? Queda(Application.Receipts.CashDesk.DesgloseDelPagoDto b)
    {
        if (b.AccountBalanceAfter is { } saldo) return $"Saldo de la cuenta después del pago: {Pesos(saldo)}";
        if (b.PaidOff) return "Con este pago el crédito queda cancelado.";
        if (b.PayoffAfter is not { } cancelar) return null;
        var partes = new List<string> { $"Saldo para cancelar: {Pesos(cancelar)}" };
        if (b.PastDueAfter is > 0m) partes.Add($"vencido pendiente {Pesos(b.PastDueAfter.Value)}{(b.DaysPastDueAfter is > 0 ? $" ({b.DaysPastDueAfter} días de mora)" : "")}");
        else partes.Add("al día");
        if (b.NextDueDate is { } proxima) partes.Add($"próxima cuota {(b.NextInstallment is { } v ? Pesos(v) + " " : "")}el {proxima:dd/MM/yyyy}");
        return string.Join(" · ", partes);
    }

    /// <summary>Los números de cuota en rangos: 1, 2, 3, 5 → «1 a 3, 5».</summary>
    private static string Rango(IReadOnlyList<int> cuotas)
    {
        var partes = new List<string>();
        for (var i = 0; i < cuotas.Count; i++)
        {
            var desde = cuotas[i];
            while (i + 1 < cuotas.Count && cuotas[i + 1] == cuotas[i] + 1) i++;
            partes.Add(desde == cuotas[i] ? desde.ToString(Co) : $"{desde} a {cuotas[i]}");
        }
        return string.Join(", ", partes);
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

    /// <summary>
    /// El egreso de caja firmado (feature 015, R1, T0374; FR-051, FR-191): qué se entrega y de dónde, la autorización del módulo, el GMF (el que liquidó el origen o
    /// el de Recaudo) y las firmas de quien entrega y de quien recibe. Anulado, lo dice arriba.
    /// </summary>
    public static byte[] Egreso(Application.Receipts.Disbursements.EgresoModel m)
    {
        ArgumentNullException.ThrowIfNull(m);
        var detalle = $"{m.Kind} · fecha {m.OperationDate:yyyy-MM-dd} · caja {m.CashBox} · cajero {m.Cashier}"
                      + (m.Voided ? $" · ANULADO ({m.VoidingNumber})" : string.Empty);
        return Documento(m.Company, "Egreso de caja", m.Number, m.VerificationCode, $"Caja {m.CashBox}", $"Registrado {Hora(m.RegisteredAt)}", detalle,
            col =>
            {
                void Fila(string rotulo, string? valor)
                {
                    if (string.IsNullOrWhiteSpace(valor)) return;
                    col.Item().Row(r =>
                    {
                        r.ConstantItem(160).Text(rotulo).SemiBold();
                        r.RelativeItem().Text(valor);
                    });
                }
                Fila("Beneficiario", m.Beneficiary);
                Fila("Recibe", m.ReceivedBy);
                Fila("Producto", m.Product);
                Fila("Autorización del módulo", m.ModuleVoucher);
                Fila("Valor entregado", Pesos(m.Amount));
                if (m.Gmf is > 0m) Fila(m.GmfByOrigin ? "GMF (liquidado por el módulo)" : "GMF (asume la cooperativa)", Pesos(m.Gmf.Value));
                Fila("Motivo", m.Reason);
            },
            row => { Firma(row, "Entregó", m.Cashier); row.ConstantItem(40); Firma(row, "Recibí conforme", m.ReceivedBy ?? m.Beneficiary); });
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

    public byte[] Egreso(Application.Receipts.Disbursements.EgresoModel modelo) => DocumentosDeRecaudoReport.Egreso(modelo);
}
