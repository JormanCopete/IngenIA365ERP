using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Lending.Withdrawals.Commands.ProcessWithdrawal;
using IngenIA365ERP.Application.Lending.Withdrawals.Queries;
using IngenIA365ERP.Application.Tests.Common.Integration.Lending;
using IngenIA365ERP.Application.Tests.Core.People;
using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Savings.Withdrawals;

/// <summary>
/// Feature 013, T0115 (T94, A5 «propuesta por defecto: sí»; contracts/interfaces.md §8.2): el retiro de un asociado es el consumidor
/// «Aportes, en el retiro» de FR-061. La vista previa (<c>GetWithdrawalPreviewQuery</c>) y el retiro (<c>ProcessWithdrawalCommand</c>)
/// cuentan las obligaciones vivas —como deudor o codeudor— por <see cref="IConsultaDeObligaciones"/>, no por la cartera heredada, y la
/// persona «migrada, sin activar» tampoco se retira. Los handlers se mudan a <c>Application/Savings</c> en la sección B (T0185).
/// </summary>
public class RetiroDeAsociadoConObligacionesTests
{
    private sealed record Escenario(PersonasTestData D, Person Asociado, WithdrawalReason Motivo, ConsultaDeObligacionesDePrueba Cartera);

    private static async Task<Escenario> CrearAsync()
    {
        var d = new PersonasTestData();
        var asociado = d.Persona(asociada: true);
        var motivo = new WithdrawalReason { Name = "Voluntario", LegacyCode = "VOL" };
        d.Db.WithdrawalReasons.Add(motivo);
        await d.Db.SaveChangesAsync();
        return new Escenario(d, asociado, motivo, new ConsultaDeObligacionesDePrueba());
    }

    // La vista previa (GetWithdrawalPreviewQueryHandler) cuenta con el mismo ObligacionesEnElRetiro; el contexto de pruebas no materializa
    // ahorros, aportes ni CDT, así que aquí se prueba el conteo que comparte con el retiro.
    private static Task<ObligacionesEnElRetiro.Conteo> VistaPreviaAsync(Escenario e, IConsultaDeObligaciones cartera) =>
        ObligacionesEnElRetiro.ContarAsync(cartera, e.Asociado.PublicId, new DateOnly(2026, 10, 7), CancellationToken.None);

    private static Task<IngenIA365ERP.Application.Common.Models.Result<Guid>> RetirarAsync(Escenario e, IConsultaDeObligaciones cartera) =>
        new ProcessWithdrawalCommandHandler(e.D.Db, e.D.Clock, e.D.User, cartera).Handle(
            new ProcessWithdrawalCommand { PersonPublicId = e.Asociado.PublicId, WithdrawalReasonPublicId = e.Motivo.PublicId }, CancellationToken.None);

    [Fact]
    public async Task Con_Cartera_sin_activar_el_asociado_se_retira_como_siempre()
    {
        var e = await CrearAsync();

        var vista = await VistaPreviaAsync(e, new ConsultaDeObligacionesSinActivar());
        var retiro = await RetirarAsync(e, new ConsultaDeObligacionesSinActivar());

        vista.Vivas.Should().Be(0);
        vista.Bloquea.Should().BeFalse();
        retiro.IsSuccess.Should().BeTrue(retiro.IsFailure ? retiro.Error.Message : string.Empty);
        (await e.D.Db.Associates.SingleAsync(a => a.PersonId == e.Asociado.Id)).Status.Should().Be("R");
    }

    [Theory]
    [InlineData(RolEnLaObligacion.Debtor)]
    [InlineData(RolEnLaObligacion.CoDebtor)]
    public async Task Una_obligacion_viva_como_deudor_o_codeudor_impide_el_retiro(RolEnLaObligacion rol)
    {
        var e = await CrearAsync();
        e.Cartera.Agregar(e.Asociado.PublicId, ConsultaDeObligacionesDePrueba.Obligacion("CR-0101", 2_000_000m, rol: rol));

        var vista = await VistaPreviaAsync(e, e.Cartera);
        var retiro = await RetirarAsync(e, e.Cartera);

        vista.Vivas.Should().Be(1);
        vista.Bloquea.Should().BeTrue();
        retiro.Error.Code.Should().Be("Withdrawal.ActiveLoans");
        e.Cartera.Consultas.Should().OnlyContain(c => c.IncludeAsCoDebtor && c.PersonPublicIds.Single() == e.Asociado.PublicId);
        (await e.D.Db.Associates.SingleAsync(a => a.PersonId == e.Asociado.Id)).Status.Should().Be("A");
    }

    [Fact]
    public async Task El_asociado_migrado_sin_activar_no_se_retira()
    {
        var e = await CrearAsync();
        e.Cartera.MarcarMigrada(e.Asociado.PublicId);

        var vista = await VistaPreviaAsync(e, e.Cartera);
        var retiro = await RetirarAsync(e, e.Cartera);

        vista.MigradaSinActivar.Should().BeTrue();
        vista.Bloquea.Should().BeTrue();
        retiro.Error.Code.Should().Be("Withdrawal.ActiveLoans");
        retiro.Error.Message.Should().Contain("Cartera por activar");
    }

    [Fact]
    public async Task Una_obligacion_cancelada_no_impide_el_retiro()
    {
        var e = await CrearAsync();
        e.Cartera.Agregar(e.Asociado.PublicId, ConsultaDeObligacionesDePrueba.Obligacion("CR-0102", 0m, estado: EstadoDeObligacion.PaidOff));

        var retiro = await RetirarAsync(e, e.Cartera);

        retiro.IsSuccess.Should().BeTrue(retiro.IsFailure ? retiro.Error.Message : string.Empty);
    }
}
