using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Lending.Adapters;
using IngenIA365ERP.Application.Lending.Collections.PriorNotices;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.DebtorEvents;
using IngenIA365ERP.Application.Lending.Payroll;
using IngenIA365ERP.Domain.Common.Compliance;
using IngenIA365ERP.Domain.Common.Layouts;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Documents;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Regulatory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Lending.Regulatory;

/// <summary>
/// Una obligación y un titular (deudor o codeudor) del archivo de una central, tal como lo decide la proyección (feature 013, K5, US16, T1003;
/// data-model §28.2): sus datos, su estado al corte y si va incluido o excluido con su motivo. <see cref="FilasDeCentral"/> convierte los incluidos en
/// filas del layout; así el mapeo es puro y los archivos dorados no necesitan base de datos. (nuevo)
/// </summary>
public sealed record TitularDeCentral
{
    public int LoanId { get; init; }
    public Guid LoanPublicId { get; init; }
    public required string NumeroDeObligacion { get; init; }
    public int PersonId { get; init; }
    public Guid PersonPublicId { get; init; }
    public bool EsDeudor { get; init; }

    /// <summary>El <c>IdType</c> de la persona; la fila lo traduce al código de la central.</summary>
    public required string TipoDeDocumento { get; init; }

    public required string NumeroDeDocumento { get; init; }
    public required string Nombre { get; init; }
    public string? CodigoDeCiudad { get; init; }
    public string? Direccion { get; init; }
    public string? Telefono { get; init; }
    public string? Correo { get; init; }
    public DateOnly FechaDeApertura { get; init; }
    public DateOnly? FechaDeVencimiento { get; init; }
    public EstadoEnCentral Estado { get; init; }
    public bool Reestructurado { get; init; }
    public DateOnly? FechaDelEstadoDeOrigen { get; init; }
    public decimal ValorInicial { get; init; }
    public decimal SaldoTotal { get; init; }
    public decimal SaldoEnMora { get; init; }
    public decimal? ValorDeLaCuota { get; init; }
    public RepaymentPeriodicity Periodicidad { get; init; }
    public int CuotasPagadas { get; init; }
    public int CuotasPactadas { get; init; }
    public int CuotasEnMora { get; init; }
    public int DiasDeMora { get; init; }

    /// <summary>
    /// Decisión del dueño del 2026-10-16: el número del rango de mora del tipo de cartera (<c>Centrales.RangosDeMora</c>) con el que se reporta la
    /// obligación en mora; nulo si no se reporta en mora. La altura de mora reportada (el código de la novedad) sale de él.
    /// </summary>
    public int? RangoDeMora { get; init; }

    /// <summary>La etiqueta del rango (<c>1-30</c>, <c>31-60</c>, <c>181+</c>); nula si no se reporta en mora.</summary>
    public string? AlturaDeMora { get; init; }

    public DateOnly? FechaLimiteDePago { get; init; }
    public DateOnly? FechaDelUltimoPago { get; init; }

    /// <summary>Reclamo sobre el reporte en trámite: va incluido con la marca (FR-137).</summary>
    public bool ReclamoEnTramite { get; init; }

    /// <summary>Retiro de un reporte negativo hecho sin aviso (migrado o de un archivo anterior, FR-123).</summary>
    public bool RetiroSinAviso { get; init; }

    /// <summary>Retiro de lo ya reportado de un crédito anulado desde su origen (FR-054): sin dato negativo.</summary>
    public bool AnuladoDesdeElOrigen { get; init; }

    /// <summary>El dato es negativo (mora o castigo): exige aviso previo <c>Complete</c> de su episodio (adaptadores-y-archivos.md §5.5).</summary>
    public bool EsNegativo { get; init; }

    public DateOnly? InicioDelEpisodio { get; init; }

    /// <summary>El aviso válido del episodio que permite reportar el dato negativo.</summary>
    public int? AvisoPrevioId { get; init; }

    public bool Incluido { get; init; } = true;

    /// <summary>Uno de <see cref="MotivosDeExclusionDeCentral"/> si va excluido.</summary>
    public string? MotivoDeExclusion { get; init; }
}

/// <summary>Lo que se informa en el encabezado y los totales del archivo de una central.</summary>
public sealed record DatosDelArchivoDeCentral(string Central, string? CodigoDeSuscriptor, DateOnly FechaDeCorte);

