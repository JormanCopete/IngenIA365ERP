using FluentAssertions;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Lending.Deposits.Operations;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Enums.Approvals;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits.Operations;

/// <summary>
/// Feature 014 (decisión del dueño del 2026-10-18, research §13 D53): la aprobación de los retiros y de las consignaciones en efectivo que superan la
/// atribución es configurable por cooperativa —niveles con su umbral y su permiso en la plataforma de aprobaciones— y los montos se fijan por rol en los
/// limitables de Cartera; sin política rige la regla fija de un nivel.
/// </summary>
public class AprobacionDeMovimientosConfigurableTests
{
    [Fact]
    public void Con_politica_mandan_sus_niveles_y_sin_ella_la_regla_fija()
    {
        var politica = new PoliticaDeAprobacion(null, new DateOnly(2026, 1, 1), null,
        [
            new NivelDeAprobacion(1, 0m, "Lending.Accounts.Withdraw"),
            new NivelDeAprobacion(2, 50_000_000m, "Lending.MembershipWithdrawals.Approve"),
        ]);

        var conPolitica = EvaluadorDePolitica.Evaluar(ApprovalSubjects.AccountWithdrawal, 80_000_000m, politica, 10_000_000m);
        conPolitica.Niveles.Should().HaveCount(2);
        conPolitica.ReglaFija.Should().BeFalse();

        var sinPolitica = EvaluadorDePolitica.Evaluar(ApprovalSubjects.AccountWithdrawal, 80_000_000m, null, 10_000_000m);
        sinPolitica.ReglaFija.Should().BeTrue();
        sinPolitica.Niveles.Should().ContainSingle().Which.PermissionCode.Should().Be("Lending.Accounts.Withdraw");

        PermisosLimitables.Admite("Lending.Accounts.Withdraw", PermisosLimitables.ModuloCartera).Should().BeTrue();
        PermisosLimitables.Admite("Lending.Accounts.Deposit", PermisosLimitables.ModuloCartera).Should().BeTrue();
    }

    [Fact]
    public async Task Con_politica_la_fuente_no_exige_el_monto_de_la_regla_fija()
    {
        var db = TestDbContextFactory.Create();
        var fuente = new FuenteDeAprobacionDeCuenta(db);
        var solicitud = new ApprovalRequest
        {
            Subject = ApprovalSubjects.AccountWithdrawal, SourceType = ApprovalSourceTypes.MemberAccountOperation, Amount = 80_000_000m,
            OperationDate = new DateOnly(2026, 12, 1), PolicyId = 7,
        };

        (await fuente.AlAprobarElNivelAsync(solicitud, 99, ApprovalMethod.OwnSession, esElUltimo: true, default)).IsSuccess.Should().BeTrue(
            "con política, los niveles de la cooperativa deciden (el aprobador de cada nivel ya cumple su permiso)");
    }
}
