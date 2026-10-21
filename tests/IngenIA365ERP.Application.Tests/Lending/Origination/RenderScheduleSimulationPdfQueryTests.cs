using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Origination;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Origination;

/// <summary>
/// Feature 013, T0367 (C87, T80; api.md §4.1): el PDF de la simulación. Lleva la leyenda «Simulación – no constituye aprobación», no lleva número
/// ni código (no es un documento emitido) y, fuera de producción —o sin ambiente declarado—, la marca «AMBIENTE DE PRUEBAS – SIN VALIDEZ». Las
/// mismas reglas que la simulación, y no escribe nada.
/// </summary>
public sealed class RenderScheduleSimulationPdfQueryTests : IAsyncLifetime
{
    private OriginacionDePrueba _o = null!;
    private readonly IRenderizadorDeSimulacion _renderizador = Substitute.For<IRenderizadorDeSimulacion>();
    private readonly List<SimulacionParaImprimir> _pintadas = [];

    public async Task InitializeAsync()
    {
        _o = await OriginacionDePrueba.CrearAsync(activada: false);
        _renderizador.Render(Arg.Do<SimulacionParaImprimir>(_pintadas.Add)).Returns([0x25, 0x50, 0x44, 0x46]);
    }

    public Task DisposeAsync()
    {
        _o.Dispose();
        return Task.CompletedTask;
    }

    private RenderScheduleSimulationPdfQueryHandler Handler(bool? produccion)
    {
        IAmbienteDeDespliegue? ambiente = null;
        if (produccion is { } p)
        {
            ambiente = Substitute.For<IAmbienteDeDespliegue>();
            ambiente.EsProduccion.Returns(p);
        }
        return new RenderScheduleSimulationPdfQueryHandler(_o.Simulacion(), _renderizador, _o.G.Reloj, null, ambiente);
    }

    private RenderScheduleSimulationPdfQuery Pedido(Guid? persona = null) =>
        new(new ScheduleSimulationInput(_o.Linea.PublicId, 12_000_000m, 36, PersonPublicId: persona));

    [Fact]
    public async Task El_pdf_lleva_la_leyenda_y_ningun_numero_ni_codigo()
    {
        var r = await Handler(produccion: true).Handle(Pedido(_o.Ana.PublicId), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Contenido.Should().NotBeEmpty();
        r.Value.NombreArchivo.Should().Be("simulacion-conlib-20261201.pdf");
        SimulacionParaImprimir.Leyenda.Should().Be("Simulación – no constituye aprobación");
        var pintada = _pintadas.Should().ContainSingle().Subject;
        pintada.AmbienteDePruebas.Should().BeFalse("en producción no lleva la marca de pruebas");
        pintada.Persona.Should().Be("Ana Solicitante");
        pintada.Simulacion.Installments.Should().HaveCount(36);
        typeof(SimulacionParaImprimir).GetProperties().Select(p => p.Name)
            .Should().NotContain(n => n.Contains("Number", StringComparison.OrdinalIgnoreCase) || n.Contains("Code", StringComparison.OrdinalIgnoreCase),
                "una simulación no es un documento emitido: sin número ni código de verificación (T80)");
        typeof(ScheduleSimulationDto).GetProperties().Select(p => p.Name).Should().NotContain(["Number", "VerificationCode"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public async Task Fuera_de_produccion_o_sin_ambiente_lleva_la_marca_de_pruebas(bool? produccion)
    {
        (await Handler(produccion).Handle(Pedido(), default)).IsSuccess.Should().BeTrue();

        _pintadas.Should().ContainSingle().Which.AmbienteDePruebas.Should().BeTrue();
        SimulacionParaImprimir.MarcaDePruebas.Should().Be("AMBIENTE DE PRUEBAS – SIN VALIDEZ");
    }

    [Fact]
    public async Task Las_reglas_son_las_de_la_simulacion_y_no_pinta_lo_que_no_se_puede_simular()
    {
        var r = await Handler(true).Handle(new RenderScheduleSimulationPdfQuery(new ScheduleSimulationInput(_o.Linea.PublicId, 99_000_000m, 36)), default);

        r.Error.Code.Should().Be("Lending.Schedule.OutOfLineRange");
        _pintadas.Should().BeEmpty();
        _o.Db.ChangeTracker.Entries().Where(e => e.State != Microsoft.EntityFrameworkCore.EntityState.Unchanged).Should().BeEmpty();
    }
}
