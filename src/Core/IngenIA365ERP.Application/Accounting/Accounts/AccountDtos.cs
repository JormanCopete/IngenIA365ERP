using IngenIA365ERP.Domain.Enums.Accounting;

namespace IngenIA365ERP.Application.Accounting.Accounts;

// Contratos del plan de cuentas (feature 009, contracts/api.md §3). Espejo en
// Shared/Services/Contabilidad/ContabilidadDtos.cs.

public sealed record CuentaNodoDto(Guid PublicId, string Code, string Name, byte Level, string Nature, bool IsMovement, bool IsActive, bool HasChildren, string Origin, Guid? ParentPublicId);

public sealed record CuentaBuscadaDto(Guid PublicId, string Code, string Name, string Nature, bool RequiresThirdParty, bool RequiresCrossDocument, bool RequiresCostCenter, bool RequiresBranch, bool RequiresTaxBase);

public sealed record CuentaBancariaDto(Guid BankPublicId, string? BankName, string? AccountNumber);

public sealed record TarifaDto(DateOnly ValidFrom, decimal Rate);

public sealed record CuentaDeImpuestoDto(string Kind, string? ConceptCode, bool RequiresTaxBase, IReadOnlyList<TarifaDto> Rates);

/// <summary>Dónde está parametrizada una cuenta en otro módulo: qué módulo, en qué tabla y qué fila.</summary>
public sealed record ReferenciaDeCuenta(string Module, string Where, string Detail);

public sealed record CuentaDto(
    Guid PublicId,
    string Code,
    string Name,
    byte Level,
    string Nature,
    Guid? ParentPublicId,
    string? ParentCode,
    string NiifItemCode,
    string Origin,
    bool IsMovement,
    bool IsActive,
    DateOnly? FirstMovementAt,
    bool Locked,
    IReadOnlyList<string> EnabledModules,
    bool RequiresThirdParty,
    bool RequiresCrossDocument,
    bool RequiresCostCenter,
    bool RequiresBranch,
    CuentaBancariaDto? Bank,
    CuentaDeImpuestoDto? Tax,
    IReadOnlyList<ReferenciaDeCuenta> References);

public sealed record EventoDeCuentaDto(DateTime OccurredAt, string Action, string? User, string? Detail);

public sealed record ParametrizacionInvalidaDto(string Module, string Where, string Detail, string? AccountCode, string Problem);

// ------------------------------------------------------------------- entradas de comandos --

public sealed record CuentaBancariaInput(Guid BankPublicId, string? AccountNumber);

public sealed record TarifaInput(DateOnly ValidFrom, decimal Rate);

public sealed record CuentaDeImpuestoInput(string Kind, string? ConceptCode, bool RequiresTaxBase, IReadOnlyList<TarifaInput> Rates);

/// <summary>Quién sabe dónde está parametrizada una cuenta (las tablas de data-model.md §1). Lo implementa Persistence sobre el contexto real.</summary>
public interface IAccountReferenceFinder
{
    Task<IReadOnlyList<ReferenciaDeCuenta>> BuscarAsync(int accountId, string code, CancellationToken ct);
}

public static class TaxKindTexto
{
    public static TaxKind? Parse(string? kind) => Enum.TryParse<TaxKind>(kind, true, out var k) ? k : null;
}
