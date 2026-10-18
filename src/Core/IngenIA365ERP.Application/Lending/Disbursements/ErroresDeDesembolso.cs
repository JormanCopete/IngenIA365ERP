using System.Globalization;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending.Operations;

namespace IngenIA365ERP.Application.Lending.Disbursements;

/// <summary>
/// Los errores del desembolso, la orden de giro, su resultado y el reverso (feature 013, US3; data-model §7.2–§7.5, §7.11, §9 y §10.1; api.md
/// §8.4, §9). Donde los artefactos chocaban rige data-model (notas de coordinación de tasks.md, 1–4, y DT §7.10 C113): garantías pendientes
/// es <c>Lending.Disbursement.GuaranteesPending</c>, oficina sin sucursal contable <c>Lending.Branch.AccountingBranchMissing</c> y el resultado
/// del giro que no cabe un solo <c>Lending.DisbursementOrder.InvalidResult</c> con el motivo como dato. Los mensajes van en español claro y
/// dicen qué hacer (FR-013). (nuevo)
/// </summary>
public static class ErroresDeDesembolso
{
    public const string CodigoNoAprobada = "Lending.Application.NotApproved";
    public const string CodigoGarantiasPendientes = "Lending.Disbursement.GuaranteesPending";
    public const string CodigoExcedeLoAprobado = "Lending.Disbursement.ExceedsApproved";
    public const string CodigoTramoNoAdmitido = "Lending.Disbursement.TrancheNotAllowed";
    public const string CodigoSinCuentaDelBeneficiario = "Lending.Disbursement.BeneficiaryAccountMissing";
    public const string CodigoVistaPreviaCambio = "Lending.Disbursement.PreviewChanged";
    public const string CodigoNetoNoPositivo = "Lending.Disbursement.NetNotPositive";
    public const string CodigoCoberturaInsuficiente = "Lending.Collateral.CoverageInsufficient";
    public const string CodigoRequiereReestructuracion = "Lending.Payoff.RequiresRestructuring";
    public const string CodigoRecogidaNoAutorizada = "Lending.Payoff.NotRequested";
    public const string CodigoSinSucursalContable = "Lending.Branch.AccountingBranchMissing";
    public const string CodigoMontoOperativo = "Lending.AmountLimit.Exceeded";
    public const string CodigoNoContabilizable = "Lending.Prevalidation.NotPostable";
    public const string CodigoOrdenInexistente = "Lending.DisbursementOrder.NotFound";
    public const string CodigoOrdenNoAnulable = DisbursementOrder.CodigoNoAnulable;
    public const string CodigoOrdenEnArchivo = DisbursementOrder.CodigoEnArchivo;
    public const string CodigoOrdenNoReemitible = "Lending.DisbursementOrder.NotReissuable";
    public const string CodigoResultadoInvalido = DisbursementOrder.CodigoResultadoInvalido;
    public const string CodigoCreditoNoPendiente = "Lending.Loan.NotPendingDisbursement";
    public const string CodigoCreditoInexistente = "Lending.Loan.NotFound";
    public const string CodigoDineroYaSalio = "Lending.Reversal.DisbursementPaidOut";
    public const string CodigoNoEncontrado = "Generic.NotFound";

    public static Error NoAprobada(string estado) => new ErrorConDatos(CodigoNoAprobada,
        "La solicitud no está aprobada: sólo se desembolsa una solicitud aprobada y vigente.", new { status = estado });

    public static Error GarantiasPendientes(IReadOnlyList<GarantiaPendienteDto> pendientes) => new ErrorConDatos(CodigoGarantiasPendientes,
        "Faltan garantías o el pagaré por perfeccionar: " + string.Join("; ", pendientes.Select(p => p.Reason)) + ".",
        new { pending = pendientes.Select(p => new { kind = p.Kind, reason = p.Reason }).ToList() });

    public static Error CoberturaInsuficiente(decimal exigida, decimal cubierta, int regla) => new ErrorConDatos(CodigoCoberturaInsuficiente,
        $"Las garantías admisibles cubren {Pct(cubierta)} del monto y la línea exige {Pct(exigida)}. Registre o revalúe las garantías.",
        new { required = exigida, present = cubierta, ruleOrder = regla });

    public static Error ExcedeLoAprobado(decimal aprobado, decimal desembolsado, decimal pedido) => new ErrorConDatos(CodigoExcedeLoAprobado,
        $"El tramo de {Pesos(pedido)} supera lo que queda por desembolsar ({Pesos(aprobado - desembolsado)} de {Pesos(aprobado)} aprobados).",
        new { approved = aprobado, disbursed = desembolsado, requested = pedido });

