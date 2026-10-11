using System.Globalization;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports;

/// <summary>
/// La simulación de un crédito en PDF (feature 013, T0389; FR-034, T80; contracts/api.md §4.1) sobre <see cref="SimulacionParaImprimir"/>: la
/// cooperativa, la línea, la persona si se simuló para alguien, lo pedido, la tasa efectiva y la nominal del período, la efectiva con cargos, la
/// usura del día y si pasa, el plan cuota a cuota (capital, interés, seguros, otros, total y saldo), los totales y la versión de reglas. Lleva la
/// leyenda «Simulación – no constituye aprobación» y, fuera de producción, «AMBIENTE DE PRUEBAS – SIN VALIDEZ». <b>No lleva número ni código de
/// verificación</b>: no es un documento emitido. No calcula nada: pinta lo que devolvió el simulador. (nuevo)
/// </summary>
public static class SimulacionDeCreditoReport
{
    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    public static byte[] Generate(SimulacionParaImprimir datos) =>
        Document.Create(c => Pagina(c, datos)).GeneratePdf();

    private static void Pagina(IDocumentContainer container, SimulacionParaImprimir d)
    {
        var s = d.Simulacion;
        container.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(30);
            page.DefaultTextStyle(x => x.FontSize(9));

            if (d.AmbienteDePruebas)
                page.Background().AlignCenter().AlignMiddle().Rotate(-30)
                    .Text(SimulacionParaImprimir.MarcaDePruebas).FontSize(30).Bold().FontColor(Colors.Grey.Lighten2);

            page.Header().Column(col =>
            {
                col.Item().Text(d.Cooperativa).Bold().FontSize(14);
                col.Item().PaddingTop(5).AlignCenter().Text("SIMULACIÓN DE CRÉDITO").FontSize(12).SemiBold();
                col.Item().AlignCenter().Text(SimulacionParaImprimir.Leyenda).Bold().FontColor(Colors.Red.Darken2);
                if (d.AmbienteDePruebas)
                    col.Item().AlignCenter().Text(SimulacionParaImprimir.MarcaDePruebas).Bold().FontColor(Colors.Red.Darken2);
                col.Item().PaddingBottom(5).LineHorizontal(1);
            });

            page.Content().PaddingVertical(5).Column(col =>
            {
                col.Item().Text(t =>
                {
                    t.Span("Línea: ").SemiBold();
                    t.Span($"{s.CreditLineName} ({s.CreditLineCode})");
                });
                if (d.Persona is { } persona)
                    col.Item().Text(t =>
                    {
                        t.Span("Simulada para: ").SemiBold();
                        t.Span(persona);
                    });
                col.Item().Text(t =>
                {
                    t.Span("Fecha de la simulación: ").SemiBold();
                    t.Span(d.Fecha.ToString("dd 'de' MMMM 'de' yyyy", Co));
                    t.Span(" · Desembolso: ").SemiBold();
                    t.Span(s.DisbursementDate.ToString("dd/MM/yyyy", Co));
                });
                col.Item().Text(t =>
                {
                    t.Span("Monto: ").SemiBold();
                    t.Span(Pesos(s.Amount));
                    t.Span($" · {s.InstallmentCount.ToString(Co)} cuotas {Periodicidad(s.Periodicity)} ({s.TermMonths.ToString(Co)} meses) · ").SemiBold();
                    t.Span(Sistema(s.AmortizationSystem));
                });
                col.Item().PaddingTop(4).Text(t =>
                {
                    t.Span("Tasa efectiva anual: ").SemiBold();
                    t.Span(Porcentaje(s.EffectiveAnnualRate) + " EA");
                    t.Span(" · Nominal del período: ").SemiBold();
                    t.Span(Porcentaje(s.NominalPeriodRate));
                    t.Span(" · Efectiva con cargos: ").SemiBold();
                    t.Span(Porcentaje(s.EffectiveRateWithCharges) + " EA");
                });
                col.Item().Text(t =>
                {
                    t.Span("Usura vigente: ").SemiBold();
                    t.Span(s.Usury.Rate is { } u ? Porcentaje(u) + " EA" : "sin certificar");
                    t.Span(s.Usury.Passes ? " · la simulación está bajo la usura." : " · la simulación SUPERA la usura: así no se puede radicar.");
                });

                col.Item().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(28);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                        c.RelativeColumn(3);
                    });
                    table.Header(h =>
                    {
                        foreach (var titulo in new[] { "N.º", "Vence", "Capital", "Interés", "Seguros", "Otros", "Cuota", "Saldo" })
                            h.Cell().Element(Encabezado).Text(titulo);
                    });
                    foreach (var c in s.Installments)
                    {
                        table.Cell().Element(Celda).Text(c.Number.ToString(Co));
                        table.Cell().Element(Celda).Text(c.DueDate.ToString("dd/MM/yyyy", Co));
                        table.Cell().Element(Celda).AlignRight().Text(Pesos(c.Principal));
                        table.Cell().Element(Celda).AlignRight().Text(Pesos(c.Interest));
                        table.Cell().Element(Celda).AlignRight().Text(Pesos(c.Insurance));
                        table.Cell().Element(Celda).AlignRight().Text(Pesos(c.Other));
                        table.Cell().Element(Celda).AlignRight().Text(Pesos(c.Total));
                        table.Cell().Element(Celda).AlignRight().Text(Pesos(c.BalanceAfter));
                    }
                });

                col.Item().PaddingTop(6).Text(t =>
                {
                    t.Span("Totales: ").SemiBold();
                    t.Span($"capital {Pesos(s.Totals.Principal)} · interés {Pesos(s.Totals.Interest)} · seguros {Pesos(s.Totals.Insurance)} · " +
                           $"otros {Pesos(s.Totals.Other)} · total a pagar {Pesos(s.Totals.ToPay)}");
                });
                col.Item().PaddingTop(6).Text(
                        "Los valores son una proyección con las condiciones de la línea vigentes hoy. La tasa, los seguros y la usura se confirman al " +
                        "aprobar y al desembolsar el crédito.")
                    .FontSize(8).Italic();
            });

            page.Footer().Row(row =>
            {
                row.RelativeItem().Text($"Reglas {s.RulesVersion} · huella {s.SimulationSha256[..16]}…").FontSize(7).FontColor(Colors.Grey.Darken1);
                row.ConstantItem(120).AlignRight().Text(t =>
                {
                    t.Span("Página ").FontSize(7);
                    t.CurrentPageNumber().FontSize(7);
                    t.Span(" de ").FontSize(7);
                    t.TotalPages().FontSize(7);
                });
            });
        });
    }

    private static IContainer Encabezado(IContainer c) =>
        c.Background(Colors.Grey.Lighten3).Padding(3).DefaultTextStyle(x => x.SemiBold().FontSize(8));

    private static IContainer Celda(IContainer c) =>
        c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).DefaultTextStyle(x => x.FontSize(8));

    private static string Pesos(decimal valor) => "$ " + valor.ToString("#,0.00", Co);

    private static string Porcentaje(decimal fraccion) => (fraccion * 100m).ToString("0.####", Co) + " %";

    private static string Periodicidad(RepaymentPeriodicity p) => p switch
    {
        RepaymentPeriodicity.SinglePayment => "de pago único",
        RepaymentPeriodicity.Weekly => "semanales",
        RepaymentPeriodicity.TenDay => "decadales",
        RepaymentPeriodicity.Biweekly => "quincenales",
        RepaymentPeriodicity.Monthly => "mensuales",
        RepaymentPeriodicity.Bimonthly => "bimestrales",
        RepaymentPeriodicity.Quarterly => "trimestrales",
        RepaymentPeriodicity.SemiAnnual => "semestrales",
        RepaymentPeriodicity.Annual => "anuales",
        _ => p.ToString(),
    };

    private static string Sistema(AmortizationSystem s) => s switch
    {
        AmortizationSystem.LevelPayment => "cuota fija",
        AmortizationSystem.ConstantPrincipal => "abono constante a capital",
        AmortizationSystem.Bullet => "pago único",
        _ => s.ToString(),
    };
}

/// <summary>El <see cref="IRenderizadorDeSimulacion"/> de la API: QuestPDF sólo lo conoce esta capa (T0389). (nuevo)</summary>
public sealed class SimulacionDeCreditoPdfRenderer : IRenderizadorDeSimulacion
{
    public byte[] Render(SimulacionParaImprimir simulacion) => SimulacionDeCreditoReport.Generate(simulacion);
}
