using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.Settlements;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Application.Tests.Common.Integration.Lending;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.Settlements;

/// <summary>
/// Feature 013 (T0105; R104, G4, FR-088; contracts/interfaces.md §6.2): la propuesta de descuentos de Cartera de la definitiva
/// sale de <see cref="IConsultaDeObligaciones"/>: <c>SaldoTotal</c> → total para cancelar, <c>SoloCuotasCausadas</c> → exigible,
/// <c>NoProponer</c> → nada; todas las vivas como deudor (castigadas incluidas), nunca las de codeudor; el desglose conserva sus
/// claves; y si Cartera falla o no responde, propuesta vacía con aviso.
/// </summary>
public class PropuestaDeDescuentosDeCarteraTests
{
    private static readonly DateOnly Hoy = new(2026, 9, 20);
    private readonly ConsultaDeObligacionesDePrueba _consulta = new();
    private readonly Guid _persona = Guid.NewGuid();

    private PropuestaDeDescuentosDeCartera Propuesta()
    {
        var reloj = Substitute.For<IDateTimeService>();
        reloj.HoyLocal.Returns(Hoy);
        return new PropuestaDeDescuentosDeCartera(_consulta, reloj, NullLogger<PropuestaDeDescuentosDeCartera>.Instance);
    }

    private void Cartera()
    {
        _consulta.Agregar(_persona, ConsultaDeObligacionesDePrueba.Obligacion("V-1", 1_000_000m, exigible: 120_000m, cuota: 100_000m)
            with { Principal = 900_000m, CurrentInterest = 80_000m, DefaultInterest = 20_000m, PendingInstallments = 9 });
        _consulta.Agregar(_persona, ConsultaDeObligacionesDePrueba.Obligacion("K-1", 400_000m, exigible: 400_000m, estado: EstadoDeObligacion.WrittenOff));
        _consulta.Agregar(_persona, ConsultaDeObligacionesDePrueba.Obligacion("CO-1", 700_000m, exigible: 70_000m, rol: RolEnLaObligacion.CoDebtor));
        _consulta.Agregar(_persona, ConsultaDeObligacionesDePrueba.Obligacion("P-1", 0m, estado: EstadoDeObligacion.PaidOff));
    }

    [Fact]
    public async Task SaldoTotal_propone_el_total_para_cancelar_de_todas_las_vivas_como_deudor_castigadas_incluidas()
    {
        Cartera();
        var avisos = new List<WarningDto>();

        var r = await Propuesta().ProponerAsync(_persona, DeduccionAlRetiro.SaldoTotal, avisos, CancellationToken.None);

        r.Deudas.Select(d => (d.ObligationNumber, d.Proposed, d.Applied)).Should().Equal(("V-1", 1_000_000m, 1_000_000m), ("K-1", 400_000m, 400_000m));
        r.Deudas.Should().OnlyContain(d => d.Kind == SettlementDeductionKind.CooperativeLoan && d.ConceptCode == WellKnownConceptCodes.LoanDeduction
                                           && !d.AccountedByOtherModule && d.ObligationPublicId != null && d.RecurringNoveltyId == null);
        avisos.Should().BeEmpty();
        var consulta = _consulta.Consultas.Should().ContainSingle().Subject;
        consulta.IncludeAsCoDebtor.Should().BeFalse("nunca las de codeudor (G4)");
        consulta.Date.Should().Be(Hoy);
        consulta.PersonPublicIds.Should().Equal(_persona);
    }

    [Fact]
    public async Task SoloCuotasCausadas_propone_lo_exigible()
    {
        Cartera();

        var r = await Propuesta().ProponerAsync(_persona, DeduccionAlRetiro.SoloCuotasCausadas, [], CancellationToken.None);

        r.Deudas.Select(d => (d.ObligationNumber, d.Proposed)).Should().Equal(("V-1", 120_000m), ("K-1", 400_000m));
    }

    [Fact]
    public async Task NoProponer_no_propone_ni_consulta()
    {
        Cartera();

        var r = await Propuesta().ProponerAsync(_persona, DeduccionAlRetiro.NoProponer, [], CancellationToken.None);

        r.Deudas.Should().BeEmpty();
        _consulta.Consultas.Should().BeEmpty();
    }

    [Fact]
    public async Task El_desglose_conserva_las_claves_de_la_010_y_la_descripcion_nombra_la_obligacion()
    {
        Cartera();

        var deuda = (await Propuesta().ProponerAsync(_persona, DeduccionAlRetiro.SaldoTotal, [], CancellationToken.None)).Deudas.First();

        using var json = JsonDocument.Parse(deuda.BreakdownJson);
        var claves = json.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        claves.Should().BeEquivalentTo(["capital", "intereses", "mora", "cuotasPendientes", "cuota", "saldoTotal", "modo"]);
        json.RootElement.GetProperty("capital").GetDecimal().Should().Be(900_000m);
        json.RootElement.GetProperty("intereses").GetDecimal().Should().Be(80_000m);
        json.RootElement.GetProperty("mora").GetDecimal().Should().Be(20_000m);
        json.RootElement.GetProperty("cuotasPendientes").GetInt32().Should().Be(9);
        json.RootElement.GetProperty("cuota").GetDecimal().Should().Be(100_000m);
        json.RootElement.GetProperty("saldoTotal").GetDecimal().Should().Be(1_000_000m);
        json.RootElement.GetProperty("modo").GetString().Should().Be("SaldoTotal");
        deuda.Description.Should().StartWith("Crédito V-1");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Si_Cartera_falla_o_no_responde_la_propuesta_sale_vacia_con_aviso(bool tiempoAgotado)
    {
        Cartera();
        _consulta.Caida = tiempoAgotado ? new TaskCanceledException("tiempo agotado") : new InvalidOperationException("Cartera caída");
        var avisos = new List<WarningDto>();

        var r = await Propuesta().ProponerAsync(_persona, DeduccionAlRetiro.SaldoTotal, avisos, CancellationToken.None);

        r.Deudas.Should().BeEmpty();
        avisos.Should().ContainSingle(w => w.Code == SettlementErrors.PortfolioUnavailableCode);
    }

    [Fact]
    public async Task La_persona_migrada_sin_activar_se_informa()
    {
        _consulta.MarcarMigrada(_persona);

        var r = await Propuesta().ProponerAsync(_persona, DeduccionAlRetiro.SaldoTotal, [], CancellationToken.None);

        r.MigradaSinActivar.Should().BeTrue();
        r.Deudas.Should().BeEmpty();
    }
}
