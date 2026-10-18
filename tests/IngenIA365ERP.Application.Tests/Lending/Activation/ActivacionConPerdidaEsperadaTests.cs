using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Application.Lending.Risk;
using IngenIA365ERP.Application.Tests.Lending.Risk;
using IngenIA365ERP.Application.Tests.Lending.Sessions;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using C = IngenIA365ERP.Application.Lending.Risk.PlantillaDeHistorialComplementario.Columnas;

namespace IngenIA365ERP.Application.Tests.Lending.Activation;

/// <summary>
/// Feature 013, K6a, T1020 (amplía <see cref="ActivateLendingCommandHandlerTests"/>; US8-10; FR-105, FR-106, FR-142): con K6a, una cooperativa
/// <b>obligada</b> no activa sin los parámetros de FR-106 vigentes al primer cierre que los usa (el Anexo 2 con la PI de su tipo) ni con créditos migrados
/// de segmentos cubiertos sin el historial de mora completo: <c>Lending.Activation.RequirementsMissing</c> con <c>data.missing[]</c> que dice qué falta y
/// lista los créditos (como <c>Lending.Migration.ArrearsHistoryRequired</c>). Completada la carga complementaria, activa. Una no obligada no exige
/// historial. Carga de <see cref="ActivacionConCarteraDePrueba"/>: CR-100 (desde septiembre, historial migrado de octubre y noviembre) y CR-200 (desde
/// noviembre, sin historial), consumo sin libranza.
/// </summary>
public sealed class ActivacionConPerdidaEsperadaTests : IAsyncLifetime
{
    private ActivacionConCarteraDePrueba _a = null!;

    public async Task InitializeAsync()
    {
        _a = await ActivacionConCarteraDePrueba.CrearAsync();
        await _a.AplicarAsync();
        _a.ConPerdidaEsperada = true;
        await PerdidaEsperadaDePrueba.ReglaAsync(_a.Db, _a.G.UtcNow, new DateOnly(2026, 1, 1));
        PerdidaEsperadaDePrueba.Perfil(_a.G, "COOP_AHORRO_CREDITO");
    }

    public Task DisposeAsync()
    {
        _a.Dispose();
        return Task.CompletedTask;
    }

    private Task AnexoAsync() => PerdidaEsperadaDePrueba.Anexo2Async(_a.Db, _a.G.UtcNow, new DateOnly(2026, 1, 1));

    private async Task<Result<LendingActivationDto>> ActivarAsync()
    {
        await _a.AceptarTodoAsync();
        return await _a.ActivarAsync(_a.Orden(_a.Acta()));
    }

    private static IReadOnlyList<(string Code, string Detail)> Faltan(Error error) =>
        GobiernoDePrueba.Datos(error).GetProperty("missing").EnumerateArray()
            .Select(m => (m.GetProperty("code").GetString()!, m.GetProperty("detail").GetString()!)).ToList();

    [Fact]
    public async Task Una_obligada_sin_historial_completo_no_activa_y_la_lista_dice_que_creditos_y_meses()
    {
        await AnexoAsync();

        var r = await ActivarAsync();

        r.Error.Code.Should().Be(ErroresDeActivacion.CodigoFaltanRequisitos);
        var historial = Faltan(r.Error).Should().ContainSingle(f => f.Code == RequisitosDeActivacion.HistorialDeMoraCompleto).Subject;
        historial.Detail.Should().Contain(ErroresDeMigracion.CodigoHistorialRequerido).And.Contain("CR-100 (2026-09)").And.Contain("CR-200 (2026-11)");
        Faltan(r.Error).Should().NotContain(f => f.Code == RequisitosDeActivacion.ParametrosDePerdidaEsperada, "el Anexo 2 sembrado trae la PI del tipo");
    }

    [Fact]
    public async Task Completada_la_carga_complementaria_activa()
    {
        await AnexoAsync();
        _a.Tabla = new TablaLeida([C.NumeroDeCredito, C.Mes, C.MoraMaximaDias, C.MoraAlCierreDias, C.Fuente],
            [new FilaLeida(2, ["CR-100", "2026-09", "0", null, "Sistema anterior"]), new FilaLeida(3, ["CR-200", "2026-11", "0", null, "Sistema anterior"])], "xlsx");
        var servicios = new ServiceCollection().AddSingleton(_a.G.Mongo).AddSingleton<IApplicationDbContext>(_a.Db).BuildServiceProvider();
        var carga = await new ImportArrearsHistoryCommandHandler(_a.Db, new EjecutorDeImportacion(_a.Db, _a.Lector, _a.G.Permisos, servicios),
                new LectorDeDocumentoNormativo(_a.Db), _a.G.Actor, _a.G.Auditoria, _a.G.Reloj)
            .Handle(new ImportArrearsHistoryCommand(ModoDeImportacion.Apply, new ArchivoDeImportacion("historial.xlsx", [1, 2, 3]), "Historial que faltaba")
                { OperationKey = Guid.NewGuid() }, default);
        carga.IsSuccess.Should().BeTrue(carga.IsFailure ? $"{carga.Error.Code}: {carga.Error.Message}" : string.Empty);
        _a.Db.ChangeTracker.Clear();

        var r = await ActivarAsync();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message} {JsonSerializer.Serialize((r.Error as ErrorConDatos)?.Data)}" : string.Empty);
        (await _a.Db.LoanArrearsHistory.AsNoTracking().CountAsync(h => h.Origin == ArrearsHistoryOrigin.ComplementaryLoad)).Should().Be(2);
    }

    [Fact]
    public async Task Una_obligada_sin_el_Anexo_2_vigente_no_activa_por_los_parametros()
    {
        var r = await ActivarAsync();

        r.Error.Code.Should().Be(ErroresDeActivacion.CodigoFaltanRequisitos);
        Faltan(r.Error).Should().Contain(f => f.Code == RequisitosDeActivacion.ParametrosDePerdidaEsperada && f.Detail.Contains("Anexo 2"));
    }

    [Fact]
    public async Task Una_no_obligada_no_exige_parametros_ni_historial()
    {
        PerdidaEsperadaDePrueba.Perfil(_a.G, "COOP_MULTIACTIVA");

        var e = await _a.Requisitos().EvaluarConAvisosAsync(await _a.Db.MigrationLoads.AsNoTracking().SingleAsync(l => l.Id == _a.Carga.Id),
            ActivacionConCarteraDePrueba.Activacion, ActivacionConCarteraDePrueba.Registro, default);

        e.Requisitos.Single(x => x.Code == RequisitosDeActivacion.ParametrosDePerdidaEsperada).Status.Should().Be(EstadosDeRequisito.Met);
        e.Requisitos.Single(x => x.Code == RequisitosDeActivacion.HistorialDeMoraCompleto).Should().BeEquivalentTo(new { Met = true, Status = EstadosDeRequisito.Met },
            o => o.ExcludingMissingMembers());
        e.Requisitos.Single(x => x.Code == RequisitosDeActivacion.HistorialDeMoraCompleto).Detail.Should().Contain("no está obligada");
    }
}
