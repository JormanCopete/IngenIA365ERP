using System.Globalization;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Inventory.Purchasing;

/// <summary>
/// La revisión diaria de los eventos RADIAN (feature 012, T347; FR-022, FR-050; decisiones-transversales §2.13): busca las
/// facturas del proveedor a crédito, confirmadas, con algún evento <c>Pending</c> y cuya emisión más
/// <c>Compras.DiasAlertaEventosRadian</c> días ya llegó (<c>IssueDate + días ≤ HoyLocal</c>), y levanta
/// <c>Compras.EventosRadianFaltantes</c> por <see cref="IAlertas"/> con <c>DedupKey = RadianPendiente:{facturaPublicId}</c>: una
/// alerta por factura, que suma la ocurrencia si vuelve a correr. Registrar los dos eventos la atiende
/// (<see cref="RegisterExternalRadianEventCommandHandler"/>). Anular el registro de la factura no toca los eventos. (nuevo)
/// </summary>
public sealed class RevisionDeEventosRadian(
    IApplicationDbContext db,
    IDateTimeService reloj,
    ILectorDeParametros parametros,
    IAlertas alertas)
{
    public const string PrefijoDeLaClave = "RadianPendiente:";

    /// <summary>La condición de la alerta de una factura.</summary>
    public static string ClaveDeLaAlerta(Guid facturaPublicId) => PrefijoDeLaClave + facturaPublicId.ToString("D");

    /// <summary>Una pasada: cuántas alertas levantó o repitió.</summary>
    public async Task<int> RevisarAsync(CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var dias = await parametros.LeerComoAsync<int>(ParametrosDeInventario.Modulo, ParametrosDeInventario.ComprasDiasAlertaEventosRadian, hoy, ct: ct);
        if (dias.IsFailure) throw new InvalidOperationException($"No se pudo leer {ParametrosDeInventario.ComprasDiasAlertaEventosRadian}: {dias.Error.Message}");
        var limite = hoy.AddDays(-dias.Value);

        var pendientes = await db.SupplierInvoiceDetails.AsNoTracking()
            .Where(d => d.IsCredit && d.DocumentClass == DocumentClass.SupplierInvoice && d.IssueDate <= limite)
            .Join(db.InventoryDocuments.AsNoTracking(), d => d.DocumentId, x => x.Id, (d, x) => new { Detalle = d, Documento = x })
            .Where(y => y.Documento.Status == DocumentStatus.Confirmed)
            .Where(y => db.SupplierInvoiceEvents.Any(e => e.DocumentId == y.Documento.Id && e.Status == SupplierInvoiceEventStatus.Pending))
            .Select(y => new
            {
                y.Documento.PublicId, y.Documento.Prefix, y.Documento.Number, y.Documento.WarehouseId,
                y.Detalle.SupplierPrefix, y.Detalle.SupplierNumber, y.Detalle.IssueDate,
            })
            .ToListAsync(ct);

        var bodegas = pendientes.Select(p => p.WarehouseId).OfType<int>().Distinct().ToList();
        var publicas = await db.Warehouses.AsNoTracking().Where(w => bodegas.Contains(w.Id)).ToDictionaryAsync(w => w.Id, w => w.PublicId, ct);

        var levantadas = 0;
        foreach (var p in pendientes)
        {
            var numero = VistaDeDocumentos.NumeroVisible(p.Prefix, p.Number);
            var r = await alertas.LevantarAsync(new AlertaALevantar(
                TiposDeAlerta.EventosRadianFaltantes,
                $"Factura {p.SupplierPrefix}{p.SupplierNumber} sin eventos RADIAN",
                string.Format(CultureInfo.InvariantCulture,
                    "La factura del proveedor {0}{1} (registro {2}), emitida el {3:dd/MM/yyyy} a crédito, sigue sin el acuse de recibo (030) o el recibo del bien (032). Emítalos en el portal y regístrelos en Compras.",
                    p.SupplierPrefix, p.SupplierNumber, numero, p.IssueDate),
                "InventoryDocument", p.PublicId,
                p.WarehouseId is int b && publicas.TryGetValue(b, out var bodega) ? bodega : null,
                DedupKey: ClaveDeLaAlerta(p.PublicId)), ct);
            if (r.IsSuccess) levantadas++;
        }
        return levantadas;
    }
}

/// <summary>
/// La tarea programada <c>compras.eventos-radian</c> (T347): una vez al día, desde la <see cref="HoraDeInicio"/> local, corre
/// <see cref="RevisionDeEventosRadian"/> en cada cooperativa. La corre <c>ProgramadorDeTareas</c> por <c>IEjecutorEnCooperativa</c>
/// con el actor «Proceso de integración». Idempotente: repetirla sólo suma la ocurrencia de la misma alerta. (nuevo)
/// </summary>
public sealed class TareaDeEventosRadian : ITareaProgramada
{
    public const string NombreDeLaTarea = "compras.eventos-radian";

    /// <summary>La hora local desde la que corre (antes de la jornada, para que la alerta esté al llegar).</summary>
    public static readonly TimeOnly HoraDeInicio = new(6, 0);

    public string Nombre => NombreDeLaTarea;

    public bool DebeCorrer(DateTimeOffset ahoraLocal, DateTimeOffset? ultimaCorrida) =>
        TimeOnly.FromTimeSpan(ahoraLocal.TimeOfDay) >= HoraDeInicio
        && (ultimaCorrida is null || DateOnly.FromDateTime(ultimaCorrida.Value.DateTime) < DateOnly.FromDateTime(ahoraLocal.DateTime));

    public async Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct) =>
        await servicios.GetRequiredService<RevisionDeEventosRadian>().RevisarAsync(ct);
}
