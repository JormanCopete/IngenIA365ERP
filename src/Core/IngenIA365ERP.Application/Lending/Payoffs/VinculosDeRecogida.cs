using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Payoffs;

/// <summary>
/// Un producto que recogió el desembolso de un crédito nuevo (feature 028, T054; FR-029, HU8): el crédito nuevo, el producto recogido, la operación del
/// desembolso y su fecha valor, el modo, lo aplicado, si se reversó (el crédito nuevo se anuló antes del giro) y la operación de pago del recogido. (nuevo)
/// </summary>
public sealed record VinculoDeRecogida(
    int CreditoNuevoId,
    Guid CreditoNuevoPublicId,
    string CreditoNuevoNumero,
    PayoffProductKind Clase,
    int? RecogidoId,
    Guid? RecogidoPublicId,
    string NumeroDelProducto,
    long OperacionDeDesembolsoId,
    DateOnly Fecha,
    PayoffMode Modo,
    decimal Aplicado,
    bool Reversada,
    long? OperacionDePagoId)
{
    /// <summary>2026-11-27 (feature 028, E2, T066, T072): la cuota causada que la recogida dejó a su canal; nula si recogió todo. (nuevo)</summary>
    public int? CuotaDejada { get; init; }

    /// <summary>2026-11-27 (feature 028, E2): lo que la cuota dejada debe al vencer. (nuevo)</summary>
    public decimal? ValorDejado { get; init; }

    /// <summary>2026-11-27 (feature 028, E2): cuándo quedó pagada la cuota dejada; nula mientras se deba. (nuevo)</summary>
    public DateTime? CuotaDejadaPagadaEn { get; init; }
}

/// <summary>
/// <b>Quién recogió a quién</b> (feature 028, T054; FR-029): lo leen el detalle del crédito («Recogido por el crédito … el …» y «Recogió: …»), el libro del
/// crédito recogido («Recogido con el crédito …») y el informe de movimientos. Lee las filas <c>LND_DisbursementPayoffs</c> y, para los desembolsos de antes
/// de la 028 que no las tienen, los pagos <c>CAR</c> con la clave de la recogida (<c>CAR:{desembolso:N}:{obligación:N}</c>,
/// <see cref="RecogidaEnElDesembolso.DesembolsoDeLaClave"/>): un pago con fila no se cuenta dos veces (las dos llevan la misma clave). Un producto omitido
/// (ya estaba cancelado) no es un vínculo. Sólo lectura; no aplica el alcance: lo aplica quien llama, sobre el crédito que muestra. (nuevo)
/// </summary>
public static class VinculosDeRecogida
{
    private static readonly string PrefijoDeLaClave = $"{ClavesDeRecaudador.Cartera}:";

    /// <summary>Los desembolsos que recogieron a <paramref name="recogidos"/>, del más antiguo al más nuevo.</summary>
    public static async Task<IReadOnlyList<VinculoDeRecogida>> DeLosRecogidosAsync(IApplicationDbContext db, IReadOnlyCollection<int> recogidos,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        if (recogidos.Count == 0) return [];
        var filas = await FilasAsync(db, db.DisbursementPayoffs.AsNoTracking().Where(f => f.LoanId != null && recogidos.Contains(f.LoanId.Value)), ct);
        var pagos = await PagosAsync(db, db.LoanOperations.AsNoTracking().Where(o => recogidos.Contains(o.LoanId)), ct);
        return await UnirAsync(db, filas, pagos, ct);
    }

    /// <summary>Lo que recogieron los desembolsos de <paramref name="nuevos"/>, en el orden en que se aplicó.</summary>
    public static async Task<IReadOnlyList<VinculoDeRecogida>> DeLosNuevosAsync(IApplicationDbContext db, IReadOnlyCollection<int> nuevos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        if (nuevos.Count == 0) return [];
        var desembolsos = await db.LoanOperations.AsNoTracking()
            .Where(o => nuevos.Contains(o.LoanId) && o.Kind == LoanOperationKind.Disbursement)
            .Select(o => new { o.Id, o.PublicId }).ToListAsync(ct);
        if (desembolsos.Count == 0) return [];
        var ids = desembolsos.Select(o => o.Id).ToList();
        var filas = await FilasAsync(db, db.DisbursementPayoffs.AsNoTracking().Where(f => ids.Contains(f.DisbursementOperationId)), ct);
        var pagos = new List<PagoDeRecogida>();
        foreach (var publico in desembolsos.Select(o => o.PublicId).Distinct())
        {
            var prefijo = $"{ClavesDeRecaudador.Cartera}:{publico:N}:";
            pagos.AddRange(await PagosAsync(db, db.LoanOperations.AsNoTracking()
                .Where(o => o.IdempotencyKey != null && o.IdempotencyKey.StartsWith(prefijo)), ct));
        }
        return await UnirAsync(db, filas, pagos, ct);
    }

