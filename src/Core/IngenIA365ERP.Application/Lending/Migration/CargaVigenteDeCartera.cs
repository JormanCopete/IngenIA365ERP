using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Lending.Origination.CreditLimits;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Migration;

/// <summary>Un crédito de la carga aplicada en la exposición de una persona: su fila, número, deudor, capital al corte y si la persona es codeudora. (nuevo)</summary>
public sealed record CreditoMigradoEnLaExposicion(int MigrationLoanRowId, string Number, int DebtorPersonId, decimal Capital, bool ComoCodeudor);

/// <summary>
/// El lector de la carga <c>Applied</c> antes de activar (feature 013, US8, T0746; FR-142, T85, T94; interfaces.md §6, «desplegada, sin activar»):
/// las personas de la carga (deudores de <c>loans</c> y <c>charged-off</c> y codeudores) y la exposición que la carga les suma como si sus
/// créditos hubieran nacido (el capital de <c>balances</c> al corte). Sólo lee las tandas vivas de la carga aplicada; una <c>Replaced</c> o un
/// <c>Draft</c> no cuentan, y <b>después de activar nadie lee la preparación</b>: con <c>LND_Setup.ActivationDate</c> devuelve vacío. Lo usan
/// <c>ObligacionesDeCartera</c> (<c>MigratedNotActivated</c>), <c>ExposicionConsolidadaDelDeudor</c> (endeudamiento consolidado, grupo conectado,
/// concentración y vinculados) y <see cref="RevalidacionDeCuposMigrados"/>. (nuevo)
/// </summary>
public sealed class CargaVigenteDeCartera(IApplicationDbContext db)
{
    /// <summary>La carga aplicada sin activar, o nula (Cartera activada, o sin carga aplicada).</summary>
    public async Task<MigrationLoad?> AplicadaAsync(CancellationToken ct)
    {
        if (await db.LendingSetups.AsNoTracking().AnyAsync(s => s.Id == LendingSetup.IdDeLaFilaUnica && s.ActivationDate != null, ct)) return null;
        return await db.MigrationLoads.AsNoTracking().Where(m => m.Status == MigrationLoadStatus.Applied && !m.IsDeleted)
            .OrderByDescending(m => m.AppliedAt).FirstOrDefaultAsync(ct);
    }

    private IQueryable<int> PartesVivas(int carga) => db.MigrationLoadParts.AsNoTracking()
        .Where(p => p.MigrationLoadId == carga && p.Status == MigrationPartStatus.Imported && !p.IsDeleted).Select(p => p.Id);

    /// <summary>De <paramref name="personas"/>, las que están en la carga aplicada como deudoras (de vivos o castigados) o codeudoras.</summary>
    public async Task<IReadOnlySet<int>> PersonasAsync(IReadOnlyCollection<int> personas, CancellationToken ct)
    {
        if (personas.Count == 0 || await AplicadaAsync(ct) is not { } carga) return new HashSet<int>();
        var ids = personas.Distinct().ToList();
        var partes = PartesVivas(carga.Id);
        var deudores = await db.MigrationLoanRows.AsNoTracking().Where(r => partes.Contains(r.MigrationLoadPartId) && ids.Contains(r.PersonId))
            .Select(r => r.PersonId).ToListAsync(ct);
        var castigados = await db.MigrationChargedOffRows.AsNoTracking().Where(r => partes.Contains(r.MigrationLoadPartId) && ids.Contains(r.PersonId))
            .Select(r => r.PersonId).ToListAsync(ct);
        var codeudores = await db.MigrationCoDebtorRows.AsNoTracking().Where(r => partes.Contains(r.MigrationLoadPartId) && ids.Contains(r.CoDebtorPersonId))
            .Select(r => r.CoDebtorPersonId).ToListAsync(ct);
        return deudores.Concat(castigados).Concat(codeudores).ToHashSet();
    }

    /// <summary>Todas las personas de la carga aplicada (deudores y codeudores), para revalidar sus cupos.</summary>
    public async Task<IReadOnlyList<int>> TodasLasPersonasAsync(MigrationLoad carga, CancellationToken ct)
    {
        var partes = PartesVivas(carga.Id);
        var deudores = await db.MigrationLoanRows.AsNoTracking().Where(r => partes.Contains(r.MigrationLoadPartId)).Select(r => r.PersonId).ToListAsync(ct);
        var codeudores = await db.MigrationCoDebtorRows.AsNoTracking().Where(r => partes.Contains(r.MigrationLoadPartId)).Select(r => r.CoDebtorPersonId)
            .ToListAsync(ct);
        var cupos = await db.MigrationCreditLimitRows.AsNoTracking().Where(r => partes.Contains(r.MigrationLoadPartId)).Select(r => r.PersonId).ToListAsync(ct);
        return deudores.Concat(codeudores).Concat(cupos).Distinct().Order().ToList();
    }

