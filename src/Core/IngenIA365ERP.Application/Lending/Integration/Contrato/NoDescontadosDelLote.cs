using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration.Contrato;

/// <summary>
/// Los <b>no descontados</b> del lote de la nómina propia (feature 013, US5, T0548; FR-112, T92, C32, C56; interfaces §3.7; data-model §24.9), en el
/// mismo <c>AplicarAsync</c> y la misma transacción: una fila de <c>LND_InstallmentNonDeductions</c> con <c>Origin = Contract</c>, la cuota
/// (<c>RepaymentInstallmentId</c>, la del período: la más antigua con saldo según la reconstrucción si Nómina no la nombra), el recaudador, el
/// motivo (<c>MotivoDeNoDescuento</c> ↔ <c>NonDeductionReason</c>, mismos números) y la clave en <c>ContractOperationKey</c>; repetir la clave
/// devuelve lo ya registrado (<c>AlreadyApplied</c>). La cuota <b>sigue exigible</b>: no mueve el libro ni emite hecho contable. Alerta
/// <c>Cartera.CuotaNoDescontadaPorNomina</c> (deduplicada por crédito y cuota) a quien lee créditos en la oficina del crédito.
/// <c>LND_InstallmentNonDeductions</c> no tiene columna para el ítem de Nómina que lo informó (<c>SourceItemPublicId</c>): va en el hecho de
/// auditoría. (nuevo)
/// </summary>
public sealed class NoDescontadosDelLote(IApplicationDbContext db, CargadorDeReconstruccion cargador, IAlertas alertas, IDateTimeService reloj)
{
    /// <summary>Los destinatarios de la alerta (en el alcance de la oficina del crédito).</summary>
    public const string PermisoDeLaAlerta = "Lending.Loans.Read";

    public async Task<RespuestaDelContratoDto<NoDescontadoRegistradoDto>> RegistrarAsync(Recaudador recaudador, NoDescontado n, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(recaudador);
        ArgumentNullException.ThrowIfNull(n);
        var credito = await db.Loans.AsNoTracking().FirstOrDefaultAsync(l => l.PublicId == n.ObligationPublicId && !l.IsDeleted, ct);
        if (credito is null) return Rechazo(ErroresDePagos.ObligacionInexistente());

        var previo = await db.InstallmentNonDeductions.AsNoTracking()
            .FirstOrDefaultAsync(x => x.LoanId == credito.Id && x.ContractOperationKey == n.IdempotencyKey && x.Origin == NonDeductionOrigin.Contract, ct);
        if (previo is not null)
            return new(ResultadoDelContrato.AlreadyApplied, AuditEventTypes.LendingObligationContractApplied, ErroresDePagos.MensajeNoDescontado,
                new NoDescontadoRegistradoDto(previo.PublicId, credito.PublicId, previo.InstallmentNumber, previo.NotDeductedAmount));

        var cuota = await CuotaAsync(credito.Id, n.InstallmentNumber, ct);
        if (cuota is null) return Rechazo(ErroresDePagos.NoPagable(credito.Number, credito.Status.ToString()));

        var sucursal = await db.Branches.AsNoTracking().Where(b => b.Id == credito.BranchId).Select(b => (Guid?)b.PublicId).FirstOrDefaultAsync(ct);
        var alerta = await alertas.LevantarAsync(new AlertaALevantar(
            TiposDeAlerta.CuotaNoDescontadaPorNomina,
            $"Cuota {cuota.Value.Numero} del crédito {credito.Number} no descontada por nómina",
            $"La nómina no descontó {ErroresDePagos.Pesos(n.NotDeductedAmount)} de la cuota {cuota.Value.Numero} del crédito {credito.Number} "
            + $"({Motivo(n.Reason)}). La cuota sigue exigible: cóbrela por caja o acuerde el pago con el deudor.",
            EntityType: "Loan", EntityPublicId: credito.PublicId,
            DedupKey: $"{TiposDeAlerta.CuotaNoDescontadaPorNomina}:{credito.PublicId:D}:{cuota.Value.Numero}",
            RecipientPermissions: [PermisoDeLaAlerta], ModuloDeOrigen: "Lending", ScopeBranchPublicId: sucursal), ct);

        var fila = new InstallmentNonDeduction
        {
            LoanId = credito.Id,
            RepaymentInstallmentId = cuota.Value.Id,
            InstallmentNumber = cuota.Value.Numero,
            DueDate = cuota.Value.Vencimiento,
            Origin = NonDeductionOrigin.Contract,
            Reason = (NonDeductionReason)(int)n.Reason,
            ReasonText = string.IsNullOrWhiteSpace(n.Detail) ? null : n.Detail.Trim(),
            ExpectedAmount = n.ExpectedAmount,
            NotDeductedAmount = n.NotDeductedAmount,
            CollectorCode = recaudador.Code,
            ContractOperationKey = n.IdempotencyKey,
            AlertPublicId = alerta.IsSuccess ? alerta.Value.AlertPublicId : null,
        };
        db.InstallmentNonDeductions.Add(fila);
        return new(ResultadoDelContrato.Applied, AuditEventTypes.LendingObligationContractApplied, ErroresDePagos.MensajeNoDescontado,
            new NoDescontadoRegistradoDto(fila.PublicId, credito.PublicId, cuota.Value.Numero, n.NotDeductedAmount));
    }

