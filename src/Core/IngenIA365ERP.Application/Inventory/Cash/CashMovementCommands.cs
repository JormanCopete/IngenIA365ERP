using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Documents.Efectos;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Pos;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Sales.Payments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Cash;

// ------------------------------------------------------------------------------------------------ entrada --

/// <summary>
/// El cuerpo de <c>POST</c> y <c>PUT /api/inventory/cash-movements</c> (contracts/api.md §21.3; feature 012, I3, T619). Se guarda por el
/// ciclo común: <see cref="ComoBorrador"/> lo convierte en el <see cref="SaveInventoryDraftRequest"/> de
/// <c>SaveInventoryDraftCommand(…, ExpectedGroup = Cash, …)</c> y lo propio del movimiento lo aplica <see cref="BorradorDeMovimientoDeCaja"/>.
/// Sin <see cref="DocumentTypePublicId"/>, el tipo de movimiento de caja por defecto (<see cref="TiposDeCaja.PorDefectoAsync"/>, que resuelve
/// quien llama). <see cref="ReclassifiedPaymentPublicId"/> y los datos del medio correcto son de la reclasificación. (nuevo)
/// </summary>
public sealed record CashMovementInput(
    Guid? DocumentTypePublicId,
    Guid CashSessionPublicId,
    CashMovementKind Kind,
    Guid SourcePaymentMeansPublicId,
    decimal Amount,
    string Reason,
    Guid? TargetPaymentMeansPublicId = null,
    CashMovementDestination? Destination = null,
    Guid? DestinationCashRegisterPublicId = null,
    IReadOnlyList<DenominationCountInput>? Denominations = null,
    Guid? ReclassifiedPaymentPublicId = null,
    string? TargetReference = null,
    string? TargetAuthorizationCode = null,
    Guid? TargetCardTerminalPublicId = null,
    Guid? DepositBankPublicId = null,
    DateOnly? OperationDate = null,
    byte[]? RowVersion = null)
{
    /// <summary>El borrador del ciclo común: sin líneas, con el motivo y los datos del movimiento (<see cref="DatosDeMovimientoDeCaja"/>).</summary>
    public SaveInventoryDraftRequest ComoBorrador(Guid tipoPorDefecto) => new(
        DocumentTypePublicId ?? tipoPorDefecto, OperationDate, null, null, null, null, null, Reason, null, null, null, null, RowVersion, [],
        CashMovement: new DatosDeMovimientoDeCaja(CashSessionPublicId, Kind, SourcePaymentMeansPublicId, TargetPaymentMeansPublicId, Destination,
            DestinationCashRegisterPublicId, Amount, Denominations, ReclassifiedPaymentPublicId, TargetReference, TargetAuthorizationCode,
            TargetCardTerminalPublicId, DepositBankPublicId));
}

/// <summary>
/// Lo que el borrador de un movimiento de caja agrega al del ciclo común (nuevo): viaja en <see cref="SaveInventoryDraftRequest.CashMovement"/>
/// y sólo lo admite el grupo <c>Cash</c>.
/// </summary>
public sealed record DatosDeMovimientoDeCaja(
    Guid CashSessionPublicId,
    CashMovementKind Kind,
    Guid SourcePaymentMeansPublicId,
    Guid? TargetPaymentMeansPublicId,
    CashMovementDestination? Destination,
    Guid? DestinationCashRegisterPublicId,
    decimal Amount,
    IReadOnlyList<DenominationCountInput>? Denominations,
    Guid? ReclassifiedPaymentPublicId,
    string? TargetReference,
    string? TargetAuthorizationCode,
    Guid? TargetCardTerminalPublicId,
    Guid? DepositBankPublicId);

/// <summary>Los tipos de documento de la caja que usa el sistema cuando no se indica uno (nuevo).</summary>
public static class TiposDeCaja
{
    /// <summary>El primer tipo activo de la clase (por código); nulo si la cooperativa no tiene ninguno.</summary>
    public static async Task<InventoryDocumentType?> PorDefectoAsync(IApplicationDbContext db, DocumentClass clase, CancellationToken ct) =>
        await db.InventoryDocumentTypes.Where(t => t.Class == clase && t.IsActive).OrderBy(t => t.Code).FirstOrDefaultAsync(ct);
}

// ------------------------------------------------------------------------------------------------ reglas --

