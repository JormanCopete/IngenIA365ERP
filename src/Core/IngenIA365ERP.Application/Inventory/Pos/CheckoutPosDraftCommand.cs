using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Pricing;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Pos;

/// <summary>Lo que devuelve el cobro (<c>CheckoutResultDto</c>, §20.2). <c>electronic</c> lo agrega I4. (nuevo)</summary>
public sealed record CheckoutResultDto(
    Guid DocumentPublicId,
    DocumentStatus Status,
    Guid? ApprovalRequestPublicId,
    DocumentClass Class,
    string? Prefix,
    long? Number,
    decimal Total,
    decimal AmountDue,
    decimal Change,
    PostingMode? PostingMode,
    object? Electronic,
    TicketDto? Ticket,
    IReadOnlyList<AvisoDto> Warnings);

/// <summary>
/// Cobra y confirma la venta del POS (<c>POST /pos/drafts/{id}/checkout</c>; feature 012, I3, T606; §20.2; SC-002). En orden, en una
/// transacción:
/// <list type="number">
/// <item>la venta sigue en borrador en la sesión del usuario; el comprador de la factura a petición está identificado; ningún descuento
/// sobre el tope espera aprobación (<c>Inventory.Discount.ApprovalPending</c>); <c>expectedAmountDue</c> es lo que la venta dice
/// (<c>Inventory.Document.TotalChanged</c>);</item>
/// <item><c>UPDATE INV_CashSessions SET LastActivityAt … WHERE Status = Open</c> con una fila afectada, antes del cerrojo
/// (<see cref="IToqueDeSesionDeCaja"/>, data-model §15 «Concurrencia»); si no, <c>Inventory.CashSession.NotOpen</c>;</item>
/// <item>los pagos por <see cref="RegistroDePagos"/> (sesión puesta por el servidor, copias del medio, comisión esperada, datáfono
/// propuesto de la caja, <see cref="Domain.Sales.Payments.ValidadorDePagos"/> y <see cref="Domain.Sales.Payments.DisponibilidadDeMedio"/>)
/// y los bonos de referencia única en <c>INV_VoucherRedemptions</c>;</item>
/// <item>la confirmación por el flujo canónico (<see cref="ConfirmacionDeDocumento"/>, el mismo de
/// <c>ConfirmInventoryDocumentCommand(DocumentPublicId, ExpectedGroup = Sales)</c>, decisiones-transversales §1.3; un servicio y no un
/// envío por <c>ISender</c>, porque un comando reintentable anidado vaciaría el seguimiento de afuera). Con un nivel de aprobación
/// queda <c>PendingApproval</c> sin número, con sus pagos guardados.</item>
/// </list>
/// Confirmada, devuelve las vueltas y la tirilla del comprobante no electrónico. Un reintento con la misma <c>Idempotency-Key</c> devuelve
/// el mismo resultado (<c>IdempotencyBehavior</c>), nunca una segunda venta. La espera en línea de la DIAN la agrega I4 (US8). (nuevo)
/// </summary>
public sealed record CheckoutPosDraftCommand(Guid DraftPublicId, IReadOnlyList<DocumentPaymentInput> Payments, decimal ExpectedAmountDue, string? SendEmailTo = null)
    : IRequest<Result<CheckoutResultDto>>, IOperacionIdempotente, IOperacionDePuntoDeVenta
{
    public Guid OperationKey { get; init; }

    public Guid CashSessionPublicId { get; init; }
}

public sealed class CheckoutPosDraftCommandValidator : AbstractValidator<CheckoutPosDraftCommand>
{
    public CheckoutPosDraftCommandValidator()
    {
        RuleFor(x => x.DraftPublicId).NotEmpty();
        RuleFor(x => x.Payments).NotNull().Must(p => p.Count is > 0 and <= 20).WithMessage("El cobro lleva entre uno y veinte pagos.");
        RuleForEach(x => x.Payments).ChildRules(p =>
        {
            p.RuleFor(x => x.PaymentMeansPublicId).NotEmpty();
            p.RuleFor(x => x.Reference).MaximumLength(60);
            p.RuleFor(x => x.AuthorizationCode).MaximumLength(20);
            p.RuleFor(x => x.BatchNumber).MaximumLength(20);
            p.RuleFor(x => x.Last4).MaximumLength(4);
        });
        RuleFor(x => x.SendEmailTo).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.SendEmailTo));
    }
}

