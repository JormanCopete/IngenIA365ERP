namespace IngenIA365ERP.Domain.Enums.Core;

// Valores nacionales en la base de cada cooperativa (feature 013, T39, T41, R4, R7; data-model parte 3 §4.3, §4.4, §9.2).
// Se guarda como int y un valor nunca se renumera.

/// <summary>Sobre qué copia recae una revisión: un valor escalar (<c>COR_NationalValues</c>) o un documento normativo (<c>COR_RegulatoryDocuments</c>).</summary>
public enum NationalValueReviewTarget
{
    Value = 1,
    Document = 2,
}
