using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.BulkCharges;

/// <summary>
/// <c>LND_BulkChargeRuns</c> (2026-11-25, pedido del dueño; feature 026): la corrida de cargos masivos. Guarda los datos del cargo —línea, período, fecha
/// del cargo, valor, cuotas, periodicidad, forma y ciclo de pago, primer pago, tercero de la contrapartida—, de dónde salen los asociados (filtros o
/// plano), la huella de la última vista previa, los totales, la corrida de fondo que la ejecuta y el lote contable. Sus líneas
/// (<see cref="BulkChargeLine"/>) son una por asociado. Ver <see cref="BulkChargeRunStatus"/> para el ciclo de vida. (nuevo)
/// </summary>
public class BulkChargeRun : AuditableEntity
{
    public const string CodigoNoBorrador = "Lending.BulkCharge.NotDraft";
    public const string CodigoEstadoInvalido = "Lending.BulkCharge.InvalidStatus";

    /// <summary>Numerador de Cartera (<c>CGM-000001</c>); único.</summary>
    public string Number { get; init; } = string.Empty;

    public BulkChargeRunStatus Status { get; private set; } = BulkChargeRunStatus.Draft;

    public int CreditLineId { get; set; }

    /// <summary>La versión de la línea vigente a la fecha del cargo en la última vista previa (se vuelve a leer al confirmar).</summary>
    public int CreditLineVersionId { get; set; }

    /// <summary>El período del cargo, <c>aaaamm</c>: un asociado recibe a lo sumo un cargo de la línea por período.</summary>
    public int Period { get; set; }

    /// <summary>La fecha del cargo: la fecha valor de las obligaciones y la del comprobante.</summary>
    public DateOnly ChargeDate { get; set; }

    /// <summary>El valor por defecto de cada obligación (el plano lo puede cambiar por fila).</summary>
    public decimal Amount { get; set; }

    public int Installments { get; set; } = 1;

    public RepaymentPeriodicity Periodicity { get; set; } = RepaymentPeriodicity.Monthly;

    public BulkChargeChannelMode ChannelMode { get; set; } = BulkChargeChannelMode.Fixed;

    /// <summary>La forma de pago con <see cref="BulkChargeChannelMode.Fixed"/>.</summary>
    public RepaymentChannel? RepaymentChannel { get; set; }

    /// <summary>Opcional: el ciclo del mes en que debe caer el primer pago (quincena, década o semana).</summary>
    public AccrualDiscountCycle? PaymentCycle { get; set; }

    public DateOnly FirstDueDate { get; set; }

    public BulkChargeCounterpart CounterpartMode { get; set; } = BulkChargeCounterpart.Debtor;

    /// <summary>La persona de la contrapartida con <see cref="BulkChargeCounterpart.Person"/> (<c>COR_People</c>).</summary>
    public int? CounterpartPersonId { get; set; }

    /// <summary>Si la línea admite más de uno; nulo = el de la versión.</summary>
    public AmortizationSystem? AmortizationSystem { get; set; }

    /// <summary>Los seguros elegidos (JSON con códigos); vacío = los obligatorios de la versión.</summary>
    public string Insurances { get; set; } = "[]";

    public BulkChargeSource Source { get; set; } = BulkChargeSource.Filters;

    /// <summary>Los filtros sobre los asociados activos (JSON: agencias, formas y períodos de descuento de la ficha, empresas empleadoras).</summary>
    public string FiltersJson { get; set; } = "{}";

    /// <summary>La huella de la última vista previa: confirmar exige que no haya cambiado.</summary>
    public string? PreviewSha256 { get; private set; }

    public DateTime? PreviewedAt { get; private set; }

    public int TotalLines { get; private set; }

    public int PendingCount { get; private set; }

    public int CreatedCount { get; private set; }

    public int SkippedCount { get; private set; }

    public int FailedCount { get; private set; }

    public int VoidedCount { get; private set; }

    /// <summary>La suma de lo creado y vivo (o, en borrador, de lo incluido).</summary>
    public decimal TotalAmount { get; private set; }

    /// <summary>La corrida de fondo que la ejecuta (<c>LND_ProcessRuns</c>).</summary>
    public int? ProcessRunId { get; private set; }

    /// <summary>La corrida de fondo de su anulación entera.</summary>
    public int? VoidProcessRunId { get; private set; }

    /// <summary>Quien armó el borrador (<c>SEC_Users</c>).</summary>
    public int PreparedByUserId { get; init; }

    public int? ConfirmedByUserId { get; private set; }

    public DateTime? ConfirmedAt { get; private set; }

    public DateTime? CompletedAt { get; private set; }

    /// <summary>El lote de contabilización de la corrida (<c>COR_IntegrationBatches</c>); nulo si nada pasa a Contabilidad.</summary>
    public Guid? BatchPublicId { get; private set; }

