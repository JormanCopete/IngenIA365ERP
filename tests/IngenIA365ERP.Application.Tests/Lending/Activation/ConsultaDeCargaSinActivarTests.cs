using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Activation;

/// <summary>
/// Feature 015, R1, T0348 (contratos-v2.md §2.1; FR-164 b; E-01 b): <see cref="IConsultaDeCargaSinActivar.PersonaAsync"/> informa si la persona está en una carga de
/// Cartera aplicada sin activar —como deudora o codeudora— y las obligaciones que la carga le trae; fuera de la carga, en un borrador o después de activar,
/// <c>InAppliedLoad = false</c>. No escribe. Prueba escrita, sin correr (estrategia de R1).
/// </summary>
public sealed class ConsultaDeCargaSinActivarTests : IDisposable
{
    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private Person _ana = null!;
    private Person _beto = null!;
    private Person _carla = null!;
    private MigrationLoad _carga = null!;

    public void Dispose() => _db.Dispose();

    private async Task SembrarAsync(MigrationLoadStatus estado = MigrationLoadStatus.Applied, DateOnly? activada = null)
    {
        _ana = new Person { FirstName = "Ana", LastName = "Deudora", TaxId = "1001" };
        _beto = new Person { FirstName = "Beto", LastName = "Codeudor", TaxId = "1002" };
        _carla = new Person { FirstName = "Carla", LastName = "Ajena", TaxId = "1003" };
        _db.People.AddRange(_ana, _beto, _carla);
        _db.LendingSetups.Add(new LendingSetup { Id = LendingSetup.IdDeLaFilaUnica, ActivationDate = activada });
        _db.CreditLines.Add(new CreditLine { Code = "CONLIB", Name = "Consumo libre inversión" });
        _carga = new MigrationLoad { Number = 1, CutoffDate = new DateOnly(2026, 11, 30), Status = estado, AppliedAt = DateTime.UtcNow };
        _db.MigrationLoads.Add(_carga);
        await _db.SaveChangesAsync();
        var parte = new MigrationLoadPart { MigrationLoadId = _carga.Id, Status = MigrationPartStatus.Imported };
        _db.MigrationLoadParts.Add(parte);
        await _db.SaveChangesAsync();
        _db.MigrationLoanRows.Add(new MigrationLoanRow { MigrationLoadPartId = parte.Id, RowNumber = 1, SourceLoanNumber = "CR-0001", PersonId = _ana.Id,
            CreditLineCode = "CONLIB" });
        _db.MigrationCoDebtorRows.Add(new MigrationCoDebtorRow { MigrationLoadPartId = parte.Id, RowNumber = 1, SourceLoanNumber = "CR-0001",
            CoDebtorPersonId = _beto.Id, Role = CreditPartyRole.CoDebtor });
        await _db.SaveChangesAsync();
    }

    private ConsultaDeCargaSinActivar Consulta() => new(_db, new CargaVigenteDeCartera(_db));

    [Fact]
    public async Task La_deudora_y_el_codeudor_de_la_carga_aplicada_estan_en_ella_con_su_obligacion()
    {
        await SembrarAsync();

        var ana = await Consulta().PersonaAsync(_ana.PublicId, default);
        ana.Result.Should().Be(ResultadoDelContrato.Answered);
        ana.Value!.InAppliedLoad.Should().BeTrue();
        var o = ana.Value.Obligations.Should().ContainSingle().Subject;
        (o.Number, o.LineName, o.CutoffDate).Should().Be(("CR-0001", "Consumo libre inversión", new DateOnly(2026, 11, 30)));

        var beto = await Consulta().PersonaAsync(_beto.PublicId, default);
        beto.Value!.InAppliedLoad.Should().BeTrue("el codeudor también está en la carga");
    }

    [Fact]
    public async Task Fuera_de_la_carga_en_un_borrador_o_despues_de_activar_no_esta()
    {
        await SembrarAsync();
        (await Consulta().PersonaAsync(_carla.PublicId, default)).Value!.InAppliedLoad.Should().BeFalse();
        (await Consulta().PersonaAsync(Guid.NewGuid(), default)).Value!.InAppliedLoad.Should().BeFalse();

        _carga.Status = MigrationLoadStatus.Draft;
        await _db.SaveChangesAsync();
        (await Consulta().PersonaAsync(_ana.PublicId, default)).Value!.InAppliedLoad.Should().BeFalse("un borrador no cuenta");

        _carga.Status = MigrationLoadStatus.Applied;
        var setup = await _db.LendingSetups.SingleAsync();
        setup.ActivationDate = new DateOnly(2026, 12, 1);
        await _db.SaveChangesAsync();
        (await Consulta().PersonaAsync(_ana.PublicId, default)).Value!.InAppliedLoad.Should().BeFalse("después de activar nadie lee la preparación");
    }
}
