using IngenIA365ERP.Domain.Common.Taxation;
using IngenIA365ERP.Domain.Enums.Treasury;

namespace IngenIA365ERP.Domain.Treasury.Gmf;

/// <summary>Qué egresos generan GMF (parámetro <c>Gmf.HechosGeneradoresDeEgresos</c>). (nuevo)</summary>
public enum HechosGeneradoresDeGmf
{
    /// <summary>Las devoluciones de saldos a favor y de partidas (disposición de una cuenta contable a favor de un tercero, FR-205). El defecto.</summary>
    DevolucionesYPartidas = 1,

    /// <summary>Todo egreso que no esté exento.</summary>
    Todos = 2,

    /// <summary>Ninguno (la cooperativa no causa GMF de egresos).</summary>
    Ninguno = 3,
}

/// <summary>Los parámetros vigentes del GMF de egresos, leídos por la aplicación a la fecha del lote. (nuevo)</summary>
public sealed record ParametrosDeGmf(decimal Tarifa, HechosGeneradoresDeGmf HechosGeneradores, bool ExencionDesembolsos, RedondeoDeGmf Redondeo)
{
    public static HechosGeneradoresDeGmf Hechos(string? texto) => texto switch
    {
        _ when string.Equals(texto, nameof(HechosGeneradoresDeGmf.Todos), StringComparison.OrdinalIgnoreCase) => HechosGeneradoresDeGmf.Todos,
        _ when string.Equals(texto, nameof(HechosGeneradoresDeGmf.Ninguno), StringComparison.OrdinalIgnoreCase) => HechosGeneradoresDeGmf.Ninguno,
        _ => HechosGeneradoresDeGmf.DevolucionesYPartidas,
    };
}

/// <summary>
/// Un egreso que se evalúa: la orden con su origen, su clase, su forma y su valor; si el beneficiario es el deudor del desembolso; si la cuenta de origen es
/// exenta y si el origen ya liquidó el GMF (los retiros de Captaciones, FR-205). (nuevo)
/// </summary>
public sealed record EgresoAEvaluar(
    PaymentOrderOrigin Origen,
    string ClaseDeOrden,
    PaymentForm Forma,
    decimal Valor,
    bool BeneficiarioEsElDeudor,
    bool CuentaDeOrigenExenta,
    bool OrigenYaLiquido);

/// <summary>
/// Lo que dice la evaluación de un egreso: si se evaluó, si está gravado o exento (con el motivo que se sella en la orden, FR-206), la base, el GMF y si
/// Tesorería emite el hecho <c>GmfDePagoCausado</c> (sólo cuando el pago dispone de una cuenta contable a favor de un tercero; el GMF del débito de los
/// desembolsos lo cobra el banco y se reconoce desde el extracto, FR-100). (nuevo)
/// </summary>
public sealed record EvaluacionDeGmf(bool Evaluado, bool Gravado, bool Exento, string Motivo, decimal Base, decimal Gmf, bool EmiteHecho)
{
    public static EvaluacionDeGmf NoEvaluado(string motivo) => new(false, false, false, motivo, 0m, 0m, false);

    public static EvaluacionDeGmf ExentoPor(string motivo, decimal valor) => new(true, false, true, motivo, valor, 0m, false);
}

