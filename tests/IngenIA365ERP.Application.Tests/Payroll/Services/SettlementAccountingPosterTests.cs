using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Application.Tests.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Payroll.Services;

/// <summary>
/// Feature 010 (T027, FR-004): el contabilizador de liquidaciones cancela la provisión con el rubro
/// pagado, lleva la diferencia al gasto (o la libera invirtiendo el asiento), deja la cuenta por
/// pagar de las cesantías anuales al fondo, fecha al corte y no agrega nada al libro si a un
/// concepto le faltan cuentas. Todo por el contrato (<c>AccountingPoster</c>), con
/// <c>SourceType</c> propio por tipo.
/// </summary>
public class SettlementAccountingPosterTests
{
    private static readonly ICurrentUserService Contadora = NominaTestData.UsuarioDePrueba("contadora@demo", 9);

    [Fact]
    public async Task La_prima_cancela_la_provision_y_lleva_la_diferencia_al_gasto_con_fecha_de_corte_y_su_SourceType()
    {
        // Provisión mensual baja a propósito: la prima liquidada (1.124.548) supera lo provisionado (6 × 150.000 = 900.000).
        var d = LiquidacionDePrueba.ConPrimerSemestre(provPrimaMensual: 150_000m);
        LiquidacionDePrueba.ConContabilidad(d);
        var run = await LiquidacionDePrueba.PrimaAsync(d);
        var filas = await LiquidacionDePrueba.FilasAsync(d, run);
        var prima = filas.Single().Lines.Single(l => l.ConceptCode == WellKnownConceptCodes.ServiceBonus);
        var ajuste = filas.Single().Lines.Single(l => l.ConceptCode == WellKnownConceptCodes.ServiceBonusProvisionAdjustment);
        ajuste.Amount.Should().Be(prima.Amount - 6 * 150_000m, "liquidado menos provisión acumulada");
        ajuste.Amount.Should().BePositive();

        var r = await d.ContabilizadorDeLiquidaciones(Contadora).PostAsync(run, filas, postingDate: null, "Prima 2026-I", CancellationToken.None);
        await d.Db.SaveChangesAsync();

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var doc = r.Value;
        doc.Date.Should().Be(new DateOnly(2026, 6, 30), "D-04: por defecto la fecha de corte, para causar en el mes correcto");
        doc.SourceType.Should().Be("ServiceBonusRun");
        doc.SourcePublicId.Should().Be(run.PublicId);
        doc.OriginModule.Should().Be(PayrollAccountingPoster.ModuleCode);
        doc.TotalDebit.Should().Be(doc.TotalCredit);

        var cuentas = await d.Db.PayrollConceptDefinitionAccounts.ToDictionaryAsync(a => a.ConceptCode);
        var asientos = await d.Db.JournalEntries.Where(j => j.DocumentId == doc.Id).ToListAsync();
        // El rubro: débito a la cuenta de débito configurada (la provisión) y crédito a la CxP del empleado.
        asientos.Should().ContainSingle(a => a.AccountId == cuentas["PRIMA"].DebitAccountId && a.Debit == prima.Amount);
        asientos.Should().ContainSingle(a => a.AccountId == cuentas["PRIMA"].CreditAccountId && a.Credit == prima.Amount && a.PersonId == d.Ana.PersonId);
        // El ajuste positivo: débito al gasto, crédito a la provisión, sin invertir.
        asientos.Should().ContainSingle(a => a.AccountId == cuentas["PRIMA_AJUSTE_PROV"].DebitAccountId && a.Debit == ajuste.Amount);
        asientos.Should().ContainSingle(a => a.AccountId == cuentas["PRIMA_AJUSTE_PROV"].CreditAccountId && a.Credit == ajuste.Amount);
    }

