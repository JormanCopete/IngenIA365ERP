using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.Application.Lending.Deposits.Accounts;

/// <summary>
/// 2026-11-24 (pedido del dueño): corrige la cuota de una cuenta de aportes —cuota, periodicidad, fecha del primer pago y canal de pago—
/// (<c>PUT /api/lending/accounts/{id}/contribution-installment</c>, <c>Lending.Accounts.Open</c>, con motivo y clave; la auditoría del comando guarda lo
/// nuevo y el motivo). Rige hacia adelante: las cuotas ya generadas no cambian, las que faltan salen con lo corregido. Si la cuenta ya tiene cuota propia
/// o la cooperativa la pide al abrir (<c>Aportes.OrigenDeLaCuota = DeLaCuenta</c>), se corrigen los cuatro datos; si no, sólo el canal. (nuevo)
/// </summary>
public sealed record SetContributionInstallmentCommand(Guid AccountPublicId, ContributionAccountInstallmentInput Input, string Reason)
    : IRequest<Result<MemberAccountDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class SetContributionInstallmentCommandValidator : ValidadorConMotivo<SetContributionInstallmentCommand>
{
    public SetContributionInstallmentCommandValidator()
    {
        RuleFor(x => x.AccountPublicId).NotEmpty();
        RuleFor(x => x.Input).NotNull();
    }
}

public sealed class SetContributionInstallmentCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    ReglasDeParametrosDeCartera parametros,
    IDateTimeService reloj) : IRequestHandler<SetContributionInstallmentCommand, Result<MemberAccountDto>>
{
    public async Task<Result<MemberAccountDto>> Handle(SetContributionInstallmentCommand request, CancellationToken ct)
    {
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: true, ct);
        if (leida.IsFailure) return Result.Failure<MemberAccountDto>(leida.Error);
        var cuenta = leida.Value;
        if (cuenta.Nature != LineNature.Contribution) return Result.Failure<MemberAccountDto>(CuotaPropiaDeAportes.SoloAportes());
        if (cuenta.AdmiteOperaciones() is { } cerrada) return Result.Failure<MemberAccountDto>(ErroresDeCuentas.De(cerrada));

        var hoy = reloj.HoyLocal;
        var todo = cuenta.ContributionInstallmentAmount is not null || await CuotaPropiaDeAportes.DeLaCuentaAsync(parametros, hoy, ct);
        if (CuotaPropiaDeAportes.Validar(request.Input, todo, cuenta.OpenedOn) is { IsFailure: true } mala)
            return Result.Failure<MemberAccountDto>(mala.Error);

        CuotaPropiaDeAportes.Aplicar(cuenta, request.Input);
        await db.SaveChangesAsync(ct);
        return Result.Success(await LectorDeCuentas.DtoAsync(db, cuenta, hoy, ct));
    }
}

/// <summary>
/// 2026-11-24: lo que pide hoy la apertura de una cuenta de aportes (<c>GET /api/lending/accounts/contribution-installment-mode</c>): el origen de la cuota
/// (<c>DeLaLinea</c> o <c>DeLaCuenta</c>) y los canales de pago que se ofrecen. (nuevo)
/// </summary>
public sealed record GetContributionInstallmentModeQuery : IRequest<Result<ContributionInstallmentModeDto>>;

public sealed class GetContributionInstallmentModeQueryHandler(ReglasDeParametrosDeCartera parametros, IDateTimeService reloj)
    : IRequestHandler<GetContributionInstallmentModeQuery, Result<ContributionInstallmentModeDto>>
{
    public async Task<Result<ContributionInstallmentModeDto>> Handle(GetContributionInstallmentModeQuery request, CancellationToken ct)
    {
        var deLaCuenta = await CuotaPropiaDeAportes.DeLaCuentaAsync(parametros, reloj.HoyLocal, ct);
        return Result.Success(new ContributionInstallmentModeDto(
            deLaCuenta ? Domain.Lending.Parameters.ParametrosDeCartera.CuotaDeLaCuenta : Domain.Lending.Parameters.ParametrosDeCartera.CuotaDeLaLinea,
            CuotaPropiaDeAportes.Canales));
    }
}
