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
    IReadOnlyList<MensajeDelCanalDto>? Messages);

/// <summary>
/// La espera en línea del POS (feature 012, I4, T736; api.md §20.2; contracts/dian.md §6.1 paso 4 y §7.2; SC-004). Corre <b>después del
/// commit</b> del cobro (<see cref="Common.Persistence.TareasTrasElCommit"/>): nunca con la venta bloqueada.
/// <list type="bullet">
/// <item>intenta la emisión por el canal sellado (<see cref="EmitElectronicDocumentCommand"/>) y, si queda «enviado sin respuesta», consulta
/// hasta cumplir <c>Dian.EsperaMaximaPosSegundos</c> (por punto de venta);</item>
/// <item>validado → <c>deliverable</c>, la tirilla con el código único y el QR, y la entrega anotada (<c>DeliveredAt</c>);</item>
/// <item>contingencia 03 o 04 → <c>deliverable</c> y la tirilla con su leyenda, en el acto (la 03 ni siquiera espera: no hay a quién);</item>
/// <item>espera cumplida → <c>pendingDelivery</c>, sin tirilla, la caja libre y una falla más para el circuito
/// (<see cref="IRegistroDeFallasDelCanal"/>): así, repetida, abre la contingencia 03 y las ventas siguientes salen en papel;</item>
/// <item>rechazo → los motivos traducidos, sin tirilla (lo corrige quien tenga <c>ElectronicInvoicing.Documents.Correct</c>).</item>
/// </list>
/// Nunca renumera ni lanza hacia el cobro: la venta ya quedó confirmada. (nuevo)
/// </summary>
public sealed class EsperaEnLineaDelPos(
    IApplicationDbContext db,
    ISender sender,
    ILectorDeParametros parametros,
    IDateTimeService reloj,
    ConstructorDeTirilla tirilla,
    IRegistroDeFallasDelCanal? fallas = null,
    ILogger<EsperaEnLineaDelPos>? logger = null)
{
    /// <summary>Lo que dura una pausa entre consultas mientras el documento sigue «enviado sin respuesta».</summary>
    public TimeSpan PausaEntreConsultas { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Completa el resultado del cobro con la espera y, si corresponde, la tirilla del documento electrónico.</summary>
    public async Task<CheckoutResultDto> CompletarAsync(CheckoutResultDto cobro, CashRegisterPrintFormat formato, CancellationToken ct)
    {
        if (cobro.Status != DocumentStatus.Confirmed) return cobro;
        var electronico = await EstadoElectronicoDeInventario.DeAsync(db, cobro.DocumentPublicId, ct);
        if (electronico is null) return cobro;

        var cronometro = Stopwatch.StartNew();
        IReadOnlyList<MensajeDelCanalDto>? mensajes = null;
        var respondio = false;
        if (electronico.Status is ElectronicDocumentStatus.Pending or ElectronicDocumentStatus.Sent)
        {
            var espera = await EsperaAsync(electronico, cobro, ct);
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limite.CancelAfter(espera);
            try
            {
                var intento = await sender.Send(new EmitElectronicDocumentCommand(electronico.PublicId), limite.Token);
                mensajes = intento.IsSuccess ? intento.Value.Messages : null;
                var estado = intento.IsSuccess ? intento.Value.Status : electronico.Status;
                while (estado == ElectronicDocumentStatus.Sent && !limite.IsCancellationRequested)
                {
                    await Task.Delay(PausaEntreConsultas, limite.Token);
                    var consulta = await sender.Send(new QueryElectronicDocumentStatusCommand(electronico.PublicId), limite.Token);
                    if (consulta.IsFailure) break;
                    estado = consulta.Value.Status;
                    if (consulta.Value.Messages.Count > 0) mensajes = consulta.Value.Messages;
                }
                respondio = estado is not (ElectronicDocumentStatus.Pending or ElectronicDocumentStatus.Sent);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // La espera se cumplió: la venta queda pendiente de entrega y el procesador sigue con el documento.
            }
        }
        cronometro.Stop();

        var final = await db.ElectronicDocuments.FirstAsync(e => e.Id == electronico.Id, ct);
        var entregable = EntregaAlComprador.Entregable(final.Status);
        if (!entregable && final.Status is ElectronicDocumentStatus.Pending or ElectronicDocumentStatus.Sent && !respondio && fallas is not null)
        {
            try
            {
                await fallas.RegistrarFallaAsync(final.ChannelCode, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger?.LogWarning(ex, "[FE.EsperaDelPos] No se pudo contar la espera vencida de {Numero} para el circuito.", final.Number);
            }
        }

        TicketDto? ticket = null;
        if (entregable)
        {
            var venta = await db.InventoryDocuments.Include(d => d.Lines).FirstAsync(d => d.PublicId == cobro.DocumentPublicId, ct);
            ticket = await TirillaElectronicaAsync(db, tirilla, venta, final, formato, copia: false, ct);
            final.DeliveredAt ??= reloj.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        var bloque = Bloque(final, cronometro.ElapsedMilliseconds, mensajes ?? MensajesGuardados(final));
        return cobro with { Electronic = bloque, Ticket = ticket ?? (entregable ? cobro.Ticket : null) };
    }

    /// <summary>El bloque <c>electronic</c> de un documento electrónico en su estado actual.</summary>
    public static ElectronicoDeLaVentaDto Bloque(ElectronicDocument e, long esperado, IReadOnlyList<MensajeDelCanalDto>? mensajes)
    {
        var entregable = EntregaAlComprador.Entregable(e.Status);
        return new ElectronicoDeLaVentaDto(e.PublicId, e.Kind, e.Status, e.UniqueCode, e.QrContent, e.ContingencyType, esperado, entregable,
            PendingDelivery: e.DeliveredAt is null && e.Status is not (ElectronicDocumentStatus.Rejected or ElectronicDocumentStatus.CancelledWithoutReplacement),
            mensajes);
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
        var segundos = leido.IsSuccess ? leido.Value.Como<int>() : 0;
        if (segundos <= 0)
            segundos = int.Parse(ParametrosDeFacturacionElectronica.Definiciones
                .First(d => d.Clave == ParametrosDeFacturacionElectronica.EsperaMaximaPosSegundos).DefectoSeguro, CultureInfo.InvariantCulture);
        return TimeSpan.FromSeconds(segundos);
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
