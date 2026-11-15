using IngenIA365ERP.Shared.Services.Recaudo;
using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Services.Pagos;

/// <summary>
/// Feature 022 (T062): de lo que trae cada módulo al panel único, y del panel a lo que cada módulo recibe. El panel no sabe de
/// ventas ni de Recaudo; sólo estos adaptadores conocen los dos contratos.
/// </summary>
public static class AdaptadoresDeCobro
{
    // ---------------------------------------------------------------------------------------- ventas --

    /// <summary>
    /// Un medio de la venta (POS u oficina) como medio del panel. Las cuentas propias (Q16) vienen en el medio del POS o, en la oficina,
    /// de la ruta de la sesión de caja (<paramref name="cuentasDeLaSesion"/>).
    /// </summary>
    public static MedioDisponible DesdeVenta(MedioDelPosDto m) => DesdeVenta(m, null);

    public static MedioDisponible DesdeVenta(MedioDelPosDto m, IEnumerable<CuentaPropiaDeLaSesionDto>? cuentasDeLaSesion) => new(
        m.PaymentMeansPublicId, m.Code, m.Name, m.Class, m.QuickKey, m.AllowsChange, m.RequiresReference, m.ReferenceKind,
        m.CardTerminals.Select(d => new OpcionDeMedio(d.PublicId, d.Code)).ToList(), m.DefaultCardTerminalPublicId,
        cuentasDeLaSesion is not null
            ? cuentasDeLaSesion.Where(c => c.PaymentMeansPublicId == m.PaymentMeansPublicId).Select(c => new OpcionDeMedio(c.OwnBankAccountPublicId, c.AccountLabel)).ToList()
            : (m.OwnBankAccounts ?? []).Select(c => new OpcionDeMedio(c.OwnBankAccountPublicId, c.Label)).ToList(),
        m.CreditDefaults is { } c ? new CreditoDelMedio(c.Installments, null, c.TermDays, null, c.PeriodicityDays, c.SuggestedLineCode) : null);

    /// <summary>Los pagos del panel como los recibe la venta; los que se arquean llevan la <paramref name="sesion"/> de caja.</summary>
    public static IReadOnlyList<PagoRequest> ParaVenta(CobroMultiple cobro, Guid? sesion) => cobro.Pagos.Select(p => new PagoRequest(
        p.Medio.PublicId,
        p.Monto,
        Tendered: p.Medio.DaVueltas ? p.Recibido : null,
        Reference: CobroMultiple.Texto(p.Medio.EsBono ? p.NumeroDeBono : p.Medio.EsCheque ? p.Cheque?.Numero : p.Referencia),
        AuthorizationCode: CobroMultiple.Texto(p.Aprobacion),
        CardTerminalPublicId: p.Medio.EsTarjeta ? p.Datafono : null,
        BatchNumber: CobroMultiple.Texto(p.Lote),
        Last4: CobroMultiple.Last4Valido(p.Last4) ? p.Last4 : null,
        CashSessionPublicId: sesion,
        // 2026-11-13 (pedido del dueño): del crédito se piden sólo las cuotas y la fecha del primer pago; la periodicidad es la del medio y el
        // plazo lo calcula el servidor (no viajan).
        Credit: p.Medio.EsCredito && p.Cuotas is { } c
            ? new CreditoDelPagoRequest(c, null, null, p.PrimerVencimiento, CobroMultiple.Texto(p.Linea), p.MoraConfirmada, p.AprobacionDeMora)
            : null,
        OwnBankAccountPublicId: p.CuentaPropia,
        Check: p.Medio.EsCheque && p.Cheque is { } ch
            ? new ChequeDelPagoRequest(ch.Banco, CobroMultiple.Texto(ch.Numero), CobroMultiple.Texto(ch.Cuenta), ch.Fecha, ch.MismaPlaza, CobroMultiple.Texto(ch.Girador))
            : null)).ToList();

    // --------------------------------------------------------------------------------------- Recaudo --

    /// <summary>Un medio de la sesión de caja de Recaudo como medio del panel, con las cuentas propias habilitadas en la caja.</summary>
    public static MedioDisponible DesdeRecaudo(MedioDeSesionDto m, IEnumerable<OpcionDeCuentaDto> cuentas) => new(
        m.PaymentMeansPublicId, m.Code, m.Name, m.Class, m.QuickKey, m.AllowsChange || m.Class == ClasesDeMedio.Efectivo, m.RequiresReference, m.ReferenceKind,
        (m.CardTerminals ?? []).Select(d => new OpcionDeMedio(d.CardTerminalPublicId, $"{d.Code} · {d.CardAcquirerCode}")).ToList(), null,
        cuentas.Where(c => c.PaymentMeansPublicId == m.PaymentMeansPublicId).Select(c => new OpcionDeMedio(c.OwnBankAccountPublicId, c.AccountLabel)).ToList());

    /// <summary>
    /// Los pagos del panel como los recibe Recaudo. Recaudo calcula las vueltas como lo que entregan los medios de más sobre lo que
    /// se paga, así que el efectivo viaja por lo recibido.
    /// </summary>
    public static IReadOnlyList<MedioDeCajaRequest> ParaRecaudo(CobroMultiple cobro) => cobro.Pagos.Select(p => new MedioDeCajaRequest(
        p.Medio.PublicId,
        p.Medio.EsEfectivo && p.Recibido is { } r && r > p.Monto ? r : p.Monto,
        Reference: CobroMultiple.Texto(p.Referencia),
        CardTerminalPublicId: p.Medio.EsTarjeta ? p.Datafono : null,
        TerminalBatch: CobroMultiple.Texto(p.Lote),
        ApprovalCode: CobroMultiple.Texto(p.Aprobacion),
        VoucherNumber: p.Medio.EsBono ? CobroMultiple.Texto(p.NumeroDeBono) : null,
        MeansId: p.Id,
        Check: p.Medio.EsCheque && p.Cheque is { Banco: { } banco } c
            ? new ChequeDeCajaRequest(banco, c.Numero ?? "", c.Cuenta ?? "", c.Fecha ?? DateOnly.FromDateTime(DateTime.Today), c.MismaPlaza,
                DrawerName: CobroMultiple.Texto(c.Girador))
            : null,
        OwnBankAccountPublicId: p.CuentaPropia,
        Last4: CobroMultiple.Last4Valido(p.Last4) ? p.Last4 : null)).ToList();
}
