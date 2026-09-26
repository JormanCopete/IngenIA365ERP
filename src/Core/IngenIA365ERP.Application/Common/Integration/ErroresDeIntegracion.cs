using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// Los errores de la plataforma de mensajería (feature 012, T496–T504; api.md §25.4). Los del lado contable siguen en
/// <c>AccountingErrors</c> (<c>Accounting.InventoryBatch.AlreadyRunning</c> incluido). (nuevo)
/// </summary>
public static class ErroresDeIntegracion
{
    /// <summary>El código con que Contabilidad rechaza una fecha en período cerrado (<c>AccountingErrors.PeriodClosed</c>).</summary>
    public const string PeriodoContableCerrado = "Accounting.Period.Closed";

    public static Error VersionNoAceptada(string type, int version) =>
        new ErrorConDatos("Integration.VersionNotAccepted",
            $"El destino no acepta la versión {version} de {type}: el mensaje queda rechazado y nunca se reescribe.",
            new { type, version });

    public static Error MensajesNoRechazados(IReadOnlyList<Guid> messagePublicIds) =>
        new ErrorConDatos("Integration.Message.NotRejected",
            messagePublicIds.Count == 1
                ? "El mensaje no está rechazado: sólo se reprocesa lo rechazado."
                : $"{messagePublicIds.Count} mensajes no están rechazados: sólo se reprocesa lo rechazado.",
            new { messagePublicIds });

    public static Error ModoSigueSinPaso(IReadOnlyList<string> documentTypeCodes) =>
        new ErrorConDatos("Integration.SendNotApplicable.ModeStillNotPosted",
            $"El modo de paso de {string.Join(", ", documentTypeCodes)} sigue en «no pasa» a hoy: cámbielo antes de enviar lo que no pasó.",
            new { documentTypeCodes });

    public static Error MensajeNoEncontrado(Guid messagePublicId) =>
        new ErrorConDatos("Integration.Message.NotFound", "El mensaje no existe.", new { messagePublicId });

    public static Error ContenidoAlterado(Guid messagePublicId) =>
        new ErrorConDatos("Integration.Message.PayloadAltered",
            "El contenido guardado del mensaje no coincide con su huella (PayloadSha256): no se entrega.",
            new { messagePublicId });

    public static Error LoteNoEncontrado(Guid batchPublicId) =>
        new ErrorConDatos("Integration.Batch.NotFound", "El lote no existe.", new { batchPublicId });

    public static Error LoteConEntregasPendientes(Guid batchPublicId, int pending) =>
        new ErrorConDatos("Integration.Batch.HasPendingDeliveries",
            $"El lote todavía tiene {pending} entrega(s) por procesar: sigue en curso.",
            new { batchPublicId, pending });

    public static Error LoteNoIniciado(Guid batchPublicId) =>
        new ErrorConDatos("Integration.Batch.NotRunning", "El lote no está en curso: sólo se cierra un lote que corrió.", new { batchPublicId });

    public static Error DestinoNoDisponible(string destination) =>
        new ErrorConDatos("Integration.Destination.Unavailable",
            $"El destino {destination} todavía no está disponible en esta instalación.",
            new { destination });
}
