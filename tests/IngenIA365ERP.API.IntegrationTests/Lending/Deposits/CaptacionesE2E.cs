using IngenIA365ERP.API.IntegrationTests.Identity;

namespace IngenIA365ERP.API.IntegrationTests.Lending.Deposits;

/// <summary>
/// Colección «Captaciones e2e» (feature 014, T003; research R30): un host propio, con sus contenedores, para las e2e de
/// captaciones y aportes, en paralelo con «Cartera e2e». Todo corre en cooperativas aisladas (<see cref="CaptacionesE2E"/>): la
/// activación de cada naturaleza es irreversible.
///
/// <para>
/// Decisión del dueño: las e2e contra base de datos se corren <b>una sola vez al final de todas las fases</b> (como las de K5 de la
/// 013); mientras tanto se escriben y compilan.
/// </para>
/// </summary>
[CollectionDefinition(Nombre)]
public sealed class CaptacionesCollection : ICollectionFixture<CentralIdentityApiFixture>
{
    public const string Nombre = "Captaciones e2e";
}

/// <summary>
/// Ayudantes de las e2e de captaciones (feature 014, T003): la cooperativa aislada con <b>créditos activados primero</b> —con carga
/// vacía, por las rutas reales de la 013 (<see cref="CarteraE2E.ActivarCarteraAsync"/>), que también registra el recaudador
/// <c>TST</c> y la tesorería <c>TES</c> de prueba—, y el punto donde la fase fundacional suma la activación de aportes y
/// captaciones sin carga (T055).
///
/// <para>
/// Hasta T055 la cooperativa queda con la Cartera activada y las dos naturalezas de captación sin activar: el contrato de cuentas
/// responde «no habilitado» (FR-093), que es lo que prueban las e2e de la fase fundacional. Desde T055,
/// <see cref="CooperativaConCaptacionesAsync"/> siembra la matriz mínima de los hechos de cada naturaleza, registra el recaudador
/// de cuentas (<c>ProveedorDeCuentas</c>) y la tesorería con <c>OrdenDeGiroEmitida</c> v2 adoptada, y activa aportes y
/// captaciones sin carga por <c>POST /api/lending/deposits/activation</c>.
/// </para>
/// </summary>
public static class CaptacionesE2E
{
    /// <summary>Lo que dejan los ayudantes: la cooperativa y su activación de créditos.</summary>
    public sealed record CooperativaDeCaptaciones(CarteraE2E.CooperativaAislada Cooperativa, CarteraE2E.CarteraActivadaE2E Creditos)
    {
        public string TokenAdmin => Cooperativa.TokenAdmin;
        public Guid TenantPublicId => Cooperativa.TenantPublicId;
    }

    /// <summary>
    /// Una cooperativa aislada por nombre (prefijo <c>cap_</c>, para no cruzarse con las de «Cartera e2e») con la Cartera de créditos
    /// activada con carga vacía. La segunda llamada con el mismo nombre devuelve la misma.
    /// </summary>
    public static async Task<CooperativaDeCaptaciones> CooperativaConCreditosAsync(CentralIdentityApiFixture fx, string nombre)
    {
        var coop = await CarteraE2E.CooperativaAisladaAsync(fx, $"cap_{nombre}");
        var creditos = await CarteraE2E.ActivarCarteraAsync(fx, coop);
        return new CooperativaDeCaptaciones(coop, creditos);
    }

    /// <summary>
    /// La cooperativa con créditos activados y, desde T055, aportes y captaciones activados sin carga. Hasta que T055 exista,
    /// devuelve la de <see cref="CooperativaConCreditosAsync"/>: las historias que la piden empiezan después de la fase fundacional.
    /// </summary>
    public static Task<CooperativaDeCaptaciones> CooperativaConCaptacionesAsync(CentralIdentityApiFixture fx, string nombre) =>
        CooperativaConCreditosAsync(fx, nombre);
}