    [Fact]
    public async Task Un_ajuste_negativo_libera_provision_invirtiendo_debito_y_credito()
    {
        // Provisión mensual alta: 6 × 300.000 = 1.800.000 supera la prima liquidada; la diferencia se libera.
        var d = LiquidacionDePrueba.ConPrimerSemestre(provPrimaMensual: 300_000m);
        LiquidacionDePrueba.ConContabilidad(d);
        var run = await LiquidacionDePrueba.PrimaAsync(d);
        var filas = await LiquidacionDePrueba.FilasAsync(d, run);
        var ajuste = filas.Single().Lines.Single(l => l.ConceptCode == WellKnownConceptCodes.ServiceBonusProvisionAdjustment);
        ajuste.Amount.Should().BeNegative();

        var r = await d.ContabilizadorDeLiquidaciones(Contadora).PostAsync(run, filas, postingDate: new DateOnly(2026, 7, 1), "Prima 2026-I", CancellationToken.None);
        await d.Db.SaveChangesAsync();

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Date.Should().Be(new DateOnly(2026, 7, 1), "quien aprueba puede fechar entre el corte y hoy");
        var cuentas = await d.Db.PayrollConceptDefinitionAccounts.SingleAsync(a => a.ConceptCode == "PRIMA_AJUSTE_PROV");
        var asientos = await d.Db.JournalEntries.Where(j => j.DocumentId == r.Value.Id).ToListAsync();
        // Invertido: la cuenta configurada como crédito (provisión) recibe el débito por el valor absoluto.
        asientos.Should().ContainSingle(a => a.AccountId == cuentas.CreditAccountId && a.Debit == -ajuste.Amount);
        asientos.Should().ContainSingle(a => a.AccountId == cuentas.DebitAccountId && a.Credit == -ajuste.Amount);
        asientos.Should().OnlyContain(a => a.Debit >= 0m && a.Credit >= 0m);
    }

    [Fact]
    public async Task En_las_cesantias_anuales_la_cuenta_por_pagar_de_CESANTIAS_es_del_fondo_y_la_de_los_intereses_del_empleado()
    {
        var d = LiquidacionDePrueba.ConPrimerSemestre();
        LiquidacionDePrueba.ConContabilidad(d);
        var fondo = d.FondoDeCesantiasConPersona(d.Ana);
        var run = await LiquidacionDePrueba.CesantiasAsync(d);
        var filas = await LiquidacionDePrueba.FilasAsync(d, run);
        filas.Single().Lines.Should().Contain(l => l.ConceptCode == WellKnownConceptCodes.Severance)
            .And.Contain(l => l.ConceptCode == WellKnownConceptCodes.SeveranceInterest);

        var r = await d.ContabilizadorDeLiquidaciones(Contadora).PostAsync(run, filas, null, "Cesantías 2026", CancellationToken.None);
        await d.Db.SaveChangesAsync();

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.SourceType.Should().Be("SeveranceRun");
        var asientos = await d.Db.JournalEntries.Where(j => j.DocumentId == r.Value.Id).ToListAsync();
        asientos.Where(a => a.Description!.Contains("Nómina CESANTIAS ")).Should().NotBeEmpty()
            .And.OnlyContain(a => a.PersonId == fondo.Id, "la consignación va al fondo (FR-011, FR-088)");
        asientos.Where(a => a.Description == "Nómina INT_CESANTIAS").Should().NotBeEmpty()
            .And.OnlyContain(a => a.PersonId == d.Ana.PersonId, "los intereses se pagan al empleado");
    }

    [Fact]
    public async Task Si_a_un_concepto_le_faltan_cuentas_no_se_agrega_nada_al_libro()
    {
        var d = LiquidacionDePrueba.ConPrimerSemestre();
        LiquidacionDePrueba.ConContabilidad(d);
        var run = await LiquidacionDePrueba.PrimaAsync(d);
        var filas = await LiquidacionDePrueba.FilasAsync(d, run);
        var cuentaPrima = await d.Db.PayrollConceptDefinitionAccounts.SingleAsync(a => a.ConceptCode == "PRIMA");
        d.Db.PayrollConceptDefinitionAccounts.Remove(cuentaPrima);
        await d.Db.SaveChangesAsync();

        var r = await d.ContabilizadorDeLiquidaciones(Contadora).PostAsync(run, filas, null, "Prima 2026-I", CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Payroll.Settlement.ConceptAccountsMissing");
        r.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { conceptCodes = new[] { "PRIMA" } });
        d.Db.ChangeTracker.Entries<Domain.Entities.Accounting.Transactions.AccountingDocument>().Should().BeEmpty();
        d.Db.ChangeTracker.Entries<Domain.Entities.Accounting.Transactions.JournalEntry>().Should().BeEmpty();
        (await d.Db.AccountingDocuments.CountAsync()).Should().Be(0);
        (await d.Db.VoucherTypes.SingleAsync(v => v.Code == "NM")).NextNumber.Should().Be(1, "el consecutivo no se movió");
    }

