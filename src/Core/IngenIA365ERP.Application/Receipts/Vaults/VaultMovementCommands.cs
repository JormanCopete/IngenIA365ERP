using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.Accounting;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Sessions;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Receipts.Vaults;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Vaults;

// Feature 015, US2 (T0094; FR-035, FR-036, FR-182; contracts/api.md §3; data-model §2.6): los movimientos propios de la bóveda —recibir una remesa que vuelve,
// el conteo, el billete retenido con acta— y su saldo por denominación, una proyección que el cuadre diario compara con la suma de los movimientos.
// Abastecer y recibir de una caja nacen del movimiento de la caja (RegisterCashMovementCommand). La remesa al banco es de Tesorería (fase 7). (nuevo)

/// <summary>Lo común de los movimientos de bóveda. (nuevo)</summary>
public static class MovimientosDeBoveda
{
    /// <summary>El prefijo de la numeración de los movimientos de bóveda (otra fila de <c>COR_Sequences</c> del tipo <c>MC</c>; DT §2.7).</summary>
    public const string PrefijoDeBoveda = "BV";

    /// <summary>
    /// El segundo custodio con <c>Caja.DobleCustodia</c>: obligatorio, custodio vigente de la bóveda y otra persona que quien registra. Sin doble custodia,
    /// opcional (si se da, se valida igual).
    /// </summary>
    public static async Task<Result<int?>> SegundoCustodioAsync(IApplicationDbContext db, SegregacionDeRecaudo segregacion, Vault boveda, int registra,
        Guid? segundoPublicId, bool dobleCustodia, DateOnly hoy, CancellationToken ct)
    {
        if (segundoPublicId is null) return dobleCustodia ? Result.Failure<int?>(ErroresDeRecaudo.DobleCustodia()) : Result.Success<int?>(null);
        var segundo = await db.Users.AsNoTracking().Where(u => u.PublicId == segundoPublicId).Select(u => (int?)u.Id).FirstOrDefaultAsync(ct);
        if (segundo is not { } id) return Result.Failure<int?>(ErroresDeRecaudo.DobleCustodia());
        var custodios = await db.VaultCustodians.AsNoTracking().Where(c => c.VaultId == boveda.Id && c.UserId == id).ToListAsync(ct);
        if (!custodios.Any(c => c.VigenteEn(hoy)) || await segregacion.MismaPersonaAsync(registra, id, ct))
            return Result.Failure<int?>(ErroresDeRecaudo.DobleCustodia());
        return Result.Success<int?>(id);
    }

    /// <summary>Saca de la bóveda las denominaciones; si alguna no alcanza, el error con lo que falta y nada cambia.</summary>
    public static async Task<Result> DescontarAsync(IApplicationDbContext db, Vault boveda, IReadOnlyList<(CashDenomination Denominacion, int Cantidad, decimal Valor)> salen,
        CancellationToken ct)
    {
        var ids = salen.Select(s => s.Denominacion.Id).ToList();
        var saldos = await db.VaultDenominationBalances.Where(b => b.VaultId == boveda.Id && ids.Contains(b.CashDenominationId)).ToListAsync(ct);
        var falta = salen.Sum(s =>
        {
            var hay = saldos.FirstOrDefault(b => b.CashDenominationId == s.Denominacion.Id)?.Quantity ?? 0;
            return hay >= s.Cantidad ? 0m : (s.Cantidad - hay) * s.Denominacion.Value;
        });
        if (falta > 0m) return Result.Failure(ErroresDeRecaudo.SaldoDeBovedaInsuficiente(falta));
        foreach (var (d, cantidad, valor) in salen)
        {
            var saldo = saldos.First(b => b.CashDenominationId == d.Id);
            saldo.Quantity -= cantidad;
            saldo.Amount -= valor;
        }
        return Result.Success();
    }

    /// <summary>Suma a la bóveda las denominaciones (crea la fila de la denominación si falta).</summary>
    public static async Task SumarAsync(IApplicationDbContext db, Vault boveda, IReadOnlyList<(CashDenomination Denominacion, int Cantidad, decimal Valor)> entran,
        CancellationToken ct)
    {
        var ids = entran.Select(s => s.Denominacion.Id).ToList();
        var saldos = await db.VaultDenominationBalances.Where(b => b.VaultId == boveda.Id && ids.Contains(b.CashDenominationId)).ToListAsync(ct);
        foreach (var (d, cantidad, valor) in entran)
        {
            var saldo = saldos.FirstOrDefault(b => b.CashDenominationId == d.Id);
            if (saldo is null)
            {
                saldo = new VaultDenominationBalance { VaultId = boveda.Id, CashDenominationId = d.Id };
                db.VaultDenominationBalances.Add(saldo);
                saldos.Add(saldo);
            }
            saldo.Quantity += cantidad;
            saldo.Amount += valor;
        }
    }