/// <summary>
/// Las filas del archivo de una central (K5, US16, T1003): cabecera y totales si el layout los tiene, y una fila de detalle por titular incluido, con las
/// rutas del layout (<c>Archivo.*</c>, <c>Titular.*</c>, <c>Obligacion.*</c>, <c>Saldos.*</c>, <c>Riesgo.*</c>, <c>Reporte.*</c>, <c>Totales.*</c>) y
/// los códigos de esa central (<see cref="TraduccionDeCentral"/>). Pura y determinista; los excluidos nunca entran. (nuevo)
/// </summary>
public static class FilasDeCentral
{
    public const string Cabecera = "Header";
    public const string Detalle = "Detail";
    public const string Totales = "Totals";

    public static IReadOnlyList<FilaDeLayout> Construir(LayoutDeArchivo layout, DatosDelArchivoDeCentral archivo, IEnumerable<TitularDeCentral> titulares)
    {
        var t = TraduccionesRegulatorias.DeLaCentral(archivo.Central);
        var incluidos = titulares.Where(x => x.Incluido)
            .OrderBy(x => x.NumeroDeObligacion, StringComparer.Ordinal).ThenBy(x => x.EsDeudor ? 0 : 1).ThenBy(x => x.NumeroDeDocumento, StringComparer.Ordinal)
            .ToList();
        var filas = new List<FilaDeLayout>(incluidos.Count + 2);
        if (layout.Registro(Cabecera) is not null)
            filas.Add(new FilaDeLayout(Cabecera, null, Archivo(archivo, t)));
        filas.AddRange(incluidos.Select(x => Fila(archivo, t, x)));
        if (layout.Registro(Totales) is not null)
        {
            var totales = Archivo(archivo, t);
            totales["Totales.Registros"] = incluidos.Count;
            totales["Totales.SaldoTotal"] = incluidos.Sum(x => x.SaldoTotal);
            filas.Add(new FilaDeLayout(Totales, null, totales));
        }
        return filas;
    }

    private static Dictionary<string, object?> Archivo(DatosDelArchivoDeCentral a, TraduccionDeCentral t) => new(StringComparer.Ordinal)
    {
        ["Archivo.CodigoDeSuscriptor"] = a.CodigoDeSuscriptor,
        ["Archivo.TipoDeCuenta"] = t.TipoDeCuenta,
        ["Archivo.FechaDeCorte"] = a.FechaDeCorte,
    };

    public static FilaDeLayout Fila(DatosDelArchivoDeCentral a, TraduccionDeCentral t, TitularDeCentral x)
    {
        var valores = Archivo(a, t);
        valores["Titular.TipoDeIdentificacion"] = t.TipoDeIdentificacion(TraduccionesRegulatorias.TipoDeDocumento(x.TipoDeDocumento));
        valores["Titular.NumeroDeDocumento"] = x.NumeroDeDocumento.Trim();
        valores["Titular.Nombre"] = x.Nombre;
        valores["Titular.Calidad"] = x.EsDeudor ? t.Deudor : t.Codeudor;
        valores["Titular.CodigoDeCiudad"] = x.CodigoDeCiudad;
        valores["Titular.Direccion"] = x.Direccion;
        valores["Titular.Telefono"] = x.Telefono;
        valores["Titular.Correo"] = x.Correo;
        valores["Obligacion.Numero"] = x.NumeroDeObligacion;
        valores["Obligacion.FechaDeApertura"] = x.FechaDeApertura;
        valores["Obligacion.FechaDeVencimiento"] = x.FechaDeVencimiento;
        valores["Obligacion.Novedad"] = t.Novedad(x.Estado, x.RangoDeMora);
        valores["Obligacion.EstadoDeOrigen"] = x.Reestructurado ? t.EstadoDeOrigenReestructurado : t.EstadoDeOrigenNormal;
        valores["Obligacion.FechaDelEstadoDeOrigen"] = x.FechaDelEstadoDeOrigen;
        valores["Obligacion.ValorInicial"] = x.ValorInicial;
        valores["Obligacion.ValorDeLaCuota"] = x.ValorDeLaCuota;
        valores["Obligacion.Periodicidad"] = t.Periodicidad(x.Periodicidad);
        valores["Obligacion.CuotasPagadas"] = x.CuotasPagadas;
        valores["Obligacion.CuotasPactadas"] = x.CuotasPactadas;
        valores["Obligacion.CuotasEnMora"] = x.CuotasEnMora;
        valores["Obligacion.FechaLimiteDePago"] = x.FechaLimiteDePago;
        valores["Obligacion.FechaDelUltimoPago"] = x.FechaDelUltimoPago;
        valores["Saldos.Total"] = x.SaldoTotal;
        valores["Saldos.EnMora"] = x.SaldoEnMora;
        valores["Riesgo.DiasDeMora"] = x.DiasDeMora;
        valores["Riesgo.RangoDeMora"] = x.RangoDeMora;
        valores["Riesgo.AlturaDeMora"] = x.AlturaDeMora;
        valores["Reporte.MarcaDeReclamo"] = x.ReclamoEnTramite ? "R" : null;
        valores["Reporte.MarcaDeRetiro"] = x.RetiroSinAviso || x.AnuladoDesdeElOrigen ? "X" : null;
        return new FilaDeLayout(Detalle, x.NumeroDeObligacion, valores);
    }
}

