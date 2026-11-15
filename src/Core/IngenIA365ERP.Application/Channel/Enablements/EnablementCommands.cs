using FluentValidation;
using IngenIA365ERP.Application.Channel.Common;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Common.Identidad;
using IngenIA365ERP.Domain.Entities.Channel;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Channel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Channel.Enablements;

// Feature 021 (T044–T047; FR-010, FR-011, FR-017, FR-026; research R8): quién puede usar el canal en la cooperativa. La habilitación
// apunta a la persona y no copia datos personales; cada cambio deja un ChangeSequence creciente que el canal usa de cursor. La política
// «todo asociado activo» no se calcula al leer: la materializa PoliticaDeHabilitacion en filas reales (al encender el canal y en cada
// conciliación que pide el canal), así el espejo sólo ve filas.

public sealed record ChannelEnablementDto(
    Guid PublicId,
    Guid PersonPublicId,
    string FullName,
    string? DocumentType,
    string Document,
    EnablementStatus Status,
    DateTime EnabledAt,
    EnablementSource Source,
    string? DisabledReason,
    DateTime? DisabledAt);

/// <summary>Las reglas comunes de las habilitaciones.</summary>
public static class HabilitacionesDelCanal
{
    public const string PersonaInexistente = "Channel.Enablement.PersonNotFound";
    public const string NoEsAsociado = "Channel.Enablement.NotAssociate";
    public const string NoExiste = "Channel.Enablement.NotFound";
    public const string MotivoDeRetiro = "Retiro del asociado";

    private static long _ultimo;

    /// <summary>
    /// El cursor de un cambio: los ticks UTC del momento, estrictamente crecientes en el proceso. Dos réplicas pueden repetir o
    /// cruzar valores y una transacción puede confirmarse después de otra con un valor mayor: por eso el canal pide con una ventana de
    /// solape y sus inserciones son idempotentes (research R8).
    /// </summary>
    public static long SiguienteSecuencia(DateTime ahoraUtc)
    {
        var propuesto = ahoraUtc.Ticks;
        while (true)
        {
            var anterior = Interlocked.Read(ref _ultimo);
            var siguiente = Math.Max(propuesto, anterior + 1);
            if (Interlocked.CompareExchange(ref _ultimo, siguiente, anterior) == anterior) return siguiente;
        }
    }

    public static void Habilitar(ChannelEnablement h, DateTime ahora, EnablementSource origen)
    {
        h.Status = EnablementStatus.Enabled;
        h.EnabledAt = ahora;
        h.Source = origen;
        h.DisabledReason = null;
        h.DisabledAt = null;
        h.ChangeSequence = SiguienteSecuencia(ahora);
    }

    public static void Inhabilitar(ChannelEnablement h, DateTime ahora, string motivo)
    {
        h.Status = EnablementStatus.Disabled;
        h.DisabledReason = motivo;
        h.DisabledAt = ahora;
        h.ChangeSequence = SiguienteSecuencia(ahora);
    }

    /// <summary>¿La persona es asociada activa en la cooperativa?</summary>
    public static Task<bool> EsAsociadoActivoAsync(IApplicationDbContext db, int personaId, CancellationToken ct) =>
        db.Associates.AsNoTracking().AnyAsync(a => a.PersonId == personaId && a.Status == EstadoActivo, ct);

    public const string EstadoActivo = "A";

    internal static ChannelEnablementDto Dto(ChannelEnablement h, Person p) => new(
        h.PublicId, p.PublicId, NombreDePersona.Completo(p), TiposDeDocumento.PorCodigoDePersona(p.IdType)?.Codigo, p.TaxId ?? string.Empty,
        h.Status, h.EnabledAt, h.Source, h.DisabledReason, h.DisabledAt);
}

