using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes;

/// <summary>
/// <c>LND_AssemblyDistributions</c> (feature 014, US12, T202; data-model §6.3; FR-045 a FR-047, D13; research §22): la revalorización de aportes, la
/// distribución de excedentes o la amortización de aportes que decide la asamblea general. Documento con estado (no hecho):
/// <code>
/// Simulated ──acta cerrada + segundo usuario──▶ Applied ──reverso entero──▶ Reversed
///    └─ descartada ─▶ Voided
/// </code>
/// Se registra en borrador con lo decidido (clase, ejercicio, base, porcentaje o valor, fondo, destino) y se simula cuantas veces haga falta; se lleva al
/// orden del día de la asamblea (asunto <c>AssemblyDistribution</c>) y la aplica <b>otra persona</b> con el punto del acta cerrado que la aprobó, el soporte
/// y la huella de la simulación que vio (segregación radicar × decidir, <c>SegregacionDeCaptaciones.DecisionDeAsamblea</c>). Aplicar escribe una
/// operación por cuenta y las líneas (<see cref="AssemblyDistributionLine"/>), y emite un hecho masivo. Se corrige reversándola entera y registrando otra
/// que <b>corrige</b> a la reversada (hereda su acta): dos aplicadas con la misma acta y la misma clase no conviven
/// (<c>UK_LND_AssemblyDistributions_Session</c>). (nuevo)
/// </summary>
public class AssemblyDistribution : AuditableEntity
{
    public const string CodigoTransicionInvalida = "Lending.AssemblyDistribution.InvalidTransition";
    public const int LargoDelNumero = 20;
    public const int LargoDelMotivo = 500;
    public const int LargoDeLaFuente = 100;

    /// <summary>Numerador <c>AssemblyDistribution</c> (19); único.</summary>
    public string Number { get; set; } = string.Empty;

    public AssemblyDistributionKind Kind { get; set; }

    /// <summary>El ejercicio cuyos excedentes se aplican (la base se mide en él).</summary>
    public short FiscalYear { get; set; }

    /// <summary>El punto del acta de la asamblea que la aprobó; lo fija la aplicación (o lo hereda la corrección de una reversada).</summary>
    public int? DecisionSessionItemId { get; set; }

    /// <summary>La distribución reversada que ésta corrige (hereda su acta).</summary>
    public int? CorrectsDistributionId { get; set; }

    public RevaluationBase BaseMethod { get; set; } = RevaluationBase.AverageBalance;

    /// <summary>El porcentaje decidido (fracción); nulo si se decidió un valor.</summary>
    public decimal? DecidedFraction { get; set; }

    /// <summary>El valor total decidido; nulo si se decidió un porcentaje.</summary>
    public decimal? DecidedValue { get; set; }

    /// <summary>Revalorización: el límite legal que rigió (el IPC del ejercicio) y su fuente; los sella la aplicación.</summary>
    public decimal? LegalLimitFraction { get; set; }

    public string? LegalLimitSource { get; set; }

    /// <summary>Lo disponible en el fondo según el acta (para revalorizar, para distribuir o para amortizar).</summary>
    public decimal FundAmount { get; set; }

    public decimal CalculatedTotal { get; set; }

    public decimal? ProrationFactor { get; set; }

    public decimal AppliedTotal { get; set; }

    /// <summary>Excedentes: a aportes, a una cuenta de ahorro de la línea de destino o en pago por orden de giro.</summary>
    public SurplusDestination? SurplusDestination { get; set; }

    /// <summary>Con <see cref="Enums.Lending.SurplusDestination.Savings"/>: la línea de ahorro donde cada asociado recibe.</summary>
    public int? DestinationLineId { get; set; }

    /// <summary>La unidad de redondeo de la entidad que rigió (1 al peso, 0,01 al centavo; <c>Asamblea.Redondeo</c>).</summary>
    public decimal RoundingUnit { get; set; } = 1m;

    public AssemblyDistributionStatus Status { get; set; } = AssemblyDistributionStatus.Simulated;

    /// <summary>La versión de las líneas que se escribió al aplicar (1; data-model §6.4).</summary>
    public int SimulationVersion { get; set; }

    public string Reason { get; set; } = string.Empty;

    public int RegisteredByUserId { get; set; }

    public DateTime RegisteredAt { get; set; }

