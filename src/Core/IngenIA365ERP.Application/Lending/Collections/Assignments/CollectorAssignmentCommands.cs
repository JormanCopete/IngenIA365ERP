using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Collections;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Collections.Assignments;

// Feature 013, K4 (US12, T0942; api.md §14.5; data-model §26.6; FR-118): las reglas de reparto, la asignación a mano, el reparto por pedido, la
// historia de asignaciones y la agenda del gestor. (nuevo)

/// <summary>Un gestor de una regla de reparto y su peso. (nuevo)</summary>
public sealed record CollectorWeightInput(Guid UserPublicId, decimal Weight);

/// <summary>Una regla de reparto (data-model §26.6). (nuevo)</summary>
public sealed record CollectorAssignmentRuleDto(
    Guid PublicId,
    int Priority,
    AssignmentDistribution Distribution,
    bool AssignWholeDebtor,
    Guid? OfficePublicId,
    string? StageCode,
    Guid? CreditLinePublicId,
    PersonaRefDto? Paymaster,
    decimal? MinBalance,
    decimal? MaxBalance,
    IReadOnlyList<CollectorWeightDto> Collectors,
    DateOnly ValidFrom,
    DateOnly? ValidTo);

/// <summary>Un gestor de una regla, con su nombre. (nuevo)</summary>
public sealed record CollectorWeightDto(Guid UserPublicId, string Name, decimal Weight);

/// <summary>
/// Una asignación de la historia (sólo inserción): el crédito o el deudor completo, el gestor, la regla o la persona y el motivo. Sin gestor
/// (<see cref="Collector"/> nulo) es el cierre de la asignación al salir de cobranza (K4, bloque D). (nuevo)
/// </summary>
public sealed record CollectorAssignmentDto(
    Guid PublicId,
    CreditoRefDto? Loan,
    PersonaRefDto Debtor,
    UsuarioRefDto? Collector,
    DateOnly AssignedOn,
    Guid? RulePublicId,
    UsuarioRefDto? AssignedBy,
    string? Reason,
    Guid? PreviousAssignmentPublicId);

/// <summary>Lo que propone o hizo el reparto (api.md §14.5): cada unidad con su gestor, y los créditos que no tomó. (nuevo)</summary>
public sealed record DistributionDto(bool PreviewOnly, IReadOnlyList<DistributionItemDto> Assignments, IReadOnlyList<CreditoRefDto> Unassigned);

/// <summary>Una unidad del reparto: un crédito, o el deudor completo con sus créditos. (nuevo)</summary>
public sealed record DistributionItemDto(IReadOnlyList<CreditoRefDto> Loans, PersonaRefDto Debtor, UsuarioRefDto Collector, bool Kept);

/// <summary>Un compromiso del día en la agenda del gestor (api.md §14.5): <c>Promise</c>, <c>FollowUp</c>, <c>AgreementInstallment</c> o <c>Stage</c>. (nuevo)</summary>
public sealed record AgendaItemDto(string Kind, CreditoRefDto Loan, PersonaRefDto Debtor, DateOnly Due, decimal? Amount, string Detail, string Route);

/// <summary>La agenda de un gestor un día. (nuevo)</summary>
public sealed record CollectorAgendaDto(DateOnly Date, UsuarioRefDto? Collector, IReadOnlyList<AgendaItemDto> Items);

/// <summary>Las clases de compromiso de la agenda (api.md §14.5). (nuevo)</summary>
public static class ClasesDeAgenda
{
    public const string Promesa = "Promise";
    public const string Seguimiento = "FollowUp";
    public const string CuotaDeAcuerdo = "AgreementInstallment";
    public const string Etapa = "Stage";

    /// <summary>La ruta de la pantalla del crédito, para ir del compromiso al crédito.</summary>
    public static string Ruta(Guid loanPublicId) => $"/cartera/creditos/{loanPublicId}";
}

/// <summary>Lo común de las asignaciones. (nuevo)</summary>
public static class AsignacionesDeCobranza
{
    /// <summary>El motivo de una asignación que hace el ciclo diario al cambiar de etapa.</summary>
    public const string MotivoDelCiclo = "Reparto por regla al cambiar de etapa";

    /// <summary>
    /// Escribe las propuestas del reparto que no conservan al gestor: una asignación por unidad (el crédito, o el deudor completo con el crédito
    /// nulo), con la anterior vigente y el motivo. <paramref name="reglaId"/> nula y <paramref name="usuarioId"/> = una asignación a mano.
    /// </summary>
    public static int Escribir(IApplicationDbContext db, ResultadoDeReparto reparto, IReadOnlyDictionary<Guid, Loan> creditos,
        IReadOnlyDictionary<int, CollectorAssignment> vigentes, IReadOnlyDictionary<Guid, int> usuarios, IReadOnlyDictionary<Guid, int>? reglas, DateOnly dia,
        int? usuarioId, string motivo)
    {
        var escritas = 0;
        foreach (var p in reparto.Asignaciones.Where(a => !a.SeMantiene))
        {
            var lista = p.Creditos.Select(c => creditos[c]).ToList();
            var principal = lista[0];
            var anterior = vigentes.GetValueOrDefault(principal.Id);
            int? reglaId = reglas is not null && reglas.TryGetValue(p.ReglaPublicId, out var id) ? id : null;
            db.CollectorAssignments.Add(CollectorAssignment.Asignar(p.CreditoPublicId is null ? null : principal.Id, principal.DebtorPersonId,
                usuarios[p.GestorPublicId], dia, reglaId, anterior is null && reglaId is not null ? null : motivo, reglaId is null ? usuarioId : null,
                anterior is { Id: > 0 } a ? a.Id : null));
            escritas++;
        }
        return escritas;
    }

