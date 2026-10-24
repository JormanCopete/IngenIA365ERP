using System.Globalization;
using System.Text.Json.Serialization;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.Accounting;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Sessions;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Entities.Receipts.Sessions;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Imputacion;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.CashDesk;

/// <summary>
/// <c>POST /api/receipts/cash-operations/{id}/confirm</c> (<c>Receipts.Receipt.Register</c>): confirma la operación —el recibo, sus aplicaciones por los
/// contratos y sus hechos— todo o nada (FR-042). Con la misma <c>Idempotency-Key</c> y el mismo cuerpo devuelve el recibo original; tras
/// <c>DistributionChanged</c> o un 428 se repite con la misma clave (y <c>X-Approval-Request</c>). (nuevo)
/// </summary>
public sealed record ConfirmCashOperationCommand(Guid OperationPublicId) : IRequest<Result<ReceiptDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }

    [JsonIgnore] public Guid? ApprovalRequestPublicId { get; init; }
}

public sealed class ConfirmCashOperationCommandHandler(ConfirmacionDeRecibo confirmacion) : IRequestHandler<ConfirmCashOperationCommand, Result<ReceiptDto>>
{
    public Task<Result<ReceiptDto>> Handle(ConfirmCashOperationCommand request, CancellationToken ct) =>
        confirmacion.ConfirmarAsync(request.OperationPublicId, request.OperationKey, request.ApprovalRequestPublicId, ct);
}

