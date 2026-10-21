using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;

namespace IngenIA365ERP.Domain.Lending.Risk;

/// <summary>
/// La calificación contable (feature 013, US9, T0692; T48; data-model §23.2 <c>AccountingRating</c>): la letra A–E con la que la
/// matriz elige la cuenta. En el Anexo 1 es la misma categoría y las subcategorías van a su letra (E1 y E2 son E, porque el CUIF no
/// las distingue); la homologación de la pérdida esperada a A–E (K6a, <see cref="DePerdidaEsperada"/>) sale de la tabla del Anexo 2. Pura. (nuevo)
/// </summary>
public static class HomologacionDeCalificacion
{
    /// <summary>La letra contable de una categoría y su subcategoría.</summary>
    public static RatingCategory Contable(RatingCategory categoria, RatingSubcategory subcategoria)
    {
        if (!Enum.IsDefined(categoria)) throw new ArgumentOutOfRangeException(nameof(categoria), categoria, "Categoría desconocida.");
        if (subcategoria != RatingSubcategory.None && categoria != RatingCategory.E)
            throw new ArgumentException($"Sólo la categoría E tiene subcategorías; llegó {categoria} {subcategoria}.", nameof(subcategoria));
        return categoria;
    }

    /// <summary>
    /// La calificación contable en un segmento de pérdida esperada (K6a, T1030; Anexo 2, numeral 5; T48): la homologación es obligatoria y
    /// <b>sólo procede si la calificación máxima es la del modelo</b> (Anexo 1, numeral 3): entonces la tabla del documento la lleva a A–E según
    /// la calificación del modelo y la mora actual del deudor (la máxima en sus créditos alineados por modalidad). El incumplido se homologa
    /// siempre por su mora (si la mora no llega a la primera fila —un pago único incumplido con pocos días—, toma la primera fila del
    /// incumplido: lectura del equipo, a validar). Si la máxima es de otra regla (mora del Anexo 1, análisis, evaluación…), la letra es esa.
    /// </summary>
    public static (RatingCategory Contable, bool Homologada, string Detalle) DePerdidaEsperada(Anexo2 anexo, string segmento,
        RatingCategory calificacionFinal, RatingCategory? calificacionDelModelo, bool incumplido, int moraActual)
    {
        ArgumentNullException.ThrowIfNull(anexo);
        var tabla = anexo.HomologacionDe(segmento);
        var mora = moraActual.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (incumplido)
        {
            var filas = tabla.Filas.Where(f => f.CalificacionDelModelo is null).OrderBy(f => f.MoraDesde).ToList();
            var fila = filas.FirstOrDefault(f => f.Contiene(moraActual)) ?? filas[0];
            return (fila.Contable, true, $"Homologación: incumplido con mora actual del deudor de {mora} días → {fila.Contable}.");
        }
        if (calificacionDelModelo != calificacionFinal)
            return (Contable(calificacionFinal, RatingSubcategory.None), false,
                $"Sin homologación: la calificación máxima {calificacionFinal} no es la del modelo ({calificacionDelModelo?.ToString() ?? "sin puntaje"}); contable {calificacionFinal}.");
        var deModelo = tabla.Filas.First(f => f.CalificacionDelModelo == calificacionFinal && f.Contiene(moraActual));
        return (deModelo.Contable, true,
            $"Homologación: modelo {calificacionFinal} con mora actual del deudor de {mora} días → {deModelo.Contable}.");
    }

    /// <summary>Cómo se muestra: «C», «E1», «E2».</summary>
    public static string Texto(RatingCategory categoria, RatingSubcategory subcategoria) =>
        subcategoria == RatingSubcategory.None ? categoria.ToString() : subcategoria.ToString();
}
