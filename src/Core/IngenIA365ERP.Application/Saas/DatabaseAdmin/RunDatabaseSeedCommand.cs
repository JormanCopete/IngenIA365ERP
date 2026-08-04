using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces.Database;
using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Saas.DatabaseAdmin;

/// <summary>
/// Feature 004 (T042) — seeding bajo demanda desde la consola master
/// (contracts/database-admin.md, FR-019). Pasa por los 4 pipeline behaviors:
/// la ejecucion queda auditada en Mongo via AuditBehavior
/// (<c>Database.Seed.Executed</c>).
/// </summary>
public sealed record RunDatabaseSeedCommand(
    string Category,                 // "Parametric" | "Test"
    string Scope,                    // "Admin" | "Tenant" | "All"
    Guid? TenantPublicId,
    bool ConfirmTestSeed
) : IRequest<Result<RunDatabaseSeedResult>>;

public sealed record RunDatabaseSeedResult(IReadOnlyList<SeedRunEntry> SeedersRun, long DurationMs);

public sealed class RunDatabaseSeedCommandValidator : AbstractValidator<RunDatabaseSeedCommand>
{
    private static readonly string[] Categories = ["Parametric", "Test"];
    private static readonly string[] Scopes = ["Admin", "Tenant", "All"];

    public RunDatabaseSeedCommandValidator()
    {
        RuleFor(x => x.Category)
            .Must(c => Categories.Contains(c, StringComparer.OrdinalIgnoreCase))
            .WithErrorCode("Database.Seed.InvalidCategory")
            .WithMessage("Categoría inválida. Valores válidos: Parametric, Test.");

        RuleFor(x => x.Scope)
            .Must(s => Scopes.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithErrorCode("Database.Seed.InvalidScope")
            .WithMessage("Alcance inválido. Valores válidos: Admin, Tenant, All.");

        RuleFor(x => x.TenantPublicId)
            .Null().When(x => x.Scope.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            .WithErrorCode("Database.Seed.InvalidScope")
            .WithMessage("TenantPublicId solo aplica con alcance Tenant.");
    }
}

public sealed class RunDatabaseSeedCommandHandler(IDataSeedRunner runner)
    : IRequestHandler<RunDatabaseSeedCommand, Result<RunDatabaseSeedResult>>
{
    public async Task<Result<RunDatabaseSeedResult>> Handle(RunDatabaseSeedCommand request, CancellationToken ct)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var scope = request.Scope.Equals("All", StringComparison.OrdinalIgnoreCase) ? null : request.Scope;
            var entries = await runner.RunAsync(
                request.Category, scope, request.TenantPublicId, request.ConfirmTestSeed, ct);
            return Result.Success(new RunDatabaseSeedResult(entries, started.ElapsedMilliseconds));
        }
        catch (SeedRunException ex)
        {
            return Result.Failure<RunDatabaseSeedResult>(ex.Code, ex.Message);
        }
    }
}
