using System.Globalization;
using System.Net;
using System.Text;

namespace IngenIA365ERP.Shared.Services.Ventas;

/// <summary>
/// La tirilla de una venta en HTML (feature 012, I3, T639; contracts/api.md §20.2 <c>TicketDto</c>): el mismo cuerpo lo pinta
/// <c>TirillaDeVenta</c> en pantalla y lo imprime <see cref="IImpresionDeDocumentos"/> como documento completo en un iframe, a 58 u 80 mm.
/// Todo texto se codifica. Sin colores: sale en la impresora térmica en negro. De la tarjeta sólo aparecen los últimos cuatro dígitos,
/// que es lo único que existe. (nuevo)
/// </summary>
public static class HtmlDeTirilla
{
    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    /// <summary>El ancho de impresión en milímetros: el del formato de la tirilla (58 u 80); la carta usa 190.</summary>
    public static int AnchoEnMilimetros(int formato) => formato is 58 or 80 ? formato : 190;

    /// <summary>El documento completo para imprimir.</summary>
    public static string Documento(TirillaDto t)
    {
        var ancho = AnchoEnMilimetros(t.Format);
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html lang=\"es\"><head><meta charset=\"utf-8\"><title>")
          .Append(E($"{t.Document.ClassLabel} {t.Document.Prefix}{t.Document.Number}"))
          .Append("</title><style>")
          .Append("@page{size:").Append(ancho).Append("mm auto;margin:2mm}")
          .Append("body{margin:0;font-family:monospace;font-size:").Append(ancho <= 58 ? "10" : "11").Append("px;width:").Append(ancho - 4).Append("mm}")
          .Append(".centro{text-align:center}.derecha{text-align:right}.fuerte{font-weight:bold}")
          .Append("table{width:100%;border-collapse:collapse}td{vertical-align:top;padding:0}hr{border:0;border-top:1px dashed}")
          .Append("</style></head><body>")
          .Append(Cuerpo(t))
          .Append("</body></html>");
        return sb.ToString();
    }

    /// <summary>El cuerpo (sin <c>html</c> ni estilos): lo que se pinta en pantalla.</summary>
    public static string Cuerpo(TirillaDto t)
    {
        var sb = new StringBuilder();
        var h = t.Header;
        sb.Append("<div class=\"centro\"><div class=\"fuerte\">").Append(E(h.CompanyName)).Append("</div>")
          .Append("<div>NIT ").Append(E(h.Nit)).Append("</div>")
          .Append("<div>").Append(E(h.BranchName)).Append("</div>");
        if (!string.IsNullOrWhiteSpace(h.Address)) sb.Append("<div>").Append(E(h.Address)).Append("</div>");
        sb.Append("<div>").Append(E(h.RegimeText)).Append("</div>");
        if (!string.IsNullOrWhiteSpace(h.ResolutionText)) sb.Append("<div>").Append(E(h.ResolutionText)).Append("</div>");
        if (t.Copy) sb.Append("<div class=\"fuerte\">COPIA</div>");
        sb.Append("</div><hr>");

        var d = t.Document;
        sb.Append("<div class=\"fuerte\">").Append(E(d.ClassLabel)).Append(' ').Append(E(d.Prefix)).Append(d.Number?.ToString(CultureInfo.InvariantCulture)).Append("</div>");
        if (d.IssuedAt is { } fecha) sb.Append("<div>").Append(E(fecha.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Co))).Append("</div>");
        if (!string.IsNullOrWhiteSpace(d.CashRegisterCode)) sb.Append("<div>Caja ").Append(E(d.CashRegisterCode)).Append(" · ").Append(E(d.CashierName)).Append("</div>");
        if (!string.IsNullOrWhiteSpace(d.SalespersonName)) sb.Append("<div>Vendedor ").Append(E(d.SalespersonName)).Append("</div>");
        sb.Append("<div>Cliente: ").Append(E(t.Party.Name)).Append("</div><div>").Append(E(t.Party.IdType)).Append(' ').Append(E(t.Party.IdNumber)).Append("</div><hr>");

        sb.Append("<table>");
        foreach (var l in t.Lines)
        {
            sb.Append("<tr><td colspan=\"2\">").Append(E(l.Description)).Append(' ').Append(E(l.TaxMark)).Append("</td></tr>")
              .Append("<tr><td>").Append(N(l.Quantity, 2)).Append(' ').Append(E(l.UnitCode)).Append(" x ").Append(N(l.UnitPrice)).Append("</td>")
              .Append("<td class=\"derecha\">").Append(N(l.Total)).Append("</td></tr>");
            if (l.Discount > 0m) sb.Append("<tr><td>Descuento</td><td class=\"derecha\">-").Append(N(l.Discount)).Append("</td></tr>");
        }
        sb.Append("</table><hr><table>");
        Fila(sb, "Subtotal", t.Totals.Subtotal);
        if (t.Totals.DiscountTotal > 0m) Fila(sb, "Descuentos", -t.Totals.DiscountTotal);
        foreach (var i in t.Taxes) Fila(sb, $"{i.Label} (base {N(i.Base)})", i.Amount);
        foreach (var r in t.Withholdings) Fila(sb, r.Label, -r.Amount);
        Fila(sb, "TOTAL", t.Totals.Total, fuerte: true);
        if (t.Totals.AmountDue != t.Totals.Total) Fila(sb, "A pagar", t.Totals.AmountDue, fuerte: true);
        sb.Append("</table><hr><table>");
        foreach (var p in t.Payments)
        {
            var detalle = p.Last4 is { Length: 4 } ? $"{p.MeansName} ****{p.Last4}" : p.MeansName;
            if (!string.IsNullOrWhiteSpace(p.Reference)) detalle += $" ref. {p.Reference}";
            Fila(sb, detalle, p.Amount);
        }
        if (t.Change > 0m) Fila(sb, "Vueltas", t.Change, fuerte: true);
        sb.Append("</table>");

        if (t.Electronic is { } el)
        {
            sb.Append("<hr><div>").Append(E(el.UniqueCodeKind)).Append(":</div><div style=\"word-break:break-all\">").Append(E(el.UniqueCode)).Append("</div>");
            if (!string.IsNullOrWhiteSpace(el.Legend)) sb.Append("<div class=\"fuerte centro\">").Append(E(el.Legend)).Append("</div>");
        }
        if (t.Footer.Count > 0)
        {
            sb.Append("<hr><div class=\"centro\">");
            foreach (var f in t.Footer) sb.Append("<div>").Append(E(f)).Append("</div>");
            sb.Append("</div>");
        }
        return sb.ToString();
    }

    private static void Fila(StringBuilder sb, string etiqueta, decimal valor, bool fuerte = false) =>
        sb.Append(fuerte ? "<tr class=\"fuerte\">" : "<tr>").Append("<td>").Append(E(etiqueta)).Append("</td><td class=\"derecha\">").Append(N(valor)).Append("</td></tr>");

    private static string N(decimal v, int decimales = 0) => v.ToString(decimales == 0 ? "N0" : "0.##", Co);

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);
}
