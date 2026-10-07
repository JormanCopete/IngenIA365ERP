namespace IngenIA365ERP.Domain.Common;

/// <summary>
/// Lo mínimo que la regla bitemporal necesita de una versión con vigencia (feature 013, T38, R48). Lo implementan las
/// filas de valores nacionales y documentos normativos (<c>ADM_</c> y su copia <c>COR_</c>); cada una dice cuál de sus
/// columnas es <see cref="ConocidaEn"/> (en la central, <c>RegisteredAt</c>; en la cooperativa, <c>KnownAt</c>).
/// </summary>
public interface IVersionBitemporal
{
    /// <summary>Desde cuándo rige (D).</summary>
    DateOnly ValidFrom { get; }

    /// <summary>Hasta cuándo rige, sólo si la norma lo trae (usura del período, festivo). Nulo: hasta el sucesor.</summary>
    DateOnly? ValidTo { get; }

    /// <summary>Numeración común creciente: desempata dos versiones con el mismo <see cref="ValidFrom"/>.</summary>
    long Sequence { get; }

    /// <summary>Instante UTC en que la versión se conoció (R).</summary>
    DateTime ConocidaEn { get; }

    Guid PublicId { get; }

    /// <summary>La versión que esta corrección reemplaza; nulo si no es corrección.</summary>
    Guid? SupersedesPublicId { get; }
}

/// <summary>
/// Lo que la regla de las versiones propias de Cartera necesita de una versión (feature 013, T38, R3, T0203; data-model §0.6): sin
/// <c>ValidTo</c> guardado, con un ordinal por dueño y el instante en que se registró. (nuevo)
/// </summary>
public interface IVersionDeCartera
{
    /// <summary>Desde cuándo rige (D).</summary>
    DateOnly ValidFrom { get; }

    /// <summary>Ordinal por dueño (1..n): desempata dos versiones con el mismo <see cref="ValidFrom"/>.</summary>
    int VersionNumber { get; }

    /// <summary>Instante UTC en que se registró (R).</summary>
    DateTime RegisteredAt { get; }
}

/// <summary>
/// Regla única de lectura de lo que rige a la fecha D con la información conocida hasta R (feature 013, T38, R48;
/// data-model parte 3 §3). Pura: sin IO, sin reloj, sin EF.
///
/// <list type="number">
/// <item>Entran sólo las versiones conocidas en R (<c>ConocidaEn ≤ R</c>).</item>
/// <item>Sale toda versión que una corrección conocida en R reemplazó (<c>SupersedesPublicId</c>), aunque la corrección
/// cambie las fechas: la corrección de una corrección reemplaza a la corrección.</item>
/// <item>Cada versión que queda cubre desde su <c>ValidFrom</c> hasta su <c>ValidTo</c> explícito si la norma lo trae;
/// si no, hasta la víspera del <c>ValidFrom</c> siguiente entre las que quedan (el <c>ValidTo</c> implícito sale del
/// sucesor conocido en R; ninguna fila se cierra reescribiéndola).</item>
/// <item>Entre las que cubren D gana el <c>ValidFrom</c> más reciente y, si empatan, la <c>Sequence</c> mayor.</item>
/// </list>
/// Sin versión que cubra D devuelve nulo: quien lee decide si es un faltante (<c>NationalValues.Missing</c>).
/// Quien llama pasa las versiones de una sola serie (mismo código y clave, o misma clase de documento).
/// </summary>
public static class VigenciaBitemporal
{
    public static T? Vigente<T>(IEnumerable<T> versiones, DateOnly D, DateTime R) where T : class, IVersionBitemporal
    {
        ArgumentNullException.ThrowIfNull(versiones);

        var conocidas = versiones.Where(v => v.ConocidaEn <= R).ToList();
        if (conocidas.Count == 0) return null;

        var reemplazadas = conocidas
            .Where(v => v.SupersedesPublicId is not null)
            .Select(v => v.SupersedesPublicId!.Value)
            .ToHashSet();

        var vigentes = conocidas.Where(v => !reemplazadas.Contains(v.PublicId)).ToList();

        return vigentes
            .Where(v => v.ValidFrom <= D && D <= (v.ValidTo ?? FinImplicito(v, vigentes)))
            .OrderByDescending(v => v.ValidFrom)
            .ThenByDescending(v => v.Sequence)
            .FirstOrDefault();
    }

    /// <summary>
    /// La regla de las <b>versiones propias de Cartera</b> (feature 013, T38, R3, T0203; data-model §0.6): líneas y sus hijas,
    /// equivalencias, matrices de atribución, reglas de órganos, cupos, preferencias, saldos de aportes, condiciones y planes. No
    /// guardan <c>ValidTo</c> ni se corrigen con <c>SupersedesPublicId</c>: la vigencia termina la víspera del <c>ValidFrom</c> de la
    /// versión siguiente conocida en R, y un fin de vigencia es una versión que lo dice.
    /// <list type="number">
    /// <item>Entran las versiones con <c>RegisteredAt ≤ R</c> y <c>ValidFrom ≤ D</c>.</item>
    /// <item>Gana el <c>ValidFrom</c> más reciente y, si empatan, el <c>VersionNumber</c> mayor.</item>
    /// </list>
    /// Sin versión que cumpla devuelve nulo. Lleva otro nombre que <see cref="Vigente{T}"/> porque C# no distingue dos métodos por
    /// la restricción del genérico; la regla de los valores nacionales queda intacta. (nuevo)
    /// </summary>
    public static T? VigenteDeCartera<T>(IEnumerable<T> versiones, DateOnly D, DateTime R) where T : class, IVersionDeCartera
    {
        ArgumentNullException.ThrowIfNull(versiones);

        return versiones
            .Where(v => v.RegisteredAt <= R && v.ValidFrom <= D)
            .OrderByDescending(v => v.ValidFrom)
            .ThenByDescending(v => v.VersionNumber)
            .FirstOrDefault();
    }

    /// <summary>La víspera del <c>ValidFrom</c> siguiente entre las versiones vigentes en R; sin sucesor, abierta.</summary>
    private static DateOnly FinImplicito<T>(T version, IReadOnlyList<T> vigentes) where T : IVersionBitemporal
    {
        var siguiente = vigentes
            .Where(v => v.ValidFrom > version.ValidFrom)
            .Select(v => (DateOnly?)v.ValidFrom)
            .Min();
        return siguiente?.AddDays(-1) ?? DateOnly.MaxValue;
    }
}
