using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Application.Common.Integration.Treasury;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Treasury.Accounting;
using IngenIA365ERP.Application.Treasury.BankAccounts;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Treasury.LiquidityFund;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Entities.Treasury.Remittances;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Remittances;

/// <summary>Una remesa de efectivo, para la pantalla. (nuevo)</summary>
public sealed record CashRemittanceDto(Guid PublicId, string Number, RemittanceDirection Direction, Guid VaultPublicId, Guid BankAccountPublicId, string BankAccountCode,
    decimal Amount, string? BagSeal, Guid? MinutesAttachmentPublicId, string? SourceDocumentNumber, DateOnly DispatchDate, RemittanceState Status, DateOnly? ConfirmedOn,
    Guid? ConfirmedByStatementLinePublicId, int DaysInTransit);

/// <summary>
/// El puerto de la remesa que Tesorería ofrece a Recaudo (feature 015, US19, T0227; R-15; FR-035, FR-105; interfaces.md §2): <see cref="RegistrarAsync"/> deja la remesa de la
/// bóveda al banco en tránsito en la transacción del despacho, sin guardar y repitiendo sin efecto la misma clave; <see cref="RecibidaEnBovedaAsync"/> confirma la que va del
/// banco a la bóveda cuando ésta la recibe. Valida y audita sus escrituras (<c>LosPuertosDeRecaudoYTesoreriaValidanYAuditanSusEscrituras</c>). (nuevo)
/// </summary>
public sealed class RemesasDeTesoreria(IApplicationDbContext db, NumeradorDeTesoreria numerador, AuditoriaDeTesoreria auditoria, IDateTimeService reloj)
    : IRemesasDeTesoreria
{
    public const string ClaseDeRemesa = "RM";

    public async Task<RemesaRegistradaDto> RegistrarAsync(SolicitudDeRemesa solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        if (await db.CashRemittances.AsNoTracking().FirstOrDefaultAsync(r => r.IdempotencyKey == solicitud.IdempotencyKey, ct) is { } previa)
            return new RemesaRegistradaDto(previa.PublicId, previa.Number);
        var cuenta = await db.OwnBankAccounts.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == solicitud.OwnBankAccountPublicId, ct);
        var oficina = await db.Branches.AsNoTracking().Where(b => b.PublicId == solicitud.OfficePublicId).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct);
        ValidarRemesa(cuenta, oficina, solicitud.Amount, solicitud.DispatchDate);
        var remesa = new CashRemittance
        {
            Number = await numerador.AsignarAsync(ClaseDeRemesa, ClaseDeRemesa, ct), Direction = RemittanceDirection.VaultToBank, BranchId = oficina!.Value,
            VaultPublicId = solicitud.VaultPublicId ?? solicitud.SourceDocumentPublicId, BankAccountId = cuenta!.Id, Amount = solicitud.Amount, BagSeal = solicitud.BagSeal,
            MinutesAttachmentPublicId = solicitud.MinutesAttachmentPublicId, SourceDocumentPublicId = solicitud.SourceDocumentPublicId,
            SourceDocumentNumber = solicitud.SourceDocumentNumber, IdempotencyKey = solicitud.IdempotencyKey, DispatchDate = solicitud.DispatchDate, DispatchedAt = reloj.UtcNow,
        };
        db.CashRemittances.Add(remesa);
        await auditoria.AgregarAsync("Treasury.Remittance.Dispatched", "CashRemittance", remesa.PublicId, null,
            new { number = remesa.Number, account = cuenta.Code, amount = remesa.Amount, seal = remesa.BagSeal, source = remesa.SourceDocumentNumber }, ct);
        return new RemesaRegistradaDto(remesa.PublicId, remesa.Number);
    }

    public async Task<EstadoDeRemesaDto> EstadoAsync(Guid remittancePublicId, CancellationToken ct)
    {
        var r = await db.CashRemittances.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == remittancePublicId, ct)
                ?? throw new InvalidOperationException($"La remesa {remittancePublicId} no existe.");
        return new EstadoDeRemesaDto(r.PublicId, r.Number, r.Status.ToString(), r.ConfirmedOn);
    }

    public async Task<EstadoDeRemesaDto> RecibidaEnBovedaAsync(Guid remittancePublicId, Guid vaultPublicId, Guid vaultMovementPublicId, decimal amount, CancellationToken ct)
    {
        var r = await db.CashRemittances.FirstOrDefaultAsync(x => x.PublicId == remittancePublicId, ct)
                ?? throw new InvalidOperationException($"La remesa {remittancePublicId} no existe.");
        ValidarRecepcion(r, vaultPublicId, amount);
        if (r.Status == RemittanceState.InTransit)
        {
            r.Status = RemittanceState.Confirmed;
            r.ConfirmedAt = reloj.UtcNow;
            r.ConfirmedOn = reloj.HoyLocal;
            r.SourceDocumentPublicId ??= vaultMovementPublicId;
            await auditoria.AgregarAsync("Treasury.Remittance.ReceivedInVault", "CashRemittance", r.PublicId, null,
                new { number = r.Number, vault = vaultPublicId, movement = vaultMovementPublicId, amount }, ct);
        }
        return new EstadoDeRemesaDto(r.PublicId, r.Number, r.Status.ToString(), r.ConfirmedOn);
    }

    private static void ValidarRemesa(OwnBankAccount? cuenta, int? oficina, decimal valor, DateOnly fecha)
    {
        if (cuenta is null || oficina is null) throw new InvalidOperationException("La remesa cita una cuenta propia o una oficina que no existen.");
        if (!cuenta.Activa(fecha)) throw new InvalidOperationException($"La cuenta {cuenta.Code} no está activa: no recibe remesas.");
        if (valor <= 0m) throw new InvalidOperationException("La remesa tiene valor positivo.");
    }

    private static void ValidarRecepcion(CashRemittance r, Guid boveda, decimal valor)
    {
        if (r.Direction != RemittanceDirection.BankToVault) throw new InvalidOperationException($"La remesa {r.Number} no va del banco a la bóveda.");
        if (r.VaultPublicId != boveda) throw new InvalidOperationException($"La remesa {r.Number} es de otra bóveda.");
        if (r.Amount != valor) throw new InvalidOperationException($"La bóveda recibe {valor:N2} y la remesa {r.Number} es por {r.Amount:N2}.");
    }

    public static async Task<CashRemittanceDto> DtoAsync(IApplicationDbContext db, CashRemittance r, DateOnly hoy, CancellationToken ct)
    {
        var cuenta = await db.OwnBankAccounts.AsNoTracking().Where(c => c.Id == r.BankAccountId).Select(c => new { c.PublicId, c.Code }).FirstAsync(ct);
        return new CashRemittanceDto(r.PublicId, r.Number, r.Direction, r.VaultPublicId, cuenta.PublicId, cuenta.Code, r.Amount, r.BagSeal, r.MinutesAttachmentPublicId,
            r.SourceDocumentNumber, r.DispatchDate, r.Status, r.ConfirmedOn, r.ConfirmedByStatementLinePublicId,
            r.Status == RemittanceState.InTransit ? hoy.DayNumber - r.DispatchDate.DayNumber : 0);
    }
}

