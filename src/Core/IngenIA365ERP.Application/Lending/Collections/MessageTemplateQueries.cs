using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Collections;

/// <summary>
/// Las plantillas de mensaje de Cartera vigentes a una fecha (feature 013, T0308; T76; contracts/api.md §14.1,
/// <c>GET /api/lending/message-templates?purpose=&amp;channel=&amp;date=</c>, <c>Lending.MessageTemplates.View</c>): una por código y canal, la
/// versión que rige ese día (sin fecha, hoy en Colombia), filtrando por propósito y canal. Sólo lee; no trae datos personales. (nuevo)
/// </summary>
public sealed record ListMessageTemplatesQuery(
    DebtorMessagePurpose? Purpose = null,
    MessageChannel? Channel = null,
    DateOnly? Date = null) : IRequest<Result<IReadOnlyList<MessageTemplateVersionDto>>>;

public sealed class ListMessageTemplatesQueryValidator : AbstractValidator<ListMessageTemplatesQuery>
{
    public ListMessageTemplatesQueryValidator()
    {
        RuleFor(x => x.Purpose).IsInEnum().When(x => x.Purpose is not null);
        RuleFor(x => x.Channel).IsInEnum().When(x => x.Channel is not null);
    }
}

public sealed class ListMessageTemplatesQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<ListMessageTemplatesQuery, Result<IReadOnlyList<MessageTemplateVersionDto>>>
{
    public async Task<Result<IReadOnlyList<MessageTemplateVersionDto>>> Handle(ListMessageTemplatesQuery request, CancellationToken ct)
    {
        var fecha = request.Date ?? reloj.HoyLocal;
        var consulta = db.NotificationTemplates.AsNoTracking().Where(t => t.Module == ColaDeMensajesAlDeudor.Modulo && !t.IsDeleted);
        if (request.Purpose is { } proposito)
        {
            var nombre = proposito.ToString();
            consulta = consulta.Where(t => t.Purpose == nombre);
        }
        if (request.Channel is { } canal)
        {
            var nombre = canal.ToString();
            consulta = consulta.Where(t => t.Channel == nombre);
        }

        var filas = await consulta.ToListAsync(ct);
        var vigentes = filas
            .Where(t => t.VigenteEn(fecha))
            .GroupBy(t => (t.Code, t.Channel))
            .Select(g => g.OrderByDescending(t => t.Version).First())
            .OrderBy(t => t.Code, StringComparer.Ordinal).ThenBy(t => t.Channel, StringComparer.Ordinal)
            .Select(ProyeccionDePlantillasDeMensaje.ADto)
            .Where(d => d is not null)
            .Select(d => d!)
            .ToList();
        return Result.Success<IReadOnlyList<MessageTemplateVersionDto>>(vigentes);
    }
}

/// <summary>
/// El historial de una plantilla (feature 013, T0308; api.md §14.1, <c>GET /api/lending/message-templates/{code}/history</c>,
/// <c>Lending.MessageTemplates.View</c>): todas sus versiones, de la más nueva a la más vieja, de todos los canales o del pedido. Un código sin
/// versiones es <c>Lending.MessageTemplate.NotFound</c>. (nuevo)
/// </summary>
public sealed record GetMessageTemplateHistoryQuery(string Code, MessageChannel? Channel = null)
    : IRequest<Result<IReadOnlyList<MessageTemplateVersionDto>>>;

public sealed class GetMessageTemplateHistoryQueryValidator : AbstractValidator<GetMessageTemplateHistoryQuery>
{
    public GetMessageTemplateHistoryQueryValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoCorto);
        RuleFor(x => x.Channel).IsInEnum().When(x => x.Channel is not null);
    }
}

public sealed class GetMessageTemplateHistoryQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetMessageTemplateHistoryQuery, Result<IReadOnlyList<MessageTemplateVersionDto>>>
{
    public async Task<Result<IReadOnlyList<MessageTemplateVersionDto>>> Handle(GetMessageTemplateHistoryQuery request, CancellationToken ct)
    {
        var code = CodigoDeCatalogo.Normalizar(request.Code) ?? request.Code.Trim().ToUpperInvariant();
        var consulta = db.NotificationTemplates.AsNoTracking()
            .Where(t => t.Module == ColaDeMensajesAlDeudor.Modulo && t.Code == code && !t.IsDeleted);
        if (request.Channel is { } canal)
        {
            var nombre = canal.ToString();
            consulta = consulta.Where(t => t.Channel == nombre);
        }

        var versiones = (await consulta.ToListAsync(ct))
            .OrderByDescending(t => t.Version).ThenBy(t => t.Channel, StringComparer.Ordinal)
            .Select(ProyeccionDePlantillasDeMensaje.ADto)
            .Where(d => d is not null)
            .Select(d => d!)
            .ToList();
        return versiones.Count == 0
            ? Result.Failure<IReadOnlyList<MessageTemplateVersionDto>>(ErroresDeMensajesAlDeudor.PlantillaInexistente(code, request.Channel))
            : Result.Success<IReadOnlyList<MessageTemplateVersionDto>>(versiones);
    }
}

/// <summary>Lo que muestra la vista previa: asunto, cuerpo, largo (y segmentos si es SMS) y avisos. (nuevo)</summary>
public sealed record MessageTemplatePreviewDto(string? Subject, string Body, int Length, int? Segments, IReadOnlyList<AvisoDePlantilla> Warnings);

