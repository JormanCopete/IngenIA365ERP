using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Documents.Efectos;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Documents;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Integration;

/// <summary>El modo que sellará un documento y los orígenes de los que es derivado (vacío si no lo es). (nuevo)</summary>
public sealed record ModoDelDocumento(PostingMode? Modo, IReadOnlyList<InventoryDocument> Origenes);

/// <summary>
/// Cómo se emiten los mensajes de un documento de Inventario, en un solo sitio (feature 012, T520, T521; decisiones-transversales §1.3;
/// contracts/contabilidad.md §4.1): lo usan la confirmación (<see cref="ConfirmacionDeDocumento"/>) para emitir y la validación previa
/// —dentro de la confirmación y en <c>PrevalidateInventoryDocumentQuery</c>— para preguntar a Contabilidad por <b>los mismos
/// sobres</b> que se emitirían. Así lo evaluado y lo emitido son el mismo contrato (SC-021). (nuevo)
/// <list type="bullet">
/// <item><see cref="ModoAsync"/>: el modo que se sellará —el del original en una anulación, el del origen en un derivado, si no
/// <c>Contabilidad.ModoDePaso</c> vigente del tipo— (data-model §5.3; FR-075, FR-079);</item>
/// <item><see cref="OrigenAsync"/> y <see cref="Solicitudes"/>: el origen y las solicitudes de emisión (una por evento; cada
/// <c>AjusteDeCostoReconocido</c> es su propia unidad, mensajes.md §9);</item>
/// <item><see cref="ModoDeEntregaAsync"/>: el modo sellado como entrega, con su horario si es por lotes;</item>
/// <item><see cref="Sobres"/>: los sobres tal como se emitirían, con un <c>messageId</c> provisional que no se guarda.</item>
/// </list>
/// </summary>
public sealed class MensajesDelDocumento(IApplicationDbContext db, ILectorDeParametros parametros, IContabilidadParaInventario? contabilidad = null)
{
    /// <summary>El modo que se sellará al confirmar (y, en un derivado, sus orígenes).</summary>
    public async Task<Result<ModoDelDocumento>> ModoAsync(ContextoDeEfecto contexto, IEfectoDeClase efecto, DateOnly hoy, CancellationToken ct)
    {
        // Los relacionados (la anulación) no leen el parámetro: copian el modo de su original (FR-079); los derivados (la factura o
        // la devolución contra sus recepciones, US9) copian el de su origen (data-model §5.3).
        if (contexto.Original is { } original) return Result.Success(new ModoDelDocumento(original.PostingMode, []));
        var origenes = await efecto.OrigenesDelModoAsync(contexto, ct);
        if (origenes.Count > 0) return Result.Success(new ModoDelDocumento(origenes[0].PostingMode, origenes));
        var modo = await ModoASellarAsync(contexto.Tipo, contexto.Clase, hoy, ct);
        return modo.IsFailure ? Result.Failure<ModoDelDocumento>(modo.Error) : Result.Success(new ModoDelDocumento(modo.Value, []));
    }

    /// <summary>
    /// <c>Contabilidad.ModoDePaso</c> vigente a la fecha de confirmación, del tipo si tiene excepción o el general, en toda clase que
    /// emite mensajes de negocio a Contabilidad; nulo en las demás. Sin vigencia guardada y sin contabilidad iniciada, «no pasa»
    /// (<see cref="ModoDePasoVigente"/>).
    /// </summary>
    private async Task<Result<PostingMode?>> ModoASellarAsync(InventoryDocumentType tipo, DescripcionDeClase clase, DateOnly hoy, CancellationToken ct)
    {
        if (!ConfirmacionDeDocumento.EmiteNegocioAContabilidad(clase)) return Result.Success<PostingMode?>(null);
        var leido = await ModoDePasoVigente.LeerAsync(parametros, contabilidad, hoy, ParameterScopeKind.DocumentType, tipo.Id, ct);
        if (leido.IsFailure) return Result.Failure<PostingMode?>(leido.Error);
        return Result.Success<PostingMode?>(leido.Value switch
        {
            ModoDePasoVigente.PorLotes => PostingMode.Batch,
            ModoDePasoVigente.NoPasa => PostingMode.NotPosted,
            _ => PostingMode.Online,
        });
    }

