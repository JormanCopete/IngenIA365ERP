using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Accounting;

// Feature 015, US5 (T0178; contracts/contabilidad.md §3; FR-064, FR-141): la anulación, el reverso del módulo y los saldos a favor. En línea, en el período abierto
// (FechaYModoAsync) y enlazados al hecho original. Sin guardar.
public sealed partial class EmisionDeRecaudo
{
    public const string ClaseAnulacion = "ReceiptVoiding";
    public const string ClaseReversoDelModulo = "ModuleReversal";
    public const string ClaseSaldoAFavor = "PayerCreditBalance";
    public const string ClaseDevolucionDeCartera = "LendingCreditBalanceRefund";

    /// <summary>Los tipos de documento del origen (el emisor los exige en un origen <c>Document</c>): el reverso del módulo y el saldo a favor (nuevo).</summary>
    public const string TipoReversoDelModulo = "RMOD";
    public const string TipoSaldoAFavor = "SAF";

    /// <summary>El hecho original de un recibo (su <c>ReciboEmitido</c>), para enlazar el reverso.</summary>
    public async Task<OriginalFactRefV1> HechoDelReciboAsync(Receipt recibo, CancellationToken ct)
    {
        var m = await db.IntegrationMessages.AsNoTracking().Where(x => x.OriginPublicId == recibo.PublicId && x.Type == ReciboEmitidoV1.Type)
            .Select(x => new { x.PublicId, x.Type }).FirstOrDefaultAsync(ct);
        return new OriginalFactRefV1 { MessageId = m?.PublicId ?? Guid.Empty, Type = ReciboEmitidoV1.Type, OriginPublicId = recibo.PublicId, Number = recibo.Number };
    }

