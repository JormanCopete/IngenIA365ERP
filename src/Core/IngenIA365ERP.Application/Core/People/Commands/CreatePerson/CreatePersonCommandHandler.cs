using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.People.Commands.CreatePerson;

public class CreatePersonCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreatePersonCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreatePersonCommand request,
        CancellationToken cancellationToken)
    {
        var existing = await context.People.AsNoTracking()
            .AnyAsync(p => p.TaxId == request.TaxId && !p.IsDeleted, cancellationToken);
        if (existing)
            return Result.Failure<Guid>(new Error("Person.TaxIdDuplicate",
                "Ya existe una persona con ese numero de identificacion."));

        int? cityId = null;
        if (request.CityPublicId.HasValue)
        {
            var city = await context.Cities.AsNoTracking()
                .FirstOrDefaultAsync(c => c.PublicId == request.CityPublicId.Value && !c.IsDeleted, cancellationToken);
            if (city is null)
                return Result.Failure<Guid>(new Error("Person.CityNotFound", "Ciudad no encontrada."));
            cityId = city.Id;
        }

        var person = new Person
        {
            IdType = request.IdType,
            TaxId = request.TaxId,
            TaxIdCheckDigit = request.TaxIdCheckDigit,
            IdIssuedAt = request.IdIssuedAt,
            IdIssueDate = request.IdIssueDate,
            FirstName = request.FirstName,
            LastName = request.LastName,
            BusinessName = request.BusinessName,
            PersonType = request.PersonType,
            Address = request.Address,
            Phone1 = request.Phone1,
            Phone2 = request.Phone2,
            Mobile = request.Mobile,
            Email = request.Email,
            CityId = cityId,
            Gender = request.Gender,
            MaritalStatus = request.MaritalStatus,
            DateOfBirth = request.DateOfBirth,
            EducationLevel = request.EducationLevel,
            IsAssociate = request.IsAssociate,
            IsEmployee = request.IsEmployee,
            IsThirdParty = request.IsThirdParty,
            IsAdvisor = request.IsAdvisor,
            IsCustomer = request.IsCustomer,
            IsSupplier = request.IsSupplier,
            IsSalesperson = request.IsSalesperson,
            ReceivesInvoice = request.ReceivesInvoice,
            Status = string.IsNullOrEmpty(request.Status) ? "A" : request.Status,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.People.Add(person);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(person.PublicId);
    }
}
