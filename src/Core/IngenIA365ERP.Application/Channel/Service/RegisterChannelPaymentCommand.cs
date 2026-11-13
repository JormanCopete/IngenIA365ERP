using System.Globalization;
using System.Text.RegularExpressions;
using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.BankItems;
using IngenIA365ERP.Domain.Entities.Channel;
using IngenIA365ERP.Domain.Enums.Channel;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Channel.Service;

// Feature 021, US5 (T100–T102; research R3, R4, R6; FR-044 a FR-046; contracts/erp-api-de-servicio.md, «payment-notices»): el canal avisa un
// pago que la pasarela aprobó y el ERP lo registra como recibo «en línea» de Recaudo contra el convenio del destino y lo aplica por los
// contratos, en la misma unidad de trabajo, a nombre del asociado que pagó (Actor.AsociadoPorCanal, que pone ActorDeLaPeticion). Un pago
// aprobado nunca se rechaza: lo que no se puede aplicar queda a favor del asociado y el aviso sale «en revisión» (FR-046).

/// <summary>Los datos del intento aprobado en la pasarela. Nunca datos de tarjeta (FR-042).</summary>
public sealed record AprobacionDeLaPasarelaDto(
    long RequestId,
    long? InternalReference,
    string? Authorization,
    string? Receipt,
    string? Franchise,
    string? PaymentMethodName,
    string? IssuerName);

/// <summary>Un concepto del aviso, con la modalidad del canal (<c>Due</c>, <c>Partial</c>, <c>Prepayment</c>, <c>Payoff</c>).</summary>
public sealed record ItemDelAvisoDto(string ConceptKey, string Modality, decimal Amount, string? PrepaymentChoice);

/// <summary>Desde dónde pagó el asociado (auditoría).</summary>
public sealed record PagadorDelAvisoDto(string? Ip, string? UserAgent, Guid? AccountId);

/// <summary>
/// <c>Applied</c> o <c>InReview</c>, con el recibo si lo hubo. Repetir el aviso devuelve exactamente esto (FR-044).
/// </summary>
public sealed record ChannelPaymentNoticeResultDto(string Outcome, Guid? ReceiptPublicId, string? ReceiptNumber, string? Reason);

/// <summary>El aviso. La persona va en la ruta (así el actor es el asociado); la clave de operación sale de la referencia.</summary>
public sealed record RegisterChannelPaymentCommand(
    Guid PersonPublicId,
    string Reference,
    Guid DestinationPublicId,
    decimal Total,
    string Currency,
    DateTimeOffset ApprovedAt,
    AprobacionDeLaPasarelaDto Gateway,
    IReadOnlyList<ItemDelAvisoDto> Items,
    PagadorDelAvisoDto? Payer) : IRequest<Result<ChannelPaymentNoticeResultDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }

    /// <summary>La clave de operación de un aviso: la misma para la misma referencia (el canal reintenta con ella).</summary>
    public static Guid ClaveDeOperacion(string referencia) => AplicacionNoInteractiva.ClaveDelRecibo("CDG.Aviso|" + referencia);
}

