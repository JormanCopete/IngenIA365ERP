using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Collections;
using IngenIA365ERP.Domain.Lending.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Lending.Collections.Tareas;

/// <summary>Qué paso del despacho se registra (T0307). (nuevo)</summary>
public enum PasoDeDespacho
{
    /// <summary>Se llamó al adaptador (o la guardia le quitó el efecto al simulado): <c>Sent</c>, <c>Simulated</c>, <c>Deferred</c> o <c>Failed</c>.</summary>
    Envio = 1,

    /// <summary>El motor difirió el mensaje al despacharlo.</summary>
    Diferido = 2,

    /// <summary>El motor lo excluyó al despacharlo (autorización revocada, fallecimiento, insolvencia, sin destino).</summary>
    Excluido = 3,

    /// <summary>La causa desapareció antes del envío (el crédito se puso al día).</summary>
    Cancelado = 4,

    /// <summary>La consulta de entrega respondió (<c>cartera.entregas</c>): <c>Delivered</c> o <c>Failed</c>.</summary>
    Entrega = 5,
}

/// <summary>
/// Registra un paso del despacho de un mensaje al deudor (feature 013, T0307; T75; contracts/adaptadores-y-archivos.md §4.6). <b>Sin ruta</b>:
/// lo envían las tareas <c>cartera.mensajes</c> y <c>cartera.entregas</c> con el actor «Proceso de integración». Un comando por mensaje e
/// intento, <b>idempotente por <see cref="DebtorMessagePublicId"/> + <see cref="Attempt"/></b>: si ese intento ya quedó, devuelve lo que
/// quedó sin escribir. El resultado del adaptador se traduce así:
/// <list type="table">
/// <item><term><c>Ok</c></term><description><c>Sent</c> con el destino copiado (C57) y la referencia; de <c>SIMULADO</c>, <c>Simulated</c>.</description></item>
/// <item><term><c>InProcess</c></term><description><c>Sent</c> sin referencia confirmada (lo resuelve <c>cartera.entregas</c>).</description></item>
/// <item><term><c>ChannelUnavailable</c></term><description><c>Deferred(ChannelUnavailable)</c> a la ventana siguiente; al llegar a
/// <c>Mensajes.Reintentos</c> intentos, <c>Failed</c>.</description></item>
/// <item><term><c>Rejected</c>, <c>InvalidData</c>, <c>NotFound</c></term><description><c>Failed</c>.</description></item>
/// </list>
/// Todo <c>Failed</c> levanta <c>Cartera.MensajeAlDeudorFallido</c> al jefe de cartera (quien tiene <c>Lending.DebtorMessages.Send</c>, con el
/// alcance de la oficina y la línea del crédito). El evento lo escribe <see cref="ColaDeMensajesAlDeudor"/>. (nuevo)
/// </summary>
public sealed record RegisterDebtorMessageAttemptCommand(Guid DebtorMessagePublicId, int Attempt, PasoDeDespacho Paso)
    : IRequest<Result<DebtorMessageEventKind>>
{
    /// <summary>Lo que respondió el adaptador (envío) o la consulta de entrega.</summary>
    public ExternalOutcome? Outcome { get; init; }

    /// <summary>El resultado vino del canal <c>SIMULADO</c>, o la guardia <c>RealDelivery</c> le quitó el efecto.</summary>
    public bool Simulado { get; init; }

    /// <summary>El destino leído del maestro al despachar (se copia en el evento, C57).</summary>
    public string? Destino { get; init; }

    public string? ReferenciaExterna { get; init; }
    public string? CodigoDelProveedor { get; init; }
    public string? Detalle { get; init; }

    /// <summary><c>Diferido</c>: el instante siguiente permitido; <c>ChannelUnavailable</c>: la ventana siguiente para reintentar (UTC).</summary>
    public DateTime? SiguienteIntento { get; init; }

    /// <summary><c>Diferido</c> y <c>Excluido</c>: los motivos del motor.</summary>
    public IReadOnlyList<ContactWindowReason>? Motivos { get; init; }

    /// <summary><c>Entrega</c>: lo que informó el proveedor.</summary>
    public IngenIA365ERP.Application.Common.Messaging.DeliveryState? Entrega { get; init; }
}

public sealed class RegisterDebtorMessageAttemptCommandValidator : AbstractValidator<RegisterDebtorMessageAttemptCommand>
{
    public RegisterDebtorMessageAttemptCommandValidator()
    {
        RuleFor(x => x.DebtorMessagePublicId).NotEmpty();
        RuleFor(x => x.Attempt).GreaterThan(0);
        RuleFor(x => x.Paso).IsInEnum();
        RuleFor(x => x.Outcome).NotNull().When(x => x.Paso is PasoDeDespacho.Envio or PasoDeDespacho.Entrega);
        RuleFor(x => x.Motivos).NotEmpty().When(x => x.Paso is PasoDeDespacho.Diferido or PasoDeDespacho.Excluido);
        RuleFor(x => x.SiguienteIntento).NotNull().When(x => x.Paso == PasoDeDespacho.Diferido);
        RuleFor(x => x.Detalle).NotEmpty().When(x => x.Paso == PasoDeDespacho.Cancelado);
    }
}

