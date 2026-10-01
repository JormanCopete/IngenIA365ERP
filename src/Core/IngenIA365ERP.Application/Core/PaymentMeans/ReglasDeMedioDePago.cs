using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing.Catalogs;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Sales.Payments;
using Microsoft.EntityFrameworkCore;
using MedioDePago = IngenIA365ERP.Domain.Entities.Core.Payments.PaymentMeans;

namespace IngenIA365ERP.Application.Core.PaymentMeans;

/// <summary>
/// Lo que un medio cita ya resuelto (feature 012, I3, T590): la franquicia, el adquirente y el banco. El alta una a una los
/// busca por <c>PublicId</c> y la plantilla 11 por código, y los dos llegan aquí con lo mismo. (nuevo)
/// </summary>
public sealed record DatosDeMedio(PaymentMeansInput Input, CardNetwork? Red, CardAcquirer? Adquirente, int? BancoId);

/// <summary>
/// La regla única de un medio de pago (feature 012, I3, T590; contracts/api.md §22.1; FR-096, FR-101): la usan el alta, la
/// edición y la importación de la plantilla 11 (<c>ImportPaymentMeansCommand</c>), así que no hay una segunda puerta con reglas
/// propias. Lo único que el código sabe del medio es su clase (<see cref="ClasesDeMedio"/>); el resto son datos cuya combinación
/// se valida aquí con <c>Core.PaymentMeans.Invalid</c> y <c>data { field, rule }</c>. No guarda. (nuevo)
/// </summary>
public static class ReglasDeMedioDePago
{
    public const int LargoDeNombre = 60;
    public const int LargoDeCuenta = 25;
    public const int LargoDeNotas = 300;
    public const int LargoDeLinea = 20;

    // Las reglas, tal como viajan en data.rule.
    public const string VueltasSoloEnEfectivo = "ChangeOnlyInCash";
    public const string CreditoSinArqueoFisico = "CreditNotPhysicallyCounted";
    public const string EfectivoSeArquea = "CashMustBeCounted";
    public const string TarjetaExigeRed = "CardRequiresNetwork";
    public const string TarjetaExigeAdquirente = "CardRequiresAcquirer";
    public const string RedSoloEnTarjetas = "NetworkOnlyForCards";
    public const string TipoDeRedIncompatible = "NetworkKindMismatch";
    public const string ReferenciaExigeTipo = "ReferenceKindRequired";
    public const string LargoDeReferencia = "ReferenceLengthRange";
    public const string BancoExigido = "BankRequired";
    public const string CuentaExigeBanco = "AccountRequiresBank";
    public const string CreditoExigeCondiciones = "CreditDefaultsRequired";
    public const string CondicionesSoloEnCredito = "CreditDefaultsOnlyForCredit";
    public const string CondicionesDeCredito = "CreditDefaultsRange";
    public const string CodigoDianDesconocido = "DianCodeUnknown";
    public const string Vigencia = "ValidityRange";
    public const string TeclaRapidaRepetida = "QuickKeyTaken";
    public const string Comision = "CommissionRange";
    public const string Tolerancia = "ToleranceRange";
    public const string ConjuntoExplicito = "ExplicitSetExists";

    /// <summary>El arqueo efectivo del medio: el que viene o el de la clase.</summary>
    public static CashCountMethod ArqueoDe(PaymentMeansInput input) => input.CountMethod ?? ClasesDeMedio.ArqueoPorDefecto(input.Class);

