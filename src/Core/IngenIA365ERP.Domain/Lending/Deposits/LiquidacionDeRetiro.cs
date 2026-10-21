using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Deposits;

/// <summary>Una cuenta del asociado que entra a la liquidación del retiro, con su saldo por concepto a la fecha. (nuevo)</summary>
public sealed record CuentaEnElRetiro(int CuentaId, string Numero, LineNature Naturaleza, DepositClass? Clase,
    IReadOnlyDictionary<MemberAccountConcept, decimal> Saldos);

/// <summary>Un crédito vivo del asociado: lo que vale cancelarlo a la fecha, su altura de mora y su tasa (para la prelación D14). (nuevo)</summary>
public sealed record CreditoEnElRetiro(int CreditoId, string Numero, decimal ParaCancelar, int DiasDeMora, decimal TasaAnual);

/// <summary>El descuento por pérdidas que decidió el acta (FR-078, D15): el año del ejercicio y la proporción sobre los aportes. (nuevo)</summary>
public sealed record PerdidaEnElRetiro(int Año, decimal Proporcion);

/// <summary>Lo que sale de un concepto de una cuenta (en positivo). (nuevo)</summary>
public sealed record DebitoDeCuentaEnElRetiro(int CuentaId, MemberAccountConcept Concepto, decimal Monto);

/// <summary>El cruce contra un crédito: cuánto se aplica, si lo cancela, su lugar en la prelación y de qué cuentas sale. (nuevo)</summary>
public sealed record CruceDeRetiroCalculado(CreditoEnElRetiro Credito, decimal Monto, bool Total, int Prelacion, IReadOnlyList<DebitoDeCuentaEnElRetiro> Debitos);

/// <summary>Una línea de la liquidación (data-model §6.2): + a favor del asociado, − lo que se descuenta; la devolución, informativa en positivo. (nuevo)</summary>
public sealed record LineaDeLiquidacionDeRetiro(MembershipWithdrawalLineKind Clase, int? CuentaId, int? CreditoId, string? Concepto, decimal Monto, string? Nota);

/// <summary>La liquidación completa: las líneas, los cruces, el descuento por pérdidas, lo que se devuelve por cuenta y los totales. (nuevo)</summary>
public sealed record LiquidacionDeRetiroCalculada(
    IReadOnlyList<LineaDeLiquidacionDeRetiro> Lineas,
    IReadOnlyList<CruceDeRetiroCalculado> Cruces,
    IReadOnlyList<DebitoDeCuentaEnElRetiro> Perdida,
    IReadOnlyList<DebitoDeCuentaEnElRetiro> Devolucion,
    decimal Aportes,
    decimal Ahorros,
    decimal Cruzado,
    decimal Perdidas,
    decimal ADevolver)
{
    /// <summary>Lo que se devuelve de aportes (lo que baja el capital social por la devolución, FR-080).</summary>
    public decimal DevolucionDeAportes(IReadOnlyCollection<int> cuentasDeAportes) =>
        Devolucion.Where(d => cuentasDeAportes.Contains(d.CuentaId)).Sum(d => d.Monto);

    /// <summary>
    /// Decisión del dueño del 2026-10-18 (D45): lo que los créditos vivos siguen debiendo después del cruce (lo que vale cancelarlos menos lo cruzado).
    /// Mayor que cero, quien liquida elige qué pasa con ese saldo.
    /// </summary>
    public decimal SaldoPendienteDeCreditos { get; init; }
}

/// <summary>
/// El orden en que el cruce y la devolución toman los fondos (feature 014, decisión del dueño del 2026-10-18, D41: lo fijan los estatutos de cada
/// cooperativa, parámetro <c>Captaciones.OrdenDeFondosAlRetiro</c>). (nuevo)
/// </summary>
public enum OrdenDeFondosAlRetiro
{
    /// <summary>Primero los aportes y después los ahorros (el defecto).</summary>
    AportesPrimero = 1,

    /// <summary>Primero los ahorros y después los aportes.</summary>
    AhorrosPrimero = 2,
}

