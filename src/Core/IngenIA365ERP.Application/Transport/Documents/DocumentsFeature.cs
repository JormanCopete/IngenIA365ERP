using FluentValidation;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Transport.Vehicles;
using IngenIA365ERP.Domain.Entities.Transport;
using IngenIA365ERP.Domain.Enums.Transport;
using IngenIA365ERP.Domain.Transport;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Transport.Documents;

// Feature 020 (US4; R7; T072–T074; FR-020 a FR-023; contracts/api.md §3, «Documentos»): tipos de documento por cooperativa, documentos del
// vehículo (uno vigente por tipo; el anterior queda en la historia), tablero de vencimientos. Nada bloquea: un vencimiento sólo se informa.

public sealed record DocumentTypeDto(Guid PublicId, string Code, string Name, bool IsRequired, int WarningDays, bool IsActive, bool IsSystem);

/// <summary>Los tipos de documento de la cooperativa.</summary>
public sealed record ListDocumentTypesQuery(bool IncludeInactive = false) : IRequest<Result<IReadOnlyList<DocumentTypeDto>>>;

public sealed class ListDocumentTypesQueryHandler(IApplicationDbContext db) : IRequestHandler<ListDocumentTypesQuery, Result<IReadOnlyList<DocumentTypeDto>>>
{
    public async Task<Result<IReadOnlyList<DocumentTypeDto>>> Handle(ListDocumentTypesQuery request, CancellationToken ct)
    {
        var q = db.TransportDocumentTypes.AsNoTracking();
        if (!request.IncludeInactive) q = q.Where(t => t.IsActive);
        return Result.Success<IReadOnlyList<DocumentTypeDto>>(await q.OrderBy(t => t.Name)
            .Select(t => new DocumentTypeDto(t.PublicId, t.Code, t.Name, t.IsRequired, t.WarningDays, t.IsActive, t.IsSystem)).ToListAsync(ct));
    }
}

/// <summary>Crea un tipo de documento (FR-020).</summary>
public sealed record CreateDocumentTypeCommand(string Code, string Name, bool IsRequired, int WarningDays) : IRequest<Result<DocumentTypeDto>>;

public sealed class CreateDocumentTypeCommandValidator : AbstractValidator<CreateDocumentTypeCommand>
{
    public CreateDocumentTypeCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(CodigoDeCatalogo.LargoCorto).Matches(CodigoDeCatalogo.Patron).WithMessage(CodigoDeCatalogo.MensajeDePatron);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.WarningDays).InclusiveBetween(0, TransportDocumentType.DiasDeAvisoMaximos);
    }
}

public sealed class CreateDocumentTypeCommandHandler(IApplicationDbContext db) : IRequestHandler<CreateDocumentTypeCommand, Result<DocumentTypeDto>>
{
    public async Task<Result<DocumentTypeDto>> Handle(CreateDocumentTypeCommand request, CancellationToken ct)
    {
        var codigo = CodigoDeCatalogo.Normalizar(request.Code)!;
        if (await db.TransportDocumentTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Code == codigo, ct) is { } existente)
            return Result.Failure<DocumentTypeDto>(CodigoDeCatalogo.Duplicado("un tipo de documento", codigo, existente.Name, existente.PublicId));
        var t = new TransportDocumentType { Code = codigo, Name = request.Name.Trim(), IsRequired = request.IsRequired, WarningDays = request.WarningDays };
        db.TransportDocumentTypes.Add(t);
        await db.SaveChangesAsync(ct);
        return Result.Success(new DocumentTypeDto(t.PublicId, t.Code, t.Name, t.IsRequired, t.WarningDays, t.IsActive, t.IsSystem));
    }
}

/// <summary>Cambia nombre, obligatoriedad, días de aviso o estado de un tipo de documento. El código no cambia; nunca se borra.</summary>
public sealed record UpdateDocumentTypeCommand(Guid PublicId, string Name, bool IsRequired, int WarningDays, bool IsActive) : IRequest<Result<DocumentTypeDto>>;

public sealed class UpdateDocumentTypeCommandValidator : AbstractValidator<UpdateDocumentTypeCommand>
{
    public UpdateDocumentTypeCommandValidator()
    {
        RuleFor(x => x.PublicId).NotEqual(Guid.Empty);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.WarningDays).InclusiveBetween(0, TransportDocumentType.DiasDeAvisoMaximos);
    }
}

