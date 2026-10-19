using System.Security.Cryptography;
using System.Text.Json;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Configuration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Documents;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Documents;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Lending.Documents;

/// <summary>
/// Lo que se emite (feature 014, US13, T196): la clase, el titular, el firmante, quién emite —nulo si es el proceso diario (el título de la prórroga
/// automática)—, la entrega y hoy; según la clase, el crédito, la cuenta, el corte, el período, el año y lo propio de cada clase. (nuevo)
/// </summary>
public sealed record SolicitudDeEmision(
    IssuedDocumentKind Clase,
    Person Titular,
    FirmanteDeDocumentos Firmante,
    int? Usuario,
    DocumentDeliveryMode Entrega,
    DateOnly Hoy)
{
    public Loan? Credito { get; init; }
    public MemberAccount? Cuenta { get; init; }
    public DateOnly? Corte { get; init; }
    public PeriodoDeDocumento? Periodo { get; init; }
    public short? Año { get; init; }
    public bool IncluyeAhorroPermanente { get; init; }
    public bool IncluyeRestricciones { get; init; }
    public Guid? RetiroPublicId { get; init; }
    public int? VersionDeCondicionesId { get; init; }
    public Deposits.Documents.RespuestaParaDocumento? Respuesta { get; init; }
}

/// <summary>
/// La emisión de un documento de Cartera <b>dentro</b> de la unidad de trabajo de quien la pide (feature 013, US13, T0784; feature 014, US13, T196): la
/// parte de <see cref="IssueLendingDocumentCommandHandler"/> que va en su transacción, sacada aquí para que los documentos que nacen solos —el título del
/// CDAT al constituir, prorrogar y renovar; la respuesta al juzgado al registrarla— se emitan en la transacción de su operación. Bajo el cerrojo de lo que
/// cubre (setup compartido, créditos y cuentas en orden de Id, T34/R08): <see cref="ConstructorDeDocumentosDeCartera"/> valida y arma; el código por
/// <see cref="CodigoDeVerificacion"/>; el número con el prefijo de la clase por <see cref="NumeradorDeCartera"/> al final (el título, con el sufijo de su
/// versión, R11); el PDF por <see cref="IDocumentoDeCarteraRenderer"/>, subido <b>antes</b> de guardar como adjunto de módulo no borrable; el documento
/// con la copia de la identificación del titular y su SHA-256; sus créditos y sus cuentas; el evento <c>Issued</c> y, con correo, el mensaje. Guarda, no
/// abre transacción (la abre quien llama). (nuevo)
/// </summary>
public sealed class EmisionDeDocumentosDeCartera(
    IApplicationDbContext db,
    ConstructorDeDocumentosDeCartera constructor,
    EntregaDeDocumentosDeCartera entrega,
    IGeneradorDeCodigosDeVerificacion codigos,
    NumeradorDeCartera numerador,
    ICerrojoDeCartera cerrojo,
    IDocumentoDeCarteraRenderer renderizador,
    ISender sender,
    IBlobStore store,
    IDateTimeService reloj,
    IOptions<IdentityEmailOptions>? aplicacion = null,
    IAmbienteDeDespliegue? ambiente = null,
    IAttachmentCipher? cifrado = null)
{
    /// <summary>Cuántas veces se genera otro código ante una colisión antes de rendirse (80 bits: no debería pasar nunca).</summary>
    public const int IntentosDeCodigo = 5;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>¿La clase cubre todas las cuentas de captación y aportes de la persona? (el certificado tributario y el de retención).</summary>
    public static bool CubreCuentasDeLaPersona(IssuedDocumentKind clase) =>
        clase is IssuedDocumentKind.DepositTaxCertificate or IssuedDocumentKind.WithholdingCertificate;

    public async Task<Result<(IssuedLendingDocument Documento, Guid? Mensaje)>> EmitirAsync(SolicitudDeEmision s, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(s);
        var titular = s.Titular;
        // El cerrojo (T34, R08): setup compartido, los créditos que el documento puede cubrir y las cuentas, en orden de Id; la numeración, al final.
        await cerrojo.TomarSetupAsync(exclusivo: false, ct);
        IReadOnlyCollection<int> creditos = s.Credito is not null
            ? [s.Credito.Id]
            : s.Clase is IssuedDocumentKind.GeneralClearance or IssuedDocumentKind.TaxCertificate ? await CreditosDeLaPersonaAsync(titular.Id, ct) : [];
        if (creditos.Count > 0) await cerrojo.BloquearCreditosAsync(creditos, ModoDeEspera.Esperar, ct);
        var cuentas = await CuentasABloquearAsync(s, ct);
        if (cuentas.Count > 0) await cerrojo.BloquearCuentasAsync(cuentas, ModoDeEspera.Esperar, ct);

        // R = el instante de emisión, nunca anterior al último registro de lo que cubre (el sello del registro puede ir unos ticks por delante del
        // reloj): así el documento ve todo lo registrado antes que él y el reverso de cualquiera de esas operaciones lo anula.
        var ultimoCredito = creditos.Count == 0
            ? null
            : await db.LoanControls.AsNoTracking().Where(c => creditos.Contains(c.LoanId)).MaxAsync(c => (DateTime?)c.LastRegisteredAt, ct);
        var ultimaCuenta = cuentas.Count == 0
            ? null
            : await db.MemberAccountControls.AsNoTracking().Where(c => cuentas.Contains(c.MemberAccountId)).MaxAsync(c => (DateTime?)c.LastRegisteredAt, ct);
        var ahora = reloj.UtcNow;
        var emision = new[] { ultimoCredito, ultimaCuenta }.Where(x => x is not null).Select(x => DateTime.SpecifyKind(x!.Value, DateTimeKind.Utc))
            .Append(ahora).Max();

        var armado = await constructor.ArmarAsync(new PedidoDeDocumentoDeCartera(s.Clase, titular, s.Credito, s.Corte, emision, s.Hoy, s.Periodo, s.Año)
        {
            IncluyeAhorroPermanente = s.IncluyeAhorroPermanente,
            RetiroPublicId = s.RetiroPublicId,
            Cuenta = s.Cuenta,
            IncluyeRestricciones = s.IncluyeRestricciones,
            VersionDeCondicionesId = s.VersionDeCondicionesId,
            Respuesta = s.Respuesta,
        }, ct);
        if (armado.IsFailure) return Result.Failure<(IssuedLendingDocument, Guid?)>(armado.Error);
        var a = armado.Value;

        var codigo = await CodigoNuevoAsync(ct);
        if (codigo is null) throw new InvalidOperationException("No se pudo generar un código de verificación que no exista.");
        var numero = a.SufijoDeVersion is { } version
            ? $"{a.BaseDelNumero ?? await numerador.NumeroDeDocumentoAsync(s.Clase, ct)}-{version}"
            : await numerador.NumeroDeDocumentoAsync(s.Clase, ct);

        var publicId = Guid.NewGuid();
        var pruebas = ambiente?.EsProduccion != true;
        var encabezado = await EncabezadoAsync(s.Clase, numero, emision, titular, s.Firmante, codigo, pruebas, a.Leyendas, ct);
        var pdf = renderizador.Render(new DocumentoDeCarteraParaImprimir(encabezado, a.Cuerpo));
        var sha256 = Convert.ToHexString(SHA256.HashData(pdf)).ToLowerInvariant();

        // El adjunto de módulo, antes de la fila del documento: si el almacén falla no queda un documento que nadie puede bajar.
        var subida = await sender.Send(new UploadAttachmentCommand(AdjuntosDeModulo.DocumentoEmitidoDeCartera, publicId,
            ModelosDeDocumentoDeCartera.NombreDelArchivo(numero), "application/pdf", pdf), ct);
        if (subida.IsFailure) return Result.Failure<(IssuedLendingDocument, Guid?)>(subida.Error);

        var documento = new IssuedLendingDocument
        {
            PublicId = publicId,
            Kind = s.Clase,
            Number = numero,
            PersonId = titular.Id,
            HolderTaxIdType = Recortar(titular.IdType, IssuedLendingDocument.LargoDelTipoDeDocumento),
            HolderTaxId = Recortar(titular.TaxId, IssuedLendingDocument.LargoDelDocumento),
            HolderName = Recortar(ReferenciasDeGobierno.Nombre(titular), IssuedLendingDocument.LargoDelNombre),
            AsOfDate = a.AsOfDate,
            PeriodFrom = s.Periodo?.From,
            PeriodTo = s.Periodo?.To,
            TaxYear = s.Año,
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
            SignerName = Recortar(s.Firmante.Nombre, IssuedLendingDocument.LargoDelFirmante),
            SignerTitle = Recortar(s.Firmante.Cargo, IssuedLendingDocument.LargoDelFirmante),
            IsTestEnvironment = pruebas,
            IssuedByUserId = s.Usuario,
            IssuedAt = emision,
            DeliveryMode = s.Entrega,
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
        foreach (var cuenta in a.Cuentas)
            db.IssuedDocumentAccounts.Add(new IssuedDocumentAccount
            {
                IssuedDocumentId = documento.Id,
                MemberAccountId = cuenta.MemberAccountId,
                LastOperationId = cuenta.LastOperationId,
                LastTransactionId = cuenta.LastTransactionId,
                BalanceAtCutoff = cuenta.BalanceAtCutoff,
                TermsVersionId = cuenta.TermsVersionId,
                PreviousIssuedDocumentId = cuenta.PreviousIssuedDocumentId,
            });
        var (actor, usuario) = s.Usuario is { } u ? (ActorKind.Person, (int?)u) : (ActorKind.Process, (int?)null);
        db.IssuedDocumentEvents.Add(IssuedDocumentEvent.Emitido(documento, emision, actor, usuario));
        // Feature 014 (T103): la liquidación emitida queda en el retiro (la de su versión vigente).
        if (s.RetiroPublicId is { } retiroPublicId
            && await db.MembershipWithdrawals.FirstOrDefaultAsync(w => w.PublicId == retiroPublicId, ct) is { } retiro)
            retiro.SettlementIssuedDocumentId = documento.Id;

        Guid? mensaje = null;
        if (s.Entrega == DocumentDeliveryMode.Email)
        {
            if (s.Usuario is not { } quien) throw new InvalidOperationException("Sólo una persona envía un documento por correo al emitirlo.");
            var encolado = await entrega.EncolarAsync(documento, titular, s.Credito?.Id, quien, ct);
            if (encolado.IsFailure) return Result.Failure<(IssuedLendingDocument, Guid?)>(encolado.Error);
            mensaje = encolado.Value.Mensaje.PublicId;
        }
        await db.SaveChangesAsync(ct);
        return Result.Success<(IssuedLendingDocument, Guid?)>((documento, mensaje));
    }

    /// <summary>Las cuentas que el documento puede cubrir, en orden de Id (después de los créditos, R08).</summary>
    private async Task<IReadOnlyCollection<int>> CuentasABloquearAsync(SolicitudDeEmision s, CancellationToken ct)
    {
        if (s.Cuenta is not null) return [s.Cuenta.Id];
        if (IssueLendingDocumentCommand.EsDeAportes(s.Clase))
            return await db.MemberAccounts.AsNoTracking().Where(a => a.HolderPersonId == s.Titular.Id && a.Nature == LineNature.Contribution)
                .Select(a => a.Id).OrderBy(id => id).ToListAsync(ct);
        if (!CubreCuentasDeLaPersona(s.Clase)) return [];
        var comoCotitular = db.MemberAccountParties.AsNoTracking()
            .Where(x => x.PersonId == s.Titular.Id && x.Role == MemberAccountPartyRole.CoHolder && !x.IsDeleted).Select(x => x.MemberAccountId);
        return await db.MemberAccounts.AsNoTracking().Where(a => a.HolderPersonId == s.Titular.Id || comoCotitular.Contains(a.Id))
            .Select(a => a.Id).OrderBy(id => id).ToListAsync(ct);
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
}
