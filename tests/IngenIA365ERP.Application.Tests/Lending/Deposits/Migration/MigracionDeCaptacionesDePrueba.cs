using System.Text;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Activation;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Application.Lending.Deposits.Migration;
using IngenIA365ERP.Application.Lending.Deposits.Migration.Plantillas;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Application.Security.Roles.Plantillas;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Contributions;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using C = IngenIA365ERP.Application.Lending.Deposits.Migration.Plantillas.DefinicionesDeMigracionDeCaptaciones.Columnas;
using P = IngenIA365ERP.Application.Lending.Deposits.Migration.Plantillas.DefinicionesDeMigracionDeCaptaciones.Partes;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits.Migration;

/// <summary>
/// El escenario de la migración de aportes y captaciones (feature 014, US9, T157): el de <see cref="AportesDePrueba"/> (hoy 2026-12-01) con los créditos
/// activados y <b>ni aportes ni captaciones</b> activados; corte al 2026-11-30 y activación el 2026-12-01; la oficina <c>PRINCIPAL</c> vinculada a una
/// oficina; una línea a la vista <c>AHVISTA</c>, una de ahorro permanente <c>AHPERM</c> y una de aportes <c>APORTES</c>, todas con versión desde el
/// 2026-11-01; el lector de archivos doble (la tabla que arma la prueba), Contabilidad doble (iniciada, mes abierto, la matriz resuelve todo y el saldo
/// contable por rol que la prueba decida), los participantes y los roles que pide la activación, la entrega K11 desplegada. (nuevo)
/// </summary>
internal sealed class MigracionDeCaptacionesDePrueba : IDisposable
{
    public static readonly DateOnly Hoy = CuentasDePrueba.Hoy;
    public static readonly DateOnly Corte = Hoy.AddDays(-1);
    public static readonly DateOnly DesdeLineas = new(2026, 11, 1);

    private MigracionDeCaptacionesDePrueba() { }

    public AportesDePrueba A { get; } = new();
    public CuentasDePrueba Cu => A.C;
    public Sessions.GobiernoDePrueba G => A.G;
    public TestApplicationDbContext Db => A.Db;

    public ITabularFileReader Lector { get; } = Substitute.For<ITabularFileReader>();
    public TablaLeida Tabla { get; set; } = new([], [], "xlsx");
    public IAmbienteDeDespliegue Ambiente { get; } = Substitute.For<IAmbienteDeDespliegue>();

    /// <summary>El saldo contable (en el sentido de la Cartera) que Contabilidad responde por rol.</summary>
    public Func<string, decimal> SaldoContable { get; set; } = _ => 0m;

    public Func<CombinacionDeSaldo, bool> SinRegla { get; set; } = _ => false;

    public EntregaDeCartera Entrega { get; set; } = EntregaDeCartera.K11;

    public LendingLineDto ALaVista { get; private set; } = null!;
    public LendingLineDto Permanente { get; private set; } = null!;
    public LendingLineDto Aportes { get; private set; } = null!;
    public User Jefe => Cu.Jefe;
    public User Contadora { get; private set; } = null!;

