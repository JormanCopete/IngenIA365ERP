namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// Los códigos del registro de participantes y las claves de negocio derivadas de un recaudador (feature 013, T0078; C35;
/// contracts/mensajes.md §16, interfaces.md §3.10). Viven en el mismo espacio que el registro para que el contrato de obligaciones
/// las use sin armarlas a mano: el reverso y la aplicación destino de una resolución usan la clave del original con
/// <see cref="SufijoReverso"/> y <see cref="SufijoDestino"/>; el recaudador de prueba usa <c>TST:{guid:N}</c>. El prefijo
/// <c>TEST:</c> se retiró. (nuevo)
/// </summary>
public static class ClavesDeRecaudador
{
    /// <summary>Contabilidad, destino <c>Accounting</c>.</summary>
    public const string Contabilidad = "CNT";

    /// <summary>Comercial, destino <c>Inventory</c> de las respuestas de Cartera.</summary>
    public const string Comercial = "INV";

    /// <summary>Nómina, recaudador del contrato.</summary>
    public const string Nomina = "NOM";

    /// <summary>Cartera: destino <c>Lending</c> (sembrado inhabilitado) y recaudador de la recogida de créditos propios, sin fila (T91).</summary>
    public const string Cartera = "CAR";

    /// <summary>Los dobles de prueba (<c>TesoreriaDePrueba</c>, <c>RecaudadorDePrueba</c>), que comparten el código.</summary>
    public const string Prueba = "TST";

    public const string SufijoReverso = ":reverso";

    public const string SufijoDestino = ":destino";

    /// <summary>La clave del reverso de lo aplicado con <paramref name="clave"/>: <c>{clave}:reverso</c>.</summary>
    public static string Reverso(string clave) => Derivada(clave, SufijoReverso);

    /// <summary>La clave de la aplicación destino de una resolución de saldo a favor: <c>{clave}:destino</c>.</summary>
    public static string Destino(string clave) => Derivada(clave, SufijoDestino);

    /// <summary>La clave del recaudador de prueba: <c>TST:{guid:N}</c>. La pantalla la propone y deja repetirla (idempotencia).</summary>
    public static string DePrueba(Guid guid) => $"{Prueba}:{guid:N}";

    /// <summary>Una clave de recaudo empieza por el código de su recaudador y dos puntos.</summary>
    public static bool EsDelRecaudador(string clave, string codigo) =>
        !string.IsNullOrWhiteSpace(clave) && clave.StartsWith(codigo + ":", StringComparison.Ordinal);

    private static string Derivada(string clave, string sufijo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clave);
        return clave + sufijo;
    }
}
