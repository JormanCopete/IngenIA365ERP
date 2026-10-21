using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Documents;

/// <summary>
/// Los eventos de un documento que deciden su estado (feature 014, US13, T194; contracts/documentos.md §1.2): anulado, reemplazado (sólo títulos de
/// CDAT, por la prórroga o la renovación) y liquidado (sólo títulos, el CDAT se redimió o se canceló). (nuevo)
/// </summary>
public readonly record struct EventosDeEstado(bool Anulado, bool Reemplazado = false, bool Liquidado = false)
{
    public static EventosDeEstado De(IEnumerable<IssuedDocumentEventKind> eventos)
    {
        ArgumentNullException.ThrowIfNull(eventos);
        var lista = eventos.ToList();
        return new EventosDeEstado(lista.Contains(IssuedDocumentEventKind.Voided), lista.Contains(IssuedDocumentEventKind.Superseded),
            lista.Contains(IssuedDocumentEventKind.Settled));
    }
}

/// <summary>
/// El estado de un documento emitido a una fecha (feature 013, US13, T0777; adaptadores-y-archivos.md §8.3; data-model §27.1). No es columna: se
/// calcula siempre con sus eventos y su firmeza, y el paso de «sujeto a firmeza» a «vigente» no lo marca ninguna tarea, lo da la fecha.
/// <list type="number">
/// <item><see cref="IssuedDocumentStatus.Voided"/> si tiene un evento de anulación (manual o por reverso);</item>
/// <item>feature 014 (T194; documentos.md §1.2): <see cref="IssuedDocumentStatus.Superseded"/> si un título posterior lo reemplazó y
/// <see cref="IssuedDocumentStatus.Settled"/> si el CDAT se redimió o se canceló (la redención después de una prórroga deja el último título
/// liquidado y los anteriores reemplazados);</item>
/// <item><see cref="IssuedDocumentStatus.SubjectToFinality"/> mientras <paramref name="hoy"/> sea anterior a <c>FinalityUntil</c> (D23);</item>
/// <item><see cref="IssuedDocumentStatus.Valid"/> en otro caso.</item>
/// </list>
/// Motor puro. (nuevo)
/// </summary>
public static class EstadoDeDocumentoEmitido
{
    public static IssuedDocumentStatus Calcular(bool anulado, DateOnly? firmezaHasta, DateOnly hoy) => Calcular(new EventosDeEstado(anulado), firmezaHasta, hoy);

    public static IssuedDocumentStatus Calcular(EventosDeEstado eventos, DateOnly? firmezaHasta, DateOnly hoy)
    {
        if (eventos.Anulado) return IssuedDocumentStatus.Voided;
        if (eventos.Reemplazado) return IssuedDocumentStatus.Superseded;
        if (eventos.Liquidado) return IssuedDocumentStatus.Settled;
        return firmezaHasta is { } hasta && hoy < hasta ? IssuedDocumentStatus.SubjectToFinality : IssuedDocumentStatus.Valid;
    }
}
