using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
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
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Processes;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Enums.Parameters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

    /// <summary>Un parámetro de la cooperativa con vigencia desde antes del ejercicio (la retención de la asamblea, K10).</summary>
    private void Parametro(string clave, string valor)
    {
        _a.C.EntregaDeParametros = EntregaDeCartera.K10;
        _a.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = clave, ScopeKind = ParameterScopeKind.None, Value = valor, ValidFrom = new DateOnly(2020, 1, 1),
            Reason = "prueba", CreatedAt = DateTime.UtcNow.AddDays(-10),
        });
        _a.Db.SaveChanges();
        _a.Db.ChangeTracker.Clear();
    }

    private void RetencionConfigurada(string tarifaRevalorizacion = "0.10", string tarifaExcedentes = "0.20", string? baseMinimaUvt = null)
    {
        Parametro(ParametrosDeCartera.AsambleaRetencionConcepto, "RETEFTE-ASAMBLEA");
        Parametro(ParametrosDeCartera.AsambleaRetencionTarifaRevalorizacion, tarifaRevalorizacion);
        Parametro(ParametrosDeCartera.AsambleaRetencionTarifaExcedentes, tarifaExcedentes);
        if (baseMinimaUvt is not null) Parametro(ParametrosDeCartera.AsambleaRetencionBaseMinimaUvt, baseMinimaUvt);
    }

    private void Uvt(decimal valor) =>
        _a.G.Valores.LeerAsync(CodigosDeValorNacional.Uvt, string.Empty, Arg.Any<DateOnly>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ValorNacionalVigente(Guid.NewGuid(), Guid.NewGuid(), CodigosDeValorNacional.Uvt, string.Empty, valor, "COP", null,
                new DateOnly(2026, 1, 1), null, null, "DIAN (valor de prueba)", 1, DateTime.UtcNow.AddMonths(-2), EstadoDeRevision.Validada)));

    private readonly ITabularFileReader _lector = Substitute.For<ITabularFileReader>();
    private TablaLeida _tabla = new([], [], "xlsx");

    /// <summary>Importa la plantilla de la base de los retornos (respuesta 90 C, E261) con las filas dadas: decisión, persona, base y concepto.</summary>
    private async Task<Result<ImportResultDto>> ImportarBaseAsync(string?[][] filas, ModoDeImportacion modo = ModoDeImportacion.Apply)
    {
        _tabla = new TablaLeida([PlantillaDeBaseDeRetornos.C.Decision, PlantillaDeBaseDeRetornos.C.Persona, PlantillaDeBaseDeRetornos.C.Base, PlantillaDeBaseDeRetornos.C.Concepto],
            filas.Select((f, i) => new FilaLeida(i + 2, f)).ToList(), "xlsx");
        _lector.LeerHojaAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Result.Success(_tabla)));
        _a.G.Permisos.EsMaestroGlobal.Returns(true);
        var ejecutor = new EjecutorDeImportacion(_a.Db, _lector, _a.G.Permisos,
            new ServiceCollection().AddSingleton(_a.G.Mongo).AddSingleton<IApplicationDbContext>(_a.Db).BuildServiceProvider());
        var r = await new ImportAssemblyReturnBasesCommandHandler(_a.Db, ejecutor, Distribuciones())
            .Handle(new ImportAssemblyReturnBasesCommand(modo, new ArchivoDeImportacion("base.xlsx", [1, 2, 3]), "Base de los retornos") { OperationKey = Guid.NewGuid() }, default);
        _a.Db.ChangeTracker.Clear();
        return r;
    }

    private static IReadOnlyList<ErrorDeFila> ErroresDeLaBase(Result<ImportResultDto> r) =>
        r.Error.Should().BeOfType<ErrorConDatos>().Subject.Data.Should().BeOfType<ImportResultDto>().Subject.Errors;

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
    public async Task Los_excedentes_a_ahorro_sin_cuenta_de_destino_se_capitalizan_en_los_aportes_del_asociado()
    {
        await AportesAsync();
        _a.G.HoyLocal = CuentasDePrueba.Hoy;
        var lineaDeAhorro = await _a.C.LineaDeCaptacionAsync("AHVISTA");
        var deAna = await _a.C.AbiertaAsync(_a.C.Apertura(lineaDeAhorro, _a.C.Ana));
        _a.G.HoyLocal = DiaDeLaAsamblea;
        var d = await RegistradaAsync(AssemblyDistributionKind.SurplusDistribution, 30_000m, porcentaje: 0.01m, @base: RevaluationBase.YearEndBalance,
            destino: SurplusDestination.Savings, linea: "AHVISTA");
        var s = await SimuladaAsync(d.PublicId);
        s.Warnings.Should().Contain(w => w.Contains("se capitaliza en sus aportes"));
        var antesBeto = await SaldoAsync(_a.C.Beto, MemberAccountConcept.ContributionOrdinary);
        _a.G.ActuarComo(_a.C.Jefe);
        var aplicada = Ok(await AplicarAsync(d.PublicId, s.Sha256, Acta(d.PublicId), Soporte(d.PublicId)));
        aplicada.Lines.Should().Contain(l => l.DestinationAccountNumber != null, "Ana tiene cuenta de ahorro");
        aplicada.Lines.Should().Contain(l => l.DestinationAccountNumber == null, "Beto no tiene cuenta: capitaliza");
        var principalDeAna = await _a.Db.MemberAccountTransactions.AsNoTracking()
            .Where(t => t.Concept == MemberAccountConcept.Principal && t.Kind == MemberAccountTransactionKind.AssemblyCredit
                        && _a.Db.MemberAccounts.Any(c => c.Id == t.MemberAccountId && c.PublicId == deAna.PublicId)).SumAsync(t => t.Amount);
        principalDeAna.Should().Be(10_000m);
        (await SaldoAsync(_a.C.Beto, MemberAccountConcept.ContributionOrdinary)).Should().BeGreaterThan(antesBeto, "su excedente entró a sus aportes");
        (await _a.Db.MemberAccounts.AsNoTracking().CountAsync(c => c.HolderPersonId == _a.C.Beto.Id && c.Nature == LineNature.Deposit))
            .Should().Be(0, "no se le abrió ninguna cuenta");
    }

    /// <summary>
    /// Respuesta 92 del dueño (E258): Tesorería rechaza el giro del excedente de un asociado: el valor no vuelve a ninguna cuenta (sigue por pagar a su favor), la
    /// orden queda pendiente de regiro y se vuelve a girar —una sola vez por orden, sólo la rechazada, sólo desde su decisión—.
    /// </summary>
    [Fact]
    public async Task E258_el_giro_rechazado_de_un_excedente_se_regira_y_mientras_tanto_sigue_por_pagar_al_asociado()
    {
        await AportesAsync();
        var d = await RegistradaAsync(AssemblyDistributionKind.SurplusDistribution, 30_000m, porcentaje: 0.01m, @base: RevaluationBase.YearEndBalance,
            destino: SurplusDestination.Payment);
        await AplicadaAsync(d);
        var ordenes = await _a.Db.AccountPayoutOrders.AsNoTracking().OrderBy(o => o.Id).ToListAsync();
        ordenes.Should().HaveCount(2);

        // Una orden que sigue emitida no se regira.
        var regiro = new RegiroDeOrdenesDeCuentas(_a.Db, Ordenes(), _a.C.Cerrojo);
        var Reemitir = () => new ReissueSurplusPayoutOrderCommandHandler(_a.Db, Distribuciones(), regiro)
            .Handle(new ReissueSurplusPayoutOrderCommand(d.PublicId, ordenes[0].PublicId, null, null, null, "Cuenta bancaria corregida") { OperationKey = Guid.NewGuid() }, default);
        (await Reemitir()).Error.Code.Should().Be(OrdenesDeGiroDeCuentas.CodigoNoSeRegira);
        _a.Db.ChangeTracker.Clear();

        var rechazada = await _a.Db.AccountPayoutOrders.SingleAsync(o => o.Id == ordenes[0].Id);
        (await Ordenes().AplicarResultadoAsync(rechazada, DisbursementOrderStatus.Rejected, "R-1", DiaDeLaAsamblea, "Cuenta cerrada", default)).IsSuccess.Should().BeTrue();
        await _a.Db.SaveChangesAsync();
        _a.Db.ChangeTracker.Clear();
        (await _a.Db.MemberAccountTransactions.AsNoTracking().CountAsync(t => t.Kind == MemberAccountTransactionKind.AssemblyCredit)).Should().Be(0,
            "el excedente pagado nunca tocó una cuenta y el rechazo tampoco la toca");
        (await OrdenesDeGiroDeCuentas.PendientesDeRegiro(_a.Db).AsNoTracking().Select(o => o.Id).ToListAsync()).Should().BeEquivalentTo([ordenes[0].Id]);

        var nueva = Ok(await Reemitir());
        _a.Db.ChangeTracker.Clear();
        nueva.ReissuesOrderNumber.Should().Be(ordenes[0].Number);
        nueva.Amount.Should().Be(ordenes[0].Amount);
        nueva.Status.Should().Be(DisbursementOrderStatus.Issued);
        var guardada = await _a.Db.AccountPayoutOrders.AsNoTracking().SingleAsync(o => o.PublicId == nueva.OrderPublicId);
        guardada.OperationId.Should().Be(ordenes[0].OperationId, "el regiro comparte la operación: el valor no volvió a ninguna cuenta");
        guardada.ReissuesOrderId.Should().Be(ordenes[0].Id);
        guardada.Purpose.Should().Be(PayoutPurpose.SurplusPayment);
        (await OrdenesDeGiroDeCuentas.PendientesDeRegiro(_a.Db).AsNoTracking().AnyAsync()).Should().BeFalse("ya tiene su regiro");
        (await _a.Db.IntegrationMessages.AsNoTracking().CountAsync(m => m.Type == OrdenDeGiroEmitidaV2.Type)).Should().Be(3, "las dos órdenes y el regiro; el rechazo no emite nada");

        // Una sola vez por orden.
        (await Reemitir()).Error.Code.Should().Be(OrdenesDeGiroDeCuentas.CodigoNoSeRegira);
        _a.Db.ChangeTracker.Clear();

        // Otra decisión no puede regirar una orden que no es suya.
        var ajena = await new ReissueSurplusPayoutOrderCommandHandler(_a.Db, Distribuciones(), regiro)
            .Handle(new ReissueSurplusPayoutOrderCommand(Guid.NewGuid(), ordenes[0].PublicId, null, null, null, "Otra") { OperationKey = Guid.NewGuid() }, default);
        ajena.IsFailure.Should().BeTrue();
    }

    /// <summary>
    /// Respuesta 91 B del dueño (E259): Ana dejó de ser asociada el 16 de diciembre (retiro liquidado, cuenta de aportes cerrada) y Beto sigue. Con los excedentes
    /// a aportes y el saldo al cierre, Ana participa a prorrata del tiempo que fue asociada (15 de los 365 días) y se le GIRA lo que le toca —no se capitaliza
    /// ni se le abre cuenta—; Beto capitaliza como siempre. La decisión mezcla dos destinos y no se reversa entera (lo girado ya salió).
    /// </summary>
    [Fact]
    public async Task E259_el_asociado_retirado_en_el_ejercicio_participa_a_prorrata_y_se_le_gira_lo_que_le_toca()
    {
        await AportesAsync();
        var cuentaDeAna = await _a.Db.MemberAccounts.SingleAsync(c => c.HolderPersonId == _a.C.Ana.Id && c.Nature == LineNature.Contribution);
        _a.Db.MembershipWithdrawals.Add(new MembershipWithdrawal
        {
            Number = "RTA-000001", PersonId = _a.C.Ana.Id, ContributionAccountId = cuentaDeAna.Id, Cause = MembershipWithdrawalCause.Voluntary,
            FiledOn = new DateOnly(2026, 12, 10), FiledByUserId = _a.C.Jefe.Id, Status = MembershipWithdrawalStatus.Settled, EffectiveDate = new DateOnly(2026, 12, 16),
            SettlementVersion = 1,
        });
        cuentaDeAna.Status = MemberAccountStatus.Closed;
        await _a.Db.SaveChangesAsync();
        _a.Db.ChangeTracker.Clear();

        var d = await RegistradaAsync(AssemblyDistributionKind.SurplusDistribution, 100_000m, valor: 100_000m, @base: RevaluationBase.YearEndBalance);
        var s = await SimuladaAsync(d.PublicId);
        s.Warnings.Should().Contain(w => w.Contains("se retiraron durante el ejercicio"));
        s.Lines.Should().HaveCount(2);
        var deAna = s.Lines.Single(l => l.Person.FullName.Contains("Ana"));
        var deBeto = s.Lines.Single(l => l.Person.FullName.Contains("Beto"));
        deAna.WithdrawnDuringYear.Should().BeTrue();
        deBeto.WithdrawnDuringYear.Should().BeFalse();
        deAna.BaseAmount.Should().Be(41_095.89m, "1.000.000 × 15/365: del 1 al 15 de diciembre fue asociada");
        deBeto.BaseAmount.Should().Be(2_000_000m, "el saldo al cierre de quien sigue");
        (deAna.AppliedAmount + deBeto.AppliedAmount).Should().Be(100_000m);
        deAna.AppliedAmount.Should().BeInRange(2_013m, 2_014m);

        _a.G.ActuarComo(_a.C.Jefe);
        var aplicada = Ok(await AplicarAsync(d.PublicId, s.Sha256, Acta(d.PublicId), Soporte(d.PublicId)));
        var lineaAna = aplicada.Lines.Single(l => l.Person.FullName.Contains("Ana"));
        lineaAna.WithdrawnDuringYear.Should().BeTrue();
        lineaAna.PayoutOrderNumber.Should().NotBeNull("lo de quien se retiró se gira");
        lineaAna.DestinationAccountNumber.Should().BeNull();
        aplicada.Lines.Single(l => l.Person.FullName.Contains("Beto")).PayoutOrderNumber.Should().BeNull("quien sigue capitaliza");

        var orden = await _a.Db.AccountPayoutOrders.AsNoTracking().SingleAsync();
        orden.Purpose.Should().Be(PayoutPurpose.SurplusPayment);
        orden.Amount.Should().Be(deAna.AppliedAmount);
        (await SaldoAsync(_a.C.Ana, MemberAccountConcept.ContributionOrdinary)).Should().Be(0m, "nada entró a los aportes ordinarios de quien se retiró");
        (await SaldoAsync(_a.C.Beto, MemberAccountConcept.ContributionOrdinary)).Should().Be(deBeto.AppliedAmount, "lo de quien sigue sí se capitaliza");
        var hecho = await _a.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == ExcedentesDistribuidosV1.Type);
        hecho.PayloadJson.Should().Contain("payments", "lo de Ana va del fondo a los retornos por pagar");

        (await Reversar().Handle(new ReverseAssemblyDistributionCommand(d.PublicId, "Error") { OperationKey = Guid.NewGuid() }, default))
            .Error.Code.Should().Be(ErroresDeAsamblea.CodigoPagoNoReversable, "lo girado ya salió a Tesorería");
    }

    /// <summary>
    /// Respuesta 89 B del dueño (E260): la revalorización de Ana (3 % de 1.000.000 al cierre = 30.000) lleva la retención del concepto de la cooperativa al 10 %: se le
    /// abonan 27.000 y 3.000 quedan por pagar; Beto es autorretenedor y recibe sus 60.000 enteros. El reverso entero devuelve los dos.
    /// </summary>
    [Fact]
    public async Task E260_la_revalorizacion_lleva_la_retencion_del_concepto_configurado_y_el_reverso_la_devuelve()
    {
        await AportesAsync();
        Ipc(0.05m);
        var beto = await _a.Db.People.SingleAsync(p => p.Id == _a.C.Beto.Id);
        beto.IsSelfWithholder = true;
        await _a.Db.SaveChangesAsync();
        _a.Db.ChangeTracker.Clear();
        RetencionConfigurada();

        var d = await RegistradaAsync(AssemblyDistributionKind.Revaluation, 1_000_000m, porcentaje: 0.03m, @base: RevaluationBase.YearEndBalance);
        var s = await SimuladaAsync(d.PublicId);
        s.Warnings.Should().Contain(w => w.Contains("RETEFTE-ASAMBLEA") && w.Contains("1 asociado"));
        s.Totals.Withheld.Should().Be(3_000m);
        s.Lines.Single(l => l.Person.FullName.Contains("Ana")).WithheldAmount.Should().Be(3_000m);
        s.Lines.Single(l => l.Person.FullName.Contains("Beto")).WithheldAmount.Should().Be(0m, "el autorretenedor no se retiene");
        s.Totals.Applied.Should().Be(90_000m, "lo aplicado es el bruto: la retención se resta al pagar");

        _a.G.ActuarComo(_a.C.Jefe);
        var aplicada = Ok(await AplicarAsync(d.PublicId, s.Sha256, Acta(d.PublicId), Soporte(d.PublicId)));
        aplicada.AppliedTotal.Should().Be(90_000m);
        aplicada.Lines.Single(l => l.Person.FullName.Contains("Ana")).WithheldAmount.Should().Be(3_000m);
        (await SaldoAsync(_a.C.Ana, MemberAccountConcept.ContributionRevaluation)).Should().Be(27_000m, "lo neto de la retención");
        (await SaldoAsync(_a.C.Beto, MemberAccountConcept.ContributionRevaluation)).Should().Be(60_000m);
        var linea = await _a.Db.AssemblyDistributionLines.AsNoTracking().SingleAsync(l => l.WithheldAmount > 0m);
        linea.WithholdingRate.Should().Be(0.10m);
        linea.WithholdingConcept.Should().Be("RETEFTE-ASAMBLEA");
        linea.ProratedAmount.Should().Be(30_000m);
        var hecho = await _a.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == RevalorizacionDeAportesAplicadaV1.Type);
        hecho.PayloadJson.Should().Contain("withholdings").And.Contain("RETEFTE-ASAMBLEA");

        var reversada = Ok(await Reversar().Handle(new ReverseAssemblyDistributionCommand(d.PublicId, "El fondo del acta era otro") { OperationKey = Guid.NewGuid() }, default));
        reversada.Status.Should().Be(AssemblyDistributionStatus.Reversed);
        _a.Db.ChangeTracker.Clear();
        (await SaldoAsync(_a.C.Ana, MemberAccountConcept.ContributionRevaluation)).Should().Be(0m);
        var reverso = await _a.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == OperacionDeCuentaReversadaV1.Type);
        reverso.PayloadJson.Should().Contain("withholdings", "el reverso devuelve también lo retenido");
    }

    /// <summary>E260: los retornos (excedentes) usan su propia tarifa; lo que se paga en dinero por orden de giro es el neto y la retención queda aparte.</summary>
    [Fact]
    public async Task E260_los_excedentes_pagados_giran_el_neto_con_la_tarifa_de_los_retornos()
    {
        await AportesAsync();
        RetencionConfigurada(tarifaRevalorizacion: "0.01", tarifaExcedentes: "0.20");
        var d = await RegistradaAsync(AssemblyDistributionKind.SurplusDistribution, 30_000m, porcentaje: 0.01m, @base: RevaluationBase.YearEndBalance,
            destino: SurplusDestination.Payment);
        var s = await SimuladaAsync(d.PublicId);
        s.Totals.Withheld.Should().Be(6_000m, "20 % de 10.000 y de 20.000: la tarifa de los retornos, no la de la revalorización");

        _a.G.ActuarComo(_a.C.Jefe);
        var aplicada = Ok(await AplicarAsync(d.PublicId, s.Sha256, Acta(d.PublicId), Soporte(d.PublicId)));
        aplicada.AppliedTotal.Should().Be(30_000m);
        var ordenes = await _a.Db.AccountPayoutOrders.AsNoTracking().OrderBy(o => o.Id).Select(o => o.Amount).ToListAsync();
        ordenes.Should().Equal(8_000m, 16_000m);
        var hecho = await _a.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == ExcedentesDistribuidosV1.Type);
        hecho.PayloadJson.Should().Contain("withholdings");
    }

    /// <summary>E260: sin concepto no se retiene aunque haya tarifa (la configuración es del tributarista y arranca vacía).</summary>
    [Fact]
    public async Task E260_sin_concepto_configurado_no_se_retiene_nada()
    {
        await AportesAsync();
        Ipc(0.05m);
        Parametro(ParametrosDeCartera.AsambleaRetencionTarifaRevalorizacion, "0.10");
        var d = await RegistradaAsync(AssemblyDistributionKind.Revaluation, 1_000_000m, porcentaje: 0.03m, @base: RevaluationBase.YearEndBalance);
        var s = await SimuladaAsync(d.PublicId);
        s.Totals.Withheld.Should().Be(0m);
        s.Lines.Should().OnlyContain(l => l.WithheldAmount == 0m);
        s.Warnings.Should().NotContain(w => w.Contains("retención"));
    }

    /// <summary>E260: la base mínima en UVT se mide con la UVT vigente; bajo ella no se retiene, y sin UVT la simulación lo dice.</summary>
    [Fact]
    public async Task E260_la_base_minima_en_UVT_deja_sin_retencion_a_quien_recibe_menos_y_exige_la_UVT()
    {
        await AportesAsync();
        Ipc(0.05m);
        RetencionConfigurada(baseMinimaUvt: "1");
        var d = await RegistradaAsync(AssemblyDistributionKind.Revaluation, 1_000_000m, porcentaje: 0.03m, @base: RevaluationBase.YearEndBalance);
        (await Simular().Handle(new SimulateAssemblyDistributionQuery(d.PublicId), default)).Error.Code.Should().Be("NationalValues.Missing", "sin UVT no se mide la base");

        Uvt(40_000m);
        var s = await SimuladaAsync(d.PublicId);
        s.Lines.Single(l => l.Person.FullName.Contains("Ana")).WithheldAmount.Should().Be(0m, "30.000 < 40.000 (1 UVT)");
        s.Lines.Single(l => l.Person.FullName.Contains("Beto")).WithheldAmount.Should().Be(6_000m, "60.000 ≥ 40.000: 10 %");
    }

    /// <summary>
    /// Respuesta 90 C del dueño (E261): la asamblea define la base de los retornos (aquí, el uso de servicios) y la cooperativa la sube por plantilla: Ana 3.000.000 y
    /// Beto 1.000.000. Todo o nada con errores por fila y columna, revisión que no guarda, cargas que se corrigen (la plantilla no borra) y una decisión aplicada que
    /// ya no admite otra carga.
    /// </summary>
    [Fact]
    public async Task E261_la_base_de_los_retornos_se_sube_por_plantilla_con_revision_todo_o_nada_y_se_corrige_con_otra_carga()
    {
        await AportesAsync();
        var d = await RegistradaAsync(AssemblyDistributionKind.SurplusDistribution, 100_000m, valor: 100_000m, @base: RevaluationBase.UploadedBase);
        d.Base.Should().Be(RevaluationBase.UploadedBase);

        // Sin archivo, nadie participa y la simulación lo dice.
        var vacia = await SimuladaAsync(d.PublicId);
        vacia.Lines.Should().BeEmpty();
        vacia.Warnings.Should().Contain(w => w.Contains("todavía no hay un archivo"));

        // Errores por fila y columna, y nada se guarda (todo o nada).
        var malo = await ImportarBaseAsync(
        [
            [d.Number, _a.C.Ana.TaxId, "3000000", "Uso de servicios 2026"],
            [d.Number, "999999", "100", null],
            [d.Number, _a.C.Carla.TaxId, "100", null],
            [d.Number, _a.C.Beto.TaxId, "-5", null],
        ]);
        malo.Error.Code.Should().Be(ImportErrors.InvalidCode);
        var errores = ErroresDeLaBase(malo);
        errores.Should().Contain(e => e.Row == 3 && e.Column == PlantillaDeBaseDeRetornos.C.Persona && e.Code == ImportErrors.CellNotFound);
        errores.Should().Contain(e => e.Row == 4 && e.Column == PlantillaDeBaseDeRetornos.C.Persona && e.Message.Contains("cuenta de aportes"));
        errores.Should().Contain(e => e.Row == 5 && e.Column == PlantillaDeBaseDeRetornos.C.Base);
        (await _a.Db.AssemblyReturnBases.AnyAsync()).Should().BeFalse("con un solo error no se guarda nada");

        // Una decisión inexistente, o dos decisiones en un archivo, tampoco se aceptan.
        ErroresDeLaBase(await ImportarBaseAsync([["ASM-999999", _a.C.Ana.TaxId, "1", null]])).Should().OnlyContain(e => e.Column == PlantillaDeBaseDeRetornos.C.Decision);
        ErroresDeLaBase(await ImportarBaseAsync([[d.Number, _a.C.Ana.TaxId, "1", null], ["ASM-000002", _a.C.Beto.TaxId, "1", null]]))
            .Should().OnlyContain(e => e.Column == PlantillaDeBaseDeRetornos.C.Decision);

        // Revisar no guarda; aplicar sí.
        var buenas = new[] { new[] { d.Number, _a.C.Ana.TaxId, "3000000", "Uso de servicios 2026" }, [d.Number, _a.C.Beto.TaxId, "1000000", "Uso de servicios 2026"] };
        (await ImportarBaseAsync(buenas, ModoDeImportacion.Review)).IsSuccess.Should().BeTrue();
        (await _a.Db.AssemblyReturnBases.AnyAsync()).Should().BeFalse("revisar no guarda");
        (await ImportarBaseAsync(buenas)).IsSuccess.Should().BeTrue();
        (await _a.Db.AssemblyReturnBases.AsNoTracking().OrderBy(b => b.PersonId).Select(b => new { b.LoadVersion, b.BaseAmount }).ToListAsync())
            .Should().Equal([new { LoadVersion = 1, BaseAmount = 3_000_000m }, new { LoadVersion = 1, BaseAmount = 1_000_000m }]);

        // La simulación reparte 100.000 por la base subida: 75 % y 25 %.
        var s = await SimuladaAsync(d.PublicId);
        s.Explanation.Should().Contain(e => e.Contains("subió la cooperativa por plantilla"));
        s.Lines.Select(l => (l.Person.FullName.Contains("Ana"), l.BaseAmount, l.AppliedAmount)).Should().BeEquivalentTo(
            [(true, 3_000_000m, 75_000m), (false, 1_000_000m, 25_000m)]);

        // Corregir a Ana: otra carga; la plantilla no borra, así que Beto sigue con su base. Repetir lo mismo no crea otra versión.
        (await ImportarBaseAsync([[d.Number, _a.C.Ana.TaxId, "3000000", "Uso de servicios 2026"]])).IsSuccess.Should().BeTrue();
        (await _a.Db.AssemblyReturnBases.AsNoTracking().MaxAsync(b => b.LoadVersion)).Should().Be(1, "sin cambios no hay otra versión");
        (await ImportarBaseAsync([[d.Number, _a.C.Ana.TaxId, "2000000", "Uso de servicios 2026"]])).IsSuccess.Should().BeTrue();
        (await _a.Db.AssemblyReturnBases.AsNoTracking().CountAsync(b => b.LoadVersion == 2)).Should().Be(2);
        var corregida = await SimuladaAsync(d.PublicId);
        corregida.Lines.Select(l => l.BaseAmount).Should().Equal(2_000_000m, 1_000_000m);
        corregida.Sha256.Should().NotBe(s.Sha256, "otra carga cambia lo que se aplicaría");

        // Quien sale con 0 no participa.
        (await ImportarBaseAsync([[d.Number, _a.C.Beto.TaxId, "0", null]])).IsSuccess.Should().BeTrue();
        (await SimuladaAsync(d.PublicId)).Lines.Should().ContainSingle().Which.AppliedAmount.Should().Be(100_000m);
        (await ImportarBaseAsync([[d.Number, _a.C.Beto.TaxId, "1000000", null]])).IsSuccess.Should().BeTrue();

        // Se aplica con lo subido, y una decisión aplicada ya no admite otra carga.
        var aplicada = await AplicadaAsync(d);
        aplicada.Lines.Select(l => l.BaseAmount).Should().Equal(2_000_000m, 1_000_000m);
        aplicada.AppliedTotal.Should().Be(100_000m);
        (await SaldoAsync(_a.C.Ana, MemberAccountConcept.ContributionOrdinary)).Should().Be(66_667m);
        (await SaldoAsync(_a.C.Beto, MemberAccountConcept.ContributionOrdinary)).Should().Be(33_333m);
        ErroresDeLaBase(await ImportarBaseAsync([[d.Number, _a.C.Ana.TaxId, "5", null]])).Should().ContainSingle(e => e.Message.Contains("Applied"));
    }

    [Fact]
    public async Task E261_la_base_por_plantilla_solo_aplica_a_los_retornos_y_su_decision_debe_usarla()
    {
        await AportesAsync();
        var validador = new RegisterAssemblyDistributionCommandValidator();
        var revalorizacion = new RegisterAssemblyDistributionCommand(AssemblyDistributionKind.Revaluation, 2026, 1_000m, 0.03m, null, RevaluationBase.UploadedBase, null, null, null,
            "Asamblea") { OperationKey = Guid.NewGuid() };
        validador.Validate(revalorizacion).Errors.Should().Contain(e => e.ErrorMessage.Contains("sólo aplica a los retornos"));
        validador.Validate(revalorizacion with { Kind = AssemblyDistributionKind.SurplusDistribution }).IsValid.Should().BeTrue();

        // Una decisión con otra base no admite el archivo.
        var conSaldos = await RegistradaAsync(AssemblyDistributionKind.SurplusDistribution, 100_000m, valor: 100_000m, @base: RevaluationBase.YearEndBalance);
        ErroresDeLaBase(await ImportarBaseAsync([[conSaldos.Number, _a.C.Ana.TaxId, "1", null]])).Should().ContainSingle(e => e.Message.Contains("otra base"));
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
