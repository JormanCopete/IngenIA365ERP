using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Accounts.GmfExemption;

// Feature 014, US2 (T075; api.md §10; data-model §3.8; FR-022, FR-036; R07): marcar y desmarcar la cuenta exenta del GMF, y la lista de marcas. Con la
// política «una cuenta marcada» (Gmf.PoliticaDeExencion, el defecto P25) cada persona tiene a lo sumo una marca vigente; con «tope por titular» o «no
// aplica» no hay marca (Lending.Gmf.PolicyWithoutMark). Cambiar la marca cierra la vigente y abre la nueva en el mismo comando. (nuevo)

/// <summary>La marca vigente de una persona con su cuenta. (nuevo)</summary>
public sealed record MarcaDeGmfVigente(GmfExemptAccount Marca, Guid AccountPublicId, string AccountNumber, DateOnly ValidFrom);

/// <summary>Lo común a marcar y desmarcar: la marca vigente de una persona. (nuevo)</summary>
public static class MarcasDeGmf
{
    public static async Task<MarcaDeGmfVigente?> VigenteAsync(IApplicationDbContext db, int personaId, CancellationToken ct, bool seguir = false)
    {
        var consulta = seguir ? db.GmfExemptAccounts : db.GmfExemptAccounts.AsNoTracking();
        var marca = await consulta.FirstOrDefaultAsync(g => g.PersonId == personaId && g.ValidTo == null && !g.IsDeleted, ct);
        if (marca is null) return null;
        var cuenta = await db.MemberAccounts.AsNoTracking().IgnoreQueryFilters().Where(a => a.Id == marca.MemberAccountId)
            .Select(a => new { a.PublicId, a.Number }).FirstAsync(ct);
        return new MarcaDeGmfVigente(marca, cuenta.PublicId, cuenta.Number, marca.ValidFrom);
    }
}

/// <summary>
/// Marca exenta del GMF una cuenta de ahorro del titular (api.md §10, <c>POST /api/lending/gmf-exemptions</c>, <c>Lending.GmfExemptions.Manage</c>, con
/// clave). Desde hoy o una fecha futura; si el titular ya tiene una vigente, <c>Lending.Gmf.AlreadyMarked</c> con la vigente, salvo que se pida
/// reemplazarla (<see cref="ReplaceCurrent"/>): entonces la vigente termina la víspera de la nueva. (nuevo)
/// </summary>
public sealed record MarkGmfExemptAccountCommand(Guid AccountPublicId, DateOnly? ValidFrom, string Reason, bool ReplaceCurrent = false)
    : IRequest<Result<GmfExemptAccountDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class MarkGmfExemptAccountCommandValidator : ValidadorConMotivo<MarkGmfExemptAccountCommand>
{
    public MarkGmfExemptAccountCommandValidator() => RuleFor(x => x.AccountPublicId).NotEmpty();
}

public sealed class MarkGmfExemptAccountCommandHandler(
    IApplicationDbContext db,
    ReglasDeParametrosDeCartera parametros,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<MarkGmfExemptAccountCommand, Result<GmfExemptAccountDto>>
{
    public async Task<Result<GmfExemptAccountDto>> Handle(MarkGmfExemptAccountCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var desde = request.ValidFrom ?? hoy;
        if (desde < hoy) return Falla(ErroresDeCuentas.MarcaRetroactiva(hoy));
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: false, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var cuenta = leida.Value;
        if (GmfExemptAccount.ValidarCuenta(cuenta) is { } noSirve) return Falla(ErroresDeCuentas.De(noSirve));
        if (await LectorDeCuentas.PoliticaDeGmfAsync(parametros, desde, ct) is var politica && politica != GmfExemptionPolicy.SingleMarkedAccount)
            return Falla(ErroresDeCuentas.PoliticaSinMarca(politica.ToString()));
        var usuario = (await actorActual.ObtenerAsync(ct)).UserId;

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            // FR-036: la marca es por persona; bajo su cerrojo, dos marcas a la vez no pasan las dos.
            await cerrojo.BloquearDeudoresAsync([cuenta.HolderPersonId], ModoDeEspera.Esperar, ct);
            if (await MarcasDeGmf.VigenteAsync(db, cuenta.HolderPersonId, ct, seguir: true) is { } vigente)
            {
                if (vigente.Marca.MemberAccountId == cuenta.Id || !request.ReplaceCurrent)
                    return Falla(ErroresDeCuentas.YaMarcada(vigente.AccountPublicId, vigente.AccountNumber, vigente.ValidFrom));
                vigente.Marca.Terminar(desde.AddDays(-1), $"Reemplazada por la cuenta {cuenta.Number}: {request.Reason.Trim()}", usuario);
                await db.SaveChangesAsync(ct);
            }

            var marca = GmfExemptAccount.Marcar(cuenta, desde, request.Reason, usuario);
            db.GmfExemptAccounts.Add(marca);
            await db.SaveChangesAsync(ct);
            return Result.Success((await ConsultasDeGmf.DtosAsync(db, [marca], ct))[0]);
        }, ct);
    }

    private static Result<GmfExemptAccountDto> Falla(Error e) => Result.Failure<GmfExemptAccountDto>(e);
}

