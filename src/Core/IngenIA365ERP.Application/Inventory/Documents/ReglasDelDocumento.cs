using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;

namespace IngenIA365ERP.Application.Inventory.Documents;

/// <summary>
/// Las reglas que comparten todas las clases (feature 012, T144, T146; contracts/api.md §9.4): líneas, campos
/// obligatorios del tipo, fecha, corte del módulo, período y bodega. Son las mismas al guardar y al confirmar: al
/// guardar vuelven como avisos (<c>warnings[]</c>) con el código que daría la confirmación, y la confirmación se detiene
/// en la primera. Puro: lo que necesita lo recibe. (nuevo)
/// </summary>
public static class ReglasDelDocumento
{
    /// <summary>Los campos obligatorios, con el nombre que usa <c>data.field</c>.</summary>
    public const string CampoContraparte = "counterparty";
    public const string CampoCentroDeCosto = "costCenter";
    public const string CampoMotivo = "reason";
    public const string CampoReferenciaExterna = "externalReference";
    public const string CampoBodega = "warehouse";

    /// <summary>Todo lo que hoy impediría confirmar el documento, en el orden en que la confirmación lo revisa.</summary>
    /// <param name="documento">El documento con sus líneas.</param>
    /// <param name="tipo">Su tipo, con sus bodegas permitidas cargadas.</param>
    /// <param name="bodega">La bodega de origen, si el documento tiene.</param>
    /// <param name="corte">El corte del módulo (<c>INV_Setup</c>).</param>
    /// <param name="hoy">La fecha local de Colombia.</param>
    public static IReadOnlyList<Error> Evaluar(
        InventoryDocument documento,
        InventoryDocumentType tipo,
        BodegaDelDocumento? bodega,
        CorteDeInventario corte,
        DateOnly hoy)
    {
        var errores = new List<Error>();
        var clase = ClasesDeDocumento.De(documento.Class);
        var esAnulacion = documento.Class == DocumentClass.Voiding;
        var vivas = documento.Lines.Where(l => !l.IsDeleted).ToList();

        if (vivas.Count == 0) errores.Add(InventoryErrors.Empty());
        if (vivas.Count > InventoryDocument.MaxLineas) errores.Add(InventoryErrors.TooManyLines());

        // Campos que exige el tipo (la anulación sólo exige su motivo: lo demás lo copia del original).
        if (!esAnulacion)
        {
            if (tipo.RequiresCounterparty && documento.CounterpartyPersonId is null) errores.Add(InventoryErrors.FieldRequired(CampoContraparte));
            if ((tipo.RequiresCostCenter || clase.RequiresCostCenter) && documento.CostCenterId is null)
                errores.Add(InventoryErrors.FieldRequired(CampoCentroDeCosto));
            if (tipo.RequiresExternalReference && string.IsNullOrWhiteSpace(documento.ExternalReference))
                errores.Add(InventoryErrors.FieldRequired(CampoReferenciaExterna));
            if (clase.Warehouses != AdmittedWarehouses.None && documento.WarehouseId is null)
                errores.Add(InventoryErrors.FieldRequired(CampoBodega));
        }
        if ((tipo.RequiresReason || esAnulacion) && string.IsNullOrWhiteSpace(documento.Reason))
            errores.Add(InventoryErrors.FieldRequired(CampoMotivo));

        // Fecha: nunca futura salvo el tipo; nunca antes del inicio del módulo ni del corte de la bodega; nunca en un
        // período cerrado.
        var fecha = documento.OperationDate;
        if (fecha > hoy && !tipo.AllowsFutureDate) errores.Add(InventoryErrors.DateInFuture(fecha, hoy));
        if (corte.StartDate is { } inicio && fecha < inicio) errores.Add(InventoryErrors.DateBeforeCutoff(inicio));
        else if (bodega?.CutoffDate is { } corteDeBodega && fecha < corteDeBodega) errores.Add(InventoryErrors.DateBeforeCutoff(corteDeBodega));
        if (corte.LastClosedDate is { } cerrado && fecha <= cerrado) errores.Add(InventoryErrors.PeriodClosed(fecha.Year, fecha.Month, cerrado));

        // Bodega: no inactiva; activada salvo el saldo inicial y su anulación; el tránsito sólo donde la clase lo admite;
        // entre las permitidas del tipo.
        if (bodega is not null)
        {
            if (bodega.Inactiva) errores.Add(InventoryErrors.WarehouseInactive(bodega.PublicId, bodega.Code));
            else if (!bodega.Activa && documento.Class is not (DocumentClass.OpeningBalance or DocumentClass.Voiding))
                errores.Add(InventoryErrors.WarehouseNotActive(bodega.PublicId, bodega.Code));
            // US4 (T299, FR-091): el saldo inicial es sólo de una bodega que todavía no opera (su anulación la mira la estrategia).
            else if (bodega.Activa && documento.Class == DocumentClass.OpeningBalance)
                errores.Add(GoLive.GoLiveErrors.OpeningBalanceWarehouseActive(bodega.PublicId, bodega.Code));

            if (bodega.EsTransito && !esAnulacion && !clase.Warehouses.HasFlag(AdmittedWarehouses.Transit))
                errores.Add(InventoryErrors.TransitNotAllowed(bodega.Code));

            if (!esAnulacion && !tipo.AllWarehouses && tipo.Warehouses.Where(w => !w.IsDeleted).All(w => w.WarehouseId != bodega.Id))
                errores.Add(InventoryErrors.WarehouseNotAllowedForType(bodega.Code, tipo.Code));
        }

        return errores;
    }

    /// <summary>El aviso de un error, para <c>warnings[]</c>.</summary>
    public static AvisoDto ComoAviso(Error error) => new(error.Code, error.Message, (error as ErrorConDatos)?.Data);
}
