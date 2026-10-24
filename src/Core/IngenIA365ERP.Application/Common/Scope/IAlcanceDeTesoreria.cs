namespace IngenIA365ERP.Application.Common.Scope;

/// <summary>
/// Las cuentas bancarias propias sobre las que opera quien hace la petición en Tesorería, y con ellas los canales sin caja de Recaudo (feature 015,
/// T0058; US12; R-30; FR-011, FR-106, FR-175; data-model §9.3). <b>Falla cerrado</b>: sin filas en <c>TES_UserBankAccountScopes</c> no hay cuentas; lo que
/// queda fuera responde <c>Generic.NotFound</c>. Total en segundo plano, para el maestro global y con <c>Treasury.Scope.All</c> (decisión del dueño del
/// 2026-10-23, DT §2.7; el administrador de la cooperativa lo trae). La implementación de la petición es
/// <c>AlcanceDeTesoreriaDeLaPeticion</c> (API). (nuevo)
/// </summary>
public interface IAlcanceDeTesoreria
{
    /// <summary>El alcance de la petición (una sola lectura por petición).</summary>
    Task<AlcanceDeTesoreria> ObtenerAsync(CancellationToken ct = default);
}

/// <summary>El alcance de un usuario en Tesorería: todo o las cuentas propias (<c>TES_BankAccounts.Id</c>) de sus filas. (nuevo)</summary>
public sealed record AlcanceDeTesoreria(bool Total, IReadOnlySet<int> Cuentas)
{
    public static AlcanceDeTesoreria Vacio { get; } = new(false, new HashSet<int>());

    public static AlcanceDeTesoreria Todo { get; } = new(true, new HashSet<int>());

    public bool IncluyeCuenta(int cuentaId) => Total || Cuentas.Contains(cuentaId);
}

/// <summary>La implementación que siempre falla cerrado: anfitriones sin petición y pruebas. (nuevo)</summary>
public sealed class AlcanceDeTesoreriaCerrado : IAlcanceDeTesoreria
{
    public Task<AlcanceDeTesoreria> ObtenerAsync(CancellationToken ct = default) => Task.FromResult(AlcanceDeTesoreria.Vacio);
}
