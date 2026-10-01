using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Common.Alerts;

/// <summary>
/// Levantar y cerrar alertas desde un proceso o un comando (feature 012, T39, T093; FR-022, SC-022). Es el único
/// escritor de <c>COR_Alerts</c> fuera de <c>AttendAlertCommand</c>. Los procesos de fondo que no tienen un comando
/// propio envían <c>RaiseAlertCommand</c>, que lo llama. (nuevo)
///
/// <para>
/// <b>Guarda.</b> Levantar guarda la alerta y cada notificación sale por <c>SendNotificationCommand</c>, que también
/// guarda: dentro de un comando con <c>IOperacionIdempotente</c> todo eso queda en la transacción de la operación y se
/// revierte con ella.
/// </para>
/// </summary>
public interface IAlertas
{
    /// <summary>
    /// Levanta la alerta con el tipo vigente a la fecha local: si hay una pendiente con la misma condición
    /// (<c>DedupKey</c>) suma la ocurrencia; si no, la crea, la entrega a los destinatarios por permiso y alcance (sin
    /// ninguno, a <c>CompanyAdmin</c> con <c>WithoutRecipient</c>) y por correo si el tipo tiene el canal. Tipo fuera del
    /// catálogo: <c>Alerts.Type.NotFound</c>; deshabilitado o sin versión vigente: no se levanta.
    /// </summary>
    Task<Result<AlertaLevantada>> LevantarAsync(AlertaALevantar alerta, CancellationToken ct);

    /// <summary>
    /// Un proceso la da por atendida porque desapareció la causa (llegaron el 030 y el 032, se resolvió la diferencia):
    /// queda atendida con el proceso como actor (<c>AttendedByKind = Process</c>). Devuelve falso si no había una
    /// pendiente con esa condición.
    /// </summary>
    Task<bool> AtenderPorProcesoAsync(string dedupKey, string nota, CancellationToken ct);
}
