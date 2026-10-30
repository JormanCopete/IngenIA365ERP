using System.Globalization;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Entities.Treasury.Checkbooks;
using IngenIA365ERP.Domain.Entities.Treasury.Remittances;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Accounting;

// Feature 015, T1 (fase 33, T0411; FR-104, FR-220; contracts/contabilidad.md §4): los traslados entre cuentas propias y el cierre del mes de los cheques girados, en línea y
// con el comprobante sugerido TB. Cada línea de Banco lleva su propia cuenta propia (las dimensiones van por línea), así un traslado mueve dos cuentas en dos hechos. Sin guardar.
public sealed partial class EmisionDeTesoreria
{
    public const string ClaseTraslado = "Transfer";
    public const string TipoDeTraslado = "TB";
    public const string ClaseCierreDeCheques = "MonthEndCheckRun";
    public const string ClaseChequeACuentaPorPagar = "IssuedCheckPayable";

    /// <summary>Las dimensiones de una línea sobre una cuenta propia, con la oficina de la cuenta.</summary>
    public async Task<TreasuryFactDimensionsV1> DimensionesDeCuentaAsync(OwnBankAccount cuenta, string? causa, CancellationToken ct)
    {
        var oficina = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == cuenta.BranchId).Select(b => b.LegacyCode).FirstOrDefaultAsync(ct);
        return new TreasuryFactDimensionsV1
        {
            BranchCode = CodigoDeOficina(oficina, cuenta.BranchId), OwnBankAccountPublicId = cuenta.PublicId, OwnBankAccountCode = cuenta.Code, Cause = causa,
        };
    }

    /// <summary><c>TrasladoEntreCuentasEnviado</c> (TEE): al enviarlo, <c>FondosEnTransito</c> → <c>Banco</c> de la cuenta de origen, con la fecha del envío.</summary>
    public async Task<IntegrationMessage> TrasladoEnviadoAsync(OwnBankAccount origen, OwnBankAccount destino, Transfer traslado, Guid? usoDelFondo, CancellationToken ct)
    {
        var fecha = traslado.SentOn ?? traslado.RequestedOn;
        var dim = await DimensionesDeCuentaAsync(origen, "Sent", ct);
        var contenido = new TrasladoEntreCuentasEnviadoV1
        {
            TransferPublicId = traslado.PublicId, Number = traslado.Number, FromBankAccountPublicId = origen.PublicId, ToBankAccountPublicId = destino.PublicId,
            Amount = traslado.Amount, SentOn = fecha, Reference = traslado.BankReference, LiquidityFundUsePublicId = usoDelFondo, RegisteredBy = await UsuarioAsync(ct),
            Lines =
            [
                Linea(RolesDeTesoreriaT1.FondosEnTransito, RolesDeTesoreria.Debito, traslado.Amount, dim, cruce: traslado.Number),
                Linea(RolesDeTesoreria.Banco, RolesDeTesoreria.Credito, traslado.Amount, dim, cruce: traslado.Number),
            ],
        };
        return await EmitirAsync(contenido, traslado.PublicId, ClaseTraslado, TipoDeTraslado, traslado.Number, fecha, origen.BranchId, null, ClavesDeEvento.Confirmacion, ct);
    }

    /// <summary>
    /// <c>TrasladoConfirmado</c> (TCF): al llegar a la de destino, <c>Banco</c> de la cuenta de destino → <c>FondosEnTransito</c>, con la fecha del abono. Los fondos en tránsito
    /// van con la cuenta de origen en sus dimensiones para que el tránsito de cada cuenta quede en cero.
    /// </summary>
    public async Task<IntegrationMessage> TrasladoConfirmadoAsync(OwnBankAccount origen, OwnBankAccount destino, Transfer traslado, CancellationToken ct)
    {
        var fecha = traslado.ConfirmedOn ?? traslado.SentOn ?? traslado.RequestedOn;
        var enTransito = await DimensionesDeCuentaAsync(origen, "Confirmed", ct);
        var banco = await DimensionesDeCuentaAsync(destino, "Confirmed", ct);
        var contenido = new TrasladoConfirmadoV1
        {
            TransferPublicId = traslado.PublicId, Number = traslado.Number, FromBankAccountPublicId = origen.PublicId, ToBankAccountPublicId = destino.PublicId,
            Amount = traslado.Amount, ConfirmedOn = fecha, StatementLinePublicId = traslado.ConfirmedByStatementLinePublicId, RegisteredBy = await UsuarioAsync(ct),
            Lines =
            [
                Linea(RolesDeTesoreria.Banco, RolesDeTesoreria.Debito, traslado.Amount, banco, cruce: traslado.Number),
                Linea(RolesDeTesoreriaT1.FondosEnTransito, RolesDeTesoreria.Credito, traslado.Amount, enTransito, cruce: traslado.Number),
            ],
        };
        return await EmitirAsync(contenido, traslado.PublicId, ClaseTraslado, TipoDeTraslado, traslado.Number, fecha, destino.BranchId, null, ClavesDeEvento.Confirmacion, ct);
    }

    /// <summary>
    /// <c>ChequesReclasificadosAlCierre</c> (CRC): los emitidos y no entregados al corte vuelven al efectivo —<c>Banco</c> → <c>ChequesPorEntregar</c>, una pareja de líneas por
    /// cheque con su número como cruce— y, con <paramref name="reverso"/>, el reverso del primer día del mes siguiente con las líneas invertidas y su propio origen.
    /// </summary>
    public async Task<IntegrationMessage> ChequesReclasificadosAsync(OwnBankAccount cuenta, MonthEndCheckRun corrida, IReadOnlyList<IssuedCheck> cheques, bool reverso,
        CancellationToken ct)
    {
        var dim = await DimensionesDeCuentaAsync(cuenta, reverso ? "Reversal" : "Reclassification", ct);
        var fecha = reverso ? corrida.CutoffDate.AddDays(1) : corrida.CutoffDate;
        var lineas = new List<TreasuryFactLineV1>();
        foreach (var c in cheques)
        {
            var numero = c.CheckNumber.ToString(CultureInfo.InvariantCulture);
            lineas.Add(Linea(RolesDeTesoreria.Banco, reverso ? RolesDeTesoreria.Credito : RolesDeTesoreria.Debito, c.Amount, dim, cruce: numero));
            lineas.Add(Linea(RolesDeTesoreriaT1.ChequesPorEntregar, reverso ? RolesDeTesoreria.Debito : RolesDeTesoreria.Credito, c.Amount, dim, cruce: numero));
        }
        var contenido = new ChequesReclasificadosAlCierreV1
        {
            MonthEndCheckRunPublicId = corrida.PublicId, RunNumber = corrida.Number, Year = corrida.PeriodYear, Month = corrida.PeriodMonth,
            BankAccountPublicId = cuenta.PublicId, IsReversal = reverso, OccurredOn = fecha, CheckCount = cheques.Count, Amount = cheques.Sum(c => c.Amount),
            RegisteredBy = await UsuarioAsync(ct), Lines = lineas,
        };
        var origen = reverso ? corrida.ReversalPublicId : corrida.PublicId;
        return await EmitirAsync(contenido, origen, ClaseCierreDeCheques, TipoDeTraslado, corrida.Number, fecha, cuenta.BranchId, null, ClavesDeEvento.Confirmacion, ct);
    }

    /// <summary>
    /// <c>ChequeNoCobradoACuentaPorPagar</c> (CNP): un cheque entregado sin cobrar más allá del plazo pasa a cuenta por pagar al beneficiario —<c>Banco</c> →
    /// <c>CuentaPorPagarChequesNoCobrados</c> con el beneficiario como tercero—, fechado al corte. Nunca a ingreso.
    /// </summary>
    public async Task<IntegrationMessage> ChequeNoCobradoACuentaPorPagarAsync(OwnBankAccount cuenta, MonthEndCheckRun corrida, IssuedCheck cheque, CancellationToken ct)
    {
        var dim = await DimensionesDeCuentaAsync(cuenta, "NotCashedToPayable", ct);
        var beneficiario = cheque.BeneficiaryPersonId is { } p
            ? await db.People.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == p).Select(x => (Guid?)x.PublicId).FirstOrDefaultAsync(ct)
            : null;
        var numero = cheque.CheckNumber.ToString(CultureInfo.InvariantCulture);
        var entregado = cheque.DeliveredOn ?? cheque.IssuedOn;
        var contenido = new ChequeNoCobradoACuentaPorPagarV1
        {
            MonthEndCheckRunPublicId = corrida.PublicId, RunNumber = corrida.Number, IssuedCheckPublicId = cheque.PublicId, CheckNumber = cheque.CheckNumber,
            BankAccountPublicId = cuenta.PublicId, BeneficiaryPersonPublicId = beneficiario, Amount = cheque.Amount, DeliveredOn = entregado,
            DaysWithoutCashing = corrida.CutoffDate.DayNumber - entregado.DayNumber, OccurredOn = corrida.CutoffDate, RegisteredBy = await UsuarioAsync(ct),
            Lines =
            [
                Linea(RolesDeTesoreria.Banco, RolesDeTesoreria.Debito, cheque.Amount, dim, cruce: numero),
                Linea(RolesDeTesoreriaT1.CuentaPorPagarChequesNoCobrados, RolesDeTesoreria.Credito, cheque.Amount, dim, beneficiario, numero),
            ],
        };
        return await EmitirAsync(contenido, cheque.PublicId, ClaseChequeACuentaPorPagar, TipoDeTraslado, corrida.Number, corrida.CutoffDate, cuenta.BranchId, beneficiario,
            ClavesDeEvento.Confirmacion, ct);
    }
}
