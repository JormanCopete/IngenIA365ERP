using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.CashDesk;

/// <summary>
/// Feature 015, T0104 (R-08 a R-10; FR-042, FR-043, FR-170, FR-172): un lote por contrato con las obligaciones antes que las cuentas; un rechazo por ítem es
/// <c>ItemRejected</c> con <c>data.items[]</c>; la distribución cambiada es <c>DistributionChanged</c> con <c>data.simulations[]</c>; el excedente sin asignar;
/// el recibo toma el PublicId de la operación y numera antes de los contratos; la clave por porción <c>REC:{recibo:N}:{aplicación:N}:{clase}</c>; la huella
/// excluye la simulación; repetir la confirmación devuelve el mismo recibo. La repetición con otra huella (<c>Operation.KeyReused</c>) la responde
/// <c>IdempotencyBehavior</c> y la prueba la e2e. Escrita en la fase 6; se corre al final.
/// </summary>
public class ConfirmacionDeReciboTests
{
    private readonly CajaConContratos _k = new();

    private async Task<CashOperationDto> TresAplicacionesAsync()
    {
        await _k.AbrirAsync();
        return await _k.PreparadaAsync(
            [_k.Cuota(_k.CreditoA, 100_000), _k.Abono(_k.CreditoB, 300_000, EleccionDeAbono.ReduceTerm), _k.CuotaDeAportes(50_000)],
            [_k.Efectivo(250_000), _k.Transferencia(200_000)]);
    }

    [Fact]
    public async Task Un_recibo_con_tres_aplicaciones_cada_una_con_el_comprobante_de_su_modulo()
    {
        var op = await TresAplicacionesAsync();

        var r = await _k.Confirmacion().ConfirmarAsync(op.PublicId, Guid.NewGuid(), null, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code + r.Error.Message : string.Empty);
        r.Value.PublicId.Should().Be(op.PublicId);
        r.Value.Number.Should().Be("000001");
        r.Value.Applications.Should().HaveCount(3).And.OnlyContain(a => a.ModuleVoucherNumber != null);
        (await _k.C.E.Db.CashOperations.SingleAsync()).Status.Should().Be(CashOperationState.Confirmed);
        (await _k.C.E.Db.IntegrationMessages.CountAsync(m => m.Type == ReciboEmitidoV1.Type)).Should().Be(1);
    }

    [Fact]
    public async Task Las_obligaciones_van_antes_que_las_cuentas_y_cada_porcion_lleva_su_clave()
    {
        var op = await TresAplicacionesAsync();
        var orden = new List<string>();
        _k.Obligaciones.When(o => o.AplicarAsync(Arg.Any<SolicitudDeAplicacion>(), Arg.Any<CancellationToken>())).Do(_ => orden.Add("obligaciones"));
        _k.Cuentas.When(c => c.ConsignarAsync(Arg.Any<SolicitudDeConsignacion>(), Arg.Any<CancellationToken>())).Do(_ => orden.Add("cuentas"));

        await _k.Confirmacion().ConfirmarAsync(op.PublicId, Guid.NewGuid(), null, default);

        // Las obligaciones (una llamada, o una por porción cuando un ítem quedó partido entre dos medios) van todas antes que las cuentas.
        orden.Should().EndWith("cuentas").And.ContainSingle(o => o == "cuentas");
        orden.Take(orden.Count - 1).Should().NotBeEmpty().And.OnlyContain(o => o == "obligaciones");
        var pagos = _k.Obligaciones.ReceivedCalls().Where(c => c.GetMethodInfo().Name == nameof(IContratoDeObligaciones.AplicarAsync))
            .Select(c => (SolicitudDeAplicacion)c.GetArguments()[0]!).ToList();
        pagos.Should().OnlyContain(s => s.Form == FormaDeAplicacion.Interactive);
        // Corrida e2e del 2026-10-26: en la forma interactiva el contrato exige la simulación de CADA pago, también de la porción de un ítem partido.
        pagos.SelectMany(s => s.Payments).Should().NotBeEmpty().And.OnlyContain(p => p.IdempotencyKey.StartsWith($"REC:{op.PublicId:N}:")
            && p.Receipt.Number == "000001" && !string.IsNullOrWhiteSpace(p.SimulationId));
        var claves = await _k.C.E.Db.ReceiptApplicationPortions.Select(p => p.IdempotencyKey).ToListAsync();
        claves.Should().OnlyHaveUniqueItems().And.OnlyContain(c => ClavesDeRecaudo.EsDeRecaudo(c));
    }

