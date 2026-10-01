using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

/// <summary>
/// Un tipo de mensaje del catálogo: su record, la cadena <see cref="Type"/> que se guarda, la versión, el destino y
/// el <see cref="Kind"/>. <see cref="Kind"/> nulo significa «el del original» (sólo <c>DocumentoAnulado</c>).
/// <see cref="FormaDeClave"/> dice qué <c>originEventKey</c> admite (contracts/mensajes.md §10.1).
/// </summary>
public sealed record TipoDeMensaje(
    Type Record,
    string Type,
    int Version,
    string Destination,
    IntegrationMessageKind? Kind,
    FormaDeClaveDeEvento FormaDeClave);

/// <summary>Las cinco formas de <c>originEventKey</c> de contracts/mensajes.md §10.1.</summary>
public enum FormaDeClaveDeEvento
{
    /// <summary><c>Confirmation</c>.</summary>
    Confirmacion = 1,

    /// <summary><c>Confirmation:{publicId:N}</c>: documento afectado o pago de crédito.</summary>
    ConfirmacionPorPublicId = 2,

    /// <summary><c>Close:{closingVersion}</c>.</summary>
    Cierre = 3,

    /// <summary><c>Reopen:{closingVersion}</c>.</summary>
    Reapertura = 4,

    /// <summary><c>Reclassification</c>.</summary>
    Reclasificacion = 5,
}

/// <summary>
/// Los veinte tipos de contracts/mensajes.md §1, en su orden (feature 012, T8, T075–T077). Es lo único que dice a
/// qué destino va un record y con qué <c>Kind</c>: <c>EmisorDeMensajes</c> no emite un record que no esté aquí.
/// Un tipo nuevo o una V2 se agrega aquí junto con su record (§12).
/// </summary>
public static class CatalogoDeMensajesV1
{
    private const string A = IntegrationDestinations.Accounting;
    private const string L = IntegrationDestinations.Lending;
    private const IntegrationMessageKind N = IntegrationMessageKind.Business;
    private const IntegrationMessageKind I = IntegrationMessageKind.Informational;

    public static IReadOnlyList<TipoDeMensaje> Todos { get; } =
    [
        new(typeof(VentaFacturadaV1), VentaFacturadaV1.Type, 1, A, N, FormaDeClaveDeEvento.Confirmacion),
        new(typeof(CostoDeVentaReconocidoV1), CostoDeVentaReconocidoV1.Type, 1, A, N, FormaDeClaveDeEvento.Confirmacion),
        new(typeof(CompraRecibidaV1), CompraRecibidaV1.Type, 1, A, N, FormaDeClaveDeEvento.Confirmacion),
        new(typeof(FacturaProveedorRegistradaV1), FacturaProveedorRegistradaV1.Type, 1, A, N, FormaDeClaveDeEvento.Confirmacion),
        new(typeof(AjusteInventarioAprobadoV1), AjusteInventarioAprobadoV1.Type, 1, A, N, FormaDeClaveDeEvento.Confirmacion),
        new(typeof(TrasladoDespachadoV1), TrasladoDespachadoV1.Type, 1, A, N, FormaDeClaveDeEvento.Confirmacion),
        new(typeof(TrasladoRecibidoV1), TrasladoRecibidoV1.Type, 1, A, N, FormaDeClaveDeEvento.Confirmacion),
        new(typeof(DevolucionRegistradaV1), DevolucionRegistradaV1.Type, 1, A, N, FormaDeClaveDeEvento.Confirmacion),
        new(typeof(DocumentoAnuladoV1), DocumentoAnuladoV1.Type, 1, A, null, FormaDeClaveDeEvento.Confirmacion),
        new(typeof(AjusteDeCostoReconocidoV1), AjusteDeCostoReconocidoV1.Type, 1, A, N, FormaDeClaveDeEvento.ConfirmacionPorPublicId),
        new(typeof(NotaCreditoEmitidaV1), NotaCreditoEmitidaV1.Type, 1, A, N, FormaDeClaveDeEvento.Confirmacion),
        new(typeof(NotaDebitoEmitidaV1), NotaDebitoEmitidaV1.Type, 1, A, N, FormaDeClaveDeEvento.Confirmacion),
        new(typeof(GrupoContableReclasificadoV1), GrupoContableReclasificadoV1.Type, 1, A, N, FormaDeClaveDeEvento.Reclasificacion),
        new(typeof(MovimientoDeCajaRegistradoV1), MovimientoDeCajaRegistradoV1.Type, 1, A, N, FormaDeClaveDeEvento.Confirmacion),
        new(typeof(DiferenciaDeArqueoAprobadaV1), DiferenciaDeArqueoAprobadaV1.Type, 1, A, N, FormaDeClaveDeEvento.Confirmacion),
        new(typeof(SaldoInicialCargadoV1), SaldoInicialCargadoV1.Type, 1, A, I, FormaDeClaveDeEvento.Confirmacion),
        new(typeof(PeriodoInventarioCerradoV1), PeriodoInventarioCerradoV1.Type, 1, A, I, FormaDeClaveDeEvento.Cierre),
        new(typeof(PeriodoInventarioReabiertoV1), PeriodoInventarioReabiertoV1.Type, 1, A, I, FormaDeClaveDeEvento.Reapertura),
        new(typeof(VentaACreditoRegistradaV1), VentaACreditoRegistradaV1.Type, 1, L, N, FormaDeClaveDeEvento.ConfirmacionPorPublicId),
        new(typeof(AjusteDeVentaACreditoV1), AjusteDeVentaACreditoV1.Type, 1, L, N, FormaDeClaveDeEvento.ConfirmacionPorPublicId),
    ];

    private static readonly Dictionary<Type, TipoDeMensaje> PorRecord = Todos.ToDictionary(t => t.Record);

    /// <summary>El tipo del record, o nulo si no es un mensaje del catálogo.</summary>
    public static TipoDeMensaje? Buscar(Type record) => PorRecord.GetValueOrDefault(record);
}
