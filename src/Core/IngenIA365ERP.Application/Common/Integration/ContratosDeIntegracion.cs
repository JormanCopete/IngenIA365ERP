namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// Los contratos que un participante del registro puede adoptar y las versiones que Cartera acepta de cada uno (feature 013, T16;
/// data-model parte 3 §4.6; contracts/mensajes.md §17): el contrato de obligaciones (<c>ProveedorDeObligaciones</c>, que adoptan los
/// recaudadores) y los tipos de mensaje del catálogo agregado (que adoptan los destinos). Una versión que no esté aquí responde
/// <c>Integration.Participant.VersionNotSupported</c>. (nuevo)
/// </summary>
public static class ContratosDeIntegracion
{
    /// <summary>El contrato de obligaciones que llaman los recaudadores (interfaces.md §3).</summary>
    public const string ProveedorDeObligaciones = "ProveedorDeObligaciones";

    /// <summary>
    /// Las versiones del contrato de obligaciones que este despliegue implementa (<c>IContratoDeObligaciones.VersionDelContrato</c>,
    /// que nace en la fase 3 con el valor 1).
    /// </summary>
    public static readonly IReadOnlyList<int> VersionesDelContratoDeObligaciones = [1];

    /// <summary>Las versiones de <paramref name="contrato"/> que este despliegue acepta; vacío si no lo conoce.</summary>
    public static IReadOnlyList<int> VersionesSoportadas(string contrato) =>
        contrato == ProveedorDeObligaciones
            ? VersionesDelContratoDeObligaciones
            : CatalogoDeMensajes.Todos.Where(t => t.Type == contrato).Select(t => t.Version).Order().ToList();
}
