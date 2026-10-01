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
        var emisor = new AccountingAuditEmitter(escritor, usuario, reloj, NullLogger<AccountingAuditEmitter>.Instance, cooperativa);

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
        var emisor = new PayrollAuditEmitter(escritor, usuario, reloj, NullLogger<PayrollAuditEmitter>.Instance, cooperativa);

        await emisor.EmitAsync("Payroll.Run.Approved", "PayrollRun", Guid.NewGuid(), null, new { neto = 1 }, CancellationToken.None);

        escrito.Should().NotBeNull();
        escrito!.TenantId.Should().Be(CooperativaDePrueba.PublicIdN);
        escrito.Module.Should().Be("Payroll");
    }

    [Fact]
    public async Task Sin_cooperativa_activa_lanza_y_no_escribe_en_la_base_global()
    {
        // Feature 012, T495 (T5, FR-083): hasta I2 un evento sin cooperativa caía vacío a la base global. Con el
        // procesador de mensajes corriendo en segundo plano eso escondería un defecto de ámbito: ahora lanza.
        var (escritor, usuario, cooperativa, reloj) = Escenario();
        cooperativa.TenantId.Returns((string?)null);

        var acto = () => new AccountingAuditEmitter(escritor, usuario, reloj, NullLogger<AccountingAuditEmitter>.Instance, cooperativa)
            .EmitAsync("Accounting.Setup.Initialized", "AccountingSetup", null, null, null, CancellationToken.None);

        await acto.Should().ThrowAsync<InvalidOperationException>().WithMessage("*cooperativa*");
        await escritor.DidNotReceiveWithAnyArgs().AppendAsync(default!, default);
    }

    [Fact]
    public async Task Sin_servicio_de_tenant_tambien_lanza_y_nunca_va_al_id_interno()
    {
        // La revisión de la feature 010 (2026-09-21) dejó el servicio opcional para las pruebas que arman el
        // emisor a mano, y de paso caía al Id interno del usuario; desde I2 de la 012 tampoco cae a la global.
        var (escritor, usuario, _, reloj) = Escenario();

        var acto = () => new AccountingAuditEmitter(escritor, usuario, reloj, NullLogger<AccountingAuditEmitter>.Instance)
            .EmitAsync("Accounting.Period.Changed", "AccountingPeriod", null, null, null, CancellationToken.None);

        await acto.Should().ThrowAsync<InvalidOperationException>();
        await escritor.DidNotReceiveWithAnyArgs().AppendAsync(default!, default);
    }

    [Fact]
    public async Task Los_eventos_de_inventario_van_a_la_base_de_la_cooperativa()
    {
        var (escritor, usuario, cooperativa, reloj) = Escenario();
        var escritos = new List<AuditEventDocument>();
        escritor.AppendAsync(Arg.Do<AuditEventDocument>(escritos.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var emisor = new AccountingAuditEmitter(escritor, usuario, reloj, NullLogger<AccountingAuditEmitter>.Instance, cooperativa);
        var comprobante = Guid.NewGuid();
        var lote = Guid.NewGuid();

        await emisor.EmitirContabilizacionDeInventarioAsync(comprobante, new { messages = 2, voucher = "EI-7" }, CancellationToken.None);
        await emisor.EmitirRechazoDeInventarioAsync(Guid.NewGuid(), new { code = "Accounting.Period.Closed" }, CancellationToken.None);
        await emisor.EmitirLoteDeInventarioProcesadoAsync(lote, new { number = 12, status = "Completed" }, CancellationToken.None);

        escritos.Select(e => e.Action).Should().Equal("Accounting.Inventory.Posted", "Accounting.Inventory.Rejected", "Accounting.Inventory.BatchProcessed");
        escritos.Should().OnlyContain(e => e.TenantId == CooperativaDePrueba.PublicIdN && e.Module == "Accounting");
        escritos[0].EntityPublicId.Should().Be(comprobante.ToString());
        escritos[0].NewValuesJson.Should().Contain("\"voucher\":\"EI-7\"");
        escritos[2].EntityType.Should().Be("IntegrationBatch");
        escritos[2].EntityPublicId.Should().Be(lote.ToString());
    }

    [Fact]
    public async Task Si_el_escritor_falla_la_operacion_no_se_cae()
    {
        var (escritor, usuario, cooperativa, reloj) = Escenario();
        escritor.AppendAsync(Arg.Any<AuditEventDocument>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException("Mongo caído")));

        var acto = () => new AccountingAuditEmitter(escritor, usuario, reloj, NullLogger<AccountingAuditEmitter>.Instance, cooperativa)
            .EmitirExportacionAsync("journal", new { }, "pdf", 1, CancellationToken.None);

        await acto.Should().NotThrowAsync();
    }
}
