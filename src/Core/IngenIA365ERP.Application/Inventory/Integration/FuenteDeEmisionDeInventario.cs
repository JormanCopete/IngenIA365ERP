using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.ElectronicInvoicing;
using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Integration;

/// <summary>
/// Inventario como fuente de documentos electrónicos (feature 012, I4, T704; contracts/dian.md §4.1 y §8.3; api.md §24.5;
/// decisiones-transversales T40): arma la <see cref="EntradaDeDocumentoElectronico"/> desde <c>INV_Documents</c>,
/// <c>INV_DocumentLines</c>, <c>INV_DocumentTaxLines</c>, <c>INV_DocumentPayments</c>, <c>INV_DocumentLinks</c> y todas las
/// versiones de <c>INV_DocumentPartySnapshots</c> (el constructor usa la mayor), y hace en su base lo que los casos b y c de un
/// rechazo le piden: el borrador de reemplazo, la anulación sin efecto fiscal y la confirmación del reemplazo. La plataforma
/// (<c>ElectronicInvoicing</c>) nunca lee tablas <c>INV_</c>: llega aquí por <see cref="IFuenteDeDocumentoElectronico"/>. (nuevo)
/// </summary>
public sealed class FuenteDeEmisionDeInventario(
    IApplicationDbContext db,
    IActorActual actorActual,
    IDateTimeService reloj,
    ILectorDeParametros parametros,
    ConfirmacionDeDocumento confirmacion,
    RechazoFiscalEnCurso? rechazoEnCurso = null,
    Purchasing.EventosRadianDeInventario? eventosRadian = null) : IFuenteDeDocumentoElectronico
{
    /// <summary>La fuente se armó sin los eventos RADIAN de compras (sólo en pruebas que no los usan). (nuevo)</summary>
    public const string RadianNotAvailableCode = "Inventory.RadianEvent.NotAvailable";
    /// <summary>El módulo fuente que se sella en <c>COR_ElectronicDocuments.SourceModule</c>.</summary>
    public const string Modulo = "INV";

    public string SourceModule => Modulo;

    /// <summary>El tipo de documento electrónico de una clase fiscal electrónica; nulo si la clase no emite (contracts/dian.md §4.3).</summary>
    public static ElectronicDocumentKind? TipoElectronicoDe(DocumentClass clase) => clase switch
    {
        DocumentClass.SalesInvoice or DocumentClass.SalesInvoiceFromShipments => ElectronicDocumentKind.Invoice,
        DocumentClass.CreditNote => ElectronicDocumentKind.CreditNote,
        DocumentClass.DebitNote => ElectronicDocumentKind.DebitNote,
        DocumentClass.PosEquivalentDocument => ElectronicDocumentKind.PosEquivalent,
        DocumentClass.PosAdjustmentNote => ElectronicDocumentKind.PosAdjustmentNote,
        DocumentClass.SupportDocument => ElectronicDocumentKind.SupportDocument,
        DocumentClass.SupportDocumentAdjustmentNote => ElectronicDocumentKind.SupportDocumentAdjustmentNote,
        _ => null,
    };

    /// <summary>La ruta de la API donde se edita el borrador de una clase (api.md §24.5, <c>editRoute</c>).</summary>
    public static string RutaDeEdicion(DocumentClass clase, Guid publicId) => clase switch
    {
        DocumentClass.CreditNote or DocumentClass.PosAdjustmentNote => $"/api/inventory/sales/credit-notes/{publicId}",
        // I6 (T888): la nota débito tiene su propia ruta (contracts/api.md §18.4).
        DocumentClass.DebitNote => $"/api/inventory/sales/debit-notes/{publicId}",
        DocumentClass.SupportDocument or DocumentClass.SupportDocumentAdjustmentNote => $"/api/inventory/purchases/support-documents/{publicId}",
        _ => $"/api/inventory/sales/invoices/{publicId}",
    };

    // ------------------------------------------------------------------------------------------ lectura --

    public async Task<Result<EntradaDeDocumentoElectronico>> LeerAsync(Guid sourceDocumentPublicId, CancellationToken ct)
    {
        var d = await db.InventoryDocuments.AsNoTracking()
            .Include(x => x.DocumentType)
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.PublicId == sourceDocumentPublicId && !x.IsDeleted, ct);
        if (d is null) return Result.Failure<EntradaDeDocumentoElectronico>(InventoryErrors.DocumentNotFound());
        if (TipoElectronicoDe(d.Class) is not { } tipo)
            return Result.Failure<EntradaDeDocumentoElectronico>(InventoryErrors.DocumentClassNotAvailable(d.Class));
        if (d.Status is not (DocumentStatus.Confirmed or DocumentStatus.Voided))
            return Result.Failure<EntradaDeDocumentoElectronico>(InventoryErrors.NotConfirmed(d.Status));

        var lineas = d.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber).ToList();
        var productoIds = lineas.Select(l => l.ProductId).Distinct().ToList();
        var unidadIds = lineas.Select(l => l.UnitId).Distinct().ToList();
        var productos = await db.Products.AsNoTracking().Where(p => productoIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new { p.Code, p.Name }, ct);
        var unidades = await db.UnitsOfMeasure.AsNoTracking().Where(u => unidadIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new { u.Code, u.DianUnitCode }, ct);
        var numeroDeLinea = lineas.ToDictionary(l => l.Id, l => l.LineNumber);

        var impuestos = await db.DocumentTaxLines.AsNoTracking().Where(t => t.DocumentId == d.Id && !t.IsDeleted).OrderBy(t => t.Id).ToListAsync(ct);
        var pagos = await db.DocumentPayments.AsNoTracking().Where(p => p.DocumentId == d.Id && !p.IsDeleted).OrderBy(p => p.LineNumber).ToListAsync(ct);
        var fotos = await db.DocumentPartySnapshots.AsNoTracking().Where(f => f.DocumentId == d.Id && !f.IsDeleted).ToListAsync(ct);

        // Lo que el documento referencia: el corregido (NoteOf, el documento es el destino) y las remisiones (FromShipment).
        var origenes = await db.DocumentLinks.AsNoTracking()
            .Where(l => l.TargetDocumentId == d.Id && !l.IsDeleted && (l.Kind == DocumentLinkKind.NoteOf || l.Kind == DocumentLinkKind.FromShipment || l.Kind == DocumentLinkKind.FromOrder))
            .Join(db.InventoryDocuments.AsNoTracking(), l => l.SourceDocumentId, o => o.Id,
                (l, o) => new { l.Kind, o.PublicId, o.Prefix, o.Number, o.OperationDate, o.Id })
            .OrderBy(x => x.Id)
            .ToListAsync(ct);
        var corregido = origenes.FirstOrDefault(o => o.Kind == DocumentLinkKind.NoteOf);
        var pedido = origenes.FirstOrDefault(o => o.Kind == DocumentLinkKind.FromOrder);

        return Result.Success(new EntradaDeDocumentoElectronico
        {
            SourceModule = Modulo,
            DocumentPublicId = d.PublicId,
            DocumentClass = d.Class.ToString(),
            DocumentTypeCode = d.DocumentType?.Code ?? string.Empty,
            DocumentNumber = VistaDeDocumentos.NumeroVisible(d.Prefix, d.Number) ?? string.Empty,
            Kind = tipo,
            OperationDate = d.OperationDate,
            ConfirmedAtUtc = d.ConfirmedAt ?? reloj.UtcNow,
            DueDate = d.DueDate,
            Currency = d.Currency,
            ExchangeRate = d.ExchangeRate,
            Contrapartes = fotos.Select(Foto).ToList(),
            Lineas = lineas.Select(l => new LineaDeEntrada(
                l.LineNumber,
                productos.TryGetValue(l.ProductId, out var p) ? p.Code : string.Empty,
                string.IsNullOrWhiteSpace(l.Description) ? (productos.TryGetValue(l.ProductId, out var q) ? q.Name : string.Empty) : l.Description!,
                l.Quantity,
                unidades.TryGetValue(l.UnitId, out var u) ? u.Code : string.Empty,
                unidades.TryGetValue(l.UnitId, out var v) ? v.DianUnitCode : null,
                l.UnitPrice,
                l.GrossAmount,
                l.DiscountAmount)).ToList(),
            Impuestos = impuestos.Select(t => new ImpuestoDeEntrada(
                t.DocumentLineId is { } lid && numeroDeLinea.TryGetValue(lid, out var n) ? n : null,
                t.TaxRateCode,
                t.DianTaxCode,
                t.Treatment is TaxTreatment.WithholdingApplied or TaxTreatment.WithholdingSuffered,
                t.Rate,
                t.AmountPerUnit,
                t.TaxableUnits,
                t.Base,
                t.Amount)).ToList(),
            // El número de la tarjeta nunca se guarda; la referencia es la autorización o la del medio.
            Pagos = pagos.Select(p => new PagoDeEntrada(p.MeansCode, p.MeansName, p.MeansClass,
                string.IsNullOrWhiteSpace(p.DianPaymentMeansCode) ? null : p.DianPaymentMeansCode,
                p.Amount, p.AuthorizationCode ?? p.Reference)).ToList(),
            Totales = new TotalesDeEntrada(d.Subtotal, d.DiscountTotal, d.TaxTotal, d.WithholdingTotal, d.Total, d.AmountDue),
            Correccion = corregido is null
                ? null
                : new CorreccionDeEntrada(corregido.PublicId, VistaDeDocumentos.NumeroVisible(corregido.Prefix, corregido.Number) ?? string.Empty,
                    corregido.OperationDate, d.CorrectionConceptCode),
            OrderReference = pedido is null ? null : VistaDeDocumentos.NumeroVisible(pedido.Prefix, pedido.Number),
            Despatches = origenes.Where(o => o.Kind == DocumentLinkKind.FromShipment)
                .Select(o => VistaDeDocumentos.NumeroVisible(o.Prefix, o.Number) ?? string.Empty).ToList(),
            Pos = await PosAsync(d, tipo, ct),
            DocumentoSoporte = await SoporteAsync(tipo, d.OperationDate, ct),
            Notas = string.IsNullOrWhiteSpace(d.Notes) ? [] : [d.Notes!.Trim()],
        });
    }

    private static FotoFiscalDeEntrada Foto(DocumentPartySnapshot f) => new()
    {
        Version = f.Version,
        OrganizationType = f.DianOrganizationType,
        IdTypeCode = f.DianIdTypeCode,
        TaxId = f.TaxId,
        CheckDigit = f.CheckDigit,
        LegalName = f.LegalName,
        Address = f.Address,
        MunicipalityDaneCode = f.MunicipalityDaneCode,
        CountryCode = f.CountryCode,
        Email = f.Email,
        Phone = f.Phone,
        Responsibilities = f.DianResponsibilities,
        TaxSchemeCode = f.DianTaxSchemeCode,
        IsVatResponsible = f.IsVatResponsible,
        IsLargeContributor = f.IsLargeContributor,
        IsSelfWithholder = f.IsSelfWithholder,
        IsVatWithholdingAgent = f.IsVatWithholdingAgent,
        IsSimpleTaxRegime = f.IsSimpleTaxRegime,
        CiiuCode = f.CiiuCode,
    };

    private async Task<PosDeEntrada?> PosAsync(InventoryDocument d, ElectronicDocumentKind tipo, CancellationToken ct)
    {
        if (tipo is not (ElectronicDocumentKind.PosEquivalent or ElectronicDocumentKind.PosAdjustmentNote)) return null;
        var caja = d.CashRegisterId is int cid
            ? await db.CashRegisters.AsNoTracking().Where(c => c.Id == cid).Select(c => new { c.DianCashRegisterPlate, c.DianCashRegisterTypeCode }).FirstOrDefaultAsync(ct)
            : null;
        var ubicacion = d.PointOfSaleId is int pid
            ? await db.PointsOfSale.AsNoTracking().Where(p => p.Id == pid).Select(p => p.Address).FirstOrDefaultAsync(ct)
            : null;
        var cajero = d.CashSessionId is int sid
            ? await db.CashSessions.AsNoTracking().Where(s => s.Id == sid).Select(s => s.CashierName).FirstOrDefaultAsync(ct)
            : null;
        return new PosDeEntrada(caja?.DianCashRegisterPlate, caja?.DianCashRegisterTypeCode, ubicacion, cajero);
    }

    private async Task<DocumentoSoporteDeEntrada?> SoporteAsync(ElectronicDocumentKind tipo, DateOnly fecha, CancellationToken ct)
    {
        if (tipo is not (ElectronicDocumentKind.SupportDocument or ElectronicDocumentKind.SupportDocumentAdjustmentNote)) return null;
        var leido = await parametros.LeerComoAsync<string>(ParametrosDeFacturacionElectronica.Modulo, ParametrosDeFacturacionElectronica.DocumentoSoporteGeneracion, fecha, ct: ct);
        // «Semanal» es la generación acumulada de la Res. 167/2021 (art. 2); su período lo fija I4 al agrupar (§14.2).
        var semanal = leido.IsSuccess && string.Equals(leido.Value, "Semanal", StringComparison.OrdinalIgnoreCase);
        return new DocumentoSoporteDeEntrada(semanal ? "Weekly" : ConstructorDelCanonico.GeneracionPorOperacion, null, null);
    }

    // ------------------------------------------------------------------------------------------ caso b: borrador --

    public async Task<Result<BorradorDeReemplazo>> CrearBorradorDeReemplazoAsync(Guid rejectedSourceDocumentPublicId, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<BorradorDeReemplazo>(ErroresDelDocumento.SinUsuario());

        var original = await db.InventoryDocuments.Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.PublicId == rejectedSourceDocumentPublicId && !x.IsDeleted, ct);
        if (original is null) return Result.Failure<BorradorDeReemplazo>(InventoryErrors.DocumentNotFound());
        if (TipoElectronicoDe(original.Class) is null) return Result.Failure<BorradorDeReemplazo>(InventoryErrors.DocumentClassNotAvailable(original.Class));
        if (original.Status != DocumentStatus.Confirmed) return Result.Failure<BorradorDeReemplazo>(InventoryErrors.NotConfirmed(original.Status));

        var vivo = await BorradorDeReemplazoVivoAsync(original.Id, ct);
        if (vivo is { } existente) return Result.Failure<BorradorDeReemplazo>(ErroresDeFacturacionElectronica.ReplacementDraftExists(existente));

        var borrador = new InventoryDocument
        {
            Class = original.Class,
            DocumentTypeId = original.DocumentTypeId,
            OperationDate = reloj.HoyLocal,
            CreatedByUserId = usuario,
            WarehouseId = original.WarehouseId,
            DestinationWarehouseId = original.DestinationWarehouseId,
            BranchId = original.BranchId,
            CostCenterId = original.CostCenterId,
            CounterpartyPersonId = original.CounterpartyPersonId,
            SalespersonId = original.SalespersonId,
            SalesChannelId = original.SalesChannelId,
            PointOfSaleId = original.PointOfSaleId,
            CashRegisterId = original.CashRegisterId,
            Currency = original.Currency,
            ExchangeRate = original.ExchangeRate,
            OperationMunicipalityDaneCode = original.OperationMunicipalityDaneCode,
            DueDate = original.DueDate,
            ReturnsGoods = original.ReturnsGoods,
            IsFullReversal = original.IsFullReversal,
            CorrectionConceptCode = original.CorrectionConceptCode,
            Notes = original.Notes,
            Subtotal = original.Subtotal,
            DiscountTotal = original.DiscountTotal,
            TaxTotal = original.TaxTotal,
            WithholdingTotal = original.WithholdingTotal,
            Total = original.Total,
            AmountDue = original.AmountDue,
        };
        foreach (var linea in original.Lines.Where(l => !l.IsDeleted).OrderBy(l => l.LineNumber))
        {
            borrador.Lines.Add(new InventoryDocumentLine
            {
                Document = borrador,
                LineNumber = linea.LineNumber,
                ProductId = linea.ProductId,
                UnitId = linea.UnitId,
                Quantity = linea.Quantity,
                Factor = linea.Factor,
                QuantityBase = linea.QuantityBase,
                RoundingQuantity = linea.RoundingQuantity,
                UnitPrice = linea.UnitPrice,
                ListPrice = linea.ListPrice,
                PriceListId = linea.PriceListId,
                ListPriceIncludesTaxes = linea.ListPriceIncludesTaxes,
                GrossAmount = linea.GrossAmount,
                DiscountAmount = linea.DiscountAmount,
                NetAmount = linea.NetAmount,
                LocationId = linea.LocationId,
                LotId = linea.LotId,
                SerialId = linea.SerialId,
                Description = linea.Description,
                AffectsCost = linea.AffectsCost,
            });
        }
        db.InventoryDocuments.Add(borrador);
        db.DocumentLinks.Add(new DocumentLink { SourceDocument = original, TargetDocument = borrador, Kind = DocumentLinkKind.ReplacementOf });
        await db.SaveChangesAsync(ct);

        return Result.Success(new BorradorDeReemplazo(borrador.PublicId, Modulo, RutaDeEdicion(borrador.Class, borrador.PublicId)));
    }

    /// <summary>El <c>PublicId</c> del borrador de reemplazo vivo (borrador o en aprobación) del documento, si lo hay.</summary>
    private Task<Guid?> BorradorDeReemplazoVivoAsync(int originalId, CancellationToken ct) =>
        db.DocumentLinks.AsNoTracking()
            .Where(l => l.SourceDocumentId == originalId && l.Kind == DocumentLinkKind.ReplacementOf && !l.IsDeleted)
            .Join(db.InventoryDocuments.AsNoTracking(), l => l.TargetDocumentId, d => d.Id, (l, d) => d)
            .Where(d => !d.IsDeleted && (d.Status == DocumentStatus.Draft || d.Status == DocumentStatus.PendingApproval))
            .Select(d => (Guid?)d.PublicId)
            .FirstOrDefaultAsync(ct);

    // ------------------------------------------------------------------------------------------ casos b y c: anular --

    public async Task<Result<AnulacionSinEfectoFiscal>> AnularSinEfectoFiscalAsync(Guid rejectedSourceDocumentPublicId, CasoFiscalDeAnulacion caso, string motivo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(motivo)) throw new ArgumentException("Anular sin efecto fiscal exige el motivo.", nameof(motivo));
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<AnulacionSinEfectoFiscal>(ErroresDelDocumento.SinUsuario());

        var original = await db.InventoryDocuments.Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.PublicId == rejectedSourceDocumentPublicId && !x.IsDeleted, ct);
        if (original is null) return Result.Failure<AnulacionSinEfectoFiscal>(InventoryErrors.DocumentNotFound());
        if (TipoElectronicoDe(original.Class) is null) return Result.Failure<AnulacionSinEfectoFiscal>(InventoryErrors.DocumentClassNotAvailable(original.Class));

        var anulacionPrevia = await db.InventoryDocuments.AsNoTracking()
            .Where(x => x.VoidsDocumentId == original.Id && x.Status != DocumentStatus.Discarded)
            .Select(x => new { x.PublicId, x.Prefix, x.Number })
            .FirstOrDefaultAsync(ct);
        if (original.Status == DocumentStatus.Voided || anulacionPrevia is not null)
        {
            return Result.Failure<AnulacionSinEfectoFiscal>(InventoryErrors.AlreadyVoided(anulacionPrevia?.PublicId ?? Guid.Empty,
                anulacionPrevia is null ? null : VistaDeDocumentos.NumeroVisible(anulacionPrevia.Prefix, anulacionPrevia.Number)));
        }
        if (original.Status != DocumentStatus.Confirmed) return Result.Failure<AnulacionSinEfectoFiscal>(InventoryErrors.NotConfirmed(original.Status));

        var tipo = await db.InventoryDocumentTypes.Where(t => t.Class == DocumentClass.Voiding && t.IsActive).OrderBy(t => t.Code).FirstOrDefaultAsync(ct);
        if (tipo is null) return Result.Failure<AnulacionSinEfectoFiscal>(InventoryErrors.DocumentClassNotAvailable(DocumentClass.Voiding));

        // Sin efecto fiscal: el número fiscal del rechazado queda libre frente al índice único filtrado (T16). Es la única
        // modificación de un documento confirmado que admite IInmutableTrasConfirmar para este caso.
        original.FiscalNumberReleased = true;

        var anulacion = VoidInventoryDocumentCommandHandler.ContrarioDe(original, tipo, reloj.HoyLocal, usuario, motivo);
        db.InventoryDocuments.Add(anulacion);
        db.DocumentLinks.Add(new DocumentLink { SourceDocument = original, TargetDocument = anulacion, Kind = DocumentLinkKind.Voids });
        await db.SaveChangesAsync(ct);

        // T724/T725: la anulación de la venta admite la clase fiscal, pone fiscalCase y ajusta el crédito con VoidingByDianRejection.
        rechazoEnCurso?.AnularSinEfectoFiscal(anulacion.PublicId, caso);

        var confirmada = await confirmacion.ConfirmarAsync(new PedidoDeConfirmacion(anulacion.PublicId, GrupoEsperado: null), ct);
        if (confirmada.IsFailure) return Result.Failure<AnulacionSinEfectoFiscal>(confirmada.Error);
        return Result.Success(new AnulacionSinEfectoFiscal(anulacion.PublicId, confirmada.Value.DisplayNumber));
    }

    // ------------------------------------------------------------------------------------------ caso b: confirmar --

    public async Task<Result<ReemplazoConfirmado>> ConfirmarReemplazoAsync(Guid rejectedSourceDocumentPublicId, Guid replacementDocumentPublicId,
        Action<InventoryDocument> asignarNumeroFiscal, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(asignarNumeroFiscal);
        var par = await db.DocumentLinks.AsNoTracking()
            .Where(l => l.Kind == DocumentLinkKind.ReplacementOf && !l.IsDeleted)
            .Join(db.InventoryDocuments.AsNoTracking(), l => l.SourceDocumentId, o => o.Id, (l, o) => new { l, Original = o })
            .Join(db.InventoryDocuments.AsNoTracking(), x => x.l.TargetDocumentId, r => r.Id, (x, r) => new { x.Original, Reemplazo = r })
            .Where(x => x.Original.PublicId == rejectedSourceDocumentPublicId && x.Reemplazo.PublicId == replacementDocumentPublicId)
            .Select(x => new { OriginalClass = x.Original.Class, ReemplazoClass = x.Reemplazo.Class, x.Reemplazo.Status, x.Reemplazo.IsDeleted })
            .FirstOrDefaultAsync(ct);
        if (par is null || par.IsDeleted || par.OriginalClass != par.ReemplazoClass
            || par.Status is not (DocumentStatus.Draft or DocumentStatus.PendingApproval))
            return Result.Failure<ReemplazoConfirmado>(ErroresDeFacturacionElectronica.NotReplacementDraft());

        // El mismo número del rechazado, por la vía exclusiva del caso b (la numeración de la confirmación no vuelve a numerar un documento
        // que ya lo tiene); la venta de reemplazo ajusta el crédito del rechazado con Replacement en vez de registrar uno nuevo.
        var reemplazo = await db.InventoryDocuments.FirstAsync(d => d.PublicId == replacementDocumentPublicId, ct);
        asignarNumeroFiscal(reemplazo);
        rechazoEnCurso?.Reemplazar(replacementDocumentPublicId, rejectedSourceDocumentPublicId);

        var confirmada = await confirmacion.ConfirmarAsync(new PedidoDeConfirmacion(replacementDocumentPublicId, GrupoEsperado: null), ct);
        if (confirmada.IsFailure) return Result.Failure<ReemplazoConfirmado>(confirmada.Error);
        return Result.Success(new ReemplazoConfirmado(replacementDocumentPublicId, confirmada.Value.DisplayNumber));
    }

    // ------------------------------------------------------------------------------------------ caso a: la contraparte --

    public async Task<Result<FotoFiscalDeEntrada?>> ContraparteDelMaestroAsync(Guid sourceDocumentPublicId, CancellationToken ct)
    {
        var d = await db.InventoryDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == sourceDocumentPublicId && !x.IsDeleted, ct);
        if (d is null) return Result.Failure<FotoFiscalDeEntrada?>(InventoryErrors.DocumentNotFound());
        if (d.CounterpartyPersonId is not int personaId) return Result.Success<FotoFiscalDeEntrada?>(null);

        var persona = await db.People.AsNoTracking().FirstAsync(p => p.Id == personaId, ct);
        var vigente = await db.DocumentPartySnapshots.AsNoTracking().Where(f => f.DocumentId == d.Id && !f.IsDeleted)
            .Select(f => (int?)f.Version).MaxAsync(ct) ?? 0;
        var foto = Foto(FotoDeLaContraparte.De(d, persona)) with { Version = vigente + 1 };
        return Result.Success<FotoFiscalDeEntrada?>(foto);
    }

    public async Task<Result> RegistrarVersionDeContraparteAsync(Guid sourceDocumentPublicId, FotoFiscalDeEntrada foto, string motivo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(foto);
        if (string.IsNullOrWhiteSpace(motivo)) throw new ArgumentException("La versión nueva de la copia fiscal exige el motivo.", nameof(motivo));

        var d = await db.InventoryDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == sourceDocumentPublicId && !x.IsDeleted, ct);
        if (d is null || d.CounterpartyPersonId is not int personaId) return Result.Failure(InventoryErrors.DocumentNotFound());
        var vigente = await db.DocumentPartySnapshots.AsNoTracking().Where(f => f.DocumentId == d.Id && !f.IsDeleted)
            .Select(f => (int?)f.Version).MaxAsync(ct) ?? 0;
        if (foto.Version != vigente + 1)
            throw new InvalidOperationException(
                $"La copia fiscal del documento {d.PublicId} va en la versión {vigente}: la nueva es la {vigente + 1}, no la {foto.Version}.");

        // Lo que la copia guarda y la entrada no lleva (nombres, ciudad, marcas de renta) sale del maestro, como en la confirmación.
        var persona = await db.People.AsNoTracking().FirstAsync(p => p.Id == personaId, ct);
        var delMaestro = FotoDeLaContraparte.De(d, persona);
        var razon = motivo.Trim();
        db.DocumentPartySnapshots.Add(new DocumentPartySnapshot
        {
            DocumentId = d.Id,
            Version = foto.Version,
            PersonId = personaId,
            DianOrganizationType = foto.OrganizationType,
            DianIdTypeCode = foto.IdTypeCode,
            TaxId = foto.TaxId,
            CheckDigit = foto.CheckDigit,
            LegalName = foto.LegalName,
            FirstName = delMaestro.FirstName,
            LastName = delMaestro.LastName,
            Address = foto.Address,
            MunicipalityDaneCode = foto.MunicipalityDaneCode,
            CityName = delMaestro.CityName,
            DepartmentName = delMaestro.DepartmentName,
            CountryCode = foto.CountryCode,
            Email = foto.Email,
            Phone = foto.Phone,
            DianResponsibilities = foto.Responsibilities,
            DianTaxSchemeCode = foto.TaxSchemeCode,
            IsVatResponsible = foto.IsVatResponsible,
            IsLargeContributor = foto.IsLargeContributor,
            IsSelfWithholder = foto.IsSelfWithholder,
            IsVatWithholdingAgent = foto.IsVatWithholdingAgent,
            IsSimpleTaxRegime = foto.IsSimpleTaxRegime,
            IsIncomeTaxFiler = delMaestro.IsIncomeTaxFiler,
            WithholdingExempt = delMaestro.WithholdingExempt,
            IcaWithholdingExempt = delMaestro.IcaWithholdingExempt,
            CiiuCode = foto.CiiuCode,
            ChangeReason = razon.Length <= 300 ? razon : razon[..300],
        });
        return Result.Success();
    }

    // ------------------------------------------------------------------------------------------ casos b y c: permiso --

    public string PermisoDeConfirmar(ElectronicDocumentKind tipo) =>
        (tipo is ElectronicDocumentKind.SupportDocument or ElectronicDocumentKind.SupportDocumentAdjustmentNote
            or ElectronicDocumentKind.RadianEvent030 or ElectronicDocumentKind.RadianEvent032
            ? PermisosDeGrupo.De(DocumentClassGroup.Purchases)
            : PermisosDeGrupo.De(DocumentClassGroup.Sales)).Confirm!;

    // ------------------------------------------------------------------------------ eventos RADIAN (I5, T803–T805) --

    public Task<Result<IReadOnlyList<EventoRadianPreparado>>> PrepararEventosRadianAsync(Guid sourceDocumentPublicId,
        IReadOnlyList<ElectronicDocumentKind> tipos, CancellationToken ct) =>
        eventosRadian?.PrepararAsync(sourceDocumentPublicId, tipos, ct)
        ?? Task.FromResult(Result.Failure<IReadOnlyList<EventoRadianPreparado>>(SinEventos()));

    public Task<Result<EntradaDeEventoRadian>> LeerEventoRadianAsync(Guid sourceDocumentPublicId, ElectronicDocumentKind tipo, CancellationToken ct) =>
        eventosRadian?.LeerAsync(sourceDocumentPublicId, tipo, ct) ?? Task.FromResult(Result.Failure<EntradaDeEventoRadian>(SinEventos()));

    public Task<Result> EnlazarEventoRadianAsync(Guid sourceDocumentPublicId, ElectronicDocumentKind tipo, Guid electronicDocumentPublicId, CancellationToken ct) =>
        eventosRadian?.EnlazarAsync(sourceDocumentPublicId, tipo, electronicDocumentPublicId, ct) ?? Task.FromResult(Result.Failure(SinEventos()));

    public Task<Result> RegistrarResultadoDeEventoRadianAsync(Guid sourceDocumentPublicId, ElectronicDocumentKind tipo, ResultadoDeEventoRadian resultado,
        CancellationToken ct) =>
        eventosRadian?.RegistrarResultadoAsync(sourceDocumentPublicId, tipo, resultado, ct) ?? Task.FromResult(Result.Failure(SinEventos()));

    private static Error SinEventos() => new(RadianNotAvailableCode, "Los eventos RADIAN de compras no están disponibles en esta instalación.");
}

