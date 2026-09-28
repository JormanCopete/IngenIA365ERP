using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Application.ElectronicInvoicing.Catalogs;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Purchasing.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Purchasing;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Purchasing;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Purchasing;

/// <summary>
/// Compras como fuente de los eventos RADIAN que emite el ERP (feature 012, I5, T803–T805; FR-050, US13-4, T42; contracts/api.md §14.8,
/// §24.7; dian.md §14.3). Lo usa <see cref="FuenteDeEmisionDeInventario"/> para las cuatro operaciones de eventos del puerto
/// <c>IFuenteDeDocumentoElectronico</c>: la plataforma no lee <c>INV_SupplierInvoiceEvents</c> ni la factura del proveedor.
/// <list type="bullet">
/// <item><see cref="PrepararAsync"/>: las reglas del módulo (<see cref="TransicionesDeEventoRadian.PedirEmision"/>), quién lo pide y la
/// entrada neutral de cada evento;</item>
/// <item><see cref="LeerAsync"/>: la misma entrada, leída de lo guardado (para que el procesador vuelva a armar el evento);</item>
/// <item><see cref="EnlazarAsync"/>: <c>ElectronicDocumentPublicId</c>;</item>
/// <item><see cref="RegistrarResultadoAsync"/>: <c>Pending → Emitted</c> (CUDE, fecha y fuente <c>Erp</c>) o <c>→ Rejected</c>, y la
/// alerta <c>Compras.EventosRadianFaltantes</c> atendida sola cuando no falta ninguno.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class EventosRadianDeInventario(
    IApplicationDbContext db,
    VistaDeDocumentos vista,
    IActorActual actorActual,
    IDateTimeService reloj,
    IAlertas alertas)
{
    /// <summary>El código del evento de compras de un tipo de documento electrónico.</summary>
    public static SupplierInvoiceEventCode CodigoDe(ElectronicDocumentKind tipo) => tipo switch
    {
        ElectronicDocumentKind.RadianEvent030 => SupplierInvoiceEventCode.Receipt030,
        ElectronicDocumentKind.RadianEvent032 => SupplierInvoiceEventCode.GoodsReceived032,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Sólo los eventos RADIAN 030 y 032."),
    };

    /// <summary>El tipo de documento electrónico de un evento de compras.</summary>
    public static ElectronicDocumentKind TipoDe(SupplierInvoiceEventCode codigo) => codigo == SupplierInvoiceEventCode.Receipt030
        ? ElectronicDocumentKind.RadianEvent030
        : ElectronicDocumentKind.RadianEvent032;

    public async Task<Result<IReadOnlyList<EventoRadianPreparado>>> PrepararAsync(Guid facturaPublicId, IReadOnlyList<ElectronicDocumentKind> tipos,
        CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDelDocumento.SinUsuario());

        var factura = await vista.BuscarAsync(facturaPublicId, DocumentClassGroup.Purchases, seguir: false, ct);
        if (factura is null || factura.Class != DocumentClass.SupplierInvoice) return Falla(InventoryErrors.DocumentNotFound());

        var detalle = await db.SupplierInvoiceDetails.AsNoTracking().FirstOrDefaultAsync(d => d.DocumentId == factura.Id, ct);
        var eventos = await db.SupplierInvoiceEvents.Where(x => x.DocumentId == factura.Id).ToListAsync(ct);
        if (detalle is null || eventos.Count == 0 || factura.Status != DocumentStatus.Confirmed || !detalle.IsCredit)
            return Falla(ErroresDeCompras.Radian(TransicionesDeEventoRadian.CodigoNoAplica));

        var codigos = tipos.Select(CodigoDe).Distinct().OrderBy(c => c).ToList();
        var actuales = eventos.Select(Actual).ToList();
        var recepcion = await RecepcionAsync(factura.Id, ct);
        var hoy = reloj.HoyLocal;
        foreach (var codigo in codigos)
        {
            var rechazo = TransicionesDeEventoRadian.PedirEmision(actuales, codigo, codigos, detalle.IssueDate, hoy, recepcion is not null);
            if (rechazo is not null) return Falla(ErroresDeCompras.Radian(rechazo));
        }

        var ahora = reloj.UtcNow;
        var quien = await QuienAsync(usuario, ct);
        var proveedor = await ProveedorAsync(factura, detalle, ct);
        var preparados = new List<EventoRadianPreparado>();
        foreach (var codigo in codigos)
        {
            var evento = eventos.First(e => e.EventCode == codigo);
            // El reintento de un rechazado vuelve a pendiente y conserva su documento electrónico (y su número).
            Guid? reintento = null;
            if (evento.Status == SupplierInvoiceEventStatus.Rejected)
            {
                evento.Status = SupplierInvoiceEventStatus.Pending;
                reintento = evento.ElectronicDocumentPublicId;
            }
            evento.RegisteredByUserId = usuario;
            evento.RegisteredAt = ahora;

            Guid? espera = null;
            if (codigo == SupplierInvoiceEventCode.GoodsReceived032)
            {
                var acuse = eventos.First(e => e.EventCode == SupplierInvoiceEventCode.Receipt030);
                if (!codigos.Contains(SupplierInvoiceEventCode.Receipt030) && acuse.Status == SupplierInvoiceEventStatus.Pending)
                    espera = acuse.ElectronicDocumentPublicId;
            }

            var tipo = TipoDe(codigo);
            preparados.Add(new EventoRadianPreparado(tipo, Entrada(factura, detalle, tipo, proveedor, recepcion, quien), reintento, espera));
        }
        return Result.Success<IReadOnlyList<EventoRadianPreparado>>(preparados);
    }

    public async Task<Result<EntradaDeEventoRadian>> LeerAsync(Guid facturaPublicId, ElectronicDocumentKind tipo, CancellationToken ct)
    {
        var factura = await db.InventoryDocuments.AsNoTracking().Include(d => d.DocumentType)
            .FirstOrDefaultAsync(d => d.PublicId == facturaPublicId && d.Class == DocumentClass.SupplierInvoice, ct);
        if (factura is null) return Result.Failure<EntradaDeEventoRadian>(InventoryErrors.DocumentNotFound());
        var detalle = await db.SupplierInvoiceDetails.AsNoTracking().FirstOrDefaultAsync(d => d.DocumentId == factura.Id, ct);
        var codigo = CodigoDe(tipo);
        var evento = await db.SupplierInvoiceEvents.AsNoTracking().FirstOrDefaultAsync(e => e.DocumentId == factura.Id && e.EventCode == codigo, ct);
        if (detalle is null || evento?.RegisteredByUserId is not { } usuario)
            return Result.Failure<EntradaDeEventoRadian>(ErroresDeCompras.Radian(TransicionesDeEventoRadian.CodigoNoAplica));

        var recepcion = await RecepcionAsync(factura.Id, ct);
        return Result.Success(Entrada(factura, detalle, tipo, await ProveedorAsync(factura, detalle, ct), recepcion, await QuienAsync(usuario, ct)));
    }

    public async Task<Result> EnlazarAsync(Guid facturaPublicId, ElectronicDocumentKind tipo, Guid electronicDocumentPublicId, CancellationToken ct)
    {
        var codigo = CodigoDe(tipo);
        var evento = await db.SupplierInvoiceEvents
            .Where(e => e.EventCode == codigo)
            .Join(db.InventoryDocuments.Where(d => d.PublicId == facturaPublicId), e => e.DocumentId, d => d.Id, (e, _) => e)
            .FirstOrDefaultAsync(ct);
        if (evento is null) return Result.Failure(ErroresDeCompras.Radian(TransicionesDeEventoRadian.CodigoNoAplica));
        evento.ElectronicDocumentPublicId = electronicDocumentPublicId;
        return Result.Success();
    }

    public async Task<Result> RegistrarResultadoAsync(Guid facturaPublicId, ElectronicDocumentKind tipo, ResultadoDeEventoRadian resultado,
        CancellationToken ct)
    {
        var facturaId = await db.InventoryDocuments.AsNoTracking().Where(d => d.PublicId == facturaPublicId).Select(d => (int?)d.Id).FirstOrDefaultAsync(ct);
        if (facturaId is null) return Result.Failure(InventoryErrors.DocumentNotFound());
        var eventos = await db.SupplierInvoiceEvents.Where(e => e.DocumentId == facturaId).ToListAsync(ct);
        var evento = eventos.FirstOrDefault(e => e.EventCode == CodigoDe(tipo));
        if (evento is null) return Result.Failure(ErroresDeCompras.Radian(TransicionesDeEventoRadian.CodigoNoAplica));

        // Un evento que ya no estaba pendiente (p. ej. se registró por fuera mientras tanto) no cambia: la transición no existe.
        if (TransicionesDeEventoRadian.TrasLaRespuesta(evento.Status, resultado.Validado) is { } destino)
        {
            evento.Status = destino;
            if (resultado.Validado)
            {
                evento.Source = SupplierInvoiceEvent.FuenteErp;
                evento.EventDate = resultado.Fecha;
                evento.Cude = string.IsNullOrWhiteSpace(resultado.Cude) ? null : Recortar(resultado.Cude.Trim().ToLowerInvariant(), SupplierInvoiceEvent.LargoDelCude);
            }
            await db.SaveChangesAsync(ct);
        }

        // Llegaron los dos: la alerta de la factura se atiende sola (un rechazado sigue faltando).
        if (eventos.All(e => TransicionesDeEventoRadian.Hecho(e.Status) || e.Status == SupplierInvoiceEventStatus.NotApplicable))
            await alertas.AtenderPorProcesoAsync(RevisionDeEventosRadian.ClaveDeLaAlerta(facturaPublicId),
                "El ERP emitió el acuse de recibo (030) y el recibo del bien (032).", ct);
        return Result.Success();
    }

    // ------------------------------------------------------------------------------------------ piezas --

    private static EventoRadianActual Actual(SupplierInvoiceEvent e) =>
        new(e.EventCode, e.Status, e.EventDate, EnEmision: e.Status == SupplierInvoiceEventStatus.Pending && e.ElectronicDocumentPublicId is not null);

    private static EntradaDeEventoRadian Entrada(InventoryDocument factura, SupplierInvoiceDetail detalle, ElectronicDocumentKind tipo,
        ProveedorDelEvento proveedor, (string Numero, DateOnly Fecha)? recepcion, string quien) =>
        new(FuenteDeEmisionDeInventario.Modulo,
            factura.PublicId,
            factura.Class.ToString(),
            factura.DocumentType?.Code ?? string.Empty,
            VistaDeDocumentos.NumeroVisible(factura.Prefix, factura.Number) ?? string.Empty,
            tipo,
            detalle.NumeroVisible,
            detalle.Cufe,
            detalle.IssueDate,
            proveedor,
            tipo == ElectronicDocumentKind.RadianEvent032 ? recepcion?.Numero : null,
            tipo == ElectronicDocumentKind.RadianEvent032 ? recepcion?.Fecha : null,
            quien);

    /// <summary>La primera recepción confirmada (por número) enlazada a la factura con <c>InvoiceOfReceipt</c>; nula si no hay.</summary>
    private async Task<(string Numero, DateOnly Fecha)?> RecepcionAsync(int facturaId, CancellationToken ct)
    {
        var r = await db.DocumentLinks.AsNoTracking()
            .Where(l => l.TargetDocumentId == facturaId && l.Kind == DocumentLinkKind.InvoiceOfReceipt && !l.IsDeleted)
            .Join(db.InventoryDocuments.AsNoTracking(), l => l.SourceDocumentId, d => d.Id, (_, d) => d)
            .Where(d => d.Status == DocumentStatus.Confirmed && d.Class == DocumentClass.PurchaseReceipt)
            .OrderBy(d => d.Number).ThenBy(d => d.Id)
            .Select(d => new { d.Prefix, d.Number, d.OperationDate })
            .FirstOrDefaultAsync(ct);
        return r is null ? null : (VistaDeDocumentos.NumeroVisible(r.Prefix, r.Number) ?? string.Empty, r.OperationDate);
    }

    /// <summary>El proveedor de la copia fiscal de mayor versión del registro; si no la tiene, el del maestro.</summary>
    private async Task<ProveedorDelEvento> ProveedorAsync(InventoryDocument factura, SupplierInvoiceDetail detalle, CancellationToken ct)
    {
        var foto = await db.DocumentPartySnapshots.AsNoTracking()
            .Where(f => f.DocumentId == factura.Id && !f.IsDeleted)
            .OrderByDescending(f => f.Version)
            .FirstOrDefaultAsync(ct);
        if (foto is not null)
            return new ProveedorDelEvento(foto.TaxId, foto.CheckDigit, foto.DianIdTypeCode, foto.DianOrganizationType, foto.LegalName);

        var p = await db.People.AsNoTracking().IgnoreQueryFilters().FirstAsync(x => x.Id == detalle.SupplierPersonId, ct);
        var juridica = !string.IsNullOrWhiteSpace(p.BusinessName);
        var catalogo = CatalogoDian.Embebido;
        var nombre = juridica ? p.BusinessName! : string.Join(' ', new[] { p.FirstName, p.OtherNames, p.LastName, p.SecondLastName }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        return new ProveedorDelEvento(p.TaxId, p.TaxIdCheckDigit, catalogo.TipoDeIdentificacionDe(p.IdType, detalle.IssueDate) ?? string.Empty,
            catalogo.TipoDePersonaDe(juridica, detalle.IssueDate) ?? string.Empty, nombre);
    }

    /// <summary>El usuario que pidió emitir el evento, por su nombre de usuario (el mismo al pedir y al volver a armar).</summary>
    private async Task<string> QuienAsync(int usuario, CancellationToken ct) =>
        await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.Id == usuario).Select(u => u.Username).FirstOrDefaultAsync(ct)
        ?? $"Usuario {usuario}";

    private static string Recortar(string texto, int maximo) => texto.Length <= maximo ? texto : texto[..maximo];

    private static Result<IReadOnlyList<EventoRadianPreparado>> Falla(Error error) => Result.Failure<IReadOnlyList<EventoRadianPreparado>>(error);
}
