using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Inventory.GoLive;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Inventory.GoLive;

// ------------------------------------------------------------------------------------------------------------ DTOs --

/// <summary>La vista previa de la activación (contracts/api.md §13.3). <see cref="Sets"/> va vacío si Contabilidad no responde. (nuevo)</summary>
public sealed record ActivationPreviewDto(
    BodegaDeActivacionDto Warehouse,
    DateOnly CutoffDate,
    SaldoInicialDeActivacionDto OpeningBalance,
    IReadOnlyList<ConjuntoDeCuentasDto> Sets,
    decimal TotalDifference,
    IReadOnlyList<BloqueoDeActivacionDto> Blockers,
    bool CanActivate,
    bool RequiresAcceptance,
    ActivationResultDto? Activation = null);

public sealed record BodegaDeActivacionDto(Guid PublicId, string Code, string Name);

public sealed record SaldoInicialDeActivacionDto(bool Confirmed, decimal Value, IReadOnlyList<DocumentoDeActivacionDto> Documents);

public sealed record DocumentoDeActivacionDto(Guid PublicId, string? DisplayNumber, DocumentStatus Status, decimal Value);

/// <summary>
/// Un conjunto de cuentas mapeadas (§13.3): los grupos contables y las cuentas de rol <c>Inventario</c> y <c>Transito</c> que
/// Contabilidad une por componentes conexos (<c>IContabilidadParaInventario.SaldosDeCuentasMapeadasAsync</c>), su saldo contable al
/// corte, el valorizado de todas las bodegas que usan esas cuentas, la diferencia (valorizado − saldo contable) y los mensajes que
/// la explican. (nuevo; US7, T523, lo llena)
/// </summary>
public sealed record ConjuntoDeCuentasDto(
    IReadOnlyList<CodigoYNombreDto> AccountingGroups,
    IReadOnlyList<CuentaDelConjuntoDto> Accounts,
    decimal LedgerBalance,
    ValorizadoDelConjuntoDto Valuation,
    decimal Difference,
    ExplicacionDelConjuntoDto Explanation);

public sealed record CodigoYNombreDto(string Code, string Name);

public sealed record CuentaDelConjuntoDto(string Code, string Name, string Role, decimal LedgerBalance);

/// <summary>
/// El valorizado del conjunto (§13.3): el de la bodega que se activa, el de las activas y el de las no activas que comparten cuentas
/// con sus cifras de referencia al corte (suman al conjunto y se muestran aparte), y el total. (nuevo, T523)
/// </summary>
public sealed record ValorizadoDelConjuntoDto(
    decimal ThisWarehouse,
    IReadOnlyList<BodegaActivaDelConjuntoDto> ActiveWarehouses,
    IReadOnlyList<BodegaFueraDelModuloDelConjuntoDto> LegacyWarehouses,
    decimal Total);

public sealed record BodegaActivaDelConjuntoDto(Guid WarehousePublicId, string Code, decimal Value);

public sealed record BodegaFueraDelModuloDelConjuntoDto(string WarehouseCode, Guid? WarehousePublicId, decimal Value, DateOnly FiguresAsOf, Guid BatchPublicId);

/// <summary>Los mensajes de Inventario a Contabilidad hasta el corte que explican parte de la diferencia (§13.3). (nuevo, T523)</summary>
public sealed record ExplicacionDelConjuntoDto(int Pending, int InBatch, int Rejected, int NotPosted);

public sealed record BloqueoDeActivacionDto(string Code, string Message, object? Data);

/// <summary>El resultado de activar (§13.3). (nuevo)</summary>
public sealed record ActivationResultDto(
    Guid ActivationPublicId,
    Guid WarehousePublicId,
    DateTime ActivatedAt,
    int ActivatedBy,
    DateOnly CutoffDate,
    decimal TotalDifference,
    bool DifferenceAccepted,
    string? Reason);

// ------------------------------------------------------------------------------------------------ vista previa --

/// <summary>
/// <c>GET /api/inventory/warehouses/{id}/activation?cutoffDate=</c> (feature 012, T313; §13.3; <c>Inventory.Warehouses.Activate</c>):
/// lo que se compararía y lo que hoy impide activar. Si la bodega ya está activa, trae la activación guardada. El alcance de bodega
/// (<c>IAlcanceDeInventario</c>) lo aplica <see cref="ComparacionDeActivacion"/>: fuera de él, 404. (nuevo)
/// </summary>
public sealed record GetWarehouseActivationPreviewQuery(Guid WarehousePublicId, DateOnly? CutoffDate = null) : IRequest<Result<ActivationPreviewDto>>;

