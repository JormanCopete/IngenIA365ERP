using IngenIA365ERP.Application.Common.Mensajeria;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Documents;

/// <summary>
/// <c>fe.emitir</c> (feature 023; contracts/mensajeria.md §4): un intento por el comando de siempre, que toma el arrendamiento de la fila, respeta
/// la espera, consulta antes de reenviar lo ambiguo y registra el resultado. Si el documento no está, ya terminó, no le toca o lo tiene otro, el
/// comando no llama al canal y el aviso se confirma sin efecto. (nuevo)
/// </summary>
public sealed class ManejadorDeEmision(ILogger<ManejadorDeEmision> logger) : IManejadorDeAviso
{
    public string Tipo => TiposDeAviso.FeEmitir;

    public async Task ManejarAsync(Aviso aviso, IServiceProvider servicios, CancellationToken ct)
    {
        if (aviso.EntidadComoGuid is not { } documento) return;
        var r = await servicios.GetRequiredService<ISender>().Send(new EmitElectronicDocumentCommand(documento), ct);
        if (r.IsFailure && r.Error.Code != ErroresDeDocumentosElectronicos.InProgressCode)
            logger.LogInformation("[FE.Cola.SinIntento] {TenantPublicId} {DocumentoPublicId}: {Codigo} {Motivo}",
                aviso.Cooperativa, documento, r.Error.Code, r.Error.Message);
        else if (r.IsSuccess && r.Value.Attempted)
            logger.LogInformation("[FE.Cola.Intento] {TenantPublicId} {DocumentoPublicId}: {Resultado} → {Estado}.",
                aviso.Cooperativa, documento, r.Value.Outcome, r.Value.Status);
    }
}

/// <summary><c>fe.seguimiento</c> (feature 023): el seguimiento durable del documento (<see cref="CompleteElectronicDocumentFollowUpCommand"/>). (nuevo)</summary>
public sealed class ManejadorDeSeguimiento(ILogger<ManejadorDeSeguimiento> logger) : IManejadorDeAviso
{
    public string Tipo => TiposDeAviso.FeSeguimiento;

    public async Task ManejarAsync(Aviso aviso, IServiceProvider servicios, CancellationToken ct)
    {
        if (aviso.EntidadComoGuid is not { } documento) return;
        var r = await servicios.GetRequiredService<ISender>().Send(new CompleteElectronicDocumentFollowUpCommand(documento), ct);
        if (r.IsFailure)
            logger.LogInformation("[FE.Cola.SinSeguimiento] {TenantPublicId} {DocumentoPublicId}: {Codigo} {Motivo}",
                aviso.Cooperativa, documento, r.Error.Code, r.Error.Message);
        else if (!r.Value.Hecho)
            logger.LogInformation("[FE.Cola.SeguimientoPendiente] {TenantPublicId} {DocumentoPublicId}: {Detalle}",
                aviso.Cooperativa, documento, r.Value.Detalle);
    }
}
