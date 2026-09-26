using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;

namespace IngenIA365ERP.Application.Inventory.Common;

/// <summary>
/// Los códigos de error del ciclo de documentos y de los tipos de documento (feature 012, T141; contracts/api.md §2.7,
/// §8 y §9.6; decisiones-transversales §2.17). Un solo catálogo para que todas las historias usen el mismo código, el
/// mismo mensaje en español y la misma forma de <c>data</c> (camelCase, lo que la persona necesita para corregir). Los
/// <c>*.NotFound</c> responden 404 —igual que sin permiso o fuera del alcance—; lo demás, 422. Lo fija
/// <c>InventoryErrorsTests</c>. (nuevo)
/// </summary>
public static class InventoryErrors
{
    // ----------------------------------------------------------------------------- documento --

    public const string DocumentNotFoundCode = "Inventory.Document.NotFound";

    /// <summary>404: no existe, es de otro grupo o está fuera del alcance (el mismo mensaje en los tres casos).</summary>
    public static Error DocumentNotFound() => new(DocumentNotFoundCode, "El documento no existe.");

    public static Error NotDraft(DocumentStatus status) => new ErrorConDatos("Inventory.Document.NotDraft",
        $"El documento ya no es un borrador (está {Estado(status)}): no se puede modificar.", new { status = status.ToString() });

    public static Error NotConfirmed(DocumentStatus status) => new ErrorConDatos("Inventory.Document.NotConfirmed",
        $"Sólo se anula un documento confirmado; éste está {Estado(status)}. Uno en aprobación se retira desde la bandeja de aprobaciones.",
        new { status = status.ToString() });

    public static Error TypeNotForRoute(DocumentClass clase, DocumentClassGroup grupo) => new ErrorConDatos("Inventory.Document.TypeNotForRoute",
        $"El tipo de documento es de la clase {clase}, que pertenece al grupo {grupo}: regístrelo desde su propia opción.",
        new { @class = clase.ToString(), group = grupo.ToString() });

    public static Error Empty() => new("Inventory.Document.Empty", "El documento no tiene líneas.");

    public static Error TooManyLines(int max = Domain.Entities.Inventory.Documents.InventoryDocument.MaxLineas) => new ErrorConDatos(
        "Inventory.Document.TooManyLines",
        $"Un documento admite hasta {max:N0} líneas. Pártalo en varios documentos.", new { max });

    public static Error FieldRequired(string field) => new ErrorConDatos("Inventory.Document.FieldRequired",
        $"El tipo de documento exige el campo «{field}».", new { field });

    public static Error WarehouseNotAllowedForType(string warehouseCode, string documentTypeCode) => new ErrorConDatos(
        "Inventory.Document.WarehouseNotAllowedForType",
        $"La bodega {warehouseCode} no está entre las permitidas del tipo {documentTypeCode}.", new { warehouseCode, documentTypeCode });

    public static Error TransitNotAllowed(string warehouseCode) => new ErrorConDatos("Inventory.Document.TransitNotAllowed",
        $"La bodega de tránsito {warehouseCode} no puede ser origen de una salida ni de un despacho.", new { warehouseCode });

    public static Error DateInFuture(DateOnly operationDate, DateOnly today) => new ErrorConDatos("Inventory.Document.DateInFuture",
        $"La fecha {operationDate:yyyy-MM-dd} es posterior a hoy ({today:yyyy-MM-dd}) y el tipo no admite fechas futuras.",
        new { operationDate, today });

    public static Error DateBeforeCutoff(DateOnly cutoffDate) => new ErrorConDatos("Inventory.Document.DateBeforeCutoff",
        $"La fecha es anterior al corte ({cutoffDate:yyyy-MM-dd}): antes de esa fecha no se registran documentos.", new { cutoffDate });

    public static Error AlreadyVoided(Guid voidingDocumentPublicId, string? displayNumber) => new ErrorConDatos("Inventory.Document.AlreadyVoided",
        $"El documento ya fue anulado{(displayNumber is null ? "" : $" por {displayNumber}")}.", new { voidingDocumentPublicId, displayNumber });

    public static Error VoidingNotVoidable() => new("Inventory.Document.VoidingNotVoidable",
        "Una anulación no se anula. Si hace falta, registre el documento de nuevo.");

    /// <summary>Un dependiente vigente del documento que se quiere anular (<c>data.dependents</c>).</summary>
    public sealed record Dependiente(Guid PublicId, string Class, string? DisplayNumber, string Status);

