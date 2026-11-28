using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Core.People.Services;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Risk;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Replay;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Reports.Vistas;

/// <summary>Un crédito vivo al corte con su estado reconstruido (nulo si no se pudo: <see cref="Error"/> dice por qué) y sus referencias. (nuevo)</summary>
public sealed record CreditoAlCorte(
    Loan Credito,
    EstadoDelCredito? Estado,
    string? Error,
    LoanTermsVersion? Condiciones,
    LoanTermsVersion? CondicionesIniciales,
    Domain.Entities.Lending.Projections.LoanDailyPosition? Posicion,
    PersonaDelInforme Deudor,
    string LineaCodigo,
    string LineaNombre,
    Guid LineaPublicId,
    string Oficina)
{
    public decimal Saldo(LoanConcept concepto) => Estado?.Saldo(concepto) ?? 0m;

    /// <summary>El capital: del estado o, si no se pudo reconstruir, de la posición diaria.</summary>
    public decimal Capital => Estado?.Saldo(LoanConcept.Principal) ?? Posicion?.Principal ?? 0m;

    public decimal InteresCorriente => Estado is null
        ? (Posicion?.CurrentInterest ?? 0m) + (Posicion?.CurrentInterestInSuspense ?? 0m)
        : Saldo(LoanConcept.CurrentInterest) + Saldo(LoanConcept.CurrentInterestInSuspense);

    public decimal InteresDeMora => Estado is null
        ? (Posicion?.DefaultInterest ?? 0m) + (Posicion?.DefaultInterestInSuspense ?? 0m)
        : Saldo(LoanConcept.DefaultInterest) + Saldo(LoanConcept.DefaultInterestInSuspense);

    public decimal Seguro => Estado is null ? Posicion?.Insurance ?? 0m : Saldo(LoanConcept.Insurance);

    public decimal Administracion => Estado is null ? Posicion?.Administration ?? 0m : Saldo(LoanConcept.Administration);

    public decimal Otros => Estado is null
        ? (Posicion?.CollectionFee ?? 0m) + (Posicion?.OtherCharge ?? 0m)
        : Saldo(LoanConcept.CollectionFee) + Saldo(LoanConcept.OtherCharge);

    public decimal AFavor => Estado is null
        ? (Posicion?.Advance ?? 0m) + (Posicion?.CreditBalance ?? 0m)
        : Saldo(LoanConcept.Advance) + Saldo(LoanConcept.CreditBalance);

    /// <summary>Todo lo que se debe: capital, intereses (con sus suspensos), seguro, administración y otros cargos, sin descontar lo que está a favor.</summary>
    public decimal TotalAdeudado => Capital + InteresCorriente + InteresDeMora + Seguro + Administracion + Otros;

    public decimal CapitalVencido => Estado?.CapitalVencido ?? Posicion?.PrincipalPastDue ?? 0m;

    public int DiasDeMora => Estado?.AlturaDeMora.Dias ?? Posicion?.DaysPastDue ?? 0;

    public string Calificacion => Posicion?.DailyRating is { } c
        ? TextosDeInformesDeCartera.Calificacion(c, Posicion.DailyRatingSubcategory ?? RatingSubcategory.None)
        : string.Empty;
}

/// <summary>Los datos de la persona que llevan los informes detallados. (nuevo)</summary>
public sealed record PersonaDelInforme(Guid PublicId, string TipoDeDocumento, string Documento, string Nombre);

/// <summary>
/// La cartera de créditos viva a un corte, crédito por crédito, con el <b>mismo estado que el estado de cuenta</b>: la reconstrucción del motor
/// (<see cref="MotorDeCartera.Reconstruir"/>) a la fecha, por tandas, con el cargador en bloque (<see cref="CargadorDeReconstruccion"/>). Así el resumen
/// por línea, el detalle por crédito y las cuotas pendientes suman lo mismo entre sí y con lo que ve el cajero. Un crédito que no se puede reconstruir
/// (falta un valor nacional del día, una regla) no se pierde: sale con la posición diaria y su motivo. El alcance y los filtros comunes los pone
/// <see cref="AmbitoDeInformeDeCartera"/>. No escribe ni audita (la ruta audita la exportación). (nuevo)
/// </summary>
public static class CarteraAlCorte
{
    private const int Tanda = 200;

