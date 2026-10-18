using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Contributions;

/// <summary>Lo que dejó un reverso: el comprobante y si ya estaba reversada. (nuevo)</summary>
public sealed record ReversoDeCuentaAplicado(ComprobanteDeReversoDeCuentaDto Comprobante, bool YaReversada);

/// <summary>
/// El reverso de una operación de cuenta (feature 014, US3, T083, T085, T089; contrato-de-cuentas §3.11; FR-002, FR-092): la operación de reverso con la
/// clave derivada <c>{clave}:reverso</c>, los espejos de todas las filas de la original —en todas sus cuentas— por <see cref="RegistroDeCuentas"/>, con la
/// fecha valor de la original y la fecha contable de hoy, la marca en la original y el hecho <c>OperacionDeCuentaReversada</c> (que hereda el modo del
/// original). Un reverso no se reversa (<c>Lending.Savings.ReversalOfReversal</c>); una ya reversada responde el reverso existente. Si el reverso deja
/// una cuenta en negativo (la consignación ya se usó) la cuenta queda con sobregiro técnico (FR-056). Guarda dos veces dentro de la transacción de
/// quien llama: la marca de la original necesita el Id del reverso. (nuevo)
/// </summary>
public sealed class ReversionDeCuentas(
    IApplicationDbContext db,
    RegistroDeCuentas registro,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    IActorActual actorActual,
    IDateTimeService reloj,
    Documents.AnulacionDeDocumentosDeCuentas? documentos = null)
{
    /// <summary>El prefijo del comprobante del reverso de una operación de cuenta.</summary>
    public const string PrefijoReverso = "RVC-";

    /// <summary>La clave derivada del reverso (contrato-de-cuentas §3.12).</summary>
    public static string ClaveDeReverso(string clave) => $"{clave}:reverso";

    /// <summary>La operación de un recaudador por su clave, con seguimiento (para reversarla).</summary>
    public Task<MemberAccountOperation?> OperacionAsync(string recaudador, string clave, CancellationToken ct) =>
        db.MemberAccountOperations.FirstOrDefaultAsync(o => o.CollectorCode == recaudador && o.IdempotencyKey == clave, ct);

    public async Task<Result<ReversoDeCuentaAplicado>> ReversarAsync(MemberAccountOperation original, string motivo, string iniciadoPor,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(original);
        if (original.Kind == MemberAccountOperationKind.Reversal || original.ReversesOperationId is not null)
            return Result.Failure<ReversoDeCuentaAplicado>(ErroresDelLibroDeCuentas.ReversoDeReverso(original.Id));
        if (original.ReversedByOperationId is { } yaId)
        {
            var existente = await db.MemberAccountOperations.AsNoTracking().FirstAsync(o => o.Id == yaId, ct);
            return Result.Success(new ReversoDeCuentaAplicado(await ComprobanteAsync(existente, original, false, [], ct), true));
        }

        var cuenta = await db.MemberAccounts.FirstAsync(a => a.Id == original.MemberAccountId, ct);
        var filas = await db.MemberAccountTransactions.AsNoTracking().Where(t => t.OperationId == original.Id && !t.IsDeleted).OrderBy(t => t.Id)
            .ToListAsync(ct);
        var hecho = await emision.HechoDeCuentaAsync(cuenta, reloj.HoyLocal, ct);
        if (hecho.IsFailure) return Result.Failure<ReversoDeCuentaAplicado>(hecho.Error);
        var control = await db.MemberAccountControls.FirstAsync(c => c.MemberAccountId == cuenta.Id, ct);
        var numero = await numerador.NumeroDeOperacionDeCuentaAsync(MemberAccountOperationKind.Reversal, PrefijoReverso, ct);
        var actor = await actorActual.ObtenerAsync(ct);
        var ahora = reloj.UtcNow;
        var texto = string.IsNullOrWhiteSpace(motivo) ? "Reverso por el recaudador" : motivo.Trim();
        var reverso = registro.RegistrarOperacion(control, ahora, sello => new MemberAccountOperation
        {
            MemberAccountId = cuenta.Id,
            CounterpartMemberAccountId = original.CounterpartMemberAccountId,
            Kind = MemberAccountOperationKind.Reversal,
            Number = numero,
            Sequence = sello.Sequence,
            ValueDate = original.ValueDate,
            AccountingDate = hecho.Value.FechaContable,
            RegisteredAt = sello.RegisteredAt,
            Amount = original.Amount,
            ActorKind = actor.Kind,
            ActorUserId = actor.UserId,
            OriginUserCentralId = actor.CentralUserId,
            CollectorClass = original.CollectorClass,
            CollectorCode = original.CollectorCode,
            IdempotencyKey = original.IdempotencyKey is { } clave ? ClaveDeReverso(clave) : null,
            ContractVersion = original.ContractVersion,
            PaymentMeansClass = original.PaymentMeansClass,
            ReversesOperationId = original.Id,
            Reason = texto.Length <= 500 ? texto : texto[..500],
            Payload = original.Payload,
            InputsSha256 = original.InputsSha256,
            RulesVersion = original.RulesVersion,
            PrevalidationOutcome = original.PrevalidationOutcome,
        });

        var tipo = TipoDelHecho(original, cuenta.Nature);
        var valores = EmisionDeCartera.ValoresDeCuenta(filas.Where(f => f.MemberAccountId == cuenta.Id), f => -f.Amount);
        var usuario = actor.UserId is null ? null : new UserRefV1 { CentralUserId = actor.CentralUserId, Name = actor.Name };
        var unidad = await emision.OperacionDeCuentaReversadaAsync(new ReversoDeCuentaAEmitir(hecho.Value, reverso, original, tipo,
            ClaseDelOrigen(original), valores, iniciadoPor, texto, usuario), ct);
        var espejos = registro.Reversar(filas, reverso, hecho.Value.FechaContable, unidad.Mensajes.FirstOrDefault()?.PublicId);
        if (espejos.IsFailure) return Result.Failure<ReversoDeCuentaAplicado>(espejos.Error);
        await db.SaveChangesAsync(ct);
        RegistroDeCuentas.MarcarReversada(original, reverso);
        // T087 (documentos.md §4): los documentos que vieron la operación y que el reverso deja sin validez, en la misma unidad de trabajo.
        var anulados = documentos is null ? [] : await documentos.AnularAsync(original, reverso, ct);

        // FR-056: si el dinero ya se usó, el reverso deja la cuenta en negativo técnico (bloquea retiros y avisa).
        var saldos = (await LectorDeCuentas.SaldosAsync(db, [cuenta.Id], ct)).GetValueOrDefault(cuenta.Id) ?? [];
        var negativo = LectorDeCuentas.SaldoDelTitular(cuenta.Nature, saldos) < 0m;
        if (negativo && cuenta.TechnicalNegativeSince is null) cuenta.TechnicalNegativeSince = reloj.HoyLocal;
        await db.SaveChangesAsync(ct);
        return Result.Success(new ReversoDeCuentaAplicado(await ComprobanteAsync(reverso, original, negativo, anulados, ct), false));
    }

    /// <summary>El tipo del hecho principal de la operación original (el que el reverso espeja).</summary>
    public static string TipoDelHecho(MemberAccountOperation original, LineNature naturaleza) => original.Kind switch
    {
        MemberAccountOperationKind.Deposit => naturaleza == LineNature.Contribution ? AporteRecibidoV1.Type : ConsignacionEnCuentaAplicadaV1.Type,
        MemberAccountOperationKind.Transfer => TrasladoEntreCuentasAplicadoV1.Type,
        MemberAccountOperationKind.ContributionRefund => DevolucionOrdenadaV1.Type,
        MemberAccountOperationKind.Withdrawal => RetiroDeCuentaAplicadoV1.Type,
        _ => original.Kind.ToString(),
    };

    /// <summary>La clase del documento de origen de la operación original (el <c>related</c> del reverso).</summary>
    public static string ClaseDelOrigen(MemberAccountOperation original) => original.Kind switch
    {
        MemberAccountOperationKind.Deposit => EmisionDeCartera.ClaseConsignacionDeCuenta,
        MemberAccountOperationKind.Transfer => EmisionDeCartera.ClaseTrasladoDeCuenta,
        MemberAccountOperationKind.ContributionRefund => EmisionDeCartera.ClaseDevolucionDeAportes,
        _ => original.Kind.ToString(),
    };

    private async Task<ComprobanteDeReversoDeCuentaDto> ComprobanteAsync(MemberAccountOperation reverso, MemberAccountOperation original, bool negativo,
        IReadOnlyList<Guid> anulados, CancellationToken ct)
    {
        var filas = await db.MemberAccountTransactions.AsNoTracking().Where(t => t.OperationId == reverso.Id).OrderBy(t => t.Id).ToListAsync(ct);
        var cuentas = filas.Select(f => f.MemberAccountId).Append(reverso.MemberAccountId).Distinct().ToList();
        var publicos = await db.MemberAccounts.AsNoTracking().Where(a => cuentas.Contains(a.Id)).Select(a => a.PublicId).ToListAsync(ct);
        return new ComprobanteDeReversoDeCuentaDto(reverso.PublicId, reverso.Number, original.PublicId, original.Number, publicos, reverso.AccountingDate,
            reverso.RegisteredAt, original.Amount, filas.Select(f => new LineaDeCuentaDto((ConceptoDeCuenta)(int)f.Concept, f.Kind.ToString(), f.Amount)).ToList(),
            negativo, anulados);
    }
}
