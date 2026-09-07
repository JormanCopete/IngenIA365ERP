using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Nomina;

/// <summary>US5: relación de pago, marca de pago y comprobantes (contracts/api.md §5).</summary>
public sealed partial class NominaClient
{
    public Task<InvitationApiResult<RelacionDePagoDto>> RelacionDePagoAsync(Guid corridaId, CancellationToken ct = default) =>
        EnviarAsync<RelacionDePagoDto>(HttpMethod.Get, $"/api/payroll/runs/{corridaId}/payments", null, ct);

    public Task<InvitationApiResult<int>> MarcarPagadosAsync(Guid corridaId, MarcarPagoRequest request, CancellationToken ct = default) =>
        EnviarAsync<int>(HttpMethod.Post, $"/api/payroll/runs/{corridaId}/payments", request, ct);

    public Task<InvitationApiResult<EmptyResponse>> RetirarMarcaDePagoAsync(Guid corridaId, Guid empleadoId, string motivo, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Post, $"/api/payroll/runs/{corridaId}/payments/{empleadoId}/revert", new { Reason = motivo }, ct);

    public Task<InvitationApiResult<ArchivoDescargado>> ComprobanteAsync(Guid corridaId, Guid empleadoId, CancellationToken ct = default) =>
        DescargarAsync($"/api/payroll/runs/{corridaId}/payslips/{empleadoId}/pdf", ct);

    public Task<InvitationApiResult<ArchivoDescargado>> TodosLosComprobantesAsync(Guid corridaId, CancellationToken ct = default) =>
        DescargarAsync($"/api/payroll/runs/{corridaId}/payslips/pdf", ct);

    public Task<InvitationApiResult<ResultadoEnvioDto>> EnviarComprobantesAsync(Guid corridaId, IReadOnlyList<Guid>? empleados, CancellationToken ct = default) =>
        EnviarAsync<ResultadoEnvioDto>(HttpMethod.Post, $"/api/payroll/runs/{corridaId}/payslips/send", new { EmployeePublicIds = empleados }, ct);

    public Task<InvitationApiResult<IReadOnlyList<EnvioComprobanteDto>>> EnviosAsync(Guid corridaId, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<EnvioComprobanteDto>>(HttpMethod.Get, $"/api/payroll/runs/{corridaId}/payslips/deliveries", null, ct);
}

public sealed record FilaRelacionDePagoDto(Guid EmployeePublicId, string EmployeeName, string Document, string? Email, decimal NetPay,
    string? BankName, string? BankAccountType, string? BankAccountNumber, bool Paid, Guid? PaymentPublicId, DateTime? PaidAt, string? PaymentMethod, string? Reference, string? PaidBy);

public sealed record RelacionDePagoDto(Guid RunPublicId, string RunStatus, int Employees, int PaidCount, decimal TotalNet, decimal PaidNet, IReadOnlyList<FilaRelacionDePagoDto> Rows);

public sealed record MarcarPagoRequest(IReadOnlyList<Guid>? EmployeePublicIds, DateTime PaidAt, string Method, string? Reference);

public sealed record DestinatarioDto(Guid EmployeePublicId, string EmployeeName);
public sealed record FalloEnvioDto(Guid EmployeePublicId, string EmployeeName, string Error);
public sealed record ResultadoEnvioDto(int Sent, int Failed, IReadOnlyList<DestinatarioDto> WithoutEmail, IReadOnlyList<FalloEnvioDto> Failures);

public sealed record EnvioComprobanteDto(Guid PublicId, Guid EmployeePublicId, string EmployeeName, string RecipientEmail, DateTime RequestedAt, string RequestedBy, DateTime? SentAt, string Status, string? ErrorMessage, int AttemptNumber);
