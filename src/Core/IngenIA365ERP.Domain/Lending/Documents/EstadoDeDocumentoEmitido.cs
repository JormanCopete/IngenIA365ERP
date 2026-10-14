using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Documents;

/// <summary>
/// El estado de un documento emitido a una fecha (feature 013, US13, T0777; adaptadores-y-archivos.md §8.3; data-model §27.1). No es columna: se
/// calcula siempre con sus eventos y su firmeza, y el paso de «sujeto a firmeza» a «vigente» no lo marca ninguna tarea, lo da la fecha.
/// <list type="number">
/// <item><see cref="IssuedDocumentStatus.Voided"/> si tiene un evento de anulación (manual o por reverso);</item>
/// <item><see cref="IssuedDocumentStatus.SubjectToFinality"/> mientras <paramref name="hoy"/> sea anterior a <c>FinalityUntil</c> (D23);</item>
/// <item><see cref="IssuedDocumentStatus.Valid"/> en otro caso.</item>
/// </list>
/// Motor puro. (nuevo)
/// </summary>
public static class EstadoDeDocumentoEmitido
{
    public static IssuedDocumentStatus Calcular(bool anulado, DateOnly? firmezaHasta, DateOnly hoy)
    {
        if (anulado) return IssuedDocumentStatus.Voided;
        return firmezaHasta is { } hasta && hoy < hasta ? IssuedDocumentStatus.SubjectToFinality : IssuedDocumentStatus.Valid;
    }
}
