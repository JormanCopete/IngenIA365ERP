using IngenIA365ERP.Application.Channel.Common;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Entities.Channel;
using IngenIA365ERP.Domain.Enums.Channel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Channel.Service;

// Feature 021, US3 (T078; FR-029 a FR-031; research R5; contracts/erp-api-de-servicio.md): los conceptos que el asociado ve en el canal.
// Los valores salen de las mismas piezas que usa la caja (`GetPayablesQuery`): el listado del contrato de obligaciones, la consulta y
// los exigibles del contrato de cuentas. Lo único propio es el filtro por los conceptos publicados de la cooperativa y la forma de la
// respuesta. Nada se guarda: cada lectura vuelve a preguntar.

/// <summary>
/// Los conceptos del asociado a la fecha, con los avisos de los módulos que no respondieron y los destinos de pago que usan (el canal
/// arma el pago con el sitio de pasarela del destino, FR-032).
/// </summary>
public sealed record ChannelPayablesDto(
    DateOnly AsOf,
    IReadOnlyList<ChannelPayableDto> Items,
    IReadOnlyList<string> Notices,
    IReadOnlyList<ChannelDestinationDto> Destinations);

/// <summary>Un destino de pago activo: su sitio en el canal (R10) y los medios que ofrece (FR-006).</summary>
public sealed record ChannelDestinationDto(Guid DestinationPublicId, Guid SitePublicId, string Name, bool OfferPse, bool OfferCard);

/// <summary>
/// Un concepto publicado del asociado. <see cref="Kind"/> va por nombre (<c>Loan</c>, <c>Contribution</c>, <c>ContractualSaving</c>,
/// <c>ProgrammedSaving</c>). En créditos, <see cref="Installment"/> es nulo; en aportes y ahorros, <see cref="PayoffAmount"/> lo es.
/// </summary>
public sealed record ChannelPayableDto(
    string ConceptKey,
    string Kind,
    string LineCode,
    string Description,
    Guid ProductPublicId,
    string ProductNumber,
    decimal Balance,
    decimal PastDue,
    decimal DefaultInterest,
    decimal CycleInstallment,
    decimal DueToday,
    decimal? PayoffAmount,
    DateOnly? NextDueDate,
    int DaysPastDue,
    Guid? DestinationPublicId,
    bool Payable,
    ModosDelConceptoDto Modes,
    bool CanCombine,
    CuotaDeCuentaDelCanalDto? Installment,
    IReadOnlyList<string> BlockReasons);

/// <summary>Las modalidades con que se puede pagar el concepto: las publicadas que además admite el módulo dueño.</summary>
public sealed record ModosDelConceptoDto(bool Due, PagoParcialDto? Partial, AbonoACapitalDto? Prepayment, bool Payoff);

public sealed record PagoParcialDto(decimal Minimum);

/// <summary>La opción de abono preseleccionada (FR-032a), por nombre: <c>ReduceTerm</c> o <c>ReduceInstallment</c>.</summary>
public sealed record AbonoACapitalDto(string DefaultChoice);

/// <summary>La cuota pendiente más antigua de un aporte o un ahorro.</summary>
public sealed record CuotaDeCuentaDelCanalDto(string Period, DateOnly DueDate, decimal Amount, decimal Pending);

public sealed record GetChannelPayablesQuery(Guid PersonPublicId, DateOnly? Date = null) : IRequest<Result<ChannelPayablesDto>>;

