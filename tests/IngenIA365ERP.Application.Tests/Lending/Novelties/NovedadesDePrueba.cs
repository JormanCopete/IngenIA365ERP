using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.DebtorEvents;
using IngenIA365ERP.Application.Lending.Novelties;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Origination.CreditLimits;
using IngenIA365ERP.Application.Lending.Waivers;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Lending.Payments;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Risk;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Novelties;

/// <summary>
/// El escenario de K3 (feature 013, US11, bloque B): el de <see cref="PagosDePrueba"/> —InMemory, hoy local 2026-12-01, línea <c>CONLIB</c> al 20 % EA,
/// el crédito de Ana desembolsado hoy por 3.000.000 a 24 cuotas mensuales, Contabilidad que acepta todo, la matriz del ensayo (analista hasta 5 millones,
/// excepción, gerencia, comité, consejo)— con el Anexo 1 sembrado y las piezas reales de novedades, condonaciones y eventos del deudor. Tres personas:
/// la asesora radica, el analista decide y la tesorera aplica (la segregación fija radicar × aplicar). (nuevo)
/// </summary>
internal sealed class NovedadesDePrueba : IDisposable
{
    private NovedadesDePrueba(PagosDePrueba p)
    {
        P = p;
        Tesorera = D.O.UsuarioConPersona("Tesorera", "1001009");
    }

    public static async Task<NovedadesDePrueba> CrearAsync()
    {
        var n = new NovedadesDePrueba(await PagosDePrueba.CrearAsync(sinTopeDePrepago: true));
        await n.P.C.AnexoAsync(new DateOnly(2026, 1, 1));
        return n;
    }

    public PagosDePrueba P { get; }
    public Disbursements.DesembolsoDePrueba D => P.D;
    public Sessions.GobiernoDePrueba G => P.G;
    public TestApplicationDbContext Db => P.Db;
    public Loan Credito => P.Credito;
    public User Tesorera { get; }

    public void Dispose() => P.Dispose();

    // ------------------------------------------------------------------------------------------------------------ piezas --

    public CalculoDeNovedad Calculo() => new(Db, P.C.Cargador(), D.O.L.Normativa());
    public HechosDeLaNovedad Hechos() => new(Db, D.Partes(), D.FechaContable(), D.Emision(), D.ValidacionPrevia(), G.Reloj);
    public SimulacionDeNovedad Simulacion() => new(Calculo(), Hechos(), D.O.Concentracion());
    public EnrutamientoDeNovedad Enrutamiento() => new(Db, new MatrizVigenteDeAtribuciones(Db), D.O.Exposicion(), new VinculadosVigentes(Db), G.Reloj);
    public RequestLoanNoveltyCommandHandler Pedir() => new(Db, Calculo(), D.Numerador(), G.Intervenciones(), D.O.Cerrojo, G.Alcance, G.Auditoria, G.Actor, G.Reloj);
    public SimulateLoanNoveltyQueryHandler Simular() => new(Db, Simulacion(), G.Alcance, G.Auditoria, G.Reloj);
    public SubmitLoanNoveltyCommandHandler Radicar() => new(Db, Simulacion(), Enrutamiento(), D.O.Cerrojo, G.Alcance, G.Auditoria, G.Reloj);
    public RegisterNoveltyConsentCommandHandler Consentir() => new(Db, G.Alcance, G.Auditoria, G.Actor, G.Reloj);
    public GetLoanNoveltyQueryHandler Leer() => new(Db, G.Alcance, G.Auditoria);
    public ListLoanNoveltiesQueryHandler Listar() => new(Db, G.Alcance);
    public CancelLoanNoveltyCommandHandler Anular() => new(Db, G.Alcance, G.Auditoria, G.Reloj);
    public DecisionSobreNovedad DecisionDeNovedad() =>
        new(Db, Enrutamiento(), Simulacion(), G.Intervenciones(), D.O.Atribucion(), D.O.Cerrojo, G.Alcance, G.Auditoria, G.Reloj);
    public CondonacionesDeCartera Condonaciones() => new(Db, new MatrizVigenteDeAtribuciones(Db), G.Reloj);
    public DecisionSobreCondonacion DecisionDeCondonacion() =>
        new(Db, Condonaciones(), G.Intervenciones(), D.O.Atribucion(), D.O.Cerrojo, G.Alcance, G.Auditoria, G.Reloj);
    public RecordCreditDecisionCommandHandler Decidir() => new([DecisionDeNovedad(), DecisionDeCondonacion()]);
    public ApplyLoanNoveltyCommandHandler Aplicar() => new(Db, Calculo(), Hechos(), D.Emision(), D.PuestaAlDia(), P.C.Cargador(), D.Registro(), D.Numerador(),
        G.Intervenciones(), new VersionVigenteDeLinea(Db), D.FechaContable(), D.O.Cerrojo, G.Alcance, G.Auditoria, G.Actor, G.Reloj);
    public ApproveNoveltyCoverageExceptionCommandHandler AprobarExcepcion() =>
        new(Db, G.Intervenciones(), new MatrizVigenteDeAtribuciones(Db), D.O.Atribucion(), D.O.Cerrojo, G.Alcance, G.Auditoria, G.Reloj);

