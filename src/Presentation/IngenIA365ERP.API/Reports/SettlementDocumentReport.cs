using System.Globalization;
using IngenIA365ERP.Application.Payroll.Services;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports;

/// <summary>
/// Documento de liquidación definitiva para firma (feature 010, FR-020, T066) sobre
/// <see cref="SettlementDocumentModel"/>: encabezado de la cooperativa, datos del contrato, cada
/// rubro con su base y sus días, las deducciones de ley, los descuentos con propuesto/aplicado y
/// motivo, el neto, lo omitido con su razón y el espacio de firmas. Un borrador sale con la marca
/// «BORRADOR». No calcula nada: pinta lo que la corrida guardó.
/// </summary>
public static class SettlementDocumentReport
{
    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    public static byte[] Generate(SettlementDocumentModel data) =>
        Document.Create(c => Pagina(c, data)).GeneratePdf();

    private static void Pagina(IDocumentContainer container, SettlementDocumentModel d)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(30);
            page.DefaultTextStyle(x => x.FontSize(9));

            if (d.IsDraft)
            {
                page.Background().AlignCenter().AlignMiddle().Rotate(-30).Text("BORRADOR").FontSize(72).Bold().FontColor(Colors.Grey.Lighten2);
            }

            page.Header().Column(col =>
            {
                col.Item().Text(d.CooperativeName).Bold().FontSize(14);
                if (!string.IsNullOrWhiteSpace(d.CooperativeTaxId))
                    col.Item().Text($"NIT: {d.CooperativeTaxId}").FontSize(8);
                col.Item().PaddingTop(5).AlignCenter().Text("LIQUIDACIÓN DEFINITIVA DE PRESTACIONES SOCIALES").FontSize(12).SemiBold();
                col.Item().AlignCenter().Text(d.IsDraft ? $"Borrador v{d.RunVersion} — sujeto a cambios hasta su aprobación" : $"Liquidación v{d.RunVersion}{(d.AccountingDocumentNumber is { } n ? $" · comprobante {n}" : string.Empty)}{(d.ApprovedAt is { } a ? $" · aprobada el {a:dd/MM/yyyy}" : string.Empty)}").FontSize(9);
                col.Item().PaddingBottom(5).LineHorizontal(1);
            });

