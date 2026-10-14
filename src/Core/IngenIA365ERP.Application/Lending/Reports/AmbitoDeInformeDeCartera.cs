using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Reports;

/// <summary>
/// El ámbito de una vista del centro de informes de Cartera (feature 013, US15, T0797; api.md §17, FR-004, SC-015): el alcance de la petición
/// (<see cref="IAlcanceDeCartera"/>) y los filtros comunes que se resuelven por PublicId —<c>branch</c>, <c>line</c>, <c>advisor</c>— ya
/// traducidos a Ids. <see cref="Creditos"/> es la consulta de créditos con el alcance <b>aplicado en la consulta</b>
/// (<see cref="FiltroDeAlcanceDeCartera.PorOficinaYLinea{T}"/>, nunca en memoria): una sucursal o una línea fuera del alcance da un resultado
/// vacío, nunca otra oficina; un PublicId que no existe, también vacío. Lo usan todas las vistas de <c>Lending/Reports/Vistas</c>. (nuevo)
/// </summary>
public sealed class AmbitoDeInformeDeCartera
{
    private AmbitoDeInformeDeCartera(AlcanceDeCartera alcance, FiltrosDeInformeDeCartera filtros, int? sucursal, int? linea, int? asesor, bool vacio)
    {
        Alcance = alcance;
        Filtros = filtros;
        Sucursal = sucursal;
        Linea = linea;
        Asesor = asesor;
        Vacio = vacio;
    }

    public AlcanceDeCartera Alcance { get; }
    public FiltrosDeInformeDeCartera Filtros { get; }
    public int? Sucursal { get; }
    public int? Linea { get; }
    /// <summary>El usuario (<c>SEC_Users.Id</c>) del filtro <c>advisor</c>: quien radicó la solicitud del crédito.</summary>
    public int? Asesor { get; }
    /// <summary>Algún filtro por PublicId nombra algo que no existe: nada que mostrar.</summary>
    public bool Vacio { get; }

    /// <summary>El alcance es de todas las oficinas y todas las líneas (lo que es de toda la cooperativa, como una carga de migración).</summary>
    public bool EsTotal => Alcance.TodasLasOficinas && Alcance.TodasLasLineas;

    /// <summary>La modalidad pedida (sin distinguir mayúsculas), o nula.</summary>
    public string? Modalidad => string.IsNullOrWhiteSpace(Filtros.Modality) ? null : Filtros.Modality.Trim();

    /// <summary>El sub-segmento pedido, o nulo.</summary>
    public string? Subsegmento => string.IsNullOrWhiteSpace(Filtros.Subsegment) ? null : Filtros.Subsegment.Trim();