public sealed class GetChannelPayablesQueryHandler(
    IApplicationDbContext db,
    IContratoDeObligaciones obligaciones,
    IContratoDeCuentas cuentas,
    IConsultaDeObligaciones consulta,
    IDateTimeService reloj,
    AuditoriaDelCanal auditoria,
    IngenIA365ERP.Application.Common.Integration.Accounting.IContabilidadParaRecaudo? contabilidad = null) : IRequestHandler<GetChannelPayablesQuery, Result<ChannelPayablesDto>>
{
    /// <summary>Hasta dónde se busca la próxima cuota de un crédito.</summary>
    internal const int DiasParaLaProximaCuota = 62;

    public async Task<Result<ChannelPayablesDto>> Handle(GetChannelPayablesQuery request, CancellationToken ct)
    {
        var persona = await PersonaDelCanal.HabilitadaAsync(db, request.PersonPublicId, ct);
        if (persona is null) return Result.Failure<ChannelPayablesDto>(PersonaDelCanal.ErrorNoHabilitada);
        var fecha = request.Date ?? reloj.HoyLocal;
        var (items, avisos) = await ListarAsync(persona.PublicId, fecha, ct);
        await auditoria.LecturaAsync(AuditoriaDelCanal.EventoConceptosLeidos, "Person", persona.PublicId,
            new { channel = "Digital", date = fecha, items = items.Count }, ct);
        var usados = items.Where(i => i.DestinationPublicId is not null).Select(i => i.DestinationPublicId!.Value).Distinct().ToList();
        var destinos = usados.Count == 0
            ? []
            : await db.ChannelPaymentDestinations.AsNoTracking()
                .Where(d => d.IsActive && usados.Contains(d.PublicId))
                .OrderBy(d => d.Name)
                .Select(d => new ChannelDestinationDto(d.PublicId, d.SitePublicId, d.Name, d.OfferPse, d.OfferCard))
                .ToListAsync(ct);
        return Result.Success(new ChannelPayablesDto(fecha, items, avisos, destinos));
    }

    /// <summary>Los conceptos publicados de la persona (ya verificada) a la fecha, sin auditar; lo usa también la simulación.</summary>
    internal async Task<(List<ChannelPayableDto> Items, List<string> Avisos)> ListarAsync(Guid persona, DateOnly fecha, CancellationToken ct)
    {
        var publicados = await ConceptosPublicados.CargarAsync(db, ct);
        var avisos = new List<string>();
        var items = new List<ChannelPayableDto>();
        if (publicados.Hay(ConceptKind.Loan))
            items.AddRange(await CreditosAsync(persona, fecha, publicados, avisos, ct));
        if (publicados.Hay(ConceptKind.Contribution) || publicados.Hay(ConceptKind.ContractualSaving) || publicados.Hay(ConceptKind.ProgrammedSaving))
            items.AddRange(await CuentasAsync(persona, fecha, publicados, avisos, ct));
        if (publicados.Hay(ConceptKind.CommercialReceivable) && contabilidad is not null)
            items.AddRange(await FacturasAsync(persona, publicados, ct));
        return (items, avisos);
    }

    /// <summary>
    /// Feature 021, C2b (T128): las facturas pendientes de cuentas por cobrar comerciales de la persona (lo que Contabilidad informa por tercero y documento
    /// cruce, menos lo que Recaudo ya aplicó), incluidos los servicios por vehículo de la 020 cuando se facturan. Sólo las que tienen comprobante contable:
    /// es la clave del concepto. «Exigible» paga el saldo; «parcial», desde el mínimo.
    /// </summary>
    private async Task<List<ChannelPayableDto>> FacturasAsync(Guid persona, ConceptosPublicados publicados, CancellationToken ct)
    {
        if (publicados.Para(ConceptKind.CommercialReceivable, string.Empty) is not { } publicado) return [];
        var pendientes = await Receipts.CashDesk.CuentasPorCobrarDeRecaudo.PendientesAsync(db, contabilidad!, persona, ct);
        var salida = new List<ChannelPayableDto>();
        foreach (var f in pendientes.Where(f => f.AccountingDocumentPublicId is not null).OrderBy(f => f.Date))
        {
            var minimo = publicado.Concepto.PartialMinimum ?? 0m;
            var modos = new ModosDelConceptoDto(
                Due: publicado.Concepto.AllowDue,
                Partial: publicado.Concepto.AllowPartial && f.Pending > minimo ? new PagoParcialDto(minimo) : null,
                Prepayment: null,
                Payoff: false);
            salida.Add(new ChannelPayableDto(
                ClaveDeConcepto.DeFactura(f.AccountingDocumentPublicId!.Value),
                nameof(ConceptKind.CommercialReceivable),
                string.Empty,
                $"Factura {f.InvoiceNumber} del {f.Date:dd/MM/yyyy}",
                f.AccountingDocumentPublicId.Value,
                f.InvoiceNumber,
                f.Pending,
                f.Pending,
                0m,
                f.Pending,
                f.Pending,
                null,
                null,
                0,
                publicado.Destino,
                publicado.Destino is not null && ModosDelCanal.Alguno(modos),
                modos,
                publicado.Concepto.CanCombine,
                null,
                []));
        }
        return salida;
    }

    private async Task<List<ChannelPayableDto>> CreditosAsync(Guid persona, DateOnly fecha, ConceptosPublicados publicados, List<string> avisos,
        CancellationToken ct)
    {
        var listado = await obligaciones.ListarAsync(new SolicitudDeListado(ClavesDeRecaudo.Recaudador, persona, fecha), ct);
        if (listado.Result == ResultadoDelContrato.Rejected) avisos.Add(listado.Message);
        var vivas = (listado.Value?.Obligations ?? []).Where(o => o.Role == RolEnLaObligacion.Debtor).ToList();
        if (vivas.Count == 0) return [];

        // Saldo total e interés de mora: la consulta de obligaciones (el listado no los trae). La próxima fecha: las cuotas de todos los
        // canales de pago en los dos meses siguientes (research R5).
        var saldos = (await consulta.ObligacionesAsync(new ConsultaDeObligaciones([persona], fecha, IncludeAsCoDebtor: false), ct))
            .People.SelectMany(p => p.Obligations).ToDictionary(o => o.ObligationPublicId);
        var proximas = (await consulta.CuotasDeNominaAsync(
                new ConsultaDeCuotasDeNomina([persona], fecha.AddDays(1), fecha.AddDays(DiasParaLaProximaCuota), TodosLosCanales: true), ct))
            .Installments.GroupBy(c => c.ObligationPublicId).ToDictionary(g => g.Key, g => g.Min(c => c.DueDate));

        var salida = new List<ChannelPayableDto>();
        foreach (var o in vivas)
        {
            if (publicados.Para(ConceptKind.Loan, o.CreditLineCode) is not { } publicado) continue;
            var admitidas = o.AdmittedModalities.ToHashSet();
            var modos = new ModosDelConceptoDto(
                Due: publicado.Concepto.AllowDue && admitidas.Contains(ModalidadDePago.PastDue) && o.DueToday > 0m,
                Partial: publicado.Concepto.AllowPartial && admitidas.Contains(ModalidadDePago.FreeAmount)
                    ? new PagoParcialDto(publicado.Concepto.PartialMinimum ?? 0m) : null,
                Prepayment: publicado.Concepto.AllowPrepayment && admitidas.Contains(ModalidadDePago.ExtraordinaryPrincipal)
                    ? new AbonoACapitalDto((publicado.Concepto.DefaultPrepaymentChoice ?? ChannelPrepaymentChoice.ReduceTerm).ToString()) : null,
                Payoff: publicado.Concepto.AllowPayoff && admitidas.Contains(ModalidadDePago.FullPayoff) && o.PayoffAmount > 0m);
            var saldo = saldos.GetValueOrDefault(o.ObligationPublicId);
            var pagable = publicado.Destino is not null && o.Status == EstadoDeObligacion.Active && !o.ValidationFailed && o.BlockReasons.Count == 0
                          && ModosDelCanal.Alguno(modos);
            salida.Add(new ChannelPayableDto(
                ClaveDeConcepto.DeCredito(o.ObligationPublicId),
                nameof(ConceptKind.Loan),
                o.CreditLineCode,
                $"Crédito {o.Number} · {o.CreditLineName}",
                o.ObligationPublicId,
                o.Number,
                saldo?.TotalBalance ?? o.PayoffAmount,
                o.PastDue,
                saldo?.DefaultInterest ?? 0m,
                o.CycleInstallment,
                o.DueToday,
                o.PayoffAmount,
                proximas.TryGetValue(o.ObligationPublicId, out var proxima) ? proxima : null,
                o.DaysPastDue,
                publicado.Destino,
                pagable,
                modos,
                publicado.Concepto.CanCombine,
                null,
                o.BlockReasons));
        }
        return salida;
    }

    private async Task<List<ChannelPayableDto>> CuentasAsync(Guid persona, DateOnly fecha, ConceptosPublicados publicados, List<string> avisos,
        CancellationToken ct)
    {
        var deCuentas = await cuentas.ConsultarAsync(new SolicitudDeConsultaDeCuentas(ClavesDeRecaudo.Recaudador, persona, fecha), ct);
        if (deCuentas.Result == ResultadoDelContrato.Rejected) avisos.Add(deCuentas.Message);
        var exigibles = await cuentas.ListarExigiblesAsync(new SolicitudDeExigibles(ClavesDeRecaudo.Recaudador, [persona], fecha), ct);
        if (exigibles.Result == ResultadoDelContrato.Rejected) avisos.Add(exigibles.Message);
        var cuotas = (exigibles.Value?.Installments ?? []).Where(c => c.PersonPublicId == persona && c.Pending > 0m)
            .GroupBy(c => c.AccountPublicId).ToDictionary(g => g.Key, g => g.OrderBy(c => c.DueDate).ToList());

        var salida = new List<ChannelPayableDto>();
        foreach (var c in deCuentas.Value?.Accounts ?? [])
        {
            if (c.Role != RolEnLaCuenta.Holder || c.Status is not (EstadoDeCuenta.Active or EstadoDeCuenta.Inactive)) continue;
            if (ClaveDeConcepto.ClaseDeCuenta(c.Nature, c.DepositClass) is not { } clase) continue;
            if (publicados.Para(clase, c.LineCode) is not { } publicado) continue;
            var pendientes = cuotas.GetValueOrDefault(c.AccountPublicId) ?? [];
            var exigibleHoy = pendientes.Where(x => x.DueDate <= fecha).Sum(x => x.Pending);
            var primera = pendientes.FirstOrDefault();
            var modos = new ModosDelConceptoDto(
                Due: publicado.Concepto.AllowDue && primera is not null,
                Partial: publicado.Concepto.AllowPartial ? new PagoParcialDto(publicado.Concepto.PartialMinimum ?? 0m) : null,
                Prepayment: null,
                Payoff: false);
            var pagable = publicado.Destino is not null && c.AcceptsDeposits && c.BlockReasons.Count == 0 && ModosDelCanal.Alguno(modos);
            salida.Add(new ChannelPayableDto(
                ClaveDeConcepto.DeCuenta(c.AccountPublicId),
                clase.ToString(),
                c.LineCode,
                $"{ClaveDeConcepto.Nombre(clase)} {c.Number} · {c.LineName}",
                c.AccountPublicId,
                c.Number,
                c.Available.Balance,
                pendientes.Where(x => x.DueDate < fecha).Sum(x => x.Pending),
                0m,
                primera?.Amount ?? 0m,
                exigibleHoy,
                null,
                primera?.DueDate,
                0,
                publicado.Destino,
                pagable,
                modos,
                publicado.Concepto.CanCombine,
                primera is null ? null : new CuotaDeCuentaDelCanalDto(primera.Period, primera.DueDate, primera.Amount, primera.Pending),
                c.BlockReasons));
        }
        return salida;
    }
}

