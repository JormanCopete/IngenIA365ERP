namespace IngenIA365ERP.Application.Common.Paging;

/// <summary>
/// Resultado paginado canónico para las queries de la fase. Se inserta dentro
/// de un <see cref="Common.Models.Result{T}"/> cuando el query devuelve
/// múltiples filas. El cliente paginador (Blazor SfGrid o curl) usa
/// <see cref="Page"/> + <see cref="PageSize"/> para navegar.
/// </summary>
public sealed record PagedResult<TItem>(
    IReadOnlyList<TItem> Items,
    int Page,
    int PageSize,
    long TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
}

/// <summary>
/// Parámetros de paginación. Defaults razonables: página 1, 20 items.
/// El handler clampa <c>PageSize</c> a [1, 200] para evitar abuso.
/// </summary>
public sealed record PageRequest(int Page = 1, int PageSize = 20)
{
    public const int MaxPageSize = 200;

    public int SafePage => Page < 1 ? 1 : Page;
    public int SafePageSize => PageSize switch
    {
        < 1 => 20,
        > MaxPageSize => MaxPageSize,
        _ => PageSize
    };
}
