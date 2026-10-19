using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Domain.Entities.Accounting.Lending;
using IngenIA365ERP.Domain.Enums.Lending;
using R = IngenIA365ERP.Application.Accounting.Lending.Reglas.RolesDeCuentaDeCartera;

namespace IngenIA365ERP.Application.Accounting.Lending.Plantilla;

/// <summary>
/// Una fila sugerida de la matriz para captaciones o aportes (feature 014, US8, T144; contabilidad.md §4.2 de la 014): el rol con los valores que exige, la
/// cuenta CUIF sugerida —texto <b>informativo</b>, «sugerencia, pendiente de la contadora»: nadie la aplica— y si la completitud la exige
/// (<see cref="Exigida"/>: lo que las entregas K7 y K8 emiten desde el primer día; lo de la asamblea, los no reclamados y los embargos sólo se sugiere). (nuevo)
/// </summary>
public sealed record FilaSugeridaDeCaptacion(
    string Rol,
    string? Naturaleza = null,
    string? Clase = null,
    string? Plazo = null,
    string? ClaseDeAporte = null,
    string? Marca = null,
    string? Motivo = null,
    string? ClaseDeRecaudador = null,
    string? CuentaSugerida = null,
    bool Exigida = true)
{
    /// <summary>La <c>DimensionKey</c> que tendría la regla (hecho <c>*</c>, sin línea ni sucursal).</summary>
    public string Clave => LendingPostingRule.ClaveDe(LendingPostingRule.Cualquiera, Rol, null, null, null, null, null, null, null, null, null,
        ClaseDeRecaudador, Motivo, null, Naturaleza, Clase, Plazo, ClaseDeAporte, Marca);

    /// <summary>La naturaleza de línea a la que pertenece la fila (para el filtro por naturaleza).</summary>
    public string NaturalezaDeLaFila => ClaseDeAporte is not null || Naturaleza == nameof(LineNature.Contribution)
        || Rol is R.FondoDeRevalorizacion or R.FondoDeExcedentes or R.PerdidaAbsorbida
        ? nameof(LineNature.Contribution)
        : nameof(LineNature.Deposit);

    /// <summary>Los valores que la fila busca en la matriz (los de una cuenta con esas dimensiones).</summary>
    public ValoresDeCartera Valores => new(CollectorClass: ClaseDeRecaudador, ReasonCode: Motivo, LineNature: Naturaleza ?? NaturalezaDeLaFila, DepositClass: Clase,
        IssueTermBand: Plazo, ContributionClass: ClaseDeAporte, AccountFlag: Marca);
}

/// <summary>
/// Las filas sugeridas sin cuenta de captaciones y aportes (feature 014, US8, T144; contabilidad.md §2 y §4.2 de la 014; investigación §3.9): una por cada
/// combinación hecho × rol × valores exigidos de las naturalezas y clases que tienen líneas, con la cuenta del CUIF como <b>sugerencia</b>. Ninguna regla se
/// crea sola (P1, C70): la plantilla las pone sin cuenta y la importación las ignora mientras sigan sin ella; la completitud dice cuáles exigidas faltan.
/// El gasto por intereses se sugiere con el hecho en <c>*</c> (no fijado en la causación) porque el ajuste de la causación usa el mismo rol. (nuevo)
/// </summary>
public static class SugerenciasDeCaptaciones
{
    private const string Deposito = nameof(LineNature.Deposit);
    private const string Aportes = nameof(LineNature.Contribution);

    private static readonly IReadOnlyDictionary<string, (string Interes, string Gasto, string Gmf)> PorClase =
        new Dictionary<string, (string, string, string)>(StringComparer.Ordinal)
        {
            [nameof(DepositClass.Demand)] = ("210595", "615005", "243005"),
            [nameof(DepositClass.TermDeposit)] = ("211095", "615010", "243010"),
            [nameof(DepositClass.Contractual)] = ("212595", "615015", "243015"),
            [nameof(DepositClass.Permanent)] = ("213095", "615020", "243020"),
        };

