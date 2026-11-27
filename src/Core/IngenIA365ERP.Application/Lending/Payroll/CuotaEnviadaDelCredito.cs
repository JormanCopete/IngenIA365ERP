using System.Globalization;
using IngenIA365ERP.Application.Common.Integration.Payroll;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Payroll;

/// <summary>La cuota de un crédito por la que se pregunta: el crédito y la cuota del período en curso (número y vencimiento; nulos, la de la fecha). (nuevo)</summary>
public sealed record CuotaPorConsultar(int LoanId, Guid LoanPublicId, int? Numero, DateOnly? Vencimiento);

/// <summary>
/// En qué va la cuota del período de un crédito frente a su canal (feature 028, T064; R6, FR-021): el estado; la cuota que nombra la línea (la primera del
/// período) o la novedad; y, si va en un archivo de libranza, la pagaduría, el convenio, el archivo, el período y los valores de la línea (lo corriente, lo
/// vencido y el total que se pidió descontar); si va en la nómina propia, el período, la corrida y el valor. (nuevo)
/// </summary>
public sealed record CuotaEnviada(
    int LoanId,
    SentInstallmentState Estado,
    int? Cuota,
    DateOnly? Vencimiento,
    string? Pagaduria,
    string? Convenio,
    int? ArchivoId,
    string? Archivo,
    string? Periodo,
    decimal? Corriente,
    decimal? Vencido,
    decimal? Total)
{
    /// <summary>La corrida de nómina que lo calculó o lo aplicó (sólo nómina propia).</summary>
    public Guid? Corrida { get; init; }

    /// <summary>Va en un archivo de libranza (enviada sin respuesta, descontada pendiente de giro o no descontada).</summary>
    public bool EnPagaduria => Estado is SentInstallmentState.SentWithoutResponse or SentInstallmentState.DeductedPendingTransfer or SentInstallmentState.NotDeducted;

    /// <summary>Va en la nómina propia (novedad generada, corrida calculada o aprobada).</summary>
    public bool EnNominaPropia => Estado is SentInstallmentState.OwnPayrollGenerated or SentInstallmentState.OwnPayrollCalculated
        or SentInstallmentState.OwnPayrollApproved;
}

/// <summary>
/// <b>La cuota enviada de cada crédito</b> (feature 028, T064, T064a; R6, FR-021; nota «cuota enviada a la pagaduría»), de sólo lectura y en lote: por cada
/// crédito y la cuota de su período en curso a la fecha,
/// <list type="number">
/// <item><b>libranza</b>: la línea del crédito en el archivo <c>Sent</c> del período que contiene el vencimiento de la cuota (el de la quincena, si el
/// convenio la parte); sin resultado, <see cref="SentInstallmentState.SentWithoutResponse"/>; con el último resultado pendiente de giro
/// (<see cref="PendientesDeGiro"/>), <see cref="SentInstallmentState.DeductedPendingTransfer"/>; con algo sin descontar,
/// <see cref="SentInstallmentState.NotDeducted"/>; descontada y girada, nada (ya está pagada). Si ese período no dice nada, lo descontado y todavía sin
/// girar de una línea anterior del crédito, si lo hay;</item>
/// <item><b>nómina propia</b>, si la libranza no dice nada: el descuento vivo que Nómina informa por su puerto (<see cref="IDescuentosDeNominaParaCartera"/>;
/// Cartera no lee tablas <c>PAY_</c>) —el del período que contiene el vencimiento o, si no, el primero vivo—: novedad generada, corrida calculada o
/// aprobada.</item>
/// </list>
/// Sólo devuelve los créditos con algo enviado. Una consulta por clase de dato para todos los créditos: la usan el listado del contrato de obligaciones (la
/// caja), la liquidación de la recogida y el detalle. No escribe ni audita. (nuevo)
/// </summary>
public sealed class CuotaEnviadaDelCredito(IApplicationDbContext db, IDescuentosDeNominaParaCartera nomina)
{
    /// <summary>Cuántos meses antes del de la fecha se buscan archivos enviados (lo anterior pendiente de giro se busca aparte).</summary>
    private const int MesesAtras = 2;

    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    /// <summary>
    /// La cuota del período en curso de un crédito a la fecha de su estado, para preguntar por ella: la primera que vence ese día o después (la que vence hoy
    /// todavía puede ir en el archivo del período). Sin plan, la fecha del estado.
    /// </summary>
    public static CuotaPorConsultar CuotaDelEstado(Domain.Entities.Lending.Loans.Loan credito, Domain.Lending.Replay.EstadoDelCredito estado)
    {
        ArgumentNullException.ThrowIfNull(credito);
        ArgumentNullException.ThrowIfNull(estado);
        var cuota = estado.Plan?.Cuotas.Where(c => c.Vencimiento >= estado.Fecha).OrderBy(c => c.Vencimiento).FirstOrDefault();
        return new CuotaPorConsultar(credito.Id, credito.PublicId, cuota?.Numero, cuota?.Vencimiento ?? estado.Fecha);
    }