    public RequestWaiverCommandHandler PedirCondonacion() =>
        new(Db, Condonaciones(), P.C.Cargador(), new VinculadosVigentes(Db), D.Numerador(), G.Intervenciones(), D.O.Cerrojo, G.Alcance, G.Auditoria, G.Actor,
            G.Reloj);
    public ApplyWaiverCommandHandler AplicarCondonacion() =>
        new(Db, P.C.Cargador(), D.PuestaAlDia(), D.Partes(), D.Registro(), D.Numerador(), G.Intervenciones(), D.Emision(), D.ValidacionPrevia(),
            D.FechaContable(), D.O.Cerrojo, G.Alcance, G.Auditoria, G.Actor, G.Reloj);
    public CancelWaiverCommandHandler AnularCondonacion() => new(Db, G.Alcance, G.Auditoria);
    public ListWaiversQueryHandler Condonadas() => new(Db, G.Alcance, Condonaciones());

    public ColaDeMensajesAlDeudor Cola() =>
        new(Db, G.Reloj, D.Parametros(), Substitute.For<Application.Common.Calendar.ICalendarioHabil>(), D.O.Autorizaciones,
            Substitute.For<Application.Common.Messaging.ICanalesDeMensajeria>(), Microsoft.Extensions.Options.Options.Create(D.O.Opciones));
    public RegisterDebtorEventCommandHandler RegistrarEvento() => new(Db, D.O.BloqueosReales(), Cola(), D.O.Cerrojo, G.Actor, G.Reloj);
    public ListDebtorEventsQueryHandler Eventos() => new(Db);
    public ChangeInsuranceClaimStatusCommandHandler CambiarReclamacion() => new(Db, D.O.Cerrojo, G.Alcance, G.Auditoria, G.Reloj);
    public ListInsuranceClaimsQueryHandler Reclamaciones() => new(Db, G.Alcance);
    public OpenInsolvencyProceedingCommandHandler AbrirInsolvencia() => new(Db, D.O.BloqueosReales(), Cola(), D.O.Cerrojo, G.Actor, G.Reloj);
    public RecordInsolvencyFailureCommandHandler Fracaso() => new(Db, D.O.Cerrojo, G.Reloj);
    public ListInsolvencyProceedingsQueryHandler Procesos() => new(Db);

    // ------------------------------------------------------------------------------------------------------------ flujo --

    /// <summary>La asesora pide la novedad (con todos los permisos de radicar).</summary>
    public async Task<Result<LoanNoveltyDto>> PedirAsync(NoveltyMechanism mecanismo, NewTermsInput terminos, NoveltyRequestMotive motivo = NoveltyRequestMotive.Hardship,
        params Loan[] creditos)
    {
        G.ActuarComo(D.O.Asesora);
        G.Conceder("Lending.Novelties.Read", "Lending.Novelties.Request");
        var lista = creditos.Length == 0 ? [Credito] : creditos;
        var r = await Pedir().Handle(new RequestLoanNoveltyCommand(lista.Select(c => c.PublicId).ToList(), mecanismo, motivo, terminos, "Dificultades de pago")
            { OperationKey = Guid.NewGuid() }, default);
        Db.ChangeTracker.Clear();
        return r;
    }

    /// <summary>La asesora pide, simula y radica la novedad; falla la prueba si no pudo.</summary>
    public async Task<LoanNoveltyDto> RadicadaAsync(NoveltyMechanism mecanismo, NewTermsInput terminos, NoveltyRequestMotive motivo = NoveltyRequestMotive.Hardship,
        params Loan[] creditos)
    {
        var pedida = await PedirAsync(mecanismo, terminos, motivo, creditos);
        pedida.IsSuccess.Should().BeTrue(pedida.IsFailure ? $"{pedida.Error.Code}: {pedida.Error.Message}" : string.Empty);
        var simulada = await Simular().Handle(new SimulateLoanNoveltyQuery(pedida.Value.PublicId), default);
        simulada.IsSuccess.Should().BeTrue(simulada.IsFailure ? $"{simulada.Error.Code}: {simulada.Error.Message}" : string.Empty);
        var radicada = await Radicar().Handle(new SubmitLoanNoveltyCommand(pedida.Value.PublicId) { OperationKey = Guid.NewGuid() }, default);
        radicada.IsSuccess.Should().BeTrue(radicada.IsFailure ? $"{radicada.Error.Code}: {radicada.Error.Message}" : string.Empty);
        Db.ChangeTracker.Clear();
        return radicada.Value;
    }

