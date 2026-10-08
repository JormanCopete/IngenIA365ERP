using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Common.Parameters.AddParameterVersion;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Common.Parameters;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Entities.Core.NationalValues;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Parameters;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Common;

/// <summary>
/// Feature 013, T0254 (T42, T43, T38; quickstart §1.5; data-model parte 3 §12.1, §12.5): los parámetros de Cartera por el alta y la lectura
/// de la plataforma. Una vigencia menos estricta que su <c>LIMITE</c> es <c>Lending.Parameter.BelowLegalFloor</c> con el límite y su fuente;
/// al leer rige lo más estricto entre la cooperativa y el límite vigente en D; una clave de K2, K4 o K6a todavía no se acepta
/// (<c>Lending.Parameter.NotYetAvailable</c> con <c>data.availableFrom</c>); <c>Segregacion.Incompatibilidades</c> exige
/// <c>Lending.Segregation.Manage</c>; con <c>informacionHasta</c> la lectura es bitemporal. Y T0291: un límite nuevo más estricto que una
/// vigencia de la cooperativa —también un horario o un sí/no— alerta <c>Cartera.LimiteLegalMasEstricto</c>.
/// </summary>
public class ReglasDeParametrosDeCarteraTests
{
    private const string Lnd = ParametrosDeCartera.Modulo;
    private static readonly DateOnly Desde = new(2026, 11, 1);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IPermissionChecker _permisos = Substitute.For<IPermissionChecker>();
    private readonly LimitesDePrueba _limites = new LimitesDePrueba()
        .Con(CatalogoDeLimitesLegales.CobranzaHoraInicioLunesAViernes, 7, fuente: "Ley 2300 de 2023")
        .Con(CatalogoDeLimitesLegales.CobranzaHoraFinLunesAViernes, 19, fuente: "Ley 2300 de 2023")
        .Con(CatalogoDeLimitesLegales.CobranzaHoraFinLunesAViernes, 18, new DateOnly(2027, 1, 1), "Ley de prueba que endurece la franja")
        .Con(CatalogoDeLimitesLegales.CobranzaContactosDia, 1, fuente: "Ley 2300 de 2023");

    public ReglasDeParametrosDeCarteraTests() =>
        _permisos.HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

    private ReglasDeParametrosDeCartera Reglas() => new(_limites, new LectorDeParametros(_db));

    private Task<Result<AddParameterVersionResponse>> Registrar(string clave, string valor, DateOnly? desde = null) =>
        new AddParameterVersionCommandHandler(_db, _permisos, new ResolutorDeAmbitoVacio(),
                new ReglasDeParametrosPorModulo(new ReglasDeParametrosVacias(), [Reglas()]))
            .Handle(new AddParameterVersionCommand(Lnd, clave, ParameterScopeKind.None, null, null, valor, desde ?? Desde, "Política aprobada",
                "Acta 12 del consejo") { OperationKey = Guid.NewGuid() }, CancellationToken.None);

    private static JsonElement Datos(Error error) => JsonSerializer.SerializeToElement(error.Should().BeOfType<ErrorConDatos>().Subject.Data);

    private void Guardar(string clave, string valor, DateOnly desde, DateTime? creada = null)
    {
        _db.ParameterVersions.Add(new ParameterVersion
        {
            Module = Lnd, Key = clave, ScopeKind = ParameterScopeKind.None, Value = valor, ValidFrom = desde, Reason = "prueba",
            CreatedAt = creada ?? new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
        });
        _db.SaveChanges();
    }

