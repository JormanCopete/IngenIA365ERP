using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Alerts.RaiseAlert;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Domain.Enums.Core;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Common.NationalValues;

/// <summary>
/// La tarea programada <c>valores-nacionales.replicar</c> (feature 013, T7, T39, R4): en <b>cada pasada</b> del
/// <c>ProgramadorDeTareas</c> y en cada cooperativa —también las que no han activado Cartera: Nómina y la 012 leen la proyección
/// del SMMLV, la UVT y los festivos— envía <see cref="ReplicateNationalValuesCommand"/> por el <see cref="ISender"/> del ámbito
/// que arma <c>IEjecutorEnCooperativa</c>. Si la central no responde no lanza: levanta
/// <c>Cartera.ReplicaDeValoresNacionalesAtrasada</c> (una sola, por su condición) y la pasada siguiente reintenta. Se registra
/// como singleton en <c>Program.cs</c>. (nuevo)
/// </summary>
public sealed class TareaDeReplicaDeValoresNacionales : ITareaProgramada
{
    public const string NombreDeLaTarea = "valores-nacionales.replicar";

    public string Nombre => NombreDeLaTarea;

    /// <summary>Cada pasada: la réplica sin novedades no escribe nada, así que correrla seguido sólo cuesta una consulta.</summary>
    public bool DebeCorrer(DateTimeOffset ahoraLocal, DateTimeOffset? ultimaCorrida) => true;

    public async Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct)
    {
        var sender = servicios.GetRequiredService<ISender>();
        var resultado = await sender.Send(new ReplicateNationalValuesCommand(NationalValueReplicationOrigin.ScheduledTask), ct);
        if (resultado.IsSuccess && resultado.Value.CentralDisponible) return;

        var motivo = resultado.IsSuccess ? resultado.Value.Motivo : resultado.Error.Message;
        var levantada = await sender.Send(new RaiseAlertCommand(new AlertaALevantar(
            AlertasDeValoresNacionales.ReplicaAtrasada,
            "La réplica de valores nacionales no corrió",
            $"La copia de los valores nacionales de esta cooperativa no se pudo poner al día: {motivo}. "
            + "Los cálculos siguen con lo ya replicado; si falta un valor, se detienen con «Falta …».",
            DedupKey: AlertasDeValoresNacionales.ClaveDeReplicaAtrasada)), ct);
        if (levantada.IsFailure)
            servicios.GetService<ILogger<TareaDeReplicaDeValoresNacionales>>()?.LogWarning(
                "[ValoresNacionales.ReplicaAtrasada] {Motivo} (la alerta no se levantó: {Codigo}).", motivo, levantada.Error.Code);
    }
}
