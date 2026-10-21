using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Adapters;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Core.ExternalChannels;

// Feature 013 (T0080; T71–T73; contracts/api.md §21.2; contracts/adaptadores-y-archivos.md §3.3; data-model parte 3 §4.7): la
// configuración de los canales externos por cooperativa, rutas /api/core/external-channels con Core.ExternalChannels.View/Manage. Las
// escrituras llevan Idempotency-Key y la configuración, motivo. La base guarda sólo la referencia a la credencial.

/// <summary>Una configuración de canal externo (§21.2). <c>simulated</c> y <c>circuit</c> se calculan. (nuevo)</summary>
public sealed record ExternalChannelSettingDto(
    Guid PublicId,
    ExternalChannelKind Kind,
    string? BureauCode,
    string ChannelCode,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string? CredentialKey,
    DateTime? VerifiedAt,
    string? VerifiedBy,
    ExternalOutcome? LastOutcome,
    bool Simulated,
    EstadoDelCircuito Circuit);

/// <summary>Lo que responde la verificación (§21.2): el resultado de <c>ProbarAsync</c>. (nuevo)</summary>
public sealed record ExternalChannelVerificationDto(ExternalOutcome Outcome, string? Detail, DateTime? VerifiedAt);

internal static class ProyeccionDeCanalesExternos
{
    public static ExternalChannelSettingDto ADto(ExternalChannelSetting s, ICircuitoDeCanalExterno circuito) => new(
        s.PublicId, s.Kind, s.BureauCode, s.ChannelCode, s.ValidFrom, s.ValidTo, s.CredentialKey, s.VerifiedAt, s.VerifiedByName,
        s.LastVerificationOutcome, CodigosDeCanalExterno.EsSimulado(s.ChannelCode), circuito.Estado(s.ChannelCode));

    /// <summary>Los códigos instalados en el despliegue para <paramref name="clase"/>, de todos los registros que la atienden.</summary>
    public static IReadOnlyList<string> Disponibles(IEnumerable<IRegistroDeCanalesExternos> registros, ExternalChannelKind clase) =>
        registros.Where(r => r.Clases.Contains(clase))
            .SelectMany(r => r.CodigosPara(clase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)
            .ToList();
}

/// <summary>
/// Configura el canal de una clase desde <see cref="ValidFrom"/> (§21.2, <c>POST /</c>): crea una fila y cierra la vigente de la misma
/// clave la víspera. <c>SIMULADO</c> y <c>CARGA-PROPIA</c> no piden credencial; cualquier otro exige <see cref="CredentialKey"/>, que tiene
/// que ser la derivada de la cooperativa resuelta. <c>SIMULADO</c> se admite en producción antes y después de activar (T73): la guardia le
/// quita el efecto legal. La auditoría no copia <c>CredentialKey</c> (<c>[NoAuditar]</c>). Errores: <c>Core.ExternalChannel.ChannelUnknown</c>
/// (<c>data: { kind, available[] }</c>), <c>.CredentialKeyRequired</c>, <c>.CredentialMismatch</c>, <c>.Overlaps</c>. (nuevo)
/// </summary>
public sealed record ConfigureExternalChannelCommand(
    ExternalChannelKind Kind,
    string? BureauCode,
    string ChannelCode,
    DateOnly ValidFrom,
    string? CredentialKey,
    string Reason) : IRequest<Result<ExternalChannelSettingDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ConfigureExternalChannelCommandValidator : ValidadorConMotivo<ConfigureExternalChannelCommand>
{
    public ConfigureExternalChannelCommandValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.ChannelCode)
            .Must(c => CodigosDeCanalExterno.Normalizar(c) is not null)
            .WithMessage("El código del canal es de 2 a 30 caracteres: mayúsculas, dígitos y guiones.");
        RuleFor(x => x.BureauCode)
            .NotEmpty().When(x => x.Kind == ExternalChannelKind.CreditBureau)
            .WithMessage("Una central de riesgo dice cuál es (bureauCode).");
        RuleFor(x => x.BureauCode)
            .Empty().When(x => x.Kind != ExternalChannelKind.CreditBureau)
            .WithMessage("Sólo una central de riesgo lleva bureauCode.");
        RuleFor(x => x.BureauCode)
            .Matches("^[A-Za-z0-9]{1,20}$").When(x => !string.IsNullOrWhiteSpace(x.BureauCode))
            .WithMessage("El código de la central es alfanumérico, sin espacios, de hasta 20 caracteres.");
        RuleFor(x => x.CredentialKey).MaximumLength(100);
        RuleFor(x => x.ValidFrom).NotEqual(default(DateOnly)).WithMessage("Indique desde cuándo rige.");
        RuleFor(x => x.Reason).MaximumLength(300);
    }
}