    public long? BatchNumber { get; private set; }

    public string? VoidReason { get; private set; }

    public int? VoidedByUserId { get; private set; }

    public DateTime? VoidedAt { get; private set; }

    // ------------------------------------------------------------------------------------------------ comportamiento --

    public bool EsBorrador => Status == BulkChargeRunStatus.Draft;

    /// <summary>Nulo si es un borrador; si no, <see cref="CodigoNoBorrador"/>.</summary>
    public ProblemaDeCartera? ExigirBorrador() => EsBorrador
        ? null
        : ProblemaDeCartera.Con(CodigoNoBorrador, "La corrida ya no es un borrador: no se cambia, no se recalcula ni se descarta.", "status", Status.ToString());

    private ProblemaDeCartera? Exigir(params BulkChargeRunStatus[] desde) => desde.Contains(Status)
        ? null
        : new ProblemaDeCartera(CodigoEstadoInvalido, $"La corrida está en «{Status}» y no admite esta operación.",
            new Dictionary<string, object?> { ["status"] = Status.ToString() });

    /// <summary>Sella el resultado de la vista previa del borrador.</summary>
    public void SellarVistaPrevia(string huella, int lineas, int incluidas, int omitidas, decimal total, DateTime ahora)
    {
        ReglaDeCarteraException.SiHay(ExigirBorrador());
        ArgumentException.ThrowIfNullOrWhiteSpace(huella);
        PreviewSha256 = huella;
        PreviewedAt = ahora;
        TotalLines = lineas;
        PendingCount = incluidas;
        SkippedCount = omitidas;
        CreatedCount = 0;
        FailedCount = 0;
        VoidedCount = 0;
        TotalAmount = total;
    }

    /// <summary><c>Draft</c> → <c>Queued</c>: confirmada; la corrida de fondo queda pedida.</summary>
    public void Confirmar(int usuario, int corridaDeFondo, DateTime ahora)
    {
        ReglaDeCarteraException.SiHay(ExigirBorrador());
        Status = BulkChargeRunStatus.Queued;
        ConfirmedByUserId = usuario;
        ConfirmedAt = ahora;
        ProcessRunId = corridaDeFondo;
    }

    /// <summary><c>Queued</c> → <c>Running</c> (idempotente si ya corre: una pasada que se retoma).</summary>
    public void Iniciar()
    {
        if (Status == BulkChargeRunStatus.Running) return;
        ReglaDeCarteraException.SiHay(Exigir(BulkChargeRunStatus.Queued));
        Status = BulkChargeRunStatus.Running;
    }

    /// <summary>Recuenta desde las líneas (lo que la ejecución y la anulación van dejando).</summary>
    public void Recontar(int pendientes, int creadas, int omitidas, int fallidas, int anuladas, decimal totalVivo)
    {
        PendingCount = pendientes;
        CreatedCount = creadas;
        SkippedCount = omitidas;
        FailedCount = fallidas;
        VoidedCount = anuladas;
        TotalAmount = totalVivo;
    }

    /// <summary><c>Running</c> → <c>Completed</c>, con el lote contable (nulo si nada pasa).</summary>
    public void Terminar(Guid? lote, long? numeroDeLote, DateTime ahora)
    {
        ReglaDeCarteraException.SiHay(Exigir(BulkChargeRunStatus.Running, BulkChargeRunStatus.Queued));
        Status = BulkChargeRunStatus.Completed;
        CompletedAt = ahora;
        BatchPublicId = lote;
        BatchNumber = numeroDeLote;
    }

    /// <summary><c>Draft</c> → <c>Discarded</c>.</summary>
    public void Descartar()
    {
        ReglaDeCarteraException.SiHay(ExigirBorrador());
        Status = BulkChargeRunStatus.Discarded;
    }

    /// <summary><c>Completed</c> → <c>Voiding</c>: la anulación de lo vivo queda pedida en segundo plano.</summary>
    public void IniciarAnulacion(string motivo, int usuario, int corridaDeFondo, DateTime ahora)
    {
        ReglaDeCarteraException.SiHay(Exigir(BulkChargeRunStatus.Completed));
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        Status = BulkChargeRunStatus.Voiding;
        VoidReason = motivo.Trim();
        VoidedByUserId = usuario;
        VoidedAt = ahora;
        VoidProcessRunId = corridaDeFondo;
    }

    /// <summary><c>Voiding</c> → <c>Voided</c> si no quedó nada vivo; si algo no se pudo anular, vuelve a <c>Completed</c>.</summary>
    public void TerminarAnulacion(bool quedaAlgoVivo)
    {
        ReglaDeCarteraException.SiHay(Exigir(BulkChargeRunStatus.Voiding));
        Status = quedaAlgoVivo ? BulkChargeRunStatus.Completed : BulkChargeRunStatus.Voided;
    }
}
