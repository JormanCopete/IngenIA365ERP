using IngenIA365ERP.Application.Common.Alerts.RaiseAlert;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Inventory.Replenishment;

/// <summary>Lo que dejó una pasada de la revisión de reorden. (nuevo)</summary>
/// <param name="Revisadas">Políticas evaluadas.</param>
/// <param name="Levantadas">Alertas pedidas (<c>Inventario.Reorden</c> e <c>Inventario.Quiebre</c>) que el catálogo aceptó, nuevas o repetidas.</param>
public sealed record ResultadoDeRevisionDeReorden(int Revisadas, int Levantadas);

/// <summary>
/// La revisión programada de reorden y quiebre (feature 012, US17, T954; FR-035, FR-022, SC-022): recorre las políticas vivas
/// de <c>INV_ReorderPolicies</c> de las bodegas <b>operativas</b>, <b>activadas</b> y activas —el tránsito y las no activadas no
/// se revisan—, evalúa cada una con <see cref="EvaluacionDeReposicion"/> (la misma posición y el mismo cálculo que el aviso al
/// confirmar) y levanta las alertas por <see cref="RaiseAlertCommand"/> (sin ruta) con la condición
/// <c>{TypeCode}:{producto}:{bodega}</c> y la bodega como alcance, así una pendiente sólo suma la ocurrencia. No aplica alcance de
/// persona: la corre <c>ProgramadorDeTareas</c> por <c>IEjecutorEnCooperativa</c> con el actor «Proceso de integración», que ve
/// todo. Recorre por tandas de <see cref="Tanda"/> políticas para no cargar la cooperativa entera en memoria. (nuevo)
/// </summary>
public sealed class RevisionDeReorden(IApplicationDbContext db, EvaluacionDeReposicion evaluacion, ISender sender)
{
    public const int Tanda = 500;

    public async Task<ResultadoDeRevisionDeReorden> RevisarAsync(CancellationToken ct)
    {
        var revisadas = 0;
        var levantadas = 0;
        var ultimo = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var desde = ultimo;
            var ids = await Revisables().Where(r => r.Id > desde).OrderBy(r => r.Id).Select(r => r.Id).Take(Tanda).ToListAsync(ct);
            if (ids.Count == 0) break;
            ultimo = ids[^1];

            var filas = await evaluacion.EvaluarAsync(db.ReorderPolicies.AsNoTracking().Where(r => ids.Contains(r.Id)), ct);
            revisadas += filas.Count;
            foreach (var fila in filas.Where(f => f.Resultado.Alerta))
            {
                foreach (var alerta in AlertasDeReposicion.De(fila))
                {
                    var r = await sender.Send(new RaiseAlertCommand(alerta), ct);
                    if (r.IsSuccess) levantadas++;
                }
            }
        }
        return new ResultadoDeRevisionDeReorden(revisadas, levantadas);
    }

    /// <summary>Las políticas vivas de bodegas operativas, activadas y activas.</summary>
    private IQueryable<Domain.Entities.Inventory.Warehousing.ReorderPolicy> Revisables() =>
        db.ReorderPolicies.AsNoTracking().Where(r => db.Warehouses.Any(w =>
            w.Id == r.WarehouseId
            && w.Behavior == WarehouseBehavior.Operational
            && w.ActivationStatus == WarehouseActivationStatus.Active
            && w.IsActive));
}

/// <summary>
/// La tarea programada <c>inventario.reorden</c> (feature 012, T954): una vez al día, desde la hora local de
/// <c>Integration:ReorderReview:StartHour</c> (la sección <c>Integration</c> de la API; por defecto las 05:00, antes de abrir),
/// corre <see cref="RevisionDeReorden"/> en cada cooperativa. La corre <c>ProgramadorDeTareas</c> por <c>IEjecutorEnCooperativa</c>
/// con el actor «Proceso de integración»; se apaga con <c>Integration:ScheduledTasks:Enabled</c> (la fixture lo apaga y la dispara
/// a mano). Idempotente: repetirla sólo suma la ocurrencia de las alertas pendientes. Se registra como singleton. (nuevo)
/// </summary>
public sealed class TareaDeRevisionDeReorden(TimeOnly horaDeInicio) : ITareaProgramada
{
    public const string NombreDeLaTarea = "inventario.reorden";

    /// <summary>La hora local por defecto (antes de la jornada, para que las alertas estén al llegar).</summary>
    public static readonly TimeOnly HoraPorDefecto = new(5, 0);

    public TareaDeRevisionDeReorden() : this(HoraPorDefecto) { }

    public TimeOnly HoraDeInicio { get; } = horaDeInicio;

    public string Nombre => NombreDeLaTarea;

    public bool DebeCorrer(DateTimeOffset ahoraLocal, DateTimeOffset? ultimaCorrida) =>
        TimeOnly.FromTimeSpan(ahoraLocal.TimeOfDay) >= HoraDeInicio
        && (ultimaCorrida is null || DateOnly.FromDateTime(ultimaCorrida.Value.DateTime) < DateOnly.FromDateTime(ahoraLocal.DateTime));

    public async Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct) =>
        await servicios.GetRequiredService<RevisionDeReorden>().RevisarAsync(ct);
}
