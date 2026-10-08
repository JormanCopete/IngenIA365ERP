using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Sessions;

/// <summary>Una persona del asunto que no vota su punto, con su motivo (<see cref="MotivosDeExclusion.Radicador"/> o <see cref="MotivosDeExclusion.Solicitante"/>).</summary>
public sealed record PersonaDelAsunto(int PersonId, string Motivo);

/// <summary>
/// Lo que un asunto dice de sí mismo cuando se lleva a una sesión y cuando se cierra el acta (feature 013, T0295, T64, C52): título y
/// resumen para el acta; si está pendiente de decisión y su estado; su expediente (segregación, data-model §3.13); la huella de su
/// contenido actual (si cambia entre que se revisó el acta y su cierre, <c>Lending.Decision.ContentChanged</c>); las personas que no
/// votan; el órgano que lo decide según la matriz y lo que se sella en <c>LND_CreditDecisions</c>. (nuevo)
/// </summary>
public sealed record AsuntoDeActa(
    string Titulo,
    string? Resumen,
    bool Pendiente,
    string Estado,
    ExpedienteDeCartera Expediente,
    string ContentSha256)
{
    /// <summary>El radicador y los solicitantes o codeudores (US2-6): no votan y, si alguno es vinculado, el punto va por la calificada.</summary>
    public IReadOnlyList<PersonaDelAsunto> Personas { get; init; } = [];

    /// <summary>El órgano (Id de <c>LND_GoverningBodies</c>) del nivel efectivo de la matriz; nulo = lo decide el órgano de la sesión.</summary>
    public int? GoverningBodyId { get; init; }

    public decimal? MontoConsolidado { get; init; }

    public string? RoutingSha256 { get; init; }

    public int? AttributionMatrixId { get; init; }

    public int? LevelId { get; init; }
}

/// <summary>Lo que un órgano decidió sobre un asunto al cerrar el acta (sólo los puntos <c>Decision</c>).</summary>
public sealed record DecisionDeActa(
    Session Sesion,
    SessionItem Punto,
    CreditDecision Decision,
    CreditDecisionOutcome Resultado,
    string? Condiciones,
    DateOnly Fecha);

/// <summary>
/// El gancho de cada sujeto de decisión con las actas (feature 013, T0295, T64; data-model §3.8; api.md §5): las historias lo implementan
/// por sujeto (US2 la solicitud y el cupo, K3 la novedad, la condonación y el castigo, US8 las diferencias de la activación…). Una sesión
/// sólo admite puntos de un sujeto con su asunto registrado.
/// <list type="bullet">
/// <item><see cref="DescribirAsync"/> no escribe: dice el estado del sujeto a la fecha de la sesión.</item>
/// <item><see cref="AlDecidirAsync"/> aplica la decisión al sujeto en la transacción del cierre y <b>no guarda</b>: si falla, el cierre no
/// deja nada (ni la decisión, ni el número, ni el sello).</item>
/// </list>
/// </summary>
public interface IAsuntoDeActa
{
    /// <summary>La constante de <see cref="LendingDecisionSubjects"/> que atiende.</summary>
    string SubjectType { get; }

    Task<Result<AsuntoDeActa>> DescribirAsync(Guid subjectPublicId, DateOnly fecha, CancellationToken ct);

    Task<Result> AlDecidirAsync(DecisionDeActa decision, CancellationToken ct);
}

/// <summary>
/// El registro de los asuntos de acta por sujeto (feature 013, T0295). Dos implementaciones que declaran el mismo sujeto son un error de
/// armado: el registro no se construye (falla al resolverlo la primera vez, al arrancar la primera petición que lo usa). (nuevo)
/// </summary>
public sealed class AsuntosDeActa
{
    private readonly Dictionary<string, IAsuntoDeActa> _porSujeto;

    public AsuntosDeActa(IEnumerable<IAsuntoDeActa> asuntos)
    {
        _porSujeto = new Dictionary<string, IAsuntoDeActa>(StringComparer.Ordinal);
        foreach (var asunto in asuntos)
        {
            if (!LendingDecisionSubjects.Existe(asunto.SubjectType))
                throw new InvalidOperationException($"{asunto.GetType().Name} declara «{asunto.SubjectType}», que no es un sujeto de decisión de Cartera.");
            if (!_porSujeto.TryAdd(asunto.SubjectType, asunto))
                throw new InvalidOperationException(
                    $"Dos asuntos de acta declaran el sujeto «{asunto.SubjectType}» ({_porSujeto[asunto.SubjectType].GetType().Name} y {asunto.GetType().Name}).");
        }
    }

