namespace IngenIA365ERP.Application.Lending.Common;

/// <summary>Si el cerrojo espera a que la fila se libere (operaciones) o la salta (el proceso, T5). (nuevo)</summary>
public enum ModoDeEspera
{
    /// <summary>Espera el candado: toda operación de pantalla, del contrato o del consumidor.</summary>
    Esperar = 1,

    /// <summary>Salta la fila ocupada (<c>SKIP LOCKED</c> / <c>READPAST</c>): el proceso la deja para la pasada siguiente.</summary>
    Saltar = 2,
}

/// <summary>
/// El cerrojo pesimista de Cartera (feature 013, T34, T0206; data-model §0.10, §22.5). Bloquea, dentro de la transacción en curso y
/// con la conexión del propio <c>ApplicationDbContext</c>, en este <b>orden fijo</b> (el que llama respeta el orden de las llamadas):
/// <list type="number">
/// <item><c>LND_Setup</c>, compartido en toda operación y en cada tanda; exclusivo sólo al cerrar o reabrir un mes;</item>
/// <item><c>LND_DebtorControls</c>, exclusivos y por Id (deudor y codeudores de la operación); la fila que falta la crea el propio
/// cerrojo;</item>
/// <item><c>LND_LoanControls</c>, exclusivos y por Id, todos los de la operación en una sola sentencia;</item>
/// <item>y la numeración al final (<see cref="BloquearNumeracionAsync"/>, que llama <c>NumeradorDeCartera</c>).</item>
/// </list>
/// <c>RowVersion</c> es la segunda defensa. Sólo dentro de <c>TransaccionExplicita</c>: sin transacción en curso lanza. Las
/// validaciones previas (contabilizable, listas, centrales) corren fuera. No se usa <c>ICerrojoPorClave</c> (CH19). La
/// implementación vive en <c>Persistence/Lending/CerrojoDeCartera</c> con el SQL de cada motor (<c>SqlDelCerrojoDeCartera</c>).
/// (nuevo)
/// </summary>
public interface ICerrojoDeCartera
{
    /// <summary>Paso 1: la fila única de <c>LND_Setup</c>.</summary>
    Task TomarSetupAsync(bool exclusivo, CancellationToken ct = default);

    /// <summary>Paso 2: crea las filas que falten de <c>LND_DebtorControls</c> y las bloquea en exclusivo, por Id.</summary>
    Task BloquearDeudoresAsync(IReadOnlyCollection<int> personIds, ModoDeEspera modo, CancellationToken ct = default);

    /// <summary>
    /// Paso 3: las filas de <c>LND_LoanControls</c> de los créditos, en exclusivo, por Id y en una sentencia. Devuelve los
    /// <c>LoanId</c> que quedaron bloqueados: con <see cref="ModoDeEspera.Esperar"/> son todos los que existen; con
    /// <see cref="ModoDeEspera.Saltar"/>, los que no estaban ocupados.
    /// </summary>
    Task<IReadOnlyList<int>> BloquearCreditosAsync(IReadOnlyCollection<int> loanIds, ModoDeEspera modo, CancellationToken ct = default);

    /// <summary>Paso 4: la fila de <c>LND_NumberSequences</c> que va a numerar, en exclusivo y al final.</summary>
    Task BloquearNumeracionAsync(int numberSequenceId, CancellationToken ct = default);
}
