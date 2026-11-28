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
public sealed class DimensionesDeCartera(IApplicationDbContext db, INormativaVigente normativa, SaldosContablesDeCartera saldos, IDateTimeService reloj,
    Accrual.LectorDePoliticaDeReconocimiento? politica = null, Common.ReglasDeParametrosDeCartera? parametros = null)
    : IDimensionesDeCartera
{
    public async Task<CatalogoDeDimensionesDeCarteraDto> CatalogoAsync(CancellationToken ct)
    {
        var lineas = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => !l.IsDeleted)
            .Select(l => new
            {
                l.Id,
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
            CatalogoDeMensajesDeCarteraV1.HechosAContabilidad.Select(t => t.Type).ToList())
        {
            PayoffDepositFactTypes = await HechosDeLasCuentasRecogidasAsync(
                lineas.Where(l => l.Nature == LineNature.Placement && l.Status == CreditLineStatus.Active).Select(l => l.Id).ToList(), ct),
        };
    }

    /// <summary>
    /// 2026-11-27 (feature 028, E3, T080): los hechos con que se consignan las cuentas que la cooperativa recoge con un crédito nuevo (<c>Recogida.ProductosAdmitidos</c>
    /// general o de alguna línea de colocación activa, a hoy): <c>AporteRecibido</c> si admite aportes y <c>ConsignacionEnCuentaAplicada</c> si admite algún
    /// ahorro. Vacío de fábrica (sólo créditos) o sin el lector de parámetros.
    /// </summary>
    private async Task<IReadOnlyList<string>> HechosDeLasCuentasRecogidasAsync(IReadOnlyList<int> lineasDeColocacion, CancellationToken ct)
    {
        if (parametros is null) return [];
        var hoy = reloj.HoyLocal;
        var admisiones = new List<Domain.Lending.Payoffs.AdmisionDeRecogida> { await Payoffs.AdmisionDeRecogidaVigente.LeerAsync(parametros, hoy, null, ct) };
        foreach (var linea in lineasDeColocacion) admisiones.Add(await Payoffs.AdmisionDeRecogidaVigente.LeerAsync(parametros, hoy, linea, ct));
        var hechos = new List<string>();
        if (admisiones.Any(a => a.Admite(Domain.Lending.Payoffs.ProductoDeRecogida.Aportes)))
            hechos.Add(IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits.AporteRecibidoV1.Type);
        if (admisiones.Any(a => a.Admite(Domain.Lending.Payoffs.ProductoDeRecogida.AhorroALaVista) || a.Admite(Domain.Lending.Payoffs.ProductoDeRecogida.AhorroPermanente)
                                || a.Admite(Domain.Lending.Payoffs.ProductoDeRecogida.AhorroContractual)))
            hechos.Add(IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits.ConsignacionEnCuentaAplicadaV1.Type);
        return hechos;
    }

    public async Task<IReadOnlyList<CombinacionDeCarteraEnUsoDto>> CombinacionesEnUsoAsync(DateOnly fecha, CancellationToken ct)
    {
        var movimientos = await saldos.MovimientosAsync(fecha, alcance: null, ct);
        var enUso = SaldosContablesDeCartera.Agrupar(movimientos)
            .Where(s => s.Saldo != 0m || s.UltimoMovimiento == fecha);
        var combinaciones = enUso
            .Where(s => s.Clave.Modalidad is { Length: > 0 })
            .Select(s => new CombinacionDeCarteraEnUsoDto(s.Clave.Rol, s.Clave.Linea, s.Clave.Modalidad ?? string.Empty, s.Clave.Subsegmento,
                s.Clave.Categoria, s.Clave.Concepto, s.Clave.Empleado ?? false, s.Clave.Gracia ?? false, s.Clave.Reestructurado ?? false,
                s.Clave.Sucursal, s.UltimoMovimiento))
            .ToList();
        combinaciones.AddRange(await DelReconocimientoAsync(fecha, combinaciones, ct));
        return combinaciones
            .OrderBy(c => c.Role, StringComparer.Ordinal).ThenBy(c => c.CreditLineCode, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// 2026-11-16 (pedido del dueño; feature 024, fase 4): con <c>Causacion.Momento = InicioDelCiclo</c> y el reconocimiento contra el ingreso diferido, el
    /// diferido de la cuota de cada línea activa de colocación que reconoce al inicio del ciclo, por modalidad y rubro —el interés corriente y, si la línea
    /// los cobra, el seguro y la administración—, aunque todavía no haya nada reconocido: así la completitud pide su regla antes del primer reconocimiento.
    /// De fábrica («Diaria») no agrega nada. (nuevo)
    /// </summary>
    private async Task<IReadOnlyList<CombinacionDeCarteraEnUsoDto>> DelReconocimientoAsync(DateOnly fecha, IReadOnlyList<CombinacionDeCarteraEnUsoDto> yaEnUso,
        CancellationToken ct)
    {
        if (politica is null) return [];
        var lineas = await db.CreditLines.AsNoTracking()
            .Where(l => !l.IsDeleted && l.Status == CreditLineStatus.Active && l.Nature == LineNature.Placement)
            .Select(l => new
            {
                l.Id,
                l.Code,
                Version = db.CreditLineVersions.Where(v => v.CreditLineId == l.Id).OrderByDescending(v => v.VersionNumber)
                    .Select(v => new { v.Id, v.ModalityCode }).FirstOrDefault(),
            })
            .ToListAsync(ct);
        if (lineas.Count == 0) return [];
        var leida = await politica.LeerAsync(fecha, lineas.Select(l => l.Id), ct);
        if (leida.LineasAlInicioDelCiclo.Count == 0 || leida.Modo != Domain.Lending.Replay.ModoDeReconocimiento.IngresoDiferido) return [];
        var sucursal = await db.Branches.AsNoTracking().Where(b => !b.IsDeleted).OrderBy(b => b.Id).Select(b => (Guid?)b.PublicId).FirstOrDefaultAsync(ct);
        if (sucursal is not { } principal) return [];
        var versiones = lineas.Where(l => l.Version is not null).Select(l => l.Version!.Id).ToList();
        var cargos = await db.CreditLineInsurances.AsNoTracking().Where(s => versiones.Contains(s.CreditLineVersionId))
            .Select(s => new { s.CreditLineVersionId, s.Kind }).ToListAsync(ct);
        var salida = new List<CombinacionDeCarteraEnUsoDto>();
        foreach (var l in lineas.Where(l => leida.AlInicioDelCiclo(l.Id) && l.Version is { ModalityCode.Length: > 0 }))
        {
            var suyos = cargos.Where(c => c.CreditLineVersionId == l.Version!.Id).ToList();
            var rubros = new List<LoanConcept> { LoanConcept.CurrentInterest };
            if (suyos.Any(c => c.Kind == InsuranceKind.Insurance)) rubros.Add(LoanConcept.Insurance);
            if (suyos.Any(c => c.Kind == InsuranceKind.Administration)) rubros.Add(LoanConcept.Administration);
            foreach (var rubro in rubros)
            {
                var concepto = rubro.ToString();
                if (yaEnUso.Any(c => c.Role == SaldosContablesDeCartera.IngresoDiferidoDeLaCuota && c.CreditLineCode == l.Code && c.ConceptCode == concepto)) continue;
                salida.Add(new CombinacionDeCarteraEnUsoDto(SaldosContablesDeCartera.IngresoDiferidoDeLaCuota, l.Code, l.Version!.ModalityCode.Trim(), null, null,
                    concepto, false, false, false, principal, fecha));
            }
        }
        return salida;
    }
}
