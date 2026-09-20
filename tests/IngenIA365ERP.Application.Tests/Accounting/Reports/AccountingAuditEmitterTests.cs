using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Common;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Reports;

/// <summary>
/// Los eventos explícitos de contabilidad y nómina llevan la cooperativa que la consola de
/// auditoría consulta: el PublicId en formato N de <see cref="ICurrentTenantService"/>. Hasta el
/// 2026-09-20 llevaban el Id interno de <see cref="ICurrentUserService.TenantId"/> y caían en una
/// base (<c>…_Audit_3</c>) que nadie leía; la e2e de informes lo destapó al buscar
/// <c>Accounting.Report.Exported</c> tras exportar.
/// </summary>
public class AccountingAuditEmitterTests
{
    private static (IAuditAppendOnlyWriter Escritor, ICurrentUserService Usuario, ICurrentTenantService Cooperativa, IDateTimeService Reloj) Escenario()
    {
        var escritor = Substitute.For<IAuditAppendOnlyWriter>();
        var usuario = Substitute.For<ICurrentUserService>();
        usuario.TenantId.Returns("3");
        usuario.UserId.Returns(7);
        usuario.UserName.Returns("contadora@coop");
        var cooperativa = Substitute.For<ICurrentTenantService>();
        cooperativa.TenantId.Returns(CooperativaDePrueba.PublicIdN);
        var reloj = Substitute.For<IDateTimeService>();
        reloj.UtcNow.Returns(new DateTime(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc));
        return (escritor, usuario, cooperativa, reloj);
    }

    [Fact]
    public async Task La_exportacion_contable_va_a_la_base_de_la_cooperativa_no_al_id_interno()
    {
        var (escritor, usuario, cooperativa, reloj) = Escenario();
        AuditEventDocument? escrito = null;
        escritor.AppendAsync(Arg.Do<AuditEventDocument>(d => escrito = d), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var emisor = new AccountingAuditEmitter(escritor, usuario, cooperativa, reloj, NullLogger<AccountingAuditEmitter>.Instance);

        await emisor.EmitirExportacionAsync("trial-balance", new { from = "2026-01-01" }, "xlsx", 42, CancellationToken.None);

        escrito.Should().NotBeNull();
        escrito!.TenantId.Should().Be(CooperativaDePrueba.PublicIdN, "la misma clave con la que la consola lee");
        escrito.TenantId.Should().NotBe("3");
        escrito.Action.Should().Be("Accounting.Report.Exported");
        escrito.Module.Should().Be("Accounting");
        escrito.UserId.Should().Be("7");
        escrito.NewValuesJson.Should().Contain("\"informe\":\"trial-balance\"").And.Contain("\"formato\":\"xlsx\"").And.Contain("\"filas\":42");
    }

    [Fact]
    public async Task El_evento_de_nomina_tambien()
    {
        var (escritor, usuario, cooperativa, reloj) = Escenario();
        AuditEventDocument? escrito = null;
        escritor.AppendAsync(Arg.Do<AuditEventDocument>(d => escrito = d), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var emisor = new PayrollAuditEmitter(escritor, usuario, cooperativa, reloj, NullLogger<PayrollAuditEmitter>.Instance);

        await emisor.EmitAsync("Payroll.Run.Approved", "PayrollRun", Guid.NewGuid(), null, new { neto = 1 }, CancellationToken.None);

        escrito.Should().NotBeNull();
        escrito!.TenantId.Should().Be(CooperativaDePrueba.PublicIdN);
        escrito.Module.Should().Be("Payroll");
    }

    [Fact]
    public async Task Sin_cooperativa_activa_el_evento_va_a_la_base_global_y_no_a_una_llamada_por_el_id_interno()
    {
        var (escritor, usuario, cooperativa, reloj) = Escenario();
        cooperativa.TenantId.Returns((string?)null);
        AuditEventDocument? escrito = null;
        escritor.AppendAsync(Arg.Do<AuditEventDocument>(d => escrito = d), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        await new AccountingAuditEmitter(escritor, usuario, cooperativa, reloj, NullLogger<AccountingAuditEmitter>.Instance)
            .EmitAsync("Accounting.Setup.Initialized", "AccountingSetup", null, null, null, CancellationToken.None);

        escrito!.TenantId.Should().BeEmpty("vacío es lo que el escritor traduce a la base global; «3» sería una base fantasma");
    }

    [Fact]
    public async Task Si_el_escritor_falla_la_operacion_no_se_cae()
    {
        var (escritor, usuario, cooperativa, reloj) = Escenario();
        escritor.AppendAsync(Arg.Any<AuditEventDocument>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException("Mongo caído")));

        var acto = () => new AccountingAuditEmitter(escritor, usuario, cooperativa, reloj, NullLogger<AccountingAuditEmitter>.Instance)
            .EmitirExportacionAsync("journal", new { }, "pdf", 1, CancellationToken.None);

        await acto.Should().NotThrowAsync();
    }
}