public sealed class GetWarehouseActivationPreviewQueryHandler(ComparacionDeActivacion comparacion)
    : IRequestHandler<GetWarehouseActivationPreviewQuery, Result<ActivationPreviewDto>>
{
    public async Task<Result<ActivationPreviewDto>> Handle(GetWarehouseActivationPreviewQuery request, CancellationToken ct)
    {
        var r = await comparacion.CalcularAsync(request.WarehousePublicId, request.CutoffDate, ct);
        return r.IsSuccess ? Result.Success(r.Value.Vista) : Result.Failure<ActivationPreviewDto>(r.Error);
    }
}

// --------------------------------------------------------------------------------------------------- activar --

/// <summary>
/// <c>POST /api/inventory/warehouses/{id}/activation</c> (feature 012, T313; FR-090, FR-091, US4-2, US4-3; §13.3; data-model §6.4;
/// <c>Inventory.Warehouses.Activate</c>, 404 sin él). Vuelve a calcular siempre (nunca confía en la vista previa):
/// <list type="bullet">
/// <item>un bloqueo duro (saldo sin confirmar, corte distinto, ya activa, período cerrado) responde 422 con su código;</item>
/// <item>sin la comparación contable (<c>Inventory.Activation.AccountingUnavailable</c>, todo I1): en producción
/// (<see cref="PuestaEnMarchaOptions.PermitirActivacionSinComparacion"/> = <c>false</c>) 422 y nada escrito; fuera de ella exige
/// <see cref="AcceptDifference"/> (si no, <c>Inventory.Activation.Difference</c>), el permiso
/// <c>Inventory.Warehouses.AcceptActivationDifference</c> (si no, <c>.AcceptDifferenceNotAllowed</c> con <c>permissionCode</c>) y
/// motivo;</item>
/// <item>al activar: <c>Warehouse.Activar</c>, la fila de <c>INV_WarehouseActivations</c> con la comparación entera y, si es la
/// primera bodega activa de la sucursal, también su tránsito. El motivo va a la auditoría por <c>AuditBehavior</c>
/// (<see cref="IConMotivo"/>); un intento rechazado no deja fila y queda en la auditoría como rechazo.</item>
/// </list>
/// Desde US7 (I2, T523) los conjuntos y el cuadre los calcula <see cref="ComparacionDeActivacion"/> con la consulta de saldos de
/// Contabilidad; bodegas no activas sin cifras y grupos sin regla son bloqueos duros como los demás. (nuevo)
/// </summary>
public sealed record ActivateWarehouseCommand(Guid WarehousePublicId, DateOnly CutoffDate, bool AcceptDifference = false, string Reason = "")
    : IRequest<Result<ActivationResultDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ActivateWarehouseCommandValidator : AbstractValidator<ActivateWarehouseCommand>
{
    public ActivateWarehouseCommandValidator()
    {
        RuleFor(x => x.WarehousePublicId).NotEmpty();
        RuleFor(x => x.CutoffDate).NotEmpty();
        RuleFor(x => x.Reason).Must(m => !string.IsNullOrWhiteSpace(m)).When(x => x.AcceptDifference)
            .WithMessage("Aceptar la diferencia exige el motivo.");
        RuleFor(x => x.Reason).MaximumLength(ValidadorConMotivo<ActivateWarehouseCommand>.LargoMaximo);
    }
}