    /// <summary>Por operación de pago: las de <paramref name="operaciones"/> que son la recogida de un crédito con el desembolso de otro.</summary>
    public static async Task<IReadOnlyDictionary<long, VinculoDeRecogida>> DeLosPagosAsync(IApplicationDbContext db, IReadOnlyCollection<long> operaciones,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        if (operaciones.Count == 0) return new Dictionary<long, VinculoDeRecogida>();
        var pagos = await PagosAsync(db, db.LoanOperations.AsNoTracking().Where(o => operaciones.Contains(o.Id)), ct);
        if (pagos.Count == 0) return new Dictionary<long, VinculoDeRecogida>();
        var claves = pagos.Select(p => p.Clave).ToList();
        var filas = await FilasAsync(db, db.DisbursementPayoffs.AsNoTracking().Where(f => claves.Contains(f.IdempotencyKey)), ct);
        return (await UnirAsync(db, filas, pagos, ct)).Where(v => v.OperacionDePagoId is not null)
            .GroupBy(v => v.OperacionDePagoId!.Value).ToDictionary(g => g.Key, g => g.First());
    }

    // ------------------------------------------------------------------------------------------------------------------------------- piezas --

    private sealed record FilaDeRecogida(long DesembolsoId, int CreditoNuevoId, DateOnly Fecha, PayoffProductKind Clase, int? RecogidoId, string Numero,
        PayoffMode Modo, decimal Aplicado, bool Reversada, long? PagoId, string Clave, int? CuotaDejada, decimal? ValorDejado, DateTime? CuotaDejadaPagadaEn);

    private sealed record PagoDeRecogida(long Id, int RecogidoId, string Clave, decimal Aplicado, PaymentModality? Modalidad, bool Reversado);

    /// <summary>Las filas no omitidas con la operación de su desembolso (el crédito nuevo y la fecha valor).</summary>
    private static async Task<List<FilaDeRecogida>> FilasAsync(IApplicationDbContext db,
        IQueryable<Domain.Entities.Lending.Payoffs.DisbursementPayoff> consulta, CancellationToken ct) =>
        await (from f in consulta
               join o in db.LoanOperations.AsNoTracking() on f.DisbursementOperationId equals o.Id
               where !f.Omitted && !f.IsDeleted
               orderby f.Id
               select new FilaDeRecogida(o.Id, o.LoanId, o.ValueDate, f.ProductKind, f.LoanId, f.ProductNumber, f.Mode, f.AppliedAmount,
                   f.ReversedAt != null, f.PaymentOperationId, f.IdempotencyKey, f.LeftInstallmentNumber, f.LeftInstallmentAmount, f.LeftInstallmentSettledAt))
            .ToListAsync(ct);

    /// <summary>Los pagos de Cartera con una clave que empieza como la de una recogida (la gramática completa la mira <see cref="UnirAsync"/>).</summary>
    private static async Task<List<PagoDeRecogida>> PagosAsync(IApplicationDbContext db, IQueryable<Domain.Entities.Lending.Operations.LoanOperation> consulta,
        CancellationToken ct) =>
        (await consulta.Where(o => o.Kind == LoanOperationKind.Payment && o.CollectorCode == ClavesDeRecaudador.Cartera && o.IdempotencyKey != null
                                   && o.IdempotencyKey.StartsWith(PrefijoDeLaClave))
            .OrderBy(o => o.Id)
            .Select(o => new { o.Id, o.LoanId, o.IdempotencyKey, o.Amount, o.ExcessAmount, o.PaymentModality, Reversado = o.ReversedByOperationId != null })
            .ToListAsync(ct))
        .Where(o => RecogidaEnElDesembolso.DesembolsoDeLaClave(o.IdempotencyKey) is not null)
        .Select(o => new PagoDeRecogida(o.Id, o.LoanId, o.IdempotencyKey!, o.Amount - (o.ExcessAmount ?? 0m), o.PaymentModality, o.Reversado))
        .ToList();

