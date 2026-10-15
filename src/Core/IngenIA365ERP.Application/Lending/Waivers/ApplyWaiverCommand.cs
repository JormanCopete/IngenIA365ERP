using System.Text.Json;
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
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Ledger;
using IngenIA365ERP.Application.Lending.Novelties;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Lending.Risk;
using IngenIA365ERP.Domain.Lending.Segregation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Waivers;

/// <summary>
/// Aplica una condonación aprobada (feature 013, K3, T0884; api.md §11.2, <c>POST /api/lending/waivers/{id}/apply</c>, <c>Lending.Condonations.Apply</c>,
/// con clave; FR-094, US11-5; mensajes.md §10.21): sólo aprobada (<c>Lending.Waiver.NotApproved</c>) y por quien no la pidió
/// (<c>Lending.Segregation.Conflict</c>); la condonación de «todas las cuotas» se reparte en prelación (la cuota más antigua primero) con los saldos de
/// hoy, que no pueden ser menores que lo aprobado (<c>Lending.Waiver.ExceedsBalance</c>); la validación previa contable de pantalla (A8); la operación
/// <c>Waiver</c> y sus filas <c>Waiver</c> por <see cref="RegistroDeCartera"/>; <c>CondonacionAprobada</c> en línea (<c>CN</c>) con lo que sale contra el
/// <b>deterioro individual del último cierre</b> por concepto (menos lo que ya consumieron otras condonaciones desde ese cierre) y el resto al gasto. Si el
/// crédito queda sin saldo, se cancela. Todo en una transacción. (nuevo)
/// </summary>
public sealed record ApplyWaiverCommand(Guid WaiverPublicId, bool AcknowledgeWarnings = false) : IRequest<Result<ApplyWaiverResultDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ApplyWaiverCommandValidator : AbstractValidator<ApplyWaiverCommand>
{
    public ApplyWaiverCommandValidator() => RuleFor(x => x.WaiverPublicId).NotEmpty();
}

