using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Receipts.Sessions;
using IngenIA365ERP.Domain.Entities.Receipts.Vaults;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Accounting;

// Feature 015, US2 (T0091; contracts/contabilidad.md §3): los cinco hechos de la caja, todos en línea, del tipo de comprobante sugerido MC (§8).
public sealed partial class EmisionDeRecaudo
{
    public const string ClaseMovimientoDeCaja = "CashMovement";
    public const string ClaseMovimientoDeBoveda = "VaultMovement";
    public const string ClaseArqueo = "CashCount";
    public const string ClaseAjuste = "CashSessionAdjustment";
    public const string ClaseSobrante = "CashSurplus";

    /// <summary>Tipos de origen del arqueo y del sobrante, que no tienen numeración propia (el emisor exige un tipo en todo origen <c>Document</c>; DT §2.7).</summary>
    public const string TipoArqueo = "ARQ";
    public const string TipoSobrante = "SOB";

    /// <summary>
    /// <c>MovimientoDeCajaRegistrado</c> de un movimiento de caja: abastecer (<c>Caja</c> ← <c>Boveda</c>), retirar a bóveda (<c>Boveda</c> ← <c>Caja</c>) o
    /// trasladar entre cajas (la de destino ← la de origen). La apertura y la custodia no emiten aquí (la custodia es de la fase 6). Sin guardar.
    /// </summary>
    public async Task<IntegrationMessage> MovimientoDeCajaAsync(CashMovement movimiento, CashSession sesion, CashSession? contraparte, Vault? boveda,
        DateOnly fecha, string codigoDelTipo, CancellationToken ct)
    {
        var caja = await DimensionesDeCajaAsync(sesion.CashBoxId, movimiento.FromMeansId, ct);
        var oficinaId = await OficinaDeLaCajaAsync(sesion.CashBoxId, ct);
        IReadOnlyList<FactLineV1> lineas = movimiento.Kind switch
        {
            CashMovementKindR.SupplyFromVault =>
            [
                Linea(RolesDeRecaudo.Caja, RolesDeRecaudo.Debito, movimiento.Amount, caja),
                Linea(RolesDeRecaudo.Boveda, RolesDeRecaudo.Credito, movimiento.Amount, await DimensionesDeOficinaAsync(boveda!.BranchId, ct)),
            ],
            CashMovementKindR.WithdrawToVault =>
            [
                Linea(RolesDeRecaudo.Boveda, RolesDeRecaudo.Debito, movimiento.Amount, await DimensionesDeOficinaAsync(boveda!.BranchId, ct)),
                Linea(RolesDeRecaudo.Caja, RolesDeRecaudo.Credito, movimiento.Amount, caja),
            ],
            CashMovementKindR.TransferBetweenBoxes =>
            [
                Linea(RolesDeRecaudo.Caja, RolesDeRecaudo.Debito, movimiento.Amount, await DimensionesDeCajaAsync(contraparte!.CashBoxId, movimiento.FromMeansId, ct)),
                Linea(RolesDeRecaudo.Caja, RolesDeRecaudo.Credito, movimiento.Amount, caja),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(movimiento), movimiento.Kind, "Ese movimiento no emite MovimientoDeCajaRegistrado."),
        };
        var contenido = new MovimientoDeCajaDeRecaudoRegistradoV1
        {
            MovementPublicId = movimiento.PublicId,
            Number = movimiento.Number,
            Kind = movimiento.Kind.ToString(),
            SessionPublicId = sesion.PublicId,
            CounterpartSessionPublicId = contraparte?.PublicId,
            VaultPublicId = boveda?.PublicId,
            Amount = movimiento.Amount,
            OperationDate = fecha,
            Reason = movimiento.Reason,
            RegisteredBy = await UsuarioAsync(ct),
            Lines = lineas,
        };
        return await EmitirAsync(contenido, movimiento.PublicId, ClaseMovimientoDeCaja, codigoDelTipo, movimiento.Number, fecha, oficinaId, null, ct);
    }

