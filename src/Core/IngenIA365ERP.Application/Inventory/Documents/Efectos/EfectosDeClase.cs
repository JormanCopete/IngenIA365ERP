using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;

namespace IngenIA365ERP.Application.Inventory.Documents.Efectos;

/// <summary>
/// El registro de estrategias por clase (feature 012, T17, T142): cada historia registra su <see cref="IEfectoDeClase"/>
/// en el contenedor y el ciclo común la resuelve por <see cref="DocumentClass"/>. Una clase sin estrategia, o de una
/// entrega que este despliegue todavía no tiene, responde <c>Inventory.DocumentClass.NotAvailable</c>. La anulación no
/// tiene estrategia propia: la de la clase del original sabe revertirla. Dos estrategias para la misma clase son un
/// defecto de registro y fallan al construir. (nuevo)
/// </summary>
public sealed class EfectosDeClase
{
    private readonly IReadOnlyDictionary<DocumentClass, IEfectoDeClase> _porClase;

    public EfectosDeClase(IEnumerable<IEfectoDeClase> efectos)
    {
        var porClase = new Dictionary<DocumentClass, IEfectoDeClase>();
        foreach (var efecto in efectos)
        {
            if (efecto.Clase == DocumentClass.Voiding)
                throw new InvalidOperationException("La anulación no tiene estrategia propia: la revierte la estrategia de la clase del original.");
            if (!porClase.TryAdd(efecto.Clase, efecto))
                throw new InvalidOperationException($"Hay dos estrategias registradas para la clase {efecto.Clase}.");
        }
        _porClase = porClase;
    }

    /// <summary>¿Hay estrategia registrada y la clase es operable en este despliegue?</summary>
    public bool Opera(DocumentClass clase) =>
        _porClase.ContainsKey(clase) && ClasesDeDocumento.De(clase).Operable();

    /// <summary>
    /// La estrategia de <paramref name="clase"/>; para una anulación, pasar la clase del <b>original</b>. Sin estrategia o
    /// fuera de la entrega vigente: <c>Inventory.DocumentClass.NotAvailable</c>.
    /// </summary>
    public Result<IEfectoDeClase> Para(DocumentClass clase)
    {
        if (clase == DocumentClass.Voiding)
            throw new ArgumentException("Para una anulación se pide la estrategia de la clase del original.", nameof(clase));
        return Opera(clase)
            ? Result.Success(_porClase[clase])
            : Result.Failure<IEfectoDeClase>(InventoryErrors.DocumentClassNotAvailable(clase));
    }
}