/// <summary>
/// El evaluador del GMF de los egresos de Tesorería (feature 015, US10, T0134; R-25; FR-100, FR-205, FR-206; DUR 1625 de 2016, ET arts. 871 y 879). Puro: sin
/// IO, sin valores legales (todos llegan en <see cref="ParametrosDeGmf"/>). El orden de las reglas es el de los casos dorados:
/// <list type="number">
/// <item>lo que el origen ya liquidó no se evalúa (<c>OrigenLoLiquido</c>: los retiros y devoluciones de Captaciones);</item>
/// <item>la cuenta de origen exenta no causa (<c>CuentaExenta</c>);</item>
/// <item>con <c>Ninguno</c> no se causa (<c>SinHechoGenerador</c>);</item>
/// <item>el desembolso de crédito, con <c>Gmf.ExencionDesembolsos</c>, está exento cuando abona en cuenta del deudor o sale en cheque de primer beneficiario
/// (<c>DesembolsoExento</c>); si no, lo grava el débito bancario (estimación, sin hecho propio);</item>
/// <item>las devoluciones de Recaudo (saldos a favor, partidas) disponen de una cuenta contable a favor de un tercero: gravadas con hecho
/// (<c>DevolucionGravada</c>);</item>
/// <item>con <c>Todos</c>, el resto se estima gravado sin hecho (<c>EgresoGravado</c>); con <c>DevolucionesYPartidas</c>, no se causa
/// (<c>NoEsHechoGenerador</c>).</item>
/// </list>
/// Una tarifa cero evalúa y da cero. (nuevo)
/// </summary>
public static class EvaluadorDeGmfDeEgresos
{
    /// <summary>Las clases de orden de Recaudo que disponen de una cuenta contable a favor de un tercero (interfaces.md §2).</summary>
    public static readonly IReadOnlySet<string> DevolucionesDeRecaudo = new HashSet<string>(StringComparer.Ordinal)
    {
        "PayerCreditBalanceRefund", "LendingCreditBalanceRefund", "UnidentifiedItemRefund",
    };

    public const string ClaseDesembolso = "LoanDisbursement";

    public static EvaluacionDeGmf Evaluar(EgresoAEvaluar egreso, ParametrosDeGmf parametros)
    {
        ArgumentNullException.ThrowIfNull(egreso);
        ArgumentNullException.ThrowIfNull(parametros);
        if (egreso.Valor <= 0m) return EvaluacionDeGmf.NoEvaluado("SinValor");
        if (egreso.OrigenYaLiquido || egreso.Origen == PaymentOrderOrigin.DepositsPayout) return EvaluacionDeGmf.NoEvaluado("OrigenLoLiquido");
        if (egreso.CuentaDeOrigenExenta) return EvaluacionDeGmf.ExentoPor("CuentaExenta", egreso.Valor);
        if (parametros.HechosGeneradores == HechosGeneradoresDeGmf.Ninguno) return new(true, false, false, "SinHechoGenerador", egreso.Valor, 0m, false);

        var esDesembolso = egreso.Origen == PaymentOrderOrigin.LendingDisbursement
                           && (string.IsNullOrEmpty(egreso.ClaseDeOrden) || string.Equals(egreso.ClaseDeOrden, ClaseDesembolso, StringComparison.Ordinal));
        if (esDesembolso)
        {
            var exentaPorForma = (egreso.Forma == PaymentForm.Transfer && egreso.BeneficiarioEsElDeudor) || egreso.Forma == PaymentForm.Check;
            if (parametros.ExencionDesembolsos && exentaPorForma) return EvaluacionDeGmf.ExentoPor("DesembolsoExento", egreso.Valor);
            return Gravado("DesembolsoGravado", egreso.Valor, parametros, emiteHecho: false);
        }

        if (egreso.Origen == PaymentOrderOrigin.Receipts && DevolucionesDeRecaudo.Contains(egreso.ClaseDeOrden))
            return Gravado("DevolucionGravada", egreso.Valor, parametros, emiteHecho: true);

        return parametros.HechosGeneradores == HechosGeneradoresDeGmf.Todos
            ? Gravado("EgresoGravado", egreso.Valor, parametros, emiteHecho: false)
            : new(true, false, false, "NoEsHechoGenerador", egreso.Valor, 0m, false);
    }

    /// <summary>El GMF estimado de un lote: la suma de lo gravado.</summary>
    public static decimal EstimadoDelLote(IEnumerable<EvaluacionDeGmf> evaluaciones) => evaluaciones.Where(e => e.Gravado).Sum(e => e.Gmf);

    private static EvaluacionDeGmf Gravado(string motivo, decimal valor, ParametrosDeGmf p, bool emiteHecho)
    {
        var gmf = CalculoDeGmf.Calcular(valor, p.Tarifa, p.Redondeo);
        return new EvaluacionDeGmf(true, true, false, motivo, valor, gmf, emiteHecho && gmf > 0m);
    }
}
