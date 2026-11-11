using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Security.Users.Common;
using IngenIA365ERP.Application.Security.Users.SetUserPerson;
using IngenIA365ERP.Application.Security.Users.UpdateUser;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Security.Users;

/// <summary>
/// El vínculo del usuario con su persona desde Seguridad › Usuarios. Cartera exige la persona y mandaba a esa pantalla, que hasta el
/// 2026-11-06 no tenía el campo. La ruta toca sólo <c>PersonId</c>: <c>PUT /api/admin/users/{id}</c> reemplaza el usuario entero.
/// </summary>
public class SetUserPersonCommandHandlerTests
{
    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly ICurrentUserService _actor = Substitute.For<ICurrentUserService>();

    public SetUserPersonCommandHandlerTests() => _actor.UserName.Returns("admin@test");

    private SetUserPersonCommandHandler Handler() => new(_db, _actor);

    private User Usuario(string nombre, string? identificacion = null, int? persona = null)
    {
        var usuario = new User { Username = nombre, Email = $"{nombre}@x.test", PasswordHash = "h", IsActive = true, IdentificationNumber = identificacion, PersonId = persona };
        _db.Users.Add(usuario);
        _db.SaveChanges();
        return usuario;
    }

    private Person Persona(string documento, bool eliminada = false)
    {
        var persona = new Person { FirstName = "Ana", LastName = "Prueba", TaxId = documento, Status = "A", CreatedBy = "test", IsDeleted = eliminada };
        _db.People.Add(persona);
        _db.SaveChanges();
        return persona;
    }

    private Task<User> LeerAsync(int id) => _db.Users.AsNoTracking().SingleAsync(u => u.Id == id);

    [Fact]
    public async Task Vincula_cambia_y_quita_sin_tocar_nada_mas()
    {
        var usuario = Usuario("ana", identificacion: "1000");
        var primera = Persona("1");
        var segunda = Persona("2");

        (await Handler().Handle(new SetUserPersonCommand(usuario.PublicId, primera.PublicId), default)).IsSuccess.Should().BeTrue();
        (await LeerAsync(usuario.Id)).PersonId.Should().Be(primera.Id);

        (await Handler().Handle(new SetUserPersonCommand(usuario.PublicId, segunda.PublicId), default)).IsSuccess.Should().BeTrue();
        (await LeerAsync(usuario.Id)).PersonId.Should().Be(segunda.Id);

        (await Handler().Handle(new SetUserPersonCommand(usuario.PublicId, null), default)).IsSuccess.Should().BeTrue();
        var final = await LeerAsync(usuario.Id);
        final.PersonId.Should().BeNull();
        final.IdentificationNumber.Should().Be("1000");
        final.Email.Should().Be("ana@x.test");
    }

    [Fact]
    public async Task Una_persona_no_queda_vinculada_a_dos_usuarios()
    {
        var persona = Persona("1");
        Usuario("ana", persona: persona.Id);
        var otro = Usuario("beto");

        var r = await Handler().Handle(new SetUserPersonCommand(otro.PublicId, persona.PublicId), default);

        r.Error.Code.Should().Be(UserErrorCodes.PersonAlreadyLinked);
        r.Error.Message.Should().Contain("ana@x.test");
        (await LeerAsync(otro.Id)).PersonId.Should().BeNull();
    }

    [Fact]
    public async Task Volver_a_vincular_la_propia_no_es_un_duplicado()
    {
        var persona = Persona("1");
        var usuario = Usuario("ana", persona: persona.Id);

        (await Handler().Handle(new SetUserPersonCommand(usuario.PublicId, persona.PublicId), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task La_de_un_usuario_retirado_se_puede_vincular()
    {
        var persona = Persona("1");
        var retirado = Usuario("ana", persona: persona.Id);
        retirado.IsDeleted = true;
        await _db.SaveChangesAsync();
        var nuevo = Usuario("beto");

        (await Handler().Handle(new SetUserPersonCommand(nuevo.PublicId, persona.PublicId), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Una_persona_inexistente_o_eliminada_no_se_vincula_y_un_usuario_inexistente_tampoco()
    {
        var usuario = Usuario("ana");
        var eliminada = Persona("1", eliminada: true);

        (await Handler().Handle(new SetUserPersonCommand(usuario.PublicId, eliminada.PublicId), default)).Error.Code.Should().Be("Core.Person.NotFound");
        (await Handler().Handle(new SetUserPersonCommand(usuario.PublicId, Guid.NewGuid()), default)).Error.Code.Should().Be("Core.Person.NotFound");
        (await Handler().Handle(new SetUserPersonCommand(Guid.NewGuid(), null), default)).Error.Code.Should().Be("Generic.NotFound");
    }

    [Fact]
    public async Task El_PUT_general_tampoco_duplica_ni_borra_la_identificacion_que_no_vino()
    {
        var persona = Persona("1");
        Usuario("ana", persona: persona.Id);
        var otro = Usuario("beto", identificacion: "2000");
        var general = new UpdateUserCommandHandler(_db, _actor);

        (await general.Handle(new UpdateUserCommand(otro.PublicId, otro.Email!, null, null, persona.PublicId), default))
            .Error.Code.Should().Be(UserErrorCodes.PersonAlreadyLinked);
        (await general.Handle(new UpdateUserCommand(otro.PublicId, otro.Email!, null, persona.Id), default))
            .Error.Code.Should().Be(UserErrorCodes.PersonAlreadyLinked);

        (await general.Handle(new UpdateUserCommand(otro.PublicId, otro.Email!, null, null), default)).IsSuccess.Should().BeTrue();
        (await LeerAsync(otro.Id)).IdentificationNumber.Should().Be("2000");
    }
}
