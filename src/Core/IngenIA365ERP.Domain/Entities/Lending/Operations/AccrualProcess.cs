using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Operations;

/// <summary>
/// <c>LND_AccrualProcesses</c> (2026-11-16, pedido del dueño; feature 024, fase 3): el proceso de causación que la persona arma, revisa en borrador y
/// pone en firme con la aprobación de otra persona. La cabecera guarda lo que se pidió —clase, periodicidad, quincena o década, rango de días causados,
/// conceptos y filtros (línea, oficina, pagaduría)—, el resultado del último cálculo (totales, huella, bloqueos y avisos en <see cref="IssuesJson"/>), el
/// motivo y la aprobación, y el lote que ordenó. Sus hechos (<see cref="AccrualProcessFact"/>) son las unidades que se contabilizan, y sus líneas
/// (<see cref="AccrualProcessLine"/>), lo de cada crédito y concepto:
/// <code>
/// Draft ──pedir firma (motivo)──▶ PendingApproval ──aprobación de otra persona──▶ Confirmed ──su reproceso en firme──▶ Reprocessed
/// Draft · PendingApproval ──anular (motivo)──▶ Voided;  PendingApproval ──rechazo, retiro o recálculo──▶ Draft
/// </code>
/// El borrador no contabiliza nada y se recalcula cuantas veces se quiera. Un proceso en firme no se anula ni se edita: se reprocesa con otro proceso
/// (<see cref="ReprocessOfId"/>), que al ponerse en firme lo deja <see cref="AccrualProcessStatus.Reprocessed"/>. No es un hecho del libro. (nuevo)
/// </summary>
public class AccrualProcess : AuditableEntity
{
    public const string CodigoNoBorrador = "Lending.AccrualProcess.NotDraft";
    public const string CodigoEstadoInvalido = "Lending.AccrualProcess.InvalidStatus";

    /// <summary>Numerador de Cartera (<c>PCA-000001</c>); único.</summary>
    public string Number { get; init; } = string.Empty;

    public AccrualProcessKind Kind { get; init; } = AccrualProcessKind.Posting;

    public AccrualProcessStatus Status { get; private set; } = AccrualProcessStatus.Draft;

    public AccrualProcessPeriodicity Periodicity { get; set; }

    /// <summary>La quincena (1 o 2) en la quincenal o la década (1 a 3) en la decadal; nulo en las demás.</summary>
    public byte? SubPeriod { get; set; }

    /// <summary>El primer día causado del rango (incluido).</summary>
    public DateOnly DateFrom { get; set; }

    /// <summary>El último día causado del rango (incluido).</summary>
    public DateOnly DateTo { get; set; }

    public AccrualProcessConcepts Concepts { get; set; }

    /// <summary>Filtro opcional: la línea de crédito (<c>LND_CreditLines</c>).</summary>
    public int? CreditLineId { get; set; }

    /// <summary>Filtro opcional: la oficina (<c>COR_Branches</c>), la sucursal contable de los créditos y de sus hechos.</summary>
    public int? BranchId { get; set; }

    /// <summary>Filtro opcional: la pagaduría (<c>COR_People</c>) del vínculo de libranza vigente del crédito.</summary>
    public int? PaymasterPersonId { get; set; }

    /// <summary>El proceso en firme que éste reprocesa; nulo en uno normal.</summary>
    public int? ReprocessOfId { get; init; }

    /// <summary>El reproceso en firme que reemplazó a éste.</summary>
    public int? ReprocessedById { get; private set; }

    /// <summary>Quien armó el proceso (<c>SEC_Users</c>).</summary>
    public int CreatedByUserId { get; init; }

    // ------------------------------------------------------------------------------------------------------ el cálculo --

    public int CalculationCount { get; private set; }

    public DateTime? CalculatedAt { get; private set; }

    public int? CalculatedByUserId { get; private set; }

    /// <summary>La huella del último cálculo (parámetros, hechos con su estado y bloqueos): lo que se pide aprobar.</summary>
    public string? ContentSha256 { get; private set; }

