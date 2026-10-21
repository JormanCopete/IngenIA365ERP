using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Origination.ConnectedGroups;

/// <summary>Por qué dos personas quedan en el mismo grupo: el fundamento y, si es un grupo registrado, cuál. (nuevo)</summary>
public sealed record CaminoAlGrupo(int Desde, int Hacia, ConnectedGroupBasis Fundamento, Guid? GrupoPublicId = null);

/// <summary>
/// El grupo conectado de una persona a una fecha (feature 013, T0393; FR-041, SC-022; C112): la persona y todos los que llegan a ella por
/// alguno de los tres caminos, sin repetir; los caminos (para explicarlo) y los grupos registrados que tocó. Una persona sola es un grupo de uno.
/// (nuevo)
/// </summary>
public sealed record GrupoConectado(int PersonId, IReadOnlyList<int> Miembros, IReadOnlyList<CaminoAlGrupo> Caminos, IReadOnlyList<Guid> GruposRegistrados)
{
    public bool EsDeUno => Miembros.Count <= 1;

    /// <summary>El primer grupo registrado que lo forma, para nombrarlo en el error; nulo si sólo lo forman vínculos o codeudores.</summary>
    public Guid? GrupoPublicId => GruposRegistrados.Count > 0 ? GruposRegistrados[0] : null;
}

/// <summary>
/// El grupo conectado de un deudor (feature 013, T0393; FR-041, SC-022; DT §7.10 C112): a la fecha D, la unión <b>transitiva</b> de
/// <list type="number">
/// <item>los miembros vigentes de los grupos registrados en <c>LND_ConnectedGroups</c> donde está (un miembro o un grupo con <c>EndedAt</c> el
/// día D o antes no cuenta);</item>
/// <item>los vinculados de <c>LND_RelatedParties</c> vigentes por la misma persona vinculada (el cónyuge o pariente y la persona por la que
/// existe el vínculo);</item>
/// <item>las personas con las que comparte un codeudor en un crédito vivo (<c>LND_LoanParties</c>) o en una solicitud <c>Approved</c> vigente
/// (<c>LND_CreditApplicationParties</c>).</item>
/// </list>
/// Recorre por niveles con una consulta por fuente en cada nivel y no repite a una persona que llega por dos caminos. Lo derivado no se guarda;
/// lo registrado, sí (lo escriben los comandos de grupos). Con US8, la carga aplicada antes de activar entra como si hubiera nacido (FR-142).
/// Sólo lee. (nuevo)
/// </summary>
public sealed class GrupoConectadoDelDeudor(IApplicationDbContext db)
{
    private static readonly LoanStatus[] Vivos = [LoanStatus.PendingDisbursement, LoanStatus.Active];

    /// <summary>Tope de niveles del recorrido (un grupo real no pasa de unos pocos saltos; es la defensa ante un ciclo raro de datos).</summary>
    private const int NivelesMaximos = 50;

    public async Task<GrupoConectado> DeAsync(int personId, DateOnly fecha, CancellationToken ct = default)
    {
        var miembros = new List<int> { personId };
        var vistos = new HashSet<int> { personId };
        var caminos = new List<CaminoAlGrupo>();
        var grupos = new List<Guid>();
        var frontera = new List<int> { personId };
        var finDelDia = fecha.AddDays(1).ToDateTime(TimeOnly.MinValue);

        for (var nivel = 0; nivel < NivelesMaximos && frontera.Count > 0; nivel++)
        {
            var nuevos = new List<CaminoAlGrupo>();
            nuevos.AddRange(await PorGruposRegistradosAsync(frontera, fecha, finDelDia, grupos, ct));
            nuevos.AddRange(await PorVinculadosAsync(frontera, fecha, ct));
            nuevos.AddRange(await PorCodeudoresComunesAsync(frontera, fecha, ct));

            frontera = [];
            foreach (var camino in nuevos)
            {
                if (!vistos.Add(camino.Hacia)) continue;
                miembros.Add(camino.Hacia);
                caminos.Add(camino);
                frontera.Add(camino.Hacia);
            }
        }
        return new GrupoConectado(personId, miembros, caminos, grupos);
    }

