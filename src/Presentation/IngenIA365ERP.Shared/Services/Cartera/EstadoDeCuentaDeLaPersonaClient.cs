using System.Net.Http.Json;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Reportes;
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

    // 2026-11-16 (pedido del dueño): el envío lleva si los valores van con centavos; las secciones por persona, el PDF con membrete, el Excel
    // con el resumen de conceptos y la simulación de cancelación a una fecha.

    /// <summary>El envío por correo con o sin centavos (el PDF es el del membrete).</summary>
    public Task<ResultadoDeInventario<EnvioDelEstadoDeCuentaDto>> EnviarPorCorreoAsync(Guid persona, DateOnly? fecha, IReadOnlyList<string> columnas,
        bool incluirCerrados, bool decimales, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<EnvioDelEstadoDeCuentaDto>(http, auth, HttpMethod.Post, $"{Ruta}/send",
            new EnvioDelEstadoDeCuentaRequest(persona, fecha, columnas, incluirCerrados, decimales), clave, ct);

    /// <summary>Una sección por persona (cuotas de todas las obligaciones, circulares, gestiones, solicitudes, novedades, cupo) como tabla.</summary>
    public Task<ResultadoDeInventario<TablaReporteDto>> SeccionAsync(string seccion, Guid persona, DateOnly? fecha, CancellationToken ct = default) =>
        EnviarAsync<TablaReporteDto>(http, auth, HttpMethod.Get, ConQuery($"{Ruta}/sections/{seccion}", Query(
            ("personPublicId", persona.ToString()), ("date", Texto(fecha)))), null, null, ct);

    /// <summary>El estado de cuenta en PDF (con membrete) o Excel (<paramref name="formato"/> <c>pdf</c> | <c>xlsx</c>).</summary>
    public Task<InvitationApiResult<ArchivoDescargado>> DescargarAsync(string formato, Guid persona, DateOnly? fecha, IReadOnlyList<string> columnas,
        bool incluirCerrados, bool decimales, CancellationToken ct = default) =>
        EnvioDeVentas.DescargarAsync(http, auth, ConQuery($"{Ruta}/{formato}", Query(
                ("personPublicId", persona.ToString()), ("date", Texto(fecha)), ("columns", string.Join(',', columnas)),
                ("includeClosed", incluirCerrados ? "true" : null), ("decimals", decimales ? "true" : null))),
            $"estado-de-cuenta.{formato}", ct);

    /// <summary>Lo que costaría cancelar los créditos elegidos en <paramref name="fecha"/> (hoy o después).</summary>
    public Task<ResultadoDeInventario<SimulacionDeCancelacionDto>> SimularCancelacionAsync(Guid persona, IReadOnlyList<Guid> creditos, DateOnly fecha,
        CancellationToken ct = default) =>
        EnviarAsync<SimulacionDeCancelacionDto>(http, auth, HttpMethod.Post, $"{Ruta}/payoff-simulation",
            new SimulacionDeCancelacionRequest(persona, creditos, fecha), null, ct);

    /// <summary>La simulación en PDF (POST: lleva la lista de créditos).</summary>
    public async Task<InvitationApiResult<ArchivoDescargado>> SimulacionEnPdfAsync(Guid persona, IReadOnlyList<Guid> creditos, DateOnly fecha,
        CancellationToken ct = default)
    {
        if (auth.CurrentAccessToken is null)
            return InvitationApiResult<ArchivoDescargado>.Failure("Identity.NoAccessToken", "Falta el token. Vuelve a iniciar sesión.", 401);
        try
        {
            using var resp = await http.PostAsJsonAsync($"{Ruta}/payoff-simulation/pdf", new SimulacionDeCancelacionRequest(persona, creditos, fecha), ct);
            if (!resp.IsSuccessStatusCode) return await CentralAuthApi.ParseAsync<ArchivoDescargado>(resp, ct);
            return InvitationApiResult<ArchivoDescargado>.Success(await EnvioDeVentas.ArchivoAsync(resp, "simulacion-de-cancelacion.pdf", ct));
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<ArchivoDescargado>.NetworkError(ex.Message);
        }
    }

    /// <summary>Envía la simulación en PDF al correo registrado de la persona.</summary>
    public Task<ResultadoDeInventario<EnvioDelEstadoDeCuentaDto>> EnviarSimulacionAsync(Guid persona, IReadOnlyList<Guid> creditos, DateOnly fecha,
        ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<EnvioDelEstadoDeCuentaDto>(http, auth, HttpMethod.Post, $"{Ruta}/payoff-simulation/send",
            new SimulacionDeCancelacionRequest(persona, creditos, fecha), clave, ct);

    /// <summary>Las secciones que se abren con los botones generales, con su título y el permiso que pide cada una.</summary>
    public static readonly IReadOnlyList<(string Codigo, string Titulo, string Permiso)> Secciones =
    [
        ("installments", "Cuotas pendientes de todas las obligaciones", "Lending.Loans.Read"),
        ("messages", "Circulares y avisos de cobro", "Lending.DebtorMessages.Read"),
        ("collection-actions", "Gestiones de cobranza", "Lending.Collections.Read"),
        ("applications", "Solicitudes de crédito", "Lending.Applications.Read"),
        ("novelties", "Novedades y descuentos", "Lending.Novelties.Read"),
        ("credit-limits", "Cupo de crédito", "Lending.CreditLimits.Read"),
    ];
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

    /// <summary>2026-11-16 (pedido del dueño; feature 024, fase 2): el saldo de la administración, aparte del seguro.</summary>
    public decimal? Administracion { get; init; }

    /// <summary>
    /// 2026-11-16 (pedido del dueño; feature 024, fase 4): la cuota del período reconocida al inicio del ciclo y todavía no causada: con lo causado, la cuota
    /// completa que se ofrece para pagar.
    /// </summary>
    public decimal? ReconocidoPorCausar { get; init; }

    /// <summary>2026-11-24 (feature 025, HU2; FR-012): la tasa mensual vencida pactada, en puntos (1,8 = 1,8 % MV); nula si se pactó en E.A. (nuevo)</summary>
    public decimal? TasaMensual { get; init; }
}

/// <summary>
/// Los totales. 2026-11-16: el cupo de crédito y el resumen por concepto de lo que debe como deudor (capital, intereses, seguro, administración y otros);
/// nulos cuando no aplican o el servidor no los pudo leer.
/// </summary>
public sealed record TotalesDelEstadoDeCuentaDto(decimal SaldoDeCreditos, decimal ParaCancelar, decimal ValorVencido, int DiasDeMoraMaximos,
    decimal SaldoEnAportes, decimal SaldoEnAhorros, decimal? CupoAsignado = null, decimal? CupoDisponible = null,
    decimal? ConceptoCapital = null, decimal? ConceptoInteresCorriente = null, decimal? ConceptoInteresDeMora = null, decimal? ConceptoSeguro = null,
    decimal? ConceptoOtros = null)
{
    public bool ConResumenDeConceptos => ConceptoCapital is not null;

    /// <summary>2026-11-16 (pedido del dueño; feature 024, fase 2): la administración del resumen, aparte del seguro y de los otros.</summary>
    public decimal? ConceptoAdministracion { get; init; }

    public decimal TotalDeConceptos => (ConceptoCapital ?? 0m) + (ConceptoInteresCorriente ?? 0m) + (ConceptoInteresDeMora ?? 0m)
        + (ConceptoSeguro ?? 0m) + (ConceptoAdministracion ?? 0m) + (ConceptoOtros ?? 0m);
}

/// <summary>2026-11-16: el membrete de la cooperativa para imprimir (nombre, NIT y logotipo de Cartera como data URI).</summary>
public sealed record MembreteDto(string Cooperativa, string? Nit, string? LogoDataUri);

public sealed record EstadoDeCuentaDeLaPersonaDto(PersonaDelEstadoDeCuentaDto Persona, DateOnly Fecha, IReadOnlyList<ProductoDelEstadoDeCuentaDto> Productos,
    TotalesDelEstadoDeCuentaDto Totales, IReadOnlyList<string> Avisos, MembreteDto? Membrete = null);

public sealed record EnvioDelEstadoDeCuentaRequest(Guid PersonPublicId, DateOnly? Date, IReadOnlyList<string> Columns, bool IncludeClosed, bool Decimals = false);

/// <summary>2026-11-16: los créditos a simular y la fecha de pago (hoy o después).</summary>
public sealed record SimulacionDeCancelacionRequest(Guid PersonPublicId, IReadOnlyList<Guid> Loans, DateOnly Date);

/// <summary>
/// Lo que costaría cancelar un crédito a la fecha simulada, por concepto. <see cref="InteresNoCausadoDeLaCuotaEnCurso"/> es lo que se suma cuando
/// la empresa cobra la cuota en curso completa (parámetro <c>Pagos.InteresesAlPagarAntesDelVencimiento</c>).
/// </summary>
public sealed record CreditoDeLaSimulacionDto(Guid LoanPublicId, string Numero, string Linea, decimal Capital, decimal InteresCorriente,
    decimal InteresDeMora, decimal Seguro, decimal Otros, decimal Anticipo, decimal Total, decimal? InteresNoCausadoDeLaCuotaEnCurso,
    IReadOnlyList<string> Avisos)
{
    /// <summary>2026-11-16 (pedido del dueño; feature 024, fase 2): la administración, aparte del seguro y de los otros.</summary>
    public decimal Administracion { get; init; }
}

public sealed record SimulacionDeCancelacionDto(DateOnly Fecha, string ModoDeIntereses, IReadOnlyList<CreditoDeLaSimulacionDto> Creditos, decimal Total,
    MembreteDto? Membrete, PersonaDelEstadoDeCuentaDto Persona)
{
    public bool CobraCuotaCompleta => ModoDeIntereses == "CuotaCompleta";
}

public sealed record EnvioDelEstadoDeCuentaDto(string SentTo, int Products);

/// <summary>
/// 2026-11-16 (pedido del dueño): cómo se ven los valores del estado de cuenta y del detalle de una obligación en la caja: sin símbolo de moneda
/// y, según la preferencia <c>cartera.estado-de-cuenta.decimales</c> del usuario, con o sin centavos. El tamaño del texto
/// (<c>cartera.estado-de-cuenta.tamano-texto</c>) se vuelve una clase CSS. (nuevo)
/// </summary>
public static class FormatoDelEstadoDeCuenta
{
    private static readonly System.Globalization.CultureInfo Cultura = System.Globalization.CultureInfo.GetCultureInfo("es-CO");

    public static readonly IReadOnlyList<(string Codigo, string Nombre)> Tamanos = [("pequeno", "Pequeño"), ("normal", "Normal"), ("grande", "Grande")];

    /// <summary>Un valor en pesos sin símbolo: <c>1.234.567</c> o <c>1.234.567,89</c>.</summary>
    public static string Valor(decimal valor, bool decimales) => valor.ToString(decimales ? "N2" : "N0", Cultura);

    public static string Valor(decimal? valor, bool decimales) => valor is { } v ? Valor(v, decimales) : "—";

    /// <summary>La clase CSS del tamaño elegido (desconocido = normal).</summary>
    public static string ClaseDeTamano(string? tamano) => tamano switch
    {
        "pequeno" => "texto-estado-pequeno",
        "grande" => "texto-estado-grande",
        _ => "texto-estado-normal",
    };
}

/// <summary>
/// Las columnas del estado de cuenta que cada usuario elige (2026-11-13): el mismo catálogo cerrado de
/// <c>Domain.Lending.PersonStatement.ColumnasDelEstadoDeCuenta</c> —el servidor rechaza un código que no esté allá— con el valor que pinta la
/// pantalla. 2026-11-16: el valor recibe si va con centavos. (nuevo)
/// </summary>
public static class ColumnasDelEstadoDeCuenta
{
    public sealed record Columna(string Codigo, string Nombre, bool PorDefecto, bool Numerica, Func<ProductoDelEstadoDeCuentaDto, bool, string> Valor);

    private static string P(decimal? v, bool d) => FormatoDelEstadoDeCuenta.Valor(v, d);
    private static string N(int? v) => v?.ToString() ?? "—";
    private static string F(DateOnly? v) => v is { } x ? TextosDeCreditos.Fecha(x) : "—";
    private static string T(string? v) => string.IsNullOrWhiteSpace(v) ? "—" : v;

    public static readonly IReadOnlyList<Columna> Todas =
    [
        new("producto", "Producto", true, false, (p, _) => p.Producto),
        new("numero", "Número", true, false, (p, _) => p.Numero),
        new("rol", "Rol", false, false, (p, _) => p.Rol),
        new("estado", "Estado", true, false, (p, _) => p.Estado),
        new("fecha", "Fecha", true, false, (p, _) => F(p.Fecha)),
        new("valorInicial", "Valor inicial", true, true, (p, d) => P(p.ValorInicial, d)),
        new("saldo", "Saldo", true, true, (p, d) => P(p.Saldo, d)),
        new("paraCancelar", "Para cancelar", false, true, (p, d) => P(p.ParaCancelar, d)),
        new("cuota", "Cuota", true, true, (p, d) => P(p.Cuota, d)),
        new("periodicidad", "Periodicidad", true, false, (p, _) => T(p.Periodicidad)),
        new("plazo", "Plazo", true, true, (p, _) => N(p.Plazo)),
        new("plazoMeses", "Plazo (meses)", false, true, (p, _) => N(p.PlazoMeses)),
        // 2026-11-24 (feature 025, HU2; FR-012): la E.A. y, si se pactó en MV, también la mensual.
        new("tasa", "Tasa E.A.", true, true, (p, _) => p.Tasa is { } t ? $"{t:N2} %{(p.TasaMensual is { } mv ? $" ({mv:0.####} % MV)" : "")}" : "—"),
        new("tipoDeCuota", "Tipo de cuota", true, false, (p, _) => T(p.TipoDeCuota)),
        new("primerVencimiento", "Primer venc.", true, false, (p, _) => F(p.PrimerVencimiento)),
        new("proximoVencimiento", "Próximo venc.", false, false, (p, _) => F(p.ProximoVencimiento)),
        new("valorProximaCuota", "Próxima cuota", false, true, (p, d) => P(p.ValorProximaCuota, d)),
        new("ultimoVencimiento", "Último venc.", false, false, (p, _) => F(p.UltimoVencimiento)),
        new("cuotasPagadas", "Pagadas", false, true, (p, _) => N(p.CuotasPagadas)),
        new("cuotasPendientes", "Pendientes", true, true, (p, _) => N(p.CuotasPendientes)),
        new("cuotasVencidas", "Vencidas", false, true, (p, _) => N(p.CuotasVencidas)),
        new("cuotasAnticipadas", "Anticipadas", false, true, (p, _) => N(p.CuotasAnticipadas)),
        new("cuotasExtras", "Extras", false, true, (p, _) => N(p.CuotasExtras)),
        new("diasDeMora", "Días mora", true, true, (p, _) => N(p.DiasDeMora)),
        new("valorVencido", "Vencido", true, true, (p, d) => P(p.ValorVencido, d)),
        new("interesCorriente", "Interés corriente", false, true, (p, d) => P(p.InteresCorriente, d)),
        new("interesDeMora", "Interés de mora", false, true, (p, d) => P(p.InteresDeMora, d)),
        new("seguro", "Seguro", false, true, (p, d) => P(p.Seguro, d)),
        new("administracion", "Administración", false, true, (p, d) => P(p.Administracion, d)),
        new("reconocidoPorCausar", "Reconocido", false, true, (p, d) => P(p.ReconocidoPorCausar, d)),
        new("anticipo", "Anticipo / a favor", false, true, (p, d) => P(p.Anticipo, d)),
        new("disponible", "Disponible", false, true, (p, d) => P(p.Disponible, d)),
        new("calificacion", "Calificación", false, false, (p, _) => T(p.Calificacion)),
        new("oficina", "Oficina", false, false, (p, _) => T(p.Oficina)),
    ];

    /// <summary>El nombre largo de cada columna (en el selector y la ayuda); en la tabla va el corto para que quepa sin desplazar.</summary>
    public static readonly IReadOnlyDictionary<string, string> NombresLargos = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["fecha"] = "Fecha del crédito o apertura", ["paraCancelar"] = "Total para cancelar", ["plazo"] = "Plazo (cuotas)",
        ["primerVencimiento"] = "Primer vencimiento", ["proximoVencimiento"] = "Próximo vencimiento", ["valorProximaCuota"] = "Valor próxima cuota",
        ["ultimoVencimiento"] = "Último vencimiento", ["cuotasPagadas"] = "Cuotas pagadas", ["cuotasPendientes"] = "Cuotas pendientes",
        ["cuotasVencidas"] = "Cuotas vencidas", ["cuotasAnticipadas"] = "Cuotas anticipadas", ["cuotasExtras"] = "Cuotas extras",
        ["diasDeMora"] = "Días de mora", ["valorVencido"] = "Valor vencido", ["anticipo"] = "Anticipo / saldo a favor",
        ["reconocidoPorCausar"] = "Cuota reconocida por causar",
    };

    public static string NombreLargo(Columna c) => NombresLargos.GetValueOrDefault(c.Codigo, c.Nombre);

    public static IReadOnlyList<string> PorDefecto { get; } = Todas.Where(c => c.PorDefecto).Select(c => c.Codigo).ToList();

    /// <summary>Las elegidas en el orden del catálogo, sin desconocidas; ninguna = las de por defecto.</summary>
    public static IReadOnlyList<Columna> Elegidas(IEnumerable<string>? codigos)
    {
        var pedidas = (codigos ?? []).ToHashSet(StringComparer.Ordinal);
        var lista = Todas.Where(c => pedidas.Contains(c.Codigo)).ToList();
        return lista.Count == 0 ? Todas.Where(c => c.PorDefecto).ToList() : lista;
    }
}