/// <summary>Lo que entrega la proyección de una central: el corte, el último archivo cargado del que parte, los titulares, el suscriptor y las alertas. (nuevo)</summary>
public sealed record ProyeccionDeCentral(
    string Central,
    DateOnly Corte,
    RegulatoryFile? UltimoCargado,
    IReadOnlyList<TitularDeCentral> Titulares,
    string? CodigoDeSuscriptor,
    IReadOnlyList<AlertaALevantar> Alertas)
{
    public IEnumerable<TitularDeCentral> Excluidos => Titulares.Where(t => !t.Incluido);

    public DatosDelArchivoDeCentral Archivo => new(Central, CodigoDeSuscriptor, Corte);
}

/// <summary>
/// La proyección del archivo de una central de riesgo por período (feature 013, K5, US16, T1003; adaptadores-y-archivos.md §5.5 y §7.4; FR-006, FR-054,
/// FR-097, FR-123, FR-137; SC-012). Al corte (el último día del mes):
/// <list type="bullet">
/// <item><b>Novedades desde el último archivo <c>Uploaded</c></b> de esa central (por sus <c>LND_RegulatoryFileLines</c>): los créditos vivos en la
/// ventana —desembolsados, al día, en mora, castigados— y los cerrados en ella (cancelaciones); un crédito cerrado antes de la ventana vuelve sólo si
/// el archivo anterior lo traía con dato negativo y la permanencia (<c>PERMANENCIA_NEGATIVO_VECES_MORA</c> y <c>PERMANENCIA_NEGATIVO_MESES_MAXIMOS</c>,
/// leídos como límites legales vigentes) no ha vencido.</item>
/// <item>Titulares: el deudor y los codeudores vigentes, cada uno con su autorización <c>CreditBureauReport</c> con evidencia
/// (<see cref="IAutorizacionDeDatos"/>); sin ella, <c>NoReportAuthorization</c>.</item>
/// <item>Dato negativo: excluido si hay reclamación al seguro de vida en trámite (<see cref="ReclamacionesEnTramite"/>, <c>DeceasedClaimInProcess</c>),
/// insolvencia negociando (<c>Insolvency</c>), descuento de libranza pendiente de giro dentro del umbral de la pagaduría
/// (<c>PaymasterArrearsWithinThreshold</c>) o sin aviso previo <c>Complete</c> de su episodio desde su fecha más temprana
/// (<c>NoPriorNoticeEvidence</c>, y la alerta <c>Cartera.AvisoPrevioSinConstancia</c>).</item>
/// <item>Decisiones del dueño del 2026-10-16: la mora se reporta por los <b>rangos de su tipo de cartera</b> (<c>Centrales.RangosDeMora</c>, vigente al
/// corte, por la modalidad de la posición): por debajo del rango negativo la obligación va al día —sin días, saldo ni cuotas en mora y sin exigir
/// aviso—; desde él va en mora con su rango, y la altura reportada (el código de la novedad) sale del rango. El codeudor con dato negativo se reporta
/// sólo con <c>AvisoPrevio.IncluirCodeudores</c> y <b>su</b> aviso completo; sin el parámetro queda fuera con <c>NoPriorNoticeEvidence</c> y sin
/// alerta.</item>
/// <item>Reclamo sobre el reporte en trámite (<c>LND_CreditReportClaims</c>): incluido con su marca.</item>
/// <item>Retiros: lo reportado con dato negativo sin aviso (en el archivo anterior, o migrado sin aviso en el primer archivo de la central) sale con la
/// marca de retiro y sin dato negativo; un crédito <b>anulado desde su origen</b> que figuraba en el archivo anterior sale como retiro de él y de sus
/// codeudores, y no vuelve a reportarse; si nunca se reportó, no aparece.</item>
/// </list>
/// Sólo lee: las alertas las levanta quien genera. (nuevo)
/// </summary>
public sealed class ProyeccionCentralDeRiesgo(
    IApplicationDbContext db,
    IAutorizacionDeDatos autorizaciones,
    IValoresNacionales valores,
    ReglasDeParametrosDeCartera parametros,
    IOptions<CanalesExternosOptions> canales,
    IDateTimeService reloj)
{
    public async Task<Result<ProyeccionDeCentral>> ProyectarAsync(string central, int anio, int mes, CancellationToken ct)
    {
        central = central.Trim().ToUpperInvariant();
        var corte = MonthlyClose.UltimoDiaDe(anio, mes);
        var periodo = anio * 12 + mes;
        var anterior = await UltimoCargadoAntesDeAsync(db, central, periodo, ct);
        var desde = anterior is null ? new DateOnly(anio, mes, 1) : MonthlyClose.UltimoDiaDe(anterior.PeriodYear, anterior.PeriodMonth).AddDays(1);
        var lineasAnteriores = anterior is null
            ? []
            : await db.RegulatoryFileLines.AsNoTracking().Where(l => l.RegulatoryFileId == anterior.Id && !l.IsDeleted).ToListAsync(ct);
        var reportadas = lineasAnteriores.Where(l => l.Included).ToLookup(l => l.LoanId);

        // El universo: lo vivo en la ventana, lo cerrado en ella y lo que traía el archivo anterior.
        var reportadosAntes = reportadas.Select(g => g.Key).ToList();
        var creditos = await db.Loans.AsNoTracking()
            .Where(l => !l.IsDeleted && l.OpenedOn <= corte && l.Status != LoanStatus.PendingDisbursement
                        && ((l.Status != LoanStatus.Voided
                             && !(l.PaidOffOn != null && l.PaidOffOn < desde && l.Status == LoanStatus.PaidOff)
                             && !(l.NovatedOn != null && l.NovatedOn < desde && l.Status == LoanStatus.Novated))
                            || reportadosAntes.Contains(l.Id)))
            .ToListAsync(ct);
        var ids = creditos.Select(l => l.Id).ToList();
        var datos = await DatosDelCreditoAlCorte.LeerAsync(db, ids, corte, ct);
        var rangos = await RangosDeMoraAsync(parametros, corte, ct);
        var incluirCodeudores = await AvisosPrevios.IncluyeCodeudoresAsync(parametros, corte, ct);

        var codeudores = (await db.LoanParties.AsNoTracking().Where(p => ids.Contains(p.LoanId) && p.Role == CreditPartyRole.CoDebtor).ToListAsync(ct))
            .Where(p => p.VigenteA(corte)).ToLookup(p => p.LoanId, p => p.PersonId);
        var retirosSinAviso = await RetirosSinAvisoAsync(central, anterior, lineasAnteriores, ct);

        // Permanencia del dato negativo de lo cerrado antes de la ventana: sólo se lee si hace falta.
        LimitesDePermanencia? permanencia = null;
        var cerradosConNegativo = creditos.Where(l => CerradoEl(l) is { } c && c < desde && l.Status != LoanStatus.Voided
                                                     && reportadas[l.Id].Any(x => x.IsNegative || x.DaysPastDue > 0)).ToList();
        if (cerradosConNegativo.Count > 0)
        {
            var veces = await valores.LeerAsync(CodigosDeValorNacional.Limite, CatalogoDeLimitesLegales.PermanenciaNegativoVecesMora, corte, null, ct);
            if (veces.IsFailure) return Result.Failure<ProyeccionDeCentral>(veces.Error);
            var meses = await valores.LeerAsync(CodigosDeValorNacional.Limite, CatalogoDeLimitesLegales.PermanenciaNegativoMesesMaximos, corte, null, ct);
            if (meses.IsFailure) return Result.Failure<ProyeccionDeCentral>(meses.Error);
            permanencia = new LimitesDePermanencia(veces.Value.Valor ?? 0m, (int)(meses.Value.Valor ?? 0m));
        }

        // Cada (crédito, titular) con su estado al corte, antes de decidir.
        var retirado = TraduccionesRegulatorias.DeLaCentral(central).Estado(EstadoEnCentral.Retirado);
        var borradores = new List<TitularDeCentral>();
        foreach (var l in creditos.OrderBy(l => l.Number, StringComparer.Ordinal))
        {
            var d = datos[l.Id];
            if (l.Status == LoanStatus.Voided)
            {
                // Anulado desde su origen: sólo si figuraba en el archivo anterior (y no como su retiro), como retiro de cada titular reportado; una vez
                // retirado no vuelve a aparecer.
                foreach (var personaId in reportadas[l.Id].Where(x => x.ReportedStatusCode != retirado).Select(x => x.PersonId).Distinct())
                    borradores.Add(Base(l, d, personaId, personaId == l.DebtorPersonId) with
                    {
                        Estado = EstadoEnCentral.Retirado, AnuladoDesdeElOrigen = true, SaldoTotal = 0m, SaldoEnMora = 0m, DiasDeMora = 0, CuotasEnMora = 0,
                        FechaDelEstadoDeOrigen = l.VoidedOn,
                    });
                continue;
            }

            EstadoEnCentral estado;
            RangoDeMora? rango = null;
            var cerrado = CerradoEl(l);
            if (cerrado is { } c && c <= corte)
            {
                if (c < desde)
                {
                    var previa = reportadas[l.Id].MaxBy(x => x.DaysPastDue);
                    if (previa is null || permanencia is null || corte > permanencia.Hasta(c, previa.DaysPastDue)) continue;
                }
                estado = EstadoEnCentral.Cancelada;
            }
            else if (l.WrittenOffOn is { } castigo && castigo <= corte) estado = EstadoEnCentral.Castigada;
            else if ((d.Posicion?.DaysPastDue ?? 0) > 0)
            {
                // Decisión del dueño del 2026-10-16: la mora se reporta por el rango de su tipo de cartera; por debajo del rango negativo, al día.
                var regla = rangos.Para(d.Posicion?.ModalityCode);
                var dias = d.Posicion!.DaysPastDue;
                rango = regla.RangoDe(dias);
                estado = regla.EsNegativo(dias) ? EstadoEnCentral.EnMora : EstadoEnCentral.AlDia;
            }
            else estado = EstadoEnCentral.AlDia;

            foreach (var (personaId, esDeudor) in new[] { (l.DebtorPersonId, true) }.Concat(codeudores[l.Id].Distinct().Select(p => (p, false))))
            {
                var b = Base(l, d, personaId, esDeudor) with { Estado = estado };
                if (estado == EstadoEnCentral.Cancelada) b = b with { SaldoTotal = 0m, SaldoEnMora = 0m, DiasDeMora = 0, CuotasEnMora = 0, FechaDelEstadoDeOrigen = cerrado };
                if (estado == EstadoEnCentral.AlDia && b.DiasDeMora > 0) b = b with { SaldoEnMora = 0m, DiasDeMora = 0, CuotasEnMora = 0 };
                borradores.Add(b with
                {
                    EsNegativo = estado is EstadoEnCentral.EnMora or EstadoEnCentral.Castigada,
                    RangoDeMora = estado == EstadoEnCentral.EnMora ? rango?.Numero : null,
                    AlturaDeMora = estado == EstadoEnCentral.EnMora ? rango?.Etiqueta : null,
                    InicioDelEpisodio = estado is EstadoEnCentral.EnMora or EstadoEnCentral.Castigada && d.Posicion is { DaysPastDue: > 0 } p
                        ? p.PositionDate.AddDays(-p.DaysPastDue) : null,
                    RetiroSinAviso = retirosSinAviso.Contains((l.Id, personaId)),
                });
            }
        }

        // Los datos de las personas.
        var personaIds = borradores.Select(b => b.PersonId).Distinct().ToList();
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personaIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        borradores = borradores.Select(b => personas.TryGetValue(b.PersonId, out var p)
            ? b with
            {
                PersonPublicId = p.PublicId, TipoDeDocumento = p.IdType, NumeroDeDocumento = p.TaxId, Nombre = ReferenciasDeGobierno.Nombre(p),
                CodigoDeCiudad = Vacio(p.DaneCityCode), Direccion = Vacio(p.Address), Telefono = Vacio(p.Mobile) ?? Vacio(p.Phone1), Correo = Vacio(p.Email),
            }
            : b).ToList();

        // Lo que decide la inclusión.
        var negativos = borradores.Where(b => b.EsNegativo).ToList();
        var conReclamacionAlSeguro = await ReclamacionesEnTramite.CreditosAsync(db, negativos.Select(b => b.LoanId).Distinct().ToList(), ct);
        var insolventes = (await db.InsolvencyProceedings.AsNoTracking().Where(i => personaIds.Contains(i.PersonId)).ToListAsync(ct))
            .Where(i => i.Suspende(corte)).Select(i => i.PersonId).ToHashSet();
        var conGiroPendiente = await GiroPendienteDentroDelUmbralAsync(negativos.Select(b => b.LoanId).Distinct().ToList(), corte, ct);
        var reclamos = (await db.CreditReportClaims.AsNoTracking()
                .Where(r => !r.IsDeleted && r.Status == CreditReportClaimStatus.InProcess && personaIds.Contains(r.PersonId)
                            && (r.BureauCode == null || r.BureauCode == central))
                .Select(r => new { r.PersonId, r.LoanId }).ToListAsync(ct));
        var avisos = await AvisosAsync(negativos.Where(n => n.EsDeudor || incluirCodeudores).ToList(), corte, ct);

        var alertas = new List<AlertaALevantar>();
        var decididos = new List<TitularDeCentral>(borradores.Count);
        foreach (var b in borradores)
        {
            var reclamo = reclamos.Any(r => r.PersonId == b.PersonId && (r.LoanId == null || r.LoanId == b.LoanId));
            var t = b with { ReclamoEnTramite = reclamo };
            if (t.AnuladoDesdeElOrigen)
            {
                decididos.Add(t);
                continue;
            }

            string? motivo = null;
            if (!await AutorizaElReporteAsync(t.PersonPublicId, ct)) motivo = MotivosDeExclusionDeCentral.NoReportAuthorization;
            else if (t.EsNegativo)
            {
                if (conReclamacionAlSeguro.Contains(t.LoanId)) motivo = MotivosDeExclusionDeCentral.DeceasedClaimInProcess;
                else if (insolventes.Contains(t.PersonId)) motivo = MotivosDeExclusionDeCentral.Insolvency;
                else if (conGiroPendiente.Contains(t.LoanId)) motivo = MotivosDeExclusionDeCentral.PaymasterArrearsWithinThreshold;
                // Decisión del dueño del 2026-10-16: sin AvisoPrevio.IncluirCodeudores el codeudor no recibe aviso y no se reporta con dato negativo; no
                // hay gestión que hacer, así que tampoco alerta.
                else if (!t.EsDeudor && !incluirCodeudores) motivo = MotivosDeExclusionDeCentral.NoPriorNoticeEvidence;
                else if (avisos.GetValueOrDefault((t.LoanId, t.PersonId)) is { } aviso && aviso.Permite) t = t with { AvisoPrevioId = aviso.Id };
                else
                {
                    motivo = MotivosDeExclusionDeCentral.NoPriorNoticeEvidence;
                    alertas.Add(AlertaSinConstancia(central, t, avisos.GetValueOrDefault((t.LoanId, t.PersonId))));
                }
            }

            if (motivo is null) decididos.Add(t);
            else if (t.RetiroSinAviso)
                // Lo reportado sin aviso se retira aunque hoy no se pueda reportar: sale la marca de retiro, sin dato negativo.
                decididos.Add(t with
                {
                    Estado = t.EsNegativo ? EstadoEnCentral.Retirado : t.Estado, EsNegativo = false, SaldoEnMora = 0m, DiasDeMora = 0, CuotasEnMora = 0,
                });
            else decididos.Add(t with { Incluido = false, MotivoDeExclusion = motivo });
        }

        var suscriptor = await CodigoDeSuscriptorAsync(central, corte, ct);
        return Result.Success(new ProyeccionDeCentral(central, corte, anterior, decididos, suscriptor, alertas));
    }

    /// <summary>El último archivo <c>Uploaded</c> de la central de un período anterior a <paramref name="periodo"/> (año × 12 + mes), o nulo.</summary>
    public static async Task<RegulatoryFile?> UltimoCargadoAntesDeAsync(IApplicationDbContext db, string central, int periodo, CancellationToken ct)
    {
        var cargados = await db.RegulatoryFiles.AsNoTracking()
            .Where(f => !f.IsDeleted && f.Kind == RegulatoryFileKind.CreditBureau && f.BureauCode == central && f.Status == RegulatoryFileStatus.Uploaded
                        && f.PeriodYear * 12 + f.PeriodMonth < periodo)
            .ToListAsync(ct);
        return cargados.OrderByDescending(f => f.PeriodYear * 12 + f.PeriodMonth).ThenByDescending(f => f.Version).FirstOrDefault();
    }

    private static DateOnly? CerradoEl(Domain.Entities.Lending.Loans.Loan l) => l.Status switch
    {
        LoanStatus.PaidOff => l.PaidOffOn,
        LoanStatus.Novated => l.NovatedOn,
        _ => null,
    };

    private static TitularDeCentral Base(Domain.Entities.Lending.Loans.Loan l, CreditoAlCorte d, int personaId, bool esDeudor)
    {
        var p = d.Posicion;
        return new TitularDeCentral
        {
            LoanId = l.Id,
            LoanPublicId = l.PublicId,
            NumeroDeObligacion = l.Number,
            PersonId = personaId,
            EsDeudor = esDeudor,
            TipoDeDocumento = string.Empty,
            NumeroDeDocumento = string.Empty,
            Nombre = string.Empty,
            FechaDeApertura = l.OpenedOn,
            FechaDeVencimiento = d.FechaDeVencimiento,
            Reestructurado = d.VecesReestructurado > 0,
            FechaDelEstadoDeOrigen = d.ReestructuradoEl,
            ValorInicial = l.ApprovedAmount,
            SaldoTotal = p is null ? 0m : p.Principal + p.CurrentInterest + p.DefaultInterest + p.CurrentInterestInSuspense + p.DefaultInterestInSuspense
                                         + p.Insurance + p.CollectionFee + p.OtherCharge,
            SaldoEnMora = p is { DaysPastDue: > 0 } ? p.PrincipalPastDue + p.DefaultInterest + p.DefaultInterestInSuspense : 0m,
            ValorDeLaCuota = d.ValorDeLaCuota,
            Periodicidad = d.Periodicidad,
            CuotasPagadas = d.CuotasPagadas,
            CuotasPactadas = d.CuotasPactadas,
            CuotasEnMora = p?.InstallmentsPastDue ?? 0,
            DiasDeMora = p?.DaysPastDue ?? 0,
            FechaLimiteDePago = d.ProximoVencimiento,
            FechaDelUltimoPago = d.FechaDelUltimoPago,
        };
    }

    /// <summary>
    /// Los (crédito, titular) con un reporte negativo hecho sin aviso que hay que retirar: los del archivo anterior incluidos con dato negativo y sin
    /// aviso; en el primer archivo de la central, los reportes migrados (<c>bureau-reports</c>) con dato negativo y <c>conAvisoPrevio = no</c>.
    /// </summary>
    private async Task<HashSet<(int LoanId, int PersonId)>> RetirosSinAvisoAsync(string central, RegulatoryFile? anterior,
        IReadOnlyList<RegulatoryFileLine> lineasAnteriores, CancellationToken ct)
    {
        var resultado = lineasAnteriores.Where(l => l.Included && l.IsNegative && l.PriorNoticeId == null && !l.IsWithdrawalOfUnnoticedReport)
            .Select(l => (l.LoanId, l.PersonId)).ToHashSet();
        if (anterior is not null) return resultado;

        var migrados = await (from r in db.MigrationBureauReportRows.AsNoTracking()
                              join p in db.MigrationLoadParts.AsNoTracking() on r.MigrationLoadPartId equals p.Id
                              join l in db.Loans.AsNoTracking() on new { Numero = r.SourceLoanNumber, Carga = (int?)p.MigrationLoadId }
                                  equals new { Numero = l.Number, Carga = l.MigrationLoadId }
                              where r.BureauCode == central && r.IsNegative && r.WithPriorNotice == false
                              select new { l.Id, l.DebtorPersonId })
            .ToListAsync(ct);
        foreach (var m in migrados) resultado.Add((m.Id, m.DebtorPersonId));
        return resultado;
    }

    /// <summary>
    /// Los créditos con un descuento de libranza pendiente de giro cuya pagaduría sigue dentro de su umbral al corte (K2, FR-114): lo descontado y no
    /// girado no es mora del deudor, así que su dato negativo no se reporta.
    /// </summary>
    private async Task<HashSet<int>> GiroPendienteDentroDelUmbralAsync(IReadOnlyCollection<int> creditos, DateOnly corte, CancellationToken ct)
    {
        if (creditos.Count == 0) return [];
        var ids = creditos.ToList();
        var pendientes = await PendientesDeGiro.Consulta(db).AsNoTracking().Where(r => ids.Contains(r.LoanId) && r.DeductionDate != null && r.DeductionDate <= corte)
            .Select(r => new { r.LoanId, r.TransferDueDate, r.PaymasterArrearsThresholdDays }).ToListAsync(ct);
        return pendientes.Where(r => !PendientesDeGiro.SuperaElUmbral(r.TransferDueDate, r.PaymasterArrearsThresholdDays, corte))
            .Select(r => r.LoanId).ToHashSet();
    }

    private sealed record AvisoEvaluado(int Id, Guid PublicId, DateOnly InicioDelEpisodio, bool Permite, DateOnly? FechaMasTemprana);

    /// <summary>
    /// El aviso previo de cada (crédito, titular) con dato negativo: el de su episodio al corte (o, si la posición no lo dice —un castigado—, el último
    /// abierto hasta el corte), evaluado por <see cref="AvisosPrevios.EvaluarAsync"/>; permite reportar si está <c>Complete</c> y el corte ya pasó su
    /// fecha más temprana de reporte.
    /// </summary>
    private async Task<Dictionary<(int LoanId, int PersonId), AvisoEvaluado>> AvisosAsync(IReadOnlyList<TitularDeCentral> negativos, DateOnly corte,
        CancellationToken ct)
    {
        var resultado = new Dictionary<(int, int), AvisoEvaluado>();
        if (negativos.Count == 0) return resultado;
        var loanIds = negativos.Select(n => n.LoanId).Distinct().ToList();
        var todos = (await db.PriorNotices.AsNoTracking().Where(n => !n.IsDeleted && loanIds.Contains(n.LoanId) && n.EpisodeStartDate <= corte).ToListAsync(ct))
            .ToLookup(a => (a.LoanId, a.PersonId));
        var elegidos = new List<(TitularDeCentral Titular, PriorNotice Aviso)>();
        foreach (var n in negativos)
        {
            var delTitular = todos[(n.LoanId, n.PersonId)].ToList();
            var aviso = n.InicioDelEpisodio is { } inicio
                ? delTitular.FirstOrDefault(a => a.EpisodeStartDate == inicio)
                : delTitular.OrderByDescending(a => a.EpisodeStartDate).FirstOrDefault();
            if (aviso is not null) elegidos.Add((n, aviso));
        }
        if (elegidos.Count == 0) return resultado;
        var evaluados = await AvisosPrevios.EvaluarAsync(db, elegidos.Select(e => e.Aviso).DistinctBy(a => a.Id).ToList(), !canales.Value.AmbienteDePruebas,
            reloj.AhoraLocal.Offset, ct);
        foreach (var (titular, aviso) in elegidos)
        {
            var e = evaluados[aviso.Id].Evaluacion;
            resultado[(titular.LoanId, titular.PersonId)] =
                new AvisoEvaluado(aviso.Id, aviso.PublicId, aviso.EpisodeStartDate, e.PermiteReportar(corte), e.FechaMasTempranaDeReporte);
        }
        return resultado;
    }

    /// <summary>La autorización <c>CreditBureauReport</c> vigente <b>con evidencia</b> (FR-006: una migrada sin evidencia no autoriza el reporte).</summary>
    private async Task<bool> AutorizaElReporteAsync(Guid persona, CancellationToken ct)
    {
        var a = await autorizaciones.VigenteAsync(persona, DataAuthorizationPurpose.CreditBureauReport, null, ct);
        return a is { Autoriza: true, HasEvidence: true };
    }

    private AlertaALevantar AlertaSinConstancia(string central, TitularDeCentral t, AvisoEvaluado? aviso) =>
        new(TiposDeAlerta.AvisoPrevioSinConstancia, $"Dato negativo sin aviso previo con constancia: crédito {t.NumeroDeObligacion}",
            $"El crédito {t.NumeroDeObligacion} llegó a su punto de reporte en {central} con dato negativo"
            + (aviso is null
                ? " y sin aviso previo del episodio."
                : aviso.FechaMasTemprana is { } f ? $", pero su aviso previo sólo permite reportar desde el {f:yyyy-MM-dd}." : " y su aviso previo sin constancia completa.")
            + " Quedó fuera del archivo: envíe o registre la constancia del aviso.",
            EntityType: aviso is null ? "Loan" : nameof(PriorNotice), EntityPublicId: aviso?.PublicId ?? t.LoanPublicId,
            DedupKey: $"{TiposDeAlerta.AvisoPrevioSinConstancia}:{central}:{t.LoanId}:{t.PersonId}:{t.InicioDelEpisodio:yyyy-MM-dd}",
            ModuloDeOrigen: "Lending");

    /// <summary>
    /// Los rangos de mora por tipo de cartera vigentes al corte (<c>Centrales.RangosDeMora</c>, decisión del dueño del 2026-10-16); si el parámetro no se
    /// puede leer o no sirve, los del defecto —toda mora es dato negativo, como antes del parámetro—.
    /// </summary>
    public static async Task<RangosDeMoraParaCentrales> RangosDeMoraAsync(ReglasDeParametrosDeCartera parametros, DateOnly corte, CancellationToken ct)
    {
        var p = await parametros.LeerAsync(ParametrosDeCartera.CentralesRangosDeMora, corte, ct: ct);
        return (p.IsSuccess ? RangosDeMoraParaCentrales.Leer(p.Value.Texto) : null) ?? RangosDeMoraParaCentrales.PorDefecto;
    }

    /// <summary>El código de suscriptor de la central (<c>Centrales.CodigosDeSuscriptor</c>, vigente al corte), o nulo si no está.</summary>
    private async Task<string?> CodigoDeSuscriptorAsync(string central, DateOnly corte, CancellationToken ct)
    {
        var p = await parametros.LeerAsync(ParametrosDeCartera.CentralesCodigosDeSuscriptor, corte, ct: ct);
        return p.IsSuccess ? CodigosDeSuscriptor(p.Value.Texto).GetValueOrDefault(central) : null;
    }

    /// <summary>Lee <c>DATACREDITO=123;TRANSUNION=ABC</c> como diccionario por central (en mayúsculas).</summary>
    public static IReadOnlyDictionary<string, string> CodigosDeSuscriptor(string? texto)
    {
        var resultado = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var par in (texto ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var i = par.IndexOf('=');
            if (i > 0 && i < par.Length - 1) resultado[par[..i].Trim().ToUpperInvariant()] = par[(i + 1)..].Trim();
        }
        return resultado;
    }

    private static string? Vacio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
