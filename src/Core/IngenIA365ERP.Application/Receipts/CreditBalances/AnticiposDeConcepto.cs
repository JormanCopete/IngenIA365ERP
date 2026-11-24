using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.CreditBalances;

/// <summary>Un anticipo disponible de la persona en un concepto, con el medio con que la caja lo cruza. (nuevo)</summary>
public sealed record AnticipoDisponibleDto(Guid MiscConceptPublicId, string ConceptCode, string ConceptName, decimal Balance, Guid? MeansPublicId, string? MeansCode,
    string? MeansName);

/// <summary>
/// Los anticipos por concepto (2026-11-23, pedido del dueño: la administración que el asociado paga antes de que se cause). Un concepto vario marcado
/// «anticipo» (<see cref="MiscConcept.IsAdvance"/>) no se gasta al cobrarse: cada aplicación suya en un recibo nace como saldo a favor de la persona con el
/// concepto (<see cref="PayerCreditBalanceOrigin.ConceptAdvance"/>) y se cruza después en la caja con el medio del concepto (<see cref="MiscConcept.AdvanceMeansId"/>),
/// de lo más antiguo a lo más nuevo. La contabilidad es la de siempre, por configuración: la matriz de Recaudo lleva el concepto (rol «Concepto vario») y el medio
/// (rol «Medio por cobrar») a la cuenta del anticipo. Anular el recibo que lo recibió lo cancela —si ya se cruzó, no se anula—, y anular el que lo cruzó lo
/// devuelve al anticipo. Un anticipo no se devuelve por orden ni por caja, ni alerta por antigüedad. Escribe en la unidad de trabajo de quien llama. (nuevo)
/// </summary>
public static class AnticiposDeConcepto
{
    public const string CodigoInsuficiente = "Receipts.Advance.Insufficient";
    public const string CodigoYaCruzado = "Receipts.Advance.AlreadyUsed";
    public const string CodigoNoSeDevuelve = "Receipts.Advance.NotRefundable";

    public static Error Insuficiente(string concepto, decimal disponible, decimal pedido) => new ErrorConDatos(CodigoInsuficiente,
        $"El anticipo de {concepto} tiene {disponible:N0} disponibles y se quieren cruzar {pedido:N0}.",
        new { concept = concepto, available = disponible, requested = pedido });

    public static Error YaCruzado(string recibo) => new(CodigoYaCruzado,
        $"El recibo {recibo} recibió un anticipo que ya se cruzó (todo o en parte): anule primero los recibos que lo cruzaron.");

    public static Error NoSeDevuelve() => new(CodigoNoSeDevuelve,
        "Un anticipo de concepto no se devuelve como saldo a favor: se cruza en la caja con su medio. Para reversarlo, anule el recibo que lo recibió.");

    /// <summary>Los conceptos de anticipo por el Id del medio con que se cruzan.</summary>
    public static async Task<Dictionary<int, MiscConcept>> PorMedioAsync(IApplicationDbContext db, CancellationToken ct) =>
        (await db.MiscConcepts.AsNoTracking().Where(c => c.IsAdvance && c.AdvanceMeansId != null).ToListAsync(ct))
            .GroupBy(c => c.AdvanceMeansId!.Value).ToDictionary(g => g.Key, g => g.First());