    private async Task<IReadOnlyList<CaminoAlGrupo>> PorGruposRegistradosAsync(List<int> frontera, DateOnly fecha, DateTime finDelDia, List<Guid> grupos,
        CancellationToken ct)
    {
        // Un miembro o un grupo terminado el día D (EndedAt antes de que acabe D) ya no cuenta ese día.
        var vigentes = from m in db.ConnectedGroupMembers.AsNoTracking()
                       join g in db.ConnectedGroups.AsNoTracking() on m.GroupId equals g.Id
                       where !m.IsDeleted && !g.IsDeleted && m.ValidFrom <= fecha && g.ValidFrom <= fecha
                             && (m.EndedAt == null || m.EndedAt >= finDelDia) && (g.EndedAt == null || g.EndedAt >= finDelDia)
                       select new { m.GroupId, m.PersonId, g.PublicId, g.Basis };
        var mios = await vigentes.Where(x => frontera.Contains(x.PersonId)).ToListAsync(ct);
        if (mios.Count == 0) return [];
        var ids = mios.Select(x => x.GroupId).Distinct().ToArray();
        var companeros = await vigentes.Where(x => ids.Contains(x.GroupId)).ToListAsync(ct);
        foreach (var g in mios.Select(x => x.PublicId).Distinct().Where(g => !grupos.Contains(g))) grupos.Add(g);
        return (from m in mios
                join c in companeros on m.GroupId equals c.GroupId
                where c.PersonId != m.PersonId
                orderby c.GroupId, c.PersonId
                select new CaminoAlGrupo(m.PersonId, c.PersonId, c.Basis, c.PublicId)).ToList();
    }

    private async Task<IReadOnlyList<CaminoAlGrupo>> PorVinculadosAsync(List<int> frontera, DateOnly fecha, CancellationToken ct)
    {
        var vinculos = await db.RelatedParties.AsNoTracking()
            .Where(v => !v.IsDeleted && v.RelatedThroughPersonId != null && v.ValidFrom <= fecha && (v.ValidTo == null || v.ValidTo >= fecha)
                        && (frontera.Contains(v.PersonId) || frontera.Contains(v.RelatedThroughPersonId!.Value)))
            .Select(v => new { v.PersonId, Por = v.RelatedThroughPersonId!.Value })
            .ToListAsync(ct);
        return vinculos
            .Select(v => frontera.Contains(v.PersonId)
                ? new CaminoAlGrupo(v.PersonId, v.Por, ConnectedGroupBasis.RelatedParty)
                : new CaminoAlGrupo(v.Por, v.PersonId, ConnectedGroupBasis.RelatedParty))
            .OrderBy(c => c.Hacia)
            .ToList();
    }

    private async Task<IReadOnlyList<CaminoAlGrupo>> PorCodeudoresComunesAsync(List<int> frontera, DateOnly fecha, CancellationToken ct)
    {
        // Los codeudores de lo que deben las personas de la frontera...
        var mios = await ObligacionesConCodeudorAsync(deudores: frontera, codeudores: null, fecha, ct);
        if (mios.Count == 0) return [];
        var codeudores = mios.Select(x => x.Codeudor).Distinct().ToList();
        // ...y quienes más deben con esos mismos codeudores.
        var otros = await ObligacionesConCodeudorAsync(deudores: null, codeudores: codeudores, fecha, ct);
        return (from m in mios
                join o in otros on m.Codeudor equals o.Codeudor
                where o.Deudor != m.Deudor
                orderby o.Deudor
                select new CaminoAlGrupo(m.Deudor, o.Deudor, ConnectedGroupBasis.CommonCoDebtor)).ToList();
    }

    /// <summary>Los pares (deudor, codeudor) de los créditos vivos y de las solicitudes aprobadas vigentes, filtrados por un lado.</summary>
    private async Task<List<(int Deudor, int Codeudor)>> ObligacionesConCodeudorAsync(List<int>? deudores, List<int>? codeudores, DateOnly fecha,
        CancellationToken ct)
    {
        var enCreditos = from p in db.LoanParties.AsNoTracking()
                         join l in db.Loans.AsNoTracking() on p.LoanId equals l.Id
                         where !p.IsDeleted && !l.IsDeleted && p.Role == CreditPartyRole.CoDebtor && Vivos.Contains(l.Status)
                               && (p.ReleasedOn == null || p.ReleasedOn > fecha) && (p.ExtinguishedOn == null || p.ExtinguishedOn > fecha)
                         select new { Deudor = l.DebtorPersonId, Codeudor = p.PersonId };
        var enSolicitudes = from p in db.CreditApplicationParties.AsNoTracking()
                            join a in db.CreditApplications.AsNoTracking() on p.ApplicationId equals a.Id
                            where !p.IsDeleted && !a.IsDeleted && p.Role == CreditPartyRole.CoDebtor && a.Status == CreditApplicationStatus.Approved
                                  && a.ApprovalValidUntil >= fecha
                            select new { Deudor = a.ApplicantPersonId, Codeudor = p.PersonId };
        if (deudores is not null)
        {
            enCreditos = enCreditos.Where(x => deudores.Contains(x.Deudor));
            enSolicitudes = enSolicitudes.Where(x => deudores.Contains(x.Deudor));
        }
        if (codeudores is not null)
        {
            enCreditos = enCreditos.Where(x => codeudores.Contains(x.Codeudor));
            enSolicitudes = enSolicitudes.Where(x => codeudores.Contains(x.Codeudor));
        }
        var a1 = await enCreditos.ToListAsync(ct);
        var a2 = await enSolicitudes.ToListAsync(ct);
        return a1.Concat(a2).Select(x => (x.Deudor, x.Codeudor)).Distinct().ToList();
    }
}
