using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Inventory.Sales.CicloComercial;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Parameters;

namespace IngenIA365ERP.Application.Inventory.Documents.Efectos;

/// <summary>
/// <c>SalesQuote</c>, la cotización (feature 012, I6, T878; FR-052; data-model §14): lleva su vigencia (<c>ValidUntil</c>) obligatoria y no
/// anterior a su fecha (<c>Inventory.Document.FieldRequired</c> con <c>validUntil</c> o <c>Inventory.Quote.ValidUntilInvalid</c>); productos
/// activos y no bloqueados; confirmada no mueve nada ni emite mensajes, y su anulación tampoco (<see cref="RevertirAsync"/> y
/// <see cref="MensajesDeAnulacionAsync"/> heredan lo neutro). Se convierte en pedido con <c>ConvertQuoteToOrderCommand</c> mientras no venza.
/// El monto que se aprueba es su total. (nuevo)
/// <para>
/// 2026-11-12 (decisión del dueño): la cotización simula una venta para un cliente y se le imprime: confirmarla exige un cliente identificado
/// (<c>Inventory.Quote.CustomerRequired</c>; el consumidor final no sirve), y el borrador lo avisa. Su precio y sus cálculos son los de la venta
/// (<see cref="BorradorDeVenta"/> la precifica como a una factura).
/// </para>
/// <para>
/// 2026-11-15 (pedido del dueño): exigir el cliente es un parámetro, <c>Comercial.CotizacionExigeCliente</c>, que por defecto no lo exige: la
/// cotización puede salir para el consumidor final y la venta que la carga toma el cliente que se le asigne. Sin lector (pruebas que no lo
/// montan) rige el defecto.
/// </para>
/// </summary>
public sealed class EfectoDeCotizacion(IMaestrosDelDocumento maestros, IApplicationDbContext db, ILectorDeParametros? parametros = null) : EfectoDeClaseBase
{
    public const string CampoVigencia = "validUntil";

    public override DocumentClass Clase => DocumentClass.SalesQuote;

    public override decimal MontoParaAprobar(ContextoDeEfecto contexto) => contexto.Documento.Total;

    public override async Task<IReadOnlyList<Error>> AvisosDelBorradorAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var avisos = new List<Error>();
        if (Vigencia(contexto) is { } vigencia) avisos.Add(vigencia);
        if (await ExigeClienteAsync(contexto, ct) && await SinClienteAsync(contexto, ct)) avisos.Add(ErroresDelCicloComercial.QuoteCustomerRequired());
        return avisos;
    }

    public override async Task<Result> ValidarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        if (contexto.EsAnulacion) return Result.Success();
        if (Vigencia(contexto) is { } error) return Result.Failure(error);
        if (await ExigeClienteAsync(contexto, ct) && await SinClienteAsync(contexto, ct)) return Result.Failure(ErroresDelCicloComercial.QuoteCustomerRequired());
        var productos = await ReglasDeLineasDeVenta.ProductosAsync(contexto.Documento, maestros, ct);
        return productos.IsFailure ? Result.Failure(productos.Error) : Result.Success();
    }

    private static Error? Vigencia(ContextoDeEfecto contexto)
    {
        var documento = contexto.Documento;
        if (documento.ValidUntil is not { } vence) return InventoryErrors.FieldRequired(CampoVigencia);
        return vence < documento.OperationDate ? ErroresDelCicloComercial.QuoteValidUntilInvalid(vence, documento.OperationDate) : null;
    }

    /// <summary>¿La cooperativa exige el cliente a la fecha de la cotización? Sin parámetro registrado, no.</summary>
    private async Task<bool> ExigeClienteAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        if (parametros is null) return false;
        var leido = await parametros.LeerComoAsync<bool>(ParametrosDeInventario.Modulo, ParametrosDeInventario.ComercialCotizacionExigeCliente,
            contexto.Documento.OperationDate, ct: ct);
        return leido.IsSuccess && leido.Value;
    }

    /// <summary>Sin contraparte o con la del consumidor final (la que pone el borrador cuando no se elige cliente).</summary>
    private Task<bool> SinClienteAsync(ContextoDeEfecto contexto, CancellationToken ct) =>
        BorradorDeVenta.EsConsumidorFinalAsync(db, contexto.Documento, ct);
}
