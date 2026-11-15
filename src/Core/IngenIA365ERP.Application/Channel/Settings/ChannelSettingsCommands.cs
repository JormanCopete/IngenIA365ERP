using System.Globalization;
using System.Text;
using FluentValidation;
using IngenIA365ERP.Application.Channel.Common;
using IngenIA365ERP.Application.Channel.Enablements;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Channel;
using IngenIA365ERP.Domain.Enums.Channel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Channel.Settings;

// Feature 021 (US1; T037, T038; FR-001, FR-007 a FR-014): la configuración del canal digital de la cooperativa. Cambiar crea
// una versión nueva con su vigencia (nunca se edita la anterior); encender asigna el código de canal y materializa la
// política de habilitación.

/// <summary>La configuración vigente (o los valores por defecto si la cooperativa nunca configuró el canal).</summary>
public sealed record ChannelSettingsDto(
    int Version,
    DateTime? EffectiveFrom,
    bool IsEnabled,
    string? ChannelCode,
    string DisplayName,
    Guid? LogoAttachmentPublicId,
    string? PrimaryColor,
    string? SecondaryColor,
    string? SupportPhone,
    string? SupportEmail,
    string? SupportHours,
    int PaymentSessionMinutes,
    bool RequireOtpToPay,
    int InactivityMinutes,
    EnablementPolicy EnablementPolicy,
    bool AllowNonAssociates,
    bool SmsEnabled,
    int DueReminderDays,
    SurplusPolicy SurplusPolicy,
    bool SatelliteConfigured);

public sealed record GetChannelSettingsQuery : IRequest<Result<ChannelSettingsDto>>;

public sealed class GetChannelSettingsQueryHandler(IApplicationDbContext db, IDateTimeService reloj, ICanalSatelite canal)
    : IRequestHandler<GetChannelSettingsQuery, Result<ChannelSettingsDto>>
{
    public async Task<Result<ChannelSettingsDto>> Handle(GetChannelSettingsQuery request, CancellationToken ct)
    {
        var vigente = await ConfiguracionDelCanal.VigenteAsync(db, reloj.UtcNow, ct);
        return Result.Success(ConfiguracionComoDto.De(vigente ?? new ChannelSettings(), vigente is null, canal.Configurado));
    }
}

internal static class ConfiguracionComoDto
{
    public static ChannelSettingsDto De(ChannelSettings c, bool sinConfigurar, bool satelite) => new(
        sinConfigurar ? 0 : c.Version, sinConfigurar ? null : c.EffectiveFrom, c.IsEnabled, c.ChannelCode, c.DisplayName,
        c.LogoAttachmentPublicId, c.PrimaryColor, c.SecondaryColor, c.SupportPhone, c.SupportEmail, c.SupportHours,
        c.PaymentSessionMinutes, c.RequireOtpToPay, c.InactivityMinutes, c.EnablementPolicy, c.AllowNonAssociates, c.SmsEnabled,
        c.DueReminderDays, c.SurplusPolicy, satelite);

    /// <summary>Una versión nueva a partir de la vigente: copia todo y deja la vigencia en ahora.</summary>
    public static ChannelSettings Siguiente(ChannelSettings? vigente, DateTime ahora) => new()
    {
        Version = (vigente?.Version ?? 0) + 1,
        EffectiveFrom = ahora,
        IsEnabled = vigente?.IsEnabled ?? false,
        ChannelCode = vigente?.ChannelCode,
        DisplayName = vigente?.DisplayName ?? string.Empty,
        LogoAttachmentPublicId = vigente?.LogoAttachmentPublicId,
        PrimaryColor = vigente?.PrimaryColor,
        SecondaryColor = vigente?.SecondaryColor,
        SupportPhone = vigente?.SupportPhone,
        SupportEmail = vigente?.SupportEmail,
        SupportHours = vigente?.SupportHours,
        PaymentSessionMinutes = vigente?.PaymentSessionMinutes ?? 15,
        RequireOtpToPay = vigente?.RequireOtpToPay ?? false,
        InactivityMinutes = vigente?.InactivityMinutes ?? 5,
        EnablementPolicy = vigente?.EnablementPolicy ?? EnablementPolicy.AllActiveAssociates,
        AllowNonAssociates = vigente?.AllowNonAssociates ?? false,
        SmsEnabled = vigente?.SmsEnabled ?? false,
        DueReminderDays = vigente?.DueReminderDays ?? 3,
        SurplusPolicy = vigente?.SurplusPolicy ?? SurplusPolicy.AdvanceElseCreditBalance,
    };
}

