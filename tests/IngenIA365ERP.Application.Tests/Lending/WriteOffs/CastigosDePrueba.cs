using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Integration.Savings;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Application.Lending.WriteOffs;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Lending.Novelties;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Core.NationalValues;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.WriteOffs;

/// <summary>
/// El escenario del castigo (feature 013, K3, US14, bloque B): el de <see cref="NovedadesDePrueba"/> —InMemory, hoy local 2026-12-01, el crédito de Ana por
/// 3.000.000 a 24 cuotas, Contabilidad que acepta todo, el consejo de administración entre los órganos— con la consulta de aportes «no disponible» (D19), las
/// piezas reales del castigo y tres personas: el jefe de cartera arma el lote (la asesora de la escena), el consejo lo aprueba (un punto cerrado) y la
/// tesorera lo aplica. Un crédito <b>castigable</b> es uno cuyo deudor ya no es asociado, con informe, concepto y soporte del cruce de aportes subidos y un
/// cierre cuyo deterioro individual cubre su saldo. (nuevo)
/// </summary>
internal sealed class CastigosDePrueba : IDisposable
{
    private CastigosDePrueba(NovedadesDePrueba n) => N = n;

    public static async Task<CastigosDePrueba> CrearAsync()
    {
        var c = new CastigosDePrueba(await NovedadesDePrueba.CrearAsync());
        c.Aportes.SaldosAsync(Arg.Any<SolicitudDeSaldoDeAportes>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(SaldosDeAportesDto.NoDisponible));
        return c;
    }

    public NovedadesDePrueba N { get; }
    public Payments.PagosDePrueba P => N.P;
    public Disbursements.DesembolsoDePrueba D => N.D;
    public Sessions.GobiernoDePrueba G => N.G;
    public TestApplicationDbContext Db => N.Db;
    public Loan Credito => N.Credito;
    public User Jefe => D.O.Asesora;
    public User Tesorera => N.Tesorera;
    public IConsultaDeAportes Aportes { get; } = Substitute.For<IConsultaDeAportes>();

    public void Dispose() => N.Dispose();

    // ------------------------------------------------------------------------------------------------------------ piezas --

    public CastigosDeCartera Castigos() => new(Db, P.C.Cargador(), Aportes, D.Parametros());
    public NormaPendienteDeCartera Norma() => new(D.Parametros(), G.Perfil, D.O.L.Normativa());
    public CreateWriteOffBatchCommandHandler Crear() =>
        new(Db, Castigos(), D.Numerador(), G.Intervenciones(), Norma(), D.O.Cerrojo, G.Alcance, G.Auditoria, G.Actor, G.Reloj);
    public VoidWriteOffBatchCommandHandler Anular() => new(Db, Castigos(), G.Alcance, G.Auditoria);
    public AddWriteOffBatchItemCommandHandler Agregar() => new(Db, Castigos(), G.Intervenciones(), D.O.Cerrojo, G.Alcance, G.Auditoria, G.Reloj);
    public RemoveWriteOffBatchItemCommandHandler Quitar() => new(Db, Castigos(), G.Alcance, G.Auditoria);
    public SubmitWriteOffBatchCommandHandler Enviar() => new(Db, Castigos(), G.Alcance, G.Auditoria);
    public ListWriteOffBatchesQueryHandler Listar() => new(Db, G.Alcance, Castigos());
    public GetWriteOffBatchQueryHandler Leer() => new(Db, G.Alcance, G.Auditoria, Castigos());
    public AsuntoDeLoteDeCastigo Asunto() => new(Db);
    public ApplyWriteOffBatchCommandHandler Aplicar() =>
        new(Db, Castigos(), new PuntoDeActaCerrado(Db), D.PuestaAlDia(), D.Partes(), D.Registro(), D.Numerador(), G.Intervenciones(), D.Emision(),
            D.ValidacionPrevia(), D.FechaContable(), G.Perfil, D.O.L.Normativa(), D.Alertas, D.O.Cerrojo, G.Alcance, G.Auditoria, G.Actor, G.Reloj);
    public RegisterWriteOffSupervisorReportCommandHandler Reportar() => new(Db, Castigos(), D.Alertas, G.Alcance, G.Auditoria, G.Reloj);