/// <summary>
/// Las reglas del movimiento de caja que comparten el borrador (como <c>issues</c>) y la confirmación (feature 012, I3, T619; §21.3):
/// los campos de cada <see cref="CashMovementKind"/>, la sesión abierta de la caja destino de un retiro a otra caja y el tope del retiro
/// —lo esperado del medio en la sesión (sólo lo confirmado) o, si la sesión ya se contó, lo contado—. (nuevo)
/// </summary>
public sealed class ReglasDeMovimientoDeCaja(IApplicationDbContext db, SesionesDeCaja sesiones)
{
    /// <summary>¿El movimiento saca dinero de la sesión de origen? (los tres retiros).</summary>
    public static bool EsRetiro(CashMovementKind kind) =>
        kind is CashMovementKind.WithdrawalToSafe or CashMovementKind.WithdrawalToRegister or CashMovementKind.WithdrawalForDeposit;

    /// <summary>
    /// Los campos de §21.3 por clase de movimiento: devuelve el destino normalizado (el que exige la clase si no vino) o el error del campo
    /// que falta o sobra.
    /// </summary>
    public static Result<CashMovementDestination?> Campos(CashMovementKind kind, CashMovementDestination? destination, bool conCajaDestino,
        bool conMedioCorrecto, bool medioCorrectoIgualAlOrigen, decimal amount)
    {
        if (amount <= 0m) return Falla(ErroresDeCaja.MovementInvalid("amount", "El valor del movimiento debe ser mayor que cero."));
        if (kind != CashMovementKind.ReclassificationBetweenMeans && conMedioCorrecto)
            return Falla(ErroresDeCaja.MovementInvalid("targetPaymentMeansPublicId", "Sólo la reclasificación lleva el medio correcto."));
        if (kind != CashMovementKind.WithdrawalToRegister && conCajaDestino)
            return Falla(ErroresDeCaja.MovementInvalid("destinationCashRegisterPublicId", "Sólo el retiro a otra caja lleva la caja destino."));

        CashMovementDestination? Exige(CashMovementDestination esperado) => destination ?? esperado;
        return kind switch
        {
            CashMovementKind.WithdrawalToSafe => Exige(CashMovementDestination.Safe) == CashMovementDestination.Safe
                ? Result.Success<CashMovementDestination?>(CashMovementDestination.Safe)
                : Falla(ErroresDeCaja.MovementInvalid("destination", "El retiro a la caja fuerte va a Safe.")),
            CashMovementKind.WithdrawalForDeposit => Exige(CashMovementDestination.Deposit) == CashMovementDestination.Deposit
                ? Result.Success<CashMovementDestination?>(CashMovementDestination.Deposit)
                : Falla(ErroresDeCaja.MovementInvalid("destination", "El retiro para consignar va a Deposit.")),
            CashMovementKind.WithdrawalToRegister => Exige(CashMovementDestination.Register) != CashMovementDestination.Register
                ? Falla(ErroresDeCaja.MovementInvalid("destination", "El retiro a otra caja va a Register."))
                : !conCajaDestino
                    ? Falla(ErroresDeCaja.MovementInvalid("destinationCashRegisterPublicId", "Indique la caja que recibe el dinero."))
                    : Result.Success<CashMovementDestination?>(CashMovementDestination.Register),
            // El ingreso de base viene de la caja fuerte.
            CashMovementKind.BaseIncome => Exige(CashMovementDestination.Safe) == CashMovementDestination.Safe
                ? Result.Success<CashMovementDestination?>(CashMovementDestination.Safe)
                : Falla(ErroresDeCaja.MovementInvalid("destination", "La base del día entra desde la caja fuerte (Safe).")),
            CashMovementKind.ReclassificationBetweenMeans => destination is not null
                ? Falla(ErroresDeCaja.MovementInvalid("destination", "La reclasificación no tiene destino: cambia el medio de un pago."))
                : !conMedioCorrecto
                    ? Falla(ErroresDeCaja.MovementInvalid("targetPaymentMeansPublicId", "Indique el medio correcto del pago."))
                    : medioCorrectoIgualAlOrigen
                        ? Falla(ErroresDeCaja.MovementInvalid("targetPaymentMeansPublicId", "El medio correcto tiene que ser distinto del registrado."))
                        : Result.Success<CashMovementDestination?>(null),
            _ => Falla(ErroresDeCaja.MovementInvalid("kind", "Clase de movimiento desconocida.")),
        };

        static Result<CashMovementDestination?> Falla(Error e) => Result.Failure<CashMovementDestination?>(e);
    }

