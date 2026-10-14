using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Documents;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Collections;
using IngenIA365ERP.Domain.Lending.Documents;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Documents;

/// <summary>Lo que respondió el envío de un documento: el mensaje al deudor que quedó en la cola (api.md §15, 201). (nuevo)</summary>
public sealed record SentLendingDocumentDto(Guid DebtorMessagePublicId);

/// <summary>
/// La entrega de un documento emitido por correo (feature 013, US13, T0784, T0786; adaptadores-y-archivos.md §8.7; T74, I3): la comparten la emisión
/// con <c>Email</c> y el envío posterior.
/// <list type="bullet">
/// <item><see cref="PrevalidarAsync"/>, <b>antes</b> de pintar o subir nada: correo registrado y autorización de tratamiento general del destinatario
/// (la entrega pedida por el titular está exenta del horario, del tope y del canal semanal —I3— y no pide la autorización del canal). Sin correo o sin
/// la autorización, <c>Lending.DebtorMessage.ChannelNotAuthorized</c>.</item>
/// <item><see cref="EncolarAsync"/>: el <c>DebtorMessage</c> de propósito <c>DocumentDelivery</c> por <see cref="ColaDeMensajesAlDeudor"/> (único
/// escritor; no guarda), plantilla <c>ENVIODOC</c>, origen <c>Document</c>, con el PDF guardado como adjunto y el documento citado.</item>
/// <item><see cref="RegistrarEntregaSiCorresponde"/>: el gancho del despachador <c>cartera.mensajes</c>; cuando el mensaje de un documento queda
/// <c>Sent</c>, el documento recibe el evento <c>Delivered</c> con su mensaje, en la misma unidad de trabajo.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class EntregaDeDocumentosDeCartera(ColaDeMensajesAlDeudor cola, IDateTimeService reloj)
{
    /// <summary>La plantilla del correo que lleva un documento (propósito <c>DocumentDelivery</c>, canal correo).</summary>
    public const string Plantilla = "ENVIODOC";

    /// <summary>El origen del mensaje (<c>DebtorMessage.Origenes</c>).</summary>
    public const string Origen = "Document";

    /// <summary>¿Puede salir el correo ahora? Nulo si sí; si no, el error (sin escribir nada).</summary>
    public async Task<Error?> PrevalidarAsync(Person destinatario, int? loanId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(destinatario);
        var nombre = ReferenciasDeGobierno.Nombre(destinatario);
        if (ColaDeMensajesAlDeudor.DestinoDe(destinatario, MessageChannel.Email) is null)
            return new ErrorConDatos(ErroresDeMensajesAlDeudor.CodigoCanalNoAutorizado,
                $"{nombre} no tiene correo registrado: regístrelo en Maestros › Personas para enviarle el documento.",
                new { channel = nameof(MessageChannel.Email), reason = nameof(ContactWindowReason.NoDestination) });
        var ventana = await cola.EvaluarAsync(new ConsultaDeVentana(destinatario.Id, loanId, MessageChannel.Email, DebtorMessagePurpose.DocumentDelivery,
            reloj.UtcNow, Inmediato: true), ct);
        if (ventana.IsFailure) return ventana.Error;
        if (!ventana.Value.Obligado) return ErroresDeMensajesAlDeudor.DestinatarioNoObligado();
        return ventana.Value.Decision switch
        {
            Excluido { Motivo: ContactWindowReason.ChannelNotAuthorized } => new ErrorConDatos(ErroresDeMensajesAlDeudor.CodigoCanalNoAutorizado,
                $"{nombre} no autorizó el tratamiento general de sus datos: sin esa autorización no se le envían documentos por correo.",
                new { channel = nameof(MessageChannel.Email), reason = nameof(ContactWindowReason.ChannelNotAuthorized) }),
            Excluido x => new ErrorConDatos(ErroresDeMensajesAlDeudor.CodigoCanalNoAutorizado,
                $"No se le puede enviar el documento a {nombre} por correo en este momento.",
                new { channel = nameof(MessageChannel.Email), reason = x.Motivo.ToString() }),
            _ => ColaDeMensajesAlDeudor.RechazoDelInmediato(ventana.Value, MessageChannel.Email),
        };
    }

    /// <summary>El mensaje con el PDF del documento, en la unidad de trabajo de quien lo pide (no guarda).</summary>
    public Task<Result<MensajeEncolado>> EncolarAsync(IssuedLendingDocument documento, Person destinatario, int? loanId, int usuario, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(documento);
        ArgumentNullException.ThrowIfNull(destinatario);
        return cola.EncolarAsync(new PedidoDeMensajeAlDeudor(destinatario.Id, loanId, DebtorMessagePurpose.DocumentDelivery, MessageChannel.Email, Plantilla,
            Origen, $"{documento.PublicId:N}:{destinatario.PublicId:N}", ActorKind.Person, usuario)
        {
            Inmediato = true,
            OriginPublicId = documento.PublicId,
            AttachmentPublicId = documento.PdfAttachmentPublicId,
            IssuedDocumentId = documento.Id,
            Valores = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["ClaseDeDocumento"] = ModelosDeDocumentoDeCartera.Titulo(documento.Kind),
                ["NumeroDelDocumento"] = documento.Number,
                ["CodigoDeVerificacion"] = CodigoDeVerificacion.Formatear(documento.VerificationCode),
            },
        }, ct);
    }

    /// <summary>
    /// El gancho del despachador (T0786): si <paramref name="mensaje"/> lleva un documento emitido y <paramref name="evento"/> lo dejó
    /// <c>Sent</c>, agrega al documento el evento <c>Delivered</c> con el mensaje (sin guardar: lo guarda el registro del intento). Una entrega ya
    /// registrada para ese mensaje no se repite.
    /// </summary>
    public static async Task RegistrarEntregaSiCorresponde(IApplicationDbContext db, DebtorMessage mensaje, DebtorMessageEvent evento, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(mensaje);
        ArgumentNullException.ThrowIfNull(evento);
        if (evento.Kind != DebtorMessageEventKind.Sent || mensaje.IssuedDocumentId is not { } documento || mensaje.Id <= 0) return;
        if (!string.Equals(mensaje.OriginKind, Origen, StringComparison.Ordinal)) return;
        var yaEntregado = db.IssuedDocumentEvents.Local.Any(e => e.IssuedDocumentId == documento && e.DebtorMessageId == mensaje.Id)
                          || await db.IssuedDocumentEvents.AsNoTracking().AnyAsync(e => e.IssuedDocumentId == documento && e.DebtorMessageId == mensaje.Id
                              && e.Kind == IssuedDocumentEventKind.Delivered, ct);
        if (yaEntregado) return;
        db.IssuedDocumentEvents.Add(IssuedDocumentEvent.Entregado(documento, mensaje.Id, evento.OccurredAt, ActorKind.Process, null));
    }
}