    /// <summary>Los usuarios gestores por su <c>PublicId</c> (activos, de la cooperativa).</summary>
    public static async Task<IReadOnlyDictionary<Guid, int>> UsuariosAsync(IApplicationDbContext db, IEnumerable<Guid> publicos, CancellationToken ct)
    {
        var lista = publicos.Distinct().ToList();
        return await db.Users.AsNoTracking().Where(u => lista.Contains(u.PublicId) && !u.IsDeleted && u.IsActive).ToDictionaryAsync(u => u.PublicId, u => u.Id, ct);
    }

    /// <summary>Lo que el reparto mira de cada crédito: oficina, etapa vigente, línea, pagaduría, saldo de capital y gestor vigente.</summary>
    public static async Task<IReadOnlyList<CreditoParaReparto>> CandidatosAsync(IApplicationDbContext db, IReadOnlyList<Loan> creditos,
        IReadOnlyDictionary<int, decimal> saldos, IReadOnlyDictionary<int, CollectionStageHistoryEntry> etapas,
        IReadOnlyDictionary<int, CollectorAssignment> vigentes, CancellationToken ct)
    {
        var oficinas = creditos.Select(c => c.BranchId).Distinct().ToList();
        var deOficina = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => oficinas.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.PublicId, ct);
        var ids = creditos.Select(c => c.Id).ToList();
        var pagadurias = await db.LoanPayrollLinks.AsNoTracking().Where(v => ids.Contains(v.LoanId) && v.ValidTo == null && !v.IsDeleted)
            .Select(v => new { v.LoanId, v.PaymasterPersonId }).ToListAsync(ct);
        var personas = creditos.Select(c => c.DebtorPersonId).Distinct().ToList();
        var deudores = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personas.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.PublicId, ct);
        var gestores = vigentes.Values.Where(v => v.CollectorUserId != null).Select(v => v.CollectorUserId!.Value).Distinct().ToList();
        var deGestor = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => gestores.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.PublicId, ct);
        return creditos.Select(c => new CreditoParaReparto(c.PublicId, deudores.GetValueOrDefault(c.DebtorPersonId), saldos.GetValueOrDefault(c.Id))
        {
            Oficina = deOficina.TryGetValue(c.BranchId, out var o) ? o : null,
            Etapa = etapas.GetValueOrDefault(c.Id)?.StageCode,
            Linea = c.CreditLineId,
            Pagaduria = pagadurias.FirstOrDefault(p => p.LoanId == c.Id)?.PaymasterPersonId,
            GestorActual = vigentes.TryGetValue(c.Id, out var a) && a.CollectorUserId is { } u && deGestor.TryGetValue(u, out var g) ? g : null,
        }).ToList();
    }

    /// <summary>El saldo de capital de cada crédito en su última posición diaria (lo que mira el rango de saldo de una regla).</summary>
    public static async Task<IReadOnlyDictionary<int, decimal>> SaldosAsync(IApplicationDbContext db, IReadOnlyCollection<int> loanIds, CancellationToken ct)
    {
        var ids = loanIds.ToArray();
        var posiciones = await db.LoanDailyPositions.AsNoTracking().Where(p => ids.Contains(p.LoanId))
            .GroupBy(p => p.LoanId)
            .Select(g => g.OrderByDescending(p => p.PositionDate).ThenByDescending(p => p.Version).Select(p => new { p.LoanId, p.Principal }).First())
            .ToListAsync(ct);
        return posiciones.ToDictionary(p => p.LoanId, p => p.Principal);
    }

    public static CollectorAssignmentRuleDto Dto(CollectorAssignmentRule r, IReadOnlyDictionary<int, PersonaRefDto> personas,
        IReadOnlyDictionary<int, Guid> lineas, IReadOnlyDictionary<Guid, string> nombres) => new(
        r.PublicId, r.Priority, r.Distribution, r.AssignWholeDebtor, r.OfficePublicId, r.StageCode,
        r.CreditLineId is { } l && lineas.TryGetValue(l, out var linea) ? linea : null,
        r.PaymasterPersonId is { } p && personas.TryGetValue(p, out var pagaduria) ? pagaduria : null,
        r.MinBalance, r.MaxBalance, r.Gestores().Select(g => new CollectorWeightDto(g.UsuarioPublicId, nombres.GetValueOrDefault(g.UsuarioPublicId) ?? string.Empty,
            g.Peso)).ToList(), r.ValidFrom, r.ValidTo);
}