    /// <summary>Los bloqueos y avisos del último cálculo (JSON <c>{ blockers: [{ code, message, data }], warnings: [] }</c>).</summary>
    public string IssuesJson { get; private set; } = "{}";

    /// <summary>Si el último cálculo dejó algo que impide ponerlo en firme (un bloqueo o un hecho excluido que bloquea).</summary>
    public bool HasBlockingIssues { get; private set; }

    public int LoanCount { get; private set; }

    public int IncludedFactCount { get; private set; }

    public int ExcludedFactCount { get; private set; }

    public decimal IncludedAmount { get; private set; }

    public decimal ExcludedAmount { get; private set; }

    // ------------------------------------------------------------------------------------- la firma y su aprobación --

    /// <summary>El motivo de la puesta en firme (o del reproceso).</summary>
    public string? Reason { get; private set; }

    public int? RequestedByUserId { get; private set; }

    public DateTime? RequestedAt { get; private set; }

    /// <summary>La solicitud de la plataforma de aprobaciones (<c>COR_ApprovalRequests</c>).</summary>
    public Guid? ApprovalRequestPublicId { get; private set; }

    public int? ApprovedByUserId { get; private set; }

    public string? ApprovedByName { get; private set; }

    public ApprovalMethod? ApprovalMethod { get; private set; }

    public DateTime? ConfirmedAt { get; private set; }

    /// <summary>El lote de contabilización que ordenó al ponerse en firme (<c>COR_IntegrationBatches</c>).</summary>
    public Guid? BatchPublicId { get; private set; }

    public long? BatchNumber { get; private set; }

    public string? VoidReason { get; private set; }

    public DateTime? VoidedAt { get; private set; }

    public int? VoidedByUserId { get; private set; }

    // ------------------------------------------------------------------------------------------------ comportamiento --

    private static readonly IReadOnlyDictionary<AccrualProcessStatus, AccrualProcessStatus[]> Transiciones =
        new Dictionary<AccrualProcessStatus, AccrualProcessStatus[]>
        {
            [AccrualProcessStatus.Draft] = [AccrualProcessStatus.PendingApproval, AccrualProcessStatus.Voided],
            [AccrualProcessStatus.PendingApproval] = [AccrualProcessStatus.Draft, AccrualProcessStatus.Confirmed, AccrualProcessStatus.Voided],
            [AccrualProcessStatus.Confirmed] = [AccrualProcessStatus.Reprocessed],
        };

    public static bool PuedePasar(AccrualProcessStatus desde, AccrualProcessStatus hacia) =>
        Transiciones.TryGetValue(desde, out var destinos) && destinos.Contains(hacia);

    /// <summary>¿Está abierto (borrador o esperando la aprobación)? Un proceso abierto de un concepto impide abrir otro del mismo concepto.</summary>
    public bool Abierto => Status is AccrualProcessStatus.Draft or AccrualProcessStatus.PendingApproval;

    /// <summary>Nulo si se puede recalcular (borrador o esperando la aprobación, que vuelve a borrador); si no, <see cref="CodigoNoBorrador"/>.</summary>
    public ProblemaDeCartera? ExigirAbierto() => Abierto
        ? null
        : ProblemaDeCartera.Con(CodigoNoBorrador, "El proceso ya no es un borrador: un proceso en firme no se recalcula ni se anula; se reprocesa.",
            "status", Status.ToString());

    public ProblemaDeCartera? PuedeIrA(AccrualProcessStatus hacia) => PuedePasar(Status, hacia)
        ? null
        : new ProblemaDeCartera(CodigoEstadoInvalido, $"El proceso está en «{Status}» y no puede pasar a «{hacia}».",
            new Dictionary<string, object?> { ["status"] = Status.ToString(), ["requested"] = hacia.ToString() });

