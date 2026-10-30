using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Common.Cuadres;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Cuadres;

// Feature 015, US13 (T0288; FR-158, FR-160, FR-228; api.md §14): lo común del cuadre de apertura de Recaudo y de Tesorería —sus DTOs, sus errores con el prefijo del
// módulo y el registro del provisional y del definitivo sobre las tablas de cada uno—. No conoce a ninguno de los dos módulos: cada uno le pasa sus líneas propuestas
// (lo cargado y lo contable) y sus DbSet, así Recaudo y Tesorería siguen sin nombrarse (RecaudoYTesoreriaSeHablanPorSusPuertos).

/// <summary>Una línea del cuadre. <c>Squares</c>: la comparable cuadra (sin diferencia, o sin nada cargado ni dato contable); la informativa siempre. (nuevo)</summary>
public sealed record OpeningSquareLineDto(string Concept, string Dimension, string? DimensionLabel, decimal Loaded, decimal? Ledger, decimal Difference,
    bool IsComparable, bool Squares);

/// <summary>Un cuadre registrado (definitivo o provisional) con sus líneas. (nuevo)</summary>
public sealed record OpeningSquareDto(
    Guid PublicId, string Kind, string Status, DateOnly CutoffDate, decimal TotalLoaded, decimal TotalLedger, decimal TotalDifference, DateTime RegisteredAt,
    string? ApprovedBy, DateTime? ApprovedAt, string? Reason, DateOnly? DeadlineOn, Guid? MinutesAttachmentPublicId, Guid? DefinitiveSquarePublicId,
    IReadOnlyList<OpeningSquareLineDto> Lines);

/// <summary>
/// <c>GET …/opening-squares/current</c>: la propuesta calculada hoy a la fecha de corte (lo cargado contra lo contable), el cuadre vigente (el último que no fue
/// reemplazado) y el provisional pendiente, si lo hay. (nuevo)
/// </summary>
public sealed record CurrentOpeningSquareDto(DateOnly CutoffDate, IReadOnlyList<OpeningSquareLineDto> Proposal, decimal TotalLoaded, decimal TotalLedger,
    decimal TotalDifference, bool Squares, OpeningSquareDto? Current, OpeningSquareDto? PendingProvisional, Guid? MinutesOwnerPublicId = null);

/// <summary>Una cifra de referencia contra lo cargado y lo contable (FR-160). <c>Loaded</c> y <c>Ledger</c> salen de la misma propuesta del cuadre. (nuevo)</summary>
public sealed record ReferenceFigureComparisonDto(DateOnly CutoffDate, string Concept, string Dimension, string? DimensionLabel, decimal Reference, decimal? Loaded,
    decimal? Ledger, decimal DifferenceLoadedVsReference);

/// <summary>Los errores del cuadre, con el prefijo del módulo (<c>Receipts</c> o <c>Treasury</c>). (nuevo, DT §2.7)</summary>
public static class ErroresDelCuadre
{
    public static string CodigoConDiferencia(string modulo) => $"{modulo}.Activation.SquareHasDifference";
    public static string CodigoFechaLimite(string modulo) => $"{modulo}.Activation.ProvisionalDeadlineInvalid";
    public static string CodigoSinCarga(string modulo) => $"{modulo}.Activation.NothingLoaded";

    public static Error ConDiferencia(string modulo, IReadOnlyList<OpeningSquareLineDto> lineas) => new ErrorConDatos(CodigoConDiferencia(modulo),
        "El cuadre de apertura tiene diferencias contra Contabilidad: corrija la carga o la contabilidad, adjunte el acta que acepta la diferencia o registre un cuadre provisional.",
        new { lines = lineas.Where(l => !l.Squares).ToList() });

    public static Error FechaLimiteInvalida(string modulo, DateOnly corte) => new ErrorConDatos(CodigoFechaLimite(modulo),
        $"La fecha límite del cuadre definitivo tiene que ser posterior a la fecha de corte ({corte:dd/MM/yyyy}).", new { cutoffDate = corte });

    public static string CodigoActaInvalida(string modulo) => $"{modulo}.Activation.MinutesInvalid";

