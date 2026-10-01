using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.Application.ElectronicInvoicing.Numeracion;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Settings;

/// <summary>
/// Una vigencia nueva de la configuración de emisión (feature 012, I4, T708; api.md §24.1; contracts/dian.md §10.1): crea la fila y
/// cierra la anterior la víspera de <see cref="ValidFrom"/>, sin cruces. Los datos del emisor, si no vienen, se toman de la vigencia
/// anterior o se proponen desde la empresa. <c>CredentialKey</c> no viaja: se <b>deriva</b> de la cooperativa resuelta y el canal
/// (<c>{tenantPublicId}.{channelCode}.json</c>) y queda fuera del diff; <c>CredentialVerifiedAt</c> queda nulo hasta verificar. (nuevo)
/// </summary>
public sealed record ConfigureEmissionCommand(
    EmissionMode Mode,
    string ChannelCode,
    DianEnvironment Environment,
    string? SoftwareId,
    string? TestSetId,
    EmailDeliveryBy EmailDeliveryBy,
    bool IsEnabled,
    DateOnly ValidFrom,
    string Reason,
    string? IssuerTaxId = null,
    string? IssuerCheckDigit = null,
    string? IssuerBusinessName = null,
    string? IssuerAddress = null,
    string? IssuerMunicipalityDaneCode = null,
    string? IssuerEmail = null)
    : IRequest<Result<ElectronicEmissionSettingDto>>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ConfigureEmissionCommandValidator : ValidadorConMotivo<ConfigureEmissionCommand>
{
    public ConfigureEmissionCommandValidator()
    {
        RuleFor(x => x.Mode).IsInEnum();
        RuleFor(x => x.Environment).IsInEnum();
        RuleFor(x => x.EmailDeliveryBy).IsInEnum();
        RuleFor(x => x.ChannelCode).NotEmpty().WithMessage("Indique el canal.").MaximumLength(40);
        RuleFor(x => x.SoftwareId).MaximumLength(36);
        RuleFor(x => x.TestSetId).MaximumLength(36);
        RuleFor(x => x.IssuerTaxId).MaximumLength(15);
        RuleFor(x => x.IssuerCheckDigit).MaximumLength(1);
        RuleFor(x => x.IssuerBusinessName).MaximumLength(200);
        RuleFor(x => x.IssuerAddress).MaximumLength(150);
        RuleFor(x => x.IssuerMunicipalityDaneCode).Matches("^[0-9]{5}$").When(x => !string.IsNullOrWhiteSpace(x.IssuerMunicipalityDaneCode))
            .WithMessage("El municipio del emisor es el código DIVIPOLA de 5 dígitos.");
        RuleFor(x => x.IssuerEmail).MaximumLength(150).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.IssuerEmail));
    }
}

