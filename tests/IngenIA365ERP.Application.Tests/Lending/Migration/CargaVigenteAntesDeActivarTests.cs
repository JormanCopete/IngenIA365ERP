using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Core.People.Commands.DeletePerson;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Application.Tests.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Migration;

/// <summary>
/// Feature 013, US8, T0726 (FR-142, T85, T94; interfaces.md §6, «desplegada, sin activar»): con una carga <c>Applied</c>, el contrato de obligaciones
/// responde sin obligaciones y con <c>MigratedNotActivated</c> para los deudores <b>y codeudores</b> de esa carga (no de una <c>Replaced</c> ni de un
/// <c>Draft</c>, ni de una tanda reemplazada); borrar a uno de ellos responde <c>Person.HasActiveLoans</c>; el endeudamiento consolidado cuenta los
/// créditos de la carga como si hubieran nacido (deudor y codeudor), y con él la concentración, que revalida los cupos otorgados a mano
/// (<see cref="RevalidacionDeCuposMigrados"/>: «cupo por revalidar» y <c>Cartera.CupoPorRevalidar</c>); después de activar, nadie lee la
/// preparación. (nuevo)
/// </summary>
public sealed class CargaVigenteAntesDeActivarTests : IAsyncLifetime
{
    private OriginacionDePrueba _o = null!;
    private MigrationLoad _carga = null!;

    public async Task InitializeAsync()
    {
        _o = await OriginacionDePrueba.CrearAsync(activada: false);
        _carga = Carga(MigrationLoadStatus.Applied, "CR-1", capital: 150_000_000m);
    }

    public Task DisposeAsync()
    {
        _o.Dispose();
        return Task.CompletedTask;
    }

    private int _numero;

    /// <summary>Una carga con un crédito de Ana (capital al corte) respaldado por Beto.</summary>
    private MigrationLoad Carga(MigrationLoadStatus estado, string credito, decimal capital, MigrationPartStatus tanda = MigrationPartStatus.Imported)
    {
        var carga = new MigrationLoad { Number = ++_numero, CutoffDate = OriginacionDePrueba.Hoy.AddDays(-1), Status = estado, AppliedAt = _o.G.UtcNow,
            CreatedByUserId = _o.Asesora.Id };
        _o.Db.MigrationLoads.Add(carga);
        _o.Db.SaveChanges();
        var parte = new MigrationLoadPart { MigrationLoadId = carga.Id, PartKey = "loans", BatchNumber = 1, FileName = "c.xlsx", FileSha256 = Guid.NewGuid().ToString("N"),
            ImportedAt = _o.G.UtcNow, ImportedByUserId = _o.Asesora.Id, RowCount = 1, Status = tanda };
        _o.Db.MigrationLoadParts.Add(parte);
        _o.Db.SaveChanges();
        _o.Db.MigrationLoanRows.Add(new MigrationLoanRow { MigrationLoadPartId = parte.Id, RowNumber = 2, SourceLoanNumber = credito, PersonId = _o.Ana.Id,
            CreditLineCode = "CONLIB", DisbursementDate = OriginacionDePrueba.Hoy.AddMonths(-6), OriginalAmount = capital });
        _o.Db.MigrationCoDebtorRows.Add(new MigrationCoDebtorRow { MigrationLoadPartId = parte.Id, RowNumber = 2, SourceLoanNumber = credito, CoDebtorPersonId = _o.Beto.Id });
        _o.Db.MigrationBalanceRows.Add(new MigrationBalanceRow { MigrationLoadPartId = parte.Id, RowNumber = 2, SourceLoanNumber = credito, Concept = LoanConcept.Principal,
            Amount = capital });
        _o.Db.SaveChanges();
        _o.Db.ChangeTracker.Clear();
        return carga;
    }

    private ObligacionesDeCartera Obligaciones() => new(_o.Db, new CargadorDeReconstruccion(_o.Db, Substitute.For<ISeriesRegulatorias>()));

    private async Task<IReadOnlyDictionary<Guid, bool>> MigradasAsync()
    {
        var r = await Obligaciones().ObligacionesAsync(
            new ConsultaDeObligaciones([_o.Ana.PublicId, _o.Beto.PublicId, _o.Carla.PublicId], OriginacionDePrueba.Hoy, IncludeAsCoDebtor: true), default);
        r.People.Should().OnlyContain(p => p.Obligations.Count == 0, "sin activar no hay obligaciones exigibles");
        return r.People.ToDictionary(p => p.PersonPublicId, p => p.MigratedNotActivated);
    }