    /// <summary>Las filas y, de los pagos sin fila (los de antes de la 028), el desembolso que nombra su clave; con los números de los créditos.</summary>
    private static async Task<IReadOnlyList<VinculoDeRecogida>> UnirAsync(IApplicationDbContext db, List<FilaDeRecogida> filas, List<PagoDeRecogida> pagos,
        CancellationToken ct)
    {
        var porClave = pagos.GroupBy(p => p.Clave, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var conFila = filas.Select(f => f.Clave).ToHashSet(StringComparer.Ordinal);
        var sueltos = pagos.Where(p => !conFila.Contains(p.Clave)).DistinctBy(p => p.Id).ToList();
        var publicos = sueltos.Select(p => RecogidaEnElDesembolso.DesembolsoDeLaClave(p.Clave)!.Value).Distinct().ToList();
        var desembolsos = await db.LoanOperations.AsNoTracking().Where(o => publicos.Contains(o.PublicId) && o.Kind == LoanOperationKind.Disbursement)
            .Select(o => new { o.Id, o.PublicId, o.LoanId, o.ValueDate }).ToListAsync(ct);
        var desembolsoPorPublico = desembolsos.ToDictionary(o => o.PublicId);

        var creditos = filas.Select(f => f.CreditoNuevoId).Concat(filas.Select(f => f.RecogidoId).OfType<int>())
            .Concat(desembolsos.Select(o => o.LoanId)).Concat(sueltos.Select(p => p.RecogidoId)).Distinct().ToList();
        var numeros = creditos.Count == 0
            ? new Dictionary<int, (Guid PublicId, string Number)>()
            : (await db.Loans.AsNoTracking().IgnoreQueryFilters().Where(l => creditos.Contains(l.Id))
                    .Select(l => new { l.Id, l.PublicId, l.Number }).ToListAsync(ct))
                .ToDictionary(l => l.Id, l => (l.PublicId, l.Number));
        (Guid PublicId, string Number) Credito(int id) => numeros.TryGetValue(id, out var c) ? c : (Guid.Empty, string.Empty);

        var vinculos = new List<(long Orden, VinculoDeRecogida Vinculo)>();
        foreach (var f in filas)
        {
            var nuevo = Credito(f.CreditoNuevoId);
            var pago = porClave.GetValueOrDefault(f.Clave);
            Guid? recogido = f.RecogidoId is { } r ? Credito(r).PublicId : null;
            vinculos.Add((f.PagoId ?? pago?.Id ?? long.MaxValue, new VinculoDeRecogida(f.CreditoNuevoId, nuevo.PublicId, nuevo.Number, f.Clase, f.RecogidoId,
                recogido, f.Numero, f.DesembolsoId, f.Fecha, f.Modo, f.Aplicado, f.Reversada || pago?.Reversado == true, f.PagoId ?? pago?.Id)
            {
                CuotaDejada = f.CuotaDejada, ValorDejado = f.ValorDejado, CuotaDejadaPagadaEn = f.CuotaDejadaPagadaEn,
            }));
        }
        foreach (var p in sueltos)
        {
            if (!desembolsoPorPublico.TryGetValue(RecogidaEnElDesembolso.DesembolsoDeLaClave(p.Clave)!.Value, out var d)) continue;
            var nuevo = Credito(d.LoanId);
            var recogido = Credito(p.RecogidoId);
            vinculos.Add((p.Id, new VinculoDeRecogida(d.LoanId, nuevo.PublicId, nuevo.Number, PayoffProductKind.Loan, p.RecogidoId, recogido.PublicId,
                recogido.Number, d.Id, d.ValueDate, p.Modalidad == PaymentModality.FullPayoff ? PayoffMode.Total : PayoffMode.Partial, p.Aplicado,
                p.Reversado, p.Id)));
        }
        return vinculos.OrderBy(v => v.Vinculo.Fecha).ThenBy(v => v.Orden).Select(v => v.Vinculo).ToList();
    }
}

/// <summary>Los textos de un vínculo de recogida (feature 028, T054; FR-029): los mismos en el libro, el informe de movimientos y el detalle. (nuevo)</summary>
public static class TextosDeVinculosDeRecogida
{
    /// <summary>«Recogido con el crédito {número}», en el pago del crédito recogido; «(reversado)» si el crédito nuevo se anuló antes del giro.</summary>
    public static string RecogidoCon(VinculoDeRecogida v)
    {
        ArgumentNullException.ThrowIfNull(v);
        return $"Recogido con el crédito {v.CreditoNuevoNumero}" + (v.Reversada ? " (reversado al anular el crédito)" : string.Empty);
    }

    /// <summary>«Recogido por el crédito {número} el {fecha}».</summary>
    public static string RecogidoPor(VinculoDeRecogida v)
    {
        ArgumentNullException.ThrowIfNull(v);
        return $"Recogido por el crédito {v.CreditoNuevoNumero} el {v.Fecha:dd/MM/yyyy}" + (v.Reversada ? " (reversado al anular el crédito)" : string.Empty);
    }
}