    /// <summary>
    /// <c>MovimientoDeCajaRegistrado</c> de un movimiento de bóveda sin caja par: la remesa que sale (<c>EfectivoEnTransito</c> ← <c>Boveda</c>, que en
    /// contabilidad.md §3 es <c>RemesaDespachada</c> y llega con la remesa) no emite aquí; sí lo hace la entrada de una remesa devuelta. El conteo y el
    /// billete retenido no mueven dinero entre cuentas y no emiten. Sin guardar.
    /// </summary>
    public async Task<IntegrationMessage> MovimientoDeBovedaAsync(VaultMovement movimiento, Vault boveda, DateOnly fecha, string codigoDelTipo, CancellationToken ct)
    {
        var oficina = await DimensionesDeOficinaAsync(boveda.BranchId, ct);
        var valor = Math.Abs(movimiento.Amount);
        IReadOnlyList<FactLineV1> lineas = movimiento.Amount >= 0m
            ? [Linea(RolesDeRecaudo.Boveda, RolesDeRecaudo.Debito, valor, oficina), Linea(RolesDeRecaudo.EfectivoEnTransito, RolesDeRecaudo.Credito, valor, oficina)]
            : [Linea(RolesDeRecaudo.EfectivoEnTransito, RolesDeRecaudo.Debito, valor, oficina), Linea(RolesDeRecaudo.Boveda, RolesDeRecaudo.Credito, valor, oficina)];
        var contenido = new MovimientoDeCajaDeRecaudoRegistradoV1
        {
            MovementPublicId = movimiento.PublicId,
            Number = movimiento.Number,
            Kind = movimiento.Kind.ToString(),
            VaultPublicId = boveda.PublicId,
            Amount = valor,
            OperationDate = fecha,
            Reason = movimiento.Reason ?? string.Empty,
            RegisteredBy = await UsuarioAsync(ct),
            Lines = lineas,
        };
        return await EmitirAsync(contenido, movimiento.PublicId, ClaseMovimientoDeBoveda, codigoDelTipo, movimiento.Number, fecha, boveda.BranchId, null, ct);
    }

    /// <summary>
    /// Feature 015, US19 (T0228): <c>RemesaDespachada</c> (RMD), la remesa de la bóveda al banco: débito <c>EfectivoEnTransito</c>, crédito <c>Boveda</c>, con la remesa de
    /// Tesorería en el contenido. Sin guardar.
    /// </summary>
    public async Task<IntegrationMessage> RemesaDespachadaAsync(VaultMovement movimiento, Vault boveda, Guid remesa, string numeroDeRemesa, Guid cuenta, DateOnly fecha,
        string codigoDelTipo, CancellationToken ct)
    {
        var oficina = await DimensionesDeOficinaAsync(boveda.BranchId, ct);
        var valor = Math.Abs(movimiento.Amount);
        var contenido = new RemesaDespachadaV1
        {
            MovementPublicId = movimiento.PublicId, Number = movimiento.Number, VaultPublicId = boveda.PublicId, RemittancePublicId = remesa, RemittanceNumber = numeroDeRemesa,
            OwnBankAccountPublicId = cuenta, Amount = valor, BagSeal = movimiento.BagSeal, DispatchedOn = fecha, RegisteredBy = await UsuarioAsync(ct),
            Lines =
            [
                Linea(RolesDeRecaudo.EfectivoEnTransito, RolesDeRecaudo.Debito, valor, oficina, cruce: numeroDeRemesa),
                Linea(RolesDeRecaudo.Boveda, RolesDeRecaudo.Credito, valor, oficina, cruce: numeroDeRemesa),
            ],
        };
        return await EmitirAsync(contenido, movimiento.PublicId, ClaseMovimientoDeBoveda, codigoDelTipo, movimiento.Number, fecha, boveda.BranchId, null, ct);
    }

