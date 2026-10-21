using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Holds;

/// <summary>
/// La reclasificación contable de una cuenta cuya condición cambia (feature 014, US11, T189; research C12, C20, E109, E159): los saldos de la cuenta pasan
/// de las dimensiones de antes a las de ahora en una operación <c>Reclassification</c> (prefijo <c>RCL-</c>, valor 0, sin filas en el libro) con su
/// <c>CaptacionReclasificada</c>. La usan el embargo y su levantamiento (la marca <c>Garnished</c>) y la exención del GMF; la inactividad de US14 la usará
/// con <c>CausaInactividad</c>/<c>CausaReactivacion</c> (cambia el rol del capital, no la marca). La condición se cambia <b>dentro</b> de
/// <paramref name="cambiar"/>: así las dimensiones anteriores se leen antes y las nuevas después, con la misma lectura de la cuenta. Sin saldo no nace
/// operación ni hecho (no hay nada que trasladar). Corre en la transacción y bajo el cerrojo de quien llama; guarda. (nuevo)
/// </summary>
public sealed class ReclasificacionDeCuenta(
    IApplicationDbContext db,
    RegistroDeCuentas registro,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    IActorActual actorActual,
    IDateTimeService reloj)
{
    public const string PrefijoReclasificacion = "RCL-";

    /// <summary>Los conceptos cuyo saldo reclasifica (contabilidad.md §3 fila 17: capital, capital inactivo e interés por pagar).</summary>
    public static readonly MemberAccountConcept[] Conceptos = [MemberAccountConcept.Principal, MemberAccountConcept.InterestPayable];

    /// <param name="exentaDespues">En la exención del GMF, si la cuenta queda marcada exenta (la marca no vive en la cuenta, vive en su vigencia).</param>
    /// <param name="exentaAntes">
    /// US14 (T208): en la exención que rige desde una fecha futura, la marca de antes (la vigencia ya guardada hace que la lectura de la fecha diga la de
    /// después).
    /// </param>
    /// <param name="saldos">
    /// US14 (T208): los saldos que se trasladan, si no son todos los de la cuenta: el ciclo diario mueve los que tenían fecha contable antes del día en que
    /// cambió la marca (lo de ese día ya se contabilizó con la marca nueva).
    /// </param>
    public async Task<Result<MemberAccountOperation?>> ReclasificarAsync(MemberAccount cuenta, DateOnly fecha, string causa, Action cambiar, int? embargoId,
        CancellationToken ct, bool? exentaDespues = null, bool? exentaAntes = null, IReadOnlyDictionary<MemberAccountConcept, decimal>? saldos = null)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        ArgumentNullException.ThrowIfNull(cambiar);
        var hecho = await emision.HechoDeCuentaAsync(cuenta, fecha, ct);
        if (hecho.IsFailure) return Result.Failure<MemberAccountOperation?>(hecho.Error);
        var anteriores = exentaAntes is { } antes ? hecho.Value.Dimensiones(antes) : hecho.Value.Dimensiones();
        cambiar();
        var despues = exentaDespues is { } e ? hecho.Value with { ExentaDeGmf = e } : hecho.Value;
        var nuevas = despues.Dimensiones();
        if (anteriores == nuevas || (anteriores.AccountFlag == nuevas.AccountFlag && anteriores.Inactive == nuevas.Inactive))
            return Result.Success<MemberAccountOperation?>(null);

        var trasladables = saldos ?? (await LectorDeCuentas.SaldosAsync(db, [cuenta.Id], ct)).GetValueOrDefault(cuenta.Id) ?? [];
        var valores = Conceptos.Select(c => new DepositConceptAmountV1 { Concept = c.ToString(), Amount = trasladables.GetValueOrDefault(c) })
            .Where(v => v.Amount != 0m).ToList();
        if (valores.Count == 0) return Result.Success<MemberAccountOperation?>(null);

        var control = await db.MemberAccountControls.FirstAsync(c => c.MemberAccountId == cuenta.Id, ct);
        var numero = await numerador.NumeroDeOperacionDeCuentaAsync(MemberAccountOperationKind.Reclassification, PrefijoReclasificacion, ct);
        var actor = await actorActual.ObtenerAsync(ct);
        var ahora = reloj.UtcNow;
        var operacion = registro.RegistrarOperacion(control, ahora, sello => new MemberAccountOperation
        {
            MemberAccountId = cuenta.Id,
            Kind = MemberAccountOperationKind.Reclassification,
            Number = numero,
            Sequence = sello.Sequence,
            ValueDate = fecha,
            AccountingDate = hecho.Value.FechaContable,
            RegisteredAt = sello.RegisteredAt,
            Amount = 0m,
            ActorKind = actor.Kind,
            ActorUserId = actor.UserId,
            OriginUserCentralId = actor.CentralUserId,
            GarnishmentId = embargoId,
            Reason = $"Reclasificación por {causa}",
            Payload = anteriores.Inactive == nuevas.Inactive
                ? $"{{\"cause\":\"{causa}\",\"from\":\"{anteriores.AccountFlag}\",\"to\":\"{nuevas.AccountFlag}\"}}"
                : $"{{\"cause\":\"{causa}\",\"from\":\"{anteriores.AccountFlag}\",\"to\":\"{nuevas.AccountFlag}\",\"inactive\":{(nuevas.Inactive ? "true" : "false")}}}",
            InputsSha256 = new HuellaDeCartera().Identificador("account", cuenta.PublicId).Texto("cause", causa).Texto("from", anteriores.AccountFlag)
                .Texto("to", nuevas.AccountFlag).Calcular(),
            RulesVersion = VersionesDeReglas.Vigente,
        });
        await db.SaveChangesAsync(ct);
        var unidad = await emision.CaptacionReclasificadaAsync(new ReclasificacionAEmitir(despues, operacion, anteriores, valores, causa), ct);
        if (unidad.IsFailure) return Result.Failure<MemberAccountOperation?>(unidad.Error);
        await db.SaveChangesAsync(ct);
        return Result.Success<MemberAccountOperation?>(operacion);
    }
}
