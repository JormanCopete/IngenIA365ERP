using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Treasury.Checkbooks;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Results;

// Feature 015, T1 (fase 35, T0421; FR-020, FR-117, FR-156, FR-177; contracts/api.md §13): el comprobante de pago numerado por pago. Nace con el resultado «girada» de la
// orden, en la misma unidad de trabajo; se imprime con su código de verificación y se envía al beneficiario por correo sólo con la autorización del canal (SMS y mensajería
// siguen simulados). Sin datos de otros titulares.

/// <summary>Un comprobante de pago, para la pantalla y el informe. (nuevo)</summary>
public sealed record PaymentVoucherDto(Guid PublicId, string Number, Guid PaymentOrderPublicId, string OrderNumber, string OrderOrigin, Guid BeneficiaryPersonPublicId,
    string BeneficiaryName, PaymentForm Form, decimal GrossAmount, decimal WithholdingAmount, decimal NetAmount, DateOnly PaidOn, string TreasuryReference, string? CheckNumber,
    string? BankAccountCode, string VerificationCode, DateTime? SentAt, string? SentTo, int SendCount);

/// <summary>Lo que se imprime en el comprobante de pago (FR-117, FR-156). (nuevo)</summary>
public sealed record ComprobanteDePagoModel(string Company, string Nit, string Number, string VerificationCode, DateOnly PaidOn, string Beneficiary, string BeneficiaryTaxId,
    string OrderNumber, string Concept, string Form, string? BankName, string? AccountNumberMasked, string? DestinationMasked, decimal GrossAmount, decimal WithholdingAmount,
    decimal NetAmount, string AmountInWords, string TreasuryReference, string? CheckNumber);

/// <summary>El resultado del envío de un comprobante: enviado, sin autorización, sin correo o simulado. (nuevo)</summary>
public sealed record AvisoDeTesoreriaDto(string Outcome, string Channel, string? Destination, string Message);

/// <summary>Lo común del comprobante de pago: la emisión al registrar «girada», el código de verificación y el DTO. (nuevo)</summary>
public static class ComprobantesDePago
{
    /// <summary>La clase de numeración de los comprobantes de pago (<c>TES:CE</c>).</summary>
    public const string Codigo = "CE";

    public const string Enviado = "Sent";
    public const string SinAutorizacion = "NotAuthorized";
    public const string SinDestino = "NoDestination";
    public const string Simulado = "Simulated";

    public static string CodigoDeVerificacion()
    {
        const string alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = RandomNumberGenerator.GetBytes(16);
        return new string(bytes.Select(b => alfabeto[b % alfabeto.Length]).ToArray());
    }

    /// <summary>
    /// Emite el comprobante del pago de una orden girada (uno por orden: si ya lo tiene, no hace nada), numerado y con su código. Sin guardar: va en la unidad de trabajo del
    /// resultado.
    /// </summary>
    public static async Task EmitirAsync(IApplicationDbContext db, NumeradorDeTesoreria numerador, PaymentOrder orden, DisbursementResult resultado, OwnBankAccount? cuenta,
        DateTime ahora, CancellationToken ct)
    {
        if (db.PaymentVouchers.Local.Any(v => v.PaymentOrderId == orden.Id) || await db.PaymentVouchers.AsNoTracking().AnyAsync(v => v.PaymentOrderId == orden.Id, ct)) return;
        var retencion = orden.Origin == PaymentOrderOrigin.Manual ? orden.WithholdingAmount ?? 0m : 0m;
        var comprobante = new PaymentVoucher
        {
            Number = await numerador.AsignarAsync(Codigo, Codigo, ct), PaymentOrderId = orden.Id, BeneficiaryPersonId = orden.BeneficiaryPersonId, BankAccountId = cuenta?.Id,
            Form = orden.Form, GrossAmount = resultado.Amount + retencion, WithholdingAmount = retencion, NetAmount = resultado.Amount, PaidOn = resultado.OccurredOn,
            TreasuryReference = resultado.TreasuryReference, CheckNumber = resultado.CheckNumber, Concept = orden.Concept, VerificationCode = CodigoDeVerificacion(),
            IssuedAt = ahora,
        };
        // El resultado recién creado todavía no tiene Id: la navegación deja que EF ponga la llave al guardar.
        comprobante.DisbursementResult = resultado;
        db.PaymentVouchers.Add(comprobante);
    }

