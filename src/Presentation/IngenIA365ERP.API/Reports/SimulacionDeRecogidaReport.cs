using System.Globalization;
using IngenIA365ERP.Application.Lending.Payoffs;
using IngenIA365ERP.Domain.Enums.Lending;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using static IngenIA365ERP.API.Reports.EstadoDeCuentaDeLaPersonaReport;

namespace IngenIA365ERP.API.Reports;

/// <summary>
/// La simulación de la recogida en PDF (2026-11-27, pedido del dueño; feature 028, HU1-7, T017): con el membrete y las piezas de la simulación de
/// cancelación del estado de cuenta (<see cref="EstadoDeCuentaDeLaPersonaReport"/>: hoja, datos de la persona, avisos), la fecha proyectada de desembolso,
/// los créditos marcados con lo que cuesta recogerlos por concepto, lo que se recoge de cada uno, el total, el crédito nuevo, el neto a girar y las cuotas
/// que se liberan. Sin nada marcado lista todos los productos con lo que costaría recogerlos. Carta apaisada. No lee la base: todo sale del DTO. (nuevo)
/// </summary>
public static class SimulacionDeRecogidaReport
{
    public const string Titulo = "Simulación de recogida";

    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    public static byte[] Generate(PayoffCandidatesDto s, bool decimales, DateTimeOffset generadoEn)
    {
        ArgumentNullException.ThrowIfNull(s);
        var marcados = s.Products.Where(p => p.Selected).ToList();
        var todos = marcados.Count == 0;
        var filas = todos ? s.Products.ToList() : marcados;
        var logotipo = Logotipo(s.Letterhead?.LogoDataUri);
        return Document.Create(container => container.Page(page =>
            {
                Hoja(page, apaisado: true, s.Letterhead, logotipo, Titulo, $"Desembolso proyectado el {Fecha(s.Date)}", generadoEn);
                page.Content().PaddingVertical(4).Column(col =>
                {
                    if (s.Person is { } persona) DatosDeLaPersona(col, persona, "Desembolso proyectado", s.Date);
                    if (todos)
                        col.Item().PaddingTop(6).Text("No hay productos marcados: se listan todos con lo que costaría recogerlos.").FontSize(8).Italic();
                    Productos(col, filas, todos, decimales);
                    Resumen(col, s, decimales);
                    col.Item().PaddingTop(8).Text(
                            $"Valores calculados al {Fecha(s.Date)} con la información registrada hasta el "
                            + $"{HoraLocal(s.RecordedAsOf, generadoEn.Offset).ToString("dd/MM/yyyy HH:mm", Co)}, sin los pagos ni los movimientos que ocurran después. Es una "
                            + "simulación: al desembolsar se vuelve a calcular y puede cambiar.")
                        .FontSize(8).Italic();
                    // 2026-11-27 (feature 028, T051): el interés de cada producto renglón a renglón, con sus días (el proporcional por la ley ya dice su motivo).
                    Avisos(col, filas.SelectMany(p => p.Warnings.Select(a => $"{p.Number}: {a}"))
                        .Concat(filas.SelectMany(p => p.InterestExplanation.Select(r => $"{p.Number}: {r.Text}")))
                        // 2026-11-27 (feature 028, E2, T066): qué pasa con la cuota causada (enviada, dejada a su canal o incluida).
                        .Concat(filas.Where(p => p.CurrentInstallment?.Explanation is not null).Select(p => $"{p.Number}: {p.CurrentInstallment!.Explanation}"))
                        .Concat(filas.Where(p => p.InterestModeExplanation is not null
                                                 && !p.InterestExplanation.Any(r => r.Kind == ClasesDeRenglonDeInteres.Proporcional))
                            .Select(p => $"{p.Number}: {p.InterestModeExplanation}"))
                        .Concat(filas.Where(p => !p.Eligible && p.BlockMessage is not null).Select(p => $"{p.Number}: {p.BlockMessage}"))
                        .Concat(s.Summary.Warnings)
                        .Distinct().ToList());
                });
            }))
            .WithMetadata(Metadatos(s.Letterhead, Titulo, generadoEn))
            .GeneratePdf();
    }

    /// <summary>El nombre del archivo: <c>simulacion-de-recogida-{documento}-{aaaammdd}</c>.</summary>
    public static string NombreDelArchivo(PayoffCandidatesDto s) =>
        $"simulacion-de-recogida-{s.Person?.Documento ?? "deudor"}-{s.Date:yyyyMMdd}";

