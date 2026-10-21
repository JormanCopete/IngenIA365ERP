using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Origination.CreditLimits;

/// <summary>El enrutamiento sellado de un cupo que espera decisión (T63): matriz, nivel, huella, consolidado, vinculado y razones. (nuevo)</summary>
public sealed record RutaDelCupo(
    int MatrixId,
    int MatrixVersion,
    int LevelId,
    string LevelCode,
    string Sha256,
    decimal Consolidated,
    bool RelatedParty,
    IReadOnlyList<RoutingReasonDto> Reasons);

/// <summary>
/// Lo que una versión de cupo guarda en <c>CalculationDetail</c> (data-model §2.10, «JSON: relación con aportes, capacidad, puntaje»; feature 013,
/// US2 bloque 6B, sin columnas nuevas): si espera decisión de la matriz y con qué enrutamiento, el monto operativo de quien la asignó, la
/// vigencia final pedida, la preferencia del rotativo, el saldo de aportes usado (D19) y la evidencia de listas y centrales. (nuevo)
/// </summary>
public sealed record DetalleDeCupo
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Superó el monto operativo de quien la asignó o la matriz pide otro nivel: no rige hasta que se decida (§3.4).</summary>
    public bool RequiresDecision { get; init; }

    public RutaDelCupo? Routing { get; init; }

    /// <summary>El monto de <c>Lending.CreditLimits.Assign</c> de quien la asignó (nulo = sin límite).</summary>
    public decimal? OperationalLimit { get; init; }

    /// <summary>Hasta cuándo rige (se aplica como versión en cero desde el día siguiente al otorgarse).</summary>
    public DateOnly? ValidTo { get; init; }

    public PrepaymentChoice? PrepaymentPreference { get; init; }

    public decimal? Contributions { get; init; }

    public DateOnly? ContributionsAsOf { get; init; }

    public string? ContributionsSource { get; init; }

    public decimal? ContributionsMultiple { get; init; }

    public Guid? BureauQueryPublicId { get; init; }

    public Guid? ScreeningResultPublicId { get; init; }

    /// <summary>La versión que esperaba decisión y que esta versión otorga (aprobada).</summary>
    public Guid? ApprovesPublicId { get; init; }

    /// <summary>Las condiciones con que se aprobó (JSON de la decisión).</summary>
    public string? Conditions { get; init; }

    public string Serializar() => JsonSerializer.Serialize(this, Json);

    public static DetalleDeCupo De(CreditLimit v)
    {
        if (string.IsNullOrWhiteSpace(v.CalculationDetail)) return new DetalleDeCupo();
        try
        {
            return JsonSerializer.Deserialize<DetalleDeCupo>(v.CalculationDetail, Json) ?? new DetalleDeCupo();
        }
        catch (JsonException)
        {
            return new DetalleDeCupo();
        }
    }
}

/// <summary>El estado calculado de una versión de cupo (api.md §7.1, texto de presentación). (nuevo)</summary>
public static class EstadosDeCupo
{
    public const string PendienteDeDecision = "PendingDecision";
    public const string Activo = "Active";
    public const string Bloqueado = "Blocked";
    public const string Terminado = "Ended";
}

/// <summary>
/// Las versiones de un cupo (persona, clase, línea) y lo que se calcula de ellas (feature 013, T0405; data-model §2.10, §0.6): una versión es
/// <b>firme</b> si no esperaba decisión o ya la tiene (<c>DecisionId</c>); la que rige a una fecha es la última firme con <c>ValidFrom ≤ D</c>;
/// una no firme sin decisión registrada espera decisión; con una decisión que niega o devuelve, terminó. Nada de esto se guarda. (nuevo)
/// </summary>
public static class VersionesDeCupo
{
    public static bool EsFirme(CreditLimit v) => v.DecisionId is not null || !DetalleDeCupo.De(v).RequiresDecision;

    /// <summary>La que rige a <paramref name="fecha"/> entre las versiones de un mismo cupo, o nula.</summary>
    public static CreditLimit? Vigente(IEnumerable<CreditLimit> delMismoCupo, DateOnly fecha) =>
        delMismoCupo.Where(v => !v.IsDeleted && EsFirme(v) && v.ValidFrom <= fecha).OrderByDescending(v => v.VersionNumber).FirstOrDefault();

    /// <summary>La última firme, aunque empiece después (para cruces de vigencia).</summary>
    public static CreditLimit? UltimaFirme(IEnumerable<CreditLimit> delMismoCupo) =>
        delMismoCupo.Where(v => !v.IsDeleted && EsFirme(v)).OrderByDescending(v => v.VersionNumber).FirstOrDefault();

