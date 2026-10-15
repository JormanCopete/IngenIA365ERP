using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Payroll.NotApplied;
using IngenIA365ERP.Application.Lending.Payroll.Transfers;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Los no aplicados de la libranza y el traslado de una libranza (feature 013, K2, US10, T0850; contracts/api.md §13.3; FR-112, FR-115, FR-116, C32,
/// C44): la lista —también los de la nómina propia nacidos en K1 y los de la activación— bajo <c>Lending.PayrollDeductionFiles.Read</c> con el alcance
/// de oficina y línea; resolver uno (pasar a caja, enviar a cobranza o cerrar, con motivo y aviso al deudor) bajo
/// <c>Lending.PayrollNotApplied.Manage</c>; y <c>POST /api/lending/loans/{id}/payroll-transfer</c> bajo <c>Lending.PayrollTransfers.Manage</c>: a otra
/// pagaduría con convenio conservando la fecha de libranza, o a la forma de pago alterna del convenio con aviso al deudor. Las escrituras con
/// <c>Idempotency-Key</c>. (nuevo)
/// </summary>
public class PayrollNotAppliedEndpoints : ICarterModule
{
    public const string Leer = "Lending.PayrollDeductionFiles.Read";
    public const string Resolver = "Lending.PayrollNotApplied.Manage";
    public const string Trasladar = "Lending.PayrollTransfers.Manage";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var noAplicados = LendingGroup.Grupo(app, "/payroll-not-applied", "Cartera Libranza");

        noAplicados.MapGet("/", async (Guid? agreementPublicId, NonDeductionOrigin? origin, string? status, int? page, int? pageSize, ISender sender,
                CancellationToken ct) =>
                await sender.Send(new ListInstallmentNonDeductionsQuery(agreementPublicId, origin, status, page ?? 1, pageSize ?? 50), ct))
            .WithName("Lending_PayrollNotApplied_List")
            .RequirePermission(Leer);

        noAplicados.MapPost("/{id:guid}/resolve", async (Guid id, ResolucionRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new ResolveInstallmentNonDeductionCommand(id, body.Action ?? default, body.Reason ?? string.Empty)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_PayrollNotApplied_Resolve")
            .ConClaveDeOperacion()
            .RequirePermission(Resolver);

        var creditos = LendingGroup.Grupo(app, "/loans", "Cartera Libranza");

        creditos.MapPost("/{id:guid}/payroll-transfer", async (Guid id, TrasladoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new TransferLoanPayrollCommand(id, body.Reason ?? string.Empty, body.NewAgreementPublicId, body.EmployeeId,
                    body.CertificationAttachmentPublicId, body.CertificationDate, body.CertifiedNetIncome)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct))
            .WithName("Lending_Loans_PayrollTransfer")
            .ConClaveDeOperacion()
            .RequirePermission(Trasladar);
    }

    /// <summary>La resolución (§13.3): <c>MovedToCashPayment</c>, <c>SentToCollection</c> o <c>Closed</c>, y el motivo.</summary>
    public sealed record ResolucionRequest(NonDeductionFollowUpAction? Action, string? Reason);

    /// <summary>
    /// El traslado (§13.3): el convenio nuevo (nulo = la forma de pago alterna del convenio vigente), la identificación ante la pagaduría nueva, la
    /// certificación (adjunto, fecha e ingreso neto certificado) y el motivo.
    /// </summary>
    public sealed record TrasladoRequest(Guid? NewAgreementPublicId, string? EmployeeId, Guid? CertificationAttachmentPublicId, DateOnly? CertificationDate,
        decimal? CertifiedNetIncome, string? Reason);
}
