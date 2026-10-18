using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Collections;

/// <summary>
/// <c>LND_MessageCampaigns</c> (feature 013, K4, T0936; data-model §26.3; FR-122, US12-4): una campaña de mensajes al deudor por un canal y una
/// plantilla, sobre los créditos que dicen sus criterios. Programarla crea un mensaje por destinatario (<c>LND_DebtorMessages.CampaignId</c>);
/// el excluido queda como mensaje con su evento <c>Excluded</c> y motivo. El ciclo:
/// <code>
/// Draft ──programar──▶ Scheduled ──empieza a salir──▶ Running ──▶ Completed
/// Draft · Scheduled · Running ──cancelar (motivo; sus mensajes en cola pasan a Cancelled)──▶ Cancelled
/// </code>
/// (nuevo)
/// </summary>
public class MessageCampaign : AuditableEntity
{
    public const int LargoDelCodigo = 10;
    public const string CodigoEstadoInvalido = "Lending.Campaign.InvalidStatus";

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public DebtorMessagePurpose Purpose { get; init; }

    public MessageChannel Channel { get; init; }

    public int TemplateId { get; init; }

    /// <summary>Etapa, días de mora, línea, oficina, pagaduría.</summary>
    public string SelectionCriteriaJson { get; init; } = "{}";

    public DateTime ScheduledFor { get; init; }

    public MessageCampaignStatus Status { get; private set; } = MessageCampaignStatus.Draft;

    public int TargetCount { get; private set; }

    public int QueuedCount { get; private set; }

    public int ExcludedCount { get; private set; }

    /// <summary><c>Lending.Campaigns.Manage</c>.</summary>
    public int CreatedByUserId { get; init; }

    public int? ScheduledByUserId { get; private set; }

    public DateTime? CancelledAt { get; private set; }

    public int? CancelledByUserId { get; private set; }

    public string? CancelReason { get; private set; }

    public static MessageCampaign Nueva(string codigo, string nombre, DebtorMessagePurpose proposito, MessageChannel canal, int plantillaId,
        string criteriosJson, DateTime programadaPara, int usuarioId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(criteriosJson);
        var code = codigo.Trim().ToUpperInvariant();
        if (code.Length > LargoDelCodigo || code.Any(char.IsWhiteSpace))
            throw new ArgumentException($"El código de la campaña es de hasta {LargoDelCodigo} caracteres y sin espacios.", nameof(codigo));
        if (proposito is not (DebtorMessagePurpose.Collection or DebtorMessagePurpose.Informative))
            throw new ArgumentException("Una campaña es de cobranza o informativa.", nameof(proposito));
        return new MessageCampaign
        {
            Code = code,
            Name = nombre.Trim(),
            Purpose = proposito,
            Channel = canal,
            TemplateId = plantillaId,
            SelectionCriteriaJson = criteriosJson,
            ScheduledFor = programadaPara,
            CreatedByUserId = usuarioId,
        };
    }

    private ProblemaDeCartera Invalido(MessageCampaignStatus hacia) => new(CodigoEstadoInvalido,
        $"La campaña está en «{Status}» y no puede pasar a «{hacia}».",
        new Dictionary<string, object?> { ["status"] = Status.ToString(), ["requested"] = hacia.ToString() });

    /// <summary><c>Draft</c> → <c>Scheduled</c> con los conteos de la programación (el objetivo es encolados más excluidos).</summary>
    public void Programar(int usuarioId, int encolados, int excluidos)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(encolados);
        ArgumentOutOfRangeException.ThrowIfNegative(excluidos);
        if (Status != MessageCampaignStatus.Draft) throw new ReglaDeCarteraException(Invalido(MessageCampaignStatus.Scheduled));
        Status = MessageCampaignStatus.Scheduled;
        ScheduledByUserId = usuarioId;
        QueuedCount = encolados;
        ExcludedCount = excluidos;
        TargetCount = encolados + excluidos;
    }

    public void Iniciar()
    {
        if (Status != MessageCampaignStatus.Scheduled) throw new ReglaDeCarteraException(Invalido(MessageCampaignStatus.Running));
        Status = MessageCampaignStatus.Running;
    }

    public void Completar()
    {
        if (Status != MessageCampaignStatus.Running) throw new ReglaDeCarteraException(Invalido(MessageCampaignStatus.Completed));
        Status = MessageCampaignStatus.Completed;
    }

    public void Cancelar(int usuarioId, string motivo, DateTime instante)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        if (Status is MessageCampaignStatus.Completed or MessageCampaignStatus.Cancelled)
            throw new ReglaDeCarteraException(Invalido(MessageCampaignStatus.Cancelled));
        Status = MessageCampaignStatus.Cancelled;
        CancelledByUserId = usuarioId;
        CancelledAt = instante;
        CancelReason = motivo.Trim();
    }
}
