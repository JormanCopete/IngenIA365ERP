using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Configuration;

/// <summary>
/// Feature 013, T0333 (US1-2; api.md §3.1; data-model §0.6, §1.3): las versiones de una línea por
/// <see cref="AddCreditLineVersionCommandHandler"/>. Una versión desde el mes siguiente queda registrada y la anterior rige hasta la
/// víspera (lectura bitemporal, sin <c>ValidTo</c> guardado); una que empieza antes de la última es <c>VersionOverlaps</c>; las reglas son
/// las del alta; una versión que pasa la usura limpia el bloqueo por usura; la versión nueva conserva el puntaje; una línea cerrada o fuera
/// del alcance no recibe versiones.
/// </summary>
public sealed class AddCreditLineVersionCommandHandlerTests : IDisposable
{
    private static readonly DateOnly MesSiguiente = new(2027, 1, 1);
    private readonly LineasDePrueba _l = new();

    public AddCreditLineVersionCommandHandlerTests() => _l.NormativaAsync(0.25m).GetAwaiter().GetResult();

    public void Dispose() => _l.Dispose();

    private Task<IngenIA365ERP.Application.Common.Models.Result<CreditLineVersionDto>> Versionar(Guid linea, CreditLineVersionInput version) =>
        _l.Versionar().Handle(new AddCreditLineVersionCommand(linea, version) { OperationKey = Guid.NewGuid() }, default);

    [Fact]
    public async Task Una_version_desde_el_mes_siguiente_queda_y_la_anterior_rige_hasta_la_vispera()
    {
        var linea = await _l.LineaAsync("CONLIB");

        var r = await Versionar(linea.PublicId, LineasDePrueba.Version(0.19m, MesSiguiente) with { Reason = "Baja de tasa" });

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.VersionNumber.Should().Be(2);
        r.Value.ValidFrom.Should().Be(MesSiguiente);
        r.Value.ValidTo.Should().BeNull();
        var hoy = await _l.Leer().Handle(new GetCreditLineQuery(linea.PublicId), default);
        var despues = await _l.Leer().Handle(new GetCreditLineQuery(linea.PublicId, MesSiguiente), default);
        hoy.Value.CurrentVersion!.EffectiveAnnual.Should().Be(0.20m, "la de hoy sigue vigente");
        hoy.Value.CurrentVersion.ValidTo.Should().Be(new DateOnly(2026, 12, 31), "rige hasta la víspera de la siguiente");
        despues.Value.CurrentVersion!.EffectiveAnnual.Should().Be(0.19m);
        hoy.Value.Versions.Should().HaveCount(2);
        (await _l.Db.CreditLineVersions.CountAsync()).Should().Be(2, "la versión anterior no se toca: es un hecho");
    }

    [Fact]
    public async Task Una_version_que_empieza_antes_de_la_ultima_es_VersionOverlaps()
    {
        var linea = await _l.LineaAsync("CONLIB");
        await Versionar(linea.PublicId, LineasDePrueba.Version(0.19m, MesSiguiente));

        var r = await Versionar(linea.PublicId, LineasDePrueba.Version(0.18m, new DateOnly(2026, 12, 15)));

        r.Error.Code.Should().Be("Lending.CreditLine.VersionOverlaps");
        LineasDePrueba.Datos(r.Error).GetProperty("existingValidFrom").GetString().Should().Be("2027-01-01");
    }

    [Fact]
    public async Task Las_reglas_son_las_del_alta()
    {
        var linea = await _l.LineaAsync("CONLIB");

        (await Versionar(linea.PublicId, LineasDePrueba.Version(0.30m, MesSiguiente))).Error.Code.Should().Be("Lending.Usury.Exceeded");
        (await Versionar(linea.PublicId, LineasDePrueba.Version(desde: LineasDePrueba.Hoy.AddDays(-3)))).Error.Code
            .Should().Be("Lending.CreditLine.VersionOverlaps", "empieza antes de la versión registrada");
        (await Versionar(linea.PublicId, LineasDePrueba.Version(desde: MesSiguiente, subsegmento: "Otro"))).Error.Code
            .Should().Be("Lending.CreditLine.ModalityUnknown");
        (await _l.Db.CreditLineVersions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Una_version_que_pasa_la_usura_limpia_el_bloqueo_por_usura()
    {
        var linea = await _l.LineaAsync("CONLIB");
        var entidad = await _l.Db.CreditLines.SingleAsync();
        entidad.BloquearPorUsura(LineasDePrueba.Hoy, Guid.NewGuid());
        await _l.Db.SaveChangesAsync();
        _l.Db.ChangeTracker.Clear();

        var r = await Versionar(linea.PublicId, LineasDePrueba.Version(0.18m, MesSiguiente));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var guardada = await _l.Db.CreditLines.AsNoTracking().SingleAsync();
        guardada.UsuryBlockedSince.Should().BeNull();
        guardada.UsuryBlockedByValuePublicId.Should().BeNull();
    }

    [Fact]
    public async Task Una_linea_cerrada_no_recibe_versiones()
    {
        var linea = await _l.LineaAsync("CONLIB");
        await _l.Cerrar().Handle(new CloseCreditLineCommand(linea.PublicId, "Se reemplaza") { OperationKey = Guid.NewGuid() }, default);
        _l.Db.ChangeTracker.Clear();

        var r = await Versionar(linea.PublicId, LineasDePrueba.Version(desde: MesSiguiente));

        r.Error.Code.Should().Be("Lending.CreditLine.Closed");
    }

    [Fact]
    public async Task Fuera_del_alcance_de_lineas_es_el_404_de_la_linea()
    {
        var linea = await _l.LineaAsync("CONLIB");
        _l.G.AlcanceActual = AlcanceDeCartera.Vacio;

        var r = await Versionar(linea.PublicId, LineasDePrueba.Version(desde: MesSiguiente));

        r.Error.Code.Should().Be("Lending.CreditLine.NotFound");
        (await _l.G.EventosAsync()).Should().Contain("Lending.Access.OutOfScope");
    }

    [Fact]
    public async Task La_version_nueva_conserva_el_puntaje_de_la_anterior()
    {
        var linea = await _l.LineaAsync("CONLIB");
        var puntaje = await _l.Puntuar().Handle(new SetCreditLineScoringCommand(linea.PublicId, LineasDePrueba.Hoy, "Puntaje aprobado", 600m, null,
            [new ScoringVariableDto("EDAD", "Edad", "Applicant.Age", 1m, [new ScoringRangeDto(18m, 70m, 1000m)])]) { OperationKey = Guid.NewGuid() }, default);
        puntaje.IsSuccess.Should().BeTrue(puntaje.IsFailure ? puntaje.Error.Message : string.Empty);
        _l.Db.ChangeTracker.Clear();

        var r = await Versionar(linea.PublicId, LineasDePrueba.Version(0.19m, MesSiguiente));

        r.Value.HasScoring.Should().BeTrue();
        r.Value.MinimumScore.Should().Be(600m);
    }

    [Fact]
    public async Task Los_creditos_vivos_no_se_tocan_solo_cambian_la_linea_y_sus_versiones()
    {
        var linea = await _l.LineaAsync("CONLIB");

        await Versionar(linea.PublicId, LineasDePrueba.Version(0.19m, MesSiguiente));

        (await _l.Db.Loans.CountAsync()).Should().Be(0);
        (await _l.Db.LoanTermsVersions.CountAsync()).Should().Be(0, "las condiciones selladas de un crédito no se reescriben (FR-032)");
        (await _l.Db.CreditLines.SingleAsync()).Status.Should().Be(CreditLineStatus.Active);
    }
}