    private static readonly IReadOnlyDictionary<string, string> CdatPorPlazo = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [nameof(IssueTermBand.UnderSixMonths)] = "211005",
        [nameof(IssueTermBand.SixToTwelve)] = "211010",
        [nameof(IssueTermBand.TwelveToEighteen)] = "211015",
        [nameof(IssueTermBand.EighteenOrMore)] = "211020",
    };

    private static bool Corto(string plazo) => plazo is nameof(IssueTermBand.UnderSixMonths) or nameof(IssueTermBand.SixToTwelve);

    /// <summary>
    /// Las sugerencias de las naturalezas y clases de las líneas (activas o no) del catálogo de Cartera: sin líneas de captación ni de aportes, ninguna. Una
    /// línea de captación sin clase conocida cuenta como ahorro a la vista.
    /// </summary>
    public static IReadOnlyList<FilaSugeridaDeCaptacion> De(IEnumerable<LineaDeCarteraDto> lineas)
    {
        var lista = lineas.ToList();
        var clases = lista.Where(l => l.LineNature == Deposito).Select(l => l.DepositClass ?? nameof(DepositClass.Demand))
            .Distinct(StringComparer.Ordinal).OrderBy(c => (int)Enum.Parse<DepositClass>(c)).ToList();
        var conAportes = lista.Any(l => l.LineNature == Aportes);
        return De(clases, conAportes);
    }

    /// <summary>Las sugerencias para unas clases de captación y, si <paramref name="conAportes"/>, las de aportes.</summary>
    public static IReadOnlyList<FilaSugeridaDeCaptacion> De(IReadOnlyCollection<string> clasesDeCaptacion, bool conAportes)
    {
        var filas = new List<FilaSugeridaDeCaptacion>();
        foreach (var clase in clasesDeCaptacion)
        {
            var (interes, gasto, gmf) = PorClase[clase];
            switch (clase)
            {
                case nameof(DepositClass.Demand):
                    foreach (var marca in R.MarcasDeCuenta) filas.Add(new(R.CapitalDeCaptacion, Clase: clase, Marca: marca, CuentaSugerida: "210505"));
                    filas.Add(new(R.CapitalDeCaptacionInactiva, Clase: clase, CuentaSugerida: "210510"));
                    break;
                case nameof(DepositClass.TermDeposit):
                    foreach (var (plazo, cuenta) in CdatPorPlazo)
                        filas.Add(new(R.CapitalDeCaptacion, Clase: clase, Plazo: plazo, Marca: nameof(AccountFlag.Standard), CuentaSugerida: cuenta));
                    break;
                case nameof(DepositClass.Contractual):
                    foreach (var plazo in R.PlazosDeEmision)
                    {
                        filas.Add(new(R.CapitalDeCaptacion, Clase: clase, Plazo: plazo, Marca: nameof(AccountFlag.Standard), CuentaSugerida: Corto(plazo) ? "212505" : "212515"));
                        filas.Add(new(R.CapitalDeCaptacionInactiva, Clase: clase, Plazo: plazo, CuentaSugerida: Corto(plazo) ? "212510" : "212520"));
                    }
                    filas.Add(new(R.GastoPorBonificacion, Clase: clase, CuentaSugerida: "615015"));
                    break;
                case nameof(DepositClass.Permanent):
                    filas.Add(new(R.CapitalDeCaptacion, Clase: clase, Marca: nameof(AccountFlag.Standard), CuentaSugerida: "213005 o 213010"));
                    break;
            }
            filas.Add(new(R.InteresPorPagar, Clase: clase, CuentaSugerida: interes));
            filas.Add(new(R.GastoPorIntereses, Clase: clase, CuentaSugerida: gasto));
            filas.Add(new(R.RetencionPorPagar, Clase: clase, CuentaSugerida: "243535"));
            filas.Add(new(R.GmfPorPagar, Clase: clase, CuentaSugerida: gmf));
            if (clase is nameof(DepositClass.TermDeposit) or nameof(DepositClass.Contractual))
                filas.Add(new(R.IngresoPorPenalidad, Clase: clase));
        }

        if (clasesDeCaptacion.Count > 0)
        {
            filas.Add(new(R.DevolucionPorPagar, Naturaleza: Deposito, Motivo: "DepositWithdrawal"));
            filas.Add(new(R.DevolucionPorPagar, Naturaleza: Deposito, Motivo: "MembershipRefund", CuentaSugerida: "246510"));
            if (clasesDeCaptacion.Contains(nameof(DepositClass.TermDeposit)))
            {
                filas.Add(new(R.DevolucionPorPagar, Naturaleza: Deposito, Motivo: "TermDepositInterest"));
                filas.Add(new(R.DevolucionPorPagar, Naturaleza: Deposito, Motivo: "TermDepositRedemption"));
            }
            foreach (var cargo in R.MotivosDelCargo) filas.Add(new(R.IngresoPorCargos, Motivo: cargo));
            filas.Add(new(R.CargoDeCuentaPorCobrar));
            filas.Add(new(R.NoReclamadosPorPagar, Naturaleza: Deposito, CuentaSugerida: "246595", Exigida: false));
            filas.Add(new(R.EmbargosPorConsignar, Exigida: false));
            filas.Add(new(R.RecaudoPorAplicar, ClaseDeRecaudador: nameof(ClaseDeRecaudador.Treasury), Exigida: false));
        }

        if (conAportes)
        {
            filas.Add(new(R.AportesSociales, ClaseDeAporte: nameof(ContributionClass.Ordinary), CuentaSugerida: "310505"));
            filas.Add(new(R.AportesSociales, ClaseDeAporte: nameof(ContributionClass.Extraordinary), CuentaSugerida: "310510"));
            filas.Add(new(R.AportesSociales, ClaseDeAporte: nameof(ContributionClass.Minimum), CuentaSugerida: "3110"));
            filas.Add(new(R.AportesSociales, ClaseDeAporte: nameof(ContributionClass.Revalued), CuentaSugerida: "310505"));
            filas.Add(new(R.DevolucionPorPagar, Naturaleza: Aportes, Motivo: "MembershipRefund", CuentaSugerida: "246505"));
            filas.Add(new(R.DevolucionPorPagar, Naturaleza: Aportes, Motivo: "ContributionRefund", CuentaSugerida: "246505"));
            filas.Add(new(R.PerdidaAbsorbida, CuentaSugerida: "3510"));
            filas.Add(new(R.FondoDeRevalorizacion, CuentaSugerida: "3310", Exigida: false));
            filas.Add(new(R.FondoDeExcedentes, CuentaSugerida: "3505", Exigida: false));
            filas.Add(new(R.NoReclamadosPorPagar, Naturaleza: Aportes, CuentaSugerida: "246595", Exigida: false));
            if (clasesDeCaptacion.Count == 0) filas.Add(new(R.RecaudoPorAplicar, ClaseDeRecaudador: nameof(ClaseDeRecaudador.Treasury), Exigida: false));
        }
        return filas;
    }
}
