using FluentValidation;
using IngenIA365ERP.Application.Channel.Common;
using IngenIA365ERP.Application.Channel.Service;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Common.Identidad;
using IngenIA365ERP.Domain.Entities.Channel;
using IngenIA365ERP.Domain.Enums.Channel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Channel.ContactUpdates;

// Feature 021 (T060; FR-024, D7): cuando el asociado cambia y verifica su correo o su celular en el canal, cada cooperativa donde está
// habilitado recibe una novedad. La cooperativa la ve y decide si actualiza la persona por el flujo normal de Personas: la novedad nunca
// toca COR_People. El valor nuevo sale enmascarado al listar; verlo completo es una lectura sensible auditada.

public sealed record ContactUpdateDto(
    Guid PublicId,
    Guid PersonPublicId,
    string FullName,
    string? DocumentType,
    string Document,
    ContactField Field,
    string NewValueMasked,
    DateTime ReportedAt,
    DateTime? AcknowledgedAt,
    string? AcknowledgedBy);

public sealed record ContactUpdateDetailDto(Guid PublicId, Guid PersonPublicId, string FullName, ContactField Field, string NewValue, string? CurrentValue,
    DateTime ReportedAt, DateTime? AcknowledgedAt, string? AcknowledgedBy);

public sealed record ContactUpdatesPage(IReadOnlyList<ContactUpdateDto> Items, int Total, int Page, int PageSize);

public static class NovedadesDeContacto
{
    public const string NoExiste = "Channel.ContactUpdate.NotFound";
    public const string ValorInvalido = "Channel.ContactUpdate.ValueInvalid";

    public static string Enmascarar(ContactField campo, string valor) => campo == ContactField.Email
        ? ContactoDeActivacion.EnmascararCorreo(valor) ?? "•••"
        : ContactoDeActivacion.EnmascararCelular(valor) ?? "•••";

    /// <summary>El valor normalizado si es válido para el campo; nulo si no.</summary>
    public static string? Normalizar(ContactField campo, string? valor) => campo == ContactField.Email
        ? ContactoDeActivacion.CorreoValido(valor)
        : ContactoDeActivacion.CelularValido(valor);

    public static string NombreCompleto(string? nombre, string? otros, string? apellido, string? segundo) =>
        string.Join(' ', new[] { nombre, otros, apellido, segundo }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));
}

// ------------------------------------------------------------------------------------------------ registrar (servicio) --

/// <summary>
/// Lo llama el canal (<c>POST /api/canal/v1/tenants/{t}/people/{p}/contact-updates</c>). Idempotente por persona, campo y fecha del
/// reporte: el canal reintenta con el mismo <see cref="ReportedAt"/> y recibe la misma novedad.
/// </summary>
public sealed record RegisterContactUpdateCommand(Guid PersonPublicId, ContactField Field, string NewValue, DateTime ReportedAt)
    : IRequest<Result<RegisteredContactUpdateDto>>;

public sealed record RegisteredContactUpdateDto(Guid PublicId, bool Created);

public sealed class RegisterContactUpdateCommandValidator : AbstractValidator<RegisterContactUpdateCommand>
{
    public RegisterContactUpdateCommandValidator()
    {
        RuleFor(x => x.Field).IsInEnum().WithMessage("El campo es Email o Mobile.");
        RuleFor(x => x.NewValue).NotEmpty().MaximumLength(150);
        RuleFor(x => x.ReportedAt).NotEqual(default(DateTime)).WithMessage("Falta la fecha del reporte.");
    }
}

