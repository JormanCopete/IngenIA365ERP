using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Calculation.Bases;
using IngenIA365ERP.Domain.Payroll.Policies;
using IngenIA365ERP.Domain.Payroll.Settlements;
using IngenIA365ERP.Domain.Payroll.Settlements.Calendar;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Services;

/// <summary>
/// Las decisiones de la cooperativa que el motor, las liquidaciones y la aprobación consultan,
/// <b>tipadas y con vigencia</b> (feature 010, research R4, data-model §2.1). Se leen de
/// <c>PAY_CompanyPolicies</c> a una fecha: la vigencia que cubre esa fecha manda, así que una
/// prima de junio se liquida con la política que regía en junio aunque la contadora la haya
/// cambiado en agosto. Los valores admitidos y el defecto de cada clave viven en
/// <see cref="CompanyPolicyKeys"/> como texto; aquí se tipan.
///
/// <para>
/// Compatibilidad: <c>Exonerada114_1</c> y <c>AllowSameUserApproval</c> nacieron en
/// <c>COR_SystemSettings</c> (<c>Payroll.ApplyEmployerExemption</c>, <c>Payroll.AllowSameUserApproval</c>).
/// La migración de datos las copia a la tabla nueva; si la clave <b>no existe</b> allí (ninguna
/// vigencia, de ninguna fecha), se cae a <c>COR_SystemSettings</c>, y si tampoco está, al defecto.
/// Una clave que sí existe pero no tiene vigencia a la fecha usa el defecto: la caída es por
/// clave ausente, no por fecha sin cubrir. <c>Payroll.Rounding</c> y
/// <c>Payroll.VariationThresholdPercent</c> siguen en <c>COR_SystemSettings</c> (no son políticas
/// con vigencia; D-09 de la feature 005).
/// </para>
///
/// <para>
/// Un valor que no está entre los admitidos de su clave <b>no</b> se tapa con el defecto: es un
/// dato roto y se dice cuál (<c>Payroll.CompanyPolicy.ValueInvalid</c> con la clave). Sin eso una
/// política mal escrita a mano correría con el defecto sin que nadie lo notara.
/// </para>
/// </summary>
public sealed class PayrollPolicyReader(IApplicationDbContext db, IDateTimeService clock)
{
    public const string RoundingKey = "Payroll.Rounding";
    public const string VariationThresholdKey = "Payroll.VariationThresholdPercent";
    public const string AllowSameUserApprovalKey = "Payroll.AllowSameUserApproval";
    public const string ApplyEmployerExemptionKey = "Payroll.ApplyEmployerExemption";

    public const string ValueInvalidCode = "Payroll.CompanyPolicy.ValueInvalid";

    /// <summary>Umbral por defecto del comparativo (D-09); es política de la cooperativa, no un valor legal.</summary>
    private const decimal DefaultVariationThresholdPercent = 10;

    /// <summary>Las políticas a una fecha (<see cref="ReadAsync(DateOnly, CancellationToken)"/>); lanza si un valor guardado no es admisible.</summary>
    public async Task<PoliticasDeNomina> ReadAsync(DateOnly fecha, CancellationToken ct)
    {
        var leidas = await LeerAsync(fecha, ct);
        if (leidas.IsFailure) throw new PoliticaDeNominaInvalidaException(leidas.Error);
        return leidas.Value;
    }

    /// <summary>Las políticas vigentes hoy (para lo que no tiene fecha de negocio propia: comparativo, aprobación de la ordinaria).</summary>
    public Task<PoliticasDeNomina> ReadAsync(CancellationToken ct) => ReadAsync(clock.TodayUtc, ct);

