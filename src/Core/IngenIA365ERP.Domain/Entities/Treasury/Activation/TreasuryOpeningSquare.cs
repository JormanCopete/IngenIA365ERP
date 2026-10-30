using IngenIA365ERP.Domain.Common.Cuadres;

namespace IngenIA365ERP.Domain.Entities.Treasury.Activation;

/// <summary><c>TES_OpeningSquares</c> (feature 015, US13, T0286; data-model §15.2): el cuadre de apertura de Tesorería, definitivo o provisional. (nuevo)</summary>
public class TreasuryOpeningSquare : CuadreDeAperturaBase;

/// <summary><c>TES_OpeningSquareLines</c> (H): saldo por cuenta bancaria, cheques girados en tránsito (informativa) y efectivo en tránsito por oficina. (nuevo)</summary>
public class TreasuryOpeningSquareLine : LineaDeCuadreDeAperturaBase;

/// <summary><c>TES_ReferenceFigures</c> (feature 015, US13, T0286; data-model §15.3; FR-160): las cifras de referencia de Tesorería. (nuevo)</summary>
public class TreasuryReferenceFigure : CifraDeReferenciaBase;
