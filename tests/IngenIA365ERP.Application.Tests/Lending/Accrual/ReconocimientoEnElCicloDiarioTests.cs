using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Lending.Accrual;
using IngenIA365ERP.Application.Lending.Settlement;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Accrual;

/// <summary>
/// Feature 024, fase 4 (pedido del dueño del 2026-11-16) sobre el ciclo diario real (el crédito de Ana, con los tipos de hecho sembrados): con
/// <c>Causacion.Momento = InicioDelCiclo</c> y el concepto en «Automatica», el primer día del período de la cuota el ciclo reconoce lo que la cuota va a
/// causar hasta su vencimiento (fila <c>PeriodRecognition</c>, hecho <c>CuotaReconocida</c>), y la causación de cada día lo consume en su mismo hecho
/// (<c>RecognizedInterest</c> negativo); lo pendiente lo ve el estado de cuenta. Con el concepto en «Manual» el ciclo no reconoce (lo hace el proceso
/// de causación), con «IngresoDirecto» el concepto es el directo, y de fábrica («Diaria») nada cambia. La política se lee por empresa y por línea.
/// </summary>
public class ReconocimientoEnElCicloDiarioTests : IAsyncLifetime
{
    private CausacionDePrueba _c = null!;

    private static readonly DateOnly Hoy = CausacionDePrueba.Hoy;

