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

    // ------------------------------------------------------------------ registro de participantes (feature 013, T16, T20)

    /// <summary>Dos consumidores para el mismo destino: el arranque falla (T15; no es un error de una petición).</summary>
    public const string CodigoConsumidorDuplicado = "Integration.Destination.DuplicateConsumer";

    /// <summary>
    /// Feature 015, T0308 (FR-148, FR-223; 2026-10-26): con Recaudo y Tesorería reales no se registran dobles de prueba en ningún ambiente. Reemplaza a
    /// <c>Integration.Participant.TestDoubleInProduction</c>, que sólo los negaba en producción.
    /// </summary>
    public const string CodigoDoblesRetirados = "Integration.Participant.TestDoublesRetired";
    public const string CodigoVersionNoSoportada = "Integration.Participant.VersionNotSupported";
    public const string CodigoParticipanteSeCruza = "Integration.Participant.Overlaps";
    public const string CodigoParticipanteEnUso = "Integration.Participant.InUse";
    public const string CodigoParticipanteNoEncontrado = "Integration.Participant.NotFound";

    public static Error DoblesRetirados(string code) =>
        new ErrorConDatos(CodigoDoblesRetirados,
            "Los dobles de prueba se retiraron: Recaudo y Tesorería se registran por su propia activación. No se registra un doble en ningún ambiente.",
            new { code });

    public static Error VersionNoSoportada(string contract, int version, IReadOnlyList<int> supported) =>
        new ErrorConDatos(CodigoVersionNoSoportada,
            supported.Count == 0
                ? $"Cartera no conoce el contrato {contract}: no se puede adoptar ninguna versión."
                : $"Cartera no acepta la versión {version} de {contract}; acepta {string.Join(", ", supported)}.",
            new { contract, supported });

    public static Error ParticipanteSeCruza(string code, string? destination, DateOnly validFrom) =>
        new ErrorConDatos(CodigoParticipanteSeCruza,
            "Ya hay un participante vigente con esa clave o ese destino en esas fechas: habilitar o inhabilitar cierra la fila vigente desde una fecha posterior a su inicio.",
            new { code, destination, validFrom });

    public static Error ParticipanteEnUso(string code, string destination) =>
        new ErrorConDatos(CodigoParticipanteEnUso,
            $"El destino {destination} es el de Cartera activada: no se retira mientras Cartera esté activa.",
            new { code, destination });

    public static Error ParticipanteNoEncontrado(Guid participantPublicId) =>
        new ErrorConDatos(CodigoParticipanteNoEncontrado, "El participante del registro de integración no existe.", new { participantPublicId });

    public static Error DestinoNoDisponible(string destination) =>
        new ErrorConDatos("Integration.Destination.Unavailable",
            $"El destino {destination} todavía no está disponible en esta instalación.",
            new { destination });
}