// -------------------------------------------------------------------------------------------------------------------- reglas --

/// <summary>
/// Crea una regla de reparto (feature 013, K4, T0942; data-model §26.6, <c>LND_CollectorAssignmentRules</c>; FR-118; <c>Lending.Collections.Assign</c>,
/// con clave y motivo). api.md no trae su ruta: la propone T0956 bajo <c>/api/lending/collector-assignments/rules</c>. Criterios (oficina, etapa,
/// línea, pagaduría, rango de saldo; nulo = cualquiera), forma del reparto, deudor completo y gestores con su peso
/// (<c>Lending.CollectorAssignmentRule.Invalid</c> si no hay gestores, se repiten, un peso no es positivo o el rango está invertido); cada gestor es un
/// usuario activo (<c>Lending.Collections.CollectorNotFound</c>). (nuevo)
/// </summary>
public sealed record CreateCollectorAssignmentRuleCommand(
    int Priority,
    AssignmentDistribution Distribution,
    IReadOnlyList<CollectorWeightInput> Collectors,
    DateOnly ValidFrom,
    string Reason,
    Guid? OfficePublicId = null,
    string? StageCode = null,
    Guid? CreditLinePublicId = null,
    Guid? PaymasterPersonPublicId = null,
    decimal? MinBalance = null,
    decimal? MaxBalance = null,
    bool AssignWholeDebtor = false) : IRequest<Result<CollectorAssignmentRuleDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class CreateCollectorAssignmentRuleCommandValidator : ValidadorConMotivo<CreateCollectorAssignmentRuleCommand>
{
    public CreateCollectorAssignmentRuleCommandValidator()
    {
        RuleFor(x => x.Distribution).IsInEnum();
        RuleFor(x => x.Collectors).NotEmpty().WithMessage("La regla necesita al menos un gestor.");
        RuleFor(x => x.StageCode).MaximumLength(CollectionStage.LargoDelCodigo);
    }
}

public sealed class CreateCollectorAssignmentRuleCommandHandler(IApplicationDbContext db)
    : IRequestHandler<CreateCollectorAssignmentRuleCommand, Result<CollectorAssignmentRuleDto>>
{
    public async Task<Result<CollectorAssignmentRuleDto>> Handle(CreateCollectorAssignmentRuleCommand request, CancellationToken ct)
    {
        var usuarios = await AsignacionesDeCobranza.UsuariosAsync(db, request.Collectors.Select(c => c.UserPublicId), ct);
        if (request.Collectors.Any(c => !usuarios.ContainsKey(c.UserPublicId))) return Falla(ErroresDeCobranza.GestorInexistente());
        int? linea = null;
        if (request.CreditLinePublicId is { } lineaPublica)
        {
            linea = await db.CreditLines.AsNoTracking().Where(l => l.PublicId == lineaPublica && !l.IsDeleted).Select(l => (int?)l.Id).FirstOrDefaultAsync(ct);
            if (linea is null) return Falla(ErroresDeGobierno.LineaInexistente());
        }
        int? pagaduria = null;
        if (request.PaymasterPersonPublicId is { } pagaduriaPublica)
        {
            pagaduria = await db.People.AsNoTracking().Where(p => p.PublicId == pagaduriaPublica && !p.IsDeleted).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
            if (pagaduria is null) return Falla(ErroresDeGobierno.PersonaInexistente());
        }
        CollectorAssignmentRule regla;
        try
        {
            regla = CollectorAssignmentRule.Nueva(request.Priority, request.Distribution,
                request.Collectors.Select(c => new GestorDeRegla(c.UserPublicId, c.Weight)).ToList(), request.ValidFrom, request.OfficePublicId, request.StageCode,
                linea, pagaduria, request.MinBalance, request.MaxBalance, request.AssignWholeDebtor);
        }
        catch (ReglaDeCarteraException ex)
        {
            return Falla(ErroresDeGobierno.De(ex.Problema));
        }
        db.CollectorAssignmentRules.Add(regla);
        await db.SaveChangesAsync(ct);
        return Result.Success((await ListCollectorAssignmentRulesQueryHandler.DtosAsync(db, [regla], ct))[0]);
    }

    private static Result<CollectorAssignmentRuleDto> Falla(Error e) => Result.Failure<CollectorAssignmentRuleDto>(e);
}

/// <summary>
/// Cierra la vigencia de una regla de reparto (feature 013, K4, T0942; <c>Lending.Collections.Assign</c>, con clave y motivo): sólo cambia su
/// <c>ValidTo</c>; ya cerrada o antes de empezar, <c>Lending.CollectorAssignmentRule.ValidityInvalid</c>. Las asignaciones que hizo se conservan. (nuevo)
/// </summary>
public sealed record CloseCollectorAssignmentRuleCommand(Guid RulePublicId, DateOnly ValidTo, string Reason)
    : IRequest<Result<CollectorAssignmentRuleDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class CloseCollectorAssignmentRuleCommandValidator : ValidadorConMotivo<CloseCollectorAssignmentRuleCommand>
{
    public CloseCollectorAssignmentRuleCommandValidator() => RuleFor(x => x.RulePublicId).NotEmpty();
}

