using System.Text;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Imports;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Lending.Configuration;
using IngenIA365ERP.Application.Tests.Lending.Sessions;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using C = IngenIA365ERP.Application.Lending.Imports.DefinicionesDeMigracion.Columnas;
using P = IngenIA365ERP.Application.Lending.Imports.DefinicionesDeMigracion.Partes;

namespace IngenIA365ERP.Application.Tests.Lending.Migration;

/// <summary>
/// El escenario de las pruebas de la carga de migración (feature 013, US8, bloque 13A): InMemory con reloj fijo (hoy = 2026-12-01, corte = 2026-11-30),
/// la línea <c>CONLIB</c> de consumo con versión vigente desde el 2026-11-01 y la usura de consumo (25 % EA) desde esa fecha, la sucursal
/// <c>NORTE</c> vinculada a una oficina y <c>SUR</c> sin vincular, personas vivas (Ana, Beto, Carla) y una dada de baja, la jefa de cartera
/// (con persona) que carga, una carga en borrador con el corte, el lector de archivos doble (la tabla que la prueba arma) y Contabilidad doble
/// (iniciada, el mes abierto y un saldo contable por combinación que la prueba decide). (nuevo)
/// </summary>
internal sealed class MigracionDePrueba : IDisposable
{
    public static readonly DateOnly Hoy = GobiernoDePrueba.Hoy;
    public static readonly DateOnly Corte = Hoy.AddDays(-1);