/// <summary>
/// La clave con que el canal nombra un concepto: <c>LOAN:{obligación}</c> o <c>ACCOUNT:{cuenta}</c>. Es estable mientras exista el
/// producto y no dice nada que el asociado no sepa.
/// </summary>
public static class ClaveDeConcepto
{
    public const string PrefijoCredito = "LOAN:";
    public const string PrefijoCuenta = "ACCOUNT:";

    /// <summary>Feature 021, C2b (T128): una factura pendiente de cuentas por cobrar comerciales, por el comprobante contable que la registró.</summary>
    public const string PrefijoFactura = "INVOICE:";

    public static string DeFactura(Guid documentoContable) => PrefijoFactura + documentoContable.ToString("D");

    public static string DeCredito(Guid obligacion) => PrefijoCredito + obligacion.ToString("D");

    public static string DeCuenta(Guid cuenta) => PrefijoCuenta + cuenta.ToString("D");

    /// <summary>Separa la clave; nulo si no tiene la forma.</summary>
    public static ClaveLeida? Leer(string? clave)
    {
        if (string.IsNullOrWhiteSpace(clave)) return null;
        if (clave.StartsWith(PrefijoCredito, StringComparison.Ordinal) && Guid.TryParse(clave[PrefijoCredito.Length..], out var o)) return new(true, o);
        if (clave.StartsWith(PrefijoCuenta, StringComparison.Ordinal) && Guid.TryParse(clave[PrefijoCuenta.Length..], out var c)) return new(false, c);
        if (clave.StartsWith(PrefijoFactura, StringComparison.Ordinal) && Guid.TryParse(clave[PrefijoFactura.Length..], out var f)) return new(false, f, EsFactura: true);
        return null;
    }

