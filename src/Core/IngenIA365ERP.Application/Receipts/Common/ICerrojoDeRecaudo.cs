namespace IngenIA365ERP.Application.Receipts.Common;

/// <summary>Los niveles del cerrojo de Recaudo, en su orden fijo (feature 015, T0038; data-model §0.5). (nuevo)</summary>
public enum NivelDeRecaudo
{
    /// <summary>1 · <c>RCD_Setup</c>, compartido.</summary>
    Configuracion = 1,

    /// <summary>2 · <c>RCD_CollectionPointDays</c> del punto y día; compartido, exclusivo al cerrar el día.</summary>
    DiaDelPunto = 2,

    /// <summary>3 · <c>RCD_CashSessions</c>, exclusivo.</summary>
    Sesion = 3,

    /// <summary>4 · <c>RCD_Vaults</c> de la oficina (sólo con movimiento de bóveda o remesa), exclusivo.</summary>
    Boveda = 4,

    /// <summary>5 · <c>RCD_PayerControls</c> de pagador, quien efectúa y beneficiarios, por Id; la fila se crea si falta.</summary>
    Pagadores = 5,

    /// <summary>6 · los orígenes (saldos a favor, partidas, líneas de giro de pagaduría, cheques, recibos al anular), por Id.</summary>
    Origenes = 6,
}

/// <summary>
/// El cerrojo pesimista de Recaudo (feature 015, T0038; R-12; FR-007, FR-170; data-model §0.5). Bloquea, dentro de la transacción en curso y con
/// la conexión del propio contexto, en el <b>orden fijo</b> de <see cref="NivelDeRecaudo"/>; después de los seis niveles propios vienen los
/// contratos de Cartera y Captaciones (nivel 7, los toma el contrato con <c>CerrojoDeCartera</c>), la cuenta bancaria propia por el puerto de
/// Tesorería (nivel 8) y la numeración al final (nivel 9, <see cref="BloquearNumeracionAsync"/>, que llama <c>NumeradorDeRecaudo</c>).
///
/// <para>
/// Una sentencia por nivel, ordenada por <c>Id</c>; un nivel que la transacción ya tiene no espera (no se vuelve a pedir). Pedir un nivel
/// menor que uno ya tomado, o una fila de Recaudo después de una de Tesorería, lanza (<see cref="OrdenDeCerrojos"/>): ninguna operación toma una
/// persona después de una cuenta suya, ni una fila <c>RCD_</c> después de una <c>TES_</c> (CH16). Sin transacción en curso lanza. La
/// implementación vive en <c>Persistence/Receipts/CerrojoDeRecaudo</c> con el SQL de cada motor (<c>SqlDelCerrojoDeRecaudo</c>). (nuevo)
/// </para>
/// </summary>
public interface ICerrojoDeRecaudo
{
    /// <summary>Nivel 1: la fila única de <c>RCD_Setup</c>.</summary>
    Task TomarSetupAsync(bool exclusivo = false, CancellationToken ct = default);

    /// <summary>
    /// Niveles 2, 3, 4 y 6: las filas por <c>Id</c> de la tabla del nivel, en exclusivo (el día del punto, compartido salvo
    /// <paramref name="exclusivo"/>). <paramref name="tabla"/> tiene que ser una de las del nivel (<c>SqlDelCerrojoDeRecaudo.TablasDelNivel</c>).
    /// </summary>
    Task BloquearAsync(NivelDeRecaudo nivel, string tabla, IReadOnlyCollection<long> ids, bool exclusivo = true, CancellationToken ct = default);

    /// <summary>Nivel 5: crea las filas que falten de <c>RCD_PayerControls</c> y las bloquea en exclusivo, por Id.</summary>
    Task BloquearPagadoresAsync(IReadOnlyCollection<int> personIds, CancellationToken ct = default);

    /// <summary>
    /// Antes de numerar por primera vez una clave: inserta su fila de <c>COR_Sequences</c> si falta (índice único filtrado
    /// <c>UK_COR_Sequences_Name_RcdTes</c>), sin error si otra operación la acaba de crear. Sin transacción no hace nada.
    /// </summary>
    Task AsegurarNumeracionAsync(string clave, string prefijo, CancellationToken ct = default);

    /// <summary>Nivel 9: la fila de <c>COR_Sequences</c> que va a numerar, en exclusivo y al final.</summary>
    Task BloquearNumeracionAsync(int sequenceId, CancellationToken ct = default);
}
