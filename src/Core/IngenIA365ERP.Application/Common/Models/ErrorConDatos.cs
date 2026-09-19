namespace IngenIA365ERP.Application.Common.Models;

/// <summary>
/// Error con datos estructurados para la pantalla (feature 009, FR-041): además del código y el
/// mensaje lleva un objeto <see cref="Data"/> que la envolvente HTTP serializa como <c>data</c>
/// —número de línea, cuenta, campo y regla de una infracción, o la lista completa de infracciones
/// de un comprobante—. Es un <see cref="Error"/> a todos los efectos: quien no sepa de datos lo
/// trata como siempre (código, mensaje, estado HTTP por prefijo).
/// </summary>
public sealed record ErrorConDatos(string Code, string Message, object Data) : Error(Code, Message);