public sealed class RegisterContactUpdateCommandHandler(IApplicationDbContext db)
    : IRequestHandler<RegisterContactUpdateCommand, Result<RegisteredContactUpdateDto>>
{
    public async Task<Result<RegisteredContactUpdateDto>> Handle(RegisterContactUpdateCommand request, CancellationToken ct)
    {
        var persona = await PersonaDelCanal.HabilitadaAsync(db, request.PersonPublicId, ct);
        if (persona is null) return Result.Failure<RegisteredContactUpdateDto>(PersonaDelCanal.ErrorNoHabilitada);

        var valor = NovedadesDeContacto.Normalizar(request.Field, request.NewValue);
        if (valor is null)
        {
            return Result.Failure<RegisteredContactUpdateDto>(NovedadesDeContacto.ValorInvalido,
                request.Field == ContactField.Email ? "El correo no tiene forma de correo." : "El celular debe tener diez dígitos y empezar por 3.");
        }

        // La fecha del reporte se guarda en UTC al microsegundo: así el reintento coincide en los dos motores.
        var reportado = DateTime.SpecifyKind(request.ReportedAt.ToUniversalTime(), DateTimeKind.Utc);
        reportado = new DateTime(reportado.Ticks - reportado.Ticks % 10, DateTimeKind.Utc);
        var existente = await db.ChannelContactUpdates.AsNoTracking()
            .Where(n => n.PersonId == persona.Id && n.Field == request.Field && n.ReportedAt == reportado)
            .Select(n => (Guid?)n.PublicId).FirstOrDefaultAsync(ct);
        if (existente is { } ya) return Result.Success(new RegisteredContactUpdateDto(ya, false));

        var novedad = new ChannelContactUpdate { PersonId = persona.Id, Field = request.Field, NewValue = valor, ReportedAt = reportado };
        db.ChannelContactUpdates.Add(novedad);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Dos reintentos a la vez: el índice único deja entrar uno; el otro devuelve el que quedó.
            db.ChannelContactUpdates.Entry(novedad).State = EntityState.Detached;
            var ganador = await db.ChannelContactUpdates.AsNoTracking()
                .Where(n => n.PersonId == persona.Id && n.Field == request.Field && n.ReportedAt == reportado)
                .Select(n => (Guid?)n.PublicId).FirstOrDefaultAsync(ct);
            if (ganador is { } g) return Result.Success(new RegisteredContactUpdateDto(g, false));
            throw;
        }
        return Result.Success(new RegisteredContactUpdateDto(novedad.PublicId, true));
    }
}

// ----------------------------------------------------------------------------------------------- listar (administración) --

public sealed record ListContactUpdatesQuery(bool? Pending = true, int Page = 1, int PageSize = 50) : IRequest<Result<ContactUpdatesPage>>;

public sealed class ListContactUpdatesQueryHandler(IApplicationDbContext db) : IRequestHandler<ListContactUpdatesQuery, Result<ContactUpdatesPage>>
{
    public async Task<Result<ContactUpdatesPage>> Handle(ListContactUpdatesQuery r, CancellationToken ct)
    {
        var tamano = Math.Clamp(r.PageSize, 1, 200);
        var pagina = Math.Max(1, r.Page);
        var consulta = from n in db.ChannelContactUpdates.AsNoTracking()
                       join p in db.People.AsNoTracking().IgnoreQueryFilters() on n.PersonId equals p.Id
                       select new { n, p };
        if (r.Pending is true) consulta = consulta.Where(x => x.n.AcknowledgedAt == null);
        else if (r.Pending is false) consulta = consulta.Where(x => x.n.AcknowledgedAt != null);

        var total = await consulta.CountAsync(ct);
        var filas = await consulta.OrderByDescending(x => x.n.ReportedAt).ThenByDescending(x => x.n.Id)
            .Skip((pagina - 1) * tamano).Take(tamano)
            .Select(x => new
            {
                x.n.PublicId, PersonPublicId = x.p.PublicId, x.p.FirstName, x.p.OtherNames, x.p.LastName, x.p.SecondLastName, x.p.IdType, x.p.TaxId,
                x.n.Field, x.n.NewValue, x.n.ReportedAt, x.n.AcknowledgedAt, x.n.AcknowledgedBy,
            })
            .ToListAsync(ct);
        var items = filas.Select(f => new ContactUpdateDto(f.PublicId, f.PersonPublicId,
            NovedadesDeContacto.NombreCompleto(f.FirstName, f.OtherNames, f.LastName, f.SecondLastName),
            TiposDeDocumento.PorCodigoDePersona(f.IdType)?.Codigo, f.TaxId ?? string.Empty, f.Field,
            NovedadesDeContacto.Enmascarar(f.Field, f.NewValue), f.ReportedAt, f.AcknowledgedAt, f.AcknowledgedBy)).ToList();
        return Result.Success(new ContactUpdatesPage(items, total, pagina, tamano));
    }
}

