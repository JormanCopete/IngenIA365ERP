namespace IngenIA365ERP.Domain.Taxes;

/// <summary>
/// Las marcas tributarias de una parte de la operación (feature 012, T24, T162; research R21): de una persona, las de
/// <c>COR_People</c> (las seis nuevas más <c>IsLargeContributor</c>, <c>WithholdingExempt</c>, <c>IcaWithholdingExempt</c>
/// y <c>CiiuCode</c>); de la cooperativa, los parámetros <c>TAX</c> con vigencia. Las condiciones de una tarifa
/// (<see cref="CondicionesDeTarifa"/>) se evalúan contra el perfil de quien soporta el impuesto o la retención (el
/// sujeto: el vendedor) y el de quien retiene (el agente: el comprador).
/// </summary>
public sealed record PerfilTributario
{
    /// <summary><c>01</c> natural, <c>02</c> jurídica (el código de <c>COR_People.PersonType</c>).</summary>
    public string? PersonType { get; init; }

    public bool IsVatResponsible { get; init; }
    public bool IsIncomeTaxFiler { get; init; }
    public bool IsLargeContributor { get; init; }
    public bool IsSelfWithholder { get; init; }
    public bool IsSimpleTaxRegime { get; init; }
    public bool IsVatWithholdingAgent { get; init; }

    /// <summary>Practica retenciones en la fuente y de ICA cuando compra (la cooperativa, una persona jurídica). (nuevo)</summary>
    public bool EsAgenteDeRetencion { get; init; }

    /// <summary>No se le practican retenciones en la fuente ni de IVA (<c>COR_People.WithholdingExempt</c>).</summary>
    public bool WithholdingExempt { get; init; }

    /// <summary>No se le practica retención de ICA (<c>COR_People.IcaWithholdingExempt</c>).</summary>
    public bool IcaWithholdingExempt { get; init; }

    /// <summary>Actividad económica CIIU: decide la tarifa de ReteICA del municipio (con caída a la fila <c>*</c>).</summary>
    public string? CiiuCode { get; init; }

    /// <summary>Retiene IVA: agente de retención de IVA o gran contribuyente.</summary>
    public bool RetieneIva => IsVatWithholdingAgent || IsLargeContributor;
}
