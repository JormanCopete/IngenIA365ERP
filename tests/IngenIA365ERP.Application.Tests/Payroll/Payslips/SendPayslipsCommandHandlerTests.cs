using FluentAssertions;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using IngenIA365ERP.Application.Payroll.Payslips;
using IngenIA365ERP.Application.Payroll.Runs.ApprovePayrollRun;
using IngenIA365ERP.Application.Payroll.Runs.CalculatePayrollRun;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.Payslips;

/// <summary>T105 — FR-024/FR-026: el comprobante se arma desde la corrida aprobada y se envía uno a uno, con registro por intento.</summary>
public class SendPayslipsCommandHandlerTests
{
    private static readonly ICurrentUserService Contadora = NominaTestData.UsuarioDePrueba("contadora@demo", 9);

    private sealed class Armado
    {
        public required NominaTestData D { get; init; }
        public required Guid RunId { get; init; }
        public required IEmailSender Sender { get; init; }
        public required SendPayslipsCommandHandler Handler { get; init; }
        public required PayslipModelBuilder Builder { get; init; }
        public required List<EmailMessage> Enviados { get; init; }
    }

    private static async Task<Armado> Preparar(bool aprobar = true, bool correoConfigurado = true, Func<EmailMessage, bool>? falla = null)
    {
        var d = new NominaTestData();
        d.ConfigurarContabilidad();
        d.Empleado("Bruno", 3_000_000m, new DateTime(2024, 2, 1));
        var carla = d.Empleado("Carla", 2_500_000m, new DateTime(2023, 5, 10));
        // Carla no tiene correo registrado.
        (await d.Db.People.SingleAsync(p => p.Id == carla.PersonId)).Email = null;
        await d.Db.SaveChangesAsync();

        var calc = new CalculatePayrollRunCommandHandler(d.Db, d.Loader, d.Recurrentes, d.Lock, d.Clock, d.User, NullLogger<CalculatePayrollRunCommandHandler>.Instance);
        var r = await calc.Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);
        r.IsSuccess.Should().BeTrue(r.Error.Message);
        if (aprobar)
        {
            var poster = d.Contabilizador(Contadora);
            var audit = new PayrollAuditEmitter(d.Audit, Contadora, CooperativaDePrueba.Actual, d.Clock, NullLogger<PayrollAuditEmitter>.Instance);
            var a = await new ApprovePayrollRunCommandHandler(d.Db, poster, d.Policies, d.Permissions, d.Clock, Contadora, audit)
                .Handle(new ApprovePayrollRunCommand(r.Value.RunPublicId, Confirm: true), CancellationToken.None);
            a.IsSuccess.Should().BeTrue(a.Error.Message);
        }

        var tenant = Substitute.For<ICurrentTenantService>();
        tenant.TenantName.Returns("Cooperativa Demo");
        var builder = new PayslipModelBuilder(d.Db, tenant, d.Clock);

        var renderer = Substitute.For<IPayslipPdfRenderer>();
        renderer.Render(Arg.Any<PayslipModel>()).Returns(ci => System.Text.Encoding.ASCII.GetBytes("%PDF-" + ci.Arg<PayslipModel>().EmployeeDocument));

        var templates = Substitute.For<IIdentityEmailTemplates>();
        templates.RenderAsync("PayslipEmail", Arg.Any<IReadOnlyDictionary<string, string?>>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult($"<p>Hola {ci.Arg<IReadOnlyDictionary<string, string?>>()["EmployeeName"]}</p>"));

