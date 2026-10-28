using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core.GoLive;
using IngenIA365ERP.Domain.Enums.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.GoLive;

// Feature 015, E-06 (T0033; R-28; FR-227; contracts/api.md §14; data-model §16.2): la lista de go/no-go de una salida en vivo, verificable en el
// sistema. Cada módulo aporta sus condiciones automáticas por IVerificacionDeSalida; las manuales las marca una persona con su evidencia. Un rojo
// nunca se oculta: decidir «Go» con un rojo exige que el ítem tenga su excepción aprobada (Platform.GoLive.Override) con motivo; «PartialGo»
// exige el escenario escrito (P15). Las escrituras llevan Idempotency-Key y van a la cadena de sellos (módulo GoLive).

/// <summary>Lo que dice una condición automática de un módulo. (nuevo)</summary>
public sealed record ResultadoDeVerificacion(string Code, string Description, bool Ok, string Evidence);

/// <summary>
/// Una fuente de condiciones automáticas de la lista de go/no-go (feature 015, T0033; interfaces.md §4): cada módulo que sale en vivo registra la
/// suya (Recaudo y Tesorería en la fase 19; Cartera y Captaciones cuando la adopten). Sólo lee. (nuevo)
/// </summary>
public interface IVerificacionDeSalida
{
    /// <summary><c>REC</c>, <c>TES</c>, <c>CAR</c> o <c>Plataforma</c>.</summary>
    string Modulo { get; }

    Task<IReadOnlyList<ResultadoDeVerificacion>> VerificarAsync(CancellationToken ct);
}

public sealed record GoLiveChecklistItemDto(
    Guid PublicId, string Module, string Code, string Description, GoLiveItemKind Kind, GoLiveItemStatus Status, string? Evidence,
    Guid? EvidenceAttachmentPublicId, DateTime? CheckedAt, string? CheckedByName, string? OverrideApprovedByName, string? OverrideReason);

public sealed record GoLiveChecklistDto(
    Guid PublicId, string Name, string Environment, DateOnly TargetDate, GoLiveChecklistStatus Status, string? DecidedByName, DateTime? DecisionAt,
    string? DecisionReason, Guid? ScenarioAttachmentPublicId, IReadOnlyList<GoLiveChecklistItemDto> Items)
{
    /// <summary>Las condiciones en rojo sin excepción aprobada: con alguna, «Go» no se admite.</summary>
    public int RedWithoutOverride => Items.Count(i => i.Status == GoLiveItemStatus.Red && i.OverrideApprovedByName is null);
}

/// <summary>Una condición manual que se agrega al crear la lista. (nuevo)</summary>
public sealed record CondicionManual(string Module, string Code, string Description);

/// <summary>Los errores de la lista de go/no-go. (nuevo)</summary>
public static class ErroresDeGoLive
{
    public const string CodigoDecidida = "Platform.GoLive.AlreadyDecided";
    public const string CodigoRojoSinExcepcion = "Platform.GoLive.RedWithoutOverride";
    public const string CodigoEscenarioRequerido = "Platform.GoLive.ScenarioRequired";
    public const string CodigoNoEstaEnRojo = "Platform.GoLive.ItemNotRed";
    public const string CodigoEvidenciaRequerida = "Platform.GoLive.EvidenceRequired";
    public const string CodigoCodigoRepetido = "Platform.GoLive.DuplicateCode";

    public static Error NoEncontrada() => new("Generic.NotFound", "Lista de go/no-go no encontrada.");

    public static Error Decidida(GoLiveChecklistStatus estado) => new ErrorConDatos(CodigoDecidida,
        "La lista ya tiene su decisión: no cambia. Abra otra lista si hay que volver a verificar.", new { status = estado.ToString() });

    public static Error RojoSinExcepcion(IReadOnlyList<string> codigos) => new ErrorConDatos(CodigoRojoSinExcepcion,
        "Hay condiciones en rojo sin la excepción aprobada: no se puede decidir «salir». Corríjalas o registre la aprobación de seguir con ellas.",
        new { items = codigos });

    public static Error EscenarioRequerido() => new(CodigoEscenarioRequerido,
        "Una salida parcial necesita el escenario escrito de lo que no sale y el plan de reversa adjuntos.");

    public static Error NoEstaEnRojo() => new(CodigoNoEstaEnRojo, "Sólo se aprueba seguir con una condición que está en rojo.");

