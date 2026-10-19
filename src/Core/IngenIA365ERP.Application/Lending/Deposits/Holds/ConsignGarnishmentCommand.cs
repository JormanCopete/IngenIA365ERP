using System.Globalization;
using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Restrictions;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Holds;

/// <summary>Lo que dejó una consignación al juzgado: una operación y una orden de giro por cuenta, y el embargo después. (nuevo)</summary>
public sealed record GarnishmentConsignmentDto(decimal Amount, IReadOnlyList<GarnishmentConsignmentLineDto> Lines, GarnishmentDto Garnishment, IReadOnlyList<string> Warnings);

/// <summary>Una cuenta de la consignación: su operación <c>EMB-</c> y su orden de giro. (nuevo)</summary>
public sealed record GarnishmentConsignmentLineDto(Guid AccountPublicId, string AccountNumber, Guid OperationPublicId, string OperationNumber,
    Guid OrderPublicId, string OrderNumber, decimal Amount);

/// <summary>
/// La consignación al juzgado (api.md §8, <c>POST /{id}/consign</c>, <c>Lending.Garnishments.Register</c>, con clave; FR-055, FR-091): lo retenido sale de
/// las cuentas en el orden del embargo —cada una en su operación <c>GarnishmentConsignment</c> (prefijo <c>EMB-</c>, filas <c>GarnishmentConsigned</c>) con su
/// orden de giro a la cuenta de depósitos judiciales (<c>AccountPayoutOrder</c> <c>GarnishmentConsignment</c>, beneficiario el juzgado) y su
/// <c>EmbargoConsignado</c> + <c>OrdenDeGiroEmitida</c> v2—; un CDAT sólo cuando venció. No supera lo retenido (<c>Lending.Garnishment.ExceedsRetained</c>).
/// Pasa por la validación previa de la pantalla (D23) con el hecho de la primera cuenta. Después recalcula: lo consignado baja el tope y el saldo. Si Tesorería
/// rechaza el giro, el saldo vuelve y queda retenido otra vez (<see cref="RetencionDeEmbargos.Reintegrar"/>). El GMF de la consignación no se cobra: no es
/// un hecho generador del parámetro todavía (research E163, pendiente de la contadora). (nuevo)
/// </summary>
public sealed record ConsignGarnishmentCommand(Guid GarnishmentPublicId, decimal Amount, Guid? BankPublicId, string? AccountNumber, byte? AccountType,
    string Reason) : IRequest<Result<GarnishmentConsignmentDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ConsignGarnishmentCommandValidator : ValidadorConMotivo<ConsignGarnishmentCommand>
{
    public ConsignGarnishmentCommandValidator()
    {
        RuleFor(x => x.GarnishmentPublicId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0m).WithMessage("El valor a consignar es mayor que cero.");
        RuleFor(x => x.AccountNumber).MaximumLength(Garnishment.LargoDeLaCuentaBancaria);
    }
}

