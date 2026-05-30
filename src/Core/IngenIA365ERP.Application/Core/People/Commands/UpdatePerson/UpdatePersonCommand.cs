using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.People.Commands.UpdatePerson;

/// <summary>
/// Actualiza datos de la persona en la tabla maestra COR_People.
/// La especializacion del rol (asociado, empleado interno, conyuge, vendedor)
/// vive en sus propias tablas hijas y se administra desde sus propios modulos.
/// Aqui SOLO se manejan datos personales, contacto, demografia y flags de rol.
/// </summary>
public record UpdatePersonCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }

    // Identificacion
    public string IdType { get; init; } = "C";
    public string TaxId { get; init; } = string.Empty;
    public string? TaxIdCheckDigit { get; init; }
    public string? IdIssuedAt { get; init; }
    public DateOnly? IdIssueDate { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? BusinessName { get; init; }
    public string? PersonType { get; init; }

    // Contacto
    public string? Address { get; init; }
    public string? Phone1 { get; init; }
    public string? Phone2 { get; init; }
    public string? Mobile { get; init; }
    public string? Email { get; init; }
    public Guid? CityPublicId { get; init; }

    // Demografia
    public string? Gender { get; init; }
    public string? MaritalStatus { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public string? EducationLevel { get; init; }

    // Roles
    public bool IsAssociate { get; init; }
    public bool IsEmployee { get; init; }
    public bool IsThirdParty { get; init; }
    public bool IsAdvisor { get; init; }
    public bool IsCustomer { get; init; }
    public bool IsSupplier { get; init; }
    public bool IsSalesperson { get; init; }
    public bool ReceivesInvoice { get; init; }

    public string? Status { get; init; }
}

public class UpdatePersonCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdatePersonCommand, Result>
{
    public async Task<Result> Handle(UpdatePersonCommand request, CancellationToken ct)
    {
        var person = await context.People.FirstOrDefaultAsync(
            p => p.PublicId == request.PublicId && !p.IsDeleted, ct);
        if (person is null)
            return Result.Failure(new Error("Person.NotFound", "Persona no encontrada."));

        if (person.TaxId != request.TaxId)
        {
            var taxIdExists = await context.People.AsNoTracking()
                .AnyAsync(p => p.TaxId == request.TaxId && p.Id != person.Id && !p.IsDeleted, ct);
            if (taxIdExists)
                return Result.Failure(new Error("Person.TaxIdDuplicate",
                    "Ya existe otra persona con ese numero de identificacion."));
        }

        int? cityId = null;
        if (request.CityPublicId.HasValue)
        {
            var city = await context.Cities.AsNoTracking()
                .FirstOrDefaultAsync(c => c.PublicId == request.CityPublicId.Value && !c.IsDeleted, ct);
            if (city is null)
                return Result.Failure(new Error("Person.CityNotFound", "Ciudad no encontrada."));
            cityId = city.Id;
        }

        person.IdType = request.IdType;
        person.TaxId = request.TaxId;
        person.TaxIdCheckDigit = request.TaxIdCheckDigit;
        person.IdIssuedAt = request.IdIssuedAt;
        person.IdIssueDate = request.IdIssueDate;
        person.FirstName = request.FirstName;
        person.LastName = request.LastName;
        person.BusinessName = request.BusinessName;
        person.PersonType = request.PersonType;
        person.Address = request.Address;
        person.Phone1 = request.Phone1;
        person.Phone2 = request.Phone2;
        person.Mobile = request.Mobile;
        person.Email = request.Email;
        person.CityId = cityId;
        person.Gender = request.Gender;
        person.MaritalStatus = request.MaritalStatus;
        person.DateOfBirth = request.DateOfBirth;
        person.EducationLevel = request.EducationLevel;
        person.IsAssociate = request.IsAssociate;
        person.IsEmployee = request.IsEmployee;
        person.IsThirdParty = request.IsThirdParty;
        person.IsAdvisor = request.IsAdvisor;
        person.IsCustomer = request.IsCustomer;
        person.IsSupplier = request.IsSupplier;
        person.IsSalesperson = request.IsSalesperson;
        person.ReceivesInvoice = request.ReceivesInvoice;
        if (!string.IsNullOrWhiteSpace(request.Status))
            person.Status = request.Status;
        person.UpdatedAt = dateTime.UtcNow;
        person.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public class UpdatePersonCommandValidator : AbstractValidator<UpdatePersonCommand>
{
    public UpdatePersonCommandValidator()
    {
        RuleFor(x => x.PublicId).NotEmpty();
        RuleFor(x => x.TaxId).NotEmpty().MaximumLength(20);
        RuleFor(x => x.IdType).NotEmpty().MaximumLength(2);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.BusinessName).MaximumLength(150);
        RuleFor(x => x.Address).MaximumLength(120);
        RuleFor(x => x.Phone1).MaximumLength(40);
        RuleFor(x => x.Phone2).MaximumLength(40);
        RuleFor(x => x.Mobile).MaximumLength(30);
        RuleFor(x => x.Email).MaximumLength(120)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("Correo electronico no valido.");
        RuleFor(x => x.DateOfBirth)
            .LessThan(DateOnly.FromDateTime(DateTime.UtcNow))
            .When(x => x.DateOfBirth.HasValue)
            .WithMessage("La fecha de nacimiento debe ser anterior a hoy.");
    }
}
