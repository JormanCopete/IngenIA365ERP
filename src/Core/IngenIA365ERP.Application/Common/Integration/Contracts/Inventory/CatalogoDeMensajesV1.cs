using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

/// <summary>
/// Un tipo de mensaje del catálogo (feature 012, T8; ampliado en la 013, T10, T11, C117; contracts/mensajes.md §2.4 de la 013):
/// su record, la cadena <see cref="Type"/> que se guarda, la versión, el módulo que lo emite, los destinos que admite, el
/// <see cref="Kind"/>, la forma de su <c>originEventKey</c> y si el proceso puede originarlo. <see cref="Kind"/> nulo significa
/// «el del original» (sólo <c>DocumentoAnulado</c>). Agregar un destino a <see cref="Destinos"/> o cambiar
/// <see cref="AdmiteProceso"/> es un cambio del catálogo, no del contenido: no crea versión.
/// </summary>
/// <param name="OriginModule"><c>INV</c>, <c>CAR</c>, <c>TES</c> o <c>REC</c> (<see cref="ModulosDeOrigen"/>): nunca lo dice la solicitud.</param>
/// <param name="Destinos">Valores de <c>IntegrationDestinations</c>; uno solo en todo V1. Con varios, la solicitud dice <c>Destino</c>.</param>
/// <param name="AdmiteProceso">Si puede nacer con <c>OriginActorKind = Process</c> (sólo tipos <c>CAR</c>, T11).</param>
/// <param name="CodigoCorto">
/// El <c>LND_FactTypes.ShortCode</c> de los hechos de Cartera a Contabilidad (3 o 4 letras, C06, C117), que va en su
/// <c>ScheduleKey</c>; nulo en los demás.
/// </param>
public sealed record TipoDeMensaje(
    Type Record,
    string Type,
    int Version,
    string OriginModule,
    IReadOnlySet<string> Destinos,
    IntegrationMessageKind? Kind,
    FormaDeClaveDeEvento FormaDeClave,
    bool AdmiteProceso,
    string? CodigoCorto = null)
{
    /// <summary>
    /// El destino de un tipo que admite uno solo (todos los de V1). Un tipo con varios no tiene «el» destino: la solicitud
    /// lo elige (<c>SolicitudDeEmision.Destino</c>).
    /// </summary>
    /// <exception cref="InvalidOperationException">Si el tipo admite más de un destino.</exception>
    public string Destination => Destinos.Count == 1
        ? Destinos.First()
        : throw new InvalidOperationException($"{Type} v{Version} admite {Destinos.Count} destinos: la solicitud de emisión dice cuál.");

    /// <summary>¿Admite este destino?</summary>
    public bool AdmiteDestino(string destino) => Destinos.Contains(destino);

    /// <summary>El conjunto de un solo destino, con comparación ordinal.</summary>
    public static IReadOnlySet<string> Un(string destino) => new HashSet<string>([destino], StringComparer.Ordinal);
}

/// <summary>Las cinco formas de <c>originEventKey</c> de contracts/mensajes.md §10.1, y la combinación de dos de ellas que usa Cartera.</summary>
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

    /// <summary>
    /// <c>Confirmation</c> o <c>Confirmation:{publicId:N}</c> (feature 013): los hechos de Cartera que viajan como acompañante de
    /// una operación o nacen de una parte o de un crédito de un proceso (contracts/mensajes.md §7.1 de la 013). No es una forma
    /// de clave nueva: admite las dos que ya existen.
    /// </summary>
    ConfirmacionOPorPublicId = 6,
}

/// <summary>
/// Los veinte tipos de contracts/mensajes.md §1, en su orden (feature 012, T8, T075–T077). Es lo único que dice a
/// qué destino va un record y con qué <c>Kind</c>: <c>EmisorDeMensajes</c> no emite un record que no esté aquí.
/// Un tipo nuevo o una V2 se agrega aquí junto con su record (§12). Desde la 013 es una de las tres partes del catálogo
/// agregado (<c>CatalogoDeMensajes</c>): todos sus tipos son de <c>INV</c>, van a un solo destino y no admiten proceso (T11:
/// para Inventario el mensaje lo origina la persona). Los bytes de sus contratos no cambian (<c>ContratosDeMensajesTests</c>).
/// </summary>
public static class CatalogoDeMensajesV1
{
    private const string A = IntegrationDestinations.Accounting;
    private const string L = IntegrationDestinations.Lending;
    private const IntegrationMessageKind N = IntegrationMessageKind.Business;
    private const IntegrationMessageKind I = IntegrationMessageKind.Informational;