    /// <summary>El origen de los mensajes del documento (sucursal, centro, bodega y contraparte por PublicId).</summary>
    public async Task<OrigenDeEmision> OrigenAsync(InventoryDocument documento, InventoryDocumentType tipo, string? codigoDeBodega, CancellationToken ct)
    {
        var sucursal = await db.Branches.AsNoTracking().Where(s => s.Id == documento.BranchId).Select(s => s.PublicId).FirstAsync(ct);
        Guid? centro = documento.CostCenterId is int cc ? await db.CostCenters.AsNoTracking().Where(c => c.Id == cc).Select(c => c.PublicId).FirstAsync(ct) : null;
        Guid? persona = documento.CounterpartyPersonId is int p ? await db.People.AsNoTracking().Where(x => x.Id == p).Select(x => x.PublicId).FirstAsync(ct) : null;
        return new OrigenDeEmision(
            MessageOriginKind.Document, documento.PublicId, documento.Class.ToString(), tipo.Code,
            VistaDeDocumentos.NumeroVisible(documento.Prefix, documento.Number) ?? string.Empty,
            documento.OperationDate, sucursal, centro, codigoDeBodega, persona);
    }

    /// <summary>
    /// Las solicitudes de emisión de los contenidos de un documento: una por el evento (original, derivado o anulación) y una por cada
    /// <c>AjusteDeCostoReconocido</c>, cuya clave es <c>Confirmation:{afectado:N}</c> y que sigue el destino del mensaje del afectado.
    /// </summary>
    public static IReadOnlyList<SolicitudDeEmision> Solicitudes(
        OrigenDeEmision origen,
        InventoryDocument? original,
        IReadOnlyList<InventoryDocument> origenes,
        IReadOnlyList<object> contenidos,
        ModoDeEntrega modoPropio,
        PrevalidationOutcome validacion)
    {
        var solicitudes = new List<SolicitudDeEmision>();
        var ajustesDeCosto = contenidos.OfType<AjusteDeCostoReconocidoV1>().ToList();
        var delEvento = contenidos.Where(c => c is not AjusteDeCostoReconocidoV1).ToList();

        if (delEvento.Count > 0)
        {
            if (original is null && origenes.Count > 0)
            {
                // Derivado: sigue el destino del mensaje de su origen (FR-075) y depende de las cadenas de todos sus orígenes.
                var raiz = origenes[0];
                solicitudes.Add(new SolicitudDeEmision(origen, ClavesDeEvento.Confirmacion, delEvento, new ModoDeEntrega.Heredado(raiz.PublicId),
                    CadenasDeLasQueDepende: origenes.Select(o => o.PublicId).ToList(),
                    Relacionado: new DocumentoRelacionado(raiz.PublicId, raiz.Class.ToString(), VistaDeDocumentos.NumeroVisible(raiz.Prefix, raiz.Number) ?? string.Empty),
                    ValidacionPrevia: validacion));
            }
            else if (original is null)
            {
                solicitudes.Add(new SolicitudDeEmision(origen, ClavesDeEvento.Confirmacion, delEvento, modoPropio, ValidacionPrevia: validacion));
            }
            else
            {
                var informativo = !ConfirmacionDeDocumento.EmiteNegocioAContabilidad(ClasesDeDocumento.De(original.Class));
                solicitudes.Add(new SolicitudDeEmision(origen, ClavesDeEvento.Confirmacion, delEvento,
                    new ModoDeEntrega.Heredado(original.PublicId),
                    Relacionado: new DocumentoRelacionado(original.PublicId, original.Class.ToString(),
                        VistaDeDocumentos.NumeroVisible(original.Prefix, original.Number) ?? string.Empty),
                    ValidacionPrevia: validacion,
                    KindDelOriginal: informativo ? IntegrationMessageKind.Informational : IntegrationMessageKind.Business));
            }
        }

        foreach (var ajuste in ajustesDeCosto)
        {
            var afectado = ajuste.AffectedDocument;
            solicitudes.Add(new SolicitudDeEmision(origen, ClavesDeEvento.ConfirmacionPor(afectado.PublicId), [ajuste],
                new ModoDeEntrega.Heredado(afectado.PublicId),
                CadenasDeLasQueDepende: [afectado.PublicId],
                Relacionado: new DocumentoRelacionado(afectado.PublicId, afectado.DocumentClass.ToString(), afectado.Number),
                ValidacionPrevia: validacion));
        }
        return solicitudes;
    }

