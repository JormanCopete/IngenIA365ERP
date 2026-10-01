using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.ElectronicInvoicing.Catalogs;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Sales;

/// <summary>
/// La elegibilidad provisional de una persona para un medio de crédito (T32; contracts/api.md §23.1) (nuevo): identificada (no el
/// consumidor final), activa en el maestro; con <c>AssociateCredit</c>, asociado sin retiro; con <c>CustomerCredit</c>, cliente.
/// <see cref="Motivos"/> vacío = elegible; si no, en el orden de §23.1 y con los códigos del cobro.
/// </summary>
public sealed record ElegibilidadDeCredito(Person? Persona, string Nombre, bool IsAssociate, bool? AssociateActive, bool IsCustomer, IReadOnlyList<Error> Motivos)
{
    public bool Elegible => Motivos.Count == 0;
}

/// <summary>
/// El crédito dentro del pago de una venta (feature 012, I3, T654; contracts/api.md §23.2; T32; FR-061, FR-062) (nuevo). Lo llama
/// <see cref="ReglasDeConfirmacionDeVenta"/> antes de la aprobación, así rige igual en la factura de oficina y en el cobro del POS
/// (<c>CheckoutPosDraftCommand</c> confirma por el mismo flujo canónico). Por cada pago de clase <c>AssociateCredit</c> o
/// <c>CustomerCredit</c>:
/// <list type="number">
/// <item>repite la evaluación: <see cref="IConsultasDeCartera"/> (mientras IC esté pendiente, <see cref="CarteraNoHabilitada"/>) y la
/// elegibilidad provisional (<see cref="ElegibilidadAsync"/>); no elegible → 422 con el primer código de <c>reasons</c>;</item>
/// <item>completa las condiciones con las propuestas del medio y las valida contra sus máximos (cuotas, plazo, periodicidad,
/// primer vencimiento dentro del plazo) → <c>Inventory.Credit.TermsOutOfRange</c>;</item>
/// <item>calcula el primer y el último vencimiento (el plazo) y deja en <c>INV_Documents.DueDate</c> el mayor;</item>
/// <item>sella <c>AccountsReceivableRecordedBy</c> desde <c>Cartera.CuentaPorCobrarRegistradaPor</c>, forzado a <c>Contabilidad</c>
/// mientras <c>Cartera.IntegracionHabilitadaDesde</c> no tenga fecha vigente o Cartera no esté habilitada, y marca
/// <c>PendingValidation = true</c> y <c>CreditOrigin = ProvisionalCredit</c>.</item>
/// </list>
/// Nunca guarda: los pagos ya están seguidos por la unidad de trabajo de la confirmación.
/// </summary>
public sealed class CreditoEnLaVenta(IApplicationDbContext db, ILectorDeParametros parametros, IConsultasDeCartera cartera)
{
    public const string Contabilidad = "Contabilidad";
    public const string Cartera = "Cartera";

    /// <summary>Periodicidad por defecto (mensual) cuando ni el pago ni el medio la dicen.</summary>
    public const short PeriodicidadPorDefecto = 30;

    public async Task<Result> ValidarAsync(InventoryDocument documento, IReadOnlyList<DocumentPayment> pagos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(documento);
        var creditos = pagos.Select((p, i) => (Pago: p, Indice: i)).Where(x => x.Pago.EsCredito).ToList();
        if (creditos.Count == 0) return Result.Success();

        var fecha = documento.OperationDate;
        var persona = documento.CounterpartyPersonId is int id ? await db.People.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct) : null;
        var ids = creditos.Select(c => c.Pago.PaymentMeansId).Distinct().ToList();
        var medios = await db.PaymentMeans.AsNoTracking().Where(m => ids.Contains(m.Id)).ToDictionaryAsync(m => m.Id, ct);
        var canal = documento.SalesChannelId is int c ? await db.SalesChannels.AsNoTracking().Where(x => x.Id == c).Select(x => x.Code).FirstOrDefaultAsync(ct) : null;
        var sello = await SelloAsync(fecha, ct);

