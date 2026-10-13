using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Integration.Comercial;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ScreeningOutcome = IngenIA365ERP.Domain.Enums.Lending.ScreeningOutcome;

namespace IngenIA365ERP.Application.Tests.Lending.Integration;

/// <summary>
/// Feature 013, US6, T0569 y T0586 (nombre de quickstart §1.5; mensajes.md §15.1; T17, C16): <see cref="DestinoCartera"/> completo —acepta toda versión de
/// la familia de Comercial y el <c>ResultadoDelGiro</c> v1, manda cada unidad al comando de su familia, una unidad por trabajo, sin totales y sin lanzar
/// nunca— y, por el camino del despachador (las entregas elegibles del destino <c>Lending</c> con la fila <c>CAR</c> de la semilla, el consumo y el
/// registro del resultado), las ventas a crédito ya llegan a Cartera: la entrega queda <c>Processed</c> con el crédito como referencia o
/// <c>ValidationFailed</c>, y nada que el destino no acepte queda rechazado.
/// </summary>
public sealed class DestinoCarteraTests : IAsyncLifetime
{
    private VentasACreditoDePrueba _v = null!;
    private readonly ISender _sender = Substitute.For<ISender>();

    public async Task InitializeAsync()
    {
        _v = await VentasACreditoDePrueba.CrearAsync();
        _sender.Send(Arg.Any<ConsumeCommercialCreditMessageCommand>(), Arg.Any<CancellationToken>())
            .Returns(ci => _v.Consumir().Handle(ci.Arg<ConsumeCommercialCreditMessageCommand>(), default));
    }

    public Task DisposeAsync()
    {
        _v.Dispose();
        return Task.CompletedTask;
    }

    private DestinoCartera Destino(ISender? sender = null) => new(sender ?? _sender, NullLogger<DestinoCartera>.Instance);

    private static UnidadDeConsumo Unidad(string modulo, params Guid[] mensajes) =>
        new(IntegrationDestinations.Lending, Guid.NewGuid(), ClavesDeEvento.Confirmacion, mensajes, null, 0) { OriginModule = modulo };

    [Theory]
    [InlineData("VentaACreditoRegistrada", 1, true)]
    [InlineData("VentaACreditoRegistrada", 2, true)]
    [InlineData("VentaACreditoRegistrada", 3, true)]
    [InlineData("AjusteDeVentaACredito", 1, true)]
    [InlineData("AjusteDeVentaACredito", 2, true)]
    [InlineData("ResultadoDelGiro", 1, true)]
    [InlineData("ResultadoDelGiro", 2, false)]
    [InlineData("ResultadoDeDescuentos", 1, false)]
    [InlineData("VentaFacturada", 1, false)]
    public void Acepta_toda_version_de_la_familia_de_Comercial_y_el_resultado_del_giro(string tipo, int version, bool acepta) =>
        Destino().Acepta(tipo, version).Should().Be(acepta);

