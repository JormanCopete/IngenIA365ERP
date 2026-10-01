using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Costing;

namespace IngenIA365ERP.Application.Inventory.Documents.Efectos;

/// <summary>
/// Las salidas por ajuste (feature 012, T253; contracts/api.md §10; FR-037): <c>NegativeAdjustment</c>,
/// <c>InternalConsumption</c> y <c>WriteOff</c>, al promedio vigente. Ninguna admite costo digitado
/// (<c>Inventory.Adjustment.UnitCostOnlyOnEntries</c>). Una estrategia por clase: <see cref="EfectoDeAjusteNegativo"/>,
/// <see cref="EfectoDeConsumoInterno"/> y <see cref="EfectoDeBaja"/> (nuevos). Emiten <c>AjusteInventarioAprobado</c> con la
/// operación de la matriz (<c>AjusteNegativo</c>, <c>ConsumoInterno</c>, <c>Baja</c>) y <c>ReasonCode</c> = código de la causa.
/// El movimiento entre ubicaciones (<c>LocationMove</c>) es de US10 sobre la misma ruta y el ensamble (<c>Assembly</c>) llega en
/// I6: sin estrategia, <c>Inventory.DocumentClass.NotAvailable</c>. (nuevo)
/// </summary>
public abstract class EfectoDeSalidaPorAjuste(
    RegistroDeKardex registro,
    ReversionDeKardex reversion,
    EmisionDeInventario emision,
    IMaestrosDelDocumento maestros,
    IPermissionChecker permisos,
    IApplicationDbContext db)
    : EfectoDeAjuste(registro, reversion, emision, maestros, permisos, db)
{
    protected override decimal Signo => -1m;

    protected override (ValoracionDelMovimiento Valoracion, decimal? Costo) Valoracion(InventoryDocumentLine linea) =>
        (ValoracionDelMovimiento.AlCostoVigente, null);

    /// <summary>¿La clase exige la causa de ajuste? (negativo y baja).</summary>
    protected virtual bool ExigeCausa => false;

    protected override Task<IReadOnlyList<Error>> ReglasDeLaClaseAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var errores = new List<Error>();
        var vivas = contexto.Documento.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).ToList();
        if (vivas.FirstOrDefault(l => l.UnitCost is not null) is { } conCosto && contexto.Documento.Status == DocumentStatus.Draft)
            errores.Add(InventoryErrors.AdjustmentUnitCostOnlyOnEntries(conCosto.LineNumber));
        if (ExigeCausa && vivas.Count > 0 && vivas.Any(l => l.AdjustmentCauseId is null))
            errores.Add(InventoryErrors.FieldRequired(InventoryErrors.CampoCausa));
        errores.AddRange(ReglasPropias(contexto));
        return Task.FromResult<IReadOnlyList<Error>>(errores);
    }

    /// <summary>Lo que agrega cada clase.</summary>
    protected virtual IEnumerable<Error> ReglasPropias(ContextoDeEfecto contexto) => [];
}

/// <summary><c>NegativeAdjustment</c>: salida con causa obligatoria y soportes adjuntos (FR-037). (nuevo)</summary>
public sealed class EfectoDeAjusteNegativo(
    RegistroDeKardex registro, ReversionDeKardex reversion, EmisionDeInventario emision, IMaestrosDelDocumento maestros,
    IPermissionChecker permisos, IApplicationDbContext db)
    : EfectoDeSalidaPorAjuste(registro, reversion, emision, maestros, permisos, db)
{
    public override DocumentClass Clase => DocumentClass.NegativeAdjustment;

    protected override bool ExigeCausa => true;
}