public sealed class ApplyWaiverCommandHandler(
    IApplicationDbContext db,
    CargadorDeReconstruccion cargador,
    PuestaAlDiaDeCredito puestaAlDia,
    PartesDeHechosDeCartera partes,
    RegistroDeCartera registro,
    NumeradorDeCartera numerador,
    RegistroDeIntervenciones intervenciones,
    EmisionDeCartera emision,
    ValidacionPreviaContableDeCartera validacionPrevia,
    FechaContable fechaContable,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<ApplyWaiverCommand, Result<ApplyWaiverResultDto>>
{
    public async Task<Result<ApplyWaiverResultDto>> Handle(ApplyWaiverCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var leida = await CondonacionesDeCartera.LeerAsync(db, alcance, auditoria, request.WaiverPublicId, seguir: true, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var (w, credito) = leida.Value;
        if (w.PuedeIrA(WaiverStatus.Applied) is { } estado) return Falla(ErroresDeGobierno.De(estado));
        var v = reloj.HoyLocal;
        var verificada = await intervenciones.VerificarAsync(new ExpedienteDeCartera(CondonacionesDeCartera.Expediente, w.PublicId), AccionesDeSegregacion.Aplicar,
            v, ct);
        if (verificada.IsFailure) return Falla(verificada.Error);

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([credito.DebtorPersonId], ModoDeEspera.Esperar, ct);
            await cerrojo.BloquearCreditosAsync([credito.Id], ModoDeEspera.Esperar, ct);
            var vivo = await db.Loans.FirstAsync(l => l.Id == credito.Id, ct);
            if (vivo.Status != LoanStatus.Active) return Falla(ErroresDeNovedades.CreditoNoActivo(vivo.Number, vivo.Status.ToString()));

            var control = await db.LoanControls.FirstAsync(c => c.LoanId == credito.Id, ct);
            var marca = control.LastAccruedDate;
            var cargado = await cargador.CargarAsync(credito.Id, null, marca > v ? marca : v, ct);
            if (cargado is not { Listo: true }) return Falla(cargado?.Error ?? Settlement.LecturaDelCredito.Inexistente());
            EstadoDelCredito antes;
            try
            {
                antes = MotorDeCartera.Reconstruir(cargado.Entrada!, v);
            }
            catch (ReglaDeCarteraException ex)
            {
                return Falla(ErroresDeGobierno.De(ex.Problema));
            }

            // Lo que se condona por concepto y cuota: la de «todas las cuotas» en prelación, con los saldos de hoy.
            var repartido = Repartir(w, antes);
            if (repartido.IsFailure) return Falla(repartido.Error);
            var lineas = repartido.Value;
            var contraElDeterioro = await DeterioroAplicableAsync(credito.Id, lineas, v, ct);

            // La validación previa contable de pantalla (A8).
            var contable = await fechaContable.CalcularAsync(v, ct);
            if (contable.IsFailure) return Falla(contable.Error);
            var datos = (await partes.DatosAsync([cargado], ct))[credito.Id];
            var hecho = datos.Hecho(contable.Value);
            var categoria = await new CategoriaContableDeCartera(db).VigenteAsync(credito.Id, contable.Value, ct);
            var condonado = lineas.Select(l => new ConceptAmountV1
            {
                Concept = l.Concepto.ToString(), InstallmentNumber = l.Cuota, Amount = l.Monto, ReasonCode = EmisionDeCartera.MotivoCondonacion,
            }).ToList();
            var punto = await NovedadesDeCartera.PuntoDeActaAsync(db, w.FinalDecisionId, ct);
            var previo = EmisionDeCartera.ContenidoDeCondonacion(hecho, categoria, w.PublicId, v, reloj.UtcNow, condonado, contraElDeterioro, punto);
            var decision = await validacionPrevia.DecidirAsync([new MensajeContableDto(new IntegrationEnvelopeV1
            {
                MessageId = Guid.Empty, Type = CondonacionAprobadaV1.Type, Kind = IntegrationMessageKind.Business, OriginModule = ModulosDeOrigen.Cartera,
                OriginEventKey = ClavesDeEvento.Confirmacion, BranchPublicId = hecho.SucursalPublicId,
            }, previo)], OrigenDeValidacionPrevia.Pantalla, $"la condonación {w.Number}", ct);
            if (decision.Bloqueo is { } bloqueo) return Falla(bloqueo);

            // La puesta al día y la operación.
            await puestaAlDia.PrepararAsync([credito.Id], v, ct);
            var pago = new DecisionDeCondonacion
            {
                WaiverPublicId = w.PublicId, WaiverNumber = w.Number, Lines = lineas.Select(PartidaDecidida.De).ToList(),
                ImpairmentApplied = contraElDeterioro.Select(a => new PartidaDecidida(a.Concept, a.InstallmentNumber, a.Amount)).ToList(),
            };
            var payload = pago.Serializar();
            var numero = await numerador.NumeroDeOperacionAsync(LoanOperationKind.Waiver, CondonacionesDeCartera.PrefijoDeOperacion, ct);
            var operacion = registro.RegistrarOperacion(control, reloj.UtcNow, sello => new LoanOperation
            {
                LoanId = credito.Id,
                Kind = LoanOperationKind.Waiver,
                Number = numero,
                Sequence = sello.Sequence,
                ValueDate = v,
                AccountingDate = contable.Value,
                RegisteredAt = sello.RegisteredAt,
                Amount = lineas.Sum(l => l.Monto),
                ActorKind = actor.Kind,
                ActorUserId = actor.UserId,
                OriginUserCentralId = actor.CentralUserId,
                WaiverId = w.Id,
                Reason = w.Reason.Length <= 500 ? w.Reason : w.Reason[..500],
                Payload = payload,
                InputsSha256 = TramoDeDesembolso.Sha256(payload),
                RulesVersion = cargado.Vigentes.RulesVersion,
                PrevalidationOutcome = decision.Outcome.ToString(),
            });
            var id = CargadorDeReconstruccion.IdEnLaEntrada(operacion);
            var libro = marca >= v ? (await cargador.LibroAsync([credito.Id], marca, ct))[credito.Id] : [];
            EfectoEnElLibro efecto;
            try
            {
                efecto = EfectoEnElLibro.Calcular(cargado.Entrada!, CargadorDeReconstruccion.ConOperacion(cargado.Entrada!, operacion, pago.Contenido()), id, v, marca,
                    libro, []);
            }
            catch (ReglaDeCarteraException ex)
            {
                return Falla(ErroresDeGobierno.De(ex.Problema));
            }

            var emitido = await emision.CondonacionAsync(new CondonacionAEmitir(hecho, w, operacion, condonado, contraElDeterioro, punto, decision.Outcome), ct);
            if (emitido.IsFailure) return Falla(emitido.Error);
            var mensaje = emitido.Value.PublicId;
            registro.RegistrarTransacciones(efecto.Propias.Select(f => RegistroDeCartera.FilaDeHecho(credito.Id, f, operacion, null, contable.Value,
                operacion.RegisteredAt, mensaje)));
            registro.RegistrarTransacciones(RegistroDeCartera.DiferenciasPorFamilia(credito.Id, efecto.Diferencias, operacion, contable.Value,
                operacion.RegisteredAt, (_, _) => mensaje));
            w.Aplicar(operacion, usuario, reloj.UtcNow);
            intervenciones.Registrar(verificada.Value, LendingDecisionSubjects.Condonation, w.PublicId);
            if (Math.Max(0m, efecto.Despues.ValorParaCancelar) == 0m) RegistroDeCartera.CambiarEstado(vivo, LoanStatus.PaidOff, v);
            await db.SaveChangesAsync(ct);

            var puesta = await puestaAlDia.RegistrarAsync(operacion, ct);
            if (puesta.IsFailure) return Falla(puesta.Error);
            await db.SaveChangesAsync(ct);
            var u = (await PosicionesDeCartera.UltimasAsync(db, [credito.Id], v, v, ct)).GetValueOrDefault((credito.Id, v));
            registro.PosicionVersionada(control, efecto.Despues, vivo, cargado.Vigentes, u.Version, u.Huella, reloj.UtcNow);
            await db.SaveChangesAsync(ct);

            await auditoria.EmitirOperacionRegistradaAsync(operacion.PublicId, operacion.Number, nameof(LoanOperationKind.Waiver), new
            {
                waiver = w.Number, valueDate = v,
                lines = lineas.Select(l => new { installment = l.Cuota, concept = l.Concepto.ToString(), amount = l.Monto }),
                impairmentApplied = contraElDeterioro.Select(a => new { concept = a.Concept, amount = a.Amount }),
            }, ct);
            var aplicado = contraElDeterioro.Sum(a => a.Amount);
            return Result.Success(new ApplyWaiverResultDto(operacion.PublicId, operacion.Number, [mensaje.ToString()], aplicado,
                condonado.Where(a => a.Concept is not (nameof(LoanConcept.CurrentInterestInSuspense) or nameof(LoanConcept.DefaultInterestInSuspense)))
                    .Sum(a => a.Amount) - aplicado, decision.Avisos));
        }, ct);
    }

    /// <summary>
    /// Reparte cada línea en sus cuotas con los saldos de hoy: la de una cuota, en ella; la de «todas», de la cuota más antigua a la más nueva (lo sin
    /// cuota al final). Si el saldo de hoy no alcanza lo aprobado, <c>Lending.Waiver.ExceedsBalance</c> con <c>concept</c> y <c>balance</c>.
    /// </summary>
    public static Result<IReadOnlyList<PartidaDeNovedad>> Repartir(Waiver w, EstadoDelCredito estado)
    {
        var salida = new List<PartidaDeNovedad>();
        foreach (var l in w.Lines.OrderBy(x => (int)x.Concept).ThenBy(x => x.InstallmentNumber ?? int.MaxValue))
        {
            var fuentes = estado.Saldos.Where(s => s.Concepto == l.Concept && s.Valor > 0m && (l.InstallmentNumber is null || s.Cuota == l.InstallmentNumber))
                .OrderBy(s => s.Cuota ?? int.MaxValue).ToList();
            var disponible = fuentes.Sum(s => s.Valor) - salida.Where(p => p.Concepto == l.Concept && (l.InstallmentNumber is null || p.Cuota == l.InstallmentNumber))
                .Sum(p => p.Monto);
            if (l.Amount > disponible)
                return Result.Failure<IReadOnlyList<PartidaDeNovedad>>(ErroresDeGobierno.De(WaiverLine.Validar(l.Concept, l.Amount, disponible)!));
            var queda = l.Amount;
            foreach (var s in fuentes)
            {
                var yaTomado = salida.Where(p => p.Concepto == s.Concepto && p.Cuota == s.Cuota).Sum(p => p.Monto);
                var t = Math.Min(queda, s.Valor - yaTomado);
                if (t <= 0m) continue;
                salida.Add(new PartidaDeNovedad(s.Concepto, s.Cuota, t));
                queda -= t;
                if (queda == 0m) break;
            }
        }
        return Result.Success<IReadOnlyList<PartidaDeNovedad>>(salida
            .GroupBy(p => (p.Concepto, p.Cuota)).Select(g => new PartidaDeNovedad(g.Key.Concepto, g.Key.Cuota, g.Sum(p => p.Monto))).ToList());
    }

    /// <summary>
    /// Lo que sale contra el deterioro individual por concepto (C22; contabilidad.md §2.1, ejemplo 13): el del último cierre vigente del crédito
    /// (repartido por concepto con <see cref="DeterioroPorConcepto"/>), menos lo que ya consumieron las condonaciones aplicadas desde ese cierre, hasta lo
    /// condonado de cada concepto. Sin cierre, nada. El suspenso no tiene deterioro (está en orden).
    /// </summary>
    private async Task<IReadOnlyList<ConceptAmountV1>> DeterioroAplicableAsync(int loanId, IReadOnlyList<PartidaDeNovedad> lineas, DateOnly v, CancellationToken ct)
    {
        var cierre = await CategoriaContableDeCartera.UltimoCierreVigenteAsync(db, v.AddDays(1), ct);
        if (cierre is null) return [];
        var linea = await CategoriaContableDeCartera.LineasDe(db, cierre).AsNoTracking().FirstOrDefaultAsync(l => l.LoanId == loanId, ct);
        if (linea is null) return [];
        var saldos = Saldos(linea.BalancesByConceptJson);
        var disponible = DeterioroPorConcepto.Repartir(linea.PrincipalImpairment, linea.InterestImpairment, linea.OtherImpairment, saldos)
            .ToDictionary(x => x.Concepto, x => x.Importe);
        var anteriores = await db.LoanOperations.AsNoTracking()
            .Where(o => o.LoanId == loanId && o.Kind == LoanOperationKind.Waiver && o.ValueDate > cierre.CloseDate && o.ReversedByOperationId == null)
            .Select(o => o.Payload).ToListAsync(ct);
        foreach (var p in anteriores.Select(DecisionDeCondonacion.Leer).Where(d => d is not null).SelectMany(d => d!.ImpairmentApplied))
            if (Enum.TryParse<LoanConcept>(p.Concept, out var c)) disponible[c] = disponible.GetValueOrDefault(c) - p.Amount;

        return lineas.Where(l => l.Concepto is not (LoanConcept.CurrentInterestInSuspense or LoanConcept.DefaultInterestInSuspense))
            .GroupBy(l => l.Concepto).OrderBy(g => (int)g.Key)
            .Select(g => new ConceptAmountV1
            {
                Concept = g.Key.ToString(), Amount = Math.Max(0m, Math.Min(g.Sum(l => l.Monto), disponible.GetValueOrDefault(g.Key))),
                ReasonCode = EmisionDeCartera.MotivoCondonacion,
            })
            .Where(a => a.Amount > 0m).ToList();
    }

    private static IReadOnlyDictionary<LoanConcept, decimal> Saldos(string json)
    {
        try
        {
            var crudo = JsonSerializer.Deserialize<Dictionary<string, decimal>>(json) ?? [];
            return crudo.Where(kv => Enum.TryParse<LoanConcept>(kv.Key, out _)).ToDictionary(kv => Enum.Parse<LoanConcept>(kv.Key), kv => kv.Value);
        }
        catch (JsonException)
        {
            return new Dictionary<LoanConcept, decimal>();
        }
    }

    private static Result<ApplyWaiverResultDto> Falla(Error e) => Result.Failure<ApplyWaiverResultDto>(e);
}
