using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// 2026-11-13 (pedido del dueño): el cliente del estado de cuenta de una persona (<c>/api/lending/person-statement</c>): la consulta con sus créditos
/// y cuentas y el envío en PDF al correo registrado de la persona. Exportar e imprimir van por <see cref="InformesDeCarteraClient"/> con la vista
/// <see cref="Vista"/>. Sin <c>Authorization</c> a mano. (nuevo)
/// </summary>
public sealed class EstadoDeCuentaDeLaPersonaClient(HttpClient http, CentralAuthClient auth)
{
    public const string Ruta = GobiernoDeCarteraClient.Base + "/person-statement";

    /// <summary>La vista del centro de informes de Cartera con el mismo estado de cuenta.</summary>
    public const string Vista = "person-statement";

    public Task<ResultadoDeInventario<EstadoDeCuentaDeLaPersonaDto>> ConsultarAsync(Guid persona, DateOnly? fecha, bool incluirCerrados,
        CancellationToken ct = default) =>
        EnviarAsync<EstadoDeCuentaDeLaPersonaDto>(http, auth, HttpMethod.Get, ConQuery(Ruta, Query(
            ("personPublicId", persona.ToString()), ("date", Texto(fecha)), ("includeClosed", incluirCerrados ? "true" : null))), null, null, ct);

    /// <summary>El envío lleva su <see cref="ClaveDeOperacion"/>: un doble clic o un reintento no manda dos correos.</summary>
    public Task<ResultadoDeInventario<EnvioDelEstadoDeCuentaDto>> EnviarPorCorreoAsync(Guid persona, DateOnly? fecha, IReadOnlyList<string> columnas,
        bool incluirCerrados, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<EnvioDelEstadoDeCuentaDto>(http, auth, HttpMethod.Post, $"{Ruta}/send",
            new EnvioDelEstadoDeCuentaRequest(persona, fecha, columnas, incluirCerrados), clave, ct);

    /// <summary>Los filtros de la vista <see cref="Vista"/> para exportar lo mismo que se ve (sin «?»).</summary>
    public static string QueryDeLaVista(Guid persona, DateOnly? fecha, IReadOnlyList<string> columnas, bool incluirCerrados)
    {
        var partes = new List<string> { $"person={persona}", $"columns={Uri.EscapeDataString(string.Join(',', columnas))}" };
        if (fecha is { } f) partes.Add($"asOf={f:yyyy-MM-dd}");
        if (incluirCerrados) partes.Add("includeClosed=true");
        return string.Join('&', partes);
    }
}

public sealed record PersonaDelEstadoDeCuentaDto(Guid PublicId, string Nombre, string Documento, string? Correo, string? Celular, bool EsAsociado);

public sealed record ProductoDelEstadoDeCuentaDto(
    string Clase,
    Guid PublicId,
    string Numero,
    string Producto,
    string Naturaleza,
    string Rol,
    string Estado,
    string? Oficina,
    DateOnly? Fecha,
    decimal? ValorInicial,
    decimal Saldo,
    decimal? ParaCancelar,
    decimal? Cuota,
    string? Periodicidad,
    int? Plazo,
    int? PlazoMeses,
    decimal? Tasa,
    string? TipoDeCuota,
    DateOnly? PrimerVencimiento,
    DateOnly? ProximoVencimiento,
    decimal? ValorProximaCuota,
    DateOnly? UltimoVencimiento,
    int? CuotasPagadas,
    int? CuotasPendientes,
    int? CuotasVencidas,
    int? CuotasAnticipadas,
    int? CuotasExtras,
    int? DiasDeMora,
    decimal? ValorVencido,
    decimal? InteresCorriente,
    decimal? InteresDeMora,
    decimal? Seguro,
    decimal? Anticipo,
    decimal? Disponible,
    string? Calificacion)
{
    public bool EsCredito => Clase == "Credito";
}

public sealed record TotalesDelEstadoDeCuentaDto(decimal SaldoDeCreditos, decimal ParaCancelar, decimal ValorVencido, int DiasDeMoraMaximos,
    decimal SaldoEnAportes, decimal SaldoEnAhorros);

public sealed record EstadoDeCuentaDeLaPersonaDto(PersonaDelEstadoDeCuentaDto Persona, DateOnly Fecha, IReadOnlyList<ProductoDelEstadoDeCuentaDto> Productos,
    TotalesDelEstadoDeCuentaDto Totales, IReadOnlyList<string> Avisos);

public sealed record EnvioDelEstadoDeCuentaRequest(Guid PersonPublicId, DateOnly? Date, IReadOnlyList<string> Columns, bool IncludeClosed);

public sealed record EnvioDelEstadoDeCuentaDto(string SentTo, int Products);

/// <summary>
/// Las columnas del estado de cuenta que cada usuario elige (2026-11-13): el mismo catálogo cerrado de
/// <c>Domain.Lending.PersonStatement.ColumnasDelEstadoDeCuenta</c> —el servidor rechaza un código que no esté allá— con el valor que pinta la
/// pantalla. (nuevo)
/// </summary>
public static class ColumnasDelEstadoDeCuenta
{
    public sealed record Columna(string Codigo, string Nombre, bool PorDefecto, bool Numerica, Func<ProductoDelEstadoDeCuentaDto, string> Valor);

