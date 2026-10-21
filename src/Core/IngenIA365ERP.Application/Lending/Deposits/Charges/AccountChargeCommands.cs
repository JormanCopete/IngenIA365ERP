using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using IngenIA365ERP.Domain.Lending.Segregation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Charges;

// Feature 014, US4 (T116, T123; api.md §3, §4; FR-128, D35): los cargos causados de una cuenta —consultar, registrar uno por evento de pantalla, condonar,
// reversar y exonerar— y el catálogo de cargos de una línea. Causar, cobrar, condonar y reversar lo hace CargosDeCuentas (y el reverso, ReversionDeCuentas);
// aquí sólo el alcance, la segregación y la transacción. (nuevo)

/// <summary>La factura de Comercial de un cargo gravado (la informa Comercial, FR-128). (nuevo)</summary>
public sealed record AccountChargeInvoiceDto(string? Number, string? Status);

/// <summary>Un cargo causado (api.md §4, <c>GET /{id}/charges</c>). (nuevo)</summary>
public sealed record AccountChargeDto(
    Guid PublicId,
    ChargeKind Kind,
    string Code,
    decimal Amount,
    bool Vat,
    AccountChargeStatus Status,
    DateTime CausedAt,
    DateOnly? PeriodFrom,
    DateOnly? PeriodTo,
    string? OperationNumber,
    string? WaiverReason,
    AccountChargeInvoiceDto? Invoice);

/// <summary>Una exoneración individual (api.md §4, <c>POST /{id}/charges/exemption</c>). (nuevo)</summary>
public sealed record AccountChargeExemptionDto(Guid PublicId, string ChargeCode, DateOnly ValidFrom, DateOnly? ValidTo, string Reason);

/// <summary>Los cargos causados de una cuenta (<c>Lending.Accounts.Read</c>). (nuevo)</summary>
public sealed record ListAccountChargesQuery(Guid AccountPublicId, AccountChargeStatus? Status = null, int Page = 1, int PageSize = 50)
    : IRequest<Result<PagedResult<AccountChargeDto>>>;

public sealed class ListAccountChargesQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria)
    : IRequestHandler<ListAccountChargesQuery, Result<PagedResult<AccountChargeDto>>>
{
    public async Task<Result<PagedResult<AccountChargeDto>>> Handle(ListAccountChargesQuery request, CancellationToken ct)
    {
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: false, ct);
        if (leida.IsFailure) return Result.Failure<PagedResult<AccountChargeDto>>(leida.Error);
        var consulta = db.AccountCharges.AsNoTracking().Where(c => c.MemberAccountId == leida.Value.Id && !c.IsDeleted);
        if (request.Status is { } estado) consulta = consulta.Where(c => c.Status == estado);
        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.LongCountAsync(ct);
        var filas = await consulta.OrderByDescending(c => c.Id).Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        return Result.Success(new PagedResult<AccountChargeDto>(await CargosDeCuentaDtos.DeAsync(db, filas, ct), pagina.SafePage, pagina.SafePageSize, total));
    }
}

/// <summary>Arma los DTO de los cargos. (nuevo)</summary>
public static class CargosDeCuentaDtos
{
    public static async Task<IReadOnlyList<AccountChargeDto>> DeAsync(IApplicationDbContext db, IReadOnlyList<AccountCharge> filas, CancellationToken ct)
    {
        var ids = filas.Select(c => c.AppliedOperationId).Distinct().ToList();
        var operaciones = await db.MemberAccountOperations.AsNoTracking().Where(o => ids.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => new { o.Number, o.RegisteredAt }, ct);
        return filas.Select(c =>
        {
            var o = operaciones.GetValueOrDefault(c.AppliedOperationId);
            return new AccountChargeDto(c.PublicId, c.Kind, c.ChargeCode, c.Amount, c.IsVatTaxed, c.Status, o?.RegisteredAt ?? c.CreatedAt, c.PeriodFrom, c.PeriodTo,
                o?.Number, c.WaiverReason,
                c.IsVatTaxed ? new AccountChargeInvoiceDto(c.CommercialInvoiceNumber, c.CommercialInvoiceStatus) : null);
        }).ToList();
    }
}

