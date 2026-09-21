using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Common;

/// <summary>
/// Revisión de la feature 010 (2026-09-21): la base de auditoría se nombra por el <c>PublicId</c>
/// del tenant, que es lo que la consola consulta. <see cref="AccountingAuditEmitter"/> escribía el Id
/// interno (<c>ICurrentUserService.TenantId</c>) y todos los eventos contables explícitos caían en
/// una base que nadie leía; <c>PayrollAuditEmitter</c> tuvo el mismo defecto y la misma corrección.
/// </summary>
public class AccountingAuditEmitterTests
{
    [Fact]
    public async Task El_documento_lleva_el_PublicId_del_tenant_y_no_el_Id_interno()
    {
        var writer = Substitute.For<IAuditAppendOnlyWriter>();
        var usuario = NominaTestData.UsuarioDePrueba("contadora@demo", 7);
        var tenant = Substitute.For<ICurrentTenantService>();
        var publicId = Guid.NewGuid().ToString("N");
        tenant.TenantId.Returns(publicId);
        var reloj = Substitute.For<IDateTimeService>();
        reloj.UtcNow.Returns(new DateTime(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc));
        var emisor = new AccountingAuditEmitter(writer, usuario, reloj, NullLogger<AccountingAuditEmitter>.Instance, tenant);

        await emisor.EmitAsync("Accounting.Account.Changed", "ChartOfAccount", Guid.NewGuid(), null, new { code = "110505" }, CancellationToken.None);

        await writer.Received(1).AppendAsync(
            Arg.Is<AuditEventDocument>(d => d.TenantId == publicId && d.Module == AccountingAuditEmitter.Modulo),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_servicio_de_tenant_cae_al_del_usuario_para_las_pruebas_que_lo_arman_a_mano()
    {
        var writer = Substitute.For<IAuditAppendOnlyWriter>();
        var usuario = NominaTestData.UsuarioDePrueba("contadora@demo", 7);
        var reloj = Substitute.For<IDateTimeService>();
        var emisor = new AccountingAuditEmitter(writer, usuario, reloj, NullLogger<AccountingAuditEmitter>.Instance);

        await emisor.EmitAsync("Accounting.Period.Changed", "AccountingPeriod", null, null, null, CancellationToken.None);

        await writer.Received(1).AppendAsync(Arg.Is<AuditEventDocument>(d => d.TenantId == "1"), Arg.Any<CancellationToken>());
    }
}
