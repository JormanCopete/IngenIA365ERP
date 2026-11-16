using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Documents;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Domain.Entities.Core;

namespace IngenIA365ERP.Application.Tests.Accounting.Documents;

/// <summary>
/// 2026-11-16 (pedido del dueño): la ventana de comprobantes sabe antes de la primera línea cómo busca F2 y qué sucursal dejar puesta:
/// la de la oficina por defecto de quien digita o, sin ella, la principal; con el alcance restringido, sólo las suyas.
/// </summary>
public class AjustesDeDigitacionTests
{
    private static async Task<AjustesDeDigitacionDto> LeerAsync(ContabilidadTestData d)
    {
        var r = await new GetVoucherEntrySettingsQueryHandler(d.Db, d.Alcance).Handle(new GetVoucherEntrySettingsQuery(), CancellationToken.None);
        r.IsSuccess.Should().BeTrue();
        return r.Value;
    }

    [Fact]
    public async Task Sin_parametro_busca_en_el_campo_y_propone_la_sucursal_principal_sin_restringir()
    {
        var d = new ContabilidadTestData();

        var a = await LeerAsync(d);

        a.SearchMode.Should().Be(BusquedaEnComprobantes.EnElCampo);
        a.ProposedBranchPublicId.Should().Be(d.Principal.PublicId);
        a.ProposedBranchName.Should().Be("Principal");
        a.AllowedBranchPublicIds.Should().BeNull();
    }

    [Fact]
    public async Task Con_el_alcance_restringido_propone_su_sucursal_y_solo_deja_elegir_las_suyas()
    {
        var d = new ContabilidadTestData();
        d.RestringirA(d.Norte);

        var a = await LeerAsync(d);

        a.ProposedBranchPublicId.Should().Be(d.Norte.PublicId);
        a.AllowedBranchPublicIds.Should().Equal(d.Norte.PublicId);
    }

    [Theory]
    [InlineData("VentanaF2", "VentanaF2")]
    [InlineData("ventanaf2", "VentanaF2")]
    [InlineData("EnElCampo", "EnElCampo")]
    [InlineData("cualquier cosa", "EnElCampo")]
    public async Task El_modo_guardado_se_lee_y_lo_desconocido_vuelve_al_defecto(string guardado, string esperado)
    {
        var d = new ContabilidadTestData();
        d.Db.SystemSettings.Add(new SystemSetting { SettingKey = BusquedaEnComprobantes.Clave, SettingValue = guardado, ValueType = "String", CreatedBy = "test" });
        d.Db.SaveChanges();

        (await LeerAsync(d)).SearchMode.Should().Be(esperado);
    }
}