/// <summary>
/// Un cargo por evento de pantalla (api.md §4, <c>POST /{id}/charges</c>, <c>Lending.Charges.Manage</c>, con clave): reposición de medio, certificado
/// adicional u otro cargo del catálogo de la versión vigente de la línea con disparador <c>Manual</c>, <c>ExtraDocument</c> o <c>MediumReplacement</c>.
/// Sin él en la línea, <c>Lending.Savings.ChargeNotInLine</c>; exonerado, <c>.ChargeExempt</c>. Quien lo registra no lo condona (segregación). (nuevo)
/// </summary>
public sealed record RegisterAccountChargeCommand(Guid AccountPublicId, string ChargeCode, string? EventReference, string Reason)
    : IRequest<Result<AccountChargeDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterAccountChargeCommandValidator : ValidadorConMotivo<RegisterAccountChargeCommand>
{
    public RegisterAccountChargeCommandValidator()
    {
        RuleFor(x => x.AccountPublicId).NotEmpty();
        RuleFor(x => x.ChargeCode).NotEmpty().MaximumLength(Domain.Entities.Lending.Deposits.Configuration.DepositLineCharge.LargoDelCodigo);
        RuleFor(x => x.EventReference).MaximumLength(100);
    }
}

public sealed class RegisterAccountChargeCommandHandler(
    IApplicationDbContext db,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    ReglasDeMovimientos movimientos,
    CargosDeCuentas cargos,
    RegistroDeIntervenciones intervenciones,
    IDateTimeService reloj) : IRequestHandler<RegisterAccountChargeCommand, Result<AccountChargeDto>>
{
    /// <summary>
    /// El expediente de la segregación de los cargos de una cuenta: quien radica un cargo no lo condona ni exonera los de la cuenta. US16 (T163): su raíz es
    /// <b>derivada</b> de la cuenta (<see cref="SegregacionDeCaptaciones.Raiz"/>); hasta entonces era la cuenta misma y quien la abrió no podía condonar.
    /// </summary>
    public const string Expediente = SegregacionDeCaptaciones.CargosDeCuenta;

    /// <summary>La raíz del expediente de los cargos de la cuenta.</summary>
    public static ExpedienteDeCartera ExpedienteDe(Guid cuenta) => new(Expediente, SegregacionDeCaptaciones.Raiz(Expediente, cuenta));

    private static readonly ChargeTrigger[] DePantalla = [ChargeTrigger.Manual, ChargeTrigger.ExtraDocument, ChargeTrigger.MediumReplacement];

    public async Task<Result<AccountChargeDto>> Handle(RegisterAccountChargeCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: false, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var cuenta = leida.Value;
        if (cuenta.Status is not (MemberAccountStatus.Active or MemberAccountStatus.Inactive)) return Falla(ErroresDeMovimientos.DestinoNoOperable(cuenta.Number, cuenta.Status));
        var codigo = request.ChargeCode.Trim().ToUpperInvariant();

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearCuentasAsync([cuenta.Id], ModoDeEspera.Esperar, ct);
            var c = await movimientos.LeerAsync(cuenta, hoy, ct);
            var def = (await cargos.CatalogoAsync(c.Version, DePantalla, ct)).FirstOrDefault(d => d.Code == codigo);
            if (def is null) return Falla(ErroresDeMovimientos.CargoNoEnLaLinea(codigo));
            var contexto = await cargos.ContextoAsync(c, def, 0m, hoy, hoy, 1, c.Disponible.Disponible, ct);
            var calculo = CargosDeCuenta.Calcular(def.Definicion(), contexto);
            if (!calculo.SeCausa) return Falla(ErroresDeMovimientos.CargoExonerado(codigo, calculo.Exoneracion, calculo.Explicacion));
            var radicada = await intervenciones.VerificarAsync(ExpedienteDe(cuenta.PublicId), AccionesDeSegregacion.Radicar, hoy, ct);
            if (radicada.IsFailure) return Falla(radicada.Error);
            var motivo = string.IsNullOrWhiteSpace(request.EventReference) ? request.Reason.Trim() : $"{request.Reason.Trim()} ({request.EventReference.Trim()})";
            var causado = await cargos.CausarAsync(c, new CargoPropuesto(def, calculo), hoy, null, def.Trigger.ToString(), motivo, ct);
            if (causado.IsFailure) return Falla(causado.Error);
            intervenciones.Registrar(radicada.Value, Expediente, causado.Value.Cargo.PublicId);
            await db.SaveChangesAsync(ct);
            return Result.Success((await CargosDeCuentaDtos.DeAsync(db, [causado.Value.Cargo], ct))[0]);
        }, ct);
    }

    private static Result<AccountChargeDto> Falla(Error e) => Result.Failure<AccountChargeDto>(e);
}

