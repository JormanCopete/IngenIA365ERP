using System.Globalization;
using System.Text.RegularExpressions;
using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Risk;

/// <summary>
/// La plantilla 8 — historial de mora complementario (feature 013, K6a, T1035; plantillas.md §7; FR-140): una hoja «Datos» con llave número del
/// crédito + mes. (nuevo)
/// </summary>
public static class PlantillaDeHistorialComplementario
{
    public const string Clave = "lending.arrears-history";

    public static class Columnas
    {
        public const string NumeroDeCredito = "numeroDeCredito";
        public const string Mes = "mes";
        public const string MoraMaximaDias = "moraMaximaDias";
        public const string MoraAlCierreDias = "moraAlCierreDias";
        public const string Fuente = "fuente";
    }

    public static DefinicionDePlantilla Definicion { get; } = new(Clave, "Historial de mora complementario", ModuloDeAuditoria.Lending,
    [
        new HojaDePlantilla("Datos",
        [
            new(Columnas.NumeroDeCredito, TipoDeValor.Texto, Obligatoria: true, Largo: 30,
                Reglas: "llave con el mes; un crédito de la carga de migración aplicada (antes de activar) o un crédito migrado (después)", Ejemplo: "CR-000123"),
            new(Columnas.Mes, TipoDeValor.Texto, Obligatoria: true, Largo: 7,
                Reglas: "llave; mes AAAA-MM sin cierre mensual de Cartera y no futuro", Ejemplo: "2024-06"),
            new(Columnas.MoraMaximaDias, TipoDeValor.Entero, Obligatoria: true, Reglas: "mora máxima del mes, entero desde 0", Ejemplo: "0"),
            new(Columnas.MoraAlCierreDias, TipoDeValor.Entero, Reglas: "mora al último día del mes, entero desde 0", Ejemplo: ""),
            new(Columnas.Fuente, TipoDeValor.Texto, Obligatoria: true, Largo: 120, Reglas: "de dónde sale el historial; la misma en todas las filas",
                Ejemplo: "Reporte de mora mensual del sistema anterior"),
        ]),
    ]);
}

/// <summary>
/// <c>POST /api/lending/risk/arrears-history/import?mode=review|apply</c> (feature 013, K6a, T1035; plantillas.md §7; api.md §12.5,
/// <c>Lending.ArrearsHistory.Import</c>). (nuevo)
/// </summary>
public sealed record ImportArrearsHistoryCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string Reason = "")
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportArrearsHistoryCommandValidator : AbstractValidator<ImportArrearsHistoryCommand>
{
    public ImportArrearsHistoryCommandValidator()
    {
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.Reason).MaximumLength(ValidadorConMotivo<ImportArrearsHistoryCommand>.LargoMaximo);
    }
}

