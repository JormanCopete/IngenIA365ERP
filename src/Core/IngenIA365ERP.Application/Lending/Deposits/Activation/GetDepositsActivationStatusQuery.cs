using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Application.Lending.Deposits.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Activation;

/// <summary>La carga que la activación tomaría: la aplicada de la naturaleza o, si no hay, el borrador. (nuevo)</summary>
public sealed record DepositsActivationLoadDto(Guid PublicId, int Number, DateOnly CutoffDate, MigrationLoadStatus Status, bool IsBalanced, int Accounts);

/// <summary>El estado de la activación de una naturaleza (api.md §16, <c>GET /activation?nature=</c>). (nuevo)</summary>
public sealed record DepositsActivationStatusDto(
    LineNature Nature,
    string Status,
    DateOnly? ActivationDate,
    Guid? ActivationPublicId,
    DepositsActivationLoadDto? Load,
    DateOnly? ExpectedDate,
    DateOnly? Deadline,
    bool WithoutLoadAllowed,
    IReadOnlyList<RequisitoDeActivacionDto> Requirements,
    IReadOnlyList<AvisoDeActivacionDto> Warnings);

/// <summary>
/// El estado de la activación de aportes o de captaciones (feature 014, US9, T155; api.md §16, <c>GET /api/lending/deposits/activation?nature=</c>,
/// <c>Lending.Deposits.Activate</c>): activada (con su fecha y su activación) o sin activar, con la carga que tomaría —la aplicada o, si no hay, el
/// borrador—, la fecha esperada (corte + 1; sin carga, hoy), el plazo y los requisitos evaluados con esa carga. Sin ninguna carga, los requisitos son los de la
/// activación sin carga (FR-117). Toda la cooperativa, sin alcance. (nuevo)
/// </summary>
public sealed record GetDepositsActivationStatusQuery(LineNature Nature) : IRequest<Result<DepositsActivationStatusDto>>;

public sealed class GetDepositsActivationStatusQueryValidator : AbstractValidator<GetDepositsActivationStatusQuery>
{
    public GetDepositsActivationStatusQueryValidator() =>
        RuleFor(x => x.Nature).Must(n => n is LineNature.Deposit or LineNature.Contribution)
            .WithMessage("Indique la naturaleza: Deposit (captaciones) o Contribution (aportes).");
}

public sealed class GetDepositsActivationStatusQueryHandler(IApplicationDbContext db, RequisitosDeActivacionDeCaptaciones requisitos, IDateTimeService reloj)
    : IRequestHandler<GetDepositsActivationStatusQuery, Result<DepositsActivationStatusDto>>
{
    public const string Activada = "Activated";
    public const string SinActivar = "NotActivated";

    public async Task<Result<DepositsActivationStatusDto>> Handle(GetDepositsActivationStatusQuery request, CancellationToken ct)
    {
        var naturaleza = request.Nature;
        var hoy = reloj.HoyLocal;
        var fechas = await ActivacionesDeCaptacion.LeerAsync(db, ct);
        if (fechas.De(naturaleza) is { } fecha)
        {
            var activacion = await db.LendingActivations.AsNoTracking().Where(a => a.Nature == naturaleza)
                .Select(a => new { a.PublicId, a.MigrationLoadId }).FirstOrDefaultAsync(ct);
            var activadaCon = activacion?.MigrationLoadId is { } id
                ? await db.MigrationLoads.AsNoTracking().Where(l => l.Id == id)
                    .Select(l => new DepositsActivationLoadDto(l.PublicId, l.Number, l.CutoffDate, l.Status, l.IsBalanced, l.AccountCount)).FirstOrDefaultAsync(ct)
                : null;
            return Result.Success(new DepositsActivationStatusDto(naturaleza, Activada, fecha, activacion?.PublicId, activadaCon, fecha, null, false, [], []));
        }

        var carga = await db.MigrationLoads.AsNoTracking()
                        .Where(l => l.Nature == naturaleza && l.Status == MigrationLoadStatus.Applied && !l.IsDeleted).FirstOrDefaultAsync(ct)
                    ?? await db.MigrationLoads.AsNoTracking()
                        .Where(l => l.Nature == naturaleza && l.Status == MigrationLoadStatus.Draft && !l.IsDeleted).FirstOrDefaultAsync(ct);
        var esperada = carga?.CutoffDate.AddDays(1) ?? hoy;
        var evaluacion = await requisitos.EvaluarAsync(naturaleza, esperada, hoy, carga, ct);
        var avisos = evaluacion.Avisos.ToList();
        if (requisitos.EntregaNoDisponible(naturaleza) is { } entrega)
            avisos.Insert(0, new AvisoDeActivacionDto(entrega.Code, entrega.Message, null));
        return Result.Success(new DepositsActivationStatusDto(naturaleza, SinActivar, null, null,
            carga is null ? null : new DepositsActivationLoadDto(carga.PublicId, carga.Number, carga.CutoffDate, carga.Status, carga.IsBalanced, carga.AccountCount),
            esperada, carga?.ActivationDeadline, carga is null, evaluacion.Requisitos, avisos));
    }
}
