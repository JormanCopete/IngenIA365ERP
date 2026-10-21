using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// La implementación de <see cref="IDimensionesDeCartera"/> (feature 013, US7, T0657; contracts/contabilidad.md §16.2; enmienda A1, C27): lo único
/// que Contabilidad sabe de las dimensiones de Cartera, para validar una regla de la matriz, armar su plantilla y medir su completitud.
/// <list type="bullet">
/// <item><see cref="CatalogoAsync"/>: las líneas (también las cerradas, con la modalidad y el sub-segmento de su última versión), las modalidades
/// con sus sub-segmentos del <c>CatalogoDeModalidades</c> vigente (<see cref="INormativaVigente"/>; sin documento vigente, las que usan las
/// líneas), los conceptos (<see cref="LoanConcept"/>), las clases de recaudador (<see cref="ClaseDeRecaudador"/>) y los tipos de hecho que van a
/// Contabilidad (<see cref="CatalogoDeMensajesDeCarteraV1.HechosAContabilidad"/>).</item>
/// <item><see cref="CombinacionesEnUsoAsync"/>: las combinaciones de saldo con saldo a la fecha o movimiento ese día, por
/// <see cref="SaldosContablesDeCartera"/> (el mismo cálculo de la conciliación). Los puentes no van: su regla exige la clase o el motivo, que el
/// DTO no lleva, y la completitud los mide aparte (recaudadores registrados).</item>
/// </list>
/// Sin el alcance de oficinas de Cartera: los puertos para otros módulos no lo aplican (T62). (nuevo)
/// </summary>
public sealed class DimensionesDeCartera(IApplicationDbContext db, INormativaVigente normativa, SaldosContablesDeCartera saldos, IDateTimeService reloj)
    : IDimensionesDeCartera
{
    public async Task<CatalogoDeDimensionesDeCarteraDto> CatalogoAsync(CancellationToken ct)
    {
        var lineas = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => !l.IsDeleted)
            .Select(l => new
            {
                l.Code,
                l.Name,
                l.Status,
                l.Nature,
                Version = db.CreditLineVersions.Where(v => v.CreditLineId == l.Id).OrderByDescending(v => v.VersionNumber)
                    .Select(v => new { v.ModalityCode, v.SubsegmentCode }).FirstOrDefault(),
                // US8 (T144): la clase de captación vive en la versión (research E37) y no cambia.
                Clase = db.DepositLineVersions.Where(v => v.CreditLineId == l.Id).OrderBy(v => v.VersionNumber).Select(v => (DepositClass?)v.DepositClass)
                    .FirstOrDefault(),
            })
            .OrderBy(l => l.Code)
            .ToListAsync(ct);
        var deLineas = lineas.Select(l => new LineaDeCarteraDto(l.Code, l.Name, l.Version?.ModalityCode ?? string.Empty, l.Version?.SubsegmentCode,
            l.Status == CreditLineStatus.Active, l.Nature.ToString(), l.Clase?.ToString())).ToList();

        var catalogo = await normativa.CatalogoDeModalidadesAsync(reloj.HoyLocal, null, ct);
        var modalidades = catalogo.IsSuccess
            ? catalogo.Value.Documento.Modalidades
                .Select(m => new ModalidadDeCarteraDto(m.Code, m.Name, m.Combinaciones.Select(c => c.SubsegmentCode).OfType<string>().Distinct().ToList()))
                .ToList()
            : deLineas.Where(l => l.ModalityCode.Length > 0).GroupBy(l => l.ModalityCode, StringComparer.Ordinal)
                .Select(g => new ModalidadDeCarteraDto(g.Key, g.Key, g.Select(l => l.SubsegmentCode).OfType<string>().Distinct().ToList()))
                .ToList();

        return new CatalogoDeDimensionesDeCarteraDto(
            deLineas,
            modalidades,
            Enum.GetNames<LoanConcept>(),
            Enum.GetNames<ClaseDeRecaudador>(),
            CatalogoDeMensajesDeCarteraV1.HechosAContabilidad.Select(t => t.Type).ToList());
    }

    public async Task<IReadOnlyList<CombinacionDeCarteraEnUsoDto>> CombinacionesEnUsoAsync(DateOnly fecha, CancellationToken ct)
    {
        var movimientos = await saldos.MovimientosAsync(fecha, alcance: null, ct);
        var enUso = SaldosContablesDeCartera.Agrupar(movimientos)
            .Where(s => s.Saldo != 0m || s.UltimoMovimiento == fecha);
        return enUso
            .Where(s => s.Clave.Modalidad is { Length: > 0 })
            .Select(s => new CombinacionDeCarteraEnUsoDto(s.Clave.Rol, s.Clave.Linea, s.Clave.Modalidad ?? string.Empty, s.Clave.Subsegmento,
                s.Clave.Categoria, s.Clave.Concepto, s.Clave.Empleado ?? false, s.Clave.Gracia ?? false, s.Clave.Reestructurado ?? false,
                s.Clave.Sucursal, s.UltimoMovimiento))
            .OrderBy(c => c.Role, StringComparer.Ordinal).ThenBy(c => c.CreditLineCode, StringComparer.Ordinal)
            .ToList();
    }
}
