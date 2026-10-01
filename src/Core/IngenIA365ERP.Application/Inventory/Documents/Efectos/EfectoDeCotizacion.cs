using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Sales.CicloComercial;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Inventory.Documents.Efectos;

/// <summary>
/// <c>SalesQuote</c>, la cotización (feature 012, I6, T878; FR-052; data-model §14): lleva su vigencia (<c>ValidUntil</c>) obligatoria y no
/// anterior a su fecha (<c>Inventory.Document.FieldRequired</c> con <c>validUntil</c> o <c>Inventory.Quote.ValidUntilInvalid</c>); productos
/// activos y no bloqueados; confirmada no mueve nada ni emite mensajes, y su anulación tampoco (<see cref="RevertirAsync"/> y
/// <see cref="MensajesDeAnulacionAsync"/> heredan lo neutro). Se convierte en pedido con <c>ConvertQuoteToOrderCommand</c> mientras no venza.
/// El monto que se aprueba es su total. (nuevo)
/// </summary>
public sealed class EfectoDeCotizacion(IMaestrosDelDocumento maestros) : EfectoDeClaseBase
{
    public const string CampoVigencia = "validUntil";

    public override DocumentClass Clase => DocumentClass.SalesQuote;

    public override decimal MontoParaAprobar(ContextoDeEfecto contexto) => contexto.Documento.Total;

    public override Task<IReadOnlyList<Error>> AvisosDelBorradorAsync(ContextoDeEfecto contexto, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Error>>(Vigencia(contexto) is { } error ? [error] : []);

    public override async Task<Result> ValidarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        if (contexto.EsAnulacion) return Result.Success();
        if (Vigencia(contexto) is { } error) return Result.Failure(error);
        var productos = await ReglasDeLineasDeVenta.ProductosAsync(contexto.Documento, maestros, ct);
        return productos.IsFailure ? Result.Failure(productos.Error) : Result.Success();
    }

    private static Error? Vigencia(ContextoDeEfecto contexto)
    {
        var documento = contexto.Documento;
        if (documento.ValidUntil is not { } vence) return InventoryErrors.FieldRequired(CampoVigencia);
        return vence < documento.OperationDate ? ErroresDelCicloComercial.QuoteValidUntilInvalid(vence, documento.OperationDate) : null;
    }
}
