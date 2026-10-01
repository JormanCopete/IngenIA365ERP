using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Application.ElectronicInvoicing.Numeracion;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing.Transactions;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Documents;

/// <summary>
/// Emitir desde el ERP el acuse de recibo (030) y/o el recibo del bien (032) de una factura del proveedor a crédito (feature 012, I5, T804;
/// FR-050, US13-4, T42; contracts/api.md §14.8 <c>POST /purchases/supplier-invoices/{id}/radian-events/emit</c>, permiso
/// <c>Inventory.Purchases.EmitRadianEvent</c>, 202; §24.7; dian.md §14.3). Responde con cada evento en <c>Pending</c> y su documento
/// electrónico; el procesador lo lleva por la máquina simplificada. (nuevo)
/// </summary>
public sealed record EmitRadianEventCommand(Guid SupplierInvoicePublicId, IReadOnlyList<SupplierInvoiceEventCode> EventCodes)
    : IRequest<Result<RadianEmissionDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

/// <summary>La respuesta de la emisión (§14.8): <c>{ events: [{ eventCode, electronicDocumentPublicId, status }] }</c>. (nuevo)</summary>
public sealed record RadianEmissionDto(IReadOnlyList<RadianEmissionEventDto> Events);

/// <summary>Un evento pedido: su código, su documento electrónico y su estado (<c>Pending</c> al pedirlo). (nuevo)</summary>
public sealed record RadianEmissionEventDto(SupplierInvoiceEventCode EventCode, Guid ElectronicDocumentPublicId, ElectronicDocumentStatus Status);

public sealed class EmitRadianEventCommandValidator : AbstractValidator<EmitRadianEventCommand>
{
    public EmitRadianEventCommandValidator()
    {
        RuleFor(x => x.SupplierInvoicePublicId).NotEmpty();
        RuleFor(x => x.EventCodes).NotEmpty().WithMessage("Indique qué eventos emitir: Receipt030, GoodsReceived032 o los dos.");
        RuleForEach(x => x.EventCodes).IsInEnum();
    }
}