    private static string P(decimal? v) => v is { } x ? TextosDeCreditos.Pesos(x) : "—";
    private static string N(int? v) => v?.ToString() ?? "—";
    private static string F(DateOnly? v) => v is { } x ? TextosDeCreditos.Fecha(x) : "—";
    private static string T(string? v) => string.IsNullOrWhiteSpace(v) ? "—" : v;

    public static readonly IReadOnlyList<Columna> Todas =
    [
        new("producto", "Producto", true, false, p => p.Producto),
        new("numero", "Número", true, false, p => p.Numero),
        new("rol", "Rol", false, false, p => p.Rol),
        new("estado", "Estado", true, false, p => p.Estado),
        new("fecha", "Fecha del crédito o apertura", true, false, p => F(p.Fecha)),
        new("valorInicial", "Valor inicial", true, true, p => P(p.ValorInicial)),
        new("saldo", "Saldo", true, true, p => P(p.Saldo)),
        new("paraCancelar", "Total para cancelar", false, true, p => P(p.ParaCancelar)),
        new("cuota", "Cuota", true, true, p => P(p.Cuota)),
        new("periodicidad", "Periodicidad", true, false, p => T(p.Periodicidad)),
        new("plazo", "Plazo (cuotas)", true, true, p => N(p.Plazo)),
        new("plazoMeses", "Plazo (meses)", false, true, p => N(p.PlazoMeses)),
        new("tasa", "Tasa E.A.", true, true, p => p.Tasa is { } t ? $"{t:N2} %" : "—"),
        new("tipoDeCuota", "Tipo de cuota", true, false, p => T(p.TipoDeCuota)),
        new("primerVencimiento", "Primer vencimiento", true, false, p => F(p.PrimerVencimiento)),
        new("proximoVencimiento", "Próximo vencimiento", false, false, p => F(p.ProximoVencimiento)),
        new("valorProximaCuota", "Valor próxima cuota", false, true, p => P(p.ValorProximaCuota)),
        new("ultimoVencimiento", "Último vencimiento", false, false, p => F(p.UltimoVencimiento)),
        new("cuotasPagadas", "Cuotas pagadas", false, true, p => N(p.CuotasPagadas)),
        new("cuotasPendientes", "Cuotas pendientes", true, true, p => N(p.CuotasPendientes)),
        new("cuotasVencidas", "Cuotas vencidas", false, true, p => N(p.CuotasVencidas)),
        new("cuotasAnticipadas", "Cuotas anticipadas", false, true, p => N(p.CuotasAnticipadas)),
        new("cuotasExtras", "Cuotas extras", false, true, p => N(p.CuotasExtras)),
        new("diasDeMora", "Días de mora", true, true, p => N(p.DiasDeMora)),
        new("valorVencido", "Valor vencido", true, true, p => P(p.ValorVencido)),
        new("interesCorriente", "Interés corriente", false, true, p => P(p.InteresCorriente)),
        new("interesDeMora", "Interés de mora", false, true, p => P(p.InteresDeMora)),
        new("seguro", "Seguro", false, true, p => P(p.Seguro)),
        new("anticipo", "Anticipo / saldo a favor", false, true, p => P(p.Anticipo)),
        new("disponible", "Disponible", false, true, p => P(p.Disponible)),
        new("calificacion", "Calificación", false, false, p => T(p.Calificacion)),
        new("oficina", "Oficina", false, false, p => T(p.Oficina)),
    ];

    public static IReadOnlyList<string> PorDefecto { get; } = Todas.Where(c => c.PorDefecto).Select(c => c.Codigo).ToList();

    /// <summary>Las elegidas en el orden del catálogo, sin desconocidas; ninguna = las de por defecto.</summary>
    public static IReadOnlyList<Columna> Elegidas(IEnumerable<string>? codigos)
    {
        var pedidas = (codigos ?? []).ToHashSet(StringComparer.Ordinal);
        var lista = Todas.Where(c => pedidas.Contains(c.Codigo)).ToList();
        return lista.Count == 0 ? Todas.Where(c => c.PorDefecto).ToList() : lista;
    }
}
