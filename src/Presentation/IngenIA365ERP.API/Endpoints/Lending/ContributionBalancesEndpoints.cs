using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Lending.Imports;
using IngenIA365ERP.Application.Lending.Origination.CreditLimits;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Los saldos de aportes y ahorro permanente por plantilla (feature 013, US2, T0408; contracts/api.md §7.4, plantillas.md §3, D19): la lista
/// con la última carga de cada persona con corte ≤ <c>asOf</c> (<c>Lending.CreditLimits.Read</c>) y la plantilla
/// <c>lending.contribution-balances</c> por <c>RutasDePlantilla.MapPlantilla</c> (la vacía con <c>.Read</c>; importar con
/// <c>Lending.CreditLimits.Assign</c> (C85), <c>Idempotency-Key</c> y todo o nada; sin descarga con datos en K1). Es provisional mientras
/// Aportes no responda <c>IConsultaDeAportes</c>. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class ContributionBalancesEndpoints : ICarterModule
{
    public const string Leer = CreditLimitsEndpoints.Leer;
    public const string Importar = CreditLimitsEndpoints.Asignar;

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var saldos = LendingGroup.Grupo(app, "/contribution-balances", "Cartera Saldos de aportes");

        saldos.MapGet("/", async (Guid? personPublicId, DateOnly? asOf, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListContributionBalancesQuery(personPublicId, asOf, page ?? 1, pageSize ?? 20), ct))
            .WithName("Lending_ContributionBalances_List")
            .RequirePermission(Leer);

        // La plantilla 3 (plantillas.md §3): GET /template.xlsx y POST /import?mode=review|apply, con Idempotency-Key.
        saldos.MapPlantilla(
            () => CatalogoDePlantillasDeCartera.SaldosDeAportes,
            "plantilla-cartera-saldos-de-aportes",
            Leer,
            Importar,
            "Lending_ContributionBalances",
            "Lending.Reports.ExportPersonalData",
            AuditEventTypes.LendingReportExported,
            importar: (modo, archivo, motivo, clave) =>
                new ImportContributionBalancesCommand(modo, archivo, motivo ?? string.Empty) { OperationKey = clave });
    }
}
