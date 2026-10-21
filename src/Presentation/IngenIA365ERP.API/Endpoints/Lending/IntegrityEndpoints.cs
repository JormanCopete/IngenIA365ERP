using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.Application.Lending.Ledger;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// La integridad del libro de Cartera (feature 013, US4, T0506; contracts/api.md §16.5; T33): <c>POST /integrity/verify</c> compara, crédito por
/// crédito y día por día hasta su marca (o hasta <c>upTo</c>), el libro con la reconstrucción, sobre los créditos pedidos, una muestra o todos
/// (<c>Lending.Integrity.Verify</c>). Es una consulta por POST: <b>sin clave</b> (api.md §1.5), no corrige nada, y una diferencia levanta
/// <c>Cartera.LibroDescuadrado</c>. La ruta sólo reenvía al <see cref="ISender"/>. (nuevo)
/// </summary>
public class IntegrityEndpoints : ICarterModule
{
    public const string Verificar = "Lending.Integrity.Verify";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var integridad = LendingGroup.Grupo(app, "/integrity", "Cartera Integridad");

        integridad.MapPost("/verify", async (VerificacionRequest? body, ISender sender, CancellationToken ct) =>
            {
                var pedido = body ?? new VerificacionRequest();
                return await sender.Send(new VerifyLendingLedgerIntegrityQuery(pedido.LoanPublicIds, pedido.SampleSize, pedido.UpTo), ct);
            })
            .WithName("Lending_Integrity_Verify")
            .RequirePermission(Verificar);
    }

    /// <summary><c>{ loanPublicIds?[], sampleSize?, upTo? }</c> (§16.5).</summary>
    public sealed record VerificacionRequest(IReadOnlyList<Guid>? LoanPublicIds = null, int? SampleSize = null, DateOnly? UpTo = null);
}