    private Task ActivarAsync()
    {
        _o.Activar();
        _o.Db.ChangeTracker.Clear();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Los_deudores_y_codeudores_de_la_carga_aplicada_quedan_migrados_sin_activar()
    {
        var migradas = await MigradasAsync();

        (migradas[_o.Ana.PublicId], migradas[_o.Beto.PublicId], migradas[_o.Carla.PublicId]).Should().Be((true, true, false));
        var cuotas = await Obligaciones().CuotasDeNominaAsync(
            new ConsultaDeCuotasDeNomina([_o.Ana.PublicId, _o.Beto.PublicId], OriginacionDePrueba.Hoy, OriginacionDePrueba.Hoy.AddDays(14)), default);
        cuotas.Installments.Should().BeEmpty();
        cuotas.MigratedNotActivated.Should().BeEquivalentTo(new[] { _o.Ana.PublicId, _o.Beto.PublicId });
    }

    [Fact]
    public async Task Una_carga_reemplazada_un_borrador_o_una_tanda_reemplazada_no_cuentan()
    {
        var aplicada = await _o.Db.MigrationLoads.SingleAsync(l => l.Id == _carga.Id);
        aplicada.Status = MigrationLoadStatus.Replaced;
        await _o.Db.SaveChangesAsync();
        _o.Db.ChangeTracker.Clear();
        Carga(MigrationLoadStatus.Draft, "CR-2", 1_000m);
        Carga(MigrationLoadStatus.Applied, "CR-3", 1_000m, tanda: MigrationPartStatus.Superseded);

        (await MigradasAsync()).Values.Should().OnlyContain(m => !m);
    }

    [Fact]
    public async Task Borrar_a_un_deudor_o_codeudor_de_la_carga_responde_HasActiveLoans()
    {
        var handler = new DeletePersonCommandHandler(_o.Db, _o.G.Reloj, Substitute.For<ICurrentUserService>(), Obligaciones());

        (await handler.Handle(new DeletePersonCommand(_o.Beto.PublicId), default)).Error.Code.Should().Be(DeletePersonCommandHandler.HasActiveLoansCode);
        (await handler.Handle(new DeletePersonCommand(_o.Carla.PublicId), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task El_endeudamiento_consolidado_cuenta_la_carga_como_si_hubiera_nacido_hasta_activar()
    {
        var exposicion = await _o.Exposicion().DeVariasAsync([_o.Ana.Id, _o.Beto.Id], OriginacionDePrueba.Hoy);

        (exposicion[_o.Ana.Id].ComoDeudor, exposicion[_o.Ana.Id].ComoCodeudor).Should().Be((150_000_000m, 0m));
        (exposicion[_o.Beto.Id].ComoDeudor, exposicion[_o.Beto.Id].ComoCodeudor).Should().Be((0m, 150_000_000m));
        exposicion[_o.Ana.Id].Creditos.Should().ContainSingle().Which.Number.Should().Be("CR-1");

        var concentracion = await _o.Concentracion().MedirAsync(_o.Ana.Id, 1_000_000m, false, OriginacionDePrueba.Hoy, null, default);
        concentracion.Value.PorDeudor.Excede.Should().BeTrue("150 millones de la carga superan el 10 % de un patrimonio técnico de 1.000 millones");

        await ActivarAsync();
        (await _o.Exposicion().DeAsync(_o.Ana.Id, OriginacionDePrueba.Hoy)).Total.Should().Be(0m, "después de activar nadie lee la preparación");
        (await MigradasAsync())[_o.Ana.PublicId].Should().BeFalse();
    }

    [Fact]
    public async Task Un_cupo_otorgado_a_mano_que_la_carga_deja_por_encima_de_un_limite_queda_por_revalidar()
    {
        _o.Db.CreditLimits.Add(new CreditLimit
        {
            PersonId = _o.Ana.Id, Kind = CreditLimitKind.Consolidated, Amount = 5_000_000m, Origin = CreditLimitOrigin.Assigned,
            ValidFrom = OriginacionDePrueba.Hoy.AddDays(-20), VersionNumber = 1, RegisteredAt = _o.G.UtcNow, Reason = "Cupo del comité",
        });
        await _o.Db.SaveChangesAsync();
        _o.Db.ChangeTracker.Clear();
        var revalidacion = new RevalidacionDeCuposMigrados(_o.Db, new CargaVigenteDeCartera(_o.Db), _o.Revalidacion(), _o.L.Alertas, _o.G.Actor, _o.G.Reloj);

        var bloqueados = await revalidacion.RevalidarAsync(await _o.Db.MigrationLoads.SingleAsync(), default);
        await _o.Db.SaveChangesAsync();

        bloqueados.Should().Be(1);
        var bloqueo = await _o.Db.DebtorBlocks.AsNoTracking().SingleAsync();
        (bloqueo.PersonId, bloqueo.Reason, bloqueo.IsAutomatic).Should().Be((_o.Ana.Id, DebtorBlockReason.LimitRevalidation, true));
        await _o.L.Alertas.Received().LevantarAsync(Arg.Is<Application.Common.Alerts.AlertaALevantar>(a => a.TypeCode == Application.Common.Alerts.TiposDeAlerta.CupoPorRevalidar),
            Arg.Any<CancellationToken>());
        (await revalidacion.RevalidarAsync(await _o.Db.MigrationLoads.SingleAsync(), default)).Should().Be(0, "un cupo ya bloqueado no se bloquea dos veces");
    }
}