        var enviados = new List<EmailMessage>();
        var sender = Substitute.For<IEmailSender>();
        sender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>()).Returns(ci =>
        {
            var m = ci.Arg<EmailMessage>();
            if (falla is not null && falla(m)) throw new InvalidOperationException("SMTP 550: buzón inexistente");
            enviados.Add(m);
            return Task.CompletedTask;
        });

        var estado = Substitute.For<IOutboundEmailStatus>();
        estado.IsConfigured.Returns(correoConfigurado);
        estado.Description.Returns(correoConfigurado ? "smtp.demo:587" : "sin host");

        var dispatcher = new PayslipEmailDispatcher(d.Db, builder, renderer, templates, sender, d.Clock, Contadora, NullLogger<PayslipEmailDispatcher>.Instance);
        var auditEmitter = new PayrollAuditEmitter(d.Audit, Contadora, CooperativaDePrueba.Actual, d.Clock, NullLogger<PayrollAuditEmitter>.Instance);
        return new Armado
        {
            D = d, RunId = r.Value.RunPublicId, Builder = builder, Sender = sender, Enviados = enviados,
            Handler = new SendPayslipsCommandHandler(dispatcher, estado, auditEmitter),
        };
    }

    [Fact]
    public async Task El_comprobante_se_arma_desde_la_corrida_aprobada_con_lineas_banco_y_estado_de_pago()
    {
        var a = await Preparar();
        a.D.Ana.PayrollBankAccountNumber = "999-1"; await a.D.Db.SaveChangesAsync();

        var r = await a.Builder.BuildAsync(a.RunId, [a.D.Ana.PublicId], CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var m = r.Value.Single().Model;
        m.CooperativeName.Should().Be("Cooperativa Demo");
        m.EmployeeName.Should().Be("Ana Prueba");
        m.PlanName.Should().Be("Nómina general");
        m.RunVersion.Should().Be(1);
        m.ApprovedAt.Should().NotBeNull();
        m.DaysWorked.Should().Be(30);
        m.MonthlySalary.Should().Be(2_000_000m);
        m.Earnings.Should().Contain(l => l.Code == "SALARIO" && l.Amount == 2_000_000m && l.Summary.Length > 0);
        m.Deductions.Should().Contain(l => l.Code == "SALUD_EMP").And.Contain(l => l.Code == "PENSION_EMP");
        m.Deductions.Should().NotContain(l => l.Code == "SALUD_EMPLEADOR", "los aportes del empleador no van en el comprobante del empleado");
        (m.TotalEarnings - m.TotalDeductions).Should().Be(m.NetPay);
        m.BankAccount.Should().Be("999-1");
        m.PaymentStatus.Should().Be("Pendiente de pago");
    }

    [Fact]
    public async Task Un_borrador_no_produce_comprobante()
    {
        var a = await Preparar(aprobar: false);

        var r = await a.Builder.BuildAsync(a.RunId, null, CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.RunNotApproved");
    }

    [Fact]
    public async Task Envia_uno_a_uno_con_adjunto_y_registra_a_quien_no_tiene_correo()
    {
        var a = await Preparar();

        var r = await a.Handler.Handle(new SendPayslipsCommand(a.RunId, null), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Sent.Should().Be(2);
        r.Value.Failed.Should().Be(0);
        r.Value.WithoutEmail.Should().ContainSingle(w => w.EmployeeName == "Carla Prueba");
        a.Enviados.Should().HaveCount(2);
        a.Enviados.Should().OnlyContain(m => m.Attachments != null && m.Attachments.Count == 1 && m.Attachments[0].ContentType == "application/pdf" && m.Attachments[0].Content.Length > 0);
        a.Enviados.Select(m => m.To).Should().BeEquivalentTo(["ana@coop.test", "bruno@coop.test"]);
        a.Enviados.Should().OnlyContain(m => m.Subject.Contains("Comprobante de pago") && m.BodyHtml.Contains("Hola "));

        var entregas = await a.D.Db.PayslipDeliveries.ToListAsync();
        entregas.Should().HaveCount(2).And.OnlyContain(e => e.Status == PayslipDeliveryStatus.Sent && e.SentAt != null && e.AttemptNumber == 1 && e.RequestedBy == "contadora@demo");
        await a.D.Audit.Received(1).AppendAsync(Arg.Is<AuditEventDocument>(x => x.Action == AuditEventTypes.PayrollPayslipsSent), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Un_correo_que_falla_no_detiene_a_los_demas_y_queda_registrado_como_fallido()
    {
        var a = await Preparar(falla: m => m.To == "bruno@coop.test");

        var r = await a.Handler.Handle(new SendPayslipsCommand(a.RunId, null), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Sent.Should().Be(1);
        r.Value.Failed.Should().Be(1);
        r.Value.Failures.Should().ContainSingle(f => f.EmployeeName == "Bruno Prueba" && f.Error.Contains("550"));
        var fallido = await a.D.Db.PayslipDeliveries.SingleAsync(e => e.RecipientEmail == "bruno@coop.test");
        fallido.Status.Should().Be(PayslipDeliveryStatus.Failed);
        fallido.ErrorMessage.Should().Contain("550");
        fallido.SentAt.Should().BeNull();

        // Reintentar sólo a Bruno: segundo intento, numerado.
        var brunoPersona = await a.D.Db.People.SingleAsync(p => p.FirstName == "Bruno");
        var bruno = await a.D.Db.Employees.SingleAsync(e => e.PersonId == brunoPersona.Id);
        var r2 = await a.Handler.Handle(new SendPayslipsCommand(a.RunId, [bruno.PublicId]), CancellationToken.None);
        r2.Value.Failed.Should().Be(1);
        (await a.D.Db.PayslipDeliveries.Where(e => e.RecipientEmail == "bruno@coop.test").Select(e => e.AttemptNumber).ToListAsync()).Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public async Task Sin_correo_saliente_configurado_informa_antes_de_intentar()
    {
        var a = await Preparar(correoConfigurado: false);

        var r = await a.Handler.Handle(new SendPayslipsCommand(a.RunId, null), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.EmailNotConfigured");
        r.Error.Message.Should().Contain("sin host");
        await a.Sender.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
        (await a.D.Db.PayslipDeliveries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task La_lista_de_envios_muestra_cada_intento_con_su_estado()
    {
        var a = await Preparar(falla: m => m.To == "ana@coop.test");
        await a.Handler.Handle(new SendPayslipsCommand(a.RunId, null), CancellationToken.None);

        var r = await new ListPayslipDeliveriesQueryHandler(a.D.Db).Handle(new ListPayslipDeliveriesQuery(a.RunId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        r.Value.Should().HaveCount(2);
        r.Value.Should().ContainSingle(x => x.EmployeeName == "Ana Prueba" && x.Status == "Failed" && x.ErrorMessage != null);
        r.Value.Should().ContainSingle(x => x.EmployeeName == "Bruno Prueba" && x.Status == "Sent");
    }
}
