using IngenIA365ERP.Application.Common.Integration.Lending;

namespace IngenIA365ERP.Application.Tests.Common.Integration.Lending;

/// <summary>
/// Doble de <see cref="IContratoDeObligaciones"/> (feature 013, T0154, R118): registra cada llamada y responde por ítem lo que la
/// prueba configure por obligación (<see cref="ComoRespondeAplicar"/>): <c>Applied</c> (por defecto, todo lo enviado), <c>Rejected</c>,
/// con excedente o <c>AlreadyApplied</c>. Reversar responde <c>Applied</c> salvo que la prueba lo rechace. No escribe nada. Sólo vive en
/// <c>tests/</c>.
/// </summary>
public sealed class ContratoDeObligacionesDePrueba : IContratoDeObligaciones
{
    /// <summary>Cómo responder un pago a una obligación.</summary>
    public sealed record Respuesta(ResultadoDelContrato Resultado, decimal? Excedente = null, string Codigo = "Lending.ObligationContract.Applied", string Mensaje = "Pago aplicado.", decimal SaldoQueQueda = 0m);

    public Dictionary<Guid, Respuesta> ComoRespondeAplicar { get; } = new();
    public Dictionary<Guid, Respuesta> ComoRespondeReversar { get; } = new();
    public decimal? SaldoSimulado { get; set; }

    public List<SolicitudDeAplicacion> Aplicaciones { get; } = [];
    public List<SolicitudDeReverso> Reversos { get; } = [];
    public List<SolicitudDeSimulacion> Simulaciones { get; } = [];
    public List<SolicitudDeListado> Listados { get; } = [];
    public List<SolicitudDeResolucion> Resoluciones { get; } = [];

    public static Respuesta Rechazo(string codigo = "Lending.Payment.ObligationNotPayable", string mensaje = "La obligación ya no recibe pagos.") =>
        new(ResultadoDelContrato.Rejected, null, codigo, mensaje);

    public Task<RespuestaDelContratoDto<ListadoDeObligacionesDto>> ListarAsync(SolicitudDeListado solicitud, CancellationToken ct)
    {
        Listados.Add(solicitud);
        return Task.FromResult(new RespuestaDelContratoDto<ListadoDeObligacionesDto>(ResultadoDelContrato.Answered, "Lending.ObligationContract.Listed", "Listado.",
            new ListadoDeObligacionesDto(EstadoDeCartera.Active, solicitud.PersonPublicId, solicitud.Date, [])));
    }

    public Task<RespuestaDelContratoDto<SimulacionDePagoDto>> SimularAsync(SolicitudDeSimulacion solicitud, CancellationToken ct)
    {
        Simulaciones.Add(solicitud);
        if (SaldoSimulado is not { } saldo)
            return Task.FromResult(new RespuestaDelContratoDto<SimulacionDePagoDto>(ResultadoDelContrato.Answered, "Lending.NotActivated", "Sin activar.", null));
        var sim = new SimulacionDePagoDto("sim", solicitud.ObligationPublicId, solicitud.Modality, solicitud.Amount, solicitud.ValueDate, null,
            solicitud.Amount, 0m, 0m, 0m, [], Saldos(saldo), null, [], []);
        return Task.FromResult(new RespuestaDelContratoDto<SimulacionDePagoDto>(ResultadoDelContrato.Answered, "Lending.ObligationContract.Simulated", "Simulado.", sim));
    }

    public Task<RespuestaDeAplicacionDto> AplicarAsync(SolicitudDeAplicacion solicitud, CancellationToken ct)
    {
        Aplicaciones.Add(solicitud);
        var pagos = solicitud.Payments.Select(p =>
        {
            var r = ComoRespondeAplicar.GetValueOrDefault(p.ObligationPublicId) ?? new Respuesta(ResultadoDelContrato.Applied);
            if (r.Resultado == ResultadoDelContrato.Rejected)
                return new RespuestaDelContratoDto<ComprobanteDeAplicacionDto>(r.Resultado, r.Codigo, r.Mensaje, null);
            var excedente = Math.Min(p.Amount, r.Excedente ?? 0m);
            var comprobante = new ComprobanteDeAplicacionDto(Guid.NewGuid(), $"PAG-{Aplicaciones.Count:000}", p.ObligationPublicId, "OBL", p.IdempotencyKey,
                p.Modality, p.ValueDate, p.ValueDate, DateTime.UtcNow, p.Amount, p.Amount - excedente, 0m, 0m, excedente, [], Saldos(r.SaldoQueQueda), null,
                r.SaldoQueQueda == 0m, null, "hash", "v1", []);
            return new RespuestaDelContratoDto<ComprobanteDeAplicacionDto>(r.Resultado, r.Codigo, r.Mensaje, comprobante);
        }).ToList();
        var noDescontados = solicitud.NotDeducted.Select(n => new RespuestaDelContratoDto<NoDescontadoRegistradoDto>(
            ResultadoDelContrato.Applied, "Lending.ObligationContract.Applied", "No descontado registrado: la cuota sigue exigible.",
            new NoDescontadoRegistradoDto(Guid.NewGuid(), n.ObligationPublicId, n.InstallmentNumber, n.NotDeductedAmount))).ToList();
        return Task.FromResult(new RespuestaDeAplicacionDto(pagos, noDescontados));
    }

    public Task<RespuestaDelContratoDto<ComprobanteDeReversoDto>> ReversarAsync(SolicitudDeReverso solicitud, CancellationToken ct)
    {
        Reversos.Add(solicitud);
        var obligacion = Aplicaciones.SelectMany(a => a.Payments).FirstOrDefault(p => p.IdempotencyKey == solicitud.PaymentIdempotencyKey)?.ObligationPublicId
                         ?? ObligacionDeLaClave(solicitud.PaymentIdempotencyKey);
        var r = ComoRespondeReversar.GetValueOrDefault(obligacion);
        if (r is { Resultado: ResultadoDelContrato.Rejected })
            return Task.FromResult(new RespuestaDelContratoDto<ComprobanteDeReversoDto>(r.Resultado, r.Codigo, r.Mensaje, null));
        var reverso = new ComprobanteDeReversoDto(Guid.NewGuid(), $"REV-{Reversos.Count:000}", Guid.NewGuid(), "PAG", obligacion, "OBL",
            DateOnly.FromDateTime(DateTime.UtcNow), DateTime.UtcNow, 0m, solicitud.Reason, [], [], Saldos(0m), false, []);
        return Task.FromResult(new RespuestaDelContratoDto<ComprobanteDeReversoDto>(ResultadoDelContrato.Applied, "Lending.ObligationContract.Reversed", "Pago reversado.", reverso));
    }

    public Task<RespuestaDelContratoDto<ComprobanteDeResolucionDto>> ResolverSaldoAFavorAsync(SolicitudDeResolucion solicitud, CancellationToken ct)
    {
        Resoluciones.Add(solicitud);
        return Task.FromResult(new RespuestaDelContratoDto<ComprobanteDeResolucionDto>(ResultadoDelContrato.Rejected, "Lending.NotActivated", "Sin activar.", null));
    }

    private static Guid ObligacionDeLaClave(string clave) =>
        clave.Split(':') is [_, _, var o] && Guid.TryParseExact(o, "N", out var g) ? g : Guid.Empty;

    private static SaldosDeObligacionDto Saldos(decimal total) => new(total, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, total, 0);
}
