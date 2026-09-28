using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Common;

/// <summary>
/// Las unicidades de la venta y la caja que garantiza la base (feature 012, I3, T589; T50; data-model §15 «Concurrencia» y §16;
/// api.md §19 y §21), con el molde de <c>PersonFactory.EsColisionDeDocumento</c>: una sesión abierta por caja
/// (<see cref="IndiceDeLaCaja"/>) y por cajero exclusivo (<see cref="IndiceDelCajero"/>), un cierre del día por punto y fecha
/// (<see cref="IndiceDelCierreDelDia"/>) y un bono de número único activo por medio (<see cref="IndiceDelBono"/>). Los comandos
/// comprueban antes (<see cref="CajaOcupadaAsync"/>, <see cref="CajeroOcupadoAsync"/>, <see cref="DiaCerradoAsync"/>,
/// <see cref="BonoUsadoAsync"/>); si dos personas llegan a la vez, el segundo <c>SaveChanges</c> choca con el índice y
/// <see cref="TraducirAsync"/> lo convierte en el mismo error, nombrando lo que ganó. Los mensajes de PostgreSQL («duplicate
/// key value violates unique constraint "…"») y de SQL Server («…with unique index '…'») traen el nombre del índice. (nuevo)
/// </summary>
public static class ColisionesDeVenta
{
    public const string IndiceDelBono = "UK_INV_VoucherRedemptions_Means_Number_Active";

    public const string IndiceDeLaCaja = "UK_INV_CashSessions_Register_Open";

    public const string IndiceDelCajero = "UK_INV_CashSessions_Cashier_Open";

    public const string IndiceDelCierreDelDia = "UK_INV_DayCloses_Point_Date_Closed";

    private static readonly string[] Todos = [IndiceDelBono, IndiceDeLaCaja, IndiceDelCajero, IndiceDelCierreDelDia];

    /// <summary>El índice de la venta o la caja que violó la excepción, o nulo si es otra cosa.</summary>
    public static string? Indice(DbUpdateException ex)
    {
        for (Exception? actual = ex; actual is not null; actual = actual.InnerException)
        {
            foreach (var indice in Todos)
                if (actual.Message.Contains(indice, StringComparison.OrdinalIgnoreCase)) return indice;
        }
        return null;
    }

    /// <summary>¿La excepción es la violación de una de las unicidades de la venta o la caja?</summary>
    public static bool Es(DbUpdateException ex) => Indice(ex) is not null;

    /// <summary>
    /// El error de negocio de la carrera, nombrando lo que quedó; nulo si la excepción es de otro índice (el que llama la
    /// relanza). Descarta lo pendiente del contexto: la transacción que chocó no se reintenta con el mismo rastreador.
    /// </summary>
    public static async Task<Error?> TraducirAsync(IApplicationDbContext db, DbUpdateException ex, DatosDeLaColision datos, CancellationToken ct)
    {
        var indice = Indice(ex);
        if (indice is null) return null;
        db.DescartarCambios();
        return indice switch
        {
            IndiceDelBono => await PrimerBonoUsadoAsync(db, datos.Bonos ?? [], ct) ?? BonoYaUsado(null),
            IndiceDeLaCaja => (datos.CashRegisterId is { } caja ? await CajaOcupadaAsync(db, caja, ct) : null) ?? CajaOcupada(null, null),
            IndiceDelCajero => (datos.CashierUserId is { } cajero ? await CajeroOcupadoAsync(db, cajero, ct) : null) ?? CajeroOcupado(null),
            _ => (datos.PointOfSaleId is { } punto && datos.OperatingDate is { } fecha ? await DiaCerradoAsync(db, punto, fecha, ct) : null)
                 ?? DiaYaCerrado(datos.OperatingDate),
        };
    }

    // ------------------------------------------------------------------------------------------ comprobaciones previas --

    /// <summary><c>Payments.VoucherAlreadyUsed</c> si el número normalizado está activo en ese medio; nulo si está libre.</summary>
    public static async Task<Error?> BonoUsadoAsync(IApplicationDbContext db, int paymentMeansId, string numeroNormalizado, CancellationToken ct)
    {
        var documentoId = await db.VoucherRedemptions.AsNoTracking()
            .Where(r => r.PaymentMeansId == paymentMeansId && r.NormalizedNumber == numeroNormalizado && r.Status == VoucherRedemptionStatus.Active)
            .Select(r => (int?)r.DocumentId)
            .FirstOrDefaultAsync(ct);
        if (documentoId is null) return null;

        var venta = await db.InventoryDocuments.AsNoTracking().IgnoreQueryFilters().Where(d => d.Id == documentoId)
            .Select(d => new VentaQueLoUso(d.PublicId, d.Class, d.Prefix, d.Number))
            .FirstOrDefaultAsync(ct);
        return BonoYaUsado(venta);
    }

