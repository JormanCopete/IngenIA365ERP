using Carter;
using IngenIA365ERP.API.Endpoints.Common;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Lending.Imports;
using IngenIA365ERP.Application.Lending.Payroll.Agreements;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Las pagadurías y sus convenios de libranza (feature 013, K2, US10, T0850; contracts/api.md §13.1; FR-108, I13, C84): lista, detalle con versiones,
/// contactos y códigos de respuesta, alta con la primera versión, versión nueva (cierra la anterior la víspera), contactos y la plantilla
/// <c>lending.payroll-agreements</c> por <c>RutasDePlantilla.MapPlantilla</c>. Leer exige <c>Lending.PayrollAgreements.View</c>; escribir,
/// <c>.Manage</c> con <c>Idempotency-Key</c>. Los pendientes de giro de un convenio son de créditos y van bajo
/// <c>Lending.PayrollDeductionFiles.Read</c>, con el alcance de quien pregunta. Cada ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class PayrollAgreementsEndpoints : ICarterModule
{
    public const string Ver = "Lending.PayrollAgreements.View";
    public const string Administrar = "Lending.PayrollAgreements.Manage";
    public const string LeerArchivos = "Lending.PayrollDeductionFiles.Read";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var convenios = LendingGroup.Grupo(app, "/payroll-agreements", "Cartera Libranza");

        convenios.MapGet("/", async (bool? includeInactive, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListPayrollAgreementsQuery(includeInactive ?? false), ct))
            .WithName("Lending_PayrollAgreements_List")
            .RequirePermission(Ver);

        convenios.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetPayrollAgreementQuery(id), ct))
            .WithName("Lending_PayrollAgreements_Get")
            .RequirePermission(Ver);

        convenios.MapPost("/", async (ConvenioRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new CreatePayrollAgreementCommand(body.Code ?? string.Empty, body.Name ?? string.Empty,
                    body.PayerPersonPublicId, body.IsOwnPayroll, body.FirstVersion!)
                {
                    OperationKey = http.ClaveDeOperacion(),
                }, ct), c => $"/api/lending/payroll-agreements/{c.PublicId}"))
            .WithName("Lending_PayrollAgreements_Create")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        convenios.MapPost("/{id:guid}/versions", async (Guid id, PayrollAgreementVersionInput body, HttpContext http, ISender sender, CancellationToken ct) =>
                LendingGroup.Creado(await sender.Send(new AddPayrollAgreementVersionCommand(id, body) { OperationKey = http.ClaveDeOperacion() }, ct),
                    c => $"/api/lending/payroll-agreements/{c.PublicId}"))
            .WithName("Lending_PayrollAgreements_AddVersion")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        convenios.MapPut("/{id:guid}/contacts", async (Guid id, ContactosRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
                await sender.Send(new SetPayrollAgreementContactsCommand(id, body.Contacts ?? []) { OperationKey = http.ClaveDeOperacion() }, ct))
            .WithName("Lending_PayrollAgreements_SetContacts")
            .ConClaveDeOperacion()
            .RequirePermission(Administrar);

        convenios.MapGet("/{id:guid}/pending-transfers", async (Guid id, DateOnly? asOf, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListPendingPayrollTransfersQuery(id, asOf), ct))
            .WithName("Lending_PayrollAgreements_PendingTransfers")
            .RequirePermission(LeerArchivos);

        // La plantilla 8 (plantillas.md §8): GET /template.xlsx y POST /import?mode=review|apply, con Idempotency-Key.
        convenios.MapPlantilla(
            () => PayrollAgreementsTemplate.Definicion,
            "plantilla-cartera-convenios-de-pagaduria",
            Ver,
            Administrar,
            "Lending_PayrollAgreements",
            "Lending.Reports.ExportPersonalData",
            AuditEventTypes.LendingReportExported,
            importar: (modo, archivo, motivo, clave) =>
                new ImportPayrollAgreementsCommand(modo, archivo, motivo ?? string.Empty) { OperationKey = clave });
    }

    /// <summary>El alta (§13.1): código, nombre, la pagaduría (persona jurídica del maestro), si es la nómina propia y la primera versión.</summary>
    public sealed record ConvenioRequest(string? Code, string? Name, Guid PayerPersonPublicId, bool IsOwnPayroll, PayrollAgreementVersionInput? FirstVersion);

    /// <summary>Los contactos vigentes del convenio: reemplazan a los anteriores.</summary>
    public sealed record ContactosRequest(IReadOnlyList<PayrollAgreementContactInput>? Contacts);
}
