using System.Globalization;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Accounting;

/// <summary>
/// El único que arma las <see cref="SolicitudDeEmision"/> de Recaudo (feature 015, T0091; CH5; FR-004, FR-140, FR-141; contracts/contabilidad.md §2; molde
/// <c>EmisionDeCartera</c>). Pone el sobre —fecha contable, oficina, tercero— y el modo de paso, y se la entrega a <see cref="EmisorDeMensajes"/>, que la
/// agrega a la unidad de trabajo <b>sin guardar</b>: el hecho nace en el mismo guardado que la operación. Ningún hecho lleva una cuenta: roles y dimensiones
/// por código (<see cref="ReceiptsFactDimensionsV1"/>); la cuenta la decide la matriz de Contabilidad.
///
/// <para>
/// <b>Modo de paso.</b> Sin contabilidad iniciada, <c>NotPosted</c> (como en Inventario y Cartera). Con ella, <c>Contabilidad.ModoDePaso</c> de <c>RCD</c> en
/// <c>NoPasa</c> deja el hecho <c>NotPosted</c>; cualquier otro valor deja en línea los hechos que contabilidad.md §3 declara «en línea» (todos los de la caja).
/// Los recibos de caja por lotes resumidos llegan con la fase 6. La fecha contable es la de la operación, o el primer día del primer mes abierto si su mes
/// está cerrado.
/// </para>
///
/// Cada historia suma su familia en su archivo: la caja en <c>EmisionDeRecaudo.Caja.cs</c>. (nuevo)
/// </summary>
public sealed partial class EmisionDeRecaudo(
    IApplicationDbContext db,
    EmisorDeMensajes emisor,
    IContabilidadParaRecaudo contabilidad,
    ILectorDeParametros lector,
    IActorActual actorActual)
{
    /// <summary>La clase de recaudador de los hechos de Recaudo (dimensión <c>CollectorClass</c>).</summary>
    public const string ClaseDeRecaudador = "Receipts";

    /// <summary>El código de una oficina en las dimensiones: su código propio, o su Id si no lo tiene (DT §2.7).</summary>
    public static string CodigoDeOficina(string? codigo, int id) => string.IsNullOrWhiteSpace(codigo) ? id.ToString(CultureInfo.InvariantCulture) : codigo;

    /// <summary>El rol contable de lo que entra o sale por un medio (contabilidad.md §5).</summary>
    public static string RolDelMedio(PaymentMeansClass clase) => clase switch
    {
        PaymentMeansClass.Cash => RolesDeRecaudo.Caja,
        PaymentMeansClass.Transfer or PaymentMeansClass.BankDeposit => RolesDeRecaudo.BancoRecaudo,
        PaymentMeansClass.Check => RolesDeRecaudo.ChequesEnCanje,
        _ => RolesDeRecaudo.MedioPorCobrar,
    };

    /// <summary>Segunda ronda del 2026-10-27 (D-f): las dimensiones de la línea del medio, con su cuenta propia si el rol es <c>BancoRecaudo</c>.</summary>
    public static ReceiptsFactDimensionsV1 ConCuentaDelMedio(ReceiptsFactDimensionsV1 dim, ReceiptMeans? medio) =>
        medio is { OwnBankAccountPublicId: { } cuenta } && RolDelMedio(medio.MeansClass) == RolesDeRecaudo.BancoRecaudo ? dim with { OwnBankAccountPublicId = cuenta } : dim;

    /// <summary>Las dimensiones de una caja (oficina, punto, caja) y, si se da, de un medio.</summary>
    public async Task<ReceiptsFactDimensionsV1> DimensionesDeCajaAsync(int cashBoxId, int? paymentMeansId, CancellationToken ct)
    {
        var caja = await (from c in db.CashBoxes.AsNoTracking()
                          join p in db.CollectionPoints.AsNoTracking() on c.CollectionPointId equals p.Id
                          join b in db.Branches.AsNoTracking().IgnoreQueryFilters() on p.BranchId equals b.Id
                          where c.Id == cashBoxId
                          select new { Caja = c.Code, Punto = p.Code, b.LegacyCode, OficinaId = b.Id }).FirstAsync(ct);
        var medio = paymentMeansId is { } m
            ? await db.PaymentMeans.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == m).Select(x => new { x.Code, x.Class }).FirstOrDefaultAsync(ct)
            : null;
        return new ReceiptsFactDimensionsV1
        {
            BranchCode = CodigoDeOficina(caja.LegacyCode, caja.OficinaId),
            CollectionPointCode = caja.Punto,
            CashBoxCode = caja.Caja,
            MeansCode = medio?.Code,
            MeansClass = medio is null ? null : (int)medio.Class,
            CollectorClass = ClaseDeRecaudador,
            Channel = "CashDesk",
        };
    }

    /// <summary>Las dimensiones de una bóveda (sólo la oficina pesa, contabilidad.md §5).</summary>
    public async Task<ReceiptsFactDimensionsV1> DimensionesDeOficinaAsync(int branchId, CancellationToken ct)
    {
        var codigo = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == branchId).Select(b => b.LegacyCode).FirstOrDefaultAsync(ct);
        return new ReceiptsFactDimensionsV1 { BranchCode = CodigoDeOficina(codigo, branchId), CollectorClass = ClaseDeRecaudador, Channel = "CashDesk" };
    }

    /// <summary>Una línea de un hecho.</summary>
    public static FactLineV1 Linea(string rol, string lado, decimal valor, ReceiptsFactDimensionsV1 dimensiones, Guid? tercero = null, string? cruce = null) =>
        new() { Role = rol, Side = lado, Amount = valor, Dimensions = dimensiones, ThirdPartyPersonPublicId = tercero, CrossDocument = cruce };

    private async Task<UserRefV1> UsuarioAsync(CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        return new UserRefV1 { CentralUserId = actor.CentralUserId, Name = actor.Name };
    }

    private async Task<UserRefV1> UsuarioAsync(int userId, CancellationToken ct)
    {
        var u = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == userId)
            .Select(x => new { x.CentralUserId, x.Username }).FirstOrDefaultAsync(ct);
        return new UserRefV1 { CentralUserId = u?.CentralUserId, Name = u?.Username ?? string.Empty };
    }

    /// <summary>La fecha contable y el modo de un hecho «en línea» de Recaudo, a la fecha de la operación.</summary>
    private async Task<(DateOnly Fecha, ModoDeEntrega Modo)> FechaYModoAsync(DateOnly fechaDeOperacion, CancellationToken ct)
    {
        var estado = await contabilidad.EstadoDelPeriodoAsync(fechaDeOperacion, ct);
        var fecha = estado.State == EstadoDePeriodoContable.Closed && estado.FirstOpenMonthFrom is { } abierto && abierto > fechaDeOperacion
            ? abierto
            : fechaDeOperacion;
        if (!estado.AccountingInitialized) return (fecha, new ModoDeEntrega.Sellado(DeliveryMode.NotPosted));
        var modo = await lector.LeerAsync(ParametrosDeRecaudoYTesoreria.ModuloRecaudo, ParametrosDeRecaudoYTesoreria.ContabilidadModoDePaso, fecha, ct: ct);
        return modo.IsSuccess && modo.Value.Texto == "NoPasa"
            ? (fecha, new ModoDeEntrega.Sellado(DeliveryMode.NotPosted))
            : (fecha, new ModoDeEntrega.Sellado(DeliveryMode.Online));
    }

    /// <summary>Emite un hecho en línea con su origen de documento. Sin guardar.</summary>
    private async Task<IntegrationMessage> EmitirAsync(object contenido, Guid origen, string claseDeDocumento, string? tipoDeDocumento, string numero,
        DateOnly fechaDeOperacion, int branchId, Guid? tercero, CancellationToken ct, DocumentoRelacionado? relacionado = null)
    {
        var (fecha, modo) = await FechaYModoAsync(fechaDeOperacion, ct);
        var oficina = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == branchId).Select(b => b.PublicId).FirstAsync(ct);
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, origen, claseDeDocumento, tipoDeDocumento, numero, fecha, oficina, PersonPublicId: tercero),
            ClavesDeEvento.Confirmacion,
            [contenido],
            modo,
            Relacionado: relacionado), ct);
        return mensajes[0];
    }
}
