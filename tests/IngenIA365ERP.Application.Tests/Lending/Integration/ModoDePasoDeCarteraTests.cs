using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Parameters;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Integration;

/// <summary>
/// Feature 013, US7, T0625 (DT §2.25; contabilidad.md §10.1, §10.2; mensajes.md §7.2; T56, C01, C06–C09): el modo de paso que sella cada hecho
/// de Cartera. Precedencia vigencia del tipo → vigencia general → defecto de la tabla; sin vigencia y sin contabilidad iniciada, <c>NoPasa</c>;
/// un modo guardado a propósito se respeta. Los acompañantes sellan el de su principal con su propio corto y el parámetro por tipo se les
/// rechaza; los reversos heredan; los contingentes no pasan; los informativos siempre. <c>ScheduleKey</c> <c>CAR.{corto}|{disparador}|{hora}|
/// {granularidad}</c> de 60 caracteres como máximo y <c>BatchScopeKey</c> <c>AccrualRun:{d}</c> / <c>{d+1}</c> / <c>LendingClose:{aaaa-mm}</c>.
/// InMemory, con un doble de <see cref="IContabilidadParaCartera"/>.
/// </summary>
public class ModoDePasoDeCarteraTests
{
    private static readonly DateOnly Hoy = new(2026, 12, 14);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IContabilidadParaCartera _contabilidad = Substitute.For<IContabilidadParaCartera>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();

    public ModoDePasoDeCarteraTests()
    {
        _reloj.HoyLocal.Returns(Hoy);
        Contabilidad(iniciada: true);
        SembrarTipos();
    }