    public async Task InitializeAsync()
    {
        _c = await CausacionDePrueba.CrearAsync();
        await _c.SembrarTiposDeHechoAsync();
    }

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    private async Task VigenciaAsync(string clave, string valor, int? linea = null)
    {
        _c.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = clave, Value = valor, ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba", CreatedBy = "test",
            ScopeKind = linea is null ? ParameterScopeKind.None : ParameterScopeKind.CreditLine, ScopeId = linea ?? 0,
        });
        await _c.Db.SaveChangesAsync();
    }

    private Task<List<Domain.Entities.Lending.Transactions.LoanTransaction>> FilasAsync(LoanConcept concepto) =>
        _c.Db.LoanTransactions.AsNoTracking().Where(t => t.LoanId == _c.Credito.Id && t.Concept == concepto).ToListAsync();

    private static IEnumerable<(string Concepto, decimal Monto)> Montos(string payload) =>
        JsonDocument.Parse(payload).RootElement.GetProperty("lines").EnumerateArray()
            .SelectMany(l => l.GetProperty("amounts").EnumerateArray())
            .Select(a => (a.GetProperty("concept").GetString()!, a.GetProperty("amount").GetDecimal()));

    [Fact]
    public async Task Al_inicio_del_ciclo_reconoce_la_cuota_el_primer_dia_y_cada_dia_consume_lo_que_causa()
    {
        await VigenciaAsync(ParametrosDeCartera.CausacionMomento, ParametrosDeCartera.CausacionAlInicioDelCiclo);
        var primero = Hoy.AddDays(1);

        await _c.CausarHastaAsync(primero);

        var reconocidas = await FilasAsync(LoanConcept.RecognizedInterest);
        var reconocimiento = reconocidas.Should().ContainSingle(t => t.Kind == LoanTransactionKind.PeriodRecognition).Subject;
        reconocimiento.ValueDate.Should().Be(primero, "el primer día del período de la cuota");
        reconocimiento.InstallmentNumber.Should().Be(1);
        reconocimiento.Amount.Should().BeGreaterThan(0m);
        var causado = (await FilasAsync(LoanConcept.CurrentInterest)).Where(t => t.Kind == LoanTransactionKind.Accrual).Sum(t => t.Amount);
        causado.Should().BeGreaterThan(0m, "el cálculo diario no cambia");
        reconocidas.Where(t => t.Kind == LoanTransactionKind.Accrual).Sum(t => t.Amount).Should().Be(-causado,
            "la causación del día consume lo reconocido por lo mismo que causa");

        var hechos = await _c.Db.IntegrationMessages.AsNoTracking().Where(m => m.Type == CuotaReconocidaV1.Type || m.Type == InteresCausadoV1.Type)
            .ToListAsync();
        Montos(hechos.Single(m => m.Type == CuotaReconocidaV1.Type).PayloadJson).Should()
            .Equal((nameof(LoanConcept.RecognizedInterest), reconocimiento.Amount));
        Montos(hechos.Single(m => m.Type == InteresCausadoV1.Type).PayloadJson).Should()
            .Contain((nameof(LoanConcept.RecognizedInterest), -causado)).And.Contain((nameof(LoanConcept.CurrentInterest), causado));

        // Al día siguiente no se vuelve a reconocer: sólo se consume.
        await _c.CausarHastaAsync(primero.AddDays(1));
        (await FilasAsync(LoanConcept.RecognizedInterest)).Count(t => t.Kind == LoanTransactionKind.PeriodRecognition).Should().Be(1);
        var causadoHastaHoy = (await FilasAsync(LoanConcept.CurrentInterest)).Where(t => t.Kind == LoanTransactionKind.Accrual).Sum(t => t.Amount);

        var estado = await _c.EstadoDeCuenta().Handle(new GetLoanStatementQuery(_c.Credito.PublicId), default);
        estado.IsSuccess.Should().BeTrue(estado.IsFailure ? estado.Error.Message : null);
        estado.Value.RecognizedPending.Should().Be(reconocimiento.Amount - causadoHastaHoy, "lo reconocido que todavía no se causó");

        var integridad = await _c.Verificar().Handle(new IngenIA365ERP.Application.Lending.Ledger.VerifyLendingLedgerIntegrityQuery([_c.Credito.PublicId]), default);
        integridad.IsSuccess.Should().BeTrue(integridad.IsFailure ? integridad.Error.Message : null);
        integridad.Value.Mismatches.Should().BeEmpty("el libro es la reconstrucción también con lo reconocido");
    }

    [Fact]
    public async Task Con_ingreso_directo_el_concepto_es_el_directo_y_no_hay_consumo()
    {
        await VigenciaAsync(ParametrosDeCartera.CausacionMomento, ParametrosDeCartera.CausacionAlInicioDelCiclo);
        await VigenciaAsync(ParametrosDeCartera.CausacionInicioDelCicloReconocimiento, ParametrosDeCartera.ReconocimientoIngresoDirecto);

        await _c.CausarHastaAsync(Hoy.AddDays(1));

        (await FilasAsync(LoanConcept.RecognizedInterestDirect)).Should().ContainSingle(t => t.Kind == LoanTransactionKind.PeriodRecognition)
            .Which.Amount.Should().BeGreaterThan(0m);
        (await FilasAsync(LoanConcept.RecognizedInterest)).Should().BeEmpty();
        (await FilasAsync(LoanConcept.RecognizedInterestDirect)).Where(t => t.Kind == LoanTransactionKind.Accrual).Sum(t => t.Amount)
            .Should().Be(-(await FilasAsync(LoanConcept.CurrentInterest)).Where(t => t.Kind == LoanTransactionKind.Accrual).Sum(t => t.Amount),
                "la causación del día anula en el mismo hecho lo que ya se reconoció como ingreso");
    }

    [Fact]
    public async Task Con_el_concepto_manual_el_ciclo_no_reconoce_y_de_fabrica_nada_cambia()
    {
        await _c.CausarHastaAsync(Hoy.AddDays(1));
        (await _c.Db.LoanTransactions.AnyAsync(t => t.Kind == LoanTransactionKind.PeriodRecognition)).Should().BeFalse("de fábrica, «Diaria»");
        (await _c.Db.IntegrationMessages.AnyAsync(m => m.Type == CuotaReconocidaV1.Type)).Should().BeFalse();

        await VigenciaAsync(ParametrosDeCartera.CausacionMomento, ParametrosDeCartera.CausacionAlInicioDelCiclo, _c.Credito.CreditLineId);
        await VigenciaAsync(ParametrosDeCartera.CausacionModoInteresCorriente, ParametrosDeCartera.CausacionManual);
        var politica = await new LectorDePoliticaDeReconocimiento(_c.D.Parametros()).LeerAsync(Hoy, [_c.Credito.CreditLineId, _c.Credito.CreditLineId + 1000], default);
        politica.AlInicioDelCiclo(_c.Credito.CreditLineId).Should().BeTrue("la excepción por línea");
        politica.AlInicioDelCiclo(_c.Credito.CreditLineId + 1000).Should().BeFalse("las demás siguen en «Diaria»");
        politica.RubrosManuales.Should().Contain(LoanConcept.CurrentInterest);
        politica.RubrosAutomaticos.Should().NotContain(LoanConcept.CurrentInterest);
        politica.Modo.Should().Be(Domain.Lending.Replay.ModoDeReconocimiento.IngresoDiferido, "el defecto");

        await _c.CausarHastaAsync(Hoy.AddDays(2));
        (await _c.Db.LoanTransactions.AnyAsync(t => t.Kind == LoanTransactionKind.PeriodRecognition)).Should().BeFalse(
            "con el interés en «Manual» lo reconoce el proceso de causación, no el ciclo");
    }
}
