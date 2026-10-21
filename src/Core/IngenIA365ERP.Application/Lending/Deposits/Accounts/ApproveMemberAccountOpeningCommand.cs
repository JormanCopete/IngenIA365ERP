using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Segregation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Accounts;

/// <summary>
/// Aprueba la apertura retenida de una cuenta (feature 014, US2, T073; api.md §4, <c>POST /api/lending/accounts/{id}/approve-opening</c>,
/// <c>Lending.Accounts.Open</c>, con clave; FR-005, FR-032, US2-2): la cuenta en apertura (<c>Lending.Savings.NotOpening</c>), aprobada por otra persona
/// que quien la abrió (<c>Lending.Segregation.Conflict</c>, la regla fija radicar × decidir) y sin coincidencias en listas sin liberar —sólo el oficial de
/// cumplimiento las libera, en «Listas restrictivas», con motivo (<c>Lending.Screening.Blocked</c>)—. Pasa a <c>Active</c>, salvo que todavía espere el
/// depósito de apertura de la línea, que llega por el contrato de cuentas: entonces sigue en apertura, ya aprobada. (nuevo)
/// </summary>
public sealed record ApproveMemberAccountOpeningCommand(Guid AccountPublicId, string Reason) : IRequest<Result<MemberAccountDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ApproveMemberAccountOpeningCommandValidator : ValidadorConMotivo<ApproveMemberAccountOpeningCommand>
{
    public ApproveMemberAccountOpeningCommandValidator() => RuleFor(x => x.AccountPublicId).NotEmpty();
}

public sealed class ApproveMemberAccountOpeningCommandHandler(
    IApplicationDbContext db,
    RegistroDeIntervenciones intervenciones,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<ApproveMemberAccountOpeningCommand, Result<MemberAccountDto>>
{
    public async Task<Result<MemberAccountDto>> Handle(ApproveMemberAccountOpeningCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: true, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var cuenta = leida.Value;
        if (cuenta.Status != MemberAccountStatus.Opening) return Falla(ErroresDeCuentas.NoEnApertura(cuenta.Status));

        var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(OpenMemberAccountCommandHandler.Expediente,
            SegregacionDeCaptaciones.Raiz(OpenMemberAccountCommandHandler.Expediente, cuenta.PublicId)),
            AccionesDeSegregacion.Decidir, hoy, ct);
        if (verificada.IsFailure) return Falla(verificada.Error);

        var personas = await db.MemberAccountParties.AsNoTracking()
            .Where(p => p.MemberAccountId == cuenta.Id && p.ValidTo == null && !p.IsDeleted
                        && (p.Role == MemberAccountPartyRole.Holder || p.Role == MemberAccountPartyRole.CoHolder))
            .Select(p => p.PersonId).ToListAsync(ct);
        if (!personas.Contains(cuenta.HolderPersonId)) personas.Add(cuenta.HolderPersonId);
        var coincidencias = await LectorDeCuentas.CoincidenciasSinLiberarAsync(db, personas, ct);
        if (coincidencias.Count > 0) return Falla(ErroresDeCuentas.ListasBloquean(coincidencias));

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearCuentasAsync([cuenta.Id], ModoDeEspera.Esperar, ct);
            if (!await LectorDeCuentas.DepositoInicialPendienteAsync(db, cuenta, hoy, ct)) cuenta.Activar(hoy, reloj.UtcNow);
            intervenciones.Registrar(verificada.Value, OpenMemberAccountCommandHandler.Expediente, cuenta.PublicId);
            await db.SaveChangesAsync(ct);
            return Result.Success(await LectorDeCuentas.DtoAsync(db, cuenta, hoy, ct));
        }, ct);
    }

    private static Result<MemberAccountDto> Falla(Error e) => Result.Failure<MemberAccountDto>(e);
}
