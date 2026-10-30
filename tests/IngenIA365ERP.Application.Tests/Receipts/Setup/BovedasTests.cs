using FluentAssertions;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Vaults;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.Setup;

/// <summary>
/// Feature 015, T0068 (US1; FR-035, FR-180, FR-182; data-model §2.6): una bóveda por oficina, propia o la caja principal como bóveda (con su caja principal);
/// los custodios con vigencia, y con <c>Caja.DobleCustodia</c> (activo por defecto) al menos dos personas distintas.
/// </summary>
public class BovedasTests
{
    private readonly EscenarioDeRecaudo _e = new();

    [Fact]
    public async Task Una_boveda_por_oficina()
    {
        var crear = new CreateVaultCommandHandler(_e.Db, _e.Alcance, _e.Reloj);
        (await crear.Handle(new CreateVaultCommand(_e.OficinaA.PublicId, VaultMode.Vault), CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await crear.Handle(new CreateVaultCommand(_e.OficinaA.PublicId, VaultMode.Vault), CancellationToken.None)).Error.Code
            .Should().Be(ErroresDeRecaudo.CodigoBovedaRepetida);
    }

    [Fact]
    public async Task La_caja_principal_como_boveda_exige_una_caja_principal_de_la_oficina()
    {
        var punto = _e.Punto("NORTE");
        var comun = _e.Caja(punto, "C1");
        var principal = _e.Caja(punto, "C2");
        principal.IsMainCashBox = true;
        await _e.Db.SaveChangesAsync();
        var crear = new CreateVaultCommandHandler(_e.Db, _e.Alcance, _e.Reloj);

        (await crear.Handle(new CreateVaultCommand(_e.OficinaA.PublicId, VaultMode.MainCashBox), CancellationToken.None)).Error.Code
            .Should().Be(ErroresDeRecaudo.CodigoBovedaSinCajaPrincipal);
        (await crear.Handle(new CreateVaultCommand(_e.OficinaA.PublicId, VaultMode.MainCashBox, comun.PublicId), CancellationToken.None)).Error.Code
            .Should().Be(ErroresDeRecaudo.CodigoBovedaSinCajaPrincipal, "la caja no está marcada como principal");
        var ok = await crear.Handle(new CreateVaultCommand(_e.OficinaA.PublicId, VaultMode.MainCashBox, principal.PublicId), CancellationToken.None);
        ok.Value.MainCashBoxCode.Should().Be("C2");
    }

    [Fact]
    public async Task Con_doble_custodia_hacen_falta_dos_personas_distintas()
    {
        var boveda = (await new CreateVaultCommandHandler(_e.Db, _e.Alcance, _e.Reloj)
            .Handle(new CreateVaultCommand(_e.OficinaA.PublicId, VaultMode.Vault), CancellationToken.None)).Value;
        var custodios = new SetVaultCustodiansCommandHandler(_e.Db, _e.Alcance, _e.Parametros, _e.Segregacion, _e.Reloj);

        (await custodios.Handle(new SetVaultCustodiansCommand(boveda.PublicId, [_e.Cajera.PublicId, _e.CajeraBis.PublicId]), CancellationToken.None)).Error.Code
            .Should().Be(ErroresDeRecaudo.CodigoCustodiosInsuficientes, "son la misma persona");
        var dos = await custodios.Handle(new SetVaultCustodiansCommand(boveda.PublicId, [_e.Cajera.PublicId, _e.Supervisor.PublicId]), CancellationToken.None);
        dos.Value.Custodians.Should().HaveCount(2);

        _e.Parametro(ParametrosDeRecaudoYTesoreria.CajaDobleCustodia, "false");
        custodios = new SetVaultCustodiansCommandHandler(_e.Db, _e.Alcance, _e.Parametros, _e.Segregacion, _e.Reloj);
        var uno = await custodios.Handle(new SetVaultCustodiansCommand(boveda.PublicId, [_e.Supervisor.PublicId]), CancellationToken.None);
        uno.Value.Custodians.Should().ContainSingle(c => c.UserName == "sergio" && c.ValidTo == null);
    }
}