/// <summary>El valor completo de una novedad, con el que la persona tiene hoy en la cooperativa. Lectura sensible auditada.</summary>
public sealed record GetContactUpdateQuery(Guid PublicId) : IRequest<Result<ContactUpdateDetailDto>>;

public sealed class GetContactUpdateQueryHandler(IApplicationDbContext db, AuditoriaDelCanal auditoria)
    : IRequestHandler<GetContactUpdateQuery, Result<ContactUpdateDetailDto>>
{
    public async Task<Result<ContactUpdateDetailDto>> Handle(GetContactUpdateQuery r, CancellationToken ct)
    {
        var f = await (from n in db.ChannelContactUpdates.AsNoTracking()
                       join p in db.People.AsNoTracking().IgnoreQueryFilters() on n.PersonId equals p.Id
                       where n.PublicId == r.PublicId
                       select new
                       {
                           n.PublicId, PersonPublicId = p.PublicId, p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, n.Field, n.NewValue,
                           p.Email, p.Mobile, n.ReportedAt, n.AcknowledgedAt, n.AcknowledgedBy,
                       }).FirstOrDefaultAsync(ct);
        if (f is null) return Result.Failure<ContactUpdateDetailDto>(NovedadesDeContacto.NoExiste, "La novedad de contacto no existe.");

        await auditoria.LecturaAsync(AuditoriaDelCanal.EventoNovedadDeContactoLeida, "ChannelContactUpdate", f.PublicId,
            new { personPublicId = f.PersonPublicId, field = f.Field.ToString() }, ct);
        return Result.Success(new ContactUpdateDetailDto(f.PublicId, f.PersonPublicId,
            NovedadesDeContacto.NombreCompleto(f.FirstName, f.OtherNames, f.LastName, f.SecondLastName), f.Field, f.NewValue,
            f.Field == ContactField.Email ? f.Email : f.Mobile, f.ReportedAt, f.AcknowledgedAt, f.AcknowledgedBy));
    }
}

// ------------------------------------------------------------------------------------------------- marcar como atendida --

/// <summary>La cooperativa deja constancia de que vio la novedad (y, si quiso, actualizó la persona en Personas). Idempotente.</summary>
public sealed record AcknowledgeContactUpdateCommand(Guid PublicId) : IRequest<Result<ContactUpdateDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class AcknowledgeContactUpdateCommandValidator : AbstractValidator<AcknowledgeContactUpdateCommand>
{
    public AcknowledgeContactUpdateCommandValidator() => RuleFor(x => x.PublicId).NotEmpty();
}

public sealed class AcknowledgeContactUpdateCommandHandler(IApplicationDbContext db, IDateTimeService reloj, ICurrentUserService usuario)
    : IRequestHandler<AcknowledgeContactUpdateCommand, Result<ContactUpdateDto>>
{
    public async Task<Result<ContactUpdateDto>> Handle(AcknowledgeContactUpdateCommand request, CancellationToken ct)
    {
        var n = await db.ChannelContactUpdates.FirstOrDefaultAsync(x => x.PublicId == request.PublicId, ct);
        if (n is null) return Result.Failure<ContactUpdateDto>(NovedadesDeContacto.NoExiste, "La novedad de contacto no existe.");
        if (n.AcknowledgedAt is null)
        {
            n.AcknowledgedAt = reloj.UtcNow;
            var quien = usuario.UserName ?? usuario.UserId?.ToString() ?? "desconocido";
            n.AcknowledgedBy = quien.Length > 100 ? quien[..100] : quien;
            await db.SaveChangesAsync(ct);
        }
        var p = await db.People.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == n.PersonId)
            .Select(x => new { x.PublicId, x.FirstName, x.OtherNames, x.LastName, x.SecondLastName, x.IdType, x.TaxId }).FirstAsync(ct);
        return Result.Success(new ContactUpdateDto(n.PublicId, p.PublicId, NovedadesDeContacto.NombreCompleto(p.FirstName, p.OtherNames, p.LastName, p.SecondLastName),
            TiposDeDocumento.PorCodigoDePersona(p.IdType)?.Codigo, p.TaxId ?? string.Empty, n.Field, NovedadesDeContacto.Enmascarar(n.Field, n.NewValue),
            n.ReportedAt, n.AcknowledgedAt, n.AcknowledgedBy));
    }
}
