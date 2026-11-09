using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Channel.Common;

/// <summary>
/// Feature 021: el emisor de auditoría del canal digital (módulo <c>Channel</c>, encadenado, diez años). Las escrituras del
/// módulo las audita <c>AuditBehavior</c>; esto es para los eventos explícitos —lecturas sensibles de la API de servicio,
/// cambios de habilitación con su motivo— con el patrón de <c>AuditoriaDeRecaudo</c>. El actor, la IP y el agente del
/// asociado los pone el contexto de la petición (<c>AuditoriaEncadenada.ContextoAsync</c>).
/// </summary>
public sealed class AuditoriaDelCanal(IServiceProvider servicios, ILogger<AuditoriaDelCanal> logger)
{
    public const string Modulo = ModuloDeAuditoria.Channel;

    public const string EventoContactoDeActivacionLeido = "Channel.ActivationContact.Read";
    public const string EventoConceptosLeidos = "Channel.Payables.Read";
    public const string EventoDocumentoDescargado = "Channel.Document.Downloaded";
    public const string EventoReciboDescargado = "Channel.Receipt.Downloaded";
    public const string EventoHabilitacionCambiada = "Channel.Enablement.Changed";
    public const string EventoCredencialesEnviadas = "Channel.Destination.CredentialsSent";
    public const string EventoNovedadDeContactoLeida = "Channel.ContactUpdate.Read";

    /// <summary>Lecturas de datos de una persona que deben quedar registradas aunque no cambien nada.</summary>
    public static IReadOnlyList<string> LecturasSensibles { get; } =
        [EventoContactoDeActivacionLeido, EventoConceptosLeidos, EventoDocumentoDescargado, EventoReciboDescargado, EventoNovedadDeContactoLeida];

    /// <summary>Agrega un evento al outbox de auditoría en la unidad de trabajo en curso (se guarda con ella).</summary>
    public async Task AgregarAsync(string accion, string entidad, Guid? entidadPublicId, object? antes, object? despues, CancellationToken ct = default)
    {
        var evento = new AuditLogCommand
        {
            Action = accion,
            EntityType = entidad,
            EntityId = entidadPublicId?.ToString(),
            Module = Modulo,
            OldValues = antes,
            NewValues = despues,
        };
        var contexto = await AuditoriaEncadenada.ContextoAsync(servicios, servicios.GetService<ICurrentUserService>(), null, ct, Modulo);
        var flujo = AuditoriaEncadenada.Flujo(contexto.TenantId);
        var db = flujo is null ? null : servicios.GetService<IApplicationDbContext>();
        if (db is null)
        {
            await servicios.GetRequiredService<IAuditService>().LogAsync(AuditoriaEncadenada.ParaMongo(contexto, evento), ct);
            return;
        }
        db.AuditOutbox.Add(AuditoriaEncadenada.Entrada(flujo!, AuditoriaEncadenada.Evento(contexto, evento)));
    }

    /// <summary>Registra una lectura sensible en su propia unidad de trabajo. Si la auditoría falla, lo deja en el log y no corta la lectura.</summary>
    public async Task<Guid?> LecturaAsync(string accion, string entidad, Guid? entidadPublicId, object? detalle, CancellationToken ct = default)
    {
        try
        {
            return await AuditoriaEncadenada.RegistrarAsync(servicios, new AuditLogCommand
            {
                Action = accion,
                EntityType = entidad,
                EntityId = entidadPublicId?.ToString(),
                Module = Modulo,
                NewValues = detalle,
            }, ct, aparte: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "[Canal.LecturaSinAuditar] La auditoría de {Action} sobre {EntityType} {EntityPublicId} falló.", accion, entidad, entidadPublicId);
            return null;
        }
    }
}
