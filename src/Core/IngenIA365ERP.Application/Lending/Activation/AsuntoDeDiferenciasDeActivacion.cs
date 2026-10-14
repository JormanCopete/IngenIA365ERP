using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Activation;

/// <summary>
/// El asunto de acta <c>ActivationDifferences</c> (feature 013, T0295, C61, C107, D17): la carga de migración aplicada cuyas diferencias
/// y activación aprueba un órgano. Pendiente mientras la carga esté <c>Applied</c>; el expediente es la carga; la huella cubre su versión
/// de cuadre y las huellas del cuadre y de la conciliación, así que una carga que cambia después de llevarla a la sesión no se cierra con
/// esa acta (<c>Lending.Decision.ContentChanged</c>). Decidir no cambia la carga: la activación (y, desde US8, la aceptación de diferencias)
/// valida el punto cerrado con <see cref="PuntoDeActaCerrado"/>. (nuevo)
/// </summary>
public sealed class AsuntoDeDiferenciasDeActivacion(IApplicationDbContext db) : IAsuntoDeActa
{
    public const string ClaseDeExpediente = "MigrationLoad";

    public string SubjectType => LendingDecisionSubjects.ActivationDifferences;

    public async Task<Result<AsuntoDeActa>> DescribirAsync(Guid subjectPublicId, DateOnly fecha, CancellationToken ct)
    {
        var carga = await db.MigrationLoads.AsNoTracking().FirstOrDefaultAsync(l => l.PublicId == subjectPublicId && !l.IsDeleted, ct);
        if (carga is null) return Result.Failure<AsuntoDeActa>(ErroresDeActivacion.CargaNoEncontrada(subjectPublicId));

        var huella = new HuellaDeCartera()
            .Identificador("load", carga.PublicId)
            .Enumeracion("status", (MigrationLoadStatus?)carga.Status)
            .Fecha("cutoffDate", carga.CutoffDate)
            .Entero("checkVersion", carga.CurrentCheckVersion)
            .Texto("balanceCheckSha256", carga.BalanceCheckSha256)
            .Texto("salesReconciliationSha256", carga.SalesReconciliationSha256)
            .Calcular();
        // US8 (T0744): lo que el órgano aprueba —las diferencias vigentes del cuadre y de la conciliación, las ventas que quedan fuera de Cartera— y
        // lo que el acta declara (vivienda fuera hasta una entrega posterior; el plan migrado se conserva y el interés lo causa el motor desde el corte, D5).
        var vigentes = carga.CurrentCheckVersion == 0 ? []
            : await Migration.ConciliacionDeVentasDeMigracion.VigentesAsync(db, carga, carga.CurrentCheckVersion, ct);
        var delCuadre = vigentes.Where(d => d.Kind == MigrationDifferenceKind.BalanceCheck).ToList();
        var fuera = vigentes.Count(d => d.Kind == MigrationDifferenceKind.SaleOutsideLending);
        var sinMigrar = vigentes.Count(d => d.Kind == MigrationDifferenceKind.SaleNotMigrated);
        var resumen = string.Create(CultureInfo.InvariantCulture,
            $"Cuadre: {delCuadre.Count} diferencias por {delCuadre.Sum(d => d.Difference):N2}. Ventas no migradas con saldo: {sinMigrar}; fuera de Cartera: {fuera}. {carga.LoanCount} créditos y {carga.ChargedOffCount} castigados al corte.")
            + " El acta declara que los créditos de vivienda quedan fuera hasta una entrega posterior y que el plan migrado se conserva: el interés lo causa el motor desde el corte.";
        return Result.Success(new AsuntoDeActa(
            string.Create(CultureInfo.InvariantCulture, $"Diferencias y activación de la carga de migración {carga.Number} (corte {carga.CutoffDate:yyyy-MM-dd})"),
            string.IsNullOrWhiteSpace(carga.Description) ? resumen : $"{carga.Description}. {resumen}",
            carga.Status == MigrationLoadStatus.Applied,
            carga.Status.ToString(),
            new ExpedienteDeCartera(ClaseDeExpediente, carga.PublicId),
            huella));
    }

    public Task<Result> AlDecidirAsync(DecisionDeActa decision, CancellationToken ct) => Task.FromResult(Result.Success());
}