/// <summary>
/// <c>POST /api/treasury/remittances/{id}/confirm</c> (<c>Treasury.Remittances.Confirm</c>, con clave; FR-105): confirma la remesa de la bóveda al banco con la constancia del
/// banco o con la línea del extracto que la cruza, y emite <c>RemesaConfirmada</c> (<c>Banco</c> ← <c>EfectivoEnTransito</c>). (nuevo)
/// </summary>
public sealed record ConfirmRemittanceCommand(Guid RemittancePublicId, Guid? SupportAttachmentPublicId = null, Guid? StatementLinePublicId = null, DateOnly? ConfirmedOn = null)
    : IRequest<Result<CashRemittanceDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class ConfirmRemittanceCommandValidator : AbstractValidator<ConfirmRemittanceCommand>
{
    public ConfirmRemittanceCommandValidator() => RuleFor(x => x.RemittancePublicId).NotEmpty();
}

public sealed class ConfirmRemittanceCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    EmisionDeTesoreria emision,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<ConfirmRemittanceCommand, Result<CashRemittanceDto>>
{
    public async Task<Result<CashRemittanceDto>> Handle(ConfirmRemittanceCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<CashRemittanceDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        var leida = await db.CashRemittances.AsNoTracking().FirstOrDefaultAsync(r => r.PublicId == request.RemittancePublicId, ct);
        if (leida is null || !(await alcance.ObtenerAsync(ct)).IncluyeCuenta(leida.BankAccountId))
            return Result.Failure<CashRemittanceDto>(ErroresDeTesoreria.NoEncontrado("La remesa"));
        var hoy = reloj.HoyLocal;
        if (leida.Status == RemittanceState.Confirmed) return Result.Success(await RemesasDeTesoreria.DtoAsync(db, leida, hoy, ct));
        if (leida.Status != RemittanceState.InTransit || leida.Direction != RemittanceDirection.VaultToBank)
            return Result.Failure<CashRemittanceDto>(ErroresDeTesoreria.RemesaNoEnTransito(leida.Status.ToString()));
        if (request.SupportAttachmentPublicId is null && request.StatementLinePublicId is null) return Result.Failure<CashRemittanceDto>(ErroresDeTesoreria.RemesaSinSoporte());
        if (request.StatementLinePublicId is { } linea
            && !await db.TreasuryBankStatementLines.AsNoTracking().AnyAsync(l => l.PublicId == linea && l.BankAccountId == leida.BankAccountId, ct))
            return Result.Failure<CashRemittanceDto>(ErroresDeTesoreria.RemesaDeOtraCuenta());
        // Validación previa contable (FR-221): RemesaConfirmada tiene que tener su regla antes de confirmar.
        var cuentaPrevia = await db.OwnBankAccounts.AsNoTracking().FirstAsync(c => c.Id == leida.BankAccountId, ct);
        var previa = await emision.ValidacionPreviaAsync([RemesaConfirmadaV1.Type], cuentaPrevia, request.ConfirmedOn ?? hoy, ct);
        if (previa.IsFailure) return Result.Failure<CashRemittanceDto>(previa.Error);
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.CuentaDeOrigen, [leida.BankAccountId], ct: ct);
        var remesa = await db.CashRemittances.FirstAsync(r => r.Id == leida.Id, ct);
        var cuenta = await db.OwnBankAccounts.AsNoTracking().FirstAsync(c => c.Id == remesa.BankAccountId, ct);
        remesa.Status = RemittanceState.Confirmed;
        remesa.ConfirmedByUserId = usuario;
        remesa.ConfirmedAt = reloj.UtcNow;
        remesa.ConfirmedOn = request.ConfirmedOn ?? hoy;
        remesa.ConfirmationSupportAttachmentPublicId = request.SupportAttachmentPublicId;
        remesa.ConfirmedByStatementLinePublicId = request.StatementLinePublicId;
        await emision.RemesaConfirmadaAsync(cuenta, remesa, ct);
        await auditoria.AgregarAsync("Treasury.Remittance.Confirmed", "CashRemittance", remesa.PublicId, null,
            new { number = remesa.Number, on = remesa.ConfirmedOn, line = remesa.ConfirmedByStatementLinePublicId, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await RemesasDeTesoreria.DtoAsync(db, remesa, hoy, ct));
    }
}