/// <summary>
/// La liquidación del retiro del asociado (feature 014, US10, T099; data-model §6.2; FR-076 a FR-078, D14, D15). <b>Pura</b>: recibe los saldos por
/// concepto de las cuentas que se liquidan, los créditos vivos y, si el acta lo dispuso, la proporción de pérdidas; devuelve las líneas y lo que sale de
/// cada cuenta. Reglas:
/// <list type="number">
/// <item>Los fondos son los saldos del titular: por defecto primero los aportes (en el orden de <see cref="DebitoDeAportes.Orden"/>) y después los
/// ahorros, cuenta por cuenta en el orden dado; los estatutos pueden invertirlo (<see cref="OrdenDeFondosAlRetiro"/>, D41); la memoria (retención,
/// GMF) no es del titular y no entra.</item>
/// <item>El descuento por pérdidas (FR-078) es la proporción sobre el total de aportes, al centavo, y sale de los aportes antes de cruzar.</item>
/// <item>El cruce sigue la prelación D14: primero los créditos en mora (más días primero), luego los vigentes de mayor tasa; a cada uno, lo que vale
/// cancelarlo o lo que quede.</item>
/// <item>Lo que sobra se devuelve, cuenta por cuenta y concepto por concepto.</item>
/// </list>
/// Σ de las líneas que no son de devolución = lo que se devuelve (research E65). (nuevo)
/// </summary>
public static class LiquidacionDeRetiro
{
    /// <summary>La prelación D14 del cruce: en mora primero (más días primero), luego mayor tasa, luego el número.</summary>
    public static IReadOnlyList<CreditoEnElRetiro> Prelacion(IEnumerable<CreditoEnElRetiro> creditos) =>
        creditos.OrderByDescending(c => c.DiasDeMora > 0)
            .ThenByDescending(c => c.DiasDeMora)
            .ThenByDescending(c => c.TasaAnual)
            .ThenBy(c => c.Numero, StringComparer.Ordinal)
            .ToList();

