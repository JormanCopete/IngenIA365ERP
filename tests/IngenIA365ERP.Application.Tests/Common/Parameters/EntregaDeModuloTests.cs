using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Common.Parameters.AddParameterVersion;
using IngenIA365ERP.Application.Common.Parameters.ListParameters;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.Parameters;

/// <summary>
/// T0027 (feature 013; decisiones-transversales T43, §2.6; data-model parte 3 §4.15, §12.1; quickstart §1.5 «Parámetros»): la entrega de
/// cada clave es una por línea (<see cref="EntregaDeModulo"/>). Una clave de Cartera marcada K2 no se ofrece ni se acepta con
/// <see cref="CatalogoDeCartera.EntregaVigente"/> en K1; las de Inventario siguen gobernadas por la suya (<see cref="EntregaDelComercio"/>
/// convertida, I6 vigente). Además, el catálogo de Cartera completo: módulo, claves, defectos admitidos y límites legales.
/// </summary>
public class EntregaDeModuloTests
{
    private static readonly DateOnly Desde = new(2026, 11, 1);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IPermissionChecker _permisos = Substitute.For<IPermissionChecker>();
    private readonly LimitesDePrueba _limites = new LimitesDePrueba().Con(CatalogoDeLimitesLegales.LibranzaVentanaGiroDias, 3);

    public EntregaDeModuloTests() =>
        _permisos.HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

    private AddParameterVersionCommandHandler Alta(EntregaDeModulo? entrega = null) => new(_db, _permisos, new ResolutorDeAmbitoVacio(),
        new ReglasDeParametrosPorModulo(new ReglasDeParametrosVacias(), [new ReglasDeParametrosDeCartera(_limites, new LectorDeParametros(_db))]),
        entrega: entrega);

    private static AddParameterVersionCommand Comando(string modulo, string clave, string valor, string? fuente = "Acta 3") =>
        new(modulo, clave, ParameterScopeKind.None, null, null, valor, Desde, "Política aprobada", fuente) { OperationKey = Guid.NewGuid() };

    private async Task<IReadOnlyList<ParameterDto>> Listar(string? modulo)
    {
        var reloj = Substitute.For<IDateTimeService>();
        reloj.HoyLocal.Returns(Desde);
        var r = await new ListParametersQueryHandler(new LectorDeParametros(_db), new ResolutorDeAmbitoVacio(), reloj)
            .Handle(new ListParametersQuery(modulo), CancellationToken.None);
        return r.Value;
    }

    // ------------------------------------------------------------------------------------------------ la entrega --

    [Fact]
    public void Las_entregas_se_convierten_y_solo_se_comparan_dentro_de_su_linea()
    {
        EntregaDeModulo i5 = EntregaDelComercio.I5;
        EntregaDeModulo k2 = EntregaDeCartera.K2;

        i5.Should().Be(new EntregaDeModulo("INV", 6, "I5"));
        k2.Should().Be(new EntregaDeModulo("LND", 2, "K2"));
        (i5 < (EntregaDeModulo)EntregaDelComercio.I6).Should().BeTrue();
        k2.ToString().Should().Be("K2");
        var comparar = () => k2 <= i5;
        comparar.Should().Throw<InvalidOperationException>("«K2 es posterior a I4» no significa nada");

        CatalogoDeParametros.EntregaVigenteDe("INV").Should().Be((EntregaDeModulo)CatalogoDeParametros.EntregaVigente);
        CatalogoDeParametros.EntregaVigenteDe("LND").Should().Be((EntregaDeModulo)CatalogoDeCartera.EntregaVigente);
        CatalogoDeCartera.EntregaVigente.Should().Be(EntregaDeCartera.K1);
    }

    [Fact]
    public async Task Una_clave_de_Cartera_de_K2_no_se_acepta_con_la_entrega_vigente_en_K1()
    {
        var r = await Alta().Handle(Comando("LND", ParametrosDeCartera.LibranzaVentanaDeGiroDias, "3"), CancellationToken.None);

        r.Error.Code.Should().Be("Lending.Parameter.NotYetAvailable");
        var datos = JsonSerializer.SerializeToElement(r.Error.Should().BeOfType<ErrorConDatos>().Subject.Data);
        datos.GetProperty("availableFrom").GetString().Should().Be("K2");
        datos.GetProperty("key").GetString().Should().Be(ParametrosDeCartera.LibranzaVentanaDeGiroDias);
    }

