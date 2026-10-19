using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes;

/// <summary>
/// <c>LND_DeceasedHolderCases</c> (feature 014, US14, T207; data-model §6.5; FR-065, FR-072, FR-082, FR-083; research §23): el fallecimiento de un titular
/// de cuentas de captación y la entrega de sus saldos. Documento con estado (no hecho):
/// <code>
/// Registered ──entrega registrada──▶ DeliveryPending ──otra persona aprueba──▶ Approved ──todas las órdenes giradas──▶ Delivered
///    │                                     │  (se puede volver a registrar la entrega)        (las entregas por traslado cierran de una vez)
///    └─ anulado (registro errado) ─▶ Cancelled
/// </code>
/// Registrar el caso <b>bloquea</b> las cuentas vivas del titular (<c>AccountHold</c> <c>Deceased</c>, total), deja que la causación se suspenda si la línea
/// lo dice (FR-065) y no deja pagar un CDAT al vencer (FR-072). La entrega va a los beneficiarios vigentes <b>en la fecha de la defunción</b> (FR-034), sin
/// juicio de sucesión hasta el tope vigente (<c>TOPE_ENTREGA_SIN_SUCESION</c>) con los soportes del parámetro, o por sucesión; la aprueba otra persona
/// (segregación <c>SegregacionDeCaptaciones.EntregaPorFallecimiento</c>) y sale en operaciones <c>DeceasedDelivery</c> con órdenes de giro o traslados. Un
/// titular tiene a lo sumo un caso que no esté anulado (<c>UK_LND_DeceasedHolderCases_Person</c>). (nuevo)
/// </summary>
public class DeceasedHolderCase : AuditableEntity
{
    public const string CodigoTransicionInvalida = "Lending.DeceasedCase.InvalidTransition";
    public const int LargoDelNumero = 20;
    public const int LargoDelMotivo = 500;

    /// <summary>Numerador <c>DeceasedHolderCase</c> (20); único.</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>El titular fallecido.</summary>
    public int PersonId { get; set; }

    public DateOnly DeathDate { get; set; }

    /// <summary>El día en que la cooperativa conoció el fallecimiento (el del registro).</summary>
    public DateOnly ReportedOn { get; set; }

    /// <summary>El registro civil de defunción (adjunto <c>LendingDeceasedSupport</c> con dueño la persona).</summary>
    public Guid DeathCertificateAttachmentPublicId { get; set; }

    /// <summary>Por dónde se entrega; nulo hasta registrar la entrega.</summary>
    public DeliveryRoute? Route { get; set; }

    /// <summary>Los beneficiarios vigentes en la fecha de la defunción por cuenta (FR-034), sellados al registrar la entrega.</summary>
    public string? BeneficiarySnapshotJson { get; set; }

    /// <summary>Lo que se entrega (saldo de las cuentas a la fecha de la vista previa que se registró; la aprobación entrega el saldo del día).</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Con <c>WithoutProbate</c>: el <c>TOPE_ENTREGA_SIN_SUCESION</c> que rigió; el total no lo supera.</summary>
    public decimal? WithoutProbateCap { get; set; }

    /// <summary>Los soportes de la entrega (lista JSON de <c>PublicId</c>): los que pide el parámetro o la sucesión.</summary>
    public string SupportAttachmentPublicIdsJson { get; set; } = "[]";

    /// <summary>A quién, cuánto (porcentaje) y por qué medio, en JSON (<c>PlanDeEntrega</c>).</summary>
    public string? DeliveryPlanJson { get; set; }

    /// <summary>La huella de la vista previa que vio quien registró la entrega.</summary>
    public string? PreviewSha256 { get; set; }

    public DeceasedCaseStatus Status { get; set; } = DeceasedCaseStatus.Registered;

    public string Reason { get; set; } = string.Empty;

    public int RegisteredByUserId { get; set; }

    public DateTime RegisteredAt { get; set; }

    public int? DeliveryRegisteredByUserId { get; set; }

    public DateTime? DeliveryRegisteredAt { get; set; }

