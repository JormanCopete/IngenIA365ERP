using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.ElectronicInvoicing;
using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Application.ElectronicInvoicing.Documents;
using IngenIA365ERP.Application.ElectronicInvoicing.Numeracion;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Documents.Efectos;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Inventory.Integration;

/// <summary>
/// El flujo fiscal de la confirmación de las clases fiscales electrónicas (feature 012, I4, T734, T735, T738; decisiones-transversales §1.3;
/// FR-038, FR-058, FR-063, FR-066, FR-067; contracts/dian.md §6.1, §7.2, §9): <c>SalesInvoice</c>, <c>SalesInvoiceFromShipments</c>,
/// <c>PosEquivalentDocument</c>, <c>CreditNote</c>, <c>PosAdjustmentNote</c>, <c>SupportDocument</c> y <c>SupportDocumentAdjustmentNote</c>.
/// <list type="number">
/// <item><b>Paso 4</b> (<see cref="EvaluarAsync"/>): el veredicto de <see cref="GuardiaDeEmisionFiscal"/> para las clases que no son de venta
/// (las de venta ya lo pidieron en <c>ReglasDeConfirmacionDeVenta</c>, antes de la aprobación), y las reglas de la contingencia 03: un tipo de
/// contingencia sin 03 abierta no confirma (<c>ElectronicInvoicing.Contingency.NotOpen</c>) y, con la 03 abierta, un tipo normal en oficina
/// (sin caja) responde <c>ElectronicInvoicing.NotReady</c> nombrando el tipo de contingencia que se debe usar
/// (<c>ElectronicInvoicing.Contingency.UseContingencyType</c>); en el POS el cobro ya tomó el tipo del rol de contingencia.</item>
/// <item><b>Paso 8</b> (<see cref="NumerarAsync"/>): <see cref="NumeradorFiscal"/> con el canal, el software y el ambiente de la configuración
/// vigente y, con un tipo de contingencia, la resolución <c>Contingency</c> que respalda a la clase. Un documento que ya llega numerado —el
/// reemplazo del caso b— no se renumera. Las notas numeran por su consecutivo (<c>Numerador</c>).</item>
/// <item><b>Paso 10</b> (<see cref="RegistrarAsync"/>), después del guardado y en la misma transacción: arma el canónico con el único
/// constructor desde lo que <b>se acaba de guardar</b> —la misma lectura de la fuente y los mismos datos sellados que usará la emisión, así las
/// dos construcciones dan los mismos bytes— y registra el documento electrónico en <c>Pending</c> (o <c>IssuerContingency</c>) con su
/// versión 1 (<see cref="RegistroDeDocumentoElectronico"/>); una nota fija <c>CorrectsDocumentId</c> y, si el original todavía no está
/// validado (contingencia o en curso), <c>WaitsForDocumentId</c>. Un dato DIAN faltante no confirma
/// (<c>ElectronicInvoicing.Document.MissingData</c>). Después del commit sube el canónico (<see cref="GuardadoDeArtefactos"/>).</item>
/// </list>
/// La fuente (que confirma por este mismo flujo) y el guardado de artefactos (que la usa) se resuelven al usarlos: pedirlos en el constructor
/// cerraría un ciclo en el contenedor. Scoped: recuerda por documento lo decidido entre pasos. (nuevo)
/// </summary>
public sealed class EmisionFiscalDeLaConfirmacion(
    IApplicationDbContext db,
    GuardiaDeEmisionFiscal guardia,
    NumeradorFiscal numerador,
    RegistroDeDocumentoElectronico registro,
    ConstructorDelCanonico constructor,
    IServiceProvider servicios,
    RechazoFiscalEnCurso? rechazoEnCurso = null,
    TareasTrasElCommit? trasElCommit = null,
    ILogger<EmisionFiscalDeLaConfirmacion>? logger = null) : IPasoFiscalDeConfirmacion
{
    public const string UseContingencyTypeCode = "ElectronicInvoicing.Contingency.UseContingencyType";

    private readonly Dictionary<Guid, NumeroFiscal> _numeros = [];
    private readonly Dictionary<Guid, Guid> _esperas = [];
    private readonly Dictionary<Guid, ElectronicDocument> _registrados = [];

    /// <summary>
    /// La factura de <c>invoice-instead</c> (T739) se transmite después de la nota de ajuste que anula el documento equivalente: al registrar
    /// <paramref name="documento"/>, su <c>WaitsForDocumentId</c> será el documento electrónico de <paramref name="esperaA"/>. (nuevo)
    /// </summary>
    public void EsperarA(Guid documento, Guid esperaA) => _esperas[documento] = esperaA;

    /// <summary>El documento electrónico que registró esta petición para el comercial <paramref name="documento"/>, si lo hay.</summary>
    public ElectronicDocument? RegistradoPara(Guid documento) => _registrados.GetValueOrDefault(documento);

    // ------------------------------------------------------------------------------------------------ paso 4 --

    public async Task<Result> EvaluarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var documento = contexto.Documento;
        var tipo = contexto.Tipo;
        if (GuardiaDeEmisionFiscal.TipoElectronicoDe(documento.Class) is null) return Result.Success();
        if (rechazoEnCurso?.ReemplazaA(documento.PublicId) is not null) return Result.Success();

        if (ClasesDeDocumento.De(documento.Class).Group != DocumentClassGroup.Sales)
        {
            var caja = documento.CashRegisterId is int c ? await db.CashRegisters.AsNoTracking().FirstOrDefaultAsync(r => r.Id == c, ct) : null;
            var evaluacion = await guardia.EvaluarAsync(documento.OperationDate, tipo, caja, ct);
            if (!evaluacion.Admite(documento.Class))
                return Result.Failure(evaluacion.Veredicto == VeredictoFiscal.Blocked
                    ? ErroresDeVentas.NotReady(evaluacion)
                    : ErroresDeVentas.FiscalClassMismatch(documento.Class, evaluacion));
        }

        var configuracion = await ConfiguracionAsync(documento.OperationDate, ct);
        if (configuracion is null) return Result.Success(); // la guardia ya bloqueó por falta de configuración
        var canal = ReglasDeResolucion.Canal(configuracion.ChannelCode);
        var abierta = await db.DianContingencyEvents.AsNoTracking()
            .AnyAsync(e => e.Type == ContingencyType.Issuer03 && e.Status == ContingencyEventStatus.Open && e.ChannelCode == canal, ct);

        if (tipo.IsContingency && !abierta) return Result.Failure(ErroresDeDocumentosElectronicos.ContingencyNotOpen(canal));

        // En oficina (sin caja) con la 03 abierta, el tipo normal de una clase numerada por resolución no numera: se usa el de contingencia.
        if (!tipo.IsContingency && abierta && documento.CashRegisterId is null && ClasesDeDocumento.De(documento.Class).NumberedBy == NumberedBy.DianResolution)
        {
            var deContingencia = await db.InventoryDocumentTypes.AsNoTracking()
                .Where(t => t.Class == documento.Class && t.IsContingency && t.IsActive)
                .OrderBy(t => t.Code).Select(t => new { t.Code, t.Name, t.FiscalPrefix }).ToListAsync(ct);
            var nombres = deContingencia.Count == 0
                ? "la cooperativa no ha creado un tipo de contingencia para esta clase"
                : "use " + string.Join(" o ", deContingencia.Select(t => $"{t.Code} {t.Name} (prefijo {t.FiscalPrefix})"));
            return Result.Failure(ErroresDeVentas.NotReady(new EvaluacionFiscal(VeredictoFiscal.Blocked, [],
            [
                new MotivoDeBloqueoFiscal(UseContingencyTypeCode,
                    $"Hay contingencia del facturador abierta en el canal {canal}: el tipo {tipo.Code} no numera; {nombres}.",
                    "Inventario › Tipos de documento", GuardiaDeEmisionFiscal.PermisoDeTiposDeDocumento, GuardiaDeEmisionFiscal.PaginaDeTiposDeDocumento),
            ])));
        }
        return Result.Success();
    }

    // ------------------------------------------------------------------------------------------------ paso 8 --

    public async Task<Result> NumerarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var documento = contexto.Documento;
        if (documento.Number is not null) return Result.Success(); // nunca se renumera (el reemplazo del caso b ya trae el suyo)
        if (GuardiaDeEmisionFiscal.TipoElectronicoDe(documento.Class) is not { } tipoElectronico) return Result.Success();
        if (ReglasDeResolucion.TipoDeResolucion(tipoElectronico) is null) return Result.Success();

        var configuracion = await ConfiguracionAsync(documento.OperationDate, ct);
        if (configuracion is null) return Result.Failure(InventoryErrors.DocumentClassNotAvailable(documento.Class));

        var numero = await numerador.NumerarDocumentoAsync(documento, new SolicitudDeNumeroFiscal(
            tipoElectronico,
            contexto.Tipo.FiscalPrefix ?? string.Empty,
            configuracion.Environment,
            configuracion.ChannelCode,
            configuracion.SoftwareId,
            documento.OperationDate,
            Contingencia03: contexto.Tipo.IsContingency), ct);
        if (numero.IsFailure) return Result.Failure(numero.Error);
        _numeros[documento.PublicId] = numero.Value;
        return Result.Success();
    }

    // ----------------------------------------------------------------------------------------------- paso 10 --

    public async Task<Result<bool>> RegistrarAsync(ContextoDeEfecto contexto, CancellationToken ct)
    {
        var documento = contexto.Documento;
        if (GuardiaDeEmisionFiscal.TipoElectronicoDe(documento.Class) is null) return Result.Success(false);
        // El reemplazo del caso b no registra otro documento electrónico: ReplaceRejectedDocumentCommand mueve el del rechazado.
        if (rechazoEnCurso?.ReemplazaA(documento.PublicId) is not null) return Result.Success(false);
        if (documento.Number is not long consecutivo) return Result.Failure<bool>(InventoryErrors.DocumentClassNotAvailable(documento.Class));

        var configuracion = await ConfiguracionAsync(documento.OperationDate, ct);
        if (configuracion is null) return Result.Failure<bool>(InventoryErrors.DocumentClassNotAvailable(documento.Class));

        var fuente = servicios.GetServices<IFuenteDeDocumentoElectronico>()
            .First(f => string.Equals(f.SourceModule, FuenteDeEmisionDeInventario.Modulo, StringComparison.OrdinalIgnoreCase));
        var entrada = await fuente.LeerAsync(documento.PublicId, ct);
        if (entrada.IsFailure) return Result.Failure<bool>(entrada.Error);

        DianNumberingResolution? resolucion = null;
        if (_numeros.TryGetValue(documento.PublicId, out var numero))
            resolucion = await db.DianNumberingResolutions.AsNoTracking().FirstAsync(r => r.Id == numero.ResolutionId, ct);

        // La nota corrige el documento electrónico de su original; si el original no está validado, espera a que lo esté (FR-066).
        ElectronicDocument? corrige = null;
        ElectronicDocument? esperaA = null;
        if (ConstructorDelCanonico.EsNota(entrada.Value.Kind))
        {
            var original = await OriginalAsync(documento, ct);
            if (original is not null)
            {
                corrige = await DelComercialAsync(original.Value, ct);
                if (corrige is not null && corrige.Status is not (ElectronicDocumentStatus.Validated or ElectronicDocumentStatus.ValidatedWithNotices))
                    esperaA = corrige;
            }
        }
        if (_esperas.TryGetValue(documento.PublicId, out var esperado)) esperaA = await DelComercialAsync(esperado, ct) ?? esperaA;

        var construido = await constructor.ConstruirAsync(entrada.Value,
            ReconstruccionDelCanonico.NumeracionDe(configuracion, resolucion, documento.Prefix, consecutivo, contexto.Tipo.IsContingency, corrige), ct);
        if (construido.IsFailure) return Result.Failure<bool>(construido.Error);

        var registrado = await registro.RegistrarAsync(new PedidoDeRegistroElectronico(
            FuenteDeEmisionDeInventario.Modulo, documento.PublicId, contexto.Tipo.Code, configuracion, construido.Value,
            resolucion?.Id, resolucion?.Kind, corrige, esperaA), ct);
        if (registrado.IsFailure) return Result.Failure<bool>(registrado.Error);
        _registrados[documento.PublicId] = registrado.Value;

        var electronico = registrado.Value;
        trasElCommit?.Agregar(t => SubirCanonicoAsync(electronico.PublicId, t));
        return Result.Success(true);
    }

    /// <summary>
    /// Después del commit: sube el canónico de la versión 1 (con su verificación contra el SHA-256 registrado). Si falla, la emisión lo vuelve
    /// a intentar antes de transmitir.
    /// </summary>
    public async Task SubirCanonicoAsync(Guid electronicoPublicId, CancellationToken ct)
    {
        var documento = await db.ElectronicDocuments.Include(d => d.Versions).Include(d => d.EmissionSetting)
            .FirstOrDefaultAsync(d => d.PublicId == electronicoPublicId, ct);
        var version = documento?.Versions.OrderByDescending(v => v.VersionNumber).FirstOrDefault();
        if (documento is null || version is null || version.CanonicalAttachmentPublicId is not null) return;
        var artefactos = servicios.GetRequiredService<GuardadoDeArtefactos>();
        var r = await artefactos.CanonicoVerificadoAsync(documento, version, ct);
        if (r.IsFailure)
        {
            logger?.LogWarning("[FE.CanonicoSinSubir] {Numero}: {Codigo} {Mensaje}", documento.Number, r.Error.Code, r.Error.Message);
            return;
        }
        await db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------------------------------------------ apoyo --

    private Task<ElectronicEmissionSetting?> ConfiguracionAsync(DateOnly fecha, CancellationToken ct) =>
        db.ElectronicEmissionSettings.AsNoTracking()
            .Where(s => s.ValidFrom <= fecha && (s.ValidTo == null || s.ValidTo >= fecha))
            .OrderByDescending(s => s.ValidFrom)
            .FirstOrDefaultAsync(ct);

    /// <summary>El <c>PublicId</c> del documento que corrige la nota (vínculo <c>NoteOf</c>, la nota es el destino).</summary>
    private async Task<Guid?> OriginalAsync(InventoryDocument nota, CancellationToken ct) =>
        await db.DocumentLinks.AsNoTracking()
            .Where(l => l.TargetDocumentId == nota.Id && l.Kind == DocumentLinkKind.NoteOf && !l.IsDeleted)
            .Join(db.InventoryDocuments.AsNoTracking(), l => l.SourceDocumentId, o => o.Id, (l, o) => (Guid?)o.PublicId)
            .FirstOrDefaultAsync(ct);

    /// <summary>El documento electrónico (seguido) de un documento comercial de Inventario, si lo tiene.</summary>
    private Task<ElectronicDocument?> DelComercialAsync(Guid comercial, CancellationToken ct) =>
        db.ElectronicDocuments.FirstOrDefaultAsync(e => e.SourceModule == FuenteDeEmisionDeInventario.Modulo && e.SourceDocumentPublicId == comercial, ct);
}
