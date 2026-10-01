using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Audit.VerifyIntegrity;

/// <summary>
/// Verifica la cadena de sellos de la auditoría de una cooperativa (feature 012, T38, FR-008, SC-012; T066;
/// <c>POST /api/audit/integrity/verify</c>, permiso <c>AuditLog.VerifyIntegrity</c>). Es una consulta: no
/// lleva clave de operación (contracts/api.md §2.3, §29).
///
/// <para>
/// El testigo es SQL: las filas delgadas de <c>COR_AuditOutbox</c> (<c>Seq</c>, <c>EventId</c>, <c>Hash</c>),
/// que nadie borra. Para cada posición del rango se compara con Mongo, recalculando el hash del documento
/// (<see cref="ILectorDeCadenaDeAuditoria"/>): <c>Altered</c> si el contenido, el hash guardado o el enlace con
/// el anterior no cuadran; <c>Deleted</c> si falta (salto de secuencia); <c>Interleaved</c> si otro documento
/// ocupa la posición; <c>PurgedByRetention</c> si falta porque venció su plazo de diez años; y
/// <c>AnchorInvalid</c> si un ancla no firma lo que dice o no coincide con la cadena. La verificación misma
/// queda como <c>AuditLog.IntegrityVerified</c> con su resultado.
/// </para>
/// </summary>
/// <param name="From">Desde (instante UTC del evento).</param>
/// <param name="To">Hasta; a lo sumo diez años después de <paramref name="From"/>.</param>
/// <param name="Stream">El flujo; sin él, el de diez años de la cooperativa.</param>
public sealed record VerifyAuditIntegrityQuery(DateTime From, DateTime To, string? Stream = null)
    : IRequest<Result<VerifyAuditIntegrityResult>>;

/// <summary>Resultado de la verificación (contracts/api.md §29).</summary>
public sealed record VerifyAuditIntegrityResult(
    string Stream,
    long? FromSeq,
    long? ToSeq,
    int Checked,
    int AnchorsChecked,
    IReadOnlyList<AuditIntegrityIncident> Incidents);

/// <summary>Un hallazgo. <see cref="Kind"/> es uno de <see cref="AuditIntegrityIncidentKinds"/>.</summary>
public sealed record AuditIntegrityIncident(string Kind, long Seq, string? EventId, DateTime? OccurredAt);

/// <summary>Las clases de hallazgo, por nombre (así salen en el JSON).</summary>
public static class AuditIntegrityIncidentKinds
{
    public const string Altered = "Altered";
    public const string Deleted = "Deleted";
    public const string Interleaved = "Interleaved";
    public const string AnchorInvalid = "AnchorInvalid";
    public const string PurgedByRetention = "PurgedByRetention";
}

public sealed class VerifyAuditIntegrityQueryValidator : AbstractValidator<VerifyAuditIntegrityQuery>
{
    /// <summary>Rango máximo: el plazo de conservación del flujo.</summary>
    public const int AniosMaximos = 10;

    public VerifyAuditIntegrityQueryValidator()
    {
        RuleFor(x => x.From).NotEmpty();
        RuleFor(x => x.To).NotEmpty()
            .GreaterThanOrEqualTo(x => x.From).WithMessage("«to» no puede ser anterior a «from».");
        RuleFor(x => x).Must(x => x.To <= x.From.AddYears(AniosMaximos))
            .WithName("to")
            .WithMessage($"El rango no puede pasar de {AniosMaximos} años.");
        RuleFor(x => x.Stream).MaximumLength(60);
    }
}

