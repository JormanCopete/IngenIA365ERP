using System.Globalization;
using IngenIA365ERP.Application.Common.Calendar;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Common.Layouts;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Regulatory;

/// <summary>
/// Un crédito del formato de cartera del SICSES tal como lo entrega la proyección (feature 013, K5, US16, T1002): todo lo que la fila necesita, ya
/// leído del cierre, del crédito, de su deudor (el maestro al generar), de sus condiciones y su plan vigentes al corte y de Contabilidad (el código
/// contable del capital). <see cref="FilasDelSicses"/> lo convierte en la fila del layout; así el mapeo es puro y los archivos dorados no necesitan base
/// de datos. (nuevo)
/// </summary>
public sealed record CreditoDelSicses
{
    public int LoanId { get; init; }
    public Guid LoanPublicId { get; init; }
    public required string Numero { get; init; }
    public int PersonId { get; init; }

    /// <summary>El <c>IdType</c> de la persona (<c>C</c>, <c>CE</c>, <c>N</c>…); la fila lo traduce.</summary>
    public required string TipoDeDocumento { get; init; }

    public required string NumeroDeDocumento { get; init; }
    public bool EsEmpleado { get; init; }

    /// <summary>El código de la cuenta del capital que resuelve la matriz contable para la combinación del crédito; nulo si no resuelve.</summary>
    public string? CodigoContable { get; init; }

    public required string Modalidad { get; init; }
    public required string Oficina { get; init; }
    public required string Linea { get; init; }
    public DateOnly FechaDeDesembolso { get; init; }
    public DateOnly? FechaDeVencimiento { get; init; }
    public decimal MontoDesembolsado { get; init; }

    /// <summary>Tasa efectiva anual como fracción; la fila la lleva con los seis decimales del campo.</summary>
    public decimal? TasaEfectivaAnual { get; init; }

    public RepaymentPeriodicity Periodicidad { get; init; }
    public int CuotasPactadas { get; init; }
    public int CuotasPagadas { get; init; }
    public decimal? ValorDeLaCuota { get; init; }
    public DateOnly? FechaDelUltimoPago { get; init; }
    public int DiasDeMora { get; init; }
    public RatingCategory Categoria { get; init; }
    public RatingSubcategory Subcategoria { get; init; }
    public decimal Capital { get; init; }
    public decimal InteresCorriente { get; init; }
    public decimal InteresDeMora { get; init; }
    public decimal InteresEnSuspenso { get; init; }
    public decimal OtrosConceptos { get; init; }
    public decimal DeterioroCapital { get; init; }
    public decimal DeterioroIntereses { get; init; }
    public decimal DeterioroOtros { get; init; }
    public CollateralKind? Garantia { get; init; }
    public decimal ValorAdmisibleDeGarantia { get; init; }
    public decimal AportesDeducidos { get; init; }
    public bool Reestructurado { get; init; }
    public int VecesReestructurado { get; init; }
}

/// <summary>
/// Las filas del formato de cartera del SICSES (K5, US16, T1002): una por crédito, con las rutas (<c>path</c>) que usa el layout
/// <c>SICSES-CARTERA-*</c> (<c>Deudor.*</c>, <c>Credito.*</c>, <c>Riesgo.*</c>, <c>Saldos.*</c>, <c>Deterioro.*</c>, <c>Garantias.*</c>,
/// <c>Aportes.*</c>). Pura y determinista. La marca «modelo obligatorio no disponible» (D28) <b>no</b> es una ruta: nunca entra al archivo. (nuevo)
/// </summary>
public static class FilasDelSicses
{
    /// <summary>El registro de detalle del layout.</summary>
    public const string Detalle = "Detail";

    /// <summary>Los decimales de la tasa en el layout (<c>Rate</c>, <c>decimals</c> 6).</summary>
    public const int DecimalesDeLaTasa = 6;