    public static Error HasDependents(IReadOnlyList<Dependiente> dependents) => new ErrorConDatos("Inventory.Document.HasDependents",
        $"El documento tiene documentos vigentes que dependen de él ({string.Join(", ", dependents.Select(d => d.DisplayNumber ?? d.Class))}): anúlelos primero.",
        new { dependents });

    public static Error FiscalUseCorrection() => new("Inventory.Document.FiscalUseCorrection",
        "Un documento fiscal emitido no se anula con un documento contrario: se corrige con su nota.");

    // ------------------------------------------------------------------------ tipos y clases --

    public const string DocumentTypeNotFoundCode = "Inventory.DocumentType.NotFound";

    public static Error DocumentTypeNotFound() => new(DocumentTypeNotFoundCode, "El tipo de documento no existe.");

    public static Error DocumentTypeInactive(string code) => new ErrorConDatos("Inventory.DocumentType.Inactive",
        $"El tipo de documento {code} está inactivo: no se usa en documentos nuevos.", new { documentTypeCode = code });

    public static Error DocumentClassNotAvailable(DocumentClass clase) => new ErrorConDatos("Inventory.DocumentClass.NotAvailable",
        $"La clase {clase} todavía no está disponible: llega con la entrega {ClasesDeDocumento.De(clase).AvailableFrom}.",
        new { @class = clase.ToString(), availableIn = ClasesDeDocumento.De(clase).AvailableFrom.ToString() });

    public static Error FlagNotApplicable(string flag, DocumentClass clase) => new ErrorConDatos("Inventory.DocumentType.FlagNotApplicable",
        $"La marca «{flag}» no aplica a la clase {clase}.", new { flag, @class = clase.ToString() });

    public static Error TypeTransitNotAllowed() => new("Inventory.DocumentType.TransitNotAllowed",
        "Una bodega de tránsito no puede ser bodega permitida de un tipo de documento: el tránsito lo manejan los traslados.");

    public static Error NumberedByResolution(DocumentClass clase) => new ErrorConDatos("Inventory.DocumentType.NumberedByResolution",
        $"La clase {clase} numera con la resolución de la DIAN: el tipo declara el prefijo de su resolución y no tiene consecutivo propio.",
        new { @class = clase.ToString() });

    public static Error HasOpenDocuments(int drafts, int pendingApproval) => new ErrorConDatos("Inventory.DocumentType.HasOpenDocuments",
        $"El tipo tiene {drafts} borrador(es) y {pendingApproval} documento(s) en aprobación: confírmelos o descártelos antes de inactivarlo.",
        new { drafts, pendingApproval });

    public static Error RequiredBySystem(DocumentClass clase) => new ErrorConDatos("Inventory.DocumentType.RequiredBySystem",
        $"Es el último tipo activo de la clase {clase}, que el sistema genera solo: no se puede inactivar.", new { @class = clase.ToString() });

    // --------------------------------------------------------------------------- numeración --

    public static Error SequenceNumberAlreadyIssued(long lastIssued) => new ErrorConDatos("Inventory.Sequence.NumberAlreadyIssued",
        $"Ya se emitió hasta el número {lastIssued:N0} con ese prefijo: el consecutivo debe continuar después.", new { lastIssued });

    public static Error SequenceOverlaps() => new("Inventory.Sequence.Overlaps",
        "La vigencia se cruza con la de otro consecutivo del mismo tipo.");

    public static Error SequenceMissing(string documentTypeCode, DateOnly operationDate) => new ErrorConDatos("Inventory.Numbering.SequenceMissing",
        $"El tipo de documento {documentTypeCode} no tiene consecutivo vigente al {operationDate:yyyy-MM-dd}. Regístrelo en Inventario › Tipos de documento.",
        new { documentTypeCode, operationDate });

    // ------------------------------------------------------------------------ período y bodega --

    public static Error PeriodClosed(int year, int month, DateOnly lastClosedDate) => new ErrorConDatos("Inventory.Period.Closed",
        $"El período {year}-{month:00} está cerrado (el último cierre es del {lastClosedDate:yyyy-MM-dd}): use una fecha posterior.",
        new { year, month, lastClosedDate });

    /// <summary>Un mes como lo nombran los errores de períodos: <c>{ year, month }</c>.</summary>
    public sealed record MesDeInventario(int Year, int Month)
    {
        public static MesDeInventario De(DateOnly fecha) => new(fecha.Year, fecha.Month);

        public override string ToString() => $"{Year}-{Month:00}";
    }

    /// <summary>US3, T289 (nuevo): sin <c>INV_Setup</c> no hay meses que cerrar (el módulo arranca con la primera fecha de corte, US4).</summary>
    public static Error PeriodNotStarted() => new("Inventory.Period.NotStarted",
        "El inventario todavía no arrancó: registre la fecha de corte y el saldo inicial de la primera bodega antes de cerrar un mes.");

