namespace IngenIA365ERP.Application.Common.Scope;

/// <summary>
/// Las oficinas, puntos, cajas y pagadurías sobre las que opera quien hace la petición en Recaudo (feature 015, T0058; US12; R-30; FR-011, FR-175;
/// data-model §1.11). <b>Falla cerrado</b>: sin filas no hay nada, salvo <c>Receipts.Scope.All</c> (o el maestro global, o el segundo plano), y lo que
/// queda fuera responde <c>Generic.NotFound</c> igual que lo inexistente (SC-018). Molde: <c>IAlcanceDeCartera</c>.
///
/// <list type="bullet">
/// <item><b>Oficinas</b>: las de Seguridad (<c>SEC_UserBranchAssignments</c> → <c>COR_Branches</c>, por <c>OficinasDelUsuario</c>); las piden lo que es
/// de la oficina entera (bóveda, cuadre diario, cierre del día de todos sus puntos).</item>
/// <item><b>Puntos</b>: <c>RCD_UserCollectionPointScopes</c>; un punto en alcance trae todas sus cajas (el supervisor de un punto ve las sesiones de su
/// punto, US12-2).</item>
/// <item><b>Cajas</b>: <c>RCD_UserCashBoxScopes</c> y las cajas de las que el usuario es cajero vigente (<c>RCD_CashBoxCashiers</c>): el cajero opera su
/// caja sin otra asignación.</item>
/// <item><b>Pagadurías</b>: <c>RCD_UserPaymasterScopes</c>. Los recibos de banco, las partidas y los saldos a favor de una cuenta propia toman el
/// alcance de esa cuenta en Tesorería (<see cref="IAlcanceDeTesoreria"/>, FR-175).</item>
/// </list>
/// La implementación de la petición es <c>AlcanceDeRecaudoDeLaPeticion</c> (API), que delega en <c>AlcanceDeRecaudoDelActor</c>, memorizado por
/// petición. (nuevo)
/// </summary>
public interface IAlcanceDeRecaudo
{
    /// <summary>El alcance de la petición (una sola lectura por petición).</summary>
    Task<AlcanceDeRecaudo> ObtenerAsync(CancellationToken ct = default);
}

/// <summary>
/// El alcance de un usuario en Recaudo: todo (<see cref="Total"/>) o las oficinas (<c>COR_Branches.Id</c>), puntos (<c>RCD_CollectionPoints.Id</c>), cajas
/// (<c>RCD_CashBoxes.Id</c>) y pagadurías (<c>COR_People.Id</c>) de sus filas. (nuevo)
/// </summary>
public sealed record AlcanceDeRecaudo(
    bool Total,
    IReadOnlySet<int> Oficinas,
    IReadOnlySet<int> Puntos,
    IReadOnlySet<int> Cajas,
    IReadOnlySet<int> Pagadurias)
{
    /// <summary>Nada: lo que ve un usuario sin filas ni alcance total.</summary>
    public static AlcanceDeRecaudo Vacio { get; } = new(false, new HashSet<int>(), new HashSet<int>(), new HashSet<int>(), new HashSet<int>());

    /// <summary>Todo: el segundo plano, el maestro global o <c>Receipts.Scope.All</c>.</summary>
    public static AlcanceDeRecaudo Todo { get; } = new(true, new HashSet<int>(), new HashSet<int>(), new HashSet<int>(), new HashSet<int>());

    public bool IncluyeOficina(int oficinaId) => Total || Oficinas.Contains(oficinaId);

    public bool IncluyePunto(int puntoId) => Total || Puntos.Contains(puntoId);

    /// <summary>La caja está en alcance si lo está ella o su punto.</summary>
    public bool IncluyeCaja(int cajaId, int puntoId) => Total || Cajas.Contains(cajaId) || Puntos.Contains(puntoId);

    public bool IncluyePagaduria(int personaId) => Total || Pagadurias.Contains(personaId);
}

/// <summary>La implementación que siempre falla cerrado (<see cref="AlcanceDeRecaudo.Vacio"/>): anfitriones sin petición y pruebas. (nuevo)</summary>
public sealed class AlcanceDeRecaudoCerrado : IAlcanceDeRecaudo
{
    public Task<AlcanceDeRecaudo> ObtenerAsync(CancellationToken ct = default) => Task.FromResult(AlcanceDeRecaudo.Vacio);
}