    /// <summary>Una clave separada: crédito, cuenta (ni crédito ni factura) o factura, y el producto (para la factura, su comprobante contable).</summary>
    public readonly record struct ClaveLeida(bool EsCredito, Guid Producto, bool EsFactura = false)
    {
        public bool EsCuenta => !EsCredito && !EsFactura;

        public void Deconstruct(out bool esCredito, out Guid producto) => (esCredito, producto) = (EsCredito, Producto);
    }

    /// <summary>La clase de concepto del canal de una cuenta; nula si el canal no la publica (ahorro a la vista, CDAT).</summary>
    public static ConceptKind? ClaseDeCuenta(NaturalezaDeCuenta naturaleza, ClaseDeCaptacion? clase) => naturaleza switch
    {
        NaturalezaDeCuenta.Contribution => ConceptKind.Contribution,
        NaturalezaDeCuenta.Deposit when clase == ClaseDeCaptacion.Contractual => ConceptKind.ContractualSaving,
        NaturalezaDeCuenta.Deposit when clase == ClaseDeCaptacion.Permanent => ConceptKind.ProgrammedSaving,
        _ => null,
    };

    public static string Nombre(ConceptKind clase) => clase switch
    {
        ConceptKind.Loan => "Crédito",
        ConceptKind.Contribution => "Aportes",
        ConceptKind.ContractualSaving => "Ahorro contractual",
        ConceptKind.ProgrammedSaving => "Ahorro programado",
        ConceptKind.CommercialReceivable => "Factura",
        _ => "Otro cobro",
    };
}