    /// <summary>
    /// Sella el resultado de un cálculo (sólo abierto): totales, huella, bloqueos y avisos, quién y cuándo. Uno esperando la aprobación vuelve a borrador
    /// (quien llama invalida la solicitud: lo aprobado cambió).
    /// </summary>
    public void SellarCalculo(string huella, string issuesJson, bool bloquea, int creditos, int incluidos, int excluidos, decimal montoIncluido,
        decimal montoExcluido, int usuario, DateTime ahora)
    {
        ReglaDeCarteraException.SiHay(ExigirAbierto());
        ArgumentException.ThrowIfNullOrWhiteSpace(huella);
        Status = AccrualProcessStatus.Draft;
        ApprovalRequestPublicId = null;
        RequestedByUserId = null;
        RequestedAt = null;
        Reason = null;
        ContentSha256 = huella;
        IssuesJson = string.IsNullOrWhiteSpace(issuesJson) ? "{}" : issuesJson;
        HasBlockingIssues = bloquea;
        LoanCount = creditos;
        IncludedFactCount = incluidos;
        ExcludedFactCount = excluidos;
        IncludedAmount = montoIncluido;
        ExcludedAmount = montoExcluido;
        CalculationCount += 1;
        CalculatedAt = ahora;
        CalculatedByUserId = usuario;
    }

    /// <summary><c>Draft</c> → <c>PendingApproval</c>: la persona pide ponerlo en firme con su motivo; la solicitud de aprobación queda sellada.</summary>
    public void PedirAprobacion(string motivo, Guid solicitud, int usuario, DateTime ahora)
    {
        ReglaDeCarteraException.SiHay(PuedeIrA(AccrualProcessStatus.PendingApproval));
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        Status = AccrualProcessStatus.PendingApproval;
        Reason = motivo.Trim();
        ApprovalRequestPublicId = solicitud;
        RequestedByUserId = usuario;
        RequestedAt = ahora;
    }

    /// <summary><c>PendingApproval</c> → <c>Draft</c>: la aprobación se rechazó o se retiró; el borrador queda como estaba para corregirlo.</summary>
    public void DevolverABorrador()
    {
        ReglaDeCarteraException.SiHay(PuedeIrA(AccrualProcessStatus.Draft));
        Status = AccrualProcessStatus.Draft;
        ApprovalRequestPublicId = null;
    }

    /// <summary><c>PendingApproval</c> → <c>Confirmed</c>: otra persona aprobó; queda el lote que se ordenó.</summary>
    public void PonerEnFirme(int aprobador, string nombre, ApprovalMethod metodo, Guid lote, long numeroDeLote, DateTime ahora)
    {
        ReglaDeCarteraException.SiHay(PuedeIrA(AccrualProcessStatus.Confirmed));
        Status = AccrualProcessStatus.Confirmed;
        ApprovedByUserId = aprobador;
        ApprovedByName = nombre;
        ApprovalMethod = metodo;
        BatchPublicId = lote;
        BatchNumber = numeroDeLote;
        ConfirmedAt = ahora;
    }

    /// <summary><c>Confirmed</c> → <c>Reprocessed</c>: su reproceso quedó en firme.</summary>
    public void MarcarReprocesado(int reproceso)
    {
        ReglaDeCarteraException.SiHay(PuedeIrA(AccrualProcessStatus.Reprocessed));
        Status = AccrualProcessStatus.Reprocessed;
        ReprocessedById = reproceso;
    }

    /// <summary>Anula un proceso abierto, con su motivo. Uno en firme no se anula.</summary>
    public void Anular(string motivo, int usuario, DateTime ahora)
    {
        ReglaDeCarteraException.SiHay(PuedeIrA(AccrualProcessStatus.Voided));
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        Status = AccrualProcessStatus.Voided;
        VoidReason = motivo.Trim();
        VoidedAt = ahora;
        VoidedByUserId = usuario;
        ApprovalRequestPublicId = null;
    }

    /// <summary>¿Comparte algún concepto con <paramref name="otros"/>?</summary>
    public bool CompartenConcepto(AccrualProcessConcepts otros) => (Concepts & otros) != AccrualProcessConcepts.None;