/// <summary>FR-007 a FR-014: cambia la configuración (crea una versión). No enciende ni apaga: eso es <see cref="EnableChannelCommand"/>.</summary>
public sealed record UpdateChannelSettingsCommand(
    string DisplayName,
    Guid? LogoAttachmentPublicId,
    string? PrimaryColor,
    string? SecondaryColor,
    string? SupportPhone,
    string? SupportEmail,
    string? SupportHours,
    int PaymentSessionMinutes,
    bool RequireOtpToPay,
    int InactivityMinutes,
    EnablementPolicy EnablementPolicy,
    bool AllowNonAssociates,
    bool SmsEnabled,
    int DueReminderDays,
    SurplusPolicy SurplusPolicy) : IRequest<Result<ChannelSettingsDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class UpdateChannelSettingsCommandValidator : AbstractValidator<UpdateChannelSettingsCommand>
{
    public const string PatronDeColor = "^#[0-9A-Fa-f]{6}$";

    public UpdateChannelSettingsCommandValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().WithMessage("Escribe el nombre con que la cooperativa aparece en el canal.")
            .MaximumLength(120).WithMessage("El nombre del canal admite hasta 120 caracteres.");
        RuleFor(x => x.PrimaryColor).Matches(PatronDeColor).When(x => !string.IsNullOrWhiteSpace(x.PrimaryColor))
            .WithMessage("El color primario va como #RRGGBB.");
        RuleFor(x => x.SecondaryColor).Matches(PatronDeColor).When(x => !string.IsNullOrWhiteSpace(x.SecondaryColor))
            .WithMessage("El color secundario va como #RRGGBB.");
        RuleFor(x => x.SupportPhone).MaximumLength(40);
        RuleFor(x => x.SupportEmail).MaximumLength(150).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.SupportEmail))
            .WithMessage("El correo de soporte no es válido.");
        RuleFor(x => x.SupportHours).MaximumLength(120);
        RuleFor(x => x.PaymentSessionMinutes).InclusiveBetween(10, 30)
            .WithMessage("La sesión de pago dura entre 10 y 30 minutos (lo exige la pasarela).");
        RuleFor(x => x.InactivityMinutes).InclusiveBetween(5, 15)
            .WithMessage("La inactividad que cierra la sesión del asociado va de 5 a 15 minutos.");
        RuleFor(x => x.DueReminderDays).InclusiveBetween(0, 15)
            .WithMessage("El aviso de cuota próxima va de 0 (sin aviso) a 15 días antes.");
        RuleFor(x => x.EnablementPolicy).IsInEnum();
        RuleFor(x => x.SurplusPolicy).IsInEnum();
    }
}

