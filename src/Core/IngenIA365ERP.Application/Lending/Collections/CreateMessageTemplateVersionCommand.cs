using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Collections;

/// <summary>Una versión de plantilla de mensaje (contracts/api.md §14.1), con los avisos de la revisión. (nuevo)</summary>
public sealed record MessageTemplateVersionDto(
    Guid PublicId,
    string Code,
    DebtorMessagePurpose Purpose,
    MessageChannel Channel,
    int Version,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string? Subject,
    string Body,
    IReadOnlyList<string> Variables,
    string? ProviderTemplateId,
    bool IsSeeded,
    bool ReviewPending,
    IReadOnlyList<AvisoDePlantilla> Warnings);

/// <summary>Los errores de las plantillas de mensaje que no son de la revisión del texto. (nuevo)</summary>
public static class ErroresDePlantillasDeMensaje
{
    public const string CodigoSeCruza = "Lending.MessageTemplate.Overlaps";
    public const string CodigoOtroProposito = "Lending.MessageTemplate.PurposeMismatch";

    public static Error SeCruza(string code, MessageChannel channel, DateOnly validFrom, DateOnly vigenteDesde) =>
        new ErrorConDatos(CodigoSeCruza,
            string.Create(CultureInfo.InvariantCulture,
                $"La plantilla {code} de {channel} ya tiene una versión desde el {vigenteDesde:yyyy-MM-dd}: la nueva tiene que empezar después."),
            new { code, channel, validFrom, currentFrom = vigenteDesde });

    public static Error OtroProposito(string code, MessageChannel channel, string propositoActual) =>
        new ErrorConDatos(CodigoOtroProposito,
            $"La plantilla {code} de {channel} es de propósito {propositoActual}: una versión nueva no lo cambia. Cree otra plantilla.",
            new { code, channel, purpose = propositoActual });
}

/// <summary>
/// Crea la versión siguiente de una plantilla de mensaje de Cartera (feature 013, T0084; T76; contracts/adaptadores-y-archivos.md §4.7;
/// contracts/api.md §14.1, <c>POST /api/lending/message-templates/{code}/versions</c>, que expone T0309). Una plantilla es
/// (<c>Code</c>, <c>Channel</c>, <c>Version</c>): la nueva lleva <c>Version + 1</c> y la vigente se cierra la víspera de
/// <see cref="ValidFrom"/>; dos versiones del mismo código y canal no se cruzan (<c>Lending.MessageTemplate.Overlaps</c>). El texto pasa por
/// <see cref="RenderizadorDePlantillasDeMensaje.Revisar"/>: una variable fuera de la lista blanca del propósito no se guarda
/// (<c>.VariableNotAllowed</c>); el SMS largo y el WhatsApp sin plantilla del proveedor son avisos. Lo que ya salió nunca cambia: cada
/// mensaje sella <c>PublicId</c> y versión. (nuevo)
/// </summary>
public sealed record CreateMessageTemplateVersionCommand(
    string Code,
    DebtorMessagePurpose Purpose,
    MessageChannel Channel,
    string? Subject,
    string Body,
    IReadOnlyList<string>? Variables,
    string? ProviderTemplateId,
    DateOnly ValidFrom,
    string Reason) : IRequest<Result<MessageTemplateVersionDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateMessageTemplateVersionCommandValidator : ValidadorConMotivo<CreateMessageTemplateVersionCommand>
{
    public CreateMessageTemplateVersionCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoCorto).Matches(CodigoDeCatalogo.Patron)
            .WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Purpose).IsInEnum();
        RuleFor(x => x.Channel).IsInEnum();
        RuleFor(x => x.Subject).MaximumLength(500);
        RuleFor(x => x.Subject).NotEmpty().When(x => x.Channel == MessageChannel.Email).WithMessage("Un correo lleva asunto.");
        RuleFor(x => x.Body).NotEmpty().WithMessage("Escriba el texto del mensaje.");
        RuleFor(x => x.ProviderTemplateId).MaximumLength(100);
        RuleFor(x => x.ValidFrom).NotEqual(default(DateOnly)).WithMessage("Indique desde cuándo rige.");
    }
}

public sealed class CreateMessageTemplateVersionCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<CreateMessageTemplateVersionCommand, Result<MessageTemplateVersionDto>>
{
    /// <summary>El módulo de las plantillas de Cartera.</summary>
    public const string Modulo = "Lending";

    public async Task<Result<MessageTemplateVersionDto>> Handle(CreateMessageTemplateVersionCommand request, CancellationToken ct)
    {
        var code = CodigoDeCatalogo.Normalizar(request.Code)!;
        var canal = request.Channel.ToString();

        var revision = RenderizadorDePlantillasDeMensaje.Revisar(request.Purpose, request.Channel, request.Subject, request.Body,
            request.ProviderTemplateId);
        if (!revision.EsValida) return Result.Failure<MessageTemplateVersionDto>(revision.Errores[0]);

        // Las variables que declara la pantalla también tienen que estar en la lista blanca (api.md §14.1, variables[]).
        var permitidas = RenderizadorDePlantillasDeMensaje.VariablesPermitidas(request.Purpose);
        var ajena = request.Variables?.Select(v => v.Trim()).FirstOrDefault(v => !permitidas.Contains(v, StringComparer.Ordinal));
        if (ajena is not null)
            return Result.Failure<MessageTemplateVersionDto>(new ErrorConDatos(RenderizadorDePlantillasDeMensaje.CodigoVariableNoPermitida,
                $"«{ajena}» no es una variable que pueda usar un mensaje de este propósito. Use una de: {string.Join(", ", permitidas)}.",
                new { variable = ajena, allowed = permitidas }));

        var versiones = await db.NotificationTemplates
            .Where(t => t.Code == code && t.Channel == canal && t.Module == Modulo && !t.IsDeleted)
            .ToListAsync(ct);
        if (versiones.FirstOrDefault(v => v.Purpose != request.Purpose.ToString()) is { } otra)
            return Result.Failure<MessageTemplateVersionDto>(ErroresDePlantillasDeMensaje.OtroProposito(code, request.Channel, otra.Purpose));
        if (versiones.FirstOrDefault(v => v.ValidFrom >= request.ValidFrom || (v.ValidTo is { } hasta && hasta >= request.ValidFrom)) is { } cruzada)
            return Result.Failure<MessageTemplateVersionDto>(
                ErroresDePlantillasDeMensaje.SeCruza(code, request.Channel, request.ValidFrom, cruzada.ValidFrom));

        var ahora = reloj.UtcNow;
        var actor = await actorActual.ObtenerAsync(ct);
        var quien = actor.Name.Length <= 100 ? actor.Name : actor.Name[..100];
        foreach (var abierta in versiones.Where(v => v.ValidTo is null))
        {
            abierta.ValidTo = request.ValidFrom.AddDays(-1);
            abierta.UpdatedAt = ahora;
            abierta.UpdatedBy = quien;
        }

        var version = versiones.Count == 0 ? 1 : versiones.Max(v => v.Version) + 1;
        var plantilla = new NotificationTemplate
        {
            TemplateName = string.Create(CultureInfo.InvariantCulture, $"{code} v{version}"),
            Channel = canal,
            Subject = string.IsNullOrWhiteSpace(request.Subject) ? null : request.Subject.Trim(),
            BodyTemplate = request.Body,
            IsActive = true,
            Code = code,
            Module = Modulo,
            Purpose = request.Purpose.ToString(),
            Version = version,
            ValidFrom = request.ValidFrom,
            ProviderTemplateId = string.IsNullOrWhiteSpace(request.ProviderTemplateId) ? null : request.ProviderTemplateId.Trim(),
            Variables = RenderizadorDePlantillasDeMensaje.VariablesComoJson(request.Purpose),
            IsSeeded = false,
            ReviewPending = false,
            CreatedAt = ahora,
            CreatedBy = quien,
        };
        db.NotificationTemplates.Add(plantilla);
        await db.SaveChangesAsync(ct);

        return Result.Success(new MessageTemplateVersionDto(plantilla.PublicId, code, request.Purpose, request.Channel, version, plantilla.ValidFrom,
            null, plantilla.Subject, plantilla.BodyTemplate, permitidas, plantilla.ProviderTemplateId, false, false, revision.Avisos));
    }
}
