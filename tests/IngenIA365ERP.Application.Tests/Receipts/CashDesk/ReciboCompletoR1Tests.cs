using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Domain.Enums.Receipts;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.CashDesk;

/// <summary>
/// Feature 015, R1, T0359 (FR-037, FR-187): el recibo completo de R1. Lo que puede pagar una persona pide, a pedido, sus obligaciones como codeudora
/// (<c>includeCoDebtor</c>); <c>F4</c> preselecciona todo lo exigible de todos los productos —créditos con algo exigible hoy y cuentas con cuotas exigibles— y corre
/// todas las simulaciones en una sola acción; un crédito bloqueado no entra; lo que ya estaba (un concepto vario) se conserva; después el cajero quita ítems.
/// Prueba escrita, sin correr.
/// </summary>
public sealed class ReciboCompletoR1Tests
{
    private readonly CajaConContratos _k = new();

    public ReciboCompletoR1Tests()
    {
        _k.Obligaciones.ListarAsync(Arg.Any<SolicitudDeListado>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            var s = ci.Arg<SolicitudDeListado>();
            var propias = new List<ObligacionListadaDto>
            {
                Obligacion(_k.CreditoA, "CR-A", RolEnLaObligacion.Debtor, exigible: 120_000m),
                Obligacion(_k.CreditoB, "CR-B", RolEnLaObligacion.Debtor, exigible: 0m),
            };
            if (s.IncludeAsCoDebtor) propias.Add(Obligacion(Guid.NewGuid(), "CR-C", RolEnLaObligacion.CoDebtor, exigible: 50_000m));
            propias.Add(Obligacion(Guid.NewGuid(), "CR-X", RolEnLaObligacion.Debtor, exigible: 70_000m, bloqueo: "Embargo"));
            return new RespuestaDelContratoDto<ListadoDeObligacionesDto>(ResultadoDelContrato.Answered, "OK", "",
                new ListadoDeObligacionesDto(EstadoDeCartera.Active, s.PersonPublicId, s.Date, propias));
        });
        _k.Cuentas.ListarExigiblesAsync(Arg.Any<SolicitudDeExigibles>(), Arg.Any<CancellationToken>()).Returns(ci =>
            new RespuestaDelContratoDto<ExigiblesDeCuentasDto>(ResultadoDelContrato.Answered, "OK", "", new ExigiblesDeCuentasDto(EstadoDeCartera.Active,
                EstadoDeCartera.Active,
                [
                    Cuota(_k.Aportes, 40_000m, 1), Cuota(_k.Aportes, 40_000m, 2),
                ], [])));
    }

    private static ObligacionListadaDto Obligacion(Guid id, string numero, RolEnLaObligacion rol, decimal exigible, string? bloqueo = null) =>
        new(id, numero, "CONLIB", "Consumo", rol, EstadoDeObligacion.Active, 0, exigible, exigible, 0m, exigible * 10, 0m, 0m,
            [ModalidadDePago.CycleInstallment, ModalidadDePago.FreeAmount], bloqueo is null ? [] : [bloqueo], false, false);

    private static ExigibleDeCuentaDto Cuota(Guid cuenta, decimal pendiente, int orden) =>
        new(Guid.NewGuid(), cuenta, "AP-1", NaturalezaDeCuenta.Contribution, null, "APORTES", Guid.NewGuid(), $"2026-1{orden}", new DateOnly(2026, 11, 5),
            pendiente, pendiente, false, "Saldo", "DESC_APORTES", orden);

    private PreselectPayablesCommandHandler Preseleccion() => new(_k.C.E.Db, _k.C.E.Alcance, _k.Obligaciones, _k.Cuentas);

    private async Task<Guid> OperacionAsync()
    {
        await _k.AbrirAsync();
        return (await _k.Crear().Handle(new CreateCashOperationCommand(_k.Sesion.PublicId, _k.Asociado.PublicId), default)).Value.PublicId;
    }

    [Fact]
    public async Task Lo_que_puede_pagar_trae_las_obligaciones_como_codeudor_solo_a_pedido()
    {
        var sin = await _k.Pagables().Handle(new GetPayablesQuery(_k.Asociado.PublicId), default);
        var con = await _k.Pagables().Handle(new GetPayablesQuery(_k.Asociado.PublicId, IncludeCoDebtor: true), default);

        sin.Value.Obligations!.Obligations.Should().NotContain(o => o.Role == RolEnLaObligacion.CoDebtor);
        con.Value.Obligations!.Obligations.Should().Contain(o => o.Role == RolEnLaObligacion.CoDebtor && o.Number == "CR-C");
    }

    [Fact]
    public async Task F4_preselecciona_todo_lo_exigible_y_corre_todas_las_simulaciones_en_una_accion()
    {
        var op = await OperacionAsync();
        await _k.Items().Handle(new UpdateCashOperationItemsCommand(op, [new ItemPedidoDto(ApplicationTarget.MiscConcept, 5_000m, MiscConceptPublicId: _k.Concepto.PublicId)]),
            default);
        _k.Obligaciones.ClearReceivedCalls();

        var r = await Preseleccion().Handle(new PreselectPayablesCommand(op, IncludeCoDebtor: true), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        var items = r.Value.Items;
        items.Should().Contain(i => i.Target == ApplicationTarget.Loan && i.ProductPublicId == _k.CreditoA && i.Amount == 120_000m);
        items.Should().NotContain(i => i.ProductPublicId == _k.CreditoB, "no tiene nada exigible hoy");
        items.Where(i => i.Target == ApplicationTarget.Loan).Should().HaveCount(2, "el crédito bloqueado no se preselecciona; el del codeudor sí");
        items.Should().ContainSingle(i => i.Target == ApplicationTarget.Account).Which.InstallmentPublicIds.Should().HaveCount(2);
        items.Should().Contain(i => i.Target == ApplicationTarget.MiscConcept, "lo que ya estaba se conserva");
        items.Where(i => i.Target is ApplicationTarget.Loan or ApplicationTarget.Account).Should().OnlyContain(i => i.SimulationId != null, "todas simuladas");
        await _k.Obligaciones.Received(2).SimularAsync(Arg.Any<SolicitudDeSimulacion>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Despues_de_preseleccionar_el_cajero_quita_items()
    {
        var op = await OperacionAsync();
        var preseleccion = (await Preseleccion().Handle(new PreselectPayablesCommand(op), default)).Value;

        var sinCuenta = preseleccion.Items.Where(i => i.Target != ApplicationTarget.Account)
            .Select(i => new ItemPedidoDto(i.Target, i.Amount, i.ProductPublicId, i.BeneficiaryPersonPublicId, i.LoanModality, i.AccountModality, ItemId: i.ItemId))
            .ToList();
        var r = await _k.Items().Handle(new UpdateCashOperationItemsCommand(op, sinCuenta), default);

        r.Value.Items.Should().NotContain(i => i.Target == ApplicationTarget.Account);
        r.Value.Items.Should().ContainSingle(i => i.ProductPublicId == _k.CreditoA);
    }
}
