using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing.Transactions;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Documents;

/// <summary>
/// Las esperas técnicas entre intentos contra el canal (feature 012, I4, T712; contracts/dian.md §6.3): 15 s, 1, 2, 5, 15, 30 y 60 minutos y
/// después cada hora. <b>No son parámetros de la cooperativa</b> (T10) sino valores técnicos de la sección <c>ElectronicInvoicing:Retries</c>;
/// el defecto lo arma <c>AddApplication</c> y la sección de la API lo reemplaza (T728). Tampoco tienen tope: fuera de contingencia se sigue
/// intentando y alertando; en contingencia el tope es <c>TransmissionDeadline</c>. (nuevo)
/// </summary>
/// <param name="Esperas">La espera tras el intento 1, 2, 3…</param>
/// <param name="Despues">La espera cuando se acaban las de la lista.</param>
/// <param name="Arrendamiento">Cuánto dura el arrendamiento de la fila mientras se llama al canal.</param>
public sealed record EsperasDeReintento(IReadOnlyList<TimeSpan> Esperas, TimeSpan Despues, TimeSpan Arrendamiento)
{
    /// <summary>La espera después del intento número <paramref name="intento"/> (1 = el primero).</summary>
    public TimeSpan Tras(int intento) =>
        intento <= 0 || Esperas.Count == 0 ? (Esperas.Count > 0 ? Esperas[0] : Despues)
        : intento <= Esperas.Count ? Esperas[intento - 1] : Despues;
}

/// <summary>
/// El arrendamiento por fila de <c>COR_ElectronicDocuments</c> (feature 012, I4, T712; contracts/dian.md §6.4): el intento en línea del POS, el
/// procesador y «Reintentar ahora» no toman el mismo documento a la vez. La implementación de Persistence es el <c>UPDATE … WHERE Id = @id
/// AND (LeaseUntil IS NULL OR LeaseUntil &lt; @ahora) AND NextAttemptAt &lt;= @ahora</c> del contrato (<c>ExecuteUpdateAsync</c>, portable). La
/// exactitud no depende de él: la unicidad del número y la regla del ambiguo impiden el duplicado aunque venza a mitad de una llamada. Se
/// suelta en la transacción que registra el resultado (<c>LeaseUntil = LeaseOwner = null</c>). (nuevo)
/// </summary>
public interface IArrendamientoDeDocumentoElectronico
{
    /// <summary>Toma la fila si está libre o vencida y —con <paramref name="respetarEspera"/>— si ya le toca; dice si afectó la fila.</summary>
    Task<bool> TomarAsync(int documentoId, DateTime ahora, DateTime hasta, string dueno, bool respetarEspera, CancellationToken ct);
}

/// <summary>
/// Vuelve a armar el canónico de una versión desde datos inmutables (el documento comercial confirmado, su copia fiscal de mayor versión y
/// los catálogos por fecha) para subirlo después del commit y para transmitirlo (contracts/dian.md §4.1). La implementación usa la fuente
/// sellada en el documento y <see cref="ConstructorDelCanonico"/>; quien la llama compara el SHA-256 con el de la versión. (nuevo)
/// </summary>
public interface IReconstruccionDelCanonico
{
    Task<Result<CanonicoConstruido>> ReconstruirAsync(ElectronicDocument documento, ElectronicDocumentVersion version, CancellationToken ct);
}