    /// <summary>La sesión abierta de la caja destino de un retiro a otra caja, o <c>Inventory.CashMovement.DestinationRegisterClosed</c>.</summary>
    public async Task<Result<CashSession>> DestinoAsync(CashMovementDetail detalle, bool seguir, CancellationToken ct)
    {
        var cajaId = detalle.DestinationCashRegisterId ?? 0;
        var consulta = seguir ? db.CashSessions : db.CashSessions.AsNoTracking();
        var abierta = await consulta.Where(s => s.CashRegisterId == cajaId && s.Status == CashSessionStatus.Open).OrderBy(s => s.Id).FirstOrDefaultAsync(ct);
        if (abierta is not null) return Result.Success(abierta);
        var codigo = await db.CashRegisters.AsNoTracking().IgnoreQueryFilters().Where(c => c.Id == cajaId).Select(c => c.Code).FirstOrDefaultAsync(ct);
        return Result.Failure<CashSession>(ErroresDeCaja.DestinationRegisterClosed(codigo ?? string.Empty));
    }

    /// <summary>
    /// El tope de un retiro: lo esperado del medio en la sesión (sólo lo confirmado) o, si la sesión ya se contó, lo contado. Sobre él →
    /// <c>Inventory.CashMovement.ExceedsExpected</c> (<c>data.expected</c>); nulo si cabe o no es un retiro.
    /// </summary>
    public async Task<Error?> ExcesoAsync(CashMovementDetail detalle, CashSession origen, CancellationToken ct)
    {
        if (!EsRetiro(detalle.Kind)) return null;
        var contado = await (from l in db.CashCountLines.AsNoTracking()
                             join c in db.CashCounts.AsNoTracking() on l.CashCountId equals c.Id
                             where c.CashSessionId == origen.Id && !c.IsDeleted && !l.IsDeleted && l.PaymentMeansId == detalle.SourcePaymentMeansId
                             select (decimal?)l.CountedAmount).FirstOrDefaultAsync(ct);
        var tope = contado ?? (await sesiones.EsperadoAsync(origen, [detalle.SourcePaymentMeansId], ct)).Lines
            .Where(l => l.Medio.PaymentMeansId == detalle.SourcePaymentMeansId).Sum(l => l.Expected);
        if (detalle.Amount <= tope) return null;
        var codigo = await db.PaymentMeans.AsNoTracking().IgnoreQueryFilters().Where(m => m.Id == detalle.SourcePaymentMeansId).Select(m => m.Code).FirstAsync(ct);
        return ErroresDeCaja.ExceedsExpected(codigo, tope);
    }
}

// ------------------------------------------------------------------------------------------------ el grupo --

/// <summary>
/// El borrador del movimiento de caja (feature 012, I3, T619; contracts/api.md §21.3; FR-100, T50): lo que el grupo <c>Cash</c> agrega al
/// guardado del ciclo común (<see cref="SaveInventoryDraftCommand"/>). La sesión de origen tiene que estar abierta y ser visible (propia, o
/// ajena con <c>Inventory.CashSessions.ViewAll</c>); el documento toma su punto, su caja, su sesión y la sucursal del punto; el satélite
/// <c>INV_CashMovementDetails</c> lleva la clase y sus campos (§21.3), las denominaciones como dato y, en la reclasificación, el pago
/// que se corrige —sin editarlo— y los datos del medio correcto. Un número de tarjeta en la referencia nunca se guarda
/// (<c>Payments.CardNumberNotAllowed</c>). La caja destino cerrada y el retiro sobre lo esperado vuelven como <c>issues</c>; la
/// confirmación los rechaza. Ninguno guarda. (nuevo)
/// </summary>
public sealed class BorradorDeMovimientoDeCaja(IApplicationDbContext db, SesionesDeCaja sesiones, ReglasDeMovimientoDeCaja reglas) : IBorradorDeGrupo
{
    public DocumentClassGroup Grupo => DocumentClassGroup.Cash;

    public Task<Result<SaveInventoryDraftRequest>> PrepararAsync(InventoryDocumentType tipo, SaveInventoryDraftRequest pedido, InventoryDocument? existente, CancellationToken ct)
    {
        if (tipo.Class != DocumentClass.CashMovement) return Task.FromResult(Result.Failure<SaveInventoryDraftRequest>(InventoryErrors.TypeNotForRoute(tipo.Class, DocumentClassGroup.Cash)));
        if (pedido.CashMovement is null)
            return Task.FromResult(Result.Failure<SaveInventoryDraftRequest>(ErroresDeCaja.MovementInvalid("kind", "Indique la clase del movimiento, la sesión, el medio y el valor.")));
        if (string.IsNullOrWhiteSpace(pedido.Reason))
            return Task.FromResult(Result.Failure<SaveInventoryDraftRequest>(ErroresDeCaja.MovementInvalid("reason", "Todo movimiento de caja lleva su motivo.")));
        if (pedido.Lines.Count > 0)
            return Task.FromResult(Result.Failure<SaveInventoryDraftRequest>(ErroresDeCaja.MovementInvalid("lines", "Un movimiento de caja no lleva líneas de producto.")));
        return Task.FromResult(Result.Success(pedido));
    }