    /// <summary>Como <see cref="ReadAsync(DateOnly, CancellationToken)"/> pero devuelve el error en vez de lanzarlo, para los handlers que responden con sobre.</summary>
    public async Task<Result<PoliticasDeNomina>> LeerAsync(DateOnly fecha, CancellationToken ct)
    {
        // --- COR_SystemSettings: las dos que no tienen vigencia y la caída de compatibilidad ---
        var filas = await db.SystemSettings.AsNoTracking()
            .Where(s => s.ModulePrefix == "PAY")
            .Select(s => new { s.SettingKey, s.SettingValue })
            .ToListAsync(ct);
        var sistema = filas.ToDictionary(f => f.SettingKey, f => f.SettingValue, StringComparer.OrdinalIgnoreCase);

        var redondeo = sistema.TryGetValue(RoundingKey, out var r) && Enum.TryParse<PayrollRounding>(r, true, out var pr)
            ? pr : PayrollRounding.Peso;
        var umbral = sistema.TryGetValue(VariationThresholdKey, out var u)
                     && decimal.TryParse(u, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)
            ? d : DefaultVariationThresholdPercent;

        // --- PAY_CompanyPolicies: todas las filas de cada clave, para distinguir «no existe» de «sin vigencia a la fecha» ---
        var politicas = await db.CompanyPolicies.AsNoTracking()
            .Select(p => new { p.Key, p.Value, p.ValidFrom, p.ValidTo })
            .ToListAsync(ct);
        var porClave = politicas.ToLookup(p => p.Key, StringComparer.Ordinal);

        string Valor(string clave, string? caidaEnSistema = null)
        {
            var definicion = CompanyPolicyKeys.Buscar(clave)
                ?? throw new InvalidOperationException($"La clave {clave} no está en el catálogo de políticas.");
            var versiones = porClave[clave].ToList();
            if (versiones.Count > 0)
            {
                var vigente = versiones
                    .Where(v => v.ValidFrom <= fecha && (v.ValidTo is null || v.ValidTo >= fecha))
                    .OrderByDescending(v => v.ValidFrom)
                    .FirstOrDefault();
                if (vigente is not null) return vigente.Value.Trim();
                return definicion.Defecto ?? string.Empty;
            }
            if (caidaEnSistema is not null && sistema.TryGetValue(caidaEnSistema, out var heredado) && !string.IsNullOrWhiteSpace(heredado))
                return heredado.Trim();
            return definicion.Defecto ?? string.Empty;
        }

        var errores = new List<string>();
        bool Booleano(string clave, string? caida = null)
        {
            var texto = Valor(clave, caida);
            if (bool.TryParse(texto, out var b)) return b;
            errores.Add(Invalido(clave, texto));
            return false;
        }
        string Admitido(string clave)
        {
            var texto = Valor(clave);
            if (CompanyPolicyKeys.Admite(clave, texto)) return texto;
            errores.Add(Invalido(clave, texto));
            return CompanyPolicyKeys.Buscar(clave)!.Defecto ?? string.Empty;
        }

        var exonerada = Booleano(CompanyPolicyKeys.Exonerada114_1, ApplyEmployerExemptionKey);
        var mismoUsuario = Booleano(CompanyPolicyKeys.AllowSameUserApproval, AllowSameUserApprovalKey);
        var semana = Admitido(CompanyPolicyKeys.SemanaLaboral) == CompanyPolicyKeys.SemanaLaboralValores.LunesAViernes
            ? SemanaLaboral.LunesAViernes : SemanaLaboral.LunesASabado;
        var pagoAnticipado = Booleano(CompanyPolicyKeys.VacacionesPagoAnticipado);
        var arlEnVacaciones = Booleano(CompanyPolicyKeys.CotizaArlEnVacaciones);
        var topes = Admitido(CompanyPolicyKeys.RetefteTopesAnualesModo) == CompanyPolicyKeys.RetefteTopesAnualesModoValores.Acumulado
            ? ModoDeTopesAnuales.Acumulado : ModoDeTopesAnuales.Mensualizado;
        var secuenciaP2 = Admitido(CompanyPolicyKeys.P2SecuenciaDepuracion) == CompanyPolicyKeys.P2SecuenciaDepuracionValores.DividirLuegoDepurar
            ? SecuenciaDepuracionP2.DividirLuegoDepurar : SecuenciaDepuracionP2.DepurarLuegoDividir;
        var plazoDian = Admitido(CompanyPolicyKeys.DianPlazoComputo) == CompanyPolicyKeys.DianPlazoComputoValores.Habiles
            ? ComputoDePlazo.Habiles : ComputoDePlazo.Calendario;
        var deduccion = Admitido(CompanyPolicyKeys.DeduccionAlRetiroModo) switch
        {
            CompanyPolicyKeys.DeduccionAlRetiroModoValores.SoloCuotasCausadas => DeduccionAlRetiro.SoloCuotasCausadas,
            CompanyPolicyKeys.DeduccionAlRetiroModoValores.NoProponer => DeduccionAlRetiro.NoProponer,
            _ => DeduccionAlRetiro.SaldoTotal,
        };

        var mapaTexto = Valor(CompanyPolicyKeys.DianMedioPagoMapa);
        var mapa = LeerMapa(mapaTexto);
        if (mapa is null) errores.Add(Invalido(CompanyPolicyKeys.DianMedioPagoMapa, mapaTexto));

        DateOnly? arranque = null;
        var arranqueTexto = Valor(CompanyPolicyKeys.ArranqueNominaFecha);
        if (!string.IsNullOrWhiteSpace(arranqueTexto))
        {
            if (DateOnly.TryParseExact(arranqueTexto, CompanyPolicyKeys.FormatoFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var f))
                arranque = f;
            else errores.Add(Invalido(CompanyPolicyKeys.ArranqueNominaFecha, arranqueTexto));
        }

        if (errores.Count > 0)
            return Result.Failure<PoliticasDeNomina>(new ErrorConDatos(ValueInvalidCode,
                "Hay políticas de nómina con un valor que no se reconoce; corríjalas en Nómina › Políticas: " + string.Join("; ", errores),
                new { keys = errores }));

        return Result.Success(new PoliticasDeNomina(
            fecha, redondeo, umbral, mismoUsuario, exonerada, semana, pagoAnticipado, arlEnVacaciones, topes, secuenciaP2, plazoDian,
            mapa ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), deduccion, arranque));
    }

    private static string Invalido(string clave, string valor)
    {
        var def = CompanyPolicyKeys.Buscar(clave);
        var admitidos = def is { Admitidos.Count: > 0 } ? $" (admite: {string.Join(", ", def.Admitidos)})" : def?.Formato is { } f ? $" (formato: {f})" : string.Empty;
        return $"{clave} = «{valor}»{admitidos}";
    }

    /// <summary>El JSON de <c>DianMedioPagoMapa</c> como diccionario forma de pago → código DIAN; nulo si no se puede leer.</summary>
    public static IReadOnlyDictionary<string, string>? LeerMapa(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var crudo = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return crudo is null ? null : new Dictionary<string, string>(crudo, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>Orden de la depuración en el cálculo del porcentaje fijo del procedimiento 2 (R8).</summary>
public enum SecuenciaDepuracionP2 { DepurarLuegoDividir = 0, DividirLuegoDepurar = 1 }

/// <summary>Cómo se cuentan los días de un plazo legal para el aviso (DIAN).</summary>
public enum ComputoDePlazo { Calendario = 0, Habiles = 1 }

/// <summary>Qué propone la definitiva como descuento de préstamos y libranzas (FR-018a).</summary>
public enum DeduccionAlRetiro { SaldoTotal = 0, SoloCuotasCausadas = 1, NoProponer = 2 }

/// <summary>
/// Las políticas de la cooperativa ya tipadas, vigentes a <see cref="Fecha"/>. Ninguna es un
/// valor legal: son decisiones (D-01, D-09 y las de data-model §2.1).
/// </summary>
public sealed record PoliticasDeNomina(
    DateOnly Fecha,
    PayrollRounding Rounding,
    decimal VariationThresholdPercent,
    bool AllowSameUserApproval,
    bool Exonerada114_1,
    SemanaLaboral SemanaLaboral,
    bool VacacionesPagoAnticipado,
    bool CotizaArlEnVacaciones,
    ModoDeTopesAnuales RetefteTopesAnualesModo,
    SecuenciaDepuracionP2 P2SecuenciaDepuracion,
    ComputoDePlazo DianPlazoComputo,
    IReadOnlyDictionary<string, string> DianMedioPagoMapa,
    DeduccionAlRetiro DeduccionAlRetiroModo,
    DateOnly? ArranqueNominaFecha)
{
    /// <summary>Nombre anterior de <see cref="Exonerada114_1"/> (feature 005): la nómina ordinaria lo lee por aquí.</summary>
    public bool ApplyEmployerExemption => Exonerada114_1;

    /// <summary>Lo que el motor ordinario recibe.</summary>
    public CalculationPolicies ForCalculation() => new() { Rounding = Rounding, ApplyEmployerExemption = Exonerada114_1 };

    /// <summary>Lo que el motor de liquidaciones recibe (Domain no conoce esta clase).</summary>
    public SettlementPolicies ForSettlement() => new()
    {
        Rounding = Rounding,
        SemanaLaboral = SemanaLaboral,
        VacacionesPagoAnticipado = VacacionesPagoAnticipado,
        RetefteTopesAnualesModo = RetefteTopesAnualesModo,
        PayrollStartDate = ArranqueNominaFecha is { } a ? a.ToDateTime(TimeOnly.MinValue) : null,
    };

    /// <summary>Código DIAN de medio de pago para la forma de pago de la ficha (<c>Transfer</c>, <c>Check</c>, <c>Cash</c>); nulo si el mapa no la trae.</summary>
    public string? CodigoDianDeMedioDePago(string formaDePago) =>
        DianMedioPagoMapa.TryGetValue(formaDePago, out var codigo) ? codigo : null;
}

/// <summary>Una política guardada con un valor que no está entre los admitidos de su clave; el mensaje nombra la clave.</summary>
public sealed class PoliticaDeNominaInvalidaException(Error error) : Exception(error.Message)
{
    public Error Error { get; } = error;
}
