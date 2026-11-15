using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Sales.Payments;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Sales;

/// <summary>Los datos del cheque que llegan con un pago de venta (feature 022, T044; FR-048): el banco librado va por <c>PublicId</c>. (nuevo)</summary>
public sealed record PaymentCheckInput(
    Guid? BankPublicId,
    string? Number,
    string? AccountNumber,
    DateOnly? IssueDate,
    bool SamePlace = false,
    string? DrawerName = null);

/// <summary>Una cuenta propia que el cajero puede elegir para un medio en un punto (y caja). (nuevo)</summary>
public sealed record CuentaPropiaElegible(int PaymentMeansId, Guid PaymentMeansPublicId, Guid OwnBankAccountPublicId, string AccountLabel);

/// <summary>Dónde se cobró un pago: el punto de venta y la caja (nula en la habilitación «todas las cajas»). (nuevo)</summary>
public sealed record LugarDelPago(int PointOfSaleId, int? CashRegisterId);

/// <summary>
/// Las cuentas propias de las transferencias y consignaciones de una venta (feature 022, T044; FR-047, Q16), como <c>CuentasDeTransferencia</c>
/// en Recaudo: con cuentas habilitadas para el medio en el punto (y caja) del pago, la cuenta es obligatoria —la única se propone sola, una no
/// habilitada se rechaza—; sin cuentas habilitadas, el pago sigue como antes y contabiliza por el medio. Las cuentas se leen de
/// <c>INV_TransferAccounts</c> (la caja no consulta Tesorería: su alcance falla cerrado para un cajero). Lo usan el cobro del POS, el borrador de
/// oficina y la confirmación. (nuevo)
/// </summary>
public static class CuentasPropiasDeVenta
{
    public const string CodigoRequerida = "Payments.OwnBankAccountRequired";
    public const string CodigoNoHabilitada = "Payments.OwnBankAccountNotEnabled";

    /// <summary>Transferencia y consignación entran a una cuenta bancaria propia.</summary>
    public static bool EntraAUnaCuenta(PaymentMeansClass clase) => clase is PaymentMeansClass.Transfer or PaymentMeansClass.BankDeposit;

    public static Error Requerida(string medio, int indice, IReadOnlyList<CuentaPropiaElegible> opciones) => new ErrorConDatos(CodigoRequerida,
        $"Elija la cuenta bancaria propia a la que entró el pago con {medio}.",
        new
        {
            paymentIndex = indice,
            paymentMeansCode = medio,
            options = opciones.Select(o => new { o.OwnBankAccountPublicId, o.AccountLabel }).ToList(),
        });

    public static Error NoHabilitada(string medio, int indice) => new ErrorConDatos(CodigoNoHabilitada,
        $"La cuenta propia elegida no está habilitada para {medio} en este punto de venta.", new { paymentIndex = indice, paymentMeansCode = medio });

    /// <summary>Las cuentas habilitadas y activas en el punto para la caja (las de «todas las cajas» y las de esa caja).</summary>
    public static async Task<IReadOnlyList<CuentaPropiaElegible>> OpcionesAsync(IApplicationDbContext db, int puntoId, int? cajaId, CancellationToken ct) =>
        await (from t in db.SalesTransferAccounts.AsNoTracking()
               join m in db.PaymentMeans.AsNoTracking().IgnoreQueryFilters() on t.PaymentMeansId equals m.Id
               where t.PointOfSaleId == puntoId && t.IsActive && !t.IsDeleted && (t.CashRegisterId == null || t.CashRegisterId == cajaId)
               orderby t.AccountLabel
               select new CuentaPropiaElegible(m.Id, m.PublicId, t.OwnBankAccountPublicId, t.AccountLabel))
            .Distinct().ToListAsync(ct);

    /// <summary>
    /// El punto y la caja del pago: los del documento en el POS; en oficina, los de la sesión de caja del pago. Nulo si no se sabe (un medio que
    /// no se arquea, sin sesión): entonces no hay cuentas habilitadas que exigir.
    /// </summary>
    public static async Task<LugarDelPago?> LugarAsync(IApplicationDbContext db, InventoryDocument documento, int? sesionId, CancellationToken ct)
    {
        if (documento.PointOfSaleId is int punto) return new LugarDelPago(punto, documento.CashRegisterId);
        if (sesionId is not int s) return null;
        var sesion = await db.CashSessions.AsNoTracking().Where(x => x.Id == s).Select(x => new { x.PointOfSaleId, x.CashRegisterId }).FirstOrDefaultAsync(ct);
        return sesion is null ? null : new LugarDelPago(sesion.PointOfSaleId, sesion.CashRegisterId);
    }