/// <summary>
/// <c>POST /api/lending/documents/{id}/send</c> (feature 013, US13, T0786; api.md §15, <c>Lending.Documents.Issue</c>, con clave): envía por correo un
/// documento ya emitido, al titular o a <see cref="RecipientPersonPublicId"/> —que en un documento de un crédito tiene que estar obligado en él, y en
/// el paz y salvo general sólo puede ser el titular—. Lleva el mismo PDF guardado. Un documento anulado no se envía
/// (<c>Lending.Document.VoidedCannotReissue</c>): se emite otro. Cuando el mensaje queda <c>Sent</c> el despachador agrega el evento <c>Delivered</c>.
/// (nuevo)
/// </summary>
public sealed record SendLendingDocumentCommand(Guid DocumentPublicId, Guid? RecipientPersonPublicId = null)
    : IRequest<Result<SentLendingDocumentDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SendLendingDocumentCommandValidator : AbstractValidator<SendLendingDocumentCommand>
{
    public SendLendingDocumentCommandValidator() => RuleFor(x => x.DocumentPublicId).NotEmpty();
}

public sealed class SendLendingDocumentCommandHandler(
    IApplicationDbContext db,
    EntregaDeDocumentosDeCartera entrega,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual) : IRequestHandler<SendLendingDocumentCommand, Result<SentLendingDocumentDto>>
{
    public async Task<Result<SentLendingDocumentDto>> Handle(SendLendingDocumentCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var leido = await DocumentosEmitidos.LeerAsync(db, alcance, auditoria, request.DocumentPublicId, ct);
        if (leido.IsFailure) return Falla(leido.Error);
        var documento = leido.Value;
        if (await DocumentosEmitidos.AnulacionAsync(db, documento.Id, ct) is not null) return Falla(DocumentErrors.AnuladoNoSeReemite(documento.Number));

        var creditos = await db.IssuedDocumentLoans.AsNoTracking().Where(x => x.IssuedDocumentId == documento.Id).Select(x => x.LoanId).ToListAsync(ct);
        int? loanId = documento.Kind == IssuedDocumentKind.GeneralClearance || creditos.Count != 1 ? null : creditos[0];
        var destinatario = request.RecipientPersonPublicId is { } otro
            ? await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == otro && !p.IsDeleted, ct)
            : await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.Id == documento.PersonId, ct);
        if (destinatario is null) return Falla(ErroresDeGobierno.PersonaInexistente());
        if (loanId is null && destinatario.Id != documento.PersonId) return Falla(ErroresDeMensajesAlDeudor.DestinatarioNoObligado());

        if (await entrega.PrevalidarAsync(destinatario, loanId, ct) is { } rechazo) return Falla(rechazo);
        var encolado = await entrega.EncolarAsync(documento, destinatario, loanId, usuario, ct);
        if (encolado.IsFailure) return Falla(encolado.Error);
        await db.SaveChangesAsync(ct);
        return Result.Success(new SentLendingDocumentDto(encolado.Value.Mensaje.PublicId));
    }

    private static Result<SentLendingDocumentDto> Falla(Error e) => Result.Failure<SentLendingDocumentDto>(e);
}
