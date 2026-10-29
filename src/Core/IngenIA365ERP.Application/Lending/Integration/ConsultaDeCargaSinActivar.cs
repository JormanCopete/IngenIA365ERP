using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// La implementación de <see cref="IConsultaDeCargaSinActivar"/> (feature 015, R1, T0350; E-01 b; contratos-v2.md §2.1; FR-164 b): lee la carga aplicada vigente
/// por el lector único <see cref="CargaVigenteDeCartera"/> (tandas vivas; nada después de activar) y devuelve los créditos de la persona como deudora —de vivos
/// y castigados— o codeudora. No escribe ni audita: no devuelve saldos, sólo número, línea y corte. Una persona que no existe responde <c>Answered</c> fuera de la
/// carga, igual que una que no está (Recaudo rechaza el ítem en los dos casos). (nuevo)
/// </summary>
public sealed class ConsultaDeCargaSinActivar(IApplicationDbContext db, CargaVigenteDeCartera carga) : IConsultaDeCargaSinActivar
{
    public const string CodigoEnLaCarga = "Lending.Migration.InAppliedLoad";
    public const string CodigoFueraDeLaCarga = "Lending.Migration.NotInAppliedLoad";

    public async Task<RespuestaDelContratoDto<CargaSinActivarDto>> PersonaAsync(Guid personPublicId, CancellationToken ct)
    {
        var persona = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.PublicId == personPublicId).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        var aplicada = persona is null ? null : await carga.AplicadaAsync(ct);
        if (persona is null || aplicada is null) return Fuera();

        var partes = db.MigrationLoadParts.AsNoTracking()
            .Where(p => p.MigrationLoadId == aplicada.Id && p.Status == MigrationPartStatus.Imported && !p.IsDeleted).Select(p => p.Id);
        var respaldados = await db.MigrationCoDebtorRows.AsNoTracking()
            .Where(r => partes.Contains(r.MigrationLoadPartId) && r.CoDebtorPersonId == persona.Value).Select(r => r.SourceLoanNumber).ToListAsync(ct);
        var creditos = await db.MigrationLoanRows.AsNoTracking()
            .Where(r => partes.Contains(r.MigrationLoadPartId) && (r.PersonId == persona.Value || respaldados.Contains(r.SourceLoanNumber)))
            .Select(r => new { r.PublicId, r.SourceLoanNumber, r.CreditLineCode }).ToListAsync(ct);
        var castigados = await db.MigrationChargedOffRows.AsNoTracking()
            .Where(r => partes.Contains(r.MigrationLoadPartId) && r.PersonId == persona.Value)
            .Select(r => new { r.PublicId, r.SourceLoanNumber, r.CreditLineCode }).ToListAsync(ct);
        var todos = creditos.Concat(castigados).DistinctBy(c => c.SourceLoanNumber).ToList();
        if (todos.Count == 0) return Fuera();

        var codigos = todos.Select(c => c.CreditLineCode).Distinct().ToList();
        var lineas = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => codigos.Contains(l.Code))
            .GroupBy(l => l.Code).Select(g => new { g.Key, Name = g.Select(l => l.Name).First() }).ToDictionaryAsync(x => x.Key, x => x.Name, ct);
        var obligaciones = todos.OrderBy(c => c.SourceLoanNumber, StringComparer.Ordinal)
            .Select(c => new ObligacionCargadaDto(c.PublicId, c.SourceLoanNumber, lineas.GetValueOrDefault(c.CreditLineCode) ?? c.CreditLineCode, aplicada.CutoffDate))
            .ToList();
        return new(ResultadoDelContrato.Answered, CodigoEnLaCarga, "La persona está en la carga de Cartera aplicada sin activar.",
            new CargaSinActivarDto(true, obligaciones));
    }

    private static RespuestaDelContratoDto<CargaSinActivarDto> Fuera() =>
        new(ResultadoDelContrato.Answered, CodigoFueraDeLaCarga, "La persona no está en una carga de Cartera aplicada sin activar.", new CargaSinActivarDto(false, []));
}
