using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes;

/// <summary>
/// <c>LND_MembershipWithdrawals</c> (feature 014, US10, T096; data-model §6.1; FR-005, FR-076 a FR-080, FR-124, D14, D16, R15): el trámite del retiro del
/// asociado, de la solicitud a la devolución. Documento con estado (no hecho):
/// <code>
/// Filed ──aprobación con acta──▶ Approved ──liquidación + cruce──▶ Settled ──devolución girada──▶ Paid
///   │ └─ rechazo con acta ─▶ Rejected                                 │  ▲
///   └─ desiste ─▶ Desisted                     reverso de un pago ─────▼  │ reliquidación
///                                                                    ToRedo
/// </code>
/// Una solicitud viva por persona (dos únicos filtrados para <c>Filed</c> y <c>Approved</c>; <c>Settled</c> y <c>ToRedo</c> los cubre la regla bajo el
/// cerrojo de la persona). Las operaciones las escribe <c>RegistroDeCuentas</c>; esta fila sólo las nombra. (nuevo)
/// </summary>
public class MembershipWithdrawal : AuditableEntity
{
    public const string CodigoTransicionInvalida = "Lending.MembershipWithdrawal.InvalidTransition";
    public const int LargoDelNumero = 20;
    public const int LargoDelDetalle = 500;
    public const int LargoDelMotivoDeDesistimiento = 300;

    /// <summary>Numerador <c>MembershipWithdrawal</c> (18); único.</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>El asociado.</summary>
    public int PersonId { get; set; }

    /// <summary>Su cuenta de aportes viva al radicar.</summary>
    public int ContributionAccountId { get; set; }

    public MembershipWithdrawalCause Cause { get; set; }

    public string? CauseDetail { get; set; }

    /// <summary>Los soportes (lista JSON de <c>PublicId</c> de adjuntos).</summary>
    public string SupportAttachmentPublicIdsJson { get; set; } = "[]";

    public DateOnly FiledOn { get; set; }

    public int FiledByUserId { get; set; }

    public MembershipWithdrawalStatus Status { get; set; } = MembershipWithdrawalStatus.Filed;

    /// <summary>El punto del acta del órgano (consejo o junta) que aprobó o rechazó.</summary>
    public int? ApprovalSessionItemId { get; set; }

    /// <summary>La fecha del retiro; la fija la aprobación.</summary>
    public DateOnly? EffectiveDate { get; set; }

    public DateTime? DecidedAt { get; set; }

    public int? DecidedByUserId { get; set; }

    public string? DecisionReason { get; set; }

    public DateTime? DesistedAt { get; set; }

    public string? DesistReason { get; set; }

    /// <summary>El <c>RetiroDeAsociadoAprobado</c> v1 que se emitió a Asociados al aprobar (FR-124).</summary>
    public Guid? ApprovedMessagePublicId { get; set; }

    /// <summary>0 hasta liquidar; sube en cada reliquidación tras <c>ToRedo</c>.</summary>
    public int SettlementVersion { get; set; }

    public DateTime? SettledAt { get; set; }

    public int? SettledByUserId { get; set; }

    /// <summary>La primera operación interna del cruce (FR-077); las demás las nombran las líneas.</summary>
    public long? OffsetOperationId { get; set; }

    public decimal? ContributionsTotal { get; set; }

    public decimal? SavingsTotal { get; set; }

    public decimal? LoansOffsetTotal { get; set; }

    public decimal? LossDeductionAmount { get; set; }

    public decimal? RefundAmount { get; set; }

    /// <summary><c>EffectiveDate</c> + los días de devolución de la versión de aportes (o de <c>Captaciones.DiasPlazoDevolucionAportes</c>).</summary>
    public DateOnly? RefundDueOn { get; set; }

    /// <summary>FR-080: la devolución perforaría el mínimo irreducible de la entidad; queda pendiente y se avisa al gerente.</summary>
    public bool RefundHeldByInstitutionalMinimum { get; set; }

    /// <summary>La devolución sobre la atribución (<c>ContributionRefund</c>); llega con las atribuciones con monto (US4).</summary>
    public int? RefundDecisionId { get; set; }

