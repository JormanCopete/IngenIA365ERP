using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Payments;

/// <summary>
/// 2026-11-16 (pedido del dueño): un rubro de los créditos tal como lo nombra la empresa en el orden en que un pago los cubre
/// (<c>Pagos.PrelacionDeRubros</c>). No se guarda en ninguna tabla: viaja como texto en el parámetro y en la decisión del pago. Cada rubro es uno o
/// varios conceptos del libro (<see cref="LoanConcept"/>):
/// <list type="bullet">
/// <item><see cref="Seguro"/> y <see cref="Administracion"/>: <see cref="LoanConcept.Insurance"/>. La administración se cobra como un seguro de la
/// línea (el libro lleva <b>un</b> concepto de seguro por cuota, la suma de los seguros del plan), así que los dos van seguidos.</item>
/// <item><see cref="Interes"/>: la mora y luego la corriente, cada una con su suspenso si la línea lo lleva con su concepto (el orden de siempre).</item>
/// <item><see cref="GastosDeCobranza"/>: <see cref="LoanConcept.CollectionFee"/>, sólo si se informaron al deudor (FR-121); sin constancia van al
/// final de todo, como siempre.</item>
/// <item><see cref="OtrosCargos"/>: <see cref="LoanConcept.OtherCharge"/>.</item>
/// <item><see cref="CuotasExtras"/>: el capital de las cuotas que llevan prima (<c>CuotaDelPlan.Extraordinaria</c> distinta de cero); todo su capital,
/// porque el libro lleva un solo capital por cuota.</item>
/// <item><see cref="Capital"/>: el capital de las demás cuotas y el que no tiene cuota.</item>
/// </list>
/// No se guarda por número: los valores siguen al primero, en este orden (que es el de los rubros que la lista no nombra). (nuevo)
/// </summary>
public enum RubroDeCartera
{
    Seguro = 1,
    Administracion,
    Interes,
    GastosDeCobranza,
    OtrosCargos,
    CuotasExtras,
    Capital,
}

/// <summary>
/// 2026-11-16 (pedido del dueño): el <b>orden de los rubros</b> en que un pago cubre lo que debe un crédito, por empresa
/// (<c>Pagos.PrelacionDeRubros</c>): los nombres de <see cref="RubroDeCartera"/> separados por coma, sin espacios. De fábrica
/// <c>Seguro,Administracion,Interes,CuotasExtras,Capital</c>. Los rubros que la lista no nombra van <b>antes del capital</b> (antes del primero de
/// <see cref="RubroDeCartera.CuotasExtras"/> y <see cref="RubroDeCartera.Capital"/>), en el orden del enum, porque el capital normalmente queda de
/// último; si no nombra el capital, va al final, y si no nombra las cuotas extras, van justo antes del capital. El orden de las cuotas (la más antigua
/// primero o concepto primero) sigue saliendo de la línea; esto ordena los rubros. Lo aplica <see cref="Prelacion.ConRubros"/>. Inmutable; dos órdenes
/// con el mismo texto son iguales. (nuevo)
/// </summary>
public sealed class OrdenDeRubros : IEquatable<OrdenDeRubros>
{
    /// <summary>El orden sellado en una decisión no se puede leer.</summary>
    public const string CodigoOrdenInvalido = "Lending.Payment.AllocationRubricsInvalid";

    /// <summary>Los nombres admitidos, en el orden del enum.</summary>
    public static IReadOnlyList<string> Nombres { get; } = Enum.GetNames<RubroDeCartera>();

    /// <summary>El de fábrica (pedido del dueño): seguro, administración, interés, cuotas extras y capital.</summary>
    public static OrdenDeRubros DeFabrica { get; } = new(
    [
        RubroDeCartera.Seguro, RubroDeCartera.Administracion, RubroDeCartera.Interes, RubroDeCartera.CuotasExtras, RubroDeCartera.Capital,
    ]);

    /// <summary>El texto de fábrica, el defecto del parámetro.</summary>
    public static string Defecto => DeFabrica.Texto;

    /// <summary>La forma del texto: uno o más nombres admitidos separados por coma, sin espacios.</summary>
    public static string Patron { get; } = $"^({string.Join("|", Nombres)})(,({string.Join("|", Nombres)}))*$";

    private OrdenDeRubros(IReadOnlyList<RubroDeCartera> rubros)
    {
        Rubros = rubros;
        Texto = string.Join(",", rubros);
        Completo = Completar(rubros);
    }

