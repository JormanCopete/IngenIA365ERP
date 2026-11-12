using System.Diagnostics;
using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.ElectronicInvoicing;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.Application.ElectronicInvoicing.Documents;
using IngenIA365ERP.Application.ElectronicInvoicing.Settings;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Inventory.Pos;

/// <summary>
/// El bloque <c>electronic</c> de la respuesta del cobro y de la venta (api.md §18.2, §20.2): el documento electrónico, su estado, el código
/// único y el QR, la contingencia, cuánto se esperó, si ya se entrega y si quedó pendiente de entrega, y los mensajes traducidos. (nuevo)
/// </summary>
public sealed record ElectronicoDeLaVentaDto(
    Guid ElectronicDocumentPublicId,
    ElectronicDocumentKind Kind,
    ElectronicDocumentStatus Status,
    string? UniqueCode,
    string? QrContent,
    ContingencyType? ContingencyType,
    long WaitedMs,
    bool Deliverable,
    bool PendingDelivery,
    IReadOnlyList<MensajeDelCanalDto>? Messages,
    // Feature 023 (contracts/api.md §1): el estado que ve la persona y los reintentos. Opcionales: los clientes anteriores los ignoran.
    string? DisplayStatus = null,
    int TransientFailures = 0,
    int? AttemptLimit = null,
    DateTime? NextAttemptAt = null,
    DateTime? DeadAt = null);

