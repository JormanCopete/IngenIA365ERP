using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Ledger;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Payoffs;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Payoffs;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Entities.Lending.Projections;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Payoffs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Disbursements;

/// <summary>
/// Desembolsa un tramo (feature 013, US3, T0444; FR-042–FR-048, D12; api.md §9.1, <c>POST /api/lending/disbursements</c>,
/// <c>Lending.Disbursements.Execute</c>, con clave). <paramref name="AcknowledgeWarnings"/> deja constancia de que quien desembolsa vio los
/// avisos (la validación previa con <c>Advertir</c>, sin respuesta): los avisos no impiden el desembolso. (nuevo)
/// </summary>
public sealed record ExecuteLoanDisbursementCommand(DisbursementInput Input, bool AcknowledgeWarnings = false)
    : IRequest<Result<DisbursementResultDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ExecuteLoanDisbursementCommandValidator : AbstractValidator<ExecuteLoanDisbursementCommand>
{
    public ExecuteLoanDisbursementCommandValidator()
    {
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.ApplicationPublicId).NotEmpty().When(x => x.Input is not null);
        RuleFor(x => x.Input.Form).IsInEnum().NotEqual(DisbursementForm.InKind)
            .WithMessage("El desembolso en especie sólo nace de una venta de Comercial.").When(x => x.Input is not null);
        RuleFor(x => x.Input.TrancheAmount).GreaterThan(0m).When(x => x.Input?.TrancheAmount is not null);
        RuleFor(x => x.Input.Beneficiary).NotNull()
            .WithMessage("Un giro a tercero necesita el beneficiario.").When(x => x.Input?.Form == DisbursementForm.ThirdPartyPayment);
        RuleForEach(x => x.Input.OwnLoanPayoffs).Must(p => p.Mode is RequisitosDelDesembolso.ModoTotal or RequisitosDelDesembolso.ModoParcial)
            .WithMessage("La recogida es «Full» (total) o «Partial» (parcial).").When(x => x.Input is not null);
        RuleForEach(x => x.Input.OwnLoanPayoffs).Must(p => p.Mode != RequisitosDelDesembolso.ModoParcial || p.Amount > 0m)
            .WithMessage("Una recogida parcial lleva el valor a aplicar.").When(x => x.Input is not null);
        // 2026-11-27 (feature 028, T032): la lista nueva es opcional (las recogidas salen de la solicitud); si viene, con su forma.
        RuleForEach(x => x.Input.Payoffs).ChildRules(p =>
        {
            p.RuleFor(i => i.Kind).IsInEnum();
            p.RuleFor(i => i.Mode).IsInEnum();
            p.RuleFor(i => i.Amount).NotNull().GreaterThan(0m).When(i => i.Mode is PayoffMode.Partial or PayoffMode.Deposit)
                .WithMessage("Una recogida parcial o un abono lleva el valor a aplicar.");
            p.RuleFor(i => i.ProductPublicId).NotEmpty().When(i => i.Kind != PayoffProductKind.CommercialReceivable)
                .WithMessage("Diga qué producto se recoge.");
        }).When(x => x.Input?.Payoffs is not null);
    }
}

