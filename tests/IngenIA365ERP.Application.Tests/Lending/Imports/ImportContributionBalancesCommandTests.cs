using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Integration.Savings;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Imports;
using IngenIA365ERP.Application.Lending.Origination.CreditLimits;
using IngenIA365ERP.Application.Tests.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using C = IngenIA365ERP.Application.Lending.Imports.CatalogoDePlantillasDeCartera.ColumnasDeSaldosDeAportes;

namespace IngenIA365ERP.Application.Tests.Lending.Imports;

/// <summary>
/// Feature 013, T0379 (plantillas.md §3, D19, C85): la plantilla <c>lending.contribution-balances</c> y el lector <see cref="SaldoDeAportesVigente"/>. Una
/// persona inexistente es error de fila y columna (la plantilla no crea personas); una fecha de corte futura, error; el cupo calculado usa la última
/// carga con corte ≤ D conocida en R y sella la que usó, así que corregir una fila después no cambia cupos ya otorgados; mientras la consulta de
/// Aportes responda «no disponible» manda la carga, y cuando responda, Aportes; importar exige <c>Lending.CreditLimits.Assign</c>.
/// </summary>
public sealed class ImportContributionBalancesCommandTests : IAsyncLifetime
{
    private static readonly string[] Encabezados = [C.Persona, C.FechaDeCorte, C.SaldoDeAportes, C.SaldoDeAhorroPermanente, C.Fuente];

    private OriginacionDePrueba _o = null!;
    private readonly ITabularFileReader _lector = Substitute.For<ITabularFileReader>();
    private TablaLeida _tabla = new([], [], "xlsx");