    private static void Productos(ColumnDescriptor col, IReadOnlyList<PayoffCandidateDto> filas, bool todos, bool decimales)
    {
        var conDevuelto = filas.Any(p => p.Breakdown.InterestReturned != 0m);
        var conCobrado = filas.Any(p => p.Breakdown.InterestCharged != 0m);
        var conAdministracion = filas.Any(p => p.Breakdown.Administration != 0m);
        var columnas = new List<(string Titulo, Func<PayoffCandidateDto, decimal> Valor)>
        {
            ("Capital", p => p.Breakdown.Principal),
            ("Interés corriente", p => p.Breakdown.CurrentInterest),
        };
        if (conDevuelto) columnas.Add(("Interés devuelto", p => -p.Breakdown.InterestReturned));
        if (conCobrado) columnas.Add(("Interés cobrado", p => p.Breakdown.InterestCharged));
        columnas.Add(("Mora", p => p.Breakdown.DefaultInterest));
        columnas.Add(("Seguro", p => p.Breakdown.Insurance));
        if (conAdministracion) columnas.Add(("Administración", p => p.Breakdown.Administration));
        columnas.Add(("Otros", p => p.Breakdown.OtherCharges));
        columnas.Add(("Anticipo", p => -p.Breakdown.Advance));
        columnas.Add(("Para recoger", p => p.ToPayOff));
        if (!todos) columnas.Add(("Se recoge", p => p.SelectedAmount ?? 0m));

        col.Item().PaddingTop(10).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2);
                c.RelativeColumn(3);
                c.RelativeColumn(2);
                foreach (var _ in columnas) c.RelativeColumn(2);
            });
            table.Header(h =>
            {
                h.Cell().Element(c => EncabezadoDeTabla(c, 7f)).Text("Crédito");
                h.Cell().Element(c => EncabezadoDeTabla(c, 7f)).Text("Línea");
                h.Cell().Element(c => EncabezadoDeTabla(c, 7f)).Text(todos ? "Estado" : "Modo");
                foreach (var (titulo, _) in columnas) h.Cell().Element(c => EncabezadoDeTabla(c, 7f)).AlignRight().Text(titulo);
            });
            foreach (var p in filas)
            {
                table.Cell().Element(x => Celda(x, 7f)).Text(p.Number);
                table.Cell().Element(x => Celda(x, 7f)).Text(p.Line);
                table.Cell().Element(x => Celda(x, 7f)).Text(todos ? (p.Eligible ? "Se puede recoger" : "No se puede recoger") : TextoDelModo(p.SelectedMode));
                foreach (var (_, valor) in columnas)
                    table.Cell().Element(x => Celda(x, 7f)).AlignRight().Text(Valor(valor(p), decimales));
            }
            if (!todos)
            {
                table.Cell().ColumnSpan(3).Element(x => EncabezadoDeTabla(x, 7f)).Text("Total");
                foreach (var (_, valor) in columnas)
                    table.Cell().Element(x => EncabezadoDeTabla(x, 7f)).AlignRight().Text(Valor(filas.Sum(valor), decimales));
            }
        });
        if (conDevuelto || filas.Any(p => p.Breakdown.Advance != 0m))
            col.Item().PaddingTop(2).Text("El interés devuelto y el anticipo se descuentan de lo que cuesta recoger.").FontSize(7).FontColor(Colors.Grey.Darken1);
    }

    private static void Resumen(ColumnDescriptor col, PayoffCandidatesDto s, bool decimales)
    {
        var r = s.Summary;
        col.Item().PaddingTop(10).AlignRight().Width(300).Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(6).Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(3);
                c.RelativeColumn(2);
            });
            void Par(string etiqueta, string valor, bool resaltar = false)
            {
                var e = table.Cell().PaddingVertical(1).Text(etiqueta).FontSize(8);
                var v = table.Cell().PaddingVertical(1).AlignRight().Text(valor).FontSize(8);
                if (resaltar) { e.SemiBold(); v.SemiBold(); }
            }
            if (s.NewLoanLine is { } linea) Par("Línea del crédito nuevo", linea);
            if (r.NewLoanAmount is { } monto) Par("Crédito nuevo", Valor(monto, decimales));
            Par("Total que se recoge", Valor(r.SelectedTotal, decimales));
            if (r.NetToDisburse is { } neto) Par("Neto a girar", Valor(neto, decimales), resaltar: true);
            if (r.NewInstallment is { } cuota) Par("Cuota del crédito nuevo", Valor(cuota, decimales));
            Par("Cuotas que se liberan", Valor(r.ReleasedInstallments, decimales));
        });
    }

    /// <summary>R (guardado en UTC) en la hora de la cooperativa, la misma de la fecha de generación.</summary>
    private static DateTimeOffset HoraLocal(DateTime utc, TimeSpan desfase) =>
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToOffset(desfase);

    private static string TextoDelModo(PayoffMode? modo) => modo switch
    {
        PayoffMode.Total => "Total",
        PayoffMode.Partial => "Parcial",
        _ => "—",
    };
}
