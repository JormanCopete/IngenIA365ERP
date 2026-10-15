using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Accrual;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Disbursements.Collaterals;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Ledger;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Entities.Lending.Schedules;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Rates;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Lending.Schedules;
using IngenIA365ERP.Domain.Lending.Segregation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Novelties;

/// <summary>
/// Aplica una novedad aprobada (feature 013, K3, T0881; api.md §11.1, <c>POST /api/lending/novelties/{id}/apply</c>, <c>Lending.Novelties.Apply</c>, con
/// clave; FR-090–FR-092, FR-098, US11-3; mensajes.md §10.20). En una transacción y un guardado (más el del enlazado, que necesita su Id):
/// <list type="number">
/// <item>sólo aprobada (<c>Lending.Novelty.NotApproved</c>; aplicada, <c>.AlreadyApplied</c>) y por quien no la radicó (<c>Lending.Segregation.Conflict</c>);</item>
/// <item>los consentimientos: los pendientes o rechazados sin resolver los resuelve quien aplica en <paramref name="WithoutConsent"/> con motivo —no
/// aplicar (la novedad queda anulada) o aplicar con esa garantía extinguida, si la cobertura que queda cumple la línea o quien aplica aprueba la
/// excepción con la atribución del nivel de excepción (<c>approveCoverageException</c>, bloque B2; si no, <c>Lending.Novelty.CoverageInsufficient</c>
/// con <c>required</c> y <c>remaining</c>)—; si queda alguno, <c>Lending.Novelty.ConsentMissing</c> con
/// <c>parties[]</c>;</item>
/// <item>el cálculo a hoy (la usura vigente no se exceptúa) y la validación previa contable de pantalla (A8, <c>Lending.Prevalidation.NotPostable</c>);</item>
/// <item>la puesta al día y, por <see cref="RegistroDeCartera"/>, la operación <c>TermsChange</c> con sus filas (capitalización; en una novación o
/// refinanciación, el traslado al enlazado) y, en una prórroga, cambio de fecha o de tasa, la versión de condiciones (con <c>NoveltyId</c> y la causa) y la
/// del plan; el número del crédito se conserva y el plan anterior queda como versión;</item>
/// <item>en una novación o refinanciación: cada crédito anterior queda <c>Novated</c> y nace el enlazado (<c>OriginLoanId</c>, <c>OriginLinkKind</c>) con su
/// versión 1, su plan, su operación de origen y sus filas; hereda la calificación, la marca, el conteo y el historial de mora por el enlace; sus
/// garantías se constituyen aparte (FR-092). La refinanciación traslada el saldo de los recogidos al enlazado como la novación, no los paga por
/// <c>OwnLoanPayoff</c> (decisión del bloque B2, tasks.md T0881);</item>
/// <item>los hechos <c>CreditoReestructurado</c>, <c>CreditoModificado</c> o <c>CreditoNovado</c> en línea (<c>CN</c>) y, si el crédito pasa a
/// reestructurado, <c>DimensionContableCambiada</c> aparte; el acuerdo de insolvencia cierra su proceso en <c>Agreement</c> y levanta el bloqueo
/// <c>Insolvency</c> de crédito nuevo (bloque B2).</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed record ApplyLoanNoveltyCommand(Guid NoveltyPublicId, IReadOnlyList<WithoutConsentInput>? WithoutConsent = null, bool AcknowledgeWarnings = false)
    : IRequest<Result<ApplyNoveltyResultDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ApplyLoanNoveltyCommandValidator : AbstractValidator<ApplyLoanNoveltyCommand>
{
    public ApplyLoanNoveltyCommandValidator()
    {
        RuleFor(x => x.NoveltyPublicId).NotEmpty();
        RuleForEach(x => x.WithoutConsent).ChildRules(c =>
        {
            c.RuleFor(x => x.PartyPersonPublicId).NotEmpty();
            c.RuleFor(x => x.Decision).IsInEnum();
            c.RuleFor(x => x.Reason).NotEmpty().WithMessage("Diga por qué decide así ante la falta de consentimiento.").MaximumLength(500);
            c.RuleFor(x => x.ApproveCoverageException).Equal(false).When(x => x.Decision != ConsentRefusalResolution.ApplyWithGuaranteeExtinguished)
                .WithMessage("La excepción de cobertura sólo se aprueba al aplicar con la garantía extinguida.");
        });
    }
}