public sealed class UpdateDocumentTypeCommandHandler(IApplicationDbContext db) : IRequestHandler<UpdateDocumentTypeCommand, Result<DocumentTypeDto>>
{
    public async Task<Result<DocumentTypeDto>> Handle(UpdateDocumentTypeCommand request, CancellationToken ct)
    {
        var t = await db.TransportDocumentTypes.FirstOrDefaultAsync(x => x.PublicId == request.PublicId, ct);
        if (t is null) return Result.Failure<DocumentTypeDto>("Transport.DocumentTypeNotFound", $"No existe el tipo de documento {request.PublicId:D}.");
        t.Name = request.Name.Trim();
        t.IsRequired = request.IsRequired;
        t.WarningDays = request.WarningDays;
        t.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return Result.Success(new DocumentTypeDto(t.PublicId, t.Code, t.Name, t.IsRequired, t.WarningDays, t.IsActive, t.IsSystem));
    }
}

/// <summary>Los datos de un documento del vehículo, de pantalla, API o plantilla.</summary>
public sealed record VehicleDocumentInput(Guid DocumentTypePublicId, string Number, string? Issuer, DateOnly? IssuedOn, DateOnly? ValidFrom, DateOnly ExpiresOn);

public sealed class VehicleDocumentInputValidator : AbstractValidator<VehicleDocumentInput>
{
    public VehicleDocumentInputValidator()
    {
        RuleFor(x => x.DocumentTypePublicId).NotEqual(Guid.Empty).WithMessage("Indique el tipo de documento.");
        RuleFor(x => x.Number).NotEmpty().WithMessage("Indique el número del documento.").MaximumLength(40);
        RuleFor(x => x.Issuer).MaximumLength(120);
        RuleFor(x => x.ExpiresOn).NotEqual(default(DateOnly)).WithMessage("Indique el vencimiento.");
        RuleFor(x => x.ExpiresOn).GreaterThanOrEqualTo(x => x.ValidFrom!.Value).When(x => x.ValidFrom is not null)
            .WithMessage("El vencimiento no puede ser anterior al inicio de vigencia.");
    }
}

/// <summary>
/// Registra el documento de un vehículo (R7, T073): marca como reemplazado el vigente del mismo tipo y deja éste vigente (FR-021). Lo usan
/// la pantalla, la API y la plantilla <c>transport.documents</c>. <b>No guarda.</b>
/// </summary>
public sealed class RegistrarDocumento(IApplicationDbContext db, IDateTimeService reloj)
{
    public async Task<Result<VehicleDocument>> RegistrarAsync(Vehicle vehiculo, VehicleDocumentInput d, CancellationToken ct)
    {
        if (vehiculo.EstaRetirado) return Result.Failure<VehicleDocument>(ErroresDeTransporte.Retirado(vehiculo.Plate));
        var tipo = await db.TransportDocumentTypes.AsNoTracking().FirstOrDefaultAsync(t => t.PublicId == d.DocumentTypePublicId, ct);
        if (tipo is null || !tipo.IsActive)
            return Result.Failure<VehicleDocument>("Transport.DocumentTypeNotFound", "El tipo de documento no existe o está inactivo.");

        var ahora = reloj.UtcNow;
        var vigentes = await db.VehicleDocuments.Where(x => x.VehicleId == vehiculo.Id && x.DocumentTypeId == tipo.Id && x.SupersededAt == null).ToListAsync(ct);
        vigentes.AddRange(db.VehicleDocuments.Local.Where(x => ReferenceEquals(x.Vehicle, vehiculo) && x.DocumentTypeId == tipo.Id && x.SupersededAt == null
                                                              && !vigentes.Contains(x)));
        foreach (var v in vigentes) v.SupersededAt = ahora;

        var doc = new VehicleDocument
        {
            VehicleId = vehiculo.Id,
            Vehicle = vehiculo.Id == 0 ? vehiculo : null,
            DocumentTypeId = tipo.Id,
            Number = d.Number.Trim().ToUpperInvariant(),
            Issuer = string.IsNullOrWhiteSpace(d.Issuer) ? null : d.Issuer.Trim(),
            IssuedOn = d.IssuedOn,
            ValidFrom = d.ValidFrom,
            ExpiresOn = d.ExpiresOn,
            CreatedAt = ahora,
        };
        db.VehicleDocuments.Add(doc);
        return Result.Success(doc);
    }
}