    /// <summary>El analista decide con la huella vista.</summary>
    public async Task<Result<CreditDecisionResultDto>> DecidirAsync(string sujeto, Guid publicId, string huella,
        CreditDecisionOutcome resultado = CreditDecisionOutcome.Approve, string? motivo = "Cumple la política")
    {
        G.ActuarComo(D.O.Analista);
        G.Conceder("Lending.Loans.Read", "Lending.Novelties.Read", "Lending.Decisions.Analyst", "Lending.Decisions.Exceptions", "Lending.Decisions.Management");
        var r = await Decidir().Handle(new RecordCreditDecisionCommand(sujeto, publicId, resultado, motivo, null, huella) { OperationKey = Guid.NewGuid() }, default);
        Db.ChangeTracker.Clear();
        return r;
    }

    /// <summary>Radica y aprueba la novedad (el analista); falla la prueba si no pudo.</summary>
    public async Task<LoanNoveltyDto> AprobadaAsync(NoveltyMechanism mecanismo, NewTermsInput terminos, NoveltyRequestMotive motivo = NoveltyRequestMotive.Hardship,
        params Loan[] creditos)
    {
        var radicada = await RadicadaAsync(mecanismo, terminos, motivo, creditos);
        var d = await DecidirAsync(LendingDecisionSubjects.Novelty, radicada.PublicId, radicada.Routing!.ContentSha256);
        d.IsSuccess.Should().BeTrue(d.IsFailure ? $"{d.Error.Code}: {d.Error.Message}" : string.Empty);
        return radicada;
    }

    /// <summary>La tesorera aplica la novedad.</summary>
    public async Task<Result<ApplyNoveltyResultDto>> AplicarAsync(Guid novedad, IReadOnlyList<WithoutConsentInput>? sinConsentimiento = null)
    {
        G.ActuarComo(Tesorera);
        G.Conceder("Lending.Novelties.Read", "Lending.Novelties.Apply");
        var r = await Aplicar().Handle(new ApplyLoanNoveltyCommand(novedad, sinConsentimiento, AcknowledgeWarnings: true) { OperationKey = Guid.NewGuid() }, default);
        Db.ChangeTracker.Clear();
        return r;
    }

    /// <summary>La asesora pide la condonación.</summary>
    public async Task<Result<WaiverDto>> PedirCondonacionAsync(params WaiverConceptInput[] conceptos)
    {
        G.ActuarComo(D.O.Asesora);
        G.Conceder("Lending.Loans.Read", "Lending.Condonations.Request");
        var r = await PedirCondonacion().Handle(new RequestWaiverCommand(Credito.PublicId, conceptos, "Acuerdo de pago") { OperationKey = Guid.NewGuid() }, default);
        Db.ChangeTracker.Clear();
        return r;
    }

    /// <summary>La tesorera aplica la condonación.</summary>
    public async Task<Result<ApplyWaiverResultDto>> AplicarCondonacionAsync(Guid condonacion)
    {
        G.ActuarComo(Tesorera);
        G.Conceder("Lending.Loans.Read", "Lending.Condonations.Apply");
        var r = await AplicarCondonacion().Handle(new ApplyWaiverCommand(condonacion, AcknowledgeWarnings: true) { OperationKey = Guid.NewGuid() }, default);
        Db.ChangeTracker.Clear();
        return r;
    }

    /// <summary>Contabilidad dice que falta la regla del hecho dado (A8: con «Bloquear», no se confirma).</summary>
    public void SinRegla(string hecho) =>
        D.Contabilidad.EvaluarAsync(Arg.Any<IReadOnlyList<MensajeContableDto>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ResultadoDeEvaluacionContableDto(false,
                [new HallazgoDeCarteraDto(hecho, "Principal", null, null, "RuleMissing", "Falta la regla", new QuienCorrigeDto("Accounting", null, null))], [])));

    /// <summary>El primer vencimiento del plan vigente del crédito.</summary>
    public async Task<DateOnly> PrimerVencimientoAsync(int? loanId = null) =>
        (await Db.RepaymentInstallments.AsNoTracking().Where(i => i.ScheduleVersion!.LoanId == (loanId ?? Credito.Id)).OrderBy(i => i.Id).FirstAsync()).DueDate;

    public static System.Text.Json.JsonElement Datos(Error error) => Sessions.GobiernoDePrueba.Datos(error);
}
