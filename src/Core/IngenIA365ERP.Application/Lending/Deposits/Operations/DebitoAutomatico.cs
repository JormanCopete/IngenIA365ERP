using System.Globalization;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accrual;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Application.Lending.Deposits.Operations;

/// <summary>Lo que hizo el débito automático de una autorización y un crédito un día: el monto debitado (cero si no debitó) y por qué. (nuevo)</summary>
public sealed record DebitoAutomaticoHecho(Guid MandatoPublicId, Guid CreditoPublicId, decimal Cuota, decimal Debitado, string? Motivo);

/// <summary>
/// El subpaso «débito automático» del paso de captaciones (feature 014, US4, T119; research R16, D25; data-model §3.6; FR-090): en cada tanda, las cuentas
/// con una autorización vigente el día d. Por cada crédito que la autorización cubre (el suyo o todos los del titular como deudor), si el crédito tiene una
/// cuota exigible ese día (la consulta de obligaciones, la misma que usa Nómina), en <b>una</b> transacción con los cerrojos crédito → cuenta (R08):
/// <list type="number">
/// <item>aplica el pago al crédito por el contrato de obligaciones con el recaudador de la Cartera (<c>CAR</c>, <c>OwnLoanPayoff</c>), medio
/// <c>InternalOffset</c> y la clave <c>CAR:debito-automatico:{crédito:N}:{aaaammdd}</c> —un débito repetido no debita dos veces—;</item>
/// <item>debita la cuenta con una operación <c>DirectDebit</c> que nombra el pago (<c>RelatedLoanOperationId</c>), con su GMF y su
/// <c>RetiroDeCuentaAplicado</c> (<c>internal</c>).</item>
/// </list>
/// Sin disponible no debita, salvo que la autorización admita el débito parcial (entonces debita lo que alcanza con el GMF). El reverso del pago arrastra el
/// del débito (<see cref="Withdrawals.ArrastreDelCruceAlRetiro"/>). Lo hace el proceso: el <c>PagoAplicado</c> del débito automático admite proceso (mensajes.md
/// §1.4). Una autorización que falla queda en <c>LND_ProcessRunErrors</c> y las demás siguen. (nuevo)
/// </summary>
public sealed class DebitoAutomatico(
    IApplicationDbContext db,
    IContratoDeObligaciones contrato,
    IConsultaDeObligaciones obligaciones,
    RetiroDeCuenta retiros,
    ReglasDeMovimientos movimientos,
    ICerrojoDeCartera cerrojo,
    ILogger<DebitoAutomatico>? logger = null,
    ValidacionPreviaDeCaptaciones? validacionPrevia = null) : ISubpasoDeCaptaciones
{
    /// <summary>El prefijo del comprobante del débito automático.</summary>
    public const string PrefijoDebito = "DBA-";

    private readonly ILogger _log = logger ?? (ILogger)NullLogger.Instance;

    public string Nombre => "DebitoAutomatico";

    /// <summary>La clave del débito automático de un crédito un día (contrato-de-cuentas §3.12, R16).</summary>
    public static string Clave(Guid credito, DateOnly dia) =>
        string.Create(CultureInfo.InvariantCulture, $"{CatalogoDeMensajesDeCarteraV1.PrefijoDelDebitoAutomatico}{credito:N}:{dia:yyyyMMdd}");

    public async Task<IReadOnlyList<FalloDeCuenta>> ProcesarAsync(LendingProcessRun corrida, DateOnly d, IReadOnlyList<int> cuentas, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(corrida);
        if (cuentas.Count == 0) return [];
        var ids = cuentas.ToArray();
        var mandatos = await db.DirectDebitMandates.AsNoTracking()
            .Where(m => ids.Contains(m.MemberAccountId) && !m.IsDeleted && m.RevokedAt == null && m.ValidFrom <= d && (m.ValidTo == null || m.ValidTo >= d))
            .OrderBy(m => m.MemberAccountId).ThenBy(m => m.Id).ToListAsync(ct);
        var fallos = new List<FalloDeCuenta>();
        var pregunta = validacionPrevia is null || mandatos.Count == 0
            ? null
            : await validacionPrevia.TandaAsync(mandatos.Select(m => m.MemberAccountId).Distinct().ToList(), d, $"el débito automático del {d:yyyy-MM-dd}", ct);
        foreach (var mandato in mandatos)
        {
            try
            {
                await DebitarAsync(mandato, d, ct, pregunta);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                db.DescartarCambiosSalvo(corrida);
                _log.LogWarning(ex, "[Cartera.DebitoAutomaticoFallido] La autorización {Mandato} no debitó el {Dia}.", mandato.PublicId, d);
                fallos.Add(new FalloDeCuenta(mandato.MemberAccountId, ex is ReglaDeCarteraException r ? r.Codigo : "Lending.DirectDebit.Failed", ex.Message));
            }
        }
        return fallos;
    }

    /// <summary>Los débitos de una autorización el día <paramref name="d"/>, cada crédito en su propia transacción.</summary>
    public async Task<IReadOnlyList<DebitoAutomaticoHecho>> DebitarAsync(DirectDebitMandate mandato, DateOnly d, CancellationToken ct,
        PreguntaDeTanda? pregunta = null)
    {
        var cuenta = await db.MemberAccounts.AsNoTracking().FirstAsync(a => a.Id == mandato.MemberAccountId, ct);
        if (cuenta.Status is not (MemberAccountStatus.Active or MemberAccountStatus.Inactive)) return [];
        var titular = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == cuenta.HolderPersonId).Select(p => p.PublicId).FirstAsync(ct);
        var cuotas = await obligaciones.CuotasDeNominaAsync(new ConsultaDeCuotasDeNomina([titular], d, d), ct);
        if (cuotas.LendingStatus != EstadoDeCartera.Active) return [];
        var creditos = mandato.Scope == DirectDebitScope.SingleLoan && mandato.LoanId is { } unico
            ? await db.Loans.AsNoTracking().Where(l => l.Id == unico).Select(l => new { l.Id, l.PublicId }).ToListAsync(ct)
            : await db.Loans.AsNoTracking().Where(l => l.DebtorPersonId == cuenta.HolderPersonId).Select(l => new { l.Id, l.PublicId }).ToListAsync(ct);
        var hechos = new List<DebitoAutomaticoHecho>();
        // US8 (T142): el débito de la cuenta (con su GMF) contra el puente OwnLoanPayoff tiene que contabilizarse; el pago del crédito lo valida el contrato.
        if (pregunta is not null && cuotas.Installments.Any(c => c.DueDate == d)
            && await pregunta.CuentaAsync(cuenta.Id, x => [HechosRepresentativosDeCaptaciones.Retiro(x, RecogidaEnElDesembolso.Recaudador.Class.ToString()),
                HechosRepresentativosDeCaptaciones.Gmf(x)], ct) is { } bloqueada)
            throw new ReglaDeCarteraException(new ProblemaDeCartera(bloqueada.Codigo, bloqueada.Mensaje));
        foreach (var cuota in cuotas.Installments.Where(c => c.DueDate == d).OrderBy(c => c.Order))
        {
            var credito = creditos.FirstOrDefault(c => c.PublicId == cuota.ObligationPublicId);
            if (credito is null || cuota.Amount <= 0m) continue;
            hechos.Add(await DebitarCreditoAsync(mandato, cuenta, credito.Id, credito.PublicId, titular, cuota.Amount, d, ct));
        }
        return hechos;
    }

    private async Task<DebitoAutomaticoHecho> DebitarCreditoAsync(DirectDebitMandate mandato, MemberAccount cuenta, int creditoId, Guid credito, Guid titular,
        decimal cuota, DateOnly d, CancellationToken ct) =>
        await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var clave = Clave(credito, d);
            if (await db.MemberAccountOperations.AsNoTracking().AnyAsync(o => o.CollectorCode == RecogidaEnElDesembolso.Recaudador.Code && o.IdempotencyKey == clave, ct))
                return new DebitoAutomaticoHecho(mandato.PublicId, credito, cuota, 0m, "Ya debitado.");

            // R08: setup → titular → crédito → cuenta; el proceso salta lo ocupado y lo deja para la pasada siguiente.
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearDeudoresAsync([cuenta.HolderPersonId], ModoDeEspera.Esperar, ct);
            if ((await cerrojo.BloquearCreditosAsync([creditoId], ModoDeEspera.Saltar, ct)).Count == 0)
                return new DebitoAutomaticoHecho(mandato.PublicId, credito, cuota, 0m, "Crédito ocupado: se intenta en la pasada siguiente.");
            if ((await cerrojo.BloquearCuentasAsync([cuenta.Id], ModoDeEspera.Saltar, ct)).Count == 0)
                return new DebitoAutomaticoHecho(mandato.PublicId, credito, cuota, 0m, "Cuenta ocupada: se intenta en la pasada siguiente.");

            var monto = await MontoAsync(cuenta, cuota, mandato.AllowsPartialDebit, d, ct);
            if (monto <= 0m) return new DebitoAutomaticoHecho(mandato.PublicId, credito, cuota, 0m, "Sin disponible suficiente.");

            var recibo = new ReferenciaDeRecibo(clave.Length <= 40 ? clave : clave[^40..], d, null, null, nameof(DirectDebitMandate));
            var respuesta = await contrato.AplicarAsync(new SolicitudDeAplicacion(RecogidaEnElDesembolso.Recaudador, FormaDeAplicacion.NonInteractive,
                [new PagoAAplicar(clave, credito, ModalidadDePago.FreeAmount, monto, d, ClaseDeMedioDeRecaudo.InternalOffset, titular, recibo)], []), ct);
            var pago = respuesta.Payments.FirstOrDefault();
            if (pago is not { Result: ResultadoDelContrato.Applied or ResultadoDelContrato.AlreadyApplied, Value: { } aplicado })
                throw new InvalidOperationException($"El pago del débito automático no se aplicó: {pago?.Code} {pago?.Message}");
            var operacionDelCredito = await db.LoanOperations.AsNoTracking().Where(o => o.PublicId == aplicado.OperationPublicId).Select(o => o.Id).FirstAsync(ct);
            var numero = await db.Loans.AsNoTracking().Where(l => l.Id == creditoId).Select(l => l.Number).FirstAsync(ct);

            var r = await retiros.AplicarAsync(new RetiroPedido(RecogidaEnElDesembolso.Recaudador, clave, clave, cuenta.PublicId, monto, d,
                ClaseDeMedioDeRecaudo.InternalOffset, [], titular, recibo, null, PrevalidationOutcome.NotApplicable, [],
                $"Débito automático para la cuota del crédito {numero}")
            {
                Clase = MemberAccountOperationKind.DirectDebit,
                ExigeFirmas = false,
                ExigeAtribucion = false,
                OperacionDelCredito = operacionDelCredito,
                Interno = new InternalWithdrawalV1
                {
                    Kind = nameof(MemberAccountOperationKind.DirectDebit),
                    Loan = new LoanRefV1 { LoanPublicId = credito, LoanNumber = numero },
                    PaymentOperationPublicId = aplicado.OperationPublicId,
                },
                Prefijo = PrefijoDebito,
            }, ct);
            if (r.IsFailure) throw new InvalidOperationException($"{r.Error.Code}: {r.Error.Message}");
            return new DebitoAutomaticoHecho(mandato.PublicId, credito, cuota, monto, null);
        }, ct);

    /// <summary>
    /// Lo que se debita (FR-090): la cuota si el disponible alcanza con su GMF; si no, con débito parcial, lo que alcanza (descontando el GMF de lo que se
    /// debita); sin débito parcial, nada.
    /// </summary>
    private async Task<decimal> MontoAsync(MemberAccount cuenta, decimal cuota, bool parcial, DateOnly d, CancellationToken ct)
    {
        var leida = await movimientos.LeerAsync(cuenta, d, ct);
        var disponible = leida.Disponible.Disponible;
        var monto = Math.Min(cuota, disponible);
        for (var intento = 0; intento < 3 && monto > 0m; intento++)
        {
            var gmf = await movimientos.GmfAsync(leida, HechosGeneradoresDeGmf.DebitoAutomatico, monto, d, ct);
            var total = monto + (gmf.IsSuccess ? gmf.Value?.Valor ?? 0m : 0m);
            if (total <= disponible) break;
            monto = Math.Floor(monto - (total - disponible));
        }
        if (monto <= 0m) return 0m;
        return monto < cuota && !parcial ? 0m : monto;
    }
}
