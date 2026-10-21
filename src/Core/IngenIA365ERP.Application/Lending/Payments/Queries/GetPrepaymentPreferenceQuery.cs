using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using MediatR;

namespace IngenIA365ERP.Application.Lending.Payments.Queries;

/// <summary>Una versión de la preferencia: la elección (nula = revocada), desde cuándo y de dónde nació. (nuevo)</summary>
public sealed record PrepaymentPreferenceVersionDto(int Version, string? Choice, DateTime Since, string Origin);

/// <summary>La preferencia vigente y su historia (api.md §8.3). (nuevo)</summary>
public sealed record PrepaymentPreferenceDto(PrepaymentPreferenceVersionDto? Current, IReadOnlyList<PrepaymentPreferenceVersionDto> History);

/// <summary>
/// <c>GET /api/lending/loans/{id}/prepayment-preference</c> (feature 013, US5, T0554; api.md §8.3; <c>Lending.Loans.Read</c>): la preferencia vigente
/// (la última con <c>RegisteredAt ≤</c> ahora, T24, por <see cref="PreferenciaDePrepagoVigente"/>) y su historia, en el alcance de oficina y línea del
/// crédito. (nuevo)
/// </summary>
public sealed record GetPrepaymentPreferenceQuery(Guid LoanPublicId) : IRequest<Result<PrepaymentPreferenceDto>>;

public sealed class GetPrepaymentPreferenceQueryHandler(
    IApplicationDbContext db,
    PreferenciaDePrepagoVigente preferencias,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<GetPrepaymentPreferenceQuery, Result<PrepaymentPreferenceDto>>
{
    public async Task<Result<PrepaymentPreferenceDto>> Handle(GetPrepaymentPreferenceQuery request, CancellationToken ct)
    {
        var leido = await CreditoEnAlcance.LeerAsync(db, alcance, auditoria, request.LoanPublicId, ct);
        if (leido.IsFailure) return Result.Failure<PrepaymentPreferenceDto>(leido.Error);
        return Result.Success(Dto(await preferencias.HistoriaDelCreditoAsync(leido.Value.Id, ct), reloj.UtcNow));
    }

    public static PrepaymentPreferenceDto Dto(IReadOnlyList<PrepaymentPreference> historia, DateTime ahora)
    {
        var vigente = PrepaymentPreference.Vigente(historia, ahora);
        static PrepaymentPreferenceVersionDto Version(PrepaymentPreference p) =>
            new(p.VersionNumber, p.Choice?.ToString(), p.RegisteredAt, p.Origin.ToString());
        return new PrepaymentPreferenceDto(vigente is null ? null : Version(vigente),
            historia.OrderByDescending(p => p.RegisteredAt).ThenByDescending(p => p.VersionNumber).Select(Version).ToList());
    }
}
