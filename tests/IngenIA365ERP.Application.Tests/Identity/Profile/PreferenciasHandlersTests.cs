using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Identity.Profile.Preferencias;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Identity.Profile;

public class PreferenciasHandlersTests
{
    private static readonly Guid Usuario = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Cooperativa = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OtraCooperativa = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static ICurrentCentralUserContext Contexto(Guid? tenant = null, bool autenticado = true)
    {
        var ctx = Substitute.For<ICurrentCentralUserContext>();
        ctx.CentralUserId.Returns(autenticado ? Usuario : null);
        ctx.IsAuthenticated.Returns(autenticado);
        ctx.Purpose.Returns(CentralJwtPurposes.Full);
        ctx.ActiveTenantPublicId.Returns(tenant);
        ctx.Email.Returns("prueba@ingenia365.co");
        return ctx;
    }

    [Fact]
    public async Task Guardar_Apariencia_QuedaEnAmbitoGlobal()
    {
        // El tema no debe depender de la cooperativa: quien necesita texto
        // grande lo necesita en todas.
        await using var db = TestAdminDbContext.Create();
        var handler = new SaveMyPreferencesCommandHandler(db, Contexto(Cooperativa));

        var r = await handler.Handle(
            new SaveMyPreferencesCommand(new Dictionary<string, string?> { ["ui.tema"] = "oscuro" }),
            CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        var fila = db.UserSettings.Single();
        fila.TenantPublicId.Should().Be(Guid.Empty, "el tema es del usuario, no de la cooperativa");
        fila.SettingValue.Should().Be("oscuro");
    }

    [Fact]
    public async Task Guardar_Navegacion_QuedaAtadaALaCooperativaActiva()
    {
        await using var db = TestAdminDbContext.Create();
        var handler = new SaveMyPreferencesCommandHandler(db, Contexto(Cooperativa));

        await handler.Handle(
            new SaveMyPreferencesCommand(
                new Dictionary<string, string?> { ["ui.pagina-inicio"] = "/cartera/recaudos" }),
            CancellationToken.None);

        db.UserSettings.Single().TenantPublicId.Should().Be(Cooperativa);
    }

    [Fact]
    public async Task Guardar_Navegacion_SinCooperativaActiva_Falla()
    {
        // Si se aceptara, quedaría en el ámbito global y la página de inicio de
        // una cooperativa aparecería al entrar a todas las demás.
        await using var db = TestAdminDbContext.Create();
        var handler = new SaveMyPreferencesCommandHandler(db, Contexto(tenant: null));

        var r = await handler.Handle(
            new SaveMyPreferencesCommand(
                new Dictionary<string, string?> { ["ui.pagina-inicio"] = "/cartera/recaudos" }),
            CancellationToken.None);

        r.IsSuccess.Should().BeFalse();
        r.Error!.Code.Should().Be("Profile.Preferencias.SinCooperativaActiva");
        db.UserSettings.Should().BeEmpty();
    }

    [Fact]
    public async Task Guardar_DosVeces_ActualizaLaMismaFila()
    {
        await using var db = TestAdminDbContext.Create();
        var handler = new SaveMyPreferencesCommandHandler(db, Contexto(Cooperativa));

        await handler.Handle(new SaveMyPreferencesCommand(
            new Dictionary<string, string?> { ["ui.tema"] = "oscuro" }), CancellationToken.None);
        await handler.Handle(new SaveMyPreferencesCommand(
            new Dictionary<string, string?> { ["ui.tema"] = "claro" }), CancellationToken.None);

        db.UserSettings.Should().HaveCount(1, "es una actualización, no un historial");
        db.UserSettings.Single().SettingValue.Should().Be("claro");
    }

    [Fact]
    public async Task Guardar_UnaClave_NoPisaLasDemas()
    {
        // La barra superior guarda sólo el tema; si el cuerpo fuera el objeto
        // completo, ese guardado borraría los favoritos.
        await using var db = TestAdminDbContext.Create();
        var handler = new SaveMyPreferencesCommandHandler(db, Contexto(Cooperativa));

        await handler.Handle(new SaveMyPreferencesCommand(new Dictionary<string, string?>
        {
            ["ui.tema"] = "oscuro",
            ["ui.escala"] = "grande",
        }), CancellationToken.None);

        await handler.Handle(new SaveMyPreferencesCommand(
            new Dictionary<string, string?> { ["ui.tema"] = "claro" }), CancellationToken.None);

        db.UserSettings.Should().HaveCount(2);
        db.UserSettings.Single(s => s.SettingKey == "ui.escala").SettingValue.Should().Be("grande");
    }

    [Fact]
    public async Task Leer_NoDevuelveLoDeOtraCooperativa()
    {
        await using var db = TestAdminDbContext.Create();
        db.UserSettings.AddRange(
            new UserSetting { CentralUserId = Usuario, TenantPublicId = Guid.Empty,        SettingKey = "ui.tema",           SettingValue = "oscuro" },
            new UserSetting { CentralUserId = Usuario, TenantPublicId = Cooperativa,       SettingKey = "ui.pagina-inicio",  SettingValue = "/cartera/recaudos" },
            new UserSetting { CentralUserId = Usuario, TenantPublicId = OtraCooperativa,   SettingKey = "ui.pagina-inicio",  SettingValue = "/contabilidad/saldos" });
        await db.SaveChangesAsync();

        var handler = new GetMyPreferencesQueryHandler(
            db, Contexto(Cooperativa), NullLogger<GetMyPreferencesQueryHandler>.Instance);

        var r = await handler.Handle(new GetMyPreferencesQuery(), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        r.Value!.Tema.Should().Be("oscuro", "la apariencia es global");
        r.Value.PaginaInicio.Should().Be("/cartera/recaudos", "no debe filtrarse la de otra cooperativa");
    }

    [Fact]
    public async Task Leer_FavoritosCorruptos_DevuelveListaVaciaYNoRompe()
    {
        // Una fila mal formada no puede impedir que el usuario entre.
        await using var db = TestAdminDbContext.Create();
        db.UserSettings.Add(new UserSetting
        {
            CentralUserId = Usuario,
            TenantPublicId = Cooperativa,
            SettingKey = "ui.favoritos",
            SettingValue = "{esto no es json",
        });
        await db.SaveChangesAsync();

        var handler = new GetMyPreferencesQueryHandler(
            db, Contexto(Cooperativa), NullLogger<GetMyPreferencesQueryHandler>.Instance);

        var r = await handler.Handle(new GetMyPreferencesQuery(), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        r.Value!.Favoritos.Should().BeEmpty();
    }

    [Fact]
    public async Task SinAutenticar_NoSeGuardaNada()
    {
        await using var db = TestAdminDbContext.Create();
        var handler = new SaveMyPreferencesCommandHandler(db, Contexto(Cooperativa, autenticado: false));

        var r = await handler.Handle(new SaveMyPreferencesCommand(
            new Dictionary<string, string?> { ["ui.tema"] = "oscuro" }), CancellationToken.None);

        r.IsSuccess.Should().BeFalse();
        db.UserSettings.Should().BeEmpty();
    }
}