    /// <summary>§13.4: sólo el mes siguiente al último cerrado.</summary>
    public static Error PeriodNotNext(int year, int month, MesDeInventario nextToClose) => new ErrorConDatos("Inventory.Period.NotNext",
        $"Los meses se cierran en orden: el siguiente por cerrar es {nextToClose}, no {year}-{month:00}.",
        new { year, month, nextToClose });

    /// <summary>§13.4: sólo un mes que ya terminó en hora de Colombia.</summary>
    public static Error PeriodNotEnded(int year, int month, DateOnly lastDay) => new ErrorConDatos("Inventory.Period.NotEnded",
        $"El mes {year}-{month:00} todavía no termina (su último día es el {lastDay:yyyy-MM-dd}).",
        new { year, month, lastDay });

    /// <summary>Un conteo abierto con foto en el mes (lo llena US11).</summary>
    public sealed record ConteoAbierto(Guid CountPublicId, string? DisplayNumber, string Warehouse, DateOnly SnapshotDate);

    /// <summary>§13.4, US3-4: los conteos abiertos con foto en el mes bloquean el cierre.</summary>
    public static Error PeriodOpenCounts(int year, int month, IReadOnlyList<ConteoAbierto> counts) => new ErrorConDatos("Inventory.Period.OpenCounts",
        $"El mes {year}-{month:00} tiene {counts.Count} conteo(s) abierto(s) con foto: ciérrelos o descártelos antes de cerrar.",
        new { year, month, counts });

    /// <summary>§13.4: el primer intento con avisos responde con ellos; con <c>acknowledgeWarnings</c> cierra.</summary>
    public static Error PeriodWarningsNotAcknowledged(int year, int month, object warnings) => new ErrorConDatos("Inventory.Period.WarningsNotAcknowledged",
        $"El mes {year}-{month:00} tiene avisos (borradores, documentos en aprobación, traslados o mensajes pendientes). Revíselos y confirme que cierra de todos modos.",
        new { year, month, warnings });

    /// <summary>§13.4 (I6): remisiones sin facturar sin aceptar.</summary>
    public static Error PeriodUnbilledShipmentsNotAccepted(int year, int month, object unbilledShipments) => new ErrorConDatos(
        "Inventory.Period.UnbilledShipmentsNotAccepted",
        $"El mes {year}-{month:00} tiene remisiones sin facturar: acéptelas con motivo para cerrar.",
        new { year, month, unbilledShipments });

    /// <summary>§13.4 (I6): aceptar remisiones sin facturar exige <c>Inventory.Periods.AcceptUnbilledShipments</c>.</summary>
    public static Error PeriodAcceptUnbilledNotAllowed(string permissionCode) => new ErrorConDatos("Inventory.Period.AcceptUnbilledNotAllowed",
        $"Aceptar remisiones sin facturar exige el permiso {permissionCode}.", new { permissionCode });

    /// <summary>§13.4, US3-5: sólo se reabre el último cerrado.</summary>
    public static Error PeriodNotLastClosed(int year, int month, MesDeInventario lastClosed) => new ErrorConDatos("Inventory.Period.NotLastClosed",
        $"Sólo se reabre el último mes cerrado ({lastClosed}), no {year}-{month:00}.",
        new { year, month, lastClosed });

    public static Error PeriodNotClosed(int year, int month) => new ErrorConDatos("Inventory.Period.NotClosed",
        $"El mes {year}-{month:00} no está cerrado.", new { year, month });

    // ------------------------------------------------------------------------------- costeo (US3) --

    /// <summary>
    /// FR-045, T285 (nuevo): un documento que deja un movimiento con fecha anterior a otro ya registrado del mismo producto y
    /// ámbito, fuera de las dos clases que I1 admite (saldo inicial de bodega no activa y ajuste de conteo). Nombra el
    /// movimiento posterior. <c>Costeo.RetroactivosPermitidos</c> no lo habilita hasta I5.
    /// </summary>
    public static Error RetroactiveNotAllowed(int lineNumber, string productCode, Guid laterDocumentPublicId, string? laterDocumentNumber, DateOnly laterOperationDate) =>
        new ErrorConDatos("Inventory.Costing.RetroactiveNotAllowed",
            $"Línea {lineNumber}: {productCode} ya tiene un movimiento posterior ({laterDocumentNumber ?? "sin número"}, del {laterOperationDate:yyyy-MM-dd}). " +
            "Un documento con fecha anterior cambiaría el costo ya registrado: use una fecha igual o posterior.",
            new { lineNumber, productCode, laterMovement = new { documentPublicId = laterDocumentPublicId, displayNumber = laterDocumentNumber, operationDate = laterOperationDate } });