    public static Error EvidenciaRequerida() => new(CodigoEvidenciaRequerida, "Una condición manual se marca con su evidencia: diga qué se verificó.");

    public static Error CodigoRepetido(string codigo) => new ErrorConDatos(CodigoCodigoRepetido,
        $"La condición «{codigo}» está dos veces en la lista.", new { code = codigo });
}

internal static class ProyeccionDeGoLive
{
    public static GoLiveChecklistDto ADto(GoLiveChecklist l) => new(l.PublicId, l.Name, l.Environment, l.TargetDate, l.Status, l.DecidedByName, l.DecisionAt,
        l.DecisionReason, l.ScenarioAttachmentPublicId,
        l.Items.Where(i => !i.IsDeleted).OrderBy(i => i.Module, StringComparer.Ordinal).ThenBy(i => i.Code, StringComparer.Ordinal)
            .Select(i => new GoLiveChecklistItemDto(i.PublicId, i.Module, i.Code, i.Description, i.Kind, i.Status, i.Evidence, i.EvidenceAttachmentPublicId,
                i.CheckedAt, i.CheckedByName, i.OverrideApprovedByName, i.OverrideReason))
            .ToList());

    public static Task<GoLiveChecklist?> CargarAsync(IApplicationDbContext db, Guid publicId, CancellationToken ct) =>
        db.GoLiveChecklists.Include(l => l.Items).FirstOrDefaultAsync(l => l.PublicId == publicId, ct);

    /// <summary>Vuelve a calcular las condiciones automáticas con las verificaciones de los módulos: agrega las nuevas y actualiza las que hay.</summary>
    public static async Task RecalcularAsync(GoLiveChecklist lista, IEnumerable<IVerificacionDeSalida> verificaciones, string quien, DateTime ahora, CancellationToken ct)
    {
        foreach (var verificacion in verificaciones)
        {
            foreach (var r in await verificacion.VerificarAsync(ct))
            {
                var item = lista.Items.FirstOrDefault(i => i.Module == verificacion.Modulo && i.Code == r.Code);
                if (item is null)
                {
                    item = new GoLiveChecklistItem { Module = verificacion.Modulo, Code = r.Code, Kind = GoLiveItemKind.Automatic };
                    lista.Items.Add(item);
                }
                item.Description = r.Description;
                item.Status = r.Ok ? GoLiveItemStatus.Green : GoLiveItemStatus.Red;
                item.Evidence = r.Evidence;
                item.CheckedAt = ahora;
                item.CheckedByName = quien;
            }
        }
    }

    /// <summary>Si la lista queda con rojos sin excepción, avisa a quien puede aprobar seguir (<c>Plataforma.GoLiveEnRojo</c>).</summary>
    public static async Task AvisarRojosAsync(IAlertas alertas, GoLiveChecklist lista, CancellationToken ct)
    {
        var rojos = lista.Items.Where(i => i.EnRojoSinExcepcion).Select(i => $"{i.Module}.{i.Code}").ToList();
        if (rojos.Count == 0) return;
        await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.PlataformaGoLiveEnRojo,
            $"Go/no-go «{lista.Name}» con condiciones en rojo",
            $"La salida del {lista.TargetDate:yyyy-MM-dd} tiene {rojos.Count} condición(es) en rojo: {string.Join(", ", rojos)}.",
            nameof(GoLiveChecklist), lista.PublicId, DedupKey: $"golive-rojo:{lista.PublicId:N}", ModuloDeOrigen: "Core"), ct);
    }
}

/// <summary>Crea una lista de go/no-go con las condiciones automáticas de los módulos y las manuales que se piden. (nuevo)</summary>
public sealed record CreateGoLiveChecklistCommand(string Name, string Environment, DateOnly TargetDate, IReadOnlyList<CondicionManual> ManualItems)
    : IRequest<Result<GoLiveChecklistDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateGoLiveChecklistCommandValidator : AbstractValidator<CreateGoLiveChecklistCommand>
{
    public CreateGoLiveChecklistCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Indique el nombre de la salida.").MaximumLength(150);
        RuleFor(x => x.Environment).NotEmpty().WithMessage("Indique el ambiente.").MaximumLength(30);
        RuleFor(x => x.TargetDate).NotEqual(default(DateOnly)).WithMessage("Indique la fecha prevista de la salida.");
        RuleFor(x => x.ManualItems).NotNull();
        RuleForEach(x => x.ManualItems).ChildRules(i =>
        {
            i.RuleFor(c => c.Module).NotEmpty().MaximumLength(20);
            i.RuleFor(c => c.Code).NotEmpty().MaximumLength(60);
            i.RuleFor(c => c.Description).NotEmpty().MaximumLength(300);
        });
    }
}