    public async Task<Result<ResultadoDelBorrador>> AplicarAsync(BorradorEnCurso borrador, CancellationToken ct)
    {
        var documento = borrador.Documento;
        var m = borrador.Pedido.CashMovement!;

        // La sesión: visible y abierta.
        var sesion = await sesiones.VisibleAsync(m.CashSessionPublicId, seguir: false, ct);
        if (sesion is null) return Falla(ErroresDeCaja.SessionNotFound());
        if (!sesion.EstaAbierta) return Falla(ErroresDelPos.CashSessionNotOpen());
        var punto = await db.PointsOfSale.AsNoTracking().FirstAsync(p => p.Id == sesion.PointOfSaleId, ct);

        // Los medios, la caja destino, el datáfono, el banco y el pago que se corrige.
        var medioIds = new[] { m.SourcePaymentMeansPublicId, m.TargetPaymentMeansPublicId ?? Guid.Empty }.Where(g => g != Guid.Empty).Distinct().ToList();
        var medios = await db.PaymentMeans.AsNoTracking().Where(x => medioIds.Contains(x.PublicId)).ToDictionaryAsync(x => x.PublicId, ct);
        if (!medios.TryGetValue(m.SourcePaymentMeansPublicId, out var origen))
            return Falla(ErroresDeCaja.MovementInvalid("sourcePaymentMeansPublicId", "El medio de pago no existe."));
        var destinoMedio = m.TargetPaymentMeansPublicId is { } t ? medios.GetValueOrDefault(t) : null;
        if (m.TargetPaymentMeansPublicId is not null && destinoMedio is null)
            return Falla(ErroresDeCaja.MovementInvalid("targetPaymentMeansPublicId", "El medio de pago correcto no existe."));

        var campos = ReglasDeMovimientoDeCaja.Campos(m.Kind, m.Destination, m.DestinationCashRegisterPublicId is not null, destinoMedio is not null,
            destinoMedio?.Id == origen.Id, m.Amount);
        if (campos.IsFailure) return Falla(campos.Error);

        int? cajaDestino = null;
        if (m.DestinationCashRegisterPublicId is { } cd)
        {
            var caja = await db.CashRegisters.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == cd, ct);
            if (caja is null) return Falla(ErroresDePuntoDeVenta.CashRegisterNotFound());
            if (caja.Id == sesion.CashRegisterId)
                return Falla(ErroresDeCaja.MovementInvalid("destinationCashRegisterPublicId", "La caja que recibe tiene que ser otra."));
            cajaDestino = caja.Id;
        }
        int? datafono = null;
        if (m.TargetCardTerminalPublicId is { } tt)
        {
            datafono = await db.CardTerminals.AsNoTracking().Where(x => x.PublicId == tt).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            if (datafono is null) return Falla(ErroresDeCaja.MovementInvalid("targetCardTerminalPublicId", "El datáfono no existe."));
        }
        int? banco = null;
        if (m.DepositBankPublicId is { } bk)
        {
            banco = await db.Banks.AsNoTracking().Where(x => x.PublicId == bk).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            if (banco is null) return Falla(ErroresDeCaja.MovementInvalid("depositBankPublicId", "El banco no existe."));
        }
        if (new[] { m.TargetReference, m.TargetAuthorizationCode }.Any(ValidadorDePagos.PareceNumeroDeTarjeta))
            return Falla(RegistroDePagos.ErrorDePago(new ErrorDePago(ValidadorDePagos.CardNumberNotAllowed, null,
                new Dictionary<string, object?> { ["paymentMeansCode"] = destinoMedio?.Code ?? origen.Code })));

        int? pagoCorregido = null;
        if (m.ReclassifiedPaymentPublicId is { } rp)
        {
            if (m.Kind != CashMovementKind.ReclassificationBetweenMeans)
                return Falla(ErroresDeCaja.MovementInvalid("reclassifiedPaymentPublicId", "Sólo la reclasificación corrige un pago."));
            var pago = await (from p in db.DocumentPayments.AsNoTracking()
                              join d in db.InventoryDocuments.AsNoTracking() on p.DocumentId equals d.Id
                              where p.PublicId == rp && !p.IsDeleted
                              select new { p.Id, p.CashSessionId, p.PaymentMeansId, p.Direction, p.Amount, d.Status }).FirstOrDefaultAsync(ct);
            if (pago is null || pago.CashSessionId != sesion.Id || pago.Status != DocumentStatus.Confirmed || pago.Direction != PaymentDirection.Received)
                return Falla(ErroresDeCaja.MovementInvalid("reclassifiedPaymentPublicId", "El pago no es un cobro confirmado de esta sesión."));
            if (pago.PaymentMeansId != origen.Id)
                return Falla(ErroresDeCaja.MovementInvalid("sourcePaymentMeansPublicId", "El medio registrado no es el del pago que se corrige."));
            if (m.Amount > pago.Amount)
                return Falla(ErroresDeCaja.MovementInvalid("amount", "La reclasificación no puede superar el valor del pago."));
            pagoCorregido = pago.Id;
        }