    /// <summary>
    /// Deja el medio en el contexto (nuevo, o el existente modificado) con las reglas de §22.1: código único para uno nuevo; con
    /// pagos, la clase, la red, el adquirente y el arqueo no cambian (<c>Core.PaymentMeans.InUse</c>); las combinaciones; la
    /// tecla rápida única entre activos; el código DIAN contra <see cref="CatalogoDian"/> a <paramref name="hoy"/>. Con
    /// <paramref name="comprobarConjuntos"/>, prender una marca «todos» con un conjunto explícito vivo es
    /// <c>Inventory.PaymentMeans.AvailabilityConflict</c> (la importación reemplaza el conjunto y no lo pide). No guarda.
    /// </summary>
    public static async Task<Result<MedioDePago>> AplicarAsync(
        IApplicationDbContext db, MedioDePago? existente, DatosDeMedio datos, DateOnly hoy, CancellationToken ct, bool comprobarConjuntos = true)
    {
        var input = datos.Input;
        var arqueo = ArqueoDe(input);

        if (existente is null)
        {
            var codigo = CodigoDeCatalogo.Normalizar(input.Code)!;
            if (await DuplicadoAsync(db, codigo, ct) is { } dup) return Result.Failure<MedioDePago>(dup);
        }
        else
        {
            var cambiaAlgoFijo = existente.Class != input.Class
                || existente.CardNetworkId != datos.Red?.Id
                || existente.CardAcquirerId != datos.Adquirente?.Id
                || existente.CountMethod != arqueo;
            if (cambiaAlgoFijo)
            {
                var pagos = await PagosAsync(db, existente.Id, ct);
                if (pagos > 0) return Result.Failure<MedioDePago>(PaymentMeansErrors.InUse(pagos));
            }
        }

        if (Combinacion(input, arqueo, datos, hoy) is { } invalido) return Result.Failure<MedioDePago>(invalido);

        var tecla = string.IsNullOrWhiteSpace(input.QuickKey) ? null : input.QuickKey.Trim().ToUpperInvariant();
        if (tecla is not null && input.IsActive && await TeclaOcupadaAsync(db, tecla, existente, ct) is { } otro)
            return Result.Failure<MedioDePago>(PaymentMeansErrors.Invalid("quickKey", TeclaRapidaRepetida,
                $"La tecla rápida «{tecla}» ya la usa el medio activo {otro}. Elija otra."));

        if (comprobarConjuntos && existente is not null && await ConflictoDeConjuntosAsync(db, existente, input, ct) is { } conflicto)
            return Result.Failure<MedioDePago>(conflicto);

        var medio = existente ?? new MedioDePago { Code = CodigoDeCatalogo.Normalizar(input.Code)! };
        medio.Name = input.Name.Trim();
        medio.DisplayOrder = input.DisplayOrder;
        medio.QuickKey = tecla;
        medio.Class = input.Class;
        Red(medio, datos.Red);
        Adquirente(medio, datos.Adquirente);
        medio.BankId = datos.BancoId;
        medio.DestinationAccountNumber = string.IsNullOrWhiteSpace(input.DestinationAccountNumber) ? null : input.DestinationAccountNumber.Trim();
        medio.DestinationAccountType = medio.DestinationAccountNumber is null ? null : input.DestinationAccountType;
        medio.RequiresReference = input.RequiresReference;
        medio.ReferenceKind = input.ReferenceKind;
        medio.ReferenceMinLength = input.ReferenceMinLength;
        medio.ReferenceMaxLength = input.ReferenceMaxLength;
        medio.AllowsChange = input.AllowsChange;
        medio.AllowsPartial = input.AllowsPartial;
        medio.UniqueReference = input.UniqueReference ?? input.Class == PaymentMeansClass.Voucher;
        medio.CountMethod = arqueo;
        medio.RequiresTerminalBatchAtClose = input.RequiresTerminalBatchAtClose;
        medio.ToleranceAmount = input.ToleranceAmount;
        medio.ExpectedCommissionRate = input.ExpectedCommissionRate;
        medio.ExpectedCommissionFixed = input.ExpectedCommissionFixed;
        medio.DianPaymentMeansCode = input.DianPaymentMeansCode.Trim();
        var credito = input.CreditDefaults;
        medio.DefaultTermDays = credito?.TermDays;
        medio.MaxTermDays = credito is null ? null : credito.MaxTermDays ?? credito.TermDays;
        medio.MaxInstallments = credito?.MaxInstallments;
        medio.DefaultInstallments = credito is null ? null : credito.DefaultInstallments ?? credito.MaxInstallments;
        medio.InstallmentPeriodDays = credito?.PeriodicityDays;
        medio.SuggestedCreditLineCode = string.IsNullOrWhiteSpace(credito?.SuggestedLineCode) ? null : credito!.SuggestedLineCode!.Trim().ToUpperInvariant();
        medio.OfferedAtAllPointsOfSale = input.OfferedAtAllPoints;
        medio.OfferedInAllChannels = input.OfferedOnAllChannels;
        medio.OfferedForAllDocumentTypes = input.OfferedForAllDocumentTypes;
        medio.IsActive = input.IsActive;
        medio.ValidFrom = input.ValidFrom;
        medio.ValidTo = input.ValidTo;
        medio.Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim();
        if (existente is null) db.PaymentMeans.Add(medio);
        return Result.Success(medio);
    }

