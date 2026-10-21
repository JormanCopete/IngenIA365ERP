using System.Globalization;
using System.Text.RegularExpressions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Restrictions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Holds;

/// <summary>Lo retenido de una cuenta por un embargo (api.md §8, <c>retained[]</c>), con lo protegido y lo embargable del motor. (nuevo)</summary>
public sealed record GarnishmentRetentionDto(Guid AccountPublicId, string Number, string? DepositClass, int Order, decimal Balance, decimal Protected,
    decimal Garnishable, decimal Retained);

/// <summary>Una respuesta al juzgado (data-model §5.2.1). (nuevo)</summary>
public sealed record GarnishmentResponseDto(Guid PublicId, GarnishmentResponseKind Kind, DateOnly SentOn, decimal? RetainedAmount, decimal? ConsignedAmount,
    string? Text, Guid? AttachmentPublicId, DateTime RespondedAt)
{
    /// <summary>US13 (T196; research E176): el documento emitido de la respuesta (número y código de verificación); nulo si no se pudo emitir.</summary>
    public Guid? IssuedDocumentPublicId { get; init; }

    public string? IssuedDocumentNumber { get; init; }

    /// <summary>US13 (T196): el aviso si la respuesta quedó sin su documento.</summary>
    public string? Warning { get; init; }
}

/// <summary>
/// Un embargo (api.md §8, con lo de research E161): la persona, la autoridad, el juzgado como persona, el oficio, el valor, si es por alimentos, el estado,
/// lo retenido por cuenta, lo consignado, el límite inembargable que rigió, el plazo informativo de respuesta, las respuestas y los avisos. (nuevo)
/// </summary>
public sealed record GarnishmentDto(
    Guid PublicId,
    MemberAccountPersonDto Person,
    GarnishmentAuthority Authority,
    MemberAccountPersonDto Court,
    string CourtName,
    string ProcessNumber,
    string OfficeReference,
    DateOnly OfficeDate,
    DateOnly ReceivedOn,
    string ClaimantName,
    decimal? Amount,
    bool IsAlimony,
    GarnishmentAccountsScope AccountsScope,
    bool IncludesContributions,
    GarnishmentStatus Status,
    decimal RetainedTotal,
    decimal Consigned,
    decimal? UnembargableLimit,
    DateOnly? ResponseDueOn,
    bool ResponseOverdue,
    IReadOnlyList<GarnishmentRetentionDto> Retained,
    IReadOnlyList<GarnishmentResponseDto> Responses,
    IReadOnlyList<string> Explanation,
    IReadOnlyList<string> Warnings,
    DateTime? ReleasedAt,
    string? ReleaseOfficeReference);

/// <summary>Un bloqueo de una cuenta (data-model §5.1). (nuevo)</summary>
public sealed record AccountHoldDto(Guid PublicId, HoldReason Reason, decimal? Amount, bool Total, DateOnly ValidFrom, DateOnly? ValidTo, string? Notes,
    string? LoanNumber, Guid? GarnishmentPublicId, DateTime? ReleasedAt, string? ReleaseReason);

/// <summary>El borrador del texto de una respuesta al juzgado con la plantilla de la cooperativa (research E158). (nuevo)</summary>
public sealed record GarnishmentResponseDraftDto(GarnishmentResponseKind Kind, string Text, bool FromEntityTemplate);

/// <summary>Arma los DTO de los embargos y los textos de las respuestas. (nuevo)</summary>
public static partial class VistaDeEmbargos
{
    /// <summary>Los textos estándar de las respuestas (rigen si la cooperativa no fijó los suyos en <c>Embargos.PlantillasDeRespuesta</c>).</summary>
    public static readonly IReadOnlyDictionary<GarnishmentResponseKind, string> Estandar = new Dictionary<GarnishmentResponseKind, string>
    {
        [GarnishmentResponseKind.Acknowledgement] =
            "En atención al oficio {Oficio} del proceso {Proceso}, informamos que el {Fecha} registramos la medida de embargo sobre los depósitos de "
            + "{Embargado}, identificado con {Documento}, a favor de {Demandante}, por {Valor}.",
        [GarnishmentResponseKind.BalanceReport] =
            "En atención al oficio {Oficio} del proceso {Proceso}, informamos que de los depósitos de {Embargado} ({Documento}) quedan retenidos {Retenido}, "
            + "con este detalle: {Cuentas}. Lo demás corresponde al límite inembargable vigente.",
        [GarnishmentResponseKind.ContributionsNotGarnishable] =
            "En atención al oficio {Oficio} del proceso {Proceso}, informamos que los aportes sociales de {Embargado} ({Documento}) son inembargables "
            + "(Ley 79 de 1988, art. 49) y no se retuvieron.",
        [GarnishmentResponseKind.ConsignmentNotice] =
            "En atención al oficio {Oficio} del proceso {Proceso}, informamos que el {Fecha} consignamos {Consignado} retenidos a {Embargado} ({Documento}) "
            + "a la cuenta de depósitos judiciales de {Autoridad}.",
        [GarnishmentResponseKind.ReleaseAcknowledgement] =
            "En atención al oficio {Oficio} del proceso {Proceso}, informamos que el {Fecha} levantamos la medida de embargo sobre los depósitos de "
            + "{Embargado} ({Documento}).",
    };

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex Variable();

    /// <summary>Llena una plantilla con las variables (las desconocidas quedan como están).</summary>
    public static string Llenar(string plantilla, IReadOnlyDictionary<string, string> valores) =>
        Variable().Replace(plantilla, m => valores.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);

    public static string Pesos(decimal? v) => v is { } x ? x.ToString("C0", CultureInfo.GetCultureInfo("es-CO")) : "sin límite";