    public async Task InitializeAsync()
    {
        _o = await OriginacionDePrueba.CrearAsync(activada: false);
        _o.G.ActuarComo(_o.Asesora);
        _o.G.Permisos.EsMaestroGlobal.Returns(true);
        _lector.LeerHojaAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Result.Success(_tabla)));
    }

    public Task DisposeAsync()
    {
        _o.Dispose();
        return Task.CompletedTask;
    }

    private void Archivo(params string?[][] filas) =>
        _tabla = new TablaLeida(Encabezados, filas.Select((f, i) => new FilaLeida(i + 2, f)).ToList(), "xlsx");

    private static string?[] Fila(string persona, string corte, string aportes, string? ahorro = "0", string fuente = "Reporte de aportes") =>
        [persona, corte, aportes, ahorro, fuente];

    private async Task<Result<ImportResultDto>> ImportarAsync(ModoDeImportacion modo = ModoDeImportacion.Apply)
    {
        var servicios = new ServiceCollection().AddSingleton(_o.G.Mongo).BuildServiceProvider();
        var ejecutor = new EjecutorDeImportacion(_o.Db, _lector, _o.G.Permisos, servicios);
        var r = await new ImportContributionBalancesCommandHandler(_o.Db, ejecutor, _o.G.Actor, _o.G.Reloj)
            .Handle(new ImportContributionBalancesCommand(modo, new ArchivoDeImportacion("aportes.xlsx", [1, 2, 3]), "Saldos de noviembre"), default);
        _o.Db.ChangeTracker.Clear();
        return r;
    }

    private static IReadOnlyList<ErrorDeFila> Errores(Result<ImportResultDto> r) =>
        r.Error.Should().BeOfType<ErrorConDatos>().Subject.Data.Should().BeOfType<ImportResultDto>().Subject.Errors;

    [Fact]
    public void La_plantilla_tiene_las_columnas_de_plantillas_3_y_se_importa_con_Assign()
    {
        var d = CatalogoDePlantillasDeCartera.SaldosDeAportes;
        var registro = CatalogoDePlantillasDeCartera.Buscar("lending.contribution-balances")!;

        d.Clave.Should().Be("lending.contribution-balances");
        d.Modulo.Should().Be(ModuloDeAuditoria.Lending);
        d.Hojas[0].Columnas.Select(c => c.Nombre).Should().Equal(Encabezados);
        registro.ImportPermission.Should().Be("Lending.CreditLimits.Assign", "C85");
        registro.Definicion.Should().BeSameAs(d);
        registro.PersonalData.Should().BeTrue();
    }

    [Fact]
    public async Task Una_persona_inexistente_es_error_de_fila_y_no_guarda_nada()
    {
        Archivo(Fila(_o.Ana.TaxId!, "2026-11-30", "1500000"), Fila("999999", "2026-11-30", "100000"));

        var r = await ImportarAsync();

        r.Error.Code.Should().Be(ImportErrors.InvalidCode);
        Errores(r).Should().ContainSingle(e => e.Row == 3 && e.Column == C.Persona && e.Code == ImportErrors.CellNotFound);
        (await _o.Db.ContributionBalanceLoads.AnyAsync()).Should().BeFalse("todo o nada");
    }

    [Fact]
    public async Task Una_fecha_de_corte_futura_es_error()
    {
        Archivo(Fila(_o.Ana.TaxId!, "2026-12-15", "1500000"));

        var r = await ImportarAsync();

        Errores(r).Should().ContainSingle(e => e.Row == 2 && e.Column == C.FechaDeCorte);
    }

    [Fact]
    public async Task Aplicar_registra_las_cargas_en_un_lote_con_su_fuente_y_revisar_no_guarda()
    {
        Archivo(Fila(_o.Ana.TaxId!, "2026-11-30", "1500000", "200000"), Fila(_o.Beto.TaxId!, "2026-11-30", "800000", null, "Sistema de aportes"));

        (await ImportarAsync(ModoDeImportacion.Review)).IsSuccess.Should().BeTrue();
        (await _o.Db.ContributionBalanceLoads.AnyAsync()).Should().BeFalse("revisar no guarda");
        var r = await ImportarAsync();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var cargas = await _o.Db.ContributionBalanceLoads.ToListAsync();
        cargas.Should().HaveCount(2);
        cargas.Select(c => c.ImportPublicId).Distinct().Should().ContainSingle("un lote por archivo aplicado");
        cargas.Single(c => c.PersonId == _o.Ana.Id).PermanentSavingsBalance.Should().Be(200_000m);
        cargas.Single(c => c.PersonId == _o.Beto.Id).Source.Should().Be("Sistema de aportes");
        (await ImportarAsync()).Value.Sheets[0].Unchanged.Should().Be(2, "la misma fila otra vez queda sin cambio");
    }

    [Fact]
    public async Task El_lector_usa_la_ultima_carga_con_corte_hasta_D_conocida_en_R()
    {
        Archivo(Fila(_o.Ana.TaxId!, "2026-10-31", "1000000"));
        (await ImportarAsync()).IsSuccess.Should().BeTrue();
        var antesDeNoviembre = _o.G.UtcNow;
        _o.G.UtcNow = _o.G.UtcNow.AddHours(1);
        Archivo(Fila(_o.Ana.TaxId!, "2026-11-30", "1500000"));
        (await ImportarAsync()).IsSuccess.Should().BeTrue();
        var lector = _o.SaldosDeAportes();

        (await lector.DeAsync(_o.Ana.Id, new DateOnly(2026, 11, 15)))!.Aportes.Should().Be(1_000_000m);
        (await lector.DeAsync(_o.Ana.Id, OriginacionDePrueba.Hoy))!.Should().Match<SaldoDeAportes>(s =>
            s.Aportes == 1_500_000m && s.Corte == new DateOnly(2026, 11, 30) && s.Fuente == SaldoDeAportes.DeLaPlantilla && s.CargaId != null);
        (await lector.DeAsync(_o.Ana.Id, OriginacionDePrueba.Hoy, antesDeNoviembre))!.Aportes.Should().Be(1_000_000m, "lo conocido hasta R");
        (await lector.DeAsync(_o.Beto.Id, OriginacionDePrueba.Hoy)).Should().BeNull();
    }

    [Fact]
    public async Task Corregir_una_fila_no_cambia_un_cupo_ya_otorgado()
    {
        var linea = await _o.L.LineaAsync("APORTES", OriginacionDePrueba.VersionDeLinea() with { CreditLimit = new CreditLimitInput(ContributionsMultiple: 3m) });
        Archivo(Fila(_o.Ana.TaxId!, "2026-11-30", "1000000"));
        (await ImportarAsync()).IsSuccess.Should().BeTrue();
        var carga = await _o.Db.ContributionBalanceLoads.SingleAsync();
        var cupo = await _o.Asignar().Handle(new AssignCreditLimitCommand(_o.Ana.PublicId, CreditLimitKind.ByLine, linea.PublicId, CreditLimitOrigin.Calculated,
            null, OriginacionDePrueba.Hoy, null, null, "Cupo por aportes") { OperationKey = Guid.NewGuid() }, default);
        cupo.IsSuccess.Should().BeTrue(cupo.IsFailure ? $"{cupo.Error.Code}: {cupo.Error.Message}" : string.Empty);
        _o.Db.ChangeTracker.Clear();

        _o.G.UtcNow = _o.G.UtcNow.AddHours(1);
        Archivo(Fila(_o.Ana.TaxId!, "2026-11-30", "2000000", fuente: "Reporte corregido"));
        var correccion = await ImportarAsync();

        correccion.Value.Sheets[0].Updated.Should().Be(1);
        (await _o.Db.ContributionBalanceLoads.CountAsync()).Should().Be(2, "la corrección es otra carga: la anterior queda");
        var otorgado = await _o.Db.CreditLimits.SingleAsync();
        otorgado.Amount.Should().Be(3_000_000m);
        otorgado.ContributionBalanceLoadId.Should().Be(carga.Id, "el cupo selló la carga que usó");
        (await _o.SaldosDeAportes().DeAsync(_o.Ana.Id, OriginacionDePrueba.Hoy))!.Aportes.Should().Be(2_000_000m);
    }

    [Fact]
    public async Task Cuando_Aportes_responde_manda_Aportes()
    {
        Archivo(Fila(_o.Ana.TaxId!, "2026-11-30", "1000000"));
        (await ImportarAsync()).IsSuccess.Should().BeTrue();
        var aportes = Substitute.For<IConsultaDeAportes>();
        aportes.SaldosAsync(Arg.Any<SolicitudDeSaldoDeAportes>(), Arg.Any<CancellationToken>())
            .Returns(new SaldosDeAportesDto(true, [new SaldoDeAportesDto(_o.Ana.PublicId, 4_000_000m, 500_000m, OriginacionDePrueba.Hoy)]));

        var saldo = await new SaldoDeAportesVigente(_o.Db, aportes).DeAsync(_o.Ana.Id, OriginacionDePrueba.Hoy);

        saldo!.Aportes.Should().Be(4_000_000m);
        saldo.Fuente.Should().Be(SaldoDeAportes.DeAportes);
        saldo.CargaId.Should().BeNull();
        (await new ConsultaDeAportesNoDisponible().SaldosAsync(new SolicitudDeSaldoDeAportes([_o.Ana.PublicId], OriginacionDePrueba.Hoy), default))
            .Available.Should().BeFalse("D19: hoy Aportes responde «no disponible»");
    }

    [Fact]
    public async Task La_lista_trae_la_ultima_carga_de_cada_persona_hasta_la_fecha()
    {
        Archivo(Fila(_o.Ana.TaxId!, "2026-10-31", "1000000"), Fila(_o.Beto.TaxId!, "2026-11-30", "800000"));
        (await ImportarAsync()).IsSuccess.Should().BeTrue();
        _o.G.UtcNow = _o.G.UtcNow.AddHours(1);
        Archivo(Fila(_o.Ana.TaxId!, "2026-11-30", "1500000"));
        (await ImportarAsync()).IsSuccess.Should().BeTrue();
        var listar = new ListContributionBalancesQueryHandler(_o.Db, _o.G.Reloj);

        var hoy = (await listar.Handle(new ListContributionBalancesQuery(), default)).Value;
        var octubre = (await listar.Handle(new ListContributionBalancesQuery(AsOf: new DateOnly(2026, 11, 1)), default)).Value;

        hoy.TotalCount.Should().Be(2);
        hoy.Items.Single(i => i.Person.PersonPublicId == _o.Ana.PublicId).Contributions.Should().Be(1_500_000m);
        octubre.Items.Should().ContainSingle().Which.Contributions.Should().Be(1_000_000m);
    }
}