    /// <summary>Quien aprobó la entrega: otra persona que quien la registró (FR-083).</summary>
    public int? ApprovedByUserId { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime? DeliveredAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public int? CancelledByUserId { get; set; }

    public string? CancelReason { get; set; }

    // ------------------------------------------------------------------------------------------------ comportamiento --

    /// <summary>¿Está vivo? (no anulado ni entregado): mientras lo esté, las cuentas siguen bloqueadas.</summary>
    public bool Vivo => Status is DeceasedCaseStatus.Registered or DeceasedCaseStatus.DeliveryPending or DeceasedCaseStatus.Approved;

    /// <summary>Registra (o vuelve a registrar) la entrega: la ruta, el plan, los soportes y la huella de lo que se vio.</summary>
    public void RegistrarEntrega(DeliveryRoute ruta, string plan, string beneficiarios, string soportes, decimal total, decimal? tope, string huella, int usuario,
        DateTime instante)
    {
        if (Status is not (DeceasedCaseStatus.Registered or DeceasedCaseStatus.DeliveryPending))
            throw Transicion(DeceasedCaseStatus.DeliveryPending);
        Route = ruta;
        DeliveryPlanJson = plan;
        BeneficiarySnapshotJson = beneficiarios;
        SupportAttachmentPublicIdsJson = soportes;
        TotalAmount = total;
        WithoutProbateCap = tope;
        PreviewSha256 = huella;
        DeliveryRegisteredByUserId = usuario;
        DeliveryRegisteredAt = instante;
        Status = DeceasedCaseStatus.DeliveryPending;
    }

    public void Aprobar(int usuario, DateTime instante, decimal entregado)
    {
        if (Status != DeceasedCaseStatus.DeliveryPending) throw Transicion(DeceasedCaseStatus.Approved);
        ApprovedByUserId = usuario;
        ApprovedAt = instante;
        TotalAmount = entregado;
        Status = DeceasedCaseStatus.Approved;
    }

    public void Entregar(DateTime instante)
    {
        if (Status != DeceasedCaseStatus.Approved) throw Transicion(DeceasedCaseStatus.Delivered);
        DeliveredAt = instante;
        Status = DeceasedCaseStatus.Delivered;
    }

    public void Anular(int usuario, DateTime instante, string motivo)
    {
        if (Status is not (DeceasedCaseStatus.Registered or DeceasedCaseStatus.DeliveryPending)) throw Transicion(DeceasedCaseStatus.Cancelled);
        CancelledByUserId = usuario;
        CancelledAt = instante;
        CancelReason = Recortar(motivo);
        Status = DeceasedCaseStatus.Cancelled;
    }

    public static string Recortar(string? texto)
    {
        var t = (texto ?? string.Empty).Trim();
        return t.Length <= LargoDelMotivo ? t : t[..LargoDelMotivo];
    }

    private ReglaDeCarteraException Transicion(DeceasedCaseStatus hacia) => new(ProblemaDeCartera.Con(CodigoTransicionInvalida,
        $"El caso de fallecimiento {Number} está en «{Status}» y no pasa a «{hacia}».", "status", Status.ToString()));
}

/// <summary>
/// <c>LND_UnclaimedBalances</c> (feature 014, US14, T207; data-model §6.6; FR-081, FR-084; research §23): un saldo que pasó a «no reclamados» por titular —lo
/// devuelto y no cobrado (la orden de giro que Tesorería rechazó o anuló y nadie volvió a ordenar) o el saldo de una cuenta inactiva cuando el reglamento lo
/// ordena (<c>CuentasAbandonadas.TrasladoHabilitado</c>)—. El dinero sale de la cuenta (que se cancela) al concepto <c>Unclaimed</c> de su libro por la
/// operación <c>ToUnclaimed</c>, con su <c>SaldoTrasladadoANoReclamados</c>. Cuando el titular aparece se le paga por orden de giro o traslado (operación
/// <c>UnclaimedPayment</c> sobre la cuenta de origen aunque esté cancelada, única excepción a <c>Lending.Savings.AccountClosed</c>); si Tesorería rechaza ese
/// giro, el saldo vuelve a «retenido». Documento con estado. (nuevo)
/// </summary>
public class UnclaimedBalance : AuditableEntity
{
    public const string CodigoTransicionInvalida = "Lending.Unclaimed.InvalidTransition";
    public const int LargoDelMotivo = 500;

    /// <summary>El titular a quien se le debe.</summary>
    public int PersonId { get; set; }

    public int SourceMemberAccountId { get; set; }

    public UnclaimedSource Source { get; set; }

    /// <summary>Lo devuelto y no cobrado: la orden de giro que volvió.</summary>
    public int? SourcePayoutOrderId { get; set; }

    public decimal Amount { get; set; }

    public DateOnly TransferredOn { get; set; }

    /// <summary>La operación <c>ToUnclaimed</c> que lo trasladó.</summary>
    public long TransferOperationId { get; set; }

    public UnclaimedStatus Status { get; set; } = UnclaimedStatus.Held;

    public string Reason { get; set; } = string.Empty;

    /// <summary>Nulo = el proceso (el ciclo diario).</summary>
    public int? TransferredByUserId { get; set; }

    public DateOnly? ClaimedOn { get; set; }

    /// <summary>La operación <c>UnclaimedPayment</c> que lo pagó.</summary>
    public long? ClaimOperationId { get; set; }

    /// <summary>Con orden de giro: la orden.</summary>
    public int? ClaimPayoutOrderId { get; set; }

    public int? ClaimedByUserId { get; set; }

    // ------------------------------------------------------------------------------------------------ comportamiento --

    public void Reclamar(DateOnly dia, long operacion, int? orden, int? usuario)
    {
        if (Status != UnclaimedStatus.Held)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoTransicionInvalida, "El saldo no reclamado ya se pagó.", "status", Status.ToString()));
        ClaimedOn = dia;
        ClaimOperationId = operacion;
        ClaimPayoutOrderId = orden;
        ClaimedByUserId = usuario;
        Status = UnclaimedStatus.Claimed;
    }

    /// <summary>Tesorería rechazó o anuló el giro del pago: el saldo vuelve a quedar retenido para el titular.</summary>
    public void Retener()
    {
        if (Status != UnclaimedStatus.Claimed)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoTransicionInvalida, "El saldo no reclamado no estaba pagado.", "status", Status.ToString()));
        ClaimedOn = null;
        ClaimOperationId = null;
        ClaimPayoutOrderId = null;
        ClaimedByUserId = null;
        Status = UnclaimedStatus.Held;
    }

    public static string Recortar(string? texto)
    {
        var t = (texto ?? string.Empty).Trim();
        return t.Length <= LargoDelMotivo ? t : t[..LargoDelMotivo];
    }
}
