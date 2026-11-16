using FluentAssertions;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Security;
using IngenIA365ERP.Application.Lending.AccrualProcesses;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Lending.Accrual;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.AccrualProcesses;

/// <summary>
/// Feature 024, fase 3 (pedido del dueño del 2026-11-16): el proceso de causación sobre el ciclo diario real (el crédito de Ana en la oficina Norte y, en
/// el reproceso, el de Darío en la oficina Sur, con el interés corriente en modo manual):
/// <list type="bullet">
/// <item>calcular arma el borrador con lo causado que espera, con y sin filtros, y deja fuera con su motivo lo que no entra (mes contable cerrado, fuera
/// del filtro, causación atrasada);</item>
/// <item>recalcular conserva la huella si nada cambió;</item>
/// <item>poner en firme pide la aprobación; la misma persona (o el mismo usuario) no la da, desde la bandeja tampoco, y otra persona presente la da:
/// entonces se ordena el lote con exactamente los hechos del borrador;</item>
/// <item>reprocesar deja intacto el crédito con abonos posteriores y toma, del otro, lo que quedó esperando (la diferencia);</item>
/// <item>un proceso abierto o en firme del mismo concepto impide otro que se cruce, y sin Cartera activada no se arma ninguno.</item>
/// </list>
/// </summary>
public class ProcesoDeCausacionTests : IAsyncLifetime
{
    private CausacionDePrueba _c = null!;
    private Branch _sur = null!;
    private User _otraSesionDeLaAsesora = null!;
    private readonly HashSet<DateOnly> _cerradas = [];

    private static readonly DateOnly Hoy = CausacionDePrueba.Hoy;
    private static readonly DateOnly D1 = Hoy.AddDays(1);
    private static readonly DateOnly D2 = Hoy.AddDays(2);

    private readonly IPermissionChecker _permisos = Substitute.For<IPermissionChecker>();
    private readonly IAutoridadDeOtroAprobador _otroAprobador = Substitute.For<IAutoridadDeOtroAprobador>();

