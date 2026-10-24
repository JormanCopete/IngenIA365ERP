using System.Globalization;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Receipts.Parameters;
using IngenIA365ERP.Domain.Treasury.Gmf;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Accounting;

/// <summary>
/// El único que arma las <see cref="SolicitudDeEmision"/> de los hechos de Tesorería a Contabilidad (feature 015, US10, T0145; CH5; FR-004, FR-142, FR-205;
/// contracts/contabilidad.md §2, §4; molde <c>EmisionDeRecaudo</c>). Pone el sobre —fecha contable, oficina de la cuenta propia, tercero— y el modo de paso, y se la
/// entrega a <see cref="EmisorDeMensajes"/>, que la agrega a la unidad de trabajo <b>sin guardar</b>: el hecho nace en el mismo guardado que su operación. Ningún
/// hecho lleva una cuenta: roles y dimensiones por código (<see cref="TreasuryFactDimensionsV1"/>) y la cuenta propia por su <c>PublicId</c>.
///
/// <para>
/// <b>Modo de paso.</b> Sin contabilidad iniciada, <c>NotPosted</c>. Con ella, <c>Contabilidad.ModoDePaso</c> de <c>TES</c> en <c>NoPasa</c> deja el hecho
/// <c>NotPosted</c>; si no, todos los de Tesorería van en línea (contabilidad.md §4). La fecha contable es la de la operación, o el primer día del primer mes
/// abierto si su mes está cerrado. Cada historia suma su familia en su archivo: el pago girado en <c>EmisionDeTesoreria.Pagos.cs</c>. (nuevo)
/// </para>
/// </summary>
public sealed partial class EmisionDeTesoreria(
    IApplicationDbContext db,
    EmisorDeMensajes emisor,
    IContabilidadParaTesoreria contabilidad,
    ILectorDeParametros lector,
    IActorActual actorActual)
{
    public const string ClaseResultado = "TreasuryResult";

    /// <summary>El código de una oficina en las dimensiones: su código propio, o su Id si no lo tiene (como Recaudo).</summary>
    public static string CodigoDeOficina(string? codigo, int id) => string.IsNullOrWhiteSpace(codigo) ? id.ToString(CultureInfo.InvariantCulture) : codigo;

    /// <summary>Las dimensiones de una orden pagada desde una cuenta propia.</summary>
    public async Task<TreasuryFactDimensionsV1> DimensionesAsync(OwnBankAccount cuenta, PaymentOrder orden, CancellationToken ct)
    {
        var oficina = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == cuenta.BranchId).Select(b => b.LegacyCode).FirstOrDefaultAsync(ct);
        return new TreasuryFactDimensionsV1
        {
            BranchCode = CodigoDeOficina(oficina, cuenta.BranchId),
            OwnBankAccountPublicId = cuenta.PublicId,
            OwnBankAccountCode = cuenta.Code,
            OrderOrigin = orden.Origin.ToString(),
            OrderKind = orden.OrderKind,
            Form = orden.Form.ToString(),
        };
    }

    public static TreasuryFactLineV1 Linea(string rol, string lado, decimal valor, TreasuryFactDimensionsV1 dimensiones, Guid? tercero = null, string? cruce = null) =>
        new() { Role = rol, Side = lado, Amount = valor, Dimensions = dimensiones, ThirdPartyPersonPublicId = tercero, CrossDocument = cruce };

    /// <summary>
    /// <c>GmfDePagoCausado</c> (FR-205): el GMF de un pago que dispone de una cuenta contable a favor de un tercero, si la evaluación lo grava y pide hecho; débito
    /// <c>GmfGasto</c>, crédito <c>GmfPorPagar</c>. Nulo si no hay nada que emitir. Sin guardar.
    /// </summary>
    public async Task<IntegrationMessage?> GmfDePagoCausadoAsync(OwnBankAccount cuenta, PaymentOrder orden, EvaluacionDeGmf evaluacion, decimal tarifa,
        DateOnly fecha, CancellationToken ct)
    {
        if (!evaluacion.EmiteHecho || evaluacion.Gmf <= 0m) return null;
        var dim = await DimensionesAsync(cuenta, orden, ct) with { Cause = evaluacion.Motivo };
        var beneficiario = await BeneficiarioAsync(orden, ct);
        var contenido = new GmfDePagoCausadoV1
        {
            PaymentOrderPublicId = orden.PublicId, OrderNumber = orden.Number, OrderOrigin = orden.Origin.ToString(), OrderKind = orden.OrderKind,
            OccurredOn = fecha, TaxBase = evaluacion.Base, Rate = tarifa, Amount = evaluacion.Gmf, RegisteredBy = await UsuarioAsync(ct),
            Lines =
            [
                Linea(RolesDeTesoreria.GmfGasto, RolesDeTesoreria.Debito, evaluacion.Gmf, dim, cruce: orden.Number),
                Linea(RolesDeTesoreria.GmfPorPagar, RolesDeTesoreria.Credito, evaluacion.Gmf, dim, cruce: orden.Number),
            ],
        };
        return await EmitirAsync(contenido, orden.PublicId, "PaymentOrderGmf", orden.Number, fecha, cuenta.BranchId, beneficiario, ClavesDeEvento.Confirmacion, ct);
    }

    private async Task<Guid> BeneficiarioAsync(PaymentOrder orden, CancellationToken ct) =>
        await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == orden.BeneficiaryPersonId).Select(p => p.PublicId).FirstAsync(ct);

    private async Task<UserRefV1> UsuarioAsync(CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        return new UserRefV1 { CentralUserId = actor.CentralUserId, Name = actor.Name };
    }

    /// <summary>La fecha contable y el modo de un hecho de Tesorería, a la fecha de la operación.</summary>
    private async Task<(DateOnly Fecha, ModoDeEntrega Modo)> FechaYModoAsync(DateOnly fechaDeOperacion, CancellationToken ct)
    {
        var estado = await contabilidad.EstadoDelPeriodoAsync(fechaDeOperacion, ct);
        var fecha = estado.State == EstadoDePeriodoContable.Closed && estado.FirstOpenMonthFrom is { } abierto && abierto > fechaDeOperacion
            ? abierto
            : fechaDeOperacion;
        if (!estado.AccountingInitialized) return (fecha, new ModoDeEntrega.Sellado(DeliveryMode.NotPosted));
        var modo = await lector.LeerAsync(ParametrosDeRecaudoYTesoreria.ModuloTesoreria, ParametrosDeRecaudoYTesoreria.ContabilidadModoDePaso, fecha, ct: ct);
        return modo.IsSuccess && modo.Value.Texto == "NoPasa"
            ? (fecha, new ModoDeEntrega.Sellado(DeliveryMode.NotPosted))
            : (fecha, new ModoDeEntrega.Sellado(DeliveryMode.Online));
    }

    /// <summary>Emite un hecho en línea con su origen de documento. Sin guardar.</summary>
    private async Task<IntegrationMessage> EmitirAsync(object contenido, Guid origen, string claseDeDocumento, string numero, DateOnly fechaDeOperacion,
        int branchId, Guid? tercero, string claveDeEvento, CancellationToken ct)
    {
        var (fecha, modo) = await FechaYModoAsync(fechaDeOperacion, ct);
        var oficina = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == branchId).Select(b => b.PublicId).FirstAsync(ct);
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, origen, claseDeDocumento, null, numero, fecha, oficina, PersonPublicId: tercero),
            claveDeEvento,
            [contenido],
            modo), ct);
        return mensajes[0];
    }
}
