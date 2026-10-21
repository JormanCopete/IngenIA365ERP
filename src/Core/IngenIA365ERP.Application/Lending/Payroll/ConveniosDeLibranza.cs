using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Payroll;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Payroll;

// Feature 013, K2 (US10, bloque B; T0833–T0845; data-model §24; api.md §13): lo que comparten los comandos y consultas de la libranza —el período
// de un archivo, las reglas del convenio que no tienen columna propia, los parámetros de la libranza y las búsquedas comunes—. (nuevo)

/// <summary>
/// El período de nómina de un archivo de descuentos (data-model §24.6: año, mes y sub-período): mensual = el mes entero (sub-período 1);
/// quincenal = 1–15 (1) y 16–fin de mes (2); las demás periodicidades se toman por mes entero (sub-período 1). (nuevo)
/// </summary>
public sealed record PeriodoDeLibranza(int Anio, int Mes, int SubPeriodo, DateOnly Desde, DateOnly Hasta)
{
    /// <summary>El período <paramref name="anio"/>-<paramref name="mes"/> (y sub-período) de una pagaduría con la periodicidad dada; nulo si no existe.</summary>
    public static PeriodoDeLibranza? De(RepaymentPeriodicity periodicidad, int anio, int mes, int? subPeriodo)
    {
        if (anio is < 2000 or > 2999 || mes is < 1 or > 12) return null;
        var primero = new DateOnly(anio, mes, 1);
        var ultimo = primero.AddMonths(1).AddDays(-1);
        if (periodicidad == RepaymentPeriodicity.Biweekly)
        {
            var sub = subPeriodo ?? 1;
            return sub switch
            {
                1 => new PeriodoDeLibranza(anio, mes, 1, primero, new DateOnly(anio, mes, 15)),
                2 => new PeriodoDeLibranza(anio, mes, 2, new DateOnly(anio, mes, 16), ultimo),
                _ => null,
            };
        }
        return subPeriodo is null or 1 ? new PeriodoDeLibranza(anio, mes, 1, primero, ultimo) : null;
    }

    /// <summary>Un día del mes del período, recortado al último día del mes.</summary>
    public DateOnly Dia(int dia)
    {
        var ultimo = DateTime.DaysInMonth(Anio, Mes);
        return new DateOnly(Anio, Mes, Math.Clamp(dia, 1, ultimo));
    }

    public string Texto => string.Create(CultureInfo.InvariantCulture, $"{Anio}-{Mes:00}") + (SubPeriodo > 1 ? $"/{SubPeriodo}" : string.Empty);
}

/// <summary>
/// Las reglas del convenio que viajan en <c>LND_PayrollAgreementVersions.NoveltyRulesJson</c> (data-model §24.2): qué novedades reporta la
/// pagaduría y el <b>día de envío</b> del archivo, que la tabla no tiene en columna propia (el corte y el pago sí). (nuevo)
/// </summary>
public sealed record ReglasDelConvenio(int? SendDay, IReadOnlyList<string>? Reports, string? Text)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Las novedades que reporta una pagaduría si el convenio no dice otra cosa: todas.</summary>
    public static IReadOnlyList<string> TodasLasNovedades { get; } = Enum.GetNames<PayrollNoveltyKind>();

    public string Serializar() => JsonSerializer.Serialize(this, Json);

    /// <summary>Lee el JSON guardado; uno que no es de esta forma (un texto o un objeto antiguo) queda como texto libre.</summary>
    public static ReglasDelConvenio Leer(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Trim() == "{}") return new ReglasDelConvenio(null, null, null);
        try
        {
            return JsonSerializer.Deserialize<ReglasDelConvenio>(json, Json) ?? new ReglasDelConvenio(null, null, json);
        }
        catch (JsonException)
        {
            return new ReglasDelConvenio(null, null, json);
        }
    }
}

/// <summary>Los parámetros de la libranza que se leen a una fecha. (nuevo)</summary>
public sealed record ParametrosDeLibranzaLeidos(int VentanaDeGiroDias, int? UmbralDeMoraDias);

/// <summary>Las búsquedas y lecturas comunes de la libranza. Sólo lee. (nuevo)</summary>
public sealed class ConveniosDeLibranza(IApplicationDbContext db, ReglasDeParametrosDeCartera parametros, IValoresNacionales valores, IPerfilRegulatorio perfil)
{
    /// <summary>La regla del convenio por defecto para el no aplicado (plantillas.md §8: <c>MovedToCashPayment</c>).</summary>
    public const NonDeductionFollowUpAction AccionPorDefecto = NonDeductionFollowUpAction.MovedToCashPayment;

