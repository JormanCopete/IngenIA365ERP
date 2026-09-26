using FluentAssertions;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using IngenIA365ERP.Domain.Taxes;

namespace IngenIA365ERP.Application.Tests.Common.Parameters;

/// <summary>
/// T012 (feature 012; decisiones-transversales T21; FR-012; data-model §4): el único lector de los parámetros con
/// vigencia. Cae ámbito → general → defecto seguro; un valor guardado fuera de lo admitido es
/// <c>Parameters.ValueNotAllowed</c>, nunca el defecto; leer a una fecha pasada devuelve la vigencia de esa fecha.
/// Sobre InMemory; las fechas son fijas (el lector no mira el reloj).
/// </summary>
public class LectorDeParametrosTests
{
    private const string Inv = ParametrosDeInventario.Modulo;
    private const string StockNegativo = ParametrosDeInventario.ExistenciasStockNegativoPermitido;

    private static readonly DateOnly Hoy = new(2026, 9, 25);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();

    private LectorDeParametros Lector() => new(_db);

    private void Guardar(string modulo, string clave, string valor, DateOnly desde, DateOnly? hasta = null,
        ParameterScopeKind ambito = ParameterScopeKind.None, int ambitoId = 0)
    {
        _db.ParameterVersions.Add(new ParameterVersion
        {
            Module = modulo, Key = clave, ScopeKind = ambito, ScopeId = ambitoId, Value = valor,
            ValidFrom = desde, ValidTo = hasta, Reason = "prueba",
        });
        _db.SaveChanges();
    }

    [Fact]
    public async Task Sin_vigencias_devuelve_el_defecto_seguro_de_la_definicion()
    {
        var r = await Lector().LeerAsync(Inv, StockNegativo, Hoy);

        r.IsSuccess.Should().BeTrue();
        r.Value.EsDefecto.Should().BeTrue();
        r.Value.Como<bool>().Should().BeFalse();
        r.Value.Texto.Should().Be("false");
    }

    [Fact]
    public async Task Con_vigencia_general_devuelve_su_valor_tipado()
    {
        Guardar(Inv, StockNegativo, "true", new DateOnly(2026, 1, 1));

        var r = await Lector().LeerAsync(Inv, StockNegativo, Hoy);

        r.Value.EsDefecto.Should().BeFalse();
        r.Value.Como<bool>().Should().BeTrue();
        r.Value.Vigencia!.ValidFrom.Should().Be(new DateOnly(2026, 1, 1));
    }

    [Fact]
    public async Task Cae_de_la_excepcion_del_ambito_al_general_y_del_general_al_defecto()
    {
        Guardar(Inv, StockNegativo, "true", new DateOnly(2026, 1, 1), ambito: ParameterScopeKind.Warehouse, ambitoId: 7);

        // La bodega 7 tiene su excepción; la 8 no, y sin general cae al defecto.
        (await Lector().LeerComoAsync<bool>(Inv, StockNegativo, Hoy, ParameterScopeKind.Warehouse, 7)).Value.Should().BeTrue();
        var sinExcepcion = await Lector().LeerAsync(Inv, StockNegativo, Hoy, ParameterScopeKind.Warehouse, 8);
        sinExcepcion.Value.EsDefecto.Should().BeTrue();
        sinExcepcion.Value.Como<bool>().Should().BeFalse();

        // Con un general, la 8 lo toma y la 7 sigue con su excepción.
        Guardar(Inv, StockNegativo, "false", new DateOnly(2026, 2, 1));
        var general = await Lector().LeerAsync(Inv, StockNegativo, Hoy, ParameterScopeKind.Warehouse, 8);
        general.Value.EsDefecto.Should().BeFalse();
        general.Value.Vigencia!.ScopeKind.Should().Be(ParameterScopeKind.None);
        (await Lector().LeerComoAsync<bool>(Inv, StockNegativo, Hoy, ParameterScopeKind.Warehouse, 7)).Value.Should().BeTrue();
    }

