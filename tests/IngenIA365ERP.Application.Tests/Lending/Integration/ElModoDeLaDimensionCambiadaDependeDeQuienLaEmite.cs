using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
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
/// Feature 013, US7, T0625 (DT §2.25 —prueba de Application, no de arquitectura—; contabilidad.md §10.1; T56, C8): <c>DimensionContableCambiada</c>
/// sin vigencia propia sale <c>EnLinea</c> si la emite una operación y <c>PorLotes</c> resumido <c>FinDeCausacion</c> si la detecta el proceso; una
/// vigencia general no la alcanza; con vigencia propia rige para los dos.
/// </summary>
public class ElModoDeLaDimensionCambiadaDependeDeQuienLaEmite
{
    private static readonly DateOnly Hoy = new(2026, 12, 14);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IContabilidadParaCartera _contabilidad = Substitute.For<IContabilidadParaCartera>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();

    public ElModoDeLaDimensionCambiadaDependeDeQuienLaEmite()
    {
        _reloj.HoyLocal.Returns(Hoy);
        _contabilidad.EstadoDelPeriodoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new EstadoDelPeriodoDto(true, EstadoDePeriodoContable.Open, null)));
        _db.LendingFactTypes.Add(new LendingFactType
        {
            Code = DimensionContableCambiadaV1.Type, ShortCode = "DCC", Name = "Dimensión contable cambiada", DefaultMode = "EnLinea",
            DefaultBatchTrigger = "FinDeCausacion", CreatedBy = "test",
        });
        _db.SaveChanges();
    }

    private Task<Result<ModoDeEntrega>> ResolverAsync(bool proceso) =>
        new ModoDePasoDeCartera(_db, _contabilidad, new LectorDeParametros(_db), _reloj)
            .ResolverAsync(DimensionContableCambiadaV1.Type, Hoy, proceso, default);

    private void Vigencia(string valor, bool propia)
    {
        _db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = ParametrosDeCartera.ContabilidadModoDePaso, Value = valor, ValidFrom = new DateOnly(2026, 1, 1),
            Reason = "prueba", ScopeKind = propia ? ParameterScopeKind.LendingFactType : ParameterScopeKind.None,
            ScopeId = propia ? _db.LendingFactTypes.Single().Id : 0, CreatedBy = "test",
        });
        _db.SaveChanges();
    }

    [Fact]
    public async Task Sin_vigencia_propia_en_linea_si_la_emite_una_operacion_y_por_lotes_si_la_emite_el_proceso()
    {
        (await ResolverAsync(proceso: false)).Value.Should().Be(new ModoDeEntrega.Sellado(DeliveryMode.Online));
        (await ResolverAsync(proceso: true)).Value.Should().Be(
            new ModoDeEntrega.Sellado(DeliveryMode.Batch, "CAR.DCC|FinDeCausacion||PorDia", "AccrualRun:2026-12-15"));
    }

    [Fact]
    public async Task Una_vigencia_general_no_la_alcanza()
    {
        Vigencia("NoPasa", propia: false);

        (await ResolverAsync(proceso: false)).Value.Should().Be(new ModoDeEntrega.Sellado(DeliveryMode.Online));
        (await ResolverAsync(proceso: true)).Value.Should().BeOfType<ModoDeEntrega.Sellado>().Which.Modo.Should().Be(DeliveryMode.Batch);
    }

    [Theory]
    [InlineData("EnLinea", DeliveryMode.Online)]
    [InlineData("PorLotes", DeliveryMode.Batch)]
    [InlineData("NoPasa", DeliveryMode.NotPosted)]
    public async Task Con_vigencia_propia_rige_para_los_dos(string valor, DeliveryMode esperado)
    {
        Vigencia(valor, propia: true);

        ((ModoDeEntrega.Sellado)(await ResolverAsync(proceso: false)).Value).Modo.Should().Be(esperado);
        ((ModoDeEntrega.Sellado)(await ResolverAsync(proceso: true)).Value).Modo.Should().Be(esperado);
    }
}
