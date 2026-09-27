using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Sales.Payments;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Sales;

/// <summary>
/// El pago dentro de un documento (contracts/api.md §22.4): lo comparten la factura, la nota y el cobro del POS. <c>last4</c> son los
/// cuatro últimos dígitos; ningún campo lleva el número completo de la tarjeta (<c>LosPagosNoGuardanElNumeroDeTarjeta</c>). (nuevo)
/// </summary>
public sealed record DocumentPaymentInput(
    Guid PaymentMeansPublicId,
    decimal Amount,
    decimal? Tendered = null,
    string? Reference = null,
    string? AuthorizationCode = null,
    Guid? CardTerminalPublicId = null,
    string? BatchNumber = null,
    string? Last4 = null,
    Guid? CashSessionPublicId = null,
    PaymentCreditInput? Credit = null);

/// <summary>Las condiciones de un pago de clase crédito. (nuevo)</summary>
public sealed record PaymentCreditInput(short Installments, short TermDays, short PeriodicityDays, DateOnly? FirstDueDate = null, string? SuggestedLineCode = null);

/// <summary>Dónde se cobra: el punto, el canal, la caja (su datáfono propuesto) y la sesión que pone el servidor en el POS. (nuevo)</summary>
public sealed record LugarDeCobro(int? PointOfSaleId, int? SalesChannelId, int? CashSessionId, int? DefaultCardTerminalId, bool EsConsumidorFinal);

/// <summary>Los pagos registrados y las vueltas. (nuevo)</summary>
public sealed record PagosRegistrados(IReadOnlyList<DocumentPayment> Pagos, decimal Change);

/// <summary>
/// El registro de los pagos de un documento de venta (feature 012, I3, T606; §22.4; FR-096 a FR-101): resuelve cada medio, comprueba
/// que se ofrezca en ese punto, canal, tipo y cliente (<see cref="DisponibilidadDeMedio"/>, también el permiso de crédito), valida con
/// <see cref="ValidadorDePagos"/> (suma exacta, vueltas, referencia, tarjeta, parcial, sesión) y escribe los
/// <c>INV_DocumentPayments</c> con las copias del medio y la comisión esperada, más los <c>INV_VoucherRedemptions</c> de los medios de
/// referencia única en la misma transacción (un bono ya usado → <c>Payments.VoucherAlreadyUsed</c> con la venta que lo usó). Los pagos
/// que el documento tenía (un cobro que volvió a borrador) se dan de baja y sus bonos se liberan. Nunca guarda. (nuevo)
/// </summary>
public sealed class RegistroDePagos(IApplicationDbContext db, IPermissionChecker permisos, IDateTimeService reloj)
{
    public const string PermisoCredito = "Inventory.Sales.SellOnCredit";

