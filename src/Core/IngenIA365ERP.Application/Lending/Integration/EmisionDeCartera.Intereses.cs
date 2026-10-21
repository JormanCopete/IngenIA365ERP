using System.Globalization;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>Lo que un hecho de una cuenta necesita saber de ella, más si es la marcada exenta del GMF (la marca de la dimensión, research C20). (nuevo)</summary>
public sealed record HechoDeCuentaMarcada(HechoDeCuenta Hecho, bool ExentaDeGmf)
{
    public DepositFactDimensionsV1 Dimensiones => Hecho.Dimensiones(ExentaDeGmf);
}

/// <summary>
/// Una liquidación de intereses tal como se emite (feature 014, US5, T134; mensajes.md §4.1): <c>InteresDeCaptacionLiquidado</c> con una línea —la de la
/// operación— y, si se retuvo, <c>RetencionPracticada</c> en la misma unidad; origen la operación (<c>InterestSettlement</c> o <c>AccountClosing</c>). (nuevo)
/// </summary>
public sealed record LiquidacionAEmitir(
    HechoDeCuentaMarcada Hecho,
    MemberAccountOperation Operacion,
    InterestSettlement Liquidacion,
    RetencionCalculada Retencion,
    PoliticaDeRetencion Politica,
    PerfilDeRetencion Perfil,
    bool PorElProceso,
    string Clase,
    PrevalidationOutcome ValidacionPrevia)
{
    /// <summary>La corrida del proceso que liquidó (nula en la manual o en la cancelación).</summary>
    public Guid? CorridaPublicId { get; init; }
}

/// <summary>
/// El ajuste de causación que deja una operación con fecha valor en días ya causados (feature 014, US5, T133; FR-062; mensajes.md §4.1): el valor (seis
/// decimales), los días que cambiaron y el hecho de la operación que lo provocó. Va <b>aparte</b>, con origen la misma operación (research E96). (nuevo)
/// </summary>
public sealed record AjusteAEmitir(
    HechoDeCuentaMarcada Hecho,
    MemberAccountOperation Operacion,
    decimal Ajuste,
    IReadOnlyList<AccrualDayV1> Dias,
    string Causa,
    string ClaseDeLaOperacion);

public sealed partial class EmisionDeCartera
{
    /// <summary>La clase del origen de una liquidación de intereses (mensajes.md §2).</summary>
    public const string ClaseLiquidacionDeIntereses = "InterestSettlement";

    /// <summary>La clase del origen de una cancelación de cuenta (mensajes.md §2).</summary>
    public const string ClaseCancelacionDeCuenta = "AccountClosing";

    /// <summary>La causa del ajuste de una operación fechada atrás (mensajes.md §4.1).</summary>
    public const string CausaOperacionFechadaAtras = "ValueDatedOperation";

    /// <summary>La causa de la corrección que hace el proceso cuando el libro no coincide con lo que el motor recalcula (mensajes.md §4.1).</summary>
    public const string CausaCorreccionDeTasa = "RateCorrection";

