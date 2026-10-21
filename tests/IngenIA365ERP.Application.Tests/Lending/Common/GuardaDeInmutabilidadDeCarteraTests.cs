using FluentAssertions;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Exceptions;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Common;

/// <summary>
/// Feature 013, T0136 (Principio XI; DT §2.3, C42, C44; data-model §0.2): la guarda que <c>ApplicationDbContext.SaveChangesAsync</c>
/// corre antes de escribir cubre los hechos de Cartera por su marca (<see cref="IHechoInmutable"/>): modificarlos o borrarlos lanza;
/// las propiedades <c>[EscrituraUnica]</c> (liberación de un resultado de listas, levantamiento de un bloqueo, anulación de un archivo
/// de desembolso, aplicación de un cobro provisional, el voto reemplazado) pasan de nulo a valor una sola vez. Molde:
/// <c>Inventory/Documents/GuardaDeInmutabilidadTests</c>.
/// </summary>
public class GuardaDeInmutabilidadDeCarteraTests
{
    private static readonly DateTime Ahora = new(2026, 10, 7, 15, 0, 0, DateTimeKind.Utc);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();

    private Task Verificar() => GuardaDeInmutabilidad.VerificarAsync(_db);

    private T Guardado<T>(T hecho) where T : class
    {
        _db.Add(hecho);
        _db.SaveChanges();
        return hecho;
    }

    [Fact]
    public async Task Una_fila_del_libro_no_se_modifica_ni_se_borra()
    {
        var fila = Guardado(new LoanTransaction { LoanId = 1, OperationId = 1, Kind = LoanTransactionKind.Accrual, Concept = LoanConcept.CurrentInterest, Amount = 100m });

        _db.Entry(fila).Property(f => f.Amount).CurrentValue = 101m;
        (await FluentActions.Awaiting(Verificar).Should().ThrowAsync<ImmutableEntityModifiedException>())
            .Which.Property.Should().Be(nameof(LoanTransaction.Amount));

        _db.Entry(fila).State = EntityState.Deleted;
        (await FluentActions.Awaiting(Verificar).Should().ThrowAsync<ImmutableEntityModifiedException>())
            .Which.EntityType.Should().Be(nameof(LoanTransaction));
    }

    [Fact]
    public async Task Las_decisiones_los_votos_y_los_puntajes_no_cambian()
    {
        var decision = Guardado(new CreditDecision { SubjectType = "CreditApplication", Outcome = CreditDecisionOutcome.Approve, DecisionSha256 = "x" });
        var puntaje = Guardado(new CreditScore { ApplicationId = 1, PersonId = 1, CreditLineVersionId = 1, Score = 700m, Explanation = "[]", InputsSha256 = "x" });

        _db.Entry(decision).Property(d => d.Outcome).CurrentValue = CreditDecisionOutcome.Deny;
        await FluentActions.Awaiting(Verificar).Should().ThrowAsync<ImmutableEntityModifiedException>();
        _db.Entry(decision).State = EntityState.Unchanged;

        _db.Entry(puntaje).Property(p => p.Score).CurrentValue = 800m;
        await FluentActions.Awaiting(Verificar).Should().ThrowAsync<ImmutableEntityModifiedException>();
    }

    [Fact]
    public async Task La_operacion_admite_una_sola_vez_el_reverso_que_la_reversa()
    {
        var op = Guardado(new LoanOperation { LoanId = 1, Kind = LoanOperationKind.Payment, Number = "RC-1", Payload = "{}", InputsSha256 = "x", RulesVersion = "v" });

        op.ReversedByOperationId = 9;
        await Verificar();
        await _db.SaveChangesAsync();

        op.ReversedByOperationId = 10;
        (await FluentActions.Awaiting(Verificar).Should().ThrowAsync<ImmutableEntityModifiedException>())
            .Which.Property.Should().Be(nameof(LoanOperation.ReversedByOperationId));
    }

    [Fact]
    public async Task La_liberacion_el_levantamiento_la_anulacion_y_la_aplicacion_son_de_escritura_unica()
    {
        var resultado = Guardado(new ScreeningResult { PersonId = 1, ChannelCode = "CARGA-PROPIA", ListVersions = "[]", Outcome = ScreeningOutcome.Match, QueriedAt = Ahora });
        var bloqueo = Guardado(new DebtorBlock { PersonId = 1, Reason = DebtorBlockReason.Manual, SourceKey = "k", StartedOn = new DateOnly(2026, 10, 1) });
        var archivo = Guardado(new DisbursementFile { Number = "DF-1", FormatCode = "CSV", SourceAccountNumber = "1", FileName = "a.txt", FileSha256 = "x" });
        var cobro = Guardado(new ProvisionalCollection { ActivationId = 1, RowNumber = 1, InvoiceNumber = "FV-1", Amount = 10m, AccountingReference = "REF" });
        var voto = Guardado(new SessionVote { ItemId = 1, PersonId = 1, MemberId = 1, Value = VoteValue.For, RecordedByUserId = 1, RecordedAt = Ahora });

        resultado.ReleasedAt = Ahora; resultado.ReleasedByUserId = 4; resultado.ReleaseReason = "Homónimo";
        bloqueo.LiftedAt = Ahora; bloqueo.LiftedOn = new DateOnly(2026, 10, 7); bloqueo.LiftedByUserId = 4; bloqueo.LiftReason = "Pagó";
        archivo.VoidedAt = Ahora; archivo.VoidedByUserId = 4; archivo.VoidReason = "Error";
        cobro.LoanId = 3; cobro.OperationId = 5; cobro.AppliedAt = Ahora;
        voto.ReplacedAt = Ahora;
        await Verificar();
        await _db.SaveChangesAsync();

        resultado.ReleaseReason = "Otro motivo";
        (await FluentActions.Awaiting(Verificar).Should().ThrowAsync<ImmutableEntityModifiedException>())
            .Which.Property.Should().Be(nameof(ScreeningResult.ReleaseReason));
        _db.Entry(resultado).State = EntityState.Unchanged;

        _db.Entry(bloqueo).Property(b => b.Reason).CurrentValue = DebtorBlockReason.Garnishment;
        (await FluentActions.Awaiting(Verificar).Should().ThrowAsync<ImmutableEntityModifiedException>())
            .Which.Property.Should().Be(nameof(DebtorBlock.Reason), "lo que no es de escritura única no cambia nunca");
    }

    [Fact]
    public async Task La_activacion_y_las_filas_de_preparacion_tampoco_se_borran()
    {
        var activacion = Guardado(new LendingActivation { MigrationLoadId = 1, SessionItemId = 1, RegisteredByUserId = 1 });
        var fila = Guardado(new MigrationLoanRow { MigrationLoadPartId = 1, RowNumber = 1, SourceLoanNumber = "123" });

        _db.Entry(activacion).State = EntityState.Deleted;
        await FluentActions.Awaiting(Verificar).Should().ThrowAsync<ImmutableEntityModifiedException>();
        _db.Entry(activacion).State = EntityState.Unchanged;

        _db.Entry(fila).State = EntityState.Deleted;
        await FluentActions.Awaiting(Verificar).Should().ThrowAsync<ImmutableEntityModifiedException>();
    }
}
