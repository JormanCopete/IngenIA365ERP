using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Entities.Inventory.Documents;

/// <summary>
/// Un faltante o un sobrante de un traslado (<c>INV_TransferDiscrepancies</c>; feature 012, US10, T365; data-model §7.1; FR-039,
/// US10-2). Nace con la recepción: lo que no llegó queda en tránsito como <see cref="TransferDiscrepancyKind.Shortage"/>; lo que
/// llegó de más, fuera de la existencia, como <see cref="TransferDiscrepancyKind.Surplus"/>. Nunca desaparece sola: se resuelve
/// con un documento que pasa por aprobación de otra persona (<c>ResolveTransferDiscrepancyCommand</c>).
/// <para>
/// Ciclo: <b>pendiente</b> (sin <see cref="Resolution"/>) → <b>en aprobación</b> (<see cref="PedirResolucion"/>: salida pedida,
/// cantidad y documento que la resuelve) → <b>resuelta</b> cuando ese documento se confirma (<see cref="Resolver"/>), o de vuelta
/// a pendiente si la aprobación se rechaza (<see cref="VolverAPendiente"/>). Una aprobación parcial resuelve sólo su cantidad
/// (<see cref="ResolvedQuantityBase"/>, nuevo) y lo demás vuelve a pendiente. Las transiciones inválidas lanzan
/// <see cref="InvalidOperationException"/>: el comando las revisa antes con su código.
/// </para>
/// </summary>
public class TransferDiscrepancy : AuditableEntity
{
    public const int LargoDelMotivo = 500;

    /// <summary>Estados derivados, como texto (api.md §11: <c>state</c>).</summary>
    public const string EstadoPendiente = "Pending";
    public const string EstadoEnAprobacion = "InApproval";
    public const string EstadoResuelta = "Resolved";

    public int DispatchDocumentId { get; set; }

    /// <summary>La recepción con que nació.</summary>
    public int ReceiptDocumentId { get; set; }

    /// <summary>La línea del despacho que faltó o sobró.</summary>
    public int DispatchLineId { get; set; }

    public int ProductId { get; set; }

    /// <summary>Sin FK hasta I6 (data-model §3.0).</summary>
    public int? LotId { get; set; }

    public TransferDiscrepancyKind Kind { get; set; }

    /// <summary>La cantidad de la diferencia en unidad base; &gt; 0.</summary>
    public decimal QuantityBase { get; set; }

    /// <summary>Faltante: el de la línea de despacho. Sobrante: el vigente al resolver (nulo hasta entonces).</summary>
    public decimal? UnitCost { get; set; }

    /// <summary>Lo ya resuelto por aprobaciones parciales. (nuevo)</summary>
    public decimal ResolvedQuantityBase { get; private set; }

    public TransferDiscrepancyResolution? Resolution { get; private set; }

    /// <summary>La cantidad de la resolución pedida (la del documento que la resuelve). (nuevo)</summary>
    public decimal? ResolutionQuantityBase { get; private set; }

    public DateTime? ResolutionRequestedAt { get; private set; }

    public int? ResolutionRequestedByUserId { get; private set; }

    public string? ResolutionReason { get; private set; }

    /// <summary>Obligatoria en <see cref="TransferDiscrepancyResolution.WriteOffFromTransit"/> y <see cref="TransferDiscrepancyResolution.SurplusAdjustment"/>.</summary>
    public int? AdjustmentCauseId { get; private set; }

    /// <summary>El documento que la resuelve (en aprobación mientras la resolución está pedida).</summary>
    public int? ResolutionDocumentId { get; private set; }

    /// <summary>Nulo = pendiente o en aprobación.</summary>
    public DateTime? ResolvedAt { get; private set; }

    // ---------------------------------------------------------------------------------------------- reglas --

    /// <summary>Las salidas de cada tipo de diferencia (api.md §11).</summary>
    public static IReadOnlyList<TransferDiscrepancyResolution> ResolucionesAdmitidas(TransferDiscrepancyKind kind) => kind switch
    {
        TransferDiscrepancyKind.Shortage =>
            [TransferDiscrepancyResolution.ReturnToOrigin, TransferDiscrepancyResolution.WriteOffFromTransit, TransferDiscrepancyResolution.LateReceipt],
        TransferDiscrepancyKind.Surplus => [TransferDiscrepancyResolution.SurplusAdjustment],
        _ => [],
    };