public sealed class ConfigureExternalChannelCommandHandler(
    IApplicationDbContext db,
    IEnumerable<IRegistroDeCanalesExternos> registros,
    ICircuitoDeCanalExterno circuito,
    ICurrentTenantService tenant,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<ConfigureExternalChannelCommand, Result<ExternalChannelSettingDto>>
{
    public async Task<Result<ExternalChannelSettingDto>> Handle(ConfigureExternalChannelCommand request, CancellationToken ct)
    {
        var canal = CodigosDeCanalExterno.Normalizar(request.ChannelCode)!;
        var central = request.Kind == ExternalChannelKind.CreditBureau ? request.BureauCode!.Trim().ToUpperInvariant() : null;

        var disponibles = ProyeccionDeCanalesExternos.Disponibles(registros, request.Kind);
        if (!disponibles.Contains(canal, StringComparer.OrdinalIgnoreCase))
            return Result.Failure<ExternalChannelSettingDto>(ErroresDeCanalesExternos.CanalDesconocido(request.Kind, canal, disponibles));

        string? credentialKey = null;
        if (!CodigosDeCanalExterno.NoPideCredencial(canal))
        {
            if (string.IsNullOrWhiteSpace(request.CredentialKey))
                return Result.Failure<ExternalChannelSettingDto>(ErroresDeCanalesExternos.FaltaLaClave(canal));
            var esperada = CooperativaDelCanal.Resuelta(tenant) is { } cooperativa
                ? CredencialDeCanalExterno.ClaveDe(cooperativa, canal)
                : "(sin cooperativa resuelta)";
            if (!string.Equals(request.CredentialKey.Trim(), esperada, StringComparison.Ordinal))
                return Result.Failure<ExternalChannelSettingDto>(ErroresDeCanalesExternos.ClaveNoCoincide(canal, esperada));
            credentialKey = esperada;
        }

        var clave = ExternalChannelSetting.ClaveDe(request.Kind, central);
        var deLaClave = await db.ExternalChannelSettings.Where(s => s.SettingKey == clave && !s.IsDeleted).ToListAsync(ct);
        // Una vigencia nueva no puede empezar el día o antes que otra, ni dentro de una ya cerrada: sólo cierra la abierta.
        if (deLaClave.Any(s => s.ValidFrom >= request.ValidFrom || (s.ValidTo is { } hasta && hasta >= request.ValidFrom)))
            return Result.Failure<ExternalChannelSettingDto>(ErroresDeCanalesExternos.SeCruza(clave, request.ValidFrom));

        var ahora = reloj.UtcNow;
        var actor = await actorActual.ObtenerAsync(ct);
        foreach (var abierta in deLaClave.Where(s => s.ValidTo is null))
        {
            abierta.ValidTo = request.ValidFrom.AddDays(-1);
            abierta.UpdatedAt = ahora;
            abierta.UpdatedBy = Recortar(actor.Name, 100);
        }

        var fila = new ExternalChannelSetting
        {
            Kind = request.Kind,
            BureauCode = central,
            SettingKey = clave,
            ChannelCode = canal,
            ValidFrom = request.ValidFrom,
            CredentialKey = credentialKey,
            Reason = Recortar(request.Reason.Trim(), 300),
            CreatedAt = ahora,
            CreatedBy = Recortar(actor.Name, 100),
        };
        db.ExternalChannelSettings.Add(fila);
        await db.SaveChangesAsync(ct);
        return Result.Success(ProyeccionDeCanalesExternos.ADto(fila, circuito));
    }

    private static string Recortar(string texto, int largo) => texto.Length <= largo ? texto : texto[..largo];
}

/// <summary>
/// Verifica la credencial de una configuración (§21.2, <c>POST /{id}/verify</c>): la resuelve del Secret, llama <c>ProbarAsync</c> del
/// adaptador y anota el resultado; si responde <c>Ok</c>, fija <c>VerifiedAt</c>. Sin archivo en el Secret,
/// <c>Core.ExternalChannel.CredentialMissing</c>; con la clave alterada en la base, <c>.CredentialMismatch</c>. Un adaptador que lanza
/// cuenta como <c>ChannelUnavailable</c>. (nuevo)
/// </summary>
public sealed record VerifyExternalChannelCredentialCommand(Guid SettingPublicId) : IRequest<Result<ExternalChannelVerificationDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class VerifyExternalChannelCredentialCommandValidator : AbstractValidator<VerifyExternalChannelCredentialCommand>
{
    public VerifyExternalChannelCredentialCommandValidator() => RuleFor(x => x.SettingPublicId).NotEqual(Guid.Empty);
}

public sealed class VerifyExternalChannelCredentialCommandHandler(
    IApplicationDbContext db,
    ICredencialesDeCanalExterno credenciales,
    IEnumerable<IRegistroDeCanalesExternos> registros,
    ICurrentTenantService tenant,
    IActorActual actorActual,
    IDateTimeService reloj,
    IOptions<CanalesExternosOptions> opciones) : IRequestHandler<VerifyExternalChannelCredentialCommand, Result<ExternalChannelVerificationDto>>
{
    public async Task<Result<ExternalChannelVerificationDto>> Handle(VerifyExternalChannelCredentialCommand request, CancellationToken ct)
    {
        var fila = await db.ExternalChannelSettings.FirstOrDefaultAsync(s => s.PublicId == request.SettingPublicId && !s.IsDeleted, ct);
        if (fila is null) return Result.Failure<ExternalChannelVerificationDto>(ErroresDeCanalesExternos.NoEncontrada(request.SettingPublicId));

        var credencial = await credenciales.ResolverAsync(fila.ChannelCode, ct);
        if (credencial.IsFailure) return Result.Failure<ExternalChannelVerificationDto>(credencial.Error);

        var contexto = new ContextoDeCanalExterno(CooperativaDelCanal.Resuelta(tenant) ?? Guid.Empty, fila.ChannelCode, fila.BureauCode,
            CodigosDeCanalExterno.NoPideCredencial(fila.ChannelCode) ? null : credencial.Value,
            $"verificacion:{fila.PublicId:N}", opciones.Value.AmbienteDePruebas);

        ResultadoExterno<bool> resultado;
        var registro = registros.FirstOrDefault(r => r.Clases.Contains(fila.Kind));
        try
        {
            resultado = registro is null
                ? ResultadoExterno<bool>.NoInstalado(fila.ChannelCode)
                : await registro.ProbarAsync(fila.Kind, fila.ChannelCode, contexto, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            resultado = ResultadoExterno<bool>.Sin(ExternalOutcome.ChannelUnavailable, fila.ChannelCode, false,
                $"El canal lanzó al probarlo ({ex.GetType().Name}).");
        }

        var ahora = reloj.UtcNow;
        var actor = await actorActual.ObtenerAsync(ct);
        fila.LastVerificationOutcome = resultado.Outcome;
        fila.LastVerificationMessage = Recortar(resultado.Detalle ?? (resultado.EsOk ? "El canal respondió." : resultado.Outcome.ToString()), 500);
        fila.VerifiedByUserId = actor.UserId;
        fila.VerifiedByName = Recortar(actor.Name, 150);
        if (resultado.EsOk) fila.VerifiedAt = ahora;
        fila.UpdatedAt = ahora;
        fila.UpdatedBy = Recortar(actor.Name, 100);
        await db.SaveChangesAsync(ct);

        return Result.Success(new ExternalChannelVerificationDto(resultado.Outcome, fila.LastVerificationMessage, fila.VerifiedAt));
    }

    private static string Recortar(string texto, int largo) => texto.Length <= largo ? texto : texto[..largo];
}

/// <summary>Las configuraciones de canales externos (§21.2, <c>GET /?kind=</c>), con <c>simulated</c> y el estado del circuito. (nuevo)</summary>
public sealed record ListExternalChannelSettingsQuery(ExternalChannelKind? Kind = null) : IRequest<Result<IReadOnlyList<ExternalChannelSettingDto>>>;

public sealed class ListExternalChannelSettingsQueryValidator : AbstractValidator<ListExternalChannelSettingsQuery>
{
    public ListExternalChannelSettingsQueryValidator() => RuleFor(x => x.Kind).IsInEnum().When(x => x.Kind is not null);
}

public sealed class ListExternalChannelSettingsQueryHandler(IApplicationDbContext db, ICircuitoDeCanalExterno circuito)
    : IRequestHandler<ListExternalChannelSettingsQuery, Result<IReadOnlyList<ExternalChannelSettingDto>>>
{
    public async Task<Result<IReadOnlyList<ExternalChannelSettingDto>>> Handle(ListExternalChannelSettingsQuery request, CancellationToken ct)
    {
        var consulta = db.ExternalChannelSettings.AsNoTracking().Where(s => !s.IsDeleted);
        if (request.Kind is { } clase) consulta = consulta.Where(s => s.Kind == clase);
        var filas = await consulta.ToListAsync(ct);
        return Result.Success<IReadOnlyList<ExternalChannelSettingDto>>(filas
            .OrderBy(s => s.Kind).ThenBy(s => s.BureauCode, StringComparer.Ordinal).ThenByDescending(s => s.ValidFrom)
            .Select(s => ProyeccionDeCanalesExternos.ADto(s, circuito))
            .ToList());
    }
}
