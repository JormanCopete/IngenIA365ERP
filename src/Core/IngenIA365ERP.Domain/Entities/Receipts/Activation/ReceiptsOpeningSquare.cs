using IngenIA365ERP.Domain.Common.Cuadres;

namespace IngenIA365ERP.Domain.Entities.Receipts.Activation;

/// <summary><c>RCD_OpeningSquares</c> (feature 015, US13, T0286; data-model §15.2): el cuadre de apertura de Recaudo, definitivo o provisional. (nuevo)</summary>
public class ReceiptsOpeningSquare : CuadreDeAperturaBase;

/// <summary><c>RCD_OpeningSquareLines</c> (H): efectivo por bóveda, partidas por identificar, cheques recibidos en canje y saldos a favor, por oficina. (nuevo)</summary>
public class ReceiptsOpeningSquareLine : LineaDeCuadreDeAperturaBase;

/// <summary><c>RCD_ReferenceFigures</c> (feature 015, US13, T0286; data-model §15.3; FR-160, FR-226): las cifras de referencia de Recaudo. (nuevo)</summary>
public class ReceiptsReferenceFigure : CifraDeReferenciaBase;