    /// <summary>
    /// El modo sellado como entrega: por lotes, con su horario (<see cref="ClavesDeLote.Horario"/>) y, si el disparador es el cierre
    /// del período, la clave de su período (<see cref="ClavesDeLote.Periodo"/>, T522).
    /// </summary>
    public async Task<ModoDeEntrega> ModoDeEntregaAsync(InventoryDocument documento, InventoryDocumentType tipo, DateOnly hoy, CancellationToken ct)
    {
        switch (documento.PostingMode)
        {
            case PostingMode.Batch:
                var disparador = await TextoAsync(ParametrosDeInventario.ContabilidadDisparadorDeLote, "HoraDiaria");
                var granularidad = await TextoAsync(ParametrosDeInventario.ContabilidadGranularidad, "PorDocumento");
                var hora = await TextoAsync(ParametrosDeInventario.ContabilidadHoraDeLote, string.Empty);
                TimeOnly? horaDeLote = disparador == ClavesDeLote.HoraDiaria
                    && TimeOnly.TryParse(hora, System.Globalization.CultureInfo.InvariantCulture, out var h) ? h : null;
                var alcance = disparador == ClavesDeLote.CierreDePeriodo
                    ? ClavesDeLote.Periodo(documento.OperationDate.Year, documento.OperationDate.Month)
                    : null;
                return new ModoDeEntrega.Sellado(DeliveryMode.Batch, ClavesDeLote.Horario(tipo.Code, disparador, horaDeLote, granularidad), alcance);
            case PostingMode.NotPosted:
                return new ModoDeEntrega.Sellado(DeliveryMode.NotPosted);
            default:
                return new ModoDeEntrega.Sellado(DeliveryMode.Online);
        }

        async Task<string> TextoAsync(string clave, string defecto)
        {
            var leido = await parametros.LeerAsync(ParametrosDeInventario.Modulo, clave, hoy, ParameterScopeKind.DocumentType, tipo.Id, ct);
            return leido.IsSuccess && !string.IsNullOrWhiteSpace(leido.Value.Texto) ? leido.Value.Texto : defecto;
        }
    }

    /// <summary>
    /// Los sobres tal como se emitirían (contracts/contabilidad.md §4.1): el mismo contenido, tipo, clave de evento, origen,
    /// relacionado, sucursal y contraparte que pondrá <see cref="EmisorDeMensajes"/>, con un <c>messageId</c> provisional que no se
    /// guarda. La raíz de la cadena es la del relacionado (o el propio documento): Contabilidad no la usa para evaluar.
    /// </summary>
    public static IReadOnlyList<MensajeContableDto> Sobres(IReadOnlyList<SolicitudDeEmision> solicitudes, Guid? usuarioCentral, string? usuario, DateTimeOffset ahora)
    {
        var sobres = new List<MensajeContableDto>();
        foreach (var solicitud in solicitudes)
        {
            foreach (var contenido in solicitud.Contenidos)
            {
                var tipo = CatalogoDeMensajesV1.Buscar(contenido.GetType());
                if (tipo is null) continue;
                var origen = solicitud.Origen;
                var sobre = new IntegrationEnvelopeV1
                {
                    MessageId = Guid.NewGuid(),
                    Type = tipo.Type,
                    Version = tipo.Version,
                    Kind = tipo.Kind ?? solicitud.KindDelOriginal ?? IntegrationMessageKind.Business,
                    OriginModule = EmisorDeMensajes.ModuloDeOrigen,
                    OriginEventKey = solicitud.OriginEventKey,
                    Origin = new MessageOriginV1
                    {
                        Kind = origen.Kind,
                        DocumentClass = Enum.TryParse<DocumentClass>(origen.DocumentClass, out var clase) ? clase : null,
                        DocumentTypeCode = origen.DocumentTypeCode,
                        Number = origen.Number,
                        PublicId = origen.PublicId,
                        OperationDate = origen.OperationDate,
                        FiscalUniqueCode = origen.FiscalUniqueCode,
                    },
                    Related = solicitud.Relacionado is { } r
                        ? new DocumentRefV1
                        {
                            PublicId = r.PublicId,
                            DocumentClass = Enum.TryParse<DocumentClass>(r.DocumentClass, out var claseRelacionada) ? claseRelacionada : default,
                            Number = r.Number,
                        }
                        : null,
                    ChainRootPublicId = solicitud.Relacionado?.PublicId ?? origen.PublicId,
                    BranchPublicId = origen.BranchPublicId,
                    CostCenterPublicId = origen.CostCenterPublicId,
                    WarehouseCode = origen.WarehouseCode,
                    PersonPublicId = origen.PersonPublicId,
                    Currency = EmisorDeMensajes.Moneda,
                    ExchangeRate = 1m,
                    OriginUser = new UserRefV1 { CentralUserId = usuarioCentral, Name = usuario ?? string.Empty },
                    EmittedAt = ahora,
                    Payload = contenido,
                };
                sobres.Add(new MensajeContableDto(sobre, contenido));
            }
        }
        return sobres;
    }

    /// <summary>¿Alguno de los sobres es un mensaje de negocio a Contabilidad? (lo único que la validación previa evalúa).</summary>
    public static bool HayNegocioAContabilidad(IReadOnlyList<MensajeContableDto> sobres) =>
        sobres.Any(m => m.Envelope.Kind == IntegrationMessageKind.Business
            && CatalogoDeMensajesV1.Todos.Any(t => t.Type == m.Envelope.Type && t.Destination == IntegrationDestinations.Accounting));
}