    /// <summary>Las denominaciones del movimiento, con el signo del movimiento (+1 entra, −1 sale).</summary>
    public static void AgregarDenominaciones(IApplicationDbContext db, VaultMovement movimiento,
        IReadOnlyList<(CashDenomination Denominacion, int Cantidad, decimal Valor)> denominaciones, int signo)
    {
        foreach (var (d, cantidad, valor) in denominaciones)
            db.VaultMovementDenominations.Add(new VaultMovementDenomination
            {
                VaultMovementId = movimiento.Id, CashDenominationId = d.Id, Quantity = signo * cantidad, Amount = signo * valor,
            });
    }

    /// <summary>
    /// La verificación del cuadre (FR-036): la proyección por denominación coincide con la suma de los movimientos de la bóveda (con signo). Devuelve las
    /// denominaciones que no cuadran (vacío si cuadra).
    /// </summary>
    public static async Task<IReadOnlyList<int>> DenominacionesQueNoCuadranAsync(IApplicationDbContext db, int bovedaId, CancellationToken ct)
    {
        var movimientos = await (from d in db.VaultMovementDenominations.AsNoTracking()
                                 join m in db.VaultMovements.AsNoTracking() on d.VaultMovementId equals m.Id
                                 where m.VaultId == bovedaId
                                 select new { d.CashDenominationId, d.Quantity }).ToListAsync(ct);
        var porMovimientos = movimientos.GroupBy(x => x.CashDenominationId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        var saldos = await db.VaultDenominationBalances.AsNoTracking().Where(b => b.VaultId == bovedaId)
            .ToDictionaryAsync(b => b.CashDenominationId, b => b.Quantity, ct);
        return porMovimientos.Keys.Union(saldos.Keys)
            .Where(k => porMovimientos.GetValueOrDefault(k) != saldos.GetValueOrDefault(k)).OrderBy(k => k).ToList();
    }

    /// <summary>El saldo en pesos de la bóveda.</summary>
    public static async Task<decimal> SaldoAsync(IApplicationDbContext db, int bovedaId, CancellationToken ct) =>
        await db.VaultDenominationBalances.AsNoTracking().Where(b => b.VaultId == bovedaId).SumAsync(b => b.Amount, ct);

    public static VaultMovementDto Dto(VaultMovement m) => new(m.PublicId, m.Number, m.Kind, m.Amount, m.BagSeal, m.Reason, m.MovedAt);
}

/// <summary><c>GET /api/receipts/vaults/{id}</c> (<c>Receipts.Vault.View</c>): la bóveda con su saldo por denominación y sus últimos movimientos. (nuevo)</summary>
public sealed record GetVaultDetailQuery(Guid PublicId) : IRequest<Result<VaultDetailDto>>;

public sealed class GetVaultDetailQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance) : IRequestHandler<GetVaultDetailQuery, Result<VaultDetailDto>>
{
    private const int Ultimos = 100;

    public async Task<Result<VaultDetailDto>> Handle(GetVaultDetailQuery request, CancellationToken ct)
    {
        var boveda = await BovedasDeRecaudo.VisibleAsync(db, alcance, request.PublicId, seguir: false, ct);
        if (boveda is null) return Result.Failure<VaultDetailDto>(ErroresDeRecaudo.NoEncontrado("La bóveda"));
        var oficina = await db.Branches.AsNoTracking().Where(b => b.Id == boveda.BranchId).Select(b => new { b.PublicId, b.Name }).FirstAsync(ct);
        var saldos = await (from b in db.VaultDenominationBalances.AsNoTracking()
                            join d in db.CashDenominations.AsNoTracking() on b.CashDenominationId equals d.Id
                            where b.VaultId == boveda.Id
                            orderby d.Value descending
                            select new VaultBalanceLineDto(d.PublicId, d.Value, b.Quantity, b.Amount)).ToListAsync(ct);
        var movimientos = await db.VaultMovements.AsNoTracking().Where(m => m.VaultId == boveda.Id).OrderByDescending(m => m.Id).Take(Ultimos).ToListAsync(ct);
        return Result.Success(new VaultDetailDto(boveda.PublicId, oficina.PublicId, oficina.Name, boveda.Mode, boveda.Status, saldos.Sum(s => s.Amount), saldos,
            movimientos.Select(MovimientosDeBoveda.Dto).ToList()));
    }
}

