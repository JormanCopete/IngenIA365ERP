using IngenIA365ERP.Application.Identity.Profile.Preferencias;
using IngenIA365ERP.Application.Lending.PersonStatement;
using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Domain.Lending.PersonStatement;

namespace IngenIA365ERP.Application.Tests.Lending.PersonStatement;

public class TablaDelEstadoDeCuentaTests
{
    private static ProductoDelEstadoDeCuentaDto Credito(string numero, string rol = "Deudor") => new(
        "Credito", Guid.NewGuid(), numero, "VENTAASO · Ventas", "Crédito", rol, "Vigente", "Principal", new DateOnly(2026, 11, 12),
        302350m, 302350m, 310000m, 151175m, "Mensual", 2, 2, 0m, "Cuota fija", new DateOnly(2026, 11, 29), new DateOnly(2026, 11, 29), 151175m,
        new DateOnly(2026, 12, 29), 0, 2, 0, 0, 0, 0, 0m, 0m, 0m, 0m, 0m, null, null);

    private static ProductoDelEstadoDeCuentaDto Aportes() => new(
        "Cuenta", Guid.NewGuid(), "AP-1", "APORTES · Aportes", "Aportes", "Titular", "Activa", "Principal", new DateOnly(2020, 1, 1),
        null, 1_000_000m, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null,
        null, null, 1_000_000m, null);

    private static EstadoDeCuentaDeLaPersonaDto Estado(params ProductoDelEstadoDeCuentaDto[] productos) => new(
        new PersonaDelEstadoDeCuentaDto(Guid.NewGuid(), "MANUEL CARDENAS", "16280096", "m@x.co", null, true), new DateOnly(2026, 11, 13), productos,
        new TotalesDelEstadoDeCuentaDto(302350m, 310000m, 0m, 0, 1_000_000m, 0m), []);

    [Fact]
    public void Cada_columna_del_catalogo_tiene_su_definicion_en_la_tabla()
    {
        Assert.Equal(ColumnasDelEstadoDeCuenta.Codigos.OrderBy(c => c), TablaDelEstadoDeCuenta.CodigosDefinidos.OrderBy(c => c));
    }

    [Fact]
    public void Sin_columnas_elegidas_salen_las_de_por_defecto_y_la_clase()
    {
        var tabla = TablaDelEstadoDeCuenta.Armar(Estado(Credito("CR-000001"), Aportes()), null);

        Assert.Equal(ColumnasDelEstadoDeCuenta.PorDefecto.Count + 1, tabla.Columnas.Count);
        Assert.Equal("Clase", tabla.Columnas[0].Nombre);
        Assert.Equal(2, tabla.Filas.Count);
        Assert.Equal("Crédito", tabla.Filas[0].Valores[0]);
        Assert.Equal("Aportes", tabla.Filas[1].Valores[0]);
    }

    [Fact]
    public void Las_columnas_salen_en_el_orden_del_catalogo_y_sin_desconocidas()
    {
        var tabla = TablaDelEstadoDeCuenta.Armar(Estado(Credito("CR-000001")), ["saldo", "inventada", "numero"]);

        Assert.Equal(["Clase", "Número", "Saldo"], tabla.Columnas.Select(c => c.Nombre));
        Assert.Equal(["Crédito", "CR-000001", (object?)302350m], tabla.Filas[0].Valores);
    }

    [Theory]
    [InlineData("[\"saldo\",\"numero\"]", true)]
    [InlineData("[]", true)]
    [InlineData("[\"saldo\",\"saldo\"]", false)]
    [InlineData("[\"inventada\"]", false)]
    [InlineData("no es json", false)]
    public void La_preferencia_de_columnas_solo_admite_codigos_del_catalogo(string valor, bool valida)
    {
        var validador = new SaveMyPreferencesCommandValidator();
        var r = validador.Validate(new SaveMyPreferencesCommand(new Dictionary<string, string?> { [UserSettingKeys.ColumnasEstadoDeCuenta] = valor }));
        Assert.Equal(valida, r.IsValid);
    }
}