    /// <summary>
    /// Los datos de hecho de varias cuentas en bloque (la tanda del proceso): el código de su línea, el titular y si es empleado, la sucursal contable de su
    /// oficina y la marca exenta del GMF a la fecha. Las cuentas cuya oficina no tiene sucursal no salen (no emiten, C9 de la 013).
    /// </summary>
    public async Task<IReadOnlyDictionary<int, HechoDeCuentaMarcada>> HechosDeCuentasAsync(IReadOnlyList<MemberAccount> cuentas, DateOnly fechaContable,
        CancellationToken ct)
    {
        if (cuentas.Count == 0) return new Dictionary<int, HechoDeCuentaMarcada>();
        var lineas = cuentas.Select(c => c.CreditLineId).Distinct().ToList();
        var personas = cuentas.Select(c => c.HolderPersonId).Distinct().ToList();
        var oficinas = cuentas.Select(c => c.BranchId).Distinct().ToList();
        var ids = cuentas.Select(c => c.Id).ToList();
        var codigos = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => lineas.Contains(l.Id)).ToDictionaryAsync(l => l.Id, l => l.Code, ct);
        var titulares = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personas.Contains(p.Id))
            .Select(p => new { p.Id, p.PublicId, p.IsEmployee }).ToDictionaryAsync(p => p.Id, ct);
        var sucursales = await db.Branches.AsNoTracking().Where(b => oficinas.Contains(b.Id) && !b.IsDeleted).ToDictionaryAsync(b => b.Id, b => b.PublicId, ct);
        var exentas = (await db.GmfExemptAccounts.AsNoTracking()
                .Where(g => ids.Contains(g.MemberAccountId) && !g.IsDeleted && g.ValidFrom <= fechaContable && (g.ValidTo == null || g.ValidTo >= fechaContable))
                .Select(g => g.MemberAccountId).ToListAsync(ct))
            .ToHashSet();
        var plazos = await PlazoDeEmisionAsync(db, cuentas, ct);
        var hechos = new Dictionary<int, HechoDeCuentaMarcada>();
        foreach (var c in cuentas)
        {
            if (!sucursales.TryGetValue(c.BranchId, out var sucursal) || !titulares.TryGetValue(c.HolderPersonId, out var titular)) continue;
            hechos[c.Id] = new HechoDeCuentaMarcada(
                new HechoDeCuenta(c, codigos.GetValueOrDefault(c.CreditLineId) ?? string.Empty, titular.PublicId, titular.IsEmployee, sucursal, fechaContable)
                {
                    ExentaDeGmf = exentas.Contains(c.Id),
                    PlazoDeEmision = plazos.GetValueOrDefault(c.Id),
                },
                exentas.Contains(c.Id));
        }
        return hechos;
    }

    /// <summary>
    /// <c>InteresDeCaptacionLiquidado</c> (+ <c>RetencionPracticada</c> si se retuvo) de una liquidación (mensajes.md §1.1 #8 y #9, §4.1): una línea por la
    /// cuenta con <c>[InterestPayable −bruto, Principal +bruto]</c> (el abono) y, en la retención, <c>[Principal −retención, Withholding +retención]</c> con su
    /// detalle. Origen la operación; el modo, el del hecho principal —por lotes si lo emite el proceso—. Sin guardar.
    /// </summary>
    public async Task<Result<UnidadEmitida>> InteresLiquidadoAsync(LiquidacionAEmitir l, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(l);
        var h = l.Hecho.Hecho;
        var liq = l.Liquidacion;
        var linea = new DepositFactLineV1
        {
            Account = h.Referencia,
            HolderPersonPublicId = h.TitularPublicId,
            Dimensions = l.Hecho.Dimensiones,
            Amounts =
            [
                new DepositConceptAmountV1 { Concept = nameof(MemberAccountConcept.InterestPayable), Amount = -liq.GrossInterest },
                new DepositConceptAmountV1 { Concept = nameof(MemberAccountConcept.Principal), Amount = liq.GrossInterest },
            ],
            ValueDate = liq.PeriodTo,
        };
        var corrida = l.PorElProceso ? nameof(LendingProcessKind.DailyCycle) : null;
        var liquidado = new InteresDeCaptacionLiquidadoV1
        {
            RunKind = corrida,
            RunPublicId = l.CorridaPublicId,
            FactDate = liq.PeriodTo,
            BatchNumber = 1,
            PartNumber = 1,
            Totals = [new DepositFactTotalV1 { Dimensions = l.Hecho.Dimensiones, Amounts = linea.Amounts, AccountCount = 1 }],
            Lines = [linea],
            LineCount = 1,
            ControlTotal = 0m,
            PeriodFrom = liq.PeriodFrom,
            PeriodTo = liq.PeriodTo,
            Accrued = liq.AccruedInterest,
            Settled = liq.GrossInterest,
            RoundingAdjustment = liq.RoundingDifference,
            Capitalized = liq.Destination == InterestDestination.Capitalize,
        };
        List<object> contenidos = [liquidado];
        if (l.Retencion.SeRetiene)
        {
            var lineaDeRetencion = new DepositFactLineV1
            {
                Account = h.Referencia,
                HolderPersonPublicId = h.TitularPublicId,
                Dimensions = l.Hecho.Dimensiones,
                Amounts =
                [
                    new DepositConceptAmountV1 { Concept = nameof(MemberAccountConcept.Principal), Amount = -liq.WithholdingAmount },
                    new DepositConceptAmountV1 { Concept = nameof(MemberAccountConcept.Withholding), Amount = liq.WithholdingAmount },
                ],
                ValueDate = liq.PeriodTo,
            };
            contenidos.Add(new RetencionPracticadaV1
            {
                RunKind = corrida,
                RunPublicId = l.CorridaPublicId,
                FactDate = liq.PeriodTo,
                BatchNumber = 1,
                PartNumber = 1,
                Totals = [new DepositFactTotalV1 { Dimensions = l.Hecho.Dimensiones, Amounts = lineaDeRetencion.Amounts, AccountCount = 1 }],
                Lines = [lineaDeRetencion],
                LineCount = 1,
                ControlTotal = 0m,
                Withholdings = [DetalleDeRetencion(l.Retencion, l.Politica, l.Perfil, liq.PeriodTo.Year, h.Cuenta.DepositClass)],
            });
        }

        var modo = await modoDePaso.ResolverAsync(InteresDeCaptacionLiquidadoV1.Type, h.FechaContable, l.PorElProceso, ct);
        if (modo.IsFailure) return Result.Failure<UnidadEmitida>(modo.Error);
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, l.Operacion.PublicId, l.Clase, Prefijo(l.Operacion.Number), l.Operacion.Number, h.FechaContable,
                h.SucursalPublicId, PersonPublicId: h.TitularPublicId),
            ClavesDeEvento.Confirmacion, contenidos, modo.Value, ValidacionPrevia: l.ValidacionPrevia), ct);
        return Result.Success(new UnidadEmitida(mensajes));
    }

    /// <summary>El bloque <c>WithholdingDetailV1</c> de una retención (mensajes.md §3).</summary>
    public static WithholdingDetailV1 DetalleDeRetencion(RetencionCalculada r, PoliticaDeRetencion p, PerfilDeRetencion perfil, int anio, DepositClass? clase) => new()
    {
        Base = r.Base,
        InflationaryComponentPercent = r.ComponenteInflacionario,
        TaxableBase = r.BaseGravable,
        Rate = r.Tarifa,
        ThresholdUvtPerDay = p.UmbralDiarioUvt,
        Uvt = p.Uvt,
        ThresholdReached = r.UmbralAlcanzado,
        HolderTaxProfile = perfil.ToString(),
        Year = anio,
        ConceptCode = (clase ?? DepositClass.Demand).ToString(),
    };

    /// <summary>
    /// <c>AjusteDeCausacionDeCaptacion</c> de una operación fechada atrás (mensajes.md §4.1; FR-062): una línea con <c>[InterestPayable ±ajuste]</c> (al
    /// centavo, con los seis decimales en <c>accruedAmount</c>), los días recalculados y el hecho principal de la operación. Va aparte de la unidad de la
    /// operación, con su mismo origen y su propio comprobante (research E96). Sin guardar.
    /// </summary>
    public async Task<Result<UnidadEmitida>> AjusteDeCausacionAsync(AjusteAEmitir a, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(a);
        var h = a.Hecho.Hecho;
        var valores = new DepositConceptAmountV1
        {
            Concept = nameof(MemberAccountConcept.InterestPayable), Amount = decimal.Round(a.Ajuste, 2, MidpointRounding.AwayFromZero), AccruedAmount = a.Ajuste,
        };
        var linea = new DepositFactLineV1
        {
            Account = h.Referencia, HolderPersonPublicId = h.TitularPublicId, Dimensions = a.Hecho.Dimensiones, Amounts = [valores], ValueDate = a.Operacion.ValueDate,
        };
        var principal = await db.IntegrationMessages.AsNoTracking()
            .Where(m => m.OriginPublicId == a.Operacion.PublicId && m.Type != AjusteDeCausacionDeCaptacionV1.Type)
            .OrderBy(m => m.Id).Select(m => new { m.PublicId, m.Type, m.OriginNumber }).FirstOrDefaultAsync(ct)
            ?? db.IntegrationMessages.Local.Where(m => m.OriginPublicId == a.Operacion.PublicId && m.Type != AjusteDeCausacionDeCaptacionV1.Type)
                .Select(m => new { m.PublicId, m.Type, m.OriginNumber }).FirstOrDefault();
        var contenido = new AjusteDeCausacionDeCaptacionV1
        {
            FactDate = h.FechaContable,
            BatchNumber = 1,
            PartNumber = 1,
            Totals = [new DepositFactTotalV1 { Dimensions = a.Hecho.Dimensiones, Amounts = [valores], AccountCount = 1 }],
            Lines = [linea],
            LineCount = 1,
            ControlTotal = a.Ajuste,
            Cause = a.Causa,
            CauseFact = principal is null
                ? null
                : new OriginalFactRefV1 { MessageId = principal.PublicId, Type = principal.Type, OriginPublicId = a.Operacion.PublicId, Number = principal.OriginNumber ?? a.Operacion.Number },
            Days = a.Dias,
        };
        var modo = await modoDePaso.ResolverAsync(AjusteDeCausacionDeCaptacionV1.Type, h.FechaContable, ct);
        if (modo.IsFailure) return Result.Failure<UnidadEmitida>(modo.Error);
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, a.Operacion.PublicId, a.ClaseDeLaOperacion, Prefijo(a.Operacion.Number), a.Operacion.Number,
                h.FechaContable, h.SucursalPublicId, PersonPublicId: h.TitularPublicId),
            ClavesDeEvento.Confirmacion, [contenido], modo.Value), ct);
        return Result.Success(new UnidadEmitida(mensajes));
    }

    /// <summary>El número de una liquidación de intereses: el de su operación (para los rótulos de la explicación).</summary>
    public static string RotuloDeLiquidacion(InterestSettlement l) =>
        string.Create(CultureInfo.InvariantCulture, $"{l.PeriodFrom:yyyy-MM-dd}..{l.PeriodTo:yyyy-MM-dd}");
}
