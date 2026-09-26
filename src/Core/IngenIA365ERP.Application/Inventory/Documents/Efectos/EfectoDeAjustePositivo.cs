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
/// La estrategia de <c>PositiveAdjustment</c> (feature 012, T253; contracts/api.md §10): entrada al costo vigente —el promedio
/// del ámbito, o el último costo si la existencia es cero— o al <c>unitCost</c> de la línea, que sólo puede digitar quien
/// tiene <c>Inventory.Adjustments.SetUnitCost</c> (si no, 422 <c>Inventory.Adjustment.UnitCostNotAllowed</c> con
/// <c>permissionCode</c>: es un permiso que depende del cuerpo, no un 404). La causa es opcional. Emite
/// <c>AjusteInventarioAprobado</c> con la operación <c>AjustePositivo</c>. (nuevo)
/// </summary>
public sealed class EfectoDeAjustePositivo(
    RegistroDeKardex registro,
    ReversionDeKardex reversion,
    EmisionDeInventario emision,
    IMaestrosDelDocumento maestros,
    IPermissionChecker permisos,
    IApplicationDbContext db)
    : EfectoDeAjuste(registro, reversion, emision, maestros, permisos, db)
{
    /// <summary>El permiso que deja digitar el costo de la entrada.</summary>
    public const string PermisoDeCosto = "Inventory.Adjustments.SetUnitCost";

    public override DocumentClass Clase => DocumentClass.PositiveAdjustment;

    protected override decimal Signo => 1m;

    protected override (ValoracionDelMovimiento Valoracion, decimal? Costo) Valoracion(InventoryDocumentLine linea) =>
        linea.UnitCost is { } costo ? (ValoracionDelMovimiento.AlCostoIndicado, costo) : (ValoracionDelMovimiento.AlCostoVigente, null);

    /// <summary>
    /// El costo digitado exige el permiso a quien lo pide. En la reentrada de la última aprobación (el documento ya está en
    /// aprobación) no se vuelve a exigir al aprobador: se comprobó al pedirla.
    /// </summary>
    protected override async Task<IReadOnlyList<Error>> ReglasDeLaClaseAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        if (contexto.Documento.Status == DocumentStatus.PendingApproval) return [];
        var conCosto = contexto.Documento.Lines.Where(l => !l.IsDeleted && l.UnitCost is not null).OrderBy(l => l.LineNumber).FirstOrDefault();
        if (conCosto is null || await Permisos.HasPermissionAsync(PermisoDeCosto, ct)) return [];
        return [InventoryErrors.AdjustmentUnitCostNotAllowed(conCosto.LineNumber, PermisoDeCosto)];
    }
}