    public DateTime? PaidAt { get; set; }

    /// <summary>La liquidación emitida (<c>MembershipWithdrawalSettlement</c>) de la versión vigente.</summary>
    public int? SettlementIssuedDocumentId { get; set; }

    /// <summary>El reverso de un pago cruzado que dejó la liquidación por rehacer (R15).</summary>
    public string? ToRedoReason { get; set; }

    /// <summary>
    /// Decisión del dueño del 2026-10-18 (D45): qué eligió quien liquidó para el saldo de los créditos que el cruce no cubrió; nulo si no quedó deuda.
    /// </summary>
    public PendingLoanBalanceHandling? PendingLoanBalanceHandling { get; set; }

    // ------------------------------------------------------------------------------------------------ comportamiento --

    /// <summary>Los estados de un trámite vivo: uno por persona.</summary>
    public static IReadOnlySet<MembershipWithdrawalStatus> Vivos { get; } = new HashSet<MembershipWithdrawalStatus>
    {
        MembershipWithdrawalStatus.Filed, MembershipWithdrawalStatus.Approved, MembershipWithdrawalStatus.Settled, MembershipWithdrawalStatus.ToRedo,
    };

    /// <summary>¿Está vivo el trámite? (radicado, aprobado, liquidado sin pagar o por rehacer).</summary>
    public bool EsVivo => Vivos.Contains(Status);

    /// <summary>¿Admite liquidar? Aprobado, o por rehacer tras el reverso de un pago cruzado.</summary>
    public bool AdmiteLiquidar => Status is MembershipWithdrawalStatus.Approved or MembershipWithdrawalStatus.ToRedo;

    /// <summary>Radica el retiro (FR-076).</summary>
    public static MembershipWithdrawal Radicar(string numero, int personaId, int cuentaDeAportesId, MembershipWithdrawalCause causa, string? detalle,
        IReadOnlyList<Guid> soportes, DateOnly fecha, int usuario)
    {
        ArgumentNullException.ThrowIfNull(soportes);
        ArgumentException.ThrowIfNullOrWhiteSpace(numero);
        return new MembershipWithdrawal
        {
            Number = numero,
            PersonId = personaId,
            ContributionAccountId = cuentaDeAportesId,
            Cause = causa,
            CauseDetail = Recortar(detalle, LargoDelDetalle),
            SupportAttachmentPublicIdsJson = "[" + string.Join(",", soportes.Distinct().Select(s => $"\"{s:D}\"")) + "]",
            FiledOn = fecha,
            FiledByUserId = usuario,
            Status = MembershipWithdrawalStatus.Filed,
        };
    }

    /// <summary>Desiste antes de aprobarse.</summary>
    public void Desistir(string motivo, DateTime instante)
    {
        Exigir(MembershipWithdrawalStatus.Filed, MembershipWithdrawalStatus.Desisted);
        Status = MembershipWithdrawalStatus.Desisted;
        DesistedAt = instante;
        DesistReason = Recortar(motivo, LargoDelMotivoDeDesistimiento);
    }

    /// <summary>Aprueba con el punto del acta y la fecha efectiva; el mensaje a Asociados nace en el mismo guardado.</summary>
    public void Aprobar(int puntoDeActa, DateOnly fechaEfectiva, int usuario, string motivo, DateTime instante, Guid mensaje)
    {
        Exigir(MembershipWithdrawalStatus.Filed, MembershipWithdrawalStatus.Approved);
        Status = MembershipWithdrawalStatus.Approved;
        ApprovalSessionItemId = puntoDeActa;
        EffectiveDate = fechaEfectiva;
        DecidedByUserId = usuario;
        DecidedAt = instante;
        DecisionReason = Recortar(motivo, LargoDelDetalle);
        ApprovedMessagePublicId = mensaje;
    }

    /// <summary>Niega con el punto del acta.</summary>
    public void Rechazar(int puntoDeActa, int usuario, string motivo, DateTime instante)
    {
        Exigir(MembershipWithdrawalStatus.Filed, MembershipWithdrawalStatus.Rejected);
        Status = MembershipWithdrawalStatus.Rejected;
        ApprovalSessionItemId = puntoDeActa;
        DecidedByUserId = usuario;
        DecidedAt = instante;
        DecisionReason = Recortar(motivo, LargoDelDetalle);
    }

