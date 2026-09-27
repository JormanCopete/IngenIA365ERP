using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Inventory.Pos;

/// <summary>
/// Los eventos explícitos de las acciones de riesgo del POS y de la entrega (feature 012, I3, T603, T606, T607; T50, pregunta F13:
/// «acciones de riesgo, no cada lectura»). Quitar una línea, descuento, precio manual, suspender, recuperar, descartar, cobrar,
/// entregar y reimprimir dejan su evento con nombre (<c>Inventory.Pos.*</c>, <c>Inventory.Document.{Delivered, Reprinted}</c>); agregar
/// una línea con el lector o consultar no. (nuevo)
/// </summary>
public interface IAuditoriaDelPuntoDeVenta
{
    /// <summary>
    /// Anota el evento en la unidad de trabajo en curso: se guarda con el <c>SaveChanges</c> del comando, en su transacción (Inventario es
    /// un módulo encadenado, <c>COR_AuditOutbox</c>). Si la operación es <see cref="Common.Behaviors.IOperacionDePuntoDeVenta"/> el canal
    /// es <c>pos</c>. <paramref name="metadata"/> se agrega a la del contexto (p. ej. <c>Format</c>).
    /// </summary>
    Task AnotarAsync(string accion, object operacion, Guid documentoPublicId, object? datos, CancellationToken ct,
        IReadOnlyDictionary<string, string>? metadata = null);

    /// <summary>¿Hay un evento <paramref name="accion"/> del documento con <c>Format = formato</c>? Sirve para saber si ya hubo primera entrega.</summary>
    Task<bool> ExisteAsync(string accion, Guid documentoPublicId, string formato, CancellationToken ct);
}

/// <summary>
/// La implementación sobre <see cref="AuditoriaEncadenada"/>: arma el contexto con la operación (canal <c>pos</c> para las del punto de
/// venta) y agrega la fila de <c>COR_AuditOutbox</c> sin guardarla. Sin cooperativa activa no inventa un flujo: el evento va por
/// <see cref="IAuditService"/> como el de cualquier módulo. (nuevo)
/// </summary>
public sealed class AuditoriaDelPuntoDeVenta(IApplicationDbContext db, IServiceProvider servicios) : IAuditoriaDelPuntoDeVenta
{
    public const string ClaveDeFormato = "Format";

    public async Task AnotarAsync(string accion, object operacion, Guid documentoPublicId, object? datos, CancellationToken ct,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accion);
        var contexto = await AuditoriaEncadenada.ContextoAsync(servicios, servicios.GetService<ICurrentUserService>(), operacion, ct);
        var evento = new AuditLogCommand
        {
            Action = accion,
            EntityType = "InventoryDocument",
            EntityId = documentoPublicId.ToString(),
            Module = ModuloDeAuditoria.Inventory,
            NewValues = datos,
            HttpStatusCode = 200,
        };
        var flujo = AuditoriaEncadenada.Flujo(contexto.TenantId);
        if (flujo is null)
        {
            if (servicios.GetService<IAuditService>() is { } mongo)
                await mongo.LogAsync(AuditoriaEncadenada.ParaMongo(contexto, evento, metadata), ct);
            return;
        }
        db.AuditOutbox.Add(AuditoriaEncadenada.Entrada(flujo, AuditoriaEncadenada.Evento(contexto, evento, metadata)));
    }

    public async Task<bool> ExisteAsync(string accion, Guid documentoPublicId, string formato, CancellationToken ct)
    {
        var id = documentoPublicId.ToString();
        var cargas = await db.AuditOutbox.AsNoTracking()
            .Where(e => e.Module == ModuloDeAuditoria.Inventory && e.PayloadJson != null && e.PayloadJson.Contains(id) && e.PayloadJson.Contains(accion))
            .Select(e => e.PayloadJson!)
            .ToListAsync(ct);
        return cargas.Select(AuditoriaEncadenada.LeerCarga).Any(e =>
            e.Action == accion && e.EntityPublicId == id
            && e.Metadata is { } m && m.TryGetValue(ClaveDeFormato, out var f) && string.Equals(f, formato, StringComparison.Ordinal));
    }
}