        string? denominaciones = null;
        if (m.Denominations is { Count: > 0 } pedidas)
        {
            var ids = pedidas.Select(d => d.CashDenominationPublicId).Distinct().ToList();
            var catalogo = await db.CashDenominations.AsNoTracking().Where(d => ids.Contains(d.PublicId)).ToDictionaryAsync(d => d.PublicId, d => d.Value, ct);
            if (catalogo.Count != ids.Count || pedidas.Any(d => d.Quantity < 0))
                return Falla(ErroresDeCaja.MovementInvalid("denominations", "Alguna denominación no existe o su cantidad es negativa."));
            denominaciones = JsonSerializer.Serialize(pedidas.Select(d => new
            {
                denominationPublicId = d.CashDenominationPublicId, quantity = d.Quantity, amount = catalogo[d.CashDenominationPublicId] * d.Quantity,
            }));
        }

        // La cabecera: punto, caja, sesión y sucursal del punto; el valor es su total.
        documento.PointOfSaleId = sesion.PointOfSaleId;
        documento.CashRegisterId = sesion.CashRegisterId;
        documento.CashSessionId = sesion.Id;
        documento.BranchId = punto.BranchId;
        documento.Subtotal = m.Amount;
        documento.DiscountTotal = 0m;
        documento.TaxTotal = 0m;
        documento.WithholdingTotal = 0m;
        documento.Total = m.Amount;
        documento.AmountDue = m.Amount;

        // El satélite (1:1).
        var detalle = documento.Id == 0 ? null : await db.CashMovementDetails.FirstOrDefaultAsync(d => d.DocumentId == documento.Id && !d.IsDeleted, ct);
        if (detalle is null)
        {
            detalle = new CashMovementDetail { Document = documento };
            db.CashMovementDetails.Add(detalle);
        }
        detalle.CashSessionId = sesion.Id;
        detalle.Kind = m.Kind;
        detalle.SourcePaymentMeansId = origen.Id;
        detalle.TargetPaymentMeansId = destinoMedio?.Id;
        detalle.Destination = campos.Value;
        detalle.DestinationCashRegisterId = cajaDestino;
        detalle.DestinationCashSessionId = null;
        detalle.DepositBankId = banco;
        detalle.Amount = m.Amount;
        detalle.DenominationsJson = denominaciones;
        detalle.ReclassifiedPaymentId = pagoCorregido;
        detalle.TargetReference = string.IsNullOrWhiteSpace(m.TargetReference) ? null : m.TargetReference.Trim();
        detalle.TargetAuthorizationCode = string.IsNullOrWhiteSpace(m.TargetAuthorizationCode) ? null : m.TargetAuthorizationCode.Trim();
        detalle.TargetCardTerminalId = datafono;

        // Lo que hoy impediría confirmar.
        var avisos = new List<Error>();
        if (m.Kind == CashMovementKind.WithdrawalToRegister && (await reglas.DestinoAsync(detalle, seguir: false, ct)) is { IsFailure: true } cerrada)
            avisos.Add(cerrada.Error);
        if (await reglas.ExcesoAsync(detalle, sesion, ct) is { } exceso) avisos.Add(exceso);
        return Result.Success(new ResultadoDelBorrador(avisos, []));
    }

    public Task<Error?> TraducirColisionAsync(DbUpdateException ex, BorradorEnCurso borrador, CancellationToken ct) => Task.FromResult<Error?>(null);

    private static Result<ResultadoDelBorrador> Falla(Error e) => Result.Failure<ResultadoDelBorrador>(e);
}

// ------------------------------------------------------------------------------------------------ estrategias --

