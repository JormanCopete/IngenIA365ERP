using System.Globalization;
using IngenIA365ERP.Application.Common.Calendar;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Compliance.DataSubjectRequests;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Domain.Common.Compliance;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Activation;

/// <summary>
/// Lo del deudor que nace de la carga en la activación (feature 013, US8, T0751; FR-139, FR-142, C64, T69, T102; data-model §29.3 «Al activar nace»),
/// después de los créditos, en la transacción de la activación. <b>Lo registrado a mano antes para la misma persona y concepto prevalece</b>: no nace
/// lo migrado y se cuenta en <see cref="ConteoDeActivacion.Prevalecio"/> (va en <c>CountsJson</c>).
/// <list type="bullet">
/// <item><c>credit-limits</c> → <c>LND_CreditLimits</c> con origen <c>Migration</c> (con fin de vigencia, una versión en cero el día siguiente) y la
/// preferencia de abono del cupo;</item>
/// <item><c>blocks</c> → <c>LND_DebtorBlocks</c> (con fin de vigencia, levantado); con <c>Deceased</c>, una <c>LND_InsuranceClaims</c> por cada crédito
/// migrado del deudor cuya línea tiene seguro de vida deudores (aseguradora: la de la fila o la del seguro); con <c>Insolvency</c>, la
/// <c>LND_InsolvencyProceedings</c> (C64: viajan en esta parte);</item>
/// <item><c>authorizations</c> → <c>CMP_HabeasDataConsents</c> por <see cref="IAutorizacionDeDatos.AgregarAsync"/> con <c>Origin = Migration</c>; la
/// referencia de la evidencia y el canal de obtención van en las notas: sin adjunto, la autorización migrada no consulta ni reporta a centrales ni
/// habilita mensajes de cobranza hasta que se adjunte su evidencia (FR-006);</item>
/// <item><c>prior-notices</c> → <c>LND_PriorNotices</c> con las columnas <c>Migrated*</c> (un episodio que no es el vigente según las cuotas no se
/// migra); <c>bureau-reports</c> con dato negativo → <c>MigratedReportedNegativeSince</c> y la central del episodio (si el episodio no trae aviso, nace
/// el registro del reporte sin comunicaciones: el primer archivo de esa central lo retira, FR-123);</item>
/// <item><c>claims</c> → <c>LND_CreditReportClaims</c> en <c>InProcess</c> con su vencimiento legal (los límites del titular y el calendario
/// hábil);</item>
/// <item><c>related-parties</c> → <c>LND_RelatedParties</c> con origen <c>Migration</c>.</item>
/// </list>
/// Agrega sin guardar; la activación guarda. (nuevo)
/// </summary>
public sealed class NacimientoDeLoDelDeudorMigrado(
    IApplicationDbContext db,
    IDateTimeService reloj,
    IAutorizacionDeDatos? autorizaciones = null,
    ReglasDeParametrosDeCartera? parametros = null,
    IValoresNacionales? valores = null,
    ICalendarioHabil? calendario = null)
{
    public const string CanalDelReclamoMigrado = "Migración";

    public async Task<Result> NacerAsync(MigrationLoad carga, Actor actor, ConteoDeActivacion conteo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(carga);
        ArgumentNullException.ThrowIfNull(conteo);
        var partes = await db.MigrationLoadParts.AsNoTracking()
            .Where(p => p.MigrationLoadId == carga.Id && p.Status == MigrationPartStatus.Imported && !p.IsDeleted).Select(p => p.Id).ToListAsync(ct);
        if (partes.Count == 0) return Result.Success();
        var creditos = await db.Loans.AsNoTracking().Where(l => l.MigrationLoadId == carga.Id && l.Origin == LoanOrigin.Migration && !l.IsDeleted)
            .Select(l => new { l.Id, l.Number, l.DebtorPersonId, l.CreditLineId, l.Status }).ToListAsync(ct);
        var porNumero = creditos.ToDictionary(c => c.Number, c => (c.Id, c.DebtorPersonId, c.CreditLineId, c.Status), StringComparer.Ordinal);
        var ahora = reloj.UtcNow;

        await CuposAsync(partes, actor, ahora, conteo, ct);
        await BloqueosAsync(partes, carga, porNumero, actor, ahora, conteo, ct);
        var autorizadas = await AutorizacionesAsync(partes, conteo, ct);
        if (autorizadas.IsFailure) return autorizadas;
        await AvisosPreviosAsync(partes, carga, porNumero, conteo, ct);
        await ReclamosAsync(partes, carga, porNumero, conteo, ct);
        await VinculadosAsync(partes, conteo, ct);
        return Result.Success();
    }

    // ------------------------------------------------------------------------------------------------------------- cupos --

    private async Task CuposAsync(List<int> partes, Actor actor, DateTime ahora, ConteoDeActivacion conteo, CancellationToken ct)
    {
        var filas = await db.MigrationCreditLimitRows.AsNoTracking().Where(r => partes.Contains(r.MigrationLoadPartId)).OrderBy(r => r.ValidFrom).ToListAsync(ct);
        if (filas.Count == 0) return;
        var personas = filas.Select(f => f.PersonId).Distinct().ToList();
        var existentes = await db.CreditLimits.AsNoTracking().Where(c => personas.Contains(c.PersonId) && !c.IsDeleted).ToListAsync(ct);
        var codigos = filas.Select(f => f.CreditLineCode).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var lineas = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => codigos.Contains(l.Code))
            .ToDictionaryAsync(l => l.Code, l => l.Id, StringComparer.OrdinalIgnoreCase, ct);
        var nuevos = new List<(CreditLimit Cupo, PrepaymentChoice? Eleccion)>();
        foreach (var f in filas)
        {
            int? linea = f.CreditLineCode is { } c && lineas.TryGetValue(c, out var id) ? id : null;
            if (existentes.Any(e => e.EsDelMismoCupo(f.PersonId, f.LimitKind, linea) && e.Origin != CreditLimitOrigin.Migration))
            {
                Prevalece(conteo, "CreditLimit");
                continue;
            }
            var numero = existentes.Concat(nuevos.Select(n => n.Cupo)).Where(e => e.EsDelMismoCupo(f.PersonId, f.LimitKind, linea))
                .Select(e => e.VersionNumber).DefaultIfEmpty(0).Max() + 1;
            var cupo = CreditLimit.NuevaVersion(f.PersonId, f.LimitKind, linea, f.Amount, CreditLimitOrigin.Migration, f.ValidFrom, numero, ahora, f.Reason,
                actor.UserId);
            db.CreditLimits.Add(cupo);
            nuevos.Add((cupo, f.PrepaymentChoice));
            conteo.Cupos++;
            if (f.ValidTo is { } hasta)
            {
                var fin = CreditLimit.NuevaVersion(f.PersonId, f.LimitKind, linea, 0m, CreditLimitOrigin.Migration, hasta.AddDays(1), numero + 1, ahora,
                    $"Fin de la vigencia migrada: {f.Reason}", actor.UserId);
                db.CreditLimits.Add(fin);
                nuevos.Add((fin, null));
            }
        }
        await db.SaveChangesAsync(ct);
        foreach (var (cupo, eleccion) in nuevos.Where(n => n.Eleccion is not null))
            db.PrepaymentPreferences.Add(PrepaymentPreference.Registrar(cupo.PersonId, null, cupo.Id, eleccion, 1, ahora, PreferenceOrigin.Migration, actor.UserId));
    }

    // ----------------------------------------------------------------------------------------------------------- bloqueos --

    private async Task BloqueosAsync(List<int> partes, MigrationLoad carga, IReadOnlyDictionary<string, (int Id, int Deudor, int Linea, LoanStatus Estado)> creditos,
        Actor actor, DateTime ahora, ConteoDeActivacion conteo, CancellationToken ct)
    {
        var filas = await db.MigrationBlockRows.AsNoTracking().Where(r => partes.Contains(r.MigrationLoadPartId)).OrderBy(r => r.RowNumber).ToListAsync(ct);
        if (filas.Count == 0) return;
        var personas = filas.Select(f => f.PersonId).Distinct().ToList();
        var aMano = await db.DebtorBlocks.AsNoTracking()
            .Where(b => personas.Contains(b.PersonId) && !b.IsAutomatic && b.LiftedAt == null && !b.IsDeleted).Select(b => new { b.PersonId, b.Reason }).ToListAsync(ct);

        // El seguro de vida deudores de la versión de la línea vigente al corte de cada crédito del deudor fallecido.
        var lineas = creditos.Values.Select(c => c.Linea).Distinct().ToList();
        var versiones = await new VersionVigenteDeLinea(db).LeerVariasAsync(lineas, carga.CutoffDate, ct: ct);

        foreach (var f in filas)
        {
            if (aMano.Any(b => b.PersonId == f.PersonId && b.Reason == f.BlockReason))
            {
                Prevalece(conteo, "DebtorBlock");
                continue;
            }
            int? credito = f.SourceLoanNumber is { } n && creditos.TryGetValue(n, out var c) ? c.Id : null;
            var bloqueo = DebtorBlock.Nuevo(f.PersonId, f.BlockReason,
                string.Create(CultureInfo.InvariantCulture, $"Migration:{carga.PublicId:N}:{f.MigrationLoadPartId}:{f.RowNumber}"), f.Since, f.SupportReference,
                actor.UserId, credito);
            if (f.Until is { } hasta && hasta <= carga.CutoffDate)
                bloqueo.Levantar(hasta, ahora, actor.UserId, "Vigencia migrada terminada antes del corte.");
            db.DebtorBlocks.Add(bloqueo);
            conteo.Bloqueos++;

            if (f.BlockReason == DebtorBlockReason.Deceased)
            {
                foreach (var (_, del) in creditos.Where(x => x.Value.Deudor == f.PersonId && x.Value.Estado == LoanStatus.Active))
                {
                    var seguro = versiones.GetValueOrDefault(del.Linea)?.Seguros.FirstOrDefault(s => s.IsDebtorLifeInsurance);
                    if (seguro is null) continue;
                    db.InsuranceClaims.Add(new InsuranceClaim
                    {
                        LoanId = del.Id,
                        InsurerPersonId = f.InsurerPersonId ?? seguro.InsurerPersonId,
                        DeceasedPersonId = f.PersonId,
                        DeathDate = f.Since,
                        Status = f.InsuranceClaimStatus ?? InsuranceClaimStatus.InProcess,
                        MigrationLoadId = carga.Id,
                    });
                    conteo.ReclamacionesAlSeguro++;
                }
            }
            else if (f.BlockReason == DebtorBlockReason.Insolvency)
            {
                db.InsolvencyProceedings.Add(new InsolvencyProceeding
                {
                    PersonId = f.PersonId,
                    StartDate = f.Since,
                    Status = f.InsolvencyStatus ?? InsolvencyProceedingStatus.Negotiating,
                    MigrationLoadId = carga.Id,
                });
                conteo.Insolvencias++;
            }
        }
    }

    // ---------------------------------------------------------------------------------------------------- autorizaciones --

    private async Task<Result> AutorizacionesAsync(List<int> partes, ConteoDeActivacion conteo, CancellationToken ct)
    {
        var filas = await db.MigrationAuthorizationRows.AsNoTracking().Where(r => partes.Contains(r.MigrationLoadPartId)).OrderBy(r => r.RowNumber).ToListAsync(ct);
        if (filas.Count == 0 || autorizaciones is null) return Result.Success();
        var personas = filas.Select(f => f.PersonId).Distinct().ToList();
        var publicos = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personas.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.PublicId, ct);
        var aMano = await db.HabeasDataConsents.AsNoTracking()
            .Where(c => personas.Contains(c.PersonId) && c.Origin != DataAuthorizationOrigin.Migration && !c.IsDeleted)
            .Select(c => new { c.PersonId, c.Purpose, c.ContactChannel }).ToListAsync(ct);
        foreach (var f in filas)
        {
            if (!Enum.TryParse<DataAuthorizationPurpose>(f.Purpose, out var finalidad)) continue;
            DataAuthorizationChannel? canal = f.ContactChannel is { } c && Enum.TryParse<DataAuthorizationChannel>(c, out var x) ? x : null;
            if (aMano.Any(a => a.PersonId == f.PersonId && a.Purpose == finalidad && a.ContactChannel == canal))
            {
                Prevalece(conteo, "Authorization");
                continue;
            }
            var notas = string.Join(" · ", new[]
            {
                f.PolicyVersionText is { } v ? $"Versión del texto: {v}" : null,
                f.EvidenceReference is { } e ? $"Evidencia: {e}" : "Sin evidencia",
                f.ObtainedThrough is { } o ? $"Obtenida por: {o}" : null,
            }.OfType<string>());
            var agregada = await autorizaciones.AgregarAsync(new AutorizacionAAgregar(publicos[f.PersonId], finalidad,
                f.Status == "Revoked" ? DecisionDeAutorizacion.Declined : DecisionDeAutorizacion.Accepted, null, canal, null, DataAuthorizationOrigin.Migration,
                f.AuthorizedOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), notas), ct);
            if (agregada.IsFailure) return Result.Failure(agregada.Error);
            conteo.Autorizaciones++;
        }
        return Result.Success();
    }

    // ----------------------------------------------------------------------------------------- avisos previos y reportes --

    private async Task AvisosPreviosAsync(List<int> partes, MigrationLoad carga, IReadOnlyDictionary<string, (int Id, int Deudor, int Linea, LoanStatus Estado)> creditos,
        ConteoDeActivacion conteo, CancellationToken ct)
    {
        var avisos = await db.MigrationPriorNoticeRows.AsNoTracking().Where(r => partes.Contains(r.MigrationLoadPartId)).ToListAsync(ct);
        var reportes = await db.MigrationBureauReportRows.AsNoTracking().Where(r => partes.Contains(r.MigrationLoadPartId) && r.IsNegative).ToListAsync(ct);
        if (avisos.Count == 0 && reportes.Count == 0) return;
        var numeros = avisos.Select(a => a.SourceLoanNumber).Concat(reportes.Select(r => r.SourceLoanNumber)).Distinct().ToList();
        var cuotas = (await db.MigrationInstallmentRows.AsNoTracking().Where(r => partes.Contains(r.MigrationLoadPartId) && numeros.Contains(r.SourceLoanNumber))
                .ToListAsync(ct))
            .ToLookup(r => r.SourceLoanNumber, StringComparer.Ordinal);
        var dias = parametros is null ? null : await parametros.LeerAsync(ParametrosDeCartera.AvisoPrevioDiasMinimos, carga.CutoffDate, ct: ct);
        var minimos = dias is { IsSuccess: true } d && d.Value.Valor is { } valor ? Convert.ToInt32(valor, CultureInfo.InvariantCulture) : 0;

        var hechos = new HashSet<(string, DateOnly)>();
        foreach (var a in avisos)
        {
            if (!creditos.TryGetValue(a.SourceLoanNumber, out var credito)) continue;
            var susCuotas = cuotas[a.SourceLoanNumber].Select(x => new CuotaMigrada(x.RowNumber, x.InstallmentNumber, x.DueDate, x.Principal, x.CurrentInterest,
                x.DefaultInterest, x.Insurance, x.OtherCharges)).ToList();
            if (!CuadresDeMigracion.EpisodioVigente(a.EpisodeStartDate, susCuotas, carga.CutoffDate)) continue;
            var reporte = reportes.FirstOrDefault(r => r.SourceLoanNumber == a.SourceLoanNumber && r.EpisodeStartDate == a.EpisodeStartDate);
            db.PriorNotices.Add(new PriorNotice
            {
                LoanId = credito.Id,
                PersonId = credito.Deudor,
                EpisodeStartDate = a.EpisodeStartDate,
                RequiredCommunications = (byte)a.RequiredCommunications,
                MinimumDays = minimos,
                MigrationLoadId = carga.Id,
                MigratedFirstSentOn = a.FirstSentOn,
                MigratedSecondSentOn = a.SecondSentOn,
                MigratedEvidenceReference = Unir(a.FirstReference, a.SecondReference, 200),
                MigratedReportedNegativeSince = reporte is null ? null : new DateOnly(reporte.LastReportedYear, reporte.LastReportedMonth, 1),
                MigratedBureauCode = reporte?.BureauCode,
            });
            hechos.Add((a.SourceLoanNumber, a.EpisodeStartDate));
            conteo.AvisosPrevios++;
        }

        // Un reporte negativo vigente sin aviso migrado de su episodio: nace el registro del reporte, sin comunicaciones (FR-123).
        foreach (var r in reportes.Where(r => r.EpisodeStartDate is { } e && !hechos.Contains((r.SourceLoanNumber, e))))
        {
            if (!creditos.TryGetValue(r.SourceLoanNumber, out var credito)) continue;
            db.PriorNotices.Add(new PriorNotice
            {
                LoanId = credito.Id,
                PersonId = credito.Deudor,
                EpisodeStartDate = r.EpisodeStartDate!.Value,
                RequiredCommunications = 1,
                MinimumDays = minimos,
                MigrationLoadId = carga.Id,
                MigratedReportedNegativeSince = new DateOnly(r.LastReportedYear, r.LastReportedMonth, 1),
                MigratedBureauCode = r.BureauCode,
            });
            hechos.Add((r.SourceLoanNumber, r.EpisodeStartDate!.Value));
            conteo.AvisosPrevios++;
        }
    }

    // ---------------------------------------------------------------------------------------------------------- reclamos --

    private async Task ReclamosAsync(List<int> partes, MigrationLoad carga, IReadOnlyDictionary<string, (int Id, int Deudor, int Linea, LoanStatus Estado)> creditos,
        ConteoDeActivacion conteo, CancellationToken ct)
    {
        var filas = await db.MigrationClaimRows.AsNoTracking().Where(r => partes.Contains(r.MigrationLoadPartId)).OrderBy(r => r.RowNumber).ToListAsync(ct);
        foreach (var f in filas)
        {
            if (!creditos.TryGetValue(f.SourceLoanNumber, out var credito)) continue;
            var (vence, prorroga) = await PlazosAsync(f.ReceivedOn, f.IsExtended, ct);
            db.CreditReportClaims.Add(new CreditReportClaim
            {
                PersonId = f.PersonId ?? credito.Deudor,
                LoanId = credito.Id,
                Kind = f.Kind,
                Status = CreditReportClaimStatus.InProcess,
                BureauCode = f.BureauCode,
                ReceivedOn = f.ReceivedOn,
                Channel = CanalDelReclamoMigrado,
                Description = string.IsNullOrWhiteSpace(f.Description) ? "Reclamo en trámite migrado." : f.Description.Trim(),
                DueDate = vence,
                ExtendedDueDate = prorroga,
                MigrationLoadId = carga.Id,
            });
            conteo.Reclamos++;
        }
    }

    /// <summary>El vencimiento legal del reclamo (y su prórroga si se prorrogó) con los límites del titular; sin ellos, la fecha de radicación.</summary>
    private async Task<(DateOnly Vence, DateOnly? Prorroga)> PlazosAsync(DateOnly radicado, bool prorrogado, CancellationToken ct)
    {
        if (valores is null || calendario is null) return (radicado, null);
        var plazos = await LecturaDePlazos.LeerAsync(valores, calendario, radicado, ct);
        if (plazos.IsFailure) return (radicado, null);
        var vence = PlazosDelTitular.Vencimiento(DataSubjectRequestKind.Rectification, radicado, plazos.Value.Limites, plazos.Value.Calendario);
        return (vence, prorrogado
            ? PlazosDelTitular.Prorroga(DataSubjectRequestKind.Rectification, vence, yaProrrogada: false, plazos.Value.Limites, plazos.Value.Calendario)
            : null);
    }

    // ------------------------------------------------------------------------------------------------------- vinculados --

    private async Task VinculadosAsync(List<int> partes, ConteoDeActivacion conteo, CancellationToken ct)
    {
        var filas = await db.MigrationRelatedPartyRows.AsNoTracking().Where(r => partes.Contains(r.MigrationLoadPartId)).OrderBy(r => r.RowNumber).ToListAsync(ct);
        if (filas.Count == 0) return;
        var personas = filas.Select(f => f.PersonId).Distinct().ToList();
        var aMano = await db.RelatedParties.AsNoTracking().Where(r => personas.Contains(r.PersonId) && r.Source != RelatedPartySource.Migration && !r.IsDeleted)
            .Select(r => new { r.PersonId, r.Basis }).ToListAsync(ct);
        foreach (var f in filas)
        {
            if (aMano.Any(r => r.PersonId == f.PersonId && r.Basis == f.Basis))
            {
                Prevalece(conteo, "RelatedParty");
                continue;
            }
            db.RelatedParties.Add(new RelatedParty
            {
                PersonId = f.PersonId,
                Basis = f.Basis,
                RelatedThroughPersonId = f.RelatedToPersonId,
                KinshipKind = f.KinshipKind,
                KinshipDegree = f.KinshipDegree is { } g and > 0 and <= byte.MaxValue ? (byte)g : null,
                ContributionPercentage = f.ContributionPercentage,
                ValidFrom = f.ValidFrom,
                ValidTo = f.ValidTo,
                Source = RelatedPartySource.Migration,
                Notes = f.SupportReference,
            });
            conteo.Vinculados++;
        }
    }

    private static void Prevalece(ConteoDeActivacion conteo, string concepto) =>
        conteo.Prevalecio[concepto] = conteo.Prevalecio.GetValueOrDefault(concepto) + 1;

    private static string? Unir(string? a, string? b, int largo)
    {
        var texto = string.Join(" · ", new[] { a, b }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return texto.Length == 0 ? null : texto.Length <= largo ? texto : texto[..largo];
    }
}
