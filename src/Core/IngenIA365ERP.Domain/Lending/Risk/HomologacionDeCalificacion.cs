using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Risk;

/// <summary>
/// La calificación contable (feature 013, US9, T0692; T48; data-model §23.2 <c>AccountingRating</c>): la letra A–E con la que la
/// matriz elige la cuenta. En el Anexo 1 es la misma categoría y las subcategorías van a su letra (E1 y E2 son E, porque el CUIF no
/// las distingue); la homologación de la pérdida esperada a A–E llega con K6a sobre el mismo punto. Pura. (nuevo)
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

    /// <summary>Cómo se muestra: «C», «E1», «E2».</summary>
    public static string Texto(RatingCategory categoria, RatingSubcategory subcategoria) =>
        subcategoria == RatingSubcategory.None ? categoria.ToString() : subcategoria.ToString();
}