    [Fact]
    public async Task Un_horario_mas_amplio_que_COBRANZA_HORA_FIN_LV_es_BelowLegalFloor_con_el_limite_y_su_fuente()
    {
        var r = await Registrar(ParametrosDeCartera.CobranzaHorarioLunesAViernes, "07:00-20:00");

        r.Error.Code.Should().Be("Lending.Parameter.BelowLegalFloor");
        var datos = Datos(r.Error);
        datos.GetProperty("limitCode").GetString().Should().Be(CatalogoDeLimitesLegales.CobranzaHoraFinLunesAViernes);
        datos.GetProperty("limitValue").GetDecimal().Should().Be(19m);
        datos.GetProperty("source").GetString().Should().Be("Ley 2300 de 2023");
        (await Registrar(ParametrosDeCartera.CobranzaHorarioLunesAViernes, "08:00-18:30")).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Al_leer_rige_lo_mas_estricto_entre_la_cooperativa_y_el_limite_vigente_en_D()
    {
        Guardar(ParametrosDeCartera.CobranzaHorarioLunesAViernes, "07:00-19:00", Desde);

        var antes = await Reglas().LeerAsync(ParametrosDeCartera.CobranzaHorarioLunesAViernes, new DateOnly(2026, 12, 15));
        var despues = await Reglas().LeerAsync(ParametrosDeCartera.CobranzaHorarioLunesAViernes, new DateOnly(2027, 2, 1));

        antes.Value.Texto.Should().Be("07:00-19:00");
        antes.Value.RigeLaLey.Should().BeFalse();
        despues.Value.Texto.Should().Be("07:00-18:00", "desde 2027 el límite de prueba acorta la franja");
        despues.Value.DeLaCooperativa.Texto.Should().Be("07:00-19:00");
        despues.Value.Limites.Should().Contain(l => l.Codigo == CatalogoDeLimitesLegales.CobranzaHoraFinLunesAViernes && l.Rige);
    }

    [Theory]
    [InlineData(ParametrosDeCartera.LibranzaVentanaDeGiroDias, "K2")]
    [InlineData(ParametrosDeCartera.AvisoPrevioDiasMinimos, "K4")]
    public async Task Una_clave_de_una_entrega_posterior_es_NotYetAvailable_con_availableFrom(string clave, string entrega)
    {
        var r = await Registrar(clave, "3");

        r.Error.Code.Should().Be("Lending.Parameter.NotYetAvailable");
        Datos(r.Error).GetProperty("availableFrom").GetString().Should().Contain(entrega);
    }

    [Fact]
    public void Las_claves_de_K2_K4_y_K6a_existen_en_el_catalogo_con_su_entrega()
    {
        ParametrosDeCartera.Definiciones.Select(d => d.DisponibleDesde.ToString()).Distinct()
            .Should().Contain(e => e.Contains("K2")).And.Contain(e => e.Contains("K4"));
        CatalogoDeParametros.Buscar(Lnd, ParametrosDeCartera.RiesgoReglaMesesFaltantes)!.PendienteDeValidar
            .Should().NotBeNullOrWhiteSpace("la regla de meses faltantes (uso K6a) se siembra pendiente de validar");
    }

    [Fact]
    public async Task Segregacion_Incompatibilidades_sin_Lending_Segregation_Manage_es_PermissionRequired()
    {
        _permisos.HasPermissionAsync(ParametrosDeCartera.PermisoDeSegregacion, Arg.Any<CancellationToken>()).Returns(false);

        var r = await Registrar(ParametrosDeCartera.SegregacionIncompatibilidades, "[{\"a\": \"Analizar\", \"b\": \"Decidir\", \"activo\": true}]");

        r.Error.Code.Should().Be("Parameters.PermissionRequired");
        Datos(r.Error).GetProperty("permissionCode").GetString().Should().Be("Lending.Segregation.Manage");
    }

    [Fact]
    public async Task Con_informacionHasta_la_lectura_es_bitemporal()
    {
        Guardar(ParametrosDeCartera.CobranzaContactosPorDia, "1", Desde, new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc));
        Guardar(ParametrosDeCartera.MensajesReintentos, "2", Desde, new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc));
        Guardar(ParametrosDeCartera.MensajesReintentos, "5", new DateOnly(2026, 11, 15), new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc));

        var sabidoEnOctubre = await Reglas().LeerAsync(ParametrosDeCartera.MensajesReintentos, new DateOnly(2026, 12, 1), new DateTime(2026, 10, 31));
        var sabidoHoy = await Reglas().LeerAsync(ParametrosDeCartera.MensajesReintentos, new DateOnly(2026, 12, 1));

        sabidoEnOctubre.Value.Texto.Should().Be("2", "en octubre no se conocía la vigencia de noviembre");
        sabidoHoy.Value.Texto.Should().Be("5");
    }

    // ----------------------------------------------------------------------------------------------- T0291: la alerta --

    private static NationalValue Limite(string codigo, decimal valor, DateOnly desde) => new()
    {
        Code = CodigosDeValorNacional.Limite, Key = codigo, Value = valor, ValidFrom = desde, Source = "Ley de prueba",
    };

    [Fact]
    public async Task Un_limite_nuevo_que_deja_un_horario_menos_estricto_alerta_LimiteLegalMasEstricto()
    {
        Guardar(ParametrosDeCartera.CobranzaHorarioLunesAViernes, "07:00-19:00", Desde);

        var efecto = await new EfectoDeReplicacionLimitesLegales().AlReplicarAsync(_db,
            new LoteReplicado([Limite(CatalogoDeLimitesLegales.CobranzaHoraFinLunesAViernes, 18, new DateOnly(2027, 1, 1))], [],
                new DateTime(2026, 12, 1), new DateOnly(2026, 12, 1)), CancellationToken.None);

        efecto.Alertas.Should().ContainSingle().Which.TypeCode.Should().Be("Cartera.LimiteLegalMasEstricto");
    }

    [Fact]
    public async Task Un_limite_que_la_cooperativa_ya_cumple_no_alerta()
    {
        Guardar(ParametrosDeCartera.CobranzaHorarioLunesAViernes, "08:00-17:00", Desde);
        Guardar(ParametrosDeCartera.MoraSobreIntereses, "false", Desde);

        var efecto = await new EfectoDeReplicacionLimitesLegales().AlReplicarAsync(_db,
            new LoteReplicado(
                [
                    Limite(CatalogoDeLimitesLegales.CobranzaHoraFinLunesAViernes, 18, new DateOnly(2027, 1, 1)),
                    Limite(CatalogoDeLimitesLegales.MoraSobreInteresesPermitida, 0, new DateOnly(2027, 1, 1)),
                ], [], new DateTime(2026, 12, 1), new DateOnly(2026, 12, 1)), CancellationToken.None);

        efecto.Alertas.Should().BeEmpty();
    }

    [Fact]
    public async Task Un_si_no_permitido_que_la_norma_prohibe_alerta()
    {
        Guardar(ParametrosDeCartera.MoraSobreIntereses, "true", Desde);

        var efecto = await new EfectoDeReplicacionLimitesLegales().AlReplicarAsync(_db,
            new LoteReplicado([Limite(CatalogoDeLimitesLegales.MoraSobreInteresesPermitida, 0, new DateOnly(2027, 1, 1))], [],
                new DateTime(2026, 12, 1), new DateOnly(2026, 12, 1)), CancellationToken.None);

        efecto.Alertas.Should().ContainSingle();
    }
}
