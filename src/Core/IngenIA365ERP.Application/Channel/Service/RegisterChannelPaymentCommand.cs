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

public sealed class RegisterChannelPaymentCommandHandler(
    IApplicationDbContext db,
    AplicacionNoInteractiva aplicacion,
    IContratoDeObligaciones obligaciones,
    IContratoDeCuentas cuentas,
    IAlertas alertas,
    IDateTimeService reloj) : IRequestHandler<RegisterChannelPaymentCommand, Result<ChannelPaymentNoticeResultDto>>
{
    public const string TotalNoCuadra = "Channel.Notice.TotalMismatch";
    public const string DestinoDesconocido = "Channel.Notice.DestinationNotFound";
    public const string Aplicado = "Applied";
    public const string EnRevision = "InReview";

    /// <summary>Colombia no tiene horario de verano.</summary>
    private static readonly TimeSpan HoraDeColombia = TimeSpan.FromHours(-5);

    /// <summary>
    /// La llave del recibo en línea (research R4): <c>GW:{convenio:N}:{referencia}</c>, la misma que tendrá la línea del archivo de la pasarela cuando
    /// Recaudo lo cargue, así el orden entre aviso y archivo no importa.
    /// </summary>
    public static string LlaveDelRecibo(Guid convenio, string referencia) =>
        string.Create(CultureInfo.InvariantCulture, $"GW:{convenio:N}:{referencia}");

    public async Task<Result<ChannelPaymentNoticeResultDto>> Handle(RegisterChannelPaymentCommand request, CancellationToken ct)
    {
        // Repetido: la misma respuesta (FR-044). Cubre también un aviso que llegó con otra clave de operación.
        var previo = await db.ChannelPaymentNotices.AsNoTracking().FirstOrDefaultAsync(n => n.Reference == request.Reference, ct);
        if (previo is not null) return Result.Success(await RespuestaAsync(previo, ct));

        if (request.Items.Sum(i => i.Amount) != request.Total)
            return Result.Failure<ChannelPaymentNoticeResultDto>(new Error(TotalNoCuadra,
                "La suma de los conceptos no coincide con el total del aviso; no se aplicó nada."));

        // La persona puede haber perdido la habilitación después de pagar: el pago igual se aplica.
        var persona = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId, ct);
        if (persona is null) return Result.Failure<ChannelPaymentNoticeResultDto>(PersonaDelCanal.ErrorNoHabilitada);
        var destino = await db.ChannelPaymentDestinations.AsNoTracking().FirstOrDefaultAsync(d => d.PublicId == request.DestinationPublicId, ct);
        if (destino is null)
            return Result.Failure<ChannelPaymentNoticeResultDto>(new Error(DestinoDesconocido, "El destino de pago del aviso no existe en la cooperativa."));
        var convenio = await db.CollectionAgreements.AsNoTracking().FirstAsync(c => c.Id == destino.CollectionAgreementId, ct);

        var config = await Common.ConfiguracionDelCanal.VigenteAsync(db, reloj.UtcNow, ct);
        var politica = config?.SurplusPolicy ?? SurplusPolicy.AdvanceElseCreditBalance;
        var fecha = DateOnly.FromDateTime(request.ApprovedAt.ToOffset(HoraDeColombia).DateTime);
        var esPse = EsPse(request.Gateway);
        var medioDelContrato = esPse ? ClaseDeMedioDeRecaudo.Transfer : ClaseDeMedioDeRecaudo.CreditCard;

        var items = request.Items.Select(Resolver).ToList();
        var reparto = await ExcedenteDelAviso.RepartirAsync(items, persona.PublicId, fecha, medioDelContrato, politica, obligaciones, cuentas, ct);

        Domain.Entities.Receipts.Operations.Receipt? recibo = null;
        var notas = reparto.Notas.ToList();
        // En revisión sólo si algo no se aplicó a un producto (saldo a favor, parte rechazada) o no hubo recibo; un anticipo es una aplicación.
        var enRevision = true;
        if (reparto.Partes.Count > 0)
        {
            var r = await aplicacion.AplicarAsync(new LineaNoInteractiva(
                LlaveDelRecibo(convenio.PublicId, request.Reference), ReceiptChannel.Online, persona.PublicId, fecha, request.Total, convenio.BranchId,
                reparto.Partes, medioDelContrato, esPse ? PaymentMeansClass.Transfer : PaymentMeansClass.CreditCard, convenio.OwnBankAccountPublicId,
                Recortar($"{request.Reference} · {request.Gateway.Authorization}", 60)), ct);
            if (r.Outcome is ProcessingLineOutcome.Applied or ProcessingLineOutcome.AlreadyApplied && r.Receipt is not null)
            {
                recibo = r.Receipt;
                notas.AddRange(r.RejectedParts.Select(p => $"Una parte no se aplicó ({p.Message}); quedó a favor del asociado."));
                enRevision = r.CreditBalance is not null || r.RejectedParts.Count > 0;
            }
            else
            {
                notas.Add($"Recaudo no pudo aplicar el pago ({r.Message ?? r.Code}); la cooperativa debe registrarlo a mano.");
            }
        }
        else
        {
            notas.Add("Ningún producto admitió el pago; la cooperativa debe registrarlo y devolverlo o reubicarlo.");
        }

        var clases = await ClasesDeCuentaAsync(persona.PublicId, fecha, request.Items, ct);
        var aviso = new ChannelPaymentNotice
        {
            Reference = request.Reference, PersonId = persona.Id, DestinationId = destino.Id, Total = request.Total,
            GatewayRequestId = request.Gateway.RequestId, InternalReference = request.Gateway.InternalReference,
            Authorization = Recortar(request.Gateway.Authorization, 60), GatewayReceipt = Recortar(request.Gateway.Receipt, 60),
            Franchise = Recortar(request.Gateway.Franchise, 30), PaymentMethodName = Recortar(request.Gateway.PaymentMethodName, 80),
            IssuerName = Recortar(request.Gateway.IssuerName, 120), ApprovedAt = request.ApprovedAt.UtcDateTime,
            PayerIp = Recortar(request.Payer?.Ip, 46), PayerUserAgent = Recortar(request.Payer?.UserAgent, 255), ChannelAccountId = request.Payer?.AccountId,
            Outcome = enRevision ? NoticeOutcome.InReview : NoticeOutcome.Applied,
            OutcomeReason = notas.Count > 0 ? Recortar(string.Join(" ", notas), 300) : null,
            ReceiptId = recibo?.Id,
            Items = request.Items.Select(i => new ChannelPaymentNoticeItem
            {
                ConceptKind = ClaseDelItem(i, clases), ProductPublicId = ClaveDeConcepto.Leer(i.ConceptKey)!.Value.Producto, Modality = i.Modality, Amount = i.Amount,
                PrepaymentChoice = i.PrepaymentChoice switch
                {
                    "ReduceTerm" => ChannelPrepaymentChoice.ReduceTerm,
                    "ReduceInstallment" => ChannelPrepaymentChoice.ReduceInstallment,
                    _ => null,
                },
            }).ToList(),
        };
        db.ChannelPaymentNotices.Add(aviso);
        await db.SaveChangesAsync(ct);

        if (enRevision)
        {
            await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.CanalPagoEnRevision, "Pago en línea en revisión",
                $"El pago en línea {request.Reference} por {request.Total.ToString("N0", CultureInfo.GetCultureInfo("es-CO"))} pesos quedó en revisión: {aviso.OutcomeReason}",
                "ChannelPaymentNotice", aviso.PublicId, DedupKey: $"Canal.PagoEnRevision:{aviso.PublicId:N}", ModuloDeOrigen: "Channel",
                ScopeBranchPublicId: await db.Branches.AsNoTracking().Where(b => b.Id == convenio.BranchId).Select(b => (Guid?)b.PublicId).FirstOrDefaultAsync(ct)), ct);
        }
        return Result.Success(new ChannelPaymentNoticeResultDto(enRevision ? EnRevision : Aplicado, recibo?.PublicId, recibo?.Number, aviso.OutcomeReason));
    }

    private async Task<ChannelPaymentNoticeResultDto> RespuestaAsync(ChannelPaymentNotice n, CancellationToken ct)
    {
        var recibo = n.ReceiptId is { } id
            ? await db.IssuedReceipts.AsNoTracking().Where(r => r.Id == id).Select(r => new { r.PublicId, r.Number }).FirstOrDefaultAsync(ct)
            : null;
        return new ChannelPaymentNoticeResultDto(n.Outcome == NoticeOutcome.Applied ? Aplicado : EnRevision, recibo?.PublicId, recibo?.Number, n.OutcomeReason);
    }

    private static ExcedenteDelAviso.ItemResuelto Resolver(ItemDelAvisoDto i)
    {
        var (esCredito, producto) = ClaveDeConcepto.Leer(i.ConceptKey)!.Value;
        EleccionDeAbono? eleccion = i.PrepaymentChoice switch
        {
            "ReduceTerm" => EleccionDeAbono.ReduceTerm,
            "ReduceInstallment" => EleccionDeAbono.ReduceInstallment,
            _ => null,
        };
        return new ExcedenteDelAviso.ItemResuelto(esCredito, producto, i.Modality, i.Amount, ModosDelCanal.DeCredito(i.Modality),
            ModosDelCanal.DeCuenta(i.Modality), eleccion);
    }

    /// <summary>La clase del concepto del aviso: crédito, o la de la cuenta según Captaciones (aportes si no la encuentra).</summary>
    private static ConceptKind ClaseDelItem(ItemDelAvisoDto i, IReadOnlyDictionary<Guid, ConceptKind> clases)
    {
        var (esCredito, producto) = ClaveDeConcepto.Leer(i.ConceptKey)!.Value;
        return esCredito ? ConceptKind.Loan : clases.GetValueOrDefault(producto, ConceptKind.Contribution);
    }

    private async Task<IReadOnlyDictionary<Guid, ConceptKind>> ClasesDeCuentaAsync(Guid persona, DateOnly fecha, IReadOnlyList<ItemDelAvisoDto> items,
        CancellationToken ct)
    {
        if (items.All(i => ClaveDeConcepto.Leer(i.ConceptKey)!.Value.EsCredito)) return new Dictionary<Guid, ConceptKind>();
        var r = await cuentas.ConsultarAsync(new SolicitudDeConsultaDeCuentas(Receipts.Common.ClavesDeRecaudo.Recaudador, persona, fecha), ct);
        return (r.Value?.Accounts ?? [])
            .Select(c => (c.AccountPublicId, Clase: ClaveDeConcepto.ClaseDeCuenta(c.Nature, c.DepositClass)))
            .Where(c => c.Clase is not null)
            .ToDictionary(c => c.AccountPublicId, c => c.Clase!.Value);
    }

    /// <summary>PSE es transferencia; lo demás (tarjeta) va como tarjeta de crédito: la pasarela no distingue débito de crédito.</summary>
    public static bool EsPse(AprobacionDeLaPasarelaDto g) =>
        (g.Franchise?.Contains("PSE", StringComparison.OrdinalIgnoreCase) ?? false)
        || string.Equals(g.PaymentMethodName, "PSE", StringComparison.OrdinalIgnoreCase);

    private static string? Recortar(string? v, int n) => string.IsNullOrWhiteSpace(v) ? null : v.Length <= n ? v : v[..n];
}
