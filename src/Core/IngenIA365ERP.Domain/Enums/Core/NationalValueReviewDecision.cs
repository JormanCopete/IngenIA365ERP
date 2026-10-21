namespace IngenIA365ERP.Domain.Enums.Core;

// Valores nacionales en la base de cada cooperativa (feature 013, T39, T41, R4, R7; data-model parte 3 §4.3, §4.4, §9.2).
// Se guarda como int y un valor nunca se renumera.

/// <summary>Conformidad del contador o del oficial con una copia. Sin revisión, la copia está «pendiente de validar» (derivado, T41).</summary>
public enum NationalValueReviewDecision
{
    Confirmed = 1,
    Objected = 2,
}
