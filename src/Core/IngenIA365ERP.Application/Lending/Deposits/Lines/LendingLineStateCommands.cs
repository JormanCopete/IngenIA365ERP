using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Lines;

// Feature 014, US1 (T065; api.md §3; data-model §1.1): cerrar una línea de captación o de aportes a cuentas nuevas y reabrirla (o activar la que
// nació en borrador). La ruta pide cualquiera de los dos «Manage»; el comando exige el de la naturaleza de la línea (Lending.Permission.Required).
// Las de crédito siguen por /api/lending/credit-lines (Lending.Line.UseCreditLinesRoute). (nuevo)

/// <summary>Lo común a cerrar y reabrir: la línea en alcance, de captación o aportes, y el permiso de su naturaleza. (nuevo)</summary>
internal static class EstadoDeLineasDeCaptacion
{
    public static async Task<Result<CreditLine>> LineaAsync(IApplicationDbContext db, IAlcanceDeCartera alcance, ICurrentUserPermissions permisos,
        LendingAuditEmitter auditoria, Guid publicId, CancellationToken ct)
    {
        var linea = await ConsultasDelCatalogo.VisibleAsync(db, alcance, permisos, auditoria, publicId, ct);
        if (linea.IsFailure) return linea;
        if (linea.Value.Nature == LineNature.Placement) return Result.Failure<CreditLine>(ErroresDeLineasDeCaptacion.UsarRutaDeCredito(linea.Value.Code));
        var permiso = PermisosDeLineas.AdministrarDe(linea.Value.Nature);
        return await PermisosDeLineas.TieneAsync(permisos, permiso, ct) ? linea : Result.Failure<CreditLine>(ErroresDeGobierno.PermisoRequerido(permiso));
    }
}

/// <summary>Cierra la línea a cuentas nuevas con motivo (api.md §3, <c>POST /{id}/close</c>). Las cuentas vivas siguen. (nuevo)</summary>
public sealed record CloseLendingLineCommand(Guid LinePublicId, string Reason) : IRequest<Result<LendingLineDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class CloseLendingLineCommandValidator : ValidadorConMotivo<CloseLendingLineCommand>
{
    public CloseLendingLineCommandValidator() => RuleFor(x => x.LinePublicId).NotEmpty();
}

/// <summary>Ya cerrada → <c>Lending.CreditLine.AlreadyClosed</c>. Quien cierra queda en <c>ClosedByUserId</c>.</summary>
public sealed class CloseLendingLineCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IAlcanceDeCartera alcance,
    ICurrentUserPermissions permisos,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<CloseLendingLineCommand, Result<LendingLineDto>>
{
    public async Task<Result<LendingLineDto>> Handle(CloseLendingLineCommand request, CancellationToken ct)
    {
        var encontrada = await EstadoDeLineasDeCaptacion.LineaAsync(db, alcance, permisos, auditoria, request.LinePublicId, ct);
        if (encontrada.IsFailure) return Result.Failure<LendingLineDto>(encontrada.Error);
        if (encontrada.Value.ValidarCierre() is { } problema) return Result.Failure<LendingLineDto>(ErroresDeLineasDeCaptacion.De(problema));
        var actor = await actorActual.ObtenerAsync(ct);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var linea = await db.CreditLines.FirstAsync(l => l.Id == encontrada.Value.Id, ct);
            linea.Cerrar(CreditLineClosedReason.Manual, request.Reason, actor.UserId, reloj.UtcNow);
            await db.SaveChangesAsync(ct);
            return Result.Success(await LectorDeLineasDeCaptacion.DtoAsync(db, linea, reloj.HoyLocal, null, ct));
        }, ct);
    }
}

/// <summary>
/// Reabre una línea cerrada o activa la que nació en borrador (api.md §3, <c>POST /{id}/reopen</c>), revalidando lo que pide estar activa: la entrega
/// desplegada, el perfil regulatorio vigente (captación, US1-1) y que no haya otra línea de aportes activa. (nuevo)
/// </summary>
public sealed record ReopenLendingLineCommand(Guid LinePublicId, string Reason) : IRequest<Result<LendingLineDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ReopenLendingLineCommandValidator : ValidadorConMotivo<ReopenLendingLineCommand>
{
    public ReopenLendingLineCommandValidator() => RuleFor(x => x.LinePublicId).NotEmpty();
}

/// <summary>
/// Ya activa → <c>Lending.CreditLine.NotClosed</c>; sin la entrega → <c>Lending.Savings.DeliveryNotAvailable</c>; captación sin perfil que autorice →
/// <c>Lending.Savings.LineNotAllowedByProfile</c>; aportes con otra activa → <c>Lending.Contributions.LineAlreadyActive</c>.
/// </summary>
public sealed class ReopenLendingLineCommandHandler(
    IApplicationDbContext db,
    IContextoDeLineasDeCaptacion contexto,
    IAlcanceDeCartera alcance,
    ICurrentUserPermissions permisos,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<ReopenLendingLineCommand, Result<LendingLineDto>>
{
    public async Task<Result<LendingLineDto>> Handle(ReopenLendingLineCommand request, CancellationToken ct)
    {
        var encontrada = await EstadoDeLineasDeCaptacion.LineaAsync(db, alcance, permisos, auditoria, request.LinePublicId, ct);
        if (encontrada.IsFailure) return Result.Failure<LendingLineDto>(encontrada.Error);
        var linea = encontrada.Value;
        if (linea.Status == CreditLineStatus.Active) return Result.Failure<LendingLineDto>(ErroresDeLineasDeCaptacion.De(linea.ValidarReapertura()!));
        if (EntregaDeLineas.NoDisponible(linea.Nature, contexto.Entrega) is { } sinEntrega) return Result.Failure<LendingLineDto>(sinEntrega);

        var hoy = reloj.HoyLocal;
        if (linea.Nature == LineNature.Deposit)
        {
            var perfil = await contexto.PerfilAsync(hoy, ct);
            var clase = await LectorDeLineasDeCaptacion.ClaseDeAsync(db, linea.Id, ct) ?? DepositClass.Demand;
            if (ReglasDeLineaDeCaptacion.ValidarClaseAutorizada(clase, perfil.AutorizaCaptar, perfil.TipoDeOrganizacion, perfil.PerfilPublicId) is { } noAutorizada)
                return Result.Failure<LendingLineDto>(ErroresDeLineasDeCaptacion.De(noAutorizada));
        }
        else if (await ReferenciasDeLineasDeAportes.OtraActivaAsync(db, linea.Id, ct) is { } otra)
        {
            return Result.Failure<LendingLineDto>(ErroresDeLineasDeCaptacion.AportesYaActiva(otra.Code, otra.Name));
        }

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var seguida = await db.CreditLines.FirstAsync(l => l.Id == linea.Id, ct);
            if (seguida.Status == CreditLineStatus.Closed) seguida.Reabrir();
            else seguida.Activar();
            await db.SaveChangesAsync(ct);
            return Result.Success(await LectorDeLineasDeCaptacion.DtoAsync(db, seguida, hoy, null, ct));
        }, ct);
    }
}
