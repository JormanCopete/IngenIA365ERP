namespace IngenIA365ERP.Domain.Enums.Integration;

// Plataforma de ejecución y mensajería (feature 012, T039; decisiones-transversales §2.5 y
// data-model §26). Se guardan como int y un valor nunca se renumera. Ésta es la única tarea que
// crea BatchTrigger, BatchStatus y DeliveryAttemptOutcome: la fase 12 (US7, I2) los usa.

/// <summary>De negocio (lo consume un destino) o informativo (queda para consulta).</summary>
public enum IntegrationMessageKind { Business = 1, Informational = 2 }

/// <summary>
/// Estado de la entrega de un mensaje a un destino. <c>Rejected</c> → reprocesar → <c>InBatch</c>
/// (lote <c>Reprocess</c>); <c>NotApplicable</c> → enviar después → <c>InBatch</c> (lote
/// <c>SendNotApplicable</c>); <c>Processed</c> (Lending) → validación negativa → <c>ValidationFailed</c>.
/// </summary>
public enum DeliveryStatus { Pending = 0, InBatch = 1, Processed = 2, Rejected = 3, NotApplicable = 4, ValidationFailed = 5 }

public enum DeliveryMode { Online = 1, Batch = 2, NotPosted = 3, Always = 4 }

public enum BatchTrigger { Scheduled = 1, CashSessionClose = 2, PeriodClose = 3, Manual = 4, Reprocess = 5, SendNotApplicable = 6 }

public enum BatchStatus { Requested = 0, Running = 1, Completed = 2, CompletedWithRejections = 3, Empty = 4 }

/// <summary>Quién actúa: una persona (con fila en SEC_Users) o el proceso automático (sin ella).</summary>
public enum ActorKind { Person = 1, Process = 2 }

/// <summary>
/// Por dónde entró la operación. <c>Web</c>/<c>App</c> por la cabecera <c>X-Canal</c>; <c>Pos</c>
/// cuando el comando es de punto de venta; <c>Process</c> en segundo plano.
/// </summary>
public enum ExecutionChannel { Web = 1, App = 2, Pos = 3, Process = 4 }

public enum PrevalidationOutcome { Postable = 1, NoResponse = 2, NotApplicable = 3 }

/// <summary>El <c>Origin.Kind</c> del sobre: un documento, o una operación (cierre, reapertura, reclasificación).</summary>
public enum MessageOriginKind { Document = 1, Operation = 2 }

/// <summary>Lo que el destino respondió en un intento de entrega (el <c>ResultadoDeConsumo</c>).</summary>
public enum DeliveryAttemptOutcome { Processed = 1, AlreadyProcessed = 2, Rejected = 3, Retry = 4 }
