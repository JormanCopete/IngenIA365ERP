using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Treasury;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.Accounting;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Sessions;
using IngenIA365ERP.Domain.Entities.Receipts.Vaults;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Vaults;

/// <summary>La remesa despachada desde la bóveda: el movimiento y la remesa de Tesorería. (nuevo)</summary>
public sealed record VaultRemittanceDto(VaultMovementDto Movement, Guid RemittancePublicId, string RemittanceNumber);

/// <summary>
/// <c>POST /api/receipts/vaults/{id}/remittances</c> (<c>Receipts.Vault.Remit</c>, con clave; R-15; FR-035, FR-105, FR-182): despacha efectivo de la bóveda a una cuenta
/// propia con doble custodia, acta y sello de bolsa. En la misma transacción: el movimiento de bóveda <c>RemittanceOut</c> (sale por denominaciones), la remesa en
/// tránsito por <see cref="IRemesasDeTesoreria.RegistrarAsync"/> y el hecho <c>RemesaDespachada</c>. Tesorería la confirma con su soporte o por el cruce del extracto. (nuevo)
/// </summary>
public sealed record DispatchVaultRemittanceCommand(
    Guid VaultPublicId,
    Guid OwnBankAccountPublicId,
    IReadOnlyList<DenominacionContadaDto> Denominations,
    string BagSeal,
    Guid MinutesAttachmentPublicId,
    Guid? SecondCustodianUserPublicId = null,
    string? Reason = null) : IRequest<Result<VaultRemittanceDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class DispatchVaultRemittanceCommandValidator : AbstractValidator<DispatchVaultRemittanceCommand>
{
    public DispatchVaultRemittanceCommandValidator()
    {
        RuleFor(x => x.VaultPublicId).NotEmpty();
        RuleFor(x => x.OwnBankAccountPublicId).NotEmpty();
        RuleFor(x => x.Denominations).NotEmpty().WithMessage("Cuente el efectivo que sale por denominaciones.");
        RuleFor(x => x.BagSeal).NotEmpty().WithMessage("Registre el sello de la bolsa.").MaximumLength(40);
        RuleFor(x => x.MinutesAttachmentPublicId).NotEmpty().WithMessage("Adjunte el acta de la remesa.");
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed class DispatchVaultRemittanceCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    IDateTimeService reloj,
    ICerrojoDeRecaudo cerrojo,
    NumeradorDeRecaudo numerador,
    SegregacionDeRecaudo segregacion,
    ParametrosDeCaja parametros,
    IRemesasDeTesoreria remesas,
    EmisionDeRecaudo emision,
    AuditoriaDeRecaudo auditoria) : IRequestHandler<DispatchVaultRemittanceCommand, Result<VaultRemittanceDto>>
{
    public async Task<Result<VaultRemittanceDto>> Handle(DispatchVaultRemittanceCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<VaultRemittanceDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var hoy = reloj.HoyLocal;
        var boveda = await BovedasDeRecaudo.VisibleAsync(db, alcance, request.VaultPublicId, seguir: true, ct);
        if (boveda is null) return Result.Failure<VaultRemittanceDto>(ErroresDeRecaudo.NoEncontrado("La bóveda"));
        if (boveda.Status != ReceiptsCatalogStatus.Active) return Result.Failure<VaultRemittanceDto>(ErroresDeRecaudo.BovedaInactiva());
        var dobleCustodia = await parametros.SiNoAsync(ParametrosDeRecaudoYTesoreria.CajaDobleCustodia, hoy, ct);
        var segundo = await MovimientosDeBoveda.SegundoCustodioAsync(db, segregacion, boveda, usuario, request.SecondCustodianUserPublicId, dobleCustodia, hoy, ct);
        if (segundo.IsFailure) return Result.Failure<VaultRemittanceDto>(segundo.Error);
        var denominaciones = await SesionesDeCaja.DenominacionesAsync(db, request.Denominations, ct);
        if (denominaciones.IsFailure) return Result.Failure<VaultRemittanceDto>(denominaciones.Error);
        var valor = denominaciones.Value.Sum(d => d.Valor);
        var oficina = await db.Branches.AsNoTracking().Where(b => b.Id == boveda.BranchId).Select(b => b.PublicId).FirstAsync(ct);

        // Cerrojos de Recaudo (configuración, bóveda) y, al final, las dos numeraciones; Tesorería después (orden global Recaudo → Tesorería).
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.Boveda, TablasDeRecaudo.Bovedas, [boveda.Id], ct: ct);
        var salida = await MovimientosDeBoveda.DescontarAsync(db, boveda, denominaciones.Value, ct);
        if (salida.IsFailure) return Result.Failure<VaultRemittanceDto>(salida.Error);
        var (tipo, prefijo) = await SesionesDeCaja.TipoYPrefijoAsync(db, ReceiptDocumentKind.CashMovement, null, ct);
        var movimiento = new VaultMovement
        {
            VaultId = boveda.Id, Kind = VaultMovementKind.RemittanceOut, Amount = -valor, CustodianUserId = usuario, SecondCustodianUserId = segundo.Value,
            BagSeal = request.BagSeal.Trim(), MinutesAttachmentPublicId = request.MinutesAttachmentPublicId,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Remesa al banco" : request.Reason.Trim(), MovedAt = reloj.UtcNow,
            Number = await numerador.AsignarAsync(tipo.Code, MovimientosDeBoveda.PrefijoDeBoveda + prefijo, ct),
        };
        var remesa = await remesas.RegistrarAsync(new SolicitudDeRemesa(request.OperationKey, request.OwnBankAccountPublicId, oficina, valor, hoy, movimiento.Number,
            movimiento.PublicId, boveda.PublicId, movimiento.BagSeal, request.MinutesAttachmentPublicId), ct);
        movimiento.RemittancePublicId = remesa.RemittancePublicId;
        db.VaultMovements.Add(movimiento);
        await db.SaveChangesAsync(ct);
        MovimientosDeBoveda.AgregarDenominaciones(db, movimiento, denominaciones.Value, -1);
        await emision.RemesaDespachadaAsync(movimiento, boveda, remesa.RemittancePublicId, remesa.Number, request.OwnBankAccountPublicId, hoy, tipo.Code, ct);
        await auditoria.AgregarAsync("Receipts.Vault.RemittanceDispatched", "VaultMovement", movimiento.PublicId, null,
            new { number = movimiento.Number, remittance = remesa.Number, amount = valor, seal = movimiento.BagSeal, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(new VaultRemittanceDto(MovimientosDeBoveda.Dto(movimiento), remesa.RemittancePublicId, remesa.Number));
    }
}

/// <summary>
/// <c>GET /api/receipts/own-bank-accounts?branch=</c> (<c>Receipts.Vault.View</c> o <c>Receipts.Vault.Remit</c>; T0230): las cuentas propias de recaudo de la oficina, por el
/// puerto de Tesorería, para elegir a cuál va la remesa. (nuevo)
/// </summary>
public sealed record ListReceiptsOwnBankAccountsQuery(Guid? BranchPublicId = null) : IRequest<Result<IReadOnlyList<CuentaBancariaPropiaDto>>>;

public sealed class ListReceiptsOwnBankAccountsQueryHandler(ICuentasBancariasPropias cuentas)
    : IRequestHandler<ListReceiptsOwnBankAccountsQuery, Result<IReadOnlyList<CuentaBancariaPropiaDto>>>
{
    public async Task<Result<IReadOnlyList<CuentaBancariaPropiaDto>>> Handle(ListReceiptsOwnBankAccountsQuery request, CancellationToken ct) =>
        Result.Success(await cuentas.DeRecaudoAsync(request.BranchPublicId, ct));
}