    // ------------------------------------------------------------------------------------------------------- escenario --

    /// <summary>Un adjunto disponible de <paramref name="dueno"/> con el tipo de dueño dado.</summary>
    public async Task<Guid> AdjuntoAsync(Guid dueno, string tipo = AdjuntosDeModulo.SoporteDeCastigo)
    {
        var a = new Attachment
        {
            OwnerEntityType = tipo, OwnerEntityPublicId = dueno, FileName = "soporte.pdf", ContentType = "application/pdf", SizeBytes = 10,
            Sha256Hex = new string('c', 64), StoragePath = "x", Status = Domain.Enums.Core.EstadoDeAdjunto.Available, CreatedBy = "test",
        };
        Db.Attachments.Add(a);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return a.PublicId;
    }

    /// <summary>El deudor del crédito deja (o no) de ser asociado.</summary>
    public async Task AsociadoAsync(Loan credito, bool asociado)
    {
        var persona = await Db.People.IgnoreQueryFilters().SingleAsync(p => p.Id == credito.DebtorPersonId);
        persona.IsAssociate = asociado;
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
    }

    /// <summary>Los tres soportes del crédito (informe, concepto y cruce de aportes), ya subidos.</summary>
    public async Task<WriteOffSupportInput> SoportesAsync(Loan? credito = null)
    {
        var c = credito ?? Credito;
        return new WriteOffSupportInput(c.PublicId, await AdjuntoAsync(c.PublicId), await AdjuntoAsync(c.PublicId), await AdjuntoAsync(c.PublicId));
    }

    /// <summary>Un cierre de ayer con el deterioro individual del crédito (por capital y por intereses).</summary>
    public async Task CierreAsync(decimal deterioroDeCapital, decimal deterioroDeIntereses = 0m, Loan? credito = null)
    {
        var c = credito ?? Credito;
        var cierre = await Db.MonthlyCloses.FirstOrDefaultAsync(x => x.CloseDate == G.HoyLocal.AddDays(-1));
        if (cierre is null)
        {
            cierre = new MonthlyClose
            {
                Kind = MonthlyCloseKind.Base, PeriodYear = (short)G.HoyLocal.Year, PeriodMonth = (byte)G.HoyLocal.Month, Version = 1,
                CloseDate = G.HoyLocal.AddDays(-1), InformationCutoffAt = G.UtcNow.AddDays(-1), LinesSha256 = new string('0', 64),
                OrderedAt = G.UtcNow.AddDays(-1), ClosedAt = G.UtcNow.AddDays(-1),
            };
            Db.MonthlyCloses.Add(cierre);
            await Db.SaveChangesAsync();
        }
        Db.MonthlyCloseLoans.Add(new MonthlyCloseLoan
        {
            MonthlyCloseId = cierre.Id, LoanId = c.Id, PersonId = c.DebtorPersonId, OfficePublicId = Guid.NewGuid(), BranchId = c.BranchId,
            CreditLineId = c.CreditLineId, ModalityCode = "CONSUMO", SarcSegmentCode = "CONSUMO", ImpairmentModel = ImpairmentModel.ArrearsAnnex1,
            ModelBasis = ModelBasis.NotObligated, ArrearsRating = RatingCategory.E, FinalRating = RatingCategory.E, AccountingRating = RatingCategory.E,
            RatingCause = RatingCause.Arrears, PrincipalImpairment = deterioroDeCapital, InterestImpairment = deterioroDeIntereses,
            TotalImpairment = deterioroDeCapital + deterioroDeIntereses,
            BalancesByConceptJson = JsonSerializer.Serialize(new Dictionary<string, decimal> { ["Principal"] = deterioroDeCapital, ["CurrentInterest"] = deterioroDeIntereses }),
        });
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
    }

