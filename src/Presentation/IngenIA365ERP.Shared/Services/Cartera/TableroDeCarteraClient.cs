using System.Globalization;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Cliente tipado del tablero de Cartera (feature 013, US15, K5, T0985; contracts/api.md §17): <c>GET /api/lending/dashboard?branch=&amp;line=&amp;asOf=</c>,
/// con <c>Lending.Dashboard.View</c>. Sólo lee: sin clave. Sin <c>Authorization</c> a mano (lo pone el handler). Molde: el tablero de inventario
/// (<c>InventarioClient.TableroAsync</c>). (nuevo)
/// </summary>
public sealed class TableroDeCarteraClient(HttpClient http, CentralAuthClient auth)
{
    public const string RutaDelTablero = "/api/lending/dashboard";

    /// <summary>El tablero a <paramref name="corte"/> (hoy si no viene), de la oficina y la línea dadas (todas las del alcance si no).</summary>
    public Task<ResultadoDeInventario<TableroDeCarteraDto>> TableroAsync(Guid? oficina, Guid? linea, DateOnly? corte = null, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<TableroDeCarteraDto>(http, auth, HttpMethod.Get, EnvioDeVentas.ConQuery(RutaDelTablero, Query(oficina, linea, corte)), null, null, ct);

    /// <summary>La query del tablero, sin «?»; vacía si no hay filtros.</summary>
    public static string Query(Guid? oficina, Guid? linea, DateOnly? corte)
    {
        var partes = new List<string>();
        if (oficina is { } o) partes.Add($"branch={o}");
        if (linea is { } l) partes.Add($"line={l}");
        if (corte is { } c) partes.Add($"asOf={c.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}");
        return string.Join('&', partes);
    }
}

/// <summary>Una oficina o una línea del alcance, para los filtros del tablero. (nuevo)</summary>
public sealed record ElementoDelTableroDeCarteraDto
{
    public Guid PublicId { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
}

/// <summary>Las oficinas y las líneas que quien consulta puede ver. (nuevo)</summary>
public sealed record AlcanceDelTableroDeCarteraDto
{
    public IReadOnlyList<ElementoDelTableroDeCarteraDto> Branches { get; init; } = [];
    public IReadOnlyList<ElementoDelTableroDeCarteraDto> Lines { get; init; } = [];
}

/// <summary>Una ficha del tablero: <see cref="Unit"/> es Money, Count, Percent o Days; <see cref="Severity"/>, Info, Warning o Critical. (nuevo)</summary>
public sealed record FichaDelTableroDeCarteraDto
{
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    public decimal? Value { get; init; }
    public decimal? PreviousValue { get; init; }
    public string Unit { get; init; } = "";
    public string? Severity { get; init; }
    public EnlaceDeFichaDto Link { get; init; } = new();
    /// <summary>Cómo se lee la ficha o por qué no tiene dato.</summary>
    public string? Note { get; init; }
}

/// <summary>El tablero de Cartera como lo devuelve la API. (nuevo)</summary>
public sealed record TableroDeCarteraDto
{
    public DateOnly AsOf { get; init; }
    public AlcanceDelTableroDeCarteraDto Scope { get; init; } = new();
    public IReadOnlyList<FichaDelTableroDeCarteraDto> Tiles { get; init; } = [];
}