/// <summary>
/// La carga complementaria del historial de mora sobre el ejecutor común (<see cref="EjecutorDeImportacion"/>: revisión previa, todo o nada, errores
/// por fila y columna). Reglas:
/// <list type="bullet">
/// <item>se admite desde que hay una carga de migración aplicada —también antes de activar— y hasta el primer cierre en pérdida esperada
/// (<c>Lending.ArrearsHistory.LoadClosed</c>); sin carga, <c>Lending.ArrearsHistory.NoAppliedLoad</c>;</item>
/// <item>cada crédito es de esa carga (<c>Lending.ArrearsHistory.LoanNotInLoad</c> en <c>numeroDeCredito</c>); cada mes, sin cierre mensual de Cartera
/// (<c>Lending.ArrearsHistory.MonthClosed</c> en <c>mes</c>) y no futuro; la <c>fuente</c>, la misma en todas las filas (error de columna);</item>
/// <item>aplicar escribe <c>LND_ArrearsHistoryLoads</c> con el SHA-256 del archivo y sus filas y, si Cartera ya se activó, <c>LND_LoanArrearsHistory</c>
/// con origen <c>ComplementaryLoad</c> y <c>ArrearsHistoryLoadId</c>; antes de activar, las filas las lleva la activación (<c>CierreBase</c>).
/// Sin transacciones ni hechos; volver a cargar un mes no borra nada: la carga más reciente manda.</item>
/// <item>la revisión trae <c>extra.completeness[]</c>, por segmento cubierto, los créditos completos y los que siguen sin historial
/// (<see cref="CoberturaDelHistorialMigrado"/>).</item>
/// </list>
/// El evento resumen de la carga (C46) se audita con módulo <c>Lending</c>.
/// </summary>
public sealed partial class ImportArrearsHistoryCommandHandler(
    IApplicationDbContext db,
    EjecutorDeImportacion ejecutor,
    INormativaVigente normativa,
    IActorActual actorActual,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<ImportArrearsHistoryCommand, Result<ImportResultDto>>
{
    [GeneratedRegex(@"^(\d{4})-(\d{2})$")]
    private static partial Regex PatronDeMes();

    public async Task<Result<ImportResultDto>> Handle(ImportArrearsHistoryCommand request, CancellationToken ct)
    {
        var activada = await db.LendingSetups.AsNoTracking().AnyAsync(s => s.Id == LendingSetup.IdDeLaFilaUnica && s.ActivationDate != null, ct);
        var estado = activada ? MigrationLoadStatus.Activated : MigrationLoadStatus.Applied;
        var carga = await db.MigrationLoads.AsNoTracking().Where(l => l.Status == estado && !l.IsDeleted).OrderByDescending(l => l.Id).FirstOrDefaultAsync(ct);
        if (carga is null) return Result.Failure<ImportResultDto>(ErroresDeRiesgo.HistorialSinCarga());

        // El primer cierre en pérdida esperada (vigente) cierra la carga complementaria.
        var conPerdida = await db.MonthlyCloses.AsNoTracking()
            .Where(c => c.Kind == MonthlyCloseKind.Monthly && !c.IsDeleted && c.ModelBySegmentJson.Contains(nameof(ImpairmentModel.ExpectedLossAnnex2))
                && !db.MonthlyCloseReopenings.Any(r => r.MonthlyCloseId == c.Id && !r.IsDeleted))
            .OrderBy(c => c.CloseDate).Select(c => (DateOnly?)c.CloseDate).FirstOrDefaultAsync(ct);
        if (conPerdida is { } primero) return Result.Failure<ImportResultDto>(ErroresDeRiesgo.HistorialCargaCerrada(primero));

        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<ImportResultDto>(ErroresDeGobierno.SinUsuario());

        // Los créditos de la carga: antes de activar, los de la preparación; después, los migrados (que guardan el mismo número).
        var numeros = activada
            ? (await db.Loans.AsNoTracking().Where(l => l.MigrationLoadId == carga.Id).Select(l => new { l.Number, l.Id }).ToListAsync(ct))
                .ToDictionary(l => l.Number, l => (int?)l.Id, StringComparer.Ordinal)
            : (await NumerosDeLaPreparacionAsync(carga, ct)).ToDictionary(n => n, _ => (int?)null, StringComparer.Ordinal);
        var cerrados = (await db.MonthlyCloses.AsNoTracking()
                .Where(c => c.Kind == MonthlyCloseKind.Monthly && !c.IsDeleted && !db.MonthlyCloseReopenings.Any(r => r.MonthlyCloseId == c.Id && !r.IsDeleted))
                .Select(c => new { c.PeriodYear, c.PeriodMonth }).ToListAsync(ct))
            .Select(c => ((int)c.PeriodYear, (int)c.PeriodMonth)).ToHashSet();
        var hoy = reloj.HoyLocal;

        var filas = new List<FilaDeHistorialComplementario>();
        ArrearsHistoryLoad? nueva = null;

        return await ejecutor.EjecutarAsync(PlantillaDeHistorialComplementario.Definicion, request, ProcesarAsync, ct, DespuesDeGuardarAsync);

        async Task ProcesarAsync(ContextoDeImportacion ctx, CancellationToken token)
        {
            string? fuente = null;
            foreach (var fila in ctx.Datos.Filas)
            {
                var numero = fila.Texto(PlantillaDeHistorialComplementario.Columnas.NumeroDeCredito, 30)?.Trim();
                var mes = Mes(fila);
                var maxima = fila.Entero(PlantillaDeHistorialComplementario.Columnas.MoraMaximaDias);
                var alCierre = fila.Entero(PlantillaDeHistorialComplementario.Columnas.MoraAlCierreDias);
                var suFuente = fila.Texto(PlantillaDeHistorialComplementario.Columnas.Fuente, 120)?.Trim();
                fila.Requerido(PlantillaDeHistorialComplementario.Columnas.Fuente);
                if (maxima is < 0) fila.Error(PlantillaDeHistorialComplementario.Columnas.MoraMaximaDias, ImportErrors.CellFormat, "No puede ser negativo.");
                if (alCierre is < 0) fila.Error(PlantillaDeHistorialComplementario.Columnas.MoraAlCierreDias, ImportErrors.CellFormat, "No puede ser negativo.");
                if (suFuente is not null)
                {
                    fuente ??= suFuente;
                    if (!string.Equals(fuente, suFuente, StringComparison.Ordinal))
                        fila.Error(PlantillaDeHistorialComplementario.Columnas.Fuente, ImportErrors.CellFormat,
                            $"La fuente es la misma en todas las filas («{fuente}»): una carga, una fuente.");
                }
                if (numero is not null && !numeros.ContainsKey(numero))
                    fila.Error(PlantillaDeHistorialComplementario.Columnas.NumeroDeCredito, ErroresDeRiesgo.CodigoHistorialFueraDeLaCarga,
                        ErroresDeRiesgo.HistorialFueraDeLaCarga(numero));
                if (mes is { } m)
                {
                    if (cerrados.Contains((m.Anio, m.Mes)))
                        fila.Error(PlantillaDeHistorialComplementario.Columnas.Mes, ErroresDeRiesgo.CodigoHistorialMesCerrado, ErroresDeRiesgo.HistorialMesCerrado(m.Anio, m.Mes));
                    else if (new DateOnly(m.Anio, m.Mes, 1) > new DateOnly(hoy.Year, hoy.Month, 1))
                        fila.Error(PlantillaDeHistorialComplementario.Columnas.Mes, ImportErrors.CellFormat, "El mes no puede ser futuro.");
                }
                if (numero is null || mes is null || maxima is null) continue;
                var llave = string.Create(CultureInfo.InvariantCulture, $"{numero}|{mes.Value.Anio:0000}-{mes.Value.Mes:00}");
                if (!ctx.Datos.LlaveUnica(fila, llave, PlantillaDeHistorialComplementario.Columnas.Mes) || fila.TieneErrores) continue;
                filas.Add(new FilaDeHistorialComplementario(numero, mes.Value.Anio, mes.Value.Mes, maxima.Value, alCierre));
                ctx.Registrar(fila, llave, AccionDeImportacion.Create);
            }

            // La completitud por segmento cubierto, con estas filas (plantillas.md §7).
            var fecha = CoberturaDelHistorialMigrado.ReferenciaDeLaCarga(carga);
            var regla = await normativa.LeerAsync(RegulatoryDocumentKind.ReglaDePerdidaEsperada, fecha, ReglaDePerdidaEsperada.Leer, ct: token);
            var catalogo = await normativa.CatalogoDeModalidadesAsync(carga.CutoffDate, null, token);
            if (regla.IsSuccess && catalogo.IsSuccess)
                ctx.Extra["completeness"] = await new CoberturaDelHistorialMigrado(db).CalcularAsync(carga, regla.Value.Documento, catalogo.Value.Documento, fecha,
                    filas, token);

            if (ctx.Modo != ModoDeImportacion.Apply || ctx.HayErrores || filas.Count == 0) return;
            nueva = new ArrearsHistoryLoad
            {
                MigrationLoadId = carga.Id,
                FileName = request.File.NombreArchivo.Length > 260 ? request.File.NombreArchivo[..260] : request.File.NombreArchivo,
                FileSha256 = request.File.Sha256,
                RowCount = filas.Count,
                Source = fuente ?? string.Empty,
                RowsJson = CoberturaDelHistorialMigrado.Serializar(filas),
                AppliedByUserId = usuario,
                AppliedAt = reloj.UtcNow,
                CoverageJson = System.Text.Json.JsonSerializer.Serialize(ctx.Extra.GetValueOrDefault("completeness") ?? Array.Empty<object>()),
            };
            db.ArrearsHistoryLoads.Add(nueva);
        }

        async Task DespuesDeGuardarAsync(ContextoDeImportacion ctx, CancellationToken token)
        {
            if (nueva is null) return;
            if (activada)
                foreach (var f in filas)
                    db.LoanArrearsHistory.Add(new LoanArrearsHistoryEntry
                    {
                        LoanId = numeros[f.LoanNumber]!.Value, PeriodYear = (short)f.Year, PeriodMonth = (byte)f.Month, MaxDaysPastDue = f.MaxDaysPastDue,
                        DaysPastDueAtMonthEnd = f.DaysPastDueAtMonthEnd, Origin = ArrearsHistoryOrigin.ComplementaryLoad, ArrearsHistoryLoadId = nueva.Id,
                    });
            await auditoria.EmitirHistorialComplementarioAplicadoAsync(nueva.PublicId, new
            {
                migrationLoadPublicId = carga.PublicId, fileName = nueva.FileName, fileSha256 = nueva.FileSha256, rows = nueva.RowCount, source = nueva.Source,
                activated = activada,
            }, token);
        }
    }

    private async Task<List<string>> NumerosDeLaPreparacionAsync(MigrationLoad carga, CancellationToken ct)
    {
        var partes = await db.MigrationLoadParts.AsNoTracking()
            .Where(p => p.MigrationLoadId == carga.Id && p.Status == MigrationPartStatus.Imported && !p.IsDeleted).Select(p => p.Id).ToListAsync(ct);
        return await db.MigrationLoanRows.AsNoTracking().Where(r => partes.Contains(r.MigrationLoadPartId)).Select(r => r.SourceLoanNumber).Distinct()
            .ToListAsync(ct);
    }

    private static (int Anio, int Mes)? Mes(FilaDeImportacion fila)
    {
        var texto = fila.Texto(PlantillaDeHistorialComplementario.Columnas.Mes, 7);
        if (texto is null) return null;
        var m = PatronDeMes().Match(texto.Trim());
        if (m.Success && int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) is >= 1 and <= 12 and var mes)
            return (int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), mes);
        fila.Error(PlantillaDeHistorialComplementario.Columnas.Mes, ImportErrors.CellFormat, "El mes se escribe AAAA-MM.");
        return null;
    }
}
