using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.People.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.People.Queries;

/// <summary>
/// La persona dueña de un documento, <b>eliminadas incluidas</b> (feature 008). El buscador y
/// el diálogo la consultan después de un «ya existe» o «está eliminada» para ofrecer «Usar esa
/// persona» o «Restaurar persona»: el envelope de error no lleva el <c>PublicId</c>, y la
/// búsqueda normal nunca devuelve eliminadas. Va por query string y no por ruta para que el
/// documento no quede en el log de peticiones.
/// </summary>
public sealed record GetPersonByDocumentQuery(string TaxId) : IRequest<Result<PersonByDocumentDto>>;

public sealed record PersonByDocumentDto(
    Guid PublicId,
    string FullName,
    string TaxId,
    string IdType,
    bool IsDeleted,
    DateTime? DeletedAt,
    bool IsEmployee,
    bool IsAssociate,
    bool IsSalesperson,
    string? Status);

public sealed class GetPersonByDocumentQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPersonByDocumentQuery, Result<PersonByDocumentDto>>
{
    public async Task<Result<PersonByDocumentDto>> Handle(GetPersonByDocumentQuery request, CancellationToken ct)
    {
        var taxId = request.TaxId.Trim();
        var persona = await context.People
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => p.TaxId == taxId)
            .OrderBy(p => p.IsDeleted) // si hubiera una viva y una eliminada, manda la viva
            .Select(p => new PersonByDocumentDto(
                p.PublicId,
                PersonFactory.NombreVisible(p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName),
                p.TaxId,
                p.IdType,
                p.IsDeleted,
                p.DeletedAt,
                p.IsEmployee,
                p.IsAssociate,
                p.IsSalesperson,
                p.Status))
            .FirstOrDefaultAsync(ct);

        return persona is null
            ? Result.Failure<PersonByDocumentDto>(new Error("Person.NotFound", "Nadie tiene ese documento."))
            : Result.Success(persona);
    }
}

public sealed class GetPersonByDocumentQueryValidator : AbstractValidator<GetPersonByDocumentQuery>
{
    public GetPersonByDocumentQueryValidator() => RuleFor(x => x.TaxId).NotEmpty().MaximumLength(20);
}
