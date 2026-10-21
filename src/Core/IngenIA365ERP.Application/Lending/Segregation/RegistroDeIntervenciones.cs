using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Segregation;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Segregation;

/// <summary>El expediente de una intervención: su clase (la solicitud, el crédito sin solicitud, la novedad…) y su <c>PublicId</c>. (nuevo)</summary>
public sealed record ExpedienteDeCartera(string Tipo, Guid PublicId);

/// <summary>Una intervención que pasó la segregación: el expediente, la acción, quién (usuario y persona) y a qué fecha. (nuevo)</summary>
public sealed record IntervencionVerificada(ExpedienteDeCartera Expediente, string Accion, int UsuarioId, int PersonaId, DateOnly Fecha);

/// <summary>
/// El único escritor de <c>LND_CaseParticipations</c> (feature 013, T66, T0286; FR-005; data-model §0.11 y §3.13). Todo acto de Cartera
/// que la segregación mira —radicar, analizar, decidir, votar, desembolsar, anular una orden de giro, armar y aplicar un castigo, cargar
/// la migración y aceptar sus diferencias— pasa por aquí en dos tiempos, dentro de la misma unidad de trabajo del acto:
///
/// <list type="number">
/// <item><see cref="VerificarAsync"/>: resuelve quién actúa por <see cref="IActorActual"/> (<c>SEC_Users.Id</c>, nunca el entero del
/// token; <c>LaSegregacionNoUsaUserIdDelToken</c>) y su persona por <c>SEC_Users.PersonId</c> —sin persona,
/// <c>Lending.Security.UserWithoutPerson</c> con <c>data.howToFix</c>—; lee <c>Segregacion.Incompatibilidades</c> por el lector único de
/// parámetros a la fecha del acto y compara con las intervenciones del expediente (también las aún sin guardar) por
/// <see cref="ReglasDeSegregacion"/>: <c>Lending.Segregation.Conflict</c> con la acción previa y su instante.</item>
/// <item><see cref="Registrar"/>: agrega la intervención <b>sin guardar</b>; la guarda el <c>SaveChanges</c> del acto, así que si el acto
/// falla la intervención tampoco queda.</item>
/// </list>
///
/// <para>Un par del parámetro que nombra una acción que no existe se ignora (el resto rige); un parámetro ilegible rige como el defecto
/// (<see cref="ReglasDeSegregacion.ParesPorDefecto"/>), que es lo más estricto que se conoce.</para>
/// </summary>
public sealed class RegistroDeIntervenciones(IApplicationDbContext db, IActorActual actorActual, ILectorDeParametros parametros, IDateTimeService reloj)
{
    public const string CodigoSinPersona = "Lending.Security.UserWithoutPerson";
    public const string CodigoConflicto = ReglasDeSegregacion.CodigoDeError;

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public async Task<Result<IntervencionVerificada>> VerificarAsync(ExpedienteDeCartera expediente, string accion, DateOnly fecha, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(expediente);
        if (!AccionesDeSegregacion.Existe(accion))
            throw new ArgumentException($"«{accion}» no es una acción de segregación de Cartera.", nameof(accion));

        var actor = await actorActual.ObtenerAsync(ct);
        var persona = actor.UserId is { } usuario
            ? await db.Users.AsNoTracking().Where(u => u.Id == usuario).Select(u => u.PersonId).FirstOrDefaultAsync(ct)
            : null;
        if (actor.UserId is not { } usuarioId || persona is not { } personaId)
            return Result.Failure<IntervencionVerificada>(SinPersona());

        return await VerificarDeAsync(expediente, accion, fecha, usuarioId, personaId, ct);
    }