public sealed class CreateGoLiveChecklistCommandHandler(
    IApplicationDbContext db,
    IEnumerable<IVerificacionDeSalida> verificaciones,
    IActorActual actorActual,
    IAlertas alertas,
    IDateTimeService reloj) : IRequestHandler<CreateGoLiveChecklistCommand, Result<GoLiveChecklistDto>>
{
    public async Task<Result<GoLiveChecklistDto>> Handle(CreateGoLiveChecklistCommand request, CancellationToken ct)
    {
        var repetido = request.ManualItems.GroupBy(i => (i.Module.Trim(), i.Code.Trim())).FirstOrDefault(g => g.Count() > 1);
        if (repetido is not null) return Result.Failure<GoLiveChecklistDto>(ErroresDeGoLive.CodigoRepetido(repetido.Key.Item2));

        var actor = await actorActual.ObtenerAsync(ct);
        var lista = new GoLiveChecklist { Name = request.Name.Trim(), Environment = request.Environment.Trim(), TargetDate = request.TargetDate };
        foreach (var m in request.ManualItems)
            lista.Items.Add(new GoLiveChecklistItem
            {
                Module = m.Module.Trim(), Code = m.Code.Trim(), Description = m.Description.Trim(), Kind = GoLiveItemKind.Manual, Status = GoLiveItemStatus.Red,
            });
        await ProyeccionDeGoLive.RecalcularAsync(lista, verificaciones, actor.Name, reloj.UtcNow, ct);
        // El índice único es lista × módulo × código desde la segunda ronda del 2026-10-27 (D-h; antes lista × código, y crear la lista respondía 500 en la corrida e2e
        // de ese día): la misma condición dos veces en un módulo se rechaza aquí con su error en vez de caer en la llave duplicada al guardar; el mismo código en dos
        // módulos es válido.
        var choque = lista.Items.GroupBy(i => (i.Module, i.Code)).FirstOrDefault(g => g.Count() > 1);
        if (choque is not null) return Result.Failure<GoLiveChecklistDto>(ErroresDeGoLive.CodigoRepetido(choque.Key.Code));

        db.GoLiveChecklists.Add(lista);
        await db.SaveChangesAsync(ct);
        await ProyeccionDeGoLive.AvisarRojosAsync(alertas, lista, ct);
        return Result.Success(ProyeccionDeGoLive.ADto(lista));
    }
}

/// <summary>
/// Verifica una condición: una manual la marca la persona con su estado y su evidencia (obligatoria); una automática se vuelve a calcular
/// (las de todos los módulos). Sólo con la lista abierta. (nuevo)
/// </summary>
public sealed record CheckGoLiveItemCommand(Guid ChecklistPublicId, Guid ItemPublicId, GoLiveItemStatus? Status, string? Evidence, Guid? EvidenceAttachmentPublicId)
    : IRequest<Result<GoLiveChecklistDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CheckGoLiveItemCommandValidator : AbstractValidator<CheckGoLiveItemCommand>
{
    public CheckGoLiveItemCommandValidator()
    {
        RuleFor(x => x.ChecklistPublicId).NotEqual(Guid.Empty);
        RuleFor(x => x.ItemPublicId).NotEqual(Guid.Empty);
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
        RuleFor(x => x.Evidence).MaximumLength(2000);
    }
}