/// <summary>
/// Condona un cargo (api.md §4, <c>POST /{id}/charges/{chargeId}/waive</c>, <c>Lending.Charges.Waive</c>, con clave): el pendiente deja de deberse; el
/// cobrado se reintegra. Quien registró el cargo no lo condona (<c>Lending.Segregation.Conflict</c>). (nuevo)
/// </summary>
public sealed record WaiveAccountChargeCommand(Guid AccountPublicId, Guid ChargePublicId, string Reason)
    : IRequest<Result<AccountChargeDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class WaiveAccountChargeCommandValidator : ValidadorConMotivo<WaiveAccountChargeCommand>
{
    public WaiveAccountChargeCommandValidator()
    {
        RuleFor(x => x.AccountPublicId).NotEmpty();
        RuleFor(x => x.ChargePublicId).NotEmpty();
    }
}

public sealed class WaiveAccountChargeCommandHandler(
    IApplicationDbContext db,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    CargosDeCuentas cargos,
    RegistroDeIntervenciones intervenciones,
    IDateTimeService reloj) : IRequestHandler<WaiveAccountChargeCommand, Result<AccountChargeDto>>
{
    public async Task<Result<AccountChargeDto>> Handle(WaiveAccountChargeCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: false, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var cuenta = leida.Value;
        var existe = await db.AccountCharges.AsNoTracking().AnyAsync(c => c.PublicId == request.ChargePublicId && c.MemberAccountId == cuenta.Id && !c.IsDeleted, ct);
        if (!existe) return Falla(ErroresDeMovimientos.CargoInexistente());
        var decidida = await intervenciones.VerificarAsync(RegisterAccountChargeCommandHandler.ExpedienteDe(cuenta.PublicId),
            AccionesDeSegregacion.Decidir, hoy, ct);
        if (decidida.IsFailure) return Falla(decidida.Error);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearCuentasAsync([cuenta.Id], ModoDeEspera.Esperar, ct);
            var cargo = await db.AccountCharges.FirstAsync(c => c.PublicId == request.ChargePublicId, ct);
            var r = await cargos.CondonarAsync(cuenta, cargo, request.Reason, null, ct);
            if (r.IsFailure) return Falla(r.Error);
            intervenciones.Registrar(decidida.Value, RegisterAccountChargeCommandHandler.Expediente, cargo.PublicId);
            await db.SaveChangesAsync(ct);
            return Result.Success((await CargosDeCuentaDtos.DeAsync(db, [cargo], ct))[0]);
        }, ct);
    }

    private static Result<AccountChargeDto> Falla(Error e) => Result.Failure<AccountChargeDto>(e);
}

/// <summary>
/// Reversa un cargo (api.md §4, <c>POST /{id}/charges/{chargeId}/reverse</c>, <c>Lending.Charges.Manage</c>, con clave): reversa su operación <c>Charge</c>
/// por <see cref="ReversionDeCuentas"/> —que marca el cargo y, si estaba gravado, pide la nota crédito a Comercial (<c>CargoDeCaptacionReversado</c>)—. El
/// cargo cuya deuda ya cobró una consignación posterior no se reversa: se condona. (nuevo)
/// </summary>
public sealed record ReverseAccountChargeCommand(Guid AccountPublicId, Guid ChargePublicId, string Reason)
    : IRequest<Result<ComprobanteDeReversoDeCuentaDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ReverseAccountChargeCommandValidator : ValidadorConMotivo<ReverseAccountChargeCommand>
{
    public ReverseAccountChargeCommandValidator()
    {
        RuleFor(x => x.AccountPublicId).NotEmpty();
        RuleFor(x => x.ChargePublicId).NotEmpty();
    }
}