    /// <summary><c>Inventory.CashSession.RegisterBusy</c> si la caja tiene una sesión abierta; nulo si no.</summary>
    public static async Task<Error?> CajaOcupadaAsync(IApplicationDbContext db, int cashRegisterId, CancellationToken ct)
    {
        var abierta = await db.CashSessions.AsNoTracking()
            .Where(s => s.CashRegisterId == cashRegisterId && s.Status == CashSessionStatus.Open)
            .Select(s => new { s.CashierName, s.OpenedAt })
            .FirstOrDefaultAsync(ct);
        return abierta is null ? null : CajaOcupada(abierta.CashierName, abierta.OpenedAt);
    }

    /// <summary>
    /// <c>Inventory.CashSession.CashierBusy</c> si el cajero tiene una sesión abierta y exclusiva en alguna caja; nulo si no. Sólo
    /// cuentan las sesiones que sellaron <c>ExclusiveCashier</c>, que son las que cubre el índice.
    /// </summary>
    public static async Task<Error?> CajeroOcupadoAsync(IApplicationDbContext db, int cashierUserId, CancellationToken ct)
    {
        var cajaId = await db.CashSessions.AsNoTracking()
            .Where(s => s.CashierUserId == cashierUserId && s.Status == CashSessionStatus.Open && s.ExclusiveCashier)
            .Select(s => (int?)s.CashRegisterId)
            .FirstOrDefaultAsync(ct);
        if (cajaId is null) return null;
        var codigo = await db.CashRegisters.AsNoTracking().IgnoreQueryFilters().Where(c => c.Id == cajaId)
            .Select(c => c.Code).FirstOrDefaultAsync(ct);
        return CajeroOcupado(codigo);
    }

    /// <summary><c>Inventory.DayClose.AlreadyClosed</c> si el punto tiene cerrado ese día; nulo si no (un cierre reabierto no cuenta).</summary>
    public static async Task<Error?> DiaCerradoAsync(IApplicationDbContext db, int pointOfSaleId, DateOnly fechaOperativa, CancellationToken ct)
    {
        var cerrado = await db.DayCloses.AsNoTracking()
            .AnyAsync(d => d.PointOfSaleId == pointOfSaleId && d.OperatingDate == fechaOperativa && d.Status == DayCloseStatus.Closed, ct);
        return cerrado ? DiaYaCerrado(fechaOperativa) : null;
    }

    private static async Task<Error?> PrimerBonoUsadoAsync(IApplicationDbContext db, IReadOnlyCollection<(int PaymentMeansId, string NormalizedNumber)> bonos,
        CancellationToken ct)
    {
        foreach (var (medio, numero) in bonos)
        {
            if (await BonoUsadoAsync(db, medio, numero, ct) is { } error) return error;
        }
        return null;
    }

    // ------------------------------------------------------------------------------------------ errores --

    private sealed record VentaQueLoUso(Guid PublicId, DocumentClass Class, string Prefix, long? Number);

    private static Error BonoYaUsado(VentaQueLoUso? venta) => venta is null
        ? new Error("Payments.VoucherAlreadyUsed", "Ese número de bono ya se usó en otra venta.")
        : new ErrorConDatos("Payments.VoucherAlreadyUsed",
            $"Ese número de bono ya se usó en la venta {VistaDeDocumentos.NumeroVisible(venta.Prefix, venta.Number) ?? "sin número"}.",
            new { documentPublicId = venta.PublicId, documentClass = venta.Class.ToString(), prefix = venta.Prefix, number = venta.Number });

    private static Error CajaOcupada(string? cajero, DateTime? abiertaEl) => cajero is null
        ? new Error("Inventory.CashSession.RegisterBusy", "La caja ya tiene una sesión abierta.")
        : new ErrorConDatos("Inventory.CashSession.RegisterBusy", $"La caja ya tiene una sesión abierta por {cajero}.",
            new { cashierName = cajero, openedAt = abiertaEl });

    private static Error CajeroOcupado(string? codigoDeCaja) => codigoDeCaja is null
        ? new Error("Inventory.CashSession.CashierBusy", "El cajero ya tiene una sesión abierta en otra caja.")
        : new ErrorConDatos("Inventory.CashSession.CashierBusy", $"El cajero ya tiene una sesión abierta en la caja {codigoDeCaja}.",
            new { cashRegisterCode = codigoDeCaja });

    private static Error DiaYaCerrado(DateOnly? fecha) => fecha is { } f
        ? new ErrorConDatos("Inventory.DayClose.AlreadyClosed", $"El día {f:yyyy-MM-dd} ya está cerrado en este punto de venta.",
            new { operatingDate = f })
        : new Error("Inventory.DayClose.AlreadyClosed", "El día ya está cerrado en este punto de venta.");
}

/// <summary>
/// Lo que el comando que guardó sabe de lo que intentó escribir, para nombrar lo que ganó la carrera (T589): los bonos
/// (medio, número normalizado) de la venta, la caja y el cajero de la apertura, el punto y la fecha del cierre del día. (nuevo)
/// </summary>
public sealed record DatosDeLaColision(
    IReadOnlyCollection<(int PaymentMeansId, string NormalizedNumber)>? Bonos = null,
    int? CashRegisterId = null,
    int? CashierUserId = null,
    int? PointOfSaleId = null,
    DateOnly? OperatingDate = null);
