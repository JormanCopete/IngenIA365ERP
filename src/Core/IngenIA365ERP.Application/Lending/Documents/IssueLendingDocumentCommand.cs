using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
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
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Documents;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Documents;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Lending.Documents;

/// <summary>El período de un extracto (K5). (nuevo)</summary>
public sealed record PeriodoDeDocumento(DateOnly From, DateOnly To);

/// <summary>
/// Lo que responde la emisión (api.md §15, 201): el documento, su número, el código en cuatro grupos, el estado calculado, el fin de la firmeza, el
/// enlace de 60 s del PDF si se pidió descarga y el mensaje al deudor si se pidió correo. Feature 014, US13 (T196; documentos.md §2.1, US13-1):
/// <see cref="AlreadyIssued"/> dice que se devolvió la copia ya emitida del mismo extracto (no consumió número). (nuevo)
/// </summary>
public sealed record IssuedLendingDocumentDto(
    Guid DocumentPublicId,
    string Number,
    string VerificationCode,
    string Status,
    DateOnly? FinalityUntil,
    EnlaceDeDescargaDto? Download,
    Guid? DebtorMessagePublicId,
    bool AlreadyIssued = false);

/// <summary>
/// <c>POST /api/lending/documents</c> (feature 013, US13, T0784; api.md §15, adaptadores-y-archivos.md §8.1–§8.7, <c>Lending.Documents.Issue</c>, con
/// clave; T80, FR-013, FR-131): emite un documento <b>en un paso</b> —un diálogo con una confirmación—:
/// <list type="number">
/// <item>Cartera activada (si no, <c>Lending.NotActivated</c>); antes de su entrega cada clase responde <c>Lending.Document.KindNotAvailable</c> con
/// <c>data.availableFrom</c>; el extracto exige un período de hasta cinco años que no termine después de hoy (<c>Lending.Document.PeriodInvalid</c>) y el
/// certificado tributario un año terminado (<c>Lending.Document.TaxYearNotClosed</c>; sin año, el anterior); sin <c>Documentos.Firmante.Nombre</c>/<c>.Cargo</c>,
/// <c>Lending.Document.SignerMissing</c>;</item>
/// <item>el titular existe; el crédito, si la clase lo lleva, está en el alcance de oficina y línea (fuera, el 404 genérico) y el titular es su
/// deudor o un codeudor vigente; con <c>Email</c>, el correo y la autorización de tratamiento general antes de pintar nada
/// (<see cref="EntregaDeDocumentosDeCartera.PrevalidarAsync"/>);</item>
/// <item>en una transacción, <see cref="EmisionDeDocumentosDeCartera"/> (bajo el cerrojo, el armado, el código, el número al final, el PDF subido antes
/// de guardar, el documento, lo que cubre, el evento y el mensaje);</item>
/// <item>la emisión se audita (<c>Lending.Document.Issued</c>) además del comando: muestra datos del titular y cuenta como lectura sensible.</item>
/// </list>
/// Feature 014, US13 (T196; contracts/documentos.md §2.1 a §2.4, §3): las clases de captaciones. El extracto y el certificado de saldo llevan
/// <see cref="AccountPublicId"/> —la cuenta en el alcance (fuera, el 404 de la cuenta), su naturaleza activada y la persona su titular o cotitular
/// (<c>Lending.Document.NotAccountHolder</c>)—; el certificado de saldo, una fecha que no sea futura (<c>Lending.Document.FutureDateNotAllowed</c>) y, si
/// se pide, los bloqueos y embargos (<see cref="IncludeRestrictions"/>); el certificado tributario de captaciones, un año terminado; el de retención, un
/// período. Las dos clases de la persona exigen el alcance sobre <b>todas</b> sus cuentas (falla cerrado, como el resumen de aportes). Pedir el mismo
/// extracto sin operaciones registradas después de su corte de información devuelve la misma copia (US13-1). El título del CDAT y la respuesta al juzgado
/// nacen con su operación: por aquí responden <c>Lending.Document.IssuedAutomatically</c>.
/// La misma <c>Idempotency-Key</c> devuelve el mismo documento (<c>IdempotencyBehavior</c>). (nuevo)
/// </summary>
public sealed record IssueLendingDocumentCommand(
    IssuedDocumentKind Kind,
    Guid PersonPublicId,
    Guid? LoanPublicId,
    DateOnly? CutoffDate,
    PeriodoDeDocumento? Period,
    short? Year,
    DocumentDeliveryMode Delivery,
    bool IncludePermanentSavings = false) : IRequest<Result<IssuedLendingDocumentDto>>, IOperacionIdempotente
{
    /// <summary>Feature 014 (T087): las clases de aportes, que exigen aportes activados y cubren la cuenta de aportes de la persona.</summary>
    public static bool EsDeAportes(IssuedDocumentKind clase) =>
        clase is IssuedDocumentKind.ContributionCertificate or IssuedDocumentKind.ContributionClearance
            or IssuedDocumentKind.MembershipWithdrawalSettlement;

    /// <summary>Feature 014, US13 (T196): las clases que se emiten sobre una cuenta de captación o de aportes.</summary>
    public static bool LlevaCuenta(IssuedDocumentKind clase) =>
        clase is IssuedDocumentKind.AccountStatement or IssuedDocumentKind.AccountBalanceCertificate;

    /// <summary>Feature 014, US13 (T196): las clases que nacen solas con su operación (por la ruta sólo se descargan).</summary>
    public static bool NaceSola(IssuedDocumentKind clase) =>
        clase is IssuedDocumentKind.TermDepositTitle or IssuedDocumentKind.GarnishmentResponse or IssuedDocumentKind.MembershipWithdrawalSettlement;

    /// <summary>¿La clase lleva período? (el extracto de un crédito o de una cuenta y el certificado de retención).</summary>
    public static bool LlevaPeriodo(IssuedDocumentKind clase) =>
        clase is IssuedDocumentKind.Statement or IssuedDocumentKind.AccountStatement or IssuedDocumentKind.WithholdingCertificate;

    /// <summary>¿La clase lleva año? (los dos certificados tributarios).</summary>
    public static bool LlevaAño(IssuedDocumentKind clase) => clase is IssuedDocumentKind.TaxCertificate or IssuedDocumentKind.DepositTaxCertificate;

    public Guid OperationKey { get; init; }

    /// <summary>Feature 014, US13 (T196; documentos.md §3): la cuenta del extracto o del certificado de saldo.</summary>
    public Guid? AccountPublicId { get; init; }

    /// <summary>Feature 014, US13 (T196; documentos.md §2.2): el certificado de saldo incluye los bloqueos y embargos vigentes.</summary>
    public bool IncludeRestrictions { get; init; }

    /// <summary>
    /// Feature 014 (T103; documentos.md §2.8, §3): el retiro cuya liquidación se emite. Sólo lo pone la liquidación del retiro (la ruta no lo lee): sin él,
    /// la clase <c>MembershipWithdrawalSettlement</c> responde <c>Lending.Document.IssuedAutomatically</c>.
    /// </summary>
    public Guid? MembershipWithdrawalPublicId { get; init; }

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
        RuleFor(x => x.AccountPublicId).NotEmpty().When(x => IssueLendingDocumentCommand.LlevaCuenta(x.Kind))
            .WithMessage("Elija la cuenta del documento.");
        RuleFor(x => x.CutoffDate).Null()
            .When(x => x.Kind is not (IssuedDocumentKind.BalanceCertificate or IssuedDocumentKind.ContributionCertificate
                or IssuedDocumentKind.AccountBalanceCertificate))
            .WithMessage("Sólo los certificados de saldo y la constancia de aportes llevan fecha de corte.");
        RuleFor(x => x.IncludePermanentSavings).Equal(false).When(x => x.Kind != IssuedDocumentKind.ContributionCertificate)
            .WithMessage("Sólo la constancia de aportes incluye el ahorro permanente.");
        RuleFor(x => x.IncludeRestrictions).Equal(false).When(x => x.Kind != IssuedDocumentKind.AccountBalanceCertificate)
            .WithMessage("Sólo el certificado de saldo de una cuenta incluye los bloqueos y embargos.");
        RuleFor(x => x.Period).Must(p => p is null || p.From <= p.To).WithMessage("La fecha inicial del período no puede ser posterior a la final.");
        RuleFor(x => x.Period).Null().When(x => !IssueLendingDocumentCommand.LlevaPeriodo(x.Kind))
            .WithMessage("Sólo el extracto y el certificado de retención llevan período.");
        RuleFor(x => x.Year).Null().When(x => !IssueLendingDocumentCommand.LlevaAño(x.Kind)).WithMessage("Sólo el certificado tributario lleva año.");
        RuleFor(x => x.Year).InclusiveBetween((short)2000, (short)2100).When(x => x.Year is not null).WithMessage("El año del certificado no es válido.");
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
    IAttachmentCipher? cifrado = null,
    EntregaDeCartera? entregaVigente = null) : IRequestHandler<IssueLendingDocumentCommand, Result<IssuedLendingDocumentDto>>
{
    /// <summary>Cuántas veces se genera otro código ante una colisión antes de rendirse (80 bits: no debería pasar nunca).</summary>
    public const int IntentosDeCodigo = EmisionDeDocumentosDeCartera.IntentosDeCodigo;

    private EmisionDeDocumentosDeCartera Emision() =>
        new(db, constructor, entrega, codigos, numerador, cerrojo, renderizador, sender, store, reloj, aplicacion, ambiente, cifrado);

    public async Task<Result<IssuedLendingDocumentDto>> Handle(IssueLendingDocumentCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        if (!await db.LendingSetups.AsNoTracking().AnyAsync(s => s.Id == LendingSetup.IdDeLaFilaUnica && s.ActivationDate != null, ct))
            return Falla(ErroresDeMensajesAlDeudor.CarteraSinActivar());
        if (!PrefijosDeDocumentosDeCartera.Disponible(request.Kind, entregaVigente ?? CatalogoDeCartera.EntregaVigente))
            return Falla(DocumentErrors.ClaseNoDisponible(request.Kind));
        // El título del CDAT, la respuesta al juzgado y la liquidación del retiro nacen con su operación (documentos.md §3).
        if (request.Kind is IssuedDocumentKind.TermDepositTitle or IssuedDocumentKind.GarnishmentResponse
            || (request.Kind == IssuedDocumentKind.MembershipWithdrawalSettlement && request.MembershipWithdrawalPublicId is null))
            return Falla(DocumentErrors.EmitidoAutomaticamente(request.Kind));
        var activaciones = await Deposits.Activation.ActivacionesDeCaptacion.LeerAsync(db, ct);
        if (IssueLendingDocumentCommand.EsDeAportes(request.Kind) && activaciones.Aportes is null)
            return Falla(Deposits.Accounts.ErroresDeCuentas.SinActivar(LineNature.Contribution));
        if (EmisionDeDocumentosDeCartera.CubreCuentasDeLaPersona(request.Kind) && activaciones.Captaciones is null)
            return Falla(Deposits.Accounts.ErroresDeCuentas.SinActivar(LineNature.Deposit));

        var hoy = reloj.HoyLocal;
        short? año = null;
        if (IssueLendingDocumentCommand.LlevaPeriodo(request.Kind) && PeriodoInvalido(request.Period, hoy) is { } malPeriodo) return Falla(malPeriodo);
        if (IssueLendingDocumentCommand.LlevaAño(request.Kind))
        {
            año = request.Year ?? (short)(hoy.Year - 1);
            if (año >= hoy.Year) return Falla(DocumentErrors.AñoNoTerminado(año.Value));
        }
        if (request.Kind == IssuedDocumentKind.AccountBalanceCertificate && request.CutoffDate is { } corte && corte > hoy)
            return Falla(DocumentErrors.FechaFutura(corte));
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

        MemberAccount? cuenta = null;
        if (IssueLendingDocumentCommand.LlevaCuenta(request.Kind))
        {
            var leida = await Deposits.Accounts.LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, request.AccountPublicId!.Value, seguir: false, ct);
            if (leida.IsFailure) return Falla(leida.Error);
            cuenta = leida.Value;
            if (!activaciones.Activada(cuenta.Nature)) return Falla(Deposits.Accounts.ErroresDeCuentas.SinActivar(cuenta.Nature));
            if (!await EsTitularAsync(titular.Id, cuenta, ct)) return Falla(DocumentErrors.NoEsTitular(ReferenciasDeGobierno.Nombre(titular), cuenta.Number));
            if (request.Kind == IssuedDocumentKind.AccountStatement && await MismaCopiaAsync(titular.Id, cuenta.Id, request.Period!, hoy, ct) is { } copia)
                return Result.Success(copia);
        }
        else if (EmisionDeDocumentosDeCartera.CubreCuentasDeLaPersona(request.Kind))
        {
            // Falla cerrado (E164): un certificado que suma una cuenta de otra oficina revela su saldo.
            var cuentas = await CuentasDeLaPersonaAsync(titular.Id, ct);
            foreach (var c in cuentas)
            {
                var dentro = await alcance.AsegurarAsync(c.BranchId, c.CreditLineId, ErroresDeGobierno.PersonaInexistente(), auditoria, nameof(MemberAccount),
                    c.PublicId, ct);
                if (dentro.IsFailure) return Falla(dentro.Error);
            }
        }

        // Primero el correo y la autorización (sin pintar ni subir nada): un envío que no puede salir no deja un PDF huérfano.
        if (request.Delivery == DocumentDeliveryMode.Email && await entrega.PrevalidarAsync(titular, credito?.Id, ct) is { } rechazo) return Falla(rechazo);

        var solicitud = new SolicitudDeEmision(request.Kind, titular, firmante.Value, usuario, request.Delivery, hoy)
        {
            Credito = credito, Cuenta = cuenta, Corte = request.CutoffDate, Periodo = request.Period, Año = año,
            IncluyeAhorroPermanente = request.IncludePermanentSavings, IncluyeRestricciones = request.IncludeRestrictions,
            RetiroPublicId = request.MembershipWithdrawalPublicId,
        };
        var emitido = await TransaccionExplicita.EjecutarAsync(db, () => Emision().EmitirAsync(solicitud, ct), ct);
        if (emitido.IsFailure) return Falla(emitido.Error);

        var (documento, mensaje) = emitido.Value;
        await auditoria.EmitirDocumentoEmitidoAsync(documento.PublicId, documento.Kind.ToString(), documento.Number, documento.DeliveryMode.ToString(),
            documento.ContentSha256, ct);
        var descarga = request.Delivery == DocumentDeliveryMode.Download ? await DescargaAsync(documento, ct) : null;
        return Result.Success(new IssuedLendingDocumentDto(documento.PublicId, documento.Number, CodigoDeVerificacion.Formatear(documento.VerificationCode),
            documento.EstadoA(false, hoy).ToString(), documento.FinalityUntil, descarga, mensaje));
    }

    private async Task<EnlaceDeDescargaDto?> DescargaAsync(IssuedLendingDocument documento, CancellationToken ct)
    {
        var adjunto = await db.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == documento.PdfAttachmentPublicId, ct);
        return adjunto is null ? null : await AdjuntosDirectos.FirmarDescargaAsync(store, adjunto, reloj, limites.Value, ct);
    }

    /// <summary>
    /// US13-1 (documentos.md §2.1): el mismo extracto de la misma cuenta, a nombre de la misma persona, vigente y sin operaciones de la cuenta registradas
    /// después de su corte de información con fecha valor hasta el fin del período; nulo si hay que emitir otro. La copia no consume número y se audita
    /// como reimpresión.
    /// </summary>
    private async Task<IssuedLendingDocumentDto?> MismaCopiaAsync(int persona, int cuentaId, PeriodoDeDocumento periodo, DateOnly hoy, CancellationToken ct)
    {
        var cubren = db.IssuedDocumentAccounts.AsNoTracking().Where(x => x.MemberAccountId == cuentaId).Select(x => x.IssuedDocumentId);
        var anulados = db.IssuedDocumentEvents.AsNoTracking().Where(e => e.Kind == IssuedDocumentEventKind.Voided).Select(e => e.IssuedDocumentId);
        var previo = await db.IssuedLendingDocuments.AsNoTracking()
            .Where(d => d.Kind == IssuedDocumentKind.AccountStatement && d.PersonId == persona && cubren.Contains(d.Id) && !anulados.Contains(d.Id)
                        && d.PeriodFrom == periodo.From && d.PeriodTo == periodo.To && !d.IsDeleted)
            .OrderByDescending(d => d.Id).FirstOrDefaultAsync(ct);
        if (previo is null) return null;
        var cambio = await db.MemberAccountTransactions.AsNoTracking()
            .AnyAsync(t => t.MemberAccountId == cuentaId && t.RegisteredAt > previo.InformationCutoff && t.ValueDate <= periodo.To, ct);
        if (cambio) return null;
        await auditoria.EmitirDocumentoReimpresoAsync(previo.PublicId, previo.Kind.ToString(), "pdf", ct);
        var descarga = await DescargaAsync(previo, ct);
        return new IssuedLendingDocumentDto(previo.PublicId, previo.Number, CodigoDeVerificacion.Formatear(previo.VerificationCode),
            previo.EstadoA(false, hoy).ToString(), previo.FinalityUntil, descarga, null, AlreadyIssued: true);
    }

    /// <summary>¿La persona es titular o cotitular de la cuenta? (el cotitular, en cualquier vigencia: el documento puede ser de un período pasado).</summary>
    private async Task<bool> EsTitularAsync(int persona, MemberAccount cuenta, CancellationToken ct) =>
        cuenta.HolderPersonId == persona
        || await db.MemberAccountParties.AsNoTracking().AnyAsync(p => p.MemberAccountId == cuenta.Id && p.PersonId == persona
            && p.Role == MemberAccountPartyRole.CoHolder && !p.IsDeleted, ct);

    private async Task<List<MemberAccount>> CuentasDeLaPersonaAsync(int persona, CancellationToken ct)
    {
        var comoCotitular = db.MemberAccountParties.AsNoTracking()
            .Where(x => x.PersonId == persona && x.Role == MemberAccountPartyRole.CoHolder && !x.IsDeleted).Select(x => x.MemberAccountId);
        return await db.MemberAccounts.AsNoTracking().Where(a => !a.IsDeleted && (a.HolderPersonId == persona || comoCotitular.Contains(a.Id)))
            .OrderBy(a => a.Id).ToListAsync(ct);
    }

    /// <summary>
    /// K5 (T0971): el período del extracto —obligatorio, la fecha inicial no posterior a la final, que no termine después de hoy y de hasta
    /// <see cref="DocumentErrors.AñosMaximosDelExtracto"/> años—; nulo si sirve. Feature 014: vale también para el extracto de cuenta y el certificado de
    /// retención.
    /// </summary>
    public static Error? PeriodoInvalido(PeriodoDeDocumento? periodo, DateOnly hoy)
    {
        if (periodo is null) return DocumentErrors.PeriodoInvalido("elija la fecha inicial y la final", null, null);
        if (periodo.From > periodo.To)
            return DocumentErrors.PeriodoInvalido("la fecha inicial es posterior a la final", periodo.From, periodo.To);
        if (periodo.To > hoy) return DocumentErrors.PeriodoInvalido("no puede terminar después de hoy", periodo.From, periodo.To);
        if (periodo.To >= periodo.From.AddYears(DocumentErrors.AñosMaximosDelExtracto))
            return DocumentErrors.PeriodoInvalido($"abarca más de {DocumentErrors.AñosMaximosDelExtracto} años", periodo.From, periodo.To);
        return null;
    }

    /// <summary>¿La persona es deudora o codeudora vigente del crédito?</summary>
    private async Task<bool> ObligadoAsync(int personId, Loan credito, DateOnly hoy, CancellationToken ct)
    {
        if (credito.DebtorPersonId == personId) return true;
        var partes = await db.LoanParties.AsNoTracking()
            .Where(p => p.LoanId == credito.Id && p.PersonId == personId && p.Role == CreditPartyRole.CoDebtor).ToListAsync(ct);
        return partes.Any(p => p.VigenteA(hoy));
    }

    private static Result<IssuedLendingDocumentDto> Falla(Error e) => Result.Failure<IssuedLendingDocumentDto>(e);
}
