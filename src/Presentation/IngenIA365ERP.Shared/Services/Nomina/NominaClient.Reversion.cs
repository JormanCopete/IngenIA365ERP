using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Nomina;

/// <summary>US7: reversión de una liquidación aprobada (contracts/api.md §4).</summary>
public sealed partial class NominaClient
{
    public Task<InvitationApiResult<ResultadoReversionDto>> ReversarAsync(Guid corridaId, string motivo, CancellationToken ct = default) =>
        EnviarAsync<ResultadoReversionDto>(HttpMethod.Post, $"/api/payroll/runs/{corridaId}/reverse", new { Reason = motivo }, ct);
}

public sealed record ResultadoReversionDto(Guid RunPublicId, Guid PeriodPublicId, Guid ReversalAccountingDocumentPublicId, string ReversalAccountingDocumentNumber);
