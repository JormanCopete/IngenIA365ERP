using System.Security.Cryptography;
using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Configuration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Settlement;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Documents;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Documents;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Lending.Documents;

/// <summary>El período de un extracto (K5). (nuevo)</summary>
public sealed record PeriodoDeDocumento(DateOnly From, DateOnly To);

/// <summary>
/// Lo que responde la emisión (api.md §15, 201): el documento, su número, el código en cuatro grupos, el estado calculado, el fin de la firmeza, el
/// enlace de 60 s del PDF si se pidió descarga y el mensaje al deudor si se pidió correo. (nuevo)
/// </summary>
public sealed record IssuedLendingDocumentDto(
    Guid DocumentPublicId,
    string Number,
    string VerificationCode,
    string Status,
    DateOnly? FinalityUntil,
    EnlaceDeDescargaDto? Download,
    Guid? DebtorMessagePublicId);

/// <summary>
/// <c>POST /api/lending/documents</c> (feature 013, US13, T0784; api.md §15, adaptadores-y-archivos.md §8.1–§8.7, <c>Lending.Documents.Issue</c>, con
/// clave; T80, FR-013, FR-131): emite un documento <b>en un paso</b> —un diálogo con una confirmación—:
/// <list type="number">
/// <item>Cartera activada (si no, <c>Lending.NotActivated</c>); el extracto y el certificado tributario responden
/// <c>Lending.Document.KindNotAvailable</c> (<c>data.availableFrom = "K5"</c>, C88); sin <c>Documentos.Firmante.Nombre</c>/<c>.Cargo</c>,
/// <c>Lending.Document.SignerMissing</c>;</item>
/// <item>el titular existe; el crédito, si la clase lo lleva, está en el alcance de oficina y línea (fuera, el 404 genérico) y el titular es su
/// deudor o un codeudor vigente; con <c>Email</c>, el correo y la autorización de tratamiento general antes de pintar nada
/// (<see cref="EntregaDeDocumentosDeCartera.PrevalidarAsync"/>);</item>
/// <item>en una transacción y bajo el cerrojo de los créditos que cubre (T34): <see cref="ConstructorDeDocumentosDeCartera"/> valida y arma; el
/// código por <see cref="CodigoDeVerificacion"/> (una colisión se regenera antes de guardar); el número con el prefijo de la clase por
/// <see cref="NumeradorDeCartera"/>, al final; el PDF por <see cref="IDocumentoDeCarteraRenderer"/>, subido <b>antes</b> de guardar como adjunto de
/// módulo <c>LendingIssuedDocument</c>; el documento con la copia de la identificación del titular, su SHA-256, el firmante y la marca de ambiente
/// sellados; sus créditos; el evento <c>Issued</c> y, con <c>Email</c>, el mensaje <c>DocumentDelivery</c> (plantilla <c>ENVIODOC</c>);</item>
/// <item>la emisión se audita (<c>Lending.Document.Issued</c>) además del comando: muestra datos del titular y cuenta como lectura sensible.</item>
/// </list>
/// La misma <c>Idempotency-Key</c> devuelve el mismo documento (<c>IdempotencyBehavior</c>). (nuevo)
/// </summary>
public sealed record IssueLendingDocumentCommand(
    IssuedDocumentKind Kind,
    Guid PersonPublicId,
    Guid? LoanPublicId,
    DateOnly? CutoffDate,
    PeriodoDeDocumento? Period,
    short? Year,
    DocumentDeliveryMode Delivery) : IRequest<Result<IssuedLendingDocumentDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }

    /// <summary>¿La clase se emite sobre un crédito? (todas menos el paz y salvo general y el certificado tributario).</summary>
    public static bool LlevaCredito(IssuedDocumentKind clase) =>
        clase is IssuedDocumentKind.PaymentPlan or IssuedDocumentKind.LoanClearance or IssuedDocumentKind.BalanceCertificate or IssuedDocumentKind.Statement;
}

