using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;

namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// El catálogo agregado de mensajes (feature 013, T0068; T10; contracts/mensajes.md §1, §2): une <see cref="CatalogoDeMensajesV1"/>
/// (Comercial, sin renombrar ni mover), <see cref="CatalogoDeMensajesDeCarteraV1"/> (Cartera) y
/// <see cref="CatalogoDeMensajesHaciaCarteraV1"/> (Tesorería y Recaudo) y, desde la feature 015, <see cref="CatalogoDeMensajesDeRecaudoV1"/> (los hechos de
/// Recaudo a Contabilidad). Es lo único que dice qué módulo emite qué tipo, a qué
/// destinos y si el proceso puede originarlo: el emisor, <c>MensajesEntrantes</c> y las versiones aceptadas lo leen a él. Lo
/// vigila <c>ElCatalogoDeMensajesEsUnico</c> (<c>(Type, Version)</c> único, record único, módulo declarado). (nuevo)
/// </summary>
public static class CatalogoDeMensajes
{
    public static IReadOnlyList<TipoDeMensaje> Todos { get; } =
        [.. CatalogoDeMensajesV1.Todos, .. CatalogoDeMensajesDeCarteraV1.Todos, .. CatalogoDeMensajesHaciaCarteraV1.Todos,
         .. CatalogoDeMensajesDeRecaudoV1.Todos, .. CatalogoDeMensajesDeTesoreriaV1.Todos];

    private static readonly Dictionary<Type, TipoDeMensaje> PorRecord = Todos.ToDictionary(t => t.Record);

    private static readonly Dictionary<(string Type, int Version), TipoDeMensaje> PorTipoYVersion =
        Todos.ToDictionary(t => (t.Type, t.Version));

    /// <summary>El tipo del record, o nulo si no es un mensaje del catálogo.</summary>
    public static TipoDeMensaje? Buscar(Type record) => PorRecord.GetValueOrDefault(record);

    /// <summary>La entrada de ese tipo y versión, o nula si no está.</summary>
    public static TipoDeMensaje? Buscar(string type, int version) => PorTipoYVersion.GetValueOrDefault((type, version));

    /// <summary>Los tipos que emite un módulo (<see cref="ModulosDeOrigen"/>).</summary>
    public static IEnumerable<TipoDeMensaje> DelModulo(string modulo) => Todos.Where(t => t.OriginModule == modulo);
}
