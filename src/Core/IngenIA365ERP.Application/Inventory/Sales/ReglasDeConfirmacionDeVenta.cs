using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.ElectronicInvoicing;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Documents.Efectos;
using IngenIA365ERP.Application.Inventory.Pos;
using IngenIA365ERP.Application.Inventory.Pricing;
using IngenIA365ERP.Application.Inventory.Purchasing.Common;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;
using IngenIA365ERP.Domain.Inventory.Parameters;
using IngenIA365ERP.Domain.Sales.Payments;
using IngenIA365ERP.Domain.Taxes;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Sales;

/// <summary>
/// Las reglas de venta dentro de la confirmación (feature 012, I3, T611, T612; contracts/api.md §18.2, §18.3, §22.4; FR-056, FR-057,
/// FR-066, T26, T50). Las llaman las estrategias de las clases de venta (<c>SalidaPorVenta</c>, <c>DevolucionDeCliente</c>) desde el flujo
/// canónico —el mismo de <c>ConfirmInventoryDocumentCommand(DocumentPublicId, ExpectedGroup = Sales)</c> y del cobro del POS—, así que
/// rigen igual en oficina y en caja. <c>expectedAmountDue</c> (<c>Inventory.Document.TotalChanged</c>) lo compara el comando antes de
/// empezar (<see cref="TotalCambioAsync"/>). Antes de la aprobación (<see cref="ValidarVentaAsync"/>, <see cref="ValidarNotaAsync"/>):
/// <list type="bullet">
/// <item>el veredicto de <see cref="GuardiaDeEmisionFiscal"/>: bloqueado → <c>ElectronicInvoicing.NotReady</c>; una clase que el veredicto
/// no admite → <c>Inventory.Sales.FiscalClassMismatch</c> con <c>verdict</c> y <c>allowedClasses</c>;</item>
/// <item>los descuentos sobre el tope aprobados (<c>Inventory.Discount.ApprovalPending</c>); el vendedor, persona con fila viva en
/// <c>INV_Salespeople</c> (<c>Inventory.Sales.SalespersonInvalid</c>, FR-057); la persona inactiva de contado según
/// <c>Ventas.PersonaInactivaDeContado</c> (<c>Inventory.Sales.PersonInactive</c>);</item>
/// <item>impuestos y retenciones por <see cref="CalculoTributarioDeVenta"/> y <c>AmountDue = Total − WithholdingTotal</c> (T26);</item>
/// <item>los pagos por <see cref="ValidadorDePagos"/> contra <c>AmountDue</c> y la disponibilidad de cada medio; en una nota, los
/// reintegros por el mismo medio o con <c>Inventory.Sales.RefundOtherMeans</c> (<c>Payments.RefundMeansNotAllowed</c>).</item>
/// </list>
/// Dentro del cerrojo (<see cref="AlConfirmarVentaAsync"/>, <see cref="AlConfirmarNotaAsync"/>): la foto de impuestos
/// (<c>INV_DocumentTaxLines</c>), los bonos de número único (<c>INV_VoucherRedemptions</c>) o su liberación en la nota, el toque de
/// <c>LastActivityAt</c> de cada sesión de los pagos que se arquean (también en oficina, T50, F14) y el vencimiento de una venta a
/// crédito. La copia fiscal de la contraparte (<c>INV_DocumentPartySnapshots</c> versión 1) la escribe el flujo canónico. La alerta
/// <c>Inventario.VentaBajoCosto</c> sale después del guardado (<see cref="AlertaDeVentaBajoCosto"/>). Scoped: guarda por documento lo
/// calculado antes del cerrojo. (nuevo)
/// </summary>
public sealed class ReglasDeConfirmacionDeVenta(
    IApplicationDbContext db,
    IActorActual actorActual,
    IDateTimeService reloj,
    ILectorDeParametros parametros,
    IPermissionChecker permisos,
    GuardiaDeEmisionFiscal guardia,
    CalculoTributarioDeVenta calculo,
    AprobacionDeDescuentos aprobaciones,
    IToqueDeSesionDeCaja toque)
{
    public const string PermisoOtroMedio = "Inventory.Sales.RefundOtherMeans";

    private readonly Dictionary<Guid, IReadOnlyList<DocumentTaxLine>> _foto = [];
    private readonly HashSet<Guid> _escritas = [];

    // --------------------------------------------------------------------------------------- total cambiado --

    /// <summary>
    /// <c>Inventory.Document.TotalChanged</c> si <paramref name="esperado"/> no es lo que el documento dice hoy (<c>data.amountDue</c>).
    /// Nulo si coincide, si no se envió o si el documento no existe (lo responde después el flujo canónico).
    /// </summary>
    public static async Task<Error?> TotalCambioAsync(IApplicationDbContext db, Guid documentoPublicId, decimal? esperado, CancellationToken ct)
    {
        if (esperado is not { } monto) return null;
        var actual = await db.InventoryDocuments.AsNoTracking().Where(d => d.PublicId == documentoPublicId).Select(d => (decimal?)d.AmountDue).FirstOrDefaultAsync(ct);
        return actual is { } a && a != monto ? ErroresDelPos.TotalChanged(monto, a) : null;
    }

    // ---------------------------------------------------------------------------------------------- venta --

    /// <summary>Las reglas de una venta (factura, documento equivalente, comprobante no electrónico) antes de la aprobación.</summary>
    public async Task<Result> ValidarVentaAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var documento = contexto.Documento;
        var fiscal = await FiscalAsync(contexto, ct);
        if (fiscal is not null) return Result.Failure(fiscal);

        var aprobados = await aprobaciones.ExigirAprobadosAsync(documento, ct);
        if (aprobados.IsFailure) return aprobados;

        if (documento.SalespersonId is int vendedor)
        {
            var vivo = await db.Salespeople.IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(s => s.Id == vendedor && !s.IsDeleted && !s.Person.IsDeleted, ct);
            if (!vivo) return Result.Failure(ErroresDelPos.SalespersonInvalid());
        }

        // Impuestos y totales: lo que se guardará al confirmar (T26).
        var calculado = await calculo.CalcularAsync(documento, ct);
        if (calculado.IsFailure) return Result.Failure(calculado.Error);
        CalculoTributarioDeVenta.AplicarTotales(documento, calculado.Value.Totales);
        _foto[documento.PublicId] = CalculoTributarioDeVenta.Foto(documento, calculado.Value.Renglones);

        var pagos = await PagosAsync(documento, PaymentDirection.Received, ct);
        if (await PersonaInactivaDeContadoAsync(documento, pagos, ct) is { } inactiva) return Result.Failure(inactiva);

        var validados = await ValidarPagosAsync(documento, pagos, ct);
        return validados;
    }

    /// <summary>Dentro del cerrojo: la foto de impuestos, los bonos, el toque de las sesiones y el vencimiento.</summary>
    public async Task<Result> AlConfirmarVentaAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var documento = contexto.Documento;
        EscribirImpuestos(documento);

        var pagos = await PagosAsync(documento, PaymentDirection.Received, ct);
        var medios = await MediosAsync(pagos, ct);
        foreach (var pago in pagos.Where(p => medios.TryGetValue(p.PaymentMeansId, out var m) && m.UniqueReference && p.NormalizedReference is not null))
        {
            var yaEsta = db.VoucherRedemptions.Local.Any(v => ReferenceEquals(v.DocumentPayment, pago) || (pago.Id != 0 && v.DocumentPaymentId == pago.Id))
                || (pago.Id != 0 && await db.VoucherRedemptions.AsNoTracking().AnyAsync(v => v.DocumentPaymentId == pago.Id && v.Status == VoucherRedemptionStatus.Active, ct));
            if (yaEsta) continue;
            if (await ColisionesDeVenta.BonoUsadoAsync(db, pago.PaymentMeansId, pago.NormalizedReference!, ct) is { } usado) return Result.Failure(usado);
            db.VoucherRedemptions.Add(new VoucherRedemption
            {
                PaymentMeansId = pago.PaymentMeansId,
                NormalizedNumber = pago.NormalizedReference!,
                DocumentPayment = pago,
                DocumentId = documento.Id,
                RedeemedAt = reloj.UtcNow,
            });
        }

        var tocadas = await TocarSesionesAsync(pagos, ct);
        if (tocadas.IsFailure) return tocadas;

        var credito = pagos.Where(p => ClasesDeMedio.EsCredito(p.MeansClass) && p.FinalDueDate is not null).Select(p => p.FinalDueDate!.Value).ToList();
        if (credito.Count > 0 && documento.DueDate is null) documento.DueDate = credito.Max();
        return Result.Success();
    }

    // ----------------------------------------------------------------------------------------------- nota --

    /// <summary>
    /// Las reglas de una nota de venta antes de la aprobación: el veredicto fiscal, el original confirmado y de la clase que corresponde,
    /// lo que queda por acreditar, los impuestos con la foto del original en proporción y los reintegros.
    /// </summary>
    public async Task<Result> ValidarNotaAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var nota = contexto.Documento;
        var fiscal = await FiscalAsync(contexto, ct);
        if (fiscal is not null) return Result.Failure(fiscal);

        var original = await NotasDeVenta.OriginalDeAsync(db, nota, ct);
        if (original is null || original.Status != DocumentStatus.Confirmed) return Result.Failure(ErroresDeVentas.CreditNoteOriginInvalid());
        if (NotasDeVenta.ClaseDeNota(original.Class) is not { } esperada) return Result.Failure(ErroresDeVentas.CreditNoteOriginInvalid());
        if (esperada != nota.Class) return Result.Failure(ErroresDeVentas.CreditNoteClassMismatch(original.Class, esperada));

        var excedido = await NotasDeVenta.ExcesoAsync(db, original, nota, ct);
        if (excedido is not null) return Result.Failure(excedido);

        var calculado = await NotasDeVenta.ImpuestosAsync(db, calculo, original, nota, ct);
        if (calculado.IsFailure) return Result.Failure(calculado.Error);
        CalculoTributarioDeVenta.AplicarTotales(nota, calculado.Value.Totales);
        _foto[nota.PublicId] = CalculoTributarioDeVenta.Foto(nota, calculado.Value.Renglones);

        var reintegros = await PagosAsync(nota, PaymentDirection.Refunded, ct);
        return await ValidarReintegrosAsync(nota, original, reintegros, ct);
    }

    /// <summary>Dentro del cerrojo: la foto de impuestos, la liberación de los bonos reintegrados y el toque de las sesiones.</summary>
    public async Task<Result> AlConfirmarNotaAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var nota = contexto.Documento;
        EscribirImpuestos(nota);
        var reintegros = await PagosAsync(nota, PaymentDirection.Refunded, ct);
        var reintegrados = reintegros.Select(r => r.RefundsPaymentId).OfType<int>().Distinct().ToList();
        if (reintegrados.Count > 0)
        {
            foreach (var bono in await db.VoucherRedemptions.Where(v => reintegrados.Contains(v.DocumentPaymentId) && v.Status == VoucherRedemptionStatus.Active).ToListAsync(ct))
                bono.Liberar(nota.Id, reloj.UtcNow, "Reintegrado por la nota.");
        }
        return await TocarSesionesAsync(reintegros, ct);
    }

    // ------------------------------------------------------------------------------------------------ apoyo --

    /// <summary>El veredicto fiscal sobre la clase del documento; nulo si confirma.</summary>
    private async Task<Error?> FiscalAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var caja = contexto.Documento.CashRegisterId is int c ? await db.CashRegisters.AsNoTracking().FirstOrDefaultAsync(r => r.Id == c, ct) : null;
        var evaluacion = await guardia.EvaluarAsync(contexto.Documento.OperationDate, contexto.Tipo, caja, ct);
        if (evaluacion.Admite(contexto.Documento.Class)) return null;
        return evaluacion.Veredicto == VeredictoFiscal.Blocked
            ? ErroresDeVentas.NotReady(evaluacion)
            : ErroresDeVentas.FiscalClassMismatch(contexto.Documento.Class, evaluacion);
    }

    /// <summary>Escribe la foto de impuestos calculada antes del cerrojo (una sola vez por documento).</summary>
    private void EscribirImpuestos(InventoryDocument documento)
    {
        if (!_foto.TryGetValue(documento.PublicId, out var filas) || !_escritas.Add(documento.PublicId)) return;
        foreach (var fila in filas) db.DocumentTaxLines.Add(fila);
    }

    /// <summary>
    /// La foto de impuestos del documento para sus mensajes: la calculada en esta confirmación (antes del cerrojo, la provisional) o la
    /// guardada.
    /// </summary>
    public async Task<IReadOnlyList<DocumentTaxLine>> ImpuestosDeAsync(InventoryDocument documento, CancellationToken ct)
    {
        if (_foto.TryGetValue(documento.PublicId, out var filas)) return filas;
        return await db.DocumentTaxLines.AsNoTracking().Where(t => t.DocumentId == documento.Id).ToListAsync(ct);
    }

    /// <summary>Los pagos (o reintegros) vivos del documento para sus mensajes.</summary>
    public Task<List<DocumentPayment>> PagosDeAsync(InventoryDocument documento, PaymentDirection sentido, CancellationToken ct) =>
        PagosAsync(documento, sentido, ct);

    /// <summary>Los pagos vivos del documento en un sentido, también los agregados en esta unidad de trabajo (el cobro del POS).</summary>
    private async Task<List<DocumentPayment>> PagosAsync(InventoryDocument documento, PaymentDirection sentido, CancellationToken ct)
    {
        if (documento.Id != 0) await db.DocumentPayments.Where(p => p.DocumentId == documento.Id).LoadAsync(ct);
        return db.DocumentPayments.Local
            .Where(p => (ReferenceEquals(p.Document, documento) || (documento.Id != 0 && p.DocumentId == documento.Id)) && !p.IsDeleted && p.Direction == sentido)
            .OrderBy(p => p.LineNumber).ToList();
    }

    private async Task<Dictionary<int, PaymentMeans>> MediosAsync(IEnumerable<DocumentPayment> pagos, CancellationToken ct)
    {
        var ids = pagos.Select(p => p.PaymentMeansId).Distinct().ToList();
        return await db.PaymentMeans.AsNoTracking().Where(m => ids.Contains(m.Id)).ToDictionaryAsync(m => m.Id, ct);
    }

    /// <summary>El pago guardado en la forma del validador puro, con el medio vigente.</summary>
    public static PagoPropuesto Propuesto(DocumentPayment pago, PaymentMeans m) => new(
        new MedioDePagoCopia(m.Id, m.Code, m.Class, m.AllowsChange, m.AllowsPartial, m.RequiresReference, m.ReferenceKind, m.ReferenceMinLength,
            m.ReferenceMaxLength, m.UniqueReference, m.CountMethod),
        pago.Amount, pago.AmountTendered, pago.Reference, pago.AuthorizationCode, pago.Last4, pago.TerminalBatchNumber, pago.CashSessionId);

    /// <summary>Los pagos de la venta contra <c>AmountDue</c> (FR-056), con las sesiones abiertas del usuario, y su disponibilidad.</summary>
    private async Task<Result> ValidarPagosAsync(InventoryDocument documento, IReadOnlyList<DocumentPayment> pagos, CancellationToken ct)
    {
        var medios = await MediosAsync(pagos, ct);
        var abiertas = await SesionesAbiertasAsync(ct);
        var validado = ValidadorDePagos.Validar(new PedidoDeCobro(documento.AmountDue, pagos.Select(p => Propuesto(p, medios[p.PaymentMeansId])).ToList(), abiertas));
        if (!validado.IsValid) return Result.Failure(RegistroDePagos.ErrorDePago(validado.Errors[0]));
        var noDisponible = await MedioNoDisponibleAsync(db, permisos, documento, medios.Values.ToList(), await BorradorDeVenta.EsConsumidorFinalAsync(db, documento, ct), ct);
        return noDisponible is null ? Result.Success() : Result.Failure(noDisponible);
    }

    /// <summary>
    /// Los reintegros de una nota (§18.3): suman el <c>AmountDue</c> de la nota (<c>Payments.TotalMismatch</c>); cada uno va por un medio de
    /// la venta, salvo con <c>Inventory.Sales.RefundOtherMeans</c> (<c>Payments.RefundMeansNotAllowed</c>); todo medio que se arquea sale de
    /// una sesión abierta del usuario (<c>Payments.CashSessionRequired</c>), aunque la venta haya sido en otra caja (T50).
    /// </summary>
    private async Task<Result> ValidarReintegrosAsync(InventoryDocument nota, InventoryDocument original, IReadOnlyList<DocumentPayment> reintegros, CancellationToken ct)
    {
        var pagado = reintegros.Sum(r => r.Amount);
        if (pagado != nota.AmountDue || reintegros.Any(r => r.Amount <= 0m))
        {
            var falta = Math.Max(0m, nota.AmountDue - pagado);
            var sobra = Math.Max(0m, pagado - nota.AmountDue);
            return Result.Failure(new ErrorConDatos(ValidadorDePagos.TotalMismatch, "Los reintegros no suman exactamente el valor de la nota.",
                new { amountDue = nota.AmountDue, paid = pagado, missing = falta, excess = sobra }));
        }
        var mediosDeLaVenta = await db.DocumentPayments.AsNoTracking()
            .Where(p => p.DocumentId == original.Id && !p.IsDeleted && p.Direction == PaymentDirection.Received)
            .Select(p => p.PaymentMeansId).Distinct().ToListAsync(ct);
        var puedeOtro = await permisos.HasPermissionAsync(PermisoOtroMedio, ct);
        foreach (var r in reintegros.Where(r => !mediosDeLaVenta.Contains(r.PaymentMeansId)))
            if (!puedeOtro) return Result.Failure(ErroresDeVentas.RefundMeansNotAllowed(r.MeansCode));

        var medios = await MediosAsync(reintegros, ct);
        var abiertas = await SesionesAbiertasAsync(ct);
        for (var i = 0; i < reintegros.Count; i++)
        {
            var r = reintegros[i];
            if (medios[r.PaymentMeansId].CountMethod != CashCountMethod.None && (r.CashSessionId is not int s || !abiertas.Contains(s)))
                return Result.Failure(RegistroDePagos.ErrorDePago(new ErrorDePago(ValidadorDePagos.CashSessionRequired, i,
                    new Dictionary<string, object?> { ["paymentMeansCode"] = r.MeansCode })));
        }
        return Result.Success();
    }

    private async Task<IReadOnlyCollection<int>> SesionesAbiertasAsync(CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not int usuario) return [];
        return await db.CashSessions.AsNoTracking().Where(s => s.CashierUserId == usuario && s.Status == CashSessionStatus.Open).Select(s => s.Id).ToListAsync(ct);
    }

    /// <summary>Toca <c>LastActivityAt</c> de cada sesión de los pagos (T50, F14); una que ya no está abierta → <c>Inventory.CashSession.NotOpen</c>.</summary>
    private async Task<Result> TocarSesionesAsync(IEnumerable<DocumentPayment> pagos, CancellationToken ct)
    {
        foreach (var sesion in pagos.Select(p => p.CashSessionId).OfType<int>().Distinct())
            if (!await toque.TocarAsync(sesion, reloj.UtcNow, ct)) return Result.Failure(ErroresDelPos.CashSessionNotOpen());
        return Result.Success();
    }

    private async Task<Error?> PersonaInactivaDeContadoAsync(InventoryDocument documento, IReadOnlyList<DocumentPayment> pagos, CancellationToken ct)
    {
        if (pagos.Any(p => ClasesDeMedio.EsCredito(p.MeansClass))) return null;
        return await BorradorDeVenta.PersonaInactivaBloqueadaAsync(db, parametros, documento, ct);
    }

    /// <summary>
    /// <c>Payments.MeansNotAvailable</c> del primer medio que no se ofrece en esta venta (punto, canal, tipo, cliente y permiso de crédito;
    /// <see cref="DisponibilidadDeMedio"/>, §22.3); nulo si todos se ofrecen.
    /// </summary>
    public static async Task<Error?> MedioNoDisponibleAsync(IApplicationDbContext db, IPermissionChecker permisos, InventoryDocument documento,
        IReadOnlyList<PaymentMeans> medios, bool esConsumidorFinal, CancellationToken ct)
    {
        if (medios.Count == 0) return null;
        var ids = medios.Select(m => m.Id).ToList();
        var puntos = (await db.PaymentMeansPointsOfSale.AsNoTracking().Where(x => ids.Contains(x.PaymentMeansId)).Select(x => new { x.PaymentMeansId, x.PointOfSaleId }).ToListAsync(ct))
            .ToLookup(x => x.PaymentMeansId, x => x.PointOfSaleId);
        var canales = (await db.PaymentMeansChannels.AsNoTracking().Where(x => ids.Contains(x.PaymentMeansId)).Select(x => new { x.PaymentMeansId, x.SalesChannelId }).ToListAsync(ct))
            .ToLookup(x => x.PaymentMeansId, x => x.SalesChannelId);
        var tipos = (await db.PaymentMeansDocumentTypes.AsNoTracking().Where(x => ids.Contains(x.PaymentMeansId)).Select(x => new { x.PaymentMeansId, x.DocumentTypeId }).ToListAsync(ct))
            .ToLookup(x => x.PaymentMeansId, x => x.DocumentTypeId);
        int? canal = documento.SalesChannelId;
        if (canal is null && documento.PointOfSaleId is int punto)
            canal = await db.PointsOfSale.AsNoTracking().Where(p => p.Id == punto).Select(p => p.SalesChannelId).FirstOrDefaultAsync(ct);
        var caso = new CasoDeCobro(documento.OperationDate, documento.PointOfSaleId, canal, documento.DocumentTypeId, esConsumidorFinal,
            await permisos.HasPermissionAsync(RegistroDePagos.PermisoCredito, ct));
        foreach (var m in medios)
        {
            var ofrecible = new MedioOfrecible(m.Id, m.Code, m.Name, m.Class, m.DisplayOrder, m.QuickKey, m.IsActive, m.ValidFrom, m.ValidTo,
                m.OfferedAtAllPointsOfSale, puntos[m.Id].ToList(), m.OfferedInAllChannels, canales[m.Id].ToList(), m.OfferedForAllDocumentTypes, tipos[m.Id].ToList());
            if (!DisponibilidadDeMedio.SeOfrece(ofrecible, caso))
                return RegistroDePagos.ErrorDePago(new ErrorDePago(DisponibilidadDeMedio.MeansNotAvailable, null,
                    new Dictionary<string, object?> { ["paymentMeansCode"] = m.Code }));
        }
        return null;
    }
}

