using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.BankItems;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Entities.Receipts.Agreements;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Convenios;
using IngenIA365ERP.Domain.Receipts.Huellas;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Agreements;

// Feature 015, R2 (fase 38, T0434; FR-089, FR-091, FR-092, FR-157, FR-171, FR-197; US16): la carga del archivo diario de un convenio o de una pasarela. Se carga con su
// huella (no dos veces), se valida su estructura y, si el aliado los informó, sus totales; cada pago se identifica por su referencia (con su dígito) y se aplica al cargar
// a nombre de quien carga, con un recibo por pago por AplicacionNoInteractiva (canal Agreement u Online); lo que no se identifica, o lo que el contrato rechaza, va a
// partidas por identificar con su motivo. Las líneas de una pasarela que no salieron aprobadas no se aplican. Sin botón de pago en línea (C2 = A).

/// <summary>Una línea de un archivo de convenio o de pasarela. (nuevo)</summary>
public sealed record CollectionFileLineDto(int Line, DateOnly PaymentDate, decimal Amount, string? Reference, string? PartnerSequence, string? PayerTaxId, string? PayerName,
    CollectionFileLineOutcome Outcome, string? Code, string? Message, string? ReceiptNumber, string? UnidentifiedItemNumber, Guid? PersonPublicId, Guid? ProductPublicId,
    string? GatewayOutcome);

/// <summary>Un archivo cargado, con su detalle. (nuevo)</summary>
public sealed record CollectionFileDto(Guid PublicId, string Number, bool IsGateway, Guid AgreementPublicId, string AgreementCode, string? FileName, DateOnly FileDate,
    int Lines, int Applied, int Unidentified, int Rejected, int Omitted, decimal TotalInformed, decimal TotalApplied, decimal TotalUnidentified, CollectionFileState Status,
    DateTime LoadedAt, Guid? AttachmentPublicId, Guid? ReconciliationPublicId, IReadOnlyList<CollectionFileLineDto> Detail);

/// <summary>Una línea que no se pudo leer. (nuevo)</summary>
public sealed record LineaIlegibleDeConvenioDto(int Line, string? Field, string Message);

/// <summary>La revisión previa o el resultado de la carga. (nuevo)</summary>
public sealed record CollectionFileLoadDto(bool Preview, CollectionFileDto? File, int Lines, int ToApply, int ToUnidentified, int NotApproved, int Omitted, decimal Total,
    IReadOnlyList<CollectionFileLineDto> Review, IReadOnlyList<LineaIlegibleDeConvenioDto> Illegible);

/// <summary>Los códigos con que una línea de un archivo va a partidas o se omite (los motivos de la partida). (nuevo)</summary>
public static class MotivosDeLineaDeConvenio
{
    public const string ReferenciaInvalida = "Receipts.AgreementFile.ReferenceInvalid";
    public const string DigitoInvalido = "Receipts.AgreementFile.CheckDigitInvalid";
    public const string ReferenciaDesconocida = "Receipts.AgreementFile.ReferenceNotFound";
    public const string PersonaSinProductoUnico = "Receipts.AgreementFile.PersonNeedsIdentification";
    public const string PagoYaCargado = "Receipts.AgreementFile.PaymentAlreadyLoaded";
    public const string NoAprobada = "Receipts.GatewayFile.NotApproved";

    public static string Mensaje(string codigo) => codigo switch
    {
        ReferenciaInvalida => "La referencia no tiene la estructura del convenio (longitud o caracteres).",
        DigitoInvalido => "El dígito de verificación de la referencia no corresponde.",
        ReferenciaDesconocida => "La referencia no corresponde a ningún producto ni persona.",
        PersonaSinProductoUnico => "La referencia es de la persona y tiene más de un producto (o ninguno) con valor exigible: identifíquelo a mano.",
        PagoYaCargado => "El pago ya llegó en otro archivo del convenio: no se aplica dos veces.",
        NoAprobada => "La transacción no salió aprobada en la pasarela: no se aplica.",
        _ => "El módulo rechazó el pago.",
    };

