using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.Application.ElectronicInvoicing.Numeracion;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Settings;

/// <summary>Un mensaje del canal en la respuesta de la verificación (api.md §24.1). (nuevo)</summary>
public sealed record MensajeDelCanalDto(string Rule, string Kind, string Text, string? Translation);

/// <summary>El resultado de verificar la credencial: si quedó verificada, cuándo, qué respondió el canal y sus mensajes. (nuevo)</summary>
public sealed record VerificacionDeCredencialDto(bool Verified, DateTime? VerifiedAt, string Outcome, IReadOnlyList<MensajeDelCanalDto> Messages);

/// <summary>
/// Verificar la credencial del canal (feature 012, I4, T708; api.md §24.1; contracts/dian.md §10.2): toma la configuración vigente (o la
/// más reciente del canal pedido), comprueba que su <c>CredentialKey</c> sea la derivada de la cooperativa resuelta, resuelve la credencial
/// por <see cref="ICredencialesDeCanal"/>, llama <see cref="ICanalDeEmisionElectronica.ProbarAsync"/> y, si responde validado, <b>sólo</b>
/// sella <c>CredentialVerifiedAt</c>. Clave distinta o archivo ausente → <c>ElectronicInvoicing.CredentialMismatch</c>. (nuevo)
/// </summary>
public sealed record VerifyChannelCredentialCommand(string? ChannelCode)
    : IRequest<Result<VerificacionDeCredencialDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class VerifyChannelCredentialCommandHandler(
    IApplicationDbContext db,
    ICanalesDeEmision canales,
    ICredencialesDeCanal credenciales,
    ICurrentTenantService tenant,
    IDateTimeService reloj)
    : IRequestHandler<VerifyChannelCredentialCommand, Result<VerificacionDeCredencialDto>>
{
    public async Task<Result<VerificacionDeCredencialDto>> Handle(VerifyChannelCredentialCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var vivas = await db.ElectronicEmissionSettings.OrderByDescending(s => s.ValidFrom).ToListAsync(ct);
        ElectronicEmissionSetting? configuracion;
        if (string.IsNullOrWhiteSpace(request.ChannelCode))
            configuracion = vivas.FirstOrDefault(s => s.VigenteEn(hoy));
        else
        {
            var pedido = ReglasDeResolucion.Canal(request.ChannelCode);
            configuracion = vivas.Where(s => string.Equals(s.ChannelCode, pedido, StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault(s => s.ValidTo is null || s.ValidTo >= hoy);
        }
        if (configuracion is null) return Falla(ErroresDeNumeracionYConfiguracion.SettingsMissing());

        var canal = ReglasDeResolucion.Canal(configuracion.ChannelCode);
        if (!canales.Codigos.Contains(canal, StringComparer.OrdinalIgnoreCase)) return Falla(ErroresDeNumeracionYConfiguracion.ChannelUnknown(canal));

        var cooperativa = ConfiguracionDeEmision.Cooperativa(tenant);
        if (cooperativa.IsFailure) return Falla(cooperativa.Error);
        var esperada = CredencialesDeCanal.ClaveDe(cooperativa.Value, canal);
        if (!string.Equals(configuracion.CredentialKey, esperada, StringComparison.Ordinal))
            return Falla(ErroresDeNumeracionYConfiguracion.CredentialMismatch(esperada));

        var resuelta = await credenciales.ResolverAsync(canal, ct);
        if (resuelta.IsFailure || !string.Equals(resuelta.Value.Clave, esperada, StringComparison.Ordinal))
            return Falla(ErroresDeNumeracionYConfiguracion.CredentialMismatch(esperada));

        var contexto = new ContextoDeCanal(cooperativa.Value, configuracion.IssuerTaxId, configuracion.IssuerCheckDigit, configuracion.Mode,
            configuracion.Environment, configuracion.SoftwareId, configuracion.TestSetId, null, null, resuelta.Value);
        var resultado = await canales.Resolver(canal).ProbarAsync(contexto, ct);

        var verificada = resultado.Outcome is ChannelOutcome.Validated or ChannelOutcome.ValidatedWithNotices;
        if (verificada)
        {
            configuracion.CredentialVerifiedAt = reloj.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        var mensajes = resultado.Mensajes
            .Select(m => new MensajeDelCanalDto(m.Regla, m.Tipo == TipoDeMensajeDelCanal.Rechazo ? "Rejection" : "Notice", m.Texto, m.Traduccion))
            .ToList();
        return Result.Success(new VerificacionDeCredencialDto(verificada, configuracion.CredentialVerifiedAt, resultado.Outcome.ToString(), mensajes));
    }

    private static Result<VerificacionDeCredencialDto> Falla(Error error) => Result.Failure<VerificacionDeCredencialDto>(error);
}