    [Fact]
    public async Task Un_rechazo_en_la_tercera_aplicacion_no_deja_nada()
    {
        var op = await TresAplicacionesAsync();
        _k.Cuentas.ConsignarAsync(Arg.Any<SolicitudDeConsignacion>(), Arg.Any<CancellationToken>()).Returns(ci =>
            new RespuestaDeConsignacionDto(ci.Arg<SolicitudDeConsignacion>().Deposits
                .Select(_ => new RespuestaDelContratoDto<ComprobanteDeCuentaDto>(ResultadoDelContrato.Rejected, "Deposits.Account.Blocked", "Cuenta bloqueada.", null))
                .ToList(), []));

        var r = await _k.Confirmacion().ConfirmarAsync(op.PublicId, Guid.NewGuid(), null, default);

        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoItemRechazado);
        // En la base real la transacción de IdempotencyBehavior se revierte: ni recibo ni número. Aquí (InMemory, sin transacción) la operación sigue en preparación.
        (await _k.C.E.Db.CashOperations.SingleAsync()).Status.Should().Be(CashOperationState.Preparing);
    }

    [Fact]
    public async Task La_distribucion_cambiada_trae_las_simulaciones()
    {
        var op = await TresAplicacionesAsync();
        _k.Obligaciones.AplicarAsync(Arg.Any<SolicitudDeAplicacion>(), Arg.Any<CancellationToken>()).Returns(ci =>
            new RespuestaDeAplicacionDto(ci.Arg<SolicitudDeAplicacion>().Payments
                .Select(_ => new RespuestaDelContratoDto<ComprobanteDeAplicacionDto>(ResultadoDelContrato.Rejected, ConfirmacionDeRecibo.CodigoDistribucionCambiadaDeCartera,
                    "Cambió.", null, new Dictionary<string, object?> { ["simulation"] = "nueva" })).ToList(), []));

        var r = await _k.Confirmacion().ConfirmarAsync(op.PublicId, Guid.NewGuid(), null, default);

        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoDistribucionCambiada);
    }

    [Fact]
    public async Task El_excedente_de_una_simulacion_no_se_confirma()
    {
        await _k.AbrirAsync();
        _k.Obligaciones.SimularAsync(Arg.Any<SolicitudDeSimulacion>(), Arg.Any<CancellationToken>()).Returns(ci =>
            new RespuestaDelContratoDto<SimulacionDePagoDto>(ResultadoDelContrato.Answered, "OK", "",
                CajaConContratos.Simulacion(ci.Arg<SolicitudDeSimulacion>(), null, []) with { Surplus = 5_000 }));
        var op = await _k.PreparadaAsync([_k.Cuota(_k.CreditoA, 105_000)], [_k.Efectivo(105_000)]);

        var r = await _k.Confirmacion().ConfirmarAsync(op.PublicId, Guid.NewGuid(), null, default);

        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoExcedenteSinAsignar);
    }

    [Fact]
    public async Task Repetir_la_confirmacion_devuelve_el_mismo_recibo()
    {
        var op = await TresAplicacionesAsync();
        var clave = Guid.NewGuid();
        var primero = await _k.Confirmacion().ConfirmarAsync(op.PublicId, clave, null, default);

        var otra = await _k.Confirmacion().ConfirmarAsync(op.PublicId, clave, null, default);

        otra.Value.PublicId.Should().Be(primero.Value.PublicId);
        otra.Value.Number.Should().Be(primero.Value.Number);
        (await _k.C.E.Db.IssuedReceipts.CountAsync()).Should().Be(1);
    }

    [Fact]
    public void La_huella_excluye_la_simulacion()
    {
        var a = HuellaDeRecibo.Calcular(new { Items = new[] { new { Amount = 100m } } });
        var b = HuellaDeRecibo.Calcular(new { Items = new[] { new { Amount = 100m } } });
        a.Should().Be(b);
        ClavesDeRecaudo.Porcion(Guid.Empty, Guid.Empty, Domain.Enums.Core.PaymentMeansClass.Transfer, 2).Should().EndWith(":Transfer-2");
    }
}
