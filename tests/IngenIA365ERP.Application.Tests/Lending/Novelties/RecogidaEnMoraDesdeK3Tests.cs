using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Novelties;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Tests.Lending.Disbursements;
using IngenIA365ERP.Domain.Entities.Lending.Projections;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Risk;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Novelties;

/// <summary>
/// Recoger un crédito en mora desde K3 (feature 013, T0866, T0882; FR-046, spec «Reglas de convivencia»): el desembolso común lo sigue rechazando
/// (<c>Lending.Payoff.RequiresRestructuring</c>), ahora remitiendo a Novedades; allí se tramita como refinanciación que lo recoge, que es reestructuración,
/// y el crédito nuevo hereda la marca. El recogido pasa al enlazado como en una novación (no por <c>OwnLoanPayoff</c>: ver el informe del bloque). (nuevo)
/// </summary>
public sealed class RecogidaEnMoraDesdeK3Tests
{
    [Fact]
    public async Task La_refinanciacion_que_recoge_un_credito_en_mora_es_reestructuracion_y_el_nuevo_hereda_la_marca()
    {
        using var n = await NovedadesDePrueba.CrearAsync();
        n.G.ActuarComo(n.D.O.Asesora);
        var segundo = await n.P.C.DesembolsarAsync(n.D.O.Solicitud(1_500_000m, 12));
        n.Db.ChangeTracker.Clear();
        var vencimiento = await n.PrimerVencimientoAsync();
        await n.P.CausarHastaAsync(vencimiento.AddDays(15));

        var pedida = await n.PedirAsync(NoveltyMechanism.Refinancing, new NewTermsInput(Installments: 36, PayoffLoans: [n.Credito.PublicId]),
            NoveltyRequestMotive.DebtorRequest, segundo);
        pedida.IsSuccess.Should().BeTrue(pedida.IsFailure ? $"{pedida.Error.Code}: {pedida.Error.Message}" : string.Empty);
        pedida.Value.Cause.Should().Be(NoveltyCause.Restructuring, "recoge un crédito en mora: es reestructuración aunque la pida el deudor");
        pedida.Value.Loans.Should().HaveCount(2);

        var radicada = await n.Radicar().Handle(new SubmitLoanNoveltyCommand(pedida.Value.PublicId) { OperationKey = Guid.NewGuid() }, default);
        radicada.IsSuccess.Should().BeTrue(radicada.IsFailure ? $"{radicada.Error.Code}: {radicada.Error.Message}" : string.Empty);
        n.Db.ChangeTracker.Clear();
        (await n.DecidirAsync(Domain.Lending.Attributions.LendingDecisionSubjects.Novelty, radicada.Value.PublicId, radicada.Value.Routing!.ContentSha256))
            .IsSuccess.Should().BeTrue();

        var r = await n.AplicarAsync(radicada.Value.PublicId);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var nuevo = await n.Db.Loans.AsNoTracking().SingleAsync(l => l.PublicId == r.Value.NewLoanPublicId);
        (nuevo.OriginLoanId, nuevo.OriginLinkKind).Should().Be((segundo.Id, LoanLinkKind.Refinancing));
        (await n.Db.Loans.AsNoTracking().Where(l => l.Id == n.Credito.Id || l.Id == segundo.Id).Select(l => l.Status).ToListAsync())
            .Should().AllBeEquivalentTo(LoanStatus.Novated);
        var condiciones = await n.Db.LoanTermsVersions.AsNoTracking().SingleAsync(t => t.LoanId == nuevo.Id);
        (condiciones.RestructuringCount, condiciones.RestructuredOn).Should().Be((1, n.G.HoyLocal), "el nuevo hereda la marca y el conteo");
        (await n.Db.IntegrationMessages.AsNoTracking().CountAsync(m => m.Type == CreditoNovadoV1.Type)).Should().Be(2, "un hecho por cada crédito anterior");
        await n.P.ElLibroCuadraAsync(n.Credito.Id);
        await n.P.ElLibroCuadraAsync(segundo.Id);
        await n.P.ElLibroCuadraAsync(nuevo.Id);
    }

    [Fact]
    public async Task El_desembolso_comun_con_recogida_en_mora_sigue_rechazando_y_remite_a_la_novedad()
    {
        using var d = await DesembolsoDePrueba.CrearAsync();
        var viejo = d.O.Credito(d.O.Ana, 1_000_000m, "CR-VIEJO");
        var s = await d.AprobadaAsync(d.O.Solicitud() with { OwnLoanPayoffs = [new OwnLoanPayoffInput(viejo.PublicId)] });
        d.Db.LoanDailyPositions.Add(new LoanDailyPosition
        {
            LoanId = viejo.Id, PositionDate = DesembolsoDePrueba.Hoy, Version = 1, RegisteredAt = d.G.UtcNow, Status = LoanStatus.Active, BranchId = d.O.Norte.Id,
            CreditLineId = 1, ModalityCode = "CONSUMO", Principal = 1_000_000m, PayoffAmount = 1_050_000m, DaysPastDue = 12,
        });
        await d.Db.SaveChangesAsync();

        var r = await d.DesembolsarAsync(d.Entrada(s) with { OwnLoanPayoffs = [new OwnLoanPayoffInput(viejo.PublicId)] });

        r.Error.Code.Should().Be(ErroresDeDesembolso.CodigoRequiereReestructuracion);
        r.Error.Message.Should().Contain("Novedades").And.Contain("refinanciación");
        var datos = DesembolsoDePrueba.Datos(r.Error);
        (datos.GetProperty("link").GetString(), datos.GetProperty("mechanism").GetString()).Should().Be(("/cartera/novedades", "Refinancing"));
    }
}
