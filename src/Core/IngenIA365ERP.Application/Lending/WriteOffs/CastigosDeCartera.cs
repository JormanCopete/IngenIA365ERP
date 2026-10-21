using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Application.Lending.Novelties;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Lending.Risk;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.WriteOffs;

/// <summary>Un crédito verificado para el castigo a una fecha: la verificación, los saldos que se castigarían, el deterioro del último cierre y la mora. (nuevo)</summary>
public sealed record CreditoVerificadoParaCastigo(
    Loan Credito,
    VerificacionDeCastigo Verificacion,
    decimal Capital,
    decimal Intereses,
    decimal Otros,
    decimal Deterioro,
    int DiasDeMora,
    Guid? Informe,
    Guid? Concepto,
    CreditoCargado Cargado,
    EstadoDelCredito Estado);

/// <summary>
/// Lo común del castigo (feature 013, K3, US14, T0906): el expediente, los prefijos, la lectura del lote en el alcance (todos sus créditos en las oficinas
/// y líneas de quien pide; si alguno queda fuera, el mismo 404), la política vigente (<c>Castigo.*</c>, T0910), la <b>verificación</b> de los requisitos de
/// cada crédito por <see cref="RequisitosDeCastigo"/> —la condición de asociado del maestro de personas, el cruce de los aportes por
/// <see cref="IConsultaDeAportes"/> (no disponible mientras rija D19: lo acredita un soporte), los soportes <c>LendingWriteOffSupport</c> del crédito, el
/// deterioro individual del último cierre frente al saldo de hoy, la altura de mora de la reconstrucción y, sin registro de gestiones hasta K4, el
/// informe motivado—, lo que sale contra el deterioro al aplicarlo y la respuesta. (nuevo)
/// </summary>
public sealed class CastigosDeCartera(
    IApplicationDbContext db,
    CargadorDeReconstruccion cargador,
    IConsultaDeAportes aportes,
    ReglasDeParametrosDeCartera parametros)
{
    /// <summary>El tipo de expediente de un lote en <c>LND_CaseParticipations</c> (segregación <c>ArmarLoteDeCastigo</c> × <c>AplicarCastigo</c>).</summary>
    public const string Expediente = "WriteOffBatch";

    /// <summary>El prefijo del número del lote.</summary>
    public const string Prefijo = "LCA-";

    /// <summary>El prefijo del comprobante de la operación <c>WriteOff</c> de cada crédito.</summary>
    public const string PrefijoDeOperacion = "CAS-";

    /// <summary>Los conceptos del activo que salen al castigar (el suspenso va aparte, a control; los pasivos con el deudor no se castigan).</summary>
    public static readonly LoanConcept[] DelActivo =
    [
        LoanConcept.Principal, LoanConcept.CurrentInterest, LoanConcept.DefaultInterest, LoanConcept.Insurance, LoanConcept.CollectionFee,
        LoanConcept.OtherCharge,
    ];

    public static readonly LoanConcept[] DeSuspenso = [LoanConcept.CurrentInterestInSuspense, LoanConcept.DefaultInterestInSuspense];

    // --------------------------------------------------------------------------------------------------------- lectura --

    /// <summary>
    /// El lote y sus ítems si existe y todos sus créditos están en el alcance; si no, <c>Lending.WriteOffBatch.NotFound</c> (el mismo, con el evento
    /// <c>Lending.Access.OutOfScope</c> si es el alcance).
    /// </summary>
    public static async Task<Result<(WriteOffBatch Lote, IReadOnlyList<WriteOffBatchItem> Items)>> LeerAsync(IApplicationDbContext db,
        IAlcanceDeCartera alcance, LendingAuditEmitter auditoria, Guid publicId, bool seguir, CancellationToken ct)
    {
        var lote = seguir
            ? await db.WriteOffBatches.FirstOrDefaultAsync(b => b.PublicId == publicId && !b.IsDeleted, ct)
            : await db.WriteOffBatches.AsNoTracking().FirstOrDefaultAsync(b => b.PublicId == publicId && !b.IsDeleted, ct);
        if (lote is null) return Result.Failure<(WriteOffBatch, IReadOnlyList<WriteOffBatchItem>)>(ErroresDeCastigo.Inexistente());
        var consulta = db.WriteOffBatchItems.Where(i => i.WriteOffBatchId == lote.Id && !i.IsDeleted);
        var items = seguir ? await consulta.ToListAsync(ct) : await consulta.AsNoTracking().ToListAsync(ct);
        var ids = items.Select(i => i.LoanId).Distinct().ToList();
        var creditos = await db.Loans.AsNoTracking().Where(l => ids.Contains(l.Id)).Select(l => new { l.BranchId, l.CreditLineId }).ToListAsync(ct);
        foreach (var c in creditos)
        {
            var dentro = await alcance.AsegurarAsync(c.BranchId, c.CreditLineId, ErroresDeCastigo.Inexistente(), auditoria, Expediente, lote.PublicId, ct);
            if (dentro.IsFailure) return Result.Failure<(WriteOffBatch, IReadOnlyList<WriteOffBatchItem>)>(dentro.Error);
        }
        return Result.Success<(WriteOffBatch, IReadOnlyList<WriteOffBatchItem>)>((lote, items));
    }

    /// <summary>El consejo de administración activo (el único que aprueba el castigo, FR-095), o nulo.</summary>
    public static Task<int?> ConsejoAsync(IApplicationDbContext db, CancellationToken ct) =>
        db.GoverningBodies.AsNoTracking().Where(b => b.Kind == GoverningBodyKind.BoardOfDirectors && b.IsActive && !b.IsDeleted)
            .OrderBy(b => b.Id).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct);

    /// <summary>
    /// Los lotes sin aplicar ni anular (borrador, enviado o aprobado) que ya tienen incluido alguno de <paramref name="loanIds"/>, fuera de
    /// <paramref name="salvoLote"/>: crédito → número del lote.
    /// </summary>
    public static async Task<IReadOnlyDictionary<int, string>> EnLotesAbiertosAsync(IApplicationDbContext db, IReadOnlyCollection<int> loanIds, int? salvoLote,
        CancellationToken ct)
    {
        var ids = loanIds.ToList();
        var abiertos = new[] { WriteOffBatchStatus.Draft, WriteOffBatchStatus.Submitted, WriteOffBatchStatus.Approved };
        return (await (from i in db.WriteOffBatchItems.AsNoTracking()
                       join b in db.WriteOffBatches.AsNoTracking() on i.WriteOffBatchId equals b.Id
                       where ids.Contains(i.LoanId) && !i.IsDeleted && !b.IsDeleted && i.Status == WriteOffItemStatus.Included
                             && abiertos.Contains(b.Status) && (salvoLote == null || b.Id != salvoLote)
                       select new { i.LoanId, b.Number }).ToListAsync(ct))
            .GroupBy(x => x.LoanId).ToDictionary(g => g.Key, g => g.First().Number);
    }

    // --------------------------------------------------------------------------------------------------------- política --

    /// <summary>
    /// La política de castigo vigente a <paramref name="fecha"/> (T0910). Sin registro de gestiones (antes de K4), el informe motivado las acredita; desde K4
    /// (T0945) se cuentan las gestiones registradas del crédito contra <c>Castigo.GestionesMinimas</c>.
    /// </summary>
    public async Task<PoliticaDeCastigo> PoliticaAsync(DateOnly fecha, CancellationToken ct)
    {
        var deterioro = await parametros.LeerAsync(ParametrosDeCartera.CastigoExigeDeterioroTotal, fecha, ct: ct);
        var dias = await parametros.LeerAsync(ParametrosDeCartera.CastigoDiasMinimosDeMora, fecha, ct: ct);
        var gestiones = await parametros.LeerAsync(ParametrosDeCartera.CastigoGestionesMinimas, fecha, ct: ct);
        var exige = deterioro.IsFailure || deterioro.Value.Valor is not bool b ? !string.Equals(deterioro.IsSuccess ? deterioro.Value.Texto?.Trim() : null,
            "false", StringComparison.OrdinalIgnoreCase) : b;
        return new PoliticaDeCastigo(exige, Entero(dias), Entero(gestiones) ?? 0) { GestionesRegistradas = CatalogoDeCartera.EntregaVigente >= EntregaDeCartera.K4 };
    }

    private static int? Entero(Result<ParametroDeCartera> leido) =>
        leido.IsSuccess && int.TryParse(leido.Value.Texto?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;

    // ------------------------------------------------------------------------------------------------------ verificación --

    /// <summary>
    /// Verifica los requisitos de cada crédito a <paramref name="fecha"/> con sus soportes (T0906): la condición de asociado del deudor (maestro de
    /// personas), el cruce de aportes (consulta de Aportes; si no está disponible, D19, el soporte del cruce), el informe motivado y el concepto de
    /// irrecuperabilidad (adjuntos <c>LendingWriteOffSupport</c> del crédito, disponibles), el deterioro individual del último cierre frente al saldo del
    /// activo de hoy, la altura de mora de la reconstrucción y la política vigente. Falla sólo si un crédito no se puede reconstruir.
    /// </summary>
    public async Task<Result<IReadOnlyList<CreditoVerificadoParaCastigo>>> VerificarAsync(IReadOnlyList<Loan> creditos,
        IReadOnlyDictionary<Guid, WriteOffSupportInput> soportes, DateOnly fecha, CancellationToken ct)
    {
        var politica = await PoliticaAsync(fecha, ct);
        var ids = creditos.Select(c => c.Id).ToList();
        var cargados = await cargador.CargarAsync(ids, null, fecha, ct);
        var deudores = creditos.Select(c => c.DebtorPersonId).Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => deudores.Contains(p.Id))
            .Select(p => new { p.Id, p.PublicId, p.IsAssociate }).ToDictionaryAsync(p => p.Id, ct);
        var consulta = await aportes.SaldosAsync(new SolicitudDeSaldoDeAportes(personas.Values.Select(p => p.PublicId).ToList(), fecha), ct);

        var adjuntos = soportes.Values.SelectMany(s => new[] { s.IrrecoverabilityAttachmentPublicId, s.ReportAttachmentPublicId, s.ContributionsOffsetAttachmentPublicId })
            .Where(a => a is not null).Select(a => a!.Value).Distinct().ToList();
        var validos = (await db.Attachments.AsNoTracking()
                .Where(a => adjuntos.Contains(a.PublicId) && a.OwnerEntityType == AdjuntosDeModulo.SoporteDeCastigo && a.Status == EstadoDeAdjunto.Available)
                .Select(a => new { a.PublicId, a.OwnerEntityPublicId }).ToListAsync(ct))
            .ToDictionary(a => a.PublicId, a => a.OwnerEntityPublicId);

        var cierre = await CategoriaContableDeCartera.UltimoCierreVigenteAsync(db, fecha.AddDays(1), ct);
        var deterioros = cierre is null
            ? new Dictionary<int, decimal>()
            : await CategoriaContableDeCartera.LineasDe(db, cierre).AsNoTracking().Where(l => ids.Contains(l.LoanId))
                .GroupBy(l => l.LoanId).Select(g => new { LoanId = g.Key, Total = g.Sum(l => l.TotalImpairment) })
                .ToDictionaryAsync(x => x.LoanId, x => x.Total, ct);

        // K4 (T0945): las gestiones registradas hasta la fecha que nadie anuló (la que anula no cuenta como gestión).
        var hasta = DateTime.SpecifyKind(fecha.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var gestiones = !politica.GestionesRegistradas
            ? new Dictionary<int, int>()
            : await db.CollectionActions.AsNoTracking()
                .Where(a => a.LoanId != null && ids.Contains(a.LoanId.Value) && !a.IsDeleted && a.VoidsActionId == null && a.OccurredAt < hasta
                            && !db.CollectionActions.Any(x => x.VoidsActionId == a.Id))
                .GroupBy(a => a.LoanId!.Value).Select(g => new { LoanId = g.Key, Total = g.Count() }).ToDictionaryAsync(x => x.LoanId, x => x.Total, ct);

        var salida = new List<CreditoVerificadoParaCastigo>();
        foreach (var credito in creditos)
        {
            if (!cargados.TryGetValue(credito.Id, out var cargado) || !cargado.Listo)
                return Result.Failure<IReadOnlyList<CreditoVerificadoParaCastigo>>(cargado?.Error ?? Settlement.LecturaDelCredito.Inexistente());
            EstadoDelCredito estado;
            try
            {
                estado = MotorDeCartera.Reconstruir(cargado.Entrada!, fecha);
            }
            catch (ReglaDeCarteraException ex)
            {
                return Result.Failure<IReadOnlyList<CreditoVerificadoParaCastigo>>(ErroresDeGobierno.De(ex.Problema));
            }
            var soporte = soportes.GetValueOrDefault(credito.PublicId);
            Guid? DelCredito(Guid? adjunto) => adjunto is { } a && validos.TryGetValue(a, out var dueno) && dueno == credito.PublicId ? a : null;
            var persona = personas.GetValueOrDefault(credito.DebtorPersonId);
            bool? cruzados = null;
            if (consulta.Available)
                cruzados = consulta.Balances.Where(b => persona is not null && b.PersonPublicId == persona.PublicId).Sum(b => b.Contributions) <= 0m;

            var capital = estado.Saldo(LoanConcept.Principal);
            var intereses = estado.Saldo(LoanConcept.CurrentInterest) + estado.Saldo(LoanConcept.DefaultInterest);
            var otros = estado.Saldo(LoanConcept.Insurance) + estado.Saldo(LoanConcept.CollectionFee) + estado.Saldo(LoanConcept.OtherCharge);
            var deterioro = deterioros.GetValueOrDefault(credito.Id);
            var informe = DelCredito(soporte?.ReportAttachmentPublicId);
            var concepto = DelCredito(soporte?.IrrecoverabilityAttachmentPublicId);
            var verificacion = RequisitosDeCastigo.Verificar(new CreditoParaCastigar(credito.Id, persona?.IsAssociate ?? false, capital + intereses + otros,
                deterioro, estado.AlturaDeMora.Dias)
            {
                AportesCruzados = cruzados,
                SoporteDeCruceDeAportes = DelCredito(soporte?.ContributionsOffsetAttachmentPublicId),
                InformeMotivado = informe,
                ConceptoDeIrrecuperabilidad = concepto,
                Gestiones = gestiones.GetValueOrDefault(credito.Id),
            }, politica);
            salida.Add(new CreditoVerificadoParaCastigo(credito, verificacion, capital, intereses, otros, deterioro, estado.AlturaDeMora.Dias, informe, concepto,
                cargado, estado));
        }
        return Result.Success<IReadOnlyList<CreditoVerificadoParaCastigo>>(salida);
    }

    /// <summary>
    /// Los soportes con que un ítem se verificó (para volver a verificarlo al aplicar): el informe y el concepto de sus columnas y el soporte del cruce de
    /// aportes de la evidencia guardada en <c>RequirementsJson</c>.
    /// </summary>
    public static WriteOffSupportInput SoportesDe(WriteOffBatchItem item, Guid loanPublicId)
    {
        Guid? cruce = null;
        foreach (var r in Requisitos(item.RequirementsJson).Where(r => r.Code == RequisitosDeCastigo.AportesCruzados && r.Met && r.Evidence is not null))
        {
            var palabras = r.Evidence!.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in palabras)
                if (Guid.TryParse(p.Trim('(', ')', '.', ','), out var g)) cruce = g;
        }
        return new WriteOffSupportInput(loanPublicId, item.IrrecoverabilityAttachmentPublicId, item.ReportAttachmentPublicId, cruce);
    }

    /// <summary>Los requisitos guardados en <c>RequirementsJson</c> (la huella canónica de <see cref="RequisitosDeCastigo"/>).</summary>
    public static IReadOnlyList<WriteOffRequirementDto> Requisitos(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("requirements", out var lista) || lista.ValueKind != JsonValueKind.Array) return [];
            return lista.EnumerateArray().Select(r => new WriteOffRequirementDto(
                r.TryGetProperty("key", out var k) ? k.GetString() ?? string.Empty : string.Empty,
                r.TryGetProperty("source", out var s) ? Origen(s) : string.Empty,
                r.TryGetProperty("met", out var m) && m.ValueKind == JsonValueKind.True,
                r.TryGetProperty("evidence", out var e) ? e.GetString() : null)).ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>El origen del requisito por su nombre (<c>Norma</c>, <c>Politica</c>): la huella lo guarda por número.</summary>
    private static string Origen(JsonElement s) =>
        s.ValueKind == JsonValueKind.Number && s.TryGetInt32(out var n) && Enum.IsDefined(typeof(OrigenDeRequisitoDeCastigo), n)
            ? ((OrigenDeRequisitoDeCastigo)n).ToString()
            : s.ValueKind == JsonValueKind.String ? s.GetString() ?? string.Empty : s.ToString();

    // -------------------------------------------------------------------------------------------------------- deterioro --

    /// <summary>
    /// Lo que sale contra el deterioro individual por concepto al castigar (C22; contabilidad.md §2.1, ejemplo 14): el del último cierre vigente del
    /// crédito, repartido por concepto con <see cref="DeterioroPorConcepto"/>, menos lo que ya consumieron las condonaciones aplicadas desde ese cierre,
    /// hasta lo castigado de cada concepto. Sin cierre, nada (todo va al gasto).
    /// </summary>
    public async Task<IReadOnlyList<ConceptAmountV1>> DeterioroAplicableAsync(int loanId, IReadOnlyList<ConceptAmountV1> castigado, DateOnly v,
        CancellationToken ct)
    {
        var cierre = await CategoriaContableDeCartera.UltimoCierreVigenteAsync(db, v.AddDays(1), ct);
        if (cierre is null) return [];
        var linea = await CategoriaContableDeCartera.LineasDe(db, cierre).AsNoTracking().FirstOrDefaultAsync(l => l.LoanId == loanId, ct);
        if (linea is null) return [];
        var saldos = SaldosDelCierre(linea.BalancesByConceptJson);
        var disponible = DeterioroPorConcepto.Repartir(linea.PrincipalImpairment, linea.InterestImpairment, linea.OtherImpairment, saldos)
            .ToDictionary(x => x.Concepto, x => x.Importe);
        var condonaciones = await db.LoanOperations.AsNoTracking()
            .Where(o => o.LoanId == loanId && o.Kind == LoanOperationKind.Waiver && o.ValueDate > cierre.CloseDate && o.ReversedByOperationId == null)
            .Select(o => o.Payload).ToListAsync(ct);
        foreach (var p in condonaciones.Select(DecisionDeCondonacion.Leer).Where(d => d is not null).SelectMany(d => d!.ImpairmentApplied))
            if (Enum.TryParse<LoanConcept>(p.Concept, out var c)) disponible[c] = disponible.GetValueOrDefault(c) - p.Amount;

        return castigado.GroupBy(a => Enum.Parse<LoanConcept>(a.Concept)).OrderBy(g => (int)g.Key)
            .Select(g => new ConceptAmountV1
            {
                Concept = g.Key.ToString(), Amount = Math.Max(0m, Math.Min(g.Sum(a => a.Amount), disponible.GetValueOrDefault(g.Key))),
            })
            .Where(a => a.Amount > 0m).ToList();
    }

    private static IReadOnlyDictionary<LoanConcept, decimal> SaldosDelCierre(string json)
    {
        try
        {
            var crudo = JsonSerializer.Deserialize<Dictionary<string, decimal>>(json) ?? [];
            return crudo.Where(kv => Enum.TryParse<LoanConcept>(kv.Key, out _)).ToDictionary(kv => Enum.Parse<LoanConcept>(kv.Key), kv => kv.Value);
        }
        catch (JsonException)
        {
            return new Dictionary<LoanConcept, decimal>();
        }
    }

    // --------------------------------------------------------------------------------------------------------- respuesta --

    /// <summary>La respuesta de un lote con sus ítems: los incluidos (o castigados) con sus requisitos y los rechazados con lo que les falta.</summary>
    public async Task<WriteOffBatchDto> DtoAsync(WriteOffBatch lote, IReadOnlyList<WriteOffBatchItem> items, CancellationToken ct)
    {
        var ids = items.Select(i => i.LoanId).Distinct().ToList();
        var creditos = await db.Loans.AsNoTracking().IgnoreQueryFilters().Where(l => ids.Contains(l.Id)).ToListAsync(ct);
        var (personas, _, _) = await ListLoansQueryHandler.ReferenciasAsync(db, creditos, ct);
        var porId = creditos.ToDictionary(c => c.Id);
        var mora = (await db.LoanDailyPositions.AsNoTracking().Where(p => ids.Contains(p.LoanId) && p.PositionDate <= lote.CutoffDate)
                .Select(p => new { p.LoanId, p.PositionDate, p.Version, p.DaysPastDue }).ToListAsync(ct))
            .GroupBy(p => p.LoanId).ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.PositionDate).ThenByDescending(p => p.Version).First().DaysPastDue);
        var operaciones = items.Where(i => i.WriteOffOperationId is not null).Select(i => i.WriteOffOperationId!.Value).ToList();
        var deOperacion = await db.LoanOperations.AsNoTracking().Where(o => operaciones.Contains(o.Id)).ToDictionaryAsync(o => o.Id, o => o.PublicId, ct);
        var usuarios = await ReferenciasDeGobierno.UsuariosAsync(db, new[] { lote.PreparedByUserId, lote.AppliedByUserId ?? 0 }.Where(u => u > 0), ct);
        WriteOffSessionItemDto? punto = null;
        if (lote.SessionItemId is { } puntoId)
            punto = await (from i in db.SessionItems.AsNoTracking()
                           join s in db.LendingSessions.AsNoTracking() on i.SessionId equals s.Id
                           where i.Id == puntoId
                           select new WriteOffSessionItemDto(i.PublicId, s.PublicId, s.Number, i.Order, i.Outcome)).FirstOrDefaultAsync(ct);

        RefDto Deudor(int loanId) => personas.TryGetValue(porId[loanId].DebtorPersonId, out var p) ? p : new RefDto(Guid.Empty, string.Empty);
        var incluidos = items.Where(i => i.Status != WriteOffItemStatus.Rejected).OrderBy(i => porId[i.LoanId].Number, StringComparer.Ordinal)
            .Select(i => new WriteOffBatchItemDto(i.PublicId, porId[i.LoanId].PublicId, porId[i.LoanId].Number, Deudor(i.LoanId), i.Status,
                i.PrincipalAmount + i.InterestAmount + i.OtherAmount, i.PrincipalAmount, i.InterestAmount, i.OtherAmount, i.ImpairmentAtCutoff,
                mora.TryGetValue(i.LoanId, out var d) ? d : null, Requisitos(i.RequirementsJson), i.ReportAttachmentPublicId,
                i.IrrecoverabilityAttachmentPublicId, i.WriteOffOperationId is { } op ? deOperacion.GetValueOrDefault(op) : null))
            .ToList();
        var rechazados = items.Where(i => i.Status == WriteOffItemStatus.Rejected).OrderBy(i => porId[i.LoanId].Number, StringComparer.Ordinal)
            .Select(i =>
            {
                var requisitos = Requisitos(i.RequirementsJson);
                return new WriteOffRejectedDto(i.PublicId, porId[i.LoanId].PublicId, porId[i.LoanId].Number, Deudor(i.LoanId),
                    requisitos.Where(r => !r.Met).Select(r => r.Code).ToList(), i.RejectionReason, requisitos);
            })
            .ToList();
        return new WriteOffBatchDto(lote.PublicId, lote.Number, lote.Status, lote.CutoffDate, lote.Reason,
            usuarios.GetValueOrDefault(lote.PreparedByUserId)?.Name ?? string.Empty, lote.PreparedAt, lote.LoanCount, lote.TotalPrincipal, lote.TotalInterest,
            lote.TotalOther, lote.TotalPrincipal + lote.TotalInterest + lote.TotalOther, punto,
            lote.AppliedByUserId is { } aplico ? usuarios.GetValueOrDefault(aplico)?.Name : null, lote.AppliedAt, lote.SupervisorReportRequired,
            lote.SupervisorReportDueDate, lote.SupervisorReportedAt, lote.SupervisorReportReference, lote.SupervisorReportAttachmentPublicId, incluidos,
            rechazados);
    }
}