/// <summary>
/// El flujo único que confirma un recibo de caja (feature 015, T0115; R-07, R-08; FR-001, FR-042, FR-043, FR-047, FR-094, FR-170, FR-172, FR-188, FR-192),
/// el único camino del dinero que entra por la caja. Dentro de la transacción de <c>IdempotencyBehavior</c>:
/// <list type="number">
/// <item><b>validación</b>: la operación en preparación en una sesión abierta, los ítems con su elección del deudor y la aceptación del aviso, la imputación
/// que cuadra, sin excedente, la fecha valor (anterior con motivo, soporte, tope de días y aprobador presente) y la declaración de origen de fondos;</item>
/// <item><b>listas</b> (<see cref="IListasDeRecaudo"/>, que completa la fase 15);</item>
/// <item><b>validación previa contable</b> (<see cref="IContabilidadParaRecaudo.EvaluarAsync"/>; sin contabilidad iniciada pasa);</item>
/// <item><b>cerrojos</b> propios en su orden: configuración, día del punto, sesión, pagadores;</item>
/// <item><b>numeración</b> por <see cref="NumeradorDeRecaudo"/>, <b>antes de los contratos</b> porque el contrato guarda el número del recibo (decisión del
/// dueño del 2026-10-23); en la misma transacción un rechazo no consume número;</item>
/// <item><b>contratos</b>: <c>AplicarAsync</c> interactivo con todas las porciones de créditos y después <c>ConsignarAsync</c> con todas las de cuentas
/// (obligaciones antes que cuentas, FR-170), cada porción con su clave <c>REC:{recibo:N}:{aplicación:N}:{clase}</c>; un rechazo es
/// <c>Receipts.Receipt.ItemRejected</c> con <c>data.items[]</c> (o <c>DistributionChanged</c> con <c>data.simulations[]</c>) y nada queda;</item>
/// <item><b>hechos</b>: <c>ReciboEmitido</c> por porción;</item>
/// <item>el recibo toma el <c>PublicId</c> de la operación.</item>
/// </list>
/// Guarda por pasos dentro de la misma transacción (las filas hijas necesitan el <c>Id</c> de su padre; no hay navegaciones): todo o nada igual (DT §2.7).
/// (nuevo)
/// </summary>
public sealed class ConfirmacionDeRecibo(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    IDateTimeService reloj,
    ICerrojoDeRecaudo cerrojo,
    NumeradorDeRecaudo numerador,
    ParametrosDeCaja parametros,
    AprobacionPresenteEnCaja aprobacion,
    IContabilidadParaRecaudo contabilidad,
    IContratoDeObligaciones obligaciones,
    IContratoDeCuentas cuentas,
    IListasDeRecaudo listas,
    EmisionDeRecaudo emision,
    IAlertas alertas)
{
    public const string CodigoDistribucionCambiadaDeCartera = "Lending.Payment.DistributionChanged";
    public const string PermisoDeFechaAnterior = "Receipts.Receipt.Backdate";

    public async Task<Result<ReceiptDto>> ConfirmarAsync(Guid operacionPublicId, Guid clave, Guid? aprobacionPublicId, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } cajero) return Result.Failure<ReceiptDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        if (await OperacionesDeCaja.VisibleAsync(db, alcance, operacionPublicId, seguir: true, ct) is not { } v)
            return Result.Failure<ReceiptDto>(ErroresDeRecaudo.NoEncontrado("La operación de caja"));
        var (operacion, sesion) = v;
        switch (operacion.Status)
        {
            case CashOperationState.Confirmed when operacion.ReceiptId is { } ya:
                return Result.Success(await RecibosDeCaja.DtoAsync(db, await db.IssuedReceipts.AsNoTracking().FirstAsync(r => r.Id == ya, ct), ct));
            case CashOperationState.Blocked:
                return Result.Failure<ReceiptDto>(ErroresDeRecaudo.ListaVinculante());
            case CashOperationState.Withheld:
                return Result.Failure<ReceiptDto>(ErroresDeRecaudo.OperacionRetenida());
            case not CashOperationState.Preparing:
                return Result.Failure<ReceiptDto>(ErroresDeRecaudo.OperacionNoEnPreparacion());
        }
        var operable = SesionesDeCaja.Operable(sesion);
        if (operable.IsFailure) return Result.Failure<ReceiptDto>(operable.Error);
        var hoy = reloj.HoyLocal;
        var sellados = ValoresSelladosDeLaSesion.De(sesion);
        var items = OperacionesDeCaja.Items(operacion);
        var medios = OperacionesDeCaja.Medios(operacion);
        if (items.Count == 0) return Result.Failure<ReceiptDto>(ErroresDeRecaudo.OperacionSinItems());
        if (medios.Count == 0) return Result.Failure<ReceiptDto>(ErroresDeRecaudo.MediosNoCuadran());

        // 1. Validación.
        foreach (var i in items)
        {
            if (i.LoanModality == ModalidadDePago.ExtraordinaryPrincipal && i.Choice is null) return Result.Failure<ReceiptDto>(ErroresDeRecaudo.EleccionRequerida(i.ItemId));
            if (i.AdvanceWithoutPrincipal && !i.AdvanceNoticeAccepted) return Result.Failure<ReceiptDto>(ErroresDeRecaudo.AvisoNoAceptado(i.ItemId));
            if (i.SimulatedSurplus is > 0m) return Result.Failure<ReceiptDto>(ErroresDeRecaudo.ExcedenteSinAsignar(i.SimulatedSurplus.Value));
        }
        var imputacion = OperacionesDeCaja.Imputar(items, medios, sellados, OrdenDeImputacion.InmediatosPrimeroChequeAlFinal, OperacionesDeCaja.Imputacion(operacion));
        if (imputacion.IsFailure) return Result.Failure<ReceiptDto>(imputacion.Error);
        var porcionesPedidas = OperacionesDeCaja.Porciones(imputacion.Value);
        var vueltas = imputacion.Value.Vueltas;
        var efectivo = OperacionesDeCaja.Efectivo(medios, sellados) - vueltas;
        if (efectivo > 0m && sellados.CashBlockCap is { } tope && await SesionesDeCaja.EfectivoAsync(db, sesion, ct) + efectivo > tope)
            return Result.Failure<ReceiptDto>(ErroresDeRecaudo.TopeDeBloqueo(tope));

        var insumos = new
        {
            operacion.PayerPersonId, operacion.BeneficiaryPersonId, operacion.DeliveredByPersonId, operacion.ValueDate,
            Items = items.Select(i => new { i.ItemId, i.Target, i.ProductPublicId, i.LoanModality, i.AccountModality, i.Choice, i.InstallmentNumber, i.Amount,
                i.MiscConceptPublicId, i.CrossDocument, i.AdvanceNoticeAccepted }),
            Means = medios.Select(m => new { m.MeansId, m.PaymentMeansPublicId, m.Amount, m.Reference, m.VoucherNumber }),
            Portions = porcionesPedidas,
        };
        var huella = HuellaDeRecibo.Calcular(insumos);

        Guid? aprobacionDeFecha = null;
        if (operacion.ValueDate > hoy) return Result.Failure<ReceiptDto>(ErroresDeRecaudo.MovimientoInvalido("La fecha valor no puede ser futura."));
        if (operacion.ValueDate < hoy)
        {
            var maximo = await parametros.EnteroAsync(ParametrosDeRecaudoYTesoreria.CajaFechaValorAnteriorMaxDias, hoy, ct) ?? 0;
            if (hoy.DayNumber - operacion.ValueDate.DayNumber > maximo) return Result.Failure<ReceiptDto>(ErroresDeRecaudo.FechaAnteriorNoAdmitida(maximo));
            if (string.IsNullOrWhiteSpace(operacion.BackdateReason) || operacion.BackdateSupportAttachmentPublicId is null)
                return Result.Failure<ReceiptDto>(ErroresDeRecaudo.FechaAnteriorConAprobacion());
            var obtenida = await aprobacion.ExigirAsync(new PedidoDeAprobacionPresente(ApprovalSubjects.ReceiptBackdate, operacion.PublicId,
                $"Recibo con fecha valor {operacion.ValueDate:dd/MM/yyyy}", items.Sum(i => i.Amount), hoy, huella, PermisoDeFechaAnterior, [cajero],
                aprobacionPublicId), ct);
            if (obtenida.IsFailure) return Result.Failure<ReceiptDto>(obtenida.Error);
            aprobacionDeFecha = obtenida.Value.ApprovalRequestPublicId;
        }

        var umbral = await parametros.DecimalAsync(ParametrosDeRecaudoYTesoreria.SarlaftUmbralDeclaracionOrigenFondos, hoy, ct);
        var exigeDeclaracion = (umbral is { } u && efectivo >= u) || items.Any(i => i.RequiresSourceOfFunds);
        if (exigeDeclaracion && operacion.SourceOfFundsDeclarationId is null) return Result.Failure<ReceiptDto>(ErroresDeRecaudo.DeclaracionRequerida(efectivo));

        // 2. Listas.
        var personasDeLaOperacion = new[] { operacion.PayerPersonId, operacion.DeliveredByPersonId ?? 0, operacion.BeneficiaryPersonId ?? 0 }
            .Where(x => x != 0).Distinct().ToList();
        var titulares = await db.People.AsNoTracking().Where(p => items.Select(i => i.BeneficiaryPersonPublicId).Contains(p.PublicId))
            .ToDictionaryAsync(p => p.PublicId, p => p.Id, ct);
        personasDeLaOperacion = personasDeLaOperacion.Union(titulares.Values).Distinct().OrderBy(x => x).ToList();
        var resultadoDeListas = await listas.EvaluarAsync(personasDeLaOperacion, operacion.PublicId, ct);
        if (resultadoDeListas == ScreeningOutcomeR.BindingMatch) return Result.Failure<ReceiptDto>(ErroresDeRecaudo.ListaVinculante());

        // 3. Validación previa contable.
        var evaluacion = await contabilidad.EvaluarAsync(
            [new CombinacionDeHecho(ClavesDeRecaudo.Codigo, IngenIA365ERP.Application.Common.Integration.Contracts.Receipts.ReciboEmitidoV1.Type, new Dictionary<string, string>(), operacion.ValueDate)], ct);
        if (!evaluacion.Postable && evaluacion.Policy == "Bloquear")
            return Result.Failure<ReceiptDto>(ErroresDeRecaudo.NoContabilizable(evaluacion.Missing.Cast<object>().ToList()));

        // 4. Cerrojos propios en su orden.
        var (oficina, punto) = await SesionesDeCaja.OficinaYPuntoAsync(db, sesion, ct);
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.DiaDelPunto, TablasDeRecaudo.DiasDelPunto, [sesion.CollectionPointDayId], exclusivo: false, ct);
        await cerrojo.BloquearAsync(NivelDeRecaudo.Sesion, TablasDeRecaudo.Sesiones, [sesion.Id], ct: ct);
        await cerrojo.BloquearPagadoresAsync(personasDeLaOperacion, ct);

        // 5. Numeración, antes de los contratos.
        var (tipo, prefijo) = await SesionesDeCaja.TipoYPrefijoAsync(db, ReceiptDocumentKind.CashReceipt, punto, ct);
        var numero = await numerador.AsignarAsync(tipo.Code, prefijo, ct);

        // El recibo y sus filas.
        var recibo = new Receipt
        {
            PublicId = operacion.PublicId,
            Number = numero,
            DocumentTypeId = tipo.Id,
            Channel = ReceiptChannel.CashDesk,
            BranchId = oficina,
            CollectionPointId = punto,
            CashBoxId = sesion.CashBoxId,
            CashSessionId = sesion.Id,
            CashierUserId = cajero,
            PayerPersonId = operacion.PayerPersonId,
            DeliveredByPersonId = operacion.DeliveredByPersonId,
            ValueDate = operacion.ValueDate,
            ProposedAccountingDate = operacion.ValueDate,
            RegisteredAt = reloj.UtcNow,
            TotalApplied = items.Sum(i => i.Amount),
            TotalMeans = medios.Sum(m => m.Amount),
            ChangeGiven = vueltas,
            SourceOfFundsDeclarationId = operacion.SourceOfFundsDeclarationId,
            BackdatedApprovalRequestPublicId = aprobacionDeFecha,
            BackdatedReason = operacion.ValueDate < hoy ? operacion.BackdateReason : null,
            PayerIsThirdParty = titulares.Values.Any(t => t != operacion.PayerPersonId),
            InputsHash = huella,
            OperationKey = clave,
            VerificationCode = CloseCollectionPointDayCommandHandler.CodigoDeVerificacion(),
        };
        db.IssuedReceipts.Add(recibo);
        await db.SaveChangesAsync(ct);

        var filasDeMedios = new Dictionary<Guid, ReceiptMeans>();
        var ordinal = 0;
        foreach (var m in medios)
        {
            var sellado = sellados.Means.First(x => x.PublicId == m.PaymentMeansPublicId);
            var terminal = m.CardTerminalPublicId is { } t
                ? await db.CardTerminals.AsNoTracking().Where(x => x.PublicId == t).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct)
                : null;
            var fila = new ReceiptMeans
            {
                ReceiptId = recibo.Id, Ordinal = ++ordinal, PaymentMeansId = sellado.PaymentMeansId, MeansClass = sellado.Class, Amount = m.Amount,
                Reference = m.Reference?.Trim(), CardTerminalId = terminal, TerminalBatch = m.TerminalBatch?.Trim(), ApprovalCode = m.ApprovalCode?.Trim(),
                VoucherNumber = string.IsNullOrWhiteSpace(m.VoucherNumber) ? null : m.VoucherNumber.Trim(),
            };
            db.ReceiptMeans.Add(fila);
            filasDeMedios[m.MeansId!.Value] = fila;
        }
        var conceptos = items.Where(i => i.MiscConceptPublicId is not null).Select(i => i.MiscConceptPublicId!.Value).ToList();
        var idsDeConceptos = await db.MiscConcepts.AsNoTracking().Where(c => conceptos.Contains(c.PublicId)).ToDictionaryAsync(c => c.PublicId, c => c.Id, ct);
        var filasDeItems = new Dictionary<Guid, ReceiptApplication>();
        ordinal = 0;
        foreach (var i in items)
        {
            var fila = new ReceiptApplication
            {
                ReceiptId = recibo.Id, Ordinal = ++ordinal, Target = i.Target, ProductPublicId = i.ProductPublicId, ProductNumber = i.ProductNumber,
                BeneficiaryPersonId = titulares.GetValueOrDefault(i.BeneficiaryPersonPublicId, operacion.PayerPersonId),
                LoanModality = (int?)i.LoanModality, AccountModality = (int?)i.AccountModality, PrepaymentChoice = (int?)i.Choice,
                InstallmentNumber = i.InstallmentNumber,
                InstallmentPublicIdsJson = i.InstallmentPublicIds is { Count: > 0 } c ? OperacionesDeCaja.Json(c) : null,
                AdvanceNoticeAccepted = i.AdvanceNoticeAccepted,
                MiscConceptId = i.MiscConceptPublicId is { } mc ? idsDeConceptos.GetValueOrDefault(mc) : null,
                CrossDocumentReference = i.CrossDocument, InvoicePublicId = i.InvoicePublicId, Amount = i.Amount,
            };
            db.ReceiptApplications.Add(fila);
            filasDeItems[i.ItemId] = fila;
        }
        await db.SaveChangesAsync(ct);

        var porciones = new List<(ReceiptApplicationPortion Fila, ItemDeCajaDto Item, MedioDeCajaDto Medio)>();
        foreach (var grupo in porcionesPedidas.GroupBy(p => p.ItemId))
        {
            var item = items.First(i => i.ItemId == grupo.Key);
            var porClase = new Dictionary<PaymentMeansClass, int>();
            foreach (var p in grupo)
            {
                var medio = medios.First(m => m.MeansId == p.MeansId);
                var fila = filasDeMedios[p.MeansId];
                var orden = porClase[fila.MeansClass] = porClase.GetValueOrDefault(fila.MeansClass) + 1;
                var porcion = new ReceiptApplicationPortion
                {
                    ReceiptApplicationId = filasDeItems[item.ItemId].Id, ReceiptMeansId = fila.Id, MeansClass = fila.MeansClass, Amount = p.Amount,
                    IdempotencyKey = ClavesDeRecaudo.Porcion(recibo.PublicId, filasDeItems[item.ItemId].PublicId, fila.MeansClass, orden),
                };
                db.ReceiptApplicationPortions.Add(porcion);
                porciones.Add((porcion, item, medio));
            }
        }
        await db.SaveChangesAsync(ct);

        // 6. Contratos: obligaciones antes que cuentas.
        var oficinaPublicId = await db.Branches.AsNoTracking().Where(b => b.Id == oficina).Select(b => (Guid?)b.PublicId).FirstOrDefaultAsync(ct);
        var pagadorPublicId = await db.People.AsNoTracking().Where(p => p.Id == operacion.PayerPersonId).Select(p => p.PublicId).FirstAsync(ct);
        var declaracion = operacion.SourceOfFundsDeclarationId is { } dId
            ? await db.SourceOfFundsDeclarations.AsNoTracking().Where(d => d.Id == dId).Select(d => (Guid?)d.SupportAttachmentPublicId).FirstOrDefaultAsync(ct)
            : null;
        ReferenciaDeRecibo Referencia(MedioDeCajaDto m) => new(numero, hoy, oficinaPublicId, m.Reference, nameof(ReceiptDocumentKind.CashReceipt));
        var rechazos = new List<object>();
        var simulaciones = new List<object>();

        var deCreditos = porciones.Where(p => p.Item.Target == ApplicationTarget.Loan).ToList();
        if (deCreditos.Count > 0)
        {
            var pagos = deCreditos.Select(p => new PagoAAplicar(p.Fila.IdempotencyKey, p.Item.ProductPublicId!.Value, p.Item.LoanModality!.Value, p.Fila.Amount,
                operacion.ValueDate, ClavesDeRecaudo.ClaseDelContrato(p.Fila.MeansClass), pagadorPublicId, Referencia(p.Medio), p.Item.Choice, p.Item.InstallmentNumber,
                UnaPorcion(porciones, p.Item) ? p.Item.SimulationId : null)).ToList();
            var respuesta = await obligaciones.AplicarAsync(new SolicitudDeAplicacion(ClavesDeRecaudo.Recaudador, FormaDeAplicacion.Interactive, pagos, []), ct);
            for (var k = 0; k < deCreditos.Count; k++)
            {
                var r = k < respuesta.Payments.Count ? respuesta.Payments[k] : null;
                if (r is null || r.Result == ResultadoDelContrato.Rejected || r.Value is null)
                {
                    Registrar(r?.Code, r?.Message, r?.Data, deCreditos[k].Item.ItemId, rechazos, simulaciones);
                    continue;
                }
                var (fila, item, _) = deCreditos[k];
                fila.ModuleOperationPublicId = r.Value.OperationPublicId;
                fila.ModuleOperationNumber = r.Value.Number;
                var aplicacion = filasDeItems[item.ItemId];
                aplicacion.ProductNumber ??= r.Value.ObligationNumber;
                aplicacion.ModuleVoucherPublicId ??= r.Value.OperationPublicId;
                aplicacion.ModuleVoucherNumber ??= r.Value.Number;
                if (r.Value.FinalityUntil is { } firmeza && (aplicacion.FinalityUntil is null || firmeza > aplicacion.FinalityUntil)) aplicacion.FinalityUntil = firmeza;
            }
        }

        var deCuentas = porciones.Where(p => p.Item.Target == ApplicationTarget.Account).ToList();
        if (deCuentas.Count > 0 && rechazos.Count == 0 && simulaciones.Count == 0)
        {
            var consignaciones = deCuentas.Select(p => new ConsignacionAAplicar(p.Fila.IdempotencyKey, p.Item.ProductPublicId!.Value,
                p.Item.AccountModality ?? ModalidadDeConsignacion.Free, p.Fila.Amount, operacion.ValueDate, ClavesDeRecaudo.ClaseDelContrato(p.Fila.MeansClass),
                pagadorPublicId, Referencia(p.Medio), p.Item.InstallmentPublicIds, declaracion, null,
                UnaPorcion(porciones, p.Item) ? p.Item.SimulationId : null)).ToList();
            var respuesta = await cuentas.ConsignarAsync(new SolicitudDeConsignacion(ClavesDeRecaudo.Recaudador, FormaDeAplicacion.Interactive, consignaciones, []), ct);
            for (var k = 0; k < deCuentas.Count; k++)
            {
                var r = k < respuesta.Deposits.Count ? respuesta.Deposits[k] : null;
                if (r is null || r.Result == ResultadoDelContrato.Rejected || r.Value is null)
                {
                    Registrar(r?.Code, r?.Message, r?.Data, deCuentas[k].Item.ItemId, rechazos, simulaciones);
                    continue;
                }
                var (fila, item, _) = deCuentas[k];
                fila.ModuleOperationPublicId = r.Value.OperationPublicId;
                fila.ModuleOperationNumber = r.Value.Number;
                var aplicacion = filasDeItems[item.ItemId];
                aplicacion.ProductNumber ??= r.Value.AccountNumber;
                aplicacion.ModuleVoucherPublicId ??= r.Value.OperationPublicId;
                aplicacion.ModuleVoucherNumber ??= r.Value.Number;
            }
        }
        if (rechazos.Count > 0) return Result.Failure<ReceiptDto>(ErroresDeRecaudo.ItemRechazado(rechazos));
        if (simulaciones.Count > 0) return Result.Failure<ReceiptDto>(ErroresDeRecaudo.DistribucionCambiada(simulaciones));

        // 7. Hechos.
        var naturalezas = await NaturalezasAsync(items, titulares, hoy, ct);
        await emision.ReciboEmitidoAsync(recibo, filasDeItems.Values.ToList(), filasDeMedios.Values.ToList(), porciones.Select(p => p.Fila).ToList(), naturalezas,
            sesion.PublicId, tipo.Code, ct);

        operacion.Status = CashOperationState.Confirmed;
        operacion.ReceiptId = recibo.Id;
        operacion.OperationKey = clave;
        operacion.ScreeningOutcome = resultadoDeListas;
        await db.SaveChangesAsync(ct);
        await SesionesDeCaja.ProyectarAsync(db, sesion, ct);
        await db.SaveChangesAsync(ct);

        await AvisarPagadorOcasionalAsync(operacion, efectivo, umbral, recibo, ct);
        return Result.Success(await RecibosDeCaja.DtoAsync(db, recibo, ct));
    }

    private static bool UnaPorcion(IEnumerable<(ReceiptApplicationPortion Fila, ItemDeCajaDto Item, MedioDeCajaDto Medio)> porciones, ItemDeCajaDto item) =>
        porciones.Count(p => p.Item.ItemId == item.ItemId) == 1;

    private static void Registrar(string? codigo, string? mensaje, IReadOnlyDictionary<string, object?>? datos, Guid item, List<object> rechazos,
        List<object> simulaciones)
    {
        if (codigo == CodigoDistribucionCambiadaDeCartera)
            simulaciones.Add(new { itemId = item, simulation = datos?.GetValueOrDefault("simulation") ?? datos });
        else
            rechazos.Add(new { itemId = item, code = codigo ?? "Receipts.Receipt.ContractUnavailable", message = mensaje ?? "El módulo no respondió." });
    }

    /// <summary>La naturaleza de cada cuenta de los ítems (para el puente del hecho), leída por el contrato de cuentas.</summary>
    private async Task<IReadOnlyDictionary<Guid, string>> NaturalezasAsync(IReadOnlyList<ItemDeCajaDto> items, IReadOnlyDictionary<Guid, int> titulares,
        DateOnly hoy, CancellationToken ct)
    {
        var resultado = new Dictionary<Guid, string>();
        foreach (var titular in items.Where(i => i.Target == ApplicationTarget.Account).Select(i => i.BeneficiaryPersonPublicId).Distinct())
        {
            var r = await cuentas.ConsultarAsync(new SolicitudDeConsultaDeCuentas(ClavesDeRecaudo.Recaudador, titular, hoy), ct);
            foreach (var c in r.Value?.Accounts ?? []) resultado[c.AccountPublicId] = c.Nature.ToString();
        }
        _ = titulares;
        return resultado;
    }

    /// <summary>El pagador que no es asociado y paga en efectivo sobre el umbral de la declaración: aviso al oficial (FR-177).</summary>
    private async Task AvisarPagadorOcasionalAsync(CashOperation operacion, decimal efectivo, decimal? umbral, Receipt recibo, CancellationToken ct)
    {
        if (umbral is not { } u || efectivo < u) return;
        var asociado = await db.People.AsNoTracking().Where(p => p.Id == operacion.PayerPersonId).Select(p => p.IsAssociate).FirstOrDefaultAsync(ct);
        if (asociado) return;
        await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.RecaudoPagadorOcasionalSobreUmbral, "Pagador ocasional sobre el umbral",
            string.Create(CultureInfo.InvariantCulture, $"El recibo {recibo.Number} lo pagó en efectivo ({efectivo:N0}) una persona que no es asociada."),
            "Receipt", recibo.PublicId, DedupKey: $"Recaudo.PagadorOcasionalSobreUmbral:{recibo.PublicId:N}", ModuloDeOrigen: "Receipts"), ct);
    }
}