/// <summary>
/// FR-010: materializa «todo asociado activo» en filas. Habilita a los asociados activos sin habilitación viva y, entre las habilitaciones
/// que puso la política, inhabilita las de quienes ya no son asociados activos. Las manuales y las de la carga masiva no las toca: la
/// cooperativa decidió por esa persona. No guarda: el llamador confirma en su unidad de trabajo.
/// </summary>
public sealed class PoliticaDeHabilitacion(IApplicationDbContext db)
{
    public sealed record Resultado(int Habilitadas, int Inhabilitadas);

    public async Task<Resultado> MaterializarAsync(ChannelSettings configuracion, DateTime ahora, CancellationToken ct)
    {
        if (!configuracion.IsEnabled || configuracion.EnablementPolicy == EnablementPolicy.Manual) return new Resultado(0, 0);

        // Sin AsNoTracking: en EF Core vale para TODA la consulta aunque vaya en una subconsulta, y las habilitaciones que se
        // inhabilitan abajo quedarían sin seguimiento y no se guardarían.
        var activos = db.Associates.Where(a => a.Status == HabilitacionesDelCanal.EstadoActivo).Select(a => a.PersonId);

        var sinHabilitacion = await activos.Distinct()
            .Where(p => !db.ChannelEnablements.Any(h => h.PersonId == p))
            .ToListAsync(ct);
        foreach (var persona in sinHabilitacion)
        {
            var h = new ChannelEnablement { PersonId = persona };
            HabilitacionesDelCanal.Habilitar(h, ahora, EnablementSource.Policy);
            db.ChannelEnablements.Add(h);
        }

        var retiradas = await db.ChannelEnablements.AsTracking()
            .Where(h => h.Source == EnablementSource.Policy && h.Status == EnablementStatus.Enabled && !activos.Contains(h.PersonId))
            .ToListAsync(ct);
        foreach (var h in retiradas) HabilitacionesDelCanal.Inhabilitar(h, ahora, HabilitacionesDelCanal.MotivoDeRetiro);

        return new Resultado(sinHabilitacion.Count, retiradas.Count);
    }
}

// ----------------------------------------------------------------------------------------------------------- lista --

public sealed record ListChannelEnablementsQuery(EnablementStatus? Status = null, string? Search = null, int Page = 1, int PageSize = 50)
    : IRequest<Result<PaginaDeHabilitaciones>>;

public sealed record PaginaDeHabilitaciones(IReadOnlyList<ChannelEnablementDto> Items, int Total, int Page, int PageSize);

public sealed class ListChannelEnablementsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListChannelEnablementsQuery, Result<PaginaDeHabilitaciones>>
{
    public async Task<Result<PaginaDeHabilitaciones>> Handle(ListChannelEnablementsQuery r, CancellationToken ct)
    {
        var tamano = Math.Clamp(r.PageSize, 1, 200);
        var pagina = Math.Max(1, r.Page);
        var consulta = from h in db.ChannelEnablements.AsNoTracking()
                       join p in db.People.AsNoTracking().IgnoreQueryFilters() on h.PersonId equals p.Id
                       select new { h, p };
        if (r.Status is { } estado) consulta = consulta.Where(x => x.h.Status == estado);
        if (!string.IsNullOrWhiteSpace(r.Search))
        {
            var texto = r.Search.Trim();
            consulta = consulta.Where(x => x.p.TaxId!.Contains(texto) || x.p.FirstName!.Contains(texto) || x.p.LastName!.Contains(texto)
                || (x.p.BusinessName != null && x.p.BusinessName.Contains(texto)));
        }
        var total = await consulta.CountAsync(ct);
        var filas = await consulta.OrderByDescending(x => x.h.EnabledAt).Skip((pagina - 1) * tamano).Take(tamano).ToListAsync(ct);
        return Result.Success(new PaginaDeHabilitaciones(filas.Select(x => HabilitacionesDelCanal.Dto(x.h, x.p)).ToList(), total, pagina, tamano));
    }
}

// --------------------------------------------------------------------------------------------- habilitar e inhabilitar --