    // ------------------------------------------------------------------------ modo de paso (US3) --

    /// <summary>Un tipo de documento como lo nombran los errores del modo de paso.</summary>
    public sealed record TipoNombrado(Guid PublicId, string Code, string Name, string? Class = null);

    /// <summary>§7, FR-075: un tipo encadenado no admite un modo propio; va por la cadena.</summary>
    public static Error PostingModeChainMismatch(PostingChain chain, IReadOnlyList<TipoNombrado> documentTypes) => new ErrorConDatos(
        "Inventory.PostingMode.ChainMismatch",
        $"El tipo pertenece a la cadena {chain}: el modo de paso se registra para toda la cadena ({string.Join(", ", documentTypes.Select(t => t.Code))}).",
        new { chain = chain.ToString(), documentTypes });

    /// <summary>§7, FR-075: dejar sin paso un tipo fiscal exige confirmarlo.</summary>
    public static Error PostingModeFiscalRequiresConfirmation(IReadOnlyList<TipoNombrado> fiscalDocumentTypes) => new ErrorConDatos(
        "Inventory.PostingMode.FiscalRequiresConfirmation",
        $"Dejar sin paso a contabilidad documentos fiscales ({string.Join(", ", fiscalDocumentTypes.Select(t => t.Code))}) exige confirmarlo.",
        new { fiscalDocumentTypes });

    public static Error WarehouseNotActive(Guid warehousePublicId, string warehouseCode) => new ErrorConDatos("Inventory.Warehouse.NotActive",
        $"La bodega {warehouseCode} todavía no está activa: sólo admite el saldo inicial y su anulación.",
        new { warehousePublicId, warehouseCode, allowedClasses = new[] { nameof(DocumentClass.OpeningBalance), nameof(DocumentClass.Voiding) } });

    public static Error WarehouseInactive(Guid warehousePublicId, string warehouseCode) => new ErrorConDatos("Inventory.Warehouse.Inactive",
        $"La bodega {warehouseCode} está inactiva.", new { warehousePublicId, warehouseCode });

    // ------------------------------------------------------------------ producto, unidad, ubicación --

    public static Error ProductNotInventoriable(int lineNumber, string productCode) => new ErrorConDatos("Inventory.Product.NotInventoriable",
        $"Línea {lineNumber}: el producto {productCode} no maneja existencias.", new { lineNumber, productCode });

    public static Error ProductInactive(int lineNumber, string productCode) => new ErrorConDatos("Inventory.Product.Inactive",
        $"Línea {lineNumber}: el producto {productCode} está inactivo.", new { lineNumber, productCode });

    public static Error ProductBlocked(int lineNumber, string productCode) => new ErrorConDatos("Inventory.Product.Blocked",
        $"Línea {lineNumber}: el producto {productCode} está bloqueado.", new { lineNumber, productCode });

    public static Error UnitNotForProduct(int lineNumber, string productCode, string unitCode) => new ErrorConDatos("Inventory.Unit.NotForProduct",
        $"Línea {lineNumber}: la unidad {unitCode} no es la base ni una alterna del producto {productCode}.", new { lineNumber, productCode, unitCode });

    public static Error UnitDecimalsNotAllowed(int lineNumber, string productCode, string unitCode, int allowedDecimals, decimal quantityBase) =>
        new ErrorConDatos("Inventory.Unit.DecimalsNotAllowed",
            $"Línea {lineNumber}: la unidad {unitCode} admite {allowedDecimals} decimal(es) y la cantidad da {quantityBase}.",
            new { lineNumber, productCode, unitCode, allowedDecimals, quantityBase });

    public static Error LocationNotInWarehouse(int lineNumber, string productCode) => new ErrorConDatos("Inventory.Location.NotInWarehouse",
        $"Línea {lineNumber}: la ubicación no es de la bodega del documento.", new { lineNumber, productCode });

    public static Error CountProductsLocked(Guid countPublicId, string? displayNumber, IReadOnlyList<string> products) => new ErrorConDatos(
        "Inventory.Count.ProductsLocked",
        // Un conteo abierto todavía no tiene número (se numera al cerrar, US11): el mensaje no lo exige.
        $"Un conteo físico abierto{(displayNumber is null ? string.Empty : " " + displayNumber)} en la bodega bloquea los movimientos de {string.Join(", ", products)} hasta que se cierre o se descarte.",
        new { countPublicId, displayNumber, products });

    // ----------------------------------------------------------------------------- existencia --

