namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Los textos en español de las captaciones y los aportes (feature 014, US1, T070): naturalezas, clases, métodos de saldo, modos de tramos y de
/// cuota, y lo del CDAT y el contractual. Los valores son los números de los enums del servidor (<c>DepositEnums</c>), que la API devuelve como
/// número; los pedidos los mandan igual. Ningún texto muestra un código técnico. (nuevo)
/// </summary>
public static class TextosDeCaptaciones
{
    public const int Colocacion = 1;
    public const int Captacion = 2;
    public const int Aportes = 3;

    public const int ALaVista = 1;
    public const int Contractual = 2;
    public const int Permanente = 3;
    public const int Cdat = 4;

    public static IReadOnlyList<(int Valor, string Texto)> Naturalezas { get; } = [(1, "Crédito"), (2, "Captación"), (3, "Aportes")];

    public static IReadOnlyList<(int Valor, string Texto)> Clases { get; } =
        [(ALaVista, "Ahorro a la vista"), (Contractual, "Ahorro contractual"), (Permanente, "Ahorro permanente"), (Cdat, "CDAT")];

    public static IReadOnlyList<(int Valor, string Texto)> MetodosDeSaldo { get; } = [(1, "Saldo diario"), (2, "Saldo promedio"), (3, "Saldo mínimo")];

    public static IReadOnlyList<(int Valor, string Texto)> ModosDeTramos { get; } =
        [(1, "Todo el saldo a la tasa de su tramo"), (2, "Cada porción a la tasa de su tramo")];

    public static IReadOnlyList<(int Valor, string Texto)> ModalidadesDeInteres { get; } = [(1, "Vencidos"), (2, "Anticipados")];

    public static IReadOnlyList<(int Valor, string Texto)> ReglasDeProrroga { get; } =
        [(1, "La misma tasa"), (2, "La tasa vigente para el plazo"), (3, "Una tasa pactada")];

    public static IReadOnlyList<(int Valor, string Texto)> TratamientosAlVencer { get; } = [(1, "Deja de causar (vencido por pagar)"), (2, "Pasa a la vista")];

    public static IReadOnlyList<(int Valor, string Texto)> PenalidadesDeRedencion { get; } =
        [(1, "Reliquidar a una tasa de penalidad"), (2, "Descontar una parte del interés causado")];

    public static IReadOnlyList<(int Valor, string Texto)> EfectosDelIncumplimiento { get; } =
        [(1, "Tasa castigo"), (2, "Pasa a la vista"), (3, "Se cancela")];

    public static IReadOnlyList<(int Valor, string Texto)> ReconocimientosDeBonificacion { get; } = [(1, "Al cumplir"), (2, "Provisionada")];

    public static IReadOnlyList<(int Valor, string Texto)> ModosDeValorDeAportes { get; } =
        [(1, "Valor fijo"), (2, "Porcentaje del ingreso"), (3, "Salarios mínimos")];

    public static IReadOnlyList<(int Valor, string Texto)> AccionesDelExceso { get; } = [(1, "Rechazarlo"), (2, "Devolverlo por decisión")];

    /// <summary>Las tarifas de retención que una línea puede nombrar (vacía = la de su clase).</summary>
    public static IReadOnlyList<(string Valor, string Texto)> TarifasDeRetencion { get; } =
    [
        ("", "La de su clase"), ("Retencion.TarifaAhorro", "Tarifa de ahorro"), ("Retencion.TarifaCdat", "Tarifa de CDAT"),
        ("Retencion.TarifaContractual", "Tarifa del contractual"), ("Retencion.TarifaPermanente", "Tarifa del permanente"),
    ];

    public static string Naturaleza(int v) => De(Naturalezas, v);

    public static string Clase(int? v) => v is { } x ? De(Clases, x) : "—";

    public static string MetodoDeSaldo(int v) => De(MetodosDeSaldo, v);

    public static string ModoDeValorDeAportes(int v) => De(ModosDeValorDeAportes, v);

    /// <summary>El parámetro <c>?naturaleza=</c> de la pantalla del catálogo: <c>captacion</c>, <c>aportes</c> o <c>credito</c>.</summary>
    public static int NaturalezaDeLaRuta(string? texto) => texto?.Trim().ToLowerInvariant() switch
    {
        "captacion" or "captación" => Captacion,
        "aportes" => Aportes,
        _ => Colocacion,
    };

    public static string RutaDeLaNaturaleza(int naturaleza) => naturaleza switch
    {
        Captacion => "captacion",
        Aportes => "aportes",
        _ => "credito",
    };

    private static string De(IReadOnlyList<(int Valor, string Texto)> lista, int v)
    {
        foreach (var (valor, texto) in lista)
            if (valor == v) return texto;
        return v.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