    public static async Task<MigracionDeCaptacionesDePrueba> CrearAsync()
    {
        var m = new MigracionDeCaptacionesDePrueba();
        m.Lector.LeerHojaAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Result.Success(m.Tabla)));
        m.A.Contabilidad.SaldosDeCuentasMapeadasAsync(Arg.Any<DateOnly>(), Arg.Any<IReadOnlyList<CombinacionDeSaldo>>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var combinaciones = ci.Arg<IReadOnlyList<CombinacionDeSaldo>>();
                var sets = combinaciones.Select((c, i) => (c, i)).Where(x => !m.SinRegla(x.c))
                    .GroupBy(x => x.c.Role)
                    .Select(g => new ConjuntoDeCuentasDeCarteraDto(g.Key, g.Select(x => x.i).ToList(),
                        [new CuentaDelConjuntoDto(Cuenta(g.Key), $"Cuenta de {g.Key}", g.Key,
                            [new SaldoPorSucursalDto(g.First().c.BranchPublicId, m.SaldoContable(g.Key) * SaldosContablesDeCartera.Signo(g.Key))])],
                        0m))
                    .ToList();
                var sinRegla = combinaciones.Select((c, i) => (c, i)).Where(x => m.SinRegla(x.c)).Select(x => x.i).ToList();
                return Task.FromResult(Result.Success(new SaldosMapeadosDeCarteraDto(sets, sinRegla)));
            });
        m.A.Registro.QueCuentanAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult<IReadOnlyList<ParticipanteVigente>>(
        [
            // Feature 015, T0308: Recaudo y Tesorería reales (los dobles TST/TES ya no cuentan en ningún ambiente).
            new ParticipanteVigente(Guid.NewGuid(), "REC", IntegrationParticipantClass.Receipts, IntegrationParticipantRole.ObligationsCollector, string.Empty, false,
                new DateOnly(2026, 1, 1), null, [new VersionAdoptada(ContratosDeIntegracion.ProveedorDeCuentas, 1)]),
        ]));
        m.A.Registro.DestinoAsync(IntegrationDestinations.Treasury, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(_ =>
            Task.FromResult<ParticipanteVigente?>(new ParticipanteVigente(Guid.NewGuid(), "TES", IntegrationParticipantClass.Treasury,
                IntegrationParticipantRole.MessageDestination, IntegrationDestinations.Treasury, false, new DateOnly(2026, 1, 1), null,
                [new VersionAdoptada(OrdenDeGiroEmitidaV1.Type, 2)])));
        m.Ambiente.EsProduccion.Returns(false);

        // Ni aportes ni captaciones activados; los créditos sí.
        var setup = m.Db.LendingSetups.Single();
        setup.DepositsActivationDate = null;
        setup.ContributionsActivationDate = null;
        var oficina = m.Db.Branches.Single(b => b.Id == m.Cu.Oficina.Id);
        oficina.LegacyCode = "PRINCIPAL";
        oficina.TenantBranchPublicId = Guid.NewGuid();
        foreach (var plantilla in PerfilesSugeridos.DeCaptaciones())
            m.Db.Roles.Add(new Role { Code = CreateSuggestedLendingRolesCommand.CodigoDeRol(plantilla.Key), Name = plantilla.Actor, CreatedBy = "test" });
        m.Db.SaveChanges();

        // Las líneas con versión vigente al corte: se crean con «hoy» = 2026-11-01.
        m.G.HoyLocal = DesdeLineas;
        m.ALaVista = await m.Cu.LineaDeCaptacionAsync("AHVISTA", CuentasDePrueba.ALaVista() with { ValidFrom = DesdeLineas, RequiresMembership = false });
        m.Permanente = await m.Cu.LineaDeCaptacionAsync("AHPERM", CuentasDePrueba.ALaVista() with { ValidFrom = DesdeLineas, AllowsPartialWithdrawals = false },
            DepositClass.Permanent);
        m.Aportes = await m.Cu.LineaDeAportesAsync("APORTES", AportesDePrueba.Version() with { ValidFrom = DesdeLineas });
        m.G.HoyLocal = Hoy;
        m.Contadora = m.G.Usuario(m.G.Persona("Rafaela", "Contadora", "9002"), "contadora@coop.test");
        m.Db.ChangeTracker.Clear();
        m.G.ActuarComo(m.Jefe);
        m.G.Permisos.EsMaestroGlobal.Returns(true);
        return m;
    }

    public void Dispose() => A.Dispose();

    private static string Cuenta(string rol) => rol switch
    {
        SaldosContablesDeCaptaciones.Capital => "21050501",
        SaldosContablesDeCaptaciones.InteresPorPagar => "24250501",
        SaldosContablesDeCaptaciones.Aportes => "31050501",
        _ => "29999999",
    };

    // ---------------------------------------------------------------------------------------------------------- archivos --

    public void Archivo(string parte, params IReadOnlyDictionary<string, string?>[] filas)
    {
        var encabezados = DefinicionesDeMigracionDeCaptaciones.Buscar(parte)!.Definicion!.Hojas[0].Columnas.Select(c => c.Nombre).ToList();
        Tabla = new TablaLeida(encabezados,
            filas.Select((f, i) => new FilaLeida(i + 2, encabezados.Select(e => f.TryGetValue(e, out var v) ? v : null).ToArray())).ToList(), "xlsx");
    }

    public static Dictionary<string, string?> Fila(params (string Columna, string? Valor)[] celdas) => celdas.ToDictionary(c => c.Columna, c => c.Valor);

    public static Dictionary<string, string?> Ahorro(string numero, string titular = "1001", string capital = "1000000", string linea = "AHVISTA",
        string clase = "Demand", string estado = "Active", string? interes = null, string? liquidadoHasta = null, string? canje = null,
        string? heredado = null) => Fila(
        (C.Numero, numero), (C.Titular, titular), (C.Oficina, "PRINCIPAL"), (C.Linea, heredado is null ? linea : null), (C.CodigoHeredado, heredado),
        (C.Clase, clase), (C.FechaDeApertura, "2020-05-14"), (C.Estado, estado), (C.FechaUltimoMovimientoDelTitular, estado == "Inactive" ? "2024-01-10" : null),
        (C.SaldoCapital, capital), (C.InteresCausadoNoLiquidado, interes), (C.UltimaLiquidacionHasta, liquidadoHasta), (C.CanjePendiente, canje));

    public static Dictionary<string, string?> Aporte(string asociado, string? numero = null, string ordinarios = "2000000", string minimos = "500000",
        string linea = "APORTES") => Fila(
        (C.Asociado, asociado), (C.Numero, numero), (C.Oficina, "PRINCIPAL"), (C.Linea, linea), (C.FechaDeVinculacion, "2020-01-01"),
        (C.AportesMinimos, minimos), (C.AportesOrdinarios, ordinarios));

    public static Dictionary<string, string?> Parte(string cuenta, string persona, string rol, string? proporcion = null, string? beneficiario = null,
        string? fundamento = null, string? soporte = null) => Fila(
        (C.Cuenta, cuenta), (C.Persona, persona), (C.Rol, rol), (C.Proporcion, proporcion), (C.PorcentajeBeneficiario, beneficiario),
        (C.Fundamento, fundamento), (C.Soporte, soporte), (C.VigenteDesde, "2020-05-14"));

    // ------------------------------------------------------------------------------------------------------------- piezas --

    public EjecutorDeImportacion Ejecutor() =>
        new(Db, Lector, G.Permisos, new ServiceCollection().AddSingleton(G.Mongo).AddSingleton<IApplicationDbContext>(Db).BuildServiceProvider());

    public RequisitosDeActivacionDeCaptaciones Requisitos() =>
        new(Db, A.Registro, A.Contabilidad, Ambiente, entregaDesplegada: Entrega);

    public ImportDepositMigrationPartCommandHandler Importador() =>
        new(Db, Ejecutor(), G.Intervenciones(), G.Alcance, Requisitos(), G.Actor, G.Reloj);

    public CuadreDeCaptaciones Cuadre() => new(Db, A.Contabilidad);

    public ApplyDepositMigrationLoadCommandHandler Aplicador() =>
        new(Db, new ValidacionEntrePartesDeCaptaciones(Db, Cu.Parametros()), Cuadre(), A.Emision(), Cu.Parametros(), G.Actor, G.Reloj);

    public NacimientoDeCuentasMigradas Nacimiento() =>
        new(Db, A.RegistroDeCuentas(), A.Numerador(), A.Movimientos(), Cu.Parametros(), G.Actor, G.Reloj);

    public ActivacionDeCaptacionesConCarga Activacion() =>
        new(Db, Cu.Cerrojo, Requisitos(), new PuntoDeActaCerrado(Db), Cuadre(), Nacimiento(), A.Emision(), G.Actor, G.Reloj);

    public AcceptMigrationDifferencesCommandHandler Aceptador() => new(Db, new PuntoDeActaCerrado(Db), G.Intervenciones(), G.Actor, G.Reloj);

    // ----------------------------------------------------------------------------------------------------------- pasos --

    public async Task<MigrationLoad> CargaAsync(LineNature naturaleza, DateOnly? corte = null)
    {
        var r = await new CreateDepositMigrationLoadCommandHandler(Db, A.Numerador(), Requisitos(), G.Actor, G.Reloj)
            .Handle(new CreateDepositMigrationLoadCommand(naturaleza, corte ?? Corte, "Carga de prueba") { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        Db.ChangeTracker.Clear();
        return await Db.MigrationLoads.AsNoTracking().SingleAsync(l => l.PublicId == r.Value.PublicId);
    }

    public async Task<Result<ImportResultDto>> ImportarAsync(MigrationLoad carga, string parte, ModoDeImportacion modo = ModoDeImportacion.Apply,
        bool reemplazar = false, string? contenido = null)
    {
        var bytes = Encoding.UTF8.GetBytes(contenido ?? $"{parte}:{Guid.NewGuid()}");
        var r = await Importador().Handle(new ImportDepositMigrationPartCommand(carga.PublicId, parte, modo, new ArchivoDeImportacion("parte.xlsx", bytes),
            reemplazar, "Carga de prueba") { OperationKey = Guid.NewGuid() }, default);
        Db.ChangeTracker.Clear();
        return r;
    }

    public async Task<ImportResultDto> CargarAsync(MigrationLoad carga, string parte, params IReadOnlyDictionary<string, string?>[] filas)
    {
        Archivo(parte, filas);
        var r = await ImportarAsync(carga, parte);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message} {Errores(r).FirstOrDefault()?.Row}/{Errores(r).FirstOrDefault()?.Column} {Errores(r).FirstOrDefault()?.Message}" : string.Empty);
        return r.Value;
    }

    public async Task<Result<AppliedDepositMigrationLoadDto>> AplicarAsync(MigrationLoad carga)
    {
        var r = await Aplicador().Handle(new ApplyDepositMigrationLoadCommand(carga.PublicId, "Carga aprobada por la gerencia") { OperationKey = Guid.NewGuid() },
            default);
        Db.ChangeTracker.Clear();
        return r;
    }

    public async Task<Result<DepositsActivationDto>> ActivarAsync(MigrationLoad carga, Guid? punto, DateOnly? fecha = null)
    {
        var r = await Activacion().ActivarAsync(carga.Nature, carga.PublicId, fecha ?? carga.CutoffDate.AddDays(1), punto, true, "Activación con la carga aprobada",
            default);
        Db.ChangeTracker.Clear();
        return r;
    }

    /// <summary>Un punto cerrado y aprobado de una sesión sobre la carga (el acta de la activación).</summary>
    public Guid Acta(Guid sujeto, SessionStatus estado = SessionStatus.Closed, CreditDecisionOutcome? resultado = CreditDecisionOutcome.Approve)
    {
        var organo = G.Organo("CONSEJO" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant(), "Consejo de administración", GoverningBodyKind.BoardOfDirectors);
        var sesion = new Session { GoverningBodyId = organo.Id, Kind = SessionKind.Extraordinary, Mode = SessionMode.InPerson, HeldOn = Corte, Status = estado, CreatedBy = "test" };
        Db.LendingSessions.Add(sesion);
        Db.SaveChanges();
        var punto = new SessionItem
        {
            SessionId = sesion.Id, Order = 1, Kind = SessionItemKind.Decision, Rule = VotingRule.Majority, SubjectType = LendingDecisionSubjects.ActivationDifferences,
            SubjectPublicId = sujeto, Title = "Diferencias de la migración de captaciones", Outcome = resultado, CreatedBy = "test",
        };
        Db.SessionItems.Add(punto);
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
        return punto.PublicId;
    }

    public static IReadOnlyList<ErrorDeFila> Errores(Result<ImportResultDto> r) =>
        r.IsSuccess ? r.Value.Errors : r.Error is ErrorConDatos { Data: ImportResultDto dto } ? dto.Errors : [];

    /// <summary>Los errores entre partes de <c>Lending.Migration.CrossPartErrors</c>.</summary>
    public static IReadOnlyList<Lending.Migration.MigracionDePrueba.ErrorLeido> ErroresEntrePartes(Error error) => Lending.Migration.MigracionDePrueba.Errores2(error);

    public Task<MigrationLoad> RecargarAsync(MigrationLoad carga) => Db.MigrationLoads.AsNoTracking().SingleAsync(l => l.Id == carga.Id);

    public Person Persona(string documento) => Db.People.AsNoTracking().Single(p => p.TaxId == documento);
}
