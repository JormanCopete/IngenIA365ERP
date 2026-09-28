using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Domain.Entities.ElectronicInvoicing;

/// <summary>
/// Un evento de contingencia 03 o 04 (<c>COR_DianContingencyEvents</c>; feature 012, I4, T692; data-model §18;
/// contracts/dian.md §7). La 04 sólo la declara el canal; la 03 el circuito (<c>Dian.UmbralFallasCircuito</c>) o una
/// persona con <c>ElectronicInvoicing.Contingencies.Declare</c>. Un solo evento abierto por tipo y canal; los
/// documentos se unen al abierto. Al cerrarse se copian las horas y la norma del parámetro vigente y se fija el plazo
/// (<c>PlazoDeContingencia</c>). Sus evidencias son adjuntos del dueño <c>DianContingencyEvent</c>.
/// </summary>
public class DianContingencyEvent : AuditableEntity
{
    public ContingencyType Type { get; set; }

    /// <summary>Máx. 40.</summary>
    public string ChannelCode { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public ActorKind DetectedByKind { get; set; }

    public int? DetectedByUserId { get; set; }

    /// <summary>«Proceso de integración» o la persona (máx. 150).</summary>
    public string DetectedByName { get; set; } = string.Empty;

    /// <summary>Máx. 500.</summary>
    public string Reason { get; set; } = string.Empty;

    public ContingencyEventStatus Status { get; set; } = ContingencyEventStatus.Open;

    /// <summary>Copia de <c>Dian.PlazoContingenciaHoras</c> a la fecha del cierre.</summary>
    public short DeadlineHoursApplied { get; set; }

    /// <summary>Copia de la norma del parámetro (máx. 200).</summary>
    public string LegalSource { get; set; } = string.Empty;

    /// <summary>Cierre + plazo (el de cada documento lo lleva su <c>TransmissionDeadline</c>).</summary>
    public DateTime? DeadlineAt { get; set; }

    /// <summary>Constancia presentada ante la DIAN.</summary>
    public DateTime? DeclaredToDianAt { get; set; }

    /// <summary>Máx. 60.</summary>
    public string? DeclaredToDianReference { get; set; }

    public ActorKind? ClosedByKind { get; set; }

    public int? ClosedByUserId { get; set; }

    /// <summary>Máx. 150.</summary>
    public string? ClosedByName { get; set; }

    /// <summary>Máx. 300.</summary>
    public string? CloseReason { get; set; }

    public bool EstaAbierto => Status == ContingencyEventStatus.Open;
}
