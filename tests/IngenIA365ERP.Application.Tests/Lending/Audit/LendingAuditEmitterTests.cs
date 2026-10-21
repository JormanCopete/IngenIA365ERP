using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace IngenIA365ERP.Application.Tests.Lending.Audit;

/// <summary>
/// Feature 013, T0256 (T67, C89; research §13 y R136; molde <c>InventoryAuditEmitterTests</c>): <see cref="LendingAuditEmitter"/> escribe
/// las lecturas sensibles de Cartera con su lista fija de diez eventos, encadenados al flujo de la cooperativa nombrado por su
/// <b>PublicId</b> (<c>LaAuditoriaSeNombraPorElPublicIdDelTenant</c>), nunca lanza hacia quien lee y devuelve el <c>EventId</c> —que la
/// consulta de cupo de Comercial usa como <c>EvidenceId</c>—.
/// </summary>
public class LendingAuditEmitterTests
{
    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IAuditService _mongo = Substitute.For<IAuditService>();

    private LendingAuditEmitter Emisor(string? cooperativa = CooperativaDePrueba.PublicIdN, IApplicationDbContext? db = null)
    {
        var tenant = Substitute.For<ICurrentTenantService>();
        tenant.TenantId.Returns(cooperativa);
        var servicios = new ServiceCollection()
            .AddSingleton(_mongo)
            .AddSingleton(db ?? _db)
            .AddSingleton(tenant)
            // El usuario del token trae el Id INTERNO de la cooperativa («1»): no es el que nombra el flujo.
            .AddSingleton(NominaTestData.UsuarioDePrueba("ana@coop", 7))
            .BuildServiceProvider();
        return new LendingAuditEmitter(servicios, NullLogger<LendingAuditEmitter>.Instance);
    }

    public static TheoryData<string, Func<LendingAuditEmitter, Task<Guid?>>> LosDiez => new()
    {
        { "Lending.Payoff.Viewed", e => e.EmitirLiquidacionVistaAsync(Guid.NewGuid(), new DateOnly(2026, 10, 8), new DateTime(2026, 10, 8, 12, 0, 0), new string('a', 64)) },
        { "Lending.CreditBureauReport.Viewed", e => e.EmitirInformeDeCentralesVistoAsync(Guid.NewGuid(), "DATACREDITO") },
        { "Lending.Document.Reprinted", e => e.EmitirDocumentoReimpresoAsync(Guid.NewGuid(), "PazYSalvo", "pdf") },
        { "Lending.Document.Verified", e => e.EmitirDocumentoVerificadoAsync(Guid.NewGuid(), valido: true) },
        { "Lending.Report.Exported", e => e.EmitirExportacionAsync("cartera-por-edades", new { asOf = "2026-09-30" }, "xlsx", 42) },
        { "Lending.Session.Viewed", e => e.EmitirActaVistaAsync(Guid.NewGuid(), "CC-0001", comoPdf: false) },
        { "Lending.RelatedParty.Viewed", e => e.EmitirVinculadosVistosAsync(new { basis = "BoardMember" }, 3) },
        { "Lending.CreditStatus.Queried", e => e.EmitirConsultaDeCupoAsync(Guid.NewGuid(), new { requested = 1_500_000m }) },
        { "Lending.Access.OutOfScope", e => e.EmitirFueraDeAlcanceAsync("Loan", Guid.NewGuid()) },
        { "Lending.ScreeningMatch.Released", e => e.EmitirCoincidenciaLiberadaAsync(Guid.NewGuid(), "Homónimo verificado con el documento") },
    };

    [Theory]
    [MemberData(nameof(LosDiez))]
    public async Task Cada_metodo_escribe_su_evento_encadenado_al_flujo_del_PublicId_y_devuelve_el_EventId(string accion, Func<LendingAuditEmitter, Task<Guid?>> emitir)
    {
        var eventId = await emitir(Emisor());

        await _mongo.DidNotReceive().LogAsync(Arg.Any<AuditLogCommand>(), Arg.Any<CancellationToken>());
        var fila = (await _db.AuditOutbox.ToListAsync()).Should().ContainSingle().Subject;
        fila.Stream.Should().Be($"{CooperativaDePrueba.PublicIdN}:10y", "el flujo se nombra por el PublicId, no por el Id interno");
        fila.Module.Should().Be("Lending");
        eventId.Should().Be(fila.EventId);
        var evento = AuditoriaEncadenada.LeerCarga(fila.PayloadJson!);
        evento.Action.Should().Be(accion);
        evento.TenantId.Should().Be(CooperativaDePrueba.PublicIdN);
        evento.Module.Should().Be("Lending");
    }

    [Fact]
    public void La_lista_fija_son_los_diez_eventos_de_research_13_y_C89()
    {
        LendingAuditEmitter.LecturasSensibles.Should().BeEquivalentTo(
        [
            "Lending.Payoff.Viewed", "Lending.CreditBureauReport.Viewed", "Lending.Document.Reprinted", "Lending.Document.Verified",
            "Lending.Report.Exported", "Lending.Session.Viewed", "Lending.RelatedParty.Viewed", "Lending.CreditStatus.Queried",
            "Lending.Access.OutOfScope", "Lending.ScreeningMatch.Released",
        ]);
        var constantes = typeof(AuditEventTypes).GetFields().Where(f => f.IsLiteral).Select(f => (string?)f.GetRawConstantValue()).ToHashSet();
        LendingAuditEmitter.LecturasSensibles.Should().OnlyContain(e => constantes.Contains(e), "cada evento es una constante de AuditEventTypes");
    }

    [Fact]
    public async Task La_liquidacion_lleva_D_R_y_la_huella()
    {
        var credito = Guid.NewGuid();

        await Emisor().EmitirLiquidacionVistaAsync(credito, new DateOnly(2026, 10, 8), new DateTime(2026, 10, 8, 12, 0, 0), new string('f', 64));

        var evento = AuditoriaEncadenada.LeerCarga((await _db.AuditOutbox.SingleAsync()).PayloadJson!);
        evento.EntityPublicId.Should().Be(credito.ToString());
        evento.NewValuesJson.Should().Contain("2026-10-08").And.Contain(new string('f', 64));
    }

    [Fact]
    public async Task Nunca_lanza_hacia_quien_lee_y_sin_poder_escribir_devuelve_nulo()
    {
        var roto = Substitute.For<IApplicationDbContext>();
        roto.AuditOutbox.Throws(new InvalidOperationException("la base no responde"));

        var eventId = await Emisor(db: roto).EmitirActaVistaAsync(Guid.NewGuid(), "CC-0001", comoPdf: true);

        eventId.Should().BeNull();
    }

    [Fact]
    public async Task Sin_cooperativa_activa_no_inventa_un_flujo_y_va_por_la_auditoria_de_siempre()
    {
        var eventId = await Emisor(cooperativa: null).EmitirExportacionAsync("cartera", new { }, "pdf", 1);

        eventId.Should().BeNull("Mongo no devuelve el identificador del evento");
        await _mongo.Received(1).LogAsync(Arg.Is<AuditLogCommand>(c => c.Action == "Lending.Report.Exported"), Arg.Any<CancellationToken>());
        (await _db.AuditOutbox.CountAsync()).Should().Be(0);
    }
}