    /// <summary>El crédito castigable: deudor no asociado, soportes subidos y un cierre con deterioro que cubre su saldo.</summary>
    public async Task<WriteOffSupportInput> CastigableAsync(Loan? credito = null, decimal deterioroDeCapital = 3_000_000m, decimal deterioroDeIntereses = 500_000m)
    {
        var c = credito ?? Credito;
        await AsociadoAsync(c, asociado: false);
        await CierreAsync(deterioroDeCapital, deterioroDeIntereses, c);
        return await SoportesAsync(c);
    }

    /// <summary>Un parámetro de Cartera vigente desde enero.</summary>
    public async Task ParametroAsync(string clave, string valor)
    {
        Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = clave, ScopeKind = ParameterScopeKind.None, Value = valor, ValidFrom = new DateOnly(2026, 1, 1),
            Reason = "Política de prueba",
        });
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
    }

    /// <summary>El jefe de cartera arma el lote.</summary>
    public async Task<Result<WriteOffBatchDto>> ArmarAsync(params WriteOffSupportInput[] creditos)
    {
        G.ActuarComo(Jefe);
        G.Conceder("Lending.Loans.Read", "Lending.WriteOffs.Prepare");
        var r = await Crear().Handle(new CreateWriteOffBatchCommand(creditos.Select(c => c.LoanPublicId).ToList(), "Cartera irrecuperable", creditos)
            { OperationKey = Guid.NewGuid() }, default);
        Db.ChangeTracker.Clear();
        return r;
    }

    /// <summary>Arma y envía el lote; falla la prueba si no pudo.</summary>
    public async Task<WriteOffBatchDto> EnviadoAsync(params WriteOffSupportInput[] creditos)
    {
        var armado = await ArmarAsync(creditos);
        armado.IsSuccess.Should().BeTrue(armado.IsFailure ? $"{armado.Error.Code}: {armado.Error.Message}" : string.Empty);
        var enviado = await Enviar().Handle(new SubmitWriteOffBatchCommand(armado.Value.PublicId) { OperationKey = Guid.NewGuid() }, default);
        enviado.IsSuccess.Should().BeTrue(enviado.IsFailure ? $"{enviado.Error.Code}: {enviado.Error.Message}" : string.Empty);
        Db.ChangeTracker.Clear();
        return enviado.Value;
    }

    /// <summary>Una sesión cerrada del órgano con un punto sobre el sujeto y su resultado (sin decidir todavía sobre el asunto).</summary>
    public async Task<(Session Sesion, SessionItem Punto)> PuntoCerradoAsync(GoverningBody organo, string sujeto, Guid sujetoPublicId, CreditDecisionOutcome resultado,
        SessionItemKind clase = SessionItemKind.Decision)
    {
        var sesion = new Session
        {
            GoverningBodyId = organo.Id, Kind = SessionKind.Ordinary, Mode = SessionMode.InPerson, HeldOn = G.HoyLocal, Status = SessionStatus.Closed,
            Number = $"{organo.Code}-{Guid.NewGuid():N}"[..20], ClosedAt = G.UtcNow,
        };
        Db.LendingSessions.Add(sesion);
        await Db.SaveChangesAsync();
        var punto = new SessionItem
        {
            SessionId = sesion.Id, Order = 1, Kind = clase, Rule = VotingRule.Majority, SubjectType = sujeto, SubjectPublicId = sujetoPublicId,
            Title = "Asunto", Outcome = resultado,
        };
        Db.SessionItems.Add(punto);
        await Db.SaveChangesAsync();
        return (sesion, punto);
    }

    /// <summary>El cierre del acta aplica el punto al asunto (lo que hace <c>CloseSessionCommand</c>, sin votos).</summary>
    public async Task<Result> DecidirAsync(IAsuntoDeActa asunto, Session sesion, SessionItem punto, CreditDecisionOutcome resultado)
    {
        var decision = CreditDecision.Colegiada(new LoDecidido(punto.SubjectType, punto.SubjectPublicId, punto.SubjectPublicId, resultado)
        {
            Reason = resultado is CreditDecisionOutcome.Approve or CreditDecisionOutcome.ApproveWithConditions ? null : "No alcanzó la mayoría",
        }, punto, G.UtcNow);
        var r = await asunto.AlDecidirAsync(new DecisionDeActa(sesion, punto, decision, resultado, null, G.HoyLocal), default);
        if (r.IsSuccess) await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        return r;
    }

    /// <summary>El consejo aprueba el lote en un punto cerrado; devuelve el punto.</summary>
    public async Task<SessionItem> AprobadoAsync(WriteOffBatchDto lote, CreditDecisionOutcome resultado = CreditDecisionOutcome.Approve)
    {
        var (sesion, punto) = await PuntoCerradoAsync(D.O.Consejo, LendingDecisionSubjects.WriteOffBatch, lote.PublicId, resultado);
        var r = await DecidirAsync(Asunto(), sesion, punto, resultado);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return punto;
    }

    /// <summary>La tesorera aplica el lote con el punto dado.</summary>
    public async Task<Result<ApplyWriteOffBatchResultDto>> AplicarAsync(Guid lote, Guid punto, Domain.Entities.Security.User? quien = null)
    {
        G.ActuarComo(quien ?? Tesorera);
        G.Conceder("Lending.Loans.Read", "Lending.WriteOffs.Apply");
        var r = await Aplicar().Handle(new ApplyWriteOffBatchCommand(lote, punto, AcknowledgeWarnings: true) { OperationKey = Guid.NewGuid() }, default);
        Db.ChangeTracker.Clear();
        return r;
    }

    /// <summary>El documento <c>TiposDeOrganizacion</c> con los datos de castigo y de evaluación del tipo, y el perfil de la cooperativa con ese tipo.</summary>
    public Task TipoDeEntidadAsync(string reporte = """{ "required": true, "dueDays": 30 }""", string? evaluacion = null) =>
        SembrarTipoDeEntidadAsync(Db, G, reporte, evaluacion);

    /// <summary>Lo mismo, para cualquier escenario con su contexto y su gobierno de prueba.</summary>
    public static async Task SembrarTipoDeEntidadAsync(TestApplicationDbContext Db, Sessions.GobiernoDePrueba G,
        string reporte = """{ "required": true, "dueDays": 30 }""", string? evaluacion = null)
    {
        var contenido = $$"""
            {
              "employeeFundCategories": [],
              "organizationTypes": [
                { "code": "COOP_AHORRO_CREDITO", "name": "Cooperativa de ahorro y crédito", "isEmployeeFund": false, "expectedLoss": "Admitted",
                  "writeOffReport": {{reporte}}{{(evaluacion is null ? string.Empty : $", \"portfolioEvaluation\": {evaluacion}")}} }
              ]
            }
            """;
        var replica = new NationalValueReplication
        {
            FromSequence = 2, ToSequence = 2, DocumentCount = 1, Origin = NationalValueReplicationOrigin.ScheduledTask,
            StartedAt = G.UtcNow.AddDays(-1), FinishedAt = G.UtcNow.AddDays(-1), ActorName = "prueba",
        };
        replica.Documents.Add(new RegulatoryDocument
        {
            Kind = nameof(RegulatoryDocumentKind.TiposDeOrganizacion), Code = "TIPOS", ValidFrom = new DateOnly(2026, 1, 1), Content = contenido,
            ContentSha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(contenido))), SchemaVersion = 1, Verified = true,
            Source = "Superintendencia de la Economía Solidaria", SourcePublicId = Guid.NewGuid(), SourceSequence = 90_001,
            CentralRegisteredAt = G.UtcNow.AddDays(-1), KnownAt = G.UtcNow.AddDays(-1),
        });
        Db.NationalValueReplications.Add(replica);
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        G.Perfil.VigenteAsync(Arg.Any<DateOnly>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<PerfilRegulatorioDto?>(
            new PerfilRegulatorioDto(Guid.NewGuid(), new DateOnly(2026, 1, 1), "COOP_AHORRO_CREDITO", true, 2, null, 2, false, null, null, "Prueba", null,
                "Perfil de prueba", null, "prueba", G.UtcNow.AddDays(-30))));
    }

    public static JsonElement Datos(Error error) => NovedadesDePrueba.Datos(error);
}