/// <summary>Registra o renueva un documento del vehículo.</summary>
public sealed record AddVehicleDocumentCommand(Guid VehiclePublicId, VehicleDocumentInput Document) : IRequest<Result<Guid>>;

public sealed class AddVehicleDocumentCommandValidator : AbstractValidator<AddVehicleDocumentCommand>
{
    public AddVehicleDocumentCommandValidator()
    {
        RuleFor(x => x.VehiclePublicId).NotEqual(Guid.Empty);
        RuleFor(x => x.Document).NotNull().SetValidator(new VehicleDocumentInputValidator());
    }
}

public sealed class AddVehicleDocumentCommandHandler(IApplicationDbContext db, RegistrarDocumento registrar) : IRequestHandler<AddVehicleDocumentCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(AddVehicleDocumentCommand request, CancellationToken ct)
    {
        var v = await db.Vehicles.FirstOrDefaultAsync(x => x.PublicId == request.VehiclePublicId, ct);
        if (v is null) return Result.Failure<Guid>(ErroresDeTransporte.VehiculoNoEncontrado(request.VehiclePublicId));
        var r = await registrar.RegistrarAsync(v, request.Document, ct);
        if (r.IsFailure) return Result.Failure<Guid>(r.Error);
        await db.SaveChangesAsync(ct);
        return Result.Success(r.Value.PublicId);
    }
}

/// <summary>Corrige un documento (número, emisor, fechas). Corregir no crea historia.</summary>
public sealed record CorrectVehicleDocumentCommand(Guid VehiclePublicId, Guid DocumentPublicId, VehicleDocumentInput Document) : IRequest<Result>;

public sealed class CorrectVehicleDocumentCommandValidator : AbstractValidator<CorrectVehicleDocumentCommand>
{
    public CorrectVehicleDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentPublicId).NotEqual(Guid.Empty);
        RuleFor(x => x.Document).NotNull().SetValidator(new VehicleDocumentInputValidator());
    }
}

