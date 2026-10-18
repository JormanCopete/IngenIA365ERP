using System.Globalization;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Application.Lending.Documents;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Documents;

/// <summary>
/// Arma los documentos de aportes (feature 014, US3, T087; contracts/documentos.md §2.6, §2.7; FR-106) para <see cref="ConstructorDeDocumentosDeCartera"/>.
/// Lee, no escribe:
/// <list type="bullet">
/// <item><b>constancia de aportes</b> (<c>ContributionCertificate</c>): la cuenta de aportes de la persona, su saldo al corte por clase con lo registrado
/// hasta la emisión —el mismo saldo de <c>IConsultaDeAportes</c>, SC-010—, el mínimo no reducible, el ahorro permanente si se pide y las cuotas
/// pendientes; sin cuenta de aportes, <c>Lending.Document.NothingToCertify</c>;</item>
/// <item><b>paz y salvo de aportes</b> (<c>ContributionClearance</c>): sólo sin cuotas de aportes pendientes a hoy con la información de ahora; si no,
/// <c>Lending.Document.ClearanceDenied</c> con las cuotas. Sujeto a firmeza: el registro del último pago que cubrió una cuota más los días hábiles de
/// <c>Pagos.FirmezaDiasPorClaseDeMedio</c> de su clase de medio desde su fecha valor, como el paz y salvo de crédito (<see cref="FirmezaDelPago"/>).</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class DocumentosDeAportes(IApplicationDbContext db, ReglasDeAportes reglas, FirmezaDelPago firmeza)
{
    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    public async Task<Result<DocumentoArmado>> ArmarAsync(PedidoDeDocumentoDeCartera p, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(p);
        var cuenta = await db.MemberAccounts.AsNoTracking()
            .Where(a => a.HolderPersonId == p.Titular.Id && a.Nature == LineNature.Contribution && !a.IsDeleted)
            .OrderBy(a => a.Status == MemberAccountStatus.Closed).ThenByDescending(a => a.Id).FirstOrDefaultAsync(ct);
        if (cuenta is null) return Falla(DocumentErrors.NadaQueCertificar("la persona no tiene cuenta de aportes"));
        var linea = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => l.Id == cuenta.CreditLineId).Select(l => l.Name).FirstAsync(ct);
        return p.Clase == IssuedDocumentKind.ContributionCertificate
            ? await ConstanciaAsync(p, cuenta, linea, ct)
            : await PazYSalvoAsync(p, cuenta, linea, ct);
    }

    // ---------------------------------------------------------------------------------------------------- constancia --

    private async Task<Result<DocumentoArmado>> ConstanciaAsync(PedidoDeDocumentoDeCartera p, MemberAccount cuenta, string linea, CancellationToken ct)
    {
        var corte = p.Corte ?? p.Hoy;
        var filas = await db.MemberAccountTransactions.AsNoTracking()
            .Where(t => t.MemberAccountId == cuenta.Id && !t.IsDeleted && t.ValueDate <= corte && t.RegisteredAt <= p.Emision)
            .ToListAsync(ct);
        var porClase = MemberAccountTransaction.ConceptosDeAportes
            .Select(c => (Concepto: c, Valor: filas.Where(f => f.Concept == c).Sum(f => f.Amount)))
            .ToList();
        var total = porClase.Sum(c => c.Valor);
        var version = await reglas.VersionAsync(cuenta, corte, ct);
        var minimo = version is null ? 0m : await reglas.MinimoAsync(version, cuenta.HolderPersonId, corte, ct);

        decimal? permanente = null;
        if (p.IncluyeAhorroPermanente)
        {
            var permanentes = db.MemberAccounts.AsNoTracking()
                .Where(a => a.HolderPersonId == cuenta.HolderPersonId && a.DepositClass == DepositClass.Permanent && !a.IsDeleted).Select(a => a.Id);
            permanente = await db.MemberAccountTransactions.AsNoTracking()
                .Where(t => permanentes.Contains(t.MemberAccountId) && t.Concept == MemberAccountConcept.Principal && !t.IsDeleted && t.ValueDate <= corte
                            && t.RegisteredAt <= p.Emision)
                .SumAsync(t => (decimal?)t.Amount, ct) ?? 0m;
        }
        var pendientes = (await LectorDeCuotas.DeCuentasAsync(db, [cuenta.Id], ct, registradoHasta: p.Emision))
            .Where(q => q.Pendiente > 0m && q.Cuota.DueDate <= corte)
            .Select(q => new CuotaDeAportesPendiente(cuenta.Number, q.Cuota.Periodo, q.Cuota.DueDate, q.Pendiente)).ToList();
        var vinculacion = await db.Associates.AsNoTracking().Where(a => a.PersonId == cuenta.HolderPersonId && !a.IsDeleted).OrderByDescending(a => a.Id)
            .Select(a => a.JoinDate).FirstOrDefaultAsync(ct);
        var (ultimaOperacion, ultimaFila) = Ultimas(filas);

        var datos = Datos(corte);
        datos["account"] = cuenta.Number;
        datos["contributions"] = total;
        datos["minimum"] = minimo;
        if (permanente is { } pp) datos["permanentSavings"] = pp;
        var cuerpo = new CuerpoDeConstanciaDeAportes(cuenta.Number, linea, vinculacion, corte,
            porClase.Select(c => new ClaseDeAporteCertificada(Clase(c.Concepto), c.Valor)).ToList(), total, minimo, permanente, pendientes);
        return Result.Success(new DocumentoArmado([], cuerpo, corte, null, null, null, null, VersionesDeReglas.Vigente, datos,
            [CuerpoDeConstanciaDeAportes.LeyendaDeGarantia])
        {
            Cuentas = [new CuentaDeDocumentoDeCartera(cuenta.Id, ultimaOperacion, ultimaFila, total)],
        });
    }

    // ---------------------------------------------------------------------------------------------------- paz y salvo --

    private async Task<Result<DocumentoArmado>> PazYSalvoAsync(PedidoDeDocumentoDeCartera p, MemberAccount cuenta, string linea, CancellationToken ct)
    {
        var cuotas = await LectorDeCuotas.DeCuentasAsync(db, [cuenta.Id], ct);
        var pendientes = cuotas.Where(q => q.Pendiente > 0m && q.Cuota.DueDate <= p.Hoy).ToList();
        if (pendientes.Count > 0)
            return Falla(new ErrorConDatos(DocumentErrors.CodigoPazYSalvoNegado,
                $"El asociado tiene {pendientes.Count.ToString(Co)} cuota(s) de aportes pendiente(s): pónganse al día antes del paz y salvo.",
                new { installments = pendientes.Select(q => new { period = q.Cuota.Periodo, dueDate = q.Cuota.DueDate, pending = q.Pendiente }).ToList() }));

        var filas = await db.MemberAccountTransactions.AsNoTracking().Where(t => t.MemberAccountId == cuenta.Id && !t.IsDeleted && t.RegisteredAt <= p.Emision)
            .ToListAsync(ct);
        var saldo = filas.Where(f => MemberAccountTransaction.ConceptosDeAportes.Contains(f.Concept)).Sum(f => f.Amount);
        DateOnly? hasta = null;
        var ultimoPago = filas.Where(f => f.InstallmentId is not null && f.OperationId is not null && f.ReversesTransactionId is null)
            .OrderByDescending(f => f.RegisteredAt).Select(f => f.OperationId).FirstOrDefault();
        if (ultimoPago is { } id)
        {
            var operacion = await db.MemberAccountOperations.AsNoTracking().FirstAsync(o => o.Id == id, ct);
            var clase = Enum.TryParse<ClaseDeMedioDeRecaudo>(operacion.PaymentMeansClass, out var c) ? c : ClaseDeMedioDeRecaudo.InternalOffset;
            hasta = clase == ClaseDeMedioDeRecaudo.InternalOffset ? null : await firmeza.CalcularAsync(clase, operacion.ValueDate, ct);
        }
        var leyendas = new List<string> { CuerpoDePazYSalvoDeAportes.LeyendaDeAlcance };
        if (hasta is { } h && p.Hoy < h)
            leyendas.Add(string.Format(Co, ModelosDeDocumentoDeCartera.LeyendaDeFirmeza, h.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));
        var (ultimaOperacion, ultimaFila) = Ultimas(filas);
        var datos = Datos(p.Hoy);
        datos["account"] = cuenta.Number;
        datos["finalityUntil"] = hasta;
        return Result.Success(new DocumentoArmado([], new CuerpoDePazYSalvoDeAportes(cuenta.Number, linea, p.Hoy, saldo, hasta), p.Hoy, hasta, null, null,
            null, VersionesDeReglas.Vigente, datos, leyendas)
        {
            Cuentas = [new CuentaDeDocumentoDeCartera(cuenta.Id, ultimaOperacion, ultimaFila, saldo)],
        });
    }

    /// <summary>El nombre de una clase de aporte en el documento.</summary>
    public static string Clase(MemberAccountConcept concepto) => concepto switch
    {
        MemberAccountConcept.ContributionMinimum => "Mínimos",
        MemberAccountConcept.ContributionOrdinary => "Ordinarios",
        MemberAccountConcept.ContributionExtraordinary => "Extraordinarios",
        MemberAccountConcept.ContributionRevaluation => "Revalorizados",
        _ => concepto.ToString(),
    };

    private static (long? Operacion, long? Fila) Ultimas(IReadOnlyList<MemberAccountTransaction> filas)
    {
        var ultima = filas.OrderByDescending(f => f.RegisteredAt).ThenByDescending(f => f.Id).FirstOrDefault();
        return (ultima?.OperationId, ultima?.Id is > 0 ? ultima.Id : null);
    }

    private static Dictionary<string, object?> Datos(DateOnly fecha) => new(StringComparer.Ordinal) { ["asOfDate"] = fecha };

    private static Result<DocumentoArmado> Falla(Error e) => Result.Failure<DocumentoArmado>(e);
}
