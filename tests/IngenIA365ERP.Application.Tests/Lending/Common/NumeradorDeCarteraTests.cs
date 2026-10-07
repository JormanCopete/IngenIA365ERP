using FluentAssertions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Common;

/// <summary>
/// Feature 013, T0134 (C48; data-model §0.3, §1.10): <see cref="NumeradorDeCartera"/> arma <c>Prefix + NextValue</c> con
/// <c>MinDigits</c>, lo incrementa por (clase, ámbito), crea la fila que falta, salta un número que ya existe en su tabla (migrado o
/// reservado; FR-057), numera las actas por órgano con su prefijo, bloquea la fila existente con el cerrojo y nunca numera un borrador.
/// </summary>
public class NumeradorDeCarteraTests
{
    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly ICerrojoDeCartera _cerrojo = Substitute.For<ICerrojoDeCartera>();

    private NumeradorDeCartera Numerador() => new(_db, _cerrojo);

    private LendingNumberSequence Secuencia(LendingNumberKind clase, string ambito, string prefijo, long siguiente, byte digitos)
    {
        var s = new LendingNumberSequence { Kind = clase, ScopeKey = ambito, Prefix = prefijo, NextValue = siguiente, MinDigits = digitos };
        _db.LendingNumberSequences.Add(s);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        return s;
    }

    [Fact]
    public async Task Asigna_prefijo_y_numero_con_sus_digitos_e_incrementa()
    {
        var s = Secuencia(LendingNumberKind.Operation, "Payment", "RC-", 41, 6);

        (await Numerador().AsignarAsync(LendingNumberKind.Operation, "Payment", "IGNORADO")).Should().Be("RC-000041");
        (await Numerador().AsignarAsync(LendingNumberKind.Operation, "Payment", "IGNORADO")).Should().Be("RC-000042");
        await _db.SaveChangesAsync();

        (await _db.LendingNumberSequences.SingleAsync(x => x.Id == s.Id)).NextValue.Should().Be(43);
        await _cerrojo.Received(1).BloquearNumeracionAsync(s.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cada_clase_y_ambito_lleva_su_propio_consecutivo_y_la_fila_que_falta_se_crea()
    {
        Secuencia(LendingNumberKind.Operation, "Payment", "RC-", 10, 0);

        var desembolso = await Numerador().AsignarAsync(LendingNumberKind.Operation, "Disbursement", "DS-");
        var pago = await Numerador().AsignarAsync(LendingNumberKind.Operation, "Payment", "RC-");
        var otroDesembolso = await Numerador().AsignarAsync(LendingNumberKind.Operation, "Disbursement", "DS-");
        await _db.SaveChangesAsync();

        desembolso.Should().Be("DS-" + "1".PadLeft(NumeradorDeCartera.DigitosPorDefecto, '0'));
        otroDesembolso.Should().Be("DS-" + "2".PadLeft(NumeradorDeCartera.DigitosPorDefecto, '0'), "la fila recién creada sigue en la misma unidad");
        pago.Should().Be("RC-10");
        (await _db.LendingNumberSequences.CountAsync()).Should().Be(2);
        (await _db.LendingNumberSequences.SingleAsync(x => x.ScopeKey == "Disbursement")).NextValue.Should().Be(3);
    }

    [Fact]
    public async Task Salta_un_numero_que_ya_existe_migrado_o_reservado()
    {
        Secuencia(LendingNumberKind.Loan, string.Empty, string.Empty, 100, 0);
        _db.Loans.Add(new Loan { Number = "100", DebtorPersonId = 1, BranchId = 1, CreditLineId = 1 });
        _db.LoanNumberReservations.Add(new LoanNumberReservation { Number = "101", PersonId = 1, IdempotencyKey = "k", RequestSha256 = "h" });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        (await Numerador().AsignarAsync(LendingNumberKind.Loan, string.Empty, string.Empty)).Should().Be("102",
            "los números no tienen que ser continuos (FR-057)");
    }

    [Fact]
    public async Task El_acta_se_numera_con_el_ambito_y_el_prefijo_del_organo_y_nunca_en_borrador()
    {
        var consejo = new GoverningBody { Code = "CAD", Name = "Consejo", MinutesPrefix = "ACTA-CAD-" };
        var sesion = new Session { Status = SessionStatus.Draft };

        await FluentActions.Awaiting(() => Numerador().NumerarActaAsync(sesion, consejo)).Should().ThrowAsync<InvalidOperationException>();
        (await _db.LendingNumberSequences.AnyAsync()).Should().BeFalse("un borrador no consume número");

        sesion.Status = SessionStatus.Closed;
        await Numerador().NumerarActaAsync(sesion, consejo);

        sesion.Number.Should().StartWith("ACTA-CAD-");
        _db.LendingNumberSequences.Local.Should().ContainSingle(s => s.Kind == LendingNumberKind.SessionMinutes
            && s.ScopeKey == consejo.PublicId.ToString("N") && s.Prefix == "ACTA-CAD-");
    }

    [Fact]
    public async Task La_solicitud_se_numera_al_radicar_y_nunca_en_borrador()
    {
        var solicitud = new CreditApplication { Status = CreditApplicationStatus.Draft };

        await FluentActions.Awaiting(() => Numerador().NumerarSolicitudAsync(solicitud, "AP-")).Should().ThrowAsync<InvalidOperationException>();
        solicitud.Number.Should().BeNull();

        solicitud.Status = CreditApplicationStatus.Submitted;
        await Numerador().NumerarSolicitudAsync(solicitud, "AP-");
        solicitud.Number.Should().StartWith("AP-");
        await FluentActions.Awaiting(() => Numerador().NumerarSolicitudAsync(solicitud, "AP-")).Should().ThrowAsync<InvalidOperationException>(
            "un número no se reasigna");
    }
}