    /// <summary>Una línea que no cabe en la existencia (<c>data.lines[]</c>).</summary>
    public sealed record LineaSinExistencia(int LineNumber, Guid ProductPublicId, string ProductCode, Guid WarehousePublicId,
        Guid? LocationPublicId, decimal Requested, decimal Available);

    /// <summary>
    /// FR-004: la primera línea que falla va en la raíz de <c>data</c> y todas en <c>data.lines</c>; al anular una
    /// entrada ya consumida, <c>suggestion</c> propone <c>SupplierReturn</c> o <c>NegativeAdjustment</c>.
    /// </summary>
    public static Error StockInsufficient(IReadOnlyList<LineaSinExistencia> lines, string? suggestion = null)
    {
        var primera = lines[0];
        return new ErrorConDatos("Inventory.Stock.Insufficient",
            $"Línea {primera.LineNumber}: no hay existencia suficiente de {primera.ProductCode} (pide {primera.Requested}, hay {primera.Available}).",
            new
            {
                lineNumber = primera.LineNumber,
                productPublicId = primera.ProductPublicId,
                productCode = primera.ProductCode,
                warehousePublicId = primera.WarehousePublicId,
                locationPublicId = primera.LocationPublicId,
                requested = primera.Requested,
                available = primera.Available,
                lines,
                suggestion,
            });
    }

    // ----------------------------------------------------------------------------- ajustes (US2) --

    /// <summary>Campo de <c>data.field</c> de la causa de ajuste obligatoria (<c>NegativeAdjustment</c>, <c>WriteOff</c>).</summary>
    public const string CampoCausa = "adjustmentCause";

    /// <summary>
    /// 422, no 404: un permiso que depende del cuerpo (decisiones-transversales §2.9). El costo digitado de un ajuste positivo
    /// exige <c>Inventory.Adjustments.SetUnitCost</c>.
    /// </summary>
    public static Error AdjustmentUnitCostNotAllowed(int lineNumber, string permissionCode) => new ErrorConDatos(
        "Inventory.Adjustment.UnitCostNotAllowed",
        $"Línea {lineNumber}: indicar el costo unitario exige el permiso {permissionCode}. Deje el costo vacío para usar el costo vigente.",
        new { lineNumber, permissionCode });

    public static Error AdjustmentUnitCostOnlyOnEntries(int lineNumber) => new ErrorConDatos("Inventory.Adjustment.UnitCostOnlyOnEntries",
        $"Línea {lineNumber}: el costo unitario sólo se indica en una entrada; una salida sale al costo vigente.", new { lineNumber });

    public static Error AdjustmentTaxableWithdrawalNotAvailable() => new("Inventory.Adjustment.TaxableWithdrawalNotAvailable",
        "El retiro gravado necesita la lista de precios general, que llega con la entrega I3. Use un tipo de consumo interno no gravado.");

    // ------------------------------------------------------------ aprobación, validación, moneda --

    public static Error AmountExceedsLimit(decimal amount, decimal maxAmount, string currency, string permissionCode) => new ErrorConDatos(
        "Inventory.Approval.AmountExceedsLimit",
        $"El monto {amount:N2} {currency} supera su monto máximo ({maxAmount:N2}) para {permissionCode}.",
        new { amount, maxAmount, currency, permissionCode });

    /// <summary>Un error de la validación previa contable (<c>data.errors[]</c>, FR-074).</summary>
    public sealed record ErrorDeValidacionPrevia(int? LineNumber, string? Account, string Rule, string Message, object? WhoFixes);

    public static Error PrevalidationNotPostable(IReadOnlyList<ErrorDeValidacionPrevia> errors) => new ErrorConDatos(
        "Inventory.Prevalidation.NotPostable",
        "Contabilidad no puede contabilizar el documento; corrija lo indicado y vuelva a confirmar.", new { errors });

    public static Error PrevalidationNoResponse() => new ErrorConDatos("Inventory.Prevalidation.NoResponse",
        "Contabilidad no respondió a tiempo y la política es no confirmar sin su validación. Intente de nuevo.", new { errors = Array.Empty<object>() });

    public static Error CurrencyNotSupported() => new("Inventory.Currency.NotSupported",
        "Los documentos se registran en pesos colombianos (COP) con tasa de cambio 1.");

    private static string Estado(DocumentStatus status) => status switch
    {
        DocumentStatus.Draft => "en borrador",
        DocumentStatus.PendingApproval => "en aprobación",
        DocumentStatus.Confirmed => "confirmado",
        DocumentStatus.Voided => "anulado",
        DocumentStatus.Discarded => "descartado",
        _ => status.ToString(),
    };
}