/// <summary>
/// <c>POST /api/receipts/vaults/{id}/movements</c> (<c>Receipts.Vault.Move</c>): lo que entra o sale de la bóveda sin pasar por una caja —una remesa que
/// vuelve del banco (<see cref="VaultMovementKind.RemittanceIn"/>), el conteo (<see cref="VaultMovementKind.Count"/>, no mueve saldo) y el billete retenido
/// (<see cref="VaultMovementKind.RetainedCounterfeit"/>, en custodia con su acta, sin tocar el saldo)— con doble custodia. (nuevo)
/// </summary>
public sealed record RegisterVaultMovementCommand(
    Guid VaultPublicId,
    VaultMovementKind Kind,
    IReadOnlyList<DenominacionContadaDto> Denominations,
    string Reason,
    Guid? SecondCustodianUserPublicId = null,
    string? BagSeal = null,
    Guid? MinutesAttachmentPublicId = null,
    Guid? RemittancePublicId = null) : IRequest<Result<VaultMovementDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterVaultMovementCommandValidator : AbstractValidator<RegisterVaultMovementCommand>
{
    public RegisterVaultMovementCommandValidator()
    {
        RuleFor(x => x.VaultPublicId).NotEmpty();
        RuleFor(x => x.Kind).Must(k => k is VaultMovementKind.RemittanceIn or VaultMovementKind.Count or VaultMovementKind.RetainedCounterfeit)
            .WithMessage("Desde la bóveda se registra la remesa que vuelve, el conteo o el billete retenido; abastecer y recibir se hacen desde la caja.");
        RuleFor(x => x.Denominations).NotEmpty().WithMessage("Cuente el efectivo por denominaciones.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.BagSeal).MaximumLength(40);
    }
}

public sealed class RegisterVaultMovementCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    IDateTimeService reloj,
    ICerrojoDeRecaudo cerrojo,
    NumeradorDeRecaudo numerador,
    SegregacionDeRecaudo segregacion,
    ParametrosDeCaja parametros,
    EmisionDeRecaudo emision) : IRequestHandler<RegisterVaultMovementCommand, Result<VaultMovementDto>>
{
    public async Task<Result<VaultMovementDto>> Handle(RegisterVaultMovementCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<VaultMovementDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var hoy = reloj.HoyLocal;
        var boveda = await BovedasDeRecaudo.VisibleAsync(db, alcance, request.VaultPublicId, seguir: true, ct);
        if (boveda is null) return Result.Failure<VaultMovementDto>(ErroresDeRecaudo.NoEncontrado("La bóveda"));
        if (boveda.Status != ReceiptsCatalogStatus.Active) return Result.Failure<VaultMovementDto>(ErroresDeRecaudo.BovedaInactiva());
        if (request.Kind == VaultMovementKind.RetainedCounterfeit && request.MinutesAttachmentPublicId is null)
            return Result.Failure<VaultMovementDto>(ErroresDeRecaudo.ActaRequerida());
        var dobleCustodia = await parametros.SiNoAsync(ParametrosDeRecaudoYTesoreria.CajaDobleCustodia, hoy, ct);
        var segundo = await MovimientosDeBoveda.SegundoCustodioAsync(db, segregacion, boveda, usuario, request.SecondCustodianUserPublicId, dobleCustodia, hoy, ct);
        if (segundo.IsFailure) return Result.Failure<VaultMovementDto>(segundo.Error);
        var denominaciones = await SesionesDeCaja.DenominacionesAsync(db, request.Denominations, ct);
        if (denominaciones.IsFailure) return Result.Failure<VaultMovementDto>(denominaciones.Error);
        var valor = denominaciones.Value.Sum(d => d.Valor);

        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.Boveda, TablasDeRecaudo.Bovedas, [boveda.Id], ct: ct);

        // Sólo la remesa que vuelve mueve el saldo. El conteo y el billete retenido (que nunca fue dinero de la bóveda) quedan como acta con valor cero.
        var signo = request.Kind == VaultMovementKind.RemittanceIn ? 1 : 0;
        if (signo == 1) await MovimientosDeBoveda.SumarAsync(db, boveda, denominaciones.Value, ct);

        var (tipo, prefijo) = await SesionesDeCaja.TipoYPrefijoAsync(db, ReceiptDocumentKind.CashMovement, null, ct);
        var movimiento = new VaultMovement
        {
            VaultId = boveda.Id,
            Kind = request.Kind,
            Amount = signo * valor,
            CustodianUserId = usuario,
            SecondCustodianUserId = segundo.Value,
            BagSeal = request.BagSeal,
            MinutesAttachmentPublicId = request.MinutesAttachmentPublicId,
            RemittancePublicId = request.RemittancePublicId,
            Reason = request.Reason.Trim(),
            MovedAt = reloj.UtcNow,
            Number = await numerador.AsignarAsync(tipo.Code, MovimientosDeBoveda.PrefijoDeBoveda + prefijo, ct),
        };
        db.VaultMovements.Add(movimiento);
        await db.SaveChangesAsync(ct);
        if (signo != 0) MovimientosDeBoveda.AgregarDenominaciones(db, movimiento, denominaciones.Value, signo);
        if (request.Kind == VaultMovementKind.RemittanceIn) await emision.MovimientoDeBovedaAsync(movimiento, boveda, hoy, tipo.Code, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(MovimientosDeBoveda.Dto(movimiento));
    }
}
