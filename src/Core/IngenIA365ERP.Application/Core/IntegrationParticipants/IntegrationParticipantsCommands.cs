using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.IntegrationParticipants;

// Feature 013 (T0078; T16, C35, C36; contracts/api.md §21.1; data-model parte 3 §4.6): la administración del registro de
// participantes de la integración, rutas /api/core/integration-participants con Core.IntegrationParticipants.View/Manage. Las
// escrituras pasan por EscritorDelRegistroDeIntegracion (el único escritor) y llevan Idempotency-Key y motivo.

/// <summary>Una adopción tal como la manda y la ve la pantalla (§21.1, <c>adoptions</c>). (nuevo)</summary>
public sealed record AdopcionDto(string Contract, int Version, DateOnly? ValidFrom = null);

/// <summary>Un participante del registro (§21.1). <c>counts</c> dice si cuenta hoy. (nuevo)</summary>
public sealed record IntegrationParticipantDto(
    Guid PublicId,
    string Code,
    IntegrationParticipantClass Class,
    IntegrationParticipantRole Role,
    string? Destination,
    bool IsEnabled,
    bool IsTestDouble,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    IReadOnlyList<AdopcionDto> Adoptions,
    bool ImplementationAvailable,
    bool Counts);

/// <summary>Cómo se proyecta un participante, con lo que dice el registro a una fecha. (nuevo)</summary>
internal static class ProyeccionDeParticipantes
{
    public static IntegrationParticipantDto ADto(IntegrationParticipant p, IRegistroDeIntegracion registro, DateOnly fecha) => new(
        p.PublicId,
        p.Code,
        p.Class,
        p.Role,
        p.Role == IntegrationParticipantRole.MessageDestination ? p.Destination : null,
        p.IsEnabled,
        p.IsTestDouble,
        p.ValidFrom,
        p.ValidTo,
        p.Adoptions.Where(a => !a.IsDeleted).OrderBy(a => a.ContractName, StringComparer.Ordinal).ThenBy(a => a.Version)
            .Select(a => new AdopcionDto(a.ContractName, a.Version, a.ValidFrom)).ToList(),
        registro.ImplementacionDisponible(p, fecha),
        registro.Cuenta(p, fecha));
}

/// <summary>
/// Registra un participante, o habilita o inhabilita uno vigente (cierra su fila y abre otra) (§21.1, <c>POST /</c>). <c>destination</c>
/// es obligatorio con <c>role = MessageDestination</c> (C36). Errores: <c>Integration.Participant.TestDoubleInProduction</c>,
/// <c>.VersionNotSupported</c> (<c>data: { contract, supported[] }</c>), <c>.Overlaps</c>. (nuevo)
/// </summary>
public sealed record RegisterIntegrationParticipantCommand(
    string Code,
    IntegrationParticipantClass Class,
    IntegrationParticipantRole Role,
    string? Destination,
    bool IsEnabled,
    bool IsTestDouble,
    IReadOnlyList<AdopcionDto> Adoptions,
    DateOnly ValidFrom,
    string Reason) : IRequest<Result<IntegrationParticipantDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterIntegrationParticipantCommandValidator : ValidadorConMotivo<RegisterIntegrationParticipantCommand>
{
    private static readonly string[] DestinosConocidos =
    [
        IntegrationDestinations.Accounting, IntegrationDestinations.Lending, IntegrationDestinations.Inventory,
        IntegrationDestinations.Treasury, IntegrationDestinations.Receipts,
    ];

    public RegisterIntegrationParticipantCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("Indique el código del participante.").MaximumLength(10)
            .Matches("^[A-Za-z0-9]+$").WithMessage("El código es alfanumérico, sin espacios.");
        RuleFor(x => x.Class).IsInEnum();
        RuleFor(x => x.Role).IsInEnum();
        RuleFor(x => x.Destination)
            .NotEmpty().When(x => x.Role == IntegrationParticipantRole.MessageDestination)
            .WithMessage("Un destino de mensajes dice qué destino de la bandeja consume (destination).")
            .Must(d => d is null || DestinosConocidos.Contains(d.Trim(), StringComparer.Ordinal))
            .When(x => x.Role == IntegrationParticipantRole.MessageDestination)
            .WithMessage($"El destino es uno de {string.Join(", ", DestinosConocidos)}.");
        RuleFor(x => x.Adoptions).NotNull();
        RuleForEach(x => x.Adoptions).ChildRules(a =>
        {
            a.RuleFor(x => x.Contract).NotEmpty().MaximumLength(60);
            a.RuleFor(x => x.Version).InclusiveBetween(1, short.MaxValue);
        });
        RuleFor(x => x.ValidFrom).NotEqual(default(DateOnly)).WithMessage("Indique desde cuándo rige.");
    }
}

public sealed class RegisterIntegrationParticipantCommandHandler(
    IApplicationDbContext db,
    EscritorDelRegistroDeIntegracion escritor,
    IRegistroDeIntegracion registro,
    IDateTimeService reloj) : IRequestHandler<RegisterIntegrationParticipantCommand, Result<IntegrationParticipantDto>>
{
    public async Task<Result<IntegrationParticipantDto>> Handle(RegisterIntegrationParticipantCommand request, CancellationToken ct)
    {
        var r = await escritor.RegistrarAsync(new NuevoParticipante(
            request.Code, request.Class, request.Role, request.Destination, request.IsEnabled, request.IsTestDouble,
            request.Adoptions.Select(a => new VersionAdoptada(a.Contract.Trim(), a.Version)).ToList(),
            request.ValidFrom, request.Reason), ct);
        if (r.IsFailure) return Result.Failure<IntegrationParticipantDto>(r.Error);

        await db.SaveChangesAsync(ct);
        return Result.Success(ProyeccionDeParticipantes.ADto(r.Value, registro, reloj.HoyLocal));
    }
}

