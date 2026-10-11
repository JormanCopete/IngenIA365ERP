using System.Diagnostics;
using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Domain.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Ledger;

/// <summary>Una diferencia del libro contra la reconstrucción (api.md §16.5). (nuevo)</summary>
public sealed record LedgerMismatchDto(Guid LoanPublicId, string Number, DateOnly Date, string Concept, int? Installment, decimal Ledger, decimal Reconstruction);

/// <summary>El resultado de verificar el libro (api.md §16.5): cuántos créditos, hasta qué día, las diferencias y lo que tardó. (nuevo)</summary>
public sealed record LedgerIntegrityDto(int Checked, DateOnly? UpTo, IReadOnlyList<LedgerMismatchDto> Mismatches, long DurationMs);

/// <summary>Lo que dejó una verificación (la consulta y la muestra del paso (8) del ciclo). (nuevo)</summary>
public sealed record ResultadoDeVerificacion(int Revisados, DateOnly? Hasta, IReadOnlyList<LedgerMismatchDto> Diferencias, long DuracionMs);

/// <summary>
/// La verificación del libro de Cartera (feature 013, US4, T0505; data-model §5.3, T33): para cada crédito, Σ libro por (día valor, concepto,
/// cuota) = la reconstrucción con la información de ahora, para todo día hasta su marca de agua (<see cref="MotorDeCartera.Verificar"/>; día por
/// día es más fuerte que el acumulado). Completa o sobre una muestra repartida en el orden de los créditos; nunca corrige nada: cada crédito con
/// diferencias levanta <c>Cartera.LibroDescuadrado</c> (bloque 8C; <c>DedupKey …:{crédito}</c>), para cartera y la contadora. (nuevo)
/// </summary>
public sealed class VerificacionDelLibro(IApplicationDbContext db, CargadorDeReconstruccion cargador, IAlertas alertas)
{
    public async Task<ResultadoDeVerificacion> VerificarAsync(IReadOnlyCollection<int>? loanIds, int? muestra, DateOnly? hasta, CancellationToken ct)
    {
        var reloj = Stopwatch.StartNew();
        var controles = db.LoanControls.AsNoTracking();
        if (loanIds is not null) controles = controles.Where(c => loanIds.Contains(c.LoanId));
        var todos = await controles.OrderBy(c => c.LoanId).Select(c => new { c.LoanId, c.LastAccruedDate }).ToListAsync(ct);
        var elegidos = muestra is { } n && n > 0 && todos.Count > n
            ? todos.Where((_, i) => i % (todos.Count / n) == 0).Take(n).ToList()
            : todos;

        var diferencias = new List<LedgerMismatchDto>();
        DateOnly? hastaVerificado = null;
        foreach (var grupo in elegidos.Chunk(100))
        {
            var maximo = grupo.Max(c => hasta is { } h && h < c.LastAccruedDate ? h : c.LastAccruedDate);
            var cargados = await cargador.CargarAsync(grupo.Select(c => c.LoanId).ToList(), null, maximo, ct);
            var libros = await cargador.LibroAsync(grupo.Select(c => c.LoanId).ToList(), maximo, ct);
            foreach (var c in grupo)
            {
                if (!cargados.TryGetValue(c.LoanId, out var cargado) || !cargado.Listo) continue;
                var tope = hasta is { } h && h < c.LastAccruedDate ? h : c.LastAccruedDate;
                hastaVerificado = hastaVerificado is { } v && v > tope ? v : tope;
                IReadOnlyList<Domain.Lending.Replay.DiferenciaDeLibro> suyas;
                try
                {
                    suyas = MotorDeCartera.Verificar(cargado.Entrada!, tope, libros[c.LoanId]);
                }
                catch (ReglaDeCarteraException)
                {
                    // No se puede reconstruir (una operación que esta versión no interpreta): no se compara; el ciclo ya lo dejó en sus errores.
                    continue;
                }
                if (suyas.Count == 0) continue;
                diferencias.AddRange(suyas.Select(x => new LedgerMismatchDto(cargado.Credito.PublicId, cargado.Credito.Number, x.Dia, x.Concepto.ToString(),
                    x.Cuota, x.Libro, x.Reconstruccion)));
                await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.LibroDescuadrado, $"Libro del crédito {cargado.Credito.Number} descuadrado",
                    $"El libro del crédito {cargado.Credito.Number} no cuadra con su reconstrucción en {suyas.Count} día(s) y concepto(s), desde el "
                    + $"{Origination.ErroresDeOriginacion.Fecha(suyas[0].Dia)}. Nada se corrigió: revise las operaciones del crédito con soporte.",
                    EntityType: "Loan", EntityPublicId: cargado.Credito.PublicId,
                    DedupKey: $"{TiposDeAlerta.LibroDescuadrado}:{cargado.Credito.PublicId:D}", ModuloDeOrigen: "Lending"), ct);
            }
        }
        return new ResultadoDeVerificacion(elegidos.Count, hastaVerificado, diferencias, reloj.ElapsedMilliseconds);
    }
}

/// <summary>
/// <c>POST /api/lending/integrity/verify</c> (feature 013, US4, T0505; api.md §16.5, <c>Lending.Integrity.Verify</c>): verifica el libro de los
/// créditos pedidos, de una muestra o de todos, hasta <see cref="UpTo"/> (o la marca de cada crédito). Sin clave de idempotencia: no corrige nada;
/// una diferencia levanta la alerta. (nuevo)
/// </summary>
public sealed record VerifyLendingLedgerIntegrityQuery(IReadOnlyList<Guid>? LoanPublicIds = null, int? SampleSize = null, DateOnly? UpTo = null)
    : IRequest<Result<LedgerIntegrityDto>>;

public sealed class VerifyLendingLedgerIntegrityQueryValidator : AbstractValidator<VerifyLendingLedgerIntegrityQuery>
{
    public VerifyLendingLedgerIntegrityQueryValidator()
    {
        RuleFor(x => x.SampleSize).GreaterThan(0).When(x => x.SampleSize is not null).WithMessage("El tamaño de la muestra debe ser mayor que cero.");
        RuleFor(x => x.LoanPublicIds).Must(l => l is null || l.Count <= 1000).WithMessage("Se verifican a lo sumo mil créditos por pedido.");
    }
}

public sealed class VerifyLendingLedgerIntegrityQueryHandler(IApplicationDbContext db, VerificacionDelLibro verificacion)
    : IRequestHandler<VerifyLendingLedgerIntegrityQuery, Result<LedgerIntegrityDto>>
{
    public async Task<Result<LedgerIntegrityDto>> Handle(VerifyLendingLedgerIntegrityQuery request, CancellationToken ct)
    {
        IReadOnlyCollection<int>? ids = null;
        if (request.LoanPublicIds is { Count: > 0 } publicos)
            ids = await db.Loans.AsNoTracking().Where(l => publicos.Contains(l.PublicId)).Select(l => l.Id).ToListAsync(ct);
        var r = await verificacion.VerificarAsync(ids, request.SampleSize, request.UpTo, ct);
        return Result.Success(new LedgerIntegrityDto(r.Revisados, r.Hasta, r.Diferencias, r.DuracionMs));
    }
}