    /// <summary>Lo que la pasarela llama aprobada (sin tildes, en mayúsculas).</summary>
    public static readonly IReadOnlySet<string> Aprobadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "APROBADA", "APROBADO", "APPROVED", "OK", "00", "1", "EXITOSA", "EXITOSO", "OK_APROBADA" };
}

/// <summary>Lo común de los archivos: el DTO. (nuevo)</summary>
public static class ArchivosDeConvenio
{
    public const string ClaseDeArchivo = "AF";
    public const string ClaseDePasarela = "GF";

    public static async Task<CollectionFileDto> DtoAsync(IApplicationDbContext db, CollectionFileBase f, bool pasarela, CancellationToken ct)
    {
        var convenio = await db.CollectionAgreements.AsNoTracking().IgnoreQueryFilters().FirstAsync(c => c.Id == f.CollectionAgreementId, ct);
        List<CollectionFileLineBase> lineas = pasarela
            ? [.. await db.GatewayFileLines.AsNoTracking().Where(l => l.GatewayFileId == f.Id).OrderBy(l => l.LineNumber).ToListAsync(ct)]
            : [.. await db.AgreementFileLines.AsNoTracking().Where(l => l.AgreementFileId == f.Id).OrderBy(l => l.LineNumber).ToListAsync(ct)];
        var recibos = lineas.Where(l => l.ReceiptId is not null).Select(l => l.ReceiptId!.Value).ToList();
        var numeros = await db.IssuedReceipts.AsNoTracking().Where(r => recibos.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Number, ct);
        var partidas = lineas.Where(l => l.UnidentifiedItemId is not null).Select(l => l.UnidentifiedItemId!.Value).ToList();
        var numerosDePartida = await db.UnidentifiedItems.AsNoTracking().Where(p => partidas.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Number, ct);
        var conciliacion = await db.AgreementReconciliations.AsNoTracking()
            .Where(r => pasarela ? r.GatewayFileId == f.Id : r.AgreementFileId == f.Id).Select(r => (Guid?)r.PublicId).FirstOrDefaultAsync(ct);
        var detalle = lineas.Select(l => new CollectionFileLineDto(l.LineNumber, l.PaymentDate, l.Amount, l.Reference, l.PartnerSequence, l.PayerTaxId, l.PayerName, l.Outcome,
            l.Code, l.Message, l.ReceiptId is { } r ? numeros.GetValueOrDefault(r) : null, l.UnidentifiedItemId is { } p ? numerosDePartida.GetValueOrDefault(p) : null, null, null,
            (l as GatewayFileLine)?.GatewayOutcome)).ToList();
        return new CollectionFileDto(f.PublicId, f.Number, pasarela, convenio.PublicId, convenio.Code, f.FileName, f.FileDate, f.Lines, f.Applied, f.Unidentified, f.Rejected,
            f.Omitted, f.TotalInformed, f.TotalApplied, f.TotalUnidentified, f.Status, f.LoadedAt, f.FileAttachmentPublicId, conciliacion, detalle);
    }
}

/// <summary>
/// <c>POST /api/receipts/collection-agreements/{id}/files</c> (<c>Receipts.Agreements.LoadFile</c>, multipart, con clave; FR-089, FR-091): carga el archivo diario del
/// convenio o de la pasarela. <see cref="Preview"/> sólo revisa: dice qué se aplicaría, qué iría a partidas, qué no está aprobado y qué se omitiría, sin guardar. Con
/// <see cref="DeclaredLines"/> y <see cref="DeclaredTotal"/> (lo que el aliado informó) los totales del archivo tienen que cuadrar. (nuevo)
/// </summary>
public sealed record LoadCollectionFileCommand(
    Guid AgreementPublicId,
    string FileName,
    byte[] Content,
    bool Preview = false,
    bool AcceptIllegible = false,
    int? DeclaredLines = null,
    decimal? DeclaredTotal = null) : IRequest<Result<CollectionFileLoadDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class LoadCollectionFileCommandValidator : AbstractValidator<LoadCollectionFileCommand>
{
    public LoadCollectionFileCommandValidator()
    {
        RuleFor(x => x.AgreementPublicId).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Content).NotEmpty().WithMessage("Adjunte el archivo del convenio.");
        RuleFor(x => x.DeclaredLines).GreaterThanOrEqualTo(0).When(x => x.DeclaredLines is not null);
    }
}

