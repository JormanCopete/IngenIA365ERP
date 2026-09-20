using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Reports;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Interfaces;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Reports;

/// <summary>
/// Consultas e informes contables (feature 009 E2, contracts/api.md §8): una ruta por vista bajo
/// <c>/api/reports/accounting/{vista}</c>, todas con los mismos filtros combinables en la query
/// string (<see cref="FiltrosDeInforme"/>) y <c>format=json|xlsx|pdf|docx</c>. La pantalla pide
/// <c>json</c> con <c>Accounting.Reports.View</c>; un archivo exige además
/// <c>Accounting.Reports.Export</c>, que sólo se mira cuando <c>format</c> lo pide
/// (<see cref="PermissionAuthorizationExtensions.RequirePermissionWhenExporting"/>). La entrega
/// —tabla o archivo, error con el sobre— es la misma que la de nómina: <see cref="EntregaDeInformes"/>.
/// La auditoría de la exportación la emite cada handler, que es quien sabe cuántas filas salieron.
/// </summary>
public class AccountingReportsEndpoints : ICarterModule
{
    /// <summary>
    /// Los filtros tal como llegan por la query string (camelCase, mismos nombres que
    /// <see cref="FiltrosDeInforme"/>). Es una clase con <c>set</c> porque <c>[AsParameters]</c>
    /// exige un tipo que pueda poblar propiedad a propiedad; el record inmutable de Application
    /// se arma después con <see cref="ToFiltros"/>.
    /// </summary>
    public sealed class FiltrosQuery
    {
        public DateOnly? From { get; set; }
        public DateOnly? To { get; set; }
        public string? AccountFrom { get; set; }
        public string? AccountTo { get; set; }
        public Guid? AccountPublicId { get; set; }
        public Guid? Person { get; set; }
        public Guid? CostCenter { get; set; }
        public Guid? Branch { get; set; }
        public string? CrossDocument { get; set; }
        public string? VoucherType { get; set; }
        public string? Origin { get; set; }
        public string? User { get; set; }
        public int? Level { get; set; }
        public bool? WithThirdParties { get; set; }
        public bool? IncludeClosing { get; set; }
        public string? Format { get; set; }

        public FiltrosDeInforme ToFiltros() => new()
        {
            From = From,
            To = To,
            AccountFrom = AccountFrom,
            AccountTo = AccountTo,
            AccountPublicId = AccountPublicId,
            Person = Person,
            CrossDocument = CrossDocument,
            CostCenter = CostCenter,
            Branch = Branch,
            VoucherType = VoucherType,
            Origin = Origin,
            User = User,
            Level = Level,
            WithThirdParties = WithThirdParties ?? false,
            IncludeClosing = IncludeClosing ?? false,
            Format = Format,
        };
    }

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports/accounting")
            .WithTags("Accounting Reports")
            .RequireAuthorization();

        // ---- Libros (US5) ----
        group.MapGet("/trial-balance", async ([AsParameters] FiltrosQuery q, ISender sender, CancellationToken ct) =>
                await EntregaDeInformes.EntregarAsync(await sender.Send(new TrialBalanceQuery(q.ToFiltros()), ct), q.Format, "balance-de-prueba"))
            .WithName("Reportes_Contabilidad_TrialBalance")
            .RequirePermission("Accounting.Reports.View")
            .RequirePermissionWhenExporting("Accounting.Reports.Export");

        // El libro auxiliar interactivo: `node` (tipo:clave) dice desde dónde se profundiza; sin él, las clases.
        group.MapGet("/ledger", async ([AsParameters] FiltrosQuery q, string? node, ISender sender, CancellationToken ct) =>
                await EntregaDeInformes.EntregarAsync(await sender.Send(new LedgerQuery(q.ToFiltros(), node), ct), q.Format, "libro-auxiliar"))
            .WithName("Reportes_Contabilidad_Ledger")
            .RequirePermission("Accounting.Reports.View")
            .RequirePermissionWhenExporting("Accounting.Reports.Export");

        group.MapGet("/journal", async ([AsParameters] FiltrosQuery q, ISender sender, CancellationToken ct) =>
                await EntregaDeInformes.EntregarAsync(await sender.Send(new JournalQuery(q.ToFiltros()), ct), q.Format, "libro-diario"))
            .WithName("Reportes_Contabilidad_Journal")
            .RequirePermission("Accounting.Reports.View")
            .RequirePermissionWhenExporting("Accounting.Reports.Export");

        group.MapGet("/general-ledger", async ([AsParameters] FiltrosQuery q, ISender sender, CancellationToken ct) =>
                await EntregaDeInformes.EntregarAsync(await sender.Send(new GeneralLedgerQuery(q.ToFiltros()), ct), q.Format, "libro-mayor-y-balances"))
            .WithName("Reportes_Contabilidad_GeneralLedger")
            .RequirePermission("Accounting.Reports.View")
            .RequirePermissionWhenExporting("Accounting.Reports.Export");

