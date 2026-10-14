using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Activation;

/// <summary>Lo que dejó recalcular el cuadre y la conciliación al activar: si cambió, la versión vigente y cuántas diferencias quedan sin aceptar. (nuevo)</summary>
public sealed record RecalculoDeActivacion(bool Cambio, int Version, int Pendientes);

/// <summary>
/// El recálculo del cuadre y de la conciliación de ventas al activar (feature 013, US8, T0752; FR-141, FR-142, FR-143; data-model §29.4): con los
/// saldos contables y las ventas acumuladas de ese momento vuelve a calcular las dos huellas de la carga. Si son las aceptadas, no deja nada (suelta
/// las filas que clasificó). Si cambió alguna, nace la versión <c>CheckVersion + 1</c> con sus diferencias del cuadre y sus filas de conciliación (las
/// marcas desde la lista se copian de la versión anterior) y la carga la toma como vigente: la aceptación anterior deja de valer y hay que volver a
/// aceptar. Esa versión se guarda <b>fuera</b> de la transacción de la activación (notas de coordinación, contradicción 9): con un contexto propio de
/// la cooperativa del ámbito (<see cref="ITenantDbContextFactory.AbrirLaDelAmbito"/>) cuando hay una transacción en curso; sin ella (las pruebas
/// sin contenedores), en el mismo contexto. Una carga sin partes no se recalcula (la activación sin cartera previa de C107). (nuevo)
/// </summary>
public sealed class RecalculoDelCuadreDeActivacion(
    IApplicationDbContext db,
    IContabilidadParaCartera contabilidad,
    IMensajesEntrantes entrantes,
    ITenantDbContextFactory? fabrica = null)
{
    public async Task<Result<RecalculoDeActivacion>> RecalcularAsync(Guid cargaPublicId, CancellationToken ct)
    {
        var propio = fabrica is not null && TransaccionExplicita.HayEnCurso(db) ? fabrica.AbrirLaDelAmbito() : null;
        try
        {
            var contexto = propio?.Db ?? db;
            var carga = await contexto.MigrationLoads.FirstOrDefaultAsync(l => l.PublicId == cargaPublicId && !l.IsDeleted, ct);
            if (carga is null) return Result.Failure<RecalculoDeActivacion>(ErroresDeActivacion.CargaNoEncontrada(cargaPublicId));
            if (carga.Status != MigrationLoadStatus.Applied
                || !await contexto.MigrationLoadParts.AsNoTracking().AnyAsync(p => p.MigrationLoadId == carga.Id && p.Status == MigrationPartStatus.Imported
                                                                                     && !p.IsDeleted, ct))
                return Result.Success(new RecalculoDeActivacion(false, carga.CurrentCheckVersion, 0));

            var cuadre = new CuadreDeMigracion(contexto, contabilidad);
            var calculado = await cuadre.CalcularAsync(carga, ct);
            if (calculado.IsFailure) return Result.Failure<RecalculoDeActivacion>(calculado.Error);
            var conciliacion = new ConciliacionDeVentasDeMigracion(contexto,
                new ConciliacionDeVentasDeLaCarga(contexto, propio is null ? entrantes : new MensajesEntrantes(contexto)));
            var nueva = carga.CurrentCheckVersion + 1;
            var clasificadas = await conciliacion.ClasificarAsync(carga, nueva, ct);
            if (clasificadas.IsFailure) return Result.Failure<RecalculoDeActivacion>(clasificadas.Error);
            var huellaDeVentas = ConciliacionDeVentasDeMigracion.Huella(clasificadas.Value.Filas);

            if (string.Equals(calculado.Value.Huella, carga.BalanceCheckSha256, StringComparison.Ordinal)
                && string.Equals(huellaDeVentas, carga.SalesReconciliationSha256, StringComparison.Ordinal))
            {
                foreach (var fila in clasificadas.Value.Filas) contexto.MigrationSaleReconciliations.Remove(fila);
                var vigentes = await ConciliacionDeVentasDeMigracion.VigentesAsync(contexto, carga, carga.CurrentCheckVersion, ct);
                return Result.Success(new RecalculoDeActivacion(false, carga.CurrentCheckVersion, vigentes.Count(d => !d.Aceptada)));
            }

            var delCuadre = cuadre.Registrar(carga, nueva, calculado.Value);
            var deVentas = await conciliacion.RegistrarAsync(carga, nueva, clasificadas.Value, ct);
            var pendientesDeVentas = deVentas.Conteos.NoMigradasConSaldo + deVentas.Conteos.FueraDeCartera;
            carga.SellarCuadre(nueva, calculado.Value.InstantaneaJson, calculado.Value.Huella, delCuadre.Count == 0);
            carga.SellarConciliacion(deVentas.Huella, pendientesDeVentas == 0);
            await contexto.SaveChangesAsync(ct);
            return Result.Success(new RecalculoDeActivacion(true, nueva, delCuadre.Count + pendientesDeVentas));
        }
        finally
        {
            if (propio is not null) await propio.DisposeAsync();
        }
    }
}