    public async Task InitializeAsync()
    {
        _c = await CausacionDePrueba.CrearAsync();
        await _c.SembrarTiposDeHechoAsync();
        await VigenciaAsync(ParametrosDeCartera.CausacionModoInteresCorriente, ParametrosDeCartera.CausacionManual);
        _sur = new Branch { Name = "Oficina Sur", CreatedBy = "test" };
        _c.Db.Branches.Add(_sur);
        await _c.Db.SaveChangesAsync();
        _otraSesionDeLaAsesora = _c.D.G.Usuario(await _c.Db.People.SingleAsync(p => p.Id == _c.D.O.Asesora.PersonId), "asesora.otra@coop.test");

        // El estado contable por fecha: abierto salvo los días que la prueba cierre.
        _c.D.Contabilidad.EstadoDelPeriodoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result.Success(new EstadoDelPeriodoDto(true,
                _cerradas.Contains(ci.Arg<DateOnly>()) ? EstadoDePeriodoContable.Closed : EstadoDePeriodoContable.Open, ci.Arg<DateOnly>()))));
        _permisos.HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _otroAprobador.TienePermisoAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _otroAprobador.AlcanceAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(AlcanceDeInventario.Total);
    }

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------------------------------------------------ piezas --

    private async Task VigenciaAsync(string clave, string valor)
    {
        _c.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = clave, ScopeKind = ParameterScopeKind.None, Value = valor, ValidFrom = new DateOnly(2026, 1, 1),
            Reason = "prueba", CreatedBy = "test",
        });
        await _c.Db.SaveChangesAsync();
    }

    private ProcesosDeCausacion Procesos() => new(_c.Db,
        [new ContabilizacionDeLoCausado(_c.Db, new CausacionPorContabilizar(_c.Db), _c.D.Contabilidad, Validacion(), _c.D.G.Actor, _c.D.G.Reloj),
            Reconocimiento()],
        _c.D.G.Alcance, _c.D.Series(), _c.D.O.Cerrojo, _c.D.Numerador(), _c.D.G.Auditoria, _c.D.G.Reloj);

    /// <summary>2026-11-16 (feature 024, fase 4): la clase que reconoce la cuota del período, con los hechos emitidos por la persona que opera.</summary>
    private ReconocimientoDelPeriodo Reconocimiento()
    {
        var emision = new EmisionDeCartera(_c.Db, new IngenIA365ERP.Application.Common.Integration.EmisorDeMensajes(_c.Db, _c.D.G.Actor, _c.D.G.Reloj),
            _c.D.FechaContable(), new ModoDePasoDeCartera(_c.Db, _c.D.Contabilidad), _c.D.G.Actor);
        return new ReconocimientoDelPeriodo(_c.Db, _c.Cargador(), new IngenIA365ERP.Application.Lending.Accrual.LectorDePoliticaDeReconocimiento(_c.D.Parametros()),
            _c.D.Contabilidad, new IngenIA365ERP.Application.Lending.Accrual.PartesDeHechosDeCartera(_c.Db, emision, _c.D.FechaContable()), _c.D.Registro(),
            _c.D.O.Cerrojo, _c.D.G.Actor, _c.D.G.Reloj);
    }

    private ValidacionPreviaContableDeCartera Validacion() =>
        new(_c.D.Contabilidad, _c.D.G.Parametros(), _c.D.G.Reloj, null, new ModoDePasoDeCartera(_c.Db, _c.D.Contabilidad), _c.D.Alertas, _c.Db);

    private EntradaDeProcesoDeCausacion Entrada(ProcesosDeCausacion procesos) =>
        new(_c.Db, procesos, new CausacionPorContabilizar(_c.Db), _c.D.G.Parametros(), _c.D.G.Reloj);

    private MotorDeAprobaciones Motor(ProcesosDeCausacion procesos)
    {
        var limites = Substitute.For<ILimitesPorPermiso>();
        limites.MontoMaximoAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns((decimal?)null);
        var alcance = Substitute.For<IAlcanceDeInventario>();
        alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(AlcanceDeInventario.Total);
        var fuente = new FuenteDeAprobacionDeCausacion(_c.Db, new PersonasDeLosUsuarios(_c.Db), procesos);
        return new MotorDeAprobaciones(_c.Db, _c.D.G.Actor, _permisos, alcance, limites, _otroAprobador, Substitute.For<IAvisosDeAprobacion>(),
            new VistaDeSolicitudes(_c.Db, [fuente]), _c.D.G.Reloj);
    }

    private async Task<Result<AccrualProcessDto>> ArmarAsync(DateOnly desde, DateOnly hasta, Guid? oficina = null, Guid? linea = null,
        AccrualProcessConcepts concepto = AccrualProcessConcepts.CurrentInterest, AccrualProcessKind clase = AccrualProcessKind.Posting)
    {
        var procesos = Procesos();
        var r = await new CreateAccrualProcessCommandHandler(_c.Db, procesos, Entrada(procesos), _c.D.Numerador(), _c.D.G.Actor)
            .Handle(new CreateAccrualProcessCommand(AccrualProcessPeriodicity.Monthly, null, desde, hasta, [concepto], linea, oficina, Kind: clase)
                { OperationKey = Guid.NewGuid() }, default);
        _c.Db.ChangeTracker.Clear();
        return r;
    }

    private async Task<AccrualProcessDto> ArmadoAsync(DateOnly desde, DateOnly hasta, Guid? oficina = null, Guid? linea = null)
    {
        var r = await ArmarAsync(desde, hasta, oficina, linea);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    private async Task<Result<AccrualProcessDto>> PedirFirmaAsync(AccrualProcessDto p, string? huella = null)
    {
        var procesos = Procesos();
        var r = await new ConfirmAccrualProcessCommandHandler(_c.Db, procesos, Motor(procesos), _c.D.G.Permisos, _c.D.G.Actor, _c.D.G.Reloj)
            .Handle(new ConfirmAccrualProcessCommand(p.PublicId, "Cierre de la causación de diciembre", huella ?? p.ContentSha256!)
                { OperationKey = Guid.NewGuid() }, default);
        _c.Db.ChangeTracker.Clear();
        return r;
    }

    private async Task<Result<DecisionResultDto>> DecidirAsync(AccrualProcessDto p, User? presente, ApprovalMethod metodo = ApprovalMethod.InPersonTotp)
    {
        var r = await Motor(Procesos()).DecidirAsync(new DecisionDeAprobacion(p.Approval!.RequestPublicId, ApprovalDecisionKind.Approve, null,
            p.Approval.ContentSha256, presente is null ? null : new AprobadorPresente(presente.Id, presente.Email!, metodo, null)), default);
        _c.Db.ChangeTracker.Clear();
        return r;
    }

    private async Task<AccrualProcessDto> LeerAsync(Guid proceso)
    {
        var r = await new GetAccrualProcessQueryHandler(Procesos()).Handle(new GetAccrualProcessQuery(proceso), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    private async Task<IReadOnlyList<AccrualProcessLineDto>> LineasAsync(Guid proceso)
    {
        var r = await new ListAccrualProcessLinesQueryHandler(_c.Db, Procesos(), _c.D.G.Alcance)
            .Handle(new ListAccrualProcessLinesQuery(proceso, PageSize: 200), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value.Items;
    }

    /// <summary>Lo que espera del interés corriente en [desde, hasta] (el punto de la fase 1).</summary>
    private Task<IReadOnlyList<CausacionPendienteDeContabilizar>> PendienteAsync(DateOnly desde, DateOnly hasta) =>
        new CausacionPorContabilizar(_c.Db).ResumenAsync(desde, hasta, [InteresCausadoV1.Type], default);

    private async Task<AccrualProcessDto> EnFirmeAsync(AccrualProcessDto borrador)
    {
        _c.D.G.ActuarComo(_c.D.O.Asesora);
        var pedida = await PedirFirmaAsync(borrador);
        pedida.IsSuccess.Should().BeTrue(pedida.IsFailure ? $"{pedida.Error.Code}: {pedida.Error.Message}" : string.Empty);
        var aprobada = await DecidirAsync(pedida.Value, _c.D.O.Analista);
        aprobada.IsSuccess.Should().BeTrue(aprobada.IsFailure ? $"{aprobada.Error.Code}: {aprobada.Error.Message}" : string.Empty);
        return await LeerAsync(borrador.PublicId);
    }

    // ------------------------------------------------------------------------------------------------------------ pruebas --

    /// <summary>
    /// 2026-11-16 (feature 024, fase 4): con <c>Causacion.Momento = InicioDelCiclo</c> y el interés corriente en «Manual», el ciclo no reconoce; el
    /// proceso de la clase «reconocer» arma la cuota que se cruza con el ciclo —lo que va a causar desde el día siguiente al último causado hasta su
    /// vencimiento—, y en firme (cuatro ojos) escribe la fila <c>PeriodRecognition</c> y emite <c>CuotaReconocida</c>; la causación siguiente lo consume.
    /// Un segundo reconocimiento que se cruza con el que está en firme no se arma.
    /// </summary>
    [Fact]
    public async Task Reconocer_la_cuota_del_ciclo_la_arma_en_borrador_y_en_firme_la_reconoce_y_la_causacion_la_consume()
    {
        await VigenciaAsync(ParametrosDeCartera.CausacionMomento, ParametrosDeCartera.CausacionAlInicioDelCiclo);
        await _c.CausarHastaAsync(D1);
        await _c.CausarHastaAsync(D2);
        (await _c.Db.LoanTransactions.AnyAsync(t => t.Kind == LoanTransactionKind.PeriodRecognition)).Should()
            .BeFalse("con el interés en «Manual» lo reconoce el proceso, no el ciclo");
        _c.D.G.ActuarComo(_c.D.O.Asesora);

        (await ArmarAsync(Hoy, new DateOnly(2026, 12, 31), concepto: AccrualProcessConcepts.DefaultInterest, clase: AccrualProcessKind.PeriodRecognition))
            .Error.Code.Should().Be(ErroresDeProcesosDeCausacion.CodigoConceptoNoReconocible, "la mora nunca se reconoce");

        var r = await ArmarAsync(Hoy, new DateOnly(2026, 12, 31), clase: AccrualProcessKind.PeriodRecognition);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var p = r.Value;
        p.Kind.Should().Be(AccrualProcessKind.PeriodRecognition);
        p.CanConfirm.Should().BeTrue();
        var linea = (await LineasAsync(p.PublicId)).Should().ContainSingle().Subject;
        linea.Concept.Should().Be(LoanConcept.RecognizedInterest, "el reconocimiento de fábrica es contra el ingreso diferido");
        linea.InstallmentNumber.Should().Be(1);
        linea.RecognitionDate.Should().Be(D2.AddDays(1), "desde el día siguiente al último causado");
        linea.Amount.Should().BeGreaterThan(0m);
        linea.Status.Should().Be(AccrualProcessItemStatus.Included);
        (await _c.Db.LoanTransactions.AnyAsync(t => t.Kind == LoanTransactionKind.PeriodRecognition)).Should().BeFalse("el borrador no reconoce nada");

        var enFirme = await EnFirmeAsync(p);
        enFirme.Status.Should().Be(AccrualProcessStatus.Confirmed);
        var fila = await _c.Db.LoanTransactions.AsNoTracking().SingleAsync(t => t.Kind == LoanTransactionKind.PeriodRecognition);
        (fila.Concept, fila.InstallmentNumber, fila.ValueDate, fila.Amount).Should().Be((LoanConcept.RecognizedInterest, (int?)1, D2.AddDays(1), linea.Amount));
        fila.ProcessRunId.Should().NotBeNull("la escribe la corrida del proceso");
        (await _c.Db.IntegrationMessages.CountAsync(m => m.Type == CuotaReconocidaV1.Type)).Should().Be(1);

        var otro = await ArmarAsync(Hoy, new DateOnly(2026, 12, 31), clase: AccrualProcessKind.PeriodRecognition);
        otro.Error.Code.Should().Be(ErroresDeProcesosDeCausacion.CodigoSeCruza, "el ciclo ya está reconocido por un proceso en firme de la misma clase");

        await _c.CausarHastaAsync(D2.AddDays(1));
        var causado = await _c.Db.LoanTransactions.AsNoTracking()
            .Where(t => t.ValueDate == D2.AddDays(1) && t.Kind == LoanTransactionKind.Accrual && t.Concept == LoanConcept.CurrentInterest).SumAsync(t => t.Amount);
        causado.Should().BeGreaterThan(0m);
        (await _c.Db.LoanTransactions.AsNoTracking()
            .Where(t => t.ValueDate == D2.AddDays(1) && t.Kind == LoanTransactionKind.Accrual && t.Concept == LoanConcept.RecognizedInterest).SumAsync(t => t.Amount))
            .Should().Be(-causado, "la causación del día consume lo reconocido");
    }

    [Fact]
    public async Task Calcular_arma_el_borrador_con_lo_causado_que_espera_y_recalcular_conserva_la_huella()
    {
        await _c.CausarHastaAsync(D1);
        await _c.CausarHastaAsync(D2);
        _c.D.G.ActuarComo(_c.D.O.Asesora);
        var esperan = await PendienteAsync(D1, D2);
        esperan.Should().HaveCount(2, "un hecho de interés corriente por día causado");

        var p = await ArmadoAsync(D1, D2);

        p.Number.Should().Be("PCA-000001");
        p.Status.Should().Be(AccrualProcessStatus.Draft);
        p.IncludedFactCount.Should().Be(2);
        p.ExcludedFactCount.Should().Be(0);
        p.IncludedAmount.Should().Be(esperan.Sum(e => e.Total));
        p.LoanCount.Should().Be(1);
        p.CanConfirm.Should().BeTrue();
        p.Issues.Should().NotContain(i => i.Blocking);
        p.TotalsByConcept.Should().ContainSingle().Which.IncludedAmount.Should().Be(p.IncludedAmount);
        var lineas = await LineasAsync(p.PublicId);
        lineas.Should().ContainSingle().Which.Should().Match<AccrualProcessLineDto>(l =>
            l.LoanNumber == _c.Credito.Number && l.Concept == LoanConcept.CurrentInterest && l.Amount == p.IncludedAmount
            && l.Status == AccrualProcessItemStatus.Included);
        (await PendienteAsync(D1, D2)).Should().HaveCount(2, "el borrador no contabiliza nada");

        var procesos = Procesos();
        var recalculado = await new RecalculateAccrualProcessCommandHandler(_c.Db, procesos, Entrada(procesos), Motor(procesos), _c.D.G.Actor)
            .Handle(new RecalculateAccrualProcessCommand(p.PublicId) { OperationKey = Guid.NewGuid() }, default);
        recalculado.IsSuccess.Should().BeTrue(recalculado.IsFailure ? recalculado.Error.Message : null);
        recalculado.Value.ContentSha256.Should().Be(p.ContentSha256, "nada cambió");
        recalculado.Value.CalculationCount.Should().Be(2);
        _c.Db.ChangeTracker.Clear();
        (await _c.Db.AccrualProcessFacts.CountAsync()).Should().Be(2, "el recálculo reemplaza el borrador (lo anterior queda de baja)");
    }

    [Fact]
    public async Task Los_filtros_de_oficina_y_de_linea_y_el_mes_contable_cerrado_dejan_fuera_con_su_motivo()
    {
        await _c.CausarHastaAsync(D1);
        _c.D.G.ActuarComo(_c.D.O.Asesora);

        // Otra oficina: el hecho es de la sucursal Norte; nada que tomar.
        var sur = await ArmadoAsync(D1, D1, oficina: _sur.PublicId);
        sur.IncludedFactCount.Should().Be(0);
        sur.Facts.Should().BeEmpty();
        sur.CanConfirm.Should().BeFalse();
        sur.Issues.Should().Contain(i => i.Code == "Lending.AccrualProcess.NothingPending" && !i.Blocking);
        await AnularAsync(sur);

        // La oficina y la línea del crédito: entra.
        var norte = await ArmadoAsync(D1, D1, oficina: _c.D.O.Norte.PublicId, linea: _c.D.O.Linea.PublicId);
        norte.IncludedFactCount.Should().Be(1);
        await AnularAsync(norte);

        // El mes contable de ese día está cerrado: el hecho queda fuera, sin bloquear; no hay nada para poner en firme.
        _cerradas.Add(D1);
        var cerrado = await ArmadoAsync(D1, D1);
        cerrado.Facts.Should().ContainSingle().Which.Should().Match<AccrualProcessFactDto>(h =>
            h.Status == AccrualProcessItemStatus.Excluded && h.Reason == AccrualExclusionReason.AccountingPeriodClosed && !h.Blocking);
        cerrado.HasBlockingIssues.Should().BeFalse();
        cerrado.CanConfirm.Should().BeFalse();
        (await LineasAsync(cerrado.PublicId)).Should().ContainSingle().Which.Reason.Should().Be(AccrualExclusionReason.AccountingPeriodClosed);
        var pedida = await PedirFirmaAsync(cerrado);
        pedida.IsFailure.Should().BeTrue();
        pedida.Error.Code.Should().Be(ErroresDeProcesosDeCausacion.CodigoNadaQueContabilizar);
    }

    [Fact]
    public async Task La_causacion_atrasada_bloquea_la_firma_y_lo_dice()
    {
        await _c.CausarHastaAsync(D1);
        _c.Ir(D2);
        _c.D.G.ActuarComo(_c.D.O.Asesora);

        var p = await ArmadoAsync(D1, D2);

        p.HasBlockingIssues.Should().BeTrue();
        p.CanConfirm.Should().BeFalse();
        p.Issues.Should().Contain(i => i.Code == "Lending.AccrualProcess.AccrualBehind" && i.Blocking && i.Message.Contains(_c.Credito.Number));
        var pedida = await PedirFirmaAsync(p);
        pedida.Error.Code.Should().Be(ErroresDeProcesosDeCausacion.CodigoBloqueado);
    }

    [Fact]
    public async Task Poner_en_firme_pide_la_aprobacion_de_otra_persona_presente_y_ordena_el_lote_con_lo_del_borrador()
    {
        await _c.CausarHastaAsync(D1);
        await _c.CausarHastaAsync(D2);
        _c.D.G.ActuarComo(_c.D.O.Asesora);
        var borrador = await ArmadoAsync(D1, D2);

        // Una huella vieja no se pide.
        (await PedirFirmaAsync(borrador, new string('0', 64))).Error.Code.Should().Be(ErroresDeProcesosDeCausacion.CodigoContenidoCambiado);

        // Sin aprobación no queda en firme: espera, y lo causado sigue esperando.
        var pedida = await PedirFirmaAsync(borrador);
        pedida.IsSuccess.Should().BeTrue(pedida.IsFailure ? pedida.Error.Message : null);
        pedida.Value.Status.Should().Be(AccrualProcessStatus.PendingApproval);
        pedida.Value.Approval!.Status.Should().Be(nameof(ApprovalRequestStatus.Pending));
        pedida.Value.Approval.PermissionCode.Should().Be("Lending.AccrualProcesses.Confirm");
        (await PendienteAsync(D1, D2)).Should().HaveCount(2);

        // La misma persona, con otro usuario, presente: no.
        var misma = await DecidirAsync(pedida.Value, _otraSesionDeLaAsesora);
        misma.Error.Code.Should().Be(ErroresDeProcesosDeCausacion.CodigoAutoaprobacion);
        // El mismo usuario: el motor no lo deja.
        (await DecidirAsync(pedida.Value, _c.D.O.Asesora)).Error.Code.Should().Be("Approvals.SelfApprovalForbidden");
        // Otra persona desde su propia sesión (la bandeja): no, tiene que estar presente.
        _c.D.G.ActuarComo(_c.D.O.Analista);
        (await DecidirAsync(pedida.Value, null)).Error.Code.Should().Be(ErroresDeProcesosDeCausacion.CodigoPresenciaRequerida);
        _c.D.G.ActuarComo(_c.D.O.Asesora);
        (await LeerAsync(borrador.PublicId)).Status.Should().Be(AccrualProcessStatus.PendingApproval, "ninguna de esas decisiones quedó");

        // Otra persona presente: queda en firme y ordena el lote con exactamente los hechos del borrador.
        var aprobada = await DecidirAsync(pedida.Value, _c.D.O.Analista);
        aprobada.IsSuccess.Should().BeTrue(aprobada.IsFailure ? $"{aprobada.Error.Code}: {aprobada.Error.Message}" : null);
        var enFirme = await LeerAsync(borrador.PublicId);
        enFirme.Status.Should().Be(AccrualProcessStatus.Confirmed);
        enFirme.Approval!.ApprovedBy.Should().Be(_c.D.O.Analista.Email);
        enFirme.Approval.Method.Should().Be(ApprovalMethod.InPersonTotp);
        enFirme.Batch.Should().NotBeNull();
        enFirme.Batch!.Facts.Should().Be(2);
        var lote = await _c.Db.IntegrationBatches.AsNoTracking().SingleAsync(b => b.PublicId == enFirme.Batch.BatchPublicId);
        lote.Trigger.Should().Be(BatchTrigger.Manual);
        lote.RequestedByUserId.Should().Be(_c.D.O.Asesora.Id, "el lote lo pide quien pidió la firma");
        var mensajes = enFirme.Facts.Select(h => h.MessagePublicId!.Value).ToList();
        (await _c.Db.IntegrationMessageDeliveries.AsNoTracking().Where(d => mensajes.Contains(d.Message!.PublicId)).Select(d => d.BatchId).ToListAsync())
            .Should().OnlyContain(b => b == lote.Id);
        (await PendienteAsync(D1, D2)).Should().BeEmpty();
        (await _c.D.G.EventosAsync()).Should().Contain("Lending.AccrualProcess.Confirmed");
    }

    [Fact]
    public async Task Un_proceso_abierto_o_en_firme_del_mismo_concepto_impide_otro_que_se_cruce()
    {
        await _c.CausarHastaAsync(D1);
        await _c.CausarHastaAsync(D2);
        _c.D.G.ActuarComo(_c.D.O.Asesora);
        var primero = await ArmadoAsync(D1, D1);

        var abierto = await ArmarAsync(D2, D2);
        abierto.Error.Code.Should().Be(ErroresDeProcesosDeCausacion.CodigoOtroAbierto, "un proceso a la vez por concepto");
        (await ArmarAsync(D2, D2, concepto: AccrualProcessConcepts.DefaultInterest)).Error.Code
            .Should().Be(ErroresDeProcesosDeCausacion.CodigoConceptoNoManual, "la mora sigue automática y no tiene nada esperando");

        await EnFirmeAsync(primero);
        var cruzado = await ArmarAsync(D1, D2);
        cruzado.Error.Code.Should().Be(ErroresDeProcesosDeCausacion.CodigoSeCruza);
        (await ArmarAsync(D2, D2)).IsSuccess.Should().BeTrue("otro rango del mismo concepto no se cruza");
    }

    [Fact]
    public async Task Sin_Cartera_activada_no_se_arma()
    {
        var setup = await _c.Db.LendingSetups.SingleAsync();
        setup.ActivationDate = null;
        await _c.Db.SaveChangesAsync();
        _c.D.G.ActuarComo(_c.D.O.Asesora);

        (await ArmarAsync(Hoy, Hoy)).Error.Code.Should().Be("Lending.NotActivated");
    }

    [Fact]
    public async Task Reprocesar_deja_intacto_el_credito_con_abonos_y_toma_del_otro_lo_que_quedo_esperando()
    {
        // Darío en la oficina Sur: sus hechos van aparte de los de Ana (Norte).
        var dario = await _c.DesembolsarAsync(_c.D.O.Solicitud(1_000_000m, 12, _c.D.O.Dario) with { OfficePublicId = _sur.PublicId });
        await _c.CausarHastaAsync(D1);
        await _c.CausarHastaAsync(D2);
        _c.D.G.ActuarComo(_c.D.O.Asesora);

        // El mes contable del segundo día está cerrado al poner en firme: ese día queda esperando.
        _cerradas.Add(D2);
        var p = await ArmadoAsync(D1, D2);
        p.IncludedFactCount.Should().Be(2, "un hecho por oficina el primer día");
        p.ExcludedFactCount.Should().Be(2);
        var enFirme = await EnFirmeAsync(p);
        enFirme.Status.Should().Be(AccrualProcessStatus.Confirmed);
        var esperanDelSegundo = await PendienteAsync(D2, D2);
        esperanDelSegundo.Should().HaveCount(1).And.OnlyContain(e => e.Hechos == 2);

        // Ana abona después de la puesta en firme; el mes se reabre.
        _c.Db.LoanOperations.Add(new LoanOperation
        {
            LoanId = _c.Credito.Id, Kind = LoanOperationKind.Payment, Number = "RC-PRUEBA-1", Sequence = 99, ValueDate = D2, AccountingDate = D2,
            RegisteredAt = enFirme.ConfirmedAt!.Value.AddMinutes(5), Amount = 50_000m, ActorKind = ActorKind.Person, CreatedBy = "test",
        });
        await _c.Db.SaveChangesAsync();
        _c.Db.ChangeTracker.Clear();
        _cerradas.Clear();

        _c.D.G.Conceder("Lending.AccrualProcesses.Reprocess");
        var procesos = Procesos();
        var reproceso = await new ReprocessAccrualProcessCommandHandler(_c.Db, procesos, _c.D.Numerador(), _c.D.G.Actor)
            .Handle(new ReprocessAccrualProcessCommand(p.PublicId, "Se reabrió el mes del segundo día") { OperationKey = Guid.NewGuid() }, default);
        reproceso.IsSuccess.Should().BeTrue(reproceso.IsFailure ? $"{reproceso.Error.Code}: {reproceso.Error.Message}" : null);
        _c.Db.ChangeTracker.Clear();
        var r = reproceso.Value;
        r.ReprocessOf!.Number.Should().Be(p.Number);
        r.Facts.Should().HaveCount(2);
        r.Facts.Should().ContainSingle(h => h.Status == AccrualProcessItemStatus.Included && h.Branch == _sur.Name, "lo de Darío que quedó esperando");
        r.Facts.Should().ContainSingle(h => h.Reason == AccrualExclusionReason.LaterPayment, "el hecho de Ana, que abonó, queda intacto");
        var lineas = await LineasAsync(r.PublicId);
        lineas.Should().ContainSingle(l => l.LoanNumber == _c.Credito.Number).Which.Reason.Should().Be(AccrualExclusionReason.LaterPayment);
        var deDario = lineas.Should().ContainSingle(l => l.LoanNumber == dario.Number).Subject;
        deDario.Status.Should().Be(AccrualProcessItemStatus.Included);
        deDario.PreviousAmount.Should().BeGreaterThan(0m, "lo que el proceso reprocesado contabilizó de Darío (el primer día)");
        deDario.Amount.Should().Be(r.IncludedAmount, "sólo la diferencia: lo del segundo día");

        _c.D.G.ActuarComo(_c.D.O.Asesora);
        var enFirmeElReproceso = await EnFirmeAsync(r);
        enFirmeElReproceso.Status.Should().Be(AccrualProcessStatus.Confirmed);
        (await LeerAsync(p.PublicId)).Should().Match<AccrualProcessDto>(x => x.Status == AccrualProcessStatus.Reprocessed
            && x.ReprocessedBy!.PublicId == r.PublicId);
        (await PendienteAsync(D2, D2)).Should().ContainSingle().Which.Hechos.Should().Be(1, "lo de Ana sigue esperando, intacto");
    }

    [Fact]
    public async Task Un_reproceso_exige_su_permiso_al_pedir_la_firma()
    {
        await _c.CausarHastaAsync(D1);
        _c.D.G.ActuarComo(_c.D.O.Asesora);
        var p = await EnFirmeAsync(await ArmadoAsync(D1, D1));
        var procesos = Procesos();
        var r = await new ReprocessAccrualProcessCommandHandler(_c.Db, procesos, _c.D.Numerador(), _c.D.G.Actor)
            .Handle(new ReprocessAccrualProcessCommand(p.PublicId, "Revisión") { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        _c.Db.ChangeTracker.Clear();
        r.Value.IncludedFactCount.Should().Be(0, "todo lo del rango ya estaba contabilizado");

        _c.D.G.Conceder();
        (await PedirFirmaAsync(r.Value)).Error.Code.Should().Be("Lending.Permission.Required");
        _c.D.G.Conceder("Lending.AccrualProcesses.Reprocess");
        (await PedirFirmaAsync(r.Value)).Error.Code.Should().Be(ErroresDeProcesosDeCausacion.CodigoNadaQueContabilizar,
            "lo ya contabilizado no se vuelve a contabilizar");
    }

    private async Task AnularAsync(AccrualProcessDto p)
    {
        var procesos = Procesos();
        var r = await new VoidAccrualProcessCommandHandler(_c.Db, procesos, Motor(procesos), _c.D.G.Actor, _c.D.G.Reloj)
            .Handle(new VoidAccrualProcessCommand(p.PublicId, "Prueba de filtros") { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Status.Should().Be(AccrualProcessStatus.Voided);
        _c.Db.ChangeTracker.Clear();
    }
}
