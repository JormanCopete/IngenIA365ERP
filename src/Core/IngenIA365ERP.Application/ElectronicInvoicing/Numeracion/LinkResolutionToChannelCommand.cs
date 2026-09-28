using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.Application.ElectronicInvoicing.Settings;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Numeracion;

/// <summary>
/// Asociar el prefijo de una resolución a un canal o software desde una fecha (feature 012, I4, T709; <c>POST
/// /api/electronic-invoicing/resolutions/{id}/channels</c>, api.md §24.2; FR-064, FR-065): es la condición para numerar por ese canal y
/// para cambiar de canal. Una sola asociación vigente por resolución: la anterior se cierra la víspera; una que empiece ese día o después
/// se cruza (<c>ElectronicInvoicing.Resolution.Overlaps</c>). La clave técnica es sólo de factura
/// (<c>ElectronicInvoicing.Resolution.TechnicalKeyNotAllowed</c>), queda fuera del diff y se devuelve enmascarada (<c>••••ab12</c>).
/// <see cref="FetchTechnicalKeyFromChannel"/> usa la consulta de rangos del canal cuando la ofrece: verifica la resolución y toma la clave
/// que el canal <b>propone</b>; nunca crea resoluciones. (nuevo)
/// </summary>
public sealed record LinkResolutionToChannelCommand(
    Guid ResolutionPublicId,
    string ChannelCode,
    string? SoftwareId,
    DateOnly ValidFrom,
    string? TechnicalKey,
    bool FetchTechnicalKeyFromChannel,
    string Reason)
    : IRequest<Result<DianNumberingResolutionDto>>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class LinkResolutionToChannelCommandValidator : ValidadorConMotivo<LinkResolutionToChannelCommand>
{
    public LinkResolutionToChannelCommandValidator()
    {
        RuleFor(x => x.ResolutionPublicId).NotEmpty();
        RuleFor(x => x.ChannelCode).NotEmpty().WithMessage("Indique el canal.").MaximumLength(40);
        RuleFor(x => x.SoftwareId).MaximumLength(36);
        RuleFor(x => x.TechnicalKey).MaximumLength(100);
    }
}

/// <summary>
/// La convención con que un adaptador devuelve la clave técnica en <see cref="ICanalDeEmisionElectronica.ConsultarRangosAsync"/>, sin
/// cambiar el resultado uniforme del puerto: un <see cref="MensajeDelCanal"/> de notificación con regla <see cref="Regla"/> y texto
/// <c>{prefijo}|{número de resolución}|{clave técnica}</c>, uno por rango. (nuevo)
/// </summary>
public static class RangosDelCanal
{
    public const string Regla = "NumberingRange.TechnicalKey";

    /// <summary>La clave técnica que el canal propone para la resolución de <paramref name="prefijo"/> y <paramref name="numero"/>; nula si no la trae.</summary>
    public static string? ClaveTecnica(ResultadoDeCanal resultado, string prefijo, string numero) =>
        resultado.Mensajes
            .Where(m => m.Regla == Regla)
            .Select(m => m.Texto.Split('|'))
            .Where(p => p.Length == 3 && string.Equals(p[0].Trim(), prefijo, StringComparison.OrdinalIgnoreCase) && p[1].Trim() == numero)
            .Select(p => p[2].Trim())
            .FirstOrDefault(c => c.Length > 0);

    /// <summary>El mensaje que arma un adaptador para un rango.</summary>
    public static MensajeDelCanal Mensaje(string prefijo, string numero, string clave) =>
        new(Regla, TipoDeMensajeDelCanal.Notificacion, $"{prefijo}|{numero}|{clave}");
}

