using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Common.Security;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Receipts;

/// <summary>
/// El escenario común de las pruebas de Recaudo (feature 015, fases 3 a 6): dos oficinas (A y B), personas y usuarios (cajera, supervisor, otro cajero,
/// un usuario que es la misma persona que la cajera), el reloj en Colombia (1 de diciembre de 2026), el lector real de parámetros sobre la base de prueba
/// (las vigencias se agregan con <see cref="Parametro"/>), el alcance fijo (<see cref="Alcance"/>, total por defecto) y las alertas y la auditoría simuladas.
/// </summary>
internal sealed class EscenarioDeRecaudo
{
    public static readonly DateOnly Hoy = new(2026, 12, 1);

    public TestApplicationDbContext Db { get; } = TestDbContextFactory.Create();
    public IActorActual Actor { get; } = Substitute.For<IActorActual>();
    public IDateTimeService Reloj { get; } = Substitute.For<IDateTimeService>();
    public IAlertas Alertas { get; } = Substitute.For<IAlertas>();
    public IAuditService Auditoria { get; } = Substitute.For<IAuditService>();
    public AlcanceFijo Alcance { get; } = new(AlcanceDeRecaudo.Todo);
    /// <summary>Un lector nuevo en cada uso: el real memoriza por petición y una vigencia agregada después no se vería.</summary>
    public ILectorDeParametros Lector => new LectorDeParametros(Db);

    public ParametrosDeCaja Parametros => new(Lector);
    public PersonasDeLosUsuarios Personas { get; }
    public SegregacionDeRecaudo Segregacion { get; }
    public IServiceProvider Servicios { get; }

    public Branch OficinaA { get; }
    public Branch OficinaB { get; }
    public Person PersonaCajera { get; }
    public Person PersonaSupervisor { get; }
    public Person PersonaOtroCajero { get; }
    public User Cajera { get; }
    public User Supervisor { get; }
    public User OtroCajero { get; }

    /// <summary>Otro usuario de la misma persona que la cajera: la segregación lo trata como ella.</summary>
    public User CajeraBis { get; }

    /// <summary>Un usuario sin persona del maestro.</summary>
    public User SinPersona { get; }

    public EscenarioDeRecaudo()
    {
        Reloj.UtcNow.Returns(new DateTime(2026, 12, 1, 15, 0, 0, DateTimeKind.Utc));
        Reloj.HoyLocal.Returns(Hoy);
        Alertas.LevantarAsync(Arg.Any<AlertaALevantar>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AlertaLevantada(Guid.NewGuid(), DesenlaceDeAlerta.Levantada, 1, false)));
        Servicios = new ServiceCollection().AddSingleton(Auditoria).BuildServiceProvider();
        Personas = new PersonasDeLosUsuarios(Db);
        Segregacion = new SegregacionDeRecaudo(Personas);

        OficinaA = new Branch { Name = "Oficina A", CreatedBy = "test" };
        OficinaB = new Branch { Name = "Oficina B", CreatedBy = "test" };
        PersonaCajera = new Person { FirstName = "Carla", LastName = "Caja", TaxId = "1001" };
        PersonaSupervisor = new Person { FirstName = "Sergio", LastName = "Supervisa", TaxId = "1002" };
        PersonaOtroCajero = new Person { FirstName = "Óscar", LastName = "Otro", TaxId = "1003" };
        Db.Branches.AddRange(OficinaA, OficinaB);
        Db.People.AddRange(PersonaCajera, PersonaSupervisor, PersonaOtroCajero);
        Db.SaveChanges();

