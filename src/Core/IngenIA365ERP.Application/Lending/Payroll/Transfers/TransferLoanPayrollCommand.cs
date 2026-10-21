using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Accrual;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Application.Lending.Settlement;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Lending.Payroll.Transfers;

/// <summary>Lo que dejó el traslado: el vínculo anterior y el nuevo, la forma de pago y el convenio que rigen, y si cambió el sub-segmento. (nuevo)</summary>
public sealed record PayrollTransferDto(
    Guid LoanPublicId,
    string LoanNumber,
    Guid PreviousLinkPublicId,
    Guid NewLinkPublicId,
    string RepaymentChannel,
    Guid? AgreementPublicId,
    string? AgreementCode,
    DateOnly ValidFrom,
    DateOnly? LibranzaDate,
    bool SubsegmentChanged,
    string? Subsegment,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Traslada una libranza (feature 013, K2, T0843; api.md §13.3, <c>POST /api/lending/loans/{id}/payroll-transfer</c>,
/// <c>Lending.PayrollTransfers.Manage</c>, con clave; FR-116, US10-5, C44). No es una operación del libro ni una <c>LoanNovelty</c>: cierra el vínculo
/// vigente y abre otro, bajo el cerrojo del crédito:
/// <list type="bullet">
/// <item><b>a otra pagaduría con convenio</b>: el convenio vivo, activo y con versión vigente; la certificación de la pagaduría nueva (adjunto, fecha
/// e ingreso neto); la capacidad de descuento contra ella (<c>Lending.Payroll.DeductionCapacityExceeded</c> con <c>available</c> y
/// <c>requested</c>); el vínculo nuevo es <c>Origin = Transfer</c> con la <b>misma fecha de libranza</b> (la prelación no se pierde, FR-111);</item>
/// <item><b>sin pagaduría nueva</b> (retiro sin nuevo empleador): la forma de pago alterna del convenio (caja por defecto), vínculo
/// <c>ChannelChange</c> y aviso al deudor (informativo <c>PASOACAJA</c>);</item>
/// <item>si pasa entre con y sin libranza y las condiciones lo distinguen, <c>DimensionContableCambiada</c> con el sub-segmento nuevo en la misma
/// transacción, con la validación previa contable de pantalla (A8).</item>
/// </list>
/// Un crédito sin pagaduría: <c>Lending.Loan.NotPayroll</c>. El vínculo nuevo rige desde hoy (o desde mañana si el vigente nació hoy). (nuevo)
/// </summary>
public sealed record TransferLoanPayrollCommand(
    Guid LoanPublicId,
    string Reason,
    Guid? NewAgreementPublicId = null,
    string? EmployeeId = null,
    Guid? CertificationAttachmentPublicId = null,
    DateOnly? CertificationDate = null,
    decimal? CertifiedNetIncome = null) : IRequest<Result<PayrollTransferDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class TransferLoanPayrollCommandValidator : AbstractValidator<TransferLoanPayrollCommand>
{
    public TransferLoanPayrollCommandValidator()
    {
        RuleFor(x => x.LoanPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Diga por qué se traslada la libranza.").MaximumLength(300);
        RuleFor(x => x.EmployeeId).MaximumLength(30);
        RuleFor(x => x.CertifiedNetIncome).GreaterThan(0m).When(x => x.CertifiedNetIncome is not null)
            .WithMessage("El ingreso neto certificado es mayor que cero.");
    }
}

public sealed class TransferLoanPayrollCommandHandler(
    IApplicationDbContext db,
    ICerrojoDeCartera cerrojo,
    CargadorDeReconstruccion cargador,
    PartesDeHechosDeCartera partes,
    FechaContable fechaContable,
    EmisionDeCartera emision,
    ValidacionPreviaContableDeCartera validacionPrevia,
    ConveniosDeLibranza convenios,
    ColaDeMensajesAlDeudor cola,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj,
    ILogger<TransferLoanPayrollCommandHandler>? logger = null) : IRequestHandler<TransferLoanPayrollCommand, Result<PayrollTransferDto>>
{
    /// <summary>La plantilla del aviso del paso a la forma alterna.</summary>
    public const string PlantillaDePasoACaja = "PASOACAJA";

    /// <summary>El origen del aviso (<c>LND_DebtorMessages.OriginKind</c>).</summary>
    public const string OrigenDelAviso = "PaymentFormChange";

    private readonly ILogger _log = logger ?? (ILogger)NullLogger.Instance;

    public async Task<Result<PayrollTransferDto>> Handle(TransferLoanPayrollCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var credito = await db.Loans.AsNoTracking().FirstOrDefaultAsync(l => l.PublicId == request.LoanPublicId && !l.IsDeleted, ct);
        if (credito is null) return Falla(LecturaDelCredito.Inexistente());
        var dentro = await alcance.AsegurarAsync(credito.BranchId, credito.CreditLineId, LecturaDelCredito.Inexistente(), auditoria, "Loan", credito.PublicId, ct);
        if (dentro.IsFailure) return Falla(dentro.Error);
        if (credito.Status is not (LoanStatus.Active or LoanStatus.WrittenOff)) return Falla(ErroresDePagos.NoPagable(credito.Number, credito.Status.ToString()));
        var hoy = reloj.HoyLocal;

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([credito.DebtorPersonId], ModoDeEspera.Esperar, ct);
            await cerrojo.BloquearCreditosAsync([credito.Id], ModoDeEspera.Esperar, ct);

            var vigente = await db.LoanPayrollLinks.Where(v => v.LoanId == credito.Id && !v.IsDeleted && v.ValidTo == null)
                .OrderByDescending(v => v.ValidFrom).FirstOrDefaultAsync(ct);
            if (vigente is null || !vigente.ConLibranza) return Falla(ErroresDeLibranza.NoEsLibranza(credito.Number));
            var desde = vigente.ValidFrom < hoy ? hoy : vigente.ValidFrom.AddDays(1);

            LoanPayrollLink nuevo;
            PayrollAgreement? convenioNuevo = null;
            if (request.NewAgreementPublicId is { } publico)
            {
                var trasladado = await TrasladarAsync(credito, vigente, publico, request, desde, ct);
                if (trasladado.IsFailure) return Falla(trasladado.Error);
                (nuevo, convenioNuevo) = trasladado.Value;
            }
            else
            {
                var alterna = await FormaAlternaAsync(vigente, desde, ct);
                nuevo = vigente.PasarALaFormaAlterna(alterna, desde, request.Reason);
            }
            // E249: un crédito con débito automático vigente no pasa a libranza o nómina (la forma alterna no lo es).
            if (nuevo.ConLibranza && await ExclusionDeLibranzaYDebitoAutomatico.CuentaConDebitoAutomaticoAsync(db, credito.Id, hoy, ct) is { } conDebito)
                return Falla(ExclusionDeLibranzaYDebitoAutomatico.CreditoConDebitoAutomatico(credito.Number, conDebito));
            db.LoanPayrollLinks.Add(nuevo);

            // El sub-segmento contable que rige antes y después (sólo cambia si las condiciones distinguen con y sin libranza).
            var cargado = await cargador.CargarAsync(credito.Id, null, desde, ct);
            if (cargado is null || !cargado.Listo) return Falla(cargado?.Error ?? LecturaDelCredito.Inexistente());
            var deLasCondiciones = cargado.Vigentes.SubsegmentCode;
            var antes = EmisionDeCartera.SubsegmentoVigente(deLasCondiciones, vigente) ?? deLasCondiciones;
            var despues = EmisionDeCartera.SubsegmentoVigente(deLasCondiciones, nuevo) ?? deLasCondiciones;
            var avisos = new List<string>();
            if (!string.Equals(antes, despues, StringComparison.Ordinal) && antes is not null && despues is not null)
            {
                var emitida = await DimensionAsync(credito.Id, cargado, nuevo, antes, despues, desde, avisos, ct);
                if (emitida is not null) return Falla(emitida);
            }
            await db.SaveChangesAsync(ct);

            if (convenioNuevo is null) await AvisarAsync(credito, nuevo, usuario, ct);
            await db.SaveChangesAsync(ct);
            return Result.Success(new PayrollTransferDto(credito.PublicId, credito.Number, vigente.PublicId, nuevo.PublicId, nuevo.RepaymentChannel.ToString(),
                convenioNuevo?.PublicId, convenioNuevo?.Code, nuevo.ValidFrom, nuevo.LibranzaDate, !string.Equals(antes, despues, StringComparison.Ordinal),
                despues, avisos));
        }, ct);
    }

    /// <summary>A otra pagaduría con convenio: convenio, versión, certificación y capacidad; el vínculo nuevo con la misma fecha de libranza.</summary>
    private async Task<Result<(LoanPayrollLink, PayrollAgreement)>> TrasladarAsync(Domain.Entities.Lending.Loans.Loan credito, LoanPayrollLink vigente, Guid publico,
        TransferLoanPayrollCommand request, DateOnly desde, CancellationToken ct)
    {
        var convenio = await db.PayrollAgreements.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == publico && !a.IsDeleted, ct);
        if (convenio is null) return Falla2(ErroresDeLibranza.ConvenioNoDisponible(null, "el convenio no existe"));
        if (convenio.IsOwnPayroll || !convenio.Operable)
            return Falla2(ErroresDeLibranza.ConvenioNoDisponible(convenio.Code, convenio.IsOwnPayroll ? "es la nómina propia" : "no está activo"));
        if (vigente.PayrollAgreementId == convenio.Id)
            return Falla2(ErroresDeLibranza.ConvenioNoDisponible(convenio.Code, "ya es el convenio vigente del crédito"));
        var version = await convenios.VersionVigenteAsync(convenio.Id, desde, ct);
        if (version is null) return Falla2(ErroresDeLibranza.SinVersionVigente(convenio.Code, desde));
        if (request.CertificationAttachmentPublicId is null || request.CertificationDate is null || request.CertifiedNetIncome is not { } ingreso)
            return Falla2(ErroresDeLibranza.CertificacionRequerida());

        var topes = await convenios.TopesAsync(version.DeductionCapFraction, desde, ct);
        if (topes.IsFailure) return Falla2(topes.Error);
        var vigentes = await convenios.DescuentosVigentesAsync(credito.DebtorPersonId, convenio.Id, null, desde, credito.Id, ct);
        var cuota = await convenios.ProximasCuotasAsync([credito.Id], desde, ct);
        var capacidad = CapacidadDeDescuento.Evaluar(ingreso, topes.Value, vigentes, cuota);
        if (!capacidad.Alcanza) return Falla2(ErroresDeLibranza.CapacidadExcedida(capacidad.Disponible, cuota));

        var nuevo = vigente.Trasladar(convenio.PaymasterPersonId, convenio.Id, request.EmployeeId, desde, request.Reason, request.CertificationAttachmentPublicId,
            request.CertificationDate, ingreso, capacidad.Disponible);
        return Result.Success((nuevo, convenio));
    }

    /// <summary>La forma de pago alterna del convenio vigente del vínculo (caja si no tiene convenio o versión).</summary>
    private async Task<RepaymentChannel> FormaAlternaAsync(LoanPayrollLink vigente, DateOnly desde, CancellationToken ct)
    {
        var convenioId = vigente.PayrollAgreementId ?? (vigente.IsOwnPayroll ? (await convenios.NominaPropiaAsync(ct))?.Id : null);
        if (convenioId is null) return RepaymentChannel.Cashier;
        return (await convenios.VersionVigenteAsync(convenioId.Value, desde, ct))?.AlternateRepaymentChannel ?? RepaymentChannel.Cashier;
    }

    /// <summary>
    /// <c>DimensionContableCambiada</c> con los saldos vivos y la validación previa de pantalla (A8): con <c>Bloquear</c> y la combinación nueva sin
    /// regla, el traslado no se confirma. Devuelve el error, o nulo si emitió (o si no había saldo que mover).
    /// </summary>
    private async Task<Error?> DimensionAsync(int loanId, CreditoCargado cargado, LoanPayrollLink nuevo, string antes, string despues, DateOnly desde,
        List<string> avisos, CancellationToken ct)
    {
        var estado = MotorDeCartera.Reconstruir(cargado.Entrada!, desde);
        var saldos = estado.SaldosPorConcepto.Where(s => s.Value > 0m && s.Key is not (LoanConcept.Advance or LoanConcept.CreditBalance))
            .Select(s => new ConceptAmountV1 { Concept = s.Key.ToString(), Amount = s.Value }).ToList();
        if (saldos.Count == 0) return null;
        var contable = await fechaContable.CalcularAsync(desde, ct);
        if (contable.IsFailure) return contable.Error;
        var hecho = (await partes.DatosAsync([cargado], ct))[loanId].Hecho(contable.Value);
        var contenido = await emision.DimensionPorCambioDeLibranzaAsync(hecho, antes, despues, saldos, desde, reloj.UtcNow, ct);
        var decision = await validacionPrevia.DecidirAsync([new MensajeContableDto(new IntegrationEnvelopeV1
        {
            MessageId = Guid.Empty, Type = DimensionContableCambiadaV1.Type, Kind = IntegrationMessageKind.Business, OriginModule = ModulosDeOrigen.Cartera,
            OriginEventKey = ClavesDeEvento.ConfirmacionPor(hecho.Credito.PublicId), BranchPublicId = hecho.SucursalPublicId,
        }, contenido)], OrigenDeValidacionPrevia.Pantalla, "el traslado de la libranza", ct);
        if (decision.Bloqueo is { } bloqueo) return bloqueo;
        avisos.AddRange(decision.Avisos);
        var emitida = await emision.EmitirDimensionPorCambioDeLibranzaAsync(hecho, nuevo, contenido, decision.Outcome, ct);
        return emitida.IsFailure ? emitida.Error : null;
    }

    /// <summary>El aviso al deudor del paso a la forma alterna (informativo <c>PASOACAJA</c>); un aviso nunca tumba el traslado.</summary>
    private async Task AvisarAsync(Domain.Entities.Lending.Loans.Loan credito, LoanPayrollLink nuevo, int usuario, CancellationToken ct)
    {
        try
        {
            var r = await cola.EncolarAsync(new PedidoDeMensajeAlDeudor(credito.DebtorPersonId, credito.Id, DebtorMessagePurpose.Informative, MessageChannel.Email,
                PlantillaDePasoACaja, OrigenDelAviso, nuevo.PublicId.ToString("N"), ActorKind.Person, usuario)
            {
                OriginPublicId = nuevo.PublicId,
                Valores = new Dictionary<string, string?>
                {
                    ["FormaDePagoNueva"] = nuevo.RepaymentChannel == RepaymentChannel.DirectDebit ? "débito automático" : "caja o los canales de pago de la cooperativa",
                },
            }, ct);
            if (r.IsFailure)
                _log.LogInformation("[Cartera.AvisoDePasoACaja] No se encoló el aviso del crédito {Credito}: {Codigo} {Mensaje}", credito.Number, r.Error.Code,
                    r.Error.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "[Cartera.AvisoDePasoACaja] El aviso del crédito {Credito} falló.", credito.Number);
        }
    }

    private static Result<PayrollTransferDto> Falla(Error e) => Result.Failure<PayrollTransferDto>(e);

    private static Result<(LoanPayrollLink, PayrollAgreement)> Falla2(Error e) => Result.Failure<(LoanPayrollLink, PayrollAgreement)>(e);
}
