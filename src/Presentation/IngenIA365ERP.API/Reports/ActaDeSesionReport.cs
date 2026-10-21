using System.Globalization;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Domain.Enums.Lending;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IngenIA365ERP.API.Reports;

/// <summary>
/// El acta de una sesión de un órgano de Cartera (feature 013, T0297, T64, T73, C48; contracts/api.md §5) sobre <see cref="ActaParaImprimir"/>:
/// órgano, número, fecha, clase y modalidad, asistentes con su papel y suplencias, quórum sellado, cada punto con su regla (mayoría o calificada
/// de vinculados), los excluidos con su motivo, los votos por persona (también los reemplazados, tachados), el conteo, el resultado, las
/// condiciones y la explicación del conteo, y al pie la huella SHA-256. Un acta sin cerrar sale con la marca «SIN CERRAR» y, fuera de
/// producción, toda acta con «AMBIENTE DE PRUEBAS – SIN VALIDEZ». No calcula nada: pinta lo que la sesión selló. (nuevo)
/// </summary>
public static class ActaDeSesionReport
{
    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    public static byte[] Generate(ActaParaImprimir datos) =>
        Document.Create(c => Pagina(c, datos)).GeneratePdf();

    private static void Pagina(IDocumentContainer container, ActaParaImprimir d)
    {
        var acta = d.Acta;
        container.Page(page =>
        {
            page.Size(PageSizes.Letter);
            page.Margin(30);
            page.DefaultTextStyle(x => x.FontSize(9));

            var marcas = new List<string>();
            if (d.SinCerrar) marcas.Add(ActaParaImprimir.MarcaSinCerrar);
            if (d.AmbienteDePruebas) marcas.Add(ActaParaImprimir.MarcaDePruebas);
            if (marcas.Count > 0)
                page.Background().AlignCenter().AlignMiddle().Rotate(-30).Column(c =>
                {
                    foreach (var marca in marcas)
                        c.Item().AlignCenter().Text(marca).FontSize(marca.Length > 12 ? 30 : 72).Bold().FontColor(Colors.Grey.Lighten2);
                });

            page.Header().Column(col =>
            {
                col.Item().Text(d.Cooperativa).Bold().FontSize(14);
                col.Item().PaddingTop(5).AlignCenter().Text($"ACTA {(acta.Number is { } n ? $"N.º {n}" : "SIN NUMERAR")} — {acta.Body.Name.ToUpper(Co)}")
                    .FontSize(12).SemiBold();
                col.Item().AlignCenter().Text(
                        $"Sesión {Clase(acta.Kind)} {Modalidad(acta.Mode)} del {acta.HeldOn.ToString("dd 'de' MMMM 'de' yyyy", Co)}"
                        + (string.IsNullOrWhiteSpace(acta.Location) ? string.Empty : $" · {acta.Location}"))
                    .FontSize(9);
                foreach (var marca in marcas)
                    col.Item().AlignCenter().Text(marca).Bold().FontColor(Colors.Red.Darken2);
                col.Item().PaddingBottom(5).LineHorizontal(1);
            });

            page.Content().PaddingVertical(5).Column(col =>
            {
                col.Item().Text("ASISTENCIA").Bold().FontSize(10);
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(4);
                        c.RelativeColumn(2);
                        c.RelativeColumn(2);
                    });
                    table.Header(h =>
                    {
                        h.Cell().Element(Encabezado).Text("Persona");
                        h.Cell().Element(Encabezado).Text("Papel");
                        h.Cell().Element(Encabezado).Text("Asistencia");
                    });
                    foreach (var a in acta.Attendees)
                    {
                        table.Cell().Element(Celda).Text(a.Person.FullName + (a.SubstitutesForMemberPublicId is null ? string.Empty : " (suplente)"));
                        table.Cell().Element(Celda).Text(a.Role is { } rol ? Papel(rol) : "Invitado");
                        table.Cell().Element(Celda).Text(Asistencia(a.Status));
                    }
                });

                if (acta.Quorum is { } q)
                    col.Item().PaddingTop(4).Text(
                        $"Quórum: {q.Present} presentes de {q.Composition} con voto; se requieren {q.Required}. {(q.Met ? "Hay quórum." : "No hay quórum.")}")
                        .SemiBold();

                foreach (var punto in acta.Items.OrderBy(i => i.Order))
                {
                    col.Item().PaddingTop(10).Text($"PUNTO {punto.Order}. {punto.Title}").Bold().FontSize(10);
                    if (!string.IsNullOrWhiteSpace(punto.Summary)) col.Item().Text(punto.Summary);
                    col.Item().Text($"{(punto.Kind == SessionItemKind.Decision ? "Decisión" : "Recomendación")} · regla: {Regla(punto.Rule)}");
                    if (punto.Excluded.Count > 0)
                        col.Item().Text("No votan: " + string.Join("; ", punto.Excluded.Select(e => $"{e.Person.FullName} ({Exclusion(e.Reason)})")));

                    if (punto.Votes.Count > 0)
                        col.Item().PaddingTop(3).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(4);
                                c.RelativeColumn(2);
                                c.RelativeColumn(3);
                                c.RelativeColumn(3);
                            });
                            table.Header(h =>
                            {
                                h.Cell().Element(Encabezado).Text("Persona");
                                h.Cell().Element(Encabezado).Text("Voto");
                                h.Cell().Element(Encabezado).Text("Forma");
                                h.Cell().Element(Encabezado).Text("Registrado");
                            });
                            foreach (var v in punto.Votes.OrderBy(v => v.At))
                            {
                                var reemplazado = v.ReplacedAt is not null;
                                table.Cell().Element(Celda).Text(t =>
                                {
                                    var s = t.Span(v.Person.FullName);
                                    if (reemplazado) s.Strikethrough();
                                });
                                table.Cell().Element(Celda).Text(t =>
                                {
                                    var s = t.Span(Voto(v.Vote));
                                    if (reemplazado) s.Strikethrough();
                                });
                                table.Cell().Element(Celda).Text(Forma(v.Method));
                                table.Cell().Element(Celda).Text(
                                    $"{v.At.ToString("dd/MM/yyyy HH:mm", Co)}{(v.RecordedBy is { } quien ? $" · {quien.Name}" : string.Empty)}"
                                    + (v.ReplacementReason is { Length: > 0 } motivo ? $" · reemplaza: {motivo}" : string.Empty));
                            }
                        });

                    if (punto.Tally is { } t)
                        col.Item().PaddingTop(3).Text(
                            $"A favor {t.For} · en contra {t.Against} · abstenciones {t.Abstain} · composición {t.Composition} · elegibles {t.Eligible} · requeridos {t.Required}");
                    if (punto.Outcome is { } resultado)
                        col.Item().Text($"Resultado: {Resultado(resultado)}").SemiBold();
                    if (!string.IsNullOrWhiteSpace(punto.Conditions)) col.Item().Text($"Condiciones: {punto.Conditions}");
                    if (!string.IsNullOrWhiteSpace(punto.Explanation)) col.Item().PaddingTop(2).Text(punto.Explanation).FontSize(8).Italic();
                }

                if (!string.IsNullOrWhiteSpace(acta.Notes))
                {
                    col.Item().PaddingTop(10).Text("OBSERVACIONES").Bold().FontSize(10);
                    col.Item().Text(acta.Notes);
                }

                if (acta.ClosedAt is { } cerrada)
                    col.Item().PaddingTop(10).Text(
                        $"Cerrada el {cerrada.ToString("dd/MM/yyyy HH:mm", Co)} (UTC){(acta.ClosedBy is { } quien ? $" por {quien.Name}" : string.Empty)}.");
            });

            page.Footer().Column(col =>
            {
                col.Item().LineHorizontal(0.5f);
                col.Item().Text(acta.ContentSha256 is { } huella
                    ? $"Huella SHA-256 del acta sellada: {huella}"
                    : $"Acta sin cerrar. Huella del contenido revisado: {acta.CurrentContentSha256 ?? "—"}").FontSize(7);
                col.Item().AlignRight().Text(x =>
                {
                    x.Span("Página ").FontSize(7);
                    x.CurrentPageNumber().FontSize(7);
                    x.Span(" de ").FontSize(7);
                    x.TotalPages().FontSize(7);
                });
            });
        });
    }

    private static IContainer Encabezado(IContainer c) =>
        c.Background(Colors.Grey.Lighten3).Padding(3).DefaultTextStyle(x => x.SemiBold().FontSize(8));

    private static IContainer Celda(IContainer c) =>
        c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(3).DefaultTextStyle(x => x.FontSize(8));

    private static string Clase(SessionKind k) => k == SessionKind.Extraordinary ? "extraordinaria" : "ordinaria";

    private static string Modalidad(SessionMode m) => m switch
    {
        SessionMode.Virtual => "virtual",
        SessionMode.Mixed => "mixta",
        _ => "presencial",
    };

    private static string Papel(BodyMemberRole r) => r switch
    {
        BodyMemberRole.Chair => "Presidente",
        BodyMemberRole.Secretary => "Secretario",
        BodyMemberRole.Alternate => "Suplente",
        _ => "Miembro",
    };

    private static string Asistencia(AttendanceStatus s) => s switch
    {
        AttendanceStatus.Present => "Presente",
        AttendanceStatus.Excused => "Excusado",
        _ => "Ausente",
    };

    private static string Regla(VotingRule r) => r == VotingRule.RelatedPartyQualified
        ? "mayoría calificada de vinculados (sobre la composición)"
        : "mayoría ordinaria";

    private static string Exclusion(string motivo) => motivo switch
    {
        "Applicant" => "solicitante",
        "Filer" => "radicó el asunto",
        "Interested" => "interesado",
        _ => motivo,
    };

    private static string Voto(VoteValue v) => v switch
    {
        VoteValue.For => "A favor",
        VoteValue.Against => "En contra",
        _ => "Abstención",
    };

    private static string Forma(VoteMethod m) => m switch
    {
        VoteMethod.RecordedBySecretary => "Registrado por secretaría",
        VoteMethod.OwnSession => "Desde su sesión",
        VoteMethod.InPersonPasskey => "Presencial con llave",
        _ => "Presencial con código",
    };

    private static string Resultado(CreditDecisionOutcome o) => o switch
    {
        CreditDecisionOutcome.Approve => "aprobado",
        CreditDecisionOutcome.ApproveWithConditions => "aprobado con condiciones",
        CreditDecisionOutcome.Deny => "no aprobado",
        _ => "devuelto",
    };
}

/// <summary><see cref="IRenderizadorDeActaDeSesion"/> con QuestPDF. Registrado en la API, que es la única capa que conoce la librería. (nuevo)</summary>
public sealed class ActaDeSesionPdfRenderer : IRenderizadorDeActaDeSesion
{
    public byte[] Render(ActaParaImprimir acta) => ActaDeSesionReport.Generate(acta);
}