    /// <summary>Los anticipos abiertos de la persona, por concepto, con su medio.</summary>
    public static async Task<IReadOnlyList<AnticipoDisponibleDto>> DisponiblesAsync(IApplicationDbContext db, int persona, CancellationToken ct)
    {
        var saldos = await db.PayerCreditBalances.AsNoTracking()
            .Where(s => s.PersonId == persona && s.Status == PayerCreditBalanceState.Open && s.MiscConceptId != null && s.Balance > 0m
                        && (s.Origin == PayerCreditBalanceOrigin.ConceptAdvance || s.Origin == PayerCreditBalanceOrigin.MigratedAdvance))
            .GroupBy(s => s.MiscConceptId!.Value).Select(g => new { Concepto = g.Key, Saldo = g.Sum(s => s.Balance) }).ToListAsync(ct);
        if (saldos.Count == 0) return [];
        var ids = saldos.Select(s => s.Concepto).ToList();
        var conceptos = await db.MiscConcepts.AsNoTracking().IgnoreQueryFilters().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var medios = conceptos.Values.Where(c => c.AdvanceMeansId is not null).Select(c => c.AdvanceMeansId!.Value).Distinct().ToList();
        var deMedios = await db.PaymentMeans.AsNoTracking().IgnoreQueryFilters().Where(m => medios.Contains(m.Id)).ToDictionaryAsync(m => m.Id, ct);
        return saldos.Where(s => conceptos.ContainsKey(s.Concepto)).Select(s =>
        {
            var c = conceptos[s.Concepto];
            var m = c.AdvanceMeansId is { } mid ? deMedios.GetValueOrDefault(mid) : null;
            return new AnticipoDisponibleDto(c.PublicId, c.Code, c.Name, s.Saldo, m?.PublicId, m?.Code, m?.Name);
        }).OrderBy(a => a.ConceptCode, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Que lo que se cruza con cada medio de anticipo no pase de lo disponible de la persona en su concepto. <paramref name="usos"/> son los medios del recibo
    /// (su Id interno y su valor); los que no son de anticipo se ignoran.
    /// </summary>
    public static async Task<Result> ValidarUsoAsync(IApplicationDbContext db, int persona, IEnumerable<(int MedioId, decimal Valor)> usos, CancellationToken ct)
    {
        var porMedio = await PorMedioAsync(db, ct);
        var pedidos = usos.Where(u => porMedio.ContainsKey(u.MedioId)).GroupBy(u => porMedio[u.MedioId].Id)
            .Select(g => (Concepto: porMedio[g.First().MedioId], Valor: g.Sum(u => u.Valor))).ToList();
        foreach (var (concepto, valor) in pedidos)
        {
            var disponible = await db.PayerCreditBalances.AsNoTracking()
                .Where(s => s.PersonId == persona && s.MiscConceptId == concepto.Id && s.Status == PayerCreditBalanceState.Open && s.Balance > 0m)
                .SumAsync(s => (decimal?)s.Balance, ct) ?? 0m;
            if (valor > disponible) return Result.Failure(Insuficiente(concepto.Name, disponible, valor));
        }
        return Result.Success();
    }

    /// <summary>
    /// Al confirmar el recibo (ya guardadas sus aplicaciones y sus medios): reconoce un anticipo por cada aplicación de un concepto de anticipo y baja, de lo más
    /// antiguo a lo más nuevo, lo que se cruzó con cada medio de anticipo. <paramref name="persona"/> es el titular de la operación. Guarda (los movimientos
    /// necesitan el Id del saldo).
    /// </summary>
    public static async Task RegistrarAsync(IApplicationDbContext db, Receipt recibo, int persona, IReadOnlyCollection<ReceiptApplication> aplicaciones,
        IReadOnlyCollection<ReceiptMeans> medios, int? usuario, DateTime ahora, CancellationToken ct)
    {
        var deAplicaciones = aplicaciones.Where(a => a.MiscConceptId is not null).Select(a => a.MiscConceptId!.Value).Distinct().ToList();
        var anticipos = deAplicaciones.Count == 0
            ? new HashSet<int>()
            : (await db.MiscConcepts.AsNoTracking().Where(c => deAplicaciones.Contains(c.Id) && c.IsAdvance).Select(c => c.Id).ToListAsync(ct)).ToHashSet();
        var nuevos = new List<PayerCreditBalance>();
        foreach (var a in aplicaciones.Where(a => a.MiscConceptId is { } c && anticipos.Contains(c) && a.Amount > 0m))
        {
            var saldo = SaldosAFavor.Reconocer(db, a.BeneficiaryPersonId, PayerCreditBalanceOrigin.ConceptAdvance, recibo.PublicId, recibo.Number, recibo.BranchId,
                a.Amount, recibo.ValueDate, usuario, ahora);
            saldo.MiscConceptId = a.MiscConceptId;
            nuevos.Add(saldo);
        }

        var porMedio = await PorMedioAsync(db, ct);
        var cruces = medios.Where(m => porMedio.ContainsKey(m.PaymentMeansId)).GroupBy(m => porMedio[m.PaymentMeansId].Id)
            .Select(g => (Concepto: g.Key, Valor: g.Sum(m => m.Amount))).ToList();
        if (nuevos.Count == 0 && cruces.Count == 0) return;
        await db.SaveChangesAsync(ct);
        foreach (var saldo in nuevos) SaldosAFavor.Reconocido(db, saldo, usuario, ahora);

        foreach (var (concepto, valor) in cruces)
        {
            var pendiente = valor;
            var saldos = await db.PayerCreditBalances
                .Where(s => s.PersonId == persona && s.MiscConceptId == concepto && s.Status == PayerCreditBalanceState.Open && s.Balance > 0m)
                .OrderBy(s => s.OpenedOn).ThenBy(s => s.Id).ToListAsync(ct);
            foreach (var saldo in saldos)
            {
                if (pendiente <= 0m) break;
                var baja = Math.Min(saldo.Balance, pendiente);
                SaldosAFavor.Bajar(db, saldo, PayerCreditBalanceMovementKind.AppliedByReceipt, baja, recibo.PublicId, recibo.Number, usuario, ahora,
                    "Cruzado en la caja con su medio.");
                pendiente -= baja;
            }
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Antes de anular un recibo: el anticipo que recibió tiene que estar entero (si ya se cruzó, primero se anulan los recibos que lo cruzaron).
    /// </summary>
    public static async Task<Result> PuedeAnularAsync(IApplicationDbContext db, Receipt recibo, CancellationToken ct)
    {
        var cruzado = await db.PayerCreditBalances.AsNoTracking()
            .AnyAsync(s => s.Origin == PayerCreditBalanceOrigin.ConceptAdvance && s.OriginDocumentPublicId == recibo.PublicId && s.Balance < s.Amount, ct);
        return cruzado ? Result.Failure(YaCruzado(recibo.Number)) : Result.Success();
    }

    /// <summary>
    /// Al anular un recibo: cancela el anticipo que recibió y devuelve a cada anticipo lo que este recibo había cruzado. Sin guardar.
    /// </summary>
    public static async Task AlAnularAsync(IApplicationDbContext db, Receipt recibo, Guid anulacion, string numeroDeAnulacion, int? usuario, DateTime ahora,
        CancellationToken ct)
    {
        var recibidos = await db.PayerCreditBalances
            .Where(s => s.Origin == PayerCreditBalanceOrigin.ConceptAdvance && s.OriginDocumentPublicId == recibo.PublicId && s.Status == PayerCreditBalanceState.Open)
            .ToListAsync(ct);
        foreach (var saldo in recibidos.Where(s => s.Balance > 0m))
            SaldosAFavor.Bajar(db, saldo, PayerCreditBalanceMovementKind.VoidedWithReceipt, saldo.Balance, anulacion, numeroDeAnulacion, usuario, ahora,
                $"Anulado con el recibo {recibo.Number}.");

        // Lo que el módulo ya reversó y volvió al anticipo no vuelve dos veces.
        foreach (var (saldo, pendiente) in await CruzadoSinDevolverAsync(db, recibo, ct))
            Devolver(db, saldo, pendiente, PayerCreditBalanceMovementKind.ReopenedByVoiding, anulacion, numeroDeAnulacion, usuario, ahora,
                $"Se anuló el recibo {recibo.Number}, que lo había cruzado.");
    }

    /// <summary>
    /// Los conceptos de anticipo activos que cubren las obligaciones de una línea de Cartera (<see cref="MiscConcept.AdvanceCreditLineCode"/>), con su medio
    /// (cruce automático, 2026-11-23).
    /// </summary>
    public static async Task<IReadOnlyList<MiscConcept>> QueCubrenAsync(IApplicationDbContext db, string linea, CancellationToken ct)
    {
        var codigo = (linea ?? string.Empty).Trim().ToUpperInvariant();
        if (codigo.Length == 0) return [];
        return await db.MiscConcepts.AsNoTracking()
            .Where(c => c.IsAdvance && c.AdvanceMeansId != null && c.AdvanceCreditLineCode == codigo && c.Status == ReceiptsCatalogStatus.Active)
            .OrderBy(c => c.Code).ToListAsync(ct);
    }

    /// <summary>
    /// Cuando el módulo reversa una porción pagada con el medio de un anticipo (<paramref name="medioId"/>): devuelve al anticipo, hasta <paramref name="valor"/>,
    /// lo que el recibo había cruzado y no ha vuelto, de lo más nuevo a lo más antiguo. Devuelve lo que volvió. Sin guardar.
    /// </summary>
    public static async Task<decimal> DevolverPorReversoAsync(IApplicationDbContext db, Receipt recibo, int medioId, decimal valor, Guid reverso, string numero,
        int? usuario, DateTime ahora, CancellationToken ct)
    {
        var porMedio = await PorMedioAsync(db, ct);
        if (!porMedio.TryGetValue(medioId, out var concepto) || valor <= 0m) return 0m;
        var resta = valor;
        var pendientes = (await CruzadoSinDevolverAsync(db, recibo, ct)).Where(x => x.Saldo.MiscConceptId == concepto.Id)
            .OrderByDescending(x => x.Saldo.OpenedOn).ThenByDescending(x => x.Saldo.Id);
        foreach (var (saldo, pendiente) in pendientes)
        {
            if (resta <= 0m) break;
            var vuelve = Math.Min(pendiente, resta);
            Devolver(db, saldo, vuelve, PayerCreditBalanceMovementKind.ReturnedByModuleReversal, reverso, numero, usuario, ahora,
                $"El módulo reversó el pago del recibo {recibo.Number}, que lo había cruzado.");
            resta -= vuelve;
        }
        return valor - resta;
    }

    /// <summary>La nota con que queda resuelto el reverso de una porción pagada con el medio de un anticipo.</summary>
    public const string NotaDelReverso = "Se pagó con el medio de un anticipo: no hubo dinero, el valor vuelve al anticipo.";

    /// <summary>
    /// El módulo reversó una porción pagada con el medio de un anticipo: no hay dinero que confirmar, así que el reverso queda «vuelve al anticipo» y el valor
    /// vuelve (<see cref="DevolverPorReversoAsync"/>). El hecho <c>AplicacionReversadaPorModulo</c> lo emite quien llama, con la causa ya puesta: acredita el rol
    /// del medio, que la matriz lleva a la cuenta del anticipo. Sin guardar.
    /// </summary>
    public static async Task ResolverReversoAsync(IApplicationDbContext db, ModuleReversal reverso, Receipt recibo, ReceiptMeans medio, int? usuario, DateTime ahora,
        CancellationToken ct)
    {
        reverso.Cause = ModuleReversalCause.ReturnedToAdvance;
        reverso.ConfirmedByUserId = usuario;
        reverso.ConfirmedAt = ahora;
        reverso.ConfirmationNote = NotaDelReverso;
        await DevolverPorReversoAsync(db, recibo, medio.PaymentMeansId, reverso.Amount, reverso.PublicId, reverso.ModuleReversalNumber, usuario, ahora, ct);
    }

    /// <summary>Si el medio es el de un concepto de anticipo.</summary>
    public static async Task<bool> EsMedioDeAnticipoAsync(IApplicationDbContext db, int medioId, CancellationToken ct) =>
        await db.MiscConcepts.AsNoTracking().AnyAsync(c => c.IsAdvance && c.AdvanceMeansId == medioId, ct);

    /// <summary>
    /// Por saldo de anticipo, lo que el recibo cruzó y todavía no ha vuelto por un reverso del módulo (los movimientos <c>ReturnedByModuleReversal</c> de los
    /// reversos de sus aplicaciones). Los saldos vienen rastreados.
    /// </summary>
    private static async Task<List<(PayerCreditBalance Saldo, decimal Pendiente)>> CruzadoSinDevolverAsync(IApplicationDbContext db, Receipt recibo,
        CancellationToken ct)
    {
        var cruzados = await (from m in db.PayerCreditBalanceMovements
                              join s in db.PayerCreditBalances on m.PayerCreditBalanceId equals s.Id
                              where m.Kind == PayerCreditBalanceMovementKind.AppliedByReceipt && m.DocumentPublicId == recibo.PublicId && s.MiscConceptId != null
                              select new { m.Amount, Saldo = s }).ToListAsync(ct);
        if (cruzados.Count == 0) return [];
        var reversos = await (from r in db.ModuleReversals
                              join a in db.ReceiptApplications on r.ReceiptApplicationId equals a.Id
                              where a.ReceiptId == recibo.Id
                              select r.PublicId).ToListAsync(ct);
        var devueltos = reversos.Count == 0
            ? []
            : (await db.PayerCreditBalanceMovements.AsNoTracking()
                .Where(m => m.Kind == PayerCreditBalanceMovementKind.ReturnedByModuleReversal && m.DocumentPublicId != null && reversos.Contains(m.DocumentPublicId.Value))
                .Select(m => new { m.PayerCreditBalanceId, m.Amount }).ToListAsync(ct))
                .GroupBy(m => m.PayerCreditBalanceId).ToDictionary(g => g.Key, g => g.Sum(m => m.Amount));
        return cruzados.GroupBy(c => c.Saldo.Id)
            .Select(g => (g.First().Saldo, Pendiente: g.Sum(c => c.Amount) - devueltos.GetValueOrDefault(g.Key)))
            .Where(x => x.Pendiente > 0m).ToList();
    }

    private static void Devolver(IApplicationDbContext db, PayerCreditBalance saldo, decimal valor, PayerCreditBalanceMovementKind clase, Guid documento,
        string numero, int? usuario, DateTime ahora, string nota)
    {
        saldo.Balance += valor;
        saldo.Status = PayerCreditBalanceState.Open;
        db.PayerCreditBalanceMovements.Add(new PayerCreditBalanceMovement
        {
            PayerCreditBalanceId = saldo.Id, Kind = clase, Amount = valor, DocumentPublicId = documento, DocumentNumber = numero, RecordedByUserId = usuario,
            RecordedAt = ahora, Note = nota,
        });
    }
}