public sealed partial class RegisterChannelPaymentCommandValidator : AbstractValidator<RegisterChannelPaymentCommand>
{
    public RegisterChannelPaymentCommandValidator()
    {
        RuleFor(x => x.PersonPublicId).NotEmpty();
        RuleFor(x => x.Reference).NotEmpty().Matches(Referencia()).WithMessage("La referencia es alfanumérica en mayúscula, de hasta 32 caracteres.");
        RuleFor(x => x.DestinationPublicId).NotEmpty();
        RuleFor(x => x.Currency).Equal("COP").WithMessage("La moneda del canal es COP.");
        RuleFor(x => x.Total).GreaterThan(0m).Must(Entero).WithMessage("El total va en pesos enteros.");
        RuleFor(x => x.Gateway).NotNull();
        RuleFor(x => x.Items).NotEmpty().Must(i => i.Count <= 20).WithMessage("Un aviso lleva entre 1 y 20 conceptos.");
        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(x => x.ConceptKey).Must(k => ClaveDeConcepto.Leer(k) is not null).WithMessage("La clave del concepto no tiene la forma LOAN:… o ACCOUNT:….");
            i.RuleFor(x => x.Modality).Must(m => m is ModosDelCanal.Due or ModosDelCanal.Partial or ModosDelCanal.Prepayment or ModosDelCanal.Payoff)
                .WithMessage("La modalidad no es una del canal (Due, Partial, Prepayment, Payoff).");
            i.RuleFor(x => x.Amount).GreaterThan(0m).Must(Entero).WithMessage("El valor va en pesos enteros.");
            i.RuleFor(x => x.PrepaymentChoice).Must(c => c is null or "ReduceTerm" or "ReduceInstallment")
                .WithMessage("La elección del abono es ReduceTerm o ReduceInstallment.");
        });
    }

    private static bool Entero(decimal v) => decimal.Truncate(v) == v;

    [GeneratedRegex("^[A-Z0-9]{1,32}$")]
    private static partial Regex Referencia();
}

public sealed class RegisterChannelPaymentCommandHandler(IApplicationDbContext db, AplicacionDelCanal aplicacionDelCanal)
    : IRequestHandler<RegisterChannelPaymentCommand, Result<ChannelPaymentNoticeResultDto>>
{
    public const string TotalNoCuadra = "Channel.Notice.TotalMismatch";
    public const string DestinoDesconocido = "Channel.Notice.DestinationNotFound";
    public const string Aplicado = AplicacionDelCanal.Aplicado;
    public const string EnRevision = AplicacionDelCanal.EnRevision;

    /// <inheritdoc cref="AplicacionDelCanal.LlaveDelRecibo"/>
    public static string LlaveDelRecibo(Guid convenio, string referencia) => AplicacionDelCanal.LlaveDelRecibo(convenio, referencia);

    /// <inheritdoc cref="AplicacionDelCanal.EsPse"/>
    public static bool EsPse(AprobacionDeLaPasarelaDto g) => AplicacionDelCanal.EsPse(g);

    public async Task<Result<ChannelPaymentNoticeResultDto>> Handle(RegisterChannelPaymentCommand request, CancellationToken ct)
    {
        // Repetido: la misma respuesta (FR-044). Cubre también un aviso que ya entró por la conciliación del archivo de la pasarela (R4).
        var previo = await db.ChannelPaymentNotices.AsNoTracking().FirstOrDefaultAsync(n => n.Reference == request.Reference, ct);
        if (previo is not null) return Result.Success(await aplicacionDelCanal.RespuestaAsync(previo, ct));

        if (request.Items.Sum(i => i.Amount) != request.Total)
            return Result.Failure<ChannelPaymentNoticeResultDto>(new Error(TotalNoCuadra,
                "La suma de los conceptos no coincide con el total del aviso; no se aplicó nada."));

        // La persona puede haber perdido la habilitación después de pagar: el pago igual se aplica.
        var persona = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId, ct);
        if (persona is null) return Result.Failure<ChannelPaymentNoticeResultDto>(PersonaDelCanal.ErrorNoHabilitada);
        var destino = await db.ChannelPaymentDestinations.AsNoTracking().FirstOrDefaultAsync(d => d.PublicId == request.DestinationPublicId, ct);
        if (destino is null)
            return Result.Failure<ChannelPaymentNoticeResultDto>(new Error(DestinoDesconocido, "El destino de pago del aviso no existe en la cooperativa."));

        var r = await aplicacionDelCanal.AplicarAsync(new PagoDelCanal(request.Reference, persona, destino, request.Total, request.ApprovedAt, request.Gateway,
            request.Items, request.Payer), null, cerrojosTomados: false, ct);
        return Result.Success(r.Respuesta);
    }
}
