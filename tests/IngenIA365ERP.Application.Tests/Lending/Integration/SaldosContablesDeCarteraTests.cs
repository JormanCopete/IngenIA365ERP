using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Tests.Lending.Disbursements;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Integration;

/// <summary>
/// Feature 013, US7, T0628 (contabilidad.md §14.2, §14.3; T58, C51): el saldo de Cartera por combinación. Suma del libro por <b>fecha contable</b>
/// ≤ corte con la combinación del crédito (línea, modalidad, sub-segmento, categoría, empleado, reestructurado y sucursal contable); concepto →
/// rol de §2.2; <c>DesembolsoPorGirar</c> = las órdenes emitidas sin «girada» ni anulación; los puentes esperan cero; el alcance de oficinas
/// filtra; y la regla <c>RecaudoPorAplicar|Payroll</c> se responde por <c>UnresolvedCombinations</c>. Sobre un desembolso real
/// (<see cref="DesembolsoDePrueba"/>).
/// </summary>
public class SaldosContablesDeCarteraTests : IAsyncLifetime
{
    private DesembolsoDePrueba _d = null!;

    public async Task InitializeAsync() => _d = await DesembolsoDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _d.Dispose();
        return Task.CompletedTask;
    }

    private static readonly DateOnly Hoy = DesembolsoDePrueba.Hoy;

    private SaldosContablesDeCartera Saldos() => new(_d.Db, _d.Contabilidad);

    private async Task<(int LoanId, Guid Sucursal, decimal Bruto)> DesembolsarAsync()
    {
        var s = await _d.AprobadaAsync();
        var r = await _d.DesembolsadoAsync(_d.Entrada(s));
        var credito = await _d.Db.Loans.AsNoTracking().SingleAsync(l => l.PublicId == r.LoanPublicId);
        var sucursal = await _d.Db.Branches.AsNoTracking().Where(b => b.Id == credito.BranchId).Select(b => b.PublicId).SingleAsync();
        return (credito.Id, sucursal, r.GrossAmount);
    }

    [Fact]
    public async Task El_saldo_es_la_suma_del_libro_por_fecha_contable_con_la_combinacion_del_credito()
    {
        var (credito, sucursal, bruto) = await DesembolsarAsync();

        (await Saldos().PorCombinacionAsync(Hoy.AddDays(-1), null, default)).Where(s => s.Clave.Rol == "Capital").Should().BeEmpty("antes del desembolso");

        var capital = (await Saldos().PorCombinacionAsync(Hoy, null, default)).Single(s => s.Clave.Rol == "Capital");
        capital.Saldo.Should().Be(bruto);
        capital.Creditos.Should().Equal(credito);
        capital.Clave.Should().BeEquivalentTo(new
        {
            Rol = "Capital", Linea = "CONLIB", Categoria = "A", Concepto = (string?)null, Gracia = (bool?)false, Reestructurado = (bool?)false,
            Sucursal = sucursal,
        });
        capital.Clave.Modalidad.Should().NotBeNullOrEmpty();
        capital.Clave.ComoCombinacion().Role.Should().Be("Capital");
    }

    [Theory]
    [InlineData(LoanConcept.Principal, false, "Capital")]
    [InlineData(LoanConcept.CurrentInterest, false, "InteresPorCobrar")]
    [InlineData(LoanConcept.DefaultInterest, false, "InteresPorCobrar")]
    [InlineData(LoanConcept.CurrentInterestInSuspense, false, "SuspensoEnOrden")]
    [InlineData(LoanConcept.DefaultInterestInSuspense, true, "CastigadaEnOrden")]
    [InlineData(LoanConcept.Insurance, false, "PagoPorCuentaDelAsociado")]
    [InlineData(LoanConcept.CollectionFee, false, "CargoPorCobrar")]
    [InlineData(LoanConcept.OtherCharge, false, "CargoPorCobrar")]
    [InlineData(LoanConcept.Advance, false, "Anticipo")]
    [InlineData(LoanConcept.CreditBalance, false, "SaldoAFavor")]
    public void Cada_concepto_va_a_su_rol_de_saldo(LoanConcept concepto, bool enControl, string rol) =>
        SaldosContablesDeCartera.RolDelConcepto(concepto, enControl).Should().Be(rol);

    [Fact]
    public void Los_roles_de_naturaleza_credito_se_leen_con_el_signo_contrario()
    {
        SaldosContablesDeCartera.Signo("Capital").Should().Be(1);
        SaldosContablesDeCartera.Signo("SuspensoEnOrden").Should().Be(1);
        SaldosContablesDeCartera.Signo("RecaudoPorAplicar").Should().Be(1);
        SaldosContablesDeCartera.Signo("Anticipo").Should().Be(-1);
        SaldosContablesDeCartera.Signo("SaldoAFavor").Should().Be(-1);
        SaldosContablesDeCartera.Signo("DesembolsoPorGirar").Should().Be(-1);
        SaldosContablesDeCartera.Signo("PuenteComercial").Should().Be(-1);
    }

    [Fact]
    public async Task DesembolsoPorGirar_son_las_ordenes_emitidas_sin_girada_ni_anulacion()
    {
        var (_, sucursal, _) = await DesembolsarAsync();
        var orden = await _d.Db.DisbursementOrders.AsNoTracking().SingleAsync();

        var abiertas = await Saldos().OrdenesAbiertasAsync(Hoy, null, default);
        abiertas.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Saldo = orden.NetAmount,
            Clave = new { Rol = "DesembolsoPorGirar", Motivo = "Transfer", Linea = "CONLIB", Sucursal = sucursal },
        });

        var girada = await _d.Db.DisbursementOrders.SingleAsync();
        girada.Status = DisbursementOrderStatus.Disbursed;
        await _d.Db.SaveChangesAsync();
        (await Saldos().OrdenesAbiertasAsync(Hoy, null, default)).Should().BeEmpty("girada: Tesorería ya la pagó");
    }

    [Fact]
    public async Task Los_puentes_esperan_cero_por_clase_y_siempre_la_recogida_de_creditos_propios()
    {
        var puentes = await Saldos().PuentesEnCeroAsync(Hoy, default);

        puentes.Should().OnlyContain(p => p.Saldo == 0m);
        puentes.Select(p => (p.Clave.Rol, p.Clave.ClaseDeRecaudador)).Should().Contain(
        [
            ("RecaudoPorAplicar", nameof(ClaseDeRecaudador.OwnLoanPayoff)), ("PuenteComercial", null), ("CuentaPorCobrarComercial", null),
        ]);
    }

    [Fact]
    public async Task El_alcance_de_oficinas_filtra_los_creditos()
    {
        await DesembolsarAsync();

        (await Saldos().PorCombinacionAsync(Hoy, AlcanceDeCartera.Vacio, default)).Where(s => s.Clave.Rol == "Capital").Should().BeEmpty();
        (await Saldos().PorCombinacionAsync(Hoy, AlcanceDeCartera.Total, default)).Where(s => s.Clave.Rol == "Capital").Should().ContainSingle();
    }

    [Fact]
    public async Task La_regla_del_puente_de_nomina_se_responde_por_las_combinaciones_sin_resolver()
    {
        var conRegla = new ContabilidadDePruebaParaConciliar(_d.Contabilidad, "RecaudoPorAplicar");
        (await Saldos().PuenteResuelveAsync(ClaseDeRecaudador.Payroll, Hoy, default)).Value.Should().BeTrue();
        conRegla.Preguntas.Single().Single().Should().BeEquivalentTo(new { Role = "RecaudoPorAplicar", CollectorClass = "Payroll" });

        _ = new ContabilidadDePruebaParaConciliar(_d.Contabilidad);
        (await Saldos().PuenteResuelveAsync(ClaseDeRecaudador.Payroll, Hoy, default)).Value.Should().BeFalse();
    }
}