    /// <summary>
    /// <c>ReciboAnulado</c>: por cada porción reversada, débito al puente del módulo (o al concepto vario) y crédito al rol del medio —o a
    /// <c>SaldoAFavorDePagadores</c> si el dinero ya no está en la sesión (<paramref name="aSaldoAFavor"/> de la porción)—.
    /// </summary>
    public async Task<IntegrationMessage> ReciboAnuladoAsync(ReceiptVoiding anulacion, Receipt recibo, IReadOnlyList<(ReceiptApplicationPortion Porcion,
        ReceiptApplication Aplicacion, ReceiptMeans Medio, bool ASaldoAFavor)> porciones, bool sesionCerrada, string codigoDelTipo, CancellationToken ct)
    {
        var lineas = new List<FactLineV1>();
        var conceptos = porciones.Where(p => p.Aplicacion.MiscConceptId is not null).Select(p => p.Aplicacion.MiscConceptId!.Value).Distinct().ToList();
        var codigos = await db.MiscConcepts.AsNoTracking().Where(c => conceptos.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Code, ct);
        var pagador = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == recibo.PayerPersonId).Select(p => p.PublicId).FirstAsync(ct);
        foreach (var (p, a, medio, aSaldo) in porciones)
        {
            var dim = recibo.CashBoxId is { } caja ? await DimensionesDeCajaAsync(caja, medio.PaymentMeansId, ct) : await DimensionesDeOficinaAsync(recibo.BranchId, ct);
            if (a.Target == ApplicationTarget.MiscConcept)
                lineas.Add(Linea(RolesDeRecaudo.ConceptoVario, RolesDeRecaudo.Debito, p.Amount, dim with { MiscConceptCode = codigos.GetValueOrDefault(a.MiscConceptId!.Value) },
                    cruce: anulacion.Number));
            else if (a.Target == ApplicationTarget.PendingApplication)
                // Feature 015, R1 (T0363): el pendiente se reconoció contra RecaudoPendienteDeAplicar; la anulación lo deshace contra ese rol.
                lineas.Add(Linea(RolesDeRecaudo.RecaudoPendienteDeAplicar, RolesDeRecaudo.Debito, p.Amount, dim with { TargetModule = ModulosDeOrigen.Cartera },
                    await PersonaPublicaAsync(a.BeneficiaryPersonId, ct), anulacion.Number));
            else
                lineas.Add(Linea(RolesDeRecaudo.PuenteDelModulo, RolesDeRecaudo.Debito, p.Amount, dim with
                {
                    TargetModule = ModulosDeOrigen.Cartera, TargetNature = Naturaleza(a.Target, null),
                }, cruce: anulacion.Number));
            lineas.Add(aSaldo
                ? Linea(RolesDeRecaudo.SaldoAFavorDePagadores, RolesDeRecaudo.Credito, p.Amount, dim, pagador, anulacion.Number)
                : Linea(RolDelMedio(medio.MeansClass), RolesDeRecaudo.Credito, p.Amount, dim, cruce: anulacion.Number));
        }
        var contenido = new ReciboAnuladoV1
        {
            VoidingPublicId = anulacion.PublicId, Number = anulacion.Number, ReceiptPublicId = recibo.PublicId, ReceiptNumber = recibo.Number,
            OriginalFact = await HechoDelReciboAsync(recibo, ct), VoidedOn = DateOnly.FromDateTime(anulacion.DecidedAt ?? anulacion.RequestedAt),
            SessionClosed = sesionCerrada, Reason = anulacion.Reason, ApprovedBy = await UsuarioAsync(ct), Lines = lineas,
        };
        return await EmitirAsync(contenido, anulacion.PublicId, ClaseAnulacion, codigoDelTipo, anulacion.Number, contenido.VoidedOn, recibo.BranchId, pagador, ct);
    }

    private async Task<Guid?> PersonaPublicaAsync(int personaId, CancellationToken ct) =>
        await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == personaId).Select(p => (Guid?)p.PublicId).FirstOrDefaultAsync(ct);

    /// <summary><c>AplicacionReversadaPorModulo</c> con la causa confirmada.</summary>
    public async Task<IntegrationMessage> AplicacionReversadaPorModuloAsync(ModuleReversal reverso, Receipt recibo, ReceiptApplication aplicacion, ReceiptMeans? medio,
        DateOnly fecha, CancellationToken ct)
    {
        var dim = recibo.CashBoxId is { } caja ? await DimensionesDeCajaAsync(caja, medio?.PaymentMeansId, ct) : await DimensionesDeOficinaAsync(recibo.BranchId, ct);
        var pagador = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == recibo.PayerPersonId).Select(p => p.PublicId).FirstAsync(ct);
        var recibido = reverso.Cause == ModuleReversalCause.MoneyReceived;
        var contenido = new AplicacionReversadaPorModuloV1
        {
            ModuleReversalPublicId = reverso.PublicId, ReceiptPublicId = recibo.PublicId, ReceiptNumber = recibo.Number, OriginalFact = await HechoDelReciboAsync(recibo, ct),
            Module = reverso.Module, ModuleReversalNumber = reverso.ModuleReversalNumber, Cause = reverso.Cause.ToString(), Amount = reverso.Amount, ConfirmedOn = fecha,
            ConfirmedBy = await UsuarioAsync(ct),
            Lines =
            [
                Linea(RolesDeRecaudo.PuenteDelModulo, RolesDeRecaudo.Debito, reverso.Amount, dim with
                {
                    TargetModule = ModulosDeOrigen.Cartera, TargetNature = Naturaleza(aplicacion.Target, null),
                }, cruce: reverso.ModuleReversalNumber),
                recibido
                    ? Linea(RolesDeRecaudo.SaldoAFavorDePagadores, RolesDeRecaudo.Credito, reverso.Amount, dim, pagador, recibo.Number)
                    : Linea(RolDelMedio(medio?.MeansClass ?? PaymentMeansClass.Cash), RolesDeRecaudo.Credito, reverso.Amount, dim, cruce: recibo.Number),
            ],
        };
        return await EmitirAsync(contenido, reverso.PublicId, ClaseReversoDelModulo, TipoReversoDelModulo, reverso.ModuleReversalNumber, fecha, recibo.BranchId, pagador, ct);
    }

    /// <summary><c>SaldoAFavorDePagadorResuelto</c>: devuelto por orden de pago o aplicado con un recibo nuevo.</summary>
    public async Task<IntegrationMessage> SaldoAFavorDePagadorResueltoAsync(PayerCreditBalance saldo, PayerCreditBalanceMovement movimiento, DateOnly fecha,
        CancellationToken ct)
    {
        var dim = await DimensionesDeOficinaAsync(saldo.BranchId, ct);
        var pagador = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == saldo.PersonId).Select(p => p.PublicId).FirstAsync(ct);
        var porOrden = movimiento.Kind == PayerCreditBalanceMovementKind.RefundedByOrder;
        // US7 (fase 12): el saldo de una pagaduría vive en SaldoDePagadurias.
        var rolDelSaldo = saldo.Kind == PayerCreditBalanceKind.Paymaster ? RolesDeRecaudo.SaldoDePagadurias : RolesDeRecaudo.SaldoAFavorDePagadores;
        var contenido = new SaldoAFavorDePagadorResueltoV1
        {
            CreditBalancePublicId = saldo.PublicId, PayerPersonPublicId = pagador, Form = movimiento.Kind.ToString(), Amount = movimiento.Amount,
            DocumentPublicId = movimiento.DocumentPublicId, DocumentNumber = movimiento.DocumentNumber, ResolvedOn = fecha, ResolvedBy = await UsuarioAsync(ct),
            Lines =
            [
                Linea(rolDelSaldo, RolesDeRecaudo.Debito, movimiento.Amount, dim, pagador, saldo.OriginDocumentNumber),
                porOrden
                    ? Linea(RolesDeRecaudo.PuenteDeDevolucion, RolesDeRecaudo.Credito, movimiento.Amount, dim, pagador, movimiento.DocumentNumber)
                    : Linea(RolesDeRecaudo.PuenteDelModulo, RolesDeRecaudo.Credito, movimiento.Amount, dim with { TargetModule = ModulosDeOrigen.Cartera },
                        cruce: movimiento.DocumentNumber),
            ],
        };
        return await EmitirAsync(contenido, movimiento.PublicId, ClaseSaldoAFavor, TipoSaldoAFavor, saldo.OriginDocumentNumber, fecha, saldo.BranchId, pagador, ct);
    }

    /// <summary><c>SaldoAFavorDeCarteraDevuelto</c>: Cartera resolvió el saldo por el recaudador y Recaudo lo devuelve con una orden de pago.</summary>
    public async Task<IntegrationMessage> SaldoAFavorDeCarteraDevueltoAsync(SaldoAFavorDeCarteraDevueltoV1 contenido, int branchId, CancellationToken ct)
    {
        var dim = await DimensionesDeOficinaAsync(branchId, ct);
        var completo = contenido with
        {
            RegisteredBy = await UsuarioAsync(ct),
            Lines =
            [
                Linea(RolesDeRecaudo.PuenteDelModulo, RolesDeRecaudo.Debito, contenido.Amount, dim with { TargetModule = ModulosDeOrigen.Cartera, TargetNature = "Loan" },
                    contenido.BeneficiaryPersonPublicId, contenido.LendingResolutionNumber),
                Linea(RolesDeRecaudo.PuenteDeDevolucion, RolesDeRecaudo.Credito, contenido.Amount, dim, contenido.BeneficiaryPersonPublicId, contenido.PaymentOrderNumber),
            ],
        };
        return await EmitirAsync(completo, contenido.PaymentOrderPublicId, ClaseDevolucionDeCartera, TipoSaldoAFavor, contenido.PaymentOrderNumber, contenido.RefundedOn, branchId,
            contenido.BeneficiaryPersonPublicId, ct);
    }
}