/// <summary>
/// Retira un participante: cierra su fila en <see cref="ValidTo"/> (§21.1, <c>POST /{id}/retire</c>). El destino <c>Lending</c> de Cartera
/// activada no se retira (<c>Integration.Participant.InUse</c>): mientras no exista la configuración de Cartera, «activada» es su fila
/// habilitada y vigente, que sólo abre la activación. (nuevo)
/// </summary>
public sealed record RetireIntegrationParticipantCommand(Guid ParticipantPublicId, DateOnly ValidTo, string Reason)
    : IRequest<Result<IntegrationParticipantDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class RetireIntegrationParticipantCommandValidator : ValidadorConMotivo<RetireIntegrationParticipantCommand>
{
    public RetireIntegrationParticipantCommandValidator()
    {
        RuleFor(x => x.ParticipantPublicId).NotEqual(Guid.Empty);
        RuleFor(x => x.ValidTo).NotEqual(default(DateOnly)).WithMessage("Indique hasta cuándo rige.");
    }
}

public sealed class RetireIntegrationParticipantCommandHandler(
    IApplicationDbContext db,
    EscritorDelRegistroDeIntegracion escritor,
    IRegistroDeIntegracion registro,
    IDateTimeService reloj) : IRequestHandler<RetireIntegrationParticipantCommand, Result<IntegrationParticipantDto>>
{
    public async Task<Result<IntegrationParticipantDto>> Handle(RetireIntegrationParticipantCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var actual = await db.IntegrationParticipants.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PublicId == request.ParticipantPublicId && !p.IsDeleted, ct);
        if (actual is { Role: IntegrationParticipantRole.MessageDestination, Destination: IntegrationDestinations.Lending, IsEnabled: true }
            && actual.VigenteEn(hoy))
        {
            return Result.Failure<IntegrationParticipantDto>(ErroresDeIntegracion.ParticipanteEnUso(actual.Code, actual.Destination));
        }

        var r = await escritor.RetirarAsync(request.ParticipantPublicId, request.ValidTo, ct);
        if (r.IsFailure) return Result.Failure<IntegrationParticipantDto>(r.Error);

        await db.SaveChangesAsync(ct);
        return Result.Success(ProyeccionDeParticipantes.ADto(r.Value, registro, hoy));
    }
}

/// <summary>
/// Los participantes del registro (§21.1, <c>GET /?class=&amp;role=&amp;destination=&amp;activeOn=</c>), con
/// <c>implementationAvailable</c> y <c>counts</c> a la fecha. Sin <see cref="ActiveOn"/> trae toda la historia. (nuevo)
/// </summary>
public sealed record ListIntegrationParticipantsQuery(
    IntegrationParticipantClass? Class = null,
    IntegrationParticipantRole? Role = null,
    string? Destination = null,
    DateOnly? ActiveOn = null) : IRequest<Result<IReadOnlyList<IntegrationParticipantDto>>>;

public sealed class ListIntegrationParticipantsQueryValidator : AbstractValidator<ListIntegrationParticipantsQuery>
{
    public ListIntegrationParticipantsQueryValidator()
    {
        RuleFor(x => x.Class).IsInEnum().When(x => x.Class is not null);
        RuleFor(x => x.Role).IsInEnum().When(x => x.Role is not null);
        RuleFor(x => x.Destination).MaximumLength(20);
    }
}

public sealed class ListIntegrationParticipantsQueryHandler(
    IApplicationDbContext db,
    IRegistroDeIntegracion registro,
    IDateTimeService reloj) : IRequestHandler<ListIntegrationParticipantsQuery, Result<IReadOnlyList<IntegrationParticipantDto>>>
{
    public async Task<Result<IReadOnlyList<IntegrationParticipantDto>>> Handle(ListIntegrationParticipantsQuery request, CancellationToken ct)
    {
        var consulta = db.IntegrationParticipants.AsNoTracking().Include(p => p.Adoptions).Where(p => !p.IsDeleted);
        if (request.Class is { } clase) consulta = consulta.Where(p => p.Class == clase);
        if (request.Role is { } rol) consulta = consulta.Where(p => p.Role == rol);
        if (!string.IsNullOrWhiteSpace(request.Destination)) consulta = consulta.Where(p => p.Destination == request.Destination);
        if (request.ActiveOn is { } dia) consulta = consulta.Where(p => p.ValidFrom <= dia && (p.ValidTo == null || p.ValidTo >= dia));

        var fecha = request.ActiveOn ?? reloj.HoyLocal;
        var filas = await consulta.ToListAsync(ct);
        return Result.Success<IReadOnlyList<IntegrationParticipantDto>>(filas
            .OrderBy(p => p.Code, StringComparer.Ordinal).ThenBy(p => p.Role).ThenBy(p => p.Destination, StringComparer.Ordinal).ThenBy(p => p.ValidFrom)
            .Select(p => ProyeccionDeParticipantes.ADto(p, registro, fecha))
            .ToList());
    }
}
