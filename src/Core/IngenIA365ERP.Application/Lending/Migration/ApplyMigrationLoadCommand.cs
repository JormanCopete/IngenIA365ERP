using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Risk;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;
using MediatR;
using Microsoft.EntityFrameworkCore;
using P = IngenIA365ERP.Application.Lending.Imports.DefinicionesDeMigracion.Partes;
using C = IngenIA365ERP.Application.Lending.Imports.DefinicionesDeMigracion.Columnas;

namespace IngenIA365ERP.Application.Lending.Migration;

/// <summary>Lo que responde aplicar una carga (api.md §19.1; plantillas.md §5.5). (nuevo)</summary>
public sealed record AppliedMigrationLoadDto(
    Guid PublicId,
    MigrationLoadStatus Status,
    DateOnly CutoffDate,
    DateOnly ActivationDeadline,
    BalanceCheckSummaryDto BalanceCheck,
    SalesReconciliationSummaryDto SalesReconciliation,
    Guid? ReplacedLoadPublicId,
    Guid AppliedMessagePublicId,
    IReadOnlyDictionary<string, int>? Counts = null,
    IReadOnlyList<LoPrevalecidoDto>? ManualPrevails = null,
    IReadOnlyList<ErrorEntrePartes>? Warnings = null,
    int RevalidatedLimits = 0);

/// <summary>El resumen del cuadre de la carga (FR-141). (nuevo)</summary>
public sealed record BalanceCheckSummaryDto(int CheckVersion, bool IsBalanced, int Differences);

/// <summary>El resumen de la conciliación de ventas de la carga (FR-143). (nuevo)</summary>
public sealed record SalesReconciliationSummaryDto(bool SalesReconciled, int Differences, int Migrated = 0, int CancelledBeforeCutoff = 0,
    int NotMigratedWithBalance = 0, int AcceptedOutsideLending = 0);

/// <summary>Lo registrado a mano antes de activar que prevalece sobre la carga para la misma persona y concepto (plantillas.md §5.4.9). (nuevo)</summary>
public sealed record LoPrevalecidoDto(Guid PersonPublicId, string Concept, string Detail);

/// <summary>
/// Aplica una carga de migración (feature 013, US8, T0743; T0232, C107; FR-142; api.md §19.1, <c>POST /api/lending/migration/loads/{id}/apply</c>;
/// plantillas.md §5.4; data-model §29.1). <b>Aplicar sólo guarda la carga</b>: nada nace en el libro hasta la activación. (nuevo)
/// </summary>
public sealed record ApplyMigrationLoadCommand(Guid MigrationLoadPublicId, string Reason)
    : IRequest<Result<AppliedMigrationLoadDto>>, IOperacionIdempotente, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ApplyMigrationLoadCommandValidator : ValidadorConMotivo<ApplyMigrationLoadCommand>
{
    public ApplyMigrationLoadCommandValidator() => RuleFor(x => x.MigrationLoadPublicId).NotEmpty();
}