    /// <summary>La de un crédito; nula si no hay nada enviado.</summary>
    public async Task<CuotaEnviada?> DeAsync(CuotaPorConsultar cuota, DateOnly fecha, CancellationToken ct) =>
        (await ConsultarAsync([cuota], fecha, ct)).GetValueOrDefault(cuota.LoanId);

    /// <summary>Las de <paramref name="cuotas"/>, por <c>LoanId</c>; los créditos sin nada enviado no aparecen.</summary>
    public async Task<IReadOnlyDictionary<int, CuotaEnviada>> ConsultarAsync(IReadOnlyCollection<CuotaPorConsultar> cuotas, DateOnly fecha, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cuotas);
        var resultado = new Dictionary<int, CuotaEnviada>();
        if (cuotas.Count == 0) return resultado;
        var porCredito = cuotas.GroupBy(c => c.LoanId).ToDictionary(g => g.Key, g => g.First());
        var ids = porCredito.Keys.ToList();

        foreach (var (credito, enviada) in await DeLibranzaAsync(porCredito, ids, fecha, ct)) resultado[credito] = enviada;

        var sinLibranza = porCredito.Values.Where(c => !resultado.ContainsKey(c.LoanId)).ToList();
        if (sinLibranza.Count > 0)
        {
            var vivos = await nomina.VivosAsync(sinLibranza.Select(c => c.LoanPublicId).Distinct().ToList(), fecha, ct);
            var porObligacion = vivos.GroupBy(v => v.ObligationPublicId).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var c in sinLibranza)
                if (porObligacion.TryGetValue(c.LoanPublicId, out var descuentos) && DeNomina(c, descuentos) is { } enviada)
                    resultado[c.LoanId] = enviada;
        }
        return resultado;
    }

    // --------------------------------------------------------------------------------------------------------------------------- libranza --

    private sealed record LineaEnviada(int Id, int LoanId, int? Cuota, DateOnly? Vencimiento, decimal Corriente, decimal Vencido, decimal Total, int ArchivoId,
        string Archivo, int Anio, int Mes, int SubPeriodo, int ConvenioId);

    private async Task<Dictionary<int, CuotaEnviada>> DeLibranzaAsync(IReadOnlyDictionary<int, CuotaPorConsultar> porCredito, List<int> ids, DateOnly fecha,
        CancellationToken ct)
    {
        var desde = fecha.Year * 12 + fecha.Month - MesesAtras;
        var recientes = await (
                from l in db.PayrollDeductionFileLines.AsNoTracking()
                join f in db.PayrollDeductionFiles.AsNoTracking() on l.PayrollDeductionFileId equals f.Id
                where ids.Contains(l.LoanId) && !l.IsDeleted && !f.IsDeleted && f.Status == PayrollFileStatus.Sent && f.PeriodYear * 12 + f.PeriodMonth >= desde
                select new LineaEnviada(l.Id, l.LoanId, l.InstallmentNumber, l.DueDate, l.CurrentAmount, l.PastDueAmount, l.TotalAmount, f.Id, f.Number,
                    f.PeriodYear, f.PeriodMonth, f.SubPeriodNumber, f.PayrollAgreementId))
            .ToListAsync(ct);
        var pendientes = await PendientesDeGiro.Consulta(db).AsNoTracking().Where(r => ids.Contains(r.LoanId))
            .Select(r => new { r.Id, r.LoanId, r.PayrollDeductionFileLineId }).ToListAsync(ct);

        // La línea del período de la cuota en curso de cada crédito.
        var delPeriodo = new Dictionary<int, LineaEnviada>();
        foreach (var (credito, cuota) in porCredito)
        {
            var vence = cuota.Vencimiento ?? fecha;
            var quincena = vence.Day > 15 ? 2 : 1;
            var linea = recientes.Where(l => l.LoanId == credito && l.Anio == vence.Year && l.Mes == vence.Month && (l.SubPeriodo == 1 || l.SubPeriodo == quincena))
                .OrderByDescending(l => l.SubPeriodo).ThenByDescending(l => l.ArchivoId).FirstOrDefault();
            if (linea is not null) delPeriodo[credito] = linea;
        }

        // Las líneas de lo pendiente de giro que no están entre las recientes.
        var conocidas = recientes.Select(l => l.Id).ToHashSet();
        var faltan = pendientes.Select(p => p.PayrollDeductionFileLineId).Where(id => !conocidas.Contains(id)).Distinct().ToList();
        var anteriores = faltan.Count == 0
            ? []
            : await (
                    from l in db.PayrollDeductionFileLines.AsNoTracking()
                    join f in db.PayrollDeductionFiles.AsNoTracking() on l.PayrollDeductionFileId equals f.Id
                    where faltan.Contains(l.Id)
                    select new LineaEnviada(l.Id, l.LoanId, l.InstallmentNumber, l.DueDate, l.CurrentAmount, l.PastDueAmount, l.TotalAmount, f.Id, f.Number,
                        f.PeriodYear, f.PeriodMonth, f.SubPeriodNumber, f.PayrollAgreementId))
                .ToListAsync(ct);
        var lineas = recientes.Concat(anteriores).GroupBy(l => l.Id).ToDictionary(g => g.Key, g => g.First());

        var idsDeLinea = delPeriodo.Values.Select(l => l.Id).ToList();
        var resultados = idsDeLinea.Count == 0
            ? []
            : await db.PayrollDeductionResults.AsNoTracking().Where(r => idsDeLinea.Contains(r.PayrollDeductionFileLineId) && !r.IsDeleted)
                .Select(r => new { r.Id, r.PayrollDeductionFileLineId, r.Outcome, r.NotDeductedAmount, r.SupersedesResultId }).ToListAsync(ct);
        var reemplazados = resultados.Where(r => r.SupersedesResultId is not null).Select(r => r.SupersedesResultId!.Value).ToHashSet();
        var ultimo = resultados.Where(r => !reemplazados.Contains(r.Id)).GroupBy(r => r.PayrollDeductionFileLineId)
            .ToDictionary(g => g.Key, g => g.MaxBy(r => r.Id)!);
        var resultadosPendientes = pendientes.Select(p => p.Id).ToHashSet();

        var convenios = lineas.Values.Select(l => l.ConvenioId).Distinct().ToList();
        var deConvenio = convenios.Count == 0
            ? []
            : await (from a in db.PayrollAgreements.AsNoTracking().IgnoreQueryFilters()
                     join p in db.People.AsNoTracking().IgnoreQueryFilters() on a.PaymasterPersonId equals p.Id
                     where convenios.Contains(a.Id)
                     select new { a.Id, a.Code, Persona = p }).ToListAsync(ct);
        var convenio = deConvenio.ToDictionary(a => a.Id, a => (a.Code, Pagaduria: ReferenciasDeGobierno.Nombre(a.Persona)));

        CuotaEnviada Enviada(LineaEnviada l, SentInstallmentState estado)
        {
            var (codigo, pagaduria) = convenio.TryGetValue(l.ConvenioId, out var c) ? c : (string.Empty, string.Empty);
            return new CuotaEnviada(l.LoanId, estado, l.Cuota, l.Vencimiento, pagaduria, codigo, l.ArchivoId, l.Archivo,
                TextosDeCuotaEnviada.PeriodoDeLibranza(l.Anio, l.Mes, l.SubPeriodo), l.Corriente, l.Vencido, l.Total);
        }

        var enviadas = new Dictionary<int, CuotaEnviada>();
        foreach (var credito in porCredito.Keys)
        {
            if (delPeriodo.TryGetValue(credito, out var linea))
            {
                var estado = !ultimo.TryGetValue(linea.Id, out var r) ? SentInstallmentState.SentWithoutResponse
                    : resultadosPendientes.Contains(r.Id) ? SentInstallmentState.DeductedPendingTransfer
                    : r.Outcome == PayrollResultOutcome.NotDeducted || r.NotDeductedAmount > 0m ? SentInstallmentState.NotDeducted
                    : SentInstallmentState.None;
                if (estado != SentInstallmentState.None)
                {
                    enviadas[credito] = Enviada(linea, estado);
                    continue;
                }
            }
            // Lo descontado y sin girar de una línea anterior del crédito (la más reciente).
            var pendiente = pendientes.Where(p => p.LoanId == credito && lineas.ContainsKey(p.PayrollDeductionFileLineId))
                .Select(p => lineas[p.PayrollDeductionFileLineId]).OrderByDescending(l => l.Anio).ThenByDescending(l => l.Mes).ThenByDescending(l => l.SubPeriodo)
                .FirstOrDefault();
            if (pendiente is not null) enviadas[credito] = Enviada(pendiente, SentInstallmentState.DeductedPendingTransfer);
        }
        return enviadas;
    }

    // ------------------------------------------------------------------------------------------------------------------- nómina propia --

    /// <summary>El descuento vivo de la nómina propia que corresponde a la cuota: el del período que contiene su vencimiento o, si no, el primero vivo.</summary>
    private static CuotaEnviada? DeNomina(CuotaPorConsultar c, IReadOnlyList<DescuentoDeNominaVivoDto> descuentos)
    {
        var elegido = (c.Vencimiento is { } vence ? descuentos.Where(d => d.PeriodStart <= vence && vence <= d.PeriodEnd).OrderBy(d => d.State).FirstOrDefault() : null)
                      ?? descuentos.Where(d => d.State != EstadoDelDescuentoDeNomina.Approved).OrderBy(d => d.PeriodStart).FirstOrDefault()
                      ?? descuentos.OrderByDescending(d => d.PeriodStart).FirstOrDefault();
        if (elegido is null) return null;
        var estado = elegido.State switch
        {
            EstadoDelDescuentoDeNomina.Calculated => SentInstallmentState.OwnPayrollCalculated,
            EstadoDelDescuentoDeNomina.Approved => SentInstallmentState.OwnPayrollApproved,
            _ => SentInstallmentState.OwnPayrollGenerated,
        };
        return new CuotaEnviada(c.LoanId, estado, elegido.InstallmentNumber ?? c.Numero, c.Vencimiento, null, null, null, null, elegido.Period, null, null,
            elegido.Amount)
        {
            Corrida = elegido.RunPublicId,
        };
    }

    internal static string Mes(int anio, int mes) => new DateOnly(anio, mes, 1).ToString("MMMM 'de' yyyy", Colombia);
}

