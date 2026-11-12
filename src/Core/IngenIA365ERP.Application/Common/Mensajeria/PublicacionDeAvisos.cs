using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Common.Mensajeria;

/// <summary>
/// Publica los avisos de trabajo de una cooperativa (feature 023; research R2; contracts/mensajeria.md §3). Dos maneras:
/// <list type="bullet">
/// <item><see cref="AvisarTrasElCommit"/>: quien crea la orden (la confirmación de una venta, el resultado de un intento) la anota; se
/// publica <b>después</b> del commit de la petición más externa por <see cref="TareasTrasElCommit"/>. Si la petición falla, no se publica
/// nada: la orden tampoco existe.</item>
/// <item><see cref="PublicarAhoraAsync"/>: el barrido y el reproceso, que no están dentro de una transacción.</item>
/// </list>
/// Un aviso confirmado por el broker sobre un documento estampa <c>EnqueuedAt</c>; uno no confirmado no estampa nada y el barrido lo
/// retoma. <b>Nunca lanza</b> por la mensajería: la verdad ya está en la base. Scoped. (nuevo)
/// </summary>
public sealed class PublicacionDeAvisos(
    IApplicationDbContext db,
    IColaDeTrabajos cola,
    ICurrentTenantService cooperativaActual,
    IDateTimeService reloj,
    TareasTrasElCommit? trasElCommit = null,
    IMedidorDeTrabajos? medidor = null,
    ILogger<PublicacionDeAvisos>? logger = null)
{
    private readonly HashSet<(string Tipo, string Entidad)> _anotados = [];

    /// <summary>La cooperativa de este ámbito, o nulo si no la hay (no se publica: el barrido la encontrará).</summary>
    public Guid? Cooperativa => Guid.TryParse(cooperativaActual.TenantId, out var g) ? g : null;

    /// <summary>Anota el aviso sobre un documento electrónico para después del commit. Repetido en la misma petición, se publica una vez.</summary>
    public void AvisarTrasElCommit(string tipo, Guid documento, string origen) => AvisarTrasElCommit(tipo, documento.ToString("D"), origen);

    /// <summary>Anota el aviso para después del commit (documentos o destinos de integración).</summary>
    public void AvisarTrasElCommit(string tipo, string entidad, string origen)
    {
        TiposDeAviso.ColaDe(tipo); // un tipo desconocido es un defecto de quien anota: que se vea aquí
        if (!_anotados.Add((tipo, entidad))) return;
        if (trasElCommit is null)
        {
            logger?.LogDebug("[Mensajeria.SinTrasElCommit] {Tipo} {Entidad}: sin petición que lo publique; lo tomará el barrido.", tipo, entidad);
            return;
        }
        trasElCommit.Agregar(t => PublicarAhoraAsync(tipo, entidad, origen, t));
    }

    /// <summary>Publica ya y, si el broker confirma un aviso de documento, estampa <c>EnqueuedAt</c>. Devuelve si quedó confirmado.</summary>
    public async Task<bool> PublicarAhoraAsync(string tipo, string entidad, string origen, CancellationToken ct)
    {
        var colaDelTipo = TiposDeAviso.ColaDe(tipo);
        if (Cooperativa is not { } cooperativa)
        {
            logger?.LogWarning("[Mensajeria.SinCooperativa] {Tipo} {Entidad}: no hay cooperativa en el ámbito; lo tomará el barrido.", tipo, entidad);
            return false;
        }
        if (!cola.Disponible)
        {
            medidor?.Publicacion(colaDelTipo, "sin-broker");
            return false;
        }

        var ahora = reloj.UtcNow;
        bool confirmado;
        try
        {
            confirmado = await cola.PublicarAsync(Aviso.De(tipo, cooperativa, entidad, ahora, origen), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // El contrato dice que la cola no lanza; si lo hace, la orden sigue en la base igual.
            logger?.LogError(ex, "[Mensajeria.PublicacionFallida] {Tipo} {TenantPublicId} {Entidad}", tipo, cooperativa, entidad);
            confirmado = false;
        }
        medidor?.Publicacion(colaDelTipo, confirmado ? "confirmada" : "rechazada");
        if (!confirmado) return false;

        if (tipo == TiposDeAviso.FeEmitir && Guid.TryParse(entidad, out var documento))
        {
            try
            {
                await db.ElectronicDocuments.Where(d => d.PublicId == documento)
                    .ExecuteUpdateAsync(s => s.SetProperty(d => d.EnqueuedAt, ahora), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                // Sin la estampa, a lo sumo el barrido vuelve a avisar: el consumidor relee y no duplica nada.
                logger?.LogWarning(ex, "[Mensajeria.EstampaFallida] {Tipo} {TenantPublicId} {Entidad}", tipo, cooperativa, entidad);
            }
        }
        return true;
    }
}
