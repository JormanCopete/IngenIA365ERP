using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Receipts.Configuration;

/// <summary>
/// <c>RCD_TransferAccounts</c> (feature 015, segunda ronda del 2026-10-27, D-f; FR-231): una cuenta bancaria propia habilitada para recibir un medio de transferencia o
/// consignación en un punto (y, si se da, sólo en una caja de ese punto). El cajero elige entre las habilitadas para el medio en su caja; con una sola, se propone. La
/// cuenta propia es de Tesorería: aquí sólo va su <c>PublicId</c>. Se da de baja lógica, no se edita. (nuevo)
/// </summary>
public class TransferAccount : AuditableEntity
{
    public int CollectionPointId { get; set; }

    /// <summary>Nula: todas las cajas del punto.</summary>
    public int? CashBoxId { get; set; }

    public int PaymentMeansId { get; set; }

    public Guid OwnBankAccountPublicId { get; set; }
}