    /// <summary>
    /// Las combinaciones que el medio no admite (§22.1), la primera que falla: vueltas fuera de efectivo, crédito con arqueo
    /// físico, efectivo sin arqueo, tarjeta sin red o sin adquirente, red de otro tipo, referencia exigida sin su tipo, banco en
    /// consignación y transferencia, condiciones de crédito sólo (y siempre) en las clases de crédito, código DIAN del anexo
    /// vigente y vigencia ordenada. Pura sobre lo pedido.
    /// </summary>
    public static Error? Combinacion(PaymentMeansInput input, CashCountMethod arqueo, DatosDeMedio datos, DateOnly hoy)
    {
        var clase = input.Class;
        if (input.AllowsChange && !ClasesDeMedio.PuedeDarVueltas(clase))
            return PaymentMeansErrors.Invalid("allowsChange", VueltasSoloEnEfectivo, "Sólo el efectivo admite vueltas.");
        if (ClasesDeMedio.EsCredito(clase) && arqueo == CashCountMethod.PhysicalCount)
            return PaymentMeansErrors.Invalid("countMethod", CreditoSinArqueoFisico, "Un crédito no se cuenta físicamente en el arqueo: use «None».");
        if (clase == PaymentMeansClass.Cash && arqueo == CashCountMethod.None)
            return PaymentMeansErrors.Invalid("countMethod", EfectivoSeArquea, "El efectivo se arquea: use «PhysicalCount».");

        if (ClasesDeMedio.EsTarjeta(clase))
        {
            if (datos.Red is null)
                return PaymentMeansErrors.Invalid("cardNetworkPublicId", TarjetaExigeRed, "Un medio de tarjeta exige su franquicia.");
            if (datos.Adquirente is null)
                return PaymentMeansErrors.Invalid("cardAcquirerPublicId", TarjetaExigeAdquirente, "Un medio de tarjeta exige su adquirente.");
            var tipoPedido = clase == PaymentMeansClass.CreditCard ? CardKind.Credit : CardKind.Debit;
            if (datos.Red.CardKind != CardKind.Both && datos.Red.CardKind != tipoPedido)
                return PaymentMeansErrors.Invalid("cardNetworkPublicId", TipoDeRedIncompatible,
                    $"La franquicia {datos.Red.Code} es de tarjeta {(datos.Red.CardKind == CardKind.Credit ? "crédito" : "débito")}: no sirve para un medio de {(tipoPedido == CardKind.Credit ? "crédito" : "débito")}.");
        }
        else if (datos.Red is not null || datos.Adquirente is not null)
        {
            return PaymentMeansErrors.Invalid(datos.Red is not null ? "cardNetworkPublicId" : "cardAcquirerPublicId", RedSoloEnTarjetas,
                "La franquicia y el adquirente sólo van en los medios de tarjeta.");
        }

        if (input.RequiresReference && input.ReferenceKind is null)
            return PaymentMeansErrors.Invalid("referenceKind", ReferenciaExigeTipo, "Si el medio exige referencia, indique qué referencia es.");
        if (input.ReferenceMinLength is { } min && input.ReferenceMaxLength is { } max && min > max)
            return PaymentMeansErrors.Invalid("referenceMaxLength", LargoDeReferencia, "El largo máximo de la referencia no puede ser menor que el mínimo.");

        if (clase is PaymentMeansClass.BankDeposit or PaymentMeansClass.Transfer && datos.BancoId is null)
            return PaymentMeansErrors.Invalid("bankPublicId", BancoExigido, "Una consignación o transferencia exige el banco de destino.");
        if (!string.IsNullOrWhiteSpace(input.DestinationAccountNumber) && datos.BancoId is null)
            return PaymentMeansErrors.Invalid("bankPublicId", CuentaExigeBanco, "La cuenta de destino va con su banco.");

        if (ClasesDeMedio.EsCredito(clase))
        {
            if (input.CreditDefaults is not { } c)
                return PaymentMeansErrors.Invalid("creditDefaults", CreditoExigeCondiciones, "Un medio de crédito exige plazo, cuotas y periodicidad.");
            if (c.TermDays <= 0 || c.MaxInstallments <= 0 || c.PeriodicityDays <= 0
                || c.MaxTermDays is { } mt && mt < c.TermDays || c.DefaultInstallments is { } di && (di <= 0 || di > c.MaxInstallments))
                return PaymentMeansErrors.Invalid("creditDefaults", CondicionesDeCredito,
                    "Plazo, cuotas y periodicidad son positivos; el plazo máximo no es menor que el propuesto ni las cuotas propuestas mayores que las máximas.");
        }
        else if (input.CreditDefaults is not null)
        {
            return PaymentMeansErrors.Invalid("creditDefaults", CondicionesSoloEnCredito, "Las condiciones de crédito sólo van en los medios de crédito.");
        }

        if (input.ToleranceAmount < 0)
            return PaymentMeansErrors.Invalid("toleranceAmount", Tolerancia, "La tolerancia de arqueo no es negativa.");
        if (input.ExpectedCommissionRate is { } tasa && (tasa < 0 || tasa >= 1) || input.ExpectedCommissionFixed is < 0)
            return PaymentMeansErrors.Invalid("expectedCommissionRate", Comision, "La comisión esperada es una fracción entre 0 y 1 y un valor no negativo.");

        if (CatalogoDian.Embebido.MedioDePago(input.DianPaymentMeansCode?.Trim(), hoy) is null)
            return PaymentMeansErrors.Invalid("dianPaymentMeansCode", CodigoDianDesconocido,
                $"«{input.DianPaymentMeansCode}» no es un medio de pago del anexo DIAN vigente (10, 48, 49, 42, 47, 20, 71…).");
        if (input.ValidTo is { } hasta && hasta < input.ValidFrom)
            return PaymentMeansErrors.Invalid("validTo", Vigencia, "La vigencia termina antes de empezar.");
        return null;
    }