public sealed class UpdateChannelSettingsCommandHandler(IApplicationDbContext db, IDateTimeService reloj, ICanalSatelite canal)
    : IRequestHandler<UpdateChannelSettingsCommand, Result<ChannelSettingsDto>>
{
    public async Task<Result<ChannelSettingsDto>> Handle(UpdateChannelSettingsCommand r, CancellationToken ct)
    {
        var ahora = reloj.UtcNow;
        var vigente = await ConfiguracionDelCanal.VigenteAsync(db, ahora, ct);
        var nueva = ConfiguracionComoDto.Siguiente(vigente, ahora);
        nueva.DisplayName = r.DisplayName.Trim();
        nueva.LogoAttachmentPublicId = r.LogoAttachmentPublicId;
        nueva.PrimaryColor = Normalizar(r.PrimaryColor)?.ToUpperInvariant();
        nueva.SecondaryColor = Normalizar(r.SecondaryColor)?.ToUpperInvariant();
        nueva.SupportPhone = Normalizar(r.SupportPhone);
        nueva.SupportEmail = Normalizar(r.SupportEmail);
        nueva.SupportHours = Normalizar(r.SupportHours);
        nueva.PaymentSessionMinutes = r.PaymentSessionMinutes;
        nueva.RequireOtpToPay = r.RequireOtpToPay;
        nueva.InactivityMinutes = r.InactivityMinutes;
        nueva.EnablementPolicy = r.EnablementPolicy;
        nueva.AllowNonAssociates = r.AllowNonAssociates;
        nueva.SmsEnabled = r.SmsEnabled;
        nueva.DueReminderDays = r.DueReminderDays;
        nueva.SurplusPolicy = r.SurplusPolicy;
        db.ChannelSettings.Add(nueva);
        await db.SaveChangesAsync(ct);
        return Result.Success(ConfiguracionComoDto.De(nueva, false, canal.Configurado));
    }

    private static string? Normalizar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

/// <summary>
/// FR-001: enciende el canal. Exige el nombre del canal y los términos y la política de datos publicados (sin ellos nadie puede
/// activar su cuenta, FR-020). La primera vez reserva el código de 3 letras en el canal (único en la plataforma, R12); luego
/// materializa la política de habilitación (R8).
/// </summary>
public sealed record EnableChannelCommand : IRequest<Result<ChannelSettingsDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class EnableChannelCommandValidator : AbstractValidator<EnableChannelCommand>
{
}

public sealed class EnableChannelCommandHandler(
    IApplicationDbContext db,
    IDateTimeService reloj,
    ICanalSatelite canal,
    ICurrentTenantService cooperativa,
    PoliticaDeHabilitacion politica)
    : IRequestHandler<EnableChannelCommand, Result<ChannelSettingsDto>>
{
    public const string SinNombre = "Channel.Settings.DisplayNameMissing";
    public const string SinTextos = "Channel.Settings.LegalTextsMissing";

    public async Task<Result<ChannelSettingsDto>> Handle(EnableChannelCommand request, CancellationToken ct)
    {
        var ahora = reloj.UtcNow;
        var vigente = await ConfiguracionDelCanal.VigenteAsync(db, ahora, ct);
        if (vigente is { IsEnabled: true }) return Result.Success(ConfiguracionComoDto.De(vigente, false, canal.Configurado));
        if (vigente is null || string.IsNullOrWhiteSpace(vigente.DisplayName))
            return Result.Failure<ChannelSettingsDto>(SinNombre, "Antes de encender el canal guarda la configuración con el nombre de la cooperativa.");

        var publicados = await db.ChannelLegalTexts.AsNoTracking().Select(t => t.Kind).Distinct().ToListAsync(ct);
        if (!publicados.Contains(LegalTextKind.Terms) || !publicados.Contains(LegalTextKind.DataPolicy))
            return Result.Failure<ChannelSettingsDto>(SinTextos,
                "Antes de encender el canal publica los términos y condiciones y la política de tratamiento de datos.");

        var codigo = vigente.ChannelCode;
        if (string.IsNullOrWhiteSpace(codigo))
        {
            if (!Guid.TryParse(cooperativa.TenantId, out var publica))
                return Result.Failure<ChannelSettingsDto>(Error.Validation);
            var reservado = await canal.ReservarCodigoAsync(publica, CodigoSugerido(vigente.DisplayName), ct);
            if (reservado.IsFailure) return Result.Failure<ChannelSettingsDto>(reservado.Error);
            codigo = reservado.Value;
        }

        var nueva = ConfiguracionComoDto.Siguiente(vigente, ahora);
        nueva.IsEnabled = true;
        nueva.ChannelCode = codigo;
        db.ChannelSettings.Add(nueva);
        await politica.MaterializarAsync(nueva, ahora, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(ConfiguracionComoDto.De(nueva, false, canal.Configurado));
    }

    /// <summary>Las iniciales del nombre en mayúsculas, sin tildes (el canal lo ajusta si ya está tomado).</summary>
    public static string CodigoSugerido(string nombre)
    {
        var sinTildes = new string(nombre.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        var palabras = sinTildes.Split([' ', '-', '.', ','], StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.Length > 2 && p.Any(char.IsAsciiLetter))
            .ToList();
        var iniciales = new string(palabras.Select(p => p.First(char.IsAsciiLetter)).ToArray()).ToUpperInvariant();
        var letras = new string(sinTildes.Where(char.IsAsciiLetter).ToArray()).ToUpperInvariant();
        var sugerido = (iniciales + letras).PadRight(3, 'X');
        return sugerido[..3];
    }
}

/// <summary>FR-001: apaga el canal. Los intentos pendientes se siguen resolviendo (el aviso de pago responde aunque esté apagado).</summary>
public sealed record DisableChannelCommand : IRequest<Result<ChannelSettingsDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class DisableChannelCommandValidator : AbstractValidator<DisableChannelCommand>
{
}

public sealed class DisableChannelCommandHandler(IApplicationDbContext db, IDateTimeService reloj, ICanalSatelite canal)
    : IRequestHandler<DisableChannelCommand, Result<ChannelSettingsDto>>
{
    public async Task<Result<ChannelSettingsDto>> Handle(DisableChannelCommand request, CancellationToken ct)
    {
        var ahora = reloj.UtcNow;
        var vigente = await ConfiguracionDelCanal.VigenteAsync(db, ahora, ct);
        if (vigente is not { IsEnabled: true })
            return Result.Success(ConfiguracionComoDto.De(vigente ?? new ChannelSettings(), vigente is null, canal.Configurado));
        var nueva = ConfiguracionComoDto.Siguiente(vigente, ahora);
        nueva.IsEnabled = false;
        db.ChannelSettings.Add(nueva);
        await db.SaveChangesAsync(ct);
        return Result.Success(ConfiguracionComoDto.De(nueva, false, canal.Configurado));
    }
}
