using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Core.People.Commands.DeletePerson;
using IngenIA365ERP.Application.Core.People.Queries;
using IngenIA365ERP.Application.Tests.Common.Integration.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Core.People;

/// <summary>
/// Feature 013, T0114 (T94, E-04; contracts/interfaces.md §7; la rama de borrado que faltaba): borrar una persona pregunta a Cartera por
/// <see cref="IConsultaDeObligaciones"/> con codeudas. Bloquea con <c>Person.HasActiveLoans</c> y el mensaje nuevo si tiene una obligación
/// viva como deudor, como codeudor o si está «migrada, sin activar»; sin nada (o sólo con obligaciones cerradas) borra como siempre.
/// <see cref="GetPersonDetailQuery"/> arma <see cref="PortfolioSummaryDto"/> desde la misma consulta, con <c>LendingStatus</c>.
/// </summary>
public class DeletePersonCommandHandlerTests
{
    private static async Task<(PersonasTestData D, ConsultaDeObligacionesDePrueba Cartera, Domain.Entities.Core.Person Persona)> EscenarioAsync()
    {
        var d = new PersonasTestData();
        var p = d.Persona();
        await d.Db.SaveChangesAsync();
        return (d, new ConsultaDeObligacionesDePrueba(), p);
    }

    private sealed record Borrado(string Codigo, string Mensaje);

    private static async Task<Borrado> BorrarAsync(PersonasTestData d, ConsultaDeObligacionesDePrueba cartera, Guid persona)
    {
        var r = await new DeletePersonCommandHandler(d.Db, d.Clock, d.User, cartera).Handle(new DeletePersonCommand(persona), CancellationToken.None);
        return r.IsFailure ? new Borrado(r.Error.Code, r.Error.Message) : new Borrado("OK", string.Empty);
    }

    [Fact]
    public async Task Sin_obligaciones_ni_Cartera_activada_borra_como_siempre()
    {
        var (d, cartera, p) = await EscenarioAsync();

        var r = await BorrarAsync(d, cartera, p.PublicId);

        r.Codigo.Should().Be("OK");
        (await d.Db.People.IgnoreQueryFilters().SingleAsync(x => x.Id == p.Id)).IsDeleted.Should().BeTrue();
        cartera.Consultas.Should().ContainSingle().Which.Should().Match<ConsultaDeObligaciones>(c =>
            c.IncludeAsCoDebtor && c.PersonPublicIds.Single() == p.PublicId);
    }

    [Theory]
    [InlineData(RolEnLaObligacion.Debtor)]
    [InlineData(RolEnLaObligacion.CoDebtor)]
    public async Task Una_obligacion_viva_como_deudor_o_codeudor_bloquea(RolEnLaObligacion rol)
    {
        var (d, cartera, p) = await EscenarioAsync();
        cartera.Agregar(p.PublicId, ConsultaDeObligacionesDePrueba.Obligacion("CR-0001", 1_000_000m, rol: rol));

        var r = await BorrarAsync(d, cartera, p.PublicId);

        r.Codigo.Should().Be(DeletePersonCommandHandler.HasActiveLoansCode);
        r.Mensaje.Should().Contain("deudor o codeudor").And.Contain("carga de Cartera por activar");
        (await d.Db.People.IgnoreQueryFilters().SingleAsync(x => x.Id == p.Id)).IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task La_persona_migrada_sin_activar_no_se_borra()
    {
        var (d, cartera, p) = await EscenarioAsync();
        cartera.MarcarMigrada(p.PublicId);

        var r = await BorrarAsync(d, cartera, p.PublicId);

        r.Codigo.Should().Be(DeletePersonCommandHandler.HasActiveLoansCode);
    }

    [Fact]
    public async Task Una_obligacion_cancelada_no_bloquea()
    {
        var (d, cartera, p) = await EscenarioAsync();
        cartera.Agregar(p.PublicId, ConsultaDeObligacionesDePrueba.Obligacion("CR-0002", 0m, estado: EstadoDeObligacion.PaidOff));

        var r = await BorrarAsync(d, cartera, p.PublicId);

        r.Codigo.Should().Be("OK");
    }

    [Fact]
    public async Task El_detalle_arma_el_resumen_desde_la_consulta_con_el_estado_de_Cartera()
    {
        var (_, cartera, p) = await EscenarioAsync();
        cartera.Agregar(p.PublicId, ConsultaDeObligacionesDePrueba.Obligacion("CR-0003", 1_000_000m, exigible: 120_000m) with { DaysPastDue = 15 });
        cartera.Agregar(p.PublicId, ConsultaDeObligacionesDePrueba.Obligacion("CR-0004", 500_000m));
        cartera.Agregar(p.PublicId, ConsultaDeObligacionesDePrueba.Obligacion("CR-0005", 900_000m, rol: RolEnLaObligacion.CoDebtor));

        // El handler arma PortfolioSummaryDto con ResumenDeCarteraDePersona (el contexto de pruebas no materializa cónyuge ni empresa).
        var resumen = await ResumenDeCarteraDePersona.ArmarAsync(cartera, p.PublicId, new DateOnly(2026, 10, 7), CancellationToken.None);

        resumen.ActiveLoans.Should().Be(2, "sólo cuenta lo que debe como deudor");
        resumen.TotalLoanBalance.Should().Be(1_500_000m);
        resumen.TotalOverdueAmount.Should().Be(120_000m);
        resumen.LendingStatus.Should().Be(PortfolioLendingStatus.Active);
        cartera.Consultas.Should().ContainSingle().Which.IncludeAsCoDebtor.Should().BeTrue();
    }

    [Fact]
    public async Task El_detalle_dice_si_Cartera_no_esta_activada_o_la_persona_esta_migrada()
    {
        var (_, cartera, p) = await EscenarioAsync();
        var hoy = new DateOnly(2026, 10, 7);
        var sinActivar = await ResumenDeCarteraDePersona.ArmarAsync(new ConsultaDeObligacionesSinActivar(), p.PublicId, hoy, CancellationToken.None);
        cartera.MarcarMigrada(p.PublicId);
        var migrada = await ResumenDeCarteraDePersona.ArmarAsync(cartera, p.PublicId, hoy, CancellationToken.None);
        cartera.Caida = new TimeoutException();
        var caida = await ResumenDeCarteraDePersona.ArmarAsync(cartera, p.PublicId, hoy, CancellationToken.None);

        sinActivar.LendingStatus.Should().Be(PortfolioLendingStatus.NotActivated);
        sinActivar.ActiveLoans.Should().Be(0);
        migrada.LendingStatus.Should().Be(PortfolioLendingStatus.MigratedNotActivated);
        caida.LendingStatus.Should().Be(PortfolioLendingStatus.Unavailable, "el detalle de la persona no se cae porque Cartera no responda");
    }
}
