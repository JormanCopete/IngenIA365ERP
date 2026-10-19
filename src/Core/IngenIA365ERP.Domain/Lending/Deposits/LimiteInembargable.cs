using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Deposits;

/// <summary>
/// Cómo protege el límite inembargable los depósitos de la persona (feature 014, US11, T188; research §20, E155): sobre la suma de sus cuentas protegidas
/// (el límite de los depósitos de ahorro que fija la Superintendencia Financiera para el embargo judicial, CGP art. 594 num. 2) o sólo sobre la cuenta
/// protegida más antigua (el límite del cobro coactivo, ET art. 837-1, modificado por la Ley 1066 de 2006 art. 9). Lo fija cada cooperativa por
/// autoridad en <c>Embargos.AplicacionDelLimite</c>. (nuevo)
/// </summary>
public enum AplicacionDelLimiteInembargable
{
    /// <summary>El límite protege la suma de todas las cuentas protegidas de la persona.</summary>
    AllProtectedAccounts = 1,

    /// <summary>El límite protege sólo la cuenta protegida más antigua; las demás se embargan enteras.</summary>
    OldestAccount = 2,
}

/// <summary>
/// Una cuenta de la persona embargada tal como la ve el motor (T188): su saldo del titular, lo que ya retienen otros embargos anteriores de la misma
/// persona, si su clase la protege el límite inembargable y si se puede embargar (los aportes y las clases inembargables por estatuto o ley no). (nuevo)
/// </summary>
public sealed record CuentaAnteElEmbargo(
    int CuentaId,
    DepositClass? Clase,
    DateOnly AbiertaEl,
    decimal Saldo,
    decimal RetenidoPorOtrosEmbargos,
    bool ProtegidaPorElLimite,
    bool Embargable);

/// <summary>
/// Lo que se le pide al motor (T188): el tope que le queda al embargo (su valor menos lo ya consignado; nulo = sin tope), si es por alimentos, el límite
/// inembargable vigente en pesos (nulo = no hay límite: persona jurídica o autoridad sin límite), cómo se aplica y el orden de las clases de captación
/// (<c>Embargos.OrdenDeCuentas</c>; vacío = por antigüedad). (nuevo)
/// </summary>
public sealed record PedidoDeEmbargo(
    decimal? TopePendiente,
    bool PorAlimentos,
    decimal? Limite,
    AplicacionDelLimiteInembargable Aplicacion,
    IReadOnlyList<DepositClass> OrdenDeClases,
    IReadOnlyList<CuentaAnteElEmbargo> Cuentas);

/// <summary>Lo que el motor deja en una cuenta: su saldo, la parte protegida por el límite, la embargable libre y lo que este embargo retiene. (nuevo)</summary>
public sealed record RetencionDeCuenta(int CuentaId, int Orden, decimal Saldo, decimal Protegido, decimal Embargable, decimal Retenido);

/// <summary>
/// El resultado del motor (T188): el límite que rigió (0 por alimentos o sin límite), lo protegido, lo embargable de la persona, lo que retiene este embargo
/// y su reparto por cuenta en el orden aplicado, con la explicación en texto para la pantalla y la respuesta al juzgado. (nuevo)
/// </summary>
public sealed record EmbargoCalculado(
    decimal LimiteAplicado,
    decimal Protegido,
    decimal TotalEmbargable,
    decimal Retenido,
    IReadOnlyList<RetencionDeCuenta> Cuentas,
    IReadOnlyList<string> Explicacion);

/// <summary>
/// El límite inembargable y el reparto de un embargo entre las cuentas de la persona (feature 014, US11, T188; FR-055, D20, R23; research §20). Puro, sin
/// reloj ni base, sin un solo valor legal: el límite llega por parámetro (valor nacional <c>LIMITE_INEMBARGABLE</c> para el judicial, el <c>LIMITE</c>
/// <c>EMBARGO_COACTIVO_SMMLV</c> × SMMLV para el coactivo).
///
/// <list type="number">
/// <item>Sólo cuentan las cuentas embargables (los aportes nunca, Ley 79 de 1988 art. 49; las clases inembargables de la cooperativa tampoco).</item>
/// <item>Por alimentos (CGP art. 594 num. 2, excepción de los créditos alimentarios) o sin límite, nada se protege.</item>
/// <item>Con el límite sobre la suma, la parte protegida se asigna a las cuentas protegidas <b>de la última a la primera</b> en el orden del embargo: así lo
/// que se retiene sale primero de las cuentas que el orden pone delante, y lo protegido queda en cuentas protegidas. Con el límite sobre la cuenta más
/// antigua, sólo esa cuenta protegida (por fecha de apertura y luego Id) guarda hasta el límite.</item>
/// <item>Lo que otro embargo anterior de la misma persona ya retiene no se vuelve a retener: el límite es de la persona, no de cada embargo.</item>
/// <item>Se retiene en el orden (<see cref="PedidoDeEmbargo.OrdenDeClases"/>, luego la antigüedad, luego el Id) hasta el tope pendiente del embargo.</item>
/// </list>
/// (nuevo)
/// </summary>
public static class LimiteInembargable
{
    public static EmbargoCalculado Calcular(PedidoDeEmbargo pedido)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        if (pedido.TopePendiente is < 0m) throw new ArgumentOutOfRangeException(nameof(pedido), "El tope pendiente del embargo no es negativo.");
        if (pedido.Limite is < 0m) throw new ArgumentOutOfRangeException(nameof(pedido), "El límite inembargable no es negativo.");

