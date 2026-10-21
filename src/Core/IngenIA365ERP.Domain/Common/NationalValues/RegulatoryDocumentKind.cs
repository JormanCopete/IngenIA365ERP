namespace IngenIA365ERP.Domain.Common.NationalValues;

/// <summary>
/// Clase de un documento normativo tipado (feature 013, T37, T45, R2; data-model parte 3 §9.2, §12.4). Se guarda como
/// <b>texto</b> (el nombre) en <c>ADM_RegulatoryDocuments.Kind</c> y <c>COR_RegulatoryDocuments.Kind</c>: los nombres son
/// los códigos del documento y no se renombran. El número sólo ordena.
/// </summary>
public enum RegulatoryDocumentKind
{
    /// <summary>Modalidades, sub-segmentos y, por combinación, segmento SARC, de pérdida esperada y clave de usura.</summary>
    CatalogoDeModalidades = 1,

    /// <summary>Tipos de organización solidaria y categorías de fondos de empleados.</summary>
    TiposDeOrganizacion = 2,

    /// <summary>Anexo 1 del SARC (lo tipa y valida Cartera).</summary>
    SarcAnexo1 = 3,

    /// <summary>Anexo 2 del SARC, pérdida esperada (K6a; lo tipa y valida Cartera).</summary>
    SarcAnexo2 = 4,

    /// <summary>Regla que dice quién está obligado a la pérdida esperada (lo tipa y valida Cartera).</summary>
    ReglaDePerdidaEsperada = 5,
}
