using System.Globalization;
using IngenIA365ERP.Application.Inventory.Cash;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports;

/// <summary>
/// El comprobante de un movimiento de caja (feature 012, I3, T626; contracts/api.md §21.3, §27): tipo, número, sesión, medio de origen y
/// de destino, destino, valor, motivo, denominaciones entregadas, quién aprobó, y las firmas de quien entrega y quien recibe. Media
/// carta: se imprime al entregar el dinero. (nuevo)
/// </summary>
public static class CashMovementReceiptReport
{
    public static byte[] Generate(CashMovementReceiptModel m)
    {
        ArgumentNullException.ThrowIfNull(m);
        var documento = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(new PageSize(PageSizes.Letter.Width, PageSizes.Letter.Height / 2));
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(m.Company.Name).Bold().FontSize(12);
                        if (!string.IsNullOrWhiteSpace(m.Company.Nit)) c.Item().Text($"NIT {m.Company.Nit}");
                    });
                    row.ConstantItem(230).AlignRight().Column(c =>
                    {
                        c.Item().Text(m.Kind).SemiBold().FontSize(11);
                        c.Item().Text($"Número {m.Number ?? "sin número"} · {m.Status}").Bold();
                        c.Item().Text($"Fecha {m.OperationDate:yyyy-MM-dd} · punto {m.PointOfSale} · caja {m.CashRegister}");
                    });
                });

                page.Content().PaddingTop(8).Column(col =>
                {
                    col.Item().Text($"Medio: {m.SourcePaymentMeans}{(m.TargetPaymentMeans is null ? string.Empty : $" → {m.TargetPaymentMeans}")}");
                    if (m.Destination is not null)
                        col.Item().Text($"Destino: {m.Destination}{(m.DestinationCashRegister is null ? string.Empty : $" (caja {m.DestinationCashRegister})")}");
                    col.Item().Text($"Valor: {CashCountReport.Pesos(m.Amount)}").Bold().FontSize(11);
                    col.Item().Text($"Motivo: {m.Reason}");
                    if (m.ApprovedBy is not null) col.Item().Text($"Aprobado por: {m.ApprovedBy}");
                    if (m.Denominations.Count > 0)
                    {
                        col.Item().PaddingTop(4).Text("Denominaciones").Bold();
                        foreach (var d in m.Denominations)
                            col.Item().Text($"{CashCountReport.Pesos(d.Value)} × {d.Quantity.ToString(CultureInfo.GetCultureInfo("es-CO"))} = {CashCountReport.Pesos(d.Amount)}");
                    }

                    col.Item().PaddingTop(30).Row(row =>
                    {
                        CashCountReport.Firma(row, "Entrega", m.DeliveredBy);
                        row.ConstantItem(40);
                        CashCountReport.Firma(row, m.ReceivedByLabel, string.Empty);
                    });
                });
            });
        });
        return documento.GeneratePdf();
    }
}
