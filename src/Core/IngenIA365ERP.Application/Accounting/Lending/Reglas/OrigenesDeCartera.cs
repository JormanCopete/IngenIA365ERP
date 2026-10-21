namespace IngenIA365ERP.Application.Accounting.Lending.Reglas;

/// <summary>
/// Los <c>SourceType</c> con que un comprobante de origen <c>CAR</c> nombra lo que lo originó (feature 013, T0640; contracts/contabilidad.md
/// §7.3; C114). Viajan en <c>AccountingOrigin</c> y los traducen a pantalla <c>EnlacesDeOrigen</c> (Application y Shared, T0651). Molde:
/// <c>OrigenesDeInventario</c>. (nuevo)
/// </summary>
public static class OrigenesDeCartera
{
    /// <summary>
    /// Un comprobante por unidad: <c>SourcePublicId</c> es el <c>origin.publicId</c> del hecho (la operación del crédito, la parte, la orden).
    /// Es <c>LendingIssuedDocument</c> y no <c>LendingDocument</c>, que es la entidad conservada de Ahorros (C114).
    /// </summary>
    public const string Documento = "LendingIssuedDocument";

    /// <summary>Un comprobante resumido: <c>SourcePublicId</c> es el lote (<c>COR_IntegrationBatches</c>); sus hechos están en <c>ACC_LendingPostings</c>.</summary>
    public const string LoteResumido = "LendingPostingBatch";

    /// <summary>Los dos, para quien necesita saber si un <c>SourceType</c> es de Cartera.</summary>
    public static IReadOnlyList<string> Todos { get; } = [Documento, LoteResumido];
}