/// <summary>
/// Termina la marca de exención (api.md §10, <c>POST /api/lending/gmf-exemptions/{id}/end</c>, <c>Lending.GmfExemptions.Manage</c>, con clave): el
/// último día exento es <see cref="ValidTo"/> (hoy si no viene; nunca antes de ayer). La cuenta que se marque después parte del acumulado exento del mes
/// de la persona (K8). (nuevo)
/// </summary>
public sealed record EndGmfExemptAccountCommand(Guid MarkPublicId, DateOnly? ValidTo, string Reason)
    : IRequest<Result<GmfExemptAccountDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class EndGmfExemptAccountCommandValidator : ValidadorConMotivo<EndGmfExemptAccountCommand>
{
    public EndGmfExemptAccountCommandValidator() => RuleFor(x => x.MarkPublicId).NotEmpty();
}

public sealed class EndGmfExemptAccountCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<EndGmfExemptAccountCommand, Result<GmfExemptAccountDto>>
{
    public async Task<Result<GmfExemptAccountDto>> Handle(EndGmfExemptAccountCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var hasta = request.ValidTo ?? hoy;
        if (hasta < hoy.AddDays(-1)) return Falla(ErroresDeCuentas.MarcaRetroactiva(hoy));
        var marca = await db.GmfExemptAccounts.FirstOrDefaultAsync(g => g.PublicId == request.MarkPublicId && !g.IsDeleted, ct);
        if (marca is null) return Falla(ErroresDeCuentas.MarcaInexistente());
        var cuenta = await db.MemberAccounts.AsNoTracking().FirstAsync(a => a.Id == marca.MemberAccountId, ct);
        var dentro = await alcance.AsegurarAsync(cuenta.BranchId, cuenta.CreditLineId, ErroresDeCuentas.MarcaInexistente(), auditoria,
            nameof(GmfExemptAccount), marca.PublicId, ct);
        if (dentro.IsFailure) return Falla(dentro.Error);
        if (marca.ValidTo is { } terminada) return Falla(ErroresDeCuentas.MarcaTerminada(terminada));
        if (hasta < marca.ValidFrom.AddDays(-1)) return Falla(ErroresDeCuentas.MarcaRetroactiva(hoy));

        var usuario = (await actorActual.ObtenerAsync(ct)).UserId;
        marca.Terminar(hasta, request.Reason, usuario);
        await db.SaveChangesAsync(ct);
        return Result.Success((await ConsultasDeGmf.DtosAsync(db, [marca], ct))[0]);
    }

    private static Result<GmfExemptAccountDto> Falla(Error e) => Result.Failure<GmfExemptAccountDto>(e);
}

/// <summary>
/// Las marcas de exención del GMF (api.md §10, <c>GET /api/lending/gmf-exemptions</c>, <c>Lending.Accounts.Read</c>): de una persona, vigentes o todas,
/// sólo las de cuentas de la oficina y la línea del alcance. (nuevo)
/// </summary>
public sealed record ListGmfExemptAccountsQuery(Guid? PersonPublicId = null, bool? Active = null) : IRequest<Result<IReadOnlyList<GmfExemptAccountDto>>>;

public sealed class ListGmfExemptAccountsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion)
    : IRequestHandler<ListGmfExemptAccountsQuery, Result<IReadOnlyList<GmfExemptAccountDto>>>
{
    public async Task<Result<IReadOnlyList<GmfExemptAccountDto>>> Handle(ListGmfExemptAccountsQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var cuentas = db.MemberAccounts.AsNoTracking().Where(a => !a.IsDeleted).PorOficinaYLinea(alcance, a => a.BranchId, a => a.CreditLineId)
            .Select(a => a.Id);
        var consulta = db.GmfExemptAccounts.AsNoTracking().Where(g => !g.IsDeleted && cuentas.Contains(g.MemberAccountId));
        if (request.PersonPublicId is { } persona) consulta = consulta.Where(g => db.People.Any(p => p.Id == g.PersonId && p.PublicId == persona));
        if (request.Active is true) consulta = consulta.Where(g => g.ValidTo == null);
        if (request.Active is false) consulta = consulta.Where(g => g.ValidTo != null);
        var filas = await consulta.OrderByDescending(g => g.ValidFrom).ThenByDescending(g => g.Id).Take(500).ToListAsync(ct);
        return Result.Success(await ConsultasDeGmf.DtosAsync(db, filas, ct));
    }
}

/// <summary>Arma los DTO de las marcas. (nuevo)</summary>
public static class ConsultasDeGmf
{
    public static async Task<IReadOnlyList<GmfExemptAccountDto>> DtosAsync(IApplicationDbContext db, IReadOnlyList<GmfExemptAccount> marcas, CancellationToken ct)
    {
        var personas = await LectorDeCuentas.PersonasAsync(db, marcas.Select(m => m.PersonId), ct);
        var idsDeCuenta = marcas.Select(m => m.MemberAccountId).Distinct().ToList();
        var cuentas = await db.MemberAccounts.AsNoTracking().IgnoreQueryFilters().Where(a => idsDeCuenta.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => new { a.PublicId, a.Number }, ct);
        var usuarios = await ReferenciasDeGobierno.UsuariosAsync(db, marcas.Select(m => m.MarkedByUserId), ct);
        return marcas.Select(m => new GmfExemptAccountDto(m.PublicId, personas[m.PersonId], cuentas[m.MemberAccountId].PublicId,
            cuentas[m.MemberAccountId].Number, m.ValidFrom, m.ValidTo, ReferenciasDeGobierno.Usuario(usuarios, m.MarkedByUserId)?.Name, m.MarkReason,
            m.EndReason)).ToList();
    }
}