public sealed class CloseCollectorAssignmentRuleCommandHandler(IApplicationDbContext db)
    : IRequestHandler<CloseCollectorAssignmentRuleCommand, Result<CollectorAssignmentRuleDto>>
{
    public async Task<Result<CollectorAssignmentRuleDto>> Handle(CloseCollectorAssignmentRuleCommand request, CancellationToken ct)
    {
        var regla = await db.CollectorAssignmentRules.FirstOrDefaultAsync(r => r.PublicId == request.RulePublicId && !r.IsDeleted, ct);
        if (regla is null) return Result.Failure<CollectorAssignmentRuleDto>(ErroresDeCobranza.ReglaInexistente());
        try
        {
            regla.CerrarVigencia(request.ValidTo);
        }
        catch (ReglaDeCarteraException ex)
        {
            return Result.Failure<CollectorAssignmentRuleDto>(ErroresDeGobierno.De(ex.Problema));
        }
        await db.SaveChangesAsync(ct);
        return Result.Success((await ListCollectorAssignmentRulesQueryHandler.DtosAsync(db, [regla], ct))[0]);
    }
}

/// <summary>
/// Las reglas de reparto (feature 013, K4, T0942; <c>Lending.Collections.Read</c>): las vigentes a <see cref="Date"/> (hoy si no viene) o todas con
/// <see cref="IncludeClosed"/>, por prioridad. Son de la cooperativa: sin alcance de oficina. (nuevo)
/// </summary>
public sealed record ListCollectorAssignmentRulesQuery(DateOnly? Date = null, bool IncludeClosed = false) : IRequest<Result<IReadOnlyList<CollectorAssignmentRuleDto>>>;

public sealed class ListCollectorAssignmentRulesQueryValidator : AbstractValidator<ListCollectorAssignmentRulesQuery>;

public sealed class ListCollectorAssignmentRulesQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<ListCollectorAssignmentRulesQuery, Result<IReadOnlyList<CollectorAssignmentRuleDto>>>
{
    public async Task<Result<IReadOnlyList<CollectorAssignmentRuleDto>>> Handle(ListCollectorAssignmentRulesQuery request, CancellationToken ct)
    {
        var dia = request.Date ?? reloj.HoyLocal;
        var consulta = db.CollectorAssignmentRules.AsNoTracking().Where(r => !r.IsDeleted);
        if (!request.IncludeClosed) consulta = consulta.Where(r => r.ValidFrom <= dia && (r.ValidTo == null || r.ValidTo >= dia));
        var reglas = await consulta.OrderBy(r => r.Priority).ThenBy(r => r.Id).ToListAsync(ct);
        return Result.Success(await DtosAsync(db, reglas, ct));
    }

    public static async Task<IReadOnlyList<CollectorAssignmentRuleDto>> DtosAsync(IApplicationDbContext db, IReadOnlyList<CollectorAssignmentRule> reglas,
        CancellationToken ct)
    {
        var personas = await ReferenciasDeGobierno.PersonasAsync(db, reglas.Where(r => r.PaymasterPersonId != null).Select(r => r.PaymasterPersonId!.Value), ct);
        var lineaIds = reglas.Where(r => r.CreditLineId != null).Select(r => r.CreditLineId!.Value).Distinct().ToList();
        var lineas = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => lineaIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.PublicId, ct);
        var gestores = reglas.SelectMany(r => r.Gestores()).Select(g => g.UsuarioPublicId).Distinct().ToList();
        var nombres = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => gestores.Contains(u.PublicId))
            .ToDictionaryAsync(u => u.PublicId, u => u.Email ?? u.Username, ct);
        return reglas.Select(r => AsignacionesDeCobranza.Dto(r, personas, lineas, nombres)).ToList();
    }
}

// ---------------------------------------------------------------------------------------------------------- asignar a mano --

/// <summary>
/// Asigna a mano créditos a un gestor (feature 013, K4, T0942; api.md §14.5, <c>POST /api/lending/collector-assignments</c>,
/// <c>Lending.Collections.Assign</c>, con clave y motivo; FR-118): una asignación por crédito, con la anterior vigente y el motivo; la que ya es de ese
/// gestor no se repite. Cada crédito vivo y del alcance (fuera, el mismo 404); el gestor, un usuario activo. (nuevo)
/// </summary>
public sealed record AssignCollectorCommand(IReadOnlyList<Guid> LoanPublicIds, Guid CollectorUserPublicId, string Reason)
    : IRequest<Result<IReadOnlyList<CollectorAssignmentDto>>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class AssignCollectorCommandValidator : ValidadorConMotivo<AssignCollectorCommand>
{
    public AssignCollectorCommandValidator()
    {
        RuleFor(x => x.LoanPublicIds).NotEmpty().WithMessage("Indique los créditos que se asignan.");
        RuleFor(x => x.CollectorUserPublicId).NotEmpty().WithMessage("Indique el gestor.");
    }
}

