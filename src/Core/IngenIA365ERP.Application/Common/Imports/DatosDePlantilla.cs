namespace IngenIA365ERP.Application.Common.Imports;

/// <summary>
/// Lo que hoy tiene la cooperativa, para la descarga con datos (<c>?withData=true</c>; feature 012, T158;
/// contracts/plantillas.md §0.6): por hoja, las filas con un valor por columna en el orden de la definición. Así se
/// exporta, se corrige y se vuelve a importar con el mismo libro. (nuevo)
/// </summary>
public sealed record DatosDePlantilla(IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<object?>>> FilasPorHoja)
{
    public int TotalDeFilas => FilasPorHoja.Values.Sum(f => f.Count);

    public IReadOnlyList<IReadOnlyList<object?>> De(string hoja) =>
        FilasPorHoja.FirstOrDefault(p => string.Equals(p.Key, hoja, StringComparison.OrdinalIgnoreCase)).Value ?? [];
}