public sealed class IssueLendingDocumentCommandValidator : AbstractValidator<IssueLendingDocumentCommand>
{
    public IssueLendingDocumentCommandValidator()
    {
        RuleFor(x => x.Kind).IsInEnum().WithMessage("Elija la clase de documento.");
        RuleFor(x => x.Delivery).IsInEnum().WithMessage("Elija si el documento se descarga o se envía por correo.");
        RuleFor(x => x.PersonPublicId).NotEmpty().WithMessage("Elija a nombre de quién se emite el documento.");
        RuleFor(x => x.LoanPublicId).NotEmpty().When(x => IssueLendingDocumentCommand.LlevaCredito(x.Kind))
            .WithMessage("Elija el crédito del documento.");
        RuleFor(x => x.CutoffDate).Null().When(x => x.Kind != IssuedDocumentKind.BalanceCertificate)
            .WithMessage("Sólo el certificado de saldo lleva fecha de corte.");
        RuleFor(x => x.Period).Must(p => p is null || p.From <= p.To).WithMessage("La fecha inicial del período no puede ser posterior a la final.");
    }
}

public sealed class IssueLendingDocumentCommandHandler(
    IApplicationDbContext db,
    ConstructorDeDocumentosDeCartera constructor,
    EntregaDeDocumentosDeCartera entrega,
    IGeneradorDeCodigosDeVerificacion codigos,
    NumeradorDeCartera numerador,
    ICerrojoDeCartera cerrojo,
    IDocumentoDeCarteraRenderer renderizador,
    ISender sender,
    IBlobStore store,
    IOptions<LimitesDeAdjuntos> limites,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj,
    IOptions<IdentityEmailOptions>? aplicacion = null,
    IAmbienteDeDespliegue? ambiente = null,
    IAttachmentCipher? cifrado = null) : IRequestHandler<IssueLendingDocumentCommand, Result<IssuedLendingDocumentDto>>
{
    /// <summary>Cuántas veces se genera otro código ante una colisión antes de rendirse (80 bits: no debería pasar nunca).</summary>
    public const int IntentosDeCodigo = 5;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<IssuedLendingDocumentDto>> Handle(IssueLendingDocumentCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        if (!await db.LendingSetups.AsNoTracking().AnyAsync(s => s.Id == LendingSetup.IdDeLaFilaUnica && s.ActivationDate != null, ct))
            return Falla(ErroresDeMensajesAlDeudor.CarteraSinActivar());
        if (!PrefijosDeDocumentosDeCartera.DeK1.Contains(request.Kind)) return Falla(DocumentErrors.ClaseNoDisponible(request.Kind));

        var hoy = reloj.HoyLocal;
        var firmante = await constructor.FirmanteAsync(hoy, ct);
        if (firmante.IsFailure) return Falla(firmante.Error);

        var titular = await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.PublicId == request.PersonPublicId && !p.IsDeleted, ct);
        if (titular is null) return Falla(ErroresDeGobierno.PersonaInexistente());

        Loan? credito = null;
        if (IssueLendingDocumentCommand.LlevaCredito(request.Kind))
        {
            credito = await db.Loans.AsNoTracking().FirstOrDefaultAsync(l => l.PublicId == request.LoanPublicId && !l.IsDeleted, ct);
            if (credito is null) return Falla(LecturaDelCredito.Inexistente());
            var dentro = await alcance.AsegurarAsync(credito.BranchId, credito.CreditLineId, LecturaDelCredito.Inexistente(), auditoria, "Loan",
                credito.PublicId, ct);
            if (dentro.IsFailure) return Falla(dentro.Error);
            if (!await ObligadoAsync(titular.Id, credito, hoy, ct))
                return Falla(DocumentErrors.TitularNoObligado(ReferenciasDeGobierno.Nombre(titular), credito.Number));
        }

        // Primero el correo y la autorización (sin pintar ni subir nada): un envío que no puede salir no deja un PDF huérfano.
        if (request.Delivery == DocumentDeliveryMode.Email && await entrega.PrevalidarAsync(titular, credito?.Id, ct) is { } rechazo) return Falla(rechazo);

        var emitido = await TransaccionExplicita.EjecutarAsync(db, () => EmitirAsync(request, titular, credito, firmante.Value, usuario, hoy, ct), ct);
        if (emitido.IsFailure) return Falla(emitido.Error);

        var (documento, mensaje) = emitido.Value;
        await auditoria.EmitirDocumentoEmitidoAsync(documento.PublicId, documento.Kind.ToString(), documento.Number, documento.DeliveryMode.ToString(),
            documento.ContentSha256, ct);

        EnlaceDeDescargaDto? descarga = null;
        if (request.Delivery == DocumentDeliveryMode.Download)
        {
            var adjunto = await db.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == documento.PdfAttachmentPublicId, ct);
            if (adjunto is not null) descarga = await AdjuntosDirectos.FirmarDescargaAsync(store, adjunto, reloj, limites.Value, ct);
        }
        return Result.Success(new IssuedLendingDocumentDto(documento.PublicId, documento.Number, CodigoDeVerificacion.Formatear(documento.VerificationCode),
            documento.EstadoA(false, hoy).ToString(), documento.FinalityUntil, descarga, mensaje));
    }

    private async Task<Result<(IssuedLendingDocument Documento, Guid? Mensaje)>> EmitirAsync(IssueLendingDocumentCommand request, Person titular, Loan? credito,
        FirmanteDeDocumentos firmante, int usuario, DateOnly hoy, CancellationToken ct)
    {
        // El cerrojo (T34): setup compartido y los créditos que el documento puede cubrir, en orden de Id; la numeración, al final.
        await cerrojo.TomarSetupAsync(exclusivo: false, ct);
        IReadOnlyCollection<int> aBloquear = credito is not null ? [credito.Id] : await CreditosDeLaPersonaAsync(titular.Id, ct);
        if (aBloquear.Count > 0) await cerrojo.BloquearCreditosAsync(aBloquear, ModoDeEspera.Esperar, ct);

        // R = el instante de emisión, nunca anterior al último registro de los créditos que cubre (el sello del registro puede ir unos ticks por
        // delante del reloj): así el documento ve todo lo registrado antes que él y el reverso de cualquiera de esas operaciones lo anula.
        var ultimoRegistro = aBloquear.Count == 0
            ? null
            : await db.LoanControls.AsNoTracking().Where(c => aBloquear.Contains(c.LoanId)).MaxAsync(c => (DateTime?)c.LastRegisteredAt, ct);
        var ahora = reloj.UtcNow;
        var emision = ultimoRegistro is { } u && u > ahora ? DateTime.SpecifyKind(u, DateTimeKind.Utc) : ahora;
        var armado = await constructor.ArmarAsync(new PedidoDeDocumentoDeCartera(request.Kind, titular, credito, request.CutoffDate, emision, hoy), ct);
        if (armado.IsFailure) return Result.Failure<(IssuedLendingDocument, Guid?)>(armado.Error);
        var a = armado.Value;

        var codigo = await CodigoNuevoAsync(ct);
        if (codigo is null) throw new InvalidOperationException("No se pudo generar un código de verificación que no exista.");
        var numero = await numerador.NumeroDeDocumentoAsync(request.Kind, ct);

        var publicId = Guid.NewGuid();
        var pruebas = ambiente?.EsProduccion != true;
        var encabezado = await EncabezadoAsync(request.Kind, numero, emision, titular, firmante, codigo, pruebas, a.Leyendas, ct);
        var pdf = renderizador.Render(new DocumentoDeCarteraParaImprimir(encabezado, a.Cuerpo));
        var sha256 = Convert.ToHexString(SHA256.HashData(pdf)).ToLowerInvariant();

        // El adjunto de módulo, antes de la fila del documento: si el almacén falla no queda un documento que nadie puede bajar.
        var subida = await sender.Send(new UploadAttachmentCommand(AdjuntosDeModulo.DocumentoEmitidoDeCartera, publicId,
            ModelosDeDocumentoDeCartera.NombreDelArchivo(numero), "application/pdf", pdf), ct);
        if (subida.IsFailure) return Result.Failure<(IssuedLendingDocument, Guid?)>(subida.Error);

        var documento = new IssuedLendingDocument
        {
            PublicId = publicId,
            Kind = request.Kind,
            Number = numero,
            PersonId = titular.Id,
            HolderTaxIdType = Recortar(titular.IdType, IssuedLendingDocument.LargoDelTipoDeDocumento),
            HolderTaxId = Recortar(titular.TaxId, IssuedLendingDocument.LargoDelDocumento),
            HolderName = Recortar(ReferenciasDeGobierno.Nombre(titular), IssuedLendingDocument.LargoDelNombre),
            AsOfDate = a.AsOfDate,
            PeriodFrom = request.Period?.From,
            PeriodTo = request.Period?.To,
            TaxYear = request.Year,
            InformationCutoff = emision,
            VerificationCode = codigo,
            ContentSha256 = sha256,
            PdfAttachmentPublicId = subida.Value,
            EssentialDataJson = JsonSerializer.Serialize(a.DatosEsenciales, Json),
            SettlementSha256 = a.SettlementSha256,
            RulesVersion = a.RulesVersion,
            FinalityUntil = a.FinalityUntil,
            CoversCoDebts = a.CoversCoDebts,
            OutsideLendingNote = a.OutsideLendingNote,
            SignerName = Recortar(firmante.Nombre, IssuedLendingDocument.LargoDelFirmante),
            SignerTitle = Recortar(firmante.Cargo, IssuedLendingDocument.LargoDelFirmante),
            IsTestEnvironment = pruebas,
            IssuedByUserId = usuario,
            IssuedAt = emision,
            DeliveryMode = request.Delivery,
        };
        db.IssuedLendingDocuments.Add(documento);
        await db.SaveChangesAsync(ct);

        foreach (var linea in a.Lineas)
            db.IssuedDocumentLoans.Add(new IssuedDocumentLoan
            {
                IssuedDocumentId = documento.Id,
                LoanId = linea.LoanId,
                LastOperationId = linea.LastOperationId,
                LastTransactionId = linea.LastTransactionId,
                BalanceAtCutoff = linea.BalanceAtCutoff,
                PayoffOperationId = linea.PayoffOperationId,
            });
        db.IssuedDocumentEvents.Add(IssuedDocumentEvent.Emitido(documento, emision, ActorKind.Person, usuario));

        Guid? mensaje = null;
        if (request.Delivery == DocumentDeliveryMode.Email)
        {
            var encolado = await entrega.EncolarAsync(documento, titular, credito?.Id, usuario, ct);
            if (encolado.IsFailure) return Result.Failure<(IssuedLendingDocument, Guid?)>(encolado.Error);
            mensaje = encolado.Value.Mensaje.PublicId;
        }
        await db.SaveChangesAsync(ct);
        return Result.Success<(IssuedLendingDocument, Guid?)>((documento, mensaje));
    }

    /// <summary>Un código que no tenga ningún documento (guardado o de esta unidad de trabajo); nulo si se agotan los intentos.</summary>
    private async Task<string?> CodigoNuevoAsync(CancellationToken ct)
    {
        for (var intento = 0; intento < IntentosDeCodigo; intento++)
        {
            var codigo = codigos.Generar();
            var existe = db.IssuedLendingDocuments.Local.Any(d => d.VerificationCode == codigo)
                         || await db.IssuedLendingDocuments.AsNoTracking().IgnoreQueryFilters().AnyAsync(d => d.VerificationCode == codigo, ct);
            if (!existe) return codigo;
        }
        return null;
    }

    private async Task<EncabezadoDeDocumentoDeCartera> EncabezadoAsync(IssuedDocumentKind clase, string numero, DateTime emision, Person titular,
        FirmanteDeDocumentos firmante, string codigo, bool pruebas, IReadOnlyList<string> leyendas, CancellationToken ct)
    {
        var cooperativa = await db.Companies.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.Id)
            .Select(c => new { c.Name, c.TaxId, c.TaxIdCheckDigit }).FirstOrDefaultAsync(ct);
        var nit = cooperativa is null || string.IsNullOrWhiteSpace(cooperativa.TaxId)
            ? null
            : string.IsNullOrWhiteSpace(cooperativa.TaxIdCheckDigit) ? cooperativa.TaxId : $"{cooperativa.TaxId}-{cooperativa.TaxIdCheckDigit}";
        var formateado = CodigoDeVerificacion.Formatear(codigo);
        var todas = new List<string>();
        if (pruebas) todas.Add(EncabezadoDeDocumentoDeCartera.MarcaDePruebas);
        todas.AddRange(leyendas);
        var desfase = reloj.AhoraLocal.Offset;
        var (logotipo, logotipoId) = await ImagenVigenteAsync(AdjuntosDeModulo.LogotipoDeCartera, ct);
        var (firma, firmaId) = await ImagenVigenteAsync(AdjuntosDeModulo.ImagenDeFirmaDeCartera, ct);
        return new EncabezadoDeDocumentoDeCartera(clase, ModelosDeDocumentoDeCartera.Titulo(clase), numero,
            new DateTimeOffset(DateTime.SpecifyKind(emision, DateTimeKind.Utc)).ToOffset(desfase), cooperativa?.Name ?? "Cooperativa", nit,
            ReferenciasDeGobierno.Nombre(titular), titular.IdType, titular.TaxId, firmante.Nombre, firmante.Cargo, formateado,
            ModelosDeDocumentoDeCartera.UrlDeVerificacion(aplicacion?.Value.BaseUrl ?? string.Empty, formateado), pruebas, todas)
        {
            Logotipo = logotipo,
            LogotipoPublicId = logotipoId,
            ImagenDeFirma = firma,
            ImagenDeFirmaPublicId = firmaId,
        };
    }

    /// <summary>
    /// El logotipo o la imagen de la firma vigentes (T0781, I6): el último adjunto disponible de ese dueño —subir otro lo reemplaza—, leído y comprobado
    /// contra su SHA-256. Sin imagen, o si no se puede leer, el PDF va sin ella (sólo nombre, cargo, código y QR): una imagen que falta no impide emitir.
    /// </summary>
    private async Task<(byte[]? Bytes, Guid? PublicId)> ImagenVigenteAsync(string tipo, CancellationToken ct)
    {
        var vigente = await db.Attachments.AsNoTracking()
            .Where(a => a.OwnerEntityType == tipo && a.Status == EstadoDeAdjunto.Available && !a.IsDeleted)
            .OrderByDescending(a => a.Id).Select(a => (Guid?)a.PublicId).FirstOrDefaultAsync(ct);
        if (vigente is not { } id) return (null, null);
        var leido = await ContenidoDeAdjunto.LeerAsync(db, store, cifrado, id, ct);
        return leido is null ? (null, null) : (leido.Bytes, id);
    }

    /// <summary>¿La persona es deudora o codeudora vigente del crédito?</summary>
    private async Task<bool> ObligadoAsync(int personId, Loan credito, DateOnly hoy, CancellationToken ct)
    {
        if (credito.DebtorPersonId == personId) return true;
        var partes = await db.LoanParties.AsNoTracking()
            .Where(p => p.LoanId == credito.Id && p.PersonId == personId && p.Role == CreditPartyRole.CoDebtor).ToListAsync(ct);
        return partes.Any(p => p.VigenteA(hoy));
    }

    /// <summary>Los créditos en que la persona es deudora o codeudora (el paz y salvo general puede cubrir cualquiera de ellos).</summary>
    private async Task<IReadOnlyCollection<int>> CreditosDeLaPersonaAsync(int personId, CancellationToken ct)
    {
        var propios = await db.Loans.AsNoTracking().Where(l => l.DebtorPersonId == personId).Select(l => l.Id).ToListAsync(ct);
        var codeudas = await db.LoanParties.AsNoTracking().Where(p => p.PersonId == personId && p.Role == CreditPartyRole.CoDebtor)
            .Select(p => p.LoanId).ToListAsync(ct);
        return propios.Concat(codeudas).Distinct().Order().ToList();
    }

    private static string Recortar(string? texto, int largo)
    {
        var t = (texto ?? string.Empty).Trim();
        return t.Length <= largo ? t : t[..largo];
    }

    private static Result<IssuedLendingDocumentDto> Falla(Error e) => Result.Failure<IssuedLendingDocumentDto>(e);
}
