using IngenIA365ERP.Application.Common.Integration.Lending;

namespace IngenIA365ERP.Application.Payroll.Services;

/// <summary>Un descuento que Nómina entrega a Cartera: la obligación, quién paga (el empleado deudor, G7), la modalidad y el valor.</summary>
public sealed record PagoDeNomina(Guid ObligationPublicId, Guid PagadorPublicId, ModalidadDePago Modalidad, decimal Valor);

/// <summary>Lo que la ordinaria no descontó de una cuota, con su motivo y la fila de <c>PAY_PayrollLoanDeductions</c> que lo informa.</summary>
public sealed record NoDescontadoDeNomina(
    Guid ObligationPublicId, decimal Esperado, decimal NoDescontado, MotivoDeNoDescuento Motivo, string? Detalle, Guid? FilaPublicId);

/// <summary>El lote de una nómina: la versión aprobada, su documento (el NM o la liquidación), la fecha valor y los ítems.</summary>
public sealed record LoteDeNomina(
    Guid RunPublicId,
    ReferenciaDeRecibo Recibo,
    DateOnly FechaValor,
    IReadOnlyList<PagoDeNomina> Pagos,
    IReadOnlyList<NoDescontadoDeNomina> NoDescontados);

/// <summary>Lo que respondió Cartera por cada obligación del lote, con la clave con que se aplicó.</summary>
public sealed record RecaudoDeNomina(
    Guid ObligationPublicId,
    string Clave,
    decimal Enviado,
    RespuestaDelContratoDto<ComprobanteDeAplicacionDto> Respuesta)
{
    /// <summary>Cartera lo aplicó (o ya lo había aplicado con la misma clave).</summary>
    public bool Aplicado => Respuesta.Result is ResultadoDelContrato.Applied or ResultadoDelContrato.AlreadyApplied && Respuesta.Value is not null;

    /// <summary>Lo que Cartera tomó: lo recibido menos el excedente que vuelve a Nómina (D5). Cero si lo rechazó.</summary>
    public decimal ValorAplicado => Aplicado ? Math.Max(0m, Respuesta.Value!.AppliedAmount) : 0m;

    /// <summary>Lo que vuelve al recaudador.</summary>
    public decimal Excedente => Aplicado ? Respuesta.Value!.Surplus : 0m;
}

/// <summary>El resultado del lote: un recaudo por pago, en el orden en que se envió, y la respuesta de cada no descontado.</summary>
public sealed record ResultadoDelLoteDeNomina(
    IReadOnlyList<RecaudoDeNomina> Pagos,
    IReadOnlyList<RespuestaDelContratoDto<NoDescontadoRegistradoDto>> NoDescontados)
{
    public static ResultadoDelLoteDeNomina Vacio { get; } = new([], []);
}

/// <summary>
/// Nómina como recaudador del contrato de obligaciones de Cartera (feature 013, T0158; T92, R103–R105; contracts/interfaces.md §3, §6):
/// arma el lote (<see cref="FormaDeAplicacion.NonInteractive"/>, <see cref="Recaudador.Nomina"/>, clase de medio
/// <see cref="ClaseDeMedioDeRecaudo.OwnPayrollDeduction"/>, pagador el empleado, recibo el NM o la liquidación, clave
/// <c>NOM:{runPublicId:N}:{obligationPublicId:N}</c>, ítems en orden ascendente de <c>ObligationPublicId</c>) y devuelve la respuesta
/// por obligación; no decide nada: la definitiva aplica «todo o nada» y la ordinaria sigue con lo que Cartera tomó.
/// <para>
/// Se llama <b>directo</b>, nunca por <c>ISender</c>, dentro de la <c>TransaccionExplicita</c> de la aprobación o de la reversión: el
/// contrato escribe en la misma unidad de trabajo y, si la nómina no confirma, la aplicación tampoco queda (T91, I-4). Un lote vacío no
/// llama a Cartera. (nuevo)
/// </para>
/// </summary>
public sealed class RecaudoPorNomina(IContratoDeObligaciones contrato)
{
    /// <summary>La clave de un pago de Nómina (interfaces §3.10, T21): la versión aprobada de la corrida y la obligación.</summary>
    public static string Clave(Guid runPublicId, Guid obligationPublicId) => $"NOM:{runPublicId:N}:{obligationPublicId:N}";