/// <summary>FR-011: habilita a una persona (o la rehabilita, con la fecha de hoy: deja de ser la «primera» si lo era).</summary>
public sealed record EnableChannelPersonCommand(Guid PersonPublicId) : IRequest<Result<ChannelEnablementDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class EnableChannelPersonCommandValidator : AbstractValidator<EnableChannelPersonCommand>
{
    public EnableChannelPersonCommandValidator() => RuleFor(x => x.PersonPublicId).NotEmpty().WithMessage("Elige la persona.");
}

public sealed class EnableChannelPersonCommandHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<EnableChannelPersonCommand, Result<ChannelEnablementDto>>
{
    public async Task<Result<ChannelEnablementDto>> Handle(EnableChannelPersonCommand r, CancellationToken ct)
    {
        var persona = await db.People.FirstOrDefaultAsync(p => p.PublicId == r.PersonPublicId, ct);
        if (persona is null) return Result.Failure<ChannelEnablementDto>(HabilitacionesDelCanal.PersonaInexistente, "La persona no existe.");

        var ahora = reloj.UtcNow;
        var configuracion = await ConfiguracionDelCanal.VigenteAsync(db, ahora, ct);
        if (configuracion is not { AllowNonAssociates: true } && !await HabilitacionesDelCanal.EsAsociadoActivoAsync(db, persona.Id, ct))
            return Result.Failure<ChannelEnablementDto>(HabilitacionesDelCanal.NoEsAsociado,
                "La persona no es asociada activa y la cooperativa no habilita a no asociados en el canal (configuración del canal).");

        var h = await db.ChannelEnablements.FirstOrDefaultAsync(x => x.PersonId == persona.Id, ct);
        if (h is { Status: EnablementStatus.Enabled }) return Result.Success(HabilitacionesDelCanal.Dto(h, persona));
        if (h is null)
        {
            h = new ChannelEnablement { PersonId = persona.Id };
            db.ChannelEnablements.Add(h);
        }
        HabilitacionesDelCanal.Habilitar(h, ahora, EnablementSource.Manual);
        await db.SaveChangesAsync(ct);
        return Result.Success(HabilitacionesDelCanal.Dto(h, persona));
    }
}

/// <summary>FR-011, FR-026: inhabilita con motivo. La persona deja de ver la información de esta cooperativa desde su siguiente petición.</summary>
public sealed record DisableChannelPersonCommand(Guid PersonPublicId, string Reason) : IRequest<Result<ChannelEnablementDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class DisableChannelPersonCommandValidator : AbstractValidator<DisableChannelPersonCommand>
{
    public DisableChannelPersonCommandValidator()
    {
        RuleFor(x => x.PersonPublicId).NotEmpty().WithMessage("Elige la persona.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Escribe el motivo de la inhabilitación.").MaximumLength(300);
    }
}

public sealed class DisableChannelPersonCommandHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<DisableChannelPersonCommand, Result<ChannelEnablementDto>>
{
    public async Task<Result<ChannelEnablementDto>> Handle(DisableChannelPersonCommand r, CancellationToken ct)
    {
        var persona = await db.People.FirstOrDefaultAsync(p => p.PublicId == r.PersonPublicId, ct);
        if (persona is null) return Result.Failure<ChannelEnablementDto>(HabilitacionesDelCanal.PersonaInexistente, "La persona no existe.");
        var h = await db.ChannelEnablements.FirstOrDefaultAsync(x => x.PersonId == persona.Id, ct);
        if (h is null) return Result.Failure<ChannelEnablementDto>(HabilitacionesDelCanal.NoExiste, "La persona no está habilitada en el canal.");
        if (h.Status == EnablementStatus.Disabled) return Result.Success(HabilitacionesDelCanal.Dto(h, persona));
        HabilitacionesDelCanal.Inhabilitar(h, reloj.UtcNow, r.Reason.Trim());
        await db.SaveChangesAsync(ct);
        return Result.Success(HabilitacionesDelCanal.Dto(h, persona));
    }
}

