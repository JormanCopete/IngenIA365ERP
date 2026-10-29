using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Treasury.BankAccounts;
using IngenIA365ERP.Application.Treasury.Reconciliation;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Treasury;

/// <summary>
/// La conciliación completa de T2 (feature 015, fase 41, T0449; FR-128 a FR-130, FR-222; contracts/api.md §13): las notas bancarias con su aprobación, las reglas de
/// emparejamiento y las cuentas propias para la exógena. Las rutas son de T2. (nuevo)
/// </summary>
public class CompleteReconciliationEndpoints : ICarterModule
{
    public const string Registrar = "Treasury.BankNotes.Register";
    public const string Aprobar = "Treasury.BankNotes.Approve";
    public const string Ver = "Treasury.Reconciliation.View";
    public const string Trabajar = "Treasury.Reconciliation.Work";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var n = TreasuryGroup.Grupo(app, "/bank-notes", "Tesorería Notas bancarias");
        n.MapGet("/", async (Guid? account, BankNoteState? state, ISender s, CancellationToken ct) => await s.Send(new ListBankNotesQuery(account, state), ct))
            .WithName("Treasury_BankNotes_List").RequirePermission(new AlgunPermiso(Ver, Registrar, Aprobar));
        n.MapGet("/suggestions", async (Guid account, DateOnly? from, DateOnly? to, ISender s, CancellationToken ct) =>
                await s.Send(new ListBankNoteSuggestionsQuery(account, from, to), ct))
            .WithName("Treasury_BankNotes_Suggestions").RequirePermission(Registrar);
        n.MapPost("/", async (NotaRequest b, HttpContext http, ISender s, CancellationToken ct) =>
                TreasuryGroup.Creado(await s.Send(new RegisterBankNoteCommand(b.BankAccountPublicId ?? Guid.Empty, b.Kind ?? BankNoteKind.Other, b.Amount ?? 0m, b.NoteDate,
                    b.BaseAmount, b.VatAmount ?? 0m, b.WithholdingAmount ?? 0m, b.StatementLinePublicId, b.ThirdPartyPersonPublicId, b.Description)
                    { OperationKey = http.ClaveDeOperacion() }, ct), x => $"/api/treasury/bank-notes/{x.PublicId}"))
            .WithName("Treasury_BankNotes_Register").ConClaveDeOperacion().RequirePermission(Registrar);
        n.MapPost("/{id:guid}/approve", async (Guid id, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new DecideBankNoteCommand(id, true) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_BankNotes_Approve").ConClaveDeOperacion().RequirePermission(Aprobar);
        n.MapPost("/{id:guid}/reject", async (Guid id, MotivoRequest b, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(new DecideBankNoteCommand(id, false, b.Reason) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_BankNotes_Reject").ConClaveDeOperacion().RequirePermission(Aprobar);

        var r = TreasuryGroup.Grupo(app, "/reconciliation-rules", "Tesorería Reglas de conciliación");
        r.MapGet("/", async (ISender s, CancellationToken ct) => await s.Send(new ListReconciliationRulesQuery(), ct))
            .WithName("Treasury_ReconciliationRules_List").RequirePermission(new AlgunPermiso(Ver, Trabajar));
        r.MapPost("/", async (ReglaRequest b, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(Regla(null, b) with { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_ReconciliationRules_Create").ConClaveDeOperacion().RequirePermission(Trabajar);
        r.MapPut("/{id:guid}", async (Guid id, ReglaRequest b, HttpContext http, ISender s, CancellationToken ct) =>
                await s.Send(Regla(id, b) with { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Treasury_ReconciliationRules_Update").ConClaveDeOperacion().RequirePermission(Trabajar);

        var e = TreasuryGroup.Grupo(app, "/bank-accounts/exogena", "Tesorería Exógena");
        e.MapGet("/", async (int year, ISender s, CancellationToken ct) => await s.Send(new ListBankAccountsForExogenaQuery(year), ct))
            .WithName("Treasury_BankAccounts_Exogena").RequirePermission("Treasury.BankBalances.View");
    }

    private static SaveReconciliationRuleCommand Regla(Guid? id, ReglaRequest b) =>
        new(id, b.Code ?? string.Empty, b.Name ?? string.Empty, b.Criterion ?? ReconciliationRuleCriterion.ReferenceAndAmount, b.ToleranceDays ?? 0, b.Priority ?? 1,
            b.BankAccountPublicId, b.IsActive ?? true);

    public sealed record NotaRequest(Guid? BankAccountPublicId, BankNoteKind? Kind, decimal? Amount, DateOnly? NoteDate, decimal? BaseAmount, decimal? VatAmount,
        decimal? WithholdingAmount, Guid? StatementLinePublicId, Guid? ThirdPartyPersonPublicId, string? Description);

    public sealed record MotivoRequest(string? Reason);

    public sealed record ReglaRequest(string? Code, string? Name, ReconciliationRuleCriterion? Criterion, int? ToleranceDays, int? Priority, Guid? BankAccountPublicId,
        bool? IsActive);
}