    /// <summary>Los sujetos con asunto registrado.</summary>
    public IReadOnlyCollection<string> Sujetos => _porSujeto.Keys;

    /// <summary>El asunto del sujeto, o nulo si ninguna historia lo registró todavía.</summary>
    public IAsuntoDeActa? Buscar(string? sujeto) => sujeto is not null && _porSujeto.TryGetValue(sujeto, out var a) ? a : null;
}

/// <summary>Un punto de acta cerrado que respalda un acto «con acta» (api.md §1.9).</summary>
public sealed record PuntoCerrado(int ItemId, Guid ItemPublicId, CreditDecisionOutcome Outcome, string? Conditions, int? DecisionId, Guid SessionPublicId,
    string? SessionNumber);

/// <summary>
/// La validación única del <c>sessionItemPublicId</c> que llevan los actos «con acta» de todas las historias (feature 013, T0295, T64; api.md
/// §1.9; aceptación de diferencias, castigo, evaluación de cartera, política SARC, activación…): el punto existe, es de una sesión
/// <b>cerrada</b>, decide (no recomienda), su resultado aprueba y su sujeto es el esperado —y, si se da, el mismo <c>PublicId</c>—. Si no,
/// <c>Lending.Session.ItemMismatch</c> con <c>data { expectedSubject, itemSubject, itemOutcome }</c>. (nuevo)
/// </summary>
public sealed class PuntoDeActaCerrado(IApplicationDbContext db)
{
    public async Task<Result<PuntoCerrado>> ValidarAsync(Guid sessionItemPublicId, string sujetoEsperado, Guid? sujetoPublicIdEsperado = null,
        CancellationToken ct = default)
    {
        var punto = await (from i in db.SessionItems.AsNoTracking()
                           join s in db.LendingSessions.AsNoTracking() on i.SessionId equals s.Id
                           where i.PublicId == sessionItemPublicId && !i.IsDeleted
                           select new { i.Id, i.PublicId, i.Kind, i.Outcome, i.Conditions, i.DecisionId, i.SubjectType, i.SubjectPublicId, s.Status, SesionPublicId = s.PublicId, s.Number })
            .FirstOrDefaultAsync(ct);

        var corresponde = punto is not null
            && punto.Status == SessionStatus.Closed
            && punto.Kind == SessionItemKind.Decision
            && punto.Outcome is CreditDecisionOutcome.Approve or CreditDecisionOutcome.ApproveWithConditions
            && punto.SubjectType == sujetoEsperado
            && (sujetoPublicIdEsperado is not { } esperado || punto.SubjectPublicId == esperado);
        return corresponde
            ? Result.Success(new PuntoCerrado(punto!.Id, punto.PublicId, punto.Outcome!.Value, punto.Conditions, punto.DecisionId, punto.SesionPublicId, punto.Number))
            : Result.Failure<PuntoCerrado>(ErroresDeGobierno.PuntoNoCorresponde(sujetoEsperado, punto?.SubjectType, punto?.Outcome?.ToString()));
    }
}

/// <summary>
/// El PDF del acta (feature 013, T0296, T0297, T73; data-model §3.8): el cierre lo pide en su transacción y guarda el adjunto de módulo no
/// borrable que devuelve en <c>MinutesAttachmentPublicId</c>. La implementación con QuestPDF (<c>ActaDeSesionReport</c>) la registra la API
/// (T0297); sin ella el acta se cierra sin PDF guardado y <c>minutes.pdf</c> la emite al pedirla. (nuevo)
/// </summary>
public interface IActaDeSesionEnPdf
{
    /// <summary>Emite y guarda el PDF del acta cerrada; devuelve el <c>PublicId</c> del adjunto, o nulo si no se guarda.</summary>
    Task<Result<Guid?>> GuardarAsync(SessionDto acta, CancellationToken ct);
}

/// <summary>La que no guarda PDF: el acta queda sin adjunto hasta que la API registre la de QuestPDF (T0297). (nuevo)</summary>
public sealed class SinActaEnPdf : IActaDeSesionEnPdf
{
    public Task<Result<Guid?>> GuardarAsync(SessionDto acta, CancellationToken ct) => Task.FromResult(Result.Success<Guid?>(null));
}