    public async Task<Result<PagosRegistrados>> RegistrarAsync(InventoryDocument venta, IReadOnlyList<DocumentPaymentInput> entradas, LugarDeCobro lugar,
        int usuarioId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(venta);
        var ids = entradas.Select(e => e.PaymentMeansPublicId).Distinct().ToList();
        var medios = await db.PaymentMeans.AsNoTracking().Include(m => m.CardNetwork).Include(m => m.CardAcquirer)
            .Where(m => ids.Contains(m.PublicId)).ToDictionaryAsync(m => m.PublicId, ct);

        // Disponibilidad (§22.3): un medio que no se ofrece aquí, o un crédito sin cliente o sin permiso.
        var credito = await permisos.HasPermissionAsync(PermisoCredito, ct);
        var caso = new CasoDeCobro(venta.OperationDate, lugar.PointOfSaleId, lugar.SalesChannelId, venta.DocumentTypeId, lugar.EsConsumidorFinal, credito);
        var ofrecibles = await OfreciblesAsync(medios.Values.Select(m => m.Id).ToList(), ct);
        for (var i = 0; i < entradas.Count; i++)
        {
            if (!medios.TryGetValue(entradas[i].PaymentMeansPublicId, out var m) || !DisponibilidadDeMedio.SeOfrece(ofrecibles[m.Id], caso))
                return Result.Failure<PagosRegistrados>(ErrorDePago(new ErrorDePago(DisponibilidadDeMedio.MeansNotAvailable, i,
                    new Dictionary<string, object?> { ["paymentMeansCode"] = m?.Code })));
        }

        // Sesiones abiertas del usuario (los medios que se arquean exigen una; en el POS la pone el servidor).
        var abiertas = await db.CashSessions.AsNoTracking()
            .Where(s => s.CashierUserId == usuarioId && s.Status == CashSessionStatus.Open)
            .Select(s => new { s.Id, s.PublicId }).ToListAsync(ct);
        var propuestos = new List<PagoPropuesto>(entradas.Count);
        foreach (var e in entradas)
        {
            var m = medios[e.PaymentMeansPublicId];
            int? sesion = null;
            if (m.CountMethod != CashCountMethod.None)
                sesion = lugar.CashSessionId ?? abiertas.FirstOrDefault(a => a.PublicId == e.CashSessionPublicId)?.Id;
            propuestos.Add(new PagoPropuesto(Copia(m), e.Amount, e.Tendered, e.Reference, e.AuthorizationCode, e.Last4, e.BatchNumber, sesion));
        }
        var validado = ValidadorDePagos.Validar(new PedidoDeCobro(venta.AmountDue, propuestos, abiertas.Select(a => a.Id).ToList()));
        if (!validado.IsValid) return Result.Failure<PagosRegistrados>(ErrorDePago(validado.Errors[0]));

        // Los datáfonos (el del cuerpo o el propuesto de la caja, si es del adquirente del medio).
        var terminalIds = entradas.Select(e => e.CardTerminalPublicId).OfType<Guid>().Distinct().ToList();
        var terminales = await db.CardTerminals.AsNoTracking().Where(t => terminalIds.Contains(t.PublicId) || t.Id == lugar.DefaultCardTerminalId)
            .Select(t => new { t.Id, t.PublicId, t.CardAcquirerId }).ToListAsync(ct);

        // Un cobro repetido después de volver a borrador reemplaza los pagos anteriores y libera sus bonos.
        var previos = await db.DocumentPayments.Where(p => p.DocumentId == venta.Id && !p.IsDeleted).ToListAsync(ct);
        if (previos.Count > 0)
        {
            var previosIds = previos.Select(p => p.Id).ToList();
            foreach (var bono in await db.VoucherRedemptions.Where(v => previosIds.Contains(v.DocumentPaymentId) && v.Status == VoucherRedemptionStatus.Active).ToListAsync(ct))
                bono.Liberar(venta.Id, reloj.UtcNow, "El cobro se volvió a registrar.");
            foreach (var p in previos)
            {
                p.IsDeleted = true;
                p.DeletedAt = reloj.UtcNow;
            }
        }

        var pagos = new List<DocumentPayment>(entradas.Count);
        var vueltas = 0m;
        for (var i = 0; i < entradas.Count; i++)
        {
            var e = entradas[i];
            var m = medios[e.PaymentMeansPublicId];
            var v = validado.Payments.Single(x => x.Index == i);
            var terminal = e.CardTerminalPublicId is { } t
                ? terminales.FirstOrDefault(x => x.PublicId == t)?.Id
                : terminales.FirstOrDefault(x => x.Id == lugar.DefaultCardTerminalId && x.CardAcquirerId == m.CardAcquirerId)?.Id;
            var pago = new DocumentPayment
            {
                Document = venta,
                DocumentId = venta.Id,
                LineNumber = (short)(i + 1),
                Direction = PaymentDirection.Received,
                Amount = e.Amount,
                AmountTendered = e.Tendered,
                ChangeGiven = v.Change > 0m ? v.Change : null,
                Reference = string.IsNullOrWhiteSpace(e.Reference) ? null : e.Reference.Trim(),
                NormalizedReference = v.NormalizedReference,
                AuthorizationCode = string.IsNullOrWhiteSpace(e.AuthorizationCode) ? null : e.AuthorizationCode.Trim(),
                CardTerminalId = terminal,
                TerminalBatchNumber = string.IsNullOrWhiteSpace(e.BatchNumber) ? null : e.BatchNumber.Trim(),
                Last4 = e.Last4,
                CashSessionId = propuestos[i].CashSessionId,
            };
            pago.CopiarDelMedio(m);
            if (ClasesDeMedio.EsCredito(m.Class) && e.Credit is { } c)
            {
                pago.InstallmentCount = c.Installments;
                pago.CreditTermDays = c.TermDays;
                pago.InstallmentPeriodDays = c.PeriodicityDays;
                pago.FirstDueDate = c.FirstDueDate;
                pago.FinalDueDate = venta.OperationDate.AddDays(c.TermDays);
                if (!string.IsNullOrWhiteSpace(c.SuggestedLineCode)) pago.SuggestedCreditLineCode = c.SuggestedLineCode.Trim();
            }
            db.DocumentPayments.Add(pago);
            pagos.Add(pago);
            vueltas += v.Change;

            if (m.UniqueReference && v.NormalizedReference is { } numero)
            {
                if (await ColisionesDeVenta.BonoUsadoAsync(db, m.Id, numero, ct) is { } usado) return Result.Failure<PagosRegistrados>(usado);
                db.VoucherRedemptions.Add(new VoucherRedemption
                {
                    PaymentMeansId = m.Id,
                    NormalizedNumber = numero,
                    DocumentPayment = pago,
                    DocumentId = venta.Id,
                    RedeemedAt = reloj.UtcNow,
                });
            }
        }
        return Result.Success(new PagosRegistrados(pagos, vueltas));
    }