    public static IReadOnlyList<TipoDeMensaje> Todos { get; } =
    [
        Inv(typeof(VentaFacturadaV1), VentaFacturadaV1.Type, A, N, FormaDeClaveDeEvento.Confirmacion),
        Inv(typeof(CostoDeVentaReconocidoV1), CostoDeVentaReconocidoV1.Type, A, N, FormaDeClaveDeEvento.Confirmacion),
        Inv(typeof(CompraRecibidaV1), CompraRecibidaV1.Type, A, N, FormaDeClaveDeEvento.Confirmacion),
        Inv(typeof(FacturaProveedorRegistradaV1), FacturaProveedorRegistradaV1.Type, A, N, FormaDeClaveDeEvento.Confirmacion),
        Inv(typeof(AjusteInventarioAprobadoV1), AjusteInventarioAprobadoV1.Type, A, N, FormaDeClaveDeEvento.Confirmacion),
        Inv(typeof(TrasladoDespachadoV1), TrasladoDespachadoV1.Type, A, N, FormaDeClaveDeEvento.Confirmacion),
        Inv(typeof(TrasladoRecibidoV1), TrasladoRecibidoV1.Type, A, N, FormaDeClaveDeEvento.Confirmacion),
        Inv(typeof(DevolucionRegistradaV1), DevolucionRegistradaV1.Type, A, N, FormaDeClaveDeEvento.Confirmacion),
        Inv(typeof(DocumentoAnuladoV1), DocumentoAnuladoV1.Type, A, null, FormaDeClaveDeEvento.Confirmacion),
        Inv(typeof(AjusteDeCostoReconocidoV1), AjusteDeCostoReconocidoV1.Type, A, N, FormaDeClaveDeEvento.ConfirmacionPorPublicId),
        Inv(typeof(NotaCreditoEmitidaV1), NotaCreditoEmitidaV1.Type, A, N, FormaDeClaveDeEvento.Confirmacion),
        Inv(typeof(NotaDebitoEmitidaV1), NotaDebitoEmitidaV1.Type, A, N, FormaDeClaveDeEvento.Confirmacion),
        Inv(typeof(GrupoContableReclasificadoV1), GrupoContableReclasificadoV1.Type, A, N, FormaDeClaveDeEvento.Reclasificacion),
        Inv(typeof(MovimientoDeCajaRegistradoV1), MovimientoDeCajaRegistradoV1.Type, A, N, FormaDeClaveDeEvento.Confirmacion),
        Inv(typeof(DiferenciaDeArqueoAprobadaV1), DiferenciaDeArqueoAprobadaV1.Type, A, N, FormaDeClaveDeEvento.Confirmacion),
        Inv(typeof(SaldoInicialCargadoV1), SaldoInicialCargadoV1.Type, A, I, FormaDeClaveDeEvento.Confirmacion),
        Inv(typeof(PeriodoInventarioCerradoV1), PeriodoInventarioCerradoV1.Type, A, I, FormaDeClaveDeEvento.Cierre),
        Inv(typeof(PeriodoInventarioReabiertoV1), PeriodoInventarioReabiertoV1.Type, A, I, FormaDeClaveDeEvento.Reapertura),
        Inv(typeof(VentaACreditoRegistradaV1), VentaACreditoRegistradaV1.Type, L, N, FormaDeClaveDeEvento.ConfirmacionPorPublicId),
        Inv(typeof(AjusteDeVentaACreditoV1), AjusteDeVentaACreditoV1.Type, L, N, FormaDeClaveDeEvento.ConfirmacionPorPublicId),
    ];

    /// <summary>Una entrada de Inventario: v1, módulo <c>INV</c>, un destino y sin proceso.</summary>
    private static TipoDeMensaje Inv(Type record, string type, string destino, IntegrationMessageKind? kind, FormaDeClaveDeEvento forma) =>
        new(record, type, 1, ModulosDeOrigen.Inventario, TipoDeMensaje.Un(destino), kind, forma, AdmiteProceso: false);

    private static readonly Dictionary<Type, TipoDeMensaje> PorRecord = Todos.ToDictionary(t => t.Record);

    /// <summary>El tipo del record, o nulo si no es un mensaje del catálogo.</summary>
    public static TipoDeMensaje? Buscar(Type record) => PorRecord.GetValueOrDefault(record);
}