    [Fact]
    public async Task Con_la_entrega_de_Cartera_en_K2_la_misma_clave_se_acepta()
    {
        var r = await Alta(EntregaDeCartera.K2).Handle(Comando("LND", ParametrosDeCartera.LibranzaVentanaDeGiroDias, "3"), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : "");
    }

    [Fact]
    public async Task Una_clave_de_Cartera_de_K2_no_se_ofrece_y_las_de_K1_si()
    {
        var claves = (await Listar("LND")).Select(p => p.Key).ToList();

        claves.Should().Contain(ParametrosDeCartera.PagosDiasMaximosFechaValor).And.Contain(ParametrosDeCartera.CobranzaHorarioLunesAViernes);
        claves.Should().NotContain(ParametrosDeCartera.LibranzaVentanaDeGiroDias).And.NotContain(ParametrosDeCartera.AvisoPrevioDiasMinimos);
        (await Listar("LND")).Should().OnlyContain(p => p.AvailableFrom == "K1");
    }

    [Fact]
    public async Task La_lista_sin_modulo_sigue_siendo_la_del_comercio()
    {
        var modulos = (await Listar(null)).Select(p => p.Module).Distinct().ToList();

        modulos.Should().NotContain("LND", "las claves de Cartera tienen su ruta y su permiso");
        modulos.Should().Contain(ParametrosDeInventario.Modulo);
    }