    [Fact]
    public async Task Un_ambito_que_la_clave_no_admite_lee_el_general()
    {
        Guardar(Inv, ParametrosDeInventario.RedondeoMontos, "Peso", new DateOnly(2026, 1, 1));

        var r = await Lector().LeerComoAsync<string>(Inv, ParametrosDeInventario.RedondeoMontos, Hoy, ParameterScopeKind.Warehouse, 7);

        r.Value.Should().Be("Peso");
    }

    [Fact]
    public async Task Un_valor_guardado_fuera_de_lo_admitido_es_ValueNotAllowed_y_nunca_el_defecto()
    {
        Guardar(Inv, StockNegativo, "quizas", new DateOnly(2026, 1, 1));

        var r = await Lector().LeerAsync(Inv, StockNegativo, Hoy);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(ErroresDeParametros.CodigoValorNoAdmitido);
    }

    [Fact]
    public async Task Peps_guardado_antes_de_I5_es_ValueNotAllowed()
    {
        Guardar(Inv, ParametrosDeInventario.CosteoMetodo, "Peps", new DateOnly(2026, 1, 1));

        var r = await Lector().LeerAsync(Inv, ParametrosDeInventario.CosteoMetodo, Hoy);

        r.Error.Code.Should().Be(ErroresDeParametros.CodigoValorNoAdmitido);
    }

    [Fact]
    public async Task Leer_a_una_fecha_pasada_devuelve_la_vigencia_de_esa_fecha()
    {
        Guardar(Inv, ParametrosDeInventario.ComprasDiasAlertaEventosRadian, "5", new DateOnly(2026, 1, 1), new DateOnly(2026, 5, 31));
        Guardar(Inv, ParametrosDeInventario.ComprasDiasAlertaEventosRadian, "8", new DateOnly(2026, 6, 1));
        var clave = ParametrosDeInventario.ComprasDiasAlertaEventosRadian;

        (await Lector().LeerComoAsync<int>(Inv, clave, new DateOnly(2025, 12, 31))).Value.Should().Be(3, "antes de toda vigencia rige el defecto");
        (await Lector().LeerComoAsync<int>(Inv, clave, new DateOnly(2026, 1, 1))).Value.Should().Be(5);
        (await Lector().LeerComoAsync<int>(Inv, clave, new DateOnly(2026, 5, 31))).Value.Should().Be(5);
        (await Lector().LeerComoAsync<int>(Inv, clave, new DateOnly(2026, 6, 1))).Value.Should().Be(8);
        (await Lector().LeerComoAsync<int>(Inv, clave, Hoy)).Value.Should().Be(8);
    }

    [Fact]
    public async Task Una_vigencia_dada_de_baja_no_cuenta()
    {
        _db.ParameterVersions.Add(new ParameterVersion
        {
            Module = Inv, Key = StockNegativo, Value = "true", ValidFrom = new DateOnly(2026, 1, 1), Reason = "futura retirada",
            IsDeleted = true,
        });
        _db.SaveChanges();

        (await Lector().LeerAsync(Inv, StockNegativo, Hoy)).Value.EsDefecto.Should().BeTrue();
    }

    [Fact]
    public async Task Una_clave_fuera_del_catalogo_es_KeyNotFound()
    {
        var r = await Lector().LeerAsync(Inv, "Existencias.Inventada", Hoy);

        r.Error.Code.Should().Be(ErroresDeParametros.CodigoClaveInexistente);
    }

    [Fact]
    public async Task Memoriza_por_peticion()
    {
        var lector = Lector();
        (await lector.LeerComoAsync<bool>(Inv, StockNegativo, Hoy)).Value.Should().BeFalse();

        Guardar(Inv, StockNegativo, "true", new DateOnly(2026, 1, 1));

        (await lector.LeerComoAsync<bool>(Inv, StockNegativo, Hoy)).Value.Should().BeFalse("la misma petición no vuelve a leer");
        (await Lector().LeerComoAsync<bool>(Inv, StockNegativo, Hoy)).Value.Should().BeTrue("otra petición sí");
    }