    public static async Task<PaymentVoucherDto> DtoAsync(IApplicationDbContext db, PaymentVoucher v, CancellationToken ct)
    {
        var orden = await db.PaymentOrders.AsNoTracking().Where(o => o.Id == v.PaymentOrderId).Select(o => new { o.PublicId, o.Number, o.Origin, o.BeneficiaryName }).FirstAsync(ct);
        var persona = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == v.BeneficiaryPersonId).Select(p => p.PublicId).FirstAsync(ct);
        var cuenta = v.BankAccountId is { } c ? await db.OwnBankAccounts.AsNoTracking().Where(x => x.Id == c).Select(x => x.Code).FirstOrDefaultAsync(ct) : null;
        return new PaymentVoucherDto(v.PublicId, v.Number, orden.PublicId, orden.Number, orden.Origin.ToString(), persona, orden.BeneficiaryName, v.Form, v.GrossAmount,
            v.WithholdingAmount, v.NetAmount, v.PaidOn, v.TreasuryReference, v.CheckNumber, cuenta, v.VerificationCode, v.SentAt, v.SentTo, v.SendCount);
    }

    public static string Enmascarar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
        var t = texto.Trim();
        var arroba = t.IndexOf('@', StringComparison.Ordinal);
        if (arroba > 1) return $"{t[0]}***{t[arroba..]}";
        return t.Length <= 4 ? "****" : $"****{t[^4..]}";
    }
}

/// <summary><c>GET /api/treasury/payment-vouchers?from=&amp;to=</c> (<c>Treasury.Orders.View</c>; nuevo): los comprobantes de pago del rango. (nuevo)</summary>
public sealed record ListPaymentVouchersQuery(DateOnly? From = null, DateOnly? To = null) : IRequest<Result<IReadOnlyList<PaymentVoucherDto>>>;

public sealed class ListPaymentVouchersQueryHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance, IDateTimeService reloj)
    : IRequestHandler<ListPaymentVouchersQuery, Result<IReadOnlyList<PaymentVoucherDto>>>
{
    public async Task<Result<IReadOnlyList<PaymentVoucherDto>>> Handle(ListPaymentVouchersQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var hasta = request.To ?? reloj.HoyLocal;
        var desde = request.From ?? hasta.AddDays(-30);
        var filas = (await db.PaymentVouchers.AsNoTracking().Where(v => v.PaidOn >= desde && v.PaidOn <= hasta).OrderByDescending(v => v.Id).Take(500).ToListAsync(ct))
            .Where(v => v.BankAccountId is null || mio.IncluyeCuenta(v.BankAccountId.Value)).ToList();
        var lista = new List<PaymentVoucherDto>(filas.Count);
        foreach (var v in filas) lista.Add(await ComprobantesDePago.DtoAsync(db, v, ct));
        return Result.Success<IReadOnlyList<PaymentVoucherDto>>(lista);
    }
}

/// <summary><c>GET /api/treasury/payment-vouchers/{id}/document</c> (<c>Treasury.Orders.View</c>): el modelo del comprobante de pago para el PDF. (nuevo)</summary>
public sealed record GetPaymentVoucherDocumentQuery(Guid PaymentVoucherPublicId) : IRequest<Result<ComprobanteDePagoModel>>;

public sealed class GetPaymentVoucherDocumentQueryHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance)
    : IRequestHandler<GetPaymentVoucherDocumentQuery, Result<ComprobanteDePagoModel>>
{
    public async Task<Result<ComprobanteDePagoModel>> Handle(GetPaymentVoucherDocumentQuery request, CancellationToken ct)
    {
        var v = await db.PaymentVouchers.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == request.PaymentVoucherPublicId, ct);
        if (v is null || (v.BankAccountId is { } c && !(await alcance.ObtenerAsync(ct)).IncluyeCuenta(c)))
            return Result.Failure<ComprobanteDePagoModel>(ErroresDeTesoreria.NoEncontrado("El comprobante de pago"));
        return Result.Success(await ModeloAsync(db, v, ct));
    }

    public static async Task<ComprobanteDePagoModel> ModeloAsync(IApplicationDbContext db, PaymentVoucher v, CancellationToken ct)
    {
        var orden = await db.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == v.PaymentOrderId, ct);
        var empresa = await db.Companies.AsNoTracking().Select(e => new { e.Name, Nit = e.TaxId }).FirstOrDefaultAsync(ct);
        var cuenta = v.BankAccountId is { } id
            ? await (from a in db.OwnBankAccounts.AsNoTracking()
                     join b in db.Banks.AsNoTracking().IgnoreQueryFilters() on a.BankId equals b.Id
                     where a.Id == id
                     select new { a.AccountNumber, b.Name }).FirstOrDefaultAsync(ct)
            : null;
        return new ComprobanteDePagoModel(empresa?.Name ?? string.Empty, empresa?.Nit ?? string.Empty, v.Number, v.VerificationCode, v.PaidOn, orden.BeneficiaryName,
            ComprobantesDePago.Enmascarar(orden.BeneficiaryTaxId), orden.Number, v.Concept, v.Form switch
            {
                PaymentForm.Check => "Cheque",
                PaymentForm.ThirdPartyPayment => "Pago a tercero",
                _ => "Transferencia",
            }, cuenta?.Name, cuenta is null ? null : ComprobantesDePago.Enmascarar(cuenta.AccountNumber),
            v.Form == PaymentForm.Check ? null : ComprobantesDePago.Enmascarar(orden.DestinationAccountNumber), v.GrossAmount, v.WithholdingAmount, v.NetAmount,
            ValorEnLetras.Pesos(v.NetAmount), v.TreasuryReference, v.CheckNumber);
    }
}

