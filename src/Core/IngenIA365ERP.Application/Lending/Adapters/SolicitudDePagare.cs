namespace IngenIA365ERP.Application.Lending.Adapters;

/// <summary>
/// Lo que se le pide al depósito para crear un pagaré desmaterializado (feature 013, US3, T0436; contracts/adaptadores-y-archivos.md §3.2, §3.8;
/// FR-044): el pagaré de Cartera (su <c>PublicId</c> es la clave de idempotencia ante el depósito: crear dos veces el mismo devuelve el que ya
/// existe, regla 3), el número que lo identifica —el del crédito, o el de la solicitud mientras el crédito no existe: el documento cruce ante
/// Contabilidad sigue siendo el número del crédito—, el valor, la fecha y los firmantes (deudor y codeudores, copia I-1). (nuevo)
/// </summary>
public sealed record SolicitudDePagare(
    Guid PromissoryNotePublicId,
    string NumeroDelCredito,
    decimal Valor,
    DateOnly FechaDeCreacion,
    IReadOnlyList<FirmanteDePagare> Firmantes);
