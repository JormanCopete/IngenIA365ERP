using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// El desembolso (feature 013, US3, T0457; contracts/api.md §9.1, FR-045, FR-046, D12): la vista previa bajo la solicitud
/// (<c>POST /applications/{id}/disbursement-preview</c>, sin clave: no escribe), desembolsar un tramo (<c>POST /disbursements</c>, con
/// <c>Idempotency-Key</c>; 201 con el crédito) y la lista de tramos desembolsados (<c>Lending.Loans.Read</c>). Quien desembolsa necesita
/// <c>Lending.Disbursements.Execute</c>, que admite monto operativo por rol; la segregación (<c>Decidir × Desembolsar</c>), las garantías, la
/// usura del día y lo demás lo evalúa <see cref="RequisitosDelDesembolso"/>. Fuera del alcance, el 404 de la solicitud. Cada ruta sólo reenvía
/// al <see cref="ISender"/>. (nuevo)
/// </summary>
public class DisbursementsEndpoints : ICarterModule
{
    public const string Desembolsar = "Lending.Disbursements.Execute";
    public const string LeerCreditos = "Lending.Loans.Read";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var desembolsos = LendingGroup.Grupo(app, string.Empty, "Cartera Desembolsos");

        desembolsos.MapPost("/applications/{id:guid}/disbursement-preview", async (Guid id, DesembolsoRequest? body, ISender sender, CancellationToken ct) =>
                await sender.Send(new PreviewDisbursementQuery((body ?? new DesembolsoRequest()).Entrada(id)), ct))
            .WithName("Lending_Disbursements_Preview")
            .RequirePermission(Desembolsar);

        desembolsos.MapPost("/disbursements", async (DesembolsoRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new ExecuteLoanDisbursementCommand(body.Entrada(body.ApplicationPublicId), body.AcknowledgeWarnings)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), r => $"/api/lending/disbursement-orders/{r.Order?.PublicId ?? r.OperationPublicId}"))
            .WithName("Lending_Disbursements_Execute")
            .ConClaveDeOperacion()
            .RequirePermission(Desembolsar);

        desembolsos.MapGet("/disbursements", async (Guid? loanPublicId, DateOnly? from, DateOnly? to, DisbursementForm? form, int? page, int? pageSize,
                ISender sender, CancellationToken ct) =>
                await sender.Send(new ListLoanDisbursementsQuery(loanPublicId, from, to, form, page ?? 1, pageSize ?? 20), ct))
            .WithName("Lending_Disbursements_List")
            .RequirePermission(LeerCreditos);
    }

    /// <summary>
    /// <c>DisbursementInput</c> (§9.1) con <c>acknowledgeWarnings</c> al lado. En la vista previa la solicitud sale de la ruta.
    /// </summary>
    public sealed record DesembolsoRequest
    {
        public Guid ApplicationPublicId { get; init; }
        public Guid? LoanPublicId { get; init; }
        public decimal? TrancheAmount { get; init; }
        public DateOnly? ValueDate { get; init; }
        public DisbursementForm Form { get; init; } = DisbursementForm.Transfer;
        public BeneficiaryInput? Beneficiary { get; init; }
        public IReadOnlyList<OwnLoanPayoffInput>? OwnLoanPayoffs { get; init; }
        public bool PaidByFile { get; init; }
        public string? ExpectedPreviewSha256 { get; init; }
        public bool AcknowledgeWarnings { get; init; }

        public DisbursementInput Entrada(Guid solicitud) => new()
        {
            ApplicationPublicId = solicitud,
            LoanPublicId = LoanPublicId,
            TrancheAmount = TrancheAmount,
            ValueDate = ValueDate,
            Form = Form,
            Beneficiary = Beneficiary,
            OwnLoanPayoffs = OwnLoanPayoffs ?? [],
            PaidByFile = PaidByFile,
            ExpectedPreviewSha256 = ExpectedPreviewSha256,
        };
    }
}
