using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Integration;

namespace IngenIA365ERP.Domain.Entities.Integration;

/// <summary>
/// Un lote de integración (<c>COR_IntegrationBatches</c>; feature 012, T12, T477; data-model §19; contracts/contabilidad.md
/// §5.4). Es de plataforma —Inventario lo muestra sin leer <c>ACC_</c>— y es a la vez <b>orden</b> (lote manual,
/// reproceso, envío posterior de «no aplica») y <b>registro</b> (qué corrió, cuándo, con qué resultado).
///
/// <para>
/// Ciclo: nace <c>Requested</c>; <see cref="Iniciar"/> lo pasa a <c>Running</c> una sola vez (<c>StartIntegrationBatchCommand</c>
/// respeta <c>RowVersion</c>, así que dos réplicas no lo arrancan dos veces); se cierra con <see cref="Completar"/>,
/// <see cref="CompletarConRechazos"/> o <see cref="MarcarVacio"/> (<c>Empty</c> prueba que corrió sin mensajes). Un lote
/// cerrado no se vuelve a cerrar. Los contadores y totales se leen de las entregas del lote al cerrarlo
/// (<see cref="TotalesDeLote"/>).
/// </para>
///
/// <para>
/// <see cref="CutoffMessageId"/> es interno (el <c>Id</c> bigint del último mensaje de la vista previa): por HTTP llega como
/// <c>cutoffMessagePublicId</c> y nunca sale en JSON (Principio VI). <see cref="ScheduledFor"/> es hora <b>local</b> de
/// Colombia, la de la franja de <c>Contabilidad.HoraDeLote</c>.
/// </para>
/// </summary>
public class IntegrationBatch : AuditableEntity
{
    /// <summary>De <c>COR_IntegrationBatchCounters</c>; único.</summary>
    public long Number { get; set; }

    /// <summary><c>IntegrationDestinations.Accounting</c> o <c>.Lending</c>.</summary>
    public string Destination { get; set; } = string.Empty;

    public BatchTrigger Trigger { get; set; }

    public string? ScheduleKey { get; set; }

    /// <summary>Hora local de la franja (<c>Scheduled</c>).</summary>
    public DateTime? ScheduledFor { get; set; }

    /// <summary>La sesión de caja cerrada (<c>CashSessionClose</c>).</summary>
    public Guid? CashSessionPublicId { get; set; }

    public short? PeriodYear { get; set; }

    public byte? PeriodMonth { get; set; }

    /// <summary><c>Manual</c> y <c>SendNotApplicable</c>: se procesa exactamente hasta aquí. Interno.</summary>
    public long? CutoffMessageId { get; set; }

    public DateOnly? DateFrom { get; set; }

    public DateOnly? DateTo { get; set; }

    /// <summary>Sellada al crear; nula en un lote manual que mezcla tipos (cada grupo usa la de su tipo).</summary>
    public PostingGranularity? Granularity { get; set; }

    public BatchStatus Status { get; private set; } = BatchStatus.Requested;

    public ActorKind RequestedByKind { get; set; }

    public int? RequestedByUserId { get; set; }

    public Guid? RequestedByCentralUserId { get; set; }

    public string? RequestedByName { get; set; }

    public string? RequestedByEmail { get; set; }

    public string? RequestedByIp { get; set; }

    /// <summary>Obligatorio en <c>Manual</c>, <c>Reprocess</c> y <c>SendNotApplicable</c>.</summary>
    public string? Reason { get; set; }

    public DateTime RequestedAt { get; set; }

    public DateTime? StartedAt { get; private set; }

    public DateTime? FinishedAt { get; private set; }

    public int MessageCount { get; set; }

    public int DocumentCount { get; set; }

    public int ProcessedCount { get; private set; }

    public int RejectedCount { get; private set; }

    public int VoucherCount { get; private set; }

    public decimal TotalDebit { get; private set; }

    public decimal TotalCredit { get; private set; }

    /// <summary>Grupos, comprobantes y rechazos con su motivo.</summary>
    public string? ResultSummaryJson { get; private set; }

    public bool EstaCerrado => Status is BatchStatus.Completed or BatchStatus.CompletedWithRejections or BatchStatus.Empty;

    /// <summary><c>Requested</c> → <c>Running</c>. Sólo una vez.</summary>
    public void Iniciar(DateTime ahora)
    {
        if (Status != BatchStatus.Requested)
            throw new InvalidOperationException($"El lote {Number} está {Status}: sólo un lote pedido se inicia.");
        Status = BatchStatus.Running;
        StartedAt = ahora;
    }

    /// <summary><c>Running</c> → <c>Completed</c>: corrió con mensajes y ninguno quedó rechazado.</summary>
    public void Completar(TotalesDeLote totales, DateTime ahora)
    {
        ExigirQueCorre();
        if (totales.RejectedCount != 0)
            throw new InvalidOperationException($"El lote {Number} tiene {totales.RejectedCount} rechazo(s): se cierra con CompletarConRechazos.");
        if (totales.MessageCount == 0)
            throw new InvalidOperationException($"El lote {Number} no tiene mensajes: se cierra con MarcarVacio.");
        Cerrar(BatchStatus.Completed, totales, ahora);
    }

    /// <summary><c>Running</c> → <c>CompletedWithRejections</c>: al menos una entrega quedó rechazada.</summary>
    public void CompletarConRechazos(TotalesDeLote totales, DateTime ahora)
    {
        ExigirQueCorre();
        if (totales.RejectedCount <= 0)
            throw new InvalidOperationException($"El lote {Number} no tiene rechazos: se cierra con Completar.");
        Cerrar(BatchStatus.CompletedWithRejections, totales, ahora);
    }

    /// <summary><c>Running</c> → <c>Empty</c>: corrió y no tenía mensajes.</summary>
    public void MarcarVacio(DateTime ahora)
    {
        ExigirQueCorre();
        Cerrar(BatchStatus.Empty, new TotalesDeLote(0, 0, 0, 0, 0, 0m, 0m, null), ahora);
    }

    private void ExigirQueCorre()
    {
        if (Status != BatchStatus.Running)
            throw new InvalidOperationException($"El lote {Number} está {Status}: sólo un lote en curso se cierra.");
    }

    private void Cerrar(BatchStatus estado, TotalesDeLote totales, DateTime ahora)
    {
        Status = estado;
        FinishedAt = ahora;
        MessageCount = totales.MessageCount;
        DocumentCount = totales.DocumentCount;
        ProcessedCount = totales.ProcessedCount;
        RejectedCount = totales.RejectedCount;
        VoucherCount = totales.VoucherCount;
        TotalDebit = totales.TotalDebit;
        TotalCredit = totales.TotalCredit;
        ResultSummaryJson = totales.ResultSummaryJson;
    }
}
