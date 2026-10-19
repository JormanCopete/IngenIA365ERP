using System.Text.Json;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Domain.Entities.Accounting.Lending;
using IngenIA365ERP.Domain.Enums.Lending;
using R = IngenIA365ERP.Application.Accounting.Lending.Reglas.RolesDeCuentaDeCartera;

namespace IngenIA365ERP.Application.Accounting.Lending.Plantilla;

/// <summary>
/// Una fila sugerida de la matriz para captaciones o aportes (feature 014, US8, T144; contabilidad.md §4.2 de la 014): el rol con los valores que exige, la
/// cuenta CUIF sugerida —texto <b>informativo</b>, «sugerencia, pendiente de la contadora»: nadie la aplica— y si la completitud la exige
/// (<see cref="Exigida"/>: lo que las entregas K7 y K8 emiten desde el primer día; lo de la asamblea, los no reclamados, los embargos y el puente de Tesorería
/// sólo se sugiere). (nuevo)
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
/// combinación hecho × rol × valores exigidos de las naturalezas y clases que tienen líneas, con la cuenta del CUIF como <b>sugerencia</b>. Las cuentas
/// sugeridas son dato embebido (<c>sugerencias-cuif-captaciones.json</c>), no código: la Contabilidad no tiene cuentas en código
/// (<c>LaContabilidadNoTieneCuentasEnCodigo</c>). Ninguna regla se crea sola (P1, C70): la plantilla las pone sin cuenta y la importación las ignora mientras
/// sigan sin ella; la completitud dice cuáles exigidas faltan. El gasto por intereses se sugiere con el hecho en <c>*</c> porque el ajuste de la causación usa
/// el mismo rol. (nuevo)
/// </summary>
public static class SugerenciasDeCaptaciones
{
    private const string Deposito = nameof(LineNature.Deposit);
    private const string Aportes = nameof(LineNature.Contribution);

    private sealed record Sugerencia(string Rol, string? Clase, string? Plazo, string? ClaseDeAporte, string? Naturaleza, string? Motivo, string Cuenta);

    private sealed record Archivo(IReadOnlyList<Sugerencia> Sugerencias);

    private static readonly Lazy<IReadOnlyList<Sugerencia>> Cuif = new(() =>
    {
        var ensamblado = typeof(SugerenciasDeCaptaciones).Assembly;
        var nombre = ensamblado.GetManifestResourceNames().Single(n => n.EndsWith("sugerencias-cuif-captaciones.json", StringComparison.Ordinal));
        using var flujo = ensamblado.GetManifestResourceStream(nombre)!;
        return JsonSerializer.Deserialize<Archivo>(flujo, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Sugerencias;
    });

    /// <summary>La cuenta CUIF sugerida de una fila (la primera del archivo que casa; un campo vacío casa con cualquiera); nula si no hay sugerencia.</summary>
    public static string? CuentaSugerida(FilaSugeridaDeCaptacion f) => Cuif.Value.FirstOrDefault(s => s.Rol == f.Rol
        && Casa(s.Clase, f.Clase) && Casa(s.Plazo, f.Plazo) && Casa(s.ClaseDeAporte, f.ClaseDeAporte) && Casa(s.Naturaleza, f.Naturaleza ?? f.NaturalezaDeLaFila)
        && Casa(s.Motivo, f.Motivo))?.Cuenta;

    private static bool Casa(string? deLaSugerencia, string? deLaFila) => deLaSugerencia is null || string.Equals(deLaSugerencia, deLaFila, StringComparison.Ordinal);

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
            switch (clase)
            {
                case nameof(DepositClass.Demand):
                    foreach (var marca in R.MarcasDeCuenta) filas.Add(new(R.CapitalDeCaptacion, Clase: clase, Marca: marca));
                    filas.Add(new(R.CapitalDeCaptacionInactiva, Clase: clase));
                    break;
                case nameof(DepositClass.TermDeposit):
                    foreach (var plazo in R.PlazosDeEmision)
                        filas.Add(new(R.CapitalDeCaptacion, Clase: clase, Plazo: plazo, Marca: nameof(AccountFlag.Standard)));
                    break;
                case nameof(DepositClass.Contractual):
                    foreach (var plazo in R.PlazosDeEmision)
                    {
                        filas.Add(new(R.CapitalDeCaptacion, Clase: clase, Plazo: plazo, Marca: nameof(AccountFlag.Standard)));
                        filas.Add(new(R.CapitalDeCaptacionInactiva, Clase: clase, Plazo: plazo));
                    }
                    filas.Add(new(R.GastoPorBonificacion, Clase: clase));
                    break;
                case nameof(DepositClass.Permanent):
                    filas.Add(new(R.CapitalDeCaptacion, Clase: clase, Marca: nameof(AccountFlag.Standard)));
                    break;
            }
            filas.Add(new(R.InteresPorPagar, Clase: clase));
            filas.Add(new(R.GastoPorIntereses, Clase: clase));
            filas.Add(new(R.RetencionPorPagar, Clase: clase));
            filas.Add(new(R.GmfPorPagar, Clase: clase));
            if (clase is nameof(DepositClass.TermDeposit) or nameof(DepositClass.Contractual))
                filas.Add(new(R.IngresoPorPenalidad, Clase: clase));
        }

