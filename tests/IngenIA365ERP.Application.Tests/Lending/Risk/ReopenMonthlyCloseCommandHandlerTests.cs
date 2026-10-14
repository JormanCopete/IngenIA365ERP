using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Risk;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Risk;

/// <summary>
/// Feature 013, US9, T0678 (C58; data-model §23.4; contracts/contabilidad.md §11.4; mensajes.md §10.18, §10.23): reabrir encola 202 la corrida
/// <c>CloseReopen</c>; sólo el último cierre vigente (<c>NotLast</c>), nunca el base (<c>BaseNotReopenable</c>) y antes del cierre contable
/// (<c>AccountingPeriodClosed</c>, con lo que respondió Contabilidad sellado). La corrida inserta la reapertura, una parte <c>CloseReopening</c> por parte
/// del cierre con <c>ReversedPartId</c> y su <c>ReversoDeCierre</c> (clave <c>Reopen:{versión}</c>, <c>related</c> la parte original, misma fecha contable,
/// modo heredado, el general incluido) y <c>CierreDeCarteraReabierto</c>, con su propio lote; el encabezado y las líneas quedan intactos, el estado
/// <c>Reopened</c> se deriva y volver a cerrar crea <c>Version + 1</c> con <c>SupersedesCloseId</c>.
/// </summary>
public class ReopenMonthlyCloseCommandHandlerTests : IAsyncLifetime
{
    private CierreDePrueba _c = null!;
    private MonthlyClose _diciembre = null!;

    public async Task InitializeAsync()
    {
        _c = await CierreDePrueba.CrearAsync();
        await _c.LineaDelBaseAsync(RatingCategory.C, deterioroDeCapital: 100_000m);
        await _c.CausarDiciembreAsync();
        _diciembre = await _c.CerrarAsync(2026, 12);
    }

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Reabrir_inserta_la_reapertura_y_revierte_cada_parte_sin_tocar_el_cierre()
    {
        var lineasAntes = await _c.Db.MonthlyCloseLoans.AsNoTracking().CountAsync();
        var partesDelCierre = await _c.Db.AccountingFactParts.AsNoTracking().Where(p => p.MonthlyCloseId == _diciembre.Id).OrderBy(p => p.Id).ToListAsync();

        var corrida = await _c.ReabrirAsync(_diciembre);

        corrida.Kind.Should().Be(LendingProcessKind.CloseReopen);
        (corrida.PeriodYear, corrida.PeriodMonth, corrida.CloseVersion).Should().Be(((short?)2026, (byte?)12, (int?)1));
        var reapertura = await _c.Db.MonthlyCloseReopenings.AsNoTracking().SingleAsync();
        reapertura.Should().BeEquivalentTo(new
        {
            MonthlyCloseId = _diciembre.Id, ProcessRunId = corrida.Id, ReopenedByUserId = _c.Jefe.Id, ReversalPartCount = partesDelCierre.Count,
            Reason = "Calificación por análisis registrada tarde",
        }, o => o.ExcludingMissingMembers());
        reapertura.AccountingPeriodStatusJson.Should().Contain("\"state\":\"Open\"");

        var reversos = await _c.Db.AccountingFactParts.AsNoTracking().Where(p => p.MonthlyCloseReopeningId == reapertura.Id).OrderBy(p => p.Id).ToListAsync();
        reversos.Select(p => p.ReversedPartId).Should().Equal(partesDelCierre.Select(p => (int?)p.Id));
        reversos.Select(p => (p.ProcessRunId, p.BatchNumber, p.BranchId, p.FactType)).Should().OnlyHaveUniqueItems(
            "UK_LND_AccountingFactParts_Batch: cada reverso es su propia parte de la corrida (la base en memoria no lo vigila; bloque 12C)");
        reversos.Should().OnlyContain(p => p.SourceKind == AccountingFactPartSource.CloseReopening && p.FactType == ReversoDeCierreV1.Type
            && p.OriginEventKey == "Reopen:1" && p.AccountingDate == CierreDePrueba.FinDeDiciembre);

        var mensajes = await _c.Db.IntegrationMessages.AsNoTracking().Where(m => m.Type == ReversoDeCierreV1.Type).OrderBy(m => m.Id).ToListAsync();
        mensajes.Should().HaveCount(partesDelCierre.Count);
        mensajes.Select(m => m.RelatedPublicId).Should().Equal(partesDelCierre.Select(p => (Guid?)p.PublicId), "related = la parte que revierte (§10.18)");
        mensajes.Should().OnlyContain(m => m.OperationDate == CierreDePrueba.FinDeDiciembre && m.OriginEventKey == "Reopen:1");
        var conGeneral = mensajes.Select(m => JsonSerializer.Deserialize<ReversoDeCierreV1>(m.PayloadJson!, OpcionesDeMensajes.Opciones)!)
            .Where(x => x.ReversedType == DeterioroCalculadoV1.Type && x.GeneralImpairment is { Count: > 0 }).ToList();
        conGeneral.Should().ContainSingle("el reverso del deterioro lleva también el general de la parte revertida");

        var entregaDelReverso = await _c.Db.IntegrationMessageDeliveries.AsNoTracking().FirstAsync(e => e.MessageId == mensajes[0].Id);
        var entregaDelOriginal = await (from e in _c.Db.IntegrationMessageDeliveries.AsNoTracking()
                                        join m in _c.Db.IntegrationMessages.AsNoTracking() on e.MessageId equals m.Id
                                        where m.PublicId == partesDelCierre[0].MessagePublicId
                                        select e).FirstAsync();
        (entregaDelReverso.Mode, entregaDelReverso.ScheduleKey == null ? null : "con clave").Should()
            .Be((entregaDelOriginal.Mode, entregaDelOriginal.ScheduleKey == null ? null : "con clave"), "el reverso hereda el modo de la parte (C08)");
        entregaDelReverso.BatchId.Should().NotBe(entregaDelOriginal.BatchId, "la reapertura crea su propio lote (C58)");

        var informativo = await _c.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == CierreDeCarteraReabiertoV1.Type);
        (informativo.OriginPublicId, informativo.RelatedPublicId, informativo.OriginEventKey).Should().Be((_diciembre.PublicId, (Guid?)_diciembre.PublicId, "Reopen:1"));

