using System.Text.Json.Serialization;
using FluentValidation;
using IngenIA365ERP.Application.Channel.Common;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Channel;
using IngenIA365ERP.Domain.Enums.Channel;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Channel.Destinations;

// Feature 021 (T040, T041; FR-004 a FR-006, FR-052; research R10): a dónde va el dinero de un concepto. El destino amarra un sitio
// de pasarela a un convenio de recaudo de clase pasarela (de él salen la cuenta propia, las comisiones y el formato del archivo de
// conciliación). Las credenciales del sitio NO se guardan en el ERP: se reenvían al canal, que las cifra y las verifica.

public sealed record ChannelDestinationDto(
    Guid PublicId,
    string Name,
    PaymentGateway Gateway,
    Guid SitePublicId,
    Guid CollectionAgreementPublicId,
    string CollectionAgreementName,
    bool OfferPse,
    bool OfferCard,
    TimeOnly ReversalCutoffLocalTime,
    CredentialsStatus CredentialsStatus,
    DateTime? CredentialsCheckedAt,
    string? LoginLast4,
    bool IsActive);

public sealed record ListChannelDestinationsQuery : IRequest<Result<IReadOnlyList<ChannelDestinationDto>>>;

public sealed class ListChannelDestinationsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListChannelDestinationsQuery, Result<IReadOnlyList<ChannelDestinationDto>>>
{
    public async Task<Result<IReadOnlyList<ChannelDestinationDto>>> Handle(ListChannelDestinationsQuery request, CancellationToken ct) =>
        Result.Success<IReadOnlyList<ChannelDestinationDto>>(await Destinos.ProyectarAsync(db, db.ChannelPaymentDestinations.AsNoTracking(), ct));
}

internal static class Destinos
{
    public static async Task<List<ChannelDestinationDto>> ProyectarAsync(IApplicationDbContext db, IQueryable<ChannelPaymentDestination> consulta, CancellationToken ct) =>
        await (from d in consulta
               join c in db.CollectionAgreements.AsNoTracking().IgnoreQueryFilters() on d.CollectionAgreementId equals c.Id
               orderby d.Name
               select new ChannelDestinationDto(d.PublicId, d.Name, d.Gateway, d.SitePublicId, c.PublicId, c.Name, d.OfferPse, d.OfferCard,
                   d.ReversalCutoffLocalTime, d.CredentialsStatus, d.CredentialsCheckedAt, d.LoginLast4, d.IsActive)).ToListAsync(ct);

    public const string ConvenioNoEsPasarela = "Channel.Destination.AgreementNotGateway";
    public const string NoExiste = "Channel.Destination.NotFound";

    /// <summary>El convenio debe existir, ser de clase pasarela y estar activo.</summary>
    public static async Task<Result<int>> ConvenioDePasarelaAsync(IApplicationDbContext db, Guid convenio, CancellationToken ct)
    {
        var c = await db.CollectionAgreements.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == convenio, ct);
        if (c is null) return Result.Failure<int>("Channel.Destination.AgreementNotFound", "El convenio de recaudo no existe.");
        if (c.Kind != CollectionAgreementKind.Gateway || !c.IsActive)
            return Result.Failure<int>(ConvenioNoEsPasarela, "El destino de pago necesita un convenio de recaudo activo de clase pasarela.");
        return Result.Success(c.Id);
    }
}

/// <summary>Crea o cambia un destino (con <paramref name="PublicId"/> cambia el existente).</summary>
public sealed record SaveChannelDestinationCommand(
    Guid? PublicId,
    string Name,
    Guid CollectionAgreementPublicId,
    bool OfferPse,
    bool OfferCard,
    TimeOnly ReversalCutoffLocalTime,
    bool IsActive) : IRequest<Result<ChannelDestinationDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SaveChannelDestinationCommandValidator : AbstractValidator<SaveChannelDestinationCommand>
{
    public SaveChannelDestinationCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Escribe el nombre del destino.").MaximumLength(120);
        RuleFor(x => x.CollectionAgreementPublicId).NotEmpty().WithMessage("Elige el convenio de recaudo de la pasarela.");
        RuleFor(x => x).Must(x => x.OfferPse || x.OfferCard).WithMessage("Ofrece al menos un medio de pago: PSE o tarjeta.");
    }
}

