using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Application.Tests.Lending.Payments;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Integration;

/// <summary>
/// Feature 013, US5, T0517 (contracts/interfaces.md §3.3–§3.17; data-model §10.1): el contrato de obligaciones <b>real</b> sobre InMemory, con el reloj
/// fijo y los puertos de Contabilidad, registro y permisos sustituidos (<see cref="PagosDePrueba"/>; lo transaccional va en e2e, quickstart §1.5):
/// el orden de los pasos, los rechazos con su código y la respuesta común (§3.4), las dos formas, la idempotencia por la clave del recaudador, las
/// clases de medio, las fechas, el abono extraordinario con y sin preferencia, la cuota específica, la validación previa como aviso, la reclamación al
/// seguro y los eventos de auditoría del intento y del hecho.
/// </summary>
public class ContratoDeObligacionesDeCarteraTests : IAsyncLifetime
{
    private PagosDePrueba _p = null!;
    private static readonly DateOnly Hoy = PagosDePrueba.Hoy;
    private static readonly DateOnly PrimerVencimiento = new(2027, 1, 1);

    public async Task InitializeAsync() => _p = await PagosDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _p.Dispose();
        return Task.CompletedTask;
    }

    private async Task AlPrimerVencimientoAsync(int dias = 0)
    {
        var fecha = (await _p.Db.RepaymentInstallments.AsNoTracking().OrderBy(i => i.Id).FirstAsync()).DueDate.AddDays(dias);
        await _p.CausarHastaAsync(fecha);
    }

    // ------------------------------------------------------------------------------------------------- listar y simular --

    [Fact]
    public async Task Listar_responde_Answered_con_lo_que_se_puede_pagar_y_deja_la_lectura_auditada()
    {
        var r = await _p.Contrato().ListarAsync(new SolicitudDeListado(PagosDePrueba.Prueba, _p.D.O.Ana.PublicId, Hoy), default);

        r.Result.Should().Be(ResultadoDelContrato.Answered);
        r.Code.Should().Be("Lending.ObligationContract.Listed");
        r.Value!.LendingStatus.Should().Be(EstadoDeCartera.Active);
        var o = r.Value.Obligations.Should().ContainSingle().Subject;
        o.ObligationPublicId.Should().Be(_p.Credito.PublicId);
        o.Role.Should().Be(RolEnLaObligacion.Debtor);
        o.Status.Should().Be(EstadoDeObligacion.Active);
        o.PayoffAmount.Should().Be(3_000_000m, "se desembolsó hoy: cancelar es el capital");
        o.DueToday.Should().Be(0m);
        o.AdmittedModalities.Should().NotContain(ModalidadDePago.SpecificInstallment, "la línea no permite cuotas fuera de orden");
        o.HasPaymentAgreement.Should().BeFalse("los acuerdos llegan con K4");
        (await _p.EventosAsync()).Should().Contain(AuditEventTypes.LendingObligationContractListed);
    }

    [Fact]
    public async Task Simular_no_escribe_y_devuelve_la_distribucion_con_su_huella()
    {
        var antes = await _p.Db.LoanOperations.CountAsync();

        var r = await _p.Contrato().SimularAsync(new SolicitudDeSimulacion(PagosDePrueba.Prueba, _p.Credito.PublicId, ModalidadDePago.FullPayoff, 3_000_000m, Hoy), default);

        r.Result.Should().Be(ResultadoDelContrato.Answered, r.Message);
        r.Code.Should().Be("Lending.ObligationContract.Simulated");
        r.Value!.SimulationId.Should().HaveLength(64);
        r.Value.AppliedAmount.Should().Be(3_000_000m);
        r.Value.Distribution.Where(l => l.Concept == ConceptoDeObligacion.Principal).Sum(l => l.Amount).Should().Be(3_000_000m);
        r.Value.BalancesAfter.PayoffAmount.Should().Be(0m);
        (await _p.Db.LoanOperations.CountAsync()).Should().Be(antes, "simular no escribe");
        (await _p.EventosAsync()).Should().Contain(AuditEventTypes.LendingObligationContractSimulated);
    }

    [Fact]
    public async Task Un_abono_sin_eleccion_se_simula_con_las_dos_alternativas()
    {
        var r = await _p.Contrato().SimularAsync(new SolicitudDeSimulacion(PagosDePrueba.Prueba, _p.Credito.PublicId, ModalidadDePago.ExtraordinaryPrincipal,
            500_000m, Hoy), default);

        r.Result.Should().Be(ResultadoDelContrato.Answered, r.Message);
        r.Value!.Alternatives.Select(a => a.Choice).Should().BeEquivalentTo([EleccionDeAbono.ReduceTerm, EleccionDeAbono.ReduceInstallment]);
        r.Value.Alternatives.Should().OnlyContain(a => a.NewPlan != null && a.SimulationId.Length == 64);
        r.Value.Alternatives.Select(a => a.SimulationId).Distinct().Should().HaveCount(2);
        r.Value.Alternatives.Single(a => a.Choice == EleccionDeAbono.ReduceTerm).NewPlan!.InstallmentCount.Should().BeLessThan(24);
    }

    // ------------------------------------------------------------------------------------------------------------ aplicar --

    [Fact]
    public async Task Aplicar_escribe_la_operacion_sus_filas_el_hecho_y_los_eventos_del_intento_y_del_hecho()
    {
        await AlPrimerVencimientoAsync();
        var cuota = await _p.Contrato().ListarAsync(new SolicitudDeListado(PagosDePrueba.Prueba, _p.D.O.Ana.PublicId, _p.G.HoyLocal), default);
        var exigible = cuota.Value!.Obligations[0].DueToday;
        exigible.Should().BeGreaterThan(0m);

        var c = await _p.AplicadoAsync(_p.Pago(ModalidadDePago.PastDue, exigible));

        c.AppliedAmount.Should().Be(exigible);
        c.Surplus.Should().Be(0m);
        c.Distribution.Sum(l => l.Amount).Should().Be(exigible);
        c.BalancesAfter.DaysPastDue.Should().Be(0);
        var op = await _p.OperacionAsync(c.OperationPublicId);
        op.Kind.Should().Be(LoanOperationKind.Payment);
        op.CollectorCode.Should().Be("TST");
        op.CollectorClass.Should().Be("Test");
        op.PaymentMeansClass.Should().Be("Cash");
        op.PayerPersonId.Should().Be(_p.D.O.Ana.Id);
        op.FinalityDate.Should().BeAfter(op.ValueDate, "el efectivo queda en firme a los diez días hábiles (D23)");
        DecisionDePago.Leer(op.Payload)!.Applied.Should().Be(exigible);
        var filas = await _p.Db.LoanTransactions.AsNoTracking().Where(t => t.OperationId == op.Id).ToListAsync();
        filas.Should().OnlyContain(t => t.Kind == LoanTransactionKind.PaymentAllocation && t.Amount < 0m && t.MessagePublicId != null);
        filas.Sum(t => t.Amount).Should().Be(-exigible);
        var mensaje = await _p.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.OriginPublicId == op.PublicId);
        mensaje.Type.Should().Be(PagoAplicadoV1.Type);
        mensaje.PayloadJson.Should().Contain("\"collectorClass\":\"Test\"").And.NotContain("account", "ningún hecho lleva cuentas");
        filas.Should().OnlyContain(t => t.MessagePublicId == mensaje.PublicId);
        (await _p.EventosAsync()).Should().Contain([AuditEventTypes.LendingObligationContractAttempted, AuditEventTypes.LendingObligationContractApplied]);
        await _p.ElLibroCuadraAsync();
    }

    [Fact]
    public async Task La_misma_clave_con_la_misma_huella_devuelve_el_comprobante_original_y_con_otra_KeyReused()
    {
        var pago = _p.Pago(ModalidadDePago.FreeAmount, 100_000m);
        var primero = await _p.AplicadoAsync(pago);

        var otra = await _p.AplicarAsync(pago);
        otra.Result.Should().Be(ResultadoDelContrato.AlreadyApplied);
        otra.Value!.OperationPublicId.Should().Be(primero.OperationPublicId);

        var distinta = await _p.AplicarAsync(pago with { Amount = 100_001m });
        distinta.Result.Should().Be(ResultadoDelContrato.Rejected);
        distinta.Code.Should().Be("Lending.Contract.KeyReused");
        (await _p.Db.LoanOperations.CountAsync(o => o.Kind == LoanOperationKind.Payment)).Should().Be(1);
    }

    [Fact]
    public async Task En_la_forma_interactiva_la_simulacion_es_obligatoria_y_si_la_distribucion_cambio_responde_DistributionChanged_sin_escribir()
    {
        var sin = await _p.AplicarAsync(_p.Pago(ModalidadDePago.FreeAmount, 100_000m), FormaDeAplicacion.Interactive);
        sin.Code.Should().Be("Validation.Invalid");

        var vieja = await _p.AplicarAsync(_p.Pago(ModalidadDePago.FreeAmount, 100_000m, simulacion: new string('a', 64)), FormaDeAplicacion.Interactive);
        vieja.Result.Should().Be(ResultadoDelContrato.Rejected);
        vieja.Code.Should().Be("Lending.Payment.DistributionChanged");
        vieja.Data!["simulation"].Should().BeOfType<SimulacionDePagoDto>();
        (await _p.Db.LoanOperations.CountAsync(o => o.Kind == LoanOperationKind.Payment)).Should().Be(0);

        var simulada = (await _p.Contrato().SimularAsync(new SolicitudDeSimulacion(PagosDePrueba.Prueba, _p.Credito.PublicId, ModalidadDePago.FreeAmount, 100_000m,
            Hoy), default)).Value!;
        var c = await _p.AplicadoAsync(_p.Pago(ModalidadDePago.FreeAmount, 100_000m, simulacion: simulada.SimulationId), FormaDeAplicacion.Interactive);
        (await _p.OperacionAsync(c.OperationPublicId)).SimulationId.Should().Be(simulada.SimulationId);
    }

    [Fact]
    public async Task En_la_forma_no_interactiva_la_simulacion_esta_prohibida()
    {
        var r = await _p.AplicarAsync(_p.Pago(ModalidadDePago.FreeAmount, 100_000m, simulacion: new string('a', 64)));
        r.Code.Should().Be("Validation.Invalid");
        Errores(r.Data).Should().Contain("SimulationIdNotAllowed");
    }

    [Fact]
    public async Task Antes_de_activar_listar_y_simular_responden_Answered_vacio_y_las_escrituras_NotActivated()
    {
        var setup = await _p.Db.LendingSetups.SingleAsync(s => s.Id == LendingSetup.IdDeLaFilaUnica);
        setup.ActivationDate = null;
        await _p.Db.SaveChangesAsync();

        var listado = await _p.Contrato().ListarAsync(new SolicitudDeListado(PagosDePrueba.Prueba, _p.D.O.Ana.PublicId, Hoy), default);
        listado.Result.Should().Be(ResultadoDelContrato.Answered);
        listado.Code.Should().Be("Lending.NotActivated");
        listado.Value!.LendingStatus.Should().Be(EstadoDeCartera.NotActivated);
        listado.Value.Obligations.Should().BeEmpty();
        var simulado = await _p.Contrato().SimularAsync(new SolicitudDeSimulacion(PagosDePrueba.Prueba, _p.Credito.PublicId, ModalidadDePago.FreeAmount, 1m, Hoy), default);
        (simulado.Result, simulado.Code, simulado.Value).Should().Be((ResultadoDelContrato.Answered, "Lending.NotActivated", (SimulacionDePagoDto?)null));

        var lote = await _p.Contrato().AplicarAsync(new SolicitudDeAplicacion(PagosDePrueba.Prueba, FormaDeAplicacion.NonInteractive,
            [_p.Pago(ModalidadDePago.FreeAmount, 1m), _p.Pago(ModalidadDePago.FreeAmount, 2m)], []), default);
        lote.Payments.Should().HaveCount(2).And.OnlyContain(x => x.Result == ResultadoDelContrato.Rejected && x.Code == "Lending.NotActivated");
        (await _p.EventosAsync()).Should().Contain([AuditEventTypes.LendingObligationContractAttempted, AuditEventTypes.LendingObligationContractRejected]);
    }

    [Fact]
    public async Task Sin_transaccion_del_recaudador_responde_TransactionRequired_en_cada_item()
    {
        _p.EnTransaccion = false;
        var lote = await _p.Contrato().AplicarAsync(new SolicitudDeAplicacion(PagosDePrueba.Prueba, FormaDeAplicacion.NonInteractive,
            [_p.Pago(ModalidadDePago.FreeAmount, 1m), _p.Pago(ModalidadDePago.FreeAmount, 2m)], []), default);
        lote.Payments.Should().OnlyContain(x => x.Code == "Lending.Contract.TransactionRequired");
        (await _p.Db.LoanOperations.CountAsync(o => o.Kind == LoanOperationKind.Payment)).Should().Be(0);
    }

    // ---------------------------------------------------------------------------------------------------------- recaudador --

    [Fact]
    public async Task Un_recaudador_fuera_del_registro_se_rechaza_y_la_recogida_esta_exenta()
    {
        var fuera = await _p.AplicarAsync(_p.Pago(ModalidadDePago.FreeAmount, 1m, clave: "REC:caja-1"), recaudador: new Recaudador("REC", ClaseDeRecaudador.Receipts));
        fuera.Code.Should().Be("Lending.Contract.CollectorNotRegistered");

        var recogida = await _p.AplicarAsync(_p.Pago(ModalidadDePago.FreeAmount, 10_000m, clave: $"CAR:{Guid.NewGuid():N}:{_p.Credito.PublicId:N}",
            medio: ClaseDeMedioDeRecaudo.InternalOffset), recaudador: new Recaudador("CAR", ClaseDeRecaudador.OwnLoanPayoff));
        recogida.Result.Should().Be(ResultadoDelContrato.Applied, recogida.Message);
    }

    [Fact]
    public async Task Un_recaudador_que_adopto_una_version_que_no_existe_responde_VersionNotSupported()
    {
        var fila = new IntegrationParticipant
        {
            Code = "REC", Class = IntegrationParticipantClass.Receipts, Role = IntegrationParticipantRole.ObligationsCollector, IsEnabled = true,
            ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba", RegisteredByName = "prueba",
        };
        fila.Adoptions.Add(new IntegrationParticipantAdoption { ContractName = "ProveedorDeObligaciones", Version = 7, ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba" });
        _p.Db.IntegrationParticipants.Add(fila);
        await _p.Db.SaveChangesAsync();

        var r = await _p.AplicarAsync(_p.Pago(ModalidadDePago.FreeAmount, 1m, clave: "REC:caja-1"), recaudador: new Recaudador("REC", ClaseDeRecaudador.Receipts));
        r.Code.Should().Be("Integration.Participant.VersionNotSupported");
    }

    // ----------------------------------------------------------------------------------------------------- clases de medio --

    [Theory]
    [InlineData(77, "Lending.Contract.UnknownMeansClass")]
    [InlineData((int)ClaseDeMedioDeRecaudo.AssociateCredit, "Lending.Payment.MeansClassNotAllowed")]
    [InlineData((int)ClaseDeMedioDeRecaudo.CustomerCredit, "Lending.Payment.MeansClassNotAllowed")]
    public async Task Las_clases_de_medio_desconocidas_o_de_credito_se_rechazan(int clase, string codigo)
    {
        var r = await _p.AplicarAsync(_p.Pago(ModalidadDePago.FreeAmount, 1m, medio: (ClaseDeMedioDeRecaudo)clase));
        r.Code.Should().Be(codigo);
    }

    [Fact]
    public async Task La_nomina_propia_solo_recauda_por_descuento_de_nomina_y_el_descuento_de_nomina_solo_es_de_Nomina()
    {
        var clave = $"NOM:{Guid.NewGuid():N}:{_p.Credito.PublicId:N}";
        var conEfectivo = await _p.AplicarAsync(_p.Pago(ModalidadDePago.FreeAmount, 1m, clave: clave), recaudador: Recaudador.Nomina);
        conEfectivo.Code.Should().Be("Lending.Payment.MeansClassNotAllowed");
        var ajeno = await _p.AplicarAsync(_p.Pago(ModalidadDePago.FreeAmount, 1m, medio: ClaseDeMedioDeRecaudo.OwnPayrollDeduction),
            recaudador: new Recaudador("TST", ClaseDeRecaudador.Test));
        ajeno.Result.Should().Be(ResultadoDelContrato.Applied, "el recaudador de prueba admite todas las clases salvo las de crédito");
        ClasesDeMedioDeRecaudo.Admite(ClaseDeRecaudador.Receipts, ClaseDeMedioDeRecaudo.OwnPayrollDeduction).Should().BeFalse();
        ClasesDeMedioDeRecaudo.Admite(ClaseDeRecaudador.Contributions, ClaseDeMedioDeRecaudo.InternalOffset).Should().BeTrue();
        ClasesDeMedioDeRecaudo.Admite(ClaseDeRecaudador.OwnLoanPayoff, ClaseDeMedioDeRecaudo.Cash).Should().BeFalse();
    }

    // -------------------------------------------------------------------------------------------------------------- fechas --

    [Fact]
    public async Task La_fecha_valor_no_es_futura_ni_mas_vieja_que_la_ventana_salvo_para_Nomina()
    {
        _p.C.Ir(Hoy.AddDays(10));
        var futura = await _p.AplicarAsync(_p.Pago(ModalidadDePago.FreeAmount, 1m, Hoy.AddDays(11)));
        futura.Code.Should().Be("Lending.Payment.ValueDateOutOfRange");
        futura.Data!["minDate"].Should().Be(Hoy.AddDays(5));

        var vieja = await _p.AplicarAsync(_p.Pago(ModalidadDePago.FreeAmount, 1m, Hoy.AddDays(4)));
        vieja.Code.Should().Be("Lending.Payment.ValueDateOutOfRange");

        var limite = await _p.AplicarAsync(_p.Pago(ModalidadDePago.FreeAmount, 1m, Hoy.AddDays(5)));
        limite.Result.Should().Be(ResultadoDelContrato.Applied, limite.Message);

        var nomina = await _p.AplicarAsync(_p.Pago(ModalidadDePago.FreeAmount, 1m, Hoy.AddDays(2), clave: $"NOM:{Guid.NewGuid():N}:{_p.Credito.PublicId:N}",
            medio: ClaseDeMedioDeRecaudo.OwnPayrollDeduction), recaudador: Recaudador.Nomina);
        nomina.Result.Should().Be(ResultadoDelContrato.Applied, "Nómina no tiene ventana de fecha valor (A9): " + nomina.Message);
    }

    [Fact]
    public async Task La_fecha_valor_igual_o_anterior_al_corte_de_la_carga_responde_BeforeCutoff()
    {
        var corte = (await _p.Db.LendingSetups.AsNoTracking().SingleAsync()).ActivationDate!.Value.AddDays(-1);
        var r = await _p.AplicarAsync(_p.Pago(ModalidadDePago.FreeAmount, 1m, corte, clave: $"NOM:{Guid.NewGuid():N}:{_p.Credito.PublicId:N}",
            medio: ClaseDeMedioDeRecaudo.OwnPayrollDeduction), recaudador: Recaudador.Nomina);
        r.Code.Should().Be("Lending.Contract.BeforeCutoff");
    }

    [Fact]
    public async Task Antes_de_un_cierre_hace_falta_la_atribucion_de_quien_actua_y_no_su_alcance()
    {
        _p.CerradoHasta = Hoy;
        var sin = await _p.AplicarAsync(_p.Pago(ModalidadDePago.FreeAmount, 1m));
        sin.Code.Should().Be("Lending.Payment.ValueDateBeforeClosing");

        _p.G.Conceder(ReglasDeFechaValor.PermisoAntesDelCierre);
        _p.G.AlcanceActual = IngenIA365ERP.Application.Common.Interfaces.Security.AlcanceDeCartera.Vacio;
        var con = await _p.AplicarAsync(_p.Pago(ModalidadDePago.FreeAmount, 1m));
        con.Result.Should().Be(ResultadoDelContrato.Applied, "el permiso se consulta sobre quien actúa, nunca su alcance (C41): " + con.Message);
    }

    // ----------------------------------------------------------------------------------------------------- obligación y modalidad --

    [Theory]
    [InlineData(LoanStatus.PaidOff)]
    [InlineData(LoanStatus.PendingDisbursement)]
    [InlineData(LoanStatus.Voided)]
    [InlineData(LoanStatus.Novated)]
    public async Task Una_obligacion_que_no_esta_viva_no_recibe_pagos(LoanStatus estado)
    {
        var credito = await _p.Db.Loans.SingleAsync(l => l.Id == _p.Credito.Id);
        credito.Status = estado;
        await _p.Db.SaveChangesAsync();
        var r = await _p.AplicarAsync(_p.Pago(ModalidadDePago.FreeAmount, 1m));
        r.Code.Should().Be("Lending.Payment.ObligationNotPayable");
    }

    [Fact]
    public async Task Un_abono_interactivo_sin_eleccion_se_rechaza_con_PreferenceMissing()
    {
        var r = await _p.AplicarAsync(_p.Pago(ModalidadDePago.ExtraordinaryPrincipal, 500_000m, simulacion: new string('b', 64)), FormaDeAplicacion.Interactive);
        r.Code.Should().Be("Lending.Payment.PreferenceMissing");
    }

    [Fact]
    public async Task Un_abono_no_interactivo_usa_la_preferencia_vigente_y_regenera_el_plan()
    {
        var c = await _p.AplicadoAsync(_p.Pago(ModalidadDePago.ExtraordinaryPrincipal, 500_000m));

        var op = await _p.OperacionAsync(c.OperationPublicId);
        op.ChoiceFromPreference.Should().BeTrue("la solicitud dejó la preferencia «reducir plazo»");
        op.PrepaymentChoice.Should().Be(PrepaymentChoice.ReduceTerm);
        c.NewPlan.Should().NotBeNull();
        c.NewPlan!.InstallmentCount.Should().BeLessThan(24);
        (await _p.Db.RepaymentScheduleVersions.AsNoTracking().AnyAsync(v => v.OperationId == op.Id)).Should().BeTrue("el abono registra la versión del plan");
        DecisionDePago.Leer(op.Payload)!.WithinLegalPrepaymentCap.Should().BeTrue("3.000.000 cabe en 880 veces el SMMLV");
        await _p.Aviso.Received(1).AvisarAsync(Arg.Any<Loan>(), Arg.Any<Domain.Entities.Lending.Operations.LoanOperation>(), 500_000m, PrepaymentChoice.ReduceTerm,
            Arg.Any<Domain.Lending.Schedules.PlanDePagos?>(), Arg.Any<CancellationToken>());
        await _p.ElLibroCuadraAsync();
    }

    [Fact]
    public async Task Un_abono_no_interactivo_sin_preferencia_queda_como_saldo_a_favor_con_el_aviso_y_su_acompanante()
    {
        _p.G.UtcNow = _p.G.UtcNow.AddMinutes(5);
        _p.Db.PrepaymentPreferences.Add(PrepaymentPreference.Registrar(_p.D.O.Ana.Id, _p.Credito.Id, null, null, 2, _p.G.UtcNow.AddMinutes(-1),
            PreferenceOrigin.DebtorRequest, null));
        await _p.Db.SaveChangesAsync();

        var c = await _p.AplicadoAsync(_p.Pago(ModalidadDePago.ExtraordinaryPrincipal, 500_000m));

        c.CreditBalanceAmount.Should().Be(500_000m);
        c.NewPlan.Should().BeNull();
        c.Warnings.Should().Contain("Lending.Payment.PreferenceAssumed").And.NotContain("Lending.Payment.PreferenceMissing", "un código por significado (C115)");
        var op = await _p.OperacionAsync(c.OperationPublicId);
        var tipos = await _p.Db.IntegrationMessages.AsNoTracking().Where(m => m.OriginPublicId == op.PublicId).Select(m => m.Type).ToListAsync();
        tipos.Should().BeEquivalentTo([PagoAplicadoV1.Type, SaldoAFavorReconocidoV1.Type]);
        (await _p.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == SaldoAFavorReconocidoV1.Type)).PayloadJson
            .Should().Contain("\"cause\":\"NoPrepaymentPreference\"");
    }

    [Fact]
    public async Task La_cuota_especifica_con_anteriores_impagas_responde_SpecificInstallmentBlocked_con_las_pendientes()
    {
        await _p.CausarHastaAsync(PrimerVencimiento.AddMonths(1).AddDays(2));
        var r = await _p.AplicarAsync(_p.Pago(ModalidadDePago.SpecificInstallment, 200_000m, cuota: 2));
        r.Code.Should().Be("Lending.Payment.SpecificInstallmentBlocked");
        ((int[])r.Data!["pendingBefore"]!).Should().Equal(1);
    }

    [Fact]
    public async Task Un_pago_mayor_que_el_total_cancela_y_el_excedente_vuelve_al_recaudador()
    {
        var c = await _p.AplicadoAsync(_p.Pago(ModalidadDePago.FullPayoff, 3_100_000m));

        c.PaidOff.Should().BeTrue();
        c.Surplus.Should().Be(100_000m);
        c.AppliedAmount.Should().Be(3_000_000m);
        c.FinalityUntil.Should().NotBeNull("la cancelación queda sujeta a firmeza (FR-082)");
        (await _p.Db.Loans.AsNoTracking().SingleAsync(l => l.Id == _p.Credito.Id)).Status.Should().Be(LoanStatus.PaidOff);
        (await _p.OperacionAsync(c.OperationPublicId)).ExcessAmount.Should().Be(100_000m);
    }

    // -------------------------------------------------------------------------------------------- validación previa y seguro --

    [Fact]
    public async Task Sin_respuesta_de_Contabilidad_el_pago_se_aplica_con_el_aviso_y_el_sello()
    {
        _p.D.Contabilidad.EvaluarAsync(Arg.Any<IReadOnlyList<MensajeContableDto>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ResultadoDeEvaluacionContableDto>(new Error("Accounting.Down", "caída")));

        var c = await _p.AplicadoAsync(_p.Pago(ModalidadDePago.FreeAmount, 10_000m));

        c.Warnings.Should().Contain("Lending.Prevalidation.NoResponse");
        (await _p.OperacionAsync(c.OperationPublicId)).PrevalidationOutcome.Should().Be(nameof(PrevalidationOutcome.NoResponse));
    }

    [Fact]
    public async Task Sin_regla_contable_el_contrato_nunca_rechaza_aunque_la_cooperativa_bloquee_y_sella_NotPostable()
    {
        _p.Db.ParameterVersions.Add(new Domain.Entities.Parameters.ParameterVersion
        {
            Module = Domain.Lending.Parameters.ParametrosDeCartera.Modulo, Key = Domain.Lending.Parameters.ParametrosDeCartera.ContabilidadFaltaDeRegla,
            ScopeKind = Domain.Enums.Parameters.ParameterScopeKind.None, Value = "Bloquear", ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba",
            CreatedAt = _p.G.UtcNow.AddDays(-10),
        });
        await _p.Db.SaveChangesAsync();
        _p.D.Contabilidad.EvaluarAsync(Arg.Any<IReadOnlyList<MensajeContableDto>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ResultadoDeEvaluacionContableDto(false,
                [new HallazgoDeCarteraDto(PagoAplicadoV1.Type, "RecaudoPorAplicar", null, null, "SinRegla", "Falta la regla", new QuienCorrigeDto("Accounting", null, null))],
                [])));

        var c = await _p.AplicadoAsync(_p.Pago(ModalidadDePago.FreeAmount, 10_000m));

        c.Warnings.Should().Contain("Lending.Prevalidation.NotPostable");
        (await _p.OperacionAsync(c.OperationPublicId)).PrevalidationOutcome.Should().Be(nameof(PrevalidationOutcome.NotPostable));
        (await _p.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == PagoAplicadoV1.Type)).PrevalidationOutcome
            .Should().Be(PrevalidationOutcome.NotPostable, "el sello va en el hecho (A8)");
    }

    [Fact]
    public async Task El_pago_de_la_aseguradora_cierra_su_reclamacion_en_proceso()
    {
        var aseguradora = _p.D.O.L.Aseguradora;
        var reclamacion = new InsuranceClaim { LoanId = _p.Credito.Id, InsurerPersonId = aseguradora.Id, DeceasedPersonId = _p.D.O.Ana.Id, DeathDate = Hoy };
        _p.Db.InsuranceClaims.Add(reclamacion);
        await _p.Db.SaveChangesAsync();

        var c = await _p.AplicadoAsync(_p.Pago(ModalidadDePago.FreeAmount, 1_000_000m, medio: ClaseDeMedioDeRecaudo.Transfer, pagador: aseguradora.PublicId));

        var op = await _p.OperacionAsync(c.OperationPublicId);
        var cerrada = await _p.Db.InsuranceClaims.AsNoTracking().SingleAsync(x => x.Id == reclamacion.Id);
        cerrada.Status.Should().Be(InsuranceClaimStatus.Paid);
        cerrada.PaidOperationId.Should().Be(op.Id);
        op.InsuranceClaimId.Should().Be(reclamacion.Id);
    }

    // ------------------------------------------------------------------------------------------------------------ reversar --

    [Fact]
    public async Task Reversar_por_la_clave_del_pago_deja_el_credito_como_antes_y_un_reverso_no_se_reversa()
    {
        var antes = await _p.Contrato().ListarAsync(new SolicitudDeListado(PagosDePrueba.Prueba, _p.D.O.Ana.PublicId, Hoy), default);
        var pago = _p.Pago(ModalidadDePago.FreeAmount, 300_000m);
        await _p.AplicadoAsync(pago);

        var r = await _p.Contrato().ReversarAsync(new SolicitudDeReverso(PagosDePrueba.Prueba, pago.IdempotencyKey, "Cheque devuelto"), default);
        _p.Db.ChangeTracker.Clear();

        r.Result.Should().Be(ResultadoDelContrato.Applied, r.Message);
        r.Code.Should().Be("Lending.ObligationContract.Reversed");
        var despues = await _p.Contrato().ListarAsync(new SolicitudDeListado(PagosDePrueba.Prueba, _p.D.O.Ana.PublicId, Hoy), default);
        despues.Value!.Obligations[0].PayoffAmount.Should().Be(antes.Value!.Obligations[0].PayoffAmount);
        despues.Value.Obligations[0].Advance.Should().Be(0m);
        (await _p.EventosAsync()).Should().Contain(AuditEventTypes.LendingObligationContractReversed);
        await _p.ElLibroCuadraAsync();

        var otra = await _p.Contrato().ReversarAsync(new SolicitudDeReverso(PagosDePrueba.Prueba, pago.IdempotencyKey, "Cheque devuelto"), default);
        otra.Result.Should().Be(ResultadoDelContrato.AlreadyApplied, "la misma clave y el mismo motivo devuelven el reverso original");
        var conOtroMotivo = await _p.Contrato().ReversarAsync(new SolicitudDeReverso(PagosDePrueba.Prueba, pago.IdempotencyKey, "Otro motivo"), default);
        conOtroMotivo.Code.Should().Be("Lending.Contract.KeyReused");
        var reverso = await _p.Contrato().ReversarAsync(new SolicitudDeReverso(PagosDePrueba.Prueba, pago.IdempotencyKey + ":reverso", "x"), default);
        reverso.Code.Should().Be("Lending.Reversal.OfReversal");
        var inexistente = await _p.Contrato().ReversarAsync(new SolicitudDeReverso(PagosDePrueba.Prueba, PagosDePrueba.Clave(), "x"), default);
        inexistente.Code.Should().Be("Generic.NotFound");
    }

    private static IReadOnlyList<string> Errores(IReadOnlyDictionary<string, object?>? data) =>
        JsonSerializer.SerializeToElement(data!["errors"]).EnumerateArray().Select(e => e.GetProperty("code").GetString()!).ToList();
}