    [Fact]
    public async Task Tipa_hora_fecha_vacia_y_decimal()
    {
        (await Lector().LeerComoAsync<TimeOnly>(Inv, ParametrosDeInventario.ContabilidadHoraDeLote, Hoy)).Value.Should().Be(new TimeOnly(23, 0));
        (await Lector().LeerComoAsync<DateOnly?>(Inv, ParametrosDeInventario.CarteraIntegracionHabilitadaDesde, Hoy)).Value.Should().BeNull();
        (await Lector().LeerComoAsync<decimal>(ParametrosDeFacturacionElectronica.Modulo, ParametrosDeFacturacionElectronica.AvisoResolucionPorcentaje, Hoy))
            .Value.Should().Be(0.90m);
    }

    // --------------------------------------------------------------------- invariantes del catálogo (T068) --

    [Fact]
    public void Todo_defecto_seguro_es_admitido_por_su_propia_definicion()
    {
        var malos = CatalogoDeParametros.Todas
            .Where(d => !d.Interpretar(d.DefectoSeguro, CatalogoDeParametros.EntregaVigente).Admitido)
            .Select(d => $"{d.Modulo}/{d.Clave}")
            .ToList();

        malos.Should().BeEmpty();
    }

    [Fact]
    public void El_catalogo_tiene_las_claves_de_data_model_sin_repetir()
    {
        CatalogoDeParametros.Todas.Select(d => (d.Modulo, d.Clave)).Should().OnlyHaveUniqueItems();
        ParametrosDeInventario.Definiciones.Should().HaveCount(41);
        ParametrosTributarios.Definiciones.Should().HaveCount(6);
        ParametrosDeFacturacionElectronica.Definiciones.Should().HaveCount(10);
        CatalogoDeParametros.Todas.Should().OnlyContain(d => d.AmbitosAdmitidos.Contains(ParameterScopeKind.None));
    }

    [Fact]
    public void Peps_solo_se_admite_desde_I5()
    {
        var metodo = CatalogoDeParametros.Buscar(Inv, ParametrosDeInventario.CosteoMetodo)!;

        metodo.Interpretar("Peps", EntregaDelComercio.I1).Admitido.Should().BeFalse();
        metodo.Interpretar("peps", EntregaDelComercio.I5).Should().Be(new ValorInterpretado(true, "Peps", "Peps"));
        metodo.Admitidos(EntregaDelComercio.I1).Should().Equal("PromedioPonderado");
    }

    [Theory]
    [InlineData(TipoDeParametro.Bool, "TRUE", true)]
    [InlineData(TipoDeParametro.Bool, "si", false)]
    [InlineData(TipoDeParametro.Int, "-1", false)]
    [InlineData(TipoDeParametro.Int, "4.5", false)]
    [InlineData(TipoDeParametro.Decimal, "0,5", false)]
    [InlineData(TipoDeParametro.Decimal, "0.5", true)]
    [InlineData(TipoDeParametro.Date, "2026-02-30", false)]
    [InlineData(TipoDeParametro.Time, "24:00", false)]
    [InlineData(TipoDeParametro.Time, "07:30", true)]
    public void Interpretar_valida_el_formato_del_tipo(TipoDeParametro tipo, string texto, bool admitido)
    {
        var definicion = new DefinicionDeParametro { Modulo = "INV", Clave = "X", Descripcion = "x", Tipo = tipo, DefectoSeguro = "" };

        definicion.Interpretar(texto, EntregaDelComercio.I1).Admitido.Should().Be(admitido);
    }

    [Fact]
    public void Las_claves_con_permiso_propio_son_las_del_contrato()
    {
        CatalogoDeParametros.Todas.Where(d => d.Clave.StartsWith("Costeo.", StringComparison.Ordinal))
            .Should().OnlyContain(d => d.PermisoAdicional == "Inventory.Costing.Manage");
        ParametrosTributarios.Definiciones.Should().OnlyContain(d => d.PermisoAdicional == "Core.Taxes.Manage");
        ParametrosDeFacturacionElectronica.Definiciones.Should().OnlyContain(d => d.PermisoAdicional == "ElectronicInvoicing.Settings.Manage");
        CatalogoDeParametros.Todas.Where(d => d.ExigeFuenteLegal).Select(d => d.Clave)
            .Should().BeEquivalentTo(ParametrosDeInventario.InformesTopeFaltantesPorcentaje, ParametrosDeFacturacionElectronica.PlazoContingenciaHoras);
    }
}