/// <summary>
/// La espera del POS (feature 012, I4, T736; reescrita en la feature 023, D7; research R3). Corre <b>después del commit</b> del cobro
/// (<see cref="Common.Persistence.TareasTrasElCommit"/>), cuando el aviso <c>fe.emitir</c> ya se publicó: <b>no llama al canal</b>. Lo hace
/// un consumidor de la cola —cualquier réplica, o la cola en proceso— y aquí sólo se <b>lee la fila</b> cada <see cref="PausaEntreConsultas"/>
/// hasta <c>Dian.EsperaMaximaPosSegundos</c> del punto de venta (defecto 2 s; 0 = no espera):
/// <list type="bullet">
/// <item>validado dentro de la espera → <c>deliverable</c>, la tirilla con el código único y el QR, y la entrega anotada;</item>
/// <item>contingencia 03 o 04 → <c>deliverable</c> y la tirilla con su leyenda, en el acto;</item>
/// <item>espera cumplida → la tirilla <b>igual</b>, sin código único ni QR, con la leyenda «pendiente de validación DIAN»; el comprador recibe
/// la representación completa cuando el documento quede validado (el seguimiento la entrega);</item>
/// <item>rechazo → los motivos traducidos, sin tirilla (lo corrige quien tenga <c>ElectronicInvoicing.Documents.Correct</c>).</item>
/// </list>
/// Como no intenta, no corta llamadas a medias ni deja arrendamientos tomados, y una espera vencida ya no cuenta como falla del circuito: el
/// circuito lo alimenta el resultado real del intento. Nunca renumera ni lanza hacia el cobro: la venta ya quedó confirmada.
/// </summary>
public sealed class EsperaEnLineaDelPos(
    IApplicationDbContext db,
    ILectorDeParametros parametros,
    IDateTimeService reloj,
    ConstructorDeTirilla tirilla,
    Application.Common.Mensajeria.IMedidorDeTrabajos? medidor = null,
    ILogger<EsperaEnLineaDelPos>? logger = null)
{
    /// <summary>Cada cuánto se vuelve a leer el estado de la fila mientras se espera.</summary>
    public TimeSpan PausaEntreConsultas { get; init; } = TimeSpan.FromMilliseconds(150);

    /// <summary>Completa el resultado del cobro con la espera y la tirilla del documento electrónico.</summary>
    public async Task<CheckoutResultDto> CompletarAsync(CheckoutResultDto cobro, CashRegisterPrintFormat formato, CancellationToken ct)
    {
        if (cobro.Status != DocumentStatus.Confirmed) return cobro;
        var electronico = await EstadoElectronicoDeInventario.DeAsync(db, cobro.DocumentPublicId, ct);
        if (electronico is null) return cobro;

        var cronometro = Stopwatch.StartNew();
        if (EnCurso(electronico.Status))
        {
            var espera = await EsperaAsync(electronico, cobro, ct);
            while (cronometro.Elapsed < espera)
            {
                var restante = espera - cronometro.Elapsed;
                await Task.Delay(restante < PausaEntreConsultas ? restante : PausaEntreConsultas, ct);
                var estado = await db.ElectronicDocuments.AsNoTracking().Where(d => d.Id == electronico.Id).Select(d => d.Status).FirstAsync(ct);
                if (!EnCurso(estado)) break;
            }
        }
        cronometro.Stop();

        // El documento puede estar seguido en este contexto desde la confirmación: se relee para no devolver el estado de entonces.
        var final = await db.ElectronicDocuments.AsNoTracking().FirstAsync(e => e.Id == electronico.Id, ct);
        var entregable = EntregaAlComprador.Entregable(final.Status);
        medidor?.EsperaDelCobro(cronometro.Elapsed, entregable);

        TicketDto? ticket = null;
        if (final.Status != ElectronicDocumentStatus.Rejected)
        {
            var venta = await db.InventoryDocuments.Include(d => d.Lines).FirstAsync(d => d.PublicId == cobro.DocumentPublicId, ct);
            ticket = await TirillaElectronicaAsync(db, tirilla, venta, final, formato, copia: false, ct);
        }
        if (entregable && final.DeliveredAt is null)
        {
            // Sólo si nadie lo anotó (el seguimiento puede estar entregando por correo en otra réplica): sin pisar la fila.
            var ahora = reloj.UtcNow;
            await db.ElectronicDocuments.Where(d => d.Id == final.Id && d.DeliveredAt == null)
                .ExecuteUpdateAsync(x => x.SetProperty(d => d.DeliveredAt, ahora), ct);
            final.DeliveredAt = ahora;
        }
        if (!entregable && EnCurso(final.Status))
            logger?.LogInformation("[FE.CobroSinValidar] {Numero} ({DocumentoPublicId}): tirilla sin código único tras {Ms} ms; sigue la cola.",
                final.Number, final.PublicId, cronometro.ElapsedMilliseconds);

        var bloque = Bloque(final, cronometro.ElapsedMilliseconds, MensajesGuardados(final));
        return cobro with { Electronic = bloque, Ticket = ticket };
    }

    private static bool EnCurso(ElectronicDocumentStatus estado) => estado is ElectronicDocumentStatus.Pending or ElectronicDocumentStatus.Sent;

    /// <summary>El bloque <c>electronic</c> de un documento electrónico en su estado actual.</summary>
    public static ElectronicoDeLaVentaDto Bloque(ElectronicDocument e, long esperado, IReadOnlyList<MensajeDelCanalDto>? mensajes)
    {
        var entregable = EntregaAlComprador.Entregable(e.Status);
        return new ElectronicoDeLaVentaDto(e.PublicId, e.Kind, e.Status, e.UniqueCode, e.QrContent, e.ContingencyType, esperado, entregable,
            PendingDelivery: e.DeliveredAt is null && e.Status is not (ElectronicDocumentStatus.Rejected or ElectronicDocumentStatus.CancelledWithoutReplacement),
            mensajes,
            DisplayStatus: EstadoVisibleDelDocumento.De(e).ToString(),
            TransientFailures: e.TransientFailures,
            AttemptLimit: e.DeadAttemptLimit,
            NextAttemptAt: e.NextAttemptAt,
            DeadAt: e.DeadAt);
    }

    /// <summary>La tirilla de un documento electrónico: el modelo de siempre más el bloque electrónico con su leyenda (§20.2).</summary>
    public static async Task<TicketDto> TirillaElectronicaAsync(IApplicationDbContext db, ConstructorDeTirilla tirilla, InventoryDocument venta,
        ElectronicDocument e, CashRegisterPrintFormat formato, bool copia, CancellationToken ct)
    {
        var modelo = await tirilla.ConstruirAsync(venta, formato, copia, ct);
        var leyendas = LeyendasDeRepresentacion.Para(e.Kind, e.Status, e.ContingencyType, e.Environment, !string.IsNullOrWhiteSpace(e.UniqueCode));
        var resolucion = e.ResolutionId is int r
            ? await db.DianNumberingResolutions.AsNoTracking().Where(x => x.Id == r)
                .Select(x => new { x.ResolutionNumber, x.ResolutionDate, x.Prefix, x.RangeFrom, x.RangeTo, x.ValidTo }).FirstOrDefaultAsync(ct)
            : null;
        var textoDeResolucion = resolucion is null ? modelo.Header.ResolutionText
            : string.Create(CultureInfo.InvariantCulture,
                $"Resolución DIAN {resolucion.ResolutionNumber} del {resolucion.ResolutionDate:yyyy-MM-dd}, prefijo {resolucion.Prefix} del {resolucion.RangeFrom} al {resolucion.RangeTo}, vigente hasta {resolucion.ValidTo:yyyy-MM-dd}");
        var tipoDeCodigo = e.UniqueCodeKind?.ToString().ToUpperInvariant() ?? (e.Kind switch
        {
            ElectronicDocumentKind.Invoice => "CUFE",
            ElectronicDocumentKind.SupportDocument or ElectronicDocumentKind.SupportDocumentAdjustmentNote => "CUDS",
            _ => "CUDE",
        });
        return modelo with
        {
            Header = modelo.Header with { ResolutionText = textoDeResolucion },
            Electronic = new TicketElectronicDto(tipoDeCodigo, e.UniqueCode ?? string.Empty, e.QrContent ?? string.Empty,
                leyendas.Count == 0 ? null : string.Join(" ", leyendas)),
            Footer = modelo.Footer.Where(f => f != ConstructorDeTirilla.LeyendaDePruebas || e.Environment == DianEnvironment.Production).ToList(),
        };
    }

    /// <summary><c>Dian.EsperaMaximaPosSegundos</c> a la fecha, por el punto de la venta (su defecto seguro del catálogo si no se lee).</summary>
    private async Task<TimeSpan> EsperaAsync(ElectronicDocument e, CheckoutResultDto cobro, CancellationToken ct)
    {
        var punto = await db.InventoryDocuments.AsNoTracking().Where(d => d.PublicId == cobro.DocumentPublicId).Select(d => d.PointOfSaleId).FirstOrDefaultAsync(ct);
        var leido = await parametros.LeerAsync(ParametrosDeFacturacionElectronica.Modulo, ParametrosDeFacturacionElectronica.EsperaMaximaPosSegundos,
            e.IssueDate, punto is null ? ParameterScopeKind.None : ParameterScopeKind.PointOfSale, punto ?? 0, ct);
        // Feature 023 (D7): 0 es un valor válido —no esperar—; sólo si no se puede leer vale el defecto seguro del catálogo.
        var definicion = ParametrosDeFacturacionElectronica.Definiciones.First(d => d.Clave == ParametrosDeFacturacionElectronica.EsperaMaximaPosSegundos);
        var segundos = leido.IsSuccess ? leido.Value.Como<int>() : int.Parse(definicion.DefectoSeguro, CultureInfo.InvariantCulture);
        var maximo = definicion.Maximo is decimal m ? (int)m : int.MaxValue;
        return TimeSpan.FromSeconds(Math.Clamp(segundos, 0, maximo));
    }

    private static IReadOnlyList<MensajeDelCanalDto>? MensajesGuardados(ElectronicDocument e)
    {
        if (string.IsNullOrWhiteSpace(e.LastMessagesJson)) return null;
        try
        {
            using var json = System.Text.Json.JsonDocument.Parse(e.LastMessagesJson);
            return json.RootElement.EnumerateArray().Select(m => new MensajeDelCanalDto(
                m.TryGetProperty("regla", out var r) ? r.GetString() ?? string.Empty : string.Empty,
                m.TryGetProperty("tipo", out var t) ? t.GetString() ?? string.Empty : string.Empty,
                m.TryGetProperty("texto", out var x) ? x.GetString() ?? string.Empty : string.Empty,
                m.TryGetProperty("traduccion", out var tr) ? tr.GetString() : null)).ToList();
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// El tipo de la venta en una caja cuando hay contingencia del facturador (feature 012, I4, T735; contracts/dian.md §7.2; FR-058, FR-067):
/// con una 03 abierta en el canal vigente, el rol <c>PosSale</c> se numera con el tipo del rol <c>PosSaleContingency</c> y
/// <c>InvoiceOnRequest</c> con el de <c>InvoiceContingency</c>. Sin tipo de contingencia en la caja, la venta conserva el suyo y la guardia
/// bloquea (<c>ElectronicInvoicing.Readiness.DocumentTypeMissing</c>). (nuevo)
/// </summary>
public static class TipoDeVentaEnContingencia
{
    /// <summary>El rol de contingencia que respalda a un rol de venta; nulo si no es de venta.</summary>
    public static CashRegisterDocumentRole? RolDeContingencia(CashRegisterDocumentRole rol) => rol switch
    {
        CashRegisterDocumentRole.PosSale => CashRegisterDocumentRole.PosSaleContingency,
        CashRegisterDocumentRole.InvoiceOnRequest => CashRegisterDocumentRole.InvoiceContingency,
        _ => null,
    };

    /// <summary>
    /// Si hay 03 abierta en el canal de la configuración vigente, cambia el tipo de <paramref name="venta"/> (todavía sin número) al del rol de
    /// contingencia de <paramref name="caja"/>. Devuelve si lo cambió. Un documento ya numerado nunca cambia.
    /// </summary>
    public static async Task<bool> AplicarAsync(IApplicationDbContext db, InventoryDocument venta, CashRegister caja, CancellationToken ct)
    {
        if (venta.Number is not null) return false;
        var rol = caja.DocumentTypes.FirstOrDefault(t => !t.IsDeleted && t.DocumentTypeId == venta.DocumentTypeId)?.Role;
        if (rol is null || RolDeContingencia(rol.Value) is not { } deContingencia) return false;
        var fecha = venta.OperationDate;
        var canal = await db.ElectronicEmissionSettings.AsNoTracking()
            .Where(s => s.ValidFrom <= fecha && (s.ValidTo == null || s.ValidTo >= fecha))
            .OrderByDescending(s => s.ValidFrom).Select(s => s.ChannelCode).FirstOrDefaultAsync(ct);
        if (canal is null) return false;
        var codigo = ElectronicInvoicing.Numeracion.ReglasDeResolucion.Canal(canal);
        var abierta = await db.DianContingencyEvents.AsNoTracking()
            .AnyAsync(e => e.Type == ContingencyType.Issuer03 && e.Status == ContingencyEventStatus.Open && e.ChannelCode == codigo, ct);
        if (!abierta) return false;
        var tipo = caja.DocumentTypes.FirstOrDefault(t => !t.IsDeleted && t.Role == deContingencia);
        if (tipo is null) return false;
        var nuevo = await db.InventoryDocumentTypes.Include(t => t.Warehouses).FirstAsync(t => t.Id == tipo.DocumentTypeId, ct);
        venta.DocumentTypeId = nuevo.Id;
        venta.DocumentType = nuevo;
        return true;
    }
}