/// <summary>
/// El orden del comando:
/// <list type="number">
/// <item>el módulo fuente verifica sus reglas y entrega la entrada de cada evento
/// (<see cref="IFuenteDeDocumentoElectronico.PrepararEventosRadianAsync"/>: 404 fuera del alcance, <c>Inventory.RadianEvent.NotApplicable</c>,
/// <c>.OutOfOrder</c>, <c>.ReceiptNotConfirmed</c>, <c>.DateInvalid</c>, <c>.AlreadyRegistered</c>);</item>
/// <item>la guardia (<see cref="GuardiaDeEmisionFiscal.EvaluarEventoAsync"/>): el canal vigente listo y con eventos entre sus capacidades
/// (<c>ElectronicInvoicing.NotReady</c> con <c>ElectronicInvoicing.Readiness.EventsNotSupported</c>);</item>
/// <item>por evento, un <c>COR_ElectronicDocuments</c> de <c>Kind = RadianEvent030/032</c> en <c>Pending</c> con el canal <b>vigente</b>
/// sellado (el evento es un documento nuevo), numeración propia (<see cref="NumeradorFiscal.NumerarEventoAsync"/>, T802) y su versión 1 con el
/// SHA-256 del evento; el 032 espera al documento del 030 si éste no está validado (<c>WaitsForDocumentId</c>). Un rechazado se reintenta en
/// su mismo documento: versión siguiente, mismo número (como el caso a);</item>
/// <item>el módulo enlaza cada evento con su documento (<c>INV_SupplierInvoiceEvents.ElectronicDocumentPublicId</c>) y, <b>después del commit</b>,
/// se intenta cada uno por el canal sin esperar al procesador.</item>
/// </list>
/// </summary>
public sealed class EmitRadianEventCommandHandler(
    IApplicationDbContext db,
    IEnumerable<IFuenteDeDocumentoElectronico> fuentes,
    GuardiaDeEmisionFiscal guardia,
    NumeradorFiscal numerador,
    ConstructorDelCanonico constructor,
    IDateTimeService reloj,
    IAlertas? alertas = null,
    TareasTrasElCommit? trasElCommit = null,
    ISender? sender = null)
    : IRequestHandler<EmitRadianEventCommand, Result<RadianEmissionDto>>
{
    /// <summary>El módulo fuente de las facturas del proveedor.</summary>
    public const string ModuloDeCompras = "INV";

    public async Task<Result<RadianEmissionDto>> Handle(EmitRadianEventCommand request, CancellationToken ct)
    {
        var fuente = fuentes.FirstOrDefault(f => string.Equals(f.SourceModule, ModuloDeCompras, StringComparison.OrdinalIgnoreCase));
        if (fuente is null) return Falla(new Error(ReconstruccionDelCanonico.SourceUnknownCode, "Compras no está registrado como fuente de eventos RADIAN."));

        var tipos = request.EventCodes.Distinct().OrderBy(c => c)
            .Select(c => c == SupplierInvoiceEventCode.Receipt030 ? ElectronicDocumentKind.RadianEvent030 : ElectronicDocumentKind.RadianEvent032)
            .ToList();
        var preparados = await fuente.PrepararEventosRadianAsync(request.SupplierInvoicePublicId, tipos, ct);
        if (preparados.IsFailure) return Falla(preparados.Error);

        var hoy = reloj.HoyLocal;
        foreach (var tipo in tipos)
        {
            var evaluacion = await guardia.EvaluarEventoAsync(hoy, tipo, ct);
            if (evaluacion.Veredicto == VeredictoFiscal.Blocked) return Falla(ErroresDeFacturacionElectronica.NotReady(evaluacion));
        }

        var configuracion = await db.ElectronicEmissionSettings
            .Where(s => s.ValidFrom <= hoy && (s.ValidTo == null || s.ValidTo >= hoy))
            .OrderByDescending(s => s.ValidFrom)
            .FirstAsync(ct);
        var ahora = SinFracciones(reloj.UtcNow);

        var documentos = new List<(SupplierInvoiceEventCode Codigo, ElectronicDocument Documento, ElectronicDocumentVersion? Nueva)>();
        ElectronicDocument? acuse = null;
        foreach (var p in preparados.Value.OrderBy(x => x.Tipo))
        {
            Result<(ElectronicDocument, ElectronicDocumentVersion?)> hecho = p.ElectronicDocumentPublicId is { } previo
                ? await ReintentarAsync(previo, p, ahora, ct)
                : await CrearAsync(p, configuracion, ahora, hoy, acuse, ct);
            if (hecho.IsFailure) return Falla(hecho.Error);
            var (documento, nueva) = hecho.Value;
            if (p.Tipo == ElectronicDocumentKind.RadianEvent030) acuse = documento;

            var enlace = await fuente.EnlazarEventoRadianAsync(request.SupplierInvoicePublicId, p.Tipo, documento.PublicId, ct);
            if (enlace.IsFailure) return Falla(enlace.Error);
            documentos.Add((p.Tipo == ElectronicDocumentKind.RadianEvent030 ? SupplierInvoiceEventCode.Receipt030 : SupplierInvoiceEventCode.GoodsReceived032,
                documento, nueva));
        }

        await db.SaveChangesAsync(ct);
        foreach (var (_, documento, nueva) in documentos)
        {
            if (nueva is not null) documento.CurrentVersionId = nueva.Id;
            else documento.CurrentVersionId ??= documento.Versions.OrderByDescending(v => v.VersionNumber).First().Id;
        }
        await db.SaveChangesAsync(ct);

        // Después del commit: un intento por el canal, en orden (el 032 espera al 030); el procesador retoma lo que quede.
        if (trasElCommit is not null && sender is not null)
        {
            var ids = documentos.Select(d => d.Documento.PublicId).ToList();
            trasElCommit.Agregar(async t =>
            {
                foreach (var id in ids) await sender.Send(new EmitElectronicDocumentCommand(id), t);
            });
        }

        return Result.Success(new RadianEmissionDto(documentos
            .Select(d => new RadianEmissionEventDto(d.Codigo, d.Documento.PublicId, d.Documento.Status)).ToList()));
    }

    /// <summary>Un evento que nunca se emitió: número propio, canal vigente sellado y versión 1.</summary>
    private async Task<Result<(ElectronicDocument, ElectronicDocumentVersion?)>> CrearAsync(EventoRadianPreparado p, ElectronicEmissionSetting configuracion,
        DateTime ahora, DateOnly hoy, ElectronicDocument? acuse, CancellationToken ct)
    {
        var numero = await numerador.NumerarEventoAsync(p.Tipo, configuracion.Environment, hoy, ct);
        if (numero.IsFailure) return Result.Failure<(ElectronicDocument, ElectronicDocumentVersion?)>(numero.Error);

        var emitidoEn = new DateTimeOffset(ahora, TimeSpan.Zero);
        var evento = await constructor.ConstruirEventoAsync(p.Entrada,
            new NumeracionDelEvento(configuracion, numero.Value.Prefijo, numero.Value.Consecutivo, emitidoEn), ct);
        if (evento.IsFailure) return Result.Failure<(ElectronicDocument, ElectronicDocumentVersion?)>(evento.Error);

        ElectronicDocument? espera = null;
        if (p.Tipo == ElectronicDocumentKind.RadianEvent032)
            espera = acuse ?? (p.EsperaAlDocumentoPublicId is { } id ? await db.ElectronicDocuments.FirstOrDefaultAsync(d => d.PublicId == id, ct) : null);

        var e = evento.Value.Evento;
        var documento = new ElectronicDocument
        {
            SourceModule = p.Entrada.SourceModule,
            SourceDocumentPublicId = p.Entrada.DocumentPublicId,
            SourceDocumentTypeCode = Recortar(p.Entrada.DocumentTypeCode, 10) ?? string.Empty,
            Kind = p.Tipo,
            DianDocumentTypeCode = e.DianDocumentTypeCode,
            Prefix = e.Number.Prefix,
            Consecutive = e.Number.Consecutive,
            Number = e.Number.Full,
            Environment = configuracion.Environment,
            EmissionSettingId = configuracion.Id,
            Mode = configuracion.Mode,
            ChannelCode = ReglasDeResolucion.Canal(configuracion.ChannelCode),
            SoftwareId = configuracion.SoftwareId,
            IssuedAt = ahora,
            IssueDate = DateOnly.FromDateTime(e.IssuedAt.DateTime),
            CounterpartyTaxId = Recortar(e.Supplier.TaxId, 20),
            CounterpartyName = Recortar(e.Supplier.Name, 200),
            TotalAmount = 0m,
            EmailDeliveryBy = configuracion.EmailDeliveryBy,
            WaitsForDocument = espera is { Status: not (ElectronicDocumentStatus.Validated or ElectronicDocumentStatus.ValidatedWithNotices) } ? espera : null,
            NextAttemptAt = ahora,
        };
        documento.Iniciar(contingencia03Abierta: false);
        documento.Versions.Add(new ElectronicDocumentVersion
        {
            VersionNumber = 1,
            SourceDocumentPublicId = p.Entrada.DocumentPublicId,
            Reason = DocumentVersionReason.Initial,
            CanonicalSchemaVersion = (short)e.SchemaVersion,
            CanonicalSha256 = evento.Value.CanonicalSha256,
            EconomicFingerprint = evento.Value.EconomicFingerprint,
        });
        db.ElectronicDocuments.Add(documento);
        return Result.Success<(ElectronicDocument, ElectronicDocumentVersion?)>((documento, null));
    }

    /// <summary>
    /// El reintento de un evento rechazado: el mismo documento y el mismo número, la versión siguiente con el evento vuelto a armar (quién lo
    /// pide cambió), y la alerta de rechazo atendida. La máquina lo lleva de <c>Rejected</c> a <c>Pending</c> como el caso a.
    /// </summary>
    private async Task<Result<(ElectronicDocument, ElectronicDocumentVersion?)>> ReintentarAsync(Guid previo, EventoRadianPreparado p, DateTime ahora,
        CancellationToken ct)
    {
        var documento = await db.ElectronicDocuments.Include(d => d.Versions).Include(d => d.EmissionSetting)
            .FirstOrDefaultAsync(d => d.PublicId == previo, ct);
        if (documento is null) return Result.Failure<(ElectronicDocument, ElectronicDocumentVersion?)>(ErroresDeDocumentosElectronicos.NotFound());

        var configuracion = documento.EmissionSetting ?? await db.ElectronicEmissionSettings.FirstAsync(s => s.Id == documento.EmissionSettingId, ct);
        var evento = await constructor.ConstruirEventoAsync(p.Entrada,
            new NumeracionDelEvento(configuracion, documento.Prefix, documento.Consecutive, CicloDelEventoRadian.InstanteDe(documento)), ct);
        if (evento.IsFailure) return Result.Failure<(ElectronicDocument, ElectronicDocumentVersion?)>(evento.Error);

        var transicion = documento.AplicarEvento(EventoDelDocumentoElectronico.CorregirCasoA, null, ahora);
        if (!transicion.Procede)
            return Result.Failure<(ElectronicDocument, ElectronicDocumentVersion?)>(ErroresDeDocumentosElectronicos.DeLaTransicion(transicion));

        var version = new ElectronicDocumentVersion
        {
            ElectronicDocumentId = documento.Id,
            VersionNumber = (short)(documento.Versions.Max(v => v.VersionNumber) + 1),
            SourceDocumentPublicId = p.Entrada.DocumentPublicId,
            Reason = DocumentVersionReason.CaseA,
            CanonicalSchemaVersion = (short)evento.Value.Evento.SchemaVersion,
            CanonicalSha256 = evento.Value.CanonicalSha256,
            EconomicFingerprint = evento.Value.EconomicFingerprint,
            CorrectionReason = "Reintento del evento RADIAN rechazado.",
        };
        documento.Versions.Add(version);
        documento.NextAttemptAt = ahora;
        documento.AttemptCount = 0;
        documento.LastMessagesJson = null;

        if (alertas is not null)
            await alertas.AtenderPorProcesoAsync($"{TiposDeAlerta.DocumentoRechazado}:{documento.PublicId:N}", "Se pidió emitir otra vez el evento.", ct);
        return Result.Success<(ElectronicDocument, ElectronicDocumentVersion?)>((documento, version));
    }

    private static DateTime SinFracciones(DateTime utc)
    {
        var u = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return u.AddTicks(-(u.Ticks % TimeSpan.TicksPerSecond));
    }

    private static string? Recortar(string? texto, int maximo) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Length <= maximo ? texto : texto[..maximo];

    private static Result<RadianEmissionDto> Falla(Error error) => Result.Failure<RadianEmissionDto>(error);
}
