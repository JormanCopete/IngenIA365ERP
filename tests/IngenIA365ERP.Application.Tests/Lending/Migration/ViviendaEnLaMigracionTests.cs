using FluentAssertions;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Application.Tests.Lending.Configuration;
using P = IngenIA365ERP.Application.Lending.Imports.DefinicionesDeMigracion.Partes;
using C = IngenIA365ERP.Application.Lending.Imports.DefinicionesDeMigracion.Columnas;

namespace IngenIA365ERP.Application.Tests.Lending.Migration;

/// <summary>
/// Feature 013, K6b, T1045 (FR-024, FR-141, D18): con K6b vigente un crédito de vivienda entra a la carga de migración, el cuadre lo pide a
/// Contabilidad por su modalidad (concepto, modalidad, categoría y sucursal, como los demás) y el acta de diferencias ya no declara la vivienda
/// fuera. Antes de K6b lo fijaban la guarda <c>Lending.Migration.HousingNotAvailable</c> y el salto en el cuadre (ver ReglasDeViviendaTests). (nuevo)
/// </summary>
public sealed class ViviendaEnLaMigracionTests : IAsyncLifetime
{
    private MigracionDePrueba _m = null!;

    public async Task InitializeAsync()
    {
        _m = await MigracionDePrueba.CrearAsync();
        var desde = new DateOnly(2026, 11, 1);
        _m.G.HoyLocal = desde;
        await _m.L.ViviendaAsync(desde: desde);
        await _m.L.LineaAsync("VIVLIB", LineasDePrueba.Vivienda(desde: desde));
        _m.G.HoyLocal = MigracionDePrueba.Hoy;
    }

    public Task DisposeAsync()
    {
        _m.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Con_K6b_el_credito_de_vivienda_se_migra_entra_al_cuadre_y_el_acta_no_lo_declara_fuera()
    {
        await _m.CargarAsync(P.Loans, MigracionDePrueba.Credito("CR-V", linea: "VIVLIB", tasa: "11"));
        await _m.CargarAsync(P.Installments, MigracionDePrueba.Cuota("CR-V", 1, "2026-12-15", "600000", "10000"),
            MigracionDePrueba.Cuota("CR-V", 2, "2027-01-15", "400000"));
        await _m.CargarAsync(P.Balances, MigracionDePrueba.Saldo("CR-V", "Principal", "1000000"), MigracionDePrueba.Saldo("CR-V", "CurrentInterest", "10000"));
        await _m.CargarAsync(P.RiskData, MigracionDePrueba.Riesgo("CR-V"));

        var r = await _m.AplicarAsync();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        _m.Pedidas.Should().ContainSingle().Which.Should().Contain(c => c.Role == "Capital" && c.ModalityCode == "VIVIENDA" && c.RiskCategory == "A",
            "desde K6b la vivienda entra al cuadre por su modalidad (FR-141)");

        var acta = await new AsuntoDeDiferenciasDeActivacion(_m.Db).DescribirAsync(_m.Carga.PublicId, MigracionDePrueba.Hoy, default);
        acta.IsSuccess.Should().BeTrue(acta.IsFailure ? acta.Error.Message : string.Empty);
        acta.Value.Resumen.Should().NotContain("vivienda").And.Contain("el plan migrado se conserva");
    }
}