/// <summary>
/// Los textos de la cuota enviada (feature 028, T066; HU1-3, HU4): dónde va la cuota y qué pasa con ella en la recogida según se incluya o se deje a su
/// canal. Los usan la liquidación de la recogida (la consulta de productos, la solicitud y la vista previa del desembolso) y, en la caja, el detalle. (nuevo)
/// </summary>
public static class TextosDeCuotaEnviada
{
    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    /// <summary>El nombre del parámetro que decide lo que la pagaduría gire de un crédito ya recogido, como lo lee la persona.</summary>
    public const string GiroPosterior = "«lo que la pagaduría gire de un crédito recogido» (Recogida.GiroPosteriorDeCreditoRecogido)";

    /// <summary>«noviembre de 2026» o, en una quincena, «noviembre de 2026 (primera quincena)».</summary>
    public static string PeriodoDeLibranza(int anio, int mes, int subPeriodo) =>
        CuotaEnviadaDelCredito.Mes(anio, mes) + subPeriodo switch { 2 => " (segunda quincena)", _ => string.Empty };

    /// <summary>Dónde va la cuota, sin el sujeto: «ya se envió a la pagaduría X en el archivo ADP-000123 de noviembre de 2026».</summary>
    public static string DondeVa(CuotaEnviada e)
    {
        ArgumentNullException.ThrowIfNull(e);
        var archivo = e.Archivo is { Length: > 0 } a ? $" en el archivo {a}" : string.Empty;
        var periodo = e.Periodo is { Length: > 0 } p ? $" de {p}" : string.Empty;
        var pagaduria = e.Pagaduria is { Length: > 0 } n ? $"la pagaduría {n}" : "la pagaduría";
        var nomina = e.Periodo is { Length: > 0 } np ? $"la nómina propia del período {np}" : "la nómina propia";
        return e.Estado switch
        {
            SentInstallmentState.SentWithoutResponse => $"ya se envió a {pagaduria}{archivo}{periodo} y no ha respondido",
            SentInstallmentState.DeductedPendingTransfer => $"ya la descontó {pagaduria}{archivo}{periodo} y está pendiente de girarla",
            SentInstallmentState.NotDeducted => $"{pagaduria} no la descontó{archivo}{periodo}",
            SentInstallmentState.OwnPayrollGenerated => $"ya está en {nomina} (novedad generada, sin calcular)",
            SentInstallmentState.OwnPayrollCalculated => $"ya la calculó {nomina} (corrida en borrador)",
            SentInstallmentState.OwnPayrollApproved => $"ya la descontó y aplicó {nomina}",
            _ => string.Empty,
        };
    }

