using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Payroll.Payslips;

public sealed record PdfFileDto(string FileName, byte[] Content);

// --------------------------------------------------------------------- PDF --

/// <summary>FR-024: el comprobante de un empleado en PDF.</summary>
public sealed record GetPayslipQuery(Guid RunPublicId, Guid EmployeePublicId) : IRequest<Result<PdfFileDto>>;

public sealed class GetPayslipQueryValidator : AbstractValidator<GetPayslipQuery>
{
    public GetPayslipQueryValidator()
    {
        RuleFor(x => x.RunPublicId).NotEmpty();
        RuleFor(x => x.EmployeePublicId).NotEmpty();
    }
}

public sealed class GetPayslipQueryHandler(PayslipModelBuilder builder, IPayslipPdfRenderer renderer)
    : IRequestHandler<GetPayslipQuery, Result<PdfFileDto>>
{
    public async Task<Result<PdfFileDto>> Handle(GetPayslipQuery request, CancellationToken ct)
    {
        var bundles = await builder.BuildAsync(request.RunPublicId, [request.EmployeePublicId], ct);
        if (bundles.IsFailure) return Result.Failure<PdfFileDto>(bundles.Error);
        var b = bundles.Value[0];
        var pdf = renderer.Render(b.Model);
        return Result.Success(new PdfFileDto($"comprobante_{b.Model.EmployeeDocument}_{b.Model.PeriodStart:yyyyMMdd}.pdf", pdf));
    }
}

/// <summary>Todos los comprobantes de la corrida en un solo PDF.</summary>
public sealed record GetAllPayslipsQuery(Guid RunPublicId) : IRequest<Result<PdfFileDto>>;

public sealed class GetAllPayslipsQueryValidator : AbstractValidator<GetAllPayslipsQuery>
{
    public GetAllPayslipsQueryValidator() => RuleFor(x => x.RunPublicId).NotEmpty();
}

public sealed class GetAllPayslipsQueryHandler(PayslipModelBuilder builder, IPayslipPdfRenderer renderer)
    : IRequestHandler<GetAllPayslipsQuery, Result<PdfFileDto>>
{
    public async Task<Result<PdfFileDto>> Handle(GetAllPayslipsQuery request, CancellationToken ct)
    {
        var bundles = await builder.BuildAsync(request.RunPublicId, null, ct);
        if (bundles.IsFailure) return Result.Failure<PdfFileDto>(bundles.Error);
        if (bundles.Value.Count == 0) return Result.Failure<PdfFileDto>(new Error("Payroll.RunEmployeeNotFound", "La corrida no tiene empleados."));
        var pdf = renderer.RenderMany(bundles.Value.Select(b => b.Model).ToList());
        var m = bundles.Value[0].Model;
        return Result.Success(new PdfFileDto($"comprobantes_{m.PeriodStart:yyyyMMdd}_{m.PeriodEnd:yyyyMMdd}_v{m.RunVersion}.pdf", pdf));
    }
}

// ------------------------------------------------------------------ envíos --

public sealed record PayslipDeliveryDto(Guid PublicId, Guid EmployeePublicId, string EmployeeName, string RecipientEmail, DateTime RequestedAt, string RequestedBy, DateTime? SentAt, string Status, string? ErrorMessage, int AttemptNumber);

public sealed record ListPayslipDeliveriesQuery(Guid RunPublicId) : IRequest<Result<IReadOnlyList<PayslipDeliveryDto>>>;

public sealed class ListPayslipDeliveriesQueryValidator : AbstractValidator<ListPayslipDeliveriesQuery>
{
    public ListPayslipDeliveriesQueryValidator() => RuleFor(x => x.RunPublicId).NotEmpty();
}

public sealed class ListPayslipDeliveriesQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListPayslipDeliveriesQuery, Result<IReadOnlyList<PayslipDeliveryDto>>>
{
    public async Task<Result<IReadOnlyList<PayslipDeliveryDto>>> Handle(ListPayslipDeliveriesQuery request, CancellationToken ct)
    {
        var run = await db.PayrollRuns.AsNoTracking().FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        if (run is null) return Result.Failure<IReadOnlyList<PayslipDeliveryDto>>(new Error("Payroll.RunNotFound", "No existe la corrida indicada."));

        var filas = await (
            from d in db.PayslipDeliveries.AsNoTracking()
            join re in db.PayrollRunEmployees.AsNoTracking() on d.PayrollRunEmployeeId equals re.Id
            join e in db.Employees.AsNoTracking() on re.EmployeeId equals e.Id
            join p in db.People.AsNoTracking() on e.PersonId equals p.Id
            where re.PayrollRunId == run.Id
            orderby d.RequestedAt descending
            select new PayslipDeliveryDto(d.PublicId, e.PublicId, (p.FirstName + " " + p.LastName).Trim(), d.RecipientEmail, d.RequestedAt, d.RequestedBy, d.SentAt, d.Status.ToString(), d.ErrorMessage, d.AttemptNumber))
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<PayslipDeliveryDto>>(filas);
    }
}

/// <summary>FR-026: envío manual de comprobantes por correo, uno a uno, con registro por intento. Nunca automático.</summary>
public sealed record SendPayslipsCommand(Guid RunPublicId, IReadOnlyList<Guid>? EmployeePublicIds) : IRequest<Result<PayslipDispatchResult>>;

public sealed class SendPayslipsCommandValidator : AbstractValidator<SendPayslipsCommand>
{
    public SendPayslipsCommandValidator() => RuleFor(x => x.RunPublicId).NotEmpty();
}

