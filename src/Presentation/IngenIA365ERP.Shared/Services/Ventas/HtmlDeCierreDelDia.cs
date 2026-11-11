using System.Globalization;
using System.Net;
using System.Text;

namespace IngenIA365ERP.Shared.Services.Ventas;

/// <summary>
/// El cierre del día impreso (pedido del dueño del 2026-11-11): lo mismo que muestra «Ver» en <c>/pos/cierre-del-dia</c> —por medio de
/// pago, tarjetas por adquirente y datáfono, y las sesiones— en una hoja para firmar, por <see cref="IImpresionDeDocumentos"/>. Sin
/// colores, para que salga igual en la térmica de 80 mm o en carta. Todo texto se codifica. (nuevo)
/// </summary>
public static class HtmlDeCierreDelDia
{
    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    public static string Titulo(DetalleDelCierreDelDiaDto d) =>
        $"Cierre del día {d.DayClose.PointOfSale.Code} {d.DayClose.OperatingDate:dd/MM/yyyy} v{d.DayClose.Version}";

    public static string Documento(DetalleDelCierreDelDiaDto d)
    {
        var c = d.DayClose;
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html lang=\"es\"><head><meta charset=\"utf-8\"><title>").Append(E(Titulo(d))).Append("</title><style>")
          .Append("@page{margin:4mm}body{margin:0;font-family:monospace;font-size:11px;max-width:190mm}")
          .Append("h1{font-size:14px;margin:0 0 2px}h2{font-size:12px;margin:8px 0 2px}")
          .Append("table{width:100%;border-collapse:collapse}th,td{padding:1px 2px;text-align:left;vertical-align:top}")
          .Append("th{border-bottom:1px solid}.n{text-align:right}tfoot td{border-top:1px solid;font-weight:bold}")
          .Append(".firma{margin-top:28px;border-top:1px solid;width:60%;padding-top:2px}")
          .Append("</style></head><body>");

        sb.Append("<h1>Cierre del día</h1>")
          .Append("<div>").Append(E($"{c.PointOfSale.Code} · {c.PointOfSale.Name}")).Append("</div>")
          .Append("<div>Fecha operativa: ").Append(c.OperatingDate.ToString("dd/MM/yyyy", Co)).Append(" · versión ").Append(c.Version).Append("</div>")
          .Append("<div>Estado: ").Append(E(TextosDeVentas.EstadoDeCierreDelDia(c.Status)))
          .Append(c.ReopenReason is { } m ? " — " + E(m) : string.Empty).Append("</div>")
          .Append("<div>Cerró: ").Append(E(c.ClosedByName)).Append(" · ").Append(c.ClosedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Co)).Append("</div>");

        sb.Append("<h2>Por medio de pago</h2><table><thead><tr><th>Medio</th><th class=\"n\">Esperado</th><th class=\"n\">Contado</th>")
          .Append("<th class=\"n\">Diferencia</th><th class=\"n\">Pagos</th></tr></thead><tbody>");
        foreach (var l in d.Lines)
            sb.Append("<tr><td>").Append(E(l.PaymentMeansCode)).Append("</td>").Append(N(l.Expected)).Append(N(l.Counted)).Append(N(l.Difference))
              .Append("<td class=\"n\">").Append(l.PaymentsCount).Append("</td></tr>");
        sb.Append("</tbody><tfoot><tr><td>Total</td>").Append(N(d.Lines.Sum(l => l.Expected))).Append(N(d.Lines.Sum(l => l.Counted)))
          .Append(N(d.Lines.Sum(l => l.Difference))).Append("<td class=\"n\">").Append(d.Lines.Sum(l => l.PaymentsCount)).Append("</td></tr></tfoot></table>");

        if (d.Cards.Count > 0)
        {
            sb.Append("<h2>Tarjetas por adquirente y datáfono</h2><table><thead><tr><th>Medio</th><th>Adquirente</th><th>Datáfono</th><th>Lotes</th>")
              .Append("<th class=\"n\">Esperado</th><th class=\"n\">Contado</th><th class=\"n\">Pagos</th></tr></thead><tbody>");
            foreach (var t in d.Cards)
                sb.Append("<tr><td>").Append(E(t.PaymentMeansCode)).Append("</td><td>").Append(E(t.AcquirerCode)).Append("</td><td>")
                  .Append(E(t.CardTerminalCode)).Append("</td><td>").Append(E(string.Join(", ", t.BatchNumbers))).Append("</td>")
                  .Append(N(t.Expected)).Append(N(t.Counted)).Append("<td class=\"n\">").Append(t.Count).Append("</td></tr>");
            sb.Append("</tbody></table>");
        }

        sb.Append("<h2>Sesiones (").Append(d.Sessions.Count).Append(")</h2><table><thead><tr><th>Caja</th><th>Cajero</th><th>Abrió</th><th>Cerró</th></tr></thead><tbody>");
        foreach (var s in d.Sessions)
            sb.Append("<tr><td>").Append(E(s.CashRegisterCode)).Append("</td><td>").Append(E(s.CashierName)).Append("</td><td>")
              .Append(s.OpenedAt.ToLocalTime().ToString("HH:mm", Co)).Append("</td><td>")
              .Append(s.ClosedAt?.ToLocalTime().ToString("HH:mm", Co) ?? string.Empty).Append("</td></tr>");
        sb.Append("</tbody></table>");

        sb.Append("<div class=\"firma\">Entrega</div><div class=\"firma\">Recibe / revisa</div>");
        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static string N(decimal v) => "<td class=\"n\">" + v.ToString("N0", Co) + "</td>";

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);
}
