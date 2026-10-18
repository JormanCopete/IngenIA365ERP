using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;

namespace IngenIA365ERP.Domain.Lending.Risk;

/// <summary>Una garantía del crédito como la mira la PDI: su clase y si está perfeccionada (vigente) al cierre.</summary>
public sealed record GarantiaParaPdi(CollateralKind Clase, bool Perfeccionada);

/// <summary>
/// El tipo de garantía de la tabla de PDI del Anexo 2 de un crédito (feature 013, K6a, bloque 2; T1029, T1036; FR-106):
/// <list type="bullet">
/// <item><b>Clase → tipo</b>: prenda → <see cref="Prendaria"/>; hipoteca → <see cref="Hipotecaria"/>; fondo de garantías → <see cref="FondosDeGarantias"/>,
/// cada una sólo si está perfeccionada al cierre (si no, no es idónea: <see cref="NoIdonea"/>); codeudor, avalista (un codeudor del crédito) y
/// aportes → <see cref="NoIdonea"/> («avalistas, codeudores y garantías que no cumplen las características de idoneidad»; los aportes no se
/// descuentan mientras rija D19); pagaré → <see cref="SinGarantia"/> (firma personal). Sin garantías ni codeudores, <see cref="SinGarantia"/>.</item>
/// <item><b>Varias garantías</b>: a falta de una regla explícita de la norma, la <b>PDI más conservadora</b> —la mayor en el estado del crédito
/// (sin incumplimiento, la del segmento o el primer tramo; en incumplimiento, el tramo de sus días)—; en empate, la primera de la lista.
/// <b>Pendiente de validar por la contadora</b> (lectura del equipo del 2026-10-16).</item>
/// </list>
/// Los códigos son los de la columna <c>collateralType</c> del documento, no valores legales. Puro. (nuevo)
/// </summary>
public static class TipoDeGarantiaDePdi
{
    public const string Prendaria = "PRENDARIA";
    public const string Hipotecaria = "HIPOTECARIA";
    public const string FondosDeGarantias = "FONDOS_GARANTIAS";
    public const string NoIdonea = "NO_IDONEA";
    public const string SinGarantia = "SIN_GARANTIA";

    /// <summary>El tipo de la tabla de PDI de una garantía.</summary>
    public static string De(GarantiaParaPdi garantia)
    {
        ArgumentNullException.ThrowIfNull(garantia);
        return garantia.Clase switch
        {
            CollateralKind.Pledge => garantia.Perfeccionada ? Prendaria : NoIdonea,
            CollateralKind.Mortgage => garantia.Perfeccionada ? Hipotecaria : NoIdonea,
            CollateralKind.GuaranteeFund => garantia.Perfeccionada ? FondosDeGarantias : NoIdonea,
            CollateralKind.CoDebtor or CollateralKind.Contributions => NoIdonea,
            CollateralKind.PromissoryNote => SinGarantia,
            _ => throw new ArgumentOutOfRangeException(nameof(garantia), garantia.Clase, "Clase de garantía desconocida."),
        };
    }

    /// <summary>Los tipos candidatos de un crédito: los de sus garantías y, si tiene codeudores, «no idónea»; sin nada, «sin garantía».</summary>
    public static IReadOnlyList<string> Candidatos(IEnumerable<GarantiaParaPdi> garantias, bool tieneCodeudores)
    {
        ArgumentNullException.ThrowIfNull(garantias);
        var tipos = garantias.Select(De).ToList();
        if (tieneCodeudores) tipos.Add(NoIdonea);
        return tipos.Count == 0 ? [SinGarantia] : tipos.Distinct(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// De los <paramref name="candidatos"/>, el de mayor PDI para el crédito en su estado (el mismo cálculo de <see cref="MotorDePerdidaEsperada.Calcular"/>).
    /// Un tipo que el documento no trae no compite; si ninguno está, se devuelve el primero y el motor se niega con su mensaje.
    /// </summary>
    public static string ElMasConservador(Anexo2 anexo, string segmento, IReadOnlyList<string> candidatos, bool incumplido, int diasEnIncumplimiento)
    {
        ArgumentNullException.ThrowIfNull(anexo);
        ArgumentNullException.ThrowIfNull(candidatos);
        if (candidatos.Count == 0) throw new ArgumentException("Hace falta al menos un tipo de garantía.", nameof(candidatos));
        var seg = anexo.SegmentoObligatorio(segmento);
        string? mejor = null;
        var mayor = decimal.MinValue;
        foreach (var tipo in candidatos)
        {
            if (anexo.Pdi(tipo) is not { } pdi) continue;
            var valor = incumplido
                ? pdi.EnIncumplimiento(diasEnIncumplimiento)
                : seg.PdiSinIncumplimiento.FirstOrDefault(x => x.TipoDeGarantia == tipo)?.Fraccion ?? pdi.Tramos[0].Fraccion;
            if (valor > mayor)
            {
                mayor = valor;
                mejor = tipo;
            }
        }
        return mejor ?? candidatos[0];
    }
}
