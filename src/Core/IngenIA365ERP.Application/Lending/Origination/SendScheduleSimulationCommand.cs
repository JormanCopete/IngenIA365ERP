using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Origination;

/// <summary>Lo que respondió el envío: el mensaje que quedó en la cola y su estado (api.md §4.1, 201). (nuevo)</summary>
public sealed record SentScheduleSimulationDto(Guid DebtorMessagePublicId, string Status);

/// <summary>
/// Envía la simulación a la persona simulada (api.md §4.1, <c>POST /api/lending/simulations/schedule/send</c>, <c>Lending.Simulations.Run</c>,
/// con clave; T74, T80, C87): un mensaje al deudor <c>Informative</c>, sólo por correo, con el PDF de la simulación como adjunto. Exige que la
/// persona exista, tenga correo registrado (<c>Lending.DebtorMessage.DestinationMissing</c>) y autorización de tratamiento general
/// (<c>Lending.Authorization.Missing</c>). El mensaje nace por <see cref="ColaDeMensajesAlDeudor"/> como envío inmediato: fuera de la ventana
/// legal se rechaza con <c>Lending.DebtorMessage.OutsideLegalWindow</c> y <c>nextAllowedAt</c>; sin la autorización del canal correo,
/// <c>Lending.DebtorMessage.ChannelNotAuthorized</c>; con Cartera sin activar, <c>Lending.NotActivated</c> (la cola no encola antes de la
/// activación). No registra nada del crédito: ni solicitud, ni cupo, ni número. (nuevo)
/// </summary>
public sealed record SendScheduleSimulationCommand(ScheduleSimulationInput Simulation, Guid RecipientPersonPublicId)
    : IRequest<Result<SentScheduleSimulationDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SendScheduleSimulationCommandValidator : AbstractValidator<SendScheduleSimulationCommand>
{
    public SendScheduleSimulationCommandValidator()
    {
        RuleFor(x => x.Simulation).NotNull().SetValidator(new ScheduleSimulationInputValidator());
        RuleFor(x => x.RecipientPersonPublicId).NotEmpty().WithMessage("Elija a quién se le envía la simulación.");
    }
}

public sealed class SendScheduleSimulationCommandHandler(
    IApplicationDbContext db,
    SimulacionEnAlcance simulacion,
    IRenderizadorDeSimulacion renderizador,
    ColaDeMensajesAlDeudor cola,
    IAutorizacionDeDatos autorizaciones,
    ISender sender,
    IActorActual actorActual,
    IDateTimeService reloj,
    ICurrentTenantService? cooperativa = null,
    IAmbienteDeDespliegue? ambiente = null) : IRequestHandler<SendScheduleSimulationCommand, Result<SentScheduleSimulationDto>>
{
    /// <summary>La plantilla del correo que lleva la simulación (propósito <c>Informative</c>, canal correo).</summary>
    public const string Plantilla = "SIMULACION";

    public async Task<Result<SentScheduleSimulationDto>> Handle(SendScheduleSimulationCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<SentScheduleSimulationDto>(ErroresDeGobierno.SinUsuario());
        if (!await cola.CarteraActivadaAsync(ct)) return Result.Failure<SentScheduleSimulationDto>(ErroresDeMensajesAlDeudor.CarteraSinActivar());

        var persona = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == request.RecipientPersonPublicId && !p.IsDeleted, ct);
        if (persona is null) return Result.Failure<SentScheduleSimulationDto>(ErroresDeGobierno.PersonaInexistente());
        var nombre = ReferenciasDeGobierno.Nombre(persona);

        // La simulación es de la persona a quien se envía: si el cuerpo trae otra, manda la destinataria (decide la banda y el nombre del PDF).
        var entrada = request.Simulation with { PersonPublicId = persona.PublicId };
        var simulada = await simulacion.SimularAsync(entrada, reloj.HoyLocal, ct);
        if (simulada.IsFailure) return Result.Failure<SentScheduleSimulationDto>(simulada.Error);

        if (ColaDeMensajesAlDeudor.DestinoDe(persona, MessageChannel.Email) is null)
            return Result.Failure<SentScheduleSimulationDto>(ErroresDeOriginacion.SinCorreo(persona.PublicId, nombre));
        var tratamiento = await autorizaciones.VigenteAsync(persona.PublicId, DataAuthorizationPurpose.GeneralTreatment, null, ct);
        if (tratamiento is not { Autoriza: true })
            return Result.Failure<SentScheduleSimulationDto>(
                ErroresDeOriginacion.SinAutorizacion(persona.PublicId, nombre, DataAuthorizationPurpose.GeneralTreatment));

        var dto = SimulacionEnAlcance.Dto(simulada.Value.Calculo, simulada.Value.Linea);

        // Primero la ventana y la autorización del canal (sin subir nada): un envío que no puede salir no deja un PDF huérfano.
        var ventana = await cola.EvaluarAsync(new ConsultaDeVentana(persona.Id, null, MessageChannel.Email, DebtorMessagePurpose.Informative,
            reloj.UtcNow, Inmediato: true) { PersonaSimulada = true }, ct);
        if (ventana.IsFailure) return Result.Failure<SentScheduleSimulationDto>(ventana.Error);
        if (ColaDeMensajesAlDeudor.RechazoDelInmediato(ventana.Value, MessageChannel.Email) is { } rechazo)
            return Result.Failure<SentScheduleSimulationDto>(rechazo);

        var pdf = renderizador.Render(SimulacionesEnPdf.Armar(dto, persona, cooperativa, ambiente, reloj.HoyLocal));
        var subida = await sender.Send(new UploadAttachmentCommand(AdjuntosDeModulo.SimulacionEnviada, persona.PublicId,
            SimulacionesEnPdf.NombreDe(dto), "application/pdf", pdf), ct);
        if (subida.IsFailure) return Result.Failure<SentScheduleSimulationDto>(subida.Error);

        var encolado = await cola.EncolarAsync(new PedidoDeMensajeAlDeudor(persona.Id, null, DebtorMessagePurpose.Informative, MessageChannel.Email,
            Plantilla, ColaDeMensajesAlDeudor.OrigenSimulacion, $"{dto.SimulationSha256}:{persona.PublicId:N}", ActorKind.Person, usuario)
        {
            Inmediato = true,
            AttachmentPublicId = subida.Value,
        }, ct);
        if (encolado.IsFailure) return Result.Failure<SentScheduleSimulationDto>(encolado.Error);

        await db.SaveChangesAsync(ct);
        return Result.Success(new SentScheduleSimulationDto(encolado.Value.Mensaje.PublicId, encolado.Value.Evento.Kind.ToString()));
    }
}
