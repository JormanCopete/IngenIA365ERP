using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Common.Alerts;

/// <summary>
/// El aviso <c>Aprobaciones.Pendiente</c> a los titulares del permiso del nivel que queda esperando (feature 012, T33,
/// T39, T093; data-model §22: «el permiso del nivel pendiente, no usa la lista»). Reemplaza a
/// <see cref="SinAvisosDeAprobacion"/>. (nuevo)
///
/// <para>
/// La condición es la solicitud y el permiso del nivel (<c>Aprobaciones.Pendiente:{solicitud}:{permiso}</c>): pasar al
/// nivel siguiente avisa a sus titulares aunque el aviso del anterior siga pendiente. Lleva el alcance de la solicitud,
/// así sólo avisa a quien puede decidirla. <see cref="IAlertas"/> guarda: el motor llama esto dentro de la transacción de
/// la operación que pide o decide, y un fallo al avisar no tumba la aprobación (queda <c>[Alertas.AvisoFallido]</c>).
/// </para>
/// </summary>
public sealed class AvisosDeAprobacionPorAlertas(IAlertas alertas, ILogger<AvisosDeAprobacionPorAlertas> logger) : IAvisosDeAprobacion
{
    public async Task PendienteAsync(ApprovalRequest solicitud, string permisoDelNivel, CancellationToken ct)
    {
        var resultado = await alertas.LevantarAsync(new AlertaALevantar(
            TiposDeAlerta.AprobacionPendiente,
            $"Aprobación pendiente: {solicitud.SourceLabel}",
            $"«{solicitud.SourceLabel}» espera el nivel {solicitud.CurrentLevel} de aprobación ({solicitud.Subject}). " +
            "Abrila en la bandeja de aprobaciones para aprobarla o rechazarla.",
            EntityType: nameof(ApprovalRequest),
            EntityPublicId: solicitud.PublicId,
            ScopeWarehousePublicId: solicitud.ScopeWarehousePublicId,
            ScopePointOfSalePublicId: solicitud.ScopePointOfSalePublicId,
            DedupKey: $"{TiposDeAlerta.AprobacionPendiente}:{solicitud.PublicId:D}:{permisoDelNivel}",
            RecipientPermissions: [permisoDelNivel]), ct);

        if (resultado.IsFailure)
        {
            logger.LogWarning("[Alertas.AvisoFallido] No se pudo avisar la solicitud {Solicitud} (nivel {Nivel}): {Codigo}.",
                solicitud.PublicId, solicitud.CurrentLevel, resultado.Error.Code);
        }
    }
}
