using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.NationalValues;

/// <summary>
/// El perfil regulatorio de la cooperativa que rige a una fecha (feature 013, FR-018, FR-108; T50; contracts/interfaces.md §11.1). Lo
/// usan el selector del modelo de deterioro, la originación a no asociados y por libranza, las mayorías para vinculados, la
/// obligación sobrevenida (D28) y la activación (sin perfil vigente, la activación lo lista como faltante). (nuevo)
/// </summary>
public interface IPerfilRegulatorio
{
    /// <summary>
    /// La versión que rige el <paramref name="fecha"/> con lo registrado hasta <paramref name="informacionHasta"/> (nulo = todo),
    /// con la regla bitemporal (T38): R es <c>CreatedAt</c>, el <c>Id</c> desempata y una corrección reemplaza a la que corrige.
    /// Nula si no hay perfil vigente.
    /// </summary>
    Task<PerfilRegulatorioDto?> VigenteAsync(DateOnly fecha, DateTime? informacionHasta, CancellationToken ct);
}

/// <summary>
/// Una versión del perfil regulatorio (contracts/interfaces.md §11.1, alineado a las columnas de data-model §4.5: la tabla manda en
/// los nombres y tipos —nivel y grupo NIIF numéricos, la categoría de fondo de empleados, el registro como operador de libranza—,
/// tasks.md, contradicción 9 de la fase 2).
/// </summary>
public sealed record PerfilRegulatorioDto(
    Guid VersionPublicId,
    DateOnly ValidFrom,
    string OrganizationTypeCode,
    bool FinancialActivityAuthorized,
    byte SupervisionLevel,
    string? EmployeeFundCategory,
    byte? NiifGroup,
    bool LibranzaOperatorRegistered,
    string? LibranzaOperatorRegistrationNumber,
    DateOnly? LibranzaOperatorRegistrationDate,
    string Source,
    Guid? SupportAttachmentPublicId,
    string Reason,
    Guid? SupersedesPublicId,
    string RegisteredByName,
    DateTime RegisteredAt,
    bool MayTakeDeposits = false,
    bool DepositsFromAssociatesOnly = false,
    string? LiquidityFundCategory = null,
    bool DepositInsuranceEnrolled = false,
    bool IsGmfWithholdingAgent = false)
{
    public static PerfilRegulatorioDto Desde(RegulatoryProfileVersion v) => new(
        v.PublicId, v.ValidFrom, v.OrganizationTypeCode, v.FinancialActivityAuthorized, v.SupervisionLevel, v.EmployeeFundCategory,
        v.NiifGroup, v.LibranzaOperatorRegistered, v.LibranzaOperatorRegistrationNumber, v.LibranzaOperatorRegistrationDate, v.Source,
        v.SupportAttachmentPublicId, v.Reason, v.SupersedesPublicId, v.RegisteredByName, v.CreatedAt, v.MayTakeDeposits, v.DepositsFromAssociatesOnly,
        v.LiquidityFundCategory, v.DepositInsuranceEnrolled, v.IsGmfWithholdingAgent);
}

/// <summary>
/// Implementación de <see cref="IPerfilRegulatorio"/> (feature 013, T50): lee <c>COR_RegulatoryProfileVersions</c> y aplica
/// <see cref="VigenciaBitemporal"/> (la entidad declara <c>CreatedAt</c> como R y su <c>Id</c> como desempate). (nuevo)
/// </summary>
public sealed class LectorDePerfilRegulatorio(IApplicationDbContext db) : IPerfilRegulatorio
{
    public async Task<PerfilRegulatorioDto?> VigenteAsync(DateOnly fecha, DateTime? informacionHasta, CancellationToken ct)
    {
        // La tabla es chica (unas pocas versiones por cooperativa): se trae entera, porque una corrección con ValidFrom posterior
        // a la fecha también reemplaza a la que corrige.
        var versiones = await db.RegulatoryProfileVersions.AsNoTracking().Where(v => !v.IsDeleted).ToListAsync(ct);
        var vigente = VigenciaBitemporal.Vigente(versiones, fecha, informacionHasta ?? DateTime.MaxValue);
        return vigente is null ? null : PerfilRegulatorioDto.Desde(vigente);
    }
}