    /// <summary><c>MedioReclasificado</c>: rol del medio de destino ← rol del medio de origen; el recibo no se toca (US2-7). Sin guardar.</summary>
    public async Task<IntegrationMessage> MedioReclasificadoAsync(CashMovement movimiento, CashSession sesion, Guid? recibo, DateOnly fecha, string codigoDelTipo,
        CancellationToken ct)
    {
        var origen = await DimensionesDeCajaAsync(sesion.CashBoxId, movimiento.FromMeansId, ct);
        var destino = await DimensionesDeCajaAsync(sesion.CashBoxId, movimiento.ToMeansId, ct);
        var contenido = new MedioReclasificadoV1
        {
            MovementPublicId = movimiento.PublicId,
            Number = movimiento.Number,
            SessionPublicId = sesion.PublicId,
            ReceiptPublicId = recibo,
            FromMeansCode = origen.MeansCode ?? string.Empty,
            ToMeansCode = destino.MeansCode ?? string.Empty,
            Amount = movimiento.Amount,
            OperationDate = fecha,
            Reason = movimiento.Reason,
            ApprovalRequestPublicId = movimiento.ApprovalRequestPublicId,
            RegisteredBy = await UsuarioAsync(ct),
            Lines =
            [
                Linea(RolDelMedio((PaymentMeansClass)(destino.MeansClass ?? 99)), RolesDeRecaudo.Debito, movimiento.Amount, destino),
                Linea(RolDelMedio((PaymentMeansClass)(origen.MeansClass ?? 99)), RolesDeRecaudo.Credito, movimiento.Amount, origen),
            ],
        };
        return await EmitirAsync(contenido, movimiento.PublicId, ClaseMovimientoDeCaja, codigoDelTipo, movimiento.Number, fecha,
            await OficinaDeLaCajaAsync(sesion.CashBoxId, ct), null, ct);
    }

    /// <summary>
    /// <c>DiferenciaDeArqueoAprobada</c> de una línea con diferencia: faltante a <c>CuentaPorCobrarCajero</c> (tercero el cajero) o <c>GastoFaltante</c> contra
    /// el rol del medio; sobrante del rol del medio a <c>SobrantesDeCaja</c>. Sin guardar.
    /// </summary>
    public async Task<IntegrationMessage> DiferenciaDeArqueoAsync(CashCountLine linea, CashSession sesion, Guid? cajeroPersonaPublicId, int aprobadorUserId,
        DateOnly fecha, CancellationToken ct)
    {
        var medio = await DimensionesDeCajaAsync(sesion.CashBoxId, linea.PaymentMeansId, ct);
        var oficinaId = await OficinaDeLaCajaAsync(sesion.CashBoxId, ct);
        var oficina = await DimensionesDeOficinaAsync(oficinaId, ct);
        var rolDelMedio = RolDelMedio((PaymentMeansClass)(medio.MeansClass ?? 99));
        var valor = Math.Abs(linea.Difference);
        IReadOnlyList<FactLineV1> lineas = linea.Treatment switch
        {
            CountTreatment.CashierReceivable =>
                [Linea(RolesDeRecaudo.CuentaPorCobrarCajero, RolesDeRecaudo.Debito, valor, oficina, cajeroPersonaPublicId), Linea(rolDelMedio, RolesDeRecaudo.Credito, valor, medio)],
            CountTreatment.Expense =>
                [Linea(RolesDeRecaudo.GastoFaltante, RolesDeRecaudo.Debito, valor, oficina), Linea(rolDelMedio, RolesDeRecaudo.Credito, valor, medio)],
            CountTreatment.Surplus =>
                [Linea(rolDelMedio, RolesDeRecaudo.Debito, valor, medio), Linea(RolesDeRecaudo.SobrantesDeCaja, RolesDeRecaudo.Credito, valor, oficina)],
            _ => throw new ArgumentOutOfRangeException(nameof(linea), linea.Treatment, "Una línea sin diferencia no emite."),
        };
        var contenido = new DiferenciaDeArqueoDeRecaudoAprobadaV1
        {
            CountLinePublicId = linea.PublicId,
            SessionPublicId = sesion.PublicId,
            MeansCode = medio.MeansCode ?? string.Empty,
            Expected = linea.Expected,
            Counted = linea.Counted,
            Difference = linea.Difference,
            Treatment = linea.Treatment.ToString(),
            CashierPersonPublicId = cajeroPersonaPublicId,
            OperationDate = fecha,
            Reason = linea.Reason,
            ApprovalRequestPublicId = linea.ApprovalRequestPublicId,
            ApprovedBy = await UsuarioAsync(aprobadorUserId, ct),
            Lines = lineas,
        };
        return await EmitirAsync(contenido, linea.PublicId, ClaseArqueo, TipoArqueo, sesion.PublicId.ToString("N")[..12], fecha, oficinaId, cajeroPersonaPublicId, ct);
    }