    /// <summary>
    /// La cuota del no descontado: la que nombra Nómina o, si no, la más antigua con saldo a hoy (la que la cuota del ciclo cubre primero); su fila
    /// en la última versión del plan.
    /// </summary>
    private async Task<(long Id, int Numero, DateOnly Vencimiento)?> CuotaAsync(int loanId, int? numero, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        if (numero is null)
        {
            var cargado = await cargador.CargarAsync(loanId, null, hoy, ct);
            if (cargado is { Listo: true })
            {
                var estado = MotorDeCartera.Reconstruir(cargado.Entrada!, hoy);
                numero = estado.Cuotas.FirstOrDefault(c => estado.Saldos.Any(s => s.Cuota == c.Numero && s.Valor > 0m
                             && s.Concepto is not (LoanConcept.Advance or LoanConcept.CreditBalance)))?.Numero
                         ?? estado.Cuotas.FirstOrDefault(c => !c.Vencida)?.Numero;
            }
        }
        var version = await db.RepaymentScheduleVersions.AsNoTracking().Where(v => v.LoanId == loanId)
            .OrderByDescending(v => v.VersionNumber).Select(v => (int?)v.Id).FirstOrDefaultAsync(ct);
        if (version is null) return null;
        var cuotas = await db.RepaymentInstallments.AsNoTracking().Where(i => i.ScheduleVersionId == version.Value)
            .OrderBy(i => i.InstallmentNumber).Select(i => new { i.Id, i.InstallmentNumber, i.DueDate }).ToListAsync(ct);
        var cuota = numero is { } n ? cuotas.FirstOrDefault(c => c.InstallmentNumber == n) : cuotas.FirstOrDefault(c => c.DueDate >= hoy) ?? cuotas.LastOrDefault();
        return cuota is null ? null : (cuota.Id, cuota.InstallmentNumber, cuota.DueDate);
    }

    private static string Motivo(MotivoDeNoDescuento m) => m switch
    {
        MotivoDeNoDescuento.DeductionCap => "tope de descuentos",
        MotivoDeNoDescuento.InsufficientNet => "neto insuficiente",
        MotivoDeNoDescuento.DeductionPriority => "prelación entre descuentos",
        MotivoDeNoDescuento.Termination => "retiro",
        MotivoDeNoDescuento.Leave => "licencia",
        MotivoDeNoDescuento.Disability => "incapacidad",
        _ => "otro motivo",
    };

    private static RespuestaDelContratoDto<NoDescontadoRegistradoDto> Rechazo(Error e) =>
        new(ResultadoDelContrato.Rejected, e.Code, e.Message, null, e is ErrorConDatos d ? ContratoDeObligacionesDeCartera.Datos(d) : null);
}
