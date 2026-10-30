namespace IngenIA365ERP.Domain.Treasury.Firmas;

/// <summary>Un firmante vigente de la cuenta, como lo ve la regla: su usuario, su clase de firma, hasta cuánto firma solo (nulo, no firma solo) y si firma en conjunto. (nuevo)</summary>
public sealed record FirmanteVigente(int UsuarioId, string Clase, decimal? TopeSolo, bool FirmaEnConjunto);

/// <summary>El veredicto de la combinación de firmas de un lote: si la cuenta exige firmas, si se cumple y quiénes firmaron. (nuevo)</summary>
public sealed record VeredictoDeFirmas(bool ExigeFirmas, bool Cumple, IReadOnlyList<int> Firmaron, string? Motivo);

/// <summary>
/// La combinación de firmas de un lote de pago (feature 015, T2, fase 42; FR-208; pedido del dueño para T2): las aprobaciones del lote tienen que venir de los firmantes
/// vigentes de la cuenta de origen en una combinación válida para el monto: un firmante cuyo tope para firmar solo cubre el lote, o dos firmantes distintos que firman en
/// conjunto. Si la cuenta no tiene firmantes registrados, no exige nada (la combinación es de la cuenta, no del lote). Pura. (nuevo)
/// </summary>
public static class CombinacionDeFirmas
{
    public static VeredictoDeFirmas Evaluar(decimal monto, IReadOnlyCollection<FirmanteVigente> firmantes, IReadOnlyCollection<int> aprobadores)
    {
        ArgumentNullException.ThrowIfNull(firmantes);
        ArgumentNullException.ThrowIfNull(aprobadores);
        if (firmantes.Count == 0) return new VeredictoDeFirmas(false, true, [], null);
        var firmaron = firmantes.Where(f => aprobadores.Contains(f.UsuarioId)).ToList();
        var ids = firmaron.Select(f => f.UsuarioId).Distinct().ToList();
        if (firmaron.Any(f => f.TopeSolo is { } tope && tope >= monto)) return new VeredictoDeFirmas(true, true, ids, null);
        var conjuntas = firmaron.Where(f => f.FirmaEnConjunto).Select(f => f.UsuarioId).Distinct().Count();
        if (conjuntas >= 2) return new VeredictoDeFirmas(true, true, ids, null);
        var motivo = firmaron.Count == 0
            ? "Ninguno de los que aprobaron el lote es firmante vigente de la cuenta."
            : "Las aprobaciones no hacen una combinación de firmas válida para el monto: hace falta un firmante que firme solo por ese valor o dos que firmen en conjunto.";
        return new VeredictoDeFirmas(true, false, ids, motivo);
    }
}
