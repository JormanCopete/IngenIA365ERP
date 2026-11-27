using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Payoffs;
using IngenIA365ERP.Domain.Entities.Lending.Operations;

namespace IngenIA365ERP.Application.Lending.Disbursements;

/// <summary>Un crédito recogido ya aplicado por el contrato: lo aplicado y el comprobante (la operación <c>Payment</c> del recogido). (nuevo)</summary>
public sealed record RecogidaAplicada(RecogidaEvaluada Recogida, string ClaveDeRecaudo, ComprobanteDeAplicacionDto Comprobante);

/// <summary>
/// La recogida de créditos propios dentro del desembolso (feature 013, US3, T0444 paso 4; FR-046; data-model §7.3; T21, T91): cada crédito
/// recogido recibe un pago por <see cref="IContratoDeObligaciones.AplicarAsync"/> <b>en la transacción del desembolso</b>, con el recaudador
/// <c>CAR</c> de clase <c>OwnLoanPayoff</c> (exento del registro de participantes, T91), la clave
/// <c>CAR:{operaciónDeDesembolso:N}:{obligationPublicId:N}</c>, el medio <c>InternalOffset</c> (en firme al aplicarse, D23) y la modalidad
/// <c>FullPayoff</c> (total). Un lote, una respuesta por ítem: si alguno no queda aplicado, el desembolso aborta
/// con su código. El reverso de esas recogidas al anular el crédito usa la clave derivada <c>{clave}:reverso</c> (C11). Llama al contrato
/// directo, nunca por <c>ISender</c> (<c>LaCarteraSoloSeTocaPorSusInterfaces</c>).
/// <para>
/// 2026-11-27 (feature 028, T035–T036; FR-016, FR-017; D7): un producto omitido (ya cancelado) no se manda; una recogida parcial —o lo proyectado que no
/// alcanza a cancelar— va como <c>ExtraordinaryPrincipal</c> con la elección del deudor: cubre lo exigible y abona el resto <b>a capital</b> (plan
/// regenerado), no como anticipo (corrige H3); si alcanza el capital por vencer, el contrato la trata como cancelación. Es un solo pago con la clave de
/// siempre: la gramática de las claves <c>CAR</c> no cambia. Y la defensa de H4: lo que aplica el contrato tiene que ser lo que se liquidó; si no,
/// <c>Lending.Payoff.AppliedDiffers</c> y nada queda hecho.
/// </para>
/// (nuevo)
/// </summary>
public sealed class RecogidaEnElDesembolso(IContratoDeObligaciones contrato)
{
    /// <summary>El recaudador de la recogida de créditos propios.</summary>
    public static Recaudador Recaudador { get; } = new(ClavesDeRecaudador.Cartera, ClaseDeRecaudador.OwnLoanPayoff);

    /// <summary>La clave de negocio de la recogida de <paramref name="obligacion"/> en el desembolso <paramref name="desembolso"/>.</summary>
    public static string Clave(Guid desembolso, Guid obligacion) => $"{ClavesDeRecaudador.Cartera}:{desembolso:N}:{obligacion:N}";

    /// <summary>
    /// Aplica las recogidas del desembolso <paramref name="operacion"/> pagadas por <paramref name="deudor"/> (las omitidas no). Devuelve las aplicadas, en
    /// el orden en que vienen.
    /// </summary>
    public async Task<Result<IReadOnlyList<RecogidaAplicada>>> AplicarAsync(LoanOperation operacion, Guid deudor, IReadOnlyList<RecogidaEvaluada> recogidas,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(operacion);
        var aAplicar = recogidas.Where(r => !r.Omitida).ToList();
        if (aAplicar.Count == 0) return Result.Success<IReadOnlyList<RecogidaAplicada>>([]);

        var pagos = aAplicar.Select(r => new PagoAAplicar(
            Clave(operacion.PublicId, r.LoanPublicId),
            r.LoanPublicId,
            r.Total ? ModalidadDePago.FullPayoff : ModalidadDePago.ExtraordinaryPrincipal,
            r.AAplicar,
            operacion.ValueDate,
            ClaseDeMedioDeRecaudo.InternalOffset,
            deudor,
            new ReferenciaDeRecibo(operacion.Number, operacion.ValueDate, null, null, Common.InteresesAlCancelar.DocumentoDelDesembolso),
            Choice: r.Total ? null : r.Eleccion ?? EleccionDeAbono.ReduceTerm)).ToList();
        var respuesta = await contrato.AplicarAsync(new SolicitudDeAplicacion(Recaudador, FormaDeAplicacion.NonInteractive, pagos, []), ct);

        var aplicadas = new List<RecogidaAplicada>(aAplicar.Count);
        for (var i = 0; i < aAplicar.Count; i++)
        {
            var item = i < respuesta.Payments.Count ? respuesta.Payments[i] : null;
            if (item is null || item.Result is not (ResultadoDelContrato.Applied or ResultadoDelContrato.AlreadyApplied) || item.Value is null)
            {
                var codigo = item?.Code ?? "Lending.Contract.NoResponse";
                var mensaje = item?.Message ?? "Cartera no respondió por el crédito que se recoge.";
                return Result.Failure<IReadOnlyList<RecogidaAplicada>>(item?.Data is { Count: > 0 } datos
                    ? new ErrorConDatos(codigo, mensaje, datos)
                    : new ErrorConDatos(codigo, mensaje, new { loanPublicId = aAplicar[i].LoanPublicId, number = aAplicar[i].Number }));
            }
            // H4: la orden de giro sale por lo aplicado; si no es lo liquidado, la recogida y la orden no cuadrarían.
            if (item.Value.AppliedAmount != pagos[i].Amount)
                return Result.Failure<IReadOnlyList<RecogidaAplicada>>(ErroresDeRecogida.AplicadoDistinto(aAplicar[i].LoanPublicId, aAplicar[i].Number,
                    pagos[i].Amount, item.Value.AppliedAmount));
            aplicadas.Add(new RecogidaAplicada(aAplicar[i], pagos[i].IdempotencyKey, item.Value));
        }
        return Result.Success<IReadOnlyList<RecogidaAplicada>>(aplicadas);
    }
}
