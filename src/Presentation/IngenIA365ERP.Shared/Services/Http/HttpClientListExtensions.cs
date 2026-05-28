using System.Net.Http.Json;
using System.Text.Json;

namespace IngenIA365ERP.Shared.Services;

/// <summary>
/// Helpers para deserializar respuestas de listado del backend.
///
/// <para>
/// Problema que resuelve: el backend expone los listados via PagedList&lt;T&gt;,
/// cuya forma JSON es un OBJETO con shape:
/// <c>{ items: [...], totalCount, pageNumber, pageSize, totalPages, hasPreviousPage, hasNextPage }</c>.
/// Las paginas de Blazor estaban llamando <c>GetFromJsonAsync&lt;List&lt;T&gt;&gt;</c>,
/// que falla al deserializar un objeto como un array y la grilla quedaba vacia
/// (la excepcion era tragada por el try/catch de cada pagina).
/// </para>
///
/// <para>
/// <see cref="GetListAsync{T}"/> deserializa de forma defensiva: acepta tanto
/// <c>PagedList&lt;T&gt;</c> (nuevo, paginado) como un array plano (endpoints legacy
/// o de tipo lookup). Si no reconoce el shape, devuelve lista vacia.
/// </para>
/// </summary>
public static class HttpClientListExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// GET <paramref name="url"/> y devuelve <c>List&lt;T&gt;</c>, manejando indistintamente:
    ///   1) <c>PagedList&lt;T&gt;</c> -&gt; toma <c>.Items</c>.
    ///   2) Array JSON plano -&gt; lo deserializa directo.
    /// Lanza <see cref="HttpRequestException"/> si el response no es exitoso.
    /// </summary>
    public static async Task<List<T>> GetListAsync<T>(this HttpClient http, string url, CancellationToken ct = default)
    {
        using var response = await http.GetAsync(url, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
        var root = doc.RootElement;

        // Caso 1: array plano JSON -> deserializa como List<T>.
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.Deserialize<List<T>>(JsonOptions) ?? new List<T>();
        }

        // Caso 2: objeto con propiedad "items" / "Items" (forma de PagedList<T>).
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("items", out var itemsCamel) && itemsCamel.ValueKind == JsonValueKind.Array)
                return itemsCamel.Deserialize<List<T>>(JsonOptions) ?? new List<T>();

            if (root.TryGetProperty("Items", out var itemsPascal) && itemsPascal.ValueKind == JsonValueKind.Array)
                return itemsPascal.Deserialize<List<T>>(JsonOptions) ?? new List<T>();
        }

        // Forma desconocida: no se rompe, se entrega lista vacia.
        return new List<T>();
    }

    /// <summary>
    /// Variante que captura excepciones y siempre devuelve una lista (vacia ante error).
    /// Util para grillas donde la pagina no maneja errores explicitamente.
    /// </summary>
    public static async Task<List<T>> TryGetListAsync<T>(this HttpClient http, string url, CancellationToken ct = default)
    {
        try
        {
            return await http.GetListAsync<T>(url, ct).ConfigureAwait(false);
        }
        catch
        {
            return new List<T>();
        }
    }
}