    /// <summary>¿La salida exige la causa de ajuste? (la baja desde el tránsito y el sobrante).</summary>
    public static bool ExigeCausa(TransferDiscrepancyResolution resolucion) =>
        resolucion is TransferDiscrepancyResolution.WriteOffFromTransit or TransferDiscrepancyResolution.SurplusAdjustment;

    public bool Admite(TransferDiscrepancyResolution resolucion) => ResolucionesAdmitidas(Kind).Contains(resolucion);

    /// <summary>Lo que falta resolver.</summary>
    public decimal Pendiente() => QuantityBase - ResolvedQuantityBase;

    public bool EstaResuelta => ResolvedAt is not null;

    public bool EnAprobacion => !EstaResuelta && Resolution is not null;

    /// <summary><see cref="EstadoPendiente"/>, <see cref="EstadoEnAprobacion"/> o <see cref="EstadoResuelta"/>.</summary>
    public string Estado => EstaResuelta ? EstadoResuelta : EnAprobacion ? EstadoEnAprobacion : EstadoPendiente;

    // --------------------------------------------------------------------------------------- transiciones --

    /// <summary>Pide la resolución: queda en aprobación con su documento. Sólo desde pendiente, con una salida admitida.</summary>
    public void PedirResolucion(
        TransferDiscrepancyResolution resolucion, decimal cantidad, int pedidaPor, DateTime ahoraUtc, string motivo, int? causaId, int documentoId)
    {
        if (Estado != EstadoPendiente)
            throw new InvalidOperationException($"La diferencia no está pendiente ({Estado}).");
        if (!Admite(resolucion))
            throw new InvalidOperationException($"Un {Kind} no se resuelve con {resolucion}.");
        if (cantidad <= 0m || cantidad > Pendiente())
            throw new InvalidOperationException($"La cantidad {cantidad} no está entre 0 y lo pendiente ({Pendiente()}).");
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("Resolver una diferencia exige el motivo.", nameof(motivo));
        if (ExigeCausa(resolucion) && causaId is null)
            throw new InvalidOperationException($"{resolucion} exige la causa de ajuste.");

        Resolution = resolucion;
        ResolutionQuantityBase = cantidad;
        ResolutionRequestedByUserId = pedidaPor;
        ResolutionRequestedAt = ahoraUtc;
        ResolutionReason = motivo.Trim();
        AdjustmentCauseId = causaId;
        ResolutionDocumentId = documentoId;
    }

    /// <summary>La aprobación se rechazó o se retiró: vuelve a pendiente.</summary>
    public void VolverAPendiente()
    {
        if (!EnAprobacion) throw new InvalidOperationException("La diferencia no tiene una resolución en aprobación.");
        Limpiar();
    }

    /// <summary>
    /// El documento que la resuelve se confirmó: resuelve <paramref name="cantidad"/>. Si cubre lo pendiente queda resuelta; si no,
    /// lo demás vuelve a pendiente.
    /// </summary>
    public void Resolver(decimal cantidad, DateTime ahoraUtc)
    {
        if (!EnAprobacion) throw new InvalidOperationException("La diferencia no tiene una resolución en aprobación.");
        if (cantidad <= 0m || cantidad > Pendiente())
            throw new InvalidOperationException($"La cantidad {cantidad} no está entre 0 y lo pendiente ({Pendiente()}).");

        ResolvedQuantityBase += cantidad;
        if (Pendiente() == 0m) ResolvedAt = ahoraUtc;
        else Limpiar();
    }

    private void Limpiar()
    {
        Resolution = null;
        ResolutionQuantityBase = null;
        ResolutionRequestedAt = null;
        ResolutionRequestedByUserId = null;
        ResolutionReason = null;
        AdjustmentCauseId = null;
        ResolutionDocumentId = null;
    }
}