    /// <summary>La que espera decisión (no firme, sin decisión registrada y posterior a la última firme), o nula.</summary>
    public static CreditLimit? Pendiente(IEnumerable<CreditLimit> delMismoCupo, IReadOnlySet<Guid> decididas)
    {
        var lista = delMismoCupo.Where(v => !v.IsDeleted).ToList();
        var firme = UltimaFirme(lista)?.VersionNumber ?? 0;
        return lista.Where(v => !EsFirme(v) && !decididas.Contains(v.PublicId) && v.VersionNumber > firme).OrderByDescending(v => v.VersionNumber).FirstOrDefault();
    }

    /// <summary>Hasta cuándo rige una versión firme: la víspera de la firme siguiente, o nula.</summary>
    public static DateOnly? Hasta(CreditLimit version, IEnumerable<CreditLimit> delMismoCupo) =>
        delMismoCupo.Where(v => !v.IsDeleted && EsFirme(v) && v.VersionNumber > version.VersionNumber).OrderBy(v => v.VersionNumber)
            .Select(v => (DateOnly?)v.ValidFrom.AddDays(-1)).FirstOrDefault();

    /// <summary>
    /// El estado de <paramref name="v"/> a <paramref name="fecha"/>: espera decisión; terminada (negada, en cero o reemplazada); bloqueada si la
    /// persona tiene un bloqueo vigente; si no, activa.
    /// </summary>
    public static string Estado(CreditLimit v, IReadOnlyList<CreditLimit> delMismoCupo, IReadOnlySet<Guid> decididas, IReadOnlyList<DebtorBlock> bloqueos,
        DateOnly fecha)
    {
        if (!EsFirme(v)) return decididas.Contains(v.PublicId) ? EstadosDeCupo.Terminado : EstadosDeCupo.PendienteDeDecision;
        if (v.Amount <= 0m) return EstadosDeCupo.Terminado;
        var vigente = Vigente(delMismoCupo, fecha);
        if (vigente is not null && vigente.VersionNumber > v.VersionNumber) return EstadosDeCupo.Terminado;
        return bloqueos.Any(b => b.PersonId == v.PersonId && b.Vigente) ? EstadosDeCupo.Bloqueado : EstadosDeCupo.Activo;
    }

    /// <summary>Las versiones de los cupos de unas personas (con las que la unidad de trabajo agregó y no guardó).</summary>
    public static async Task<IReadOnlyList<CreditLimit>> DeAsync(IApplicationDbContext db, IReadOnlyCollection<int> personas, CancellationToken ct)
    {
        var ids = personas.Distinct().ToArray();
        var guardadas = await db.CreditLimits.AsNoTracking().Where(c => ids.Contains(c.PersonId) && !c.IsDeleted).ToListAsync(ct);
        var vistas = guardadas.Select(c => c.PublicId).ToHashSet();
        return guardadas.Concat(db.CreditLimits.Local.Where(c => ids.Contains(c.PersonId) && !vistas.Contains(c.PublicId))).ToList();
    }

    /// <summary>Los <c>PublicId</c> de versiones de cupo que ya tienen una decisión registrada.</summary>
    /// <remarks><paramref name="salvo"/>: la decisión que se está aplicando (el cierre del acta la agrega antes de avisarle al asunto) no cuenta.</remarks>
    public static async Task<IReadOnlySet<Guid>> DecididasAsync(IApplicationDbContext db, IReadOnlyCollection<Guid> versiones, CancellationToken ct,
        Domain.Entities.Lending.Governance.CreditDecision? salvo = null)
    {
        var lista = versiones.Distinct().ToArray();
        var guardadas = await db.CreditDecisions.AsNoTracking()
            .Where(d => d.SubjectType == LendingDecisionSubjects.CreditLimit && lista.Contains(d.SubjectPublicId) && !d.IsDeleted)
            .Select(d => d.SubjectPublicId).ToListAsync(ct);
        return guardadas.Concat(db.CreditDecisions.Local.Where(d => d.SubjectType == LendingDecisionSubjects.CreditLimit && lista.Contains(d.SubjectPublicId)
            && !ReferenceEquals(d, salvo)).Select(d => d.SubjectPublicId)).ToHashSet();
    }

    /// <summary>¿Es del mismo cupo (persona, clase, línea)?</summary>
    public static bool MismoCupo(CreditLimit a, CreditLimit b) => a.EsDelMismoCupo(b.PersonId, b.Kind, b.CreditLineId);
}

/// <summary>Una línea como la nombra un cupo. (nuevo)</summary>
public sealed record CreditLimitLineDto(Guid PublicId, string Code, string Name);

/// <summary>El enrutamiento de un cupo que espera decisión (api.md §7.1, <c>routing</c>). (nuevo)</summary>
public sealed record CreditLimitRoutingDto(
    int MatrixVersion,
    string LevelCode,
    string LevelName,
    AttributionLevelKind LevelKind,
    string? GoverningBody,
    IReadOnlyList<RoutingReasonDto> Reasons,
    decimal ConsolidatedAmount,
    bool RelatedParty,
    string ContentSha256);