        group.MapGet("/voucher-list", async ([AsParameters] FiltrosQuery q, ISender sender, CancellationToken ct) =>
                await EntregaDeInformes.EntregarAsync(await sender.Send(new VoucherListQuery(q.ToFiltros()), ct), q.Format, "relacion-de-comprobantes"))
            .WithName("Reportes_Contabilidad_VoucherList")
            .RequirePermission("Accounting.Reports.View")
            .RequirePermissionWhenExporting("Accounting.Reports.Export");

        // ---- Terceros y documentos cruce ----
        group.MapGet("/third-party-statement", async ([AsParameters] FiltrosQuery q, ISender sender, CancellationToken ct) =>
                await EntregaDeInformes.EntregarAsync(await sender.Send(new ThirdPartyStatementQuery(q.ToFiltros()), ct), q.Format, "estado-de-cuenta-tercero"))
            .WithName("Reportes_Contabilidad_ThirdPartyStatement")
            .RequirePermission("Accounting.Reports.View")
            .RequirePermissionWhenExporting("Accounting.Reports.Export");

        group.MapGet("/pending-documents", async ([AsParameters] FiltrosQuery q, ISender sender, CancellationToken ct) =>
                await EntregaDeInformes.EntregarAsync(await sender.Send(new PendingDocumentsQuery(q.ToFiltros()), ct), q.Format, "documentos-pendientes"))
            .WithName("Reportes_Contabilidad_PendingDocuments")
            .RequirePermission("Accounting.Reports.View")
            .RequirePermissionWhenExporting("Accounting.Reports.Export");

        group.MapGet("/daily-average", async ([AsParameters] FiltrosQuery q, ISender sender, CancellationToken ct) =>
                await EntregaDeInformes.EntregarAsync(await sender.Send(new DailyAverageBalanceQuery(q.ToFiltros()), ct), q.Format, "saldo-diario-promedio"))
            .WithName("Reportes_Contabilidad_DailyAverage")
            .RequirePermission("Accounting.Reports.View")
            .RequirePermissionWhenExporting("Accounting.Reports.Export");

        // ---- Estados financieros (FR-047) ----
        group.MapGet("/financial-position", async ([AsParameters] FiltrosQuery q, ISender sender, CancellationToken ct) =>
                await EntregaDeInformes.EntregarAsync(await sender.Send(new FinancialPositionQuery(q.ToFiltros()), ct), q.Format, "estado-de-situacion-financiera"))
            .WithName("Reportes_Contabilidad_FinancialPosition")
            .RequirePermission("Accounting.Reports.View")
            .RequirePermissionWhenExporting("Accounting.Reports.Export");

        group.MapGet("/income-statement", async ([AsParameters] FiltrosQuery q, ISender sender, CancellationToken ct) =>
                await EntregaDeInformes.EntregarAsync(await sender.Send(new IncomeStatementQuery(q.ToFiltros()), ct), q.Format, "estado-de-resultados"))
            .WithName("Reportes_Contabilidad_IncomeStatement")
            .RequirePermission("Accounting.Reports.View")
            .RequirePermissionWhenExporting("Accounting.Reports.Export");

        group.MapGet("/equity-changes", async ([AsParameters] FiltrosQuery q, ISender sender, CancellationToken ct) =>
                await EntregaDeInformes.EntregarAsync(await sender.Send(new EquityChangesQuery(q.ToFiltros()), ct), q.Format, "cambios-en-el-patrimonio"))
            .WithName("Reportes_Contabilidad_EquityChanges")
            .RequirePermission("Accounting.Reports.View")
            .RequirePermissionWhenExporting("Accounting.Reports.Export");

        group.MapGet("/cash-flow", async ([AsParameters] FiltrosQuery q, ISender sender, CancellationToken ct) =>
                await EntregaDeInformes.EntregarAsync(await sender.Send(new CashFlowQuery(q.ToFiltros()), ct), q.Format, "flujo-de-efectivo"))
            .WithName("Reportes_Contabilidad_CashFlow")
            .RequirePermission("Accounting.Reports.View")
            .RequirePermissionWhenExporting("Accounting.Reports.Export");

        // ---- Ejecución presupuestal (US9) ----
        // `year` y `month` son propios de esta vista (contracts/api.md §10). Si no vienen, el mes
        // en curso según el reloj de la aplicación; el «último cerrado» lo decide quien consulta.
        group.MapGet("/budget-execution", async ([AsParameters] FiltrosQuery q, int? year, int? month, ISender sender, IDateTimeService clock, CancellationToken ct) =>
            {
                var hoy = DateOnly.FromDateTime(clock.UtcNow);
                var consulta = new BudgetExecutionQuery(year ?? hoy.Year, month ?? hoy.Month, q.ToFiltros());
                return await EntregaDeInformes.EntregarAsync(await sender.Send(consulta, ct), q.Format, "ejecucion-presupuestal");
            })
            .WithName("Reportes_Contabilidad_BudgetExecution")
            .RequirePermission("Accounting.Reports.View")
            .RequirePermissionWhenExporting("Accounting.Reports.Export");
    }
}
