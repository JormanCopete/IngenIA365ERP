using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Integration.Queries;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// Los hechos contables de Cartera y su conciliación vistos desde Cartera (feature 013, US7, T0662; contracts/api.md §16.3; DC-16, T58):
/// <c>GET /api/lending/accounting/facts</c> y <c>/fact-parts/{id}</c> con <c>Lending.Messages.Read</c> —nunca un <c>*.View</c>: la lista por
/// crédito lleva el tercero (§2.14)— y <c>GET /api/lending/accounting/reconciliation?date=&amp;branch=</c> con <c>Lending.Reconciliation.View</c>.
/// Todo dentro del grupo de Cartera (<see cref="LendingGroup"/>, que audita el intento denegado como <c>Lending.Access.Denied</c>) y con el
/// alcance de oficinas de quien pregunta; Cartera no lee tablas <c>ACC_</c>: el estado y el comprobante salen de la entrega. (nuevo)
/// </summary>
public class AccountingIntegrationEndpoints : ICarterModule
{
    public const string LeerMensajes = "Lending.Messages.Read";
    public const string VerConciliacion = "Lending.Reconciliation.View";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var g = LendingGroup.Grupo(app, "/accounting", "Cartera Integración contable");

        g.MapGet("/facts", async (string? type, DeliveryStatus? status, DateOnly? from, DateOnly? to, Guid? branch, Guid? loanPublicId, int? page,
                int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new ListLendingAccountingFactsQuery(type, status, from, to, branch, loanPublicId, page ?? 1, pageSize ?? 50), ct))
            .WithName("Lending_Accounting_Facts_List")
            .RequirePermission(LeerMensajes);

        g.MapGet("/fact-parts/{id:guid}", async (Guid id, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetAccountingFactPartQuery(id, page ?? 1, pageSize ?? 100), ct))
            .WithName("Lending_Accounting_FactParts_Get")
            .RequirePermission(LeerMensajes);

        g.MapGet("/reconciliation", async (DateOnly? date, Guid? branch, ISender sender, IDateTimeService reloj, CancellationToken ct) =>
                await sender.Send(new GetLendingAccountingReconciliationQuery(date ?? reloj.HoyLocal, branch), ct))
            .WithName("Lending_Accounting_Reconciliation")
            .RequirePermission(VerConciliacion);
    }
}