/// <summary>
/// El aviso de venta bajo costo después del guardado de la confirmación (feature 012, I3, T611; §18.2): con <c>Ventas.BajoCosto =
/// Alertar</c>, una venta con alguna línea cuyo neto por unidad base quedó bajo el costo con que salió levanta
/// <c>Inventario.VentaBajoCosto</c> por <see cref="IAlertas"/> y lo devuelve en <c>warnings[]</c>. Nunca bloquea (con «Bloquear» ya lo
/// rechazó la precificación). (nuevo)
/// </summary>
public sealed class AlertaDeVentaBajoCosto(IApplicationDbContext db, ILectorDeParametros parametros, IAlertas alertas) : IAvisoAlConfirmar
{
    public async Task<IReadOnlyList<AvisoDto>> AvisarAsync(InventoryDocument documento, CancellationToken ct)
    {
        if (!VentasConSalida.Contains(documento.Class) || documento.Status != DocumentStatus.Confirmed) return [];
        var bajas = documento.Lines.Where(l => !l.IsDeleted && l.UnitCost is > 0m && l.QuantityBase > 0m && l.NetAmount / l.QuantityBase < l.UnitCost!.Value)
            .OrderBy(l => l.LineNumber).ToList();
        if (bajas.Count == 0) return [];
        var leido = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.VentasBajoCosto, documento.OperationDate, ct: ct);
        var politica = leido.IsSuccess && !string.IsNullOrWhiteSpace(leido.Value.Texto) ? leido.Value.Texto : "Alertar";
        if (!string.Equals(politica, "Alertar", StringComparison.OrdinalIgnoreCase)) return [];

