using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Lines;

/// <summary>
/// El único que registra una versión de una línea de captación (con sus tramos) o de aportes (feature 014, US1, T065): número siguiente, versión y
/// tramos. No abre transacción ni decide la activación: quien llama ya pasó las reglas y corre dentro de una. Las cuentas vivas no se tocan: las que
/// sellan (CDAT, contractual, tasa pactada) conservan lo suyo y las demás leen cada día la vigente (FR-007). (nuevo)
/// </summary>
public sealed class AltaDeVersionDeCaptacion(IApplicationDbContext db, IDateTimeService reloj)
{
    /// <summary>La versión nueva no empieza antes de la última registrada de la línea (<c>VersionOverlaps</c>).</summary>
    public async Task<Result> ValidarContraLoRegistradoAsync(CreditLine linea, DateOnly validFrom, CancellationToken ct)
    {
        DateOnly? ultima = linea.Nature == LineNature.Contribution
            ? await db.ContributionLineVersions.AsNoTracking().Where(v => v.CreditLineId == linea.Id && !v.IsDeleted).Select(v => (DateOnly?)v.ValidFrom).MaxAsync(ct)
            : await db.DepositLineVersions.AsNoTracking().Where(v => v.CreditLineId == linea.Id && !v.IsDeleted).Select(v => (DateOnly?)v.ValidFrom).MaxAsync(ct);
        return ultima is { } u && validFrom < u ? Result.Failure(ErroresDeLineasDeCaptacion.VersionCruzada(linea.Nature, u)) : Result.Success();
    }

    /// <summary>Registra la versión de captación de <paramref name="linea"/> (ya guardada) con sus tramos. Guarda dos veces dentro de la transacción de quien llama.</summary>
    public async Task<DepositLineVersion> RegistrarAsync(CreditLine linea, BorradorDeVersionDeCaptacion b, CancellationToken ct)
    {
        ReglaDeCarteraException.SiHay(linea.ValidarNaturaleza(LineNature.Deposit));
        var ultimo = await db.DepositLineVersions.IgnoreQueryFilters().Where(v => v.CreditLineId == linea.Id).MaxAsync(v => (int?)v.VersionNumber, ct) ?? 0;
        var version = DepositLineVersion.Registrar(linea.Id, b, ultimo + 1,
            await Activation.ActivacionesDeCaptacion.FechaMinimaDeVersionAsync(db, LineNature.Deposit, reloj.HoyLocal, ct), reloj.UtcNow);
        db.DepositLineVersions.Add(version);
        await db.SaveChangesAsync(ct);
        db.DepositLineRateTiers.AddRange(DepositLineVersion.Tramos(version.Id, b));
        // US4 (T114): el catálogo de cargos de la versión, en el mismo guardado que sus tramos.
        db.DepositLineCharges.AddRange(DepositLineVersion.Cargos(version.Id, b));
        await db.SaveChangesAsync(ct);
        return version;
    }

    /// <summary>Registra la versión de aportes de <paramref name="linea"/> (ya guardada).</summary>
    public async Task<ContributionLineVersion> RegistrarAportesAsync(CreditLine linea, BorradorDeVersionDeAportes b, CancellationToken ct)
    {
        ReglaDeCarteraException.SiHay(linea.ValidarNaturaleza(LineNature.Contribution));
        var ultimo = await db.ContributionLineVersions.IgnoreQueryFilters().Where(v => v.CreditLineId == linea.Id).MaxAsync(v => (int?)v.VersionNumber, ct) ?? 0;
        var version = ContributionLineVersion.Registrar(linea.Id, b, ultimo + 1,
            await Activation.ActivacionesDeCaptacion.FechaMinimaDeVersionAsync(db, LineNature.Contribution, reloj.HoyLocal, ct), reloj.UtcNow);
        db.ContributionLineVersions.Add(version);
        await db.SaveChangesAsync(ct);
        return version;
    }
}

