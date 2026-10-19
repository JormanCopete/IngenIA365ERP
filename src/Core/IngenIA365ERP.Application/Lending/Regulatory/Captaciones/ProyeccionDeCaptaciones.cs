using System.Globalization;
using IngenIA365ERP.Application.Common.Calendar;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Accrual;
using IngenIA365ERP.Application.Lending.Deposits.Liquidity;
using IngenIA365ERP.Domain.Common.Layouts;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Regulatory.Captaciones;

/// <summary>Lo que entrega la proyección de un formato de captaciones: las filas, las advertencias, los totales, el plazo informativo y el cierre. (nuevo)</summary>
public sealed record ProyeccionRegulatoriaDeCaptaciones(
    FormatoDeCaptaciones Formato,
    int Anio,
    int Mes,
    DateOnly Corte,
    IReadOnlyList<FilaDeLayout> Filas,
    IReadOnlyList<AdvertenciaRegulatoria> Advertencias,
    IReadOnlyDictionary<string, object?> Totales,
    PlazoDelSicses? Plazo,
    int? CierreMensualId);

/// <summary>Los errores de los formatos regulatorios de captaciones (api.md §18.2). (nuevo)</summary>
public static class ErroresDeFormatosDeCaptaciones
{
    public const string CodigoFormatoDesconocido = "Lending.Regulatory.FormatUnknown";
    public const string CodigoPeriodoSinCerrar = "Lending.Regulatory.PeriodNotClosed";
    public const string CodigoSinAlcanceTotal = "Lending.Regulatory.ScopeNotTotal";

    public static Error FormatoDesconocido(string formato) => new ErrorConDatos(CodigoFormatoDesconocido,
        $"«{formato}» no es un formato regulatorio de captaciones. Formatos: {string.Join(", ", FormatosDeCaptaciones.Todos.Select(f => f.Codigo))}.",
        new { formatCode = formato, known = FormatosDeCaptaciones.Todos.Select(f => f.Codigo).ToList() });

    public static Error PeriodoSinCerrar(FormatoDeCaptaciones formato, int anio, int mes) => new ErrorConDatos(CodigoPeriodoSinCerrar,
        formato.Anual
            ? $"El año {anio} no está cerrado: la exógena sale del año completo y la Cartera no tiene cierre mensual vigente de diciembre de {anio}."
            : string.Create(CultureInfo.InvariantCulture,
                $"La Cartera no tiene cierre mensual vigente de {anio:0000}-{mes:00}: el formato sale del mes cerrado. Cierre el mes (o vuelva a cerrarlo si se reabrió)."),
        new { formatCode = formato.Codigo, year = anio, month = mes });

    public static Error SinAlcanceTotal() => new(CodigoSinAlcanceTotal,
        "Los formatos regulatorios son de toda la cooperativa: los consulta y los genera quien tiene alcance sobre todas las oficinas y líneas.");

    public static Error SinLayout(FormatoDeCaptaciones formato, DateOnly fin) => new ErrorConDatos(ErroresDeArchivosRegulatorios.CodigoSinLayout,
        $"No hay layout del formato {formato.Codigo} vigente al {fin:yyyy-MM-dd}.", new { formatCode = formato.Codigo, periodEnd = fin });

    public static Error MesRequerido(FormatoDeCaptaciones formato) => new ErrorConDatos("Lending.Regulatory.PeriodInvalid",
        $"El formato {formato.Codigo} es mensual: indique el mes (1 a 12).", new { formatCode = formato.Codigo });
}

/// <summary>Las advertencias propias de los formatos de captaciones; ninguna va dentro del archivo. (nuevo)</summary>
public static class AdvertenciasDeCaptaciones
{
    public const string UmbralSinValor = "Lending.Regulatory.UmbralSinValor";
    public const string FondoConInversionSinTipo = "Lending.Regulatory.InversionSinTipo";

    public static AdvertenciaRegulatoria SinUmbral(int anio) => new(UmbralSinValor,
        $"No hay UVT del año {anio} o umbral EXOGENA_1019_UMBRAL_UVT vigente: el formato 1019 lleva a todas las personas con cuenta de ahorro, sin umbral.");
}

