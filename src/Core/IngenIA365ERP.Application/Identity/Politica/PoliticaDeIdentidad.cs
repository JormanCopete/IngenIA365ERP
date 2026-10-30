using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Memberships;
using IngenIA365ERP.Domain.Common.Identidad;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Identity.Politica;

/// <summary>
/// La política de identidad de una cooperativa (feature 017, FR-008, FR-030, D-06): qué tipos de
/// documento acepta al invitar, desde cuándo exige el documento para entrar y cuántos intentos da al
/// activar. La administra quien administra la cooperativa (o el maestro), como la de segundo factor.
/// </summary>
public sealed record PoliticaDeIdentidadDto(
    Guid TenantPublicId,
    IReadOnlyList<string> TiposAceptados,
    IReadOnlyList<TipoDeDocumentoDto> Catalogo,
    DateOnly? DocumentoExigidoDesde,
    int IntentosAlActivar);

/// <summary>Un tipo del catálogo fijo, para la pantalla.</summary>
public sealed record TipoDeDocumentoDto(string Codigo, string Nombre, string Prefijo, bool AdmiteLetras);

public sealed record GetPoliticaDeIdentidadQuery(Guid TenantPublicId) : IRequest<Result<PoliticaDeIdentidadDto>>;

public sealed record UpdatePoliticaDeIdentidadCommand(
    Guid TenantPublicId,
    IReadOnlyList<string> TiposAceptados,
    DateOnly? DocumentoExigidoDesde,
    int IntentosAlActivar) : IRequest<Result<PoliticaDeIdentidadDto>>;

public sealed class UpdatePoliticaDeIdentidadCommandValidator : AbstractValidator<UpdatePoliticaDeIdentidadCommand>
{
    public UpdatePoliticaDeIdentidadCommandValidator()
    {
        RuleFor(x => x.TiposAceptados).NotEmpty().WithMessage("La cooperativa debe aceptar al menos un tipo de documento.");
        RuleForEach(x => x.TiposAceptados)
            .Must(c => TiposDeDocumento.PorCodigo(c) is { SirveParaEntrar: true })
            .WithMessage("Tipo de documento no válido: {PropertyValue}.");
        RuleFor(x => x.IntentosAlActivar)
            .InclusiveBetween(TenantIdentityPolicy.IntentosMinimos, TenantIdentityPolicy.IntentosMaximos);
    }
}

public sealed class GetPoliticaDeIdentidadQueryHandler(ICurrentCentralUserContext currentUser, IAdminDbContext db)
    : IRequestHandler<GetPoliticaDeIdentidadQuery, Result<PoliticaDeIdentidadDto>>
{
    public async Task<Result<PoliticaDeIdentidadDto>> Handle(GetPoliticaDeIdentidadQuery request, CancellationToken ct)
    {
        var guard = await Authz.EnsureTenantAdminOrMasterAsync(currentUser, db, request.TenantPublicId, ct);
        if (!guard.IsAllowed)
            return Result.Failure<PoliticaDeIdentidadDto>(guard.ErrorCode!, guard.Message!);

        var politica = await db.TenantIdentityPolicies.AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == request.TenantPublicId, ct);
        return Result.Success(Dto.De(request.TenantPublicId, politica));
    }
}

public sealed class UpdatePoliticaDeIdentidadCommandHandler(
    ICurrentCentralUserContext currentUser,
    IAdminDbContext db,
    AuditoriaDeIdentidad auditoria,
    IDateTimeService clock)
    : IRequestHandler<UpdatePoliticaDeIdentidadCommand, Result<PoliticaDeIdentidadDto>>
{
    public async Task<Result<PoliticaDeIdentidadDto>> Handle(UpdatePoliticaDeIdentidadCommand request, CancellationToken ct)
    {
        var guard = await Authz.EnsureTenantAdminOrMasterAsync(currentUser, db, request.TenantPublicId, ct);
        if (!guard.IsAllowed)
            return Result.Failure<PoliticaDeIdentidadDto>(guard.ErrorCode!, guard.Message!);

        var politica = await db.TenantIdentityPolicies.FirstOrDefaultAsync(p => p.TenantId == request.TenantPublicId, ct);
        var anterior = Dto.De(request.TenantPublicId, politica);
        if (politica is null)
        {
            politica = TenantIdentityPolicy.CreateForTenant(request.TenantPublicId);
            db.TenantIdentityPolicies.Add(politica);
        }

        politica.Cambiar(request.TiposAceptados, request.DocumentoExigidoDesde, request.IntentosAlActivar);
        await db.SaveChangesAsync(ct);

        var nueva = Dto.De(request.TenantPublicId, politica);
        await auditoria.EnCooperativaAsync(
            request.TenantPublicId, AuditEventTypes.IdentityPolicyChanged, currentUser.CentralUserId!.Value, currentUser.Email,
            nameof(TenantIdentityPolicy), politica.PublicId,
            new { anterior.TiposAceptados, anterior.DocumentoExigidoDesde, anterior.IntentosAlActivar },
            new { nueva.TiposAceptados, nueva.DocumentoExigidoDesde, nueva.IntentosAlActivar },
            clock.UtcNow, ct);
        return Result.Success(nueva);
    }
}

internal static class Dto
{
    internal static PoliticaDeIdentidadDto De(Guid tenant, TenantIdentityPolicy? politica) => new(
        tenant,
        (politica?.TiposAceptados() ?? TenantIdentityPolicy.TiposAceptadosDe(null)).Select(t => t.Codigo).ToList(),
        TiposDeDocumento.DeAcceso.Select(t => new TipoDeDocumentoDto(t.Codigo, t.Nombre, t.Prefijo, t.AdmiteLetras)).ToList(),
        politica?.DocumentRequiredFrom,
        politica?.MaxActivationAttempts ?? TenantIdentityPolicy.IntentosPorDefecto);
}