/// <summary>
/// Lo que comparten las consultas y los comandos del catálogo común (feature 014, US1, T066): la línea en alcance de cualquier naturaleza, la versión
/// vigente a una fecha con la regla bitemporal (<see cref="VigenciaBitemporal.VigenteDeCartera{T}"/>), los DTO y el resumen de la grilla. (nuevo)
/// </summary>
public static class LectorDeLineasDeCaptacion
{
    /// <summary>La línea viva por su PublicId y dentro del alcance de líneas (de cualquier naturaleza); fuera, el mismo 404 que si no existiera.</summary>
    public static async Task<Result<CreditLine>> LineaEnAlcanceAsync(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria,
        Guid publicId, bool seguir, CancellationToken ct)
    {
        var consulta = seguir ? db.CreditLines : db.CreditLines.AsNoTracking();
        var linea = await consulta.FirstOrDefaultAsync(l => l.PublicId == publicId && !l.IsDeleted, ct);
        if (linea is null) return Result.Failure<CreditLine>(ErroresDeLineasDeCaptacion.LineaInexistente());
        var dentro = await alcance.AsegurarAsync(null, linea.Id, ErroresDeLineasDeCaptacion.LineaInexistente(), auditoria, nameof(CreditLine), linea.PublicId, ct);
        return dentro.IsFailure ? Result.Failure<CreditLine>(dentro.Error) : Result.Success(linea);
    }

    /// <summary>La clase de una línea de captación: la de su primera versión (es la misma en todas). Nula si no tiene versiones.</summary>
    public static async Task<DepositClass?> ClaseDeAsync(IApplicationDbContext db, int lineaId, CancellationToken ct) =>
        await db.DepositLineVersions.AsNoTracking().Where(v => v.CreditLineId == lineaId && !v.IsDeleted)
            .OrderBy(v => v.VersionNumber).Select(v => (DepositClass?)v.DepositClass).FirstOrDefaultAsync(ct);

    /// <summary>La versión de captación vigente en <paramref name="fecha"/> con lo registrado hasta <paramref name="hasta"/> (nulo = todo).</summary>
    public static async Task<DepositLineVersion?> VigenteDeCaptacionAsync(IApplicationDbContext db, int lineaId, DateOnly fecha, DateTime? hasta, CancellationToken ct)
    {
        var versiones = await db.DepositLineVersions.AsNoTracking().Where(v => v.CreditLineId == lineaId && !v.IsDeleted).ToListAsync(ct);
        return VigenciaBitemporal.VigenteDeCartera(versiones, fecha, hasta ?? DateTime.MaxValue);
    }

    /// <summary>La versión de aportes vigente en <paramref name="fecha"/> con lo registrado hasta <paramref name="hasta"/> (nulo = todo).</summary>
    public static async Task<ContributionLineVersion?> VigenteDeAportesAsync(IApplicationDbContext db, int lineaId, DateOnly fecha, DateTime? hasta, CancellationToken ct)
    {
        var versiones = await db.ContributionLineVersions.AsNoTracking().Where(v => v.CreditLineId == lineaId && !v.IsDeleted).ToListAsync(ct);
        return VigenciaBitemporal.VigenteDeCartera(versiones, fecha, hasta ?? DateTime.MaxValue);
    }

    /// <summary>La vigente a <paramref name="fecha"/> o, si la línea todavía no tiene una, la próxima (para versionar o reabrir una línea programada).</summary>
    public static async Task<DepositLineVersion?> VigenteOProximaDeCaptacionAsync(IApplicationDbContext db, int lineaId, DateOnly fecha, CancellationToken ct) =>
        await VigenteDeCaptacionAsync(db, lineaId, fecha, null, ct)
        ?? await db.DepositLineVersions.AsNoTracking().Where(v => v.CreditLineId == lineaId && !v.IsDeleted)
            .OrderBy(v => v.ValidFrom).ThenBy(v => v.VersionNumber).FirstOrDefaultAsync(ct);

    public static async Task<List<DepositLineRateTier>> TramosAsync(IApplicationDbContext db, int versionId, CancellationToken ct) =>
        await db.DepositLineRateTiers.AsNoTracking().Where(t => t.DepositLineVersionId == versionId && !t.IsDeleted).OrderBy(t => t.Order).ToListAsync(ct);