    /// <summary>
    /// <c>AjusteDeSesionRegistrado</c>: un ajuste que sube lo contado lleva el dinero del rol del medio a <c>SobrantesDeCaja</c>; uno que lo baja, de
    /// <c>GastoFaltante</c> al rol del medio. <paramref name="original"/> es el hecho de la diferencia que corrige, si lo hubo. Sin guardar.
    /// </summary>
    public async Task<IntegrationMessage> AjusteDeSesionAsync(CashSessionAdjustment ajuste, CashSession sesion, IntegrationMessage? original, DateOnly fecha,
        string codigoDelTipo, CancellationToken ct)
    {
        var medio = await DimensionesDeCajaAsync(sesion.CashBoxId, ajuste.PaymentMeansId, ct);
        var oficinaId = await OficinaDeLaCajaAsync(sesion.CashBoxId, ct);
        var oficina = await DimensionesDeOficinaAsync(oficinaId, ct);
        var rolDelMedio = RolDelMedio((PaymentMeansClass)(medio.MeansClass ?? 99));
        var valor = Math.Abs(ajuste.Amount);
        IReadOnlyList<FactLineV1> lineas = ajuste.Amount >= 0m
            ? [Linea(rolDelMedio, RolesDeRecaudo.Debito, valor, medio), Linea(RolesDeRecaudo.SobrantesDeCaja, RolesDeRecaudo.Credito, valor, oficina)]
            : [Linea(RolesDeRecaudo.GastoFaltante, RolesDeRecaudo.Debito, valor, oficina), Linea(rolDelMedio, RolesDeRecaudo.Credito, valor, medio)];
        var contenido = new AjusteDeSesionRegistradoV1
        {
            AdjustmentPublicId = ajuste.PublicId,
            Number = ajuste.Number,
            SessionPublicId = sesion.PublicId,
            Kind = ajuste.Kind.ToString(),
            MeansCode = medio.MeansCode ?? string.Empty,
            Amount = ajuste.Amount,
            OperationDate = fecha,
            Reason = ajuste.Reason,
            ApprovalRequestPublicId = ajuste.ApprovalRequestPublicId,
            Original = original is null ? null : new OriginalFactRefV1 { MessageId = original.PublicId, Type = original.Type, OriginPublicId = original.OriginPublicId, Number = original.OriginNumber ?? string.Empty },
            RegisteredBy = await UsuarioAsync(ct),
            Lines = lineas,
        };
        return await EmitirAsync(contenido, ajuste.PublicId, ClaseAjuste, codigoDelTipo, ajuste.Number, fecha, oficinaId, null, ct);
    }

    /// <summary><c>SobranteLlevadoAIngreso</c>: <c>IngresoSobrantes</c> ← <c>SobrantesDeCaja</c>. Sin guardar.</summary>
    public async Task<IntegrationMessage> SobranteAIngresoAsync(CashSurplus sobrante, CashCountLine linea, int oficinaId, int aprobadorUserId, DateOnly fecha,
        CancellationToken ct)
    {
        var oficina = await DimensionesDeOficinaAsync(oficinaId, ct);
        var contenido = new SobranteLlevadoAIngresoV1
        {
            SurplusPublicId = sobrante.PublicId,
            CountLinePublicId = linea.PublicId,
            Amount = sobrante.Amount,
            OperationDate = fecha,
            ApprovalRequestPublicId = sobrante.ApprovalRequestPublicId,
            ApprovedBy = await UsuarioAsync(aprobadorUserId, ct),
            Lines =
            [
                Linea(RolesDeRecaudo.SobrantesDeCaja, RolesDeRecaudo.Debito, sobrante.Amount, oficina),
                Linea(RolesDeRecaudo.IngresoSobrantes, RolesDeRecaudo.Credito, sobrante.Amount, oficina),
            ],
        };
        return await EmitirAsync(contenido, sobrante.PublicId, ClaseSobrante, TipoSobrante, sobrante.PublicId.ToString("N")[..12], fecha, oficinaId, null, ct);
    }

    private async Task<int> OficinaDeLaCajaAsync(int cashBoxId, CancellationToken ct) =>
        await (from c in db.CashBoxes.AsNoTracking()
               join p in db.CollectionPoints.AsNoTracking() on c.CollectionPointId equals p.Id
               where c.Id == cashBoxId
               select p.BranchId).FirstAsync(ct);
}