        var explicacion = new List<string>();
        var ordenadas = Ordenar(pedido.Cuentas, pedido.OrdenDeClases);
        var embargables = ordenadas.Where(c => c.Embargable).ToList();
        foreach (var c in ordenadas.Where(c => !c.Embargable))
            explicacion.Add($"La cuenta {c.CuentaId} no se embarga ({(c.Clase is null ? "aportes" : $"clase {c.Clase} inembargable")}).");

        var limite = pedido.PorAlimentos || pedido.Limite is null ? 0m : pedido.Limite.Value;
        if (pedido.PorAlimentos) explicacion.Add("Embargo por alimentos: el límite inembargable no aplica.");
        else if (pedido.Limite is null) explicacion.Add("Sin límite inembargable para esta persona o autoridad.");

        // Lo protegido por cuenta.
        var protegido = embargables.ToDictionary(c => c.CuentaId, _ => 0m);
        if (limite > 0m)
        {
            var protegibles = embargables.Where(c => c.ProtegidaPorElLimite).ToList();
            if (pedido.Aplicacion == AplicacionDelLimiteInembargable.OldestAccount)
            {
                var mas = protegibles.OrderBy(c => c.AbiertaEl).ThenBy(c => c.CuentaId).FirstOrDefault();
                if (mas is not null) protegido[mas.CuentaId] = Math.Min(limite, Positivo(mas.Saldo));
                explicacion.Add(mas is null
                    ? $"Límite de {limite:N2} sobre la cuenta más antigua: la persona no tiene cuentas protegidas."
                    : $"Límite de {limite:N2} sobre la cuenta más antigua ({mas.CuentaId}): protege {protegido[mas.CuentaId]:N2}.");
            }
            else
            {
                var restante = limite;
                for (var i = protegibles.Count - 1; i >= 0 && restante > 0m; i--)
                {
                    var c = protegibles[i];
                    var p = Math.Min(restante, Positivo(c.Saldo));
                    protegido[c.CuentaId] = p;
                    restante -= p;
                }
                explicacion.Add($"Límite de {limite:N2} sobre la suma de {protegibles.Count} cuenta(s) protegida(s) "
                                + $"({protegibles.Sum(c => Positivo(c.Saldo)):N2}): protege {limite - restante:N2}.");
            }
        }

        // Lo que retiene este embargo, en el orden.
        var pendiente = pedido.TopePendiente;
        var cuentas = new List<RetencionDeCuenta>();
        var totalEmbargable = 0m;
        var orden = 0;
        foreach (var c in ordenadas)
        {
            orden++;
            if (!c.Embargable)
            {
                cuentas.Add(new RetencionDeCuenta(c.CuentaId, orden, Positivo(c.Saldo), 0m, 0m, 0m));
                continue;
            }
            var saldo = Positivo(c.Saldo);
            var embargable = Math.Max(0m, saldo - protegido[c.CuentaId] - Positivo(c.RetenidoPorOtrosEmbargos));
            totalEmbargable += embargable;
            var retenido = pendiente is { } p ? Math.Min(embargable, p) : embargable;
            if (pendiente is not null) pendiente -= retenido;
            cuentas.Add(new RetencionDeCuenta(c.CuentaId, orden, saldo, protegido[c.CuentaId], embargable, retenido));
        }

        var total = cuentas.Sum(c => c.Retenido);
        explicacion.Add(pedido.TopePendiente is { } tope
            ? $"Embargable libre {totalEmbargable:N2}; el embargo retiene {total:N2} de un tope pendiente de {tope:N2}."
            : $"Embargable libre {totalEmbargable:N2}; el embargo sin tope retiene {total:N2}.");
        return new EmbargoCalculado(limite, protegido.Values.Sum(), totalEmbargable, total, cuentas, explicacion);
    }

    /// <summary>El orden del embargo: primero las clases en el orden del parámetro (las que no nombra, después), luego la antigüedad y el Id.</summary>
    public static IReadOnlyList<CuentaAnteElEmbargo> Ordenar(IEnumerable<CuentaAnteElEmbargo> cuentas, IReadOnlyList<DepositClass> ordenDeClases)
    {
        ArgumentNullException.ThrowIfNull(cuentas);
        ordenDeClases ??= [];
        int Posicion(DepositClass? clase)
        {
            if (clase is null) return int.MaxValue;
            var i = ordenDeClases is List<DepositClass> l ? l.IndexOf(clase.Value) : ordenDeClases.ToList().IndexOf(clase.Value);
            return i < 0 ? ordenDeClases.Count : i;
        }
        return cuentas.OrderBy(c => Posicion(c.Clase)).ThenBy(c => c.AbiertaEl).ThenBy(c => c.CuentaId).ToList();
    }

    private static decimal Positivo(decimal x) => x < 0m ? 0m : x;
}