    /// <summary>¿Su rango se cruza con [<paramref name="desde"/>, <paramref name="hasta"/>]?</summary>
    public bool SeCruzaCon(DateOnly desde, DateOnly hasta) => DateFrom <= hasta && DateTo >= desde;
}

/// <summary>
/// <c>LND_AccrualProcessFacts</c> (feature 024, fase 3): un hecho del borrador —la unidad que se contabiliza—. En la clase <c>Posting</c> es un mensaje de
/// causación que espera (<see cref="MessagePublicId"/>: lo causado de un día, una sucursal y un concepto, con todos sus créditos), con su estado y, si
/// queda fuera, su motivo y si ese motivo impide ponerlo en firme. Un recálculo los reemplaza (baja lógica). Sin diferencias de auditoría: son muchos y el
/// evento es el del comando. (nuevo)
/// </summary>
[SinDiffDeAuditoria]
public class AccrualProcessFact : AuditableEntity
{
    public int AccrualProcessId { get; init; }

    /// <summary>El mensaje del hecho (<c>COR_IntegrationMessages.PublicId</c>; sin FK, como en el libro).</summary>
    public Guid? MessagePublicId { get; init; }

    /// <summary>El tipo del hecho (<c>InteresCausado</c>, <c>MoraCausada</c>…).</summary>
    public string FactType { get; init; } = string.Empty;

    /// <summary>El día causado (el del alcance <c>CausacionManual:{día}</c>).</summary>
    public DateOnly CausedDate { get; init; }

    /// <summary>La fecha contable del hecho (la de su sobre).</summary>
    public DateOnly AccountingDate { get; init; }

    /// <summary>La sucursal contable del hecho (<c>COR_Branches</c>).</summary>
    public int? BranchId { get; init; }

    public int LoanCount { get; init; }

    public decimal Amount { get; init; }

    public AccrualProcessItemStatus Status { get; init; }

    public AccrualExclusionReason? ExclusionReason { get; init; }

    /// <summary>Si el motivo impide poner el proceso en firme (la falta de regla con <c>Contabilidad.FaltaDeRegla = Bloquear</c>).</summary>
    public bool IsBlocking { get; init; }

    /// <summary>El detalle que lee la persona (qué regla falta, qué mes está cerrado…).</summary>
    public string? Detail { get; init; }
}

/// <summary>
/// <c>LND_AccrualProcessLines</c> (feature 024, fase 3): lo de un crédito y un concepto en el rango —sumado sobre los hechos del proceso—, con su estado y
/// motivo. En un reproceso, <see cref="PreviousAmount"/> es lo que el proceso reprocesado contabilizó de ese crédito y concepto, y los créditos con abonos
/// posteriores aparecen aunque no tengan nada pendiente (<see cref="FactType"/> nulo, motivo <c>LaterPayment</c>). La fase 4 escribe aquí lo que
/// reconoce de cada crédito. Un recálculo las reemplaza (baja lógica). (nuevo)
/// </summary>
[SinDiffDeAuditoria]
public class AccrualProcessLine : AuditableEntityLong
{
    public int AccrualProcessId { get; init; }

    public int LoanId { get; init; }

    /// <summary>El tipo del hecho; nulo en la línea informativa de un crédito con abonos posteriores sin nada pendiente.</summary>
    public string? FactType { get; init; }

    public LoanConcept? Concept { get; init; }

    public decimal Amount { get; init; }

    /// <summary>Reproceso: lo que el proceso reprocesado contabilizó de este crédito y concepto.</summary>
    public decimal? PreviousAmount { get; init; }

    public AccrualProcessItemStatus Status { get; init; }

    public AccrualExclusionReason? ExclusionReason { get; init; }

    /// <summary>La oficina y la línea del crédito al calcular (para filtrar y totalizar sin releer).</summary>
    public int BranchId { get; init; }

    public int CreditLineId { get; init; }
}