    public static IReadOnlyList<FilaDeLayout> Construir(IEnumerable<CreditoDelSicses> creditos) =>
        creditos.OrderBy(c => c.Numero, StringComparer.Ordinal).Select(Fila).ToList();

    public static FilaDeLayout Fila(CreditoDelSicses c) => new(Detalle, c.Numero, new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["Deudor.TipoDeDocumento"] = TraduccionesRegulatorias.TipoDeDocumento(c.TipoDeDocumento),
        ["Deudor.NumeroDeDocumento"] = c.NumeroDeDocumento.Trim(),
        ["Deudor.EsEmpleado"] = TraduccionesRegulatorias.SiNo(c.EsEmpleado),
        ["Credito.CodigoContable"] = c.CodigoContable,
        ["Credito.Modalidad"] = TraduccionesRegulatorias.ModalidadDelSicses(c.Modalidad),
        ["Credito.Numero"] = c.Numero,
        ["Credito.Oficina"] = c.Oficina,
        ["Credito.Linea"] = c.Linea,
        ["Credito.FechaDeDesembolso"] = c.FechaDeDesembolso,
        ["Credito.FechaDeVencimiento"] = c.FechaDeVencimiento,
        ["Credito.MontoDesembolsado"] = c.MontoDesembolsado,
        ["Credito.TasaEfectivaAnual"] = c.TasaEfectivaAnual is { } t ? decimal.Round(t, DecimalesDeLaTasa) : null,
        ["Credito.PeriodicidadEnDias"] = (int)c.Periodicidad,
        ["Credito.CuotasPactadas"] = c.CuotasPactadas,
        ["Credito.CuotasPagadas"] = c.CuotasPagadas,
        ["Credito.ValorDeLaCuota"] = c.ValorDeLaCuota,
        ["Credito.FechaDelUltimoPago"] = c.FechaDelUltimoPago,
        ["Credito.Reestructurado"] = TraduccionesRegulatorias.SiNo(c.Reestructurado),
        ["Credito.VecesReestructurado"] = c.VecesReestructurado,
        ["Riesgo.DiasDeMora"] = c.DiasDeMora,
        ["Riesgo.Categoria"] = c.Categoria.ToString(),
        ["Riesgo.Subcategoria"] = TraduccionesRegulatorias.Subcategoria(c.Subcategoria),
        ["Saldos.Capital"] = c.Capital,
        ["Saldos.InteresCorriente"] = c.InteresCorriente,
        ["Saldos.InteresDeMora"] = c.InteresDeMora,
        ["Saldos.InteresEnSuspenso"] = c.InteresEnSuspenso,
        ["Saldos.OtrosConceptos"] = c.OtrosConceptos,
        ["Deterioro.Capital"] = c.DeterioroCapital,
        ["Deterioro.Intereses"] = c.DeterioroIntereses,
        ["Deterioro.OtrosConceptos"] = c.DeterioroOtros,
        ["Deterioro.Individual"] = c.DeterioroCapital + c.DeterioroIntereses + c.DeterioroOtros,
        ["Garantias.Tipo"] = TraduccionesRegulatorias.GarantiaDelSicses(c.Garantia),
        ["Garantias.ValorAdmisible"] = c.ValorAdmisibleDeGarantia,
        ["Aportes.Deducidos"] = c.AportesDeducidos,
    });
}

/// <summary>
/// El plazo del formato de cartera del SICSES de un período según el perfil regulatorio vigente (K5, US16, T1002, T1007): el nivel de supervisión, si
/// el mes se reporta y, si el documento <c>TiposDeOrganizacion</c> o la propia cooperativa lo dicen, el último día y desde cuándo se avisa. (nuevo)
/// <para>Decisión del dueño del 2026-10-18: es <b>informativo</b>. La entidad reporta cuando su calendario lo diga; el plazo sólo alimenta el aviso
/// <c>Cartera.PlazoRegulatorioPorVencer</c> y la advertencia de mes fuera de la periodicidad. Ni validar, ni generar, ni marcar cargado, ni el cierre
/// miran este plazo.</para>
/// </summary>
public sealed record PlazoDelSicses(int NivelDeSupervision, bool SeReporta, DateOnly? Limite, DateOnly? AvisoDesde);