public sealed class CheckGoLiveItemCommandHandler(
    IApplicationDbContext db,
    IEnumerable<IVerificacionDeSalida> verificaciones,
    IActorActual actorActual,
    IAlertas alertas,
    IDateTimeService reloj) : IRequestHandler<CheckGoLiveItemCommand, Result<GoLiveChecklistDto>>
{
    public async Task<Result<GoLiveChecklistDto>> Handle(CheckGoLiveItemCommand request, CancellationToken ct)
    {
        var lista = await ProyeccionDeGoLive.CargarAsync(db, request.ChecklistPublicId, ct);
        var item = lista?.Items.FirstOrDefault(i => i.PublicId == request.ItemPublicId);
        if (lista is null || item is null) return Result.Failure<GoLiveChecklistDto>(ErroresDeGoLive.NoEncontrada());
        if (lista.Status != GoLiveChecklistStatus.Open) return Result.Failure<GoLiveChecklistDto>(ErroresDeGoLive.Decidida(lista.Status));

        var actor = await actorActual.ObtenerAsync(ct);
        if (item.Kind == GoLiveItemKind.Manual)
        {
            if (request.Status is not { } estado || string.IsNullOrWhiteSpace(request.Evidence))
                return Result.Failure<GoLiveChecklistDto>(ErroresDeGoLive.EvidenciaRequerida());
            item.Status = estado;
            item.Evidence = request.Evidence.Trim();
            item.EvidenceAttachmentPublicId = request.EvidenceAttachmentPublicId;
            item.CheckedAt = reloj.UtcNow;
            item.CheckedByName = actor.Name;
        }
        else
        {
            await ProyeccionDeGoLive.RecalcularAsync(lista, verificaciones, actor.Name, reloj.UtcNow, ct);
        }

        await db.SaveChangesAsync(ct);
        await ProyeccionDeGoLive.AvisarRojosAsync(alertas, lista, ct);
        return Result.Success(ProyeccionDeGoLive.ADto(lista));
    }
}

/// <summary>Aprueba seguir con una condición en rojo, con motivo (<c>Platform.GoLive.Override</c>): el rojo queda a la vista, aceptado. (nuevo)</summary>
public sealed record OverrideGoLiveItemCommand(Guid ChecklistPublicId, Guid ItemPublicId, string Reason)
    : IRequest<Result<GoLiveChecklistDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class OverrideGoLiveItemCommandValidator : ValidadorConMotivo<OverrideGoLiveItemCommand>
{
    public OverrideGoLiveItemCommandValidator()
    {
        RuleFor(x => x.ChecklistPublicId).NotEqual(Guid.Empty);
        RuleFor(x => x.ItemPublicId).NotEqual(Guid.Empty);
    }
}

public sealed class OverrideGoLiveItemCommandHandler(IApplicationDbContext db, IActorActual actorActual, IDateTimeService reloj)
    : IRequestHandler<OverrideGoLiveItemCommand, Result<GoLiveChecklistDto>>
{
    public async Task<Result<GoLiveChecklistDto>> Handle(OverrideGoLiveItemCommand request, CancellationToken ct)
    {
        var lista = await ProyeccionDeGoLive.CargarAsync(db, request.ChecklistPublicId, ct);
        var item = lista?.Items.FirstOrDefault(i => i.PublicId == request.ItemPublicId);
        if (lista is null || item is null) return Result.Failure<GoLiveChecklistDto>(ErroresDeGoLive.NoEncontrada());
        if (lista.Status != GoLiveChecklistStatus.Open) return Result.Failure<GoLiveChecklistDto>(ErroresDeGoLive.Decidida(lista.Status));
        if (item.Status != GoLiveItemStatus.Red) return Result.Failure<GoLiveChecklistDto>(ErroresDeGoLive.NoEstaEnRojo());

        var actor = await actorActual.ObtenerAsync(ct);
        item.OverrideApprovedByUserId = actor.UserId;
        item.OverrideApprovedByName = actor.Name;
        item.OverrideApprovedAt = reloj.UtcNow;
        item.OverrideReason = request.Reason.Trim();
        await db.SaveChangesAsync(ct);
        return Result.Success(ProyeccionDeGoLive.ADto(lista));
    }
}

/// <summary>
/// Decide la salida: <c>Go</c> sólo sin rojos sin excepción (<c>Platform.GoLive.RedWithoutOverride</c>), <c>PartialGo</c> con el escenario adjunto
/// (<c>Platform.GoLive.ScenarioRequired</c>), <c>NoGo</c> siempre. La decisión se toma una vez y lleva motivo. (nuevo)
/// </summary>
public sealed record DecideGoLiveCommand(Guid ChecklistPublicId, GoLiveChecklistStatus Decision, string Reason, Guid? ScenarioAttachmentPublicId)
    : IRequest<Result<GoLiveChecklistDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class DecideGoLiveCommandValidator : ValidadorConMotivo<DecideGoLiveCommand>
{
    public DecideGoLiveCommandValidator()
    {
        RuleFor(x => x.ChecklistPublicId).NotEqual(Guid.Empty);
        RuleFor(x => x.Decision).Must(d => d is GoLiveChecklistStatus.Go or GoLiveChecklistStatus.NoGo or GoLiveChecklistStatus.PartialGo)
            .WithMessage("La decisión es Go, NoGo o PartialGo.");
    }
}

