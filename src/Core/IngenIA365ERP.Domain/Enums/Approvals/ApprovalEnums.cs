namespace IngenIA365ERP.Domain.Enums.Approvals;

// Aprobaciones de la plataforma (feature 012; decisiones-transversales §2.5). Se guardan como int y un
// valor nunca se renumera. Las creó la sección de mensajes (T073–T078) antes que la de aprobaciones
// (T079) porque ApprovalMethod viaja en DiferenciaDeArqueoAprobadaV1 y VentaACreditoRegistradaV1
// (contracts/mensajes.md §6.15, §8.1); T079 las usa sin recrearlas y agrega ApprovalSubjects.

public enum ApprovalRequestStatus { Pending = 0, Approved = 1, Rejected = 2, Cancelled = 3 }

public enum ApprovalDecisionKind { Approve = 1, Reject = 2 }

/// <summary>Cómo decidió el aprobador: desde su propia sesión o en persona sobre la del solicitante.</summary>
public enum ApprovalMethod { OwnSession = 1, InPersonPasskey = 2, InPersonTotp = 3 }