        DateOnly? mayor = null;
        foreach (var (pago, indice) in creditos)
        {
            var elegibilidad = await ElegibilidadAsync(db, persona, fecha, pago.MeansClass, ct);
            if (!elegibilidad.Elegible) return Result.Failure(elegibilidad.Motivos[0]);

            // IC pendiente: Cartera no habilitada → crédito provisional. Con Cartera habilitada (T662, bloqueada por D-02) aquí se
            // aplicaría su respuesta (en mora, sin cupo, sin respuesta); hasta entonces se trata como provisional.
            var estado = await cartera.EstadoCrediticioAsync(new ConsultaCrediticia(persona!.PublicId, pago.MeansClass, pago.Amount, fecha, canal), ct);

            if (!medios.TryGetValue(pago.PaymentMeansId, out var medio)) return Result.Failure(ErroresDeCredito.MeansNotCredit(pago.MeansCode));
            var condiciones = Condiciones(medio, pago.InstallmentCount, pago.CreditTermDays, pago.InstallmentPeriodDays, pago.FirstDueDate, fecha);
            if (condiciones is null) return Result.Failure(ErroresDeCredito.TermsOutOfRange(medio.Code, indice, medio.MaxInstallments, MaximoDePlazo(medio)));

            pago.InstallmentCount = condiciones.Cuotas;
            pago.CreditTermDays = condiciones.PlazoDias;
            pago.InstallmentPeriodDays = condiciones.PeriodicidadDias;
            pago.FirstDueDate = condiciones.PrimerVencimiento;
            pago.FinalDueDate = condiciones.UltimoVencimiento;
            pago.SuggestedCreditLineCode ??= medio.SuggestedCreditLineCode;
            pago.PendingValidation = true;
            pago.CreditOrigin = CreditOrigin.ProvisionalCredit;
            pago.AccountsReceivableRecordedBy = estado.LendingEnabled ? sello : Contabilidad;
            mayor = mayor is { } m && m >= condiciones.UltimoVencimiento ? m : condiciones.UltimoVencimiento;
        }
        documento.DueDate = mayor;
        return Result.Success();
    }

    /// <summary>
    /// <c>Cartera.CuentaPorCobrarRegistradaPor</c> a la fecha, forzado a <c>Contabilidad</c> mientras <c>Cartera.IntegracionHabilitadaDesde</c>
    /// esté vacío (o sea posterior a la fecha, o aún no disponible): lo que se sella en el pago (FR-062).
    /// </summary>
    public async Task<string> SelloAsync(DateOnly fecha, CancellationToken ct)
    {
        var desde = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.CarteraIntegracionHabilitadaDesde, fecha, ct: ct);
        var habilitada = desde.IsSuccess && DateOnly.TryParse(desde.Value.Texto, System.Globalization.CultureInfo.InvariantCulture, out var d) && d <= fecha;
        if (!habilitada) return Contabilidad;
        var quien = await parametros.LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.CarteraCuentaPorCobrarRegistradaPor, fecha, ct: ct);
        return quien.IsSuccess && string.Equals(quien.Value.Texto, Cartera, StringComparison.OrdinalIgnoreCase) ? Cartera : Contabilidad;
    }

    /// <summary>Las condiciones del crédito, ya completas y validadas. (nuevo)</summary>
    public sealed record CondicionesDeCredito(short Cuotas, short PlazoDias, short PeriodicidadDias, DateOnly PrimerVencimiento, DateOnly UltimoVencimiento);

    /// <summary>El plazo máximo del medio: el máximo, o el propuesto si no tiene máximo.</summary>
    public static short? MaximoDePlazo(PaymentMeans medio) => medio.MaxTermDays ?? medio.DefaultTermDays;

    /// <summary>
    /// Completa las condiciones pedidas con las propuestas del medio y las valida contra sus máximos (§23.2): cuotas de 1 al máximo,
    /// plazo de 1 al máximo (o al propuesto), periodicidad positiva, primer vencimiento después de la fecha y no después del último, que
    /// es la fecha más el plazo. Con una cuota, el primer vencimiento es el último. Nulo si algo queda fuera.
    /// </summary>
    public static CondicionesDeCredito? Condiciones(PaymentMeans medio, short? cuotas, short? plazo, short? periodicidad, DateOnly? primer, DateOnly fecha)
    {
        ArgumentNullException.ThrowIfNull(medio);
        var n = (short)(cuotas ?? medio.DefaultInstallments ?? medio.MaxInstallments ?? 1);
        var dias = (short)(plazo ?? medio.DefaultTermDays ?? medio.MaxTermDays ?? 0);
        var cada = (short)(periodicidad ?? medio.InstallmentPeriodDays ?? PeriodicidadPorDefecto);
        if (n < 1 || medio.MaxInstallments is { } maxN && n > maxN) return null;
        if (dias < 1 || MaximoDePlazo(medio) is { } maxD && dias > maxD) return null;
        if (cada < 1) return null;
        var ultimo = fecha.AddDays(dias);
        var primero = primer ?? (n == 1 ? ultimo : fecha.AddDays(Math.Min(cada, dias)));
        if (primero <= fecha || primero > ultimo) return null;
        return new CondicionesDeCredito(n, dias, cada, primero, ultimo);
    }

    /// <summary>La elegibilidad provisional de la persona para la clase del medio (T32, §23.1).</summary>
    public static async Task<ElegibilidadDeCredito> ElegibilidadAsync(IApplicationDbContext db, Person? persona, DateOnly fecha, PaymentMeansClass clase,
        CancellationToken ct)
    {
        var consumidorFinal = CatalogoDian.Embebido.ConsumidorFinal(fecha)?.Numero;
        if (persona is null || persona.IsDeleted || consumidorFinal is not null && persona.TaxId == consumidorFinal)
            return new ElegibilidadDeCredito(persona, persona is null ? string.Empty : Pos.BorradorDelPos.Nombre(persona), persona?.IsAssociate ?? false, null,
                persona?.IsCustomer ?? false, [ErroresDeCredito.PersonNotIdentified()]);

        var nombre = Pos.BorradorDelPos.Nombre(persona);
        bool? asociadoVigente = null;
        if (persona.IsAssociate)
        {
            var asociado = await db.Associates.AsNoTracking().Where(a => a.PersonId == persona.Id && !a.IsDeleted)
                .Select(a => new { a.Status, a.WithdrawalDate, a.RejoinDate }).FirstOrDefaultAsync(ct);
            asociadoVigente = asociado is not null && !Retirado(asociado.Status, asociado.WithdrawalDate, asociado.RejoinDate, fecha);
        }

        var motivos = new List<Error>();
        if (string.Equals(persona.Status, "I", StringComparison.OrdinalIgnoreCase)) motivos.Add(ErroresDeCredito.PersonInactive(nombre));
        if (clase == PaymentMeansClass.AssociateCredit && asociadoVigente != true) motivos.Add(ErroresDeCredito.NotAssociate(nombre));
        if (clase == PaymentMeansClass.CustomerCredit && !persona.IsCustomer) motivos.Add(ErroresDeCredito.NotCustomer(nombre));
        return new ElegibilidadDeCredito(persona, nombre, persona.IsAssociate, asociadoVigente, persona.IsCustomer, motivos);
    }

    /// <summary>
    /// ¿El asociado tiene retiro a la fecha? Estado <c>R</c> o fecha de retiro cumplida, salvo un reingreso posterior al retiro que ya
    /// rige.
    /// </summary>
    public static bool Retirado(string? estado, DateOnly? retiro, DateOnly? reingreso, DateOnly fecha)
    {
        var reingresado = reingreso is { } r && r <= fecha && (retiro is null || r >= retiro);
        if (reingresado) return false;
        return string.Equals(estado, "R", StringComparison.OrdinalIgnoreCase) || retiro is { } w && w <= fecha;
    }
}