/// <summary>
/// Todo o nada en una <see cref="TransaccionExplicita"/>:
/// <list type="number">
/// <item>Cartera ya activada, <c>Lending.Activation.AlreadyActivated</c>; carga que no es borrador, <c>Lending.Migration.LoadNotDraft</c>.</item>
/// <item>Una carga <b>sin ninguna parte</b> sigue siendo la activación sin cartera previa de C107 (conteos en cero, cuadrada y conciliada).</item>
/// <item>Las partes obligatorias (<c>loans</c>, <c>installments</c>, <c>balances</c>, <c>risk-data</c>) tienen filas
/// (<c>Lending.Migration.PartMissing</c> con <c>data.parts</c>).</item>
/// <item>Las validaciones entre partes de §5.4 (<see cref="ValidacionEntrePartes"/>): referencias, cada crédito con cuotas, saldos y riesgo, los
/// cuadres de <see cref="CuadresDeMigracion"/>, ventas y créditos uno a uno, número en <c>loans</c> y <c>charged-off</c> a la vez, cupos que se
/// cruzan, personas dadas de baja; con un error, 422 <c>Lending.Migration.CrossPartErrors</c> (<c>data.errors[] { part, batch, row, column,
/// message }</c>) y la carga sigue <c>Draft</c>. El historial de mora de los segmentos cubiertos de una cooperativa obligada (por el selector de
/// modelo de deterioro y <c>ReglaDePerdidaEsperada</c>) completo, o <c>Lending.Migration.ArrearsHistoryRequired</c>.</item>
/// <item>Con todo: la <c>Applied</c> anterior pasa a <c>Replaced</c> (<c>ReplacedByLoadId</c>), la carga queda <c>Applied</c> con quién y cuándo,
/// <c>ActivationDeadline</c> = corte + <c>Pagos.DiasMaximosFechaValor</c>, conteos, totales y <c>ValidationSummaryJson</c> con los avisos y
/// <c>manualPrevails</c>; el cuadre (<see cref="CuadreDeMigracion"/>) y la conciliación de ventas (<see cref="ConciliacionDeVentasDeMigracion"/>)
/// de la versión 1 con sus huellas; al reemplazar, la revalidación de los cupos otorgados a mano (<see cref="RevalidacionDeCuposMigrados"/>); y
/// <c>CargaDeCarteraAplicada</c> por <see cref="EmisionDeCartera"/> en el mismo guardado.</item>
/// </list>
/// </summary>
public sealed class ApplyMigrationLoadCommandHandler(
    IApplicationDbContext db,
    EmisionDeCartera emision,
    ILectorDeParametros parametros,
    IActorActual actorActual,
    IDateTimeService reloj,
    CuadreDeMigracion? cuadre = null,
    ConciliacionDeVentasDeMigracion? conciliacion = null,
    INormativaVigente? normativa = null,
    IPerfilRegulatorio? perfiles = null,
    RevalidacionDeCuposMigrados? revalidacion = null) : IRequestHandler<ApplyMigrationLoadCommand, Result<AppliedMigrationLoadDto>>
{
    /// <summary>La huella de una instantánea vacía (<c>[]</c>): el cuadre y la conciliación de una carga sin partes.</summary>
    public static readonly string HuellaVacia = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("[]")));

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Las partes que una carga con partes tiene que traer (plantillas.md §5.2).</summary>
    public static IReadOnlyList<string> Obligatorias { get; } = [P.Loans, P.Installments, P.Balances, P.RiskData];

    public async Task<Result<AppliedMigrationLoadDto>> Handle(ApplyMigrationLoadCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<AppliedMigrationLoadDto>(ErroresDeActivacion.SinUsuario());

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var setup = await db.LendingSetups.AsNoTracking().FirstOrDefaultAsync(s => s.Id == LendingSetup.IdDeLaFilaUnica, ct);
            if (setup?.ActivationDate is { } activada) return Result.Failure<AppliedMigrationLoadDto>(ErroresDeActivacion.YaActivada(activada));

            var carga = await db.MigrationLoads.FirstOrDefaultAsync(l => l.PublicId == request.MigrationLoadPublicId && !l.IsDeleted, ct);
            if (carga is null) return Result.Failure<AppliedMigrationLoadDto>(ErroresDeActivacion.CargaNoEncontrada(request.MigrationLoadPublicId));
            if (!carga.EsBorrador) return Result.Failure<AppliedMigrationLoadDto>(ErroresDeActivacion.CargaNoBorrador());

            var tandas = await db.MigrationLoadParts.AsNoTracking()
                .Where(p => p.MigrationLoadId == carga.Id && p.Status == MigrationPartStatus.Imported && !p.IsDeleted).ToListAsync(ct);
            var validacion = ValidacionEntrePartes.Vacia;
            if (tandas.Count > 0)
            {
                var faltan = Obligatorias.Where(o => !tandas.Any(t => t.PartKey == o && t.RowCount > 0)).ToList();
                if (faltan.Count > 0)
                    return Result.Failure<AppliedMigrationLoadDto>(new ErrorConDatos(ErroresDeMigracion.CodigoParteFaltante,
                        ErroresDeMigracion.ParteFaltante(string.Join(", ", faltan)), new { parts = faltan }));

                validacion = await ValidacionEntrePartes.ValidarAsync(db, carga, tandas, ct);
                if (validacion.Errores.Count > 0)
                    return Result.Failure<AppliedMigrationLoadDto>(ErroresDeMigracion.ErroresEntrePartes(validacion.Errores));

                var historial = await HistorialFaltanteAsync(carga, validacion, ct);
                if (historial.Count > 0)
                    return Result.Failure<AppliedMigrationLoadDto>(new ErrorConDatos(ErroresDeMigracion.CodigoHistorialRequerido,
                        ErroresDeMigracion.HistorialRequerido(historial.Count),
                        new { loans = historial.Select(h => new { loanNumber = h.Numero, months = h.Meses }).ToList() }));
            }

            var hoy = reloj.HoyLocal;
            var dias = await parametros.LeerAsync(ParametrosDeCartera.Modulo, ParametrosDeCartera.PagosDiasMaximosFechaValor, hoy, ct: ct);
            if (dias.IsFailure) return Result.Failure<AppliedMigrationLoadDto>(dias.Error);
            var plazo = carga.CutoffDate.AddDays(int.Parse(dias.Value.Texto, CultureInfo.InvariantCulture));
            var ahora = reloj.UtcNow;

            // Hay a lo sumo una aplicada (índice único filtrado): la anterior queda reemplazada (sirve de ensayo) y se guarda antes.
            var anterior = await db.MigrationLoads.FirstOrDefaultAsync(l => l.Status == MigrationLoadStatus.Applied && !l.IsDeleted, ct);
            if (anterior is not null)
            {
                anterior.Reemplazar(carga.Id, ahora);
                await db.SaveChangesAsync(ct);
            }

            var resumen = JsonSerializer.Serialize(new { errors = Array.Empty<object>(), warnings = validacion.Avisos, manualPrevails = validacion.Prevalece }, Json);
            carga.Aplicar(usuario, ahora, plazo, validacion.Creditos, validacion.Castigados, validacion.Personas,
                JsonSerializer.Serialize(validacion.Totales, Json), resumen);
            const int version = 1;

            var calculado = cuadre is null || tandas.Count == 0 ? CuadreDeMigracion.Vacio() : null;
            if (calculado is null)
            {
                var r = await cuadre!.CalcularAsync(carga, ct);
                if (r.IsFailure) return Result.Failure<AppliedMigrationLoadDto>(r.Error);
                calculado = r.Value;
            }
            var diferenciasDelCuadre = cuadre?.Registrar(carga, version, calculado) ?? [];
            carga.SellarCuadre(version, calculado.InstantaneaJson, calculado.Huella, diferenciasDelCuadre.Count == 0);

            ConciliacionRegistrada? ventas = null;
            if (conciliacion is not null && tandas.Count > 0)
            {
                var r = await conciliacion.ConciliarAsync(carga, version, ct);
                if (r.IsFailure) return Result.Failure<AppliedMigrationLoadDto>(r.Error);
                ventas = r.Value;
            }
            var diferenciasDeVentas = ventas is null ? 0 : ventas.Conteos.NoMigradasConSaldo + ventas.Conteos.FueraDeCartera;
            carga.SellarConciliacion(ventas?.Huella ?? HuellaVacia, diferenciasDeVentas == 0);

            var revalidados = anterior is not null && revalidacion is not null && tandas.Count > 0
                ? await revalidacion.RevalidarAsync(carga, ct) : 0;

            var diferencias = diferenciasDelCuadre.Count + diferenciasDeVentas;
            var mensaje = await emision.CargaAplicadaAsync(carga, anterior?.PublicId, hoy, diferencias, ct, validacion.Capital);
            if (mensaje.IsFailure) return Result.Failure<AppliedMigrationLoadDto>(mensaje.Error);

            await db.SaveChangesAsync(ct);
            return Result.Success(new AppliedMigrationLoadDto(carga.PublicId, carga.Status, carga.CutoffDate, plazo,
                new BalanceCheckSummaryDto(carga.CurrentCheckVersion, carga.IsBalanced, diferenciasDelCuadre.Count),
                new SalesReconciliationSummaryDto(carga.SalesReconciled, diferenciasDeVentas, ventas?.Conteos.Migradas ?? 0,
                    ventas?.Conteos.CanceladasAntesDelCorte ?? 0, ventas?.Conteos.NoMigradasConSaldo ?? 0, ventas?.Conteos.FueraDeCartera ?? 0),
                anterior?.PublicId, mensaje.Value.PublicId, validacion.Conteos, validacion.Prevalece, validacion.Avisos, revalidados));
        }, ct);
    }

    /// <summary>
    /// Los créditos de los segmentos cubiertos de una cooperativa <b>obligada</b> a la pérdida esperada a la fecha de activación (corte + 1) cuyo
    /// historial de mora no trae los meses que pide la regla (desde el mes del desembolso o desde los meses de la regla antes del corte, hasta el
    /// mes anterior al del corte). Sin normativa o sin perfil no hay obligación que mirar.
    /// </summary>
    private async Task<IReadOnlyList<(string Numero, IReadOnlyList<string> Meses)>> HistorialFaltanteAsync(MigrationLoad carga, ValidacionEntrePartes v,
        CancellationToken ct)
    {
        if (normativa is null || perfiles is null || v.Creditos == 0) return [];
        var fecha = carga.CutoffDate.AddDays(1);
        var regla = await normativa.LeerAsync(RegulatoryDocumentKind.ReglaDePerdidaEsperada, fecha, ReglaDePerdidaEsperada.Leer, ct: ct);
        if (regla.IsFailure) return [];
        var perfil = await perfiles.VigenteAsync(fecha, null, ct);
        var seleccion = SelectorDeModeloDeDeterioro.Seleccionar(regla.Value.Documento,
            perfil is null ? null : new PerfilParaModelo(perfil.OrganizationTypeCode, perfil.EmployeeFundCategory), null, IngenIA365ERP.Domain.Lending.CatalogoDeCartera.EntregaVigente);
        if (!seleccion.Obligada) return [];
        var catalogo = await normativa.CatalogoDeModalidadesAsync(fecha, null, ct);
        if (catalogo.IsFailure) return [];

        var meses = regla.Value.Documento.MesesDeHistorial;
        var mesDelCorte = new DateOnly(carga.CutoffDate.Year, carga.CutoffDate.Month, 1);
        var faltantes = new List<(string, IReadOnlyList<string>)>();
        foreach (var credito in v.CreditosParaHistorial)
        {
            var segmento = catalogo.Value.Documento.Buscar(credito.Modalidad ?? string.Empty, credito.Subsegmento)?.ExpectedLossSegment;
            if (segmento is null || !regla.Value.Documento.SegmentosCubiertos.Contains(segmento, StringComparer.Ordinal)) continue;
            var desde = new DateOnly(credito.Desembolso.Year, credito.Desembolso.Month, 1);
            if (desde < mesDelCorte.AddMonths(-meses)) desde = mesDelCorte.AddMonths(-meses);
            var faltan = new List<string>();
            for (var m = desde; m < mesDelCorte; m = m.AddMonths(1))
                if (!credito.Meses.Contains((m.Year, m.Month))) faltan.Add(string.Create(CultureInfo.InvariantCulture, $"{m.Year:0000}-{m.Month:00}"));
            if (faltan.Count > 0) faltantes.Add((credito.Numero, faltan));
        }
        return faltantes;
    }
}

