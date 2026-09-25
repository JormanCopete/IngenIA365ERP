using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;

namespace IngenIA365ERP.Application.Inventory.Documents.Efectos;

/// <summary>
/// Lo que el ciclo común le pasa a la estrategia de una clase (feature 012, T17, T142). (nuevo)
/// </summary>
/// <param name="Documento">El documento, con sus líneas, seguido por el contexto. En una anulación es el <c>Voiding</c>.</param>
/// <param name="Tipo">Su tipo de documento.</param>
/// <param name="Clase">La descripción fija de la clase del documento (<see cref="ClasesDeDocumento"/>).</param>
/// <param name="Original">En una anulación, el documento anulado (con sus líneas); nulo en los demás.</param>
public sealed record ContextoDeEfecto(
    InventoryDocument Documento,
    InventoryDocumentType Tipo,
    DescripcionDeClase Clase,
    InventoryDocument? Original = null)
{
    /// <summary>¿Es la confirmación de una anulación?</summary>
    public bool EsAnulacion => Original is not null;
}

/// <summary>
/// La estrategia de una clase de documento (feature 012, T17, T142; decisiones-transversales §1.3). El ciclo común
/// (<c>SaveInventoryDraftCommand</c>, <c>ConfirmInventoryDocumentCommand</c>, <c>VoidInventoryDocumentCommand</c>) hace
/// lo que comparten las 34 clases —relectura, alcance, fechas, período, campos del tipo, aprobación, cerrojo, número,
/// mensajes, un solo guardado— y le pregunta a la estrategia de la clase lo que es suyo:
/// <list type="bullet">
/// <item>los avisos del borrador que sólo ella conoce (<see cref="AvisosDelBorradorAsync"/>);</item>
/// <item>sus reglas antes de la aprobación (<see cref="ValidarAsync"/>) y el monto que se aprueba (<see cref="MontoParaAprobar"/>);</item>
/// <item>qué bloquear (<see cref="Cerrojo"/>) y el efecto dentro del cerrojo, con <c>RegistroDeKardex</c>
/// (<see cref="AplicarAsync"/>);</item>
/// <item>los contenidos de sus mensajes (<see cref="MensajesAsync"/>);</item>
/// <item>y, para la anulación de un documento de su clase, revertir y los mensajes del contrario
/// (<see cref="RevertirAsync"/>, <see cref="MensajesDeAnulacionAsync"/>).</item>
/// </list>
/// Una clase sin estrategia registrada no se opera: <c>Inventory.DocumentClass.NotAvailable</c>
/// (<see cref="EfectosDeClase"/>). Las estrategias concretas las escribe la historia de cada clase (US2 ajustes, US4 saldo
/// inicial, US9 compras, US10 traslados, US11 conteos…); ninguna llama a <c>SaveChanges</c>. (nuevo)
/// </summary>
public interface IEfectoDeClase
{
    /// <summary>La clase que atiende (una estrategia por clase).</summary>
    DocumentClass Clase { get; }

    /// <summary>
    /// Al guardar el borrador: lo que hoy impediría confirmarlo y sólo la clase sabe (existencia, costo digitado…). Vuelve
    /// en <c>warnings[]</c> con el mismo código que daría la confirmación; nunca bloquea el guardado.
    /// </summary>
    Task<IReadOnlyList<Error>> AvisosDelBorradorAsync(ContextoDeEfecto contexto, CancellationToken ct);

    /// <summary>Paso 2 del flujo canónico: las reglas propias de la clase, antes de la aprobación y fuera del cerrojo.</summary>
    Task<Result> ValidarAsync(ContextoDeEfecto contexto, CancellationToken ct);

    /// <summary>
    /// El monto que evalúan la política y el monto máximo (contracts/api.md §1.3): el <c>Total</c> en compras, el valor al
    /// costo en ajustes, traslados y saldo inicial.
    /// </summary>
    decimal MontoParaAprobar(ContextoDeEfecto contexto);

    /// <summary>Lo que la confirmación bloquea (pasos 1 a 4 del orden canónico; la numeración la bloquea el numerador).</summary>
    PedidoDeCerrojo Cerrojo(ContextoDeEfecto contexto);

    /// <summary>
    /// Paso 7, dentro del cerrojo: costeo y kardex por <c>RegistroDeKardex</c>, disponibilidad; escribe
    /// <c>UnitCost</c>/<c>TotalCost</c> de las salidas y la ubicación por defecto. Sin guardar.
    /// </summary>
    Task<Result> AplicarAsync(ContextoDeEfecto contexto, CancellationToken ct);

    /// <summary>Paso 9: los contenidos <c>*V1</c> que emite la confirmación (vacío = no emite).</summary>
    Task<IReadOnlyList<object>> MensajesAsync(ContextoDeEfecto contexto, CancellationToken ct);

    /// <summary>
    /// La anulación de un documento de esta clase, dentro del cerrojo: revierte sus cantidades al costo del original
    /// (<c>ReversionDeKardex</c>). <see cref="ContextoDeEfecto.Original"/> es el anulado. Sin guardar.
    /// </summary>
    Task<Result> RevertirAsync(ContextoDeEfecto contexto, CancellationToken ct);

    /// <summary>Los contenidos de la anulación (<c>DocumentoAnulado</c>, ajustes de costo), vacío si no emite.</summary>
    Task<IReadOnlyList<object>> MensajesDeAnulacionAsync(ContextoDeEfecto contexto, CancellationToken ct);
}

/// <summary>
/// Base de las estrategias con el comportamiento neutro: sin avisos ni reglas propias, el monto al costo, bloquea la
/// bodega de origen y la de destino, no mueve nada y no emite. Cada historia sobrescribe lo suyo. (nuevo)
/// </summary>
public abstract class EfectoDeClaseBase : IEfectoDeClase
{
    public abstract DocumentClass Clase { get; }

    public virtual Task<IReadOnlyList<Error>> AvisosDelBorradorAsync(ContextoDeEfecto contexto, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Error>>([]);

    public virtual Task<Result> ValidarAsync(ContextoDeEfecto contexto, CancellationToken ct) => Task.FromResult(Result.Success());

    public virtual decimal MontoParaAprobar(ContextoDeEfecto contexto) => contexto.Documento.CostTotal;

    public virtual PedidoDeCerrojo Cerrojo(ContextoDeEfecto contexto)
    {
        var bodegas = new List<int>();
        if (contexto.Documento.WarehouseId is int origen) bodegas.Add(origen);
        if (contexto.Documento.DestinationWarehouseId is int destino) bodegas.Add(destino);
        if (contexto.Documento.TransitWarehouseId is int transito) bodegas.Add(transito);
        return new PedidoDeCerrojo { Bodegas = bodegas };
    }

    public virtual Task<Result> AplicarAsync(ContextoDeEfecto contexto, CancellationToken ct) => Task.FromResult(Result.Success());

    public virtual Task<IReadOnlyList<object>> MensajesAsync(ContextoDeEfecto contexto, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<object>>([]);

    public virtual Task<Result> RevertirAsync(ContextoDeEfecto contexto, CancellationToken ct) => Task.FromResult(Result.Success());

    public virtual Task<IReadOnlyList<object>> MensajesDeAnulacionAsync(ContextoDeEfecto contexto, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<object>>([]);
}
