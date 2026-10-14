using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Migration;

/// <summary>
/// <c>LND_ProvisionalCollections</c> (feature 013, K1; data-model §29.5, C44, C62; FR-142): un cobro por fuera de ventas
/// confirmadas registrado en Contabilidad o Tesorería después del corte, durante el crédito provisional. Hecho
/// (<see cref="IHechoInmutable"/>) que guarda la activación con la plantilla <c>lending.provisional-collections</c>; el consumidor
/// de Comercial lo aplica al nacer la obligación de su venta, escribiendo una sola vez la aplicación (idempotente por la fila).
/// </summary>
public class ProvisionalCollection : AuditableEntity, IHechoInmutable
{
    public int ActivationId { get; init; }

    public int RowNumber { get; init; }

    public string InvoiceNumber { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public DateOnly ValueDate { get; init; }

    /// <summary>Referencia del registro contable que redujo la cuenta por cobrar provisional; única con la factura.</summary>
    public string AccountingReference { get; init; } = string.Empty;

    /// <summary>La obligación de la venta; la escribe el consumidor en el mismo <c>SaveChanges</c>.</summary>
    [EscrituraUnica]
    public int? LoanId { get; set; }

    /// <summary>La operación <c>ProvisionalCollection</c>; un reproceso que la encuentra escrita no repite.</summary>
    [EscrituraUnica]
    public long? OperationId { get; set; }

    [EscrituraUnica]
    public DateTime? AppliedAt { get; set; }

    /// <summary><c>CobroProvisionalIncorporado</c> (bandeja, sin FK).</summary>
    [EscrituraUnica]
    public Guid? MessagePublicId { get; set; }

    public bool Aplicado => AppliedAt is not null;

    /// <summary>La aplicación sobre la obligación de su venta (C62): una sola vez, con la operación y el mensaje que emitió.</summary>
    public void Aplicar(int credito, long operacion, Guid mensaje, DateTime ahora)
    {
        if (Aplicado) throw new InvalidOperationException("El cobro provisional ya se aplicó.");
        LoanId = credito;
        OperationId = operacion;
        MessagePublicId = mensaje;
        AppliedAt = ahora;
    }
}