/// <summary>El comprobante de pago en PDF (lo implementa la API con QuestPDF, como el cheque). (nuevo)</summary>
public interface IComprobanteDePagoEnPdf
{
    byte[] ComprobanteDePago(ComprobanteDePagoModel modelo);
}

/// <summary>
/// <c>POST /api/treasury/payment-vouchers/{id}/send</c> (<c>Treasury.Results.Register</c>, con clave): envía el comprobante al beneficiario. Con correo, el PDF al correo del
/// maestro o al indicado, sólo si el beneficiario autorizó el canal; sin autorización o sin correo no sale nada y se responde por qué. SMS y mensajería: simulados. Deja el
/// evento <c>Treasury.PaymentVoucher.Sent</c> con el resultado (sin el contenido). (nuevo)
/// </summary>
public sealed record SendPaymentVoucherCommand(Guid PaymentVoucherPublicId, MessageChannel Channel = MessageChannel.Email, string? Email = null)
    : IRequest<Result<AvisoDeTesoreriaDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SendPaymentVoucherCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    IAutorizacionDeDatos autorizaciones,
    IComprobanteDePagoEnPdf pdf,
    IEmailSender correo,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<SendPaymentVoucherCommand, Result<AvisoDeTesoreriaDto>>
{
    public const string CanalDeCorreo = nameof(MessageChannel.Email);

    public async Task<Result<AvisoDeTesoreriaDto>> Handle(SendPaymentVoucherCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        var v = await db.PaymentVouchers.FirstOrDefaultAsync(x => x.PublicId == request.PaymentVoucherPublicId, ct);
        if (v is null || (v.BankAccountId is { } c && !(await alcance.ObtenerAsync(ct)).IncluyeCuenta(c)))
            return Result.Failure<AvisoDeTesoreriaDto>(ErroresDeTesoreria.NoEncontrado("El comprobante de pago"));
        var beneficiario = await db.People.AsNoTracking().IgnoreQueryFilters().FirstAsync(p => p.Id == v.BeneficiaryPersonId, ct);
        AvisoDeTesoreriaDto aviso;
        if (request.Channel is not MessageChannel.Email)
            aviso = new AvisoDeTesoreriaDto(ComprobantesDePago.Simulado, request.Channel.ToString(), null,
                "SMS y mensajería siguen simulados hasta que haya proveedor: el comprobante no salió (FR-020).");
        else if (await autorizaciones.VigenteAsync(beneficiario.PublicId, DataAuthorizationPurpose.ContactChannel, CanalDeCorreo, ct) is not { Autoriza: true })
            aviso = new AvisoDeTesoreriaDto(ComprobantesDePago.SinAutorizacion, CanalDeCorreo, null,
                "El beneficiario no autorizó el correo como canal de contacto: el comprobante no se envía. Entréguelo impreso.");
        else if ((string.IsNullOrWhiteSpace(request.Email) ? beneficiario.Email?.Trim() : request.Email.Trim()) is not { Length: > 0 } destino)
            aviso = new AvisoDeTesoreriaDto(ComprobantesDePago.SinDestino, CanalDeCorreo, null, "El beneficiario no tiene correo registrado: regístrelo o indíquelo.");
        else
        {
            var modelo = await GetPaymentVoucherDocumentQueryHandler.ModeloAsync(db, v, ct);
            var cuerpo = string.Create(CultureInfo.InvariantCulture,
                $"<p>{WebUtility.HtmlEncode(modelo.Company)} le informa el pago {WebUtility.HtmlEncode(v.Number)} del {v.PaidOn:dd/MM/yyyy} por {v.NetAmount:N0}.</p>" +
                $"<p>Código de verificación: <strong>{WebUtility.HtmlEncode(v.VerificationCode)}</strong>. El comprobante va adjunto.</p>");
            await correo.SendAsync(new EmailMessage(destino, $"Comprobante de pago {v.Number}", cuerpo,
                Attachments: [new EmailAttachment($"comprobante-{v.Number}.pdf", "application/pdf", pdf.ComprobanteDePago(modelo))]), ct);
            aviso = new AvisoDeTesoreriaDto(ComprobantesDePago.Enviado, CanalDeCorreo, ComprobantesDePago.Enmascarar(destino), "Comprobante enviado por correo.");
            v.SentAt = reloj.UtcNow;
            v.SentTo = aviso.Destination;
            v.SentByUserId = actor.UserId;
            v.SendCount++;
        }
        await auditoria.AgregarAsync("Treasury.PaymentVoucher.Sent", "PaymentVoucher", v.PublicId, null,
            new { voucher = v.Number, channel = aviso.Channel, outcome = aviso.Outcome, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(aviso);
    }
}