/// <summary>
/// La vista previa de una plantilla (feature 013, T0308; api.md §14.1, <c>POST /api/lending/message-templates/preview</c>,
/// <c>Lending.MessageTemplates.View</c>): la vigente de un código o un texto en edición (con su propósito), renderizado con los datos de un
/// crédito de ejemplo o con valores de muestra. <b>Sin clave</b>: no escribe nada. Una variable fuera de la lista blanca responde
/// <c>Lending.MessageTemplate.VariableNotAllowed</c>; el SMS largo, el aviso <c>.SmsTooLong</c>. El crédito de ejemplo sólo cuenta si está en el
/// alcance de Cartera de quien pregunta (T0259); fuera de él se usan los valores de muestra, como si no se hubiera indicado. (nuevo)
/// </summary>
public sealed record RenderMessageTemplatePreviewQuery(
    MessageChannel Channel,
    string? Code = null,
    string? Body = null,
    DebtorMessagePurpose? Purpose = null,
    string? Subject = null,
    Guid? SampleLoanPublicId = null,
    string? ProviderTemplateId = null) : IRequest<Result<MessageTemplatePreviewDto>>;

public sealed class RenderMessageTemplatePreviewQueryValidator : AbstractValidator<RenderMessageTemplatePreviewQuery>
{
    public RenderMessageTemplatePreviewQueryValidator()
    {
        RuleFor(x => x.Channel).IsInEnum();
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Code) || !string.IsNullOrWhiteSpace(x.Body))
            .WithMessage("Indique la plantilla o el texto que quiere ver.");
        RuleFor(x => x.Purpose).NotNull().When(x => string.IsNullOrWhiteSpace(x.Code))
            .WithMessage("Indique el propósito del texto: de él depende qué variables puede usar.");
    }
}

public sealed class RenderMessageTemplatePreviewQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<RenderMessageTemplatePreviewQuery, Result<MessageTemplatePreviewDto>>
{
    public async Task<Result<MessageTemplatePreviewDto>> Handle(RenderMessageTemplatePreviewQuery request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var asunto = request.Subject;
        var cuerpo = request.Body;
        var proveedor = request.ProviderTemplateId;
        var proposito = request.Purpose;

        if (string.IsNullOrWhiteSpace(cuerpo))
        {
            var code = CodigoDeCatalogo.Normalizar(request.Code) ?? request.Code!.Trim().ToUpperInvariant();
            var canal = request.Channel.ToString();
            var plantilla = (await db.NotificationTemplates.AsNoTracking()
                    .Where(t => t.Module == ColaDeMensajesAlDeudor.Modulo && t.Code == code && t.Channel == canal && !t.IsDeleted)
                    .ToListAsync(ct))
                .Where(t => t.VigenteEn(hoy)).OrderByDescending(t => t.Version).FirstOrDefault();
            if (plantilla is null || !Enum.TryParse<DebtorMessagePurpose>(plantilla.Purpose, out var delaPlantilla))
                return Result.Failure<MessageTemplatePreviewDto>(ErroresDeMensajesAlDeudor.PlantillaInexistente(code, request.Channel));
            asunto ??= plantilla.Subject;
            cuerpo = plantilla.BodyTemplate;
            proveedor ??= plantilla.ProviderTemplateId;
            proposito ??= delaPlantilla;
        }

        var valores = RenderizadorDePlantillasDeMensaje.VariablesPermitidas(proposito!.Value)
            .ToDictionary(v => v, v => (string?)$"[{v}]", StringComparer.Ordinal);
        if (request.SampleLoanPublicId is { } loanPublicId)
        {
            var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
            var credito = await (from l in db.Loans.AsNoTracking().PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId)
                                 join p in db.People.AsNoTracking() on l.DebtorPersonId equals p.Id
                                 where l.PublicId == loanPublicId
                                 select new { l.Number, Persona = p }).FirstOrDefaultAsync(ct);
            if (credito is not null)
            {
                valores["NumeroDelCredito"] = credito.Number;
                valores["NombreDelTitular"] = ReferenciasDeGobierno.Nombre(credito.Persona);
            }
        }
        valores["FechaDeCorte"] = hoy.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);

        var r = RenderizadorDePlantillasDeMensaje.Renderizar(proposito.Value, request.Channel, asunto, cuerpo!, proveedor, valores);
        if (r.IsFailure) return Result.Failure<MessageTemplatePreviewDto>(r.Error);
        return Result.Success(new MessageTemplatePreviewDto(r.Value.Asunto, r.Value.Cuerpo, r.Value.Sms?.Largo ?? r.Value.Cuerpo.Length,
            r.Value.Sms?.Segmentos, r.Value.Avisos));
    }
}

/// <summary>De la fila de <c>COR_NotificationTemplates</c> al contrato (api.md §14.1). (nuevo)</summary>
internal static class ProyeccionDePlantillasDeMensaje
{
    public static MessageTemplateVersionDto? ADto(NotificationTemplate t)
    {
        if (!Enum.TryParse<DebtorMessagePurpose>(t.Purpose, out var proposito) || !Enum.TryParse<MessageChannel>(t.Channel, out var canal)) return null;
        IReadOnlyList<string> variables;
        try
        {
            variables = JsonSerializer.Deserialize<List<string>>(t.Variables) ?? [];
        }
        catch (JsonException)
        {
            // Una lista ilegible no impide mostrar la plantilla: se muestra la lista blanca del propósito.
            variables = RenderizadorDePlantillasDeMensaje.VariablesPermitidas(proposito);
        }
        var avisos = RenderizadorDePlantillasDeMensaje.Revisar(proposito, canal, t.Subject, t.BodyTemplate, t.ProviderTemplateId).Avisos;
        return new MessageTemplateVersionDto(t.PublicId, t.Code, proposito, canal, t.Version, t.ValidFrom, t.ValidTo, t.Subject, t.BodyTemplate,
            variables, t.ProviderTemplateId, t.IsSeeded, t.ReviewPending, avisos);
    }
}