    /// <summary>Los bonos de los pagos, para traducir una colisión del índice único bajo concurrencia.</summary>
    public static IReadOnlyCollection<(int PaymentMeansId, string NormalizedNumber)> Bonos(IEnumerable<DocumentPayment> pagos) =>
        pagos.Where(p => p.NormalizedReference is not null).Select(p => (p.PaymentMeansId, p.NormalizedReference!)).ToList();

    private static MedioDePagoCopia Copia(PaymentMeans m) => new(m.Id, m.Code, m.Class, m.AllowsChange, m.AllowsPartial, m.RequiresReference,
        m.ReferenceKind, m.ReferenceMinLength, m.ReferenceMaxLength, m.UniqueReference, m.CountMethod);

    private async Task<IReadOnlyDictionary<int, MedioOfrecible>> OfreciblesAsync(IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        var medios = await db.PaymentMeans.AsNoTracking().Where(m => ids.Contains(m.Id)).ToListAsync(ct);
        var puntos = (await db.PaymentMeansPointsOfSale.AsNoTracking().Where(x => ids.Contains(x.PaymentMeansId)).Select(x => new { x.PaymentMeansId, x.PointOfSaleId }).ToListAsync(ct))
            .ToLookup(x => x.PaymentMeansId, x => x.PointOfSaleId);
        var canales = (await db.PaymentMeansChannels.AsNoTracking().Where(x => ids.Contains(x.PaymentMeansId)).Select(x => new { x.PaymentMeansId, x.SalesChannelId }).ToListAsync(ct))
            .ToLookup(x => x.PaymentMeansId, x => x.SalesChannelId);
        var tipos = (await db.PaymentMeansDocumentTypes.AsNoTracking().Where(x => ids.Contains(x.PaymentMeansId)).Select(x => new { x.PaymentMeansId, x.DocumentTypeId }).ToListAsync(ct))
            .ToLookup(x => x.PaymentMeansId, x => x.DocumentTypeId);
        return medios.ToDictionary(m => m.Id, m => new MedioOfrecible(m.Id, m.Code, m.Name, m.Class, m.DisplayOrder, m.QuickKey, m.IsActive, m.ValidFrom,
            m.ValidTo, m.OfferedAtAllPointsOfSale, puntos[m.Id].ToList(), m.OfferedInAllChannels, canales[m.Id].ToList(), m.OfferedForAllDocumentTypes,
            tipos[m.Id].ToList()));
    }

    /// <summary>El error de pago con su sobre (§22.5): código, mensaje y <c>data</c> con el índice del pago.</summary>
    public static Error ErrorDePago(ErrorDePago e)
    {
        var datos = new Dictionary<string, object?>(e.Data, StringComparer.Ordinal);
        if (e.PaymentIndex is int i) datos["paymentIndex"] = i;
        var medio = e.Data.TryGetValue("paymentMeansCode", out var c) ? c?.ToString() : null;
        var mensaje = e.Code switch
        {
            ValidadorDePagos.TotalMismatch => "Los pagos no suman exactamente el valor a pagar.",
            ValidadorDePagos.ChangeNotAllowed => $"El medio {medio} no da vueltas: el valor entregado debe ser igual al pago.",
            ValidadorDePagos.ReferenceRequired => $"El medio {medio} exige la referencia del pago.",
            ValidadorDePagos.ReferenceInvalid => $"La referencia del medio {medio} no tiene el formato ni la longitud que exige.",
            ValidadorDePagos.DuplicateReference => $"La misma referencia se repite en dos pagos del medio {medio}.",
            ValidadorDePagos.CardNumberNotAllowed => "No escriba el número de la tarjeta: sólo los cuatro últimos dígitos y la autorización.",
            ValidadorDePagos.PartialNotAllowed => $"El medio {medio} sólo puede cubrir todo lo que falta por pagar.",
            ValidadorDePagos.CashSessionRequired => $"El medio {medio} se arquea: el pago va en una sesión de caja abierta suya.",
            ValidadorDePagos.AmountInvalid => "Cada pago debe tener un valor mayor que cero.",
            DisponibilidadDeMedio.MeansNotAvailable => $"El medio {medio} no se ofrece en esta venta.",
            _ => "El pago no es válido.",
        };
        return new ErrorConDatos(e.Code, mensaje, datos);
    }
}
