using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Tests.Lending.Payments;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Integration;

/// <summary>
/// Feature 013, US5, T0522 (interfaces §6.1, §12; FR-061, FR-112, C26, C32, C44): la consulta de obligaciones real. <c>ObligacionesAsync</c> por persona
/// como deudor y codeudor, las vivas con las castigadas, las cerradas con <c>ClosedOn</c> si se piden, los saldos de la reconstrucción a la fecha en
/// lote; <c>CuotasDeNominaAsync</c> sólo las vivas como deudor con nómina propia y las cuotas del período, sin las cubiertas por anticipo y sin vencido
/// acumulado en K1; sin activar, <c>NotActivated</c> con <c>MigratedNotActivated</c> para los de la carga aplicada; sin el alcance de Cartera.
/// </summary>
public class ObligacionesDeCarteraTests : IAsyncLifetime
{
    private PagosDePrueba _p = null!;
    private static readonly DateOnly Hoy = PagosDePrueba.Hoy;

    public async Task InitializeAsync() => _p = await PagosDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _p.Dispose();
        return Task.CompletedTask;
    }

    private Task<ObligacionesDePersonasDto> ConsultarAsync(bool codeudas = false, bool cerradas = false, DateOnly? fecha = null, params Guid[] personas) =>
        _p.Obligaciones().ObligacionesAsync(new ConsultaDeObligaciones(personas.Length == 0 ? [_p.D.O.Ana.PublicId] : personas, fecha ?? Hoy, codeudas, cerradas), default);

    [Fact]
    public async Task Devuelve_las_obligaciones_de_la_persona_con_sus_saldos_de_la_reconstruccion_y_sin_el_alcance_de_Cartera()
    {
        _p.G.AlcanceActual = AlcanceDeCartera.Vacio;

        var r = await ConsultarAsync();

        r.LendingStatus.Should().Be(EstadoDeCartera.Active);
        var o = r.People.Should().ContainSingle().Subject.Obligations.Should().ContainSingle().Subject;
        o.ObligationPublicId.Should().Be(_p.Credito.PublicId);
        (o.Role, o.Status, o.IsLive, o.ClosedOn).Should().Be((RolEnLaObligacion.Debtor, EstadoDeObligacion.Active, true, (DateOnly?)null));
        o.Principal.Should().Be(3_000_000m);
        o.TotalBalance.Should().Be(3_000_000m);
        o.PendingInstallments.Should().Be(24);
        o.Kind.Should().Be(ClaseDeObligacion.Standard, "la forma de pago es caja");
    }

    [Fact]
    public async Task Como_codeudor_solo_si_se_pide()
    {
        var deBeto = await _p.C.DesembolsarAsync(_p.D.O.Solicitud(monto: 1_000_000m, cuotas: 12, solicitante: _p.D.O.Beto, codeudores: _p.D.O.Ana));

        (await ConsultarAsync()).People[0].Obligations.Should().ContainSingle();
        var con = (await ConsultarAsync(codeudas: true)).People[0].Obligations;
        con.Should().HaveCount(2);
        con.Single(o => o.ObligationPublicId == deBeto.PublicId).Role.Should().Be(RolEnLaObligacion.CoDebtor);
    }

    [Fact]
    public async Task Las_castigadas_son_vivas_y_las_cerradas_salen_solo_si_se_piden_con_su_fecha()
    {
        var otro = await _p.C.DesembolsarAsync(_p.D.O.Solicitud(monto: 1_000_000m, cuotas: 12));
        foreach (var (id, estado) in new[] { (_p.Credito.Id, LoanStatus.WrittenOff), (otro.Id, LoanStatus.PaidOff) })
        {
            var l = await _p.Db.Loans.SingleAsync(x => x.Id == id);
            l.Status = estado;
            if (estado == LoanStatus.PaidOff) l.PaidOffOn = Hoy;
            else l.WrittenOffOn = Hoy;
        }
        await _p.Db.SaveChangesAsync();
        _p.Db.ChangeTracker.Clear();

        var vivas = (await ConsultarAsync()).People[0].Obligations;
        vivas.Should().ContainSingle().Which.Should().Match<ObligacionDto>(o => o.Status == EstadoDeObligacion.WrittenOff && o.IsLive);
        var todas = (await ConsultarAsync(cerradas: true)).People[0].Obligations;
        todas.Single(o => o.ObligationPublicId == otro.PublicId).Should().Match<ObligacionDto>(o => !o.IsLive && o.ClosedOn == Hoy);
    }

    [Fact]
    public async Task Responde_en_bloque_una_entrada_por_persona_pedida()
    {
        var personas = Enumerable.Range(0, 300).Select(_ => Guid.NewGuid()).Append(_p.D.O.Ana.PublicId).ToArray();

        var r = await ConsultarAsync(personas: personas);

        r.People.Should().HaveCount(301);
        r.People.Single(p => p.PersonPublicId == _p.D.O.Ana.PublicId).Obligations.Should().ContainSingle();
        r.People.Where(p => p.PersonPublicId != _p.D.O.Ana.PublicId).Should().OnlyContain(p => p.Obligations.Count == 0);
    }

    [Fact]
    public async Task Las_cuotas_de_nomina_son_las_del_periodo_de_las_obligaciones_con_nomina_propia_sin_las_cubiertas_por_anticipo()
    {
        var ana = await _p.Db.People.SingleAsync(p => p.Id == _p.D.O.Ana.Id);
        ana.IsEmployee = true;
        await _p.Db.SaveChangesAsync();
        var deNomina = await _p.C.DesembolsarAsync(_p.D.O.Solicitud(monto: 1_200_000m, cuotas: 12) with { PaymentForm = RepaymentChannel.OwnPayroll });
        var vencimiento = (await _p.Db.RepaymentInstallments.AsNoTracking()
            .Where(i => _p.Db.RepaymentScheduleVersions.Where(v => v.LoanId == deNomina.Id).Select(v => v.Id).Contains(i.ScheduleVersionId))
            .OrderBy(i => i.InstallmentNumber).FirstAsync()).DueDate;
        var consulta = new ConsultaDeCuotasDeNomina([_p.D.O.Ana.PublicId], vencimiento.AddDays(-14), vencimiento);

        var r = await _p.Obligaciones().CuotasDeNominaAsync(consulta, default);

        r.LendingStatus.Should().Be(EstadoDeCartera.Active);
        var cuota = r.Installments.Should().ContainSingle("el crédito de caja no entra").Subject;
        (cuota.ObligationPublicId, cuota.InstallmentNumber, cuota.DueDate, cuota.PastDueIncluded, cuota.Order)
            .Should().Be((deNomina.PublicId, (int?)1, vencimiento, 0m, 1));
        cuota.Amount.Should().BeGreaterThan(100_000m);

        await _p.AplicadoAsync(_p.Pago(ModalidadDePago.Advance, cuota.Amount, obligacion: deNomina.PublicId));
        (await _p.Obligaciones().CuotasDeNominaAsync(consulta, default)).Installments.Should().BeEmpty("el anticipo la cubre en su vencimiento");
    }

    [Fact]
    public async Task Sin_activar_responde_NotActivated_y_marca_a_los_de_la_carga_aplicada()
    {
        var setup = await _p.Db.LendingSetups.SingleAsync(s => s.Id == LendingSetup.IdDeLaFilaUnica);
        setup.ActivationDate = null;
        var carga = new MigrationLoad { CutoffDate = Hoy, Status = MigrationLoadStatus.Applied, AppliedAt = _p.G.UtcNow, Number = 1, CreatedByUserId = 1 };
        _p.Db.MigrationLoads.Add(carga);
        await _p.Db.SaveChangesAsync();
        var parte = new MigrationLoadPart { MigrationLoadId = carga.Id, PartKey = "loans", FileName = "c.xlsx", FileSha256 = "x", ImportedAt = _p.G.UtcNow };
        _p.Db.MigrationLoadParts.Add(parte);
        await _p.Db.SaveChangesAsync();
        _p.Db.MigrationLoanRows.Add(new MigrationLoanRow { MigrationLoadPartId = parte.Id, RowNumber = 1, SourceLoanNumber = "S-1", PersonId = _p.D.O.Ana.Id });
        await _p.Db.SaveChangesAsync();

        var r = await ConsultarAsync(personas: [_p.D.O.Ana.PublicId, _p.D.O.Beto.PublicId]);
        r.LendingStatus.Should().Be(EstadoDeCartera.NotActivated);
        r.People.Should().OnlyContain(p => p.Obligations.Count == 0);
        r.People.Single(p => p.PersonPublicId == _p.D.O.Ana.PublicId).MigratedNotActivated.Should().BeTrue();
        r.People.Single(p => p.PersonPublicId == _p.D.O.Beto.PublicId).MigratedNotActivated.Should().BeFalse();

        var cuotas = await _p.Obligaciones().CuotasDeNominaAsync(new ConsultaDeCuotasDeNomina([_p.D.O.Ana.PublicId], Hoy, Hoy.AddDays(14)), default);
        cuotas.LendingStatus.Should().Be(EstadoDeCartera.NotActivated);
        cuotas.Installments.Should().BeEmpty();
        cuotas.MigratedNotActivated.Should().Equal(_p.D.O.Ana.PublicId);
    }
}