    /// <summary>El DTO del embargo con su último cálculo (si lo hay) y sus respuestas.</summary>
    public static async Task<GarnishmentDto> DtoAsync(IApplicationDbContext db, Garnishment g, EmbargoCalculado? calculo, decimal consignado, DateOnly hoy,
        IReadOnlyList<string> avisos, CancellationToken ct)
    {
        var personas = await LectorDeCuentas.PersonasAsync(db, [g.PersonId, g.CourtPersonId], ct);
        var bloqueos = await db.AccountHolds.AsNoTracking().Where(h => h.GarnishmentId == g.Id && h.ReleasedAt == null && !h.IsDeleted).ToListAsync(ct);
        var locales = db.AccountHolds.Local.Where(h => h.GarnishmentId == g.Id).ToList();
        var porId = locales.Where(h => h.Id != 0).ToDictionary(h => h.Id);
        var vivos = bloqueos.Select(b => porId.GetValueOrDefault(b.Id) ?? b).Concat(locales.Where(h => h.Id == 0)).Where(h => h.ReleasedAt is null).ToList();
        var ids = vivos.Select(h => h.MemberAccountId).Concat(calculo?.Cuentas.Select(c => c.CuentaId) ?? []).Distinct().ToList();
        var cuentas = await db.MemberAccounts.AsNoTracking().Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        var retenidas = calculo is not null
            ? calculo.Cuentas.Where(c => cuentas.ContainsKey(c.CuentaId)).Select(c => new GarnishmentRetentionDto(cuentas[c.CuentaId].PublicId,
                cuentas[c.CuentaId].Number, cuentas[c.CuentaId].DepositClass?.ToString(), c.Orden, c.Saldo, c.Protegido, c.Embargable, c.Retenido)).ToList()
            : vivos.GroupBy(h => h.MemberAccountId).Select((grupo, i) => new GarnishmentRetentionDto(cuentas[grupo.Key].PublicId, cuentas[grupo.Key].Number,
                cuentas[grupo.Key].DepositClass?.ToString(), i + 1, 0m, 0m, 0m, grupo.Sum(h => h.Amount ?? 0m))).ToList();
        var respuestas = await db.GarnishmentResponses.AsNoTracking().Where(r => r.GarnishmentId == g.Id && !r.IsDeleted).OrderBy(r => r.Id).ToListAsync(ct);
        var documentos = respuestas.Where(r => r.IssuedDocumentId is not null).Select(r => r.IssuedDocumentId!.Value).ToList();
        var emitidos = await db.IssuedLendingDocuments.AsNoTracking().Where(d => documentos.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => new { d.PublicId, d.Number }, ct);
        var vencida = g.Status == GarnishmentStatus.Active && g.ResponseDueOn is { } plazo && plazo < hoy && respuestas.Count == 0;
        IReadOnlyList<string> conAviso = vencida
            ? [.. avisos, $"{ErroresDeEmbargos.CodigoRespuestaVencida}: el plazo informativo de respuesta venció el {g.ResponseDueOn:yyyy-MM-dd} y no hay respuesta registrada."]
            : avisos;
        return new GarnishmentDto(g.PublicId, personas[g.PersonId], g.Authority, personas[g.CourtPersonId], g.CourtName, g.ProcessNumber, g.OrderNumber,
            g.OrderDate, g.ReceivedOn, g.ClaimantName, g.Amount, g.IsChildSupport, g.AccountsScope, g.IncludesContributions, g.Status,
            vivos.Sum(h => h.Amount ?? 0m), consignado, calculo?.LimiteAplicado, g.ResponseDueOn, vencida, retenidas,
            respuestas.Select(r => new GarnishmentResponseDto(r.PublicId, r.Kind, r.SentOn, r.RetainedAmount, r.ConsignedAmount, r.Text, r.AttachmentPublicId,
                r.RespondedAt)
            {
                IssuedDocumentPublicId = r.IssuedDocumentId is { } d && emitidos.TryGetValue(d, out var e1) ? e1.PublicId : null,
                IssuedDocumentNumber = r.IssuedDocumentId is { } d2 && emitidos.TryGetValue(d2, out var e2) ? e2.Number : null,
            }).ToList(),
            calculo?.Explicacion ?? [], conAviso, g.ReleasedAt, g.ReleaseOrderNumber);
    }

    /// <summary>Las variables de las plantillas de respuesta del embargo.</summary>
    public static async Task<Dictionary<string, string>> VariablesAsync(IApplicationDbContext db, Garnishment g, decimal retenido, decimal consignado,
        DateOnly fecha, IReadOnlyList<GarnishmentRetentionDto> cuentas, CancellationToken ct)
    {
        var personas = await LectorDeCuentas.PersonasAsync(db, [g.PersonId, g.CourtPersonId], ct);
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Oficio"] = g.OrderNumber,
            ["Proceso"] = g.ProcessNumber,
            ["Autoridad"] = string.IsNullOrWhiteSpace(g.CourtName) ? personas[g.CourtPersonId].FullName : g.CourtName,
            ["Embargado"] = personas[g.PersonId].FullName,
            ["Documento"] = personas[g.PersonId].DocumentNumber,
            ["Demandante"] = g.ClaimantName,
            ["Valor"] = Pesos(g.Amount),
            ["Retenido"] = Pesos(retenido),
            ["Consignado"] = Pesos(consignado),
            ["Fecha"] = fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["Cuentas"] = cuentas.Count == 0
                ? "ninguna cuenta con saldo embargable"
                : string.Join("; ", cuentas.Where(c => c.Retained > 0m).Select(c => $"cuenta {c.Number}: {Pesos(c.Retained)}")),
        };
    }
}
