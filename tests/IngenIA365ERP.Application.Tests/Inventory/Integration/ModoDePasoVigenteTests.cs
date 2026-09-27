using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Inventory;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using MediatR;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Integration;

/// <summary>
/// Feature 012, decisión del dueño del 2026-09-26: sin contabilidad iniciada, el modo de paso por defecto es «no pasa». Antes una
/// cooperativa nueva no confirmaba ningún documento: el defecto era «en línea» y la validación previa rechazaba todo con
/// <c>Accounting.NotInitialized</c>. Un modo guardado a propósito se respeta, y con la contabilidad iniciada el defecto sigue «en línea».
/// </summary>
public class ModoDePasoVigenteTests
{
    private static readonly DateOnly Hoy = new(2026, 3, 15);

    private static ILectorDeParametros Lector(string modo, bool guardado)
    {
        var definicion = ParametrosDeInventario.Definiciones.Single(d => d.Clave == ParametrosDeInventario.ContabilidadModoDePaso);
        var vigencia = guardado
            ? new VigenciaDeParametro(Guid.NewGuid(), ParametrosDeInventario.Modulo, ParametrosDeInventario.ContabilidadModoDePaso,
                ParameterScopeKind.None, 0, modo, new DateOnly(2026, 1, 1), null, "prueba", null, "test", DateTime.UtcNow)
            : null;
        var lector = Substitute.For<ILectorDeParametros>();
        lector.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.ContabilidadModoDePaso, Arg.Any<DateOnly>(),
                Arg.Any<ParameterScopeKind>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ValorDeParametro(definicion, modo, modo, vigencia)));
        return lector;
    }

    private static IContabilidadParaInventario Contabilidad(bool sinIniciar)
    {
        var contabilidad = Substitute.For<IContabilidadParaInventario>();
        contabilidad.SinIniciarAsync(Arg.Any<CancellationToken>()).Returns(sinIniciar);
        return contabilidad;
    }

    private static async Task<string> LeerAsync(ILectorDeParametros lector, IContabilidadParaInventario? contabilidad)
    {
        var r = await ModoDePasoVigente.LeerAsync(lector, contabilidad, Hoy, ParameterScopeKind.DocumentType, 7, default);
        r.IsSuccess.Should().BeTrue();
        return r.Value;
    }

    [Fact]
    public async Task Sin_vigencia_y_sin_contabilidad_iniciada_no_pasa() =>
        (await LeerAsync(Lector("EnLinea", guardado: false), Contabilidad(sinIniciar: true))).Should().Be(ModoDePasoVigente.NoPasa);

    [Fact]
    public async Task Sin_vigencia_y_con_contabilidad_iniciada_sigue_en_linea() =>
        (await LeerAsync(Lector("EnLinea", guardado: false), Contabilidad(sinIniciar: false))).Should().Be(ModoDePasoVigente.EnLinea);

    [Theory]
    [InlineData("EnLinea")]
    [InlineData("PorLotes")]
    public async Task Un_modo_guardado_se_respeta_aunque_falte_iniciar_la_contabilidad(string modo) =>
        (await LeerAsync(Lector(modo, guardado: true), Contabilidad(sinIniciar: true))).Should().Be(modo);

    [Fact]
    public async Task Sin_puerto_contable_se_queda_con_lo_leido() =>
        (await LeerAsync(Lector("EnLinea", guardado: false), null)).Should().Be(ModoDePasoVigente.EnLinea);

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task El_adaptador_mira_la_configuracion_inicial(bool iniciada, bool sinIniciar)
    {
        var datos = new ContabilidadTestData(iniciada);
        var adaptador = new ContabilidadParaInventario(Substitute.For<ISender>(), datos.Db);
        (await adaptador.SinIniciarAsync(default)).Should().Be(sinIniciar);
    }
}
