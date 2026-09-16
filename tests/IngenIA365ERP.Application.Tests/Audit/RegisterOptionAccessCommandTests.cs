using FluentAssertions;
using IngenIA365ERP.Application.Audit.RegisterOptionAccess;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Audit;

/// <summary>
/// T088 — US7 (FR-051): el ingreso a una opción es un comando sin efecto en SQL cuyo único rastro
/// es el evento que deja <c>AuditBehavior</c>, con módulo <c>Navigation</c>, ruta y título.
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
    public async Task El_comando_no_hace_nada_y_el_evento_queda_en_el_modulo_Navigation_con_ruta_y_titulo()
    {
        var auditoria = Substitute.For<IAuditService>();
        AuditLogCommand? registrado = null;
        auditoria.LogAsync(Arg.Do<AuditLogCommand>(c => registrado = c), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var behavior = new AuditBehavior<RegisterOptionAccessCommand, Result>(auditoria, NominaTestData.UsuarioDePrueba("ana@demo", 7), NullLogger<AuditBehavior<RegisterOptionAccessCommand, Result>>.Instance);
        var comando = new RegisterOptionAccessCommand("/contabilidad/comprobantes", "Contabilidad › Comprobantes");

        var r = await behavior.Handle(comando, _ => new RegisterOptionAccessCommandHandler().Handle(comando, CancellationToken.None), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        registrado.Should().NotBeNull();
        registrado!.Module.Should().Be("Navigation");
        registrado.Action.Should().Be("RegisterOptionAccess");
        registrado.NewValues.Should().BeSameAs(comando, "ruta y título viajan en newValuesJson para la consola");
        registrado.HttpStatusCode.Should().Be(200);
    }
}
