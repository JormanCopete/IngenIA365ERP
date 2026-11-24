using IngenIA365ERP.Application.Identity.Profile.Preferencias;
using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Admin;

namespace IngenIA365ERP.Application.Tests.Lending.PersonStatement;

/// <summary>
/// 2026-11-16 (pedido del dueño): el tamaño del texto y los centavos del estado de cuenta se guardan en las preferencias del usuario con lista
/// cerrada; y los documentos firmados del crédito rotulan como libranza los soportes que lo dicen.
/// </summary>
public class PreferenciasDelEstadoDeCuentaTests
{
    [Theory]
    [InlineData("pequeno", true)]
    [InlineData("normal", true)]
    [InlineData("grande", true)]
    [InlineData("enorme", false)]
    [InlineData("<script>", false)]
    public void El_tamano_del_texto_es_de_lista_cerrada(string valor, bool valida)
    {
        var r = new SaveMyPreferencesCommandValidator().Validate(
            new SaveMyPreferencesCommand(new Dictionary<string, string?> { [UserSettingKeys.TamanoTextoEstadoDeCuenta] = valor }));
        Assert.Equal(valida, r.IsValid);
    }

    [Theory]
    [InlineData("si", true)]
    [InlineData("no", true)]
    [InlineData("2", false)]
    public void Los_centavos_son_si_o_no(string valor, bool valida)
    {
        var r = new SaveMyPreferencesCommandValidator().Validate(
            new SaveMyPreferencesCommand(new Dictionary<string, string?> { [UserSettingKeys.DecimalesEstadoDeCuenta] = valor }));
        Assert.Equal(valida, r.IsValid);
    }

    [Fact]
    public void Las_dos_claves_son_de_la_cooperativa()
    {
        Assert.Contains(UserSettingKeys.TamanoTextoEstadoDeCuenta, UserSettingKeys.PorTenant);
        Assert.Contains(UserSettingKeys.DecimalesEstadoDeCuenta, UserSettingKeys.PorTenant);
        Assert.Contains(UserSettingKeys.ImpresionReciboCaja, UserSettingKeys.PorTenant);
    }

    /// <summary>2026-11-23: la impresión del recibo de la caja es de lista cerrada (preguntar, tirilla o carta).</summary>
    [Theory]
    [InlineData("preguntar", true)]
    [InlineData("tirilla", true)]
    [InlineData("carta", true)]
    [InlineData("oficio", false)]
    public void La_impresion_del_recibo_es_de_lista_cerrada(string valor, bool valida)
    {
        var r = new SaveMyPreferencesCommandValidator().Validate(
            new SaveMyPreferencesCommand(new Dictionary<string, string?> { [UserSettingKeys.ImpresionReciboCaja] = valor }));
        Assert.Equal(valida, r.IsValid);
    }

    [Theory]
    [InlineData("Libranza firmada", true)]
    [InlineData("Autorización de descuento por nómina", true)]
    [InlineData("Autorizacion de descuento", true)]
    [InlineData("Cédula del deudor", false)]
    public void Un_soporte_que_habla_de_libranza_se_rotula_libranza(string nombre, bool esLibranza) =>
        Assert.Equal(esLibranza, GetLoanSignedDocumentsQueryHandler.EsLibranza(nombre));
}