/// <summary>
/// La proyección de los formatos regulatorios de captaciones (feature 014, US17, T220; contracts/plantillas-y-archivos.md §8.1; FR-111, FR-112): del libro de
/// cuentas (la autoridad: los saldos son la suma de las filas con fecha valor hasta el corte), los titulares y cotitulares vigentes al corte, las condiciones
/// selladas del CDAT y del contractual, la tasa de la línea, las marcas de GMF y embargo, las cuotas, el fondo de liquidez (por
/// <see cref="FondoDeLiquidezDeCartera"/>) y los acumulados tributarios. El SICSES exige el cierre mensual vigente del mes; la exógena, el de diciembre. El
/// plazo del SICSES es <b>informativo</b> (decisión del dueño del 2026-10-18): sólo da la advertencia del mes fuera de la periodicidad. Sólo lee. (nuevo)
/// </summary>
public sealed class ProyeccionDeCaptaciones(
    IApplicationDbContext db,
    FondoDeLiquidezDeCartera fondo,
    IValoresNacionales valores,
    IPerfilRegulatorio perfiles,
    INormativaVigente normativa,
    ICalendarioHabil? calendario = null,
    ILectorDeParametros? parametros = null)
{
    private static readonly MemberAccountTransactionKind[] ClasesDeMovimientoDelTitular =
    [
        MemberAccountTransactionKind.Deposit, MemberAccountTransactionKind.Withdrawal, MemberAccountTransactionKind.TransferIn,
        MemberAccountTransactionKind.TransferOut,
    ];

    public async Task<Result<ProyeccionRegulatoriaDeCaptaciones>> ProyectarAsync(string codigo, int anio, int? mes, CancellationToken ct)
    {
        var formato = FormatosDeCaptaciones.Buscar(codigo);
        if (formato is null) return Falla(ErroresDeFormatosDeCaptaciones.FormatoDesconocido(codigo));
        if (!formato.Anual && mes is not (>= 1 and <= 12)) return Falla(ErroresDeFormatosDeCaptaciones.MesRequerido(formato));
        var mesDelPeriodo = formato.MesDelPeriodo(mes);
        var cierre = await ProyeccionSicsesCartera.CierreVigenteAsync(db, anio, mesDelPeriodo, ct);
        if (cierre is null) return Falla(ErroresDeFormatosDeCaptaciones.PeriodoSinCerrar(formato, anio, mesDelPeriodo));
        var corte = formato.FinDelPeriodo(anio, mes);

        var advertencias = new List<AdvertenciaRegulatoria>();
        var totales = new Dictionary<string, object?>(StringComparer.Ordinal) { ["Cierre.Version"] = cierre.Version, ["Corte"] = corte };
        IReadOnlyList<FilaDeLayout> filas = formato.Codigo switch
        {
            FormatosDeCaptaciones.Captaciones => await CaptacionesAsync(corte, totales, ct),
            FormatosDeCaptaciones.Cotitulares => await CotitularesAsync(corte, totales, ct),
            FormatosDeCaptaciones.Aportes => await AportesAsync(corte, totales, ct),
            FormatosDeCaptaciones.SaldosDiarios => await SaldosDiariosAsync(anio, mesDelPeriodo, totales, ct),
            FormatosDeCaptaciones.FondoDeLiquidez => await FondoDeLiquidezAsync(corte, totales, advertencias, ct),
            FormatosDeCaptaciones.Exogena1019 => await Exogena1019Async(anio, totales, advertencias, ct),
            _ => await Exogena1020Async(anio, totales, ct),
        };

        PlazoDelSicses? plazo = null;
        if (!formato.Anual)
        {
            plazo = await ProyeccionSicsesCartera.PlazoAsync(perfiles, normativa, anio, mesDelPeriodo, ct, calendario, parametros ?? new LectorDeParametros(db));
            if (plazo is { SeReporta: false } p) advertencias.Add(AdvertenciasRegulatorias.MesFueraDePeriodicidad(anio, mesDelPeriodo, p.NivelDeSupervision));
        }
        totales["Totales.Registros"] = filas.Count(f => f.Registro == FilasDeCaptaciones.Detalle);
        return Result.Success(new ProyeccionRegulatoriaDeCaptaciones(formato, anio, mesDelPeriodo, corte, filas, advertencias, totales, plazo, cierre.Id));
    }

    // ----------------------------------------------------------------------------------------------------------- 9026 --

    private async Task<IReadOnlyList<FilaDeLayout>> CaptacionesAsync(DateOnly corte, Dictionary<string, object?> totales, CancellationToken ct)
    {
        var cuentas = await VivasAsync(LineNature.Deposit, corte, ct);
        var ids = cuentas.Select(c => c.Id).ToList();
        var saldos = await SaldosAsync(ids, corte, ct);
        var personas = await IdentificacionesAsync(cuentas.Select(c => c.HolderPersonId), ct);
        var cotitulares = await CotitularesVigentesAsync(ids, corte, ct);
        var lineas = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => cuentas.Select(c => c.CreditLineId).Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.Name, ct);
        var oficinas = await OficinasAsync(cuentas.Select(c => c.BranchId), ct);
        var condiciones = (await db.MemberAccountTermsVersions.AsNoTracking().Where(v => ids.Contains(v.MemberAccountId) && v.EffectiveValueDate <= corte)
                .Select(v => new { v.MemberAccountId, v.VersionNumber, v.TermDays, v.MaturityDate, v.AppliedAnnualRate, v.InterestPeriodicity }).ToListAsync(ct))
            .GroupBy(v => v.MemberAccountId).ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.VersionNumber).First());
        var exentas = await ExentasDeGmfAsync(ids, corte, corte, ct);
        var cuotas = (await db.AccountInstallments.AsNoTracking().Where(i => ids.Contains(i.MemberAccountId) && i.CancelledAt == null && i.DueDate <= corte)
                .Select(i => new { i.MemberAccountId, i.DueDate, i.Amount, i.Id }).ToListAsync(ct))
            .GroupBy(i => i.MemberAccountId).ToDictionary(g => g.Key, g => g.OrderByDescending(i => i.DueDate).ThenByDescending(i => i.Id).First().Amount);
        var ultimos = await db.MemberAccountTransactions.AsNoTracking()
            .Where(t => ids.Contains(t.MemberAccountId) && !t.IsDeleted && t.ValueDate <= corte && ClasesDeMovimientoDelTitular.Contains(t.Kind))
            .GroupBy(t => t.MemberAccountId).Select(g => new { g.Key, Fecha = g.Max(t => t.ValueDate) }).ToDictionaryAsync(x => x.Key, x => x.Fecha, ct);
        var lector = new LectorDeInteres(db, valores);

        var resultado = new List<CuentaDelSicses>(cuentas.Count);
        foreach (var c in cuentas)
        {
            var s = saldos.GetValueOrDefault(c.Id);
            var capital = s?.GetValueOrDefault(MemberAccountConcept.Principal) ?? 0m;
            var interes = (s?.GetValueOrDefault(MemberAccountConcept.InterestPayable) ?? 0m) + (s?.GetValueOrDefault(MemberAccountConcept.Bonus) ?? 0m);
            condiciones.TryGetValue(c.Id, out var v);
            var tasa = v?.AppliedAnnualRate ?? await TasaDeLaLineaAsync(lector, c, corte, capital, ct);
            var periodicidad = v?.InterestPeriodicity is { } p && p != RepaymentPeriodicity.SinglePayment ? (int?)p : null;
            resultado.Add(new CuentaDelSicses
            {
                Numero = c.Number,
                Titular = personas.GetValueOrDefault(c.HolderPersonId) ?? new IdentificacionDePersona(string.Empty, string.Empty),
                Clase = c.DepositClass,
                Linea = lineas.GetValueOrDefault(c.CreditLineId) ?? string.Empty,
                Oficina = oficinas.GetValueOrDefault(c.BranchId) ?? string.Empty,
                Estado = c.Status,
                FechaDeApertura = c.OpenedOn,
                PlazoEnDias = v?.TermDays,
                FechaDeVencimiento = v?.MaturityDate,
                PeriodicidadDeInteresesEnDias = periodicidad,
                TasaEfectivaAnual = tasa,
                Capital = capital,
                InteresPorPagar = interes,
                ValorDeLaCuota = cuotas.TryGetValue(c.Id, out var cuota) ? cuota : null,
                FechaDelUltimoMovimiento = ultimos.TryGetValue(c.Id, out var u) ? u : null,
                ExentaDeGmf = exentas.Contains(c.Id),
                TieneCotitulares = cotitulares.Contains(c.Id),
                Manejo = c.HandlingMode,
                Embargada = c.IsGarnished,
            });
        }
        totales["Totales.Capital"] = resultado.Sum(c => c.Capital);
        totales["Totales.InteresPorPagar"] = decimal.Round(resultado.Sum(c => c.InteresPorPagar), 2, MidpointRounding.AwayFromZero);
        return FilasDeCaptaciones.Captaciones(resultado);
    }

    /// <summary>La tasa EA de la línea que rige la cuenta al corte (la del tramo del saldo, si los tiene); nula si no se resuelve.</summary>
    private static async Task<decimal?> TasaDeLaLineaAsync(LectorDeInteres lector, MemberAccount cuenta, DateOnly corte, decimal capital, CancellationToken ct)
    {
        var versiones = await lector.VersionesAsync(cuenta.CreditLineId, ct);
        var vt = LectorDeInteres.VersionDelDia(versiones, cuenta, corte, DateTime.MaxValue);
        if (vt is null) return null;
        var tasa = await lector.TasaAsync(vt, corte, ct);
        if (tasa.IsFailure) return null;
        var t = tasa.Value;
        if (!t.TieneTramos) return t.TasaEA;
        return t.Tramos.FirstOrDefault(r => capital >= r.Desde && (r.Hasta is null || capital <= r.Hasta))?.TasaEA ?? t.TasaEA;
    }

    // ----------------------------------------------------------------------------------------------------------- 9068 --

    private async Task<IReadOnlyList<FilaDeLayout>> CotitularesAsync(DateOnly corte, Dictionary<string, object?> totales, CancellationToken ct)
    {
        var cuentas = await VivasAsync(LineNature.Deposit, corte, ct);
        var ids = cuentas.Select(c => c.Id).ToList();
        var partes = await db.MemberAccountParties.AsNoTracking()
            .Where(p => ids.Contains(p.MemberAccountId) && !p.IsDeleted && p.Role == MemberAccountPartyRole.CoHolder && p.ValidFrom <= corte
                        && (p.ValidTo == null || p.ValidTo >= corte))
            .Select(p => new { p.MemberAccountId, p.PersonId }).ToListAsync(ct);
        var personas = await IdentificacionesAsync(cuentas.Select(c => c.HolderPersonId).Concat(partes.Select(p => p.PersonId)), ct);
        var porId = cuentas.ToDictionary(c => c.Id);
        var filas = partes.Select(p =>
        {
            var c = porId[p.MemberAccountId];
            return new CotitularDelSicses(personas.GetValueOrDefault(c.HolderPersonId) ?? new(string.Empty, string.Empty),
                personas.GetValueOrDefault(p.PersonId) ?? new(string.Empty, string.Empty), c.DepositClass, c.Number);
        }).ToList();
        totales["Totales.CuentasConCotitulares"] = partes.Select(p => p.MemberAccountId).Distinct().Count();
        return FilasDeCaptaciones.Cotitulares(filas);
    }

    // ----------------------------------------------------------------------------------------------------------- 9013 --

    private async Task<IReadOnlyList<FilaDeLayout>> AportesAsync(DateOnly corte, Dictionary<string, object?> totales, CancellationToken ct)
    {
        var cuentas = await VivasAsync(LineNature.Contribution, corte, ct);
        var ids = cuentas.Select(c => c.Id).ToList();
        var conceptos = MemberAccountTransaction.ConceptosDeAportes.ToList();
        var filasDelLibro = await db.MemberAccountTransactions.AsNoTracking()
            .Where(t => ids.Contains(t.MemberAccountId) && !t.IsDeleted && t.ValueDate <= corte && conceptos.Contains(t.Concept))
            .Select(t => new { t.MemberAccountId, t.Concept, t.Kind, t.Amount, t.ValueDate }).ToListAsync(ct);
        var personas = await IdentificacionesAsync(cuentas.Select(c => c.HolderPersonId), ct);
        var inicioDelEjercicio = new DateOnly(corte.Year, 1, 1);
        var cuotas = await db.AccountInstallments.AsNoTracking()
            .Where(i => ids.Contains(i.MemberAccountId) && i.CancelledAt == null && i.PeriodYear == corte.Year && i.PeriodMonth == corte.Month)
            .Select(i => new { i.MemberAccountId, Valor = i.ContributionShare ?? i.Amount }).ToListAsync(ct);

        var asociados = cuentas.GroupBy(c => c.HolderPersonId).Select(g =>
        {
            var deEllas = g.Select(c => c.Id).ToHashSet();
            var libro = filasDelLibro.Where(f => deEllas.Contains(f.MemberAccountId)).ToList();
            decimal Suma(MemberAccountConcept concepto) => libro.Where(f => f.Concept == concepto).Sum(f => f.Amount);
            var desde = new[] { inicioDelEjercicio, g.Min(c => c.OpenedOn) }.Max();
            var serie = EstadisticasDeSaldos.SerieDiaria(libro.Select(f => (f.ValueDate, f.Amount)), desde, corte);
            var pagos = libro.Where(f => f.Amount > 0m && f.Kind is MemberAccountTransactionKind.Deposit or MemberAccountTransactionKind.TransferIn).ToList();
            return new AsociadoDelSicses
            {
                Asociado = personas.GetValueOrDefault(g.Key) ?? new IdentificacionDePersona(string.Empty, string.Empty),
                Minimos = Suma(MemberAccountConcept.ContributionMinimum),
                Ordinarios = Suma(MemberAccountConcept.ContributionOrdinary),
                Extraordinarios = Suma(MemberAccountConcept.ContributionExtraordinary),
                Revalorizados = Suma(MemberAccountConcept.ContributionRevaluation),
                CuotaMensual = cuotas.Where(c => deEllas.Contains(c.MemberAccountId)).Sum(c => c.Valor),
                PromedioDelEjercicio = EstadisticasDeSaldos.Resumir(serie.Select(d => d.Saldo)).Promedio,
                UltimaFechaDePago = pagos.Count == 0 ? null : pagos.Max(p => p.ValueDate),
            };
        }).ToList();
        totales["Totales.Aportes"] = asociados.Sum(a => a.Saldo);
        return FilasDeCaptaciones.Aportes(asociados);
    }

    // ------------------------------------------------------------------------------------------------------------ 169 --

    private async Task<IReadOnlyList<FilaDeLayout>> SaldosDiariosAsync(int anio, int mes, Dictionary<string, object?> totales, CancellationToken ct)
    {
        var desde = new DateOnly(anio, mes, 1);
        var hasta = desde.AddMonths(1).AddDays(-1);
        var movimientos = await (from t in db.MemberAccountTransactions.AsNoTracking()
                                 join a in db.MemberAccounts.AsNoTracking() on t.MemberAccountId equals a.Id
                                 where a.Nature == LineNature.Deposit && a.DepositClass == DepositClass.Demand && !a.IsDeleted && !t.IsDeleted
                                       && t.Concept == MemberAccountConcept.Principal && t.ValueDate <= hasta
                                 group t by t.ValueDate into g
                                 select new { Fecha = g.Key, Monto = g.Sum(x => x.Amount) }).ToListAsync(ct);
        var serie = EstadisticasDeSaldos.SerieDiaria(movimientos.Select(m => (m.Fecha, m.Monto)), desde, hasta);
        totales["Totales.SaldoDelUltimoDia"] = serie.Count == 0 ? 0m : serie[^1].Saldo;
        return FilasDeCaptaciones.SaldosDiarios(serie);
    }

    // ----------------------------------------------------------------------------------------------------------- 9067 --

    private async Task<IReadOnlyList<FilaDeLayout>> FondoDeLiquidezAsync(DateOnly corte, Dictionary<string, object?> totales,
        List<AdvertenciaRegulatoria> advertencias, CancellationToken ct)
    {
        var depositos = await fondo.DepositosAsync(corte, ct);
        decimal Suma(params ClaseDeDepositoParaLiquidez[] clases) => depositos.Where(d => clases.Contains(d.Clase)).Sum(d => d.Capital + d.InteresCausado);
        var encabezado = new EncabezadoDelFondoDeLiquidez(Suma(ClaseDeDepositoParaLiquidez.ALaVista), Suma(ClaseDeDepositoParaLiquidez.Cdat),
            Suma(ClaseDeDepositoParaLiquidez.Contractual),
            Suma(ClaseDeDepositoParaLiquidez.PermanenteConRetiroParcial, ClaseDeDepositoParaLiquidez.PermanenteSoloAlRetirarse),
            depositos.Any(d => d.Clase == ClaseDeDepositoParaLiquidez.PermanenteConRetiroParcial));
        var inversiones = await (from h in db.LiquidityFundHoldings.AsNoTracking()
                                 join p in db.People.AsNoTracking().IgnoreQueryFilters() on h.InstitutionPersonId equals p.Id
                                 where !h.IsDeleted && h.ConstitutedOn <= corte && (h.WithdrawnOn == null || h.WithdrawnOn > corte)
                                 select new { h.Kind, h.Reference, h.Amount, h.ConstitutedOn, h.MaturesOn, p.TaxId, p.TaxIdCheckDigit, p.BusinessName, p.FirstName,
                                     p.LastName }).ToListAsync(ct);
        var titulos = inversiones.Select(h => new TituloDelFondoDeLiquidez(
            string.IsNullOrWhiteSpace(h.TaxIdCheckDigit) ? h.TaxId.Trim() : $"{h.TaxId.Trim()}-{h.TaxIdCheckDigit.Trim()}",
            string.IsNullOrWhiteSpace(h.BusinessName) ? $"{h.FirstName} {h.LastName}".Trim() : h.BusinessName.Trim(), h.Kind, h.Reference, h.Amount, 0m,
            h.MaturesOn is { } v ? v.DayNumber - h.ConstitutedOn.DayNumber : null, h.MaturesOn, false)).ToList();
        if (titulos.Any(t => TraduccionesDeCaptaciones.TituloDelFondo(t.Clase) is null))
            advertencias.Add(new AdvertenciaRegulatoria(AdvertenciasDeCaptaciones.FondoConInversionSinTipo,
                "Hay inversiones del fondo registradas como «otra»: el formato no tiene tipo de título para ellas y el validador las bloquea."));
        totales["Totales.SaldoDeDepositos"] = encabezado.SaldoDeDepositos;
        totales["Totales.Constituido"] = titulos.Sum(t => t.Capital);
        return FilasDeCaptaciones.FondoDeLiquidez(encabezado, titulos);
    }

    // ----------------------------------------------------------------------------------------------------------- 1019 --

    private async Task<IReadOnlyList<FilaDeLayout>> Exogena1019Async(int anio, Dictionary<string, object?> totales, List<AdvertenciaRegulatoria> advertencias,
        CancellationToken ct)
    {
        var (inicio, fin) = (new DateOnly(anio, 1, 1), new DateOnly(anio, 12, 31));
        var cuentas = await DelAnioAsync(LineNature.Deposit, inicio, fin, ct);
        cuentas = cuentas.Where(c => c.DepositClass != DepositClass.TermDeposit).ToList();
        var ids = cuentas.Select(c => c.Id).ToList();
        var libro = (await db.MemberAccountTransactions.AsNoTracking()
                .Where(t => ids.Contains(t.MemberAccountId) && !t.IsDeleted && t.Concept == MemberAccountConcept.Principal && t.ValueDate <= fin)
                .Select(t => new { t.MemberAccountId, t.OperationId, t.Id, t.Kind, t.Amount, t.ValueDate }).ToListAsync(ct))
            .ToLookup(t => t.MemberAccountId);
        var partes = await db.MemberAccountParties.AsNoTracking()
            .Where(p => ids.Contains(p.MemberAccountId) && !p.IsDeleted && p.Role == MemberAccountPartyRole.CoHolder && p.ValidFrom <= fin
                        && (p.ValidTo == null || p.ValidTo >= inicio))
            .Select(p => new { p.MemberAccountId, p.PersonId }).Distinct().ToListAsync(ct);
        var exentas = await ExentasDeGmfAsync(ids, inicio, fin, ct);

        var cifras = new Dictionary<int, (ResumenDeSerie Saldos, decimal Final, ResumenDeSerie Creditos, ResumenDeSerie Debitos, decimal[] CreditosPorMes)>();
        foreach (var c in cuentas)
        {
            var filas = libro[c.Id].ToList();
            var desde = c.OpenedOn > inicio ? c.OpenedOn : inicio;
            var hasta = c.ClosedOn is { } cerrada && cerrada < fin ? cerrada : fin;
            var serie = EstadisticasDeSaldos.SerieDiaria(filas.Select(f => (f.ValueDate, f.Amount)), desde, hasta);
            // Un movimiento es una operación (o la fila suelta): la migración del saldo inicial y las reclasificaciones no son movimientos del titular.
            var movimientos = filas.Where(f => f.ValueDate >= inicio && f.Kind is not (MemberAccountTransactionKind.OpeningBalance or MemberAccountTransactionKind.Reclassification))
                .GroupBy(f => f.OperationId ?? -f.Id).Select(g => (Fecha: g.Min(x => x.ValueDate), Monto: g.Sum(x => x.Amount))).Where(m => m.Monto != 0m).ToList();
            var porMes = new decimal[13];
            foreach (var m in movimientos.Where(m => m.Monto > 0m)) porMes[m.Fecha.Month] += m.Monto;
            cifras[c.Id] = (EstadisticasDeSaldos.Resumir(serie.Select(d => d.Saldo)), filas.Sum(f => f.Amount),
                EstadisticasDeSaldos.Resumir(movimientos.Where(m => m.Monto > 0m).Select(m => m.Monto)),
                EstadisticasDeSaldos.Resumir(movimientos.Where(m => m.Monto < 0m).Select(m => -m.Monto)), porMes);
        }

        // Quién entra: la persona cuyo saldo de fin de año sumado o cuyos créditos de algún mes sumados llegan al umbral (en todas sus cuentas).
        var titularidades = cuentas.Select(c => (Cuenta: c.Id, Persona: c.HolderPersonId, Principal: true))
            .Concat(partes.Select(p => (Cuenta: p.MemberAccountId, Persona: p.PersonId, Principal: false))).Distinct().ToList();
        var umbral = await UmbralDe1019Async(anio, ct);
        if (umbral is null) advertencias.Add(AdvertenciasDeCaptaciones.SinUmbral(anio));
        var personasQueEntran = titularidades.GroupBy(t => t.Persona).Where(g =>
        {
            if (umbral is not { } u) return true;
            var deEllas = g.Select(t => cifras[t.Cuenta]).ToList();
            if (deEllas.Sum(x => x.Final) >= u) return true;
            for (var m = 1; m <= 12; m++)
                if (deEllas.Sum(x => x.CreditosPorMes[m]) > u) return true;
            return false;
        }).Select(g => g.Key).ToHashSet();

        var personas = await PersonasDeLaExogenaAsync(personasQueEntran, ct);
        var porId = cuentas.ToDictionary(c => c.Id);
        var filasDeLaExogena = titularidades.Where(t => personasQueEntran.Contains(t.Persona) && personas.ContainsKey(t.Persona)).Select(t =>
        {
            var c = porId[t.Cuenta];
            var x = cifras[t.Cuenta];
            return new CuentaDeLaExogena1019
            {
                Persona = personas[t.Persona], Numero = c.Number, Clase = c.DepositClass, EsTitularPrincipal = t.Principal, ExentaDeGmf = exentas.Contains(c.Id),
                Saldos = x.Saldos, SaldoFinal = x.Final, Creditos = x.Creditos, Debitos = x.Debitos,
            };
        }).ToList();
        totales["Totales.Personas"] = personasQueEntran.Count;
        totales["Totales.Umbral"] = umbral;
        totales["Totales.SaldoFinal"] = filasDeLaExogena.Where(f => f.EsTitularPrincipal).Sum(f => f.SaldoFinal);
        return FilasDeCaptaciones.Exogena1019(filasDeLaExogena);
    }

    /// <summary>El umbral de la 1019 en pesos: la UVT del año por <c>EXOGENA_1019_UMBRAL_UVT</c>; nulo si falta alguno.</summary>
    private async Task<decimal?> UmbralDe1019Async(int anio, CancellationToken ct)
    {
        var fin = new DateOnly(anio, 12, 31);
        var uvt = await valores.LeerAsync(CodigosDeValorNacional.Uvt, string.Empty, fin, ct: ct);
        var veces = await valores.LeerAsync(CodigosDeValorNacional.Limite, CatalogoDeLimitesLegales.Exogena1019UmbralUvt, fin, ct: ct);
        return uvt is { IsSuccess: true, Value.Valor: { } u } && veces is { IsSuccess: true, Value.Valor: { } v } ? u * v : null;
    }

    // ----------------------------------------------------------------------------------------------------------- 1020 --

    private async Task<IReadOnlyList<FilaDeLayout>> Exogena1020Async(int anio, Dictionary<string, object?> totales, CancellationToken ct)
    {
        var (inicio, fin) = (new DateOnly(anio, 1, 1), new DateOnly(anio, 12, 31));
        var cuentas = (await DelAnioAsync(LineNature.Deposit, inicio, fin, ct)).Where(c => c.DepositClass == DepositClass.TermDeposit).ToList();
        var ids = cuentas.Select(c => c.Id).ToList();
        var libro = (await db.MemberAccountTransactions.AsNoTracking()
                .Where(t => ids.Contains(t.MemberAccountId) && !t.IsDeleted && t.ValueDate <= fin
                            && (t.Concept == MemberAccountConcept.Principal || t.Concept == MemberAccountConcept.InterestPayable))
                .Select(t => new { t.MemberAccountId, t.Kind, t.Concept, t.Amount, t.ValueDate }).ToListAsync(ct))
            .ToLookup(t => t.MemberAccountId);
        var partes = await db.MemberAccountParties.AsNoTracking()
            .Where(p => ids.Contains(p.MemberAccountId) && !p.IsDeleted && p.Role == MemberAccountPartyRole.CoHolder && p.ValidFrom <= fin
                        && (p.ValidTo == null || p.ValidTo >= inicio))
            .Select(p => new { p.MemberAccountId, p.PersonId, p.Share, p.ValidFrom, p.ValidTo, p.Id }).ToListAsync(ct);
        var renovaciones = await db.MemberAccountTermsVersions.AsNoTracking()
            .Where(v => ids.Contains(v.MemberAccountId) && v.EffectiveValueDate >= inicio && v.EffectiveValueDate <= fin
                        && (v.Cause == AccountTermsCause.Rollover || v.Cause == AccountTermsCause.Renewal))
            .Select(v => new { v.MemberAccountId, v.EffectiveValueDate }).ToListAsync(ct);
        var acumulados = await (from a in db.WithholdingAccumulations.AsNoTracking()
                                join o in db.MemberAccountOperations.AsNoTracking() on a.OperationId equals o.Id
                                where ids.Contains(a.MemberAccountId) && a.TaxYear == anio
                                select new { a.MemberAccountId, a.PersonId, o.ValueDate, a.GrossInterest, a.WithholdingAmount }).ToListAsync(ct);
        var causacion = LectorDeInteres.ClasesDeLoCausado.Where(k => k != MemberAccountTransactionKind.OpeningBalance).ToHashSet();

        var filas = new List<(int Persona, TituloDeLaExogena1020 SinPersona)>();
        foreach (var c in cuentas)
        {
            var delLibro = libro[c.Id].ToList();
            for (var m = 1; m <= 12; m++)
            {
                var desde = new DateOnly(anio, m, 1);
                var hasta = desde.AddMonths(1).AddDays(-1);
                if (c.OpenedOn > hasta || c.ClosedOn is { } cerrada && cerrada < desde) continue;
                var capital = delLibro.Where(t => t.Concept == MemberAccountConcept.Principal).ToList();
                var inicial = capital.Where(t => t.ValueDate < desde).Sum(t => t.Amount);
                var final = capital.Where(t => t.ValueDate <= hasta).Sum(t => t.Amount);
                var inversion = capital.Where(t => t.ValueDate >= desde && t.ValueDate <= hasta && t.Amount > 0m
                                                   && t.Kind is MemberAccountTransactionKind.Deposit or MemberAccountTransactionKind.TransferIn).Sum(t => t.Amount);
                var causados = delLibro.Where(t => t.Concept == MemberAccountConcept.InterestPayable && t.ValueDate >= desde && t.ValueDate <= hasta
                                                   && causacion.Contains(t.Kind)).Sum(t => t.Amount);
                var movimiento = c.OpenedOn >= desde && c.OpenedOn <= hasta && c.Origin != MemberAccountOrigin.Migration ? MovimientoDeCdatEnElMes.Emision
                    : c.ClosedOn is { } cierre && cierre >= desde && cierre <= hasta ? MovimientoDeCdatEnElMes.Cancelacion
                    : renovaciones.Any(r => r.MemberAccountId == c.Id && r.EffectiveValueDate >= desde && r.EffectiveValueDate <= hasta) ? MovimientoDeCdatEnElMes.Renovacion
                    : MovimientoDeCdatEnElMes.Vigente;
                var cotitulares = partes.Where(p => p.MemberAccountId == c.Id && p.ValidFrom <= hasta && (p.ValidTo == null || p.ValidTo >= hasta))
                    .OrderBy(p => p.Id).Select(p => (p.PersonId, p.Share)).ToList();
                var proporciones = ProporcionesDeTitularidad.Calcular(c.HolderPersonId, cotitulares);
                IReadOnlyDictionary<int, decimal> Repartir(decimal v) => ProporcionesDeTitularidad.Repartir(v, proporciones);
                var (ri, rf, rv, rc) = (Repartir(inicial), Repartir(final), Repartir(inversion), Repartir(causados));
                foreach (var persona in proporciones.Select(p => p.Persona).Distinct())
                {
                    var suyos = acumulados.Where(a => a.MemberAccountId == c.Id && a.PersonId == persona && a.ValueDate >= desde && a.ValueDate <= hasta).ToList();
                    filas.Add((persona, new TituloDeLaExogena1020
                    {
                        Persona = null!, Numero = c.Number, Mes = m, Movimiento = movimiento, SaldoInicial = ri.GetValueOrDefault(persona),
                        ValorDeLaInversion = rv.GetValueOrDefault(persona), InteresesCausados = rc.GetValueOrDefault(persona),
                        InteresesPagados = suyos.Sum(a => a.GrossInterest), Retencion = suyos.Sum(a => a.WithholdingAmount), SaldoFinal = rf.GetValueOrDefault(persona),
                    }));
                }
            }
        }
        var personas = await PersonasDeLaExogenaAsync(filas.Select(f => f.Persona), ct);
        var resultado = filas.Where(f => personas.ContainsKey(f.Persona)).Select(f => f.SinPersona with { Persona = personas[f.Persona] }).ToList();
        totales["Totales.Cdat"] = cuentas.Count;
        totales["Totales.InteresesPagados"] = resultado.Sum(r => r.InteresesPagados);
        totales["Totales.Retencion"] = resultado.Sum(r => r.Retencion);
        return FilasDeCaptaciones.Exogena1020(resultado);
    }

    // ---------------------------------------------------------------------------------------------------------- lectura --

    /// <summary>Las cuentas de la naturaleza vivas al corte (abiertas, sin cerrar ese día; la que todavía se abre no cuenta).</summary>
    private Task<List<MemberAccount>> VivasAsync(LineNature naturaleza, DateOnly corte, CancellationToken ct) =>
        db.MemberAccounts.AsNoTracking()
            .Where(a => a.Nature == naturaleza && !a.IsDeleted && a.Status != MemberAccountStatus.Opening && a.OpenedOn <= corte
                        && (a.ClosedOn == null || a.ClosedOn > corte))
            .OrderBy(a => a.Id).ToListAsync(ct);

    /// <summary>Las cuentas de la naturaleza que existieron algún día del año.</summary>
    private Task<List<MemberAccount>> DelAnioAsync(LineNature naturaleza, DateOnly inicio, DateOnly fin, CancellationToken ct) =>
        db.MemberAccounts.AsNoTracking()
            .Where(a => a.Nature == naturaleza && !a.IsDeleted && a.Status != MemberAccountStatus.Opening && a.OpenedOn <= fin
                        && (a.ClosedOn == null || a.ClosedOn >= inicio))
            .OrderBy(a => a.Id).ToListAsync(ct);

    private async Task<Dictionary<int, Dictionary<MemberAccountConcept, decimal>>> SaldosAsync(IReadOnlyCollection<int> ids, DateOnly corte, CancellationToken ct) =>
        (await db.MemberAccountTransactions.AsNoTracking().Where(t => ids.Contains(t.MemberAccountId) && !t.IsDeleted && t.ValueDate <= corte)
            .GroupBy(t => new { t.MemberAccountId, t.Concept }).Select(g => new { g.Key.MemberAccountId, g.Key.Concept, Total = g.Sum(t => t.Amount) })
            .ToListAsync(ct))
        .GroupBy(s => s.MemberAccountId).ToDictionary(g => g.Key, g => g.ToDictionary(x => x.Concept, x => x.Total));

    private async Task<HashSet<int>> CotitularesVigentesAsync(IReadOnlyCollection<int> ids, DateOnly corte, CancellationToken ct) =>
        (await db.MemberAccountParties.AsNoTracking()
            .Where(p => ids.Contains(p.MemberAccountId) && !p.IsDeleted && p.Role == MemberAccountPartyRole.CoHolder && p.ValidFrom <= corte
                        && (p.ValidTo == null || p.ValidTo >= corte))
            .Select(p => p.MemberAccountId).Distinct().ToListAsync(ct)).ToHashSet();

    /// <summary>Las cuentas marcadas exentas de GMF algún día del rango.</summary>
    private async Task<HashSet<int>> ExentasDeGmfAsync(IReadOnlyCollection<int> ids, DateOnly desde, DateOnly hasta, CancellationToken ct) =>
        (await db.GmfExemptAccounts.AsNoTracking()
            .Where(g => ids.Contains(g.MemberAccountId) && !g.IsDeleted && g.ValidFrom <= hasta && (g.ValidTo == null || g.ValidTo >= desde))
            .Select(g => g.MemberAccountId).Distinct().ToListAsync(ct)).ToHashSet();

    private async Task<Dictionary<int, IdentificacionDePersona>> IdentificacionesAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var lista = ids.Distinct().ToList();
        return await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => lista.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new IdentificacionDePersona(p.IdType, p.TaxId), ct);
    }

    private async Task<Dictionary<int, string>> OficinasAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var lista = ids.Distinct().ToList();
        return await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => lista.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => string.IsNullOrWhiteSpace(b.LegacyCode) ? b.Id.ToString(CultureInfo.InvariantCulture) : b.LegacyCode!.Trim(), ct);
    }

    private async Task<Dictionary<int, PersonaDeLaExogena>> PersonasDeLaExogenaAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var lista = ids.Distinct().ToList();
        var filas = await (from p in db.People.AsNoTracking().IgnoreQueryFilters()
                           where lista.Contains(p.Id)
                           join c in db.Cities.AsNoTracking().IgnoreQueryFilters() on p.CityId equals c.Id into ciudades
                           from c in ciudades.DefaultIfEmpty()
                           select new { Persona = p, Dane = c == null ? null : c.DaneCode }).ToListAsync(ct);
        return filas.ToDictionary(x => x.Persona.Id, x =>
        {
            var p = x.Persona;
            var juridica = p.PersonType == LectorDeCuentas.PersonaJuridica || TraduccionesDeCaptaciones.DocumentoDian(p.IdType) == 31;
            return new PersonaDeLaExogena
            {
                Identificacion = new IdentificacionDePersona(p.IdType, p.TaxId),
                EsJuridica = juridica,
                PrimerApellido = p.LastName,
                SegundoApellido = p.SecondLastName,
                PrimerNombre = p.FirstName,
                OtrosNombres = p.OtherNames,
                RazonSocial = juridica ? (string.IsNullOrWhiteSpace(p.BusinessName) ? $"{p.FirstName} {p.LastName}".Trim() : p.BusinessName) : null,
                Direccion = p.Address,
                Divipola = string.IsNullOrWhiteSpace(p.DaneCityCode) ? x.Dane : p.DaneCityCode,
            };
        });
    }

    private static Result<ProyeccionRegulatoriaDeCaptaciones> Falla(Error e) => Result.Failure<ProyeccionRegulatoriaDeCaptaciones>(e);
}