public sealed class AssignCollectorCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<AssignCollectorCommand, Result<IReadOnlyList<CollectorAssignmentDto>>>
{
    public async Task<Result<IReadOnlyList<CollectorAssignmentDto>>> Handle(AssignCollectorCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var gestor = await CobranzaDeCartera.UsuarioAsync(db, request.CollectorUserPublicId, ct);
        if (gestor is null) return Falla(ErroresDeCobranza.GestorInexistente());
        var creditos = new List<Loan>();
        foreach (var publico in request.LoanPublicIds.Distinct())
        {
            var leido = await CobranzaDeCartera.CreditoAsync(db, alcance, auditoria, publico, ct);
            if (leido.IsFailure) return Falla(leido.Error);
            creditos.Add(leido.Value);
        }
        var vigentes = await CobranzaDeCartera.GestorVigenteAsync(db, creditos.Select(c => (c.Id, c.DebtorPersonId)).ToList(), ct);
        var hoy = reloj.HoyLocal;
        var nuevas = new List<CollectorAssignment>();
        foreach (var c in creditos)
        {
            var anterior = vigentes.GetValueOrDefault(c.Id);
            if (anterior?.CollectorUserId == gestor && anterior.LoanId == c.Id) continue;
            var a = CollectorAssignment.Asignar(c.Id, c.DebtorPersonId, gestor.Value, hoy, null, request.Reason, usuario,
                anterior is { Id: > 0 } previa ? previa.Id : null);
            db.CollectorAssignments.Add(a);
            nuevas.Add(a);
        }
        await db.SaveChangesAsync(ct);
        return Result.Success(await ListCollectorAssignmentsQueryHandler.DtosAsync(db, nuevas, ct));
    }

    private static Result<IReadOnlyList<CollectorAssignmentDto>> Falla(Error e) => Result.Failure<IReadOnlyList<CollectorAssignmentDto>>(e);
}

// ----------------------------------------------------------------------------------------------------------- repartir --

/// <summary>Los filtros del reparto por pedido (api.md §14.5): oficina, etapa, línea, pagaduría y rango de saldo de capital. (nuevo)</summary>
public sealed record DistributionFilters(
    Guid? OfficePublicId = null,
    string? StageCode = null,
    Guid? CreditLinePublicId = null,
    Guid? PaymasterPersonPublicId = null,
    decimal? BalanceFrom = null,
    decimal? BalanceTo = null);

/// <summary>
/// Reparte la cartera en cobranza entre gestores (feature 013, K4, T0942; api.md §14.5, <c>POST /api/lending/collector-assignments/distribute</c>,
/// <c>Lending.Collections.Assign</c>; FR-118): los créditos vivos del alcance que están en una etapa de cobranza y casan con los filtros se reparten
/// entre <see cref="CollectorUserPublicIds"/> (mismo peso) por número o por saldo con <see cref="RepartoDeGestores"/> —reproducible: el que ya tiene a
/// uno de esos gestores lo conserva—. Con <see cref="PreviewOnly"/> no escribe nada (y no exige clave); si no, escribe una asignación a mano por crédito
/// que cambia de gestor, con el motivo. (nuevo)
/// </summary>
public sealed record DistributeCollectionsCommand(
    AssignmentDistribution Basis,
    DistributionFilters Filters,
    IReadOnlyList<Guid> CollectorUserPublicIds,
    bool PreviewOnly,
    string Reason) : IRequest<Result<DistributionDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class DistributeCollectionsCommandValidator : ValidadorConMotivo<DistributeCollectionsCommand>
{
    public DistributeCollectionsCommandValidator()
    {
        RuleFor(x => x.Basis).IsInEnum();
        RuleFor(x => x.Filters).NotNull();
        RuleFor(x => x.CollectorUserPublicIds).NotEmpty().WithMessage("Indique entre qué gestores se reparte.");
    }
}

