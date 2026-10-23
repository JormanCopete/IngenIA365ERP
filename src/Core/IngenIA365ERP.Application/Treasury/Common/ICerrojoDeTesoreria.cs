namespace IngenIA365ERP.Application.Treasury.Common;

/// <summary>Los niveles del cerrojo de la Tesorería nueva, en su orden fijo (feature 015, T0038; data-model §0.5). (nuevo)</summary>
public enum NivelDeTesoreria
{
    /// <summary>1 · <c>TES_Setup</c>, compartido.</summary>
    Configuracion = 1,

    /// <summary>2 · <c>TES_BankAccounts</c>: exclusivo al aprobar, generar, emitir un cheque o confirmar; compartido para Recaudo por el puerto.</summary>
    CuentaDeOrigen = 2,

    /// <summary>3 · <c>TES_Checkbooks</c>, exclusivo al emitir o anular.</summary>
    Chequera = 3,

    /// <summary>4 · <c>TES_PaymentBatches</c>, exclusivo.</summary>
    Lote = 4,

    /// <summary>5 · <c>TES_PaymentOrders</c> por Id, exclusivo.</summary>
    Ordenes = 5,

    /// <summary>6 · <c>TES_Reconciliations</c> de la cuenta y mes, exclusivo al cruzar o cerrar.</summary>
    Conciliacion = 6,
}

/// <summary>
/// El cerrojo pesimista de la Tesorería nueva (feature 015, T0038; R-12; FR-007, FR-170; data-model §0.5). Mismas reglas que
/// <c>ICerrojoDeRecaudo</c>: orden fijo de <see cref="NivelDeTesoreria"/>, una sentencia por nivel ordenada por <c>Id</c>, la numeración al final
/// (nivel 7). Recaudo va antes que Tesorería también cuando quien llama es Tesorería (CH16): un comando que va a escribir por
/// <c>IResultadoDeOrdenesDeRecaudo</c> o <c>IPartidasDesdeConciliacion</c> llama antes a su <c>TomarCerrojosAsync</c>, y
/// <see cref="Receipts.Common.OrdenDeCerrojos"/> lanza si se pide una fila de Recaudo después de una de Tesorería. (nuevo)
/// </summary>
public interface ICerrojoDeTesoreria
{
    /// <summary>Nivel 1: la fila única de <c>TES_Setup</c>.</summary>
    Task TomarSetupAsync(bool exclusivo = false, CancellationToken ct = default);

    /// <summary>Niveles 2 a 6: las filas por <c>Id</c> de la tabla del nivel; <paramref name="exclusivo"/> falso toma compartido.</summary>
    Task BloquearAsync(NivelDeTesoreria nivel, IReadOnlyCollection<long> ids, bool exclusivo = true, CancellationToken ct = default);

    /// <summary>Antes de numerar por primera vez una clave: inserta su fila de <c>COR_Sequences</c> si falta.</summary>
    Task AsegurarNumeracionAsync(string clave, string prefijo, CancellationToken ct = default);

    /// <summary>Nivel 7: la fila de <c>COR_Sequences</c> que va a numerar, en exclusivo y al final.</summary>
    Task BloquearNumeracionAsync(int sequenceId, CancellationToken ct = default);
}
