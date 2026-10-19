using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Deposits.Accrual;
using IngenIA365ERP.Application.Lending.Deposits.Activation;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Projections;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Lending.Deposits.Contributions;

/// <summary>Lo que el generador dejó de una cuenta y un período: la cuota de aportes y, si la hay, la hermana del ahorro permanente. (nuevo)</summary>
public sealed record CuotaGenerada(AccountInstallment Aportes, AccountInstallment? Permanente, CuotaDeAportesCalculada Calculo);

/// <summary>
/// El subpaso «cuotas de aportes» del paso de captaciones (feature 014, US3, T082; data-model §2.5, §3.5; FR-040, FR-049; research R10): en cada tanda,
/// las cuentas de aportes vivas (activas o inactivas) generan la cuota de cada período que ya empezó desde el último generado (o desde la apertura y
/// la activación de aportes, la que sea posterior) hasta el día d, con la versión de su línea vigente al inicio de cada período:
/// <list type="bullet">
/// <item>el valor por modo (<see cref="CuotasDeAportes"/>): valor fijo, fracción del <b>ingreso registrado</b> del asociado
/// (<c>COR_Associates.ExternalSalary</c>, research E54) o fracción del <c>SMMLV</c> vigente al inicio del período, con el redondeo de la línea;</item>
/// <item>con ahorro permanente en la línea (fondos de empleados, FR-049), la parte del permanente va a la cuenta permanente viva del titular en esa línea
/// como cuota hermana (<c>LinkedInstallmentId</c>) y la de aportes lleva el resto; las dos partes quedan en la de aportes. Sin cuenta permanente viva,
/// la de aportes sólo exige su parte y la explicación lo dice (research E55).</item>
/// </list>
/// Una cuenta por transacción, escrita por <see cref="RegistroDeCuentas"/>; idempotente por el único <c>(cuenta, período)</c> y la marca
/// <c>LastInstallmentPeriod</c> del control. Lo que falla (sin ingreso, sin salario mínimo) queda en <c>LND_ProcessRunErrors</c> y las demás siguen.
/// No emite hechos: una cuota exigible no mueve dinero. US7 (T182): también las cuotas del ahorro contractual, por su plan sellado
/// (<see cref="Contractual.GeneradorDeCuotasDeContractual"/>), con captaciones activadas. (nuevo)
/// </summary>
public sealed class GeneradorDeCuotas(
    IApplicationDbContext db,
    RegistroDeCuentas registro,
    IValoresNacionales valores,
    ILogger<GeneradorDeCuotas>? logger = null,
    Contractual.GeneradorDeCuotasDeContractual? contractual = null) : ISubpasoDeCaptaciones
{
    private readonly ILogger _log = logger ?? (ILogger)NullLogger.Instance;

    public string Nombre => "CuotasDeAportes";

    public async Task<IReadOnlyList<FalloDeCuenta>> ProcesarAsync(LendingProcessRun corrida, DateOnly d, IReadOnlyList<int> cuentas, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(corrida);
        ArgumentNullException.ThrowIfNull(cuentas);
        if (cuentas.Count == 0) return [];
        var fechas = await ActivacionesDeCaptacion.LeerAsync(db, ct);
        var fallos = new List<FalloDeCuenta>();
        var ids = cuentas.ToArray();
        // US7 (T182): las cuotas del ahorro contractual, con captaciones activadas (el contrato las fija; no dependen de la línea vigente).
        if (contractual is not null && fechas.RigeEn(LineNature.Deposit, d))
            fallos.AddRange(await ContractualesAsync(corrida, ids, d, ct));
        var activacion = fechas.Aportes;
        if (activacion is not { } desdeActivacion || desdeActivacion > d) return fallos;

        var deAportes = await db.MemberAccounts.AsNoTracking()
            .Where(a => ids.Contains(a.Id) && a.Nature == LineNature.Contribution && !a.IsDeleted
                        && (a.Status == MemberAccountStatus.Active || a.Status == MemberAccountStatus.Inactive))
            .OrderBy(a => a.Id).Select(a => a.Id).ToListAsync(ct);

        foreach (var id in deAportes)
        {
            try
            {
                await GenerarAsync(corrida, id, desdeActivacion, d, ct);
            }
            catch (ReglaDeCarteraException ex)
            {
                db.DescartarCambiosSalvo(corrida);
                fallos.Add(new FalloDeCuenta(id, ex.Codigo, ex.Problema.Mensaje));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                db.DescartarCambiosSalvo(corrida);
                _log.LogWarning(ex, "[Cartera.CuotasDeAportesFallidas] La cuenta {Cuenta} no generó sus cuotas.", id);
                fallos.Add(new FalloDeCuenta(id, "Lending.Contributions.InstallmentsFailed", ex.Message));
            }
        }
        return fallos;
    }

    /// <summary>US7 (T182): las cuotas del contractual de las cuentas de la tanda, cada cuenta en su transacción (lo que falla queda en el registro).</summary>
    private async Task<IReadOnlyList<FalloDeCuenta>> ContractualesAsync(LendingProcessRun corrida, int[] ids, DateOnly d, CancellationToken ct)
    {
        var contractuales = await db.MemberAccounts.AsNoTracking()
            .Where(a => ids.Contains(a.Id) && a.Nature == LineNature.Deposit && a.DepositClass == DepositClass.Contractual && !a.IsDeleted
                        && (a.Status == MemberAccountStatus.Active || a.Status == MemberAccountStatus.Inactive))
            .OrderBy(a => a.Id).Select(a => a.Id).ToListAsync(ct);
        var fallos = new List<FalloDeCuenta>();
        foreach (var id in contractuales)
        {
            try
            {
                await contractual!.GenerarAsync(corrida, id, d, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                db.DescartarCambiosSalvo(corrida);
                _log.LogWarning(ex, "[Cartera.CuotasDeContractualFallidas] El contractual {Cuenta} no generó sus cuotas.", id);
                fallos.Add(new FalloDeCuenta(id, ex is ReglaDeCarteraException r ? r.Codigo : "Lending.Contractual.InstallmentsFailed", ex.Message));
            }
        }
        return fallos;
    }

    /// <summary>Las cuotas que le faltan a una cuenta hasta <paramref name="d"/>, en su propia transacción; devuelve las generadas.</summary>
    public async Task<IReadOnlyList<CuotaGenerada>> GenerarAsync(LendingProcessRun corrida, int cuentaId, DateOnly activacion, DateOnly d, CancellationToken ct) =>
        await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var cuenta = await db.MemberAccounts.AsNoTracking().FirstAsync(a => a.Id == cuentaId, ct);
            var control = await db.MemberAccountControls.FirstAsync(c => c.MemberAccountId == cuentaId, ct);
            var versiones = await db.ContributionLineVersions.AsNoTracking().Where(v => v.CreditLineId == cuenta.CreditLineId && !v.IsDeleted)
                .ToListAsync(ct);
            var vigente = VigenciaBitemporal.VigenteDeCartera(versiones, d, DateTime.MaxValue);
            if (vigente is null) return (IReadOnlyList<CuotaGenerada>)[];

            var inicio = Mayor(cuenta.ActivatedOn ?? cuenta.OpenedOn, activacion);
            var periodos = CuotasDeAportes.Periodos(vigente.InstallmentPeriodicity, vigente.InstallmentDueDay, inicio, d)
                .Where(p => control.LastInstallmentPeriod is null || string.CompareOrdinal(p.Clave, control.LastInstallmentPeriod) > 0)
                .ToList();
            if (periodos.Count == 0) return [];

            var existentes = (await db.AccountInstallments.AsNoTracking().Where(c => c.MemberAccountId == cuentaId)
                    .Select(c => new { c.PeriodYear, c.PeriodMonth, c.SubPeriodNumber }).ToListAsync(ct))
                .Select(c => PeriodoDeCuota.Marca(c.PeriodYear, c.PeriodMonth, c.SubPeriodNumber)).ToHashSet(StringComparer.Ordinal);
            var ingreso = await db.Associates.AsNoTracking().Where(a => a.PersonId == cuenta.HolderPersonId && !a.IsDeleted)
                .OrderByDescending(a => a.Id).Select(a => (decimal?)a.ExternalSalary).FirstOrDefaultAsync(ct);

            var generadas = new List<CuotaGenerada>();
            foreach (var periodo in periodos)
            {
                if (existentes.Contains(periodo.Clave))
                {
                    RegistroDeCuentas.MarcarCuotaGenerada(control, periodo.Clave);
                    continue;
                }
                var version = VigenciaBitemporal.VigenteDeCartera(versiones, periodo.Desde, DateTime.MaxValue) ?? vigente;
                var generada = await CuotaAsync(corrida, cuenta, version, periodo, ingreso, ct);
                generadas.Add(generada);
                RegistroDeCuentas.MarcarCuotaGenerada(control, periodo.Clave);
            }
            await db.SaveChangesAsync(ct);
            return (IReadOnlyList<CuotaGenerada>)generadas;
        }, ct);

    private async Task<CuotaGenerada> CuotaAsync(LendingProcessRun corrida, MemberAccount cuenta, ContributionLineVersion version, PeriodoDeCuota periodo,
        decimal? ingreso, CancellationToken ct)
    {
        decimal? smmlv = null;
        if (version.InstallmentMode == ContributionAmountMode.MinimumWageFraction)
        {
            var leido = await valores.LeerAsync(CodigosDeValorNacional.Smmlv, string.Empty, periodo.Desde, null, ct);
            smmlv = leido.IsSuccess ? leido.Value.Valor : null;
        }
        var (calculo, problema) = CuotasDeAportes.Calcular(new EntradaDeCuotaDeAportes(version.InstallmentMode, version.InstallmentAmount,
            version.InstallmentFraction, ingreso, smmlv, version.InstallmentRoundingUnit, version.InstallmentRoundingMode,
            version.PermanentSavingsLineId is null ? null : version.PermanentSavingsFraction));
        ReglaDeCarteraException.SiHay(problema);
        var cuota = calculo!;

        // La cuenta permanente viva del titular en la línea del reparto (FR-049).
        MemberAccount? permanente = null;
        if (cuota.Permanente > 0m && version.PermanentSavingsLineId is { } lineaPermanente)
            permanente = await db.MemberAccounts.AsNoTracking()
                .Where(a => a.HolderPersonId == cuenta.HolderPersonId && a.CreditLineId == lineaPermanente && a.DepositClass == DepositClass.Permanent
                            && a.Status != MemberAccountStatus.Closed && a.Status != MemberAccountStatus.Opening && !a.IsDeleted)
                .OrderBy(a => a.Id).FirstOrDefaultAsync(ct);
        var huella = new HuellaDeCartera()
            .Entero("version", version.VersionNumber)
            .Texto("versionSha256", version.ContentSha256)
            .Texto("period", periodo.Clave)
            .Decimal("base", cuota.Base, 2)
            .Decimal("total", cuota.Total, 2)
            .Decimal("permanent", cuota.Permanente, 2)
            .Logico("permanentAccount", permanente is not null)
            .Calcular();

        var aportes = new AccountInstallment
        {
            MemberAccountId = cuenta.Id,
            PeriodYear = (short)periodo.Anio,
            PeriodMonth = (byte)periodo.Mes,
            SubPeriodNumber = (byte)periodo.Subperiodo,
            DueDate = periodo.Vencimiento,
            Amount = cuota.Aportes,
            ContributionShare = cuota.Permanente > 0m ? cuota.Aportes : null,
            PermanentSavingsShare = cuota.Permanente > 0m ? cuota.Permanente : null,
            GeneratedByProcessRunId = corrida.Id,
            RulesVersion = VersionesDeReglas.Vigente,
            InputsSha256 = huella,
        };
        var cuotas = new List<AccountInstallment> { aportes };
        AccountInstallment? hermana = null;
        if (permanente is not null)
        {
            var yaTiene = await db.AccountInstallments.AsNoTracking().AnyAsync(c => c.MemberAccountId == permanente.Id && c.PeriodYear == periodo.Anio
                && c.PeriodMonth == periodo.Mes && c.SubPeriodNumber == periodo.Subperiodo, ct);
            if (!yaTiene)
            {
                hermana = new AccountInstallment
                {
                    MemberAccountId = permanente.Id,
                    PeriodYear = (short)periodo.Anio,
                    PeriodMonth = (byte)periodo.Mes,
                    SubPeriodNumber = (byte)periodo.Subperiodo,
                    DueDate = periodo.Vencimiento,
                    Amount = cuota.Permanente,
                    LinkedInstallment = aportes,
                    GeneratedByProcessRunId = corrida.Id,
                    RulesVersion = VersionesDeReglas.Vigente,
                    InputsSha256 = huella,
                };
                cuotas.Add(hermana);
            }
        }
        else if (cuota.Permanente > 0m)
        {
            _log.LogInformation("[Cartera.CuotaSinCuentaPermanente] La cuenta {Cuenta} reparte {Permanente} a ahorro permanente en {Periodo} y su titular no tiene "
                + "cuenta permanente viva: sólo se exige la parte de aportes.", cuenta.Number, cuota.Permanente.ToString(CultureInfo.InvariantCulture), periodo.Texto);
        }
        registro.RegistrarCuotas(cuotas);
        return new CuotaGenerada(aportes, hermana, cuota);
    }

    private static DateOnly Mayor(DateOnly a, DateOnly b) => a > b ? a : b;
}
