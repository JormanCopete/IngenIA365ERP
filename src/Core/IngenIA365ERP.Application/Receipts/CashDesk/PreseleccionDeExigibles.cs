using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.CashDesk;

/// <summary>
/// <c>POST /api/receipts/cash-operations/{id}/preselect</c> (<c>Receipts.Receipt.Register</c>; atajo <c>F4</c>) <b>(nuevo)</b>: preselecciona <b>todo lo exigible</b>
/// de todos los productos de la persona y corre todas las simulaciones en una sola acción (feature 015, R1, T0363; FR-187): cada crédito con algo exigible hoy
/// (como deudor y, con <see cref="IncludeCoDebtor"/>, como codeudor) por su cuota del ciclo hasta lo exigible, y cada cuenta con cuotas exigibles por sus cuotas en
/// orden de vencimiento. Los ítems que ya estaban y no son de esos productos (conceptos varios, otros valores) se conservan; después el cajero puede quitar ítems.
/// Un crédito bloqueado no se preselecciona. La simulación es la del paso de ítems (<see cref="UpdateCashOperationItemsCommandHandler"/>): un rechazo responde
/// <c>Receipts.Receipt.ItemRejected</c> con <c>data.items[]</c> y nada cambia. (nuevo)
/// </summary>
public sealed record PreselectPayablesCommand(Guid OperationPublicId, bool IncludeCoDebtor = false)
    : IRequest<Result<CashOperationDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