public sealed class LoadCollectionFileCommandHandler(
    IApplicationDbContext db,
    IActorActual actorActual,
    IAlcanceDeRecaudo alcance,
    ICerrojoDeRecaudo cerrojo,
    NumeradorDeRecaudo numerador,
    ReferenciasDePagoDeRecaudo referencias,
    IContratoDeObligaciones obligaciones,
    AplicacionNoInteractiva aplicacion,
    PartidasPorIdentificar partidas,
    AuditoriaDeRecaudo auditoria,
    ISender sender,
    IDateTimeService reloj) : IRequestHandler<LoadCollectionFileCommand, Result<CollectionFileLoadDto>>
{
    private sealed record Leida(int Linea, DateOnly Fecha, decimal Valor, string? Referencia, string? Secuencia, string? Documento, string? Nombre, string? Resultado,
        string? ReferenciaDeLaPasarela, DateOnly? FechaDeLaPasarela);

    private sealed record Destino(Leida L, CollectionFileLineOutcome Resultado, string? Motivo, ReferenciaResuelta? Resuelta, Guid? Producto, ApplicationTarget? Objetivo);

    public async Task<Result<CollectionFileLoadDto>> Handle(LoadCollectionFileCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<CollectionFileLoadDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var convenio = await db.CollectionAgreements.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == request.AgreementPublicId, ct);
        if (convenio is null || !(await alcance.ObtenerAsync(ct)).IncluyeOficina(convenio.BranchId))
            return Result.Failure<CollectionFileLoadDto>(ErroresDeRecaudo.NoEncontrado("El convenio"));
        if (!convenio.IsActive) return Result.Failure<CollectionFileLoadDto>(ErroresDeConvenios.Inactivo());
        var hoy = reloj.HoyLocal;
        var version = await ConveniosDeRecaudo.VersionVigenteAsync(db, convenio.Id, hoy, ct);
        if (version is null) return Result.Failure<CollectionFileLoadDto>(ErroresDeConvenios.SinVersion());
        var pasarela = convenio.Kind == CollectionAgreementKind.Gateway;
        var formato = await db.BankFileFormats.AsNoTracking().Include(f => f.Fields).FirstAsync(f => f.Id == version.BankFileFormatId, ct);

        // 1. Huella del archivo (FR-197): el mismo archivo no se carga dos veces.
        var huella = HuellaDeMovimientoBancario.DelArchivo(request.Content);
        var yaCargado = pasarela
            ? await db.GatewayFiles.AsNoTracking().Where(f => f.CollectionAgreementId == convenio.Id && f.FileSha256 == huella).Select(f => f.Number).FirstOrDefaultAsync(ct)
            : await db.AgreementFiles.AsNoTracking().Where(f => f.CollectionAgreementId == convenio.Id && f.FileSha256 == huella).Select(f => f.Number).FirstOrDefaultAsync(ct);
        if (yaCargado is not null) return Result.Failure<CollectionFileLoadDto>(ErroresDeConvenios.ArchivoYaCargado(yaCargado));

        // 2. Estructura: las filas con su fecha, valor y referencia; las ilegibles se informan.
        var leido = FlatFileReader.Leer(formato, request.Content);
        var ilegibles = leido.Errors.Select(e => new LineaIlegibleDeConvenioDto(e.LineNumber, e.Field, e.Message)).ToList();
        var conError = leido.Errors.Select(e => e.LineNumber).ToHashSet();
        var leidas = new List<Leida>();
        foreach (var fila in leido.Rows.Where(r => !conError.Contains(r.LineNumber)))
        {
            var valor = fila.Monto(BankFieldSource.Amount) ?? fila.Monto(BankFieldSource.Credit);
            var fecha = fila.Fecha(BankFieldSource.MovementDate) ?? fila.Fecha(BankFieldSource.PaymentDate) ?? fila.Fecha(BankFieldSource.ValueDate);
            if (valor is not > 0m || fecha is null)
            {
                ilegibles.Add(new LineaIlegibleDeConvenioDto(fila.LineNumber, valor is not > 0m ? nameof(BankFieldSource.Amount) : nameof(BankFieldSource.MovementDate),
                    "Sin fecha o sin valor positivo: el pago no se carga."));
                continue;
            }
            leidas.Add(new Leida(fila.LineNumber, fecha.Value, valor.Value, Texto(fila.Texto(BankFieldSource.MovementReference), 60),
                Texto(fila.Texto(BankFieldSource.BankSequence), 60), Texto(fila.Texto(BankFieldSource.DepositorTaxId), 20), Texto(fila.Texto(BankFieldSource.DepositorName), 200),
                Texto(fila.Texto(BankFieldSource.Outcome), 40), Texto(fila.Texto(BankFieldSource.BankReference), 60), fila.Fecha(BankFieldSource.BankDate)));
        }
        if (leidas.Count == 0 && ilegibles.Count == 0) return Result.Failure<CollectionFileLoadDto>(ErroresDeConvenios.ArchivoVacio());
        if (request.DeclaredLines is { } dl && request.DeclaredTotal is { } dt && (dl != leidas.Count + ilegibles.Count || dt != leidas.Sum(l => l.Valor)))
            return Result.Failure<CollectionFileLoadDto>(ErroresDeConvenios.TotalesNoCuadran(dl, leidas.Count + ilegibles.Count, dt, leidas.Sum(l => l.Valor)));

        // 3. Destino de cada línea: omitida (ya cargada), no aprobada (pasarela), a partidas (referencia) o a aplicar.
        var secuencias = leidas.Where(l => l.Secuencia is not null).Select(l => l.Secuencia!).Distinct().ToList();
        var yaLlegaron = (pasarela
                ? await (from l in db.GatewayFileLines.AsNoTracking()
                         join f in db.GatewayFiles.AsNoTracking() on l.GatewayFileId equals f.Id
                         where f.CollectionAgreementId == convenio.Id && l.PartnerSequence != null && secuencias.Contains(l.PartnerSequence)
                               && l.Outcome != CollectionFileLineOutcome.Rejected
                         select l.PartnerSequence!).ToListAsync(ct)
                : await (from l in db.AgreementFileLines.AsNoTracking()
                         join f in db.AgreementFiles.AsNoTracking() on l.AgreementFileId equals f.Id
                         where f.CollectionAgreementId == convenio.Id && l.PartnerSequence != null && secuencias.Contains(l.PartnerSequence)
                               && l.Outcome != CollectionFileLineOutcome.Rejected
                         select l.PartnerSequence!).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
        var estructura = new EstructuraDeReferencia(version.ReferenceLength, version.CheckDigit);
        var vistas = new HashSet<string>(StringComparer.Ordinal);
        var destinos = new List<Destino>(leidas.Count);
        foreach (var l in leidas)
        {
            if (l.Secuencia is not null && (yaLlegaron.Contains(l.Secuencia) || !vistas.Add(l.Secuencia)))
            {
                destinos.Add(new Destino(l, CollectionFileLineOutcome.Rejected, MotivosDeLineaDeConvenio.PagoYaCargado, null, null, null));
                continue;
            }
            if (pasarela && !MotivosDeLineaDeConvenio.Aprobadas.Contains(l.Resultado ?? string.Empty))
            {
                destinos.Add(new Destino(l, CollectionFileLineOutcome.NotApproved, MotivosDeLineaDeConvenio.NoAprobada, null, null, null));
                continue;
            }
            var lectura = estructura.Leer(l.Referencia);
            if (!lectura.Valida)
            {
                var motivo = lectura.Motivo == MotivoDeReferenciaInvalida.DigitoInvalido ? MotivosDeLineaDeConvenio.DigitoInvalido : MotivosDeLineaDeConvenio.ReferenciaInvalida;
                destinos.Add(new Destino(l, CollectionFileLineOutcome.UnidentifiedItem, motivo, null, null, null));
                continue;
            }
            var resuelta = await referencias.ResolverAsync(lectura, version.ReferenceMode, version.CheckDigit, ct);
            if (resuelta is null)
            {
                destinos.Add(new Destino(l, CollectionFileLineOutcome.UnidentifiedItem, MotivosDeLineaDeConvenio.ReferenciaDesconocida, null, null, null));
                continue;
            }
            var (producto, objetivo) = (resuelta.ProductPublicId, resuelta.Target);
            if (producto is null)
            {
                var lista = await obligaciones.ListarAsync(new SolicitudDeListado(ClavesDeRecaudo.Recaudador, resuelta.PersonPublicId, l.Fecha), ct);
                var exigibles = (lista.Value?.Obligations ?? []).Where(o => o.DueToday > 0m).ToList();
                if (exigibles.Count != 1)
                {
                    destinos.Add(new Destino(l, CollectionFileLineOutcome.UnidentifiedItem, MotivosDeLineaDeConvenio.PersonaSinProductoUnico, resuelta, null, null));
                    continue;
                }
                (producto, objetivo) = (exigibles[0].ObligationPublicId, ApplicationTarget.Loan);
            }
            destinos.Add(new Destino(l, CollectionFileLineOutcome.Applied, null, resuelta, producto, objetivo));
        }
        var revision = destinos.Select(d => new CollectionFileLineDto(d.L.Linea, d.L.Fecha, d.L.Valor, d.L.Referencia, d.L.Secuencia, d.L.Documento, d.L.Nombre, d.Resultado,
            d.Motivo, d.Motivo is null ? null : MotivosDeLineaDeConvenio.Mensaje(d.Motivo), null, null, d.Resuelta?.PersonPublicId, d.Producto, d.L.Resultado)).ToList();
        var resumen = new CollectionFileLoadDto(request.Preview, null, leidas.Count + ilegibles.Count, destinos.Count(d => d.Resultado == CollectionFileLineOutcome.Applied),
            destinos.Count(d => d.Resultado == CollectionFileLineOutcome.UnidentifiedItem), destinos.Count(d => d.Resultado == CollectionFileLineOutcome.NotApproved),
            destinos.Count(d => d.Motivo == MotivosDeLineaDeConvenio.PagoYaCargado), leidas.Sum(l => l.Valor), revision, ilegibles);
        if (request.Preview) return Result.Success(resumen);
        if (ilegibles.Count > 0 && !request.AcceptIllegible)
            return Result.Failure<CollectionFileLoadDto>(ErroresDeConvenios.ArchivoIlegible(ilegibles.Take(100).Cast<object>().ToList()));

        // 4. Cerrojos de la tanda antes de numerar: configuración y todos los pagadores, en orden.
        await cerrojo.TomarSetupAsync(ct: ct);
        var pagadores = destinos.Where(d => d.Resultado == CollectionFileLineOutcome.Applied).Select(d => d.Resuelta!.PersonId).Distinct().Order().ToList();
        if (pagadores.Count > 0) await cerrojo.BloquearPagadoresAsync(pagadores, ct);

        // 5. La fila del archivo, su adjunto y la corrida.
        var clase = pasarela ? ArchivosDeConvenio.ClaseDePasarela : ArchivosDeConvenio.ClaseDeArchivo;
        CollectionFileBase archivo = pasarela ? new GatewayFile() : new AgreementFile();
        (archivo.CollectionAgreementId, archivo.CollectionAgreementVersionId, archivo.FileName, archivo.FileSha256, archivo.LoadedByUserId, archivo.LoadedAt,
            archivo.FileDate, archivo.Lines, archivo.TotalInformed) = (convenio.Id, version.Id, Texto(request.FileName, 200), huella, usuario, reloj.UtcNow,
            leidas.Count > 0 ? leidas.Max(l => l.Fecha) : hoy, leidas.Count + ilegibles.Count, leidas.Sum(l => l.Valor));
        var subida = await sender.Send(new UploadAttachmentCommand(AdjuntosDeModulo.ArchivoDeConvenio, archivo.PublicId, archivo.FileName ?? "convenio.txt",
            formato.ContentType ?? "text/plain", request.Content), ct);
        if (subida.IsFailure) return Result.Failure<CollectionFileLoadDto>(subida.Error);
        archivo.FileAttachmentPublicId = subida.Value;
        await numerador.NumerarAsync(archivo, clase, clase, ct);
        if (archivo is GatewayFile g) db.GatewayFiles.Add(g);
        else db.AgreementFiles.Add((AgreementFile)archivo);
        await db.SaveChangesAsync(ct);
        var corrida = await CorridasNoInteractivas.AbrirAsync(db, ProcessingRunKind.AgreementFile, archivo.PublicId, archivo.Number, null, usuario, reloj.UtcNow, ct);
        await db.SaveChangesAsync(ct);
        archivo.ProcessingRunId = corrida.Id;

        // 6. Cada línea es una unidad: su recibo por el contrato o su partida con el motivo.
        var canal = pasarela ? ReceiptChannel.Online : ReceiptChannel.Agreement;
        var recibos = new List<Receipt>();
        var rechazadas = new List<Domain.Entities.Receipts.Banks.ProcessingRunLine>();
        foreach (var d in destinos)
        {
            CollectionFileLineBase linea = pasarela
                ? new GatewayFileLine { GatewayFileId = archivo.Id, GatewayOutcome = d.L.Resultado, GatewayReference = d.L.ReferenciaDeLaPasarela, GatewayDate = d.L.FechaDeLaPasarela }
                : new AgreementFileLine { AgreementFileId = archivo.Id };
            (linea.LineNumber, linea.PaymentDate, linea.Amount, linea.Reference, linea.PartnerSequence, linea.PayerTaxId, linea.PayerName) =
                (d.L.Linea, d.L.Fecha, d.L.Valor, d.L.Referencia, d.L.Secuencia, d.L.Documento, d.L.Nombre);
            var resultado = d.Resultado;
            var motivo = d.Motivo;
            string? mensaje = motivo is null ? null : MotivosDeLineaDeConvenio.Mensaje(motivo);
            if (resultado == CollectionFileLineOutcome.Applied)
            {
                var clave = string.Create(CultureInfo.InvariantCulture, $"{ClavesDeRecaudo.Codigo}:convenio:{archivo.PublicId:N}:{d.L.Linea}");
                var lineaDeCorrida = await CorridasNoInteractivas.LineaAsync(db, corrida, $"{archivo.Number}·{d.L.Linea}", clave, d.L.Valor,
                    new { d.L.Linea, d.L.Referencia, d.Producto, d.L.Valor }, ct);
                var r = await aplicacion.AplicarAsync(new LineaNoInteractiva(clave, canal, d.Resuelta!.PersonPublicId, d.L.Fecha, d.L.Valor, convenio.BranchId,
                    [new ParteNoInteractiva(d.Objetivo ?? ApplicationTarget.Loan, d.Producto, d.Resuelta.PersonPublicId, d.L.Valor)], ClaseDeMedioDeRecaudo.Transfer,
                    PaymentMeansClass.Transfer, convenio.OwnBankAccountPublicId, d.L.Referencia ?? archivo.Number, CerrojosTomados: true), ct);
                CorridasNoInteractivas.Anotar(corrida, lineaDeCorrida, r, reloj.UtcNow);
                if (r.Outcome is ProcessingLineOutcome.Applied or ProcessingLineOutcome.AlreadyApplied && r.Receipt is not null)
                {
                    linea.ReceiptId = r.Receipt.Id;
                    if (r.Outcome == ProcessingLineOutcome.Applied) recibos.Add(r.Receipt);
                    var fila = d.Resuelta.Fila ?? await referencias.RegistrarAsync(d.Resuelta.PersonId, estructura.Leer(d.L.Referencia).Base,
                        version.ReferenceMode, d.Producto, d.Objetivo, d.Resuelta.ProductNumber, version.CheckDigit, ct);
                    if (fila.Id == 0) await db.SaveChangesAsync(ct);
                    linea.PaymentReferenceId = fila.Id;
                }
                else
                {
                    rechazadas.Add(lineaDeCorrida);
                    (resultado, motivo, mensaje) = (CollectionFileLineOutcome.UnidentifiedItem, r.Code ?? "Receipts.Receipt.ContractUnavailable",
                        r.Message ?? MotivosDeLineaDeConvenio.Mensaje(string.Empty));
                }
            }
            if (resultado == CollectionFileLineOutcome.UnidentifiedItem)
            {
                var partida = await partidas.ReconocerDeArchivoAsync(convenio.OwnBankAccountPublicId, convenio.BranchId, d.L.Fecha, d.L.Valor, d.L.Referencia, d.L.Nombre,
                    d.L.Documento, archivo.Number, d.L.Linea, motivo ?? MotivosDeLineaDeConvenio.ReferenciaDesconocida, ct);
                linea.UnidentifiedItemId = partida.Id;
            }
            (linea.Outcome, linea.Code, linea.Message) = (resultado, motivo, mensaje);
            if (linea is GatewayFileLine gl) db.GatewayFileLines.Add(gl);
            else db.AgreementFileLines.Add((AgreementFileLine)linea);
            switch (resultado)
            {
                case CollectionFileLineOutcome.Applied:
                    archivo.Applied++;
                    archivo.TotalApplied += d.L.Valor;
                    break;
                case CollectionFileLineOutcome.UnidentifiedItem:
                    archivo.Unidentified++;
                    archivo.TotalUnidentified += d.L.Valor;
                    break;
                case CollectionFileLineOutcome.Rejected:
                    archivo.Omitted++;
                    break;
                default:
                    archivo.Rejected++;
                    break;
            }
        }
        archivo.Rejected += ilegibles.Count;
        archivo.Status = archivo.Applied == 0 && archivo.Unidentified == 0
            ? CollectionFileState.Rejected
            : archivo.Unidentified > 0 || archivo.Rejected > 0 || archivo.Omitted > 0 ? CollectionFileState.AppliedWithExceptions : CollectionFileState.Applied;
        await CorridasNoInteractivas.CerrarTandaAsync(db, auditoria, corrida, recibos, rechazadas, reloj.UtcNow, ct);
        await auditoria.AgregarAsync(pasarela ? "Receipts.GatewayFile.Loaded" : "Receipts.AgreementFile.Loaded", pasarela ? "GatewayFile" : "AgreementFile", archivo.PublicId,
            null, new { number = archivo.Number, agreement = convenio.Code, lines = archivo.Lines, applied = archivo.Applied, unidentified = archivo.Unidentified,
                omitted = archivo.Omitted, rejected = archivo.Rejected, total = archivo.TotalInformed, sha256 = huella, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(resumen with { Preview = false, File = await ArchivosDeConvenio.DtoAsync(db, archivo, pasarela, ct) });
    }

    private static string? Texto(string? texto, int largo)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var t = texto.Trim();
        return t.Length <= largo ? t : t[..largo];
    }
}

/// <summary><c>GET /api/receipts/collection-agreements/files?agreement=</c> y <c>…/gateway-files</c> (<c>Receipts.Agreements.View</c>): los archivos cargados. (nuevo)</summary>
public sealed record ListCollectionFilesQuery(Guid? AgreementPublicId = null, bool Gateway = false) : IRequest<Result<IReadOnlyList<CollectionFileDto>>>;

public sealed class ListCollectionFilesQueryHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance)
    : IRequestHandler<ListCollectionFilesQuery, Result<IReadOnlyList<CollectionFileDto>>>
{
    public async Task<Result<IReadOnlyList<CollectionFileDto>>> Handle(ListCollectionFilesQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var convenios = (await db.CollectionAgreements.AsNoTracking().ToListAsync(ct))
            .Where(c => mio.IncluyeOficina(c.BranchId) && (request.AgreementPublicId is null || c.PublicId == request.AgreementPublicId)).Select(c => c.Id).ToList();
        List<CollectionFileBase> archivos = request.Gateway
            ? [.. await db.GatewayFiles.AsNoTracking().Where(f => convenios.Contains(f.CollectionAgreementId)).OrderByDescending(f => f.Id).Take(100).ToListAsync(ct)]
            : [.. await db.AgreementFiles.AsNoTracking().Where(f => convenios.Contains(f.CollectionAgreementId)).OrderByDescending(f => f.Id).Take(100).ToListAsync(ct)];
        var lista = new List<CollectionFileDto>(archivos.Count);
        foreach (var f in archivos) lista.Add(await ArchivosDeConvenio.DtoAsync(db, f, request.Gateway, ct));
        return Result.Success<IReadOnlyList<CollectionFileDto>>(lista);
    }
}