    [Fact]
    public async Task Las_claves_de_Inventario_siguen_gobernadas_por_su_entrega()
    {
        // I6 vigente: las claves de I5 se ofrecen y Peps se admite, como antes.
        var retroactivos = CatalogoDeParametros.Buscar("INV", ParametrosDeInventario.CosteoRetroactivosPermitidos)!;
        retroactivos.DisponibleDesde.Should().Be((EntregaDeModulo)EntregaDelComercio.I5);
        retroactivos.DisponibleEnLaEntrega().Should().BeTrue();
        retroactivos.DisponibleEnLaEntrega(EntregaDelComercio.I4).Should().BeFalse();
        retroactivos.DisponibleEnLaEntrega(EntregaDeCartera.K1).Should().BeTrue("una entrega de otra línea no la juzga: rige la vigente del comercio");
        (await Listar("INV")).Select(p => p.Key).Should().Contain(ParametrosDeInventario.CosteoRetroactivosPermitidos);

        var metodo = CatalogoDeParametros.Buscar("INV", ParametrosDeInventario.CosteoMetodo)!;
        metodo.Interpretar("Peps").Admitido.Should().BeTrue();
        metodo.Interpretar("Peps", EntregaDelComercio.I4).Admitido.Should().BeFalse();

        var r = await Alta().Handle(Comando("INV", ParametrosDeInventario.CosteoMetodo, "Peps"), CancellationToken.None);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : "");
    }

    // ---------------------------------------------------------------------------------------- el catálogo LND --

    [Fact]
    public void El_catalogo_de_Cartera_es_el_modulo_LND_sin_prefijo_y_registrado_en_la_plataforma()
    {
        ParametrosDeCartera.Modulo.Should().Be("LND");
        CatalogoDeParametros.Modulos.Should().Contain("LND");
        ParametrosDeCartera.Definiciones.Should().OnlyContain(d => d.Modulo == "LND");
        ParametrosDeCartera.Definiciones.Should().OnlyContain(d => !d.Clave.StartsWith("Cartera.", StringComparison.Ordinal));
        ParametrosDeCartera.Definiciones.Select(d => d.Clave).Should().OnlyHaveUniqueItems();
        ParametrosDeCartera.Definiciones.Should().OnlyContain(d => d.DisponibleDesde.Modulo == EntregaDeModulo.LineaDeCartera,
            "cada clave declara su entrega de Cartera");
        ParametrosDeCartera.Definiciones.Should().OnlyContain(d => d.AmbitosAdmitidos.All(a =>
            a == ParameterScopeKind.None || a == ParameterScopeKind.CreditLine || a == ParameterScopeKind.LendingFactType));
    }

    [Fact]
    public void Todo_defecto_es_admitido_por_su_definicion()
    {
        ParametrosDeCartera.Definiciones
            .Where(d => !d.Interpretar(d.DefectoSeguro, EntregaDeCartera.K6b).Admitido)
            .Select(d => d.Clave)
            .Should().BeEmpty();
    }

    [Fact]
    public void Los_limites_de_cada_clave_son_los_del_catalogo_de_limites_legales()
    {
        foreach (var d in ParametrosDeCartera.Definiciones)
        {
            var delCatalogo = CatalogoDeLimitesLegales.Todos.Where(l => l.ClaveDeParametro == d.Clave).Select(l => l.Codigo);
            d.LimitesLegales.Should().BeEquivalentTo(delCatalogo, $"«{d.Clave}» declara los límites que la acotan");
            d.ExigeFuenteLegal.Should().BeFalse("la fuente de un límite vive en su valor nacional LIMITE; la plataforma fija las dos únicas claves que exigen legalSource");
        }
    }

    [Theory]
    [InlineData(ParametrosDeCartera.PagosDiasMaximosFechaValor, "5", EntregaDeCartera.K1)]
    [InlineData(ParametrosDeCartera.CupoMoraMaximaParaVender, "0", EntregaDeCartera.K1)]
    [InlineData(ParametrosDeCartera.BloqueosDiasDeMoraParaBloquear, "30", EntregaDeCartera.K1)]
    [InlineData(ParametrosDeCartera.ContabilidadDiferirComisiones, "false", EntregaDeCartera.K1)]
    [InlineData(ParametrosDeCartera.ContabilidadPoliticaSinRespuesta, "ConfirmarConPendiente", EntregaDeCartera.K1)]
    [InlineData(ParametrosDeCartera.ContabilidadFaltaDeRegla, "Advertir", EntregaDeCartera.K1)]
    [InlineData(ParametrosDeCartera.InformesRangosDeEdad, "30,60,90,180,360", EntregaDeCartera.K1)]
    [InlineData(ParametrosDeCartera.ListasUmbralDeCoincidenciaDeNombre, "0.90", EntregaDeCartera.K1)]
    [InlineData(ParametrosDeCartera.CobranzaHorarioLunesAViernes, "07:00-19:00", EntregaDeCartera.K1)]
    [InlineData(ParametrosDeCartera.LibranzaUmbralDeMoraDePagaduria, "", EntregaDeCartera.K2)]
    [InlineData(ParametrosDeCartera.AvisoPrevioDiasMinimos, "20", EntregaDeCartera.K4)]
    public void Defectos_y_entregas_de_data_model(string clave, string defecto, EntregaDeCartera entrega)
    {
        var d = CatalogoDeParametros.Buscar("LND", clave)!;

        d.DefectoSeguro.Should().Be(defecto);
        d.DisponibleDesde.Should().Be((EntregaDeModulo)entrega);
    }

    [Fact]
    public void Los_defectos_que_valida_la_contadora_quedan_pendientes_de_validar()
    {
        foreach (var clave in new[] { ParametrosDeCartera.CupoMoraMaximaParaVender, ParametrosDeCartera.BloqueosDiasDeMoraParaBloquear,
                     ParametrosDeCartera.ContabilidadDiferirComisiones, ParametrosDeCartera.LibranzaUmbralDeMoraDePagaduria })
            CatalogoDeParametros.Buscar("LND", clave)!.PendienteDeValidar.Should().NotBeNullOrWhiteSpace(clave);

        var falta = CatalogoDeParametros.Buscar("LND", ParametrosDeCartera.ContabilidadFaltaDeRegla)!;
        falta.Descripcion.Should().Contain("pantalla").And.Contain("A8");
        CatalogoDeParametros.Buscar("LND", ParametrosDeCartera.ContabilidadPoliticaSinRespuesta)!.Descripcion.Should().Contain("A8");
    }
}