    /// <summary>Aplica el lote. Sin pagos ni no descontados no llama a Cartera.</summary>
    public async Task<ResultadoDelLoteDeNomina> AplicarAsync(LoteDeNomina lote, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(lote);
        if (lote.Pagos.Count == 0 && lote.NoDescontados.Count == 0) return ResultadoDelLoteDeNomina.Vacio;

        var pagos = lote.Pagos.OrderBy(p => p.ObligationPublicId).ToList();
        var noDescontados = lote.NoDescontados.OrderBy(n => n.ObligationPublicId).ToList();
        var solicitud = new SolicitudDeAplicacion(
            Recaudador.Nomina,
            FormaDeAplicacion.NonInteractive,
            pagos.Select(p => new PagoAAplicar(
                Clave(lote.RunPublicId, p.ObligationPublicId), p.ObligationPublicId, p.Modalidad, p.Valor, lote.FechaValor,
                ClaseDeMedioDeRecaudo.OwnPayrollDeduction, p.PagadorPublicId, lote.Recibo)).ToList(),
            noDescontados.Select(n => new NoDescontado(
                Clave(lote.RunPublicId, n.ObligationPublicId), n.ObligationPublicId, null, n.Esperado, n.NoDescontado,
                n.Motivo, n.Detalle, n.FilaPublicId)).ToList());

        var respuesta = await contrato.AplicarAsync(solicitud, ct);
        var recaudos = pagos.Select((p, i) => new RecaudoDeNomina(
                p.ObligationPublicId, solicitud.Payments[i].IdempotencyKey, p.Valor,
                i < respuesta.Payments.Count ? respuesta.Payments[i] : SinRespuesta<ComprobanteDeAplicacionDto>()))
            .ToList();
        var registrados = noDescontados.Select((_, i) => i < respuesta.NotDeducted.Count ? respuesta.NotDeducted[i] : SinRespuesta<NoDescontadoRegistradoDto>()).ToList();
        return new ResultadoDelLoteDeNomina(recaudos, registrados);
    }

    /// <summary>Reversa el pago que Nómina aplicó por esa obligación en esa versión de la corrida.</summary>
    public Task<RespuestaDelContratoDto<ComprobanteDeReversoDto>> ReversarAsync(
        Guid runPublicId, Guid obligationPublicId, string motivo, ReferenciaDeRecibo? recibo, CancellationToken ct) =>
        contrato.ReversarAsync(new SolicitudDeReverso(Recaudador.Nomina, Clave(runPublicId, obligationPublicId), motivo, recibo), ct);

    /// <summary>
    /// Simula un pago de valor libre sin escribir (DE-06): el ajuste de un descuento de la definitiva muestra el saldo exacto que
    /// quedaría. Responde nulo si Cartera no simuló (sin activar, rechazo).
    /// </summary>
    public async Task<SimulacionDePagoDto?> SimularValorLibreAsync(Guid obligationPublicId, decimal valor, DateOnly fechaValor, CancellationToken ct)
    {
        var r = await contrato.SimularAsync(new SolicitudDeSimulacion(Recaudador.Nomina, obligationPublicId, ModalidadDePago.FreeAmount, valor, fechaValor), ct);
        return r.Result == ResultadoDelContrato.Answered ? r.Value : null;
    }

    private static RespuestaDelContratoDto<T> SinRespuesta<T>() =>
        new(ResultadoDelContrato.Rejected, "Lending.Contract.NoAnswer", "Cartera no respondió por este ítem del lote.", default);
}