        var numero = VistaDeDocumentos.NumeroVisible(documento.Prefix, documento.Number) ?? documento.PublicId.ToString();
        var lineas = string.Join(", ", bajas.Select(l => l.LineNumber));
        var bodega = documento.WarehouseId is int b ? await db.Warehouses.AsNoTracking().Where(w => w.Id == b).Select(w => (Guid?)w.PublicId).FirstOrDefaultAsync(ct) : null;
        var punto = documento.PointOfSaleId is int p ? await db.PointsOfSale.AsNoTracking().Where(x => x.Id == p).Select(x => (Guid?)x.PublicId).FirstOrDefaultAsync(ct) : null;
        await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.VentaBajoCosto,
            $"Venta bajo costo: {numero}",
            $"La venta {numero} vendió por debajo del costo con que salió la mercancía (líneas {lineas}).",
            "InventoryDocument", documento.PublicId, bodega, punto, $"{TiposDeAlerta.VentaBajoCosto}:{documento.PublicId:N}"), ct);
        return bajas.Select(l => new AvisoDto(ErroresDePrecios.BelowCostCode, $"La línea {l.LineNumber} se vendió por debajo de su costo.", new { lineNumber = l.LineNumber }))
            .ToList();
    }

    private static readonly HashSet<DocumentClass> VentasConSalida =
        [DocumentClass.SalesInvoice, DocumentClass.PosEquivalentDocument, DocumentClass.NonElectronicSalesReceipt];
}