    private MigracionDePrueba()
    {
        Lector.LeerHojaAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Result.Success(Tabla)));
        Contabilidad.EstadoDelPeriodoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result.Success(ContabilidadIniciada
                ? new EstadoDelPeriodoDto(true, EstadoDePeriodoContable.Open, ci.Arg<DateOnly>())
                : new EstadoDelPeriodoDto(false, EstadoDePeriodoContable.NotFound, null))));
        Contabilidad.SaldosDeCuentasMapeadasAsync(Arg.Any<DateOnly>(), Arg.Any<IReadOnlyList<CombinacionDeSaldo>>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var combinaciones = ci.Arg<IReadOnlyList<CombinacionDeSaldo>>();
                Pedidas.Add(combinaciones);
                var sets = combinaciones.Select((c, i) => (c, i)).Where(x => !SinRegla(x.c))
                    .GroupBy(x => (x.c.Role, x.c.ConceptCode))
                    .Select(g => new ConjuntoDeCuentasDeCarteraDto(g.Key.Role, g.Select(x => x.i).ToList(),
                        [new CuentaDelConjuntoDto(Cuenta(g.Key.Role), $"Cuenta de {g.Key.Role}", g.Key.Role,
                            [new SaldoPorSucursalDto(g.First().c.BranchPublicId, SaldoContable(g.Key.Role, g.Key.ConceptCode)
                                * IngenIA365ERP.Application.Lending.Integration.SaldosContablesDeCartera.Signo(g.Key.Role))])],
                        0m))
                    .ToList();
                var sinRegla = combinaciones.Select((c, i) => (c, i)).Where(x => SinRegla(x.c)).Select(x => x.i).ToList();
                return Task.FromResult(Result.Success(new SaldosMapeadosDeCarteraDto(sets, sinRegla)));
            });
        G.Mongo.LogAsync(Arg.Do<AuditLogCommand>(Eventos.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
    }

    public LineasDePrueba L { get; } = new();
    public GobiernoDePrueba G => L.G;
    public TestApplicationDbContext Db => L.Db;

    public ITabularFileReader Lector { get; } = Substitute.For<ITabularFileReader>();
    public TablaLeida Tabla { get; set; } = new([], [], "xlsx");
    public IContabilidadParaCartera Contabilidad { get; } = Substitute.For<IContabilidadParaCartera>();
    public bool ContabilidadIniciada { get; set; } = true;
    public List<IReadOnlyList<CombinacionDeSaldo>> Pedidas { get; } = [];
    public Func<string, string?, decimal> SaldoContable { get; set; } = (_, _) => 0m;
    public Func<CombinacionDeSaldo, bool> SinRegla { get; set; } = _ => false;
    public List<AuditLogCommand> Eventos { get; } = [];

    public Person Ana { get; private set; } = null!;
    public Person Beto { get; private set; } = null!;
    public Person Carla { get; private set; } = null!;
    public Person Dada { get; private set; } = null!;
    public User Jefa { get; private set; } = null!;
    public User Contadora { get; private set; } = null!;
    public Branch Norte { get; private set; } = null!;
    public Branch Sur { get; private set; } = null!;
    public int LineaId { get; private set; }
    public MigrationLoad Carga { get; private set; } = null!;

    public static async Task<MigracionDePrueba> CrearAsync()
    {
        var m = new MigracionDePrueba();
        m.Ana = m.G.Persona("Ana", "Deudora", "52000001");
        m.Beto = m.G.Persona("Beto", "Codeudor", "79000002");
        m.Carla = m.G.Persona("Carla", "Cliente", "41000003");
        m.Dada = m.G.Persona("Dora", "Retirada", "30000009");
        m.Jefa = m.G.Usuario(m.G.Persona("Jefa", "Cartera", "1001001"), "jefa@coop.test");
        m.Contadora = m.G.Usuario(m.G.Persona("Rafaela", "Contadora", "1001002"), "contadora@coop.test");
        m.Norte = new Branch { Name = "Oficina Norte", LegacyCode = "NORTE", TenantBranchPublicId = Guid.NewGuid(), CreatedBy = "test" };
        m.Sur = new Branch { Name = "Oficina Sur", LegacyCode = "SUR", CreatedBy = "test" };
        m.Db.Branches.AddRange(m.Norte, m.Sur);
        if (!m.Db.LendingSetups.Any()) m.Db.LendingSetups.Add(new LendingSetup { Id = LendingSetup.IdDeLaFilaUnica, CreatedBy = "SYSTEM" });
        m.Db.SaveChanges();

        // La línea con versión vigente al corte: se crea con «hoy» = 2026-11-01.
        var desde = new DateOnly(2026, 11, 1);
        m.G.HoyLocal = desde;
        await m.L.NormativaAsync(conUsura: false);
        await m.L.UsuraAsync(LineasDePrueba.ClaveConsumo, 0.25m, desde);
        var linea = await m.L.LineaAsync("CONLIB", LineasDePrueba.Version(desde: desde));
        m.G.HoyLocal = Hoy;
        m.LineaId = m.Db.CreditLines.Single(l => l.PublicId == linea.PublicId).Id;

        m.Carga = new MigrationLoad { Number = 1, CutoffDate = Corte, Status = MigrationLoadStatus.Draft, CreatedByUserId = m.Jefa.Id };
        m.Db.MigrationLoads.Add(m.Carga);
        m.Db.SaveChanges();
        m.Db.ChangeTracker.Clear();
        m.G.ActuarComo(m.Jefa);
        m.G.Permisos.EsMaestroGlobal.Returns(true);
        return m;
    }

    public void Dispose() => L.Dispose();

    private static string Cuenta(string rol) => rol switch
    {
        "Capital" => "14410501",
        "InteresPorCobrar" => "14430501",
        "DeterioroIndividual" => "14450501",
        "DeterioroGeneral" => "14680501",
        _ => "19999999",
    };

    // ---------------------------------------------------------------------------------------------------------- archivos --

    /// <summary>Una tabla con todos los encabezados de la parte y una fila por diccionario (columna → valor; las demás vacías).</summary>
    public void Archivo(string parte, params IReadOnlyDictionary<string, string?>[] filas)
    {
        var encabezados = DefinicionesDeMigracion.Buscar(parte)!.Definicion.Hojas[0].Columnas.Select(c => c.Nombre).ToList();
        Tabla = new TablaLeida(encabezados,
            filas.Select((f, i) => new FilaLeida(i + 2, encabezados.Select(e => f.TryGetValue(e, out var v) ? v : null).ToArray())).ToList(), "xlsx");
    }

    public static Dictionary<string, string?> Fila(params (string Columna, string? Valor)[] celdas) =>
        celdas.ToDictionary(c => c.Columna, c => c.Valor);

    public static Dictionary<string, string?> Credito(string numero, string deudor = "52000001", string monto = "1000000", string? factura = null,
        string oficina = "NORTE", string linea = "CONLIB", string? tasa = "22,5") => Fila(
        (C.Numero, numero), (C.Deudor, deudor), (C.Oficina, oficina), (C.Linea, linea), (C.Factura, factura), (C.FechaDeDesembolso, "2026-05-15"),
        (C.MontoDesembolsado, monto), (C.TipoDeTasa, "Fixed"), (C.TasaCorrienteEA, tasa), (C.TipoDeTasaDeMora, "RelativeToCurrent"),
        (C.FactorDeMora, "1,5"), (C.Periodicidad, "Monthly"), (C.Plazo, "12"), (C.FormaDePago, "Cashier"));

    public static Dictionary<string, string?> Cuota(string numero, int cuota, string vence, string capital, string? interes = null, string? mora = null) =>
        Fila((C.NumeroDeCredito, numero), (C.Cuota, cuota.ToString(System.Globalization.CultureInfo.InvariantCulture)), (C.FechaDeVencimiento, vence),
            (C.Capital, capital), (C.InteresCorriente, interes), (C.InteresDeMora, mora));

    public static Dictionary<string, string?> Saldo(string numero, string concepto, string valor) =>
        Fila((C.NumeroDeCredito, numero), (C.Concepto, concepto), (C.Valor, valor));

    public static Dictionary<string, string?> Riesgo(string numero, string dias = "0", string categoria = "A", string deterioro = "0", string? general = null) =>
        Fila((C.NumeroDeCredito, numero), (C.DiasDeMora, dias), (C.Calificacion, categoria), (C.DeterioroCapital, deterioro), (C.DeterioroGeneral, general));

    // ------------------------------------------------------------------------------------------------------------- piezas --

    public EjecutorDeImportacion Ejecutor() =>
        new(Db, Lector, G.Permisos, new ServiceCollection().AddSingleton(G.Mongo).AddSingleton<Application.Common.Interfaces.IApplicationDbContext>(Db).BuildServiceProvider());

    public ImportMigrationPartCommandHandler Importador() => new(Db, Ejecutor(), G.Intervenciones(), G.Alcance, G.Actor, G.Reloj, L.Reglas());

    public async Task<Result<ImportResultDto>> ImportarAsync(string parte, ModoDeImportacion modo = ModoDeImportacion.Apply, bool reemplazar = false,
        string archivo = "parte.xlsx", string? contenido = null)
    {
        var bytes = Encoding.UTF8.GetBytes(contenido ?? $"{parte}:{Guid.NewGuid()}");
        var r = await Importador().Handle(new ImportMigrationPartCommand(Carga.PublicId, parte, modo, new ArchivoDeImportacion(archivo, bytes), reemplazar,
            "Carga de prueba") { OperationKey = Guid.NewGuid() }, default);
        Db.ChangeTracker.Clear();
        return r;
    }

    /// <summary>Importa y falla la prueba si no pudo.</summary>
    public async Task<ImportResultDto> CargarAsync(string parte, params IReadOnlyDictionary<string, string?>[] filas)
    {
        Archivo(parte, filas);
        var r = await ImportarAsync(parte);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message} {Errores(r).FirstOrDefault()?.Message}" : string.Empty);
        return r.Value;
    }

    /// <summary>Un crédito válido completo: cuotas 600.000 + 400.000 (la primera vencida con 10.000 de interés), capital 1.000.000, riesgo A.</summary>
    public async Task CreditoCompletoAsync(string numero = "CR-1", string deudor = "52000001", string? factura = null)
    {
        await CargarAsync(P.Loans, Credito(numero, deudor, factura: factura));
        await CargarAsync(P.Installments, Cuota(numero, 1, "2026-12-15", "600000", "10000"), Cuota(numero, 2, "2027-01-15", "400000"));
        await CargarAsync(P.Balances, Saldo(numero, "Principal", "1000000"), Saldo(numero, "CurrentInterest", "10000"));
        await CargarAsync(P.RiskData, Riesgo(numero));
    }

    public static IReadOnlyList<ErrorDeFila> Errores(Result<ImportResultDto> r) =>
        r.IsSuccess ? r.Value.Errors : r.Error.Should().BeOfType<ErrorConDatos>().Subject.Data.Should().BeOfType<ImportResultDto>().Subject.Errors;

    /// <summary>Un error entre partes leído del <c>data.errors[]</c> de <c>Lending.Migration.CrossPartErrors</c>.</summary>
    public sealed record ErrorLeido(string Part, int? Batch, int Row, string Column, string Code, string Message);

    public static IReadOnlyList<ErrorLeido> Errores2(Error error) =>
        GobiernoDePrueba.Datos(error).GetProperty("errors").EnumerateArray().Select(e => new ErrorLeido(
            e.GetProperty("part").GetString()!, e.GetProperty("batch").ValueKind == System.Text.Json.JsonValueKind.Null ? null : e.GetProperty("batch").GetInt32(),
            e.GetProperty("row").GetInt32(), e.GetProperty("column").GetString()!, e.GetProperty("code").GetString()!, e.GetProperty("message").GetString()!)).ToList();

    public ContabilidadParaCarteraProvisional ContabilidadDeEmision() => new(Db);

    public EmisionDeCartera Emision()
    {
        var contabilidad = ContabilidadDeEmision();
        return new EmisionDeCartera(Db, new EmisorDeMensajes(Db, G.Actor, G.Reloj), new FechaContable(contabilidad), new ModoDePasoDeCartera(Db, contabilidad), G.Actor);
    }

    public CuadreDeMigracion Cuadre() => new(Db, Contabilidad);

    public ConciliacionDeVentasDeMigracion Conciliacion() => new(Db, new ConciliacionDeVentasDeLaCarga(Db, new MensajesEntrantes(Db)));

    public ApplyMigrationLoadCommandHandler Aplicador(Application.Lending.Migration.RevalidacionDeCuposMigrados? revalidacion = null) =>
        new(Db, Emision(), G.Parametros(), G.Actor, G.Reloj, Cuadre(), Conciliacion(), null, null, revalidacion);

    public async Task<Result<AppliedMigrationLoadDto>> AplicarAsync(MigrationLoad? carga = null)
    {
        var r = await Aplicador().Handle(new ApplyMigrationLoadCommand((carga ?? Carga).PublicId, "Carga de ensayo") { OperationKey = Guid.NewGuid() }, default);
        Db.ChangeTracker.Clear();
        return r;
    }

    // ----------------------------------------------------------------------------------------------------------- actas --

    /// <summary>Un punto de sesión del sujeto <c>ActivationDifferences</c> de la carga, con lo que la prueba quiera cambiar.</summary>
    public Guid Acta(Guid sujeto, SessionStatus estado = SessionStatus.Closed, CreditDecisionOutcome? resultado = CreditDecisionOutcome.Approve,
        string tipoDeSujeto = LendingDecisionSubjects.ActivationDifferences)
    {
        var organo = G.Organo("CONSEJO" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant(), "Consejo de administración", GoverningBodyKind.BoardOfDirectors);
        var sesion = new Session { GoverningBodyId = organo.Id, Kind = SessionKind.Extraordinary, Mode = SessionMode.InPerson, HeldOn = Corte, Status = estado, CreatedBy = "test" };
        Db.LendingSessions.Add(sesion);
        Db.SaveChanges();
        var punto = new SessionItem
        {
            SessionId = sesion.Id, Order = 1, Kind = SessionItemKind.Decision, Rule = VotingRule.Majority, SubjectType = tipoDeSujeto, SubjectPublicId = sujeto,
            Title = "Diferencias de la migración", Outcome = resultado, CreatedBy = "test",
        };
        Db.SessionItems.Add(punto);
        Db.SaveChanges();
        Db.ChangeTracker.Clear();
        return punto.PublicId;
    }

    public AcceptMigrationDifferencesCommandHandler Aceptador() => new(Db, new PuntoDeActaCerrado(Db), G.Intervenciones(), G.Actor, G.Reloj);

    public MarkMigrationSaleCommandHandler Marcador() => new(Db, G.Actor, G.Reloj);
}