public sealed class ConsignGarnishmentCommandHandler(
    IApplicationDbContext db,
    ICerrojoDeCartera cerrojo,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    RetencionDeEmbargos retencion,
    RegistroDeCuentas registro,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    ValidacionPreviaDeCaptaciones validacionPrevia,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<ConsignGarnishmentCommand, Result<GarnishmentConsignmentDto>>
{
    public const string PrefijoConsignacion = "EMB-";

    public async Task<Result<GarnishmentConsignmentDto>> Handle(ConsignGarnishmentCommand request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var leido = await AlcanceDeEmbargos.EmbargoEnAlcanceAsync(db, alcance, auditoria, request.GarnishmentPublicId, seguir: false, ct);
        if (leido.IsFailure) return Falla(leido.Error);
        var vista = leido.Value;
        if (vista.Status != GarnishmentStatus.Active) return Falla(ErroresDeEmbargos.NoActivo(vista.OrderNumber, vista.Status.ToString()));
        int? banco = vista.DestinationBankId;
        string? transferencia = null;
        if (request.BankPublicId is { } b)
        {
            var fila = await db.Banks.AsNoTracking().Where(x => x.PublicId == b && !x.IsDeleted).Select(x => new { x.Id, x.TransferCode }).FirstOrDefaultAsync(ct);
            if (fila is null) return Falla(new ErrorConDatos("Generic.NotFound", "El banco de la cuenta de depósitos judiciales no existe.", new { field = "bankPublicId" }));
            (banco, transferencia) = (fila.Id, fila.TransferCode);
        }
        else if (banco is { } id) transferencia = await db.Banks.AsNoTracking().Where(x => x.Id == id).Select(x => x.TransferCode).FirstOrDefaultAsync(ct);
        var numeroBancario = RegisterGarnishmentCommandHandler.Texto(request.AccountNumber) ?? vista.DestinationBankAccountNumber;

        // D23: la pantalla bloquea si la consignación no se contabilizaría (con las dimensiones vigentes de la primera cuenta retenida).
        var primera = await db.AccountHolds.AsNoTracking().Where(h => h.GarnishmentId == vista.Id && h.ReleasedAt == null && !h.IsDeleted)
            .OrderBy(h => h.MemberAccountId).Select(h => (int?)h.MemberAccountId).FirstOrDefaultAsync(ct);
        var validacion = PrevalidationOutcome.NotApplicable;
        var avisos = new List<string>();
        if (primera is { } cuentaId && (await validacionPrevia.DimensionesAsync([cuentaId], hoy, ct)).TryGetValue(cuentaId, out var d))
        {
            var juzgadoPublico = await db.People.AsNoTracking().Where(p => p.Id == vista.CourtPersonId).Select(p => p.PublicId).FirstAsync(ct);
            var decision = await validacionPrevia.EnPantallaAsync(HechosRepresentativosDeCaptaciones.Unidad(hoy, d,
                HechosRepresentativosDeCaptaciones.EmbargoConsignado(d, juzgadoPublico)), $"la consignación del embargo {vista.OrderNumber}", ct);
            if (decision.Bloqueo is { } bloqueo) return Falla(bloqueo);
            validacion = decision.Outcome;
            avisos.AddRange(decision.Avisos);
        }

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([vista.PersonId], ModoDeEspera.Esperar, ct);
            var ids = (await retencion.CuentasDeLaPersonaAsync(vista.PersonId, seguir: false, ct)).Select(c => c.Id).ToList();
            if (ids.Count > 0) await cerrojo.BloquearCuentasAsync(ids, ModoDeEspera.Esperar, ct);
            var g = await db.Garnishments.FirstAsync(x => x.Id == vista.Id, ct);
            var antes = await retencion.RecalcularAsync(g.PersonId, hoy, null, $"Consignación del embargo {g.OrderNumber}", ct);
            if (antes.IsFailure) return Falla(antes.Error);
            var calculo = antes.Value.First(x => x.Embargo.Id == g.Id).Calculo;
            var cuentas = await db.MemberAccounts.Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
            var consignables = calculo.Cuentas.Where(c => c.Retenido > 0m && cuentas.TryGetValue(c.CuentaId, out var a)
                                                          && (a.DepositClass != DepositClass.TermDeposit || a.Status == MemberAccountStatus.Matured))
                .OrderBy(c => c.Orden).ToList();
            var disponible = consignables.Sum(c => c.Retenido);
            if (request.Amount > disponible) return Falla(ErroresDeEmbargos.ExcedeRetenido(disponible, request.Amount));

            var juzgado = await db.People.AsNoTracking().IgnoreQueryFilters().FirstAsync(p => p.Id == g.CourtPersonId, ct);
            var actor = await actorActual.ObtenerAsync(ct);
            var lineas = new List<GarnishmentConsignmentLineDto>();
            var restante = request.Amount;
            foreach (var c in consignables)
            {
                if (restante <= 0m) break;
                var monto = Math.Min(restante, c.Retenido);
                restante -= monto;
                var cuenta = cuentas[c.CuentaId];
                var saldos = (await LectorDeCuentas.SaldosAsync(db, [cuenta.Id], ct)).GetValueOrDefault(cuenta.Id) ?? [];
                var deCapital = Math.Min(monto, Math.Max(0m, saldos.GetValueOrDefault(MemberAccountConcept.Principal)));
                var deInteres = monto - deCapital;
                var hecho = await emision.HechoDeCuentaAsync(cuenta, hoy, ct);
                if (hecho.IsFailure) return Falla(hecho.Error);
                var control = await db.MemberAccountControls.FirstAsync(x => x.MemberAccountId == cuenta.Id, ct);
                var numero = await numerador.NumeroDeOperacionDeCuentaAsync(MemberAccountOperationKind.GarnishmentConsignment, PrefijoConsignacion, ct);
                var ahora = reloj.UtcNow;
                var operacion = registro.RegistrarOperacion(control, ahora, sello => new MemberAccountOperation
                {
                    MemberAccountId = cuenta.Id,
                    Kind = MemberAccountOperationKind.GarnishmentConsignment,
                    Number = numero,
                    Sequence = sello.Sequence,
                    ValueDate = hoy,
                    AccountingDate = hecho.Value.FechaContable,
                    RegisteredAt = sello.RegisteredAt,
                    Amount = monto,
                    ActorKind = actor.Kind,
                    ActorUserId = actor.UserId,
                    OriginUserCentralId = actor.CentralUserId,
                    GarnishmentId = g.Id,
                    Reason = Recortar($"{request.Reason.Trim()} (embargo {g.OrderNumber})"),
                    Payload = JsonSerializer.Serialize(new { garnishment = g.PublicId, office = g.OrderNumber, amount = monto, principal = deCapital, interest = deInteres }),
                    InputsSha256 = new HuellaDeCartera().Identificador("garnishment", g.PublicId).Identificador("account", cuenta.PublicId)
                        .Decimal("amount", monto, 2).Calcular(),
                    RulesVersion = VersionesDeReglas.Vigente,
                    PrevalidationOutcome = validacion.ToString(),
                });
                await db.SaveChangesAsync(ct);

                var orden = AccountPayoutOrder.Emitir(cuenta.Id, operacion.Id, PayoutPurpose.GarnishmentConsignment, juzgado.Id, juzgado.IdType ?? string.Empty,
                    juzgado.TaxId ?? string.Empty, ReferenciasDeGobierno.Nombre(juzgado), banco, request.AccountType, numeroBancario, monto,
                    hecho.Value.FechaContable.AddDays(1), ahora, actor.UserId);
                await numerador.NumerarOrdenDeCuentaAsync(orden, Disbursements.TramoDeDesembolso.PrefijoDeOrden, ct);
                db.AccountPayoutOrders.Add(orden);
                await db.SaveChangesAsync(ct);

                var valores = new List<DepositConceptAmountV1>();
                if (deCapital > 0m) valores.Add(new() { Concept = nameof(MemberAccountConcept.Principal), Amount = -deCapital, ReasonCode = "GarnishmentConsignment" });
                if (deInteres > 0m) valores.Add(new() { Concept = nameof(MemberAccountConcept.InterestPayable), Amount = -deInteres, ReasonCode = "GarnishmentConsignment" });
                var payee = new PayeeV1
                {
                    PersonPublicId = juzgado.PublicId, TaxIdType = juzgado.IdType ?? string.Empty, TaxId = juzgado.TaxId ?? string.Empty,
                    Name = ReferenciasDeGobierno.Nombre(juzgado), BankCode = transferencia, BankAccountNumber = numeroBancario,
                    BankAccountType = request.AccountType?.ToString(CultureInfo.InvariantCulture),
                };
                var unidad = await emision.EmbargoConsignadoAsync(new ConsignacionDeEmbargoAEmitir(hecho.Value, operacion, valores, g, juzgado.PublicId, orden, payee,
                    validacion), ct);
                if (unidad.IsFailure) return Falla(unidad.Error);
                var mensaje = unidad.Value.De(EmbargoConsignadoV1.Type);
                var filas = new List<MemberAccountTransaction>();
                if (deCapital > 0m) filas.Add(Fila(cuenta, operacion, MemberAccountConcept.Principal, -deCapital, hecho.Value.FechaContable, mensaje));
                if (deInteres > 0m) filas.Add(Fila(cuenta, operacion, MemberAccountConcept.InterestPayable, -deInteres, hecho.Value.FechaContable, mensaje));
                registro.RegistrarTransacciones(filas);
                await db.SaveChangesAsync(ct);
                lineas.Add(new GarnishmentConsignmentLineDto(cuenta.PublicId, cuenta.Number, operacion.PublicId, operacion.Number, orden.PublicId, orden.Number, monto));
            }

            var despues = await retencion.RecalcularAsync(g.PersonId, hoy, actor.UserId, $"Consignación del embargo {g.OrderNumber}", ct);
            if (despues.IsFailure) return Falla(despues.Error);
            var consignado = (await retencion.ConsignadoAsync([g.Id], ct)).GetValueOrDefault(g.Id);
            var dto = await VistaDeEmbargos.DtoAsync(db, g, despues.Value.FirstOrDefault(x => x.Embargo.Id == g.Id)?.Calculo, consignado, hoy, [], ct);
            return Result.Success(new GarnishmentConsignmentDto(request.Amount, lineas, dto, avisos));
        }, ct);
    }

    private static MemberAccountTransaction Fila(MemberAccount cuenta, MemberAccountOperation operacion, MemberAccountConcept concepto, decimal monto,
        DateOnly contable, Guid? mensaje) => new()
    {
        MemberAccountId = cuenta.Id,
        Operation = operacion,
        OperationId = operacion.Id,
        Kind = MemberAccountTransactionKind.GarnishmentConsigned,
        Concept = concepto,
        Amount = monto,
        ValueDate = operacion.ValueDate,
        AccountingDate = contable,
        RegisteredAt = operacion.RegisteredAt,
        MessagePublicId = mensaje,
    };

    private static string Recortar(string texto) => texto.Length <= 500 ? texto : texto[..500];

    private static Result<GarnishmentConsignmentDto> Falla(Error e) => Result.Failure<GarnishmentConsignmentDto>(e);
}
