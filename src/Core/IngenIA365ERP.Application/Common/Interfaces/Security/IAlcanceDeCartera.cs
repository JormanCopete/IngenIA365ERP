namespace IngenIA365ERP.Application.Common.Interfaces.Security;

/// <summary>
/// Las oficinas y las líneas de crédito sobre las que opera quien hace la petición en Cartera (feature 013, T62, R131; FR-004, SC-015;
/// data-model §3.14). <b>Falla cerrado</b>: sin filas en <c>SEC_UserBranchAssignments</c> no hay oficinas, salvo
/// <c>Lending.Scope.AllOffices</c>; sin filas en <c>LND_UserCreditLineScopes</c> no hay líneas, salvo <c>Lending.Scope.AllLines</c>. En
/// segundo plano el alcance es total. Molde: <see cref="IAlcanceDeInventario"/>.
///
/// <para>
/// La implementación de la petición es <c>AlcanceDeCarteraDeLaPeticion</c> (API), que delega en <c>AlcanceDeCarteraDelActor</c>
/// (memorizado por petición); las oficinas salen por <see cref="IngenIA365ERP.Application.Common.Security.OficinasDelUsuario"/>, el mismo salto que usa el alcance
/// contable. Las consultas lo aplican con <c>FiltroDeAlcanceDeCartera</c> y los comandos con su <c>AsegurarAsync</c> (404 del recurso y
/// <c>Lending.Access.OutOfScope</c>). Los puertos para otros módulos no aplican este alcance.
/// </para>
/// </summary>
public interface IAlcanceDeCartera
{
    /// <summary>El alcance de la petición (una sola lectura por petición).</summary>
    Task<AlcanceDeCartera> ObtenerAsync(CancellationToken ct = default);
}

/// <summary>
/// El alcance de un usuario en Cartera: todas las oficinas o las de <see cref="Oficinas"/> (Ids de <c>COR_Branches</c>, la sucursal de
/// los créditos y de sus hechos contables) con la que se propone, y todas las líneas o las de <see cref="Lineas"/> (Ids de
/// <c>LND_CreditLines</c>).
/// </summary>
public sealed record AlcanceDeCartera(
    bool TodasLasOficinas,
    IReadOnlySet<int> Oficinas,
    int? OficinaPorDefecto,
    bool TodasLasLineas,
    IReadOnlySet<int> Lineas)
{
    /// <summary>Ninguna oficina ni línea: lo que ve un usuario sin asignaciones ni alcance total.</summary>
    public static AlcanceDeCartera Vacio { get; } = new(false, new HashSet<int>(), null, false, new HashSet<int>());

    /// <summary>Todas las oficinas y todas las líneas (segundo plano, maestro o los dos permisos de alcance total).</summary>
    public static AlcanceDeCartera Total { get; } = new(true, new HashSet<int>(), null, true, new HashSet<int>());

    public bool IncluyeOficina(int oficinaId) => TodasLasOficinas || Oficinas.Contains(oficinaId);

    public bool IncluyeLinea(int lineaId) => TodasLasLineas || Lineas.Contains(lineaId);
}

/// <summary>
/// La implementación que siempre falla cerrado: <see cref="AlcanceDeCartera.Vacio"/>. La API registra la de la petición; ésta queda
/// para anfitriones sin petición y para las pruebas. (nuevo)
/// </summary>
public sealed class AlcanceDeCarteraCerrado : IAlcanceDeCartera
{
    public Task<AlcanceDeCartera> ObtenerAsync(CancellationToken ct = default) => Task.FromResult(AlcanceDeCartera.Vacio);
}