/// <summary>
/// En una <see cref="TransaccionExplicita"/> (data-model §7.2, §7.3):
/// <list type="number">
/// <item><see cref="RequisitosDelDesembolso"/>, fuera del cerrojo; la huella de la vista previa, si viene;</item>
/// <item>el cerrojo de T34: <c>LND_Setup</c> compartido → <c>LND_DebtorControls</c> del deudor y los codeudores → <c>LND_LoanControls</c> del crédito
/// (tramo siguiente) y de los recogidos, en una sentencia;</item>
/// <item>la puesta al día de los recogidos y del crédito (<see cref="IMotorDeCreditoEnDesembolso"/>, US4);</item>
/// <item>por <see cref="RegistroDeCartera"/>: el crédito con su control (primer tramo), la operación <c>Disbursement</c> con su fila
/// <c>+Principal</c>, la versión de condiciones <c>Origination</c> (primer tramo), el plan regenerado sobre lo desembolsado (D12) y la posición del
/// día; en el primer tramo además las partes, el vínculo de forma de pago y la preferencia de abono (<c>Origin = Application</c>);</item>
/// <item>las recogidas por <see cref="RecogidaEnElDesembolso"/> (el contrato, en esta misma transacción);</item>
/// <item>la orden de giro por el neto con beneficiario y cuenta sellados;</item>
/// <item>la solicitud <c>Disbursed</c>, sus garantías y su pagaré al crédito, la intervención <c>Desembolsar</c>;</item>
/// <item>la numeración (al final del orden de cerrojos) y la emisión por <see cref="EmisionDeCartera"/>.</item>
/// </list>
/// Guarda dentro de la transacción en dos tiempos —el crédito y la operación con su libro, condiciones y plan (sus Id) y, después, la orden,
/// los vínculos y los mensajes— porque esas tablas no tienen navegación entre sí; todo se confirma o se revierte junto, y nada se guarda antes
/// de que las recogidas queden aplicadas. El evento de auditoría lleva la distribución completa por
/// cuota y concepto (T23).
/// <para>
/// 2026-11-27 (feature 028, T035, T041; FR-015, FR-016): si la vista previa tuvo diferencias u omitidos, quien desembolsa los reconoce
/// (<c>acknowledgedPayoffDifferences</c>; si no, <c>Lending.Payoff.DifferencesNotAcknowledged</c>); bajo el cerrojo, después de la puesta al día, las
/// recogidas se vuelven a evaluar y si lo que se aplica cambió se pide otra vista previa; lo que aplica el contrato debe ser lo liquidado
/// (<c>Lending.Payoff.AppliedDiffers</c>); la orden sale por el bruto menos lo <b>aplicado</b> (H4); cada producto deja su fila
/// <c>LND_DisbursementPayoffs</c> —también el omitido— y la auditoría lleva lo proyectado, lo de ahora, lo aplicado, la diferencia y quién la reconoció.
/// Un tramo siguiente no recoge.
/// </para>
/// (nuevo)
/// </summary>
public sealed class ExecuteLoanDisbursementCommandHandler(
    IApplicationDbContext db,
    RequisitosDelDesembolso requisitos,
    TramoDeDesembolso tramos,
    ICerrojoDeCartera cerrojo,
    RegistroDeCartera registro,
    NumeradorDeCartera numerador,
    RecogidaEnElDesembolso recogida,
    IMotorDeCreditoEnDesembolso motor,
    EmisionDeCartera emision,
    FechaContable fechaContable,
    RegistroDeIntervenciones intervenciones,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<ExecuteLoanDisbursementCommand, Result<DisbursementResultDto>>
{
    public async Task<Result<DisbursementResultDto>> Handle(ExecuteLoanDisbursementCommand request, CancellationToken ct)
    {
        var e = request.Input;
        var evaluados = await requisitos.EvaluarAsync(e, ct);
        if (evaluados.IsFailure) return Falla(evaluados.Error);
        var r = evaluados.Value;
        if (r.Bloqueos.Count > 0) return Falla(r.Bloqueos[0]);
        if (e.ExpectedPreviewSha256 is { Length: > 0 } vista && !string.Equals(vista, r.Huella, StringComparison.Ordinal))
            return Falla(ErroresDeDesembolso.VistaPreviaCambio(r.Huella));
        // 2026-11-27 (feature 028, T035; FR-015): una diferencia u omisión se reconoce antes de confirmar.
        if (r.RequiereReconocimiento && !e.AcknowledgedPayoffDifferences)
        {
            var porReconocer = r.Recogidas.Where(x => x.PorReconocer).ToList();
            return Falla(ErroresDeRecogida.DiferenciasSinReconocer(porReconocer.Count, porReconocer.Sum(x => x.Diferencia.Total)));
        }
        var contable = await fechaContable.CalcularAsync(r.FechaValor, ct);
        if (contable.IsFailure) return Falla(contable.Error);
        var fecha = contable.Value;

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var s = await db.CreditApplications.FirstAsync(a => a.Id == r.Solicitud.Id, ct);
            var deudores = r.Partes.Select(p => p.PersonId).Append(s.ApplicantPersonId).Distinct().Order().ToList();
            var creditos = r.Recogidas.Select(x => x.LoanId).Concat(r.CreditoId is { } c ? [c] : []).Distinct().Order().ToList();
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync(deudores, ModoDeEspera.Esperar, ct);
            if (creditos.Count > 0) await cerrojo.BloquearCreditosAsync(creditos, ModoDeEspera.Esperar, ct);

            // Lo que el cerrojo protege se relee: otro desembolso del mismo expediente pudo ganar.
            if (r.EsElPrimero ? s.Status != CreditApplicationStatus.Approved : s.Status != CreditApplicationStatus.Disbursed)
                return Falla(ErroresDeDesembolso.NoAprobada(s.Status.ToString()));
            // 2026-11-27 (feature 028): un omitido (ya cancelado) se bloquea —que nadie lo reabra mientras tanto— pero no se pone al día.
            var omitidos = r.Recogidas.Where(x => x.Omitida).Select(x => x.LoanId).ToHashSet();
            await motor.PonerAlDiaAsync(creditos.Where(c => !omitidos.Contains(c)).ToList(), r.FechaValor, ct);
            // 2026-11-27 (feature 028, T035): bajo el cerrojo las recogidas se vuelven a evaluar; si lo que se aplica cambió (un pago entre la vista previa y
            // la confirmación), se pide otra vista previa.
            var releidas = await requisitos.ReevaluarRecogidasAsync(r, ct);
            if (releidas.IsFailure) return Falla(releidas.Error);
            if (!MismasRecogidas(r.Recogidas, releidas.Value))
                return Falla(ErroresDeDesembolso.VistaPreviaCambio(RequisitosDelDesembolso.HuellaCon(r, releidas.Value)));

            var deudor = r.Personas[s.ApplicantPersonId];
            var actor = await actorActual.ObtenerAsync(ct);
            var ahora = reloj.UtcNow;

            // El crédito y su control: en el primer tramo nacen (y se guardan para tener el Id); en uno siguiente se leen.
            Loan credito;
            LoanControl control;
            if (r.CreditoId is { } existente)
            {
                credito = await db.Loans.FirstAsync(l => l.Id == existente, ct);
                control = await db.LoanControls.FirstAsync(x => x.LoanId == existente, ct);
            }
            else
            {
                credito = new Loan
                {
                    DebtorPersonId = s.ApplicantPersonId,
                    DebtorKind = s.DebtorKind,
                    BranchId = s.BranchId,
                    CreditLineId = s.CreditLineId,
                    Origin = LoanOrigin.Origination,
                    Status = LoanStatus.Active,
                    ApplicationId = s.Id,
                    ApprovedAmount = r.Aprobado,
                    OpenedOn = r.FechaValor,
                };
                await numerador.NumerarCreditoAsync(credito, TramoDeDesembolso.PrefijoDeCredito, ct);
                db.Loans.Add(credito);
                control = registro.IniciarControl(credito, r.FechaValor, ahora);
            }

            var entradas = new
            {
                application = s.PublicId,
                tranche = r.Tramo,
                gross = r.Bruto,
                valueDate = r.FechaValor,
                form = r.Forma.ToString(),
                payee = r.Beneficiario?.PersonPublicId,
                bank = r.Beneficiario?.BankId,
                account = r.Beneficiario?.AccountNumber,
                payoffs = r.Recogidas.Select(x => new { loan = x.LoanPublicId, mode = x.Modo, amount = x.AAplicar, omitted = x.Omitida }),
                rate = r.Simulacion.TasaPactada,
                usury = r.Simulacion.Usura.UsuraPublicId,
                simulation = r.Simulacion.Huella,
                acknowledgeWarnings = request.AcknowledgeWarnings,
            };
            var operacion = await tramos.RegistrarAsync(credito, control, r.Tramo, r.Forma, r.Bruto, r.FechaValor, fecha, entradas,
                r.Simulacion.Plan.RulesVersion, r.Prevalidacion.Outcome, null, ct);

            var condiciones = r.EsElPrimero
                ? tramos.Condiciones(credito, operacion, r)
                : await db.LoanTermsVersions.AsNoTracking().Where(t => t.LoanId == credito.Id).OrderByDescending(t => t.VersionNumber).FirstAsync(ct);
            var numeroDelPlan = await tramos.SiguienteVersionDelPlanAsync(credito.Id, ct);
            var planDelTramo = await tramos.PlanDelTramoAsync(r, ct);
            var plan = await tramos.PlanAsync(credito, condiciones, operacion, r, numeroDelPlan, planDelTramo, ct);
            registro.RegistrarPosicion(control, Posicion(credito, r, operacion, condiciones));

            // Recogidas por el contrato, en esta transacción y antes de guardar nada: si una no queda aplicada, el desembolso no existe.
            var aplicadas = await recogida.AplicarAsync(operacion, deudor.PublicId, r.Recogidas, ct);
            if (aplicadas.IsFailure) return Falla(aplicadas.Error);
            await db.SaveChangesAsync(ct);

            // 2026-11-27 (feature 028, T035): la fila de cada producto recogido (también el omitido), con lo que se aplicó y quién reconoció la diferencia.
            var aplicado = aplicadas.Value.Sum(a => a.Comprobante.AppliedAmount);
            var reconocio = r.RequiereReconocimiento ? Recortar(actor.Name, DisbursementPayoff.LargoDeQuienReconoce) : null;
            await FilasDeRecogidaAsync(operacion, r.Recogidas, aplicadas.Value, reconocio, ahora, ct);

            // La puesta al día calculada antes del tramo (US4, C04): sus partes LoanCatchUp apuntan a esta operación, ya guardada.
            var puestaAlDia = await motor.RegistrarPuestaAlDiaAsync(operacion, ct);
            if (puestaAlDia.IsFailure) return Falla(puestaAlDia.Error);

            // La orden por el neto (nunca en especie: el validador lo impide).
            // 2026-11-23 (descuento condicionado): al proveedor se le gira el valor del bien: el capital más lo que paga el fondo.
            // 2026-11-27 (feature 028, T035; H4): el neto es el bruto menos lo que el contrato aplicó de verdad, no lo evaluado antes del cerrojo.
            var orden = await tramos.EmitirOrdenAsync(credito, operacion, r.Beneficiario!, r.Bruto + r.Subsidio, aplicado, e.PaidByFile, null, ct);
            orden.SubsidyAmount = r.Subsidio;

            if (r.EsElPrimero)
            {
                foreach (var parte in r.Partes.Where(p => p.Role is CreditPartyRole.CoDebtor or CreditPartyRole.Guarantor))
                    db.LoanParties.Add(LoanParty.DeLaSolicitud(credito.Id, parte.PersonId, parte.Role, r.FechaValor, parte.Id));
                db.LoanPayrollLinks.Add(await VinculoAlOriginarAsync(s, credito.Id, operacion.Id, r.FechaValor, ct));
                db.PrepaymentPreferences.Add(PrepaymentPreference.Registrar(s.ApplicantPersonId, credito.Id, null, s.PrepaymentChoice, 1, operacion.RegisteredAt,
                    PreferenceOrigin.Application, actor.UserId, s.Id, operacion.Id));
                s.Desembolsar(credito.Id);
                foreach (var garantia in await db.Collaterals.Where(x => x.ApplicationId == s.Id && !x.IsDeleted && x.LoanId == null).ToListAsync(ct))
                    garantia.AsignarCredito(credito.Id);
                foreach (var pagare in await db.PromissoryNotes.Where(x => x.ApplicationId == s.Id && !x.IsDeleted && x.LoanId == null).ToListAsync(ct))
                    pagare.AsignarCredito(credito.Id);
            }
            intervenciones.Registrar(r.Intervencion!, EmisionDeCartera.ClaseDesembolso, operacion.PublicId);

            // Emisión: aparte, la baja del contingente (A3) antes que el hecho principal, que es el último de su cadena (C08 hereda de él).
            var hecho = new HechoDeCredito(credito, condiciones, r.Linea.Code, deudor.PublicId, deudor.IsEmployee, r.SucursalPublicId!.Value, fecha);
            await emision.BajaDeAprobadoSinDesembolsarAsync(hecho, operacion, s.PublicId, s.Number ?? string.Empty, r.Bruto, r.Aprobado - r.Desembolsado, ct);
            var recogidas = aplicadas.Value.Select(a => new PayoffDeductionV1
            {
                Loan = new LoanRefV1 { LoanPublicId = a.Recogida.LoanPublicId, LoanNumber = a.Recogida.Number },
                OperationPublicId = a.Comprobante.OperationPublicId,
                Amount = a.Comprobante.AppliedAmount,
            }).ToList();
            var emitidos = await emision.DesembolsoAsync(new DesembolsoAEmitir(hecho, operacion, r.Tramo, r.EsElUltimo, r.Forma, r.Beneficiario!.ComoPayee(),
                r.Bruto, recogidas, orden, TramoDeDesembolso.Resumen(r, numeroDelPlan, planDelTramo), r.Prevalidacion.Outcome,
                Subsidio: r.Compra?.ComoHecho()), ct);
            if (emitidos.IsFailure) return Falla(emitidos.Error);
            await requisitos.AlertarNoContabilizableAsync(r, operacion.Number, ct);

            await db.SaveChangesAsync(ct);
            await auditoria.EmitirOperacionRegistradaAsync(operacion.PublicId, operacion.Number, nameof(LoanOperationKind.Disbursement),
                Distribucion(r, operacion, recogidas, aplicadas.Value, reconocio), ct);

            return Result.Success(new DisbursementResultDto(
                credito.PublicId, credito.Number, operacion.PublicId, operacion.Number, r.Tramo, r.Bruto,
                aplicadas.Value.Select(a => new DisbursementPayoffDto(a.Recogida.LoanPublicId, a.Recogida.Number, a.Comprobante.AppliedAmount,
                    a.Comprobante.OperationPublicId)
                {
                    Mode = a.Recogida.Total ? nameof(PayoffMode.Total) : nameof(PayoffMode.Partial),
                    Projected = a.Recogida.ConProyeccion ? a.Recogida.Proyectado : null,
                    Current = a.Recogida.Actual,
                    Difference = a.Recogida.ConProyeccion ? a.Recogida.Diferencia.Total : null,
                    Causes = RequisitosDelDesembolso.Causas(a.Recogida.Diferencia.Causa),
                }).ToList(),
                orden.NetAmount,
                new DisbursementOrderRefDto(orden.PublicId, orden.Number, orden.Status.ToString()),
                plan.VersionNumber,
                emitidos.Value.Select(m => new EmittedMessageDto(m.Type, m.PublicId)).ToList(),
                PreviewDisbursementQueryHandler.Prevalidacion(r),
                r.Avisos)
            {
                OmittedPayoffs = r.Recogidas.Where(x => x.Omitida).Select(x => new DisbursementOmittedPayoffDto(x.LoanPublicId, x.Number,
                    x.ConProyeccion ? x.Proyectado : null, x.Avisos.FirstOrDefault() ?? string.Empty)).ToList(),
            });
        }, ct);
    }

    /// <summary>La posición del día del desembolso: el capital desembolsado, sin mora (data-model §6.1).</summary>
    private static LoanDailyPosition Posicion(Loan credito, RequisitosEvaluados r, Domain.Entities.Lending.Operations.LoanOperation operacion,
        LoanTermsVersion condiciones) => new()
    {
        LoanId = credito.Id,
        Loan = credito.Id == 0 ? credito : null,
        PositionDate = r.FechaValor,
        Version = r.Tramo,
        RegisteredAt = operacion.RegisteredAt,
        Status = LoanStatus.Active,
        BranchId = credito.BranchId,
        CreditLineId = credito.CreditLineId,
        ModalityCode = condiciones.ModalityCode,
        SubsegmentCode = condiciones.SubsegmentCode,
        Principal = r.Desembolsado,
        PayoffAmount = r.Desembolsado,
        AppliedAnnualRate = condiciones.AnnualRate,
        InputsSha256 = operacion.InputsSha256,
    };

    /// <summary>
    /// La distribución completa por cuota y concepto del tramo (T23): el capital, el plan y las recogidas. 2026-11-27 (feature 028, T041; FR-030): por
    /// producto recogido, lo proyectado, lo de ahora, lo aplicado, la diferencia con su causa, si se omitió y quién lo reconoció.
    /// </summary>
    private static object Distribucion(RequisitosEvaluados r, Domain.Entities.Lending.Operations.LoanOperation operacion, IReadOnlyList<PayoffDeductionV1> recogidas,
        IReadOnlyList<RecogidaAplicada> aplicadas, string? reconocio) => new
    {
        operation = operacion.Number,
        tranche = r.Tramo,
        valueDate = r.FechaValor,
        lines = new[] { new { installment = (int?)null, concept = nameof(LoanConcept.Principal), amount = r.Bruto } },
        // 2026-11-16 (feature 024, fase 2): con la administración de cada cuota, aparte del seguro.
        schedule = r.Simulacion.Plan.Cuotas.Select(c => new
        {
            installment = c.Numero, dueDate = c.Vencimiento, principal = c.Capital, interest = c.Interes, insurance = c.Seguro, administration = c.Administracion,
        }),
        payoffs = recogidas.Select(p => new { loan = p.Loan.LoanNumber, operation = p.OperationPublicId, amount = p.Amount }),
        net = r.Bruto - recogidas.Sum(p => p.Amount),
        payoffDetail = r.Recogidas.Select(x => new
        {
            loan = x.Number,
            mode = x.Modo,
            projected = x.ConProyeccion ? x.Proyectado : (decimal?)null,
            current = x.Actual,
            applied = aplicadas.FirstOrDefault(a => a.Recogida.LoanId == x.LoanId)?.Comprobante.AppliedAmount ?? 0m,
            difference = x.Diferencia.Total,
            causes = RequisitosDelDesembolso.Causas(x.Diferencia.Causa),
            omitted = x.Omitida,
            acknowledgedBy = x.PorReconocer ? reconocio : null,
        }),
    };

    /// <summary>¿Las recogidas releídas bajo el cerrojo aplican lo mismo que las de la vista previa? (mismos créditos, modo, valor, diferencia y omisión).</summary>
    private static bool MismasRecogidas(IReadOnlyList<RecogidaEvaluada> antes, IReadOnlyList<RecogidaEvaluada> ahora) =>
        antes.Count == ahora.Count
        && antes.Zip(ahora).All(p => p.First.LoanId == p.Second.LoanId && p.First.Modo == p.Second.Modo && p.First.AAplicar == p.Second.AAplicar
                                     && p.First.Omitida == p.Second.Omitida && p.First.Diferencia.Total == p.Second.Diferencia.Total);

    /// <summary>
    /// 2026-11-27 (feature 028, T035; data-model <c>LND_DisbursementPayoffs</c>): una fila por producto recogido (<see cref="DisbursementPayoff.Evaluar"/> y,
    /// si se aplicó, <see cref="DisbursementPayoff.Aplicar"/> con la operación de pago del recogido). La operación del desembolso ya tiene su Id.
    /// </summary>
    private async Task FilasDeRecogidaAsync(Domain.Entities.Lending.Operations.LoanOperation operacion, IReadOnlyList<RecogidaEvaluada> recogidas,
        IReadOnlyList<RecogidaAplicada> aplicadas, string? reconocio, DateTime ahora, CancellationToken ct)
    {
        if (recogidas.Count == 0) return;
        var publicos = aplicadas.Select(a => a.Comprobante.OperationPublicId).ToList();
        var pagos = publicos.Count == 0
            ? new Dictionary<Guid, long>()
            : await db.LoanOperations.AsNoTracking().Where(o => publicos.Contains(o.PublicId)).ToDictionaryAsync(o => o.PublicId, o => o.Id, ct);
        foreach (var x in recogidas)
        {
            var fila = DisbursementPayoff.Evaluar(operacion.Id, x.ProyectadaId, ProductoRecogido.Credito(x.LoanId, x.Number),
                x.Total ? PayoffMode.Total : PayoffMode.Partial, InstallmentTreatment.Include, x.ModoDeIntereses, x.Proyectado, x.Actual, x.Diferencia,
                x.Omitida, RecogidaEnElDesembolso.Clave(operacion.PublicId, x.LoanPublicId), x.PorReconocer ? reconocio : null,
                x.PorReconocer && reconocio is not null ? ahora : null);
            if (aplicadas.FirstOrDefault(a => a.Recogida.LoanId == x.LoanId) is { } a)
                fila.Aplicar(a.Comprobante.AppliedAmount, pagos.TryGetValue(a.Comprobante.OperationPublicId, out var pago) ? pago : null);
            db.DisbursementPayoffs.Add(fila);
        }
    }

    private static string? Recortar(string? texto, int largo) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Length <= largo ? texto : texto[..largo];

    /// <summary>
    /// El vínculo de forma de pago con que nace el crédito (C44; K2, T0842; FR-109, FR-111): la forma aprobada; la nómina propia con el empleado como
    /// pagador (G7) y, desde K2, el convenio de la nómina propia si existe; la libranza con la pagaduría y el convenio de la solicitud, la
    /// identificación del empleado ante ella, la certificación (el documento de la solicitud sin requisito) y el ingreso certificado. La fecha de la
    /// libranza, para la prelación, es la del desembolso.
    /// </summary>
    private async Task<LoanPayrollLink> VinculoAlOriginarAsync(CreditApplication s, int creditoId, long operacionId, DateOnly fecha, CancellationToken ct)
    {
        if (s.RepaymentChannel == RepaymentChannel.PaymasterDeduction && s.PayrollAgreementId is { } convenioId)
        {
            var pagaduria = await db.PayrollAgreements.AsNoTracking().Where(a => a.Id == convenioId).Select(a => a.PaymasterPersonId).FirstAsync(ct);
            var certificacion = await db.CreditApplicationDocuments.AsNoTracking()
                .Where(d => d.ApplicationId == s.Id && d.RequirementId == null && d.PartyId == null && !d.IsDeleted)
                .OrderByDescending(d => d.DocumentDate).ThenByDescending(d => d.Id).FirstOrDefaultAsync(ct);
            var ingreso = await db.CreditApplicationParties.AsNoTracking()
                .Where(p => p.ApplicationId == s.Id && p.Role == CreditPartyRole.Applicant && !p.IsDeleted).Select(p => p.MonthlyIncome).FirstOrDefaultAsync(ct);
            var libranza = LoanPayrollLink.AlOriginar(creditoId, s.RepaymentChannel, pagaduria, operacionId, fecha);
            libranza.PayrollAgreementId = convenioId;
            libranza.EmployeeIdAtPaymaster = s.PayrollEmployeeIdentifier;
            libranza.LibranzaDate = fecha;
            libranza.CertificationAttachmentPublicId = certificacion?.AttachmentPublicId;
            libranza.CertificationDate = certificacion?.DocumentDate;
            libranza.CertifiedNetIncome = ingreso;
            return libranza;
        }
        var vinculo = LoanPayrollLink.AlOriginar(creditoId, s.RepaymentChannel,
            s.RepaymentChannel == RepaymentChannel.OwnPayroll ? s.ApplicantPersonId : null, operacionId, fecha);
        if (s.RepaymentChannel == RepaymentChannel.OwnPayroll)
        {
            vinculo.LibranzaDate = fecha;
            vinculo.PayrollAgreementId = await db.PayrollAgreements.AsNoTracking().Where(a => a.IsOwnPayroll && !a.IsDeleted).Select(a => (int?)a.Id)
                .FirstOrDefaultAsync(ct);
        }
        return vinculo;
    }

    private static Result<DisbursementResultDto> Falla(Error e) => Result.Failure<DisbursementResultDto>(e);
}