/// <summary>
/// <c>POST /api/treasury/remittances/bank-to-vault</c> (<c>Treasury.Remittances.Confirm</c>, con clave; FR-035, FR-105, FR-207): Tesorería saca efectivo de una cuenta propia
/// para la bóveda de una oficina —desde una cuenta del fondo de liquidez exige un uso aprobado— y emite <c>RemesaConfirmada</c> (<c>EfectivoEnTransito</c> ← <c>Banco</c>); la
/// remesa queda en tránsito hasta que la bóveda la recibe con su movimiento (<c>RemittanceIn</c>, que la confirma por el puerto). (nuevo)
/// </summary>
public sealed record RegisterBankToVaultRemittanceCommand(Guid BankAccountPublicId, Guid VaultPublicId, Guid BranchPublicId, decimal Amount, string? BagSeal = null,
    Guid? MinutesAttachmentPublicId = null, Guid? LiquidityFundUsePublicId = null) : IRequest<Result<CashRemittanceDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterBankToVaultRemittanceCommandValidator : AbstractValidator<RegisterBankToVaultRemittanceCommand>
{
    public RegisterBankToVaultRemittanceCommandValidator()
    {
        RuleFor(x => x.BankAccountPublicId).NotEmpty();
        RuleFor(x => x.VaultPublicId).NotEmpty();
        RuleFor(x => x.BranchPublicId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.BagSeal).MaximumLength(40);
    }
}

public sealed class RegisterBankToVaultRemittanceCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    NumeradorDeTesoreria numerador,
    GuardaDelFondoDeLiquidez fondo,
    EmisionDeTesoreria emision,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<RegisterBankToVaultRemittanceCommand, Result<CashRemittanceDto>>
{
    public async Task<Result<CashRemittanceDto>> Handle(RegisterBankToVaultRemittanceCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<CashRemittanceDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        var hoy = reloj.HoyLocal;
        var cuenta = await db.OwnBankAccounts.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == request.BankAccountPublicId, ct);
        if (cuenta is null || !(await alcance.ObtenerAsync(ct)).IncluyeCuenta(cuenta.Id)) return Result.Failure<CashRemittanceDto>(ErroresDeTesoreria.NoEncontrado("La cuenta bancaria propia"));
        if (!cuenta.Activa(hoy)) return Result.Failure<CashRemittanceDto>(ErroresDeTesoreria.RemesaCuentaInvalida());
        var oficina = await db.Branches.AsNoTracking().Where(b => b.PublicId == request.BranchPublicId).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct);
        if (oficina is null) return Result.Failure<CashRemittanceDto>(ErroresDeTesoreria.NoEncontrado("La oficina"));
        // Validación previa contable (FR-221): RemesaConfirmada tiene que tener su regla antes de registrar la remesa.
        var previa = await emision.ValidacionPreviaAsync([RemesaConfirmadaV1.Type], cuenta, hoy, ct);
        if (previa.IsFailure) return Result.Failure<CashRemittanceDto>(previa.Error);
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.CuentaDeOrigen, [cuenta.Id], ct: ct);
        var uso = await fondo.ExigirAsync(cuenta, request.Amount, request.LiquidityFundUsePublicId, ct);
        if (uso.IsFailure) return Result.Failure<CashRemittanceDto>(uso.Error);
        var remesa = new CashRemittance
        {
            Number = await numerador.AsignarAsync(RemesasDeTesoreria.ClaseDeRemesa, RemesasDeTesoreria.ClaseDeRemesa, ct), Direction = RemittanceDirection.BankToVault,
            BranchId = oficina.Value, VaultPublicId = request.VaultPublicId, BankAccountId = cuenta.Id, Amount = request.Amount, BagSeal = request.BagSeal,
            MinutesAttachmentPublicId = request.MinutesAttachmentPublicId, IdempotencyKey = request.OperationKey, DispatchDate = hoy, DispatchedAt = reloj.UtcNow,
            LiquidityFundUseId = uso.Value?.Id,
        };
        db.CashRemittances.Add(remesa);
        if (uso.Value is { } u) GuardaDelFondoDeLiquidez.MarcarUsado(u, remesa.PublicId);
        await emision.RemesaConfirmadaAsync(cuenta, remesa, ct);
        await auditoria.AgregarAsync("Treasury.Remittance.BankToVault", "CashRemittance", remesa.PublicId, null,
            new { number = remesa.Number, account = cuenta.Code, amount = remesa.Amount, vault = remesa.VaultPublicId, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await RemesasDeTesoreria.DtoAsync(db, remesa, hoy, ct));
    }
}

/// <summary><c>GET /api/treasury/remittances?state=</c> (<c>Treasury.Remittances.View</c>): las remesas de las cuentas al alcance, con los días en tránsito. (nuevo)</summary>
public sealed record ListCashRemittancesQuery(RemittanceState? State = null) : IRequest<Result<IReadOnlyList<CashRemittanceDto>>>;

public sealed class ListCashRemittancesQueryHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance, IDateTimeService reloj)
    : IRequestHandler<ListCashRemittancesQuery, Result<IReadOnlyList<CashRemittanceDto>>>
{
    public async Task<Result<IReadOnlyList<CashRemittanceDto>>> Handle(ListCashRemittancesQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var consulta = db.CashRemittances.AsNoTracking();
        if (request.State is { } s) consulta = consulta.Where(r => r.Status == s);
        var filas = (await consulta.OrderByDescending(r => r.Id).Take(300).ToListAsync(ct)).Where(r => mio.IncluyeCuenta(r.BankAccountId)).ToList();
        var lista = new List<CashRemittanceDto>(filas.Count);
        foreach (var r in filas) lista.Add(await RemesasDeTesoreria.DtoAsync(db, r, reloj.HoyLocal, ct));
        return Result.Success<IReadOnlyList<CashRemittanceDto>>(lista);
    }
}