        if (clasesDeCaptacion.Count > 0)
        {
            filas.Add(new(R.DevolucionPorPagar, Naturaleza: Deposito, Motivo: "DepositWithdrawal"));
            filas.Add(new(R.DevolucionPorPagar, Naturaleza: Deposito, Motivo: "MembershipRefund"));
            if (clasesDeCaptacion.Contains(nameof(DepositClass.TermDeposit)))
            {
                filas.Add(new(R.DevolucionPorPagar, Naturaleza: Deposito, Motivo: "TermDepositInterest"));
                filas.Add(new(R.DevolucionPorPagar, Naturaleza: Deposito, Motivo: "TermDepositRedemption"));
            }
            foreach (var cargo in R.MotivosDelCargo) filas.Add(new(R.IngresoPorCargos, Motivo: cargo));
            filas.Add(new(R.CargoDeCuentaPorCobrar));
            filas.Add(new(R.NoReclamadosPorPagar, Naturaleza: Deposito, Exigida: false));
            filas.Add(new(R.EmbargosPorConsignar, Exigida: false));
            filas.Add(new(R.RecaudoPorAplicar, ClaseDeRecaudador: nameof(ClaseDeRecaudador.Treasury), Exigida: false));
        }

        if (conAportes)
        {
            foreach (var claseDeAporte in new[] { ContributionClass.Ordinary, ContributionClass.Extraordinary, ContributionClass.Minimum, ContributionClass.Revalued })
                filas.Add(new(R.AportesSociales, ClaseDeAporte: claseDeAporte.ToString()));
            filas.Add(new(R.DevolucionPorPagar, Naturaleza: Aportes, Motivo: "MembershipRefund"));
            filas.Add(new(R.DevolucionPorPagar, Naturaleza: Aportes, Motivo: "ContributionRefund"));
            filas.Add(new(R.PerdidaAbsorbida));
            filas.Add(new(R.FondoDeRevalorizacion, Exigida: false));
            filas.Add(new(R.FondoDeExcedentes, Exigida: false));
            filas.Add(new(R.NoReclamadosPorPagar, Naturaleza: Aportes, Exigida: false));
            if (clasesDeCaptacion.Count == 0) filas.Add(new(R.RecaudoPorAplicar, ClaseDeRecaudador: nameof(ClaseDeRecaudador.Treasury), Exigida: false));
        }
        return filas.Select(f => f with { CuentaSugerida = CuentaSugerida(f) }).ToList();
    }
}