        Cajera = new User { Username = "carla", PasswordHash = "x", PersonId = PersonaCajera.Id, IsActive = true };
        Supervisor = new User { Username = "sergio", PasswordHash = "x", PersonId = PersonaSupervisor.Id, IsActive = true };
        OtroCajero = new User { Username = "oscar", PasswordHash = "x", PersonId = PersonaOtroCajero.Id, IsActive = true };
        CajeraBis = new User { Username = "carla.bis", PasswordHash = "x", PersonId = PersonaCajera.Id, IsActive = true };
        SinPersona = new User { Username = "sinpersona", PasswordHash = "x", IsActive = true };
        Db.Users.AddRange(Cajera, Supervisor, OtroCajero, CajeraBis, SinPersona);
        Db.SaveChanges();
        Como(Cajera);
    }

    /// <summary>Quien actúa es <paramref name="usuario"/>.</summary>
    public void Como(User usuario) => Actor.ObtenerAsync(Arg.Any<CancellationToken>())
        .Returns(new Actor(ActorKind.Person, usuario.Id, usuario.PublicId, Guid.NewGuid(), usuario.Username, null, ExecutionChannel.Web, "pruebas", null, null));

    /// <summary>Una vigencia de un parámetro <c>RCD</c> desde hace un año.</summary>
    public void Parametro(string clave, string valor, string modulo = ParametrosDeRecaudoYTesoreria.ModuloRecaudo)
    {
        Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = modulo, Key = clave, ScopeKind = ParameterScopeKind.None, Value = valor, ValidFrom = Hoy.AddYears(-1), Reason = "prueba",
        });
        Db.SaveChanges();
    }

    public CollectionPoint Punto(string codigo, Branch? oficina = null)
    {
        var punto = new CollectionPoint { BranchId = (oficina ?? OficinaA).Id, Code = codigo, Name = $"Punto {codigo}", ValidFrom = Hoy.AddMonths(-1) };
        Db.CollectionPoints.Add(punto);
        Db.SaveChanges();
        return punto;
    }

    public CashBox Caja(CollectionPoint punto, string codigo, decimal baseDeCaja = 500_000, decimal? aviso = null, decimal? bloqueo = null)
    {
        var caja = new CashBox
        {
            CollectionPointId = punto.Id, Code = codigo, Name = $"Caja {codigo}", BaseAmount = baseDeCaja, CashWarnCap = aviso, CashBlockCap = bloqueo,
            ValidFrom = Hoy.AddMonths(-1),
        };
        Db.CashBoxes.Add(caja);
        Db.SaveChanges();
        return caja;
    }

    public PaymentMeans Medio(string codigo, PaymentMeansClass clase, decimal tolerancia = 1_000, CashCountMethod? arqueo = null, bool activo = true)
    {
        var medio = new PaymentMeans
        {
            Code = codigo, Name = codigo, Class = clase, DianPaymentMeansCode = "10", IsActive = activo, ToleranceAmount = tolerancia,
            AllowsChange = clase == PaymentMeansClass.Cash,
            CountMethod = arqueo ?? (clase == PaymentMeansClass.Cash ? CashCountMethod.PhysicalCount : CashCountMethod.ByReference),
            ValidFrom = Hoy.AddYears(-1),
        };
        Db.PaymentMeans.Add(medio);
        Db.SaveChanges();
        return medio;
    }

    public void Ofrecer(CollectionPoint punto, params PaymentMeans[] medios)
    {
        foreach (var m in medios)
            Db.PaymentMeansCollectionPoints.Add(new PaymentMeansCollectionPoint { CollectionPointId = punto.Id, PaymentMeansId = m.Id, ValidFrom = Hoy.AddMonths(-1) });
        Db.SaveChanges();
    }

    public void AsignarCajero(CashBox caja, User usuario)
    {
        Db.CashBoxCashiers.Add(new CashBoxCashier { CashBoxId = caja.Id, UserId = usuario.Id, ValidFrom = Hoy.AddMonths(-1) });
        Db.SaveChanges();
    }

    public CashDenomination Denominacion(decimal valor)
    {
        var d = new CashDenomination { Value = valor, Kind = valor >= 1000 ? Domain.Enums.Inventory.CashDenominationKind.Bill : Domain.Enums.Inventory.CashDenominationKind.Coin, ValidFrom = Hoy.AddYears(-1) };
        Db.CashDenominations.Add(d);
        Db.SaveChanges();
        return d;
    }
}

/// <summary>Un alcance de Recaudo que la prueba fija. (nuevo)</summary>
internal sealed class AlcanceFijo(AlcanceDeRecaudo inicial) : IAlcanceDeRecaudo
{
    public AlcanceDeRecaudo Valor { get; set; } = inicial;

    public Task<AlcanceDeRecaudo> ObtenerAsync(CancellationToken ct = default) => Task.FromResult(Valor);

    public void SoloPuntos(params int[] puntos) =>
        Valor = new AlcanceDeRecaudo(false, new HashSet<int>(), puntos.ToHashSet(), new HashSet<int>(), new HashSet<int>());

    public void SoloOficinas(params int[] oficinas) =>
        Valor = new AlcanceDeRecaudo(false, oficinas.ToHashSet(), new HashSet<int>(), new HashSet<int>(), new HashSet<int>());
}