public sealed class LinkResolutionToChannelCommandHandler(
    IApplicationDbContext db,
    ICanalesDeEmision canales,
    ICredencialesDeCanal credenciales,
    ICurrentTenantService tenant,
    IDateTimeService reloj)
    : IRequestHandler<LinkResolutionToChannelCommand, Result<DianNumberingResolutionDto>>
{
    public async Task<Result<DianNumberingResolutionDto>> Handle(LinkResolutionToChannelCommand request, CancellationToken ct)
    {
        var resolucion = await db.DianNumberingResolutions.Include(r => r.Channels).FirstOrDefaultAsync(r => r.PublicId == request.ResolutionPublicId, ct);
        if (resolucion is null) return Falla(ErroresDeNumeracionYConfiguracion.ResolutionNotFound());

        var canal = ReglasDeResolucion.Canal(request.ChannelCode);
        if (!canales.Codigos.Contains(canal, StringComparer.OrdinalIgnoreCase)) return Falla(ErroresDeNumeracionYConfiguracion.ChannelUnknown(canal));

        var clave = string.IsNullOrWhiteSpace(request.TechnicalKey) ? null : request.TechnicalKey.Trim();
        if ((clave is not null || request.FetchTechnicalKeyFromChannel) && resolucion.Kind != ResolutionKind.Invoice)
            return Falla(ErroresDeNumeracionYConfiguracion.TechnicalKeyNotAllowed());

        var vivas = resolucion.Channels.Where(c => !c.IsDeleted).ToList();
        if (vivas.Any(c => c.ValidFrom >= request.ValidFrom))
            return Falla(ErroresDeNumeracionYConfiguracion.ResolutionOverlaps(resolucion.ResolutionNumber));

        var software = string.IsNullOrWhiteSpace(request.SoftwareId) ? null : request.SoftwareId.Trim();
        if (clave is null && request.FetchTechnicalKeyFromChannel)
        {
            var propuesta = await ClaveDelCanalAsync(resolucion, canal, software, ct);
            if (propuesta.IsFailure) return Falla(propuesta.Error);
            clave = propuesta.Value;
        }

        foreach (var abierta in vivas.Where(c => c.ValidTo is null || c.ValidTo >= request.ValidFrom))
            abierta.ValidTo = request.ValidFrom.AddDays(-1);
        resolucion.Channels.Add(new DianResolutionChannel
        {
            Resolution = resolucion,
            ResolutionId = resolucion.Id,
            ChannelCode = canal,
            SoftwareId = software,
            ValidFrom = request.ValidFrom,
            TechnicalKey = clave,
        });

        await db.SaveChangesAsync(ct);
        return Result.Success(DianNumberingResolutionDto.De(resolucion, reloj.HoyLocal));
    }

    /// <summary>Pregunta los rangos al canal con la configuración vigente y toma la clave que propone para esta resolución.</summary>
    private async Task<Result<string>> ClaveDelCanalAsync(DianNumberingResolution resolucion, string canal, string? software, CancellationToken ct)
    {
        var adaptador = canales.Resolver(canal);
        if (!adaptador.Capacidades.ConsultaRangos) return Result.Failure<string>(ErroresDeNumeracionYConfiguracion.TechnicalKeyUnavailable(canal));

        var cooperativa = ConfiguracionDeEmision.Cooperativa(tenant);
        if (cooperativa.IsFailure) return Result.Failure<string>(cooperativa.Error);
        var hoy = reloj.HoyLocal;
        var configuracion = await db.ElectronicEmissionSettings.AsNoTracking()
            .Where(s => s.ValidFrom <= hoy && (s.ValidTo == null || s.ValidTo >= hoy))
            .OrderByDescending(s => s.ValidFrom).FirstOrDefaultAsync(ct);
        var resuelta = await credenciales.ResolverAsync(canal, ct);
        if (resuelta.IsFailure)
            return Result.Failure<string>(ErroresDeNumeracionYConfiguracion.CredentialMismatch(CredencialesDeCanal.ClaveDe(cooperativa.Value, canal)));

        var contexto = new ContextoDeCanal(cooperativa.Value, configuracion?.IssuerTaxId ?? string.Empty, configuracion?.IssuerCheckDigit ?? string.Empty,
            configuracion?.Mode ?? EmissionMode.TechnologyProvider, resolucion.Environment, software ?? configuracion?.SoftwareId,
            configuracion?.TestSetId, null, null, resuelta.Value);
        var resultado = await adaptador.ConsultarRangosAsync(contexto, ct);
        var clave = RangosDelCanal.ClaveTecnica(resultado, resolucion.Prefix, resolucion.ResolutionNumber);
        return clave is null
            ? Result.Failure<string>(ErroresDeNumeracionYConfiguracion.TechnicalKeyUnavailable(canal))
            : Result.Success(clave);
    }

    private static Result<DianNumberingResolutionDto> Falla(Error error) => Result.Failure<DianNumberingResolutionDto>(error);
}