/// <remarks>
/// 2026-11-16 (pedido del dueño): con <c>Caja.ConMoraSoloExigible</c> cada crédito con algo exigible entra por lo exigible (modalidad <c>PastDue</c>) por su valor; y
/// el paso de ítems se arma con la consulta de Contabilidad y los parámetros de la caja —hasta ese día iba sin la consulta y, si ya había una factura comercial entre
/// los ítems, F4 fallaba entero con «No hay consulta de Contabilidad.»—.
/// </remarks>
public sealed class PreselectPayablesCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IContratoDeObligaciones obligaciones,
    IContratoDeCuentas cuentas,
    IConsultaDeCargaSinActivar? carga = null,
    IContabilidadParaRecaudo? contabilidad = null,
    ParametrosDeCaja? parametros = null) : IRequestHandler<PreselectPayablesCommand, Result<CashOperationDto>>
{
    public async Task<Result<CashOperationDto>> Handle(PreselectPayablesCommand request, CancellationToken ct)
    {
        if (await OperacionesDeCaja.VisibleAsync(db, alcance, request.OperationPublicId, seguir: false, ct) is not { } v)
            return Result.Failure<CashOperationDto>(ErroresDeRecaudo.NoEncontrado("La operación de caja"));
        var operacion = v.Operacion;
        if (operacion.Status != CashOperationState.Preparing) return Result.Failure<CashOperationDto>(ErroresDeRecaudo.OperacionNoEnPreparacion());
        var titular = await db.People.AsNoTracking().Where(p => p.Id == (operacion.BeneficiaryPersonId ?? operacion.PayerPersonId)).Select(p => p.PublicId).FirstAsync(ct);
        var politica = await PoliticaDeCaja.LeerAsync(parametros, operacion.ValueDate, ct);
        var soloLoExigible = politica.ConMoraSoloExigible;

        var exigibles = new List<ItemPedidoDto>();
        var listado = await obligaciones.ListarAsync(new SolicitudDeListado(ClavesDeRecaudo.Recaudador, titular, operacion.ValueDate, request.IncludeCoDebtor), ct);
        foreach (var o in listado.Value?.Obligations ?? [])
        {
            // 2026-11-16 (pedido del dueño; feature 024, fase 4): la cuota reconocida al inicio del ciclo es exigible del período: entra completa, por la cuota del
            // ciclo, aunque todavía no venza. 2026-11-26 (pedido del dueño): también la del período calendario en curso con su interés ya causado, si no hay nada
            // vencido; antes entraba sólo ese interés, como si fuera mora.
            if (o.BlockReasons.Count == 0 && ReglasDeLaCaja.Vencido(o) <= 0m && (o.RecognizedPending > 0m || o.DueToday > 0m) && o.CycleInstallment > 0m
                && o.AdmittedModalities.Contains(ModalidadDePago.CycleInstallment))
            {
                exigibles.Add(new ItemPedidoDto(ApplicationTarget.Loan, o.CycleInstallment, o.ObligationPublicId, titular, ModalidadDePago.CycleInstallment));
                continue;
            }
            if (o.DueToday <= 0m || o.BlockReasons.Count > 0) continue;
            var modalidad = soloLoExigible && o.AdmittedModalities.Contains(ModalidadDePago.PastDue) ? ModalidadDePago.PastDue
                : o.AdmittedModalities.Contains(ModalidadDePago.CycleInstallment) ? ModalidadDePago.CycleInstallment
                : o.AdmittedModalities.Contains(ModalidadDePago.PastDue) ? ModalidadDePago.PastDue
                : ModalidadDePago.FreeAmount;
            exigibles.Add(new ItemPedidoDto(ApplicationTarget.Loan, o.DueToday, o.ObligationPublicId, titular, modalidad));
        }
        var deCuentas = await cuentas.ListarExigiblesAsync(new SolicitudDeExigibles(ClavesDeRecaudo.Recaudador, [titular], operacion.ValueDate), ct);
        foreach (var cuenta in (deCuentas.Value?.Installments ?? []).Where(i => i.Pending > 0m).GroupBy(i => i.AccountPublicId))
            exigibles.Add(new ItemPedidoDto(ApplicationTarget.Account, cuenta.Sum(i => i.Pending), cuenta.Key, titular, AccountModality: ModalidadDeConsignacion.Installments,
                InstallmentPublicIds: cuenta.Select(i => i.InstallmentPublicId).ToList()));
        // 2026-11-16: si la prelación de la cooperativa nombra las facturas, F4 trae también las pendientes que no estén ya entre los ítems; si no, sus propios créditos
        // y cuentas quedarían detrás de una factura sin cubrir y la preselección se rechazaría entera.
        if (contabilidad is not null && politica.PrelacionPorClase.Contains(PoliticaDeCaja.Facturas))
        {
            var yaEstan = OperacionesDeCaja.Items(operacion).Where(i => i.Target == ApplicationTarget.CommercialReceivable).Select(i => i.CrossDocument)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var f in await CuentasPorCobrarDeRecaudo.PendientesAsync(db, contabilidad, titular, ct))
                if (!yaEstan.Contains(f.InvoiceNumber))
                    exigibles.Add(new ItemPedidoDto(ApplicationTarget.CommercialReceivable, f.Pending, BeneficiaryPersonPublicId: titular, CrossDocument: f.InvoiceNumber,
                        InvoicePublicId: f.AccountingDocumentPublicId));
        }

        // Lo que ya estaba y no es de esos productos se conserva (con su id).
        var productos = exigibles.Select(e => e.ProductPublicId).ToHashSet();
        var conservados = OperacionesDeCaja.Items(operacion)
            .Where(i => i.ProductPublicId is null || !productos.Contains(i.ProductPublicId))
            .Select(i => new ItemPedidoDto(i.Target, i.Amount, i.ProductPublicId, i.BeneficiaryPersonPublicId, i.LoanModality, i.AccountModality, i.Choice,
                i.InstallmentNumber, i.InstallmentPublicIds, i.AdvanceNoticeAccepted, i.MiscConceptPublicId, i.CrossDocument, i.InvoicePublicId, i.ItemId, i.PendingTarget,
                i.LoadObligationPublicId, i.ProductReference))
            .ToList();
        return await new UpdateCashOperationItemsCommandHandler(db, alcance, obligaciones, cuentas, carga, contabilidad, parametros)
            .Handle(new UpdateCashOperationItemsCommand(request.OperationPublicId, [.. conservados, .. exigibles]) { OperationKey = request.OperationKey }, ct);
    }
}