public sealed class VerifyAuditIntegrityQueryHandler(
    IApplicationDbContext db,
    ICurrentTenantService cooperativa,
    ILectorDeCadenaDeAuditoria lector,
    IAuditService auditoria,
    IDateTimeService reloj)
    : IRequestHandler<VerifyAuditIntegrityQuery, Result<VerifyAuditIntegrityResult>>
{
    private static readonly string HashInicial = new('0', 64);

    public async Task<Result<VerifyAuditIntegrityResult>> Handle(VerifyAuditIntegrityQuery request, CancellationToken ct)
    {
        var tenantId = cooperativa.TenantId;
        var propio = AuditoriaEncadenada.Flujo(tenantId);
        if (tenantId is null || propio is null)
            return Result.Failure<VerifyAuditIntegrityResult>("Validation.Invalid", "No hay cooperativa activa cuya cadena verificar.");

        var stream = string.IsNullOrWhiteSpace(request.Stream) ? propio : request.Stream.Trim();
        if (!stream.StartsWith(tenantId + ":", StringComparison.Ordinal))
            return Result.Failure<VerifyAuditIntegrityResult>("Validation.Invalid", "El flujo no es de esta cooperativa.");

        var desde = Utc(request.From);
        var hasta = Utc(request.To);
        var enRango = await db.AuditOutbox.AsNoTracking()
            .Where(e => e.Stream == stream && e.Seq != null && e.OccurredAt >= desde && e.OccurredAt <= hasta)
            .Select(e => e.Seq!.Value)
            .ToListAsync(ct);

        var resultado = enRango.Count == 0
            ? new VerifyAuditIntegrityResult(stream, null, null, 0, 0, [])
            : await VerificarAsync(tenantId, stream, enRango.Min(), enRango.Max(), ct);

        await RegistrarAsync(resultado, desde, hasta, ct);
        return Result.Success(resultado);
    }

    private async Task<VerifyAuditIntegrityResult> VerificarAsync(string tenantId, string stream, long desdeSeq, long hastaSeq, CancellationToken ct)
    {
        // El testigo: las filas delgadas por posición (y la anterior al rango, para el primer enlace).
        var filas = await db.AuditOutbox.AsNoTracking()
            .Where(e => e.Stream == stream && e.Seq != null && e.Seq >= desdeSeq - 1 && e.Seq <= hastaSeq)
            .Select(e => new { Seq = e.Seq!.Value, e.EventId, e.Hash, e.OccurredAt })
            .ToListAsync(ct);
        var sql = filas.GroupBy(f => f.Seq).ToDictionary(g => g.Key, g => g.First());

        var mongo = (await lector.LeerAsync(tenantId, stream, desdeSeq, hastaSeq, ct))
            .GroupBy(e => e.Seq).ToDictionary(g => g.Key, g => g.ToList());

        var incidentes = new List<AuditIntegrityIncident>();
        var anterior = desdeSeq == 1 ? HashInicial : sql.TryGetValue(desdeSeq - 1, out var previa) ? previa.Hash : null;
        var revisados = 0;
        var ahora = reloj.UtcNow;

        for (var seq = desdeSeq; seq <= hastaSeq; seq++)
        {
            sql.TryGetValue(seq, out var testigo);
            var documentos = mongo.TryGetValue(seq, out var enMongo) ? enMongo : [];
            var eventId = testigo?.EventId.ToString("D");

            if (documentos.Count == 0)
            {
                var vencido = testigo is not null && testigo.OccurredAt + AuditoriaEncadenada.Retencion < ahora;
                incidentes.Add(new AuditIntegrityIncident(
                    vencido ? AuditIntegrityIncidentKinds.PurgedByRetention : AuditIntegrityIncidentKinds.Deleted,
                    seq, eventId, testigo?.OccurredAt));
            }
            else
            {
                var genuino = eventId is null ? documentos[0] : documentos.FirstOrDefault(d => d.EventId == eventId);
                foreach (var intruso in documentos.Where(d => d != genuino))
                    incidentes.Add(new AuditIntegrityIncident(AuditIntegrityIncidentKinds.Interleaved, seq, intruso.EventId, intruso.OccurredAt));

                if (genuino is null)
                {
                    // El original no está y otro ocupa su lugar: también falta el original.
                    incidentes.Add(new AuditIntegrityIncident(AuditIntegrityIncidentKinds.Deleted, seq, eventId, testigo?.OccurredAt));
                }
                else if (genuino.HashRecalculado != genuino.Hash
                    || (testigo is not null && genuino.Hash != testigo.Hash)
                    || (anterior is not null && genuino.PrevHash != anterior))
                {
                    incidentes.Add(new AuditIntegrityIncident(AuditIntegrityIncidentKinds.Altered, seq, genuino.EventId, genuino.OccurredAt));
                }
            }

            revisados++;
            anterior = testigo?.Hash;
        }

        var anclas = await db.AuditAnchors.AsNoTracking()
            .Where(a => a.Stream == stream && a.Seq >= desdeSeq - 1 && a.Seq <= hastaSeq)
            .OrderBy(a => a.Seq)
            .ToListAsync(ct);
        foreach (var ancla in anclas)
        {
            var enLaCadena = ancla.Seq == 0
                ? HashInicial
                : sql.TryGetValue(ancla.Seq, out var fila) ? fila.Hash : null;
            var valida = lector.AnclaValida(stream, ancla.Seq, ancla.Hash, ancla.AnchoredAt, ancla.Hmac, ancla.KeyVersion)
                && enLaCadena == ancla.Hash;
            if (!valida)
                incidentes.Add(new AuditIntegrityIncident(AuditIntegrityIncidentKinds.AnchorInvalid, ancla.Seq, null, ancla.AnchoredAt));
        }

        return new VerifyAuditIntegrityResult(stream, desdeSeq, hastaSeq, revisados, anclas.Count, incidentes);
    }

    private Task RegistrarAsync(VerifyAuditIntegrityResult r, DateTime desde, DateTime hasta, CancellationToken ct) =>
        auditoria.LogAsync(new AuditLogCommand
        {
            Action = AuditEventTypes.AuditLogIntegrityVerified,
            EntityType = "AuditChain",
            EntityId = r.Stream,
            Module = ModuloDeAuditoria.Audit,
            NewValues = r,
            HttpStatusCode = 200,
            Metadata = new Dictionary<string, string>
            {
                ["Stream"] = r.Stream,
                ["From"] = desde.ToString("O"),
                ["To"] = hasta.ToString("O"),
                ["Checked"] = r.Checked.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["AnchorsChecked"] = r.AnchorsChecked.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Incidents"] = r.Incidents.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Result"] = r.Incidents.Count == 0 ? "Clean" : string.Join(",", r.Incidents.Select(i => i.Kind).Distinct()),
            },
        }, ct);

    private static DateTime Utc(DateTime valor) => valor.Kind switch
    {
        DateTimeKind.Local => valor.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(valor, DateTimeKind.Utc),
        _ => valor,
    };
}