public sealed class DistributeCollectionsCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcanceDeLaPeticion,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<DistributeCollectionsCommand, Result<DistributionDto>>
{
    public async Task<Result<DistributionDto>> Handle(DistributeCollectionsCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (!request.PreviewOnly && actor.UserId is null) return Result.Failure<DistributionDto>(ErroresDeGobierno.SinUsuario());
        var usuarios = await AsignacionesDeCobranza.UsuariosAsync(db, request.CollectorUserPublicIds, ct);
        if (request.CollectorUserPublicIds.Any(u => !usuarios.ContainsKey(u))) return Result.Failure<DistributionDto>(ErroresDeCobranza.GestorInexistente());
        var f = request.Filters;
        int? linea = f.CreditLinePublicId is { } lp
            ? await db.CreditLines.AsNoTracking().Where(l => l.PublicId == lp).Select(l => (int?)l.Id).FirstOrDefaultAsync(ct) ?? -1
            : null;
        int? pagaduria = f.PaymasterPersonPublicId is { } pp
            ? await db.People.AsNoTracking().Where(p => p.PublicId == pp).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct) ?? -1
            : null;
        var regla = new ReglaDeReparto(Guid.Empty, 0, request.Basis,
            request.CollectorUserPublicIds.Distinct().Select(u => new GestorDeRegla(u, 1m)).ToList())
        {
            Oficina = f.OfficePublicId,
            Etapa = string.IsNullOrWhiteSpace(f.StageCode) ? null : f.StageCode.Trim().ToUpperInvariant(),
            Linea = linea,
            Pagaduria = pagaduria,
            SaldoMinimo = f.BalanceFrom,
            SaldoMaximo = f.BalanceTo,
        };
        if (RepartoDeGestores.Validar(regla) is { } invalida) return Result.Failure<DistributionDto>(ErroresDeGobierno.De(invalida));

        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var creditos = await db.Loans.AsNoTracking().Where(l => !l.IsDeleted && l.Status == LoanStatus.Active)
            .PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId).ToListAsync(ct);
        var ids = creditos.Select(c => c.Id).ToList();
        var etapas = await CobranzaDeCartera.EtapaVigenteAsync(db, ids, ct);
        creditos = creditos.Where(c => etapas.ContainsKey(c.Id)).ToList();
        ids = creditos.Select(c => c.Id).ToList();
        var vigentes = await CobranzaDeCartera.GestorVigenteAsync(db, creditos.Select(c => (c.Id, c.DebtorPersonId)).ToList(), ct);
        var saldos = await AsignacionesDeCobranza.SaldosAsync(db, ids, ct);
        var candidatos = await AsignacionesDeCobranza.CandidatosAsync(db, creditos, saldos, etapas, vigentes, ct);
        var casan = candidatos.Where(regla.Casa).ToList();
        var reparto = RepartoDeGestores.Repartir([regla], casan);

        var porPublico = creditos.ToDictionary(c => c.PublicId);
        if (!request.PreviewOnly)
        {
            AsignacionesDeCobranza.Escribir(db, reparto, porPublico, vigentes, usuarios, null, reloj.HoyLocal, actor.UserId, request.Reason.Trim());
            await db.SaveChangesAsync(ct);
        }

        var personas = await ReferenciasDeGobierno.PersonasAsync(db, creditos.Select(c => c.DebtorPersonId), ct);
        var nombres = await ReferenciasDeGobierno.UsuariosAsync(db, usuarios.Values, ct);
        var items = reparto.Asignaciones.Select(a =>
        {
            var lista = a.Creditos.Select(c => porPublico[c]).ToList();
            return new DistributionItemDto(lista.Select(c => new CreditoRefDto(c.PublicId, c.Number)).ToList(), personas[lista[0].DebtorPersonId],
                nombres[usuarios[a.GestorPublicId]], a.SeMantiene);
        }).ToList();
        var sinAsignar = candidatos.Where(c => !regla.Casa(c)).Select(c => porPublico[c.CreditoPublicId]).Select(c => new CreditoRefDto(c.PublicId, c.Number))
            .ToList();
        return Result.Success(new DistributionDto(request.PreviewOnly, items, sinAsignar));
    }
}

// -------------------------------------------------------------------------------------------------------- historia --

