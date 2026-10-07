using System.Globalization;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// El único que arma las <see cref="SolicitudDeEmision"/> de Cartera (feature 013, T0229; CH42, molde <c>EmisionDeInventario</c>;
/// contracts/mensajes.md §5, §10.23; data-model §0.11). Pone el sobre de T13 —fecha contable por <see cref="FechaContable"/>, sucursal
/// contable, tercero; la moneda (<c>COP</c>) la pone <see cref="EmisorDeMensajes"/>— y el modo por <see cref="ModoDePasoDeCartera"/>, y se
/// la entrega a <see cref="EmisorDeMensajes"/>, que la agrega a la unidad de trabajo <b>sin guardar</b>: el hecho nace en el mismo
/// guardado que la operación (T5).
///
/// <para>
/// Por ahora emite sólo los dos informativos de la activación (<c>Always</c>, clave <c>Confirmation</c>, <c>origin.kind = Operation</c>,
/// sin tercero, sucursal principal): <see cref="CargaAplicadaAsync"/> y <see cref="ActivacionAsync"/>. US3 en adelante le suman sus
/// familias (desembolso, causación, pagos, cierres…), y US9 la categoría contable de cada hecho de un crédito. (nuevo)
/// </para>
/// </summary>
public sealed class EmisionDeCartera(
    IApplicationDbContext db,
    EmisorDeMensajes emisor,
    FechaContable fechaContable,
    ModoDePasoDeCartera modoDePaso,
    IActorActual actorActual)
{
    /// <summary>
    /// <c>CargaDeCarteraAplicada</c>: origen la carga, número <c>CORTE-{aaaammdd}-{n}</c>, fecha la de aplicación. Sin guardar.
    /// </summary>
    public async Task<Result<IntegrationMessage>> CargaAplicadaAsync(
        MigrationLoad carga, Guid? reemplazada, DateOnly fechaDeAplicacion, int diferencias, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(carga);
        var contenido = new CargaDeCarteraAplicadaV1
        {
            MigrationLoadPublicId = carga.PublicId,
            CutoffDate = carga.CutoffDate,
            ReplacesLoadPublicId = reemplazada,
            LoanCount = carga.LoanCount,
            PrincipalTotal = 0m,
            DifferencesCount = diferencias,
            AppliedBy = await UsuarioAsync(ct),
        };
        var numero = string.Create(CultureInfo.InvariantCulture, $"CORTE-{carga.CutoffDate:yyyyMMdd}-{carga.Number}");
        return await EmitirAsync(contenido, CargaDeCarteraAplicadaV1.Type, carga.PublicId, numero, fechaDeAplicacion, ct);
    }

    /// <summary>
    /// <c>CarteraActivada</c>: origen la activación (<paramref name="activacionPublicId"/>, que la fila de <c>LND_Activations</c> toma
    /// después), número = la fecha de activación, fecha la de activación. <paramref name="cierreBase"/> es nulo hasta que US9 cree el cierre
    /// base (T0708): el contenido lleva entonces <see cref="Guid.Empty"/>. Sin guardar.
    /// </summary>
    public async Task<Result<IntegrationMessage>> ActivacionAsync(
        Guid activacionPublicId, DateOnly fechaDeActivacion, MigrationLoad carga, Guid? cierreBase, decimal capitalTotal, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(carga);
        var contenido = new CarteraActivadaV1
        {
            ActivationDate = fechaDeActivacion,
            CutoffDate = carga.CutoffDate,
            MigrationLoadPublicId = carga.PublicId,
            BaseClosePublicId = cierreBase ?? Guid.Empty,
            LoanCount = carga.LoanCount,
            PrincipalTotal = capitalTotal,
            ActivatedBy = await UsuarioAsync(ct),
        };
        var numero = fechaDeActivacion.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return await EmitirAsync(contenido, CarteraActivadaV1.Type, activacionPublicId, numero, fechaDeActivacion, ct);
    }

    /// <summary>
    /// La sucursal del sobre de una operación sin sucursal propia (mensajes.md §10.23, «sucursal principal»). Cartera no lee la
    /// configuración contable (FR-126): la principal es la primera sucursal registrada de <c>COR_Branches</c>, como en Inventario.
    /// </summary>
    public async Task<Guid> SucursalPrincipalAsync(CancellationToken ct) =>
        await db.Branches.AsNoTracking().Where(b => !b.IsDeleted).OrderBy(b => b.Id).Select(b => (Guid?)b.PublicId).FirstOrDefaultAsync(ct)
        ?? throw new InvalidOperationException("La cooperativa no tiene sucursales: un hecho de Cartera necesita al menos una.");

    private async Task<Result<IntegrationMessage>> EmitirAsync(
        object contenido, string tipo, Guid origen, string numero, DateOnly fechaValor, CancellationToken ct)
    {
        var fecha = await fechaContable.CalcularAsync(fechaValor, ct);
        if (fecha.IsFailure) return Result.Failure<IntegrationMessage>(fecha.Error);
        var modo = await modoDePaso.ResolverAsync(tipo, fecha.Value, ct);
        if (modo.IsFailure) return Result.Failure<IntegrationMessage>(modo.Error);

        var sucursal = await SucursalPrincipalAsync(ct);
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Operation, origen, null, null, numero, fecha.Value, sucursal),
            ClavesDeEvento.Confirmacion,
            [contenido],
            modo.Value), ct);
        return Result.Success(mensajes[0]);
    }

    private async Task<UserRefV1> UsuarioAsync(CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        return new UserRefV1 { CentralUserId = actor.CentralUserId, Name = actor.Name };
    }
}