    public static async Task<IReadOnlyList<CreditoAlCorte>> LeerAsync(IApplicationDbContext db, CargadorDeReconstruccion cargador,
        AmbitoDeInformeDeCartera ambito, DateOnly corte, CancellationToken ct)
    {
        var creditos = await CargadorDeRiesgo.VivosAl(ambito.Creditos(db), corte).OrderBy(l => l.Number).ThenBy(l => l.Id).ToListAsync(ct);
        if (creditos.Count == 0) return [];
        var ids = creditos.Select(c => c.Id).ToList();
        var posiciones = await AmbitoDeInformeDeCartera.PosicionesAlCorteAsync(db, ids, corte, ct);
        if (ambito.Modalidad is not null || ambito.Subsegmento is not null)
        {
            creditos = creditos.Where(c => posiciones.TryGetValue(c.Id, out var p) && ambito.DeLaModalidad(p.ModalityCode, p.SubsegmentCode)).ToList();
            ids = creditos.Select(c => c.Id).ToList();
        }

        var vigentes = await AmbitoDeInformeDeCartera.CondicionesVigentesAsync(db, ids, ct);
        var iniciales = await AmbitoDeInformeDeCartera.CondicionesInicialesAsync(db, ids, ct);
        var personas = await PersonasAsync(db, creditos.Select(c => c.DebtorPersonId).Distinct().ToList(), ct);
        var lineaIds = creditos.Select(c => c.CreditLineId).Distinct().ToList();
        var lineas = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => lineaIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => new { l.Code, l.Name, l.PublicId }, ct);
        var oficinaIds = creditos.Select(c => c.BranchId).Distinct().ToList();
        var oficinas = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => oficinaIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.Name, ct);

        var resultado = new List<CreditoAlCorte>(creditos.Count);
        foreach (var tanda in creditos.Chunk(Tanda))
        {
            var cargados = await cargador.CargarAsync(tanda.Select(c => c.Id).ToList(), null, corte, ct);
            foreach (var c in tanda)
            {
                EstadoDelCredito? estado = null;
                string? error = null;
                if (cargados.TryGetValue(c.Id, out var cargado) && cargado.Listo)
                {
                    try
                    {
                        estado = MotorDeCartera.Reconstruir(cargado.Entrada!, corte);
                    }
                    catch (ReglaDeCarteraException ex)
                    {
                        error = ex.Problema.Mensaje;
                    }
                }
                else
                {
                    error = cargado?.Error?.Message ?? "No se pudo cargar el crédito.";
                }
                var linea = lineas.GetValueOrDefault(c.CreditLineId);
                resultado.Add(new CreditoAlCorte(c, estado, error, vigentes.GetValueOrDefault(c.Id), iniciales.GetValueOrDefault(c.Id),
                    posiciones.GetValueOrDefault(c.Id), personas.GetValueOrDefault(c.DebtorPersonId) ?? new PersonaDelInforme(Guid.Empty, string.Empty, string.Empty, string.Empty),
                    linea?.Code ?? string.Empty, linea?.Name ?? string.Empty, linea?.PublicId ?? Guid.Empty, oficinas.GetValueOrDefault(c.BranchId) ?? string.Empty));
            }
        }
        return resultado;
    }

    /// <summary>La nota de los créditos que no se pudieron reconstruir (salen con la posición diaria), o nula si no hubo.</summary>
    public static string? NotaDeErrores(IReadOnlyList<CreditoAlCorte> creditos)
    {
        var conError = creditos.Where(c => c.Error is not null).ToList();
        if (conError.Count == 0) return null;
        var ejemplos = string.Join("; ", conError.Take(5).Select(c => $"{c.Credito.Number}: {c.Error}"));
        return $"{conError.Count} crédito(s) no se pudieron recalcular al corte y salen con su posición diaria ({ejemplos}{(conError.Count > 5 ? "…" : string.Empty)}).";
    }

    public static async Task<Dictionary<int, PersonaDelInforme>> PersonasAsync(IApplicationDbContext db, IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        var filas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.PublicId, p.IdType, p.TaxId, p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName })
            .ToListAsync(ct);
        return filas.ToDictionary(p => p.Id, p => new PersonaDelInforme(p.PublicId, p.IdType, p.TaxId,
            PersonFactory.NombreVisible(p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName)));
    }
}