    /// <summary>
    /// Segunda ronda del 2026-10-27 (D-g; FR-232): el acta citada tiene que ser un adjunto «Acta del cuadre de apertura» (<c>OpeningSquareMinutes</c>) de la configuración
    /// del módulo (<paramref name="dueno"/>) y estar disponible. Nulo si no se cita acta o si la citada sirve.
    /// </summary>
    public static async Task<Error?> ActaAsync(IngenIA365ERP.Application.Common.Interfaces.IApplicationDbContext db, string modulo, Guid? acta, Guid? dueno, CancellationToken ct)
    {
        if (acta is not { } id) return null;
        var valida = dueno is { } d && await db.Attachments.AsNoTracking().AnyAsync(a => a.PublicId == id
            && a.OwnerEntityType == IngenIA365ERP.Application.Attachments.Common.AdjuntosDeModulo.ActaDelCuadreDeApertura && a.OwnerEntityPublicId == d
            && a.Status == IngenIA365ERP.Domain.Enums.Core.EstadoDeAdjunto.Available, ct);
        return valida
            ? null
            : new ErrorConDatos(CodigoActaInvalida(modulo),
                "El acta tiene que subirse como «Acta del cuadre de apertura» de este módulo y estar disponible antes de registrar el cuadre.", new { minutesAttachmentPublicId = id });
    }
}

/// <summary>Lo común de los dos comandos del provisional: motivo y fecha límite obligatorios. (nuevo)</summary>
public interface ISolicitudDeCuadreProvisional
{
    DateOnly? CutoffDate { get; }
    string Reason { get; }
    DateOnly DeadlineOn { get; }
}

/// <summary>El validador común del provisional (el motivo lo exige también <see cref="ValidadorConMotivo{T}"/>). (nuevo)</summary>
public abstract class ValidadorDeCuadreProvisional<T> : AbstractValidator<T> where T : ISolicitudDeCuadreProvisional
{
    protected ValidadorDeCuadreProvisional()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500).WithMessage("Diga por qué se activa con un cuadre provisional (hasta 500 caracteres).");
        RuleFor(x => x.DeadlineOn).NotEmpty().WithMessage("Diga la fecha límite del cuadre definitivo.");
    }
}

/// <summary>
/// El registro de un cuadre sobre las tablas de un módulo (nuevo). Reglas (decisión del dueño, FR-228):
/// <list type="bullet">
/// <item>el <b>provisional</b> exige motivo y fecha límite posterior al corte y queda <c>ProvisionalApproved</c> a nombre de quien lo registra (quien tiene
/// <c>…Activation.ProvisionalSquare</c>: ésa es la aprobación); otro provisional pendiente queda <c>Superseded</c>;</item>
/// <item>el <b>definitivo</b> queda <c>Squared</c> si todo cuadra o <c>AcceptedWithMinutes</c> con el acta; con diferencia y sin acta no se registra
/// (<c>…Activation.SquareHasDifference</c> con las líneas); el provisional pendiente queda enlazado a él (<c>DefinitiveSquareId</c>) y <c>Superseded</c>, igual que un
/// definitivo anterior;</item>
/// <item>ninguno escribe hechos contables (FR-158): es una comparación.</item>
/// </list>
/// Guarda: dos <c>SaveChanges</c>, porque el enlace necesita el Id del definitivo.
/// </summary>
public static class RegistroDeCuadres
{
    public static IReadOnlyList<OpeningSquareLineDto> Lineas(CalculoDeCuadre calculo) =>
        calculo.Lineas.Select(l => new OpeningSquareLineDto(l.Concepto, l.Dimension, l.Etiqueta, l.Cargado, l.Contable, l.Diferencia, l.Comparable, l.Cuadra)).ToList();

    public static async Task<Result<TCuadre>> RegistrarAsync<TCuadre, TLinea>(
        DbSet<TCuadre> cuadres, DbSet<TLinea> lineas, Func<CancellationToken, Task<int>> guardar, string modulo, OpeningSquareKind tipo, DateOnly corte,
        CalculoDeCuadre calculo, int usuario, DateTime ahora, string? motivo, DateOnly? fechaLimite, Guid? acta, CancellationToken ct)
        where TCuadre : CuadreDeAperturaBase, new()
        where TLinea : LineaDeCuadreDeAperturaBase, new()
    {
        OpeningSquareState estado;
        if (tipo == OpeningSquareKind.Provisional)
        {
            if (fechaLimite is not { } limite || !CalculoDelCuadreDeApertura.FechaLimiteValida(corte, limite))
                return Result.Failure<TCuadre>(ErroresDelCuadre.FechaLimiteInvalida(modulo, corte));
            estado = OpeningSquareState.ProvisionalApproved;
        }
        else
        {
            var definitivo = CalculoDelCuadreDeApertura.EstadoDelDefinitivo(calculo, acta is not null);
            if (definitivo is not { } e) return Result.Failure<TCuadre>(ErroresDelCuadre.ConDiferencia(modulo, Lineas(calculo)));
            estado = e;
        }

        var cuadre = new TCuadre
        {
            Kind = tipo, Status = estado, CutoffDate = corte, TotalLoaded = calculo.TotalCargado, TotalLedger = calculo.TotalContable,
            TotalDifference = calculo.TotalDiferencia, RegisteredByUserId = usuario, RegisteredAt = ahora,
            ApprovedByUserId = tipo == OpeningSquareKind.Provisional || estado == OpeningSquareState.AcceptedWithMinutes ? usuario : null,
            ApprovedAt = tipo == OpeningSquareKind.Provisional || estado == OpeningSquareState.AcceptedWithMinutes ? ahora : null,
            Reason = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim(), DeadlineOn = tipo == OpeningSquareKind.Provisional ? fechaLimite : null,
            MinutesAttachmentPublicId = acta,
        };
        cuadres.Add(cuadre);
        await guardar(ct);
        foreach (var l in calculo.Lineas)
            lineas.Add(new TLinea
            {
                OpeningSquareId = cuadre.Id, Concept = l.Concepto, Dimension = l.Dimension, DimensionLabel = l.Etiqueta, Loaded = l.Cargado, Ledger = l.Contable,
                Difference = l.Diferencia, IsComparable = l.Comparable,
            });

        // Lo anterior que este reemplaza: el provisional pendiente (y en un definitivo, el definitivo anterior).
        var anteriores = await cuadres.Where(c => c.Id != cuadre.Id && c.Status != OpeningSquareState.Superseded
                                                  && (c.Kind == OpeningSquareKind.Provisional || tipo == OpeningSquareKind.Definitive)).ToListAsync(ct);
        foreach (var a in anteriores)
        {
            a.Status = OpeningSquareState.Superseded;
            if (tipo == OpeningSquareKind.Definitive && a.Kind == OpeningSquareKind.Provisional) a.DefinitiveSquareId = cuadre.Id;
        }
        await guardar(ct);
        return Result.Success(cuadre);
    }

