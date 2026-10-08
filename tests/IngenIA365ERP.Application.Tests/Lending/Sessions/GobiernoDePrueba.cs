using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Sessions;

/// <summary>
/// El escenario común de las pruebas del gobierno de Cartera (feature 013, US17, bloque D): una cooperativa InMemory con reloj fijo (hoy
/// local = 2026-12-01), un actor que se cambia con <see cref="ActuarComo"/>, sus permisos, alcance total, los límites nacionales
/// <c>VINCULADOS_FRACCION_CALIFICADA</c> = 0,8 y <c>VINCULADOS_GRADO_PARENTESCO</c> = 2, un perfil regulatorio con actividad financiera y
/// la auditoría real de Cartera sobre el outbox. (nuevo)
/// </summary>
internal sealed class GobiernoDePrueba : IDisposable
{
    public static readonly DateOnly Hoy = new(2026, 12, 1);
    public static readonly DateTime Ahora = new(2026, 12, 1, 15, 0, 0, DateTimeKind.Utc);

    private readonly HashSet<string> _permisos = new(StringComparer.OrdinalIgnoreCase);
    private Actor _actor;

    public GobiernoDePrueba()
    {
        Reloj.HoyLocal.Returns(_ => HoyLocal);
        Reloj.UtcNow.Returns(_ => UtcNow);
        _actor = Anonimo();
        Actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(_actor));
        Permisos.ListAsync(Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult<IReadOnlyCollection<string>>(_permisos.ToList()));
        Alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(AlcanceActual));
        Limite(CatalogoDeLimitesLegales.VinculadosFraccionCalificada, 0.8m, "Ley 454 de 1998, art. 61");
        Limite(CatalogoDeLimitesLegales.VinculadosGradoParentesco, 2m, "Ley 454 de 1998, art. 61");
        ConActividadFinanciera(true);

        var tenant = Substitute.For<ICurrentTenantService>();
        tenant.TenantId.Returns(CooperativaDePrueba.PublicIdN);
        var servicios = new ServiceCollection()
            .AddSingleton(Mongo)
            .AddSingleton<IApplicationDbContext>(Db)
            .AddSingleton(tenant)
            .AddSingleton(NominaTestData.UsuarioDePrueba("secretaria@coop", 7))
            .BuildServiceProvider();
        Auditoria = new LendingAuditEmitter(servicios, NullLogger<LendingAuditEmitter>.Instance);
    }

    public TestApplicationDbContext Db { get; } = TestDbContextFactory.Create();
    public IDateTimeService Reloj { get; } = Substitute.For<IDateTimeService>();
    public IActorActual Actor { get; } = Substitute.For<IActorActual>();
    public ICurrentUserPermissions Permisos { get; } = Substitute.For<ICurrentUserPermissions>();
    public IAlcanceDeCartera Alcance { get; } = Substitute.For<IAlcanceDeCartera>();
    public IValoresNacionales Valores { get; } = Substitute.For<IValoresNacionales>();
    public IPerfilRegulatorio Perfil { get; } = Substitute.For<IPerfilRegulatorio>();
    public IAuditService Mongo { get; } = Substitute.For<IAuditService>();
    public LendingAuditEmitter Auditoria { get; }

    public DateOnly HoyLocal { get; set; } = Hoy;
    public DateTime UtcNow { get; set; } = Ahora;
    public AlcanceDeCartera AlcanceActual { get; set; } = AlcanceDeCartera.Total;

    public ILectorDeParametros Parametros() => new LectorDeParametros(Db);

    public RegistroDeIntervenciones Intervenciones() => new(Db, Actor, Parametros(), Reloj);

    public void Dispose() => Db.Dispose();

    // --------------------------------------------------------------------------------------------------------- actores --

    public Person Persona(string nombre, string apellido, string documento)
    {
        var p = new Person { FirstName = nombre, LastName = apellido, TaxId = documento, IdType = "C", CreatedBy = "test" };
        Db.People.Add(p);
        Db.SaveChanges();
        return p;
    }

    public User Usuario(Person? persona, string correo)
    {
        var u = new User
        {
            Username = correo, Email = correo, PersonId = persona?.Id, IsActive = true, CentralUserId = Guid.NewGuid(), CreatedBy = "test",
        };
        Db.Users.Add(u);
        Db.SaveChanges();
        return u;
    }

    public void ActuarComo(User usuario) =>
        _actor = new Actor(ActorKind.Person, usuario.Id, usuario.PublicId, usuario.CentralUserId, usuario.Username, usuario.Email,
            ExecutionChannel.Web, "POST /api/lending", "10.0.0.7", null);

    public void Conceder(params string[] permisos)
    {
        _permisos.Clear();
        foreach (var p in permisos) _permisos.Add(p);
    }

    private static Actor Anonimo() =>
        new(ActorKind.Person, null, null, null, "Anónimo", null, ExecutionChannel.Web, "POST /api/lending", "10.0.0.7", null);

    // ---------------------------------------------------------------------------------------------- normativa y ley --

    public void Limite(string codigo, decimal valor, string fuente) =>
        Valores.LeerAsync(CodigosDeValorNacional.Limite, codigo, Arg.Any<DateOnly>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ValorNacionalVigente(Guid.NewGuid(), Guid.NewGuid(), CodigosDeValorNacional.Limite, codigo, valor, "Fraccion",
                null, new DateOnly(2026, 1, 1), null, null, fuente, 1, Ahora.AddYears(-1), EstadoDeRevision.Validada)));

    public void ConActividadFinanciera(bool conActividad) =>
        Perfil.VigenteAsync(Arg.Any<DateOnly>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(new PerfilRegulatorioDto(Guid.NewGuid(), new DateOnly(2026, 1, 1), "CAC", conActividad, 1, null, 2, false, null, null,
                "Resolución de prueba", null, "Perfil de prueba", null, "Prueba", Ahora.AddYears(-1)));

    // ------------------------------------------------------------------------------------------------------ órganos --

    /// <summary>Un órgano con su regla vigente desde 2026-01-01 (quórum &gt; 1/2, mayoría &gt; 1/2 de presentes elegibles).</summary>
    public GoverningBody Organo(string codigo, string nombre, GoverningBodyKind clase, bool decideCredito = true, int? vinculadosN = null,
        int? vinculadosD = null)
    {
        var organo = new GoverningBody { Code = codigo, Name = nombre, Kind = clase, DecidesCredit = decideCredito, MinutesPrefix = codigo + "-", IsActive = true };
        Db.GoverningBodies.Add(organo);
        Db.SaveChanges();
        Db.GoverningBodyRuleVersions.Add(new GoverningBodyRuleVersion
        {
            GoverningBodyId = organo.Id, VersionNumber = 1, ValidFrom = new DateOnly(2026, 1, 1), RegisteredAt = Ahora.AddYears(-1),
            QuorumNumerator = 1, QuorumDenominator = 2, QuorumComparison = FractionComparison.MoreThan,
            MajorityNumerator = 1, MajorityDenominator = 2, MajorityComparison = FractionComparison.MoreThan, MajorityBase = MajorityBase.PresentEligible,
            RelatedPartyNumerator = vinculadosN, RelatedPartyDenominator = vinculadosD, Source = "Estatutos", Reason = "Semilla",
        });
        Db.SaveChanges();
        return organo;
    }

    public GoverningBodyMember Miembro(GoverningBody organo, Person persona, BodyMemberRole papel = BodyMemberRole.Member, GoverningBodyMember? suplenteDe = null)
    {
        var m = new GoverningBodyMember
        {
            GoverningBodyId = organo.Id, PersonId = persona.Id, Role = suplenteDe is null ? papel : BodyMemberRole.Alternate, HasVote = true,
            IsPrincipal = suplenteDe is null, AlternateForMemberId = suplenteDe?.Id, ValidFrom = new DateOnly(2026, 1, 1),
        };
        Db.GoverningBodyMembers.Add(m);
        Db.SaveChanges();
        return m;
    }

    // ---------------------------------------------------------------------------------------------------- aserciones --

    public static JsonElement Datos(Error error) =>
        JsonSerializer.SerializeToElement(error.Should().BeOfType<ErrorConDatos>().Subject.Data);

    /// <summary>Los eventos de auditoría encadenados que quedaron en el outbox, por acción.</summary>
    public async Task<IReadOnlyList<string>> EventosAsync() =>
        (await Db.AuditOutbox.AsNoTracking().ToListAsync()).Select(f => AuditoriaEncadenada.LeerCarga(f.PayloadJson!).Action).ToList();
}