    /// <summary>«La cuota 7 ya se envió a la pagaduría X en el archivo ADP-000123 de noviembre de 2026 y no ha respondido.» (sin la cuota: «La cuota del período …»).</summary>
    public static string Frase(CuotaEnviada e)
    {
        ArgumentNullException.ThrowIfNull(e);
        return (e.Cuota is { } n ? $"La cuota {n} " : "La cuota del período ") + DondeVa(e) + ".";
    }

    /// <summary>
    /// Qué pasa con la cuota causada en la recogida: con «Excluir», que se deja en el crédito y la paga su canal (el crédito queda cancelado cuando la pague);
    /// con «Incluir» y la cuota enviada, que se recoge completa y qué pasa con lo que el canal descuente después. Nula si se incluye y no hay nada enviado.
    /// </summary>
    public static string? EnLaRecogida(int cuota, decimal valor, InstallmentTreatment tratamiento, CuotaEnviada? enviada)
    {
        var sujeto = $"La cuota {cuota} por {Pesos(valor)}";
        var donde = enviada is not null && enviada.Estado != SentInstallmentState.None ? $"{sujeto} {DondeVa(enviada)}" : null;
        if (tratamiento == InstallmentTreatment.Exclude)
        {
            // HU5-4 (D13-A): la cuota queda tal como se envió, con el interés de su período completo.
            const string TalComoSeEnvio = " Queda tal como se envió, con el interés de su período completo, para que su canal la pague al peso.";
            if (enviada is { EnNominaPropia: true })
                return $"{donde}: se deja en el crédito y la descontará la nómina; el crédito queda cancelado cuando se apruebe la nómina que la aplica."
                       + TalComoSeEnvio;
            if (enviada is { Estado: SentInstallmentState.NotDeducted })
                return $"{donde}: se deja en el crédito y se cobra como cualquier cuota no descontada, con la regla del convenio." + TalComoSeEnvio;
            if (enviada is { EnPagaduria: true })
                return $"{donde}: se deja en el crédito y la pagará la pagaduría; el crédito queda cancelado cuando la gire." + TalComoSeEnvio;
            return $"{sujeto} se deja en el crédito: la paga su canal de siempre y el crédito queda cancelado cuando se pague." + TalComoSeEnvio;
        }
        if (donde is null) return null;
        return enviada!.Estado switch
        {
            SentInstallmentState.OwnPayrollApproved => $"{donde}: ya está pagada.",
            SentInstallmentState.OwnPayrollGenerated or SentInstallmentState.OwnPayrollCalculated =>
                $"{donde}: se recoge completa; al aprobar la nómina, el descuento de este crédito baja a cero y el neto del empleado sube.",
            SentInstallmentState.NotDeducted => $"{donde}: se recoge completa.",
            _ => $"{donde}: se recoge completa; lo que la pagaduría gire después se tratará según {GiroPosterior}.",
        };
    }

    private static string Pesos(decimal valor) => "$" + valor.ToString("#,##0.##", Colombia);
}
