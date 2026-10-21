namespace IngenIA365ERP.Domain.Lending.Documents;

/// <summary>Un documento emitido tal como lo mira la vigencia: su Id, los créditos que cubre, su <c>InformationCutoff</c> (R) y si ya está anulado. (nuevo)</summary>
public sealed record DocumentoQueCubre(int Id, IReadOnlyCollection<int> Creditos, DateTime InformacionHasta, bool Anulado);

/// <summary>
/// Qué documentos deja sin valor un reverso (feature 013, US13, T0777; T80; adaptadores-y-archivos.md §8.8; data-model §27.3). Un documento certifica
/// lo que se sabía en su <c>InformationCutoff</c>; si después se reversa una operación del crédito registrada <b>antes o en ese instante</b>, lo que
/// certificó ya no es cierto: el paz y salvo emitido después del pago que canceló, el certificado de saldo que contaba ese pago. Por eso anula todo
/// documento que <b>cubra el crédito</b> y cuyo <c>InformationCutoff</c> sea igual o posterior al registro de lo reversado; los emitidos antes de esa
/// operación no la conocían y siguen valiendo. Un documento ya anulado no se anula dos veces (hay una sola anulación por documento). Motor puro: no
/// escribe; el evento lo agrega <c>AnulacionDeDocumentosPorReverso</c> en la unidad de trabajo del reverso. (nuevo)
/// </summary>
public static class VigenciaDeDocumentos
{
    /// <summary>Los Ids de <paramref name="documentos"/> que el reverso de una operación del crédito <paramref name="loanId"/> registrada en
    /// <paramref name="registroDeLoReversado"/> anula, en orden de Id.</summary>
    public static IReadOnlyList<int> AAnular(IEnumerable<DocumentoQueCubre> documentos, int loanId, DateTime registroDeLoReversado)
    {
        ArgumentNullException.ThrowIfNull(documentos);
        return documentos
            .Where(d => !d.Anulado && d.Creditos.Contains(loanId) && d.InformacionHasta >= registroDeLoReversado)
            .Select(d => d.Id)
            .Distinct()
            .Order()
            .ToList();
    }
}