    /// <summary>
    /// La misma verificación para una persona que no es quien hace la petición (feature 013, T0296): el voto que registra la secretaría es
    /// del miembro que vota, no de la secretaria. <paramref name="usuarioId"/> es el <c>SEC_Users.Id</c> del miembro o 0 si no tiene
    /// usuario (los miembros son personas, no necesariamente usuarios, I-1): entonces se compara sólo por persona y la intervención no se
    /// puede registrar (<c>LND_CaseParticipations.UserId</c> es obligatorio).
    /// </summary>
    public async Task<Result<IntervencionVerificada>> VerificarDeAsync(
        ExpedienteDeCartera expediente, string accion, DateOnly fecha, int usuarioId, int personaId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(expediente);
        if (!AccionesDeSegregacion.Existe(accion))
            throw new ArgumentException($"«{accion}» no es una acción de segregación de Cartera.", nameof(accion));

        var pares = await ParesAsync(fecha, ct);
        var previas = await db.CaseParticipations.AsNoTracking()
            .Where(c => c.CaseRootPublicId == expediente.PublicId)
            .ToListAsync(ct);
        // Las de la misma unidad de trabajo que todavía no se guardaron también cuentan (un acto que radica y analiza a la vez).
        var guardadas = previas.Select(p => p.PublicId).ToHashSet();
        previas.AddRange(db.CaseParticipations.Local.Where(c => c.CaseRootPublicId == expediente.PublicId && !guardadas.Contains(c.PublicId)));

        var conflicto = ReglasDeSegregacion.Evaluar(
            new ActoDeSegregacion(expediente.PublicId, accion, usuarioId, personaId), previas.Select(p => p.ComoPrevia()), pares);
        return conflicto is null
            ? Result.Success(new IntervencionVerificada(expediente, accion, usuarioId, personaId, fecha))
            : Result.Failure<IntervencionVerificada>(Conflicto(conflicto));
    }

    /// <summary>Agrega la intervención verificada sobre <paramref name="subjectType"/>/<paramref name="subjectPublicId"/>, sin guardar.</summary>
    public CaseParticipation Registrar(IntervencionVerificada verificada, string subjectType, Guid subjectPublicId)
    {
        ArgumentNullException.ThrowIfNull(verificada);
        var intervencion = CaseParticipation.Registrar(verificada.Expediente.Tipo, verificada.Expediente.PublicId, subjectType, subjectPublicId,
            verificada.Accion, verificada.UsuarioId, verificada.PersonaId, reloj.UtcNow);
        db.CaseParticipations.Add(intervencion);
        return intervencion;
    }

    private async Task<IReadOnlyList<ParIncompatible>> ParesAsync(DateOnly fecha, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeCartera.Modulo, ParametrosDeCartera.SegregacionIncompatibilidades, fecha, ct: ct);
        if (leido.IsFailure) return ReglasDeSegregacion.ParesPorDefecto;

        List<ParDelParametro>? filas;
        try
        {
            filas = JsonSerializer.Deserialize<List<ParDelParametro>>(leido.Value.Texto, Json);
        }
        catch (JsonException)
        {
            filas = null;
        }
        if (filas is null) return ReglasDeSegregacion.ParesPorDefecto;

        return filas
            .Where(f => AccionesDeSegregacion.Existe(f.A) && AccionesDeSegregacion.Existe(f.B))
            .Select(f => new ParIncompatible(f.A!, f.B!, f.Activo))
            .ToList();
    }

    private static Error SinPersona() => new ErrorConDatos(CodigoSinPersona,
        "Su usuario no tiene una persona asociada, y en Cartera quien radica, decide, vota o desembolsa debe tenerla.",
        new { howToFix = "Pida al administrador que asocie su usuario a su persona del maestro en Seguridad > Usuarios." });

    private static Error Conflicto(ConflictoDeSegregacion c) => new ErrorConDatos(CodigoConflicto,
        c.EsReglaFija
            ? "Usted radicó este expediente y no puede decidirlo ni votarlo. Otra persona debe hacerlo."
            : "Usted ya intervino en este expediente con una acción incompatible con ésta. Otra persona debe hacerla.",
        new { action = c.Accion, previousAction = c.AccionPrevia, previousAt = c.InstantePrevio });

    private sealed record ParDelParametro(string? A, string? B, bool Activo);
}