public sealed class DecideGoLiveCommandHandler(IApplicationDbContext db, IActorActual actorActual, IAlertas alertas, IDateTimeService reloj)
    : IRequestHandler<DecideGoLiveCommand, Result<GoLiveChecklistDto>>
{
    public async Task<Result<GoLiveChecklistDto>> Handle(DecideGoLiveCommand request, CancellationToken ct)
    {
        var lista = await ProyeccionDeGoLive.CargarAsync(db, request.ChecklistPublicId, ct);
        if (lista is null) return Result.Failure<GoLiveChecklistDto>(ErroresDeGoLive.NoEncontrada());
        if (lista.Status != GoLiveChecklistStatus.Open) return Result.Failure<GoLiveChecklistDto>(ErroresDeGoLive.Decidida(lista.Status));

        if (request.Decision == GoLiveChecklistStatus.Go)
        {
            var rojos = lista.Items.Where(i => i.EnRojoSinExcepcion).Select(i => $"{i.Module}.{i.Code}").ToList();
            if (rojos.Count > 0)
            {
                await ProyeccionDeGoLive.AvisarRojosAsync(alertas, lista, ct);
                return Result.Failure<GoLiveChecklistDto>(ErroresDeGoLive.RojoSinExcepcion(rojos));
            }
        }
        if (request.Decision == GoLiveChecklistStatus.PartialGo && request.ScenarioAttachmentPublicId is null)
            return Result.Failure<GoLiveChecklistDto>(ErroresDeGoLive.EscenarioRequerido());

        var actor = await actorActual.ObtenerAsync(ct);
        lista.Status = request.Decision;
        lista.DecidedByUserId = actor.UserId;
        lista.DecidedByName = actor.Name;
        lista.DecisionAt = reloj.UtcNow;
        lista.DecisionReason = request.Reason.Trim();
        lista.ScenarioAttachmentPublicId = request.ScenarioAttachmentPublicId;
        await db.SaveChangesAsync(ct);
        return Result.Success(ProyeccionDeGoLive.ADto(lista));
    }
}

/// <summary>Las listas de go/no-go, de la más reciente a la más antigua. (nuevo)</summary>
public sealed record ListGoLiveChecklistsQuery : IRequest<Result<IReadOnlyList<GoLiveChecklistDto>>>;

public sealed class ListGoLiveChecklistsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListGoLiveChecklistsQuery, Result<IReadOnlyList<GoLiveChecklistDto>>>
{
    public async Task<Result<IReadOnlyList<GoLiveChecklistDto>>> Handle(ListGoLiveChecklistsQuery request, CancellationToken ct)
    {
        var listas = await db.GoLiveChecklists.AsNoTracking().Include(l => l.Items).OrderByDescending(l => l.Id).ToListAsync(ct);
        return Result.Success<IReadOnlyList<GoLiveChecklistDto>>(listas.Select(ProyeccionDeGoLive.ADto).ToList());
    }
}

/// <summary>Una lista de go/no-go con sus condiciones. (nuevo)</summary>
public sealed record GetGoLiveChecklistQuery(Guid ChecklistPublicId) : IRequest<Result<GoLiveChecklistDto>>;

public sealed class GetGoLiveChecklistQueryHandler(IApplicationDbContext db) : IRequestHandler<GetGoLiveChecklistQuery, Result<GoLiveChecklistDto>>
{
    public async Task<Result<GoLiveChecklistDto>> Handle(GetGoLiveChecklistQuery request, CancellationToken ct)
    {
        var lista = await db.GoLiveChecklists.AsNoTracking().Include(l => l.Items).FirstOrDefaultAsync(l => l.PublicId == request.ChecklistPublicId, ct);
        return lista is null ? Result.Failure<GoLiveChecklistDto>(ErroresDeGoLive.NoEncontrada()) : Result.Success(ProyeccionDeGoLive.ADto(lista));
    }
}