    [Fact]
    public async Task Manda_lo_de_Comercial_y_lo_de_Tesoreria_al_comando_de_su_familia()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<IRequest<Result<ResultadoDeConsumo>>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Processed(null, null, null)));
        var venta = Unidad(ModulosDeOrigen.Inventario, Guid.NewGuid());
        var giro = Unidad(ModulosDeOrigen.Tesoreria, Guid.NewGuid());

        await Destino(sender).ConsumirAsync(TrabajoDeConsumo.DeUnaUnidad(venta), default);
        await Destino(sender).ConsumirAsync(TrabajoDeConsumo.DeUnaUnidad(giro), default);

        await sender.Received(1).Send(Arg.Is<IRequest<Result<ResultadoDeConsumo>>>(c => c is ConsumeCommercialCreditMessageCommand), Arg.Any<CancellationToken>());
        await sender.Received(1).Send(Arg.Is<IRequest<Result<ResultadoDeConsumo>>>(c => c is ConsumeTreasuryResultCommand), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Nunca_lanza_una_excepcion_es_Retry_y_un_fallo_del_comando_Rejected()
    {
        var falla = Substitute.For<ISender>();
        falla.Send(Arg.Any<IRequest<Result<ResultadoDeConsumo>>>(), Arg.Any<CancellationToken>()).ThrowsAsync(new TimeoutException("base ocupada"));
        var rechaza = Substitute.For<ISender>();
        rechaza.Send(Arg.Any<IRequest<Result<ResultadoDeConsumo>>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ResultadoDeConsumo>(new Error("Integration.Message.NotFound", "no está")));

        (await Destino(falla).ConsumirAsync(TrabajoDeConsumo.DeUnaUnidad(Unidad(ModulosDeOrigen.Inventario, Guid.NewGuid())), default))
            .Single().Resultado.Should().BeOfType<ResultadoDeConsumo.Retry>();
        (await Destino(rechaza).ConsumirAsync(TrabajoDeConsumo.DeUnaUnidad(Unidad(ModulosDeOrigen.Inventario, Guid.NewGuid())), default))
            .Single().Resultado.Should().BeOfType<ResultadoDeConsumo.Rejected>().Which.Code.Should().Be("Integration.Message.NotFound");
    }

    [Fact]
    public async Task Una_unidad_por_trabajo_y_sin_totales()
    {
        var venta = _v.NuevaVenta();
        var otra = _v.NuevaVenta();
        await _v.EmitirAsync(venta, VentasACreditoDePrueba.ContenidoV2(venta));
        await _v.EmitirAsync(otra, VentasACreditoDePrueba.ContenidoV2(otra));
        var leidos = await new MensajesEntrantes(_v.Db).LeerAsync(
            await _v.Db.IntegrationMessages.AsNoTracking().Select(m => m.PublicId).ToListAsync(), IntegrationDestinations.Lending, default);

        var trabajos = Destino().PlanearLote(leidos.Value);

        trabajos.Should().HaveCount(2).And.OnlyContain(t => t.Unidades.Count == 1 && t.ClaveDeGrupo == null);
        (await ((IDestinoDeMensajes)Destino()).TotalesDelLoteAsync(Guid.NewGuid(), default)).Should().Be(TotalesDeLoteEnDestino.Cero);
    }

    // ---------------------------------------------------------------------------------------------- por el despachador --

    private void SembrarCar()
    {
        var p = new IntegrationParticipant
        {
            Code = "CAR", Class = IntegrationParticipantClass.Lending, Role = IntegrationParticipantRole.MessageDestination,
            Destination = IntegrationDestinations.Lending, IsEnabled = true, ValidFrom = VentasACreditoDePrueba.Hoy.AddDays(-30), Reason = "Activación de Cartera",
            RegisteredByKind = ActorKind.Process, RegisteredByName = "Migración PlataformaParaCartera",
        };
        foreach (var (contrato, version) in new[] { ("VentaACreditoRegistrada", (short)1), ("VentaACreditoRegistrada", (short)2), ("AjusteDeVentaACredito", (short)1),
                     ("ResultadoDelGiro", (short)1) })
            p.Adoptions.Add(new IntegrationParticipantAdoption { Participant = p, ContractName = contrato, Version = version, ValidFrom = p.ValidFrom, Reason = "Semilla" });
        _v.Db.IntegrationParticipants.Add(p);
        _v.Db.SaveChanges();
        _v.Db.ChangeTracker.Clear();
    }

    /// <summary>Una pasada del despachador sobre el destino Lending: elegibles, consumo y registro del resultado (lo que hace DespachadorDeMensajes).</summary>
    private async Task<IReadOnlyList<(UnidadDeConsumo Unidad, ResultadoDeConsumo Resultado)>> PasadaAsync()
    {
        var destino = Destino();
        var registro = new RegistroDeIntegracion(_v.Db, [destino], Substitute.For<IAmbienteDeDespliegue>());
        (await registro.DestinoHabilitadoAsync(IntegrationDestinations.Lending, VentasACreditoDePrueba.Hoy, default)).Should().BeTrue(
            "con la fila CAR de la semilla y la familia de Comercial aceptada, el destino Lending cuenta");
        var unidades = await new EntregasElegibles(_v.Db, [destino]).EnLineaAsync(IntegrationDestinations.Lending, _v.G.UtcNow, 50, default);
        var salida = new List<(UnidadDeConsumo, ResultadoDeConsumo)>();
        foreach (var unidad in unidades)
        {
            var r = (await destino.ConsumirAsync(TrabajoDeConsumo.DeUnaUnidad(unidad), default)).Single();
            _v.Db.ChangeTracker.Clear();
            var registrado = await new RegisterDeliveryResultCommandHandler(_v.Db, _v.G.Actor, _v.D.Alertas, Options.Create(new ReintentosDeIntegracion()),
                    NullLogger<RegisterDeliveryResultCommandHandler>.Instance)
                .Handle(new RegisterDeliveryResultCommand(IntegrationDestinations.Lending, unidad.MessagePublicIds, r.Resultado, unidad.IntentosLeidos,
                    _v.G.UtcNow, _v.G.UtcNow, "prueba"), default);
            registrado.IsSuccess.Should().BeTrue(registrado.IsFailure ? registrado.Error.Message : string.Empty);
            _v.Db.ChangeTracker.Clear();
            salida.Add((unidad, r.Resultado));
        }
        return salida;
    }

    [Fact]
    public async Task Por_el_despachador_la_venta_a_credito_ya_llega_a_Cartera_y_queda_procesada()
    {
        SembrarCar();
        var venta = _v.NuevaVenta();
        var mensaje = await _v.EmitirAsync(venta, VentasACreditoDePrueba.ContenidoV2(venta));

        var pasada = await PasadaAsync();

        pasada.Should().ContainSingle(p => p.Unidad.MessagePublicIds.Contains(mensaje));
        var credito = await _v.CreditoDeAsync(venta);
        var entrega = await _v.Db.IntegrationMessageDeliveries.AsNoTracking()
            .SingleAsync(e => e.Destination == IntegrationDestinations.Lending && _v.Db.IntegrationMessages.Any(m => m.Id == e.MessageId && m.PublicId == mensaje));
        (entrega.Status, entrega.ResultReference, entrega.ResultVoucherNumber).Should()
            .Be((DeliveryStatus.Processed, credito.PublicId.ToString("D"), credito.Number));
    }

    [Fact]
    public async Task Por_el_despachador_una_validacion_fallida_deja_la_entrega_ValidationFailed()
    {
        SembrarCar();
        _v.O.ListasDe[_v.Ana.Id] = ScreeningOutcome.Match;
        var venta = _v.NuevaVenta();
        var mensaje = await _v.EmitirAsync(venta, VentasACreditoDePrueba.ContenidoV2(venta, pendiente: true));

        await PasadaAsync();

        var entrega = await _v.Db.IntegrationMessageDeliveries.AsNoTracking()
            .SingleAsync(e => e.Destination == IntegrationDestinations.Lending && _v.Db.IntegrationMessages.Any(m => m.Id == e.MessageId && m.PublicId == mensaje));
        (entrega.Status, entrega.LastErrorCode).Should().Be((DeliveryStatus.ValidationFailed, "Compliance.RestrictiveList.Match"));
    }

    [Fact]
    public async Task Por_el_despachador_nada_de_lo_que_acepta_queda_rechazado_en_el_despachador()
    {
        SembrarCar();
        var venta = _v.NuevaVenta();
        await _v.EmitirAsync(venta, VentasACreditoDePrueba.ContenidoV2(venta));
        var otra = _v.NuevaVenta();
        await _v.EmitirAsync(otra, VentasACreditoDePrueba.ContenidoV2(otra));

        var pasada = await PasadaAsync();

        pasada.Should().HaveCount(2).And.OnlyContain(p => p.Resultado is ResultadoDeConsumo.Processed);
        (await _v.Db.IntegrationMessageDeliveries.AsNoTracking().Where(e => e.Destination == IntegrationDestinations.Lending)
            .Select(e => e.LastErrorCode).ToListAsync()).Should().NotContain("Integration.VersionNotAccepted");
    }
}