/// <summary>
/// Las modalidades con que el canal pide pagar (<c>Due</c>, <c>Partial</c>, <c>Prepayment</c>, <c>Payoff</c>) y su traducción a las de
/// los contratos de Cartera y Captaciones. El canal no conoce los nombres de los contratos.
/// </summary>
public static class ModosDelCanal
{
    public const string Due = "Due";
    public const string Partial = "Partial";
    public const string Prepayment = "Prepayment";
    public const string Payoff = "Payoff";

    public static bool Alguno(ModosDelConceptoDto m) => m.Due || m.Partial is not null || m.Prepayment is not null || m.Payoff;

    /// <summary>¿El concepto admite esa modalidad?</summary>
    public static bool Admite(ModosDelConceptoDto m, string modalidad) => modalidad switch
    {
        Due => m.Due,
        Partial => m.Partial is not null,
        Prepayment => m.Prepayment is not null,
        Payoff => m.Payoff,
        _ => false,
    };

    public static ModalidadDePago? DeCredito(string modalidad) => modalidad switch
    {
        Due => ModalidadDePago.PastDue,
        Partial => ModalidadDePago.FreeAmount,
        Prepayment => ModalidadDePago.ExtraordinaryPrincipal,
        Payoff => ModalidadDePago.FullPayoff,
        _ => null,
    };

    public static ModalidadDeConsignacion? DeCuenta(string modalidad) => modalidad switch
    {
        Due => ModalidadDeConsignacion.Installments,
        Partial => ModalidadDeConsignacion.Free,
        _ => null,
    };
}

/// <summary>Los conceptos publicados de la cooperativa con el destino de cada uno, para resolver un producto por clase y línea.</summary>
internal sealed class ConceptosPublicados
{
    private readonly List<(ChannelPublishedConcept Concepto, Guid? Destino)> _filas;

    private ConceptosPublicados(List<(ChannelPublishedConcept, Guid?)> filas) => _filas = filas;

    public static async Task<ConceptosPublicados> CargarAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var filas = await (from c in db.ChannelPublishedConcepts.AsNoTracking()
                           join d in db.ChannelPaymentDestinations.AsNoTracking() on c.DestinationId equals (int?)d.Id into ds
                           from d in ds.DefaultIfEmpty()
                           select new { Concepto = c, Destino = d == null || !d.IsActive ? (Guid?)null : d.PublicId }).ToListAsync(ct);
        return new ConceptosPublicados(filas.Select(f => (f.Concepto, f.Destino)).ToList());
    }

    public bool Hay(ConceptKind clase) => _filas.Any(f => f.Concepto.Kind == clase);

    /// <summary>La publicación de la línea; si no hay, la de toda la clase (línea nula); nula si la clase no se publica para esa línea.</summary>
    public (ChannelPublishedConcept Concepto, Guid? Destino)? Para(ConceptKind clase, string linea)
    {
        var deLaClase = _filas.Where(f => f.Concepto.Kind == clase).ToList();
        var exacta = deLaClase.FirstOrDefault(f => string.Equals(f.Concepto.LineCode, linea, StringComparison.OrdinalIgnoreCase));
        if (exacta.Concepto is not null) return exacta;
        var general = deLaClase.FirstOrDefault(f => f.Concepto.LineCode is null);
        return general.Concepto is null ? null : general;
    }
}