public sealed class ActivateWarehouseCommandHandler(
    IApplicationDbContext db,
    ComparacionDeActivacion comparacion,
    IActorActual actorActual,
    IPermissionChecker permisos,
    IDateTimeService reloj,
    IOptions<PuestaEnMarchaOptions> opciones)
    : IRequestHandler<ActivateWarehouseCommand, Result<ActivationResultDto>>
{
    /// <summary>El permiso de aceptar una diferencia al activar (US4-3).</summary>
    public const string PermisoDeAceptarDiferencia = "Inventory.Warehouses.AcceptActivationDifference";

    /// <summary>Cómo empieza el motivo guardado cuando se activa sin comparación contable (sólo fuera de producción, I1).</summary>
    public const string PrefijoSinComparacion = "Sin comparación contable";

    public async Task<Result<ActivationResultDto>> Handle(ActivateWarehouseCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDelDocumento.SinUsuario());

        var calculo = await comparacion.CalcularAsync(request.WarehousePublicId, request.CutoffDate, ct);
        if (calculo.IsFailure) return Falla(calculo.Error);
        var (vista, bodega, duro) = calculo.Value;

        if (duro is not null) return Falla(duro);

        var sinComparacion = vista.Blockers.Any(b => b.Code == GoLiveErrors.ActivationAccountingUnavailableCode);
        if (sinComparacion && !opciones.Value.PermitirActivacionSinComparacion) return Falla(GoLiveErrors.ActivationAccountingUnavailable());

        var aceptada = false;
        if (sinComparacion || vista.TotalDifference != 0m)
        {
            if (!request.AcceptDifference)
                return Falla(GoLiveErrors.ActivationDifference(
                    vista.Sets.Select(s => new { accountingGroups = s.AccountingGroups, difference = s.Difference }).ToList(), vista.TotalDifference));
            if (!await permisos.HasPermissionAsync(PermisoDeAceptarDiferencia, ct))
                return Falla(GoLiveErrors.ActivationAcceptDifferenceNotAllowed(PermisoDeAceptarDiferencia));
            if (string.IsNullOrWhiteSpace(request.Reason))
                return Falla(new Error(Error.Validation.Code, "Aceptar la diferencia exige el motivo."));
            aceptada = true;
        }

        var ahora = new DateTimeOffset(reloj.UtcNow, TimeSpan.Zero);
        bodega.Activar(request.CutoffDate, usuario, ahora);
        await ActivarTransitoAsync(bodega, request.CutoffDate, usuario, ahora, ct);
        await ArrancarElModuloAsync(request.CutoffDate, usuario, ct);

        var motivo = aceptada ? Motivo(request.Reason, sinComparacion) : null;
        var activacion = new WarehouseActivation
        {
            WarehouseId = bodega.Id,
            CutoffDate = request.CutoffDate,
            ComparisonJson = JsonSerializer.Serialize(new { vista.Sets, vista.TotalDifference, vista.OpeningBalance, accountingAvailable = !sinComparacion },
                new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            TotalDifference = vista.TotalDifference,
            IsBalanced = !sinComparacion && vista.TotalDifference == 0m,
            DifferenceAcceptedByUserId = aceptada ? usuario : null,
            AcceptanceReason = motivo,
            ActivatedAt = ahora.UtcDateTime,
            ActivatedByUserId = usuario,
        };
        db.WarehouseActivations.Add(activacion);
        await db.SaveChangesAsync(ct);

        return Result.Success(new ActivationResultDto(activacion.PublicId, bodega.PublicId, activacion.ActivatedAt, usuario, activacion.CutoffDate,
            activacion.TotalDifference, aceptada, motivo));
    }

    /// <summary>
    /// <c>INV_Setup</c> la crea el primer registro de una fecha de corte (data-model §6.1): la carga del saldo inicial o, si la
    /// primera bodega se activa sin saldo, esta activación, con <c>StartDate</c> el primer día del mes del corte. Sin ella el
    /// módulo no tenía inicio y ningún mes se podía cerrar (<c>Inventory.Period.NotStarted</c>; lo destapó la e2e del cierre
    /// de I1, T443). Si ya existe, no se toca.
    /// </summary>
    private async Task ArrancarElModuloAsync(DateOnly corte, int usuario, CancellationToken ct)
    {
        if (await db.InventorySetups.AnyAsync(ct) || db.InventorySetups.Local.Count > 0) return;
        db.InventorySetups.Add(new Domain.Entities.Inventory.Periods.InventorySetup
        {
            StartDate = new DateOnly(corte.Year, corte.Month, 1),
            StartedAt = reloj.UtcNow,
            StartedByUserId = usuario,
        });
    }

    /// <summary>La bodega de tránsito de la sucursal se activa con la primera bodega operativa activa de ella (data-model §6.4).</summary>
    private async Task ActivarTransitoAsync(Warehouse bodega, DateOnly corte, int usuario, DateTimeOffset ahora, CancellationToken ct)
    {
        if (bodega.EsTransito) return;
        var otraActiva = await db.Warehouses.AnyAsync(w => w.BranchId == bodega.BranchId && w.Id != bodega.Id
            && w.Behavior == WarehouseBehavior.Operational && w.ActivationStatus == WarehouseActivationStatus.Active, ct);
        if (otraActiva) return;
        var transito = await db.Warehouses.FirstOrDefaultAsync(w => w.BranchId == bodega.BranchId && w.Behavior == WarehouseBehavior.Transit
            && w.ActivationStatus == WarehouseActivationStatus.NotActivated, ct);
        transito?.Activar(corte, usuario, ahora);
    }

    private static string Motivo(string motivo, bool sinComparacion)
    {
        var texto = sinComparacion ? $"{PrefijoSinComparacion}: {motivo.Trim()}" : motivo.Trim();
        return texto.Length > WarehouseActivation.LargoDelMotivo ? texto[..WarehouseActivation.LargoDelMotivo] : texto;
    }

    private static Result<ActivationResultDto> Falla(Error error) => Result.Failure<ActivationResultDto>(error);
}