/// <summary>
/// La estrategia del movimiento de caja (clase <c>CashMovement</c>; feature 012, I3, T619; §21.3; FR-100). En la confirmación: la sesión
/// de origen sigue abierta; un retiro a otra caja resuelve la sesión abierta de la destino (<c>DestinationRegisterClosed</c>) y toca las
/// dos sesiones <b>por Id ascendente</b> antes del cerrojo (<see cref="IToqueDeSesionDeCaja"/>, el mismo orden en toda operación que toma
/// dos, para no cruzarse); un retiro no supera lo esperado del medio (<c>ExceedsExpected</c>); el monto que aprueba la política del tipo
/// es el valor; emite <c>MovimientoDeCajaRegistrado</c>. La anulación sólo con la sesión todavía abierta (<c>SessionClosed</c>) y emite
/// <c>DocumentoAnulado</c>. No mueve inventario. (nuevo)
/// </summary>
public sealed class EfectoMovimientoDeCaja(
    IApplicationDbContext db,
    EmisionDeInventario emision,
    ReglasDeMovimientoDeCaja reglas,
    IToqueDeSesionDeCaja toque,
    IDateTimeService reloj) : EfectoDeClaseBase
{
    public override DocumentClass Clase => DocumentClass.CashMovement;

    public override async Task<Result> ValidarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        if (contexto.EsAnulacion)
        {
            var delOriginal = await DetalleAsync(contexto.Original!, ct);
            var abierta = delOriginal is not null
                && await db.CashSessions.AsNoTracking().AnyAsync(s => s.Id == delOriginal.CashSessionId && s.Status == CashSessionStatus.Open, ct);
            return abierta ? Result.Success() : Result.Failure(ErroresDeCaja.MovementSessionClosed());
        }

        var documento = contexto.Documento;
        if (string.IsNullOrWhiteSpace(documento.Reason)) return Result.Failure(InventoryErrors.FieldRequired(ReglasDelDocumento.CampoMotivo));
        var detalle = await DetalleAsync(documento, ct);
        if (detalle is null) return Result.Failure(ErroresDeCaja.MovementInvalid("kind", "El movimiento no tiene sus datos: vuelva a guardarlo."));

        var origen = await db.CashSessions.FirstOrDefaultAsync(s => s.Id == detalle.CashSessionId, ct);
        if (origen is null || !origen.EstaAbierta) return Result.Failure(ErroresDelPos.CashSessionNotOpen());

        CashSession? destino = null;
        if (detalle.Kind == CashMovementKind.WithdrawalToRegister)
        {
            var resuelta = await reglas.DestinoAsync(detalle, seguir: true, ct);
            if (resuelta.IsFailure) return Result.Failure(resuelta.Error);
            destino = resuelta.Value;
            detalle.DestinationCashSessionId = destino.Id;
        }

        // Las dos sesiones por Id ascendente, antes del cerrojo: un cierre en curso espera o el movimiento llega tarde.
        foreach (var sesion in new[] { origen, destino }.OfType<CashSession>().DistinctBy(s => s.Id).OrderBy(s => s.Id))
        {
            if (await toque.TocarAsync(sesion.Id, reloj.UtcNow, ct)) continue;
            return Result.Failure(sesion.Id == origen.Id
                ? ErroresDelPos.CashSessionNotOpen()
                : (await reglas.DestinoAsync(detalle, seguir: false, ct)).Error);
        }

        return await reglas.ExcesoAsync(detalle, origen, ct) is { } exceso ? Result.Failure(exceso) : Result.Success();
    }

    public override decimal MontoParaAprobar(ContextoDeEfecto contexto) => contexto.Documento.Total;

    public override PedidoDeCerrojo Cerrojo(ContextoDeEfecto contexto) => new();

    public override async Task<IReadOnlyList<object>> MensajesAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var detalle = await DetalleAsync(contexto.Documento, ct)
            ?? throw new InvalidOperationException($"El movimiento {contexto.Documento.PublicId} no tiene sus datos.");
        return [await emision.MovimientoDeCajaAsync(contexto.Documento, detalle, ct)];
    }

    public override Task<IReadOnlyList<object>> MensajesDeAnulacionAsync(ContextoDeEfecto contexto, CancellationToken ct) =>
        emision.AnulacionAsync(contexto.Documento, contexto.Original!, [], ct);

    private Task<CashMovementDetail?> DetalleAsync(InventoryDocument documento, CancellationToken ct) =>
        db.CashMovementDetails.FirstOrDefaultAsync(d => d.DocumentId == documento.Id && !d.IsDeleted, ct);
}