/// <summary>Un aviso que no detiene la operación (api.md §1.9). (nuevo)</summary>
public sealed record AvisoDeCarteraDto(string Code, string Message, Guid? ScreeningResultPublicId = null);

/// <summary>Una versión de cupo como la muestran las respuestas (api.md §7.1 <c>CreditLimitDto</c>). (nuevo)</summary>
public sealed record CreditLimitDto(
    Guid PublicId,
    PersonaRefDto Person,
    CreditLimitKind Kind,
    CreditLimitLineDto? Line,
    decimal Amount,
    CreditLimitOrigin Origin,
    string Status,
    DebtorBlockReason? BlockReason,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    int VersionNumber,
    string Reason,
    PrepaymentChoice? PrepaymentPreference,
    Guid? DecisionPublicId,
    CreditLimitRoutingDto? Routing,
    IReadOnlyList<AvisoDeCarteraDto> Warnings);

/// <summary>Arma el DTO de una versión de cupo. (nuevo)</summary>
public static class ConsultasDeCupos
{
    public static async Task<CreditLimitDto> DtoAsync(IApplicationDbContext db, CreditLimit v, DateOnly fecha, CancellationToken ct,
        IReadOnlyList<AvisoDeCarteraDto>? avisos = null)
    {
        var versiones = (await VersionesDeCupo.DeAsync(db, [v.PersonId], ct)).Where(x => VersionesDeCupo.MismoCupo(x, v)).ToList();
        if (versiones.All(x => x.PublicId != v.PublicId)) versiones.Add(v);
        var decididas = await VersionesDeCupo.DecididasAsync(db, versiones.Select(x => x.PublicId).ToList(), ct);
        var bloqueos = await new BloqueosDelDeudor(db).VigentesAsync([v.PersonId], ct);
        var estado = VersionesDeCupo.Estado(v, versiones, decididas, bloqueos, fecha);
        var persona = (await ReferenciasDeGobierno.PersonasAsync(db, [v.PersonId], ct))[v.PersonId];
        var linea = v.CreditLineId is { } l
            ? await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == l).Select(x => new CreditLimitLineDto(x.PublicId, x.Code, x.Name))
                .FirstOrDefaultAsync(ct)
            : null;
        var detalle = DetalleDeCupo.De(v);
        var preferencia = await PreferenciaAsync(db, versiones, ct);
        var decision = v.DecisionId is { } d
            ? await db.CreditDecisions.AsNoTracking().Where(x => x.Id == d).Select(x => (Guid?)x.PublicId).FirstOrDefaultAsync(ct)
            : null;

        CreditLimitRoutingDto? ruta = null;
        if (estado == EstadosDeCupo.PendienteDeDecision && detalle.Routing is { } r)
        {
            var nivel = await db.AttributionLevels.AsNoTracking().FirstOrDefaultAsync(n => n.Id == r.LevelId, ct);
            var organo = nivel?.GoverningBodyId is { } g
                ? await db.GoverningBodies.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == g).Select(b => b.Name).FirstOrDefaultAsync(ct)
                : null;
            ruta = new CreditLimitRoutingDto(r.MatrixVersion, r.LevelCode, nivel?.Name ?? r.LevelCode, nivel?.Kind ?? AttributionLevelKind.Individual, organo,
                r.Reasons, r.Consolidated, r.RelatedParty, r.Sha256);
        }

        return new CreditLimitDto(v.PublicId, persona, v.Kind, linea, v.Amount, v.Origin, estado,
            estado == EstadosDeCupo.Bloqueado ? bloqueos.First(b => b.PersonId == v.PersonId).Reason : null,
            v.ValidFrom, VersionesDeCupo.EsFirme(v) ? VersionesDeCupo.Hasta(v, versiones) ?? detalle.ValidTo : detalle.ValidTo, v.VersionNumber, v.Reason,
            preferencia, decision, ruta, avisos ?? []);
    }

    /// <summary>La preferencia vigente de un cupo rotativo: la última registrada en cualquiera de sus versiones (T24).</summary>
    public static async Task<PrepaymentChoice?> PreferenciaAsync(IApplicationDbContext db, IReadOnlyList<CreditLimit> versiones, CancellationToken ct)
    {
        if (versiones.Count == 0 || versiones[0].Kind != CreditLimitKind.Revolving) return null;
        var ids = versiones.Where(v => v.Id > 0).Select(v => v.Id).ToArray();
        var guardadas = await db.PrepaymentPreferences.AsNoTracking().Where(p => p.CreditLimitId != null && ids.Contains(p.CreditLimitId.Value) && !p.IsDeleted)
            .ToListAsync(ct);
        var todas = guardadas.Concat(db.PrepaymentPreferences.Local.Where(p => p.CreditLimitId is { } c && ids.Contains(c) && guardadas.All(g => g.PublicId != p.PublicId)));
        return PrepaymentPreference.Vigente(todas, DateTime.MaxValue)?.Choice;
    }
}
