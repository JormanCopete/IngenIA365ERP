namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// Los contratos que un participante del registro puede adoptar y las versiones que Cartera acepta de cada uno (feature 013, T16;
/// data-model parte 3 §4.6; contracts/mensajes.md §17): el contrato de obligaciones (<c>ProveedorDeObligaciones</c>, que adoptan los
/// recaudadores) y los tipos de mensaje del catálogo agregado (que adoptan los destinos). Una versión que no esté aquí responde
/// <c>Integration.Participant.VersionNotSupported</c>. Feature 014 (T042): suma el contrato de cuentas (<c>ProveedorDeCuentas</c>). (nuevo)
/// </summary>
public static class ContratosDeIntegracion
{
    /// <summary>El contrato de obligaciones que llaman los recaudadores (interfaces.md §3).</summary>
    public const string ProveedorDeObligaciones = "ProveedorDeObligaciones";

    /// <summary>
    /// Las versiones del contrato de obligaciones que este despliegue implementa (<c>IContratoDeObligaciones.VersionDelContrato</c>,
    /// que nace en la fase 3 con el valor 1).
    /// </summary>
    /// <remarks>Feature 015, R1 (E-01 a; contratos-v2.md §1): la 2 convive con la 1.</remarks>
    public static readonly IReadOnlyList<int> VersionesDelContratoDeObligaciones = [1, 2];

    /// <summary>
    /// Feature 014 (T042; research R09; contrato-de-cuentas §3.3): el contrato de cuentas que llaman Recaudo, Tesorería y Nómina. La adopción de
    /// <see cref="ProveedorDeObligaciones"/> no habilita cuentas ni al revés.
    /// </summary>
    public const string ProveedorDeCuentas = "ProveedorDeCuentas";

    /// <summary>
    /// Las versiones del contrato de cuentas que este despliegue implementa. Feature 015, R1 (E-02 a; contratos-v2.md §3.1): la 2 convive con la 1.
    /// </summary>
    public static readonly IReadOnlyList<int> VersionesDelContratoDeCuentas = [1, 2];

    /// <summary>
    /// La primera versión de cada contrato que admite la partida bancaria citada (feature 015, R1; contratos-v2.md §1.2, §3.1). (nuevo)
    /// </summary>
    public const int VersionConPartidaBancaria = 2;

    /// <summary>Las versiones de <paramref name="contrato"/> que este despliegue acepta; vacío si no lo conoce.</summary>
    public static IReadOnlyList<int> VersionesSoportadas(string contrato) => contrato switch
    {
        ProveedorDeObligaciones => VersionesDelContratoDeObligaciones,
        ProveedorDeCuentas => VersionesDelContratoDeCuentas,
        _ => CatalogoDeMensajes.Todos.Where(t => t.Type == contrato).Select(t => t.Version).Order().ToList(),
    };
}
