using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// Registra lo que respondió un destino por una unidad (feature 012, T498; FR-080; contracts/mensajes.md §11, §13;
/// decisiones-transversales T10, T11). <b>Sin ruta</b>: lo envía sólo el despachador, en un ámbito distinto del consumo
/// (<c>LosComandosDeConsumoNoTienenRuta</c>). Es el único que escribe el resultado en la entrega: el consumidor nunca toca
/// tablas de la plataforma.
///
/// <para>
/// Aplica el <see cref="ResultadoDeConsumo"/> a <b>todas</b> las entregas de la unidad (estado, intentos, espera, último error,
/// <c>ProcessedAt</c>, <c>ResultReference</c>, tipo y número del comprobante), agrega una fila de
/// <c>COR_IntegrationDeliveryAttempts</c> por entrega con el actor de <see cref="IActorActual"/> y la réplica, y levanta por
/// <see cref="IAlertas"/> <c>Integracion.MensajeSinEntregar</c> (al intento N o a los M minutos de <c>Integration:Retries</c>)
/// o <c>Integracion.MensajeRechazado</c>, con los destinatarios de <c>COR_AlertTypes</c>.
/// </para>
///
/// <para>
/// <b>Sin duplicar.</b> <see cref="IntentosLeidos"/> es el número de intentos que el despachador leyó: si la entrega ya tiene
/// más, otra réplica registró este intento y no se escribe nada. Un choque de <c>RowVersion</c> se reintenta entero
/// (<see cref="IReintentableAnteConcurrencia"/>): la relectura ve lo que escribió la otra réplica.
/// </para>
///
/// <para>
/// <b>Período cerrado en un envío posterior</b> (FR-078, T501): si la entrega rechazada con <c>Accounting.Period.Closed</c> era
/// de un lote <c>SendNotApplicable</c>, las demás entregas del lote de ese documento y de su clausura pasan también a
/// <c>Rejected</c> con el mismo código y motivo: ninguna queda <c>InBatch</c> huérfana y el resto del lote sigue.
/// </para>
/// </summary>
public sealed record RegisterDeliveryResultCommand(
    string Destination,
    IReadOnlyList<Guid> MessagePublicIds,
    ResultadoDeConsumo Resultado,
    int IntentosLeidos,
    DateTime StartedAt,
    DateTime FinishedAt,
    string Instance) : IRequest<Result<RegistroDeEntregaDto>>, IReintentableAnteConcurrencia;

/// <summary>Qué quedó: si se registró, el estado, el intento, la próxima espera y las alertas levantadas. (nuevo)</summary>
public sealed record RegistroDeEntregaDto(
    bool Registrado,
    DeliveryStatus Status,
    int Attempt,
    DateTime? NextAttemptAt,
    IReadOnlyList<string> Alerts,
    int DraggedToRejected);

public sealed class RegisterDeliveryResultCommandValidator : AbstractValidator<RegisterDeliveryResultCommand>
{
    public RegisterDeliveryResultCommandValidator()
    {
        RuleFor(x => x.Destination).NotEmpty().MaximumLength(20);
        RuleFor(x => x.MessagePublicIds).NotEmpty();
        RuleForEach(x => x.MessagePublicIds).NotEqual(Guid.Empty);
        RuleFor(x => x.Resultado).NotNull();
        RuleFor(x => x.IntentosLeidos).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Instance).NotEmpty().MaximumLength(100);
        RuleFor(x => x.FinishedAt).GreaterThanOrEqualTo(x => x.StartedAt).WithMessage("El intento no puede terminar antes de empezar.");
        RuleFor(x => x.Resultado)
            .Must(r => r is not ResultadoDeConsumo.Rejected rechazo || !string.IsNullOrWhiteSpace(rechazo.Code))
            .WithMessage("Un rechazo lleva su código.");
    }
}