        var encabezado = await _c.Db.MonthlyCloses.AsNoTracking().SingleAsync(c => c.Id == _diciembre.Id);
        encabezado.Should().BeEquivalentTo(_diciembre, o => o.Excluding(c => c.RowVersion), "el encabezado no cambia (R61)");
        (await _c.Db.MonthlyCloseLoans.AsNoTracking().CountAsync()).Should().Be(lineasAntes);
        var lista = await new ListMonthlyClosesQueryHandler(_c.Db).Handle(new ListMonthlyClosesQuery(2026, MonthlyCloseKind.Monthly), default);
        lista.Value.Should().ContainSingle().Which.Status.Should().Be(nameof(MonthlyCloseStatus.Reopened), "el estado se deriva de la reapertura");
    }

    [Fact]
    public async Task Volver_a_cerrar_crea_la_version_siguiente_que_reemplaza_a_la_reabierta()
    {
        await _c.ReabrirAsync(_diciembre);

        var otra = await _c.CerrarAsync(2026, 12);

        (otra.Version, otra.SupersedesCloseId, otra.PreviousCloseId).Should().Be((2, (int?)_diciembre.Id, (int?)_c.Base!.Id));
        var informativos = await _c.Db.IntegrationMessages.AsNoTracking().Where(m => m.Type == CierreDeCarteraRealizadoV1.Type).OrderBy(m => m.Id).ToListAsync();
        informativos.Select(m => m.OriginEventKey).Should().Equal("Close:1", "Close:2");
        (await CategoriaContableDeCartera.UltimoCierreVigenteAsync(_c.Db, new DateOnly(2027, 1, 1), default))!.Id.Should().Be(otra.Id);
    }

    [Fact]
    public async Task Solo_se_reabre_el_ultimo_cierre_vigente_y_nunca_el_base()
    {
        await _c.C.CausarHastaAsync(new DateOnly(2027, 1, 31));
        _c.IrA(new DateOnly(2027, 2, 2));
        await _c.CerrarAsync(2027, 1);

        var diciembre = await _c.Reabrir().Handle(new ReopenMonthlyCloseCommand(_diciembre.PublicId, "Ajuste") { OperationKey = Guid.NewGuid() }, default);
        var base_ = await _c.Reabrir().Handle(new ReopenMonthlyCloseCommand(_c.Base!.PublicId, "Ajuste") { OperationKey = Guid.NewGuid() }, default);

        diciembre.Error.Code.Should().Be(ErroresDeRiesgo.CodigoNoEsElUltimo);
        base_.Error.Code.Should().Be(ErroresDeRiesgo.CodigoBaseNoSeReabre);
    }

    [Fact]
    public async Task Con_el_mes_cerrado_en_Contabilidad_no_se_reabre()
    {
        _c.D.Contabilidad.EstadoDelPeriodoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(new EstadoDelPeriodoDto(true, EstadoDePeriodoContable.Closed, new DateOnly(2027, 1, 1)))));

        var r = await _c.Reabrir().Handle(new ReopenMonthlyCloseCommand(_diciembre.PublicId, "Ajuste") { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be(ErroresDeRiesgo.CodigoPeriodoContableCerrado);
        (await _c.Db.LendingProcessRuns.CountAsync(x => x.Kind == LendingProcessKind.CloseReopen)).Should().Be(0);
    }
}
