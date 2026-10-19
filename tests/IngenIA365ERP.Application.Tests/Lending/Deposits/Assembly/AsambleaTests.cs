using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Assembly;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Contributions;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits.Assembly;

/// <summary>
/// Feature 014, US12 (T204; FR-045 a FR-047, D13; research §22): la decisión de asamblea —registrada por una persona, simulada con el límite del IPC y el
/// prorrateo al fondo, aplicada por otra con el acta cerrada y su soporte, reversada entera y corregida con la misma acta—, los excedentes a aportes, a
/// ahorro y en pago, y la amortización hasta el mínimo. Escenario: Ana con 1.000.000 de aportes y Beto con 2.000.000, consignados el 2026-12-01; la
/// asamblea es el 2027-03-20 y decide sobre el ejercicio 2026. (nuevo)
/// </summary>
public sealed class AsambleaTests : IDisposable
{
    private static readonly DateOnly DiaDeLaAsamblea = new(2027, 3, 20);
    private readonly AportesDePrueba _a = new();

    public AsambleaTests() => _a.C.Entrega = EntregaDeCartera.K10;

    public void Dispose() => _a.Dispose();

    // ---------------------------------------------------------------------------------------------------------- piezas --

    private DistribucionesDeAsamblea Distribuciones() => new(_a.Db, _a.G.Alcance, _a.G.Auditoria, _a.C.Parametros(), _a.G.Valores, _a.Reglas());

    private RegisterAssemblyDistributionCommandHandler Registrar() =>
        new(_a.Db, Distribuciones(), _a.C.Contexto, _a.G.Intervenciones(), _a.Numerador(), _a.G.Actor, _a.G.Reloj);

    private SimulateAssemblyDistributionQueryHandler Simular() => new(Distribuciones(), _a.G.Reloj);

    private OrdenesDeGiroDeCuentas Ordenes() => new(_a.Db, _a.RegistroDeCuentas(), _a.Numerador(), _a.Emision(), _a.Reversion(), _a.C.Parametros(), _a.G.Actor,
        _a.G.Reloj);

    private ApplyAssemblyDistributionCommandHandler Aplicar() => new(_a.Db, Distribuciones(), new PuntoDeActaCerrado(_a.Db), _a.G.Intervenciones(),
        _a.RegistroDeCuentas(), _a.Numerador(), _a.Emision(), Ordenes(), _a.Reglas(), new ValidacionPreviaDeCaptaciones(_a.Db, _a.ValidacionPrevia()),
        _a.C.Cerrojo, _a.G.Actor, _a.G.Reloj);

    private ReverseAssemblyDistributionCommandHandler Reversar() => new(_a.Db, Distribuciones(), _a.G.Intervenciones(), _a.RegistroDeCuentas(), _a.Numerador(),
        _a.Emision(), _a.C.Cerrojo, _a.G.Actor, _a.G.Reloj);

    private static T Ok<T>(Result<T> r)
    {
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    // ------------------------------------------------------------------------------------------------------- escenario --

    /// <summary>Ana con 1.000.000 y Beto con 2.000.000 de aportes desde el 2026-12-01; después, el día de la asamblea.</summary>
    private async Task AportesAsync(ContributionLineVersionInput? version = null)
    {
        var linea = await _a.C.LineaDeAportesAsync(propia: version ?? AportesDePrueba.Version());
        var ana = await _a.CuentaDeAportesAsync(_a.C.Ana, linea);
        var beto = await _a.CuentaDeAportesAsync(_a.C.Beto, linea);
        await _a.ConsignadaAsync(_a.Consignacion(ana, 1_000_000m, ModalidadDeConsignacion.Free));
        await _a.ConsignadaAsync(_a.Consignacion(beto, 2_000_000m, ModalidadDeConsignacion.Free));
        _a.G.HoyLocal = DiaDeLaAsamblea;
    }

    /// <summary>El IPC de 2026 (valor de prueba), serie ANUAL.</summary>
    private void Ipc(decimal valor) =>
        _a.G.Valores.LeerAsync(CodigosDeValorNacional.Ipc, "ANUAL", Arg.Any<DateOnly>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ValorNacionalVigente(Guid.NewGuid(), Guid.NewGuid(), CodigosDeValorNacional.Ipc, "ANUAL", valor, "Fraccion", null,
                new DateOnly(2027, 1, 9), null, new DateOnly(2026, 12, 31), "DANE (valor de prueba)", 1, DateTime.UtcNow.AddMonths(-2), EstadoDeRevision.Validada)));