/// <summary>
/// La historia de asignaciones (feature 013, K4, T0942; api.md §14.5, <c>GET /api/lending/collector-assignments?collector=&amp;loanPublicId=&amp;page=</c>,
/// <c>Lending.Collections.Read</c>): sólo inserción, la más reciente primero, filtrada por el alcance de la oficina y la línea del crédito (las del
/// deudor completo, por las de sus créditos: sólo las ve quien tiene todas las oficinas, falla cerrado). (nuevo)
/// </summary>
public sealed record ListCollectorAssignmentsQuery(Guid? Collector = null, Guid? LoanPublicId = null, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<CollectorAssignmentDto>>>;

public sealed class ListCollectorAssignmentsQueryValidator : AbstractValidator<ListCollectorAssignmentsQuery>;

public sealed class ListCollectorAssignmentsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion)
    : IRequestHandler<ListCollectorAssignmentsQuery, Result<PagedResult<CollectorAssignmentDto>>>
{
    public async Task<Result<PagedResult<CollectorAssignmentDto>>> Handle(ListCollectorAssignmentsQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var creditos = db.Loans.AsNoTracking().PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId).Select(l => l.Id);
        var consulta = db.CollectorAssignments.AsNoTracking().Where(a => !a.IsDeleted);
        consulta = alcance.TodasLasOficinas && alcance.TodasLasLineas
            ? consulta
            : consulta.Where(a => a.LoanId != null && creditos.Contains(a.LoanId.Value));
        if (request.Collector is { } gestor)
        {
            var id = await db.Users.AsNoTracking().Where(u => u.PublicId == gestor).Select(u => (int?)u.Id).FirstOrDefaultAsync(ct) ?? -1;
            consulta = consulta.Where(a => a.CollectorUserId == id);
        }
        if (request.LoanPublicId is { } loan)
        {
            var credito = await db.Loans.AsNoTracking().Where(l => l.PublicId == loan).Select(l => new { l.Id, l.DebtorPersonId }).FirstOrDefaultAsync(ct);
            consulta = credito is null
                ? consulta.Where(_ => false)
                : consulta.Where(a => a.LoanId == credito.Id || (a.LoanId == null && a.PersonId == credito.DebtorPersonId));
        }
        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.LongCountAsync(ct);
        var filas = await consulta.OrderByDescending(a => a.AssignedOn).ThenByDescending(a => a.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        return Result.Success(new PagedResult<CollectorAssignmentDto>(await DtosAsync(db, filas, ct), pagina.SafePage, pagina.SafePageSize, total));
    }

    public static async Task<IReadOnlyList<CollectorAssignmentDto>> DtosAsync(IApplicationDbContext db, IReadOnlyList<CollectorAssignment> filas,
        CancellationToken ct)
    {
        var loanIds = filas.Where(f => f.LoanId != null).Select(f => f.LoanId!.Value).Distinct().ToList();
        var creditos = await db.Loans.AsNoTracking().IgnoreQueryFilters().Where(l => loanIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => new CreditoRefDto(l.PublicId, l.Number), ct);
        var personas = await ReferenciasDeGobierno.PersonasAsync(db, filas.Select(f => f.PersonId), ct);
        var usuarios = await ReferenciasDeGobierno.UsuariosAsync(db, filas.Select(f => f.CollectorUserId).Concat(filas.Select(f => f.ActorUserId)), ct);
        var reglaIds = filas.Where(f => f.CollectorAssignmentRuleId != null).Select(f => f.CollectorAssignmentRuleId!.Value).Distinct().ToList();
        var reglas = await db.CollectorAssignmentRules.AsNoTracking().IgnoreQueryFilters().Where(r => reglaIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.PublicId, ct);
        var anterioresIds = filas.Where(f => f.PreviousAssignmentId != null).Select(f => f.PreviousAssignmentId!.Value).Distinct().ToList();
        var anteriores = await db.CollectorAssignments.AsNoTracking().IgnoreQueryFilters().Where(a => anterioresIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.PublicId, ct);
        return filas.Select(f => new CollectorAssignmentDto(f.PublicId,
            f.LoanId is { } l && creditos.TryGetValue(l, out var c) ? c : null, personas[f.PersonId], ReferenciasDeGobierno.Usuario(usuarios, f.CollectorUserId), f.AssignedOn,
            f.CollectorAssignmentRuleId is { } r && reglas.TryGetValue(r, out var regla) ? regla : null,
            f.ActorUserId is { } u && usuarios.TryGetValue(u, out var quien) ? quien : null, f.Reason,
            f.PreviousAssignmentId is { } p && anteriores.TryGetValue(p, out var anterior) ? anterior : null)).ToList();
    }
}

// ---------------------------------------------------------------------------------------------------------- agenda --

/// <summary>
/// La agenda del gestor un día (feature 013, K4, T0942; api.md §14.5, <c>GET /api/lending/agenda?date=&amp;collector=</c>, <c>Lending.Collections.Read</c>;
/// FR-118, FR-119): sobre los créditos que el gestor tiene asignados hoy y están en el alcance —sin <see cref="Collector"/>, los de quien pregunta—,
/// las promesas que vencen ese día (<c>Promise</c>), las promesas incumplidas que esperan una gestión nueva (<c>FollowUp</c>: «vuelve a la agenda»),
/// las cuotas de acuerdos aprobados que vencen ese día (<c>AgreementInstallment</c>) y los créditos que entraron a una etapa ese día (<c>Stage</c>). Es
/// una consulta: no guarda nada (data-model §26.6). (nuevo)
/// </summary>
public sealed record GetCollectorAgendaQuery(DateOnly? Date = null, Guid? Collector = null) : IRequest<Result<CollectorAgendaDto>>;

public sealed class GetCollectorAgendaQueryValidator : AbstractValidator<GetCollectorAgendaQuery>;

public sealed class GetCollectorAgendaQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<GetCollectorAgendaQuery, Result<CollectorAgendaDto>>
{
    public async Task<Result<CollectorAgendaDto>> Handle(GetCollectorAgendaQuery request, CancellationToken ct)
    {
        var dia = request.Date ?? reloj.HoyLocal;
        int? gestor;
        if (request.Collector is { } publico) gestor = await db.Users.AsNoTracking().Where(u => u.PublicId == publico).Select(u => (int?)u.Id).FirstOrDefaultAsync(ct);
        else gestor = (await actorActual.ObtenerAsync(ct)).UserId;
        if (gestor is null) return Result.Success(new CollectorAgendaDto(dia, null, []));

        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var creditos = await db.Loans.AsNoTracking().Where(l => !l.IsDeleted && (l.Status == LoanStatus.Active || l.Status == LoanStatus.WrittenOff))
            .PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId).ToListAsync(ct);
        var vigentes = await CobranzaDeCartera.GestorVigenteAsync(db, creditos.Select(c => (c.Id, c.DebtorPersonId)).ToList(), ct);
        var suyos = creditos.Where(c => vigentes.TryGetValue(c.Id, out var a) && a.CollectorUserId == gestor).ToDictionary(c => c.Id);
        var ids = suyos.Keys.ToList();
        var personas = await ReferenciasDeGobierno.PersonasAsync(db, suyos.Values.Select(c => c.DebtorPersonId), ct);
        var items = new List<AgendaItemDto>();
        AgendaItemDto Item(string clase, Loan c, DateOnly vence, decimal? valor, string detalle) =>
            new(clase, new CreditoRefDto(c.PublicId, c.Number), personas[c.DebtorPersonId], vence, valor, detalle, ClasesDeAgenda.Ruta(c.PublicId));