    private void Contabilidad(bool iniciada) =>
        _contabilidad.EstadoDelPeriodoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new EstadoDelPeriodoDto(iniciada, iniciada ? EstadoDePeriodoContable.Open : EstadoDePeriodoContable.NotFound, null)));

    private void SembrarTipos()
    {
        LendingFactType Tipo(string code, string corto, string modo, string? disparador = null) =>
            new() { Code = code, ShortCode = corto, Name = code, DefaultMode = modo, DefaultBatchTrigger = disparador, CreatedBy = "test" };
        _db.LendingFactTypes.AddRange(
            Tipo(PagoAplicadoV1.Type, "PAP", "EnLinea"),
            Tipo(AnticipoRecibidoV1.Type, "ANR", "EnLinea"),
            Tipo(CreditoDesembolsadoV1.Type, "DES", "EnLinea"),
            Tipo(InteresCausadoV1.Type, "ICA", "PorLotes", "FinDeCausacion"),
            Tipo(DeterioroCalculadoV1.Type, "DET", "PorLotes", "CierreDeCartera"),
            Tipo(DimensionContableCambiadaV1.Type, "DCC", "EnLinea", "FinDeCausacion"),
            Tipo(CreditoAprobadoSinDesembolsarV1.Type, "CAD", "NoPasa"),
            Tipo(CupoRotativoOtorgadoV1.Type, "CRO", "NoPasa"),
            Tipo(GarantiaConstituidaV1.Type, "GCT", "NoPasa"));
        _db.SaveChanges();
    }

    private int IdDelTipo(string code) => _db.LendingFactTypes.Single(f => f.Code == code).Id;

    private void Vigencia(string clave, string valor, string? tipo = null)
    {
        _db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = clave, Value = valor, ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba",
            ScopeKind = tipo is null ? ParameterScopeKind.None : ParameterScopeKind.LendingFactType, ScopeId = tipo is null ? 0 : IdDelTipo(tipo),
            CreatedBy = "test",
        });
        _db.SaveChanges();
    }

    private ModoDePasoDeCartera Modo() => new(_db, _contabilidad, new LectorDeParametros(_db), _reloj);

    private async Task<ModoDeEntrega> ResolverAsync(string tipo, bool proceso = false, DateOnly? fecha = null) =>
        (await Modo().ResolverAsync(tipo, fecha ?? Hoy, proceso, default)).Value;

    // ----------------------------------------------------------------------------------------------- precedencia --

    [Fact]
    public async Task Sin_vigencias_rige_el_defecto_de_la_tabla()
    {
        (await ResolverAsync(PagoAplicadoV1.Type)).Should().Be(new ModoDeEntrega.Sellado(DeliveryMode.Online));
        (await ResolverAsync(InteresCausadoV1.Type)).Should().Be(
            new ModoDeEntrega.Sellado(DeliveryMode.Batch, "CAR.ICA|FinDeCausacion||PorDia", "AccrualRun:2026-12-15"));
    }

    [Fact]
    public async Task La_vigencia_del_tipo_gana_a_la_general_y_la_general_al_defecto()
    {
        Vigencia(ParametrosDeCartera.ContabilidadModoDePaso, "NoPasa");
        (await ResolverAsync(PagoAplicadoV1.Type)).Should().Be(new ModoDeEntrega.Sellado(DeliveryMode.NotPosted), "la general gana al defecto");

        Vigencia(ParametrosDeCartera.ContabilidadModoDePaso, "PorLotes", PagoAplicadoV1.Type);
        (await ResolverAsync(PagoAplicadoV1.Type)).Should().Be(
            new ModoDeEntrega.Sellado(DeliveryMode.Batch, "CAR.PAP|FinDeCausacion||PorDia", "AccrualRun:2026-12-15"), "la del tipo gana a la general");
        (await ResolverAsync(CreditoDesembolsadoV1.Type)).Should().Be(new ModoDeEntrega.Sellado(DeliveryMode.NotPosted), "a los demás les rige la general");
    }

    [Fact]
    public async Task Sin_vigencia_y_sin_contabilidad_iniciada_no_pasa_pero_un_modo_guardado_a_proposito_se_respeta()
    {
        Contabilidad(iniciada: false);
        (await ResolverAsync(PagoAplicadoV1.Type)).Should().Be(new ModoDeEntrega.Sellado(DeliveryMode.NotPosted));
        (await ResolverAsync(InteresCausadoV1.Type)).Should().Be(new ModoDeEntrega.Sellado(DeliveryMode.NotPosted));

        Vigencia(ParametrosDeCartera.ContabilidadModoDePaso, "EnLinea", PagoAplicadoV1.Type);
        (await ResolverAsync(PagoAplicadoV1.Type)).Should().Be(new ModoDeEntrega.Sellado(DeliveryMode.Online),
            "quien guarda «en línea» sin contabilidad iniciada recibe Accounting.NotInitialized en la bandeja: se respeta");
    }

    [Fact]
    public async Task Los_contingentes_no_pasan_y_los_informativos_y_otros_destinos_siempre_se_entregan()
    {
        (await ResolverAsync(CreditoAprobadoSinDesembolsarV1.Type)).Should().Be(new ModoDeEntrega.Sellado(DeliveryMode.NotPosted));
        (await ResolverAsync(CupoRotativoOtorgadoV1.Type)).Should().Be(new ModoDeEntrega.Sellado(DeliveryMode.NotPosted));
        (await ResolverAsync(GarantiaConstituidaV1.Type)).Should().Be(new ModoDeEntrega.Sellado(DeliveryMode.NotPosted));
        (await ResolverAsync(CarteraActivadaV1.Type)).Should().Be(new ModoDeEntrega.Sellado(DeliveryMode.Always));
        (await ResolverAsync(CausacionSuspendidaV1.Type)).Should().Be(new ModoDeEntrega.Sellado(DeliveryMode.Always));
        (await ResolverAsync(ObligacionCreadaV1.Type)).Should().Be(new ModoDeEntrega.Sellado(DeliveryMode.Always));
    }

    // ------------------------------------------------------------------------------------- acompañantes y reversos --

    [Fact]
    public async Task Un_acompanante_sella_el_modo_del_principal_con_su_propio_corto()
    {
        Vigencia(ParametrosDeCartera.ContabilidadModoDePaso, "PorLotes", PagoAplicadoV1.Type);
        Vigencia(ParametrosDeCartera.ContabilidadDisparadorDeLote, "Hora", PagoAplicadoV1.Type);
        Vigencia(ParametrosDeCartera.ContabilidadHoraDeLote, "02:00", PagoAplicadoV1.Type);
        Vigencia(ParametrosDeCartera.ContabilidadGranularidad, "PorHecho", PagoAplicadoV1.Type);

        (await ResolverAsync(PagoAplicadoV1.Type)).Should().Be(new ModoDeEntrega.Sellado(DeliveryMode.Batch, "CAR.PAP|HoraDiaria|02:00|PorHecho"));
        (await ResolverAsync(AnticipoRecibidoV1.Type)).Should().Be(new ModoDeEntrega.Sellado(DeliveryMode.Batch, "CAR.ANR|HoraDiaria|02:00|PorHecho"),
            "mismo modo, disparador, hora y granularidad que el principal, con su corto");
        ModoDePasoDeCartera.DelAcompanante(new ModoDeEntrega.Sellado(DeliveryMode.Batch, "CAR.PAP|HoraDiaria|02:00|PorHecho"), "CIS")
            .Should().Be(new ModoDeEntrega.Sellado(DeliveryMode.Batch, "CAR.CIS|HoraDiaria|02:00|PorHecho"));
        ModoDePasoDeCartera.DelAcompanante(new ModoDeEntrega.Sellado(DeliveryMode.Online), "CIS").Should().Be(new ModoDeEntrega.Sellado(DeliveryMode.Online));
    }

    [Fact]
    public void La_lista_de_acompanantes_es_la_de_mensajes_y_los_reversos_heredan()
    {
        ModoDePasoDeCarteraAcompanantes().Should().BeEquivalentTo(new Dictionary<string, string[]>
        {
            [PagoAplicadoV1.Type] = [AnticipoRecibidoV1.Type, CobroDeInteresEnSuspensoV1.Type, RecuperacionDeCastigadaV1.Type, SaldoAFavorReconocidoV1.Type,
                AjusteDeCausacionPorFechaValorV1.Type],
            [PagoReversadoV1.Type] = [AjusteDeCausacionPorFechaValorV1.Type],
            [DesembolsoReversadoV1.Type] = [AjusteDeCausacionPorFechaValorV1.Type],
            [CreditoAjustadoDesdeComercialV1.Type] = [SaldoAFavorReconocidoV1.Type],
        });
        ModoDePasoDeCartera.SinModoPropio.Should().BeEquivalentTo(AnticipoRecibidoV1.Type, CobroDeInteresEnSuspensoV1.Type, RecuperacionDeCastigadaV1.Type);
        ModoDePasoDeCartera.Heredan.Should().BeEquivalentTo(DesembolsoReversadoV1.Type, PagoReversadoV1.Type, ReversoDeCierreV1.Type);
    }

    private static Dictionary<string, string[]> ModoDePasoDeCarteraAcompanantes() =>
        ModoDePasoDeCartera.AcompanantesPorPrincipal.ToDictionary(p => p.Key, p => p.Value.ToArray());

    [Theory]
    [InlineData(nameof(ParametrosDeCartera.ContabilidadModoDePaso))]
    [InlineData(nameof(ParametrosDeCartera.ContabilidadGranularidad))]
    [InlineData(nameof(ParametrosDeCartera.ContabilidadDisparadorDeLote))]
    public async Task El_modo_por_tipo_se_rechaza_para_un_acompanante_que_solo_viaja_con_su_principal(string campo)
    {
        var clave = (string)typeof(ParametrosDeCartera).GetField(campo)!.GetValue(null)!;
        var definicion = ParametrosDeCartera.Definiciones.Single(d => d.Clave == clave);
        var reglas = new ReglasDeParametrosDeCartera(Substitute.For<IValoresNacionales>(), new LectorDeParametros(_db));
        AltaDeParametro Alta(string tipo) => new(definicion, ParameterScopeKind.LendingFactType,
            new AmbitoDeParametro(ParameterScopeKind.LendingFactType, IdDelTipo(tipo), Guid.NewGuid(), tipo, tipo), null,
            definicion.ValoresAdmitidos.FirstOrDefault() ?? "PorLotes", Hoy, false);

        var r = await reglas.EvaluarAsync(Alta(AnticipoRecibidoV1.Type), default);
        r.Error.Code.Should().Be(ErroresDeParametrosDeCartera.CodigoAcompananteSinModo);
        r.Error.Message.Should().Contain(PagoAplicadoV1.Type);

        (await reglas.EvaluarAsync(Alta(PagoAplicadoV1.Type), default)).IsSuccess.Should().BeTrue("el principal sí tiene modo propio");
    }

    [Fact]
    public async Task Un_reverso_hereda_el_modo_y_la_clave_de_su_original_con_su_propio_corto()
    {
        Vigencia(ParametrosDeCartera.ContabilidadModoDePaso, "PorLotes", PagoAplicadoV1.Type);
        var actor = Substitute.For<IngenIA365ERP.Application.Common.Interfaces.Security.IActorActual>();
        actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new IngenIA365ERP.Application.Common.Execution.Actor(
            ActorKind.Person, 7, Guid.NewGuid(), Guid.NewGuid(), "Ana", null, ExecutionChannel.Web, "prueba", null, null));
        var emisor = new EmisorDeMensajes(_db, actor, _reloj);
        var pago = Guid.NewGuid();
        var sucursal = Guid.NewGuid();
        var modo = await ResolverAsync(PagoAplicadoV1.Type);
        await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, pago, "LoanPayment", "PG", "PG1", Hoy, sucursal),
            ClavesDeEvento.Confirmacion, [new PagoAplicadoV1(), new AnticipoRecibidoV1()], modo));
        await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, Guid.NewGuid(), "LoanPayment", "PG", "PG2", Hoy, sucursal),
            ClavesDeEvento.Confirmacion, [new PagoReversadoV1()], new ModoDeEntrega.Heredado(pago)));
        await _db.SaveChangesAsync();

        var entregas = _db.IntegrationMessageDeliveries.Select(d => new { d.Message!.Type, d.Mode, d.ScheduleKey }).ToList();
        entregas.Should().BeEquivalentTo(new[]
        {
            new { Type = PagoAplicadoV1.Type, Mode = DeliveryMode.Batch, ScheduleKey = (string?)"CAR.PAP|FinDeCausacion||PorDia" },
            new { Type = AnticipoRecibidoV1.Type, Mode = DeliveryMode.Batch, ScheduleKey = (string?)"CAR.ANR|FinDeCausacion||PorDia" },
            new { Type = PagoReversadoV1.Type, Mode = DeliveryMode.Batch, ScheduleKey = (string?)"CAR.PRV|FinDeCausacion||PorDia" },
        });
    }

    // --------------------------------------------------------------------------------- clave de horario y alcance --

    [Fact]
    public async Task La_clave_de_horario_lleva_el_corto_el_disparador_la_hora_y_la_granularidad()
    {
        (await ResolverAsync(DeterioroCalculadoV1.Type, proceso: true, fecha: new DateOnly(2026, 11, 30))).Should().Be(
            new ModoDeEntrega.Sellado(DeliveryMode.Batch, "CAR.DET|CierreDeCartera||PorDia", "LendingClose:2026-11"));

        Vigencia(ParametrosDeCartera.ContabilidadGranularidad, "PorDiaYSucursal", InteresCausadoV1.Type);
        var causacion = (ModoDeEntrega.Sellado)await ResolverAsync(InteresCausadoV1.Type, proceso: true);
        causacion.ScheduleKey.Should().Be("CAR.ICA|FinDeCausacion||PorDiaYSucursal", "la hora va vacía en FinDeCausacion");
        ClavesDeLote.Leer(causacion.ScheduleKey)!.Modulo.Should().Be(ModulosDeOrigen.Cartera);

        foreach (var tipo in CatalogoDeMensajesDeCarteraV1.HechosAContabilidad)
            ClavesDeLote.Horario("CAR." + tipo.CodigoCorto, "CierreDeCartera", new TimeOnly(23, 59), "PorDiaYSucursal").Length
                .Should().BeLessThanOrEqualTo(ModoDePasoDeCartera.LargoMaximoDeClave);
    }

    [Fact]
    public async Task Un_hecho_FinDeCausacion_nacido_fuera_de_la_corrida_va_al_lote_del_dia_siguiente_y_las_partes_al_de_su_dia()
    {
        (await ResolverAsync(InteresCausadoV1.Type, proceso: false)).Should().BeOfType<ModoDeEntrega.Sellado>()
            .Which.BatchScopeKey.Should().Be("AccrualRun:2026-12-15", "C09: el día siguiente al de su registro");
        ModoDePasoDeCartera.AlcanceDeCausacion(Hoy).Should().Be("AccrualRun:2026-12-14");
        EmisionDeCartera.AlcanceDeLoteDeCausacion(Hoy).Should().Be(ModoDePasoDeCartera.AlcanceDeCausacion(Hoy));
        ModoDePasoDeCartera.AlcanceDeCierre(new DateOnly(2026, 11, 30)).Should().Be("LendingClose:2026-11");
    }
}
