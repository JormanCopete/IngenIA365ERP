using System.Globalization;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.GoLive;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.Treasury;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Activation;

/// <summary>
/// Las condiciones automáticas de Tesorería en la lista de go/no-go (feature 015, US24, T0320; R-28; FR-227). Sólo lee:
/// <list type="bullet">
/// <item><c>TES.REQ.{código}</c>: cada requisito de la activación completa (<see cref="RequisitosDeActivacionDeTesoreria"/>), con «gira cheques» = hay una chequera
/// activa (el sistema no guarda ese dato: lo declara quien activa, fase 19);</item>
/// <item><see cref="GiroDePruebaPorCuenta"/>: cada cuenta propia de pagos vigente pagó al menos una orden (el giro de prueba);</item>
/// <item><see cref="GiroDePruebaPorForma"/>: al menos un pago por transferencia (archivo o portal) y, si hay chequera activa, uno por cheque;</item>
/// <item><see cref="SegundoFactorDeAprobadores"/>: quienes aprueban lotes (<c>Treasury.Batches.Approve</c>) y registran resultados (<c>Treasury.Results.Register</c>)
/// tienen el segundo factor inscrito.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class VerificacionDeSalidaDeTesoreria(
    IApplicationDbContext db,
    RequisitosDeActivacionDeTesoreria requisitos,
    IAutoridadDeOtroAprobador permisos,
    IDateTimeService reloj) : IVerificacionDeSalida
{
    public const string GiroDePruebaPorCuenta = "TestDisbursementPerAccount";
    public const string GiroDePruebaPorForma = "TestDisbursementPerForm";
    public const string SegundoFactorDeAprobadores = "SecondFactorApprovers";
    /// <summary>El prefijo de los requisitos de la activación, con el módulo (ver <see cref="Receipts.Activation.VerificacionDeSalidaDeRecaudo.PrefijoDeRequisito"/>).</summary>
    public const string PrefijoDeRequisito = "TES.REQ.";

    /// <summary>Los permisos de quienes deciden sobre el dinero en Tesorería.</summary>
    public static readonly IReadOnlyList<string> PermisosDeAprobador = ["Treasury.Batches.Approve", "Treasury.Results.Register"];

    public string Modulo => EstadoOperativoDeTesoreriaParaElCierre.Modulo;

    public async Task<IReadOnlyList<ResultadoDeVerificacion>> VerificarAsync(CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var lista = new List<ResultadoDeVerificacion>();
        var conChequera = await db.Checkbooks.AsNoTracking().AnyAsync(k => k.Status == Domain.Enums.Treasury.CheckbookState.Active, ct);
        var (reqs, _) = await requisitos.EvaluarAsync(hoy, completa: true, corte: null, ofreceCheques: conChequera, ct);
        lista.AddRange(reqs.Select(r => new ResultadoDeVerificacion(PrefijoDeRequisito + r.Code, $"Activación de Tesorería: {r.Description}", r.Met,
            r.Met ? "Cumple." : "Falta: " + r.Description)));

        var cuentas = (await db.OwnBankAccounts.AsNoTracking().ToListAsync(ct)).Where(c => c.Activa(hoy) && c.Purposes.HasFlag(BankAccountPurpose.Payments))
            .ToDictionary(c => c.Id, c => c.Code);
        var pagados = await (from i in db.PaymentBatchItems.AsNoTracking()
                             join b in db.PaymentBatches.AsNoTracking() on i.PaymentBatchId equals b.Id
                             where i.Status == PaymentBatchItemState.Paid
                             select new { b.BankAccountId, b.Form }).Distinct().ToListAsync(ct);
        var sinGiro = cuentas.Where(c => !pagados.Any(p => p.BankAccountId == c.Key)).Select(c => c.Value).ToList();
        lista.Add(new(GiroDePruebaPorCuenta, "Un giro de prueba pagado desde cada cuenta propia de pagos.", cuentas.Count > 0 && sinGiro.Count == 0,
            cuentas.Count == 0 ? "No hay cuentas propias de pagos vigentes." : sinGiro.Count == 0
                ? string.Create(CultureInfo.InvariantCulture, $"{cuentas.Count} cuenta(s) con su giro.")
                : "Sin giro pagado: " + string.Join(", ", sinGiro)));

        var transferencia = pagados.Any(p => p.Form is PaymentBatchForm.File or PaymentBatchForm.BankPortal);
        var cheque = !conChequera || pagados.Any(p => p.Form == PaymentBatchForm.Checks);
        lista.Add(new(GiroDePruebaPorForma, "Un giro de prueba pagado por cada forma: transferencia y, si gira cheques, cheque.", transferencia && cheque,
            (transferencia ? "Transferencia pagada." : "Falta un pago por transferencia (archivo o portal).") + " "
            + (conChequera ? cheque ? "Cheque pagado." : "Falta un pago por cheque." : "Sin chequera activa: no se exige el cheque.")));

        var usuarios = await db.Users.AsNoTracking().Where(u => u.IsActive && !u.IsSaasOperator).Select(u => new { u.Id, u.Username, u.IsMfaEnabled }).ToListAsync(ct);
        var aprobadores = new List<(string Nombre, bool Mfa)>();
        foreach (var u in usuarios)
            foreach (var p in PermisosDeAprobador)
                if (await permisos.TienePermisoAsync(u.Id, p, ct)) { aprobadores.Add((u.Username, u.IsMfaEnabled)); break; }
        var sinFactor = aprobadores.Where(a => !a.Mfa).Select(a => a.Nombre).ToList();
        // Segunda ronda del 2026-10-27 (D-l; recomendación aceptada): sin segundo factor sólo avisa (verde con los nombres en la evidencia).
        lista.Add(new(SegundoFactorDeAprobadores, "Aprobadores de lotes y de resultados con el segundo factor inscrito (si falta, avisa).", aprobadores.Count > 0,
            aprobadores.Count == 0 ? "Nadie tiene permiso para aprobar lotes ni registrar resultados." : sinFactor.Count == 0
                ? string.Create(CultureInfo.InvariantCulture, $"{aprobadores.Count} aprobador(es) con segundo factor.")
                : "Sin segundo factor: " + string.Join(", ", sinFactor)));
        return lista;
    }
}