    [Fact]
    public async Task La_fecha_del_comprobante_no_puede_ser_anterior_al_corte_ni_posterior_a_hoy()
    {
        var d = LiquidacionDePrueba.ConPrimerSemestre();
        LiquidacionDePrueba.ConContabilidad(d);
        var run = await LiquidacionDePrueba.PrimaAsync(d);
        var filas = await LiquidacionDePrueba.FilasAsync(d, run);
        var poster = d.ContabilizadorDeLiquidaciones(Contadora);

        var antes = await poster.PostAsync(run, filas, new DateOnly(2026, 6, 29), "x", CancellationToken.None);
        var despues = await poster.PostAsync(run, filas, new DateOnly(2026, 7, 11), "x", CancellationToken.None);

        antes.Error.Code.Should().Be("Payroll.Settlement.PostingDateInvalid");
        despues.Error.Code.Should().Be("Payroll.Settlement.PostingDateInvalid");
        antes.Error.Message.Should().Contain("30/06/2026").And.Contain("10/07/2026");
    }

    [Fact]
    public async Task Sin_contabilidad_iniciada_responde_su_propio_codigo()
    {
        var d = LiquidacionDePrueba.ConPrimerSemestre();
        var run = await LiquidacionDePrueba.PrimaAsync(d);
        var filas = await LiquidacionDePrueba.FilasAsync(d, run);

        var r = await d.ContabilizadorDeLiquidaciones(Contadora).PostAsync(run, filas, null, "x", CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.Settlement.AccountingNotInitialized");
    }

    [Fact]
    public void El_tercero_por_linea_solo_cambia_en_las_cesantias_anuales()
    {
        var cesantias = new Domain.Entities.Payroll.Transactions.PayrollRunLine { ConceptCode = "CESANTIAS", Nature = ConceptNature.Earning };
        var intereses = new Domain.Entities.Payroll.Transactions.PayrollRunLine { ConceptCode = "INT_CESANTIAS", Nature = ConceptNature.Earning };

        SettlementAccountingPoster.TerceroPara(PayrollRunKind.Severance)(cesantias).Should().Be(EntidadInstitucional.FondoDeCesantias);
        SettlementAccountingPoster.TerceroPara(PayrollRunKind.Severance)(intereses).Should().Be(EntidadInstitucional.Ninguna);
        SettlementAccountingPoster.TerceroPara(PayrollRunKind.Settlement)(cesantias).Should().Be(EntidadInstitucional.Ninguna, "en la definitiva las cesantías se pagan al empleado");
    }

    /// <summary>
    /// Revisión N1 (2026-09-21): los cuatro ajustes de provisión son gasto contra pasivo estimado y van sin
    /// tercero, como la provisión que corrigen. <c>CESANTIAS_AJUSTE_PROV</c> caía en el prefijo «CESANT» y
    /// salía con el fondo de cesantías como tercero en la anual y en la definitiva.
    /// </summary>
    [Theory]
    [InlineData("PRIMA_AJUSTE_PROV")]
    [InlineData("CESANTIAS_AJUSTE_PROV")]
    [InlineData("INT_CESANTIAS_AJUSTE_PROV")]
    [InlineData("VACACIONES_AJUSTE_PROV")]
    [InlineData("PROV_CESANTIAS")]
    public void Los_ajustes_de_provision_van_sin_tercero_en_toda_liquidacion(string codigo)
    {
        var linea = new Domain.Entities.Payroll.Transactions.PayrollRunLine { ConceptCode = codigo, Nature = ConceptNature.Provision };

        TercerosDeNomina.EntidadDe(codigo, ConceptNature.Provision).Should().Be(EntidadInstitucional.Ninguna);
        SettlementAccountingPoster.TerceroPara(PayrollRunKind.Severance)(linea).Should().Be(EntidadInstitucional.Ninguna);
        SettlementAccountingPoster.TerceroPara(PayrollRunKind.Settlement)(linea).Should().Be(EntidadInstitucional.Ninguna);
    }
}
