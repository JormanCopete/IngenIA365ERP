using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.Integration;

/// <summary>
/// T0029, parte de consumo (feature 013; T17, C18, CH12; contracts/mensajes.md §14.3, §14.4): <c>ResultadoDeConsumo.ValidationFailed</c>
/// lleva la entrega a <c>Lending</c> directo desde <c>Pending</c> o <c>InBatch</c> a <c>ValidationFailed</c> —terminal— con el crédito
/// como referencia, el intento <c>DeliveryAttemptOutcome.ValidationFailed = 5</c> y la alerta <c>Integracion.ValidacionFallida</c> a
/// Cartera y al módulo de origen. En otro destino es un defecto. Los rechazos de lo que no es de Inventario van a quien reprocesa la
/// bandeja de Cartera (T19); los de Inventario conservan los destinatarios del tipo de alerta.
/// </summary>
public class ConsumoConValidacionFallidaTests
{
    private readonly BandejaDePrueba _b = new();
    private readonly IAlertas _alertas = Substitute.For<IAlertas>();
    private readonly List<AlertaALevantar> _levantadas = [];

    public ConsumoConValidacionFallidaTests()
    {
        _alertas.LevantarAsync(Arg.Do<AlertaALevantar>(_levantadas.Add), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AlertaLevantada(Guid.NewGuid(), DesenlaceDeAlerta.Levantada, 2, false)));
    }

    private RegisterDeliveryResultCommandHandler Handler() => new(
        _b.Db, _b.ActorActual, _alertas, Options.Create(new ReintentosDeIntegracion()), NullLogger<RegisterDeliveryResultCommandHandler>.Instance);

    private static RegisterDeliveryResultCommand Comando(string destino, Guid mensaje, ResultadoDeConsumo resultado) => new(
        destino, [mensaje], resultado, 0, BandejaDePrueba.Ahora, BandejaDePrueba.Ahora.AddMilliseconds(120), "api-7f9c");

    [Theory]
    [InlineData(DeliveryStatus.Pending)]
    [InlineData(DeliveryStatus.InBatch)]
    public async Task ValidationFailed_deja_la_entrega_terminal_con_el_credito_y_el_intento_5(DeliveryStatus inicial)
    {
        var venta = _b.Mensaje(tipo: "VentaACreditoRegistrada", clave: $"Confirmation:{Guid.NewGuid():N}", numero: "PV01-2207");
        _b.Entrega(venta, inicial, IntegrationDestinations.Lending, modo: DeliveryMode.Always,
            lote: inicial == DeliveryStatus.InBatch ? _b.Lote(destino: IntegrationDestinations.Lending) : null);
        var credito = Guid.NewGuid();

        var r = await Handler().Handle(Comando(IntegrationDestinations.Lending, venta.PublicId,
            new ResultadoDeConsumo.ValidationFailed(credito, "CR-0002001", "Lending.Sale.QuotaExceeded", "El deudor excede su cupo.")), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Status.Should().Be(DeliveryStatus.ValidationFailed);
        _b.Olvidar();
        var entrega = await _b.Db.IntegrationMessageDeliveries.SingleAsync();
        entrega.Status.Should().Be(DeliveryStatus.ValidationFailed);
        entrega.ResultReference.Should().Be(credito.ToString("D"));
        entrega.ResultVoucherNumber.Should().Be("CR-0002001");
        entrega.ResultVoucherTypeCode.Should().BeNull();
        entrega.LastErrorCode.Should().Be("Lending.Sale.QuotaExceeded");
        entrega.LastErrorMessage.Should().Be("El deudor excede su cupo.");
        entrega.NextAttemptAt.Should().BeNull();
        entrega.ProcessedAt.Should().Be(BandejaDePrueba.Ahora.AddMilliseconds(120));

        var intento = await _b.Db.IntegrationDeliveryAttempts.SingleAsync();
        intento.Outcome.Should().Be(DeliveryAttemptOutcome.ValidationFailed);
        ((int)intento.Outcome).Should().Be(5);

        _levantadas.Should().ContainSingle();
        _levantadas[0].TypeCode.Should().Be(TiposDeAlerta.ValidacionFallida);
        _levantadas[0].RecipientPermissions.Should().BeEquivalentTo(["Lending.Messages.Reprocess", "Inventory.Messages.Reprocess"],
            "una validación fallida llega a Cartera y a Comercial, el módulo de origen de la venta");
    }

    [Fact]
    public async Task Una_validacion_fallida_ya_registrada_no_se_vuelve_a_escribir()
    {
        var venta = _b.Mensaje(tipo: "VentaACreditoRegistrada", clave: $"Confirmation:{Guid.NewGuid():N}");
        _b.Entrega(venta, DeliveryStatus.ValidationFailed, IntegrationDestinations.Lending, modo: DeliveryMode.Always);

        var r = await Handler().Handle(Comando(IntegrationDestinations.Lending, venta.PublicId,
            new ResultadoDeConsumo.ValidationFailed(Guid.NewGuid(), "CR-1", "X", "Y")), default);

        r.Value.Registrado.Should().BeFalse("ValidationFailed es terminal");
        (await _b.Db.IntegrationDeliveryAttempts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ValidationFailed_en_otro_destino_es_un_defecto()
    {
        var m = _b.Mensaje();
        _b.Entrega(m);

        var registrar = () => Handler().Handle(Comando(IntegrationDestinations.Accounting, m.PublicId,
            new ResultadoDeConsumo.ValidationFailed(Guid.NewGuid(), "CR-1", "X", "Y")), default);

        await registrar.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Lending*");
        (await _b.Db.IntegrationDeliveryAttempts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public void El_validador_exige_codigo_y_credito()
    {
        var validador = new RegisterDeliveryResultCommandValidator();

        validador.Validate(Comando(IntegrationDestinations.Lending, Guid.NewGuid(),
            new ResultadoDeConsumo.ValidationFailed(Guid.Empty, "CR-1", "X", "Y"))).IsValid.Should().BeFalse();
        validador.Validate(Comando(IntegrationDestinations.Lending, Guid.NewGuid(),
            new ResultadoDeConsumo.ValidationFailed(Guid.NewGuid(), "CR-1", " ", "Y"))).IsValid.Should().BeFalse();
        validador.Validate(Comando(IntegrationDestinations.Lending, Guid.NewGuid(),
            new ResultadoDeConsumo.ValidationFailed(Guid.NewGuid(), "CR-1", "X", "Y"))).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Un_rechazo_de_Cartera_llega_a_quien_reprocesa_su_bandeja_y_uno_de_Inventario_a_los_del_tipo()
    {
        var deCartera = _b.Mensaje(tipo: "PagoAplicado", numero: "PAG-2026-004811");
        ConModulo(deCartera, ModulosDeOrigen.Cartera);
        _b.Entrega(deCartera);
        var deInventario = _b.Mensaje(numero: "CO-9");
        _b.Entrega(deInventario);

        await Handler().Handle(Comando(IntegrationDestinations.Accounting, deCartera.PublicId,
            new ResultadoDeConsumo.Rejected("Accounting.LendingRule.Missing", "Falta la regla.")), default);
        await Handler().Handle(Comando(IntegrationDestinations.Accounting, deInventario.PublicId,
            new ResultadoDeConsumo.Rejected("Accounting.InventoryRule.Missing", "Falta la regla.")), default);

        _levantadas.Should().HaveCount(2);
        _levantadas[0].RecipientPermissions.Should().BeEquivalentTo(["Lending.Messages.Reprocess", "Accounting.LendingBatches.Run"]);
        _levantadas[1].RecipientPermissions.Should().BeNull("lo de Inventario conserva los destinatarios configurados en el tipo de alerta");
    }

    /// <summary>El mensaje de la bandeja de prueba nace de Inventario; aquí se vuelve de otro módulo (sólo prueba).</summary>
    private void ConModulo(Domain.Entities.Integration.Transactions.IntegrationMessage mensaje, string modulo)
    {
        mensaje.GetType().GetProperty(nameof(mensaje.OriginModule))!.SetValue(mensaje, modulo);
        _b.Db.SaveChanges();
    }
}
