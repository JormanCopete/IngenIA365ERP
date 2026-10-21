using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Deposits.Withdrawals;
using IngenIA365ERP.Application.Lending.Documents;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Documents;

/// <summary>
/// Arma la liquidación del retiro del asociado (feature 014, US10, T103; contracts/documentos.md §2.8; FR-076 a FR-080) para
/// <see cref="ConstructorDeDocumentosDeCartera"/>. Lee, no escribe: el trámite con su aprobación, las líneas de la versión vigente por bloque, los cruces
/// por crédito con su prelación y lo que valía cancelarlo, el descuento por pérdidas con su acta, la devolución con su forma y su plazo (o pendiente por el
/// mínimo irreducible) y los CDAT que siguen. Cubre las cuentas liquidadas (<c>LND_IssuedDocumentAccounts</c>): el reverso de un cruce o de una devolución
/// la anula (documentos.md §4). Sólo para un retiro liquidado de la persona. (nuevo)
/// </summary>
public sealed class DocumentosDeRetiro(IApplicationDbContext db)
{
    private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

    public async Task<Result<DocumentoArmado>> ArmarAsync(PedidoDeDocumentoDeCartera p, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(p);
        var retiro = p.RetiroPublicId is { } id
            ? await db.MembershipWithdrawals.AsNoTracking().FirstOrDefaultAsync(w => w.PublicId == id && w.PersonId == p.Titular.Id && !w.IsDeleted, ct)
            : null;
        if (retiro is null) return Falla(ErroresDeRetiro.Inexistente());
        if (retiro.SettlementVersion == 0 || retiro.Status is not (MembershipWithdrawalStatus.Settled or MembershipWithdrawalStatus.Paid))
            return Falla(DocumentErrors.NadaQueCertificar($"el retiro {retiro.Number} no está liquidado"));

        var lineas = await db.MembershipWithdrawalLines.AsNoTracking()
            .Where(l => l.MembershipWithdrawalId == retiro.Id && l.SettlementVersion == retiro.SettlementVersion).OrderBy(l => l.LineNumber).ToListAsync(ct);
        var idsDeCuenta = lineas.Where(l => l.MemberAccountId is not null).Select(l => l.MemberAccountId!.Value).Append(retiro.ContributionAccountId).Distinct()
            .ToList();
        var cuentas = await db.MemberAccounts.AsNoTracking().IgnoreQueryFilters().Where(a => idsDeCuenta.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);
        var idsDeCredito = lineas.Where(l => l.LoanId is not null).Select(l => l.LoanId!.Value).Distinct().ToList();
        var creditos = await db.Loans.AsNoTracking().IgnoreQueryFilters().Where(l => idsDeCredito.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.Number, ct);
        var opsDeCruce = lineas.Where(l => l.Kind == MembershipWithdrawalLineKind.LoanOffset && l.AccountOperationId is not null)
            .Select(l => l.AccountOperationId!.Value).Distinct().ToList();
        var cargas = await db.MemberAccountOperations.AsNoTracking().Where(o => opsDeCruce.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.Payload, ct);

        var impresas = lineas.Select(l => new LineaDeLiquidacionImpresa(Bloque(l.Kind), Detalle(l, cuentas, creditos), l.Amount)).ToList();
        var cruces = lineas.Where(l => l.Kind == MembershipWithdrawalLineKind.LoanOffset && l.LoanId is not null)
            .GroupBy(l => l.LoanId!.Value)
            .Select(g =>
            {
                var (prelacion, paraCancelar) = Carga(g.Select(l => l.AccountOperationId).FirstOrDefault(o => o is not null) is { } op
                    ? cargas.GetValueOrDefault(op)
                    : null);
                var aplicado = -g.Sum(l => l.Amount);
                var antes = paraCancelar ?? aplicado;
                return new CruceImpreso(prelacion, creditos[g.Key], antes, aplicado, Math.Max(0m, antes - aplicado));
            })
            .OrderBy(c => c.Prelacion).ToList();

        string? aprobacion = null;
        if (retiro.ApprovalSessionItemId is { } punto)
        {
            var a = await (from i in db.SessionItems.AsNoTracking()
                           join s in db.LendingSessions.AsNoTracking() on i.SessionId equals s.Id
                           join b in db.GoverningBodies.AsNoTracking() on s.GoverningBodyId equals b.Id
                           where i.Id == punto
                           select new { b.Name, s.Number, s.HeldOn, i.Order }).FirstOrDefaultAsync(ct);
            if (a is not null)
                aprobacion = string.Create(Co, $"{a.Name}, acta {a.Number}, punto {a.Order}, del {a.HeldOn:dd/MM/yyyy}");
        }
        var perdidas = retiro.LossDeductionAmount is > 0m and var monto
            ? string.Create(Co, $"Se descontaron {monto:N2} por pérdidas no cubiertas por la reserva (D15).")
            : CuerpoDeLiquidacionDeRetiro.SinPerdidas;
        var cdat = await db.MemberAccounts.AsNoTracking()
            .Where(a => a.HolderPersonId == retiro.PersonId && a.DepositClass == DepositClass.TermDeposit && a.Status != MemberAccountStatus.Closed && !a.IsDeleted)
            .OrderBy(a => a.Number).Select(a => a.Number).ToListAsync(ct);
        var corte = p.Hoy;
        var leyendas = new List<string>();
        if (retiro.RefundHeldByInstitutionalMinimum) leyendas.Add(CuerpoDeLiquidacionDeRetiro.LeyendaPendiente);

        var cuerpo = new CuerpoDeLiquidacionDeRetiro(retiro.Number, Withdrawals.AsuntoDeRetiroDeAsociado.Causa(retiro.Cause), retiro.FiledOn, aprobacion,
            retiro.EffectiveDate, retiro.SettledAt is { } s2 ? DateOnly.FromDateTime(s2) : corte, retiro.SettlementVersion, impresas, cruces, perdidas,
            retiro.RefundAmount ?? 0m, "Orden de giro a Tesorería", retiro.RefundDueOn, retiro.RefundHeldByInstitutionalMinimum, cdat);

        var ultimas = await db.MemberAccountOperations.AsNoTracking().Where(o => idsDeCuenta.Contains(o.MemberAccountId))
            .GroupBy(o => o.MemberAccountId).Select(g => new { Cuenta = g.Key, Ultima = g.Max(o => o.Id) }).ToDictionaryAsync(x => x.Cuenta, x => x.Ultima, ct);
        var datos = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["asOfDate"] = corte,
            ["withdrawal"] = retiro.Number,
            ["settlementVersion"] = retiro.SettlementVersion,
            ["contributions"] = retiro.ContributionsTotal,
            ["savings"] = retiro.SavingsTotal,
            ["crossed"] = retiro.LoansOffsetTotal,
            ["loss"] = retiro.LossDeductionAmount,
            ["refund"] = retiro.RefundAmount,
        };
        return Result.Success(new DocumentoArmado([], cuerpo, corte, null, null, null, null, VersionesDeReglas.Vigente, datos, leyendas)
        {
            Cuentas = idsDeCuenta.Select(c => new CuentaDeDocumentoDeCartera(c, ultimas.TryGetValue(c, out var u) ? u : null, null, null)).ToList(),
        });
    }

    private static string Bloque(MembershipWithdrawalLineKind clase) => clase switch
    {
        MembershipWithdrawalLineKind.ContributionBalance => "Aportes",
        MembershipWithdrawalLineKind.SavingsBalance => "Ahorros",
        MembershipWithdrawalLineKind.TermDepositBalance => "CDAT",
        MembershipWithdrawalLineKind.InterestToDate => "Intereses a la fecha",
        MembershipWithdrawalLineKind.Withholding => "Retención",
        MembershipWithdrawalLineKind.Gmf => "GMF",
        MembershipWithdrawalLineKind.LoanOffset => "Cruces",
        MembershipWithdrawalLineKind.LossDeduction => "Pérdidas",
        MembershipWithdrawalLineKind.Refund => "Devolución",
        _ => clase.ToString(),
    };

    private static string Detalle(Domain.Entities.Lending.Deposits.Processes.MembershipWithdrawalLine l,
        IReadOnlyDictionary<int, Domain.Entities.Lending.Deposits.Accounts.MemberAccount> cuentas, IReadOnlyDictionary<int, string> creditos)
    {
        var cuenta = l.MemberAccountId is { } c && cuentas.TryGetValue(c, out var a) ? a.Number : string.Empty;
        var concepto = Enum.TryParse<MemberAccountConcept>(l.Concept, out var k) ? DocumentosDeAportes.Clase(k) : l.Concept ?? string.Empty;
        return l.Kind switch
        {
            MembershipWithdrawalLineKind.LoanOffset => $"Crédito {(l.LoanId is { } id ? creditos[id] : string.Empty)} desde {cuenta} ({concepto})",
            _ => $"{cuenta} ({concepto})",
        };
    }

    private static (int Prelacion, decimal? ParaCancelar) Carga(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return (0, null);
        using var json = JsonDocument.Parse(payload);
        var raiz = json.RootElement;
        var prelacion = raiz.TryGetProperty("priority", out var p) && p.TryGetInt32(out var n) ? n : 0;
        decimal? paraCancelar = raiz.TryGetProperty("payoff", out var v) && v.TryGetDecimal(out var d) ? d : null;
        return (prelacion, paraCancelar);
    }

    private static Result<DocumentoArmado> Falla(Error e) => Result.Failure<DocumentoArmado>(e);
}