public sealed class RegisterDeliveryResultCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IAlertas alertas,
    IOptions<ReintentosDeIntegracion> reintentos,
    ILogger<RegisterDeliveryResultCommandHandler> logger) : IRequestHandler<RegisterDeliveryResultCommand, Result<RegistroDeEntregaDto>>
{
    public async Task<Result<RegistroDeEntregaDto>> Handle(RegisterDeliveryResultCommand request, CancellationToken ct)
    {
        var ids = request.MessagePublicIds.Distinct().ToList();
        var entregas = await db.IntegrationMessageDeliveries
            .Include(d => d.Message)
            .Where(d => d.Destination == request.Destination && ids.Contains(d.Message!.PublicId))
            .OrderBy(d => d.MessageId)
            .ToListAsync(ct);
        if (entregas.Count != ids.Count)
        {
            var faltante = ids.First(id => entregas.All(e => e.Message!.PublicId != id));
            return Result.Failure<RegistroDeEntregaDto>(ErroresDeIntegracion.MensajeNoEncontrado(faltante));
        }

        // Otra réplica ya registró este intento (o la entrega dejó de estar por consumir): nada que escribir.
        var yaRegistrada = entregas.Any(e => e.Attempts > request.IntentosLeidos
                                             || e.Status is not (DeliveryStatus.Pending or DeliveryStatus.InBatch));
        if (yaRegistrada)
        {
            var primera = entregas[0];
            return Result.Success(new RegistroDeEntregaDto(false, primera.Status, primera.Attempts, primera.NextAttemptAt, [], 0));
        }

        var actor = await actorActual.ObtenerAsync(ct);
        var politica = reintentos.Value;
        var intento = request.IntentosLeidos + 1;
        var duracion = (int)Math.Clamp((request.FinishedAt - request.StartedAt).TotalMilliseconds, 0, int.MaxValue);
        var (salida, codigo, mensaje) = Desenlace(request.Resultado);

        DateTime? espera = null;
        if (request.Resultado is ResultadoDeConsumo.Retry)
            espera = request.FinishedAt + politica.Espera(intento, Random.Shared.NextDouble());

        foreach (var entrega in entregas)
        {
            Aplicar(entrega, request.Resultado, intento, request.FinishedAt, espera);
            db.IntegrationDeliveryAttempts.Add(new IntegrationDeliveryAttempt
            {
                DeliveryId = entrega.Id,
                MessageId = entrega.MessageId,
                AttemptNumber = intento,
                StartedAt = request.StartedAt,
                FinishedAt = request.FinishedAt,
                DurationMs = duracion,
                Outcome = salida,
                ErrorCode = Recortar(codigo, 80),
                ErrorMessage = Recortar(mensaje, 1000),
                ActorKind = actor.Kind,
                ActorUserId = actor.UserId,
                ActorName = Recortar(actor.Name, 150) ?? string.Empty,
                BatchId = entrega.BatchId,
                Instance = Recortar(request.Instance, 100)!,
            });
        }

        var arrastradas = request.Resultado is ResultadoDeConsumo.Rejected { Code: ErroresDeIntegracion.PeriodoContableCerrado } cerrado
            ? await ArrastrarPeriodoCerradoAsync(entregas, cerrado, ct)
            : 0;

        await db.SaveChangesAsync(ct);

        var levantadas = new List<string>();
        var principal = entregas[0].Message!;
        if (request.Resultado is ResultadoDeConsumo.Rejected rechazo)
        {
            if (await LevantarAsync(TiposDeAlerta.MensajeRechazado, principal,
                    $"Rechazado: {principal.Type} {principal.OriginNumber}",
                    $"El destino {request.Destination} rechazó {principal.Type} de {principal.OriginNumber} ({principal.OperationDate:yyyy-MM-dd}): {rechazo.Reason} [{rechazo.Code}]. Corrija la causa y reprocese desde la bandeja de mensajes.",
                    ct))
                levantadas.Add(TiposDeAlerta.MensajeRechazado);
        }
        else if (request.Resultado is ResultadoDeConsumo.Retry reintento && await DebeAlertarSinEntregarAsync(entregas, intento, request, politica, ct))
        {
            if (await LevantarAsync(TiposDeAlerta.MensajeSinEntregar, principal,
                    $"Sin entregar: {principal.Type} {principal.OriginNumber}",
                    $"{principal.Type} de {principal.OriginNumber} lleva {intento} intento(s) sin entregarse a {request.Destination}: {reintento.Reason}. Se sigue reintentando solo.",
                    ct))
                levantadas.Add(TiposDeAlerta.MensajeSinEntregar);
        }

        return Result.Success(new RegistroDeEntregaDto(true, entregas[0].Status, intento, espera, levantadas, arrastradas));
    }

    private static (DeliveryAttemptOutcome Salida, string? Codigo, string? Mensaje) Desenlace(ResultadoDeConsumo resultado) => resultado switch
    {
        ResultadoDeConsumo.Processed => (DeliveryAttemptOutcome.Processed, null, null),
        ResultadoDeConsumo.AlreadyProcessed => (DeliveryAttemptOutcome.AlreadyProcessed, null, null),
        ResultadoDeConsumo.Rejected r => (DeliveryAttemptOutcome.Rejected, r.Code, r.Reason),
        ResultadoDeConsumo.Retry r => (DeliveryAttemptOutcome.Retry, r.Code, r.Reason),
        _ => throw new ArgumentOutOfRangeException(nameof(resultado), resultado, "Resultado de consumo desconocido."),
    };

    private static void Aplicar(IntegrationMessageDelivery entrega, ResultadoDeConsumo resultado, int intento, DateTime ahora, DateTime? espera)
    {
        entrega.Attempts = intento;
        entrega.LastAttemptAt = ahora;
        switch (resultado)
        {
            case ResultadoDeConsumo.Processed p:
                Procesada(entrega, ahora, p.AccountingDocumentPublicId, p.VoucherTypeCode, p.VoucherNumber, p.SinComprobante);
                break;
            case ResultadoDeConsumo.AlreadyProcessed p:
                Procesada(entrega, ahora, p.AccountingDocumentPublicId, p.VoucherTypeCode, p.VoucherNumber, p.SinComprobante);
                break;
            case ResultadoDeConsumo.Rejected r:
                Rechazada(entrega, r.Code, r.Reason, r.DataJson);
                break;
            case ResultadoDeConsumo.Retry r:
                // Pending sigue Pending; InBatch sigue InBatch con el mismo lote, y el lote sigue en curso (data-model §19).
                entrega.NextAttemptAt = espera;
                entrega.LastErrorCode = Recortar(r.Code, 80);
                entrega.LastErrorMessage = Recortar(r.Reason, 1000);
                entrega.LastErrorDataJson = null;
                break;
        }
    }

    private static void Procesada(IntegrationMessageDelivery entrega, DateTime ahora, Guid? comprobante, string? tipo, string? numero, MotivoSinComprobante? sin)
    {
        entrega.Status = DeliveryStatus.Processed;
        entrega.ProcessedAt = ahora;
        entrega.NextAttemptAt = null;
        entrega.ResultReference = ReferenciasDeResultado.De(comprobante, sin);
        entrega.ResultVoucherTypeCode = comprobante is null ? null : Recortar(tipo, 10);
        entrega.ResultVoucherNumber = comprobante is null ? null : Recortar(numero, 30);
        entrega.LastErrorCode = null;
        entrega.LastErrorMessage = null;
        entrega.LastErrorDataJson = null;
    }

    private static void Rechazada(IntegrationMessageDelivery entrega, string codigo, string motivo, string? datos)
    {
        entrega.Status = DeliveryStatus.Rejected;
        entrega.NextAttemptAt = null;
        entrega.LastErrorCode = Recortar(codigo, 80);
        entrega.LastErrorMessage = Recortar(motivo, 1000);
        entrega.LastErrorDataJson = datos;
    }

    /// <summary>
    /// En un lote <c>SendNotApplicable</c>, el período cerrado rechaza el documento completo: las demás entregas <c>InBatch</c> del
    /// lote que están en la clausura de la unidad rechazada pasan a <c>Rejected</c> con el mismo código y motivo.
    /// </summary>
    private async Task<int> ArrastrarPeriodoCerradoAsync(List<IntegrationMessageDelivery> entregas, ResultadoDeConsumo.Rejected rechazo, CancellationToken ct)
    {
        var loteId = entregas.Select(e => e.BatchId).FirstOrDefault(b => b is not null);
        if (loteId is null) return 0;
        var disparador = await db.IntegrationBatches.AsNoTracking().Where(b => b.Id == loteId).Select(b => (BatchTrigger?)b.Trigger).FirstOrDefaultAsync(ct);
        if (disparador != BatchTrigger.SendNotApplicable) return 0;

        var destino = entregas[0].Destination;
        var delLote = await db.IntegrationMessageDeliveries
            .Where(d => d.BatchId == loteId && d.Destination == destino && d.Status == DeliveryStatus.InBatch)
            .ToListAsync(ct);
        var propias = entregas.Select(e => e.Id).ToHashSet();
        var candidatas = delLote.Where(d => !propias.Contains(d.Id)).ToDictionary(d => d.MessageId);
        if (candidatas.Count == 0) return 0;

        var clausura = await LotesDeIntegracion.ClausuraAsync(db, entregas.Select(e => e.MessageId),
            ids => Task.FromResult(ids.Where(candidatas.ContainsKey).ToHashSet()), ct);

        var arrastradas = 0;
        foreach (var id in clausura)
        {
            if (!candidatas.TryGetValue(id, out var entrega)) continue;
            Rechazada(entrega, rechazo.Code, rechazo.Reason, rechazo.DataJson);
            arrastradas++;
        }

        return arrastradas;
    }

    private async Task<bool> DebeAlertarSinEntregarAsync(
        List<IntegrationMessageDelivery> entregas, int intento, RegisterDeliveryResultCommand request, ReintentosDeIntegracion politica, CancellationToken ct)
    {
        if (intento >= politica.AlertAfterAttempts) return true;
        var entregaIds = entregas.Select(e => e.Id).ToList();
        var primero = await db.IntegrationDeliveryAttempts.AsNoTracking()
            .Where(a => entregaIds.Contains(a.DeliveryId))
            .Select(a => (DateTime?)a.StartedAt)
            .MinAsync(ct) ?? request.StartedAt;
        if (primero > request.StartedAt) primero = request.StartedAt;
        return request.FinishedAt - primero >= TimeSpan.FromMinutes(politica.AlertAfterMinutes);
    }

    private async Task<bool> LevantarAsync(string tipo, IntegrationMessage mensaje, string asunto, string cuerpo, CancellationToken ct)
    {
        var r = await alertas.LevantarAsync(new AlertaALevantar(
            tipo,
            Recortar(asunto, 200)!,
            Recortar(cuerpo, 2000)!,
            EntityType: "IntegrationMessage",
            EntityPublicId: mensaje.PublicId,
            DedupKey: $"{tipo}:{mensaje.PublicId:N}"), ct);
        if (r.IsFailure)
        {
            logger.LogWarning("[Integracion.AlertaNoLevantada] {Tipo} para el mensaje {Mensaje}: {Codigo} {Motivo}",
                tipo, mensaje.PublicId, r.Error.Code, r.Error.Message);
            return false;
        }

        return true;
    }

    private static string? Recortar(string? texto, int largo) =>
        texto is null ? null : texto.Length <= largo ? texto : texto[..largo];
}