    /// <summary>Cuántos pagos tiene el medio (en cualquier documento, recibidos o reintegrados).</summary>
    public static Task<int> PagosAsync(IApplicationDbContext db, int medioId, CancellationToken ct) =>
        medioId == 0 ? Task.FromResult(0) : db.DocumentPayments.CountAsync(p => p.PaymentMeansId == medioId && !p.IsDeleted, ct);

    public static async Task<Error?> DuplicadoAsync(IApplicationDbContext db, string codigo, CancellationToken ct)
    {
        var local = db.PaymentMeans.Local.FirstOrDefault(m => m.Code == codigo && !m.IsDeleted);
        if (local is not null) return CodigoDeCatalogo.Duplicado("un medio de pago", codigo, local.Name, local.PublicId);
        var existente = await db.PaymentMeans.Where(m => m.Code == codigo && !m.IsDeleted).Select(m => new { m.PublicId, m.Name }).FirstOrDefaultAsync(ct);
        return existente is null ? null : CodigoDeCatalogo.Duplicado("un medio de pago", codigo, existente.Name, existente.PublicId);
    }

    private static async Task<string?> TeclaOcupadaAsync(IApplicationDbContext db, string tecla, MedioDePago? propio, CancellationToken ct)
    {
        var local = db.PaymentMeans.Local.FirstOrDefault(m => m.QuickKey == tecla && m.IsActive && !m.IsDeleted && !ReferenceEquals(m, propio));
        if (local is not null) return local.Code;
        var propioId = propio?.Id ?? 0;
        return await db.PaymentMeans.Where(m => m.QuickKey == tecla && m.IsActive && !m.IsDeleted && m.Id != propioId)
            .Select(m => m.Code).FirstOrDefaultAsync(ct);
    }

    /// <summary>Prender una marca «todos» con su conjunto explícito vivo es contradecirse (§22.3).</summary>
    private static async Task<Error?> ConflictoDeConjuntosAsync(IApplicationDbContext db, MedioDePago medio, PaymentMeansInput input, CancellationToken ct)
    {
        if (input.OfferedAtAllPoints && await db.PaymentMeansPointsOfSale.AnyAsync(x => x.PaymentMeansId == medio.Id && !x.IsDeleted, ct))
            return Inventory.Pos.ErroresDePuntoDeVenta.AvailabilityConflict("pointsOfSale");
        if (input.OfferedOnAllChannels && await db.PaymentMeansChannels.AnyAsync(x => x.PaymentMeansId == medio.Id && !x.IsDeleted, ct))
            return Inventory.Pos.ErroresDePuntoDeVenta.AvailabilityConflict("salesChannels");
        if (input.OfferedForAllDocumentTypes && await db.PaymentMeansDocumentTypes.AnyAsync(x => x.PaymentMeansId == medio.Id && !x.IsDeleted, ct))
            return Inventory.Pos.ErroresDePuntoDeVenta.AvailabilityConflict("documentTypes");
        return null;
    }

    /// <summary>La franquicia: por Id si ya existe, por navegación si nace en esta misma operación (la plantilla).</summary>
    private static void Red(MedioDePago medio, CardNetwork? red)
    {
        medio.CardNetwork = red;
        medio.CardNetworkId = red is null ? null : red.Id == 0 ? medio.CardNetworkId : red.Id;
    }

    private static void Adquirente(MedioDePago medio, CardAcquirer? adquirente)
    {
        medio.CardAcquirer = adquirente;
        medio.CardAcquirerId = adquirente is null ? null : adquirente.Id == 0 ? medio.CardAcquirerId : adquirente.Id;
    }
}