public sealed class RegisterDebtorMessageAttemptCommandHandler(
    IApplicationDbContext db,
    ColaDeMensajesAlDeudor cola,
    ReglasDeParametrosDeCartera parametros,
    IAlertas alertas,
    IDateTimeService reloj,
    ILogger<RegisterDebtorMessageAttemptCommandHandler> logger) : IRequestHandler<RegisterDebtorMessageAttemptCommand, Result<DebtorMessageEventKind>>
{
    /// <summary>El código de motivo de la cancelación porque el crédito se puso al día.</summary>
    public const string MotivoCreditoAlDia = "LoanCurrent";

    /// <summary>El texto de esa cancelación.</summary>
    public const string TextoCreditoAlDia = "El crédito se puso al día antes del envío.";

    private const int LargoDelCodigo = 80;
    private const int LargoDeLaReferencia = 120;

    public async Task<Result<DebtorMessageEventKind>> Handle(RegisterDebtorMessageAttemptCommand request, CancellationToken ct)
    {
        var mensaje = await db.DebtorMessages.FirstOrDefaultAsync(m => m.PublicId == request.DebtorMessagePublicId, ct);
        if (mensaje is null) return Result.Failure<DebtorMessageEventKind>(ErroresDeMensajesAlDeudor.MensajeInexistente());

        // Idempotente por mensaje + intento: lo que ya quedó se devuelve tal cual.
        var registrado = await db.DebtorMessageEvents.AsNoTracking()
            .Where(e => e.DebtorMessageId == mensaje.Id && e.AttemptNumber == request.Attempt)
            .OrderByDescending(e => e.Sequence).Select(e => (DebtorMessageEventKind?)e.Kind).FirstOrDefaultAsync(ct);
        if (registrado is { } yaQuedo) return Result.Success(yaQuedo);

        var ultimo = await cola.UltimoEventoAsync(mensaje, ct);
        if (ultimo is null) return Result.Failure<DebtorMessageEventKind>(ErroresDeMensajesAlDeudor.MensajeInexistente());
        if (!DebtorMessageEvent.PuedeSeguir(ultimo.Kind, Siguiente(request), cola.EnProduccion))
        {
            // Una persona lo canceló, o ya salió: el paso llegó tarde y no se escribe nada.
            logger.LogInformation("[Cartera.Mensajes] El mensaje {Mensaje} ya está {Estado}: el paso {Paso} del intento {Intento} no se registra.",
                mensaje.PublicId, ultimo.Kind, request.Paso, request.Attempt);
            return Result.Success(ultimo.Kind);
        }

        var evento = await EventoAsync(request, mensaje, ultimo, ct);
        await cola.RegistrarAsync(mensaje, evento, ct);
        await db.SaveChangesAsync(ct);

        if (evento.Kind == DebtorMessageEventKind.Failed) await AlertarAsync(mensaje, evento, ct);
        return Result.Success(evento.Kind);
    }

    /// <summary>La clase que tendría el evento sin contar los reintentos (para preguntar si la transición existe).</summary>
    private static DebtorMessageEventKind Siguiente(RegisterDebtorMessageAttemptCommand r) => r.Paso switch
    {
        PasoDeDespacho.Diferido => DebtorMessageEventKind.Deferred,
        PasoDeDespacho.Excluido => DebtorMessageEventKind.Excluded,
        PasoDeDespacho.Cancelado => DebtorMessageEventKind.Cancelled,
        PasoDeDespacho.Entrega => r.Outcome == ExternalOutcome.Ok && r.Entrega == IngenIA365ERP.Application.Common.Messaging.DeliveryState.Delivered
            ? DebtorMessageEventKind.Delivered
            : DebtorMessageEventKind.Failed,
        _ => r.Outcome switch
        {
            ExternalOutcome.Ok or ExternalOutcome.InProcess => DebtorMessageEventKind.Sent,
            ExternalOutcome.ChannelUnavailable => DebtorMessageEventKind.Deferred,
            _ => DebtorMessageEventKind.Failed,
        },
    };

    private async Task<EventoDeMensaje> EventoAsync(RegisterDebtorMessageAttemptCommand r, DebtorMessage mensaje, DebtorMessageEvent ultimo,
        CancellationToken ct)
    {
        var ahora = reloj.UtcNow;
        EventoDeMensaje Nuevo(DebtorMessageEventKind kind) => new(kind, ahora, ActorKind.Process)
        {
            AttemptNumber = r.Attempt,
            ExternalOutcome = r.Outcome,
            ProviderReference = Recortar(r.ReferenciaExterna, LargoDeLaReferencia),
            CodigoDeMotivo = Recortar(r.CodigoDelProveedor, LargoDelCodigo),
            TextoDelMotivo = r.Detalle,
        };

        switch (r.Paso)
        {
            case PasoDeDespacho.Diferido:
                return Nuevo(DebtorMessageEventKind.Deferred) with { DueAt = r.SiguienteIntento, Motivos = r.Motivos, CodigoDeMotivo = null };
            case PasoDeDespacho.Excluido:
                return Nuevo(DebtorMessageEventKind.Excluded) with { Motivos = r.Motivos, CodigoDeMotivo = null };
            case PasoDeDespacho.Cancelado:
                return Nuevo(DebtorMessageEventKind.Cancelled) with { CodigoDeMotivo = r.CodigoDelProveedor ?? MotivoCreditoAlDia };
            case PasoDeDespacho.Entrega:
                var kind = Siguiente(r);
                return Nuevo(kind) with
                {
                    ProviderReference = Recortar(r.ReferenciaExterna, LargoDeLaReferencia) ?? ultimo.ProviderReference,
                    DestinationAddress = kind == DebtorMessageEventKind.Failed ? ultimo.DestinationAddress : null,
                };
        }

        var simulado = r.Simulado || CodigosDeCanalExterno.EsSimulado(mensaje.ChannelCode);
        switch (r.Outcome)
        {
            case ExternalOutcome.Ok or ExternalOutcome.InProcess:
                return Nuevo(simulado ? DebtorMessageEventKind.Simulated : DebtorMessageEventKind.Sent) with { DestinationAddress = r.Destino };
            case ExternalOutcome.ChannelUnavailable:
                var reintentos = await ReintentosAsync(ct);
                var caidas = await db.DebtorMessageEvents.AsNoTracking()
                    .Where(e => e.DebtorMessageId == mensaje.Id && e.Kind == DebtorMessageEventKind.Deferred && e.ReasonCode != null)
                    .Select(e => e.ReasonCode!).ToListAsync(ct);
                var intentosFallidos = caidas.Count(c => c.Split(',').Contains(nameof(ContactWindowReason.ChannelUnavailable), StringComparer.Ordinal)
                                                         || c.Split(',').Contains(((int)ContactWindowReason.ChannelUnavailable).ToString(CultureInfo.InvariantCulture), StringComparer.Ordinal)) + 1;
                if (intentosFallidos >= reintentos || r.SiguienteIntento is null)
                    return Nuevo(DebtorMessageEventKind.Failed) with
                    {
                        DestinationAddress = r.Destino,
                        TextoDelMotivo = $"El canal no estuvo disponible en {intentosFallidos.ToString(CultureInfo.InvariantCulture)} intentos. {r.Detalle}".Trim(),
                    };
                return Nuevo(DebtorMessageEventKind.Deferred) with
                {
                    DueAt = r.SiguienteIntento,
                    Motivos = [ContactWindowReason.ChannelUnavailable],
                    CodigoDeMotivo = null,
                };
            default:
                return Nuevo(DebtorMessageEventKind.Failed) with { DestinationAddress = r.Destino };
        }
    }

    private async Task<int> ReintentosAsync(CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeCartera.MensajesReintentos, reloj.HoyLocal, ct: ct);
        return leido.IsSuccess && int.TryParse(leido.Value.Texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0 ? n : 1;
    }

    /// <summary>La alerta de un fallo definitivo (§4.6): al jefe de cartera, con el alcance del crédito. Nunca tumba el registro.</summary>
    private async Task AlertarAsync(DebtorMessage mensaje, EventoDeMensaje evento, CancellationToken ct)
    {
        var alcance = mensaje.LoanId is { } loanId
            ? await (from l in db.Loans.AsNoTracking()
                     join b in db.Branches.AsNoTracking() on l.BranchId equals b.Id into oficinas
                     from b in oficinas.DefaultIfEmpty()
                     join c in db.CreditLines.AsNoTracking() on l.CreditLineId equals c.Id into lineas
                     from c in lineas.DefaultIfEmpty()
                     where l.Id == loanId
                     select new { l.Number, Oficina = b == null ? (Guid?)null : b.PublicId, Linea = c == null ? (Guid?)null : c.PublicId })
                .FirstOrDefaultAsync(ct)
            : null;
        var canal = ErroresDeMensajesAlDeudor.NombreDelCanal(mensaje.Channel);
        var credito = alcance is null ? string.Empty : $" del crédito {alcance.Number}";
        var r = await alertas.LevantarAsync(new AlertaALevantar(
            TiposDeAlerta.MensajeAlDeudorFallido,
            $"Un mensaje por {canal}{credito} no se pudo enviar",
            $"El mensaje por {canal}{credito} falló en definitiva ({evento.ExternalOutcome?.ToString() ?? "sin respuesta"}). Revise el destino de la persona " +
            "y programe otro mensaje si hace falta.",
            "DebtorMessage", mensaje.PublicId,
            DedupKey: $"{TiposDeAlerta.MensajeAlDeudorFallido}:{mensaje.PublicId:N}",
            ModuloDeOrigen: "Lending",
            ScopeBranchPublicId: alcance?.Oficina,
            ScopeCreditLinePublicId: alcance?.Linea), ct);
        if (r.IsFailure)
            logger.LogWarning("[Cartera.Mensajes] No se levantó la alerta del mensaje {Mensaje}: {Codigo} {Detalle}", mensaje.PublicId, r.Error.Code,
                r.Error.Message);
    }

    private static string? Recortar(string? texto, int largo) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Length <= largo ? texto : texto[..largo];
}
