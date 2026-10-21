using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Security.Users.UpdateUser;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Security;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Security.Users;

/// <summary>
/// Feature 013 (T0267, F5): la persona del maestro de un usuario se asocia por su <c>PublicId</c>. Cartera exige
/// <c>SEC_Users.PersonId</c> a quien radica, decide, vota o desembolsa, y hasta el 2026-10-10 sólo se podía escribir con el entero
/// interno de la persona, que la API no publica. (nuevo)
/// </summary>
public class UpdateUserCommandHandlerTests
{
    private static (UpdateUserCommandHandler Handler, TestApplicationDbContext Db) Build()
    {
        var db = TestDbContextFactory.Create();
        var cu = Substitute.For<ICurrentUserService>();
        cu.UserName.Returns("admin@test");
        return (new UpdateUserCommandHandler(db, cu), db);
    }

    private static (User Usuario, Person Persona) Sembrar(TestApplicationDbContext db, bool personaEliminada = false)
    {
        var usuario = new User { Username = "ana", Email = "ana@x.test", PasswordHash = "h", IsActive = true };
        var persona = new Person { FirstName = "Ana", LastName = "Prueba", TaxId = "1000000010", Status = "A", CreatedBy = "test", IsDeleted = personaEliminada };
        db.Users.Add(usuario);
        db.People.Add(persona);
        db.SaveChanges();
        return (usuario, persona);
    }

    [Fact]
    public async Task Asocia_la_persona_por_su_PublicId()
    {
        var (handler, db) = Build();
        var (usuario, persona) = Sembrar(db);

        var r = await handler.Handle(new UpdateUserCommand(usuario.PublicId, usuario.Email!, null, null, persona.PublicId), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        (await db.Users.AsNoTracking().SingleAsync(u => u.Id == usuario.Id)).PersonId.Should().Be(persona.Id);
    }

    [Fact]
    public async Task El_PublicId_manda_sobre_el_entero()
    {
        var (handler, db) = Build();
        var (usuario, persona) = Sembrar(db);

        var r = await handler.Handle(new UpdateUserCommand(usuario.PublicId, usuario.Email!, null, 9_999, persona.PublicId), default);

        r.IsSuccess.Should().BeTrue();
        (await db.Users.AsNoTracking().SingleAsync(u => u.Id == usuario.Id)).PersonId.Should().Be(persona.Id);
    }

    [Fact]
    public async Task Una_persona_inexistente_o_eliminada_responde_Core_Person_NotFound_y_no_cambia_nada()
    {
        var (handler, db) = Build();
        var (usuario, eliminada) = Sembrar(db, personaEliminada: true);

        foreach (var persona in new[] { Guid.NewGuid(), eliminada.PublicId })
        {
            var r = await handler.Handle(new UpdateUserCommand(usuario.PublicId, usuario.Email!, null, null, persona), default);
            r.IsFailure.Should().BeTrue();
            r.Error.Code.Should().Be("Core.Person.NotFound");
        }
        (await db.Users.AsNoTracking().SingleAsync(u => u.Id == usuario.Id)).PersonId.Should().BeNull();
    }

    [Fact]
    public async Task Sin_PublicId_se_comporta_como_antes()
    {
        var (handler, db) = Build();
        var (usuario, persona) = Sembrar(db);

        (await handler.Handle(new UpdateUserCommand(usuario.PublicId, usuario.Email!, null, persona.Id), default)).IsSuccess.Should().BeTrue();
        (await db.Users.AsNoTracking().SingleAsync(u => u.Id == usuario.Id)).PersonId.Should().Be(persona.Id);

        (await handler.Handle(new UpdateUserCommand(usuario.PublicId, usuario.Email!, null, null), default)).IsSuccess.Should().BeTrue();
        (await db.Users.AsNoTracking().SingleAsync(u => u.Id == usuario.Id)).PersonId.Should().BeNull();
    }
}
