using System.Diagnostics;
using IngenIA365ERP.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Common.Behaviors;

public class AuditBehavior<TRequest, TResponse>(
    IAuditService auditService,
    ICurrentUserService currentUserService,
    ILogger<AuditBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // Only audit Commands (writes), not Queries (reads)
    private static readonly bool IsCommand = typeof(TRequest).Name.EndsWith("Command");

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!IsCommand)
            return await next(cancellationToken);

        var requestName = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();

        try
        {
            var response = await next(cancellationToken);
            sw.Stop();

            var (action, entityType, module) = ParseRequestName(requestName);

            await auditService.LogAsync(new AuditLogCommand
            {
                Action = action,
                EntityType = entityType,
                Module = module,
                NewValues = request,
                DurationMs = sw.ElapsedMilliseconds,
                HttpStatusCode = 200
            }, cancellationToken);

            logger.LogDebug("Audit: {RequestName} completed in {Duration}ms by user {UserId}",
                requestName, sw.ElapsedMilliseconds, currentUserService.UserId);

            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();

            await auditService.LogAsync(new AuditLogCommand
            {
                Action = "Failed",
                EntityType = requestName,
                Module = InferModuleFromNamespace(typeof(TRequest).Namespace),
                NewValues = request,
                DurationMs = sw.ElapsedMilliseconds,
                HttpStatusCode = 500,
                Metadata = new Dictionary<string, string>
                {
                    ["Error"] = ex.Message,
                    ["ExceptionType"] = ex.GetType().Name
                }
            }, cancellationToken);

            throw;
        }
    }

    private static (string Action, string EntityType, string Module) ParseRequestName(string name)
    {
        // "CreateJournalEntryCommand" → Action="Create", EntityType="JournalEntry"
        // "UpdatePersonCommand" → Action="Update", EntityType="Person"
        // "DeleteLoanPortfolioCommand" → Action="Delete", EntityType="LoanPortfolio"
        // "ProcessPayrollCommand" → Action="Process", EntityType="Payroll"

        var cleanName = name.EndsWith("Command") ? name[..^7] : name;

        string action;
        string entityType;

        if (cleanName.StartsWith("Create")) { action = "Create"; entityType = cleanName[6..]; }
        else if (cleanName.StartsWith("Update")) { action = "Update"; entityType = cleanName[6..]; }
        else if (cleanName.StartsWith("Delete")) { action = "Delete"; entityType = cleanName[6..]; }
        else if (cleanName.StartsWith("Process")) { action = "Process"; entityType = cleanName[7..]; }
        else if (cleanName.StartsWith("Approve")) { action = "Approve"; entityType = cleanName[7..]; }
        else if (cleanName.StartsWith("Reject")) { action = "Reject"; entityType = cleanName[6..]; }
        else if (cleanName.StartsWith("Close")) { action = "Close"; entityType = cleanName[5..]; }
        else { action = cleanName; entityType = cleanName; }

        var module = InferModuleFromNamespace(typeof(TRequest).Namespace);

        return (action, entityType, module);
    }

    // Feature 012 (T055): interno para que IdempotencyBehavior ponga el evento Operation.Replayed en el
    // modulo del comando. T059 lo reemplaza por ModuloDeAuditoria.Inferir, compartido con el interceptor.
    internal static string InferModuleFromNamespace(string? ns)
    {
        if (ns is null) return "Unknown";
        // Feature 009 (FR-051): el ingreso a una opción del ERP se audita como navegación, no como escritura de un módulo.
        if (ns.Contains(".Audit.RegisterOptionAccess")) return "Navigation";
        if (ns.Contains(".Accounting")) return "Accounting";
        if (ns.Contains(".Lending")) return "Lending";
        if (ns.Contains(".Payroll")) return "Payroll";
        if (ns.Contains(".Inventory")) return "Inventory";
        if (ns.Contains(".CDT")) return "CDT";
        if (ns.Contains(".Debit")) return "Debit";
        if (ns.Contains(".Treasury")) return "Treasury";
        if (ns.Contains(".Security")) return "Security";
        if (ns.Contains(".Core")) return "Core";
        return "General";
    }
}