/// <summary>Un crédito de la carga con lo que pide la completitud del historial. (nuevo)</summary>
public sealed record CreditoParaHistorialMigrado(string Numero, string? Modalidad, string? Subsegmento, DateOnly Desembolso, IReadOnlySet<(int, int)> Meses);

/// <summary>
/// Las validaciones entre partes de la carga (feature 013, US8, T0743; plantillas.md §5.4): lee una vez las tandas vivas de las partes y responde
/// los errores (parte, tanda, fila, columna), los avisos (<c>Lending.Migration.ArrearsMismatch</c>, <c>.EpisodeNotCurrent</c>), lo registrado a mano
/// que prevalece, los conteos y los totales. No escribe. (nuevo)
/// </summary>
public sealed class ValidacionEntrePartes
{
    private ValidacionEntrePartes() { }

    public static ValidacionEntrePartes Vacia { get; } = new();

    public List<ErrorEntrePartes> Errores { get; } = [];

    public List<ErrorEntrePartes> Avisos { get; } = [];

    public List<LoPrevalecidoDto> Prevalece { get; } = [];

    public Dictionary<string, int> Conteos { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, decimal> Totales { get; } = new(StringComparer.Ordinal);

    public List<CreditoParaHistorialMigrado> CreditosParaHistorial { get; } = [];

    public int Creditos { get; private set; }

    public int Castigados { get; private set; }

    public int Personas { get; private set; }

    public decimal Capital { get; private set; }

    public static async Task<ValidacionEntrePartes> ValidarAsync(IApplicationDbContext db, MigrationLoad carga, IReadOnlyList<MigrationLoadPart> tandas,
        CancellationToken ct)
    {
        var v = new ValidacionEntrePartes();
        var ids = tandas.Select(t => t.Id).ToList();
        var tanda = tandas.ToDictionary(t => t.Id, t => (t.PartKey, t.BatchNumber));
        void Error(int parte, int fila, string columna, string codigo, string mensaje) =>
            v.Errores.Add(new ErrorEntrePartes(tanda[parte].PartKey, tanda[parte].BatchNumber, fila, columna, codigo, mensaje));
        void Aviso(int parte, int fila, string columna, string codigo, string mensaje) =>
            v.Avisos.Add(new ErrorEntrePartes(tanda[parte].PartKey, tanda[parte].BatchNumber, fila, columna, codigo, mensaje));

        var creditos = await db.MigrationLoanRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct);
        var cuotas = await db.MigrationInstallmentRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct);
        var saldos = await db.MigrationBalanceRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct);
        var riesgo = await db.MigrationRiskDataRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct);
        var reestructuras = await db.MigrationRestructuringRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct);
        var codeudores = await db.MigrationCoDebtorRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct);
        var garantias = await db.MigrationCollateralRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct);
        var castigados = await db.MigrationChargedOffRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct);
        var ventas = await db.MigrationSaleRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct);
        var cupos = await db.MigrationCreditLimitRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct);
        var bloqueos = await db.MigrationBlockRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct);
        var autorizaciones = await db.MigrationAuthorizationRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct);
        var avisos = await db.MigrationPriorNoticeRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct);
        var reportes = await db.MigrationBureauReportRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct);
        var reclamos = await db.MigrationClaimRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct);
        var vinculados = await db.MigrationRelatedPartyRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct);
        var historial = await db.MigrationArrearsHistoryRows.AsNoTracking().Where(r => ids.Contains(r.MigrationLoadPartId)).ToListAsync(ct);

        foreach (var t in tandas.GroupBy(t => t.PartKey)) v.Conteos[t.Key] = t.Sum(x => x.RowCount);
        var vivos = creditos.Select(c => c.SourceLoanNumber).ToHashSet(StringComparer.Ordinal);
        var deCastigo = castigados.Select(c => c.SourceLoanNumber).ToHashSet(StringComparer.Ordinal);
        var todos = vivos.Concat(deCastigo).ToHashSet(StringComparer.Ordinal);

        // 2. Cada número citado existe en loans (o en charged-off donde se admite).
        void Citado(int parte, int fila, string columna, string? numero, bool admiteCastigado)
        {
            if (numero is null || (admiteCastigado ? todos : vivos).Contains(numero)) return;
            Error(parte, fila, columna, ImportMigrationPartCommandHandler.CodigoReferenciaPendiente,
                admiteCastigado
                    ? $"El crédito {numero} no está en las partes de créditos ni de cartera castigada de la carga."
                    : $"El crédito {numero} no está en la parte de créditos de la carga.");
        }
        foreach (var r in cuotas) Citado(r.MigrationLoadPartId, r.RowNumber, C.NumeroDeCredito, r.SourceLoanNumber, false);
        foreach (var r in saldos) Citado(r.MigrationLoadPartId, r.RowNumber, C.NumeroDeCredito, r.SourceLoanNumber, false);
        foreach (var r in riesgo) Citado(r.MigrationLoadPartId, r.RowNumber, C.NumeroDeCredito, r.SourceLoanNumber, false);
        foreach (var r in reestructuras) Citado(r.MigrationLoadPartId, r.RowNumber, C.NumeroDeCredito, r.SourceLoanNumber, false);
        foreach (var r in avisos) Citado(r.MigrationLoadPartId, r.RowNumber, C.NumeroDeCredito, r.SourceLoanNumber, false);
        foreach (var r in codeudores) Citado(r.MigrationLoadPartId, r.RowNumber, C.NumeroDeCredito, r.SourceLoanNumber, true);
        foreach (var r in garantias) Citado(r.MigrationLoadPartId, r.RowNumber, C.NumeroDeCredito, r.SourceLoanNumber, true);
        foreach (var r in reportes) Citado(r.MigrationLoadPartId, r.RowNumber, C.NumeroDeCredito, r.SourceLoanNumber, true);
        foreach (var r in reclamos) Citado(r.MigrationLoadPartId, r.RowNumber, C.NumeroDeCredito, r.SourceLoanNumber, true);
        foreach (var r in historial) Citado(r.MigrationLoadPartId, r.RowNumber, C.NumeroDeCredito, r.SourceLoanNumber, true);
        foreach (var r in bloqueos) Citado(r.MigrationLoadPartId, r.RowNumber, C.NumeroDeCredito, r.SourceLoanNumber, true);

        // 6. Un número en loans y en charged-off a la vez.
        foreach (var c in castigados.Where(c => vivos.Contains(c.SourceLoanNumber)))
            Error(c.MigrationLoadPartId, c.RowNumber, C.Numero, ImportErrorsCodigos.Duplicado,
                $"El crédito {c.SourceLoanNumber} está a la vez en la parte de créditos y en la de cartera castigada.");

        // 3 y 4. Cada crédito con sus cuotas, sus saldos y su riesgo; el plan contra el capital; los conceptos contra las cuotas; la mora.
        var cuotasDe = cuotas.ToLookup(c => c.SourceLoanNumber, StringComparer.Ordinal);
        var saldosDe = saldos.ToLookup(s => s.SourceLoanNumber, StringComparer.Ordinal);
        var riesgoDe = riesgo.GroupBy(r => r.SourceLoanNumber, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        foreach (var c in creditos)
        {
            var susCuotas = cuotasDe[c.SourceLoanNumber].Select(x => new CuotaMigrada(x.RowNumber, x.InstallmentNumber, x.DueDate, x.Principal,
                x.CurrentInterest, x.DefaultInterest, x.Insurance, x.OtherCharges)).ToList();
            var susSaldos = saldosDe[c.SourceLoanNumber].Select(s => new SaldoMigrado(s.Concept, s.Amount)).ToList();
            if (susCuotas.Count == 0)
                Error(c.MigrationLoadPartId, c.RowNumber, C.Numero, ImportErrorsCodigos.Faltante, $"El crédito {c.SourceLoanNumber} no trae cuotas pendientes.");
            if (!susSaldos.Any(s => s.Concepto == LoanConcept.Principal))
                Error(c.MigrationLoadPartId, c.RowNumber, C.Numero, ImportErrorsCodigos.Faltante,
                    $"El crédito {c.SourceLoanNumber} no trae su saldo de capital en la parte de saldos.");
            if (!riesgoDe.TryGetValue(c.SourceLoanNumber, out var suRiesgo))
                Error(c.MigrationLoadPartId, c.RowNumber, C.Numero, ImportErrorsCodigos.Faltante,
                    $"El crédito {c.SourceLoanNumber} no trae su fila de mora, calificación y deterioro.");
            if (susCuotas.Count == 0 || susSaldos.Count == 0) continue;

            var capital = susSaldos.Where(s => s.Concepto == LoanConcept.Principal).Sum(s => s.Valor);
            if (CuadresDeMigracion.PlanDescuadrado(susCuotas, capital) is { } plan)
            {
                var ultima = cuotasDe[c.SourceLoanNumber].OrderBy(x => x.InstallmentNumber).Last();
                Error(ultima.MigrationLoadPartId, ultima.RowNumber, C.Capital, ErroresDeMigracion.CodigoPlanDescuadrado,
                    ErroresDeMigracion.PlanDescuadrado(c.SourceLoanNumber, plan.Calculado, plan.Declarado));
            }
            foreach (var d in CuadresDeMigracion.ConceptosDescuadrados(susCuotas, susSaldos))
            {
                var fila = saldosDe[c.SourceLoanNumber].First();
                Error(fila.MigrationLoadPartId, fila.RowNumber, C.Valor, ErroresDeMigracion.CodigoSaldoDescuadrado,
                    ErroresDeMigracion.SaldoDescuadrado(c.SourceLoanNumber, d.Concepto, d.Declarado, d.Calculado));
            }
            if (suRiesgo is not null && CuadresDeMigracion.MoraDistinta(suRiesgo.DaysPastDue, susCuotas, carga.CutoffDate))
                Aviso(suRiesgo.MigrationLoadPartId, suRiesgo.RowNumber, C.DiasDeMora, ErroresDeMigracion.CodigoMoraDistinta,
                    ErroresDeMigracion.MoraDistinta(c.SourceLoanNumber, suRiesgo.DaysPastDue, CuadresDeMigracion.MoraSegunCuotas(susCuotas, carga.CutoffDate)));
        }

        // El episodio de cada aviso previo: si no es el vigente según las cuotas, aviso y no se migra.
        foreach (var a in avisos)
        {
            var susCuotas = cuotasDe[a.SourceLoanNumber].Select(x => new CuotaMigrada(x.RowNumber, x.InstallmentNumber, x.DueDate, x.Principal,
                x.CurrentInterest, x.DefaultInterest, x.Insurance, x.OtherCharges)).ToList();
            if (!CuadresDeMigracion.EpisodioVigente(a.EpisodeStartDate, susCuotas, carga.CutoffDate))
                Aviso(a.MigrationLoadPartId, a.RowNumber, C.Episodio, ErroresDeMigracion.CodigoEpisodioNoVigente,
                    ErroresDeMigracion.EpisodioNoVigente(a.SourceLoanNumber, a.EpisodeStartDate));
        }

        // 5. Ventas migradas y créditos uno a uno, por factura.
        var porFactura = creditos.Where(c => c.InvoiceNumber is not null).ToLookup(c => c.InvoiceNumber!.ToUpperInvariant(), StringComparer.Ordinal);
        foreach (var s in ventas.Where(s => s.Outcome == SaleReconciliationOutcome.Migrated))
        {
            var suyos = porFactura[s.InvoiceNumber.ToUpperInvariant()].ToList();
            if (suyos.Count != 1 || !string.Equals(suyos[0].SourceLoanNumber, s.SourceLoanNumber, StringComparison.Ordinal))
                Error(s.MigrationLoadPartId, s.RowNumber, C.NumeroDeCredito, ImportErrorsCodigos.Faltante,
                    $"La venta {s.InvoiceNumber} está como migrada, pero no hay un único crédito {s.SourceLoanNumber} con esa factura en la parte de créditos.");
        }
        var migradas = ventas.Where(s => s.Outcome == SaleReconciliationOutcome.Migrated).Select(s => s.InvoiceNumber.ToUpperInvariant())
            .ToHashSet(StringComparer.Ordinal);
        foreach (var c in creditos.Where(c => c.InvoiceNumber is not null && !migradas.Contains(c.InvoiceNumber.ToUpperInvariant())))
            Error(c.MigrationLoadPartId, c.RowNumber, C.Factura, ImportErrorsCodigos.Faltante,
                $"El crédito {c.SourceLoanNumber} dice que nació de la venta {c.InvoiceNumber}, que no está como migrada en la parte de ventas.");
        foreach (var g in creditos.Where(c => c.InvoiceNumber is not null).GroupBy(c => c.InvoiceNumber!.ToUpperInvariant()).Where(g => g.Count() > 1))
            foreach (var c in g.Skip(1))
                Error(c.MigrationLoadPartId, c.RowNumber, C.Factura, ImportErrorsCodigos.Duplicado, $"La factura {c.InvoiceNumber} está en más de un crédito.");

        // 7. Cupos que se cruzan en el tiempo (misma persona, tipo y línea).
        foreach (var g in cupos.GroupBy(c => (c.PersonId, c.LimitKind, c.CreditLineCode)))
        {
            var orden = g.OrderBy(c => c.ValidFrom).ToList();
            for (var i = 1; i < orden.Count; i++)
                if (orden[i - 1].ValidTo is not { } hasta || hasta >= orden[i].ValidFrom)
                    Error(orden[i].MigrationLoadPartId, orden[i].RowNumber, C.VigenteDesde, ImportErrorsCodigos.Duplicado,
                        "Este cupo se cruza en el tiempo con otro de la misma persona, tipo y línea.");
        }

        // 8. Personas dadas de baja después de importar.
        var citadas = creditos.Select(c => (c.MigrationLoadPartId, c.RowNumber, C.Deudor, c.PersonId))
            .Concat(castigados.Select(c => (c.MigrationLoadPartId, c.RowNumber, C.Deudor, c.PersonId)))
            .Concat(codeudores.Select(c => (c.MigrationLoadPartId, c.RowNumber, C.Codeudor, c.CoDebtorPersonId)))
            .Concat(ventas.Select(c => (c.MigrationLoadPartId, c.RowNumber, C.Deudor, c.PersonId)))
            .Concat(cupos.Select(c => (c.MigrationLoadPartId, c.RowNumber, C.Persona, c.PersonId)))
            .Concat(bloqueos.Select(c => (c.MigrationLoadPartId, c.RowNumber, C.Persona, c.PersonId)))
            .Concat(autorizaciones.Select(c => (c.MigrationLoadPartId, c.RowNumber, C.Persona, c.PersonId)))
            .Concat(vinculados.Select(c => (c.MigrationLoadPartId, c.RowNumber, C.Persona, c.PersonId)))
            .ToList();
        var personas = citadas.Select(c => c.Item4).Distinct().ToList();
        var vivas = (await db.People.AsNoTracking().Where(p => personas.Contains(p.Id) && !p.IsDeleted).Select(p => p.Id).ToListAsync(ct)).ToHashSet();
        foreach (var (parte, fila, columna, persona) in citadas.Where(c => !vivas.Contains(c.Item4)))
            Error(parte, fila, columna, IngenIA365ERP.Application.Common.Imports.ImportErrors.CellNotFound,
                "La persona se dio de baja en Maestros › Personas después de importar la fila. Restáurela o retire la fila.");

        // 9. Lo registrado a mano antes de activar para la misma persona y concepto prevalece (se informa).
        var publicos = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personas.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.PublicId, ct);
        var conCupo = cupos.Select(c => c.PersonId).Distinct().ToList();
        foreach (var c in await db.CreditLimits.AsNoTracking().Where(l => conCupo.Contains(l.PersonId) && l.Origin != CreditLimitOrigin.Migration && !l.IsDeleted)
                     .ToListAsync(ct))
            if (cupos.Any(m => m.PersonId == c.PersonId && m.LimitKind == c.Kind))
                v.Prevalece.Add(new LoPrevalecidoDto(publicos.GetValueOrDefault(c.PersonId), "CreditLimit",
                    string.Create(CultureInfo.InvariantCulture, $"Cupo {c.Kind} otorgado a mano desde el {c.ValidFrom:yyyy-MM-dd}")));
        var conBloqueo = bloqueos.Select(b => b.PersonId).Distinct().ToList();
        foreach (var b in await db.DebtorBlocks.AsNoTracking().Where(b => conBloqueo.Contains(b.PersonId) && !b.IsAutomatic && b.LiftedAt == null && !b.IsDeleted)
                     .ToListAsync(ct))
            if (bloqueos.Any(m => m.PersonId == b.PersonId && m.BlockReason == b.Reason))
                v.Prevalece.Add(new LoPrevalecidoDto(publicos.GetValueOrDefault(b.PersonId), "DebtorBlock",
                    string.Create(CultureInfo.InvariantCulture, $"Bloqueo {b.Reason} registrado a mano desde el {b.StartedOn:yyyy-MM-dd}")));
        var conVinculo = vinculados.Select(r => r.PersonId).Distinct().ToList();
        foreach (var r in await db.RelatedParties.AsNoTracking().Where(r => conVinculo.Contains(r.PersonId) && r.Source != RelatedPartySource.Migration && !r.IsDeleted)
                     .ToListAsync(ct))
            if (vinculados.Any(m => m.PersonId == r.PersonId && m.Basis == r.Basis))
                v.Prevalece.Add(new LoPrevalecidoDto(publicos.GetValueOrDefault(r.PersonId), "RelatedParty",
                    string.Create(CultureInfo.InvariantCulture, $"Vínculo {r.Basis} registrado a mano desde el {r.ValidFrom:yyyy-MM-dd}")));

        // Conteos, totales y lo que pide el historial.
        v.Creditos = creditos.Count;
        v.Castigados = castigados.Count;
        v.Personas = creditos.Select(c => c.PersonId).Concat(castigados.Select(c => c.PersonId)).Concat(codeudores.Select(c => c.CoDebtorPersonId)).Distinct().Count();
        v.Capital = saldos.Where(s => s.Concept == LoanConcept.Principal).Sum(s => s.Amount);
        foreach (var g in saldos.GroupBy(s => s.Concept)) v.Totales[g.Key.ToString()] = g.Sum(s => s.Amount);
        v.Totales["IndividualImpairment"] = riesgo.Sum(r => r.PrincipalImpairment + r.InterestImpairment + r.OtherImpairment);
        v.Totales["GeneralImpairment"] = riesgo.Sum(r => r.GeneralImpairment);
        v.Totales["ChargedOff"] = castigados.Sum(c => c.PrincipalBalance + c.InterestBalance + c.OtherBalance);
        foreach (var g in riesgo.GroupBy(r => r.Rating)) v.Totales[$"Category{g.Key}"] = g.Count();

        var codigos = creditos.Select(c => c.CreditLineCode).Distinct().ToList();
        var lineas = await db.CreditLines.AsNoTracking().Where(l => codigos.Contains(l.Code)).Select(l => new { l.Id, l.Code }).ToListAsync(ct);
        var versiones = await new Configuration.VersionVigenteDeLinea(db).LeerVariasAsync(lineas.Select(l => l.Id).ToList(), carga.CutoffDate, ct: ct);
        var modalidad = lineas.ToDictionary(l => l.Code, l => versiones.TryGetValue(l.Id, out var x) ? (x.Version.ModalityCode, x.Version.SubsegmentCode) : (null, null),
            StringComparer.OrdinalIgnoreCase);
        var historialDe = historial.ToLookup(h => h.SourceLoanNumber, StringComparer.Ordinal);
        foreach (var c in creditos)
        {
            var (m, s) = modalidad.GetValueOrDefault(c.CreditLineCode);
            v.CreditosParaHistorial.Add(new CreditoParaHistorialMigrado(c.SourceLoanNumber, m, s, c.DisbursementDate,
                historialDe[c.SourceLoanNumber].Select(h => ((int)h.PeriodYear, (int)h.PeriodMonth)).ToHashSet()));
        }
        return v;
    }

    /// <summary>Los códigos de fila que se repiten en las validaciones entre partes.</summary>
    private static class ImportErrorsCodigos
    {
        public const string Duplicado = IngenIA365ERP.Application.Common.Imports.ImportErrors.RowDuplicate;
        public const string Faltante = IngenIA365ERP.Application.Common.Imports.ImportErrors.CellRequired;
    }
}