        // Promesas: las que vencen hoy (pendientes) y las incumplidas sin gestión posterior (vuelven a la agenda).
        var promesas = await (from p in db.PaymentPromises.AsNoTracking()
                              join a in db.CollectionActions.AsNoTracking() on p.CollectionActionId equals a.Id
                              where ids.Contains(p.LoanId) && !p.IsDeleted && p.PromisedDate <= dia
                              select new { Promesa = p, a.OccurredAt, Anulada = db.CollectionActions.Any(x => x.VoidsActionId == a.Id) }).ToListAsync(ct);
        var pagos = await CobranzaDeCartera.PagosVigentesAsync(db, promesas.Select(p => p.Promesa.LoanId).Distinct().ToList(), ct);
        var desfase = reloj.AhoraLocal.Offset;
        var ultimaGestion = await db.CollectionActions.AsNoTracking().Where(a => a.LoanId != null && ids.Contains(a.LoanId.Value) && !a.IsDeleted)
            .GroupBy(a => a.LoanId!.Value).Select(g => new { LoanId = g.Key, Ultima = g.Max(a => a.OccurredAt) }).ToDictionaryAsync(x => x.LoanId, x => x.Ultima, ct);
        foreach (var p in promesas.Where(p => !p.Anulada))
        {
            var estado = EstadoDePromesa.Calcular(p.Promesa.ParaEstado(p.OccurredAt, false), pagos.GetValueOrDefault(p.Promesa.LoanId) ?? [], dia);
            var c = suyos[p.Promesa.LoanId];
            if (p.Promesa.PromisedDate == dia && estado.Estado == PaymentPromiseStatus.Pending)
                items.Add(Item(ClasesDeAgenda.Promesa, c, dia, p.Promesa.PromisedAmount, $"Promesa de pago por {ErroresDeCobranza.Pesos(p.Promesa.PromisedAmount)}"));
            else if (estado.Estado == PaymentPromiseStatus.Broken
                     && ultimaGestion.GetValueOrDefault(p.Promesa.LoanId) < DateTime.SpecifyKind(p.Promesa.PromisedDate.AddDays(1).ToDateTime(TimeOnly.MinValue) - desfase,
                         DateTimeKind.Utc))
                items.Add(Item(ClasesDeAgenda.Seguimiento, c, p.Promesa.PromisedDate, p.Promesa.PromisedAmount,
                    $"Promesa incumplida del {ErroresDeCobranza.Fecha(p.Promesa.PromisedDate)}: gestione de nuevo"));
        }

        // Cuotas de acuerdos aprobados que vencen hoy.
        var cuotas = await (from i in db.PaymentAgreementInstallments.AsNoTracking()
                            join a in db.PaymentAgreements.AsNoTracking() on i.PaymentAgreementId equals a.Id
                            where ids.Contains(a.LoanId) && a.Status == PaymentAgreementStatus.Approved && i.DueDate == dia && !a.IsDeleted
                            select new { a.LoanId, Acuerdo = a.Number, Cuota = i.Number, i.Amount }).ToListAsync(ct);
        foreach (var q in cuotas)
            items.Add(Item(ClasesDeAgenda.CuotaDeAcuerdo, suyos[q.LoanId], dia, q.Amount, $"Cuota {q.Cuota} del acuerdo {q.Acuerdo}"));

        // Cambios de etapa del día.
        var entradas = await db.CollectionStageHistory.AsNoTracking()
            .Where(h => ids.Contains(h.LoanId) && h.EnteredOn == dia && !h.IsDeleted && h.Source != CollectionStageHistoryEntry.FuenteSalida).ToListAsync(ct);
        foreach (var h in entradas)
            items.Add(Item(ClasesDeAgenda.Etapa, suyos[h.LoanId], dia, null, $"Entró a la etapa {h.StageCode} con {h.DaysPastDueAtEntry} días"));

        var usuario = (await ReferenciasDeGobierno.UsuariosAsync(db, [gestor.Value], ct)).GetValueOrDefault(gestor.Value);
        return Result.Success(new CollectorAgendaDto(dia, usuario, items.OrderBy(i => i.Due).ThenBy(i => i.Kind, StringComparer.Ordinal)
            .ThenBy(i => i.Loan.Number, StringComparer.Ordinal).ToList()));
    }
}