    private async Task<AssemblyDistributionDto> RegistradaAsync(AssemblyDistributionKind clase, decimal fondo, decimal? porcentaje = null, decimal? valor = null,
        RevaluationBase? @base = null, SurplusDestination? destino = null, string? linea = null, Guid? corrige = null)
    {
        _a.G.ActuarComo(_a.C.Asesora);
        var r = Ok(await Registrar().Handle(new RegisterAssemblyDistributionCommand(clase, 2026, fondo, porcentaje, valor, @base, destino, linea, corrige,
            "Asamblea general ordinaria de marzo") { OperationKey = Guid.NewGuid() }, default));
        _a.Db.ChangeTracker.Clear();
        return r;
    }

    /// <summary>Un punto de acta cerrado de la asamblea que aprueba la decisión.</summary>
    private Guid Acta(Guid decision)
    {
        var organo = _a.G.Organo("ASAMBLEA" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant(), "Asamblea general", GoverningBodyKind.Other);
        var sesion = new Session
        {
            GoverningBodyId = organo.Id, Kind = SessionKind.Ordinary, Mode = SessionMode.InPerson, HeldOn = DiaDeLaAsamblea, Status = SessionStatus.Closed,
            Number = "ASAMBLEA-2027", CreatedBy = "test",
        };
        _a.Db.LendingSessions.Add(sesion);
        _a.Db.SaveChanges();
        var punto = new SessionItem
        {
            SessionId = sesion.Id, Order = 1, Kind = SessionItemKind.Decision, Rule = VotingRule.Majority, SubjectType = LendingDecisionSubjects.AssemblyDistribution,
            SubjectPublicId = decision, Title = "Proyecto de distribución de excedentes", Outcome = CreditDecisionOutcome.Approve, CreatedBy = "test",
        };
        _a.Db.SessionItems.Add(punto);
        _a.Db.SaveChanges();
        _a.Db.ChangeTracker.Clear();
        return punto.PublicId;
    }

    /// <summary>El acta firmada, subida como soporte de la decisión.</summary>
    private Guid Soporte(Guid decision)
    {
        var adjunto = new Attachment
        {
            TenantId = 1, OwnerEntityType = AdjuntosDeModulo.SoporteDeAsamblea, OwnerEntityPublicId = decision, FileName = "acta-asamblea.pdf",
            ContentType = "application/pdf", SizeBytes = 10, Sha256Hex = new string('a', 64), StoragePath = "x/acta.bin",
        };
        _a.Db.Attachments.Add(adjunto);
        _a.Db.SaveChanges();
        _a.Db.ChangeTracker.Clear();
        return adjunto.PublicId;
    }

    private async Task<AssemblyDistributionSimulationDto> SimuladaAsync(Guid decision)
    {
        var s = Ok(await Simular().Handle(new SimulateAssemblyDistributionQuery(decision), default));
        _a.Db.ChangeTracker.Clear();
        return s;
    }

    private async Task<Result<AssemblyDistributionDto>> AplicarAsync(Guid decision, string huella, Guid? punto, Guid? soporte)
    {
        var r = await Aplicar().Handle(new ApplyAssemblyDistributionCommand(decision, punto, huella, soporte is { } s ? [s] : [], "Aplicada según el acta")
        {
            OperationKey = Guid.NewGuid(),
        }, default);
        _a.Db.ChangeTracker.Clear();
        return r;
    }

    private async Task<AssemblyDistributionDto> AplicadaAsync(AssemblyDistributionDto d)
    {
        var s = await SimuladaAsync(d.PublicId);
        _a.G.ActuarComo(_a.C.Jefe);
        return Ok(await AplicarAsync(d.PublicId, s.Sha256, d.SessionItemPublicId is null ? Acta(d.PublicId) : null, Soporte(d.PublicId)));
    }

    private async Task<decimal> SaldoAsync(Person persona, MemberAccountConcept concepto) =>
        await _a.Db.MemberAccountTransactions.AsNoTracking()
            .Where(t => t.Concept == concepto && _a.Db.MemberAccounts.Any(c => c.Id == t.MemberAccountId && c.HolderPersonId == persona.Id))
            .SumAsync(t => t.Amount);

