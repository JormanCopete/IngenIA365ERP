using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Integration.Receipts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.Treasury;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Treasury.Liquidity;

// Feature 015, T2 (fase 43, T0461; FR-137, FR-139; US20): la posición diaria de liquidez. Cada componente dice su grupo y su signo, así la posición es exactamente la suma
// de lo que se muestra (US20 «Independent Test»). Los recaudos esperados y el efectivo de Recaudo llegan por puertos; si un puerto no responde, la posición sale sin ese
// componente y con el aviso (US20-2), nunca con un cero que parezca real.

/// <summary>
/// Un componente de la posición: su grupo (<c>Banks</c>, <c>Vaults</c>, <c>CashSessions</c>, <c>Commitments</c>, <c>Expected</c>), su signo (+1 suma a lo disponible, −1
/// resta, 0 informativo) y si entra sólo en lo proyectado. (nuevo)
/// </summary>
public sealed record ComponenteDeLiquidezDto(string Code, string Group, string Name, decimal Amount, int Sign, bool OnlyProjected, Guid? ReferencePublicId = null);

/// <summary>Un componente que no se pudo calcular y por qué. (nuevo)</summary>
public sealed record ComponenteFaltanteDto(string Code, string Reason);

/// <summary>La posición de un día. (nuevo)</summary>
public sealed record LiquidityPositionDto(
    DateOnly Date,
    IReadOnlyList<ComponenteDeLiquidezDto> Components,
    IReadOnlyList<ComponenteFaltanteDto> Missing,
    decimal Banks,
    decimal Vaults,
    decimal CashSessions,
    decimal Commitments,
    decimal? ExpectedLending,
    decimal? ExpectedDeposits,
    decimal Available,
    decimal Projected,
    decimal LiquidityFund,
    IReadOnlyList<CuentaDelFondoV1> FundAccounts);