    /// <summary>Liquida (o reliquida tras <c>ToRedo</c>): sube la versión y sella los totales de la versión nueva.</summary>
    public void Liquidar(int usuario, DateTime instante, decimal aportes, decimal ahorros, decimal cruces, decimal perdidas, decimal devolucion,
        DateOnly devolverHasta, bool retenidaPorMinimo, long? primeraOperacionDeCruce)
    {
        if (!AdmiteLiquidar)
            throw new ReglaDeCarteraException(new ProblemaDeCartera(CodigoTransicionInvalida,
                $"El retiro {Number} está en «{Status}»: sólo se liquida uno aprobado o por rehacer."));
        if (devolucion < 0m) throw new ArgumentOutOfRangeException(nameof(devolucion), devolucion, "La devolución no es negativa.");
        SettlementVersion += 1;
        Status = MembershipWithdrawalStatus.Settled;
        SettledAt = instante;
        SettledByUserId = usuario;
        ContributionsTotal = aportes;
        SavingsTotal = ahorros;
        LoansOffsetTotal = cruces;
        LossDeductionAmount = perdidas;
        RefundAmount = devolucion;
        RefundDueOn = devolverHasta;
        RefundHeldByInstitutionalMinimum = retenidaPorMinimo;
        OffsetOperationId = primeraOperacionDeCruce;
        ToRedoReason = null;
        SettlementIssuedDocumentId = null;
    }

    /// <summary>La devolución quedó retenida o liberada (FR-080) al reintentarla.</summary>
    public void MarcarRetencionPorMinimo(bool retenida) => RefundHeldByInstitutionalMinimum = retenida;

    /// <summary>El reverso de un pago cruzado deja la liquidación por rehacer (US10-4).</summary>
    public void PorRehacer(string motivo)
    {
        if (Status is not (MembershipWithdrawalStatus.Settled or MembershipWithdrawalStatus.ToRedo))
            throw new ReglaDeCarteraException(new ProblemaDeCartera(CodigoTransicionInvalida,
                $"El retiro {Number} está en «{Status}»: sólo un retiro liquidado queda por rehacer."));
        Status = MembershipWithdrawalStatus.ToRedo;
        ToRedoReason = Recortar(motivo, LargoDelDetalle);
    }

    /// <summary>
    /// Decisión del dueño del 2026-10-18 (D44): el reverso tardío de un pago del cruce, con el retiro ya pagado y sus cuentas canceladas, reabre el retiro
    /// (<c>Paid → Settled</c>): el saldo devuelto a la cuenta reabierta queda a favor del exasociado y se ordena con «Aprobar devolución».
    /// </summary>
    public void ReabrirPorReversoTardio(string motivo)
    {
        Exigir(MembershipWithdrawalStatus.Paid, MembershipWithdrawalStatus.Settled);
        Status = MembershipWithdrawalStatus.Settled;
        PaidAt = null;
        ToRedoReason = Recortar(motivo, LargoDelDetalle);
    }

    /// <summary>Todas sus órdenes de devolución quedaron giradas (o no había nada que devolver).</summary>
    public void Pagar(DateTime instante)
    {
        Exigir(MembershipWithdrawalStatus.Settled, MembershipWithdrawalStatus.Paid);
        Status = MembershipWithdrawalStatus.Paid;
        PaidAt = instante;
    }

    private void Exigir(MembershipWithdrawalStatus desde, MembershipWithdrawalStatus hacia)
    {
        if (Status != desde)
            throw new ReglaDeCarteraException(new ProblemaDeCartera(CodigoTransicionInvalida,
                $"El retiro {Number} está en «{Status}» y no puede pasar a «{hacia}».",
                new Dictionary<string, object?> { ["from"] = Status.ToString(), ["to"] = hacia.ToString() }));
    }

    private static string? Recortar(string? texto, int largo)
    {
        if (texto is null) return null;
        var t = texto.Trim();
        return t.Length <= largo ? t : t[..largo];
    }
}
