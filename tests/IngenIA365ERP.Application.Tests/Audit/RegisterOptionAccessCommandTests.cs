using FluentAssertions;
using IngenIA365ERP.Application.Audit.RegisterOptionAccess;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Audit;

/// <summary>
/// T088 — US7 (FR-051): el ingreso a una opción queda en la auditoría con módulo <c>Navigation</c>,
/// ruta y título. Feature 012 (T062): lo registra el handler, encadenado si hay cooperativa.
/// </summary>
public class RegisterOptionAccessCommandTests
{
    private readonly RegisterOptionAccessCommandValidator _validador = new();

    [Theory]
    [InlineData("/contabilidad/plan-de-cuentas", "Contabilidad › Plan de cuentas", true)]
    [InlineData("contabilidad/plan-de-cuentas", "Sin barra", false)]
    [InlineData("/nomina", "", false)]
    [InlineData("", "Vacía", false)]
    public void La_ruta_empieza_con_barra_y_el_titulo_no_va_vacio(string ruta, string titulo, bool valido)
    {
        _validador.Validate(new RegisterOptionAccessCommand(ruta, titulo)).IsValid.Should().Be(valido);
    }

    [Fact]
    public async Task Sin_cooperativa_el_evento_va_a_Mongo_en_el_modulo_Navigation_con_ruta_y_titulo()
    {
        var auditoria = Substitute.For<IAuditService>();
        AuditLogCommand? registrado = null;
        auditoria.LogAsync(Arg.Do<AuditLogCommand>(c => registrado = c), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var servicios = new ServiceCollection().AddSingleton(auditoria).BuildServiceProvider();
        var behavior = new AuditBehavior<RegisterOptionAccessCommand, Result>(auditoria, NominaTestData.UsuarioDePrueba("ana@demo", 7),
            NullLogger<AuditBehavior<RegisterOptionAccessCommand, Result>>.Instance, servicios);
        var comando = new RegisterOptionAccessCommand("/contabilidad/comprobantes", "Contabilidad › Comprobantes");

        var r = await behavior.Handle(comando, _ => new RegisterOptionAccessCommandHandler(servicios).Handle(comando, CancellationToken.None), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        await auditoria.Received(1).LogAsync(Arg.Any<AuditLogCommand>(), Arg.Any<CancellationToken>());
        registrado!.Module.Should().Be("Navigation");
        registrado.Action.Should().Be("RegisterOptionAccess");
        registrado.NewValues.Should().BeSameAs(comando, "ruta y título viajan en newValuesJson para la consola");
    }

    [Fact]
    public async Task Con_cooperativa_el_handler_inserta_su_propia_fila_encadenada_y_AuditBehavior_no_repite()
    {
        // Feature 012 (T37, T38; T062): Navigation es un módulo encadenado.
        using var db = TestDbContextFactory.Create();
        var auditoria = Substitute.For<IAuditService>();
        var cooperativa = Substitute.For<ICurrentTenantService>();
        var tenant = Guid.NewGuid();
        cooperativa.TenantId.Returns(tenant.ToString("N"));
        var servicios = new ServiceCollection()
            .AddSingleton(auditoria)
            .AddSingleton<IApplicationDbContext>(db)
            .AddSingleton(cooperativa)
            .AddSingleton(NominaTestData.UsuarioDePrueba("ana@demo", 7))
            .BuildServiceProvider();
        var behavior = new AuditBehavior<RegisterOptionAccessCommand, Result>(auditoria, NominaTestData.UsuarioDePrueba("ana@demo", 7),
            NullLogger<AuditBehavior<RegisterOptionAccessCommand, Result>>.Instance, servicios);
        var comando = new RegisterOptionAccessCommand("/inventario/kardex", "Inventario › Kardex");

        var r = await behavior.Handle(comando, _ => new RegisterOptionAccessCommandHandler(servicios).Handle(comando, CancellationToken.None), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        await auditoria.DidNotReceive().LogAsync(Arg.Any<AuditLogCommand>(), Arg.Any<CancellationToken>());
        var fila = db.AuditOutbox.Should().ContainSingle().Subject;
        fila.Module.Should().Be("Navigation");
        fila.Stream.Should().Be($"{tenant:N}:10y");
        var evento = AuditoriaEncadenada.LeerCarga(fila.PayloadJson!);
        evento.Action.Should().Be("RegisterOptionAccess");
        evento.NewValuesJson.Should().Contain("/inventario/kardex");
    }
}