/// <summary>
/// La estrategia de la diferencia de arqueo (clase <c>CashCountDifference</c>; feature 012, I3, T618; §21.2; FR-099, T50). Sus líneas
/// (<c>INV_CashDocumentLines</c>) las escribe el cierre de la sesión. Con todas las líneas dentro de la tolerancia se confirma con su motivo
/// y sin aprobación; si no, la política del tipo evalúa la suma de lo que supera la tolerancia y el cajero de la sesión nunca aprueba
/// (aunque la haya cerrado un supervisor). Al confirmarse fija el arqueo (<see cref="CashCount.Fijar"/>) y emite
/// <c>DiferenciaDeArqueoAprobada</c>, también dentro de la tolerancia. No se anula: se recuenta antes de confirmarla. (nuevo)
/// </summary>
public sealed class EfectoDiferenciaDeArqueo(IApplicationDbContext db, EmisionDeInventario emision) : EfectoDeClaseBase
{
    private readonly Dictionary<int, IReadOnlyList<CashDocumentLine>> _lineas = [];

    public override DocumentClass Clase => DocumentClass.CashCountDifference;

    public override async Task<Result> ValidarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        if (contexto.Documento.CashSessionId is null) return Result.Failure(ErroresDeCaja.SessionNotFound());
        var lineas = await LineasAsync(contexto.Documento, ct);
        return lineas.Count == 0 ? Result.Failure(InventoryErrors.Empty()) : Result.Success();
    }

    public override async Task<bool> OmiteAprobacionAsync(ContextoDeEfecto contexto, CancellationToken ct) =>
        (await LineasAsync(contexto.Documento, ct)).All(l => l.WithinTolerance);

    public override async Task<IReadOnlyCollection<int>> ExcluidosDeLaAprobacionAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var cajero = await db.CashSessions.AsNoTracking().Where(s => s.Id == contexto.Documento.CashSessionId).Select(s => (int?)s.CashierUserId).FirstOrDefaultAsync(ct);
        return cajero is int c ? [c] : [];
    }

    public override decimal MontoParaAprobar(ContextoDeEfecto contexto) =>
        _lineas.TryGetValue(contexto.Documento.Id, out var lineas) ? lineas.Where(l => !l.WithinTolerance).Sum(l => l.Amount) : contexto.Documento.Total;

    public override PedidoDeCerrojo Cerrojo(ContextoDeEfecto contexto) => new();

    public override async Task<Result> AplicarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var arqueo = await db.CashCounts.Include(c => c.Lines).FirstOrDefaultAsync(c => c.DifferenceDocumentId == contexto.Documento.Id && !c.IsDeleted, ct);
        arqueo?.Fijar();
        return Result.Success();
    }

    public override async Task<IReadOnlyList<object>> MensajesAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var lineas = await LineasAsync(contexto.Documento, ct);
        var cajero = await (from s in db.CashSessions.AsNoTracking()
                            join u in db.Users.AsNoTracking().IgnoreQueryFilters() on s.CashierUserId equals u.Id
                            where s.Id == contexto.Documento.CashSessionId
                            select u.CentralUserId).FirstOrDefaultAsync(ct);
        return [await emision.DiferenciaDeArqueoAsync(contexto.Documento, lineas, null, cajero, ct)];
    }

    private async Task<IReadOnlyList<CashDocumentLine>> LineasAsync(InventoryDocument documento, CancellationToken ct)
    {
        var lineas = await db.CashDocumentLines.AsNoTracking().Where(l => l.DocumentId == documento.Id && !l.IsDeleted).OrderBy(l => l.LineNumber).ToListAsync(ct);
        _lineas[documento.Id] = lineas;
        return lineas;
    }
}

// ------------------------------------------------------------------------------------------------ consultas --

/// <summary>
/// <c>GET /api/inventory/cash-movements</c> (§21.3): los movimientos de las sesiones que quien pregunta ve (de su alcance de puntos; sin
/// <c>ViewAll</c>, sólo las suyas), con sus filtros; por fecha y número, descendente. (nuevo)
/// </summary>
public sealed record ListCashMovementsQuery(
    Guid? CashSessionPublicId = null,
    Guid? PointOfSalePublicId = null,
    CashMovementKind? Kind = null,
    DocumentStatus? Status = null,
    DateOnly? From = null,
    DateOnly? To = null,
    PageRequest? Pagina = null) : IRequest<Result<PagedResult<CashMovementDto>>>;