public sealed class CheckoutPosDraftCommandHandler(
    IApplicationDbContext db,
    BorradorDelPos pos,
    RegistroDePagos pagos,
    AprobacionDeDescuentos aprobaciones,
    IToqueDeSesionDeCaja toque,
    ConfirmacionDeDocumento confirmacion,
    ConstructorDeTirilla tirilla,
    IAuditoriaDelPuntoDeVenta auditoria,
    TareasTrasElCommit? trasElCommit = null,
    EsperaEnLineaDelPos? esperaEnLinea = null)
    : IRequestHandler<CheckoutPosDraftCommand, Result<CheckoutResultDto>>
{
    public Task<Result<CheckoutResultDto>> Handle(CheckoutPosDraftCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var r = await CobrarAsync(request, ct);
            if (r.IsFailure) db.DescartarCambios();
            return r;
        }, ct);

    private async Task<Result<CheckoutResultDto>> CobrarAsync(CheckoutPosDraftCommand request, CancellationToken ct)
    {
        // (1) La venta y su sesión.
        var cargada = await pos.VentaAsync(request.DraftPublicId, soloBorrador: true, ct);
        if (cargada.IsFailure) return Falla(cargada.Error);
        var venta = cargada.Value;
        if (venta.IsSuspended) return Falla(ErroresDelPos.CashSessionNotOpen());
        var sesion = await pos.SesionDeLaVentaAsync(venta, exigirPos: false, ct);
        if (sesion.IsFailure) return Falla(sesion.Error);
        var (s, caja, punto) = sesion.Value;
        var usuario = await pos.UsuarioAsync(ct);

        var final = await pos.EsConsumidorFinalAsync(venta, ct);
        var rol = caja.DocumentTypes.FirstOrDefault(t => t.DocumentTypeId == venta.DocumentTypeId)?.Role;
        if (rol == CashRegisterDocumentRole.InvoiceOnRequest && final) return Falla(ErroresDelPos.InvoiceRequiresCustomer());
        if (!venta.Lines.Any(l => !l.IsDeleted)) return Falla(InventoryErrors.Empty());

        var aprobados = await aprobaciones.ExigirAprobadosAsync(venta, ct);
        if (aprobados.IsFailure) return Falla(aprobados.Error);
        if (request.ExpectedAmountDue != venta.AmountDue) return Falla(ErroresDelPos.TotalChanged(request.ExpectedAmountDue, venta.AmountDue));

        // (2) La sesión sigue abierta: el toque antes del cerrojo.
        if (!await toque.TocarAsync(s.Id, pos.Reloj.UtcNow, ct)) return Falla(ErroresDelPos.CashSessionNotOpen());

        // (3) Los pagos.
        var registrados = await pagos.RegistrarAsync(venta, request.Payments,
            new LugarDeCobro(punto.Id, venta.SalesChannelId ?? punto.SalesChannelId, s.Id, caja.DefaultCardTerminalId, final), usuario.Value.UserId, ct);
        if (registrados.IsFailure) return Falla(registrados.Error);
        await auditoria.AnotarAsync(AuditEventTypes.InventoryPosCheckout, request, venta.PublicId,
            new { venta.AmountDue, payments = registrados.Value.Pagos.Select(p => new { p.MeansCode, p.Amount, p.Last4 }).ToList(), change = registrados.Value.Change }, ct);

        // (3b) I4 (T735): con la contingencia del facturador abierta, la venta se numera con el tipo del rol de contingencia de la caja.
        await TipoDeVentaEnContingencia.AplicarAsync(db, venta, caja, ct);

        // (4) La confirmación por el flujo canónico.
        Result<ConfirmationResultDto> confirmada;
        try
        {
            confirmada = await confirmacion.ConfirmarAsync(new PedidoDeConfirmacion(venta.PublicId, DocumentClassGroup.Sales, venta.RowVersion), ct);
        }
        catch (DbUpdateException ex) when (ColisionesDeVenta.Es(ex))
        {
            var error = await ColisionesDeVenta.TraducirAsync(db, ex, new DatosDeLaColision(RegistroDePagos.Bonos(registrados.Value.Pagos)), ct);
            return Falla(error ?? throw ex);
        }
        if (confirmada.IsFailure) return Falla(confirmada.Error);
        var c = confirmada.Value;

        TicketDto? ticket = null;
        ElectronicoDeLaVentaDto? electronico = null;
        var formato = ReglasDePuntoDeVenta.FormatoDe(caja.ReceiptWidthMm);
        if (c.Status == DocumentStatus.Confirmed)
        {
            // I4 (T736): un documento electrónico no se entrega antes de validarse (o de salir en contingencia): la tirilla la trae la espera
            // en línea, después del commit.
            var registrado = await Integration.EstadoElectronicoDeInventario.DeAsync(db, venta.PublicId, ct);
            if (registrado is not null)
            {
                electronico = EsperaEnLineaDelPos.Bloque(registrado, 0, null);
            }
            else
            {
                var confirmado = await db.InventoryDocuments.Include(d => d.Lines).FirstAsync(d => d.PublicId == venta.PublicId, ct);
                ticket = await tirilla.ConstruirAsync(confirmado, formato, copia: false, ct);
            }
        }
        var resultado = new CheckoutResultDto(venta.PublicId, c.Status, c.Approval?.RequestPublicId, venta.Class,
            c.Number is null ? null : venta.Prefix, c.Number, venta.Total, venta.AmountDue, registrados.Value.Change, c.PostingMode, electronico, ticket,
            c.Warnings);
        if (electronico is not null && trasElCommit is not null && esperaEnLinea is not null)
        {
            trasElCommit.Completar<Result<CheckoutResultDto>>(async (r, t) =>
                r.IsSuccess ? Result.Success(await esperaEnLinea.CompletarAsync(r.Value, formato, t)) : r);
        }
        return Result.Success(resultado);
    }

    private static Result<CheckoutResultDto> Falla(Error error) => Result.Failure<CheckoutResultDto>(error);
}