public sealed class ReverseAccountChargeCommandHandler(
    IApplicationDbContext db,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    ReversionDeCuentas reversion) : IRequestHandler<ReverseAccountChargeCommand, Result<ComprobanteDeReversoDeCuentaDto>>
{
    public async Task<Result<ComprobanteDeReversoDeCuentaDto>> Handle(ReverseAccountChargeCommand request, CancellationToken ct)
    {
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: false, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var cuenta = leida.Value;
        var cargo = await db.AccountCharges.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == request.ChargePublicId && c.MemberAccountId == cuenta.Id && !c.IsDeleted, ct);
        if (cargo is null) return Falla(ErroresDeMovimientos.CargoInexistente());
        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([cuenta.HolderPersonId], ModoDeEspera.Esperar, ct);
            await cerrojo.BloquearCuentasAsync([cuenta.Id], ModoDeEspera.Esperar, ct);
            var operacion = await db.MemberAccountOperations.FirstAsync(o => o.Id == cargo.AppliedOperationId, ct);
            var r = await reversion.ReversarAsync(operacion, request.Reason, EmisionDeCartera.ReversoIniciadoPorLaCartera, ct);
            return r.IsFailure ? Falla(r.Error) : Result.Success(r.Value.Comprobante);
        }, ct);
    }

    private static Result<ComprobanteDeReversoDeCuentaDto> Falla(Error e) => Result.Failure<ComprobanteDeReversoDeCuentaDto>(e);
}

/// <summary>
/// La exoneración individual de cargos de una cuenta (api.md §4, <c>POST /{id}/charges/exemption</c>, <c>Lending.Charges.Waive</c>, con clave; FR-128):
/// una fila por código con su vigencia y motivo; la vigente anterior del mismo código se cierra la víspera. La aprueba quien la registra, que no puede ser
/// quien radicó un cargo de la cuenta (<c>Lending.Segregation.Conflict</c>). (nuevo)
/// </summary>
public sealed record SetAccountChargeExemptionCommand(Guid AccountPublicId, IReadOnlyList<string> ChargeCodes, DateOnly ValidFrom, DateOnly? ValidTo, string Reason)
    : IRequest<Result<IReadOnlyList<AccountChargeExemptionDto>>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class SetAccountChargeExemptionCommandValidator : ValidadorConMotivo<SetAccountChargeExemptionCommand>
{
    public SetAccountChargeExemptionCommandValidator()
    {
        RuleFor(x => x.AccountPublicId).NotEmpty();
        RuleFor(x => x.ChargeCodes).NotEmpty();
        RuleForEach(x => x.ChargeCodes).NotEmpty().MaximumLength(Domain.Entities.Lending.Deposits.Configuration.DepositLineCharge.LargoDelCodigo);
        RuleFor(x => x.ValidTo).GreaterThanOrEqualTo(x => x.ValidFrom).When(x => x.ValidTo is not null)
            .WithMessage("La exoneración no termina antes de empezar.");
    }
}