            page.Content().PaddingVertical(5).Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text($"Trabajador: {d.EmployeeName}").SemiBold();
                        c.Item().Text($"Documento: {d.EmployeeDocumentType} {d.EmployeeDocument}");
                        if (!string.IsNullOrWhiteSpace(d.EmployeePosition)) c.Item().Text($"Cargo: {d.EmployeePosition}");
                        c.Item().Text($"Tipo de contrato: {d.ContractType}" + (d.ContractEndDate is { } fin ? $" (hasta {fin:dd/MM/yyyy})" : string.Empty));
                    });
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text($"Fecha de ingreso: {d.HireDate:dd/MM/yyyy}");
                        c.Item().Text($"Fecha de retiro: {d.TerminationDate:dd/MM/yyyy}");
                        c.Item().Text($"Tiempo de servicio: {d.DaysOfService} días (30/360)");
                        c.Item().Text($"Último salario base: {Pesos(d.BaseSalary)}");
                    });
                });
                col.Item().PaddingTop(4).Text($"Motivo del retiro: {d.ReasonName}" + (d.GeneratesSeverancePay ? " (genera indemnización)" : string.Empty)
                                                + (string.IsNullOrWhiteSpace(d.ReasonLegalBasis) ? string.Empty : $" — {d.ReasonLegalBasis}")).FontSize(9);

                col.Item().PaddingVertical(8).LineHorizontal(0.5f);

                Bloque(col, "RUBROS LIQUIDADOS", d.Earnings, d.TotalEarnings);
                col.Item().PaddingVertical(6);
                Bloque(col, "DEDUCCIONES DE LEY", d.Deductions, d.Deductions.Sum(l => l.Amount));

                if (d.PortfolioDeductions.Count > 0)
                {
                    col.Item().PaddingVertical(6);
                    col.Item().Text("DESCUENTOS POR OBLIGACIONES").Bold().FontSize(10);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(3);
                            cols.ConstantColumn(85);
                            cols.ConstantColumn(85);
                            cols.RelativeColumn(2);
                            cols.ConstantColumn(85);
                        });
                        table.Header(h =>
                        {
                            h.Cell().BorderBottom(1).Padding(2).Text("Obligación").Bold();
                            h.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Propuesto").Bold();
                            h.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Aplicado").Bold();
                            h.Cell().BorderBottom(1).Padding(2).Text("Motivo del ajuste").Bold();
                            h.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Saldo que queda").Bold();
                        });
                        foreach (var x in d.PortfolioDeductions)
                        {
                            table.Cell().Padding(2).Text(x.Description).FontSize(8);
                            table.Cell().Padding(2).AlignRight().Text(Pesos(x.Proposed)).FontSize(8);
                            table.Cell().Padding(2).AlignRight().Text(Pesos(x.Applied)).FontSize(8);
                            table.Cell().Padding(2).Text(x.Reason ?? string.Empty).FontSize(7);
                            table.Cell().Padding(2).AlignRight().Text(x.RemainingAfter is { } r ? Pesos(r) : "—").FontSize(8);
                        }
                    });
                    col.Item().BorderTop(1).PaddingTop(3).Row(row =>
                    {
                        row.RelativeItem().Text("TOTAL DESCUENTOS").Bold();
                        row.ConstantItem(95).AlignRight().Text(Pesos(d.PortfolioDeductions.Sum(x => x.Applied))).Bold();
                    });
                }

                col.Item().PaddingTop(10).Row(row =>
                {
                    row.RelativeItem().Text("NETO A PAGAR").Bold().FontSize(12);
                    row.ConstantItem(150).AlignRight().Text(Pesos(d.NetPay)).Bold().FontSize(12);
                });
                col.Item().Text($"Total devengado {Pesos(d.TotalEarnings)} · Total deducido {Pesos(d.TotalDeductions)}").FontSize(8);

                if (d.Omitted.Count > 0)
                {
                    col.Item().PaddingTop(8).Text("Rubros que no aplican y por qué").SemiBold().FontSize(9);
                    foreach (var o in d.Omitted) col.Item().Text("• " + o).FontSize(7);
                }

                col.Item().PaddingTop(12).Text("El trabajador declara recibir a satisfacción los valores aquí liquidados por concepto de salarios, prestaciones sociales e indemnizaciones a que hubiere lugar, sin perjuicio de las reclamaciones que la ley le reconoce.").FontSize(7).Italic();

                col.Item().PaddingTop(36).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().LineHorizontal(0.5f);
                        c.Item().AlignCenter().Text("Empleador").FontSize(8);
                        c.Item().AlignCenter().Text(d.CooperativeName).FontSize(7);
                    });
                    row.ConstantItem(30);
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().LineHorizontal(0.5f);
                        c.Item().AlignCenter().Text("Trabajador").FontSize(8);
                        c.Item().AlignCenter().Text($"{d.EmployeeName} · {d.EmployeeDocumentType} {d.EmployeeDocument}").FontSize(7);
                    });
                });
            });

            page.Footer().Row(row =>
            {
                row.RelativeItem().Text($"Generado el {d.GeneratedAt:dd/MM/yyyy HH:mm} UTC" + (d.ApprovedBy is { } q && !d.IsDraft ? $" · aprobada por {q}" : string.Empty)).FontSize(7);
                row.RelativeItem().AlignRight().Text(x =>
                {
                    x.DefaultTextStyle(s => s.FontSize(7));
                    x.Span("Página ");
                    x.CurrentPageNumber();
                    x.Span(" de ");
                    x.TotalPages();
                });
            });
        });
    }

    private static void Bloque(ColumnDescriptor col, string titulo, IReadOnlyList<SettlementDocumentLineModel> lineas, decimal total)
    {
        col.Item().Text(titulo).Bold().FontSize(10);
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(cols =>
            {
                cols.RelativeColumn(2);
                cols.RelativeColumn(3);
                cols.ConstantColumn(85);
                cols.ConstantColumn(45);
                cols.ConstantColumn(90);
            });
            table.Header(h =>
            {
                h.Cell().BorderBottom(1).Padding(2).Text("Concepto").Bold();
                h.Cell().BorderBottom(1).Padding(2).Text("Cómo se calculó").Bold();
                h.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Base").Bold();
                h.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Días").Bold();
                h.Cell().BorderBottom(1).Padding(2).AlignRight().Text("Valor").Bold();
            });
            if (lineas.Count == 0)
            {
                table.Cell().ColumnSpan(5).Padding(2).Text("Sin líneas.").FontSize(8).Italic();
            }
            foreach (var l in lineas)
            {
                table.Cell().Padding(2).Text($"{l.Name} ({l.Code})").FontSize(8);
                table.Cell().Padding(2).Text(l.Summary).FontSize(7);
                table.Cell().Padding(2).AlignRight().Text(l.BaseAmount is { } b ? Pesos(b) : string.Empty).FontSize(8);
                table.Cell().Padding(2).AlignRight().Text(l.Days is { } q ? q.ToString("0.##", Co) : string.Empty).FontSize(8);
                table.Cell().Padding(2).AlignRight().Text(Pesos(l.Amount)).FontSize(8);
            }
        });
        col.Item().BorderTop(1).PaddingTop(3).Row(row =>
        {
            row.RelativeItem().Text($"TOTAL {titulo}").Bold();
            row.ConstantItem(95).AlignRight().Text(Pesos(total)).Bold();
        });
    }

    private static string Pesos(decimal v) => v.ToString("N0", Co);
}

/// <summary><see cref="ISettlementDocumentRenderer"/> con QuestPDF. Registrado en la API, que es la única capa que conoce la librería.</summary>
public sealed class SettlementDocumentPdfRenderer : ISettlementDocumentRenderer
{
    public byte[] Render(SettlementDocumentModel document) => SettlementDocumentReport.Generate(document);
}
