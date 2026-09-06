using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Payroll.Payments;
using IngenIA365ERP.Application.Payroll.Runs.ApprovePayrollRun;
using IngenIA365ERP.Application.Payroll.Runs.CalculatePayrollRun;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.Payments;

/// <summary>T103 — FR-025/FR-040: relación de pago, marca de pagado y su retiro con motivo.</summary>
public class PaymentCommandsTests
{
    private static readonly ICurrentUserService Contadora = NominaTestData.UsuarioDePrueba("contadora@demo", 9);
    private static readonly ICurrentUserService Tesorera = NominaTestData.UsuarioDePrueba("tesorera@demo", 11);

    private static async Task<Guid> Aprobada(NominaTestData d)
    {
        d.ConfigurarContabilidad();
        var calc = new CalculatePayrollRunCommandHandler(d.Db, d.Loader, d.Lock, d.Clock, d.User, NullLogger<CalculatePayrollRunCommandHandler>.Instance);
        var r = await calc.Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);
        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var poster = new PayrollAccountingPoster(d.Db, d.Clock, Contadora);
        var audit = new PayrollAuditEmitter(d.Audit, Contadora, d.Clock, NullLogger<PayrollAuditEmitter>.Instance);
        var apr = new ApprovePayrollRunCommandHandler(d.Db, poster, d.Policies, d.Permissions, d.Clock, Contadora, audit);
        var a = await apr.Handle(new ApprovePayrollRunCommand(r.Value.RunPublicId, Confirm: true), CancellationToken.None);
        a.IsSuccess.Should().BeTrue(a.Error.Message);
        return r.Value.RunPublicId;
    }

    private static MarkPaymentsCommandHandler Marcador(NominaTestData d) =>
        new(d.Db, d.Clock, Tesorera, new PayrollAuditEmitter(d.Audit, Tesorera, d.Clock, NullLogger<PayrollAuditEmitter>.Instance));

    private static RevertPaymentMarkCommandHandler Retirador(NominaTestData d) =>
        new(d.Db, d.Clock, Tesorera, new PayrollAuditEmitter(d.Audit, Tesorera, d.Clock, NullLogger<PayrollAuditEmitter>.Instance));

    [Fact]
    public async Task La_relacion_de_pago_trae_neto_banco_cuenta_y_estado()
    {
        var d = new NominaTestData();
        d.Db.Banks.Add(new Bank { LegacyCode = "07", Name = "Bancolombia", CreatedBy = "test" });
        d.Ana.PayrollBankId = "07"; d.Ana.PayrollBankAccountType = 1; d.Ana.PayrollBankAccountNumber = "123-456";
        d.Db.SaveChanges();
        var runId = await Aprobada(d);

        var r = await new GetPaymentRegisterQueryHandler(d.Db).Handle(new GetPaymentRegisterQuery(runId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Employees.Should().Be(1);
        r.Value.PaidCount.Should().Be(0);
        var fila = r.Value.Rows.Single();
        fila.EmployeeName.Should().Be("Ana Prueba");
        fila.BankName.Should().Be("Bancolombia");
        fila.BankAccountType.Should().Be("Ahorros");
        fila.BankAccountNumber.Should().Be("123-456");
        fila.NetPay.Should().Be(r.Value.TotalNet).And.BeGreaterThan(0m);
        fila.Paid.Should().BeFalse();
    }

    [Fact]
    public async Task Marcar_a_todos_deja_una_marca_por_empleado_y_audita()
    {
        var d = new NominaTestData();
        d.Empleado("Bruno", 3_000_000m, new DateTime(2024, 2, 1));
        var runId = await Aprobada(d);

        var r = await Marcador(d).Handle(new MarkPaymentsCommand(runId, null, new DateTime(2026, 3, 30), PayrollPaymentMethod.Transfer, "LOTE-77"), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Should().Be(2);
        var pagos = await d.Db.PayrollPayments.ToListAsync();
        pagos.Should().HaveCount(2);
        pagos.Should().OnlyContain(p => p.PaymentMethod == PayrollPaymentMethod.Transfer && p.Reference == "LOTE-77" && p.PaidBy == "tesorera@demo" && !p.IsReverted);
        await d.Audit.Received(1).AppendAsync(Arg.Is<AuditEventDocument>(a => a.Action == AuditEventTypes.PayrollPaymentsMarked), Arg.Any<CancellationToken>());

        var relacion = await new GetPaymentRegisterQueryHandler(d.Db).Handle(new GetPaymentRegisterQuery(runId), CancellationToken.None);
        relacion.Value.PaidCount.Should().Be(2);
        relacion.Value.PaidNet.Should().Be(relacion.Value.TotalNet);
    }

    [Fact]
    public async Task Marcar_a_algunos_y_volver_a_marcar_al_mismo_se_rechaza()
    {
        var d = new NominaTestData();
        var bruno = d.Empleado("Bruno", 3_000_000m, new DateTime(2024, 2, 1));
        var runId = await Aprobada(d);

        var primero = await Marcador(d).Handle(new MarkPaymentsCommand(runId, [bruno.PublicId], new DateTime(2026, 3, 30), PayrollPaymentMethod.Cash, null), CancellationToken.None);
        primero.Value.Should().Be(1);

        var repetido = await Marcador(d).Handle(new MarkPaymentsCommand(runId, [bruno.PublicId], new DateTime(2026, 3, 31), PayrollPaymentMethod.Check, "CH-1"), CancellationToken.None);
        repetido.Error.Code.Should().Be("Payroll.PaymentAlreadyMarked");

        // «Todos» sólo completa a los que faltan: no duplica a Bruno.
        var resto = await Marcador(d).Handle(new MarkPaymentsCommand(runId, null, new DateTime(2026, 3, 31), PayrollPaymentMethod.Transfer, null), CancellationToken.None);
        resto.Value.Should().Be(1);
        (await d.Db.PayrollPayments.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Una_corrida_no_aprobada_no_se_marca()
    {
        var d = new NominaTestData();
        var run = d.Borrador(d.Marzo);

        var r = await Marcador(d).Handle(new MarkPaymentsCommand(run.PublicId, null, new DateTime(2026, 3, 30), PayrollPaymentMethod.Transfer, null), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.RunNotApproved");
    }

    [Fact]
    public async Task Un_empleado_que_no_esta_en_la_corrida_se_rechaza()
    {
        var d = new NominaTestData();
        var runId = await Aprobada(d);

        var r = await Marcador(d).Handle(new MarkPaymentsCommand(runId, [Guid.NewGuid()], new DateTime(2026, 3, 30), PayrollPaymentMethod.Transfer, null), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.RunEmployeeNotFound");
    }

    [Fact]
    public async Task Retirar_la_marca_exige_motivo_y_deja_la_fila_revertida_no_borrada()
    {
        var d = new NominaTestData();
        var runId = await Aprobada(d);
        await Marcador(d).Handle(new MarkPaymentsCommand(runId, null, new DateTime(2026, 3, 30), PayrollPaymentMethod.Transfer, null), CancellationToken.None);

        var validador = new RevertPaymentMarkCommandValidator();
        validador.Validate(new RevertPaymentMarkCommand(runId, d.Ana.PublicId, "")).IsValid.Should().BeFalse();

        var r = await Retirador(d).Handle(new RevertPaymentMarkCommand(runId, d.Ana.PublicId, "Transferencia rechazada por el banco"), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var pago = await d.Db.PayrollPayments.SingleAsync();
        pago.IsReverted.Should().BeTrue();
        pago.RevertedBy.Should().Be("tesorera@demo");
        pago.RevertReason.Should().Be("Transferencia rechazada por el banco");
        await d.Audit.Received(1).AppendAsync(Arg.Is<AuditEventDocument>(a => a.Action == AuditEventTypes.PayrollPaymentMarkReverted), Arg.Any<CancellationToken>());

        // Sin marca vigente no hay nada que retirar; y se puede volver a marcar.
        (await Retirador(d).Handle(new RevertPaymentMarkCommand(runId, d.Ana.PublicId, "otra vez"), CancellationToken.None)).Error.Code.Should().Be("Payroll.PaymentNotFound");
        var otraVez = await Marcador(d).Handle(new MarkPaymentsCommand(runId, [d.Ana.PublicId], new DateTime(2026, 4, 2), PayrollPaymentMethod.Check, "CH-9"), CancellationToken.None);
        otraVez.Value.Should().Be(1);
        (await d.Db.PayrollPayments.CountAsync(p => !p.IsReverted)).Should().Be(1);
    }
}