/// <summary>
/// <c>InternalConsumption</c>: salida con centro de costo (lo exige la clase en las reglas comunes). Un tipo de <b>retiro gravado</b>
/// (<c>IsTaxableWithdrawal</c>, I3, T615) lleva además base a la lista de precios general vigente e IVA del producto
/// (<see cref="RetiroGravado"/>): sin precio → <c>Inventory.Price.NotFound</c>; el mensaje sale con operación <c>RetiroGravado</c> y su
/// bloque de base e IVA. Sin <see cref="RetiroGravado"/> registrado (un anfitrión sin I3), el retiro gravado responde
/// <c>Inventory.Adjustment.TaxableWithdrawalNotAvailable</c>. (nuevo)
/// </summary>
public sealed class EfectoDeConsumoInterno(
    RegistroDeKardex registro, ReversionDeKardex reversion, EmisionDeInventario emision, IMaestrosDelDocumento maestros,
    IPermissionChecker permisos, IApplicationDbContext db, RetiroGravado? retiroGravado = null)
    : EfectoDeSalidaPorAjuste(registro, reversion, emision, maestros, permisos, db)
{
    private readonly Dictionary<Guid, IngenIA365ERP.Application.Common.Integration.Contracts.Inventory.TaxableWithdrawalV1> _gravados = [];

    public override DocumentClass Clase => DocumentClass.InternalConsumption;

    protected override IEnumerable<Error> ReglasPropias(ContextoDeEfecto contexto)
    {
        if (contexto.Tipo.IsTaxableWithdrawal && retiroGravado is null) yield return InventoryErrors.AdjustmentTaxableWithdrawalNotAvailable();
    }

    public override async Task<Result> ValidarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var comun = await base.ValidarAsync(contexto, ct);
        if (comun.IsFailure || contexto.EsAnulacion || !contexto.Tipo.IsTaxableWithdrawal || retiroGravado is null) return comun;
        var gravado = await retiroGravado.CalcularAsync(contexto.Documento, ct);
        if (gravado.IsFailure) return Result.Failure(gravado.Error);
        _gravados[contexto.Documento.PublicId] = gravado.Value;
        return Result.Success();
    }

    public override async Task<IReadOnlyList<object>> MensajesAsync(ContextoDeEfecto contexto, CancellationToken ct) =>
        ConBaseEIva(contexto, await base.MensajesAsync(contexto, ct));

    public override async Task<IReadOnlyList<object>> MensajesProvisionalesAsync(ContextoDeEfecto contexto, CancellationToken ct) =>
        ConBaseEIva(contexto, await base.MensajesProvisionalesAsync(contexto, ct));

    private IReadOnlyList<object> ConBaseEIva(ContextoDeEfecto contexto, IReadOnlyList<object> contenidos) =>
        !contexto.EsAnulacion && _gravados.TryGetValue(contexto.Documento.PublicId, out var gravado)
            ? contenidos.Select(c => c is IngenIA365ERP.Application.Common.Integration.Contracts.Inventory.AjusteInventarioAprobadoV1 a ? a with { TaxableWithdrawal = gravado } : c).ToList()
            : contenidos;
}

/// <summary>
/// <c>WriteOff</c>: baja por daño, vencimiento, hurto o destrucción, con causa obligatoria y soportes (acta, denuncia). (nuevo)
/// <para>
/// US10 (T371): la <b>baja desde el tránsito</b> que resuelve un faltante de traslado (<c>WriteOffFromTransit</c>) es la única salida
/// que admite la bodega de tránsito, y sale al costo de la línea de despacho (el de la diferencia), no al promedio: así el tránsito
/// queda en cero exacto y el valor del traslado no se mueve. La reconoce por la diferencia que la tiene como documento que la
/// resuelve.
/// </para>
/// </summary>
public sealed class EfectoDeBaja(
    RegistroDeKardex registro, ReversionDeKardex reversion, EmisionDeInventario emision, IMaestrosDelDocumento maestros,
    IPermissionChecker permisos, IApplicationDbContext db)
    : EfectoDeSalidaPorAjuste(registro, reversion, emision, maestros, permisos, db)
{
    public override DocumentClass Clase => DocumentClass.WriteOff;

    protected override bool ExigeCausa => true;

    protected override async Task<bool> AdmiteTransitoAsync(ContextoDeEfecto contexto, CancellationToken ct) =>
        await FaltanteQueResuelveAsync(contexto.Documento, ct) is not null;

    protected override async Task<IReadOnlyList<MovimientoDeKardex>> MovimientosAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var faltante = await FaltanteQueResuelveAsync(contexto.Documento, ct);
        if (faltante is null || contexto.Documento.WarehouseId is not int transito) return Movimientos(contexto.Documento);
        return contexto.Documento.Lines.Where(l => !l.IsDeleted && l.QuantityBase > 0m).OrderBy(l => l.LineNumber)
            .Select(l => new MovimientoDeKardex(l, transito, -l.QuantityBase, ValoracionDelMovimiento.AlCostoDeOrigen, faltante.UnitCost ?? 0m,
                LocationId: l.LocationId))
            .ToList();
    }

    /// <summary>El faltante de traslado que esta baja resuelve (en aprobación), o nulo.</summary>
    private async Task<Domain.Entities.Inventory.Documents.TransferDiscrepancy?> FaltanteQueResuelveAsync(InventoryDocument documento, CancellationToken ct) =>
        documento.Id == 0
            ? null
            : await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(Db.TransferDiscrepancies,
                d => d.ResolutionDocumentId == documento.Id && d.Resolution == TransferDiscrepancyResolution.WriteOffFromTransit && d.ResolvedAt == null, ct);

    private IApplicationDbContext Db { get; } = db;
}