    // ---------------------------------------------------------------------------------------------------------- pruebas --

    [Fact]
    public async Task Revalorizar_al_5_por_ciento_con_IPC_del_4_y_fondo_menor_se_limita_y_se_prorratea_al_fondo()
    {
        await AportesAsync();
        Ipc(0.04m);
        var d = await RegistradaAsync(AssemblyDistributionKind.Revaluation, 9_000m, porcentaje: 0.05m);
        d.Status.Should().Be(AssemblyDistributionStatus.Simulated);
        d.Base.Should().Be(RevaluationBase.AverageBalance, "D13: el saldo promedio por defecto");

        var s = await SimuladaAsync(d.PublicId);
        s.LimitedByIpc.Applied.Should().BeTrue();
        s.AppliedRate.Should().Be(0.04m);
        s.ProratedToFund.Applied.Should().BeTrue();
        // Promedios del ejercicio: 1.000.000 × 31/365 = 84.931,51 y 2.000.000 × 31/365 = 169.863,01; al 4 %, 3.397 y 6.795 (10.192) > 9.000.
        s.Totals.Calculated.Should().Be(10_192m);
        s.Totals.Applied.Should().Be(9_000m, "US12-1: la suma de los abonos es el fondo aprobado");
        s.Lines.Select(l => l.AppliedAmount).Should().Equal(3_000m, 6_000m);

        var punto = Acta(d.PublicId);
        var soporte = Soporte(d.PublicId);
        _a.G.ActuarComo(_a.C.Asesora);
        var mismaPersona = await AplicarAsync(d.PublicId, s.Sha256, punto, soporte);
        mismaPersona.Error.Code.Should().Be(RegistroDeIntervenciones.CodigoConflicto, "quien registra no aplica");

        _a.G.ActuarComo(_a.C.Jefe);
        (await AplicarAsync(d.PublicId, s.Sha256, null, soporte)).Error.Code.Should().Be(ErroresDeAsamblea.CodigoInvalida, "sin el punto del acta");
        var aplicada = Ok(await AplicarAsync(d.PublicId, s.Sha256, punto, soporte));
        aplicada.Status.Should().Be(AssemblyDistributionStatus.Applied);
        aplicada.AppliedTotal.Should().Be(9_000m);
        aplicada.LegalLimit.Should().Be(0.04m);
        aplicada.Lines.Should().HaveCount(2).And.OnlyContain(l => l.OperationNumber!.StartsWith("RVA-"));
        (await SaldoAsync(_a.C.Ana, MemberAccountConcept.ContributionRevaluation)).Should().Be(3_000m);
        (await SaldoAsync(_a.C.Beto, MemberAccountConcept.ContributionRevaluation)).Should().Be(6_000m);
        (await _a.HechosAsync()).Should().ContainSingle(t => t == RevalorizacionDeAportesAplicadaV1.Type);
        var hecho = await _a.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == RevalorizacionDeAportesAplicadaV1.Type);
        hecho.OriginPublicId.Should().Be(d.PublicId, "un hecho masivo con origen la decisión");
    }

    [Fact]
    public async Task Una_decision_aplicada_no_se_aplica_otra_vez_se_reversa_entera_y_se_corrige_con_la_misma_acta()
    {
        await AportesAsync();
        Ipc(0.05m);
        var d = await RegistradaAsync(AssemblyDistributionKind.Revaluation, 1_000_000m, porcentaje: 0.03m, @base: RevaluationBase.YearEndBalance);
        var aplicada = await AplicadaAsync(d);
        aplicada.AppliedTotal.Should().Be(90_000m, "3 % de 1.000.000 y de 2.000.000 al cierre");

        var otraVez = await AplicarAsync(d.PublicId, new string('0', 64), null, Soporte(d.PublicId));
        otraVez.Error.Code.Should().Be(ErroresDeAsamblea.CodigoYaAplicada, "US12-2: no se aplica dos veces con la misma acta");

        _a.G.ActuarComo(_a.C.Asesora);
        (await Reversar().Handle(new ReverseAssemblyDistributionCommand(d.PublicId, "Error en el fondo") { OperationKey = Guid.NewGuid() }, default))
            .Error.Code.Should().Be(RegistroDeIntervenciones.CodigoConflicto, "quien la registró no la reversa");
        _a.G.ActuarComo(_a.C.Jefe);
        var reversada = Ok(await Reversar().Handle(new ReverseAssemblyDistributionCommand(d.PublicId, "El fondo del acta era otro")
        {
            OperationKey = Guid.NewGuid(),
        }, default));
        _a.Db.ChangeTracker.Clear();
        reversada.Status.Should().Be(AssemblyDistributionStatus.Reversed);
        (await SaldoAsync(_a.C.Ana, MemberAccountConcept.ContributionRevaluation)).Should().Be(0m);
        (await SaldoAsync(_a.C.Beto, MemberAccountConcept.ContributionRevaluation)).Should().Be(0m);
        (await _a.HechosAsync()).Should().ContainSingle(t => t == OperacionDeCuentaReversadaV1.Type, "un solo reverso para toda la decisión");
        var reverso = await _a.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == OperacionDeCuentaReversadaV1.Type);
        reverso.PayloadJson.Should().Contain(RevalorizacionDeAportesAplicadaV1.Type);

        // La corrección hereda el acta de la reversada y se aplica sin otro punto.
        var correccion = await RegistradaAsync(AssemblyDistributionKind.Revaluation, 60_000m, porcentaje: 0.02m, @base: RevaluationBase.YearEndBalance,
            corrige: d.PublicId);
        correccion.CorrectsNumber.Should().Be(d.Number);
        correccion.SessionItemPublicId.Should().Be(aplicada.SessionItemPublicId);
        var corregida = await AplicadaAsync(correccion);
        corregida.AppliedTotal.Should().Be(60_000m);

        (await Registrar().Handle(new RegisterAssemblyDistributionCommand(AssemblyDistributionKind.Revaluation, 2026, 1m, 0.01m, null, null, null, null,
                d.PublicId, "Otra corrección") { OperationKey = Guid.NewGuid() }, default))
            .Error.Code.Should().Be(ErroresDeAsamblea.CodigoCorreccionInvalida, "ya tiene su corrección aplicada");
    }

    [Fact]
    public async Task La_simulacion_que_cambio_no_se_aplica()
    {
        await AportesAsync();
        Ipc(0.05m);
        var d = await RegistradaAsync(AssemblyDistributionKind.Revaluation, 1_000_000m, porcentaje: 0.03m, @base: RevaluationBase.YearEndBalance);
        var s = await SimuladaAsync(d.PublicId);
        Ipc(0.02m);
        _a.G.ActuarComo(_a.C.Jefe);
        var r = await AplicarAsync(d.PublicId, s.Sha256, Acta(d.PublicId), Soporte(d.PublicId));
        r.Error.Code.Should().Be(ErroresDeAsamblea.CodigoVistaPreviaCambio, "otro IPC cambió lo que se aplicaría");
    }

    [Fact]
    public async Task Sin_soporte_o_sin_el_IPC_del_ejercicio_no_se_aplica()
    {
        await AportesAsync();
        var d = await RegistradaAsync(AssemblyDistributionKind.Revaluation, 1_000m, porcentaje: 0.03m);
        (await Simular().Handle(new SimulateAssemblyDistributionQuery(d.PublicId), default)).Error.Code.Should().Be("NationalValues.Missing");

        Ipc(0.05m);
        var s = await SimuladaAsync(d.PublicId);
        _a.G.ActuarComo(_a.C.Jefe);
        (await AplicarAsync(d.PublicId, s.Sha256, Acta(d.PublicId), null)).Error.Code.Should().Be(ErroresDeAsamblea.CodigoSinSoporte);
    }

    [Fact]
    public async Task El_registro_exige_la_entrega_K10_y_un_ejercicio_cerrado()
    {
        await AportesAsync();
        _a.C.Entrega = EntregaDeCartera.K9;
        _a.G.ActuarComo(_a.C.Asesora);
        (await Registrar().Handle(new RegisterAssemblyDistributionCommand(AssemblyDistributionKind.Revaluation, 2026, 1m, 0.01m, null, null, null, null, null,
            "Acta") { OperationKey = Guid.NewGuid() }, default)).Error.Code.Should().Be(ErroresDeAportes.CodigoEntregaNoDisponible);
        _a.C.Entrega = EntregaDeCartera.K10;
        (await Registrar().Handle(new RegisterAssemblyDistributionCommand(AssemblyDistributionKind.Revaluation, 2027, 1m, 0.01m, null, null, null, null, null,
            "Acta") { OperationKey = Guid.NewGuid() }, default)).Error.Code.Should().Be(ErroresDeAsamblea.CodigoInvalida);
    }

    [Fact]
    public async Task Los_excedentes_por_valor_van_a_aportes_ordinarios_por_el_mayor_residuo()
    {
        await AportesAsync();
        var d = await RegistradaAsync(AssemblyDistributionKind.SurplusDistribution, 100_000m, valor: 100_000m, @base: RevaluationBase.YearEndBalance);
        d.Destination.Should().Be(SurplusDestination.Contributions, "el destino por defecto de la entidad");
        var aplicada = await AplicadaAsync(d);
        aplicada.AppliedTotal.Should().Be(100_000m);
        (await SaldoAsync(_a.C.Ana, MemberAccountConcept.ContributionOrdinary)).Should().Be(33_333m, "un tercio del valor, hacia abajo");
        (await SaldoAsync(_a.C.Beto, MemberAccountConcept.ContributionOrdinary)).Should().Be(66_667m, "el peso que sobra va al mayor residuo (0,67)");
        (await _a.HechosAsync()).Should().Contain(ExcedentesDistribuidosV1.Type);
    }

    [Fact]
    public async Task Los_excedentes_a_ahorro_exigen_la_cuenta_de_destino_de_cada_asociado()
    {
        await AportesAsync();
        _a.G.HoyLocal = CuentasDePrueba.Hoy;
        var lineaDeAhorro = await _a.C.LineaDeCaptacionAsync("AHVISTA");
        var deAna = await _a.C.AbiertaAsync(_a.C.Apertura(lineaDeAhorro, _a.C.Ana));
        _a.G.HoyLocal = DiaDeLaAsamblea;
        var d = await RegistradaAsync(AssemblyDistributionKind.SurplusDistribution, 30_000m, porcentaje: 0.01m, @base: RevaluationBase.YearEndBalance,
            destino: SurplusDestination.Savings, linea: "AHVISTA");
        var s = await SimuladaAsync(d.PublicId);
        s.Warnings.Should().Contain(w => w.Contains("cuenta viva en la línea de ahorro de destino"));
        _a.G.ActuarComo(_a.C.Jefe);
        (await AplicarAsync(d.PublicId, s.Sha256, Acta(d.PublicId), Soporte(d.PublicId))).Error.Code.Should().Be(ErroresDeAsamblea.CodigoCuentasDeDestino,
            "Beto no tiene cuenta de ahorro");

        _a.G.HoyLocal = CuentasDePrueba.Hoy;
        _a.G.ActuarComo(_a.C.Asesora);
        await _a.C.AbiertaAsync(_a.C.Apertura(lineaDeAhorro, _a.C.Beto));
        _a.G.HoyLocal = DiaDeLaAsamblea;
        var otra = await SimuladaAsync(d.PublicId);
        _a.G.ActuarComo(_a.C.Jefe);
        var aplicada = Ok(await AplicarAsync(d.PublicId, otra.Sha256, (await _a.Db.SessionItems.AsNoTracking().FirstAsync()).PublicId,
            (await _a.Db.Attachments.AsNoTracking().FirstAsync()).PublicId));
        aplicada.Lines.Should().OnlyContain(l => l.DestinationAccountNumber != null);
        var principalDeAna = await _a.Db.MemberAccountTransactions.AsNoTracking()
            .Where(t => t.Concept == MemberAccountConcept.Principal && t.Kind == MemberAccountTransactionKind.AssemblyCredit
                        && _a.Db.MemberAccounts.Any(c => c.Id == t.MemberAccountId && c.PublicId == deAna.PublicId)).SumAsync(t => t.Amount);
        principalDeAna.Should().Be(10_000m);
    }

    [Fact]
    public async Task Los_excedentes_pagados_salen_por_orden_de_giro_sin_tocar_las_cuentas_y_no_se_reversan()
    {
        await AportesAsync();
        var d = await RegistradaAsync(AssemblyDistributionKind.SurplusDistribution, 30_000m, porcentaje: 0.01m, @base: RevaluationBase.YearEndBalance,
            destino: SurplusDestination.Payment);
        var aplicada = await AplicadaAsync(d);
        aplicada.Lines.Should().OnlyContain(l => l.PayoutOrderNumber != null);
        (await _a.Db.AccountPayoutOrders.AsNoTracking().Select(o => o.Purpose).ToListAsync()).Should().AllBeEquivalentTo(PayoutPurpose.SurplusPayment);
        (await _a.Db.MemberAccountTransactions.AsNoTracking().CountAsync(t => t.Kind == MemberAccountTransactionKind.AssemblyCredit)).Should().Be(0);
        var hecho = await _a.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == ExcedentesDistribuidosV1.Type);
        hecho.PayloadJson.Should().Contain("payments");
        (await Reversar().Handle(new ReverseAssemblyDistributionCommand(d.PublicId, "Error") { OperationKey = Guid.NewGuid() }, default))
            .Error.Code.Should().Be(ErroresDeAsamblea.CodigoPagoNoReversable);
    }

    [Fact]
    public async Task La_amortizacion_no_perfora_el_minimo_deja_fuera_a_los_deudores_y_sale_por_orden_de_giro()
    {
        await AportesAsync();
        _a.Db.Loans.Add(new Loan { Number = "CR-0001", DebtorPersonId = _a.C.Beto.Id, Status = LoanStatus.Active, CreditLineId = 1, BranchId = _a.C.Oficina.Id });
        await _a.Db.SaveChangesAsync();
        _a.Db.ChangeTracker.Clear();
        var d = await RegistradaAsync(AssemblyDistributionKind.Amortization, 5_000_000m, porcentaje: 0.6m, @base: RevaluationBase.YearEndBalance);
        var s = await SimuladaAsync(d.PublicId);
        s.Warnings.Should().Contain(w => w.Contains("créditos vivos"));
        var linea = s.Lines.Should().ContainSingle().Subject;
        linea.Person.FullName.Should().Contain("Ana");
        linea.CalculatedAmount.Should().Be(500_000m, "el 60 % de 1.000.000 bajaría del mínimo de 500.000");
        linea.LimitedByMinimum.Should().BeTrue();

        _a.G.ActuarComo(_a.C.Jefe);
        var aplicada = Ok(await AplicarAsync(d.PublicId, s.Sha256, Acta(d.PublicId), Soporte(d.PublicId)));
        aplicada.Lines.Single().OperationNumber.Should().StartWith("AMZ-");
        (await _a.Db.AccountPayoutOrders.AsNoTracking().SingleAsync()).Purpose.Should().Be(PayoutPurpose.ContributionRefund);
        (await SaldoAsync(_a.C.Ana, MemberAccountConcept.ContributionExtraordinary)).Should().Be(500_000m, "sale de los extraordinarios y deja el mínimo");
        (await _a.HechosAsync()).Should().Contain([AportesAmortizadosV1.Type, OrdenDeGiroEmitidaV2.Type]);
        (await Reversar().Handle(new ReverseAssemblyDistributionCommand(d.PublicId, "Error") { OperationKey = Guid.NewGuid() }, default))
            .Error.Code.Should().Be(ErroresDeAsamblea.CodigoGirosEnCurso, "la orden de giro sigue viva");
    }

    [Fact]
    public async Task Sin_alcance_total_la_decision_no_existe()
    {
        await AportesAsync();
        var d = await RegistradaAsync(AssemblyDistributionKind.Revaluation, 1_000m, porcentaje: 0.03m);
        _a.G.AlcanceActual = new Application.Common.Interfaces.Security.AlcanceDeCartera(false, new HashSet<int> { _a.C.Oficina.Id }, null, true,
            new HashSet<int>());
        (await Simular().Handle(new SimulateAssemblyDistributionQuery(d.PublicId), default)).Error.Code.Should().Be(ErroresDeAsamblea.CodigoInexistente);
        (await new ListAssemblyDistributionsQueryHandler(_a.Db, Distribuciones(), _a.G.Alcance).Handle(new ListAssemblyDistributionsQuery(), default))
            .Value.Should().BeEmpty();
    }
}