public sealed class CorrectVehicleDocumentCommandHandler(IApplicationDbContext db) : IRequestHandler<CorrectVehicleDocumentCommand, Result>
{
    public async Task<Result> Handle(CorrectVehicleDocumentCommand request, CancellationToken ct)
    {
        var doc = await db.VehicleDocuments.Include(d => d.Vehicle)
            .FirstOrDefaultAsync(d => d.PublicId == request.DocumentPublicId && d.Vehicle!.PublicId == request.VehiclePublicId, ct);
        if (doc is null) return Result.Failure("Transport.DocumentNotFound", "No existe ese documento del vehículo.");
        if (doc.Vehicle!.EstaRetirado) return Result.Failure(ErroresDeTransporte.Retirado(doc.Vehicle.Plate));
        var d = request.Document;
        doc.Number = d.Number.Trim().ToUpperInvariant();
        doc.Issuer = string.IsNullOrWhiteSpace(d.Issuer) ? null : d.Issuer.Trim();
        doc.IssuedOn = d.IssuedOn;
        doc.ValidFrom = d.ValidFrom;
        doc.ExpiresOn = d.ExpiresOn;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>Una fila del tablero de vencimientos (FR-022).</summary>
public sealed record DocumentExpirationDto(
    Guid VehiclePublicId, string Plate, string InternalNumber, VehicleStatus VehicleStatus, Guid OwnerPersonPublicId, string OwnerTaxId, string OwnerName,
    string DocumentTypeCode, string DocumentTypeName, string Situation, string? Number, DateOnly? ExpiresOn, int? DaysToExpire);

/// <summary>Los conteos del tablero.</summary>
public sealed record DocumentExpirationsDto(int Expired, int Expiring, int Missing, IReadOnlyList<DocumentExpirationDto> Items);

/// <summary>
/// El tablero de vencimientos (T074; FR-022): de los vehículos vivos (activos e inactivos), lo vencido, lo que vence dentro de los días de aviso
/// de cada tipo y los tipos obligatorios que faltan. <paramref name="Status"/> filtra por situación; <paramref name="Days"/> amplía «por vencer»
/// a una ventana fija en lugar de los días de cada tipo.
/// </summary>
public sealed record GetDocumentExpirationsQuery(string? Status = null, int? Days = null, bool IncludeCurrent = false) : IRequest<Result<DocumentExpirationsDto>>;

public sealed class GetDocumentExpirationsQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<GetDocumentExpirationsQuery, Result<DocumentExpirationsDto>>
{
    public async Task<Result<DocumentExpirationsDto>> Handle(GetDocumentExpirationsQuery request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var tipos = await db.TransportDocumentTypes.AsNoTracking().Where(t => t.IsActive)
            .Select(t => new TipoParaVencimiento(t.Id, t.Code, t.Name, t.IsRequired, request.Days ?? t.WarningDays)).ToListAsync(ct);
        var vehiculos = await db.Vehicles.AsNoTracking().Where(v => v.Status != VehicleStatus.Retired)
            .Select(v => new { v.Id, v.PublicId, v.Plate, v.InternalNumber, v.Status, Dueño = v.OwnerPerson! }).ToListAsync(ct);
        var ids = vehiculos.Select(v => v.Id).ToList();
        var docs = await db.VehicleDocuments.AsNoTracking().Where(d => ids.Contains(d.VehicleId) && d.SupersededAt == null)
            .Select(d => new { d.VehicleId, d.DocumentTypeId, d.Number, d.ExpiresOn }).ToListAsync(ct);
        var porVehiculo = docs.GroupBy(d => d.VehicleId).ToDictionary(g => g.Key, g => g.Select(d => new DocumentoParaVencimiento(d.DocumentTypeId, d.Number, d.ExpiresOn)).ToList());

        var filas = new List<DocumentExpirationDto>();
        foreach (var v in vehiculos.OrderBy(v => v.InternalNumber))
        {
            var clasificados = VencimientosDeDocumentos.Clasificar(hoy, tipos, porVehiculo.GetValueOrDefault(v.Id) ?? []);
            foreach (var c in clasificados.Where(c => request.IncludeCurrent || c.Situacion != SituacionDeDocumento.Vigente))
            {
                filas.Add(new DocumentExpirationDto(v.PublicId, v.Plate, v.InternalNumber, v.Status, v.Dueño.PublicId, v.Dueño.TaxId, NombreVisible.De(v.Dueño),
                    c.Tipo.Codigo, c.Tipo.Nombre, VencimientosDeDocumentos.Texto(c.Situacion), c.Numero, c.Vence, c.DiasParaVencer));
            }
        }

        var conteo = (Vencidos: filas.Count(f => f.Situation == "Vencido"), PorVencer: filas.Count(f => f.Situation == "Por vencer"),
            Faltan: filas.Count(f => f.Situation == "Falta"));
        if (!string.IsNullOrWhiteSpace(request.Status))
            filas = filas.Where(f => string.Equals(Clave(f.Situation), request.Status.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        return Result.Success(new DocumentExpirationsDto(conteo.Vencidos, conteo.PorVencer, conteo.Faltan, filas));
    }

    /// <summary>La situación como la pide la ruta (<c>expired</c>, <c>expiring</c>, <c>missing</c>, <c>current</c>).</summary>
    public static string Clave(string situacion) => situacion switch
    {
        "Vencido" => "expired",
        "Por vencer" => "expiring",
        "Falta" => "missing",
        _ => "current",
    };
}

/// <summary>La parte de documentos de la ficha del vehículo: los vigentes con su situación y los reemplazados.</summary>
public sealed class DocumentosEnLaFicha(IApplicationDbContext db, IDateTimeService reloj) : IParteDeLaFichaDelVehiculo
{
    public async Task<VehicleDetailDto> CompletarAsync(VehicleDetailDto ficha, Vehicle vehiculo, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var docs = await db.VehicleDocuments.AsNoTracking().Where(d => d.VehicleId == vehiculo.Id).Include(d => d.DocumentType)
            .OrderBy(d => d.DocumentType!.Name).ThenByDescending(d => d.ExpiresOn).ToListAsync(ct);
        return ficha with
        {
            Documents = docs.Select(d =>
            {
                var dias = d.ExpiresOn.DayNumber - hoy.DayNumber;
                var situacion = d.SupersededAt is null
                    ? VencimientosDeDocumentos.Texto(VencimientosDeDocumentos.Situacion(dias, d.DocumentType!.WarningDays))
                    : "Reemplazado";
                return new VehicleDocumentDto(d.PublicId, d.DocumentType!.PublicId, d.DocumentType.Code, d.DocumentType.Name, d.Number, d.Issuer,
                    d.IssuedOn, d.ValidFrom, d.ExpiresOn, d.SupersededAt is null, situacion, dias);
            }).ToList(),
        };
    }
}