    public static LiquidacionDeRetiroCalculada Calcular(IReadOnlyList<CuentaEnElRetiro> cuentas, IReadOnlyList<CreditoEnElRetiro> creditos,
        PerdidaEnElRetiro? perdida, OrdenDeFondosAlRetiro ordenDeFondos = OrdenDeFondosAlRetiro.AportesPrimero)
    {
        ArgumentNullException.ThrowIfNull(cuentas);
        ArgumentNullException.ThrowIfNull(creditos);
        if (perdida is { } p && (p.Proporcion < 0m || p.Proporcion > 1m))
            throw new ArgumentOutOfRangeException(nameof(perdida), p.Proporcion, "La proporción de pérdidas va entre 0 y 1.");

        // Los fondos, en orden: aportes por clase y ahorros por concepto del titular; cuál va primero lo dicen los estatutos (D41).
        var deAportes = new List<(int Cuenta, MemberAccountConcept Concepto, decimal Disponible)>();
        foreach (var cuenta in cuentas.Where(c => c.Naturaleza == LineNature.Contribution))
            foreach (var concepto in DebitoDeAportes.Orden)
                if (cuenta.Saldos.GetValueOrDefault(concepto) is var v and > 0m) deAportes.Add((cuenta.CuentaId, concepto, v));
        var deAhorros = new List<(int Cuenta, MemberAccountConcept Concepto, decimal Disponible)>();
        foreach (var cuenta in cuentas.Where(c => c.Naturaleza != LineNature.Contribution))
            foreach (var (concepto, v) in cuenta.Saldos.Where(s => MemberAccountTransaction.ConceptosDelTitular.Contains(s.Key) && s.Value > 0m)
                         .OrderBy(s => (int)s.Key))
                deAhorros.Add((cuenta.CuentaId, concepto, v));
        var fondos = ordenDeFondos == OrdenDeFondosAlRetiro.AhorrosPrimero ? [.. deAhorros, .. deAportes] : deAportes.Concat(deAhorros).ToList();

        var lineas = new List<LineaDeLiquidacionDeRetiro>();
        foreach (var f in fondos)
        {
            var clase = cuentas.First(c => c.CuentaId == f.Cuenta).Naturaleza == LineNature.Contribution
                ? MembershipWithdrawalLineKind.ContributionBalance
                : MembershipWithdrawalLineKind.SavingsBalance;
            lineas.Add(new LineaDeLiquidacionDeRetiro(clase, f.Cuenta, null, f.Concepto.ToString(), f.Disponible, null));
        }
        var aportes = fondos.Where(f => MemberAccountTransaction.ConceptosDeAportes.Contains(f.Concepto)).Sum(f => f.Disponible);
        var ahorros = fondos.Sum(f => f.Disponible) - aportes;
        var restante = fondos.Select(f => f.Disponible).ToArray();

        // FR-078: el descuento por pérdidas sobre el total de aportes, al centavo, de los aportes.
        var deLaPerdida = new List<DebitoDeCuentaEnElRetiro>();
        if (perdida is { Proporcion: > 0m } pp && aportes > 0m)
        {
            var aDescontar = decimal.Round(aportes * pp.Proporcion, 2, MidpointRounding.AwayFromZero);
            deLaPerdida.AddRange(Tomar(fondos, restante, aDescontar, soloAportes: true));
            lineas.AddRange(deLaPerdida.Select(d => new LineaDeLiquidacionDeRetiro(MembershipWithdrawalLineKind.LossDeduction, d.CuentaId, null,
                d.Concepto.ToString(), -d.Monto, $"Pérdidas del ejercicio {pp.Año}")));
        }

        // D14: los cruces en orden de prelación.
        var cruces = new List<CruceDeRetiroCalculado>();
        var orden = 0;
        foreach (var credito in Prelacion(creditos.Where(c => c.ParaCancelar > 0m)))
        {
            var disponible = restante.Sum();
            if (disponible <= 0m) break;
            var monto = Math.Min(disponible, credito.ParaCancelar);
            var debitos = Tomar(fondos, restante, monto, soloAportes: false);
            orden++;
            cruces.Add(new CruceDeRetiroCalculado(credito, monto, monto == credito.ParaCancelar, orden, debitos));
            lineas.AddRange(debitos.Select(d => new LineaDeLiquidacionDeRetiro(MembershipWithdrawalLineKind.LoanOffset, d.CuentaId, credito.CreditoId,
                d.Concepto.ToString(), -d.Monto, $"Crédito {credito.Numero}")));
        }

        // Lo que sobra se devuelve (informativo, en positivo).
        var devolucion = new List<DebitoDeCuentaEnElRetiro>();
        for (var i = 0; i < fondos.Count; i++)
            if (restante[i] > 0m) devolucion.Add(new DebitoDeCuentaEnElRetiro(fondos[i].Cuenta, fondos[i].Concepto, restante[i]));
        lineas.AddRange(devolucion.Select(d => new LineaDeLiquidacionDeRetiro(MembershipWithdrawalLineKind.Refund, d.CuentaId, null, d.Concepto.ToString(),
            d.Monto, null)));

        var cruzado = cruces.Sum(c => c.Monto);
        var perdidas = deLaPerdida.Sum(d => d.Monto);
        return new LiquidacionDeRetiroCalculada(lineas, cruces, deLaPerdida, devolucion, aportes, ahorros, cruzado, perdidas, devolucion.Sum(d => d.Monto))
        {
            SaldoPendienteDeCreditos = Math.Max(0m, creditos.Where(c => c.ParaCancelar > 0m).Sum(c => c.ParaCancelar) - cruzado),
        };
    }

    /// <summary>Toma <paramref name="monto"/> de los fondos en su orden (sólo aportes si se pide) y descuenta lo tomado de <paramref name="restante"/>.</summary>
    private static List<DebitoDeCuentaEnElRetiro> Tomar(List<(int Cuenta, MemberAccountConcept Concepto, decimal Disponible)> fondos, decimal[] restante,
        decimal monto, bool soloAportes)
    {
        var tomados = new List<DebitoDeCuentaEnElRetiro>();
        var falta = monto;
        for (var i = 0; i < fondos.Count && falta > 0m; i++)
        {
            if (soloAportes && !MemberAccountTransaction.ConceptosDeAportes.Contains(fondos[i].Concepto)) continue;
            var toma = Math.Min(falta, restante[i]);
            if (toma <= 0m) continue;
            restante[i] -= toma;
            falta -= toma;
            tomados.Add(new DebitoDeCuentaEnElRetiro(fondos[i].Cuenta, fondos[i].Concepto, toma));
        }
        return tomados;
    }
}