public sealed class ApplyLoanNoveltyCommandHandler(
    IApplicationDbContext db,
    CalculoDeNovedad calculo,
    HechosDeLaNovedad hechos,
    EmisionDeCartera emision,
    PuestaAlDiaDeCredito puestaAlDia,
    CargadorDeReconstruccion cargador,
    RegistroDeCartera registro,
    NumeradorDeCartera numerador,
    RegistroDeIntervenciones intervenciones,
    VersionVigenteDeLinea versiones,
    FechaContable fechaContable,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj,
    Attributions.MatrizVigenteDeAtribuciones matrices,
    Attributions.AtribucionDelDecisor atribucion) : IRequestHandler<ApplyLoanNoveltyCommand, Result<ApplyNoveltyResultDto>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<ApplyNoveltyResultDto>> Handle(ApplyLoanNoveltyCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var leida = await NovedadesDeCartera.LeerAsync(db, alcance, auditoria, request.NoveltyPublicId, seguir: true, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var (n, principal) = leida.Value;
        if (n.PuedeIrA(LoanNoveltyStatus.Applied) is { } estado) return Falla(ErroresDeGobierno.De(estado));
        var v = reloj.HoyLocal;
        var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(NovedadesDeCartera.Expediente, n.PublicId), AccionesDeSegregacion.Aplicar,
            v, ct);
        if (verificada.IsFailure) return Falla(verificada.Error);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var creditos = await calculo.CreditosAsync(n, ct);
            var ids = creditos.Select(c => c.Credito.Id).ToList();
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([principal.DebtorPersonId], ModoDeEspera.Esperar, ct);
            await cerrojo.BloquearCreditosAsync(ids.Order().ToList(), ModoDeEspera.Esperar, ct);
            if (creditos.Any(c => c.Credito.Status != LoanStatus.Active))
                return Falla(ErroresDeNovedades.CreditoNoActivo(creditos.First(c => c.Credito.Status != LoanStatus.Active).Credito.Number,
                    creditos.First(c => c.Credito.Status != LoanStatus.Active).Credito.Status.ToString()));

            // (2) Los consentimientos: quien aplica resuelve los que faltan.
            var consentimientos = await ConsentimientosAsync(n, request.WithoutConsent ?? [], verificada.Value, v, ct);
            if (consentimientos.IsFailure) return Falla(consentimientos.Error);
            if (consentimientos.Value is { } noSeAplica)
            {
                n.Cancelar(reloj.UtcNow, noSeAplica);
                await db.SaveChangesAsync(ct);
                return Result.Success(new ApplyNoveltyResultDto(null, null, null, null, null, [], [$"La novedad no se aplicó y quedó anulada: {noSeAplica}"]));
            }

            // (3) El cálculo a hoy y la validación previa contable (A8).
            var controles = await db.LoanControls.Where(c => ids.Contains(c.LoanId)).ToDictionaryAsync(c => c.LoanId, ct);
            var marcas = controles.ToDictionary(kv => kv.Key, kv => kv.Value.LastAccruedDate);
            var calculada = await calculo.CalcularAsync(n, v, ct, seriesHasta: marcas.Values.Max());
            if (calculada.IsFailure) return Falla(calculada.Error);
            var c = calculada.Value;
            if (c.Usura.Veredicto == VeredictoDeUsura.Excede)
                return Falla(ErroresDeLineasDeCredito.UsuraExcedida("newTerms.rate", v, c.Usura, c.Principal.Cargado.Vigentes.ModalityCode));
            var previa = await hechos.ArmarAsync(c, null, null, null, null, reloj.UtcNow, ct);
            if (previa.IsFailure) return Falla(previa.Error);
            var decision = await hechos.ValidarAsync(EmisionDeCartera.HechosDeNovedad(previa.Value), $"la novedad {n.Number}", alertar: true, ct);
            if (decision.Bloqueo is { } bloqueo) return Falla(bloqueo);
            var avisos = decision.Avisos.ToList();

            // (4) La puesta al día y las operaciones.
            await puestaAlDia.PrepararAsync(ids, v, ct);
            var contable = await fechaContable.CalcularAsync(v, ct);
            if (contable.IsFailure) return Falla(contable.Error);
            var punto = await NovedadesDeCartera.PuntoDeActaAsync(db, n.FinalDecisionId, ct);
            var ahora = reloj.UtcNow;
            var operaciones = new Dictionary<int, LoanOperation>();
            var efectos = new Dictionary<int, EfectoEnElLibro>();
            foreach (var credito in c.Creditos)
            {
                var esPrincipal = credito.Credito.Id == principal.Id;
                var contenido = new DecisionDeNovedad
                {
                    NoveltyPublicId = n.PublicId, NoveltyNumber = n.Number, Mechanism = n.Mechanism.ToString(), Cause = n.Cause.ToString(),
                    Side = nameof(LadoDeLaNovedad.Credito), Installments = c.Entrada.Cuotas, Rate = c.Terminos.Rate, Periodicity = c.Terminos.Periodicity?.ToString(),
                    PaymentDay = c.Terminos.PaymentDay, FirstDueDate = c.Terminos.FirstDueDate, RestructuringCountAfter = c.ReestructuracionesDespues,
                    Capitalized = esPrincipal ? c.Capitalizaciones.Select(PartidaDecidida.De).ToList() : [],
                };
                var operacion = await OperacionAsync(controles[credito.Credito.Id], credito.Credito, contenido, esPrincipal ? c.Capitalizado : 0m, n, contable.Value,
                    decision.Outcome, ahora, ct);
                operaciones[credito.Credito.Id] = operacion;
                var entrada = credito.Cargado.Entrada!;
                var marca = marcas[credito.Credito.Id];
                var libro = marca >= v ? (await cargador.LibroAsync([credito.Credito.Id], marca, ct))[credito.Credito.Id] : [];
                try
                {
                    efectos[credito.Credito.Id] = EfectoEnElLibro.Calcular(entrada,
                        CargadorDeReconstruccion.ConOperacion(entrada, operacion, contenido.Contenido(n.Id)), CargadorDeReconstruccion.IdEnLaEntrada(operacion), v,
                        marca, libro, []);
                }
                catch (ReglaDeCarteraException ex)
                {
                    return Falla(ErroresDeGobierno.De(ex.Problema));
                }
            }

            // (5) Las versiones de condiciones y del plan, o el crédito enlazado.
            var versionesDelPlan = await hechos.VersionesDelPlanAsync(ids, ct);
            int? versionDelPlan = null;
            LoanTermsVersion? condiciones = null;
            RepaymentScheduleVersion? plan = null;
            Loan? enlazado = null;
            LoanOperation? origenDelEnlazado = null;
            LoanPlanSummaryV1? resumenNuevo;
            if (!c.CreaEnlazado)
            {
                var operacion = operaciones[principal.Id];
                var completo = efectos[principal.Id].Despues.Plan!;
                condiciones = CondicionesDeNovedad.Version(c.Principal.Cargado.Vigentes, n, operacion, completo, c.Principal.Cargado.Vigentes.VersionNumber + 1,
                    c.ReestructuracionesDespues, v);
                registro.RegistrarVersionDeCondiciones(condiciones);
                versionDelPlan = versionesDelPlan.GetValueOrDefault(principal.Id) + 1;
                plan = registro.RegistrarPlanRegenerado(principal.Id, versionDelPlan.Value, condiciones, operacion, completo, c.Principal.Cargado.Entrada!.Festivos);
                resumenNuevo = ComprobantesDePago.PlanDelHecho(completo, versionDelPlan.Value, completo.Entrada.Periodicidad.ToString());
            }
            else
            {
                (enlazado, origenDelEnlazado, condiciones, plan) = await EnlazadoAsync(n, c, contable.Value, decision.Outcome, ahora, ct);
                versionDelPlan = 1;
                resumenNuevo = ComprobantesDePago.PlanDelHecho(c.Plan, 1, c.Plan.Entrada.Periodicidad.ToString());
            }

            // (6) Los hechos y las filas.
            var armada = await hechos.ArmarAsync(c, operaciones, enlazado is null ? null : new LoanRefV1 { LoanPublicId = enlazado.PublicId, LoanNumber = enlazado.Number },
                punto, resumenNuevo, ahora, ct);
            if (armada.IsFailure) return Falla(armada.Error);
            var emitidos = await emision.NovedadAsync(EmisionDeCartera.HechosDeNovedad(armada.Value), decision.Outcome, ct);
            if (emitidos.IsFailure) return Falla(emitidos.Error);
            foreach (var (loanId, efecto) in efectos)
            {
                var mensaje = emitidos.Value.GetValueOrDefault(loanId);
                registro.RegistrarTransacciones(efecto.Propias.Select(f => RegistroDeCartera.FilaDeHecho(loanId, f, operaciones[loanId], null, contable.Value,
                    operaciones[loanId].RegisteredAt, mensaje)));
                registro.RegistrarTransacciones(RegistroDeCartera.DiferenciasPorFamilia(loanId, efecto.Diferencias, operaciones[loanId], contable.Value,
                    operaciones[loanId].RegisteredAt, (_, _) => mensaje));
            }

            // (7) Los estados: los anteriores de una novación quedan novados; la novedad, aplicada; el acuerdo de insolvencia, cerrado.
            var actualizados = await db.Loans.Where(l => ids.Contains(l.Id)).ToListAsync(ct);
            if (n.Mechanism == NoveltyMechanism.BranchChange && c.Terminos.BranchPublicId is { } nueva)
            {
                // T0883 (C51): la oficina del crédito sólo cambia por novedad.
                var oficina = await db.Branches.AsNoTracking().Where(b => b.PublicId == nueva).Select(b => b.Id).FirstAsync(ct);
                actualizados.First(l => l.Id == principal.Id).BranchId = oficina;
            }
            if (c.CreaEnlazado)
                foreach (var anterior in actualizados)
                {
                    RegistroDeCartera.CambiarEstado(anterior, LoanStatus.Novated, v);
                    anterior.NovatedOn = v;
                }
            try
            {
                n.Aplicar(operaciones[principal.Id], condiciones, plan, enlazado, n.Cause == NoveltyCause.Restructuring ? c.ReestructuracionesDespues : null,
                    usuario, ahora);
            }
            catch (ReglaDeCarteraException ex)
            {
                return Falla(ErroresDeGobierno.De(ex.Problema));
            }
            if (n.InsolvencyProceedingId is { } proceso)
            {
                var insolvencia = await db.InsolvencyProceedings.FirstAsync(p => p.Id == proceso, ct);
                if (insolvencia.Status == InsolvencyProceedingStatus.Negotiating)
                {
                    insolvencia.CerrarConAcuerdo(n.Id, ahora);
                    await DebtorEvents.CierreDeInsolvencia.LevantarBloqueosAsync(db, insolvencia.PersonId, v, ahora, usuario,
                        $"Acuerdo de insolvencia aplicado con la novedad {n.Number}.", ct);
                }
            }
            if (enlazado is not null) db.LoanNoveltyLoans.Add(new LoanNoveltyLoan { LoanNoveltyId = n.Id, Loan = enlazado, Role = NoveltyLoanRole.Resulting });
            intervenciones.Registrar(verificada.Value, LendingDecisionSubjectsDeK3.Novelty, n.PublicId);
            await db.SaveChangesAsync(ct);

            // (8) La puesta al día (apunta a la operación del principal, ya guardada), la posición del día con la novedad y el enlazado (sus filas
            // necesitan su Id).
            var puesta = await puestaAlDia.RegistrarAsync(operaciones[principal.Id], ct);
            if (puesta.IsFailure) return Falla(puesta.Error);
            await db.SaveChangesAsync(ct);
            var ultimas = await PosicionesDeCartera.UltimasAsync(db, ids, v, v, ct);
            foreach (var (loanId, efecto) in efectos)
            {
                var u = ultimas.GetValueOrDefault((loanId, v));
                registro.PosicionVersionada(controles[loanId], efecto.Despues, actualizados.First(l => l.Id == loanId),
                    !c.CreaEnlazado && condiciones is not null ? condiciones : c.Creditos.First(x => x.Credito.Id == loanId).Cargado.Vigentes, u.Version, u.Huella,
                    ahora);
            }
            if (enlazado is not null && origenDelEnlazado is not null)
            {
                var nacido = await NacimientoDelEnlazadoAsync(enlazado, origenDelEnlazado, principal, contable.Value, emitidos.Value.GetValueOrDefault(principal.Id),
                    ahora, ct);
                if (nacido is not null) return Falla(nacido);
            }
            await db.SaveChangesAsync(ct);

            await auditoria.EmitirOperacionRegistradaAsync(operaciones[principal.Id].PublicId, operaciones[principal.Id].Number, nameof(LoanOperationKind.TermsChange),
                new
                {
                    novelty = n.Number, mechanism = n.Mechanism.ToString(), cause = n.Cause.ToString(), valueDate = v,
                    capitalized = c.Capitalizaciones.Select(p => new { installment = p.Cuota, concept = p.Concepto.ToString(), amount = p.Monto }),
                    lines = efectos.SelectMany(e => e.Value.Propias.Select(f => new { loan = e.Key, installment = f.Cuota, concept = f.Concepto.ToString(), kind = f.Clase.ToString(), amount = f.Monto })),
                    schedule = c.Plan.Cuotas.Select(q => new { installment = q.Numero, dueDate = q.Vencimiento, principal = q.Capital, interest = q.Interes }),
                    newLoan = enlazado?.Number,
                    withoutConsent = request.WithoutConsent,
                }, ct);
            return Result.Success(new ApplyNoveltyResultDto(operaciones[principal.Id].PublicId, operaciones[principal.Id].Number, versionDelPlan, enlazado?.PublicId,
                enlazado?.Number, emitidos.Value.Values.Select(x => x.ToString()).ToList(), avisos));
        }, ct);
    }

    /// <summary>La operación <c>TermsChange</c> de un crédito, numerada y sellada por el registro.</summary>
    private async Task<LoanOperation> OperacionAsync(LoanControl control, Loan credito, DecisionDeNovedad decision, decimal monto, LoanNovelty n, DateOnly contable,
        PrevalidationOutcome validacion, DateTime ahora, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        var payload = decision.Serializar();
        var numero = await numerador.NumeroDeOperacionAsync(LoanOperationKind.TermsChange, NovedadesDeCartera.PrefijoDeOperacion, ct);
        return registro.RegistrarOperacion(control, ahora, sello => new LoanOperation
        {
            LoanId = credito.Id,
            Loan = credito.Id == 0 ? credito : null,
            Kind = LoanOperationKind.TermsChange,
            Number = numero,
            Sequence = sello.Sequence,
            ValueDate = reloj.HoyLocal,
            AccountingDate = contable,
            RegisteredAt = sello.RegisteredAt,
            Amount = monto,
            ActorKind = actor.Kind,
            ActorUserId = actor.UserId,
            OriginUserCentralId = actor.CentralUserId,
            NoveltyId = n.Id,
            Reason = Recortar($"Novedad {n.Number}: {n.RequestReason}"),
            Payload = payload,
            InputsSha256 = TramoDeDesembolso.Sha256(payload),
            RulesVersion = VersionesDeReglas.Vigente,
            PrevalidationOutcome = validacion.ToString(),
        });
    }

    /// <summary>
    /// El crédito enlazado de una novación o refinanciación, sin guardar: el número, el control, la versión 1 de condiciones (causa <c>Novation</c>, el
    /// conteo heredado), la operación de origen (<c>TermsChange</c>, lado enlazado, con lo trasladado y lo capitalizado) y la versión 1 del plan.
    /// </summary>
    private async Task<(Loan, LoanOperation, LoanTermsVersion, RepaymentScheduleVersion)> EnlazadoAsync(LoanNovelty n, NovedadCalculada c, DateOnly contable,
        PrevalidationOutcome validacion, DateTime ahora, CancellationToken ct)
    {
        var anterior = c.Principal.Credito;
        var nuevo = new Loan
        {
            DebtorPersonId = anterior.DebtorPersonId,
            DebtorKind = anterior.DebtorKind,
            BranchId = anterior.BranchId,
            CreditLineId = anterior.CreditLineId,
            Origin = LoanOrigin.Origination,
            Status = LoanStatus.Active,
            ApprovedAmount = c.CapitalNuevo,
            OpenedOn = c.Fecha,
            OriginLoanId = anterior.Id,
            OriginLinkKind = n.Mechanism == NoveltyMechanism.Novation ? LoanLinkKind.Novation : LoanLinkKind.Refinancing,
        };
        await numerador.NumerarCreditoAsync(nuevo, TramoDeDesembolso.PrefijoDeCredito, ct);
        db.Loans.Add(nuevo);
        var control = registro.IniciarControl(nuevo, c.Fecha, ahora);

        var traslados = new List<PartidaDeNovedad> { new(LoanConcept.Principal, null, c.SaldoDeCapital + c.Recogido) };
        var otros = c.Creditos.SelectMany(x => HechosDeLaNovedad.Trasladado(x.Estado, x.Credito.Id == anterior.Id ? c.Capitalizaciones : []))
            .Where(p => p.Concepto != LoanConcept.Principal)
            .GroupBy(p => (p.Concepto, Cuota: p.Concepto is LoanConcept.Advance or LoanConcept.CreditBalance ? (int?)null : 1))
            .Select(g => new PartidaDeNovedad(g.Key.Concepto, g.Key.Cuota, g.Sum(p => p.Monto))).Where(p => p.Monto > 0m);
        traslados.AddRange(otros);
        var decision = new DecisionDeNovedad
        {
            NoveltyPublicId = n.PublicId, NoveltyNumber = n.Number, Mechanism = n.Mechanism.ToString(), Cause = n.Cause.ToString(),
            Side = nameof(LadoDeLaNovedad.Enlazado), Installments = c.Entrada.Cuotas, Rate = c.Terminos.Rate, Periodicity = c.Terminos.Periodicity?.ToString(),
            PaymentDay = c.Terminos.PaymentDay, FirstDueDate = c.Terminos.FirstDueDate, RestructuringCountAfter = c.ReestructuracionesDespues,
            Capitalized = c.Capitalizaciones.Select(PartidaDecidida.De).ToList(), Transfers = traslados.Select(PartidaDecidida.De).ToList(),
            LinkedLoans = c.Creditos.Select(x => x.Credito.Number).ToList(),
        };
        var operacion = await OperacionAsync(control, nuevo, decision, c.CapitalNuevo, n, contable, validacion, ahora, ct);
        var condiciones = CondicionesDeNovedad.Version(c.Principal.Cargado.Vigentes, n, operacion, c.Plan, 1, c.ReestructuracionesDespues, c.Fecha, nuevo);
        registro.RegistrarVersionDeCondiciones(condiciones);
        var plan = new RepaymentScheduleVersion
        {
            Loan = nuevo,
            VersionNumber = 1,
            TermsVersion = condiciones,
            Operation = operacion,
            EffectiveValueDate = c.Fecha,
            RegisteredAt = operacion.RegisteredAt,
            PrincipalBase = c.Plan.TotalCapital,
            FirstRegeneratedInstallment = 1,
            InstallmentCount = c.Plan.Cuotas.Count,
            RulesVersion = c.Plan.RulesVersion,
            InputsSha256 = operacion.InputsSha256,
        };
        var articulo829 = FechasDeVencimiento.CalendarioDelArticulo829(c.Principal.Cargado.Entrada!.Festivos);
        registro.RegistrarPlan(plan, c.Plan.Cuotas.Select(q => new RepaymentInstallment
        {
            ScheduleVersion = plan,
            InstallmentNumber = q.Numero,
            PeriodStart = q.Desde,
            DueDate = q.Vencimiento,
            ExigibleDate = FechasDeVencimiento.FechaExigible(q.Vencimiento, articulo829),
            OpeningPrincipal = q.SaldoInicial,
            Principal = q.Capital,
            Interest = q.Interes,
            GraceInterest = q.InteresDeGracia,
            Insurance = q.Seguro,
            OtherCharges = 0m,
            Total = q.Total,
            ClosingPrincipal = q.SaldoFinal,
            IsExtraordinary = q.Extraordinaria != 0m,
            IsGraceInstallment = q.SoloInteres,
        }).ToList());
        return (nuevo, operacion, condiciones, plan);
    }

    /// <summary>
    /// Ya guardado el enlazado: sus filas (las de su origen en la reconstrucción, con el mensaje del <c>CreditoNovado</c> del principal), su vínculo de
    /// forma de pago (el del anterior) y su posición del día. Devuelve el error si la reconstrucción no lo reconoce.
    /// </summary>
    private async Task<Error?> NacimientoDelEnlazadoAsync(Loan nuevo, LoanOperation origen, Loan anterior, DateOnly contable, Guid mensaje, DateTime ahora,
        CancellationToken ct)
    {
        var cargado = await cargador.CargarAsync(nuevo.Id, null, origen.ValueDate, ct);
        if (cargado is not { Listo: true }) return cargado?.Error ?? Settlement.LecturaDelCredito.Inexistente();
        EstadoDelCredito estado;
        try
        {
            estado = MotorDeCartera.Reconstruir(cargado.Entrada!, origen.ValueDate, origen.ValueDate);
        }
        catch (ReglaDeCarteraException ex)
        {
            return ErroresDeGobierno.De(ex.Problema);
        }
        var id = CargadorDeReconstruccion.IdEnLaEntrada(origen);
        registro.RegistrarTransacciones(estado.Libro.Movimientos.Where(m => m.OperacionId == id)
            .Select(f => RegistroDeCartera.FilaDeHecho(nuevo.Id, f, origen, null, contable, origen.RegisteredAt, mensaje)));

        var vinculo = await db.LoanPayrollLinks.AsNoTracking().Where(x => x.LoanId == anterior.Id && !x.IsDeleted && x.ValidTo == null)
            .OrderByDescending(x => x.ValidFrom).FirstOrDefaultAsync(ct);
        if (vinculo is not null)
        {
            var nuevoVinculo = LoanPayrollLink.AlOriginar(nuevo.Id, vinculo.RepaymentChannel, vinculo.PaymasterPersonId, origen.Id, origen.ValueDate);
            nuevoVinculo.IsOwnPayroll = vinculo.IsOwnPayroll;
            nuevoVinculo.PayrollAgreementId = vinculo.PayrollAgreementId;
            nuevoVinculo.EmployeeIdAtPaymaster = vinculo.EmployeeIdAtPaymaster;
            nuevoVinculo.LibranzaDate = vinculo.LibranzaDate;
            nuevoVinculo.CertificationAttachmentPublicId = vinculo.CertificationAttachmentPublicId;
            nuevoVinculo.CertificationDate = vinculo.CertificationDate;
            nuevoVinculo.CertifiedNetIncome = vinculo.CertifiedNetIncome;
            db.LoanPayrollLinks.Add(nuevoVinculo);
        }
        var control = await db.LoanControls.FirstAsync(x => x.LoanId == nuevo.Id, ct);
        var condiciones = await db.LoanTermsVersions.AsNoTracking().Where(t => t.LoanId == nuevo.Id).OrderByDescending(t => t.VersionNumber).FirstAsync(ct);
        registro.PosicionVersionada(control, estado, nuevo, condiciones, 0, null, ahora);
        return null;
    }

    /// <summary>
    /// Resuelve los consentimientos (FR-092): los de <paramref name="sinConsentimiento"/> quedan rehusados y resueltos con su motivo —aplicar con la
    /// garantía extinguida exige que la cobertura restante cumpla la línea—; si alguno queda sin resolver, <c>ConsentMissing</c>. Devuelve el motivo si
    /// la decisión es no aplicar (la novedad se anula), o nulo para seguir.
    /// </summary>
    private async Task<Result<string?>> ConsentimientosAsync(LoanNovelty n, IReadOnlyList<WithoutConsentInput> sinConsentimiento,
        IntervencionVerificada quien, DateOnly v, CancellationToken ct)
    {
        var usuario = quien.UsuarioId;
        var consentimientos = await db.LoanNoveltyConsents.Where(x => x.LoanNoveltyId == n.Id && !x.IsDeleted).ToListAsync(ct);
        var idsDePersona = consentimientos.Select(x => x.PersonId).Distinct().ToList();
        var personas = await db.People.AsNoTracking().Where(p => idsDePersona.Contains(p.Id)).ToDictionaryAsync(p => p.PublicId, p => p.Id, ct);
        var idsDeGarantia = consentimientos.Where(x => x.CollateralId != null).Select(x => x.CollateralId!.Value).Distinct().ToList();
        var garantias = await db.Collaterals.Where(g => idsDeGarantia.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, ct);
        string? noAplicar = null;
        foreach (var d in sinConsentimiento)
        {
            if (!personas.TryGetValue(d.PartyPersonPublicId, out var persona)) return Result.Failure<string?>(ErroresDeNovedades.ParteDesconocida());
            var suyos = consentimientos.Where(x => x.PersonId == persona && x.Status != ConsentStatus.Accepted
                && (d.CollateralPublicId is null || (x.CollateralId is { } g && garantias[g].PublicId == d.CollateralPublicId))).ToList();
            if (suyos.Count == 0) return Result.Failure<string?>(ErroresDeNovedades.ParteDesconocida());
            foreach (var x in suyos)
            {
                if (x.Status == ConsentStatus.Pending) x.Rehusar(usuario, reloj.UtcNow);
                if (d.Decision == ConsentRefusalResolution.DoNotApply)
                {
                    x.ResolverRechazo(ConsentRefusalResolution.DoNotApply, d.Reason);
                    noAplicar ??= d.Reason.Trim();
                    continue;
                }
                var extinguida = await ExtinguirAsync(n, x, garantias, d.Reason, d.ApproveCoverageException ? quien : null, v, ct);
                if (extinguida.IsFailure) return Result.Failure<string?>(extinguida.Error);
                x.ResolverRechazo(ConsentRefusalResolution.ApplyWithGuaranteeExtinguished, d.Reason, extinguida.Value.Cobertura, extinguida.Value.Excepcion);
            }
        }
        if (noAplicar is not null) return Result.Success<string?>(noAplicar);
        var faltan = consentimientos.Where(x => !x.Resuelto).ToList();
        if (faltan.Count > 0)
        {
            var nombres = await ReferenciasDeGobierno.PersonasAsync(db, faltan.Select(x => x.PersonId), ct);
            return Result.Failure<string?>(ErroresDeNovedades.ConsentimientosFaltantes(faltan.Select(x => (object)new
            {
                personPublicId = nombres[x.PersonId].PersonPublicId, fullName = nombres[x.PersonId].FullName, role = x.PartyRole.ToString(), status = x.Status.ToString(),
            }).ToList()));
        }
        return Result.Success<string?>(null);
    }

    /// <summary>
    /// Extingue la garantía de quien no consintió (la real, o su papel de codeudor o avalista en los créditos) si lo que queda cumple la regla de
    /// garantías de la línea vigente (cobertura mínima de las reales; codeudores exigidos) o, si no la cumple y quien aplica pidió la excepción
    /// (<paramref name="excepcion"/>), con la excepción aprobada (FR-092, <see cref="ExcepcionDeCoberturaAsync"/>); si no, <c>CoverageInsufficient</c>.
    /// Devuelve la cobertura restante en JSON y la decisión de la excepción, si la hubo. Sin guardar la extinción.
    /// </summary>
    private async Task<Result<(string Cobertura, int? Excepcion)>> ExtinguirAsync(LoanNovelty n, LoanNoveltyConsent x,
        IReadOnlyDictionary<int, Domain.Entities.Lending.Origination.Collateral> garantias, string motivo, IntervencionVerificada? excepcion, DateOnly v,
        CancellationToken ct)
    {
        var credito = await db.Loans.AsNoTracking().FirstAsync(l => l.Id == n.LoanId, ct);
        var condiciones = await db.LoanTermsVersions.AsNoTracking().Where(t => t.LoanId == credito.Id).OrderByDescending(t => t.VersionNumber).FirstAsync(ct);
        var linea = await versiones.LeerAsync(credito.CreditLineId, v, ct: ct);
        var cargado = await cargador.CargarAsync(credito.Id, null, v, ct);
        var capital = cargado is { Listo: true } ? MotorDeCartera.Reconstruir(cargado.Entrada!, v).Saldo(LoanConcept.Principal) : credito.ApprovedAmount;
        var regla = linea?.Garantias.OrderBy(g => g.Order).FirstOrDefault(g =>
            (g.MinAmount is not { } mn || capital >= mn) && (g.MaxAmount is not { } mx || capital <= mx)
            && (g.MinTermMonths is not { } pmn || condiciones.TermMonths >= pmn) && (g.MaxTermMonths is not { } pmx || condiciones.TermMonths <= pmx));

        if (x.CollateralId is { } id && garantias.TryGetValue(id, out var g))
        {
            var otras = await db.Collaterals.AsNoTracking()
                .Where(c => c.LoanId == credito.Id && c.Id != id && !c.IsDeleted && (c.Status == CollateralStatus.Constituted || c.Status == CollateralStatus.Valid))
                .ToListAsync(ct);
            var restante = capital == 0m ? 0m : decimal.Round(otras.Where(c => GarantiasDeCartera.Reales.Contains(c.Kind)).Sum(c => c.AdmissibleValue) / capital, 6);
            int? decisionReal = null;
            if (regla?.MinimumCoverage is { } exigida && GarantiasDeCartera.Reales.Contains(g.Kind) && restante < exigida)
            {
                if (excepcion is null) return Falla2(ErroresDeNovedades.CoberturaInsuficiente(exigida, restante));
                var aprobada = await ExcepcionDeCoberturaAsync(n, excepcion, $"cobertura {restante} frente a {exigida} de la línea", motivo, v, ct);
                if (aprobada.IsFailure) return Falla2(aprobada.Error);
                decisionReal = aprobada.Value;
            }
            if (g.PuedeIrA(CollateralStatus.Extinguished) is { } problema) return Falla2(ErroresDeGobierno.De(problema));
            g.Status = CollateralStatus.Extinguished;
            g.ExtinguishedOn = v;
            g.ExtinguishReason = Recortar($"Novedad {n.Number}: {motivo.Trim()}");
            var dueno = await GarantiasDeCartera.DuenoDeAsync(db, g.LoanId, g.ApplicationId, ct);
            if (dueno.IsSuccess)
            {
                var emitida = await GarantiasDeCartera.EmitirAsync(db, emision, fechaContable, v, reloj.UtcNow, g, dueno.Value,
                    EmisionDeCartera.MovimientosDeGarantia.Extinguida, -g.Value, 0m, ct);
                if (emitida.IsFailure) return Falla2(emitida.Error);
            }
            return Result.Success<(string, int?)>((JsonSerializer.Serialize(new
            {
                required = regla?.MinimumCoverage, remaining = restante, collateral = g.PublicId, exception = decisionReal is not null,
            }, Json), decisionReal));
        }

        var partes = await db.LoanParties.Where(p => p.LoanId == credito.Id && !p.IsDeleted).ToListAsync(ct);
        var codeudores = partes.Count(p => p.VigenteA(v) && p.Role == CreditPartyRole.CoDebtor && p.PersonId != x.PersonId);
        int? decisionPersonal = null;
        if (regla is { RequiredCoDebtors: > 0 } r && x.PartyRole == ConsentPartyRole.CoDebtor && codeudores < r.RequiredCoDebtors)
        {
            if (excepcion is null) return Falla2(ErroresDeNovedades.CoberturaInsuficiente(r.RequiredCoDebtors, codeudores));
            var aprobada = await ExcepcionDeCoberturaAsync(n, excepcion, $"{codeudores} codeudores frente a {r.RequiredCoDebtors} que exige la línea", motivo, v,
                ct);
            if (aprobada.IsFailure) return Falla2(aprobada.Error);
            decisionPersonal = aprobada.Value;
        }
        foreach (var p in partes.Where(p => p.PersonId == x.PersonId && p.VigenteA(v)))
        {
            p.ExtinguishedOn = v;
            p.ExtinguishReason = Recortar($"Novedad {n.Number}: {motivo.Trim()}");
        }
        return Result.Success<(string, int?)>((JsonSerializer.Serialize(new
        {
            required = regla?.RequiredCoDebtors, remaining = codeudores, exception = decisionPersonal is not null,
        }, Json), decisionPersonal));
    }

    /// <summary>
    /// La excepción de cobertura (FR-092: «sólo si la cobertura restante cumple la línea o se aprueba la excepción»): la aprueba quien aplica si tiene
    /// la atribución del nivel de excepción de la matriz vigente (<c>IsExceptionLevel</c>; un nivel colegiado o uno que no alcanza responde
    /// <c>Lending.Attribution.LevelRequired</c>); queda como decisión individual <c>Approve</c> sobre la novedad con <c>IsPolicyException</c>, ese nivel y
    /// el motivo, y su Id se sella en el consentimiento (<c>ExceptionDecisionId</c>). Sin matriz o sin nivel de excepción no hay excepción posible.
    /// </summary>
    private async Task<Result<int>> ExcepcionDeCoberturaAsync(LoanNovelty n, IntervencionVerificada quien, string faltante, string motivo,
        DateOnly v, CancellationToken ct)
    {
        var matriz = await matrices.LeerAsync(v, reloj.UtcNow, ct);
        if (matriz is null) return Result.Failure<int>(ErroresDeGobierno.SinMatrizVigente(v));
        var nivel = matriz.Niveles.Where(l => l.IsExceptionLevel).OrderByDescending(l => l.Rank).FirstOrDefault();
        if (nivel is null)
            return Result.Failure<int>(new Error(ErroresDeNovedades.CodigoCoberturaInsuficiente,
                "La matriz de atribuciones vigente no tiene nivel de excepción: la cobertura que falta no se puede aprobar por excepción."));
        if (await atribucion.ExigirAsync(nivel, matriz.Niveles, ErroresDeDecisionesYCupos.NivelRequerido(nivel.Code, nivel.Name, null), ct) is { } sinAtribucion)
            return Result.Failure<int>(sinAtribucion);
        var decision = CreditDecision.Individual(new LoDecidido(LendingDecisionSubjects.Novelty, n.PublicId, n.PublicId, CreditDecisionOutcome.Approve)
        {
            AttributionMatrixId = matriz.Matriz.Id,
            LevelId = nivel.Id,
            IsPolicyException = true,
            Reason = Recortar($"Excepción de cobertura al extinguir una garantía ({faltante}): {motivo.Trim()}"),
        }, quien.UsuarioId, quien.PersonaId, reloj.UtcNow);
        db.CreditDecisions.Add(decision);
        await db.SaveChangesAsync(ct);
        return Result.Success(decision.Id);
    }

    private static Result<(string Cobertura, int? Excepcion)> Falla2(Error e) => Result.Failure<(string Cobertura, int? Excepcion)>(e);

    private static string Recortar(string texto) => texto.Length <= 500 ? texto : texto[..500];

    private static Result<ApplyNoveltyResultDto> Falla(Error e) => Result.Failure<ApplyNoveltyResultDto>(e);
}

/// <summary>Los sujetos de K3 (alias de <see cref="Domain.Lending.Attributions.LendingDecisionSubjects"/>). (nuevo)</summary>
internal static class LendingDecisionSubjectsDeK3
{
    public const string Novelty = Domain.Lending.Attributions.LendingDecisionSubjects.Novelty;
}
