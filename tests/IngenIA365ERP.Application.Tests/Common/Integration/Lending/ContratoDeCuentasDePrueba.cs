using IngenIA365ERP.Application.Common.Integration.Lending;

namespace IngenIA365ERP.Application.Tests.Common.Integration.Lending;

/// <summary>
/// Doble de <see cref="IContratoDeCuentas"/> (feature 014, T089): lista las cuotas exigibles que la prueba ponga en <see cref="Exigibles"/>,
/// consigna lo enviado (<c>Applied</c>, o lo que diga <see cref="ComoRespondeConsignar"/> por cuenta), registra los no descontados y reversa
/// (<c>Applied</c> salvo <see cref="RechazarReverso"/>). No escribe nada. Sólo vive en <c>tests/</c>.
/// </summary>
public sealed class ContratoDeCuentasDePrueba : IContratoDeCuentas
{
    public List<ExigibleDeCuentaDto> Exigibles { get; } = [];
    public Dictionary<Guid, (ResultadoDelContrato Resultado, string Codigo, string Mensaje)> ComoRespondeConsignar { get; } = new();
    public bool RechazarReverso { get; set; }
    public bool FallaAlListar { get; set; }

    public List<SolicitudDeExigibles> Listados { get; } = [];
    public List<SolicitudDeConsignacion> Consignaciones { get; } = [];
    public List<SolicitudDeReversoDeCuenta> Reversos { get; } = [];

    /// <summary>Una cuota de aportes exigible para la persona.</summary>
    public ExigibleDeCuentaDto Cuota(Guid persona, Guid cuenta, decimal valor, string periodo = "2026-03", DateOnly? vence = null,
        NaturalezaDeCuenta naturaleza = NaturalezaDeCuenta.Contribution, string concepto = "DESC_APORTES", int orden = 0)
    {
        var cuota = new ExigibleDeCuentaDto(persona, cuenta, $"AP-{Exigibles.Count + 1:000}", naturaleza, null, "APORTES", Guid.NewGuid(), periodo,
            vence ?? new DateOnly(2026, 3, 31), valor, valor, false, "None", concepto, orden);
        Exigibles.Add(cuota);
        return cuota;
    }

    public Task<RespuestaDelContratoDto<CuentasDePersonaDto>> ConsultarAsync(SolicitudDeConsultaDeCuentas solicitud, CancellationToken ct) =>
        throw new NotSupportedException("El doble de nómina no consulta cuentas.");

    public Task<RespuestaDelContratoDto<ExigiblesDeCuentasDto>> ListarExigiblesAsync(SolicitudDeExigibles solicitud, CancellationToken ct)
    {
        Listados.Add(solicitud);
        if (FallaAlListar) throw new TimeoutException("La Cartera no respondió.");
        var cuotas = Exigibles.Where(e => solicitud.PersonPublicIds.Contains(e.PersonPublicId)).ToList();
        return Task.FromResult(new RespuestaDelContratoDto<ExigiblesDeCuentasDto>(ResultadoDelContrato.Answered, "Lending.AccountContract.Listed", "Listado.",
            new ExigiblesDeCuentasDto(EstadoDeCartera.Active, EstadoDeCartera.Active, cuotas, [])));
    }

    public Task<RespuestaDelContratoDto<SimulacionDeCuentaDto>> SimularAsync(SolicitudDeSimulacionDeCuenta solicitud, CancellationToken ct) =>
        throw new NotSupportedException("El doble de nómina no simula.");

    public Task<RespuestaDeConsignacionDto> ConsignarAsync(SolicitudDeConsignacion solicitud, CancellationToken ct)
    {
        Consignaciones.Add(solicitud);
        var depositos = solicitud.Deposits.Select(d =>
        {
            if (ComoRespondeConsignar.TryGetValue(d.AccountPublicId, out var r) && r.Resultado == ResultadoDelContrato.Rejected)
                return new RespuestaDelContratoDto<ComprobanteDeCuentaDto>(r.Resultado, r.Codigo, r.Mensaje, null);
            var comprobante = new ComprobanteDeCuentaDto(Guid.NewGuid(), $"CNC-{Consignaciones.Count:000}", d.AccountPublicId, "AP", d.IdempotencyKey, "Deposit",
                d.ValueDate, d.ValueDate, DateTime.UtcNow, d.Amount, [], null, [], [], new DisponibleDto(d.Amount, 0m, 0m, 0m, 0m, 0m), null, null, false, "hash", "v1", []);
            return new RespuestaDelContratoDto<ComprobanteDeCuentaDto>(ResultadoDelContrato.Applied, "Lending.AccountContract.Applied", "Consignación aplicada.", comprobante);
        }).ToList();
        var noDescontados = solicitud.NotDeducted.Select(n => new RespuestaDelContratoDto<NoDescontadoDeCuentaRegistradoDto>(ResultadoDelContrato.Applied,
            "Lending.AccountContract.Applied", "No descontado registrado: la cuota sigue exigible.",
            new NoDescontadoDeCuentaRegistradoDto(n.InstallmentPublicId, n.NotDeductedAmount, n.Reason.ToString()))).ToList();
        return Task.FromResult(new RespuestaDeConsignacionDto(depositos, noDescontados));
    }

    public Task<RespuestaDelContratoDto<ComprobanteDeCuentaDto>> RetirarAsync(SolicitudDeRetiro solicitud, CancellationToken ct) =>
        throw new NotSupportedException("El doble de nómina no retira.");

    public Task<RespuestaDelContratoDto<ComprobanteDeCuentaDto>> TrasladarAsync(SolicitudDeTraslado solicitud, CancellationToken ct) =>
        throw new NotSupportedException("El doble de nómina no traslada.");

    public Task<RespuestaDelContratoDto<ComprobanteDeReversoDeCuentaDto>> ReversarAsync(SolicitudDeReversoDeCuenta solicitud, CancellationToken ct)
    {
        Reversos.Add(solicitud);
        if (RechazarReverso)
            return Task.FromResult(new RespuestaDelContratoDto<ComprobanteDeReversoDeCuentaDto>(ResultadoDelContrato.Rejected,
                "Lending.AccountContract.TechnicalOverdraft", "La cuenta ya no tiene con qué reversar.", null));
        var reverso = new ComprobanteDeReversoDeCuentaDto(Guid.NewGuid(), $"RVC-{Reversos.Count:000}", Guid.NewGuid(), "CNC", [], DateOnly.FromDateTime(DateTime.UtcNow),
            DateTime.UtcNow, 0m, [], false, []);
        return Task.FromResult(new RespuestaDelContratoDto<ComprobanteDeReversoDeCuentaDto>(ResultadoDelContrato.Applied, "Lending.AccountContract.Reversed",
            "Consignación reversada.", reverso));
    }
}