    /// <summary>
    /// Lo que la carga aplicada suma a la exposición de cada persona como si sus créditos hubieran nacido: el capital al corte de cada crédito de
    /// <c>loans</c> como deudora y, como codeudora (rol <c>CoDebtor</c>), el de los créditos que respalda. Vacío después de activar.
    /// </summary>
    public async Task<IReadOnlyDictionary<int, IReadOnlyList<CreditoMigradoEnLaExposicion>>> ExposicionAsync(IReadOnlyCollection<int> personas,
        CancellationToken ct)
    {
        var resultado = new Dictionary<int, IReadOnlyList<CreditoMigradoEnLaExposicion>>();
        if (personas.Count == 0 || await AplicadaAsync(ct) is not { } carga) return resultado;
        var ids = personas.Distinct().ToList();
        var partes = PartesVivas(carga.Id);
        var codeudas = await db.MigrationCoDebtorRows.AsNoTracking()
            .Where(r => partes.Contains(r.MigrationLoadPartId) && ids.Contains(r.CoDebtorPersonId) && r.Role == CreditPartyRole.CoDebtor)
            .Select(r => new { r.CoDebtorPersonId, r.SourceLoanNumber }).ToListAsync(ct);
        var numerosRespaldados = codeudas.Select(c => c.SourceLoanNumber).Distinct().ToList();
        var creditos = await db.MigrationLoanRows.AsNoTracking()
            .Where(r => partes.Contains(r.MigrationLoadPartId) && (ids.Contains(r.PersonId) || numerosRespaldados.Contains(r.SourceLoanNumber)))
            .Select(r => new { r.Id, r.SourceLoanNumber, r.PersonId }).ToListAsync(ct);
        if (creditos.Count == 0) return resultado;
        var numeros = creditos.Select(c => c.SourceLoanNumber).Distinct().ToList();
        var capital = await db.MigrationBalanceRows.AsNoTracking()
            .Where(b => partes.Contains(b.MigrationLoadPartId) && numeros.Contains(b.SourceLoanNumber) && b.Concept == LoanConcept.Principal)
            .GroupBy(b => b.SourceLoanNumber).Select(g => new { g.Key, Capital = g.Sum(b => b.Amount) })
            .ToDictionaryAsync(x => x.Key, x => x.Capital, ct);

        foreach (var persona in ids)
        {
            var propios = creditos.Where(c => c.PersonId == persona)
                .Select(c => new CreditoMigradoEnLaExposicion(c.Id, c.SourceLoanNumber, c.PersonId, capital.GetValueOrDefault(c.SourceLoanNumber), false));
            var respaldados = codeudas.Where(c => c.CoDebtorPersonId == persona)
                .Join(creditos, c => c.SourceLoanNumber, l => l.SourceLoanNumber, (_, l) => l)
                .Where(l => l.PersonId != persona)
                .Select(l => new CreditoMigradoEnLaExposicion(l.Id, l.SourceLoanNumber, l.PersonId, capital.GetValueOrDefault(l.SourceLoanNumber), true));
            var lista = propios.Concat(respaldados).ToList();
            if (lista.Count > 0) resultado[persona] = lista;
        }
        return resultado;
    }
}

/// <summary>
/// La revalidación de los cupos otorgados a mano contra la carga (feature 013, US8, T0746; FR-142, FR-060): al aplicar una carga que reemplaza otra
/// —y, en la activación, con lo que nace—, cada cupo vigente otorgado a mano (no migrado) de una persona de la carga que ya no cumple (excede la
/// concentración con la exposición de la carga, o es de un vinculado sin decisión del consejo; <see cref="RevalidacionDeCupos.QueFaltaAsync"/>)
/// queda con el bloqueo automático <c>LimitRevalidation</c> («cupo por revalidar») y la alerta <c>Cartera.CupoPorRevalidar</c> (<c>DedupKey</c>
/// <c>…:{cupo}</c>). No guarda. (nuevo)
/// </summary>
public sealed class RevalidacionDeCuposMigrados(
    IApplicationDbContext db,
    CargaVigenteDeCartera carga,
    RevalidacionDeCupos revalidacion,
    IAlertas alertas,
    IActorActual actorActual,
    IDateTimeService reloj)
{
    public async Task<int> RevalidarAsync(MigrationLoad aplicada, CancellationToken ct)
    {
        var personas = await carga.TodasLasPersonasAsync(aplicada, ct);
        if (personas.Count == 0) return 0;
        var hoy = reloj.HoyLocal;
        var todas = await VersionesDeCupo.DeAsync(db, personas, ct);
        var vigentes = todas.GroupBy(v => (v.PersonId, v.Kind, v.CreditLineId)).Select(g => VersionesDeCupo.Vigente(g, hoy)).OfType<CreditLimit>()
            .Where(v => v.Amount > 0m && v.Origin != CreditLimitOrigin.Migration).ToList();
        var usuario = (await actorActual.ObtenerAsync(ct)).UserId;
        var bloqueados = 0;
        foreach (var cupo in vigentes)
        {
            if (await revalidacion.QueFaltaAsync(cupo, hoy, ct) is null) continue;
            var clave = cupo.PublicId.ToString();
            if (await db.DebtorBlocks.AnyAsync(b => b.PersonId == cupo.PersonId && b.Reason == DebtorBlockReason.LimitRevalidation && b.SourceKey == clave
                    && b.LiftedAt == null && !b.IsDeleted, ct)) continue;
            db.DebtorBlocks.Add(DebtorBlock.Nuevo(cupo.PersonId, DebtorBlockReason.LimitRevalidation, clave, hoy,
                "Con los créditos de la carga de migración el cupo otorgado a mano ya no cumple un límite: revalídelo.", usuario));
            await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.CupoPorRevalidar, "Cupo por revalidar",
                "Con los créditos de la carga de migración, un cupo otorgado a mano excede un límite o es de un vinculado sin decisión del consejo. "
                + "Queda bloqueado hasta revalidarlo en Cartera › Cupos.",
                "CreditLimit", cupo.PublicId, DedupKey: $"{TiposDeAlerta.CupoPorRevalidar}:{cupo.PublicId:N}"), ct);
            bloqueados++;
        }
        return bloqueados;
    }
}