    /// <summary>Lee el alcance de la petición y resuelve los filtros por PublicId.</summary>
    public static async Task<AmbitoDeInformeDeCartera> LeerAsync(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion,
        FiltrosDeInformeDeCartera filtros, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filtros);
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var vacio = false;
        int? sucursal = null, linea = null, asesor = null;
        if (filtros.Branch is { } b)
        {
            sucursal = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(x => x.PublicId == b).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            vacio |= sucursal is null;
        }
        if (filtros.Line is { } l)
        {
            linea = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(x => x.PublicId == l).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            vacio |= linea is null;
        }
        if (filtros.Advisor is { } a)
        {
            asesor = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(x => x.PublicId == a).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            vacio |= asesor is null;
        }
        return new AmbitoDeInformeDeCartera(alcance, filtros, sucursal, linea, asesor, vacio);
    }

    /// <summary>
    /// Los créditos (sin borrar) del alcance y de la sucursal, la línea y el asesor pedidos. La modalidad y el sub-segmento se miran sobre las
    /// condiciones vigentes del crédito (<see cref="CondicionesVigentesAsync"/>) o sobre la posición diaria, según la vista.
    /// </summary>
    public IQueryable<Loan> Creditos(IApplicationDbContext db)
    {
        var consulta = db.Loans.AsNoTracking().Where(l => !l.IsDeleted).PorOficinaYLinea(Alcance, l => l.BranchId, l => l.CreditLineId);
        if (Vacio) return consulta.Where(l => false);
        if (Sucursal is { } s) consulta = consulta.Where(l => l.BranchId == s);
        if (Linea is { } li) consulta = consulta.Where(l => l.CreditLineId == li);
        if (Asesor is { } a)
            consulta = consulta.Where(l => db.CreditApplications.Any(c => c.LoanId == l.Id && (c.SubmittedByUserId ?? c.CreatedByUserId) == a));
        return consulta;
    }

    /// <summary>¿La modalidad y el sub-segmento (de la posición o de las condiciones) son los pedidos?</summary>
    public bool DeLaModalidad(string? modalidad, string? subsegmento) =>
        (Modalidad is null || string.Equals(Modalidad, modalidad, StringComparison.OrdinalIgnoreCase))
        && (Subsegmento is null || string.Equals(Subsegmento, subsegmento, StringComparison.OrdinalIgnoreCase));

    /// <summary>Las condiciones vigentes (la última versión) de unos créditos.</summary>
    public static async Task<IReadOnlyDictionary<int, LoanTermsVersion>> CondicionesVigentesAsync(IApplicationDbContext db, IReadOnlyCollection<int> creditos,
        CancellationToken ct)
    {
        if (creditos.Count == 0) return new Dictionary<int, LoanTermsVersion>();
        return (await db.LoanTermsVersions.AsNoTracking().Where(t => creditos.Contains(t.LoanId)).ToListAsync(ct))
            .GroupBy(t => t.LoanId).ToDictionary(g => g.Key, g => g.OrderBy(t => t.VersionNumber).Last());
    }

    /// <summary>Las condiciones con que nació cada crédito (la primera versión): la tasa y el plazo de la colocación.</summary>
    public static async Task<IReadOnlyDictionary<int, LoanTermsVersion>> CondicionesInicialesAsync(IApplicationDbContext db, IReadOnlyCollection<int> creditos,
        CancellationToken ct)
    {
        if (creditos.Count == 0) return new Dictionary<int, LoanTermsVersion>();
        return (await db.LoanTermsVersions.AsNoTracking().Where(t => creditos.Contains(t.LoanId)).ToListAsync(ct))
            .GroupBy(t => t.LoanId).ToDictionary(g => g.Key, g => g.OrderBy(t => t.VersionNumber).First());
    }

    /// <summary>
    /// La posición diaria vigente de cada crédito al <paramref name="corte"/>: la última fecha hasta el corte y, en ella, la última versión.
    /// </summary>
    public static async Task<IReadOnlyDictionary<int, Domain.Entities.Lending.Projections.LoanDailyPosition>> PosicionesAlCorteAsync(
        IApplicationDbContext db, IReadOnlyCollection<int> creditos, DateOnly corte, CancellationToken ct)
    {
        if (creditos.Count == 0) return new Dictionary<int, Domain.Entities.Lending.Projections.LoanDailyPosition>();
        return (await db.LoanDailyPositions.AsNoTracking().Where(p => creditos.Contains(p.LoanId) && p.PositionDate <= corte).ToListAsync(ct))
            .GroupBy(p => p.LoanId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.PositionDate).ThenByDescending(p => p.Version).First());
    }

    /// <summary>Quién radicó la solicitud de cada crédito (el asesor de la colocación); los migrados y los de Comercial no tienen.</summary>
    public static async Task<IReadOnlyDictionary<int, int>> AsesoresAsync(IApplicationDbContext db, IReadOnlyCollection<int> creditos, CancellationToken ct)
    {
        if (creditos.Count == 0) return new Dictionary<int, int>();
        return (await db.CreditApplications.AsNoTracking().Where(a => a.LoanId != null && creditos.Contains(a.LoanId.Value))
                .Select(a => new { LoanId = a.LoanId!.Value, Usuario = a.SubmittedByUserId ?? a.CreatedByUserId }).ToListAsync(ct))
            .GroupBy(a => a.LoanId).ToDictionary(g => g.Key, g => g.First().Usuario);
    }

    /// <summary>Lo que dice el subtítulo sobre el alcance: «todas las oficinas y líneas» o «N oficinas, M líneas del alcance».</summary>
    public string Descripcion()
    {
        if (Vacio) return "un filtro no corresponde a nada registrado";
        var oficinas = Alcance.TodasLasOficinas ? "todas las oficinas" : $"{Alcance.Oficinas.Count} oficina(s) del alcance";
        var lineas = Alcance.TodasLasLineas ? "todas las líneas" : $"{Alcance.Lineas.Count} línea(s) del alcance";
        return $"{oficinas}, {lineas}";
    }

    /// <summary>Los estados de un crédito que ya nació (los que tienen saldo o historia).</summary>
    public static bool Nacido(LoanStatus estado) => estado is not (LoanStatus.PendingDisbursement or LoanStatus.Voided);
}
