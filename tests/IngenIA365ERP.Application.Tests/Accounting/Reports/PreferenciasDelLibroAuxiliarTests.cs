using IngenIA365ERP.Application.Identity.Profile.Preferencias;
using IngenIA365ERP.Domain.Entities.Admin;

namespace IngenIA365ERP.Application.Tests.Accounting.Reports;

/// <summary>
/// 2026-11-15: las columnas del libro auxiliar y la casilla «Sólo cuentas de movimiento» se guardan en las preferencias del usuario, con
/// lista cerrada (la tabla la escribe el propio usuario).
/// </summary>
public class PreferenciasDelLibroAuxiliarTests
{
    [Theory]
    [InlineData("[\"fecha\",\"tercero\"]", true)]
    [InlineData("[]", true)]
    [InlineData("[\"fecha\",\"fecha\"]", false)]
    [InlineData("[\"saldo\"]", false)]
    [InlineData("no es json", false)]
    public void Las_columnas_solo_admiten_codigos_del_catalogo(string valor, bool valida)
    {
        var r = new SaveMyPreferencesCommandValidator().Validate(
            new SaveMyPreferencesCommand(new Dictionary<string, string?> { [UserSettingKeys.ColumnasLibroAuxiliar] = valor }));
        Assert.Equal(valida, r.IsValid);
    }

    [Theory]
    [InlineData("si", true)]
    [InlineData("no", true)]
    [InlineData("tal vez", false)]
    public void Solo_movimiento_es_si_o_no(string valor, bool valida)
    {
        var r = new SaveMyPreferencesCommandValidator().Validate(
            new SaveMyPreferencesCommand(new Dictionary<string, string?> { [UserSettingKeys.LibroAuxiliarSoloMovimiento] = valor }));
        Assert.Equal(valida, r.IsValid);
    }
}
