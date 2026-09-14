using FluentValidation;

namespace IngenIA365ERP.Application.Security.Permissions;

/// <summary>Sin parámetros que validar; existe porque el Principio VIII exige un validador hermano por request.</summary>
public sealed class GetMyPermissionsQueryValidator : AbstractValidator<GetMyPermissionsQuery>;