    /// <summary>Quién la aprobó y aplicó (otra persona que quien la registró).</summary>
    public int? ApprovedByUserId { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime? AppliedAt { get; set; }

    /// <summary>La fecha valor y contable de las operaciones (el día de la aplicación).</summary>
    public DateOnly? AppliedOn { get; set; }

    /// <summary>El acta firmada y el proyecto de distribución (lista JSON de <c>PublicId</c> de adjuntos <c>LendingAssemblySupport</c>).</summary>
    public string SupportAttachmentPublicIdsJson { get; set; } = "[]";

    /// <summary>El hecho masivo que emitió la aplicación.</summary>
    public Guid? AppliedMessagePublicId { get; set; }

    public DateTime? ReversedAt { get; set; }

    public int? ReversedByUserId { get; set; }

    public string? ReversalReason { get; set; }

    public DateTime? VoidedAt { get; set; }

    public string? VoidReason { get; set; }

    /// <summary>La huella de lo que se aplicó (la de la simulación que vio quien aplicó).</summary>
    public string InputsSha256 { get; set; } = string.Empty;

    // ------------------------------------------------------------------------------------------------ comportamiento --

    public void Aplicar(int sessionItemId, int usuario, DateTime instante, DateOnly fecha, decimal calculado, decimal? factor, decimal aplicado,
        decimal? limiteLegal, string? fuenteDelLimite, string huella, string soportesJson)
    {
        Exigir(AssemblyDistributionStatus.Simulated, AssemblyDistributionStatus.Applied);
        DecisionSessionItemId = sessionItemId;
        ApprovedByUserId = usuario;
        ApprovedAt = instante;
        AppliedAt = instante;
        AppliedOn = fecha;
        CalculatedTotal = calculado;
        ProrationFactor = factor;
        AppliedTotal = aplicado;
        LegalLimitFraction = limiteLegal;
        LegalLimitSource = fuenteDelLimite is { Length: > LargoDeLaFuente } f ? f[..LargoDeLaFuente] : fuenteDelLimite;
        InputsSha256 = huella;
        SupportAttachmentPublicIdsJson = soportesJson;
        SimulationVersion = 1;
        Status = AssemblyDistributionStatus.Applied;
    }

    public void Reversar(int usuario, DateTime instante, string motivo)
    {
        Exigir(AssemblyDistributionStatus.Applied, AssemblyDistributionStatus.Reversed);
        ReversedByUserId = usuario;
        ReversedAt = instante;
        ReversalReason = Recortar(motivo);
        Status = AssemblyDistributionStatus.Reversed;
    }

    public void Descartar(DateTime instante, string motivo)
    {
        Exigir(AssemblyDistributionStatus.Simulated, AssemblyDistributionStatus.Voided);
        VoidedAt = instante;
        VoidReason = Recortar(motivo);
        Status = AssemblyDistributionStatus.Voided;
    }

    public static string Recortar(string? texto)
    {
        var t = (texto ?? string.Empty).Trim();
        return t.Length <= LargoDelMotivo ? t : t[..LargoDelMotivo];
    }

    private void Exigir(AssemblyDistributionStatus desde, AssemblyDistributionStatus hacia)
    {
        if (Status != desde)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoTransicionInvalida,
                $"La decisión de asamblea {Number} está en «{Status}» y no pasa a «{hacia}».", "status", Status.ToString()));
    }
}

/// <summary>
/// <c>LND_AssemblyDistributionLines</c> (feature 014, US12, T202; data-model §6.4; FR-045, FR-046): lo que la aplicación de una decisión de asamblea dejó a
/// un asociado —la base, lo calculado, lo aplicado tras el prorrateo— con la operación que lo abonó o devolvió, la cuenta de ahorro de destino y la orden de
/// giro del pago. Hecho de sólo inserción (<see cref="IHechoInmutable"/>): se escribe una sola vez, al aplicar, con todo. (nuevo)
/// </summary>
public class AssemblyDistributionLine : AuditableEntity, IHechoInmutable
{
    public int AssemblyDistributionId { get; init; }

    public int SimulationVersion { get; init; }

    public int PersonId { get; init; }

    public int ContributionAccountId { get; init; }

    /// <summary>Excedentes a ahorro: la cuenta que recibe; nula en los demás destinos.</summary>
    public int? DestinationAccountId { get; init; }

    public decimal BaseAmount { get; init; }

    public decimal CalculatedAmount { get; init; }

    public decimal ProratedAmount { get; init; }

    /// <summary>Amortización: el mínimo no reducible acotó lo devuelto.</summary>
    public bool LimitedByMinimum { get; init; }

    /// <summary>La operación de cuenta que lo abonó o devolvió (nula si lo aplicado es cero).</summary>
    public long? OperationId { get; init; }

    /// <summary>La orden de giro de la amortización o del pago de excedentes.</summary>
    public int? PayoutOrderId { get; init; }

    /// <summary>
    /// Respuesta 91 B del dueño (E259): el asociado se retiró durante el ejercicio; participó a prorrata del tiempo que lo fue y lo que le tocó se le giró
    /// (no se capitalizó ni se llevó a ahorro: ya no tiene vínculo).
    /// </summary>
    public bool WithdrawnDuringYear { get; init; }

    /// <summary>
    /// Respuesta 89 B del dueño (E260): la retención en la fuente practicada sobre lo aplicado a este asociado (0 si no se retuvo). Lo que se le abona o se le gira
    /// es <see cref="ProratedAmount"/> menos esto.
    /// </summary>
    public decimal WithheldAmount { get; init; }

    /// <summary>La tarifa con que se retuvo (fracción); nula si no se retuvo.</summary>
    public decimal? WithholdingRate { get; init; }

    /// <summary>El concepto de retención vigente al aplicar (<c>Asamblea.Retencion.Concepto</c>); nulo si no se retuvo.</summary>
    public string? WithholdingConcept { get; init; }
}
