using MediatR;

namespace IngenIA365ERP.Application.Compliance.HabeasData.Events;

/// <summary>
/// T127 — Evento publicado cuando un titular revoca su consentimiento
/// habeas data (US7). Otros módulos pueden suscribirse vía
/// <c>INotificationHandler&lt;HabeasDataRevokedEvent&gt;</c> para:
/// <list type="bullet">
///   <item>Detener envíos de marketing al titular.</item>
///   <item>Anonimizar datos derivados (analytics, dashboards).</item>
///   <item>Notificar al área de protección de datos.</item>
/// </list>
///
/// <para>
/// Vive en Application (no en Domain) porque depende de MediatR
/// <see cref="INotification"/> — Domain debe quedar libre de paquetes
/// (Principio II Clean Architecture). El evento se despacha tras commit
/// exitoso del consent.
/// </para>
/// </summary>
public sealed record HabeasDataRevokedEvent(
    int TenantId,
    int PersonId,
    Guid PolicyVersionPublicId,
    DateTime RevokedAt,
    string RevokedBy,
    string? Notes) : INotification;