    /// <summary>El vigente (el último que no fue reemplazado) y el provisional pendiente.</summary>
    public static async Task<(TCuadre? Vigente, TCuadre? Provisional)> VigentesAsync<TCuadre>(IQueryable<TCuadre> cuadres, CancellationToken ct)
        where TCuadre : CuadreDeAperturaBase
    {
        var vivos = await cuadres.AsNoTracking().Where(c => c.Status != OpeningSquareState.Superseded).OrderByDescending(c => c.Id).ToListAsync(ct);
        return (vivos.FirstOrDefault(), vivos.FirstOrDefault(c => c.Kind == OpeningSquareKind.Provisional && c.Status == OpeningSquareState.ProvisionalApproved));
    }

    public static async Task<OpeningSquareDto> DtoAsync<TCuadre, TLinea>(TCuadre c, IQueryable<TLinea> lineas, IQueryable<TCuadre> cuadres,
        Func<int?, CancellationToken, Task<string?>> nombreDe, CancellationToken ct)
        where TCuadre : CuadreDeAperturaBase where TLinea : LineaDeCuadreDeAperturaBase
    {
        var filas = await lineas.AsNoTracking().Where(l => l.OpeningSquareId == c.Id).OrderBy(l => l.Concept).ThenBy(l => l.Dimension).ToListAsync(ct);
        Guid? definitivo = c.DefinitiveSquareId is { } d ? await cuadres.AsNoTracking().Where(x => x.Id == d).Select(x => (Guid?)x.PublicId).FirstOrDefaultAsync(ct) : null;
        return new OpeningSquareDto(c.PublicId, c.Kind.ToString(), c.Status.ToString(), c.CutoffDate, c.TotalLoaded, c.TotalLedger, c.TotalDifference, c.RegisteredAt,
            await nombreDe(c.ApprovedByUserId, ct), c.ApprovedAt, c.Reason, c.DeadlineOn, c.MinutesAttachmentPublicId, definitivo,
            filas.Select(l => new OpeningSquareLineDto(l.Concept, l.Dimension, l.DimensionLabel, l.Loaded, l.Ledger, l.Difference, l.IsComparable,
                !l.IsComparable || (l.Ledger is null ? l.Loaded == 0m : l.Difference == 0m))).ToList());
    }

    /// <summary>Las cifras de referencia vigentes de un corte contra la propuesta del cuadre (FR-160).</summary>
    public static IReadOnlyList<ReferenceFigureComparisonDto> Cotejar(IEnumerable<CifraDeReferenciaBase> cifras, CalculoDeCuadre propuesta)
    {
        var porClave = propuesta.Lineas.ToDictionary(l => (l.Concepto, l.Dimension));
        return cifras.OrderBy(c => c.Concept, StringComparer.Ordinal).ThenBy(c => c.Dimension, StringComparer.Ordinal)
            .Select(c =>
            {
                porClave.TryGetValue((c.Concept, c.Dimension), out var l);
                return new ReferenceFigureComparisonDto(c.CutoffDate, c.Concept, c.Dimension, l?.Etiqueta, c.Amount, l?.Cargado, l?.Contable, (l?.Cargado ?? 0m) - c.Amount);
            }).ToList();
    }
}