/// <summary>Lo que entrega la proyección del SICSES: el cierre del que sale, sus créditos, las filas, las advertencias, el plazo y los totales. (nuevo)</summary>
public sealed record ProyeccionDelSicses(
    MonthlyClose Cierre,
    IReadOnlyList<CreditoDelSicses> Creditos,
    IReadOnlyList<FilaDeLayout> Filas,
    IReadOnlyList<AdvertenciaRegulatoria> Advertencias,
    PlazoDelSicses? Plazo,
    IReadOnlyDictionary<string, object?> Totales);

/// <summary>
/// La proyección del formato de cartera del SICSES (feature 013, K5, US16, T1002; adaptadores-y-archivos.md §7.4; FR-136, D28): sale del <b>cierre
/// mensual vigente</b> del período (<c>Kind = Monthly</c>, el de mayor versión sin reapertura: uno reabierto no cuenta y su <c>Version + 1</c> sí) y de
/// sus líneas —una fila por crédito con los saldos por concepto, los días de mora, la calificación y la subcategoría, el deterioro por concepto, la
/// garantía admisible, los aportes, la reestructuración y la modalidad del cierre—, más el deudor del maestro, las condiciones y el plan vigentes al
/// corte, el último pago y el código contable del capital que resuelve la matriz por el puerto de Contabilidad (Cartera no conoce cuentas). Sin cierre:
/// <c>Lending.MonthlyClose.NotFound</c>. Devuelve las advertencias de D28 (<c>ModeloObligatorioNoDisponible</c>), de la normativa sin validar y del mes
/// fuera de la periodicidad del nivel de supervisión; ninguna entra al archivo. Sólo lee. (nuevo)
/// </summary>
public sealed class ProyeccionSicsesCartera(
    IApplicationDbContext db,
    IContabilidadParaCartera contabilidad,
    IPerfilRegulatorio perfiles,
    INormativaVigente normativa,
    ICalendarioHabil? calendario = null,
    ILectorDeParametros? parametros = null)
{
    public async Task<Result<ProyeccionDelSicses>> ProyectarAsync(int anio, int mes, CancellationToken ct)
    {
        var cierre = await CierreVigenteAsync(db, anio, mes, ct);
        if (cierre is null) return Result.Failure<ProyeccionDelSicses>(ErroresDeArchivosRegulatorios.SinCierre(anio, mes));
        var corte = cierre.CloseDate;

        var lineas = await CategoriaContableDeCartera.LineasDe(db, cierre).OrderBy(l => l.LoanId).ToListAsync(ct);
        var ids = lineas.Select(l => l.LoanId).Distinct().ToList();

        var creditos = await db.Loans.AsNoTracking().IgnoreQueryFilters().Where(l => ids.Contains(l.Id))
            .Select(l => new { l.Id, l.PublicId, l.Number, l.OpenedOn, l.ApprovedAmount }).ToDictionaryAsync(l => l.Id, ct);
        var personaIds = lineas.Select(l => l.PersonId).Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personaIds.Contains(p.Id))
            .Select(p => new { p.Id, p.IdType, p.TaxId }).ToDictionaryAsync(p => p.Id, ct);
        var lineaIds = lineas.Select(l => l.CreditLineId).Distinct().ToList();
        var lineasDeCredito = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(c => lineaIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Code, ct);
        var oficinaIds = lineas.Select(l => l.BranchId).Distinct().ToList();
        var oficinas = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => oficinaIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => string.IsNullOrWhiteSpace(b.LegacyCode) ? b.Id.ToString(CultureInfo.InvariantCulture) : b.LegacyCode!.Trim(), ct);

        var alCorte = await DatosDelCreditoAlCorte.LeerAsync(db, ids, corte, ct);
        var garantias = (await db.Collaterals.AsNoTracking().Where(c => c.LoanId != null && ids.Contains(c.LoanId.Value) && !c.IsDeleted
                    && c.ConstitutedOn <= corte && (c.ReleasedOn == null || c.ReleasedOn > corte))
                .Select(c => new { LoanId = c.LoanId!.Value, c.Kind }).ToListAsync(ct))
            .ToLookup(c => c.LoanId, c => c.Kind);
        var codigos = await CodigosContablesAsync(ids, corte, ct);

        var resultado = new List<CreditoDelSicses>(lineas.Count);
        foreach (var l in lineas)
        {
            var credito = creditos[l.LoanId];
            var persona = personas.GetValueOrDefault(l.PersonId);
            var d = alCorte[l.LoanId];
            resultado.Add(new CreditoDelSicses
            {
                LoanId = l.LoanId,
                LoanPublicId = credito.PublicId,
                Numero = credito.Number,
                PersonId = l.PersonId,
                TipoDeDocumento = persona?.IdType ?? string.Empty,
                NumeroDeDocumento = persona?.TaxId ?? string.Empty,
                EsEmpleado = l.IsEmployee,
                CodigoContable = codigos.GetValueOrDefault(l.LoanId),
                Modalidad = l.ModalityCode,
                Oficina = oficinas.GetValueOrDefault(l.BranchId) ?? string.Empty,
                Linea = lineasDeCredito.GetValueOrDefault(l.CreditLineId) ?? string.Empty,
                FechaDeDesembolso = credito.OpenedOn,
                FechaDeVencimiento = d.FechaDeVencimiento,
                MontoDesembolsado = credito.ApprovedAmount,
                TasaEfectivaAnual = d.TasaEfectivaAnual,
                Periodicidad = d.Periodicidad,
                CuotasPactadas = d.CuotasPactadas,
                CuotasPagadas = d.CuotasPagadas,
                ValorDeLaCuota = d.ValorDeLaCuota,
                FechaDelUltimoPago = d.FechaDelUltimoPago,
                DiasDeMora = l.DaysPastDueAtClose,
                Categoria = l.FinalRating,
                Subcategoria = l.FinalSubcategory,
                Capital = l.PrincipalBalance,
                InteresCorriente = l.CurrentInterestBalance,
                InteresDeMora = l.DefaultInterestBalance,
                InteresEnSuspenso = l.SuspendedInterestBalance,
                OtrosConceptos = l.OtherBalance,
                DeterioroCapital = l.PrincipalImpairment,
                DeterioroIntereses = l.InterestImpairment,
                DeterioroOtros = l.OtherImpairment,
                Garantia = TraduccionesRegulatorias.GarantiaMasFuerte(garantias[l.LoanId]),
                ValorAdmisibleDeGarantia = l.CollateralAdmissibleValue,
                AportesDeducidos = l.ContributionsDeducted,
                Reestructurado = l.IsRestructured,
                VecesReestructurado = l.RestructuringCount,
            });
        }

        var advertencias = new List<AdvertenciaRegulatoria>();
        if (cierre.ObligatoryModelUnavailable) advertencias.Add(AdvertenciasRegulatorias.SinModeloObligatorio());
        if (cierre.UnvalidatedParameters) advertencias.Add(AdvertenciasRegulatorias.NormativaPendiente(cierre.UnvalidatedParametersJson));
        var plazo = await PlazoAsync(perfiles, normativa, anio, mes, ct, calendario, parametros ?? new LectorDeParametros(db));
        if (plazo is { SeReporta: false } p)
            advertencias.Add(AdvertenciasRegulatorias.MesFueraDePeriodicidad(anio, mes, p.NivelDeSupervision));

        var totales = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["Totales.Registros"] = resultado.Count,
            ["Totales.SaldoDeCapital"] = resultado.Sum(c => c.Capital),
            ["Totales.DeterioroIndividual"] = resultado.Sum(c => c.DeterioroCapital + c.DeterioroIntereses + c.DeterioroOtros),
            ["Totales.DeterioroGeneral"] = cierre.TotalGeneralImpairment,
            ["Cierre.Version"] = cierre.Version,
            ["Cierre.Fecha"] = cierre.CloseDate,
        };
        return Result.Success(new ProyeccionDelSicses(cierre, resultado, FilasDelSicses.Construir(resultado), advertencias, plazo, totales));
    }

    /// <summary>
    /// El cierre mensual vigente de <paramref name="anio"/>-<paramref name="mes"/>: <c>Kind = Monthly</c>, sin reapertura, el de mayor versión. Nulo si
    /// no hay (el mes no se ha cerrado o su cierre se reabrió y no se ha vuelto a cerrar).
    /// </summary>
    public static Task<MonthlyClose?> CierreVigenteAsync(IApplicationDbContext db, int anio, int mes, CancellationToken ct) =>
        db.MonthlyCloses.AsNoTracking()
            .Where(c => !c.IsDeleted && c.Kind == MonthlyCloseKind.Monthly && c.PeriodYear == anio && c.PeriodMonth == mes
                        && !db.MonthlyCloseReopenings.Any(r => r.MonthlyCloseId == c.Id && !r.IsDeleted))
            .OrderByDescending(c => c.Version).FirstOrDefaultAsync(ct);

    /// <summary>
    /// El plazo del SICSES para el período según el perfil regulatorio vigente al fin del mes y el documento <c>TiposDeOrganizacion</c>
    /// (<c>sicsesReport</c> del tipo, por nivel de supervisión). Nulo si no hay perfil; sin el dato del documento ni de la cooperativa, se reporta sin
    /// plazo conocido.
    /// <para>Decisión del dueño del 2026-10-18 (informativo): la cooperativa puede fijar su propia periodicidad con <c>Sicses.MesesDeCorte</c> y
    /// <c>Sicses.DiasDePlazo</c> (los fondos de empleados y mutuales de nivel 3, o quien reporte los formatos básicos trimestrales), que pisan los del
    /// documento; con días propios no se aplican los plazos especiales. Con <paramref name="calendario"/>, el «último día hábil» de un plazo especial
    /// descuenta los festivos (sábado y domingo de descanso).</para>
    /// </summary>
    public static async Task<PlazoDelSicses?> PlazoAsync(IPerfilRegulatorio perfiles, INormativaVigente normativa, int anio, int mes, CancellationToken ct,
        ICalendarioHabil? calendario = null, ILectorDeParametros? parametros = null)
    {
        var fin = MonthlyClose.UltimoDiaDe(anio, mes);
        var perfil = await perfiles.VigenteAsync(fin, null, ct);
        if (perfil is null) return null;
        var tipos = await normativa.TiposDeOrganizacionAsync(fin, null, ct);
        var reporte = tipos.IsSuccess ? tipos.Value.Documento.Buscar(perfil.OrganizationTypeCode)?.SicsesDelNivel(perfil.SupervisionLevel) : null;
        var (mesesPropios, diasPropios) = await PeriodicidadPropiaAsync(parametros, fin, ct);
        var meses = mesesPropios ?? reporte?.CutoffMonths;
        if (meses is not null && !meses.Contains(mes)) return new PlazoDelSicses(perfil.SupervisionLevel, false, null, null);

        var efectivo = reporte is null
            ? diasPropios is { } d ? new ReporteSicsesPorNivel(perfil.SupervisionLevel, meses ?? [mes], d, 0) : null
            : reporte with
            {
                CutoffMonths = meses!, DueDays = diasPropios ?? reporte.DueDays,
                DueExceptions = diasPropios is null ? reporte.DueExceptions : [],
            };
        if (efectivo is null) return new PlazoDelSicses(perfil.SupervisionLevel, true, null, null);
        var habiles = calendario is null
            ? null
            : await calendario.ObtenerAsync(fin, fin.AddMonths(13), DiasDeDescanso.SabadoYDomingo, ct: ct);
        return new PlazoDelSicses(perfil.SupervisionLevel, true, efectivo.Limite(anio, mes, habiles), efectivo.AvisoDesde(anio, mes, habiles));
    }

    /// <summary>
    /// La periodicidad propia de la cooperativa (<c>Sicses.MesesDeCorte</c>, <c>Sicses.DiasDePlazo</c>) vigente al fin del mes; nulos si no la fijó.
    /// </summary>
    private static async Task<(IReadOnlyList<int>? Meses, int? Dias)> PeriodicidadPropiaAsync(ILectorDeParametros? parametros, DateOnly fecha,
        CancellationToken ct)
    {
        if (parametros is null) return (null, null);
        var meses = await parametros.LeerAsync(ParametrosDeCartera.Modulo, ParametrosDeCartera.SicsesMesesDeCorte, fecha, ct: ct);
        var dias = await parametros.LeerAsync(ParametrosDeCartera.Modulo, ParametrosDeCartera.SicsesDiasDePlazo, fecha, ct: ct);
        IReadOnlyList<int>? lista = meses.IsSuccess && meses.Value.Texto is { } texto && !string.IsNullOrWhiteSpace(texto)
            ? texto.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(m => int.Parse(m, CultureInfo.InvariantCulture)).Distinct().ToList()
            : null;
        return (lista, dias.IsSuccess && dias.Value.Valor is int n ? n : null);
    }

    /// <summary>
    /// El código de la cuenta del capital de cada crédito al corte: la combinación del rol de saldo del capital (<c>Capital</c>, o
    /// <c>CastigadaEnOrden</c> si está castigado) con las dimensiones que Cartera informa, resuelta por la matriz de Contabilidad
    /// (<see cref="IContabilidadParaCartera.SaldosDeCuentasMapeadasAsync"/>). Lo que no resuelve —o si Contabilidad no responde— queda sin código, y el
    /// validador lo señala como obligatorio vacío.
    /// </summary>
    private async Task<IReadOnlyDictionary<int, string>> CodigosContablesAsync(IReadOnlyCollection<int> ids, DateOnly corte, CancellationToken ct)
    {
        var resultado = new Dictionary<int, string>();
        if (ids.Count == 0) return resultado;
        var dimensiones = await new SaldosContablesDeCartera(db, contabilidad).DimensionesAsync(ids, corte, null, ct);
        var claves = dimensiones.ToDictionary(d => d.Key, d => SaldosContablesDeCartera.Clave(d.Value,
            SaldosContablesDeCartera.RolDelConcepto(LoanConcept.Principal, false, d.Value.Castigado), LoanConcept.Principal));
        var distintas = claves.Values.Distinct().ToList();
        var respuesta = await contabilidad.SaldosDeCuentasMapeadasAsync(corte, distintas.Select(c => c.ComoCombinacion()).ToList(), ct);
        if (respuesta is null || respuesta.IsFailure) return resultado;
        var porIndice = new Dictionary<int, string>();
        foreach (var conjunto in respuesta.Value.Sets)
        {
            var cuenta = conjunto.Accounts.OrderBy(a => a.AccountCode, StringComparer.Ordinal).FirstOrDefault();
            if (cuenta is null) continue;
            foreach (var i in conjunto.Combinations) porIndice.TryAdd(i, cuenta.AccountCode);
        }
        foreach (var (loanId, clave) in claves)
            if (porIndice.TryGetValue(distintas.IndexOf(clave), out var codigo)) resultado[loanId] = codigo;
        return resultado;
    }
}
