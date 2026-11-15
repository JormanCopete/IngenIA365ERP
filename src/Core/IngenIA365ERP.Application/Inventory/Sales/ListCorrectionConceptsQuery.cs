using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing.Catalogs;
using MediatR;

namespace IngenIA365ERP.Application.Inventory.Sales;

/// <summary>Un concepto de corrección de la DIAN: código, nombre y si anula el documento. (nuevo)</summary>
public sealed record ConceptoDeCorreccionDto(string Code, string Name, bool IsVoiding);

/// <summary>
/// 2026-11-12: los conceptos de corrección de una nota (<c>GET /api/inventory/sales/correction-concepts?note=Debit|Credit&amp;asOf=</c>,
/// <c>Inventory.Sales.View</c>) del catálogo de la DIAN embebido y vigente a la fecha (<see cref="CatalogoDian.ConceptosDeCorreccion"/>): son
/// dato, no una lista escrita en la pantalla. La nota débito los elige de una lista en vez de digitar el código. Sin fecha, hoy. (nuevo)
/// </summary>
public sealed record ListCorrectionConceptsQuery(string? Note = null, DateOnly? AsOf = null) : IRequest<Result<IReadOnlyList<ConceptoDeCorreccionDto>>>
{
    public const string NotaDebito = "Debit";
    public const string NotaCredito = "Credit";
}

public sealed class ListCorrectionConceptsQueryValidator : AbstractValidator<ListCorrectionConceptsQuery>
{
    public ListCorrectionConceptsQueryValidator() =>
        RuleFor(x => x.Note).Must(n => string.IsNullOrWhiteSpace(n)
                || string.Equals(n, ListCorrectionConceptsQuery.NotaDebito, StringComparison.OrdinalIgnoreCase)
                || string.Equals(n, ListCorrectionConceptsQuery.NotaCredito, StringComparison.OrdinalIgnoreCase))
            .WithMessage("La nota es «Debit» o «Credit».");
}

public sealed class ListCorrectionConceptsQueryHandler(IDateTimeService reloj)
    : IRequestHandler<ListCorrectionConceptsQuery, Result<IReadOnlyList<ConceptoDeCorreccionDto>>>
{
    public Task<Result<IReadOnlyList<ConceptoDeCorreccionDto>>> Handle(ListCorrectionConceptsQuery request, CancellationToken ct)
    {
        var clase = string.Equals(request.Note, ListCorrectionConceptsQuery.NotaCredito, StringComparison.OrdinalIgnoreCase)
            ? ClaseDeNotaDian.NotaCredito
            : ClaseDeNotaDian.NotaDebito;
        IReadOnlyList<ConceptoDeCorreccionDto> conceptos = CatalogoDian.Embebido.ConceptosDeCorreccion(clase, request.AsOf ?? reloj.HoyLocal)
            .Select(c => new ConceptoDeCorreccionDto(c.Codigo, c.Nombre, c.EsAnulacion)).ToList();
        return Task.FromResult(Result.Success(conceptos));
    }
}
