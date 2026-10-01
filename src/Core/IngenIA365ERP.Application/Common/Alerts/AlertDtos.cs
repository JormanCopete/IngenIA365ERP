using System.Text.Json;
using IngenIA365ERP.Domain.Entities.Alerts;
using IngenIA365ERP.Domain.Enums.Alerts;

namespace IngenIA365ERP.Application.Common.Alerts;

/// <summary>
/// Una alerta en la bandeja (contracts/api.md §16.1). <c>severity</c> y <c>status</c> viajan por nombre, como los
/// muestra el contrato; <c>entity.label</c> y <c>entity.route</c> los describe el módulo dueño (pantallas de la fase 11)
/// y aquí van nulos. (nuevo)
/// </summary>
public sealed record AlertDto(
    Guid PublicId,
    string TypeCode,
    string Module,
    string Severity,
    string Subject,
    string Body,
    AlertEntityDto? Entity,
    string Status,
    DateTime RaisedAt,
    string RaisedBy,
    int OccurrenceCount,
    DateTime LastOccurredAt,
    DateTime? AttendedAt,
    string? AttendedBy,
    string? AttendNote,
    bool WithoutRecipient);

public sealed record AlertEntityDto(string? Type, Guid PublicId, string? Label, string? Route);

/// <summary>
/// Una versión de un tipo de alerta (contracts/api.md §16.2). <c>activeRecipients</c> son los usuarios activos que hoy
/// la recibirían; <c>withoutRecipient</c>, que no hay ninguno y se iría a <c>CompanyAdmin</c>. (nuevo)
/// </summary>
public sealed record AlertTypeDto(
    string TypeCode,
    string Module,
    string Description,
    string Severity,
    IReadOnlyList<string> RecipientPermissions,
    IReadOnlyList<string> Channels,
    IReadOnlyDictionary<string, decimal>? Thresholds,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string? Reason,
    bool IsEnabled,
    int ActiveRecipients,
    bool WithoutRecipient,
    string AvailableFrom);

/// <summary>Lo que hay que levantar (lo arman los procesos y los comandos que detectan la condición). (nuevo)</summary>
/// <param name="DedupKey">
/// La condición: mientras haya una pendiente con la misma clave, se suma en vez de crear otra. Nula: la canónica
/// <c>{TypeCode}:{entidad}[:{bodega}]</c> (<see cref="Alertas.ClaveDe"/>).
/// </param>
/// <param name="RecipientPermissions">
/// Permisos destinatarios que reemplazan a los del tipo (el del nivel pendiente en <c>Aprobaciones.Pendiente</c>).
/// </param>
public sealed record AlertaALevantar(
    string TypeCode,
    string Subject,
    string Body,
    string? EntityType = null,
    Guid? EntityPublicId = null,
    Guid? ScopeWarehousePublicId = null,
    Guid? ScopePointOfSalePublicId = null,
    string? DedupKey = null,
    IReadOnlyList<string>? RecipientPermissions = null);

/// <summary>Qué pasó al pedir levantar una alerta. (nuevo)</summary>
public enum DesenlaceDeAlerta
{
    /// <summary>Se creó y se entregó.</summary>
    Levantada = 1,

    /// <summary>Había una pendiente con la misma condición: se sumó la ocurrencia, sin volver a notificar.</summary>
    Repetida = 2,

    /// <summary>El tipo está deshabilitado o sin versión vigente en la cooperativa: no se levanta.</summary>
    TipoInactivo = 3,
}

/// <summary>El resultado de levantar una alerta. (nuevo)</summary>
public sealed record AlertaLevantada(Guid? AlertPublicId, DesenlaceDeAlerta Desenlace, int RecipientCount, bool WithoutRecipient);

/// <summary>Proyecciones compartidas por los comandos y las consultas de alertas. (nuevo)</summary>
internal static class ProyeccionDeAlertas
{
    public static AlertDto ADto(Alert a) => new(
        a.PublicId,
        a.TypeCode,
        a.Module,
        a.Severity.ToString(),
        a.Subject,
        a.Body,
        a.EntityPublicId is { } entidad ? new AlertEntityDto(a.EntityType, entidad, null, null) : null,
        a.Status.ToString(),
        a.RaisedAt,
        a.RaisedByName,
        a.OccurrenceCount,
        a.LastOccurredAt,
        a.AttendedAt,
        a.AttendedByName,
        a.AttendNote,
        a.WithoutRecipient);

    public static AlertTypeDto ADto(AlertType t, int destinatariosActivos)
    {
        var def = TiposDeAlerta.Buscar(t.TypeCode);
        var usaLista = def?.UsaDestinatarios ?? true;
        return new AlertTypeDto(
            t.TypeCode,
            t.Module,
            def?.Description ?? string.Empty,
            t.Severity.ToString(),
            t.Permisos(),
            Canales(t.Channels),
            Umbrales(t.ThresholdsJson),
            t.ValidFrom,
            t.ValidTo,
            string.IsNullOrWhiteSpace(t.Reason) ? null : t.Reason,
            t.IsEnabled,
            destinatariosActivos,
            usaLista && destinatariosActivos == 0,
            def?.DisponibleDesde ?? string.Empty);
    }

    public static IReadOnlyList<string> Canales(AlertChannels canales) =>
        Enum.GetValues<AlertChannels>().Where(c => canales.HasFlag(c)).Select(c => c.ToString()).ToList();

    public static IReadOnlyDictionary<string, decimal>? Umbrales(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, decimal>>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