    /// <summary>
    /// La cuenta del pago <paramref name="indice"/>: nula si el medio no entra a una cuenta o el punto no tiene habilitadas para él; la elegida si
    /// está habilitada; la única si no eligió. Falla con <see cref="CodigoNoHabilitada"/> o <see cref="CodigoRequerida"/>.
    /// </summary>
    public static Result<CuentaPropiaElegible?> Resolver(int medioId, string medioCodigo, PaymentMeansClass clase, Guid? elegida,
        IReadOnlyList<CuentaPropiaElegible> opciones, int indice)
    {
        if (!EntraAUnaCuenta(clase)) return Result.Success<CuentaPropiaElegible?>(null);
        var delMedio = opciones.Where(o => o.PaymentMeansId == medioId).ToList();
        if (elegida is { } cuenta)
        {
            var habilitada = delMedio.FirstOrDefault(o => o.OwnBankAccountPublicId == cuenta);
            return habilitada is null
                ? Result.Failure<CuentaPropiaElegible?>(NoHabilitada(medioCodigo, indice))
                : Result.Success<CuentaPropiaElegible?>(habilitada);
        }
        return delMedio.Count switch
        {
            0 => Result.Success<CuentaPropiaElegible?>(null),
            1 => Result.Success<CuentaPropiaElegible?>(delMedio[0]),
            _ => Result.Failure<CuentaPropiaElegible?>(Requerida(medioCodigo, indice, delMedio)),
        };
    }

    /// <summary>Los datos del cheque para el validador puro, con el banco ya resuelto a su <c>Id</c>.</summary>
    public static DatosDeCheque? Cheque(DocumentPayment pago) => pago.MeansClass != PaymentMeansClass.Check
        ? null
        : new DatosDeCheque(pago.CheckBankId, pago.CheckNumber, pago.CheckAccountNumber, pago.CheckIssueDate, pago.CheckSamePlace ?? false, pago.CheckDrawerName);

    /// <summary>Los bancos librados que nombran los cheques de las entradas: <c>PublicId → (Id, nombre)</c>.</summary>
    public static async Task<IReadOnlyDictionary<Guid, (int Id, string Name)>> BancosAsync(IApplicationDbContext db, IEnumerable<DocumentPaymentInput> entradas,
        CancellationToken ct)
    {
        var ids = entradas.Select(e => e.Check?.BankPublicId).OfType<Guid>().Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, (int, string)>();
        return await db.Banks.AsNoTracking().Where(b => ids.Contains(b.PublicId)).ToDictionaryAsync(b => b.PublicId, b => (b.Id, b.Name), ct);
    }

    /// <summary>La referencia del pago: la digitada o, en un cheque, su número (si el medio pide referencia, es el número del cheque).</summary>
    public static string? Referencia(DocumentPaymentInput e, PaymentMeansClass clase) =>
        !string.IsNullOrWhiteSpace(e.Reference) ? e.Reference
        : clase == PaymentMeansClass.Check && !string.IsNullOrWhiteSpace(e.Check?.Number) ? e.Check.Number.Trim()
        : null;

    /// <summary>Los datos del cheque de una entrada para el validador puro (nulos fuera de la clase cheque).</summary>
    public static DatosDeCheque? Cheque(DocumentPaymentInput e, PaymentMeansClass clase, IReadOnlyDictionary<Guid, (int Id, string Name)> bancos)
    {
        if (clase != PaymentMeansClass.Check) return null;
        var c = e.Check;
        int? banco = c?.BankPublicId is { } b && bancos.TryGetValue(b, out var x) ? x.Id : null;
        return new DatosDeCheque(banco, Texto(c?.Number, 20), Texto(c?.AccountNumber, 30), c?.IssueDate, c?.SamePlace ?? false, Texto(c?.DrawerName, 200));
    }

    /// <summary>Escribe en el pago los datos del cheque (sólo en clase cheque); un banco desconocido queda nulo y el validador lo pide.</summary>
    public static void AplicarCheque(DocumentPayment pago, DocumentPaymentInput e, IReadOnlyDictionary<Guid, (int Id, string Name)> bancos)
    {
        if (pago.MeansClass != PaymentMeansClass.Check || e.Check is not { } c) return;
        if (c.BankPublicId is { } b && bancos.TryGetValue(b, out var banco))
        {
            pago.CheckBankId = banco.Id;
            pago.CheckBankName = Recortar(banco.Name, 100);
        }
        pago.CheckNumber = Texto(c.Number, 20);
        pago.CheckAccountNumber = Texto(c.AccountNumber, 30);
        pago.CheckIssueDate = c.IssueDate;
        pago.CheckSamePlace = c.SamePlace;
        pago.CheckDrawerName = Texto(c.DrawerName, 200);
    }

    /// <summary>Pone en el pago la cuenta propia resuelta (o la quita).</summary>
    public static void AplicarCuenta(DocumentPayment pago, CuentaPropiaElegible? cuenta)
    {
        pago.OwnBankAccountPublicId = cuenta?.OwnBankAccountPublicId;
        pago.OwnBankAccountLabel = cuenta is null ? null : Recortar(cuenta.AccountLabel, 150);
    }

    private static string? Texto(string? s, int largo) => string.IsNullOrWhiteSpace(s) ? null : Recortar(s.Trim(), largo);

    private static string Recortar(string s, int largo) => s.Length <= largo ? s : s[..largo];
}
