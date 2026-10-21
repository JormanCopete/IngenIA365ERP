using FluentAssertions;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Configuration;

/// <summary>
/// Feature 013, T0334 (US1; api.md §3.1; data-model §1.2; FR-032): cerrar, reabrir y borrar una línea. Cerrar no toca los créditos vivos y
/// deja la línea sin créditos nuevos (<c>AdmitsNewCredits = false</c>; simular, radicar y vender lo prueba US2 con
/// <c>Lending.CreditLine.Closed</c>); cerrar dos veces es <c>AlreadyClosed</c>; reabrir una que no está cerrada es <c>NotClosed</c> y reabrir
/// revalida la usura del día; borrar es lógico y sólo si la línea nunca tuvo solicitud, crédito, cupo, reserva ni venta: si no,
/// <c>HasLoans</c> con <c>data.alternatives = ["Close"]</c>.
/// </summary>
public sealed class CreditLineLifecycleTests : IDisposable
{
    private readonly LineasDePrueba _l = new();

    public CreditLineLifecycleTests() => _l.NormativaAsync(0.25m).GetAwaiter().GetResult();

    public void Dispose() => _l.Dispose();

    private async Task<IngenIA365ERP.Application.Common.Models.Result<CreditLineDto>> Cerrar(Guid linea, string motivo = "Se reemplaza por otra línea")
    {
        var r = await _l.Cerrar().Handle(new CloseCreditLineCommand(linea, motivo) { OperationKey = Guid.NewGuid() }, default);
        _l.Db.ChangeTracker.Clear();
        return r;
    }

    private async Task<IngenIA365ERP.Application.Common.Models.Result<CreditLineDto>> Reabrir(Guid linea)
    {
        var r = await _l.Reabrir().Handle(new ReopenCreditLineCommand(linea, "Vuelve a ofrecerse") { OperationKey = Guid.NewGuid() }, default);
        _l.Db.ChangeTracker.Clear();
        return r;
    }

    [Fact]
    public async Task Cerrar_deja_la_linea_cerrada_con_motivo_y_quien_sin_creditos_nuevos()
    {
        var linea = await _l.LineaAsync("CONLIB");

        var r = await Cerrar(linea.PublicId);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Status.Should().Be(CreditLineStatus.Closed);
        r.Value.ClosedReason.Should().Be(CreditLineClosedReason.Manual);
        r.Value.CloseNotes.Should().Be("Se reemplaza por otra línea");
        r.Value.AdmitsNewCredits.Should().BeFalse("una línea cerrada no admite simular, radicar ni ventas nuevas");
        (await _l.Db.CreditLines.SingleAsync()).ClosedByUserId.Should().Be(_l.Jefe.Id);
        (await _l.Db.CreditLineVersions.CountAsync()).Should().Be(1, "cerrar no crea ni toca versiones");
        (await Cerrar(linea.PublicId)).Error.Code.Should().Be("Lending.CreditLine.AlreadyClosed");
    }

    [Fact]
    public async Task Reabrir_una_linea_que_no_esta_cerrada_es_NotClosed()
    {
        var linea = await _l.LineaAsync("CONLIB");

        (await Reabrir(linea.PublicId)).Error.Code.Should().Be("Lending.CreditLine.NotClosed");
    }

    [Fact]
    public async Task Reabrir_revalida_la_usura_del_dia()
    {
        var linea = await _l.LineaAsync("CONLIB", LineasDePrueba.Version(0.22m));
        await Cerrar(linea.PublicId);
        await _l.UsuraAsync(LineasDePrueba.ClaveConsumo, 0.21m, LineasDePrueba.Hoy);

        var r = await Reabrir(linea.PublicId);

        r.Error.Code.Should().Be("Lending.Usury.Exceeded", "la usura del día bajó a 21 % y la línea está en 22 %");
        (await _l.Db.CreditLines.SingleAsync()).Status.Should().Be(CreditLineStatus.Closed);
    }

    [Fact]
    public async Task Reabrir_con_la_usura_del_dia_vuelve_a_Active()
    {
        var linea = await _l.LineaAsync("CONLIB");
        await Cerrar(linea.PublicId);

        var r = await Reabrir(linea.PublicId);

        r.Value.Status.Should().Be(CreditLineStatus.Active);
        r.Value.ClosedReason.Should().BeNull();
        r.Value.AdmitsNewCredits.Should().BeTrue();
    }

    [Fact]
    public async Task Borrar_una_linea_sin_uso_es_logico()
    {
        var linea = await _l.LineaAsync("CONLIB");

        var r = await _l.Borrar().Handle(new DeleteCreditLineCommand(linea.PublicId, "Se creó por error") { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var borrada = await _l.Db.CreditLines.IgnoreQueryFilters().SingleAsync();
        borrada.IsDeleted.Should().BeTrue();
        borrada.DeletedBy.Should().NotBeNullOrWhiteSpace();
        (await _l.Db.CreditLineVersions.IgnoreQueryFilters().CountAsync()).Should().Be(1, "las versiones son hechos y quedan");
        (await _l.Leer().Handle(new GetCreditLineQuery(linea.PublicId), default)).Error.Code.Should().Be("Lending.CreditLine.NotFound");
    }

    [Fact]
    public async Task Borrar_una_linea_con_solicitudes_es_HasLoans_con_la_alternativa_de_cerrar()
    {
        var linea = await _l.LineaAsync("CONLIB");
        var entidad = await _l.Db.CreditLines.SingleAsync();
        var version = await _l.Db.CreditLineVersions.SingleAsync();
        _l.Db.CreditApplications.Add(new CreditApplication
        {
            ApplicantPersonId = _l.Aseguradora.Id, DebtorKind = DebtorKind.Associate, BranchId = 1, CreditLineId = entidad.Id,
            CreditLineVersionId = version.Id, RequestedAmount = 1_000_000m, RepaymentChannel = RepaymentChannel.Cashier,
        });
        await _l.Db.SaveChangesAsync();
        _l.Db.ChangeTracker.Clear();

        var r = await _l.Borrar().Handle(new DeleteCreditLineCommand(linea.PublicId, "Ya no se ofrece") { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be("Lending.CreditLine.HasLoans");
        LineasDePrueba.Datos(r.Error).GetProperty("alternatives")[0].GetString().Should().Be("Close");
        (await _l.Db.CreditLines.SingleAsync()).IsDeleted.Should().BeFalse();
    }
}
