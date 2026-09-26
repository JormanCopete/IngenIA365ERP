using FluentValidation;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// Crea los lotes programados vencidos de la cooperativa (feature 012, T502; contracts/contabilidad.md §5.4; T12). <b>Comando de
/// proceso, sin ruta</b>: lo envía sólo <c>DespachadorDeMensajes</c> por <c>ISender</c> dentro de <c>IEjecutorEnCooperativa</c>, para
/// que la escritura pase por <c>ValidationBehavior</c> y <c>AuditBehavior</c> con el actor «Proceso de integración» (Principios III
/// y X; <c>NingunTrabajoDeFondoOperaSinCooperativa</c>).
///
/// <para>
/// Por cada <c>ScheduleKey</c> con disparador <c>HoraDiaria</c> que tiene entregas <c>InBatch</c> sin lote: la franja de hoy
/// (<c>HoyLocal</c>) es la hora sellada en la clave —la de <c>Contabilidad.HoraDeLote</c> al confirmar, y todas las entregas de
/// una clave la comparten—, o la vigente leída por <see cref="ILectorDeParametros"/> si la clave no la trae. Pasada la franja
/// (<c>AhoraLocal</c>) y sin lote para <c>(ScheduleKey, ScheduledFor)</c>, crea el lote <c>Scheduled</c> con su número, su
/// granularidad, su corte (el último mensaje de la clave) y sus entregas, todo en una transacción. Si otra réplica lo creó
/// primero, el índice único <c>UK_COR_IntegrationBatches_Schedule</c> choca y se traduce a «ya existe», no a error.
/// </para>
///
/// <para>
/// Responde además los lotes programados que siguen <c>Requested</c> pasada su franja más <see cref="LateToleranceMinutes"/>
/// (<c>Integration:Dispatcher:LateToleranceMinutes</c>, T526): el despachador levanta con ellos <c>Integracion.LoteNoCorrio</c>
/// por <c>RaiseAlertCommand</c>.
/// </para>
/// </summary>
public sealed record ScheduleIntegrationBatchesCommand(int LateToleranceMinutes) : IRequest<Result<LotesProgramadosDto>>;

/// <summary>Lo que dejó la pasada: los lotes creados y los atrasados. (nuevo)</summary>
public sealed record LotesProgramadosDto(IReadOnlyList<LoteProgramadoDto> Created, IReadOnlyList<LoteProgramadoDto> Late);

/// <summary>Un lote programado: su franja local y cuántos mensajes tomó. (nuevo)</summary>
public sealed record LoteProgramadoDto(Guid BatchPublicId, long Number, string ScheduleKey, DateTime ScheduledFor, int Messages);

public sealed class ScheduleIntegrationBatchesCommandValidator : AbstractValidator<ScheduleIntegrationBatchesCommand>
{
    public ScheduleIntegrationBatchesCommandValidator()
    {
        RuleFor(x => x.LateToleranceMinutes).GreaterThanOrEqualTo(0).WithMessage("La tolerancia de un lote atrasado no puede ser negativa.");
    }
}

public sealed class ScheduleIntegrationBatchesCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IDateTimeService reloj,
    ILectorDeParametros parametros) : IRequestHandler<ScheduleIntegrationBatchesCommand, Result<LotesProgramadosDto>>
{
    /// <summary>El índice único de la franja (<c>IntegrationBatchConfigurations.UnicoDeLaFranja</c> en Persistence).</summary>
    public const string IndiceUnicoDeLaFranja = "UK_COR_IntegrationBatches_Schedule";

    private const string Destino = IntegrationDestinations.Accounting;

    public async Task<Result<LotesProgramadosDto>> Handle(ScheduleIntegrationBatchesCommand request, CancellationToken ct)
    {
        var ahoraLocal = reloj.AhoraLocal.DateTime;
        var hoy = reloj.HoyLocal;

        var claves = await db.IntegrationMessageDeliveries.AsNoTracking()
            .Where(d => d.Destination == Destino && d.Status == DeliveryStatus.InBatch && d.BatchId == null && d.ScheduleKey != null)
            .Select(d => d.ScheduleKey!).Distinct().ToListAsync(ct);

        var vencidas = new List<(string Clave, HorarioDeLote Horario, DateTime Franja)>();
        foreach (var clave in claves.OrderBy(c => c, StringComparer.Ordinal))
        {
            var horario = ClavesDeLote.Leer(clave);
            if (horario is null || horario.Disparador != ClavesDeLote.HoraDiaria) continue;
            var hora = horario.Hora ?? await HoraVigenteAsync(horario.TipoDeDocumentoRaiz, hoy, ct);
            if (hora is null) continue;
            var franja = hoy.ToDateTime(hora.Value);
            if (ahoraLocal < franja) continue;
            var existe = await db.IntegrationBatches.AsNoTracking().AnyAsync(b => b.ScheduleKey == clave && b.ScheduledFor == franja, ct);
            if (!existe) vencidas.Add((clave, horario, franja));
        }

        var creados = new List<LoteProgramadoDto>();
        if (vencidas.Count > 0)
        {
            try
            {
                creados = await CrearAsync(vencidas, ct);
            }
            catch (DbUpdateException ex) when (EsColisionDeLaFranja(ex))
            {
                // Otra réplica creó la franja primero: ya existe. Lo que no se creó aquí lo toma la pasada siguiente.
                db.DescartarCambios();
                creados = [];
            }
        }

        var limite = ahoraLocal.AddMinutes(-request.LateToleranceMinutes);
        var atrasados = await db.IntegrationBatches.AsNoTracking()
            .Where(b => b.Trigger == BatchTrigger.Scheduled && b.Status == BatchStatus.Requested && b.ScheduledFor != null && b.ScheduledFor < limite)
            .OrderBy(b => b.ScheduledFor).ThenBy(b => b.Number)
            .Select(b => new LoteProgramadoDto(b.PublicId, b.Number, b.ScheduleKey!, b.ScheduledFor!.Value, b.MessageCount))
            .ToListAsync(ct);

        return Result.Success(new LotesProgramadosDto(creados, atrasados));
    }

    private async Task<List<LoteProgramadoDto>> CrearAsync(List<(string Clave, HorarioDeLote Horario, DateTime Franja)> vencidas, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var lotes = new List<(IntegrationBatch Lote, List<IntegrationMessageDelivery> Entregas)>();
            foreach (var (clave, horario, franja) in vencidas)
            {
                var entregas = await db.IntegrationMessageDeliveries.Include(d => d.Message)
                    .Where(d => d.Destination == Destino && d.Status == DeliveryStatus.InBatch && d.BatchId == null && d.ScheduleKey == clave)
                    .OrderBy(d => d.MessageId)
                    .ToListAsync(ct);
                var lote = LotesDeIntegracion.Nuevo(await LotesDeIntegracion.TomarNumeroAsync(db, ct), Destino, BatchTrigger.Scheduled,
                    actor, motivo: null, reloj.UtcNow);
                lote.ScheduleKey = clave;
                lote.ScheduledFor = franja;
                lote.Granularity = horario.GranularidadDelLote;
                lote.CutoffMessageId = entregas.Count == 0 ? null : entregas[^1].MessageId;
                lote.MessageCount = entregas.Count;
                lote.DocumentCount = entregas.Select(e => e.Message!.OriginPublicId).Distinct().Count();
                if (entregas.Count > 0)
                {
                    lote.DateFrom = entregas.Min(e => e.Message!.OperationDate);
                    lote.DateTo = entregas.Max(e => e.Message!.OperationDate);
                }

                db.IntegrationBatches.Add(lote);
                lotes.Add((lote, entregas));
            }

            await db.SaveChangesAsync(ct);
            foreach (var (lote, entregas) in lotes) LotesDeIntegracion.Asignar(lote, entregas);
            await db.SaveChangesAsync(ct);

            return lotes.Select(l => new LoteProgramadoDto(l.Lote.PublicId, l.Lote.Number, l.Lote.ScheduleKey!, l.Lote.ScheduledFor!.Value, l.Lote.MessageCount)).ToList();
        }, ct);
    }

    private async Task<TimeOnly?> HoraVigenteAsync(string tipoDeDocumento, DateOnly hoy, CancellationToken ct)
    {
        var tipoId = await db.InventoryDocumentTypes.AsNoTracking().Where(t => t.Code == tipoDeDocumento).Select(t => (int?)t.Id).FirstOrDefaultAsync(ct);
        var leido = tipoId is int id
            ? await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.ContabilidadHoraDeLote, hoy, ParameterScopeKind.DocumentType, id, ct)
            : await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.ContabilidadHoraDeLote, hoy, ct: ct);
        if (leido.IsFailure) return null;
        return leido.Value.Valor switch
        {
            TimeOnly t => t,
            _ => TimeOnly.TryParse(leido.Value.Texto, System.Globalization.CultureInfo.InvariantCulture, out var h) ? h : null,
        };
    }

    public static bool EsColisionDeLaFranja(DbUpdateException ex)
    {
        for (Exception? actual = ex; actual is not null; actual = actual.InnerException)
        {
            if (actual.Message.Contains(IndiceUnicoDeLaFranja, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }
}
