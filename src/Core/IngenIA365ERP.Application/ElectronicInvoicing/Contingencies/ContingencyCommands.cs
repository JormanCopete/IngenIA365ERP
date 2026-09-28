using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing.Numeracion;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Contingencies;

/// <summary>
/// Declarar una contingencia de facturación, tipo 03 (feature 012, I4, T726; FR-067; contracts/dian.md §7.2; api.md §24.6
/// <c>POST /contingencies</c>, <c>ElectronicInvoicing.Contingencies.Declare</c>): falla el facturador, su conexión o su proveedor, y una
/// persona la declara con motivo en el canal de la configuración vigente. Mientras esté abierta, las ventas fiscales <b>nuevas</b> se numeran
/// con la resolución de contingencia y se transmiten al cerrarla. La 04 la declara <b>sólo</b> el canal (<c>DianUnavailable</c>) →
/// 422 <c>ElectronicInvoicing.Contingency.Dian04OnlyByChannel</c>; con otra 03 abierta en el canal → 422 <c>.AlreadyOpen</c>. Levanta
/// <c>Dian.ContingenciaAbierta</c> después de guardar. <see cref="EvidenceNote"/> queda con el motivo (la evidencia en archivo se sube por
/// las rutas de adjuntos con dueño <c>DianContingencyEvent</c>). (nuevo)
/// </summary>
public sealed record OpenContingencyCommand(ContingencyType Type, string Reason, DateTime? StartedAt = null, string? EvidenceNote = null)
    : IRequest<Result<DianContingencyEventDto>>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class OpenContingencyCommandValidator : ValidadorConMotivo<OpenContingencyCommand>
{
    public OpenContingencyCommandValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.EvidenceNote).MaximumLength(LargoMaximo);
    }
}

public sealed class OpenContingencyCommandHandler(
    IApplicationDbContext db,
    ContingenciaDeLaDian contingencias,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<OpenContingencyCommand, Result<DianContingencyEventDto>>
{
    public async Task<Result<DianContingencyEventDto>> Handle(OpenContingencyCommand request, CancellationToken ct)
    {
        if (request.Type != ContingencyType.Issuer03) return Falla(ErroresDeContingencias.Dian04OnlyByChannel());

        var ahora = reloj.UtcNow;
        var inicio = request.StartedAt is { } s ? Utc(s) : ahora;
        if (inicio > ahora) return Falla(ErroresDeContingencias.InvalidDates("La contingencia no puede empezar en el futuro."));

        var hoy = reloj.HoyLocal;
        var vigente = (await db.ElectronicEmissionSettings.AsNoTracking().Where(x => x.IsEnabled).ToListAsync(ct))
            .Where(x => x.VigenteEn(hoy)).OrderByDescending(x => x.ValidFrom).FirstOrDefault();
        if (vigente is null) return Falla(ErroresDeNumeracionYConfiguracion.SettingsMissing());

        var actor = await actorActual.ObtenerAsync(ct);
        var abierto = await contingencias.AbrirDeFacturacionAsync(ReglasDeResolucion.Canal(vigente.ChannelCode), inicio,
            new QuienDeclara(actor.Kind, actor.UserId, actor.Name), ConsultasDeContingencias.ConEvidencia(request.Reason, request.EvidenceNote), ct);
        if (abierto.IsFailure) return Falla(abierto.Error);

        await db.SaveChangesAsync(ct);
        await contingencias.AvisarAperturaAsync(abierto.Value, ct);
        return Result.Success(ConsultasDeContingencias.Dto(abierto.Value, []));
    }

    internal static DateTime Utc(DateTime d) => d.Kind switch
    {
        DateTimeKind.Utc => d,
        DateTimeKind.Local => d.ToUniversalTime(),
        _ => DateTime.SpecifyKind(d, DateTimeKind.Utc),
    };

    private static Result<DianContingencyEventDto> Falla(Error error) => Result.Failure<DianContingencyEventDto>(error);
}

/// <summary>
/// Cerrar una contingencia 03 o 04 a mano (feature 012, I4, T726; contracts/dian.md §7.1–§7.3; api.md §24.6
/// <c>POST /contingencies/{id}/close</c>, <c>ElectronicInvoicing.Contingencies.Declare</c>): fija <c>EndedAt</c> (el indicado o ahora), copia
/// <c>Dian.PlazoContingenciaHoras</c> y su <c>LegalSource</c> vigentes, fija <c>DeadlineAt</c> y el <c>TransmissionDeadline</c> de cada
/// documento del evento con <c>PlazoDeContingencia</c> (factura, DEE y notas desde el fin; documento soporte y su nota desde las 00:00 del día
/// siguiente) y los deja listos para el procesador, que transmite en orden de consecutivo como <c>TransmitContingency</c>. La 04 se cierra
/// también sola con la primera respuesta definitiva de la DIAN posterior a su inicio (<see cref="ContingenciaDeLaDian.CerrarPorRespuestaAsync"/>).
/// (nuevo)
/// </summary>
public sealed record CloseContingencyCommand(Guid ContingencyPublicId, string Reason, DateTime? EndedAt = null, string? EvidenceNote = null)
    : IRequest<Result<DianContingencyEventDto>>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class CloseContingencyCommandValidator : ValidadorConMotivo<CloseContingencyCommand>
{
    public CloseContingencyCommandValidator()
    {
        RuleFor(x => x.ContingencyPublicId).NotEmpty();
        RuleFor(x => x.EvidenceNote).MaximumLength(LargoMaximo);
    }
}

public sealed class CloseContingencyCommandHandler(
    IApplicationDbContext db,
    ContingenciaDeLaDian contingencias,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<CloseContingencyCommand, Result<DianContingencyEventDto>>
{
    public async Task<Result<DianContingencyEventDto>> Handle(CloseContingencyCommand request, CancellationToken ct)
    {
        var evento = await db.DianContingencyEvents.FirstOrDefaultAsync(e => e.PublicId == request.ContingencyPublicId, ct);
        if (evento is null) return Falla(ErroresDeContingencias.NotFound());
        if (!evento.EstaAbierto) return Falla(ErroresDeContingencias.AlreadyClosed());

        var ahora = reloj.UtcNow;
        var fin = request.EndedAt is { } e ? OpenContingencyCommandHandler.Utc(e) : ahora;
        if (fin > ahora) return Falla(ErroresDeContingencias.InvalidDates("La contingencia no puede cerrarse en el futuro."));
        if (fin < evento.StartedAt) return Falla(ErroresDeContingencias.InvalidDates("La contingencia no puede terminar antes de empezar."));

        var actor = await actorActual.ObtenerAsync(ct);
        await contingencias.CerrarAsync(evento, fin, new QuienDeclara(actor.Kind, actor.UserId, actor.Name),
            ConsultasDeContingencias.ConEvidencia(request.Reason, request.EvidenceNote), ct);
        await db.SaveChangesAsync(ct);

        var documentos = await ConsultasDeContingencias.EstadosAsync(db, [evento.Id], ct);
        return Result.Success(ConsultasDeContingencias.Dto(evento, documentos.GetValueOrDefault(evento.Id) ?? []));
    }

    private static Result<DianContingencyEventDto> Falla(Error error) => Result.Failure<DianContingencyEventDto>(error);
}