// ------------------------------------------------------------------------------------------- servicio: canal → ERP --

/// <summary>Un cambio de habilitación para el espejo del canal (contracts/erp-api-de-servicio.md). El estado va como texto.</summary>
public sealed record EnablementChangeDto(Guid PersonPublicId, string? DocumentType, string DocumentNumber, string Status, DateTime EnabledAt, long Sequence);

public sealed record EnablementChangesPage(IReadOnlyList<EnablementChangeDto> Items, long Next, bool HasMore);

/// <summary>R8: los cambios de habilitación con cursor mayor que <paramref name="Since"/>, en orden.</summary>
public sealed record ListEnablementChangesQuery(long Since, int Limit = 500) : IRequest<Result<EnablementChangesPage>>;

public sealed class ListEnablementChangesQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListEnablementChangesQuery, Result<EnablementChangesPage>>
{
    public async Task<Result<EnablementChangesPage>> Handle(ListEnablementChangesQuery r, CancellationToken ct)
    {
        var limite = Math.Clamp(r.Limit, 1, 1000);
        var filas = await (from h in db.ChannelEnablements.AsNoTracking()
                           join p in db.People.AsNoTracking().IgnoreQueryFilters() on h.PersonId equals p.Id
                           where h.ChangeSequence > r.Since
                           orderby h.ChangeSequence
                           select new { h.ChangeSequence, h.Status, h.EnabledAt, p.PublicId, p.IdType, p.TaxId, PersonaBorrada = p.IsDeleted })
            .Take(limite + 1).ToListAsync(ct);
        var hayMas = filas.Count > limite;
        var pagina = filas.Take(limite).Select(f => new EnablementChangeDto(
            f.PublicId,
            TiposDeDocumento.PorCodigoDePersona(f.IdType)?.Codigo,
            f.TaxId ?? string.Empty,
            f.Status == EnablementStatus.Enabled && !f.PersonaBorrada ? "Enabled" : "Disabled",
            f.EnabledAt,
            f.ChangeSequence)).ToList();
        return Result.Success(new EnablementChangesPage(pagina, pagina.Count > 0 ? pagina[^1].Sequence : r.Since, hayMas));
    }
}

/// <summary>R8: el canal pide conciliar la política antes de arrastrar (idempotente). Sin canal encendido no hace nada.</summary>
public sealed record ReconcileChannelEnablementsCommand : IRequest<Result<ConciliacionDeHabilitacionesDto>>
{
}

public sealed record ConciliacionDeHabilitacionesDto(int Enabled, int Disabled);

public sealed class ReconcileChannelEnablementsCommandValidator : AbstractValidator<ReconcileChannelEnablementsCommand>
{
}

public sealed class ReconcileChannelEnablementsCommandHandler(IApplicationDbContext db, IDateTimeService reloj, PoliticaDeHabilitacion politica)
    : IRequestHandler<ReconcileChannelEnablementsCommand, Result<ConciliacionDeHabilitacionesDto>>
{
    public async Task<Result<ConciliacionDeHabilitacionesDto>> Handle(ReconcileChannelEnablementsCommand request, CancellationToken ct)
    {
        var ahora = reloj.UtcNow;
        var configuracion = await ConfiguracionDelCanal.VigenteAsync(db, ahora, ct);
        if (configuracion is null) return Result.Success(new ConciliacionDeHabilitacionesDto(0, 0));
        var resultado = await politica.MaterializarAsync(configuracion, ahora, ct);
        if (resultado.Habilitadas + resultado.Inhabilitadas > 0) await db.SaveChangesAsync(ct);
        return Result.Success(new ConciliacionDeHabilitacionesDto(resultado.Habilitadas, resultado.Inhabilitadas));
    }
}
