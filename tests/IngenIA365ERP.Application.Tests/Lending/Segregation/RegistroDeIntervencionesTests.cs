using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using static IngenIA365ERP.Domain.Lending.Segregation.AccionesDeSegregacion;

namespace IngenIA365ERP.Application.Tests.Lending.Segregation;

/// <summary>
/// Feature 013, T0245 (T66, FR-005, data-model §0.11 y §3.13; quickstart §1.5): <see cref="RegistroDeIntervenciones"/> es el único
/// escritor de <c>LND_CaseParticipations</c>. Verifica el acto con <c>ReglasDeSegregacion</c> y el parámetro
/// <c>Segregacion.Incompatibilidades</c> leído por <c>LectorDeParametros</c> a la fecha del acto; registra sin guardar (lo guarda el
/// <c>SaveChanges</c> del acto); exige la persona del usuario (<c>Lending.Security.UserWithoutPerson</c> con <c>data.howToFix</c>) y
/// compara por el <c>SEC_Users.Id</c> que resuelve <see cref="IActorActual"/>, nunca por el entero del token.
/// </summary>
public class RegistroDeIntervencionesTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 8);
    private static readonly DateTime Ahora = new(2026, 10, 8, 15, 30, 0, DateTimeKind.Utc);
    private static readonly ExpedienteDeCartera Solicitud = new("CreditApplication", Guid.Parse("44444444-4444-4444-4444-444444444444"));

    private const int Ana = 7, AnaOtroUsuario = 8, Beto = 9, SinPersona = 10;
    private const int PersonaAna = 70, PersonaBeto = 90;

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IActorActual _actor = Substitute.For<IActorActual>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();

    public RegistroDeIntervencionesTests()
    {
        _db.Users.AddRange(
            new User { Id = Ana, Username = "ana", PersonId = PersonaAna, IsActive = true },
            new User { Id = AnaOtroUsuario, Username = "ana.gerencia", PersonId = PersonaAna, IsActive = true },
            new User { Id = Beto, Username = "beto", PersonId = PersonaBeto, IsActive = true },
            new User { Id = SinPersona, Username = "tecnico", IsActive = true });
        _db.SaveChanges();
        _reloj.UtcNow.Returns(Ahora);
        Actua(Ana);
    }

    private void Actua(int? usuario) =>
        _actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(
            new Actor(ActorKind.Person, usuario, Guid.NewGuid(), Guid.NewGuid(), "Quien actúa", null, ExecutionChannel.Web, "POST /api/lending", null, null)));

    private RegistroDeIntervenciones Registro() => new(_db, _actor, new LectorDeParametros(_db), _reloj);

    private async Task<IntervencionVerificada> Hacer(string accion, int usuario)
    {
        Actua(usuario);
        var v = await Registro().VerificarAsync(Solicitud, accion, Hoy);
        v.IsSuccess.Should().BeTrue(v.IsFailure ? v.Error.Message : string.Empty);
        Registro().Registrar(v.Value, "CreditApplication", Solicitud.PublicId);
        await _db.SaveChangesAsync();
        return v.Value;
    }

    private void Incompatibilidades(string json, DateOnly desde)
    {
        _db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = ParametrosDeCartera.SegregacionIncompatibilidades, ScopeKind = ParameterScopeKind.None,
            Value = json, ValidFrom = desde, Reason = "Política de segregación",
        });
        _db.SaveChanges();
    }

    private static JsonElement Datos(Error error) => JsonSerializer.SerializeToElement(error.Should().BeOfType<ErrorConDatos>().Subject.Data);

    [Fact]
    public async Task Registra_sin_guardar_y_lo_guarda_el_SaveChanges_del_acto()
    {
        var v = await Registro().VerificarAsync(Solicitud, Radicar, Hoy);

        Registro().Registrar(v.Value, "CreditApplication", Solicitud.PublicId);

        (await _db.CaseParticipations.CountAsync()).Should().Be(0, "el registro no guarda por su cuenta");
        _db.CaseParticipations.Local.Should().ContainSingle();
        await _db.SaveChangesAsync();
        var fila = await _db.CaseParticipations.SingleAsync();
        fila.Action.Should().Be(Radicar);
        fila.UserId.Should().Be(Ana);
        fila.PersonId.Should().Be(PersonaAna);
        fila.CaseRootType.Should().Be("CreditApplication");
        fila.CaseRootPublicId.Should().Be(Solicitud.PublicId);
        fila.At.Should().Be(Ahora);
    }

    [Fact]
    public async Task Un_usuario_sin_persona_es_UserWithoutPerson_con_howToFix()
    {
        Actua(SinPersona);

        var r = await Registro().VerificarAsync(Solicitud, Decidir, Hoy);

        r.Error.Code.Should().Be("Lending.Security.UserWithoutPerson");
        Datos(r.Error).GetProperty("howToFix").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Sin_usuario_resuelto_tampoco_se_interviene()
    {
        Actua(null);

        (await Registro().VerificarAsync(Solicitud, Radicar, Hoy)).Error.Code.Should().Be("Lending.Security.UserWithoutPerson");
    }

    [Fact]
    public async Task Quien_radico_no_decide_aunque_use_otro_usuario_de_la_misma_persona()
    {
        await Hacer(Radicar, Ana);
        Actua(AnaOtroUsuario);

        var r = await Registro().VerificarAsync(Solicitud, Decidir, Hoy);

        r.Error.Code.Should().Be("Lending.Segregation.Conflict");
        var datos = Datos(r.Error);
        datos.GetProperty("action").GetString().Should().Be(Decidir);
        datos.GetProperty("previousAction").GetString().Should().Be(Radicar);
        datos.GetProperty("previousAt").GetDateTime().Should().Be(Ahora);
        Actua(Beto);
        (await Registro().VerificarAsync(Solicitud, Decidir, Hoy)).IsSuccess.Should().BeTrue("otra persona sí decide");
    }

    [Fact]
    public async Task Con_el_defecto_quien_decide_no_desembolsa()
    {
        await Hacer(Decidir, Beto);
        Actua(Beto);

        (await Registro().VerificarAsync(Solicitud, Desembolsar, Hoy)).Error.Code.Should().Be("Lending.Segregation.Conflict");
    }

    [Fact]
    public async Task Los_pares_sugeridos_del_defecto_vienen_inactivos()
    {
        await Hacer(ArmarLoteDeCastigo, Beto);
        await Hacer(Analizar, Beto);
        Actua(Beto);

        (await Registro().VerificarAsync(Solicitud, AplicarCastigo, Hoy)).IsSuccess.Should().BeTrue("F6: sugerido e inactivo");
        (await Registro().VerificarAsync(Solicitud, Decidir, Hoy)).IsSuccess.Should().BeTrue("Analizar × Decidir viene inactivo");
    }

    [Fact]
    public async Task El_parametro_se_lee_a_la_fecha_del_acto()
    {
        Incompatibilidades("[{\"a\": \"Decidir\", \"b\": \"Desembolsar\", \"activo\": false}, {\"a\": \"Analizar\", \"b\": \"Decidir\", \"activo\": true}]",
            new DateOnly(2026, 11, 1));
        await Hacer(Decidir, Beto);
        await Hacer(Analizar, Ana);
        Actua(Beto);

        (await Registro().VerificarAsync(Solicitud, Desembolsar, Hoy)).IsFailure.Should().BeTrue("hoy rige el defecto");
        (await Registro().VerificarAsync(Solicitud, Desembolsar, new DateOnly(2026, 11, 2))).IsSuccess.Should().BeTrue("en noviembre el par está inactivo");
        Actua(Ana);
        (await Registro().VerificarAsync(Solicitud, Decidir, new DateOnly(2026, 11, 2))).Error.Code.Should().Be("Lending.Segregation.Conflict");
        (await Registro().VerificarAsync(Solicitud, Decidir, Hoy)).IsSuccess.Should().BeTrue("hoy el par Analizar × Decidir no existe");
    }

    [Fact]
    public async Task Compara_por_el_usuario_que_resuelve_IActorActual_no_por_el_token()
    {
        // El entero del token (ICurrentUserService.UserId) no entra al registro: ni siquiera es una dependencia.
        typeof(RegistroDeIntervenciones).GetConstructors().Single().GetParameters()
            .Should().NotContain(p => p.ParameterType == typeof(ICurrentUserService));

        await Hacer(Radicar, Beto);

        (await _db.CaseParticipations.SingleAsync()).UserId.Should().Be(Beto);
    }

    [Fact]
    public async Task Un_par_que_nombra_una_accion_desconocida_no_tumba_la_verificacion()
    {
        Incompatibilidades("[{\"a\": \"Decidir\", \"b\": \"Firmar\", \"activo\": true}]", Hoy);
        await Hacer(Decidir, Beto);
        Actua(Beto);

        (await Registro().VerificarAsync(Solicitud, Desembolsar, Hoy)).IsSuccess.Should().BeTrue("el par válido del defecto fue reemplazado");
    }

    [Fact]
    public async Task Una_intervencion_de_otro_expediente_no_muerde()
    {
        await Hacer(Radicar, Ana);

        Actua(Ana);
        (await Registro().VerificarAsync(new ExpedienteDeCartera("CreditApplication", Guid.NewGuid()), Decidir, Hoy)).IsSuccess.Should().BeTrue();
    }
}