public sealed class ListCashMovementsQueryHandler(IApplicationDbContext db, SesionesDeCaja sesiones)
    : IRequestHandler<ListCashMovementsQuery, Result<PagedResult<CashMovementDto>>>
{
    public async Task<Result<PagedResult<CashMovementDto>>> Handle(ListCashMovementsQuery request, CancellationToken ct)
    {
        var visibles = await sesiones.VisiblesAsync(ct);
        if (request.CashSessionPublicId is { } s) visibles = visibles.Where(x => x.PublicId == s);
        if (request.PointOfSalePublicId is { } p)
        {
            var punto = await db.PointsOfSale.AsNoTracking().Where(x => x.PublicId == p).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct) ?? -1;
            visibles = visibles.Where(x => x.PointOfSaleId == punto);
        }
        var ids = visibles.Select(x => x.Id);
        var q = from m in db.CashMovementDetails.AsNoTracking()
                join d in db.InventoryDocuments.AsNoTracking() on m.DocumentId equals d.Id
                where !m.IsDeleted && ids.Contains(m.CashSessionId)
                select new { m, d };
        if (request.Kind is { } k) q = q.Where(x => x.m.Kind == k);
        if (request.Status is { } st) q = q.Where(x => x.d.Status == st);
        if (request.From is { } desde) q = q.Where(x => x.d.OperationDate >= desde);
        if (request.To is { } hasta) q = q.Where(x => x.d.OperationDate <= hasta);

        var pagina = request.Pagina ?? new PageRequest();
        var total = await q.LongCountAsync(ct);
        var filas = await q.OrderByDescending(x => x.d.OperationDate).ThenByDescending(x => x.d.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        var dtos = await VistaDeMovimientosDeCaja.DtosAsync(db, filas.Select(x => (x.m, x.d)).ToList(), ct);
        return Result.Success(new PagedResult<CashMovementDto>(dtos, pagina.SafePage, pagina.SafePageSize, total));
    }
}

/// <summary><c>GET /api/inventory/cash-movements/{id}</c>: un movimiento de una sesión visible (si no, el 404 del documento). (nuevo)</summary>
public sealed record GetCashMovementQuery(Guid DocumentPublicId) : IRequest<Result<CashMovementDto>>;

public sealed class GetCashMovementQueryHandler(IApplicationDbContext db, SesionesDeCaja sesiones)
    : IRequestHandler<GetCashMovementQuery, Result<CashMovementDto>>
{
    public async Task<Result<CashMovementDto>> Handle(GetCashMovementQuery request, CancellationToken ct)
    {
        var visibles = (await sesiones.VisiblesAsync(ct)).Select(s => s.Id);
        var fila = await (from m in db.CashMovementDetails.AsNoTracking()
                          join d in db.InventoryDocuments.AsNoTracking() on m.DocumentId equals d.Id
                          where !m.IsDeleted && d.PublicId == request.DocumentPublicId && visibles.Contains(m.CashSessionId)
                          select new { m, d }).FirstOrDefaultAsync(ct);
        return fila is null
            ? Result.Failure<CashMovementDto>(InventoryErrors.DocumentNotFound())
            : Result.Success((await VistaDeMovimientosDeCaja.DtosAsync(db, [(fila.m, fila.d)], ct))[0]);
    }
}

/// <summary>El <see cref="CashMovementDto"/> de varios movimientos. (nuevo)</summary>
public static class VistaDeMovimientosDeCaja
{
    public static async Task<IReadOnlyList<CashMovementDto>> DtosAsync(IApplicationDbContext db, IReadOnlyList<(CashMovementDetail Detalle, InventoryDocument Documento)> filas,
        CancellationToken ct)
    {
        if (filas.Count == 0) return [];
        var medioIds = filas.SelectMany(f => new[] { f.Detalle.SourcePaymentMeansId, f.Detalle.TargetPaymentMeansId ?? 0 }).Distinct().ToList();
        var medios = await db.PaymentMeans.AsNoTracking().IgnoreQueryFilters().Where(m => medioIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Code, ct);
        var sesionIds = filas.Select(f => f.Detalle.CashSessionId).Distinct().ToList();
        var sesiones = await db.CashSessions.AsNoTracking().Where(s => sesionIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.PublicId, ct);
        var cajaIds = filas.Select(f => f.Detalle.DestinationCashRegisterId).OfType<int>().Distinct().ToList();
        var cajas = await db.CashRegisters.AsNoTracking().IgnoreQueryFilters().Where(c => cajaIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Code, ct);
        return filas.Select(f => new CashMovementDto(
            f.Documento.PublicId, VistaDeDocumentos.NumeroVisible(f.Documento.Prefix, f.Documento.Number), f.Documento.Status,
            sesiones.GetValueOrDefault(f.Detalle.CashSessionId), f.Detalle.Kind, medios.GetValueOrDefault(f.Detalle.SourcePaymentMeansId) ?? string.Empty,
            f.Detalle.TargetPaymentMeansId is int t ? medios.GetValueOrDefault(t) : null, f.Detalle.Destination,
            f.Detalle.DestinationCashRegisterId is int c ? cajas.GetValueOrDefault(c) : null, f.Detalle.Amount, f.Documento.Reason ?? string.Empty,
            f.Documento.CreatedBy, null)).ToList();
    }
}