    /// <summary>US4 (T114): los cargos de una versión de captación, por código.</summary>
    public static async Task<List<DepositLineCharge>> CargosAsync(IApplicationDbContext db, int versionId, CancellationToken ct) =>
        await db.DepositLineCharges.AsNoTracking().Where(c => c.DepositLineVersionId == versionId && !c.IsDeleted).OrderBy(c => c.Code).ToListAsync(ct);

    /// <summary>El fin de vigencia implícito de una versión: la víspera de la siguiente.</summary>
    public static DateOnly? VigenteHasta<T>(T version, IEnumerable<T> versiones) where T : BaseEntity, IVersionDeCartera
    {
        var siguiente = versiones
            .Where(v => v.Id != version.Id && (v.ValidFrom > version.ValidFrom || v.ValidFrom == version.ValidFrom && v.VersionNumber > version.VersionNumber))
            .Select(v => (DateOnly?)v.ValidFrom)
            .Min();
        return siguiente?.AddDays(-1);
    }

    public static async Task<LendingLineRefDto?> ReferenciaAsync(IApplicationDbContext db, int? lineaId, CancellationToken ct) =>
        lineaId is not { } id
            ? null
            : await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => l.Id == id)
                .Select(l => new LendingLineRefDto(l.PublicId, l.Code, l.Name)).FirstOrDefaultAsync(ct);

    public static async Task<DepositLineVersionDto> DtoDeCaptacionAsync(IApplicationDbContext db, DepositLineVersion version, CancellationToken ct)
    {
        var versiones = await db.DepositLineVersions.AsNoTracking().Where(v => v.CreditLineId == version.CreditLineId && !v.IsDeleted).ToListAsync(ct);
        return DepositLineVersionDto.De(version, await TramosAsync(db, version.Id, ct), VigenteHasta(version, versiones),
            await ReferenciaAsync(db, version.MoveToDemandLineId, ct), await CargosAsync(db, version.Id, ct));
    }

    public static async Task<ContributionLineVersionDto> DtoDeAportesAsync(IApplicationDbContext db, ContributionLineVersion version, CancellationToken ct)
    {
        var versiones = await db.ContributionLineVersions.AsNoTracking().Where(v => v.CreditLineId == version.CreditLineId && !v.IsDeleted).ToListAsync(ct);
        return ContributionLineVersionDto.De(version, VigenteHasta(version, versiones), await ReferenciaAsync(db, version.PermanentSavingsLineId, ct));
    }

    /// <summary>La línea con la versión vigente en <paramref name="fecha"/> según su naturaleza y su historial (lo registrado hasta <paramref name="hasta"/>).</summary>
    public static async Task<LendingLineDto> DtoAsync(IApplicationDbContext db, CreditLine linea, DateOnly fecha, DateTime? hasta, CancellationToken ct)
    {
        var r = hasta ?? DateTime.MaxValue;
        DepositLineVersionDto? captacion = null;
        ContributionLineVersionDto? aportes = null;
        DepositClass? clase = null;
        IReadOnlyList<LendingLineVersionSummaryDto> historial;
        switch (linea.Nature)
        {
            case LineNature.Deposit:
            {
                var versiones = await db.DepositLineVersions.AsNoTracking().Where(v => v.CreditLineId == linea.Id && !v.IsDeleted && v.RegisteredAt <= r).ToListAsync(ct);
                var vigente = VigenciaBitemporal.VigenteDeCartera(versiones, fecha, r);
                if (vigente is not null) captacion = await DtoDeCaptacionAsync(db, vigente, ct) with { ValidTo = VigenteHasta(vigente, versiones) };
                clase = versiones.OrderBy(v => v.VersionNumber).Select(v => (DepositClass?)v.DepositClass).FirstOrDefault();
                historial = Historial(versiones, vigente);
                break;
            }
            case LineNature.Contribution:
            {
                var versiones = await db.ContributionLineVersions.AsNoTracking().Where(v => v.CreditLineId == linea.Id && !v.IsDeleted && v.RegisteredAt <= r).ToListAsync(ct);
                var vigente = VigenciaBitemporal.VigenteDeCartera(versiones, fecha, r);
                if (vigente is not null) aportes = await DtoDeAportesAsync(db, vigente, ct) with { ValidTo = VigenteHasta(vigente, versiones) };
                historial = Historial(versiones, vigente);
                break;
            }
            default:
            {
                var versiones = await db.CreditLineVersions.AsNoTracking().Where(v => v.CreditLineId == linea.Id && !v.IsDeleted && v.RegisteredAt <= r).ToListAsync(ct);
                historial = Historial(versiones, VigenciaBitemporal.VigenteDeCartera(versiones, fecha, r));
                break;
            }
        }
        return new LendingLineDto(linea.PublicId, linea.Code, linea.Name, linea.Nature, clase, linea.Status, linea.ClosedReason, linea.ClosedAt, linea.CloseNotes,
            fecha, captacion, aportes, historial);
    }

    private static List<LendingLineVersionSummaryDto> Historial<T>(IReadOnlyList<T> versiones, T? vigente) where T : AuditableEntity, IVersionDeCartera =>
        versiones.OrderByDescending(v => v.ValidFrom).ThenByDescending(v => v.VersionNumber)
            .Select(v => new LendingLineVersionSummaryDto(v.PublicId, v.VersionNumber, v.ValidFrom, VigenteHasta(v, versiones), v.RegisteredAt, v.CreatedBy,
                RazonDe(v), v.Id == vigente?.Id))
            .ToList();

    private static string RazonDe<T>(T v) => v switch
    {
        DepositLineVersion d => d.Reason,
        ContributionLineVersion c => c.Reason,
        CreditLineVersion c => c.Reason,
        _ => string.Empty,
    };

    // ----------------------------------------------------------------------------------------------------------- resumen --

    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-CO");

    /// <summary>El resumen de una versión de captación para la grilla: clase y tasa (fija, variable o por tramos).</summary>
    public static string Resumen(DepositLineVersion v, IReadOnlyList<DepositLineRateTier> tramos)
    {
        var tasa = v.RateKind == RateKind.Variable
            ? $"{v.IndexCode} + {Puntos(v.Margin ?? 0m)}"
            : tramos.Count > 0
                ? $"{tramos.Count} tramos ({Porcentaje(tramos.Min(t => t.AnnualRate ?? 0m))} a {Porcentaje(tramos.Max(t => t.AnnualRate ?? 0m))} EA)"
                : $"{Porcentaje(v.AnnualRate ?? 0m)} EA";
        return $"{Capital(ReglasDeLineaDeCaptacion.NombreDeClase(v.DepositClass))} · {tasa}";
    }

    /// <summary>El resumen de una versión de aportes para la grilla: la cuota y su periodicidad.</summary>
    public static string Resumen(ContributionLineVersion v)
    {
        var cuota = v.InstallmentMode switch
        {
            ContributionAmountMode.IncomeFraction => $"{Porcentaje(v.InstallmentFraction ?? 0m)} del ingreso",
            ContributionAmountMode.MinimumWageFraction => $"{(v.InstallmentFraction ?? 0m).ToString("0.######", Es)} salarios mínimos",
            _ => $"$ {(v.InstallmentAmount ?? 0m).ToString("N0", Es)}",
        };
        var permanente = v.PermanentSavingsFraction is { } p ? $" ({Porcentaje(p)} a ahorro permanente)" : string.Empty;
        return $"Cuota {cuota}{permanente}, {Periodicidad(v.InstallmentPeriodicity)}";
    }

    private static string Periodicidad(RepaymentPeriodicity p) => p switch
    {
        RepaymentPeriodicity.Weekly => "semanal",
        RepaymentPeriodicity.TenDay => "decadal",
        RepaymentPeriodicity.Biweekly => "quincenal",
        RepaymentPeriodicity.Monthly => "mensual",
        RepaymentPeriodicity.Bimonthly => "bimestral",
        RepaymentPeriodicity.Quarterly => "trimestral",
        RepaymentPeriodicity.SemiAnnual => "semestral",
        RepaymentPeriodicity.Annual => "anual",
        _ => p.ToString(),
    };

    private static string Porcentaje(decimal fraccion) => (fraccion * 100m).ToString("0.####", Es) + " %";

    private static string Puntos(decimal fraccion) => (fraccion * 100m).ToString("0.####", Es) + " pts";

    private static string Capital(string texto) => texto.Length == 0 ? texto : char.ToUpper(texto[0], Es) + texto[1..];
}