/// <summary>El cálculo de la posición diaria (FR-137) y de las cuentas del fondo (FR-139). Sin guardar. (nuevo)</summary>
public sealed class PosicionDeLiquidez(
    IApplicationDbContext db,
    IContabilidadParaTesoreria contabilidad,
    ILogger<PosicionDeLiquidez> logger,
    IEfectivoDeRecaudo? efectivo = null,
    IRecaudosEsperadosDeCartera? cartera = null,
    IRecaudosEsperadosDeCaptaciones? captaciones = null)
{
    public const string GrupoBancos = "Banks";
    public const string GrupoBovedas = "Vaults";
    public const string GrupoCajas = "CashSessions";
    public const string GrupoCompromisos = "Commitments";
    public const string GrupoEsperado = "Expected";

    public const string CodigoEfectivo = "RECAUDO";
    public const string CodigoCartera = "E-CARTERA";
    public const string CodigoCaptaciones = "E-CAPTAC";
    public const string CodigoLotes = "C-LOTES";
    public const string CodigoBandeja = "C-BANDEJA";
    public const string CodigoNomina = "C-NOMINA";
    public const string CodigoCheques = "C-CHEQUES";

    /// <summary>Las órdenes en un lote aprobado o ya enviado al banco, sin resultado.</summary>
    public static readonly IReadOnlySet<PaymentOrderState> EnLotesAprobados = new HashSet<PaymentOrderState> { PaymentOrderState.Approved, PaymentOrderState.Sent };

    /// <summary>Las órdenes de la bandeja que todavía no están en un lote aprobado.</summary>
    public static readonly IReadOnlySet<PaymentOrderState> EnBandeja = new HashSet<PaymentOrderState>
    {
        PaymentOrderState.Received, PaymentOrderState.Pending, PaymentOrderState.Ready, PaymentOrderState.InBatch,
    };

    /// <summary>La posición del día <paramref name="fecha"/>; lo esperado, de <paramref name="fecha"/> a <paramref name="hastaEsperado"/>.</summary>
    public async Task<LiquidityPositionDto> CalcularAsync(DateOnly fecha, DateOnly hastaEsperado, CancellationToken ct)
    {
        var componentes = new List<ComponenteDeLiquidezDto>();
        var faltan = new List<ComponenteFaltanteDto>();

        // Bancos: el saldo en libros de cada cuenta propia activa (ya neto de los cheques girados, que el libro acredita al emitirlos).
        var cuentas = await (from c in db.OwnBankAccounts.AsNoTracking()
                             join b in db.Banks.AsNoTracking() on c.BankId equals b.Id
                             where c.Status == TreasuryCatalogStatus.Active && c.ValidFrom <= fecha && (c.ValidTo == null || c.ValidTo >= fecha)
                             orderby c.Code
                             select new { c.Id, c.PublicId, c.Code, c.Name, c.AccountNumber, c.Purposes, Banco = b.TransferCode ?? b.LegacyCode ?? string.Empty })
            .ToListAsync(ct);
        var fondo = new List<CuentaDelFondoV1>();
        foreach (var c in cuentas)
        {
            decimal saldo;
            try
            {
                saldo = await contabilidad.SaldoEnLibrosAsync(c.PublicId, fecha, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "[Tesoreria.Liquidez] Contabilidad no dio el saldo en libros de la cuenta {Cuenta}.", c.Code);
                faltan.Add(new ComponenteFaltanteDto($"BANCO-{c.Code}", $"Contabilidad no respondió el saldo en libros de la cuenta {c.Code}."));
                continue;
            }
            componentes.Add(new ComponenteDeLiquidezDto($"BANCO-{c.Code}", GrupoBancos, $"{c.Name} ({c.AccountNumber})", saldo, 1, false, c.PublicId));
            if (c.Purposes.HasFlag(BankAccountPurpose.LiquidityFund))
            {
                var extracto = await db.TreasuryBankStatements.AsNoTracking().Where(e => e.BankAccountId == c.Id).OrderByDescending(e => e.To)
                    .Select(e => (DateOnly?)e.To).FirstOrDefaultAsync(ct);
                fondo.Add(new CuentaDelFondoV1 { OwnBankAccountPublicId = c.PublicId, BankCode = c.Banco, BankAccountNumber = c.AccountNumber, Balance = saldo, StatementDate = extracto });
            }
        }

        // Bóvedas y cajas: lo que Recaudo custodia (puerto; TR3).
        if (efectivo is null)
            faltan.Add(new ComponenteFaltanteDto(CodigoEfectivo, "Recaudo no ofrece el efectivo de bóvedas y cajas en esta instalación."));
        else
        {
            try
            {
                foreach (var e in await efectivo.SaldosAsync(ct))
                {
                    var grupo = e.Kind == IEfectivoDeRecaudo.ClaseBoveda ? GrupoBovedas : GrupoCajas;
                    componentes.Add(new ComponenteDeLiquidezDto($"{(grupo == GrupoBovedas ? "BOVEDA" : "CAJA")}-{e.BranchName}", grupo, e.Name, e.Amount, 1, false,
                        e.BranchPublicId));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "[Tesoreria.Liquidez] Recaudo no respondió el efectivo de bóvedas y cajas.");
                faltan.Add(new ComponenteFaltanteDto(CodigoEfectivo, "Recaudo no respondió el efectivo de bóvedas y cajas."));
            }
        }

        // Compromisos.
        var lotes = EnLotesAprobados.ToList();
        var bandeja = EnBandeja.ToList();
        var enLotes = await db.PaymentOrders.AsNoTracking().Where(o => lotes.Contains(o.Status)).SumAsync(o => (decimal?)o.NetAmount, ct) ?? 0m;
        var enBandeja = await db.PaymentOrders.AsNoTracking().Where(o => bandeja.Contains(o.Status)).SumAsync(o => (decimal?)o.NetAmount, ct) ?? 0m;
        var nomina = await db.TreasuryPayrollDispersions.AsNoTracking().Where(d => d.PayDate >= fecha).SumAsync(d => (decimal?)d.Total, ct) ?? 0m;
        var cheques = await db.IssuedChecks.AsNoTracking()
            .Where(c => (c.Status == IssuedCheckState.Issued || c.Status == IssuedCheckState.Delivered) && c.MovedToPayableRunId == null)
            .SumAsync(c => (decimal?)c.Amount, ct) ?? 0m;
        componentes.Add(new ComponenteDeLiquidezDto(CodigoLotes, GrupoCompromisos, "Órdenes en lotes aprobados o enviados", enLotes, -1, false));
        componentes.Add(new ComponenteDeLiquidezDto(CodigoBandeja, GrupoCompromisos, "Órdenes en la bandeja", enBandeja, -1, false));
        componentes.Add(new ComponenteDeLiquidezDto(CodigoNomina, GrupoCompromisos, "Dispersiones de nómina por pagar", nomina, -1, false));
        componentes.Add(new ComponenteDeLiquidezDto(CodigoCheques, GrupoCompromisos, "Cheques en tránsito (ya descontados del saldo en libros)", cheques, 0, false));

        // Recaudos esperados (E-01 d, E-02 f).
        var deCartera = await EsperadoAsync(cartera, CodigoCartera, "Cuotas de Cartera por vencer", "Cartera", fecha, hastaEsperado, componentes, faltan, ct);
        var deCaptaciones = await EsperadoAsync(captaciones, CodigoCaptaciones, "Cuotas de aportes y ahorros por vencer", "Captaciones", fecha, hastaEsperado, componentes,
            faltan, ct);

        var bancos = componentes.Where(c => c.Group == GrupoBancos).Sum(c => c.Amount);
        var bovedas = componentes.Where(c => c.Group == GrupoBovedas).Sum(c => c.Amount);
        var cajas = componentes.Where(c => c.Group == GrupoCajas).Sum(c => c.Amount);
        var compromisos = componentes.Where(c => c.Group == GrupoCompromisos && c.Sign < 0).Sum(c => c.Amount);
        var disponible = componentes.Where(c => !c.OnlyProjected).Sum(c => c.Sign * c.Amount);
        var proyectado = disponible + componentes.Where(c => c.OnlyProjected).Sum(c => c.Sign * c.Amount);
        return new LiquidityPositionDto(fecha, componentes, faltan, bancos, bovedas, cajas, compromisos, deCartera, deCaptaciones, disponible, proyectado,
            fondo.Sum(f => f.Balance), fondo);
    }

    private async Task<decimal?> EsperadoAsync(object? puerto, string codigo, string nombre, string modulo, DateOnly desde, DateOnly hasta,
        List<ComponenteDeLiquidezDto> componentes, List<ComponenteFaltanteDto> faltan, CancellationToken ct)
    {
        if (puerto is null)
        {
            faltan.Add(new ComponenteFaltanteDto(codigo, $"{modulo} no ofrece sus recaudos esperados en esta instalación."));
            return null;
        }
        try
        {
            var dias = puerto switch
            {
                IRecaudosEsperadosDeCartera c => await c.PorFechaAsync(desde, hasta, ct),
                IRecaudosEsperadosDeCaptaciones c => await c.PorFechaAsync(desde, hasta, ct),
                _ => [],
            };
            // Sólo los días del rango pedido, aunque el puerto devuelva de más: la posición no suma lo que vence fuera de él.
            var total = dias.Where(d => d.Date >= desde && d.Date <= hasta).Sum(d => d.Amount);
            componentes.Add(new ComponenteDeLiquidezDto(codigo, GrupoEsperado, nombre, total, 1, true));
            return total;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "[Tesoreria.Liquidez] La consulta de recaudos esperados de {Modulo} no respondió.", modulo);
            faltan.Add(new ComponenteFaltanteDto(codigo, $"La consulta de {modulo} no respondió: la posición se muestra sin sus recaudos esperados."));
            return null;
        }
    }
}
