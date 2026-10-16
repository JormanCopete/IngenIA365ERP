using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Risk;

namespace IngenIA365ERP.Domain.Entities.Lending.Operations;

/// <summary>
/// <c>LND_WriteOffBatchItems</c> (feature 013, K3, T0872; data-model §25.6; FR-095, C98): un crédito del lote de castigo con la verificación de
/// sus requisitos (<c>RequisitosDeCastigo</c>) —cada uno con resultado y evidencia en <see cref="RequirementsJson"/>—, los montos al corte y
/// el deterioro del último cierre. Nace <c>Included</c> si cumple o <c>Rejected</c> con lo que falta; al aplicar el lote pasa a
/// <c>WrittenOff</c> con su operación <c>WriteOff</c>. Único por lote y crédito. No es hecho. (nuevo)
/// </summary>
public class WriteOffBatchItem : AuditableEntity
{
    public int WriteOffBatchId { get; init; }

    public int LoanId { get; init; }

    public WriteOffItemStatus Status { get; private set; }

    /// <summary>Cada requisito con resultado y evidencia (de la norma y de la política).</summary>
    public string RequirementsJson { get; private set; } = "{}";

    /// <summary>Qué falta (<c>Lending.WriteOff.RequirementMissing</c>).</summary>
    public string? RejectionReason { get; private set; }

    public Guid? ReportAttachmentPublicId { get; init; }

    public Guid? IrrecoverabilityAttachmentPublicId { get; init; }

    public decimal PrincipalAmount { get; init; }

    public decimal InterestAmount { get; init; }

    public decimal OtherAmount { get; init; }

    public decimal ImpairmentAtCutoff { get; init; }

    /// <summary>Operación <c>WriteOff</c>; emite <c>CreditoCastigado</c> (<c>PV</c>).</summary>
    public long? WriteOffOperationId { get; private set; }

    /// <summary>Navegación para enlazar en el mismo guardado la operación todavía sin Id.</summary>
    public LoanOperation? WriteOffOperation { get; private set; }

    /// <summary>
    /// El ítem a partir de la verificación: <c>Included</c> si cumple; si no, <c>Rejected</c> con lo que falta. Los montos son los del crédito al
    /// corte y el deterioro, el individual del último cierre.
    /// </summary>
    public static WriteOffBatchItem Desde(int loteId, VerificacionDeCastigo verificacion, decimal capital, decimal intereses, decimal otros,
        decimal deterioro, Guid? informe, Guid? concepto)
    {
        ArgumentNullException.ThrowIfNull(verificacion);
        return new WriteOffBatchItem
        {
            WriteOffBatchId = loteId,
            LoanId = verificacion.LoanId,
            Status = verificacion.Cumple ? WriteOffItemStatus.Included : WriteOffItemStatus.Rejected,
            RequirementsJson = verificacion.RequisitosJson,
            RejectionReason = verificacion.Cumple ? null : Recortar(verificacion.Problema!.Mensaje),
            ReportAttachmentPublicId = informe,
            IrrecoverabilityAttachmentPublicId = concepto,
            PrincipalAmount = capital,
            InterestAmount = intereses,
            OtherAmount = otros,
            ImpairmentAtCutoff = deterioro,
        };
    }

    /// <summary><c>Included</c> → <c>WrittenOff</c> con la operación <c>WriteOff</c> (al aplicar el lote).</summary>
    public void Castigar(LoanOperation operacion)
    {
        ArgumentNullException.ThrowIfNull(operacion);
        if (Status != WriteOffItemStatus.Included)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(RequisitosDeCastigo.CodigoRequisitoFaltante,
                "Sólo se castiga un crédito incluido en el lote.", "status", Status.ToString()));
        if (operacion.Kind != LoanOperationKind.WriteOff)
            throw new ArgumentException("Un crédito se castiga con una operación WriteOff.", nameof(operacion));
        Status = WriteOffItemStatus.WrittenOff;
        WriteOffOperation = operacion;
        if (operacion.Id > 0) WriteOffOperationId = operacion.Id;
    }

    /// <summary>
    /// <c>Included</c> → <c>Rejected</c> al aplicar el lote (bloque D, decisión del dueño del 2026-10-15): el crédito ya no cumple los requisitos a la
    /// fecha de aplicación (o ya no está vigente) y se excluye con su motivo; el resto del lote se castiga. Con la verificación nueva, sus requisitos
    /// quedan como se verificaron al aplicar.
    /// </summary>
    public void Excluir(string motivo, string? requisitosJson = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        if (Status != WriteOffItemStatus.Included)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(RequisitosDeCastigo.CodigoRequisitoFaltante,
                "Sólo se excluye un crédito incluido en el lote.", "status", Status.ToString()));
        Status = WriteOffItemStatus.Rejected;
        RejectionReason = Recortar(motivo.Trim());
        if (!string.IsNullOrWhiteSpace(requisitosJson)) RequirementsJson = requisitosJson;
    }

    private static string Recortar(string texto) => texto.Length <= 500 ? texto : texto[..500];
}
