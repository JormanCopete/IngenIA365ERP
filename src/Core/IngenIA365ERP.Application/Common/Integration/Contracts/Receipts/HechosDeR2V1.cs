using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;

// Feature 015, R2 (fase 38; FR-093, FR-218; contracts/contabilidad.md §3, §5): el hecho de la conciliación de un convenio de recaudo, con la forma común (líneas por rol,
// sin cuentas) y el tipo de comprobante sugerido RB. (nuevo)

/// <summary>Los roles de Recaudo que llegan con R2 (contabilidad.md §5): por cuenta propia; el tercero es el aliado del convenio. (nuevo)</summary>
public static class RolesDeRecaudoR2
{
    public const string ComisionesDeConvenio = "ComisionesDeConvenio";
    public const string IvaDescontable = "IvaDescontable";
    public const string RetencionesAFavor = "RetencionesAFavor";
}

/// <summary>Una retención del hecho, con su clase y su tercero (FR-218). (nuevo)</summary>
public sealed record RetencionDelHechoV1
{
    /// <summary><c>WithholdingTax</c> (en la fuente), <c>VatWithholding</c> (de IVA), <c>IcaWithholding</c> (de ICA) o la clase que traiga el aliado.</summary>
    public string Kind { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public Guid? ThirdPartyPersonPublicId { get; init; }
}

/// <summary>
/// <c>ComisionDeConvenioRegistrada</c> v1 (<c>CCR</c>; FR-093, FR-218): la conciliación del archivo de un convenio o de una pasarela contra el abono en la cuenta
/// recaudadora explica la diferencia: <c>ComisionesDeConvenio</c>, <c>IvaDescontable</c> y <c>RetencionesAFavor</c> (cada una con su tercero) → <c>BancoRecaudo</c>. Lo
/// que quedó sin explicar y se aprobó va también a <c>ComisionesDeConvenio</c> (decisión pendiente del dueño). En línea. (nuevo)
/// </summary>
public sealed record ComisionDeConvenioRegistradaV1
{
    public const string Type = "ComisionDeConvenioRegistrada";

    public Guid ReconciliationPublicId { get; init; }

    public string Number { get; init; } = string.Empty;

    public string AgreementCode { get; init; } = string.Empty;

    public Guid? FilePublicId { get; init; }

    public string? FileNumber { get; init; }

    public Guid StatementLinePublicId { get; init; }

    public Guid OwnBankAccountPublicId { get; init; }

    public DateOnly CreditedOn { get; init; }

    public decimal InformedAmount { get; init; }

    public decimal CreditedAmount { get; init; }

    public decimal CommissionAmount { get; init; }

    public decimal VatAmount { get; init; }

    public IReadOnlyList<RetencionDelHechoV1> Withholdings { get; init; } = [];

    public decimal UnexplainedAmount { get; init; }

    public Guid? PartnerPersonPublicId { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();

    public UserRefV1? ApprovedBy { get; init; }

    public IReadOnlyList<FactLineV1> Lines { get; init; } = [];
}