    public static Error TramoNoAdmitido(string linea) => new ErrorConDatos(CodigoTramoNoAdmitido,
        $"La línea {linea} no admite desembolsos por tramos: el crédito se desembolsa en uno solo.", new { creditLineCode = linea });

    public static Error SinCuentaDelBeneficiario() => new(CodigoSinCuentaDelBeneficiario,
        "Una transferencia necesita el banco, el tipo y el número de cuenta del beneficiario. Complételos o elija otra forma.");

    public static Error VistaPreviaCambio(string huella) => new ErrorConDatos(CodigoVistaPreviaCambio,
        "Algo cambió desde la vista previa (usura, recogidas, garantías o plan). Revise la vista previa nueva y confirme otra vez.",
        new { previewSha256 = huella });

    public static Error NetoNoPositivo(decimal bruto, decimal recogido) => new ErrorConDatos(CodigoNetoNoPositivo,
        $"Lo recogido ({Pesos(recogido)}) no deja nada por girar del tramo de {Pesos(bruto)}: el neto debe ser mayor que cero.",
        new { grossAmount = bruto, payoffAmount = recogido });

    /// <summary>
    /// K3 (T0882; FR-046): recoger un crédito en mora es una reestructuración; desde K3 se tramita como novedad de <b>refinanciación</b> en Novedades
    /// (<c>/cartera/novedades</c>), que recoge los créditos en mora y nace como crédito enlazado. <c>data.link</c> lleva la pantalla.
    /// </summary>
    public const string PantallaDeNovedades = "/cartera/novedades";

    public static Error RequiereReestructuracion(IReadOnlyList<RecogidaEnMoraDto> creditos) => new ErrorConDatos(CodigoRequiereReestructuracion,
        $"El crédito {string.Join(", ", creditos.Select(c => c.Number))} está en mora: recogerlo es una reestructuración. Tramítela en Novedades como "
        + "una refinanciación que lo recoge.",
        new
        {
            loans = creditos.Select(c => new { loanPublicId = c.LoanPublicId, number = c.Number, daysPastDue = c.DaysPastDue }).ToList(),
            link = PantallaDeNovedades,
            mechanism = nameof(Domain.Enums.Lending.NoveltyMechanism.Refinancing),
        });

    public static Error RecogidaNoAutorizada(Guid credito) => new ErrorConDatos(CodigoRecogidaNoAutorizada,
        "Ese crédito no está entre los que la solicitud aprobada autoriza a recoger. Sólo se recoge lo aprobado.", new { loanPublicId = credito });

    public static Error SinSucursalContable() => new(CodigoSinSucursalContable,
        "La oficina del crédito no tiene sucursal contable vinculada. Vincúlela en Agencias antes de desembolsar.");

    public static Error MontoOperativo(decimal maximo, decimal pedido) => new ErrorConDatos(CodigoMontoOperativo,
        $"Su atribución para desembolsar llega a {Pesos(maximo)} y el tramo es de {Pesos(pedido)}. Pídale a quien tenga una atribución mayor.",
        new { limit = maximo, requested = pedido });

    public static Error NoContabilizable(object hallazgos) => new ErrorConDatos(CodigoNoContabilizable,
        "Contabilidad no tiene regla para registrar este desembolso. Pida a la contadora completar la matriz.", new { findings = hallazgos });

    public static Error OrdenInexistente() => new(CodigoOrdenInexistente, "La orden de giro no existe.");

    public static Error OrdenNoReemitible(string estado) => new ErrorConDatos(CodigoOrdenNoReemitible,
        "Sólo se gira de nuevo una orden fallida de un crédito pendiente de giro.", new { loanStatus = estado });

    public static Error CreditoNoPendiente(string estado) => new ErrorConDatos(CodigoCreditoNoPendiente,
        "Sólo se anula un crédito pendiente de giro, sin otro tramo vivo.", new { status = estado });

    public static Error CreditoInexistente() => new(CodigoCreditoInexistente, "El crédito no existe.");

    public static Error DineroYaSalio(string orden) => new ErrorConDatos(CodigoDineroYaSalio,
        $"La orden {orden} ya se giró: el desembolso no se reversa. Gestione la devolución como un pago.", new { orderNumber = orden });

    public static Error ResultadoInvalido(string motivo, object datos, string mensaje) => new ErrorConDatos(CodigoResultadoInvalido, mensaje, datos);

    public static string Pesos(decimal valor) => "$" + valor.ToString("#,##0.##", CultureInfo.GetCultureInfo("es-CO"));

    private static string Pct(decimal fraccion) => (fraccion * 100m).ToString("0.##", CultureInfo.GetCultureInfo("es-CO")) + " %";
}