public sealed class SendPayslipsCommandHandler(IPayslipEmailDispatcher dispatcher, IOutboundEmailStatus emailStatus, PayrollAuditEmitter audit)
    : IRequestHandler<SendPayslipsCommand, Result<PayslipDispatchResult>>
{
    public async Task<Result<PayslipDispatchResult>> Handle(SendPayslipsCommand request, CancellationToken ct)
    {
        if (!emailStatus.IsConfigured)
            return Result.Failure<PayslipDispatchResult>(new Error("Payroll.EmailNotConfigured",
                $"Este ambiente no tiene correo saliente configurado ({emailStatus.Description}). Descargue los comprobantes y entréguelos por otro medio."));

        var resultado = await dispatcher.DispatchAsync(request.RunPublicId, request.EmployeePublicIds, ct);
        await audit.EmitAsync(AuditEventTypes.PayrollPayslipsSent, "PayrollRun", request.RunPublicId, null,
            new { resultado.Sent, resultado.Failed, withoutEmail = resultado.WithoutEmail.Count, requested = request.EmployeePublicIds?.Count }, ct);
        return Result.Success(resultado);
    }
}

/// <summary>
/// <see cref="IPayslipEmailDispatcher"/> (D-12): plantilla <c>PayslipEmail</c>, PDF adjunto,
/// un <see cref="PayslipDelivery"/> por intento. Un fallo de un correo no detiene a los
/// demás ni se esconde: queda <c>Failed</c> con el mensaje y en el log.
/// </summary>
public sealed class PayslipEmailDispatcher(
    IApplicationDbContext db,
    PayslipModelBuilder builder,
    IPayslipPdfRenderer renderer,
    IIdentityEmailTemplates templates,
    IEmailSender sender,
    IDateTimeService clock,
    ICurrentUserService user,
    ILogger<PayslipEmailDispatcher> logger) : IPayslipEmailDispatcher
{
    public const string TemplateName = "PayslipEmail";

    public async Task<PayslipDispatchResult> DispatchAsync(Guid runPublicId, IReadOnlyList<Guid>? employeePublicIds, CancellationToken ct)
    {
        var bundles = await builder.BuildAsync(runPublicId, employeePublicIds, ct);
        if (bundles.IsFailure)
            throw new InvalidOperationException($"{bundles.Error.Code}: {bundles.Error.Message}");

        var sinCorreo = new List<PayslipRecipientDto>();
        var fallos = new List<PayslipFailureDto>();
        var enviados = 0;
        var ahora = clock.UtcNow;
        var quien = user.UserName ?? "sistema";

        var ids = bundles.Value.Select(b => b.RunEmployeeId).ToList();
        var intentos = await db.PayslipDeliveries.Where(d => ids.Contains(d.PayrollRunEmployeeId))
            .GroupBy(d => d.PayrollRunEmployeeId).Select(g => new { g.Key, N = g.Max(d => d.AttemptNumber) }).ToDictionaryAsync(g => g.Key, g => g.N, ct);

        foreach (var b in bundles.Value)
        {
            if (b.Email is null)
            {
                sinCorreo.Add(new PayslipRecipientDto(b.EmployeePublicId, b.Model.EmployeeName));
                continue;
            }

            var entrega = new PayslipDelivery
            {
                PayrollRunEmployeeId = b.RunEmployeeId,
                RecipientEmail = b.Email,
                RequestedAt = ahora,
                RequestedBy = quien,
                AttemptNumber = intentos.GetValueOrDefault(b.RunEmployeeId) + 1,
                CreatedAt = ahora,
                CreatedBy = quien,
            };
            intentos[b.RunEmployeeId] = entrega.AttemptNumber;

            try
            {
                var pdf = renderer.Render(b.Model);
                var html = await templates.RenderAsync(TemplateName, new Dictionary<string, string?>
                {
                    ["EmployeeName"] = b.Model.EmployeeName,
                    ["CooperativeName"] = b.Model.CooperativeName,
                    ["PeriodLabel"] = b.Model.PeriodLabel,
                    ["NetPay"] = b.Model.NetPay.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-CO")),
                    ["Year"] = ahora.Year.ToString(),
                }, ct);
                var asunto = $"Comprobante de pago de nómina · {b.Model.PeriodLabel} · {b.Model.CooperativeName}";
                var adjunto = new EmailAttachment($"comprobante_{b.Model.EmployeeDocument}_{b.Model.PeriodStart:yyyyMMdd}.pdf", "application/pdf", pdf);
                await sender.SendAsync(new EmailMessage(b.Email, asunto, html, Attachments: [adjunto]), ct);

                entrega.Status = PayslipDeliveryStatus.Sent;
                entrega.SentAt = clock.UtcNow;
                enviados++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                entrega.Status = PayslipDeliveryStatus.Failed;
                entrega.ErrorMessage = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                fallos.Add(new PayslipFailureDto(b.EmployeePublicId, b.Model.EmployeeName, ex.Message));
                logger.LogWarning(ex, "Falló el envío del comprobante de {Employee} a {Email} (intento {Attempt})", b.Model.EmployeeName, b.Email, entrega.AttemptNumber);
            }
            db.PayslipDeliveries.Add(entrega);
        }

        await db.SaveChangesAsync(ct);
        return new PayslipDispatchResult(enviados, fallos.Count, sinCorreo, fallos);
    }
}