public sealed class SaveChannelDestinationCommandHandler(IApplicationDbContext db)
    : IRequestHandler<SaveChannelDestinationCommand, Result<ChannelDestinationDto>>
{
    public async Task<Result<ChannelDestinationDto>> Handle(SaveChannelDestinationCommand r, CancellationToken ct)
    {
        var convenio = await Destinos.ConvenioDePasarelaAsync(db, r.CollectionAgreementPublicId, ct);
        if (convenio.IsFailure) return Result.Failure<ChannelDestinationDto>(convenio.Error);

        ChannelPaymentDestination destino;
        if (r.PublicId is { } id)
        {
            var existente = await db.ChannelPaymentDestinations.FirstOrDefaultAsync(d => d.PublicId == id, ct);
            if (existente is null) return Result.Failure<ChannelDestinationDto>(Destinos.NoExiste, "El destino de pago no existe.");
            destino = existente;
        }
        else
        {
            destino = new ChannelPaymentDestination();
            db.ChannelPaymentDestinations.Add(destino);
        }

        destino.Name = r.Name.Trim();
        destino.CollectionAgreementId = convenio.Value;
        destino.OfferPse = r.OfferPse;
        destino.OfferCard = r.OfferCard;
        destino.ReversalCutoffLocalTime = r.ReversalCutoffLocalTime;
        destino.IsActive = r.IsActive;
        await db.SaveChangesAsync(ct);

        var dto = await Destinos.ProyectarAsync(db, db.ChannelPaymentDestinations.AsNoTracking().Where(d => d.Id == destino.Id), ct);
        return Result.Success(dto.Single());
    }
}

/// <summary>
/// R10: envía las credenciales del sitio al canal. El secreto no se guarda, no se registra y no entra a la auditoría
/// (<see cref="JsonIgnoreAttribute"/>: <c>AuditBehavior</c> serializa el comando).
/// </summary>
public sealed record SetChannelGatewayCredentialsCommand(
    Guid DestinationPublicId,
    string BaseUrl,
    string Login,
    [property: JsonIgnore] string SecretKey) : IRequest<Result<ChannelDestinationDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }

    public override string ToString() =>
        $"SetChannelGatewayCredentialsCommand {{ Destination = {DestinationPublicId}, BaseUrl = {BaseUrl}, Login = …{CredencialesDePasarela.Ultimos4(Login)} }}";
}

public sealed class SetChannelGatewayCredentialsCommandValidator : AbstractValidator<SetChannelGatewayCredentialsCommand>
{
    public SetChannelGatewayCredentialsCommandValidator()
    {
        RuleFor(x => x.DestinationPublicId).NotEmpty();
        RuleFor(x => x.BaseUrl).NotEmpty().WithMessage("Escribe la URL base de la pasarela.")
            .Must(u => Uri.TryCreate(u, UriKind.Absolute, out var url) && (url.Scheme == Uri.UriSchemeHttps || url.IsLoopback))
            .WithMessage("La URL base de la pasarela debe ser https.");
        RuleFor(x => x.Login).NotEmpty().WithMessage("Escribe el login del sitio.").MaximumLength(100);
        RuleFor(x => x.SecretKey).NotEmpty().WithMessage("Escribe la llave secreta del sitio.").MaximumLength(200);
    }
}

public sealed class SetChannelGatewayCredentialsCommandHandler(IApplicationDbContext db, ICanalSatelite canal, ICurrentTenantService cooperativa)
    : IRequestHandler<SetChannelGatewayCredentialsCommand, Result<ChannelDestinationDto>>
{
    public async Task<Result<ChannelDestinationDto>> Handle(SetChannelGatewayCredentialsCommand r, CancellationToken ct)
    {
        var destino = await db.ChannelPaymentDestinations.FirstOrDefaultAsync(d => d.PublicId == r.DestinationPublicId, ct);
        if (destino is null) return Result.Failure<ChannelDestinationDto>(Destinos.NoExiste, "El destino de pago no existe.");
        if (!Guid.TryParse(cooperativa.TenantId, out var publica)) return Result.Failure<ChannelDestinationDto>(Error.Validation);

        var medios = new List<string>();
        if (destino.OfferPse) medios.Add("pse");
        if (destino.OfferCard) medios.AddRange(["visa", "master"]);
        var verificacion = await canal.GuardarCredencialesAsync(publica, destino.SitePublicId,
            new CredencialesDePasarela(destino.Gateway.ToString(), r.BaseUrl.Trim(), r.Login.Trim(), r.SecretKey, medios), ct);
        if (verificacion.IsFailure) return Result.Failure<ChannelDestinationDto>(verificacion.Error);

        destino.CredentialsStatus = verificacion.Value switch
        {
            { Activa: true } => CredentialsStatus.Active,
            { VerificadaEn: null } => CredentialsStatus.PendingVerification,
            _ => CredentialsStatus.Failed,
        };
        destino.CredentialsCheckedAt = verificacion.Value.VerificadaEn;
        destino.LoginLast4 = verificacion.Value.LoginLast4;
        await db.SaveChangesAsync(ct);

        var dto = await Destinos.ProyectarAsync(db, db.ChannelPaymentDestinations.AsNoTracking().Where(d => d.Id == destino.Id), ct);
        return Result.Success(dto.Single());
    }
}