public sealed class SetAccountChargeExemptionCommandHandler(
    IApplicationDbContext db,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    RegistroDeIntervenciones intervenciones,
    ReglasDeMovimientos movimientos,
    CargosDeCuentas cargos,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<SetAccountChargeExemptionCommand, Result<IReadOnlyList<AccountChargeExemptionDto>>>
{
    public async Task<Result<IReadOnlyList<AccountChargeExemptionDto>>> Handle(SetAccountChargeExemptionCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var leida = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId, seguir: false, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var cuenta = leida.Value;
        var c = await movimientos.LeerAsync(cuenta, request.ValidFrom < hoy ? hoy : request.ValidFrom, ct);
        var catalogo = (await cargos.CatalogoAsync(c.Version, Enum.GetValues<ChargeTrigger>(), ct)).Select(d => d.Code).ToHashSet();
        var codigos = request.ChargeCodes.Select(x => x.Trim().ToUpperInvariant()).Distinct().ToList();
        if (codigos.FirstOrDefault(x => !catalogo.Contains(x)) is { } ajeno) return Falla(ErroresDeMovimientos.CargoNoEnLaLinea(ajeno));
        var decidida = await intervenciones.VerificarAsync(RegisterAccountChargeCommandHandler.ExpedienteDe(cuenta.PublicId),
            AccionesDeSegregacion.Decidir, hoy, ct);
        if (decidida.IsFailure) return Falla(decidida.Error);
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } aprobador) return Falla(new Error(RegistroDeIntervenciones.CodigoSinPersona, "La exoneración la aprueba una persona."));

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearCuentasAsync([cuenta.Id], ModoDeEspera.Esperar, ct);
            var motivo = request.Reason.Trim();
            if (motivo.Length > AccountChargeExemption.LargoDelMotivo) motivo = motivo[..AccountChargeExemption.LargoDelMotivo];
            var nuevas = new List<AccountChargeExemption>();
            foreach (var codigo in codigos)
            {
                var vigentes = await db.AccountChargeExemptions
                    .Where(e => e.MemberAccountId == cuenta.Id && e.ChargeCode == codigo && !e.IsDeleted && (e.ValidTo == null || e.ValidTo >= request.ValidFrom))
                    .ToListAsync(ct);
                foreach (var v in vigentes)
                {
                    if (v.ValidFrom >= request.ValidFrom) v.IsDeleted = true;
                    else v.ValidTo = request.ValidFrom.AddDays(-1);
                }
                var nueva = new AccountChargeExemption
                {
                    MemberAccountId = cuenta.Id, ChargeCode = codigo, ValidFrom = request.ValidFrom, ValidTo = request.ValidTo, Reason = motivo,
                    ApprovedByUserId = aprobador,
                };
                db.AccountChargeExemptions.Add(nueva);
                nuevas.Add(nueva);
            }
            intervenciones.Registrar(decidida.Value, nameof(AccountChargeExemption), cuenta.PublicId);
            await db.SaveChangesAsync(ct);
            return Result.Success<IReadOnlyList<AccountChargeExemptionDto>>(nuevas
                .Select(e => new AccountChargeExemptionDto(e.PublicId, e.ChargeCode, e.ValidFrom, e.ValidTo, e.Reason)).ToList());
        }, ct);
    }

    private static Result<IReadOnlyList<AccountChargeExemptionDto>> Falla(Error e) => Result.Failure<IReadOnlyList<AccountChargeExemptionDto>>(e);
}

/// <summary>
/// El catálogo de cargos de una línea de captación en la versión vigente a <c>date</c> (api.md §3, <c>GET /lines/{id}/charges?date=</c>,
/// <c>Lending.DepositLines.View</c>). (nuevo)
/// </summary>
public sealed record ListDepositLineChargesQuery(Guid LinePublicId, DateOnly? Date = null) : IRequest<Result<IReadOnlyList<DepositChargeInput>>>;

public sealed class ListDepositLineChargesQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria, IDateTimeService reloj)
    : IRequestHandler<ListDepositLineChargesQuery, Result<IReadOnlyList<DepositChargeInput>>>
{
    public async Task<Result<IReadOnlyList<DepositChargeInput>>> Handle(ListDepositLineChargesQuery request, CancellationToken ct)
    {
        var linea = await LectorDeLineasDeCaptacion.LineaEnAlcanceAsync(db, alcance, auditoria, request.LinePublicId, seguir: false, ct);
        if (linea.IsFailure) return Result.Failure<IReadOnlyList<DepositChargeInput>>(linea.Error);
        var version = await LectorDeLineasDeCaptacion.VigenteDeCaptacionAsync(db, linea.Value.Id, request.Date ?? reloj.HoyLocal, null, ct);
        if (version is null) return Result.Success<IReadOnlyList<DepositChargeInput>>([]);
        return Result.Success<IReadOnlyList<DepositChargeInput>>((await LectorDeLineasDeCaptacion.CargosAsync(db, version.Id, ct)).Select(DepositChargeInput.De).ToList());
    }
}
