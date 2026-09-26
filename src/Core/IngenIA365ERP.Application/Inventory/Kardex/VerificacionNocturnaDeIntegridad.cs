using IngenIA365ERP.Application.Common.Execution;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Inventory.Kardex;

/// <summary>
/// La tarea nocturna <c>inventario.integridad</c> (feature 012, T259; contracts/api.md §6.2; FR-003, SC-006): una vez por noche,
/// desde la <see cref="HoraDeInicio"/> local, corre <see cref="VerifyInventoryIntegrityQuery"/> sobre todo en cada cooperativa.
/// La corre <c>ProgramadorDeTareas</c> por <c>IEjecutorEnCooperativa</c> con el actor «Proceso de integración» (en segundo plano
/// el alcance es total); si hay diferencias, la consulta levanta <c>Inventario.IncidenteDeIntegridad</c>. Es idempotente:
/// repetirla tras un reinicio sólo vuelve a verificar y suma la ocurrencia de la misma alerta. Se registra como singleton en la
/// API. (nuevo)
/// </summary>
public sealed class VerificacionNocturnaDeIntegridad : ITareaProgramada
{
    public const string NombreDeLaTarea = "inventario.integridad";

    /// <summary>La hora local desde la que corre (madrugada, fuera de la operación).</summary>
    public static readonly TimeOnly HoraDeInicio = new(2, 0);

    public string Nombre => NombreDeLaTarea;

    /// <summary>Desde las 02:00 locales, si no corrió ya ese día.</summary>
    public bool DebeCorrer(DateTimeOffset ahoraLocal, DateTimeOffset? ultimaCorrida) =>
        TimeOnly.FromTimeSpan(ahoraLocal.TimeOfDay) >= HoraDeInicio
        && (ultimaCorrida is null || DateOnly.FromDateTime(ultimaCorrida.Value.DateTime) < DateOnly.FromDateTime(ahoraLocal.DateTime));

    public async Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct)
    {
        var resultado = await servicios.GetRequiredService<ISender>().Send(new VerifyInventoryIntegrityQuery(), ct);
        if (resultado.IsFailure)
            throw new InvalidOperationException($"La verificación nocturna del kardex falló: {resultado.Error.Code} {resultado.Error.Message}");
    }
}