public sealed class ConfigureEmissionCommandHandler(IApplicationDbContext db, ICanalesDeEmision canales, ICurrentTenantService tenant)
    : IRequestHandler<ConfigureEmissionCommand, Result<ElectronicEmissionSettingDto>>
{
    public async Task<Result<ElectronicEmissionSettingDto>> Handle(ConfigureEmissionCommand request, CancellationToken ct)
    {
        var canal = ReglasDeResolucion.Canal(request.ChannelCode);
        if (!canales.Codigos.Contains(canal, StringComparer.OrdinalIgnoreCase)) return Falla(ErroresDeNumeracionYConfiguracion.ChannelUnknown(canal));
        var capacidades = canales.Resolver(canal).Capacidades;
        if (!capacidades.AceptaNumeroDelErp) return Falla(ErroresDeNumeracionYConfiguracion.ChannelRejectsErpNumber(canal));
        var software = string.IsNullOrWhiteSpace(request.SoftwareId) ? null : request.SoftwareId.Trim();
        if (request.Mode == EmissionMode.OwnSoftware && software is null) return Falla(ErroresDeNumeracionYConfiguracion.SoftwareIdRequired());
        if (request.Environment == DianEnvironment.Production && string.Equals(canal, GuardiaDeEmisionFiscal.CanalSimulado, StringComparison.OrdinalIgnoreCase))
            return Falla(ErroresDeNumeracionYConfiguracion.SimulatedInProduction());
        if (request.EmailDeliveryBy == EmailDeliveryBy.Channel && !capacidades.PuedeEnviarCorreo)
            return Falla(ErroresDeNumeracionYConfiguracion.ChannelCannotSendEmail(canal));

        var existentes = await db.ElectronicEmissionSettings.OrderBy(s => s.ValidFrom).ToListAsync(ct);
        if (existentes.Any(s => s.ValidFrom >= request.ValidFrom)) return Falla(ErroresDeNumeracionYConfiguracion.SettingsOverlaps(request.ValidFrom));

        var faltantes = await TiposSinResolucionAsync(canal, software, request.Environment, request.ValidFrom, ct);
        if (faltantes.Count > 0) return Falla(ErroresDeNumeracionYConfiguracion.NoResolutionForChannel(faltantes, canal));

        var cooperativa = ConfiguracionDeEmision.Cooperativa(tenant);
        if (cooperativa.IsFailure) return Falla(cooperativa.Error);

        var anterior = existentes.LastOrDefault(s => s.ValidFrom < request.ValidFrom);
        var empresa = anterior is null ? await db.Companies.AsNoTracking().OrderBy(c => c.Id).FirstOrDefaultAsync(ct) : null;
        var testSet = string.IsNullOrWhiteSpace(request.TestSetId) ? null : request.TestSetId.Trim();

        var nueva = new ElectronicEmissionSetting
        {
            Mode = request.Mode,
            ChannelCode = canal,
            Environment = request.Environment,
            SoftwareId = software,
            TestSetId = testSet,
            // El set de pruebas aceptado se conserva si sigue siendo el mismo software y el mismo set.
            TestSetAcceptedAt = anterior is not null && anterior.Mode == request.Mode && anterior.Environment == request.Environment
                && anterior.SoftwareId == software && anterior.TestSetId == testSet ? anterior.TestSetAcceptedAt : null,
            CredentialKey = CredencialesDeCanal.ClaveDe(cooperativa.Value, canal),
            CredentialVerifiedAt = null,
            EmailDeliveryBy = request.EmailDeliveryBy,
            IssuerTaxId = Dato(request.IssuerTaxId, anterior?.IssuerTaxId, empresa?.TaxId),
            IssuerCheckDigit = Dato(request.IssuerCheckDigit, anterior?.IssuerCheckDigit, empresa?.TaxIdCheckDigit),
            IssuerBusinessName = Dato(request.IssuerBusinessName, anterior?.IssuerBusinessName, empresa?.Name),
            IssuerAddress = Dato(request.IssuerAddress, anterior?.IssuerAddress, empresa?.Address),
            IssuerMunicipalityDaneCode = Dato(request.IssuerMunicipalityDaneCode, anterior?.IssuerMunicipalityDaneCode, null),
            IssuerEmail = Dato(request.IssuerEmail, anterior?.IssuerEmail, null),
            IsEnabled = request.IsEnabled,
            ValidFrom = request.ValidFrom,
            Reason = request.Reason.Trim(),
        };

        // La anterior se cierra la víspera: sin cruces.
        foreach (var abierta in existentes.Where(s => s.ValidFrom < request.ValidFrom && (s.ValidTo is null || s.ValidTo >= request.ValidFrom)))
            abierta.ValidTo = request.ValidFrom.AddDays(-1);

        db.ElectronicEmissionSettings.Add(nueva);
        await db.SaveChangesAsync(ct);
        return Result.Success(ConfiguracionDeEmision.ADto(nueva));
    }

    /// <summary>
    /// «Al menos una resolución vigente de cada tipo que la cooperativa usa, con su prefijo asociado a ese canal o software» (§10.1): los
    /// tipos en uso son los que tienen alguna resolución activa vigente en ese ambiente a la fecha; cada uno necesita una asociada al canal
    /// nuevo desde esa fecha. La primera configuración, sin resoluciones todavía, pasa: la preparación dirá después lo que falta.
    /// </summary>
    private async Task<IReadOnlyList<string>> TiposSinResolucionAsync(string canal, string? software, DianEnvironment ambiente, DateOnly desde, CancellationToken ct)
    {
        var vigentes = (await db.DianNumberingResolutions.AsNoTracking().Include(r => r.Channels)
                .Where(r => r.IsActive && r.Environment == ambiente)
                .ToListAsync(ct))
            .Where(r => r.VigenteEn(desde))
            .ToList();
        return vigentes
            .GroupBy(r => r.Kind)
            .Where(g => !g.Any(r => ReglasDeResolucion.AsociacionVigente(r, canal, software, desde) is not null))
            .OrderBy(g => g.Key)
            .Select(g => g.Key.ToString())
            .ToList();
    }

    private static string Dato(string? pedido, string? anterior, string? propuesto) =>
        !string.IsNullOrWhiteSpace(pedido) ? pedido.Trim() : anterior ?? propuesto?.Trim() ?? string.Empty;

    private static Result<ElectronicEmissionSettingDto> Falla(Error error) => Result.Failure<ElectronicEmissionSettingDto>(error);
}
