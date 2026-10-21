using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// El único escritor del registro de participantes (feature 013, T0078; T16, T20, C35, C36; data-model parte 3 §4.6). <b>Agrega sin
/// guardar</b>: la activación de Cartera y la habilitación de su destino van en el mismo guardado, y los comandos de administración
/// guardan después. Reglas:
/// <list type="bullet">
/// <item>un doble de prueba (<c>Class = Test</c> o <c>IsTestDouble</c>) no se registra en un despliegue de producción
/// (<c>Integration.Participant.TestDoubleInProduction</c>);</item>
/// <item>cada adopción es de una versión que Cartera acepta (<see cref="ContratosDeIntegracion"/>;
/// <c>Integration.Participant.VersionNotSupported</c> con <c>data: { contract, supported[] }</c>);</item>
/// <item>dos vigencias del mismo <c>(Code, Role, Destination)</c>, o dos destinos habilitados que no son dobles con el mismo
/// <c>Destination</c>, no se cruzan (<c>Integration.Participant.Overlaps</c>);</item>
/// <item>registrar de nuevo una clave vigente es habilitarla o inhabilitarla: <b>cierra</b> la fila vigente la víspera y abre otra
/// (la historia queda; <c>ValidTo</c> se escribe una sola vez).</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class EscritorDelRegistroDeIntegracion(
    IApplicationDbContext db,
    IAmbienteDeDespliegue ambiente,
    IActorActual actorActual,
    IDateTimeService reloj)
{
    /// <summary>
    /// Registra un participante (o cambia la habilitación de uno vigente, cerrando su fila la víspera de <see cref="NuevoParticipante.ValidFrom"/>)
    /// y lo agrega a la unidad de trabajo, sin guardar.
    /// </summary>
    public async Task<Result<IntegrationParticipant>> RegistrarAsync(NuevoParticipante nuevo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(nuevo);
        var codigo = nuevo.Code.Trim().ToUpperInvariant();
        var destino = nuevo.Role == IntegrationParticipantRole.MessageDestination ? (nuevo.Destination ?? string.Empty).Trim() : string.Empty;
        var esDoble = nuevo.IsTestDouble || nuevo.Class == IntegrationParticipantClass.Test;

        if (esDoble && ambiente.EsProduccion)
            return Result.Failure<IntegrationParticipant>(ErroresDeIntegracion.DobleEnProduccion(codigo));

        foreach (var adopcion in nuevo.Adopciones)
        {
            var soportadas = ContratosDeIntegracion.VersionesSoportadas(adopcion.Contrato);
            if (!soportadas.Contains(adopcion.Version))
                return Result.Failure<IntegrationParticipant>(ErroresDeIntegracion.VersionNoSoportada(adopcion.Contrato, adopcion.Version, soportadas));
        }

        var existentes = await ConLaClaveAsync(codigo, nuevo.Role, destino, ct);
        if (existentes.Any(p => p.ValidTo is { } hasta && hasta >= nuevo.ValidFrom))
            return Result.Failure<IntegrationParticipant>(ErroresDeIntegracion.ParticipanteSeCruza(codigo, destino, nuevo.ValidFrom));

        var abierta = existentes.FirstOrDefault(p => p.ValidTo is null);
        if (abierta is not null && abierta.ValidFrom >= nuevo.ValidFrom)
            return Result.Failure<IntegrationParticipant>(ErroresDeIntegracion.ParticipanteSeCruza(codigo, destino, nuevo.ValidFrom));

        if (nuevo.Role == IntegrationParticipantRole.MessageDestination && nuevo.IsEnabled && !esDoble
            && await OtroDestinoRealSeCruzaAsync(codigo, destino, nuevo.ValidFrom, ct))
        {
            return Result.Failure<IntegrationParticipant>(ErroresDeIntegracion.ParticipanteSeCruza(codigo, destino, nuevo.ValidFrom));
        }

        if (abierta is not null) Cerrar(abierta, nuevo.ValidFrom.AddDays(-1));

        var actor = await actorActual.ObtenerAsync(ct);
        var participante = new IntegrationParticipant
        {
            Code = codigo,
            Class = nuevo.Class,
            Role = nuevo.Role,
            Destination = destino,
            IsTestDouble = nuevo.IsTestDouble,
            IsEnabled = nuevo.IsEnabled,
            ValidFrom = nuevo.ValidFrom,
            Reason = Recortar(nuevo.Reason.Trim(), 300),
            RegisteredByKind = actor.Kind,
            RegisteredByUserId = actor.UserId,
            RegisteredByName = Recortar(actor.Name, 150),
            CreatedAt = reloj.UtcNow,
            CreatedBy = Recortar(actor.Name, 100),
        };
        foreach (var adopcion in nuevo.Adopciones.DistinctBy(a => (a.Contrato, a.Version)))
        {
            participante.Adoptions.Add(new IntegrationParticipantAdoption
            {
                Participant = participante,
                ContractName = adopcion.Contrato,
                Version = (short)adopcion.Version,
                ValidFrom = nuevo.ValidFrom,
                Reason = participante.Reason,
                CreatedAt = participante.CreatedAt,
                CreatedBy = participante.CreatedBy,
            });
        }

        db.IntegrationParticipants.Add(participante);
        return Result.Success(participante);
    }

    /// <summary>
    /// Habilita o inhabilita la fila vigente de una clave desde <paramref name="desde"/>, conservando sus adopciones: cierra la vigente la
    /// víspera y abre otra (T16, C36). Es lo que hace la activación de Cartera con su destino <c>Lending</c>. No guarda.
    /// </summary>
    public async Task<Result<IntegrationParticipant>> CambiarHabilitacionAsync(
        string codigo, IntegrationParticipantRole rol, string destino, bool habilitado, DateOnly desde, string motivo, CancellationToken ct)
    {
        var clave = codigo.Trim().ToUpperInvariant();
        var destinoNormal = rol == IntegrationParticipantRole.MessageDestination ? destino.Trim() : string.Empty;
        var abierta = (await ConLaClaveAsync(clave, rol, destinoNormal, ct)).FirstOrDefault(p => p.ValidTo is null);
        if (abierta is null)
            return Result.Failure<IntegrationParticipant>(ErroresDeIntegracion.ParticipanteSeCruza(clave, destinoNormal, desde));

        return await RegistrarAsync(new NuevoParticipante(
            clave, abierta.Class, abierta.Role, destinoNormal, habilitado, abierta.IsTestDouble,
            abierta.Adoptions.Where(a => !a.IsDeleted && a.ValidTo is null).Select(a => new VersionAdoptada(a.ContractName, a.Version)).ToList(),
            desde, motivo), ct);
    }

    /// <summary>Retira un participante: cierra su fila (y sus adopciones) en <paramref name="validTo"/>. No guarda.</summary>
    public async Task<Result<IntegrationParticipant>> RetirarAsync(Guid participantPublicId, DateOnly validTo, CancellationToken ct)
    {
        var participante = await db.IntegrationParticipants.Include(p => p.Adoptions)
            .FirstOrDefaultAsync(p => p.PublicId == participantPublicId && !p.IsDeleted, ct);
        if (participante is null)
            return Result.Failure<IntegrationParticipant>(ErroresDeIntegracion.ParticipanteNoEncontrado(participantPublicId));
        if (participante.ValidTo is not null || validTo < participante.ValidFrom)
            return Result.Failure<IntegrationParticipant>(ErroresDeIntegracion.ParticipanteSeCruza(participante.Code, participante.Destination, validTo));

        Cerrar(participante, validTo);
        return Result.Success(participante);
    }

    private static void Cerrar(IntegrationParticipant participante, DateOnly hasta)
    {
        participante.ValidTo = hasta;
        foreach (var adopcion in participante.Adoptions.Where(a => !a.IsDeleted && a.ValidTo is null))
            adopcion.ValidTo = hasta < adopcion.ValidFrom ? adopcion.ValidFrom : hasta;
    }

    /// <summary>Las filas de la clave, de la base (con seguimiento) y las agregadas en esta unidad de trabajo.</summary>
    private async Task<List<IntegrationParticipant>> ConLaClaveAsync(string codigo, IntegrationParticipantRole rol, string destino, CancellationToken ct)
    {
        var deLaBase = await db.IntegrationParticipants.Include(p => p.Adoptions)
            .Where(p => !p.IsDeleted && p.Code == codigo && p.Role == rol && p.Destination == destino)
            .ToListAsync(ct);
        var agregadas = db.IntegrationParticipants.Local
            .Where(p => !p.IsDeleted && p.Code == codigo && p.Role == rol && p.Destination == destino && !deLaBase.Contains(p));
        return [.. deLaBase, .. agregadas];
    }

    /// <summary>¿Otro participante real (no doble) habilitado consume el mismo destino en fechas que se cruzan?</summary>
    private async Task<bool> OtroDestinoRealSeCruzaAsync(string codigo, string destino, DateOnly desde, CancellationToken ct)
    {
        var deLaBase = await db.IntegrationParticipants.AsNoTracking()
            .AnyAsync(p => !p.IsDeleted && p.Role == IntegrationParticipantRole.MessageDestination && p.Destination == destino
                           && p.Code != codigo && p.IsEnabled && !p.IsTestDouble && p.Class != IntegrationParticipantClass.Test
                           && (p.ValidTo == null || p.ValidTo >= desde), ct);
        return deLaBase || db.IntegrationParticipants.Local.Any(p =>
            !p.IsDeleted && p.Role == IntegrationParticipantRole.MessageDestination && p.Destination == destino
            && p.Code != codigo && p.IsEnabled && !RegistroDeIntegracion.EsDoble(p) && (p.ValidTo is null || p.ValidTo >= desde));
    }

    private static string Recortar(string texto, int largo) => texto.Length <= largo ? texto : texto[..largo];
}

/// <summary>Lo que se registra de un participante (data-model parte 3 §4.6; contracts/api.md §21.1). (nuevo)</summary>
/// <param name="Destination">El destino de la bandeja (<c>IntegrationDestinations</c>); obligatorio con <c>MessageDestination</c>, ignorado en un recaudador.</param>
public sealed record NuevoParticipante(
    string Code,
    IntegrationParticipantClass Class,
    IntegrationParticipantRole Role,
    string? Destination,
    bool IsEnabled,
    bool IsTestDouble,
    IReadOnlyList<VersionAdoptada> Adopciones,
    DateOnly ValidFrom,
    string Reason);
