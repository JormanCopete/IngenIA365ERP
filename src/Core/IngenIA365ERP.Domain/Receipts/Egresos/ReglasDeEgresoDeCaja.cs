using IngenIA365ERP.Domain.Common.Taxation;
using IngenIA365ERP.Domain.Enums.Receipts;

namespace IngenIA365ERP.Domain.Receipts.Egresos;

/// <summary>Lo que dice la evaluación del GMF de un egreso de caja: si se causa, el valor y el motivo que se sella en el egreso. (nuevo)</summary>
public sealed record GmfDeEgreso(bool Causado, decimal Valor, string Motivo)
{
    public static GmfDeEgreso No(string motivo) => new(false, 0m, motivo);
}

/// <summary>
/// Las reglas puras del egreso de caja (feature 015, R1, T0372; FR-051 a FR-056, FR-101, FR-205; R-14). Sin IO y sin valores legales: la tarifa, los hechos
/// generadores y el redondeo llegan de los parámetros <c>Gmf.*</c> de <c>RCD</c> con su vigencia (<c>RecaudoYTesoreriaNoTienenValoresLegalesFijos</c>). (nuevo)
/// <list type="bullet">
/// <item><b>GMF</b>: lo que el módulo de origen ya liquidó (el retiro y la cancelación con entrega de Captaciones) <b>nunca</b> se causa otra vez
/// (<c>OrigenLoLiquido</c>); con <c>Ninguno</c> no se causa; las devoluciones de saldos a favor (de Cartera y de pagadores) disponen de una cuenta contable a favor
/// de un tercero y se causan con <c>DevolucionesYPartidas</c> o <c>Todos</c> (<c>DevolucionGravada</c>); el egreso vario sólo con <c>Todos</c>
/// (<c>EgresoGravado</c>). La cooperativa lo asume: el beneficiario recibe el valor entero.</item>
/// <item><b>Tope diario</b> (<c>DailyCashOutCap</c> de la caja, sellado en la sesión): lo entregado hoy por la caja más el egreso no lo supera.</item>
/// <item><b>Efectivo suficiente</b>: el egreso no deja el efectivo esperado de la sesión bajo cero; se mide <b>antes</b> de pedir la autorización al módulo.</item>
/// </list>
/// </summary>
public static class ReglasDeEgresoDeCaja
{
    public const string HechosNinguno = "Ninguno";
    public const string HechosTodos = "Todos";

    /// <summary>¿El egreso entrega dinero que el módulo de origen ya liquidó con su propio GMF? (los de Captaciones).</summary>
    public static bool OrigenLiquidaElGmf(CashDisbursementKind clase) =>
        clase is CashDisbursementKind.SavingsWithdrawal or CashDisbursementKind.AccountClosingDelivery;

    public static GmfDeEgreso Gmf(CashDisbursementKind clase, decimal valor, decimal tarifa, string? hechosGeneradores, RedondeoDeGmf redondeo)
    {
        if (valor <= 0m) return GmfDeEgreso.No("SinValor");
        if (OrigenLiquidaElGmf(clase)) return GmfDeEgreso.No("OrigenLoLiquido");
        if (string.Equals(hechosGeneradores, HechosNinguno, StringComparison.OrdinalIgnoreCase)) return GmfDeEgreso.No("SinHechoGenerador");
        var todos = string.Equals(hechosGeneradores, HechosTodos, StringComparison.OrdinalIgnoreCase);
        var motivo = clase switch
        {
            CashDisbursementKind.LendingCreditBalanceRefund or CashDisbursementKind.PayerCreditBalanceRefund => "DevolucionGravada",
            CashDisbursementKind.MiscExpense when todos => "EgresoGravado",
            _ => null,
        };
        if (motivo is null) return GmfDeEgreso.No("NoEsHechoGenerador");
        var gmf = CalculoDeGmf.Calcular(valor, tarifa, redondeo);
        return gmf > 0m ? new GmfDeEgreso(true, gmf, motivo) : GmfDeEgreso.No("TarifaCero");
    }

    /// <summary>¿Lo entregado hoy por la caja más <paramref name="valor"/> supera el tope? Sin tope, nunca.</summary>
    public static bool SuperaElTopeDiario(decimal? tope, decimal entregadoHoy, decimal valor) => tope is { } t && entregadoHoy + valor > t;

    /// <summary>¿Alcanza el efectivo esperado de la sesión para entregar <paramref name="valor"/>?</summary>
    public static bool AlcanzaElEfectivo(decimal efectivoEsperado, decimal valor) => efectivoEsperado - valor >= 0m;
}