    /// <summary>Los rubros tal como los nombra la empresa.</summary>
    public IReadOnlyList<RubroDeCartera> Rubros { get; }

    /// <summary>El texto canónico (el que se sella en la decisión del pago).</summary>
    public string Texto { get; }

    /// <summary>Los siete rubros en el orden en que se cubren: los nombrados y, donde dice la regla, los que faltan.</summary>
    public IReadOnlyList<RubroDeCartera> Completo { get; }

    /// <summary>Por qué el texto no sirve (vacío, un nombre que no es un rubro, uno repetido, seguro y administración separados); nulo si sirve.</summary>
    public static string? Problema(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return "Indique al menos un rubro.";
        var vistos = new List<RubroDeCartera>();
        foreach (var parte in texto.Split(','))
        {
            var nombre = parte.Trim();
            if (!Enum.TryParse<RubroDeCartera>(nombre, ignoreCase: false, out var rubro) || !Enum.IsDefined(rubro) || !Nombres.Contains(nombre))
                return $"«{nombre}» no es un rubro de los créditos. Admite: {string.Join(", ", Nombres)}.";
            if (vistos.Contains(rubro)) return $"«{nombre}» va una sola vez.";
            vistos.Add(rubro);
        }
        var seguro = vistos.IndexOf(RubroDeCartera.Seguro);
        var administracion = vistos.IndexOf(RubroDeCartera.Administracion);
        if (seguro >= 0 && administracion >= 0 && Math.Abs(seguro - administracion) != 1)
            return "Seguro y Administracion van seguidos: la administración se cobra como un seguro de la línea y el crédito lleva un solo concepto de seguro.";
        return null;
    }

    /// <summary>Lee el texto del parámetro; nulo si está vacío o no sirve (quien lee usa <see cref="DeFabrica"/>).</summary>
    public static OrdenDeRubros? Leer(string? texto) =>
        Problema(texto) is null ? new OrdenDeRubros(texto!.Split(',').Select(p => Enum.Parse<RubroDeCartera>(p.Trim())).ToList()) : null;

    /// <summary>
    /// Lee el orden sellado en la decisión de un pago: nulo si no lo trae (un pago anterior al parámetro, que se reparte con la prelación de la línea);
    /// si lo trae y no sirve, <see cref="ReglaDeCarteraException"/> con <see cref="CodigoOrdenInvalido"/> (el crédito falla, a propósito).
    /// </summary>
    public static OrdenDeRubros? DesdeSellado(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        return Leer(texto) ?? throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoOrdenInvalido,
            $"El orden de rubros sellado no se puede leer: {Problema(texto)}", "allocationRubrics", texto));
    }

    /// <summary>La lista completa: el capital al final si falta, las cuotas extras antes del capital y los demás antes del primero de los dos.</summary>
    private static IReadOnlyList<RubroDeCartera> Completar(IReadOnlyList<RubroDeCartera> rubros)
    {
        var lista = rubros.ToList();
        if (!lista.Contains(RubroDeCartera.Capital)) lista.Add(RubroDeCartera.Capital);
        if (!lista.Contains(RubroDeCartera.CuotasExtras)) lista.Insert(lista.IndexOf(RubroDeCartera.Capital), RubroDeCartera.CuotasExtras);
        // Seguro y administración son el mismo concepto: el que falta va junto al que está.
        if (lista.Contains(RubroDeCartera.Seguro) && !lista.Contains(RubroDeCartera.Administracion))
            lista.Insert(lista.IndexOf(RubroDeCartera.Seguro) + 1, RubroDeCartera.Administracion);
        else if (lista.Contains(RubroDeCartera.Administracion) && !lista.Contains(RubroDeCartera.Seguro))
            lista.Insert(lista.IndexOf(RubroDeCartera.Administracion), RubroDeCartera.Seguro);
        var faltan = Enum.GetValues<RubroDeCartera>().Where(r => !lista.Contains(r)).ToList();
        var antesDelCapital = lista.FindIndex(r => r is RubroDeCartera.CuotasExtras or RubroDeCartera.Capital);
        lista.InsertRange(antesDelCapital, faltan);
        return lista;
    }

    public bool Equals(OrdenDeRubros? other) => other is not null && string.Equals(Texto, other.Texto, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as OrdenDeRubros);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Texto);

    public override string ToString() => Texto;
}