    /// <summary>¿La persona es jurídica? (el mismo criterio de Comercial: tipo 02 o razón social).</summary>
    public static bool EsJuridica(Person p) => p.PersonType == "02" || !string.IsNullOrWhiteSpace(p.BusinessName);

    /// <summary>La persona jurídica de la cooperativa: la del maestro con el NIT de la empresa (I13); nula si no está registrada.</summary>
    public async Task<Person?> PersonaDeLaCooperativaAsync(CancellationToken ct)
    {
        var nit = await db.Companies.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.Id).Select(c => c.TaxId).FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(nit)) return null;
        var limpio = nit.Trim();
        return await db.People.AsNoTracking().Where(p => p.TaxId == limpio && !p.IsDeleted).OrderBy(p => p.Id).FirstOrDefaultAsync(ct);
    }

    /// <summary>¿El perfil regulatorio vigente registra a la cooperativa como operadora de libranza (FR-108)? Error si no.</summary>
    public async Task<Error?> RegistroDeOperadorAsync(DateOnly fecha, CancellationToken ct)
    {
        var vigente = await perfil.VigenteAsync(fecha, null, ct);
        return vigente is { LibranzaOperatorRegistered: true } ? null : ErroresDeLibranza.SinRegistroDeOperador(fecha);
    }

    /// <summary>El convenio de la nómina propia vivo, o nulo (en K1 no existe: Nómina sigue por el contrato sin convenio, C32).</summary>
    public Task<PayrollAgreement?> NominaPropiaAsync(CancellationToken ct) =>
        db.PayrollAgreements.AsNoTracking().FirstOrDefaultAsync(a => a.IsOwnPayroll && !a.IsDeleted, ct);

    /// <summary>Las versiones vivas de un convenio, en orden de vigencia.</summary>
    public async Task<IReadOnlyList<PayrollAgreementVersion>> VersionesAsync(int convenioId, CancellationToken ct) =>
        await db.PayrollAgreementVersions.AsNoTracking().Where(v => v.PayrollAgreementId == convenioId && !v.IsDeleted)
            .OrderBy(v => v.ValidFrom).ToListAsync(ct);

    /// <summary>La versión que rige el <paramref name="dia"/>, o nula.</summary>
    public async Task<PayrollAgreementVersion?> VersionVigenteAsync(int convenioId, DateOnly dia, CancellationToken ct) =>
        PayrollAgreementVersion.Vigente(await VersionesAsync(convenioId, ct), dia);

    /// <summary>
    /// La ventana de giro (la del convenio o <c>Libranza.VentanaDeGiroDias</c>, acotada por la ley) y el umbral de mora de la pagaduría
    /// (<c>Libranza.UmbralDeMoraDePagaduria</c>; vacío o sin el límite replicado = nulo: la mora de la pagaduría nunca se imputa al deudor, C71).
    /// </summary>
    public async Task<Result<ParametrosDeLibranzaLeidos>> ParametrosAsync(PayrollAgreementVersion version, DateOnly fecha, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(version);
        int ventana;
        if (version.TransferWindowDays is { } propia) ventana = propia;
        else
        {
            var leida = await parametros.LeerAsync(ParametrosDeCartera.LibranzaVentanaDeGiroDias, fecha, ct: ct);
            if (leida.IsFailure) return Result.Failure<ParametrosDeLibranzaLeidos>(leida.Error);
            ventana = Entero(leida.Value) ?? 0;
        }
        int? umbral = null;
        var leido = await parametros.LeerAsync(ParametrosDeCartera.LibranzaUmbralDeMoraDePagaduria, fecha, ct: ct);
        if (leido.IsSuccess) umbral = Entero(leido.Value);
        return Result.Success(new ParametrosDeLibranzaLeidos(Math.Max(0, ventana), umbral));
    }

    /// <summary>
    /// Los topes de descuento a la fecha (FR-110): el límite legal replicado (<c>LIBRANZA_CAPACIDAD_DESCUENTO</c>, obligatorio), el de la cooperativa
    /// (<c>Libranza.CapacidadDeDescuento</c>, vacío = no rige) y el del convenio (nulo = sin tope propio).
    /// </summary>
    public async Task<Result<TopesDeDescuento>> TopesAsync(decimal? delConvenio, DateOnly fecha, CancellationToken ct)
    {
        var limite = await LimiteLegalAsync(fecha, ct);
        if (limite.IsFailure) return Result.Failure<TopesDeDescuento>(limite.Error);
        decimal? deLaCooperativa = null;
        var leido = await parametros.LeerAsync(ParametrosDeCartera.LibranzaCapacidadDeDescuento, fecha, ct: ct);
        if (leido.IsSuccess && decimal.TryParse(leido.Value.DeLaCooperativa.Texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var propio) && propio > 0m)
            deLaCooperativa = propio;
        return Result.Success(new TopesDeDescuento(limite.Value, deLaCooperativa, delConvenio));
    }

    /// <summary>El límite legal de la capacidad de descuento a la fecha (<c>NationalValues.Missing</c> si no está replicado).</summary>
    public async Task<Result<decimal>> LimiteLegalAsync(DateOnly fecha, CancellationToken ct)
    {
        var limite = await valores.LeerAsync(CodigosDeValorNacional.Limite, CatalogoDeLimitesLegales.LibranzaCapacidadDescuento, fecha, null, ct);
        if (limite.IsFailure) return Result.Failure<decimal>(limite.Error);
        return limite.Value.Valor is { } v and > 0m and <= 1m
            ? Result.Success(v)
            : Result.Failure<decimal>(Saas.NationalValues.ErroresDeValoresNacionales.Faltante(CodigosDeValorNacional.Limite,
                CatalogoDeLimitesLegales.LibranzaCapacidadDescuento, fecha));
    }

    /// <summary>
    /// Lo que la pagaduría ya descuenta al deudor por Cartera: la próxima cuota a la fecha de cada crédito vivo vinculado al mismo convenio
    /// (o, sin convenio, a la misma pagaduría), sin el crédito <paramref name="excepto"/>.
    /// </summary>
    public async Task<decimal> DescuentosVigentesAsync(int deudorId, int? convenioId, int? pagaduriaId, DateOnly fecha, int? excepto, CancellationToken ct)
    {
        var creditos = await db.Loans.AsNoTracking()
            .Where(l => !l.IsDeleted && l.DebtorPersonId == deudorId && (l.Status == LoanStatus.Active || l.Status == LoanStatus.PendingDisbursement)
                        && (excepto == null || l.Id != excepto))
            .Select(l => l.Id).ToListAsync(ct);
        if (creditos.Count == 0) return 0m;
        var vinculados = await db.LoanPayrollLinks.AsNoTracking()
            .Where(v => !v.IsDeleted && creditos.Contains(v.LoanId) && v.ValidFrom <= fecha && (v.ValidTo == null || v.ValidTo >= fecha)
                        && (convenioId != null ? v.PayrollAgreementId == convenioId : v.PaymasterPersonId == pagaduriaId))
            .Select(v => v.LoanId).Distinct().ToListAsync(ct);
        return await ProximasCuotasAsync(vinculados, fecha, ct);
    }

    /// <summary>La próxima cuota (vencimiento ≥ fecha) del plan vigente de cada crédito, sumadas.</summary>
    public async Task<decimal> ProximasCuotasAsync(IReadOnlyCollection<int> creditos, DateOnly fecha, CancellationToken ct)
    {
        if (creditos.Count == 0) return 0m;
        var ids = creditos.ToList();
        var planes = await db.RepaymentScheduleVersions.AsNoTracking().Where(v => ids.Contains(v.LoanId) && !v.IsDeleted)
            .Select(v => new { v.Id, v.LoanId, v.VersionNumber }).ToListAsync(ct);
        var vigentes = planes.GroupBy(p => p.LoanId).Select(g => g.MaxBy(p => p.VersionNumber)!.Id).ToList();
        if (vigentes.Count == 0) return 0m;
        var proximas = await db.RepaymentInstallments.AsNoTracking().Where(c => vigentes.Contains(c.ScheduleVersionId) && c.DueDate >= fecha)
            .Select(c => new { c.ScheduleVersionId, c.DueDate, c.Total }).ToListAsync(ct);
        return proximas.GroupBy(c => c.ScheduleVersionId).Sum(g => g.MinBy(c => c.DueDate)!.Total);
    }

    private static int? Entero(ParametroDeCartera p) =>
        p.Valor switch
        {
            int i => i,
            long l => (int)l,
            decimal d => (int)d,
            _ => int.TryParse(p.Texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null,
        };
}
