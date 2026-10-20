using System.Globalization;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Deposits;
using IngenIA365ERP.Domain.Lending.Parameters;

namespace IngenIA365ERP.Application.Lending.Deposits.Accrual;

/// <summary>
/// La política de retención que rige y el perfil del titular (feature 014, US5, T128, T132; research R06, E15, E94; FR-021): lee los parámetros
/// <c>Retencion.*</c> con vigencia —la tarifa de la clase con ámbito de línea (la versión nombra su parámetro; el permanente y el contractual vacíos caen a la
/// del ahorro a la vista), el umbral diario en UVT, su alcance, si se descuenta el componente inflacionario y el redondeo—, la UVT del día y el último
/// componente inflacionario publicado (el del año anterior y, si todavía no se publicó, el de dos años antes; sin ninguno no se descuenta). Antes de que
/// suba la entrega (K8) rige el defecto del catálogo. El perfil sale de <c>COR_People</c> (012): no se retiene a quien no se le practican retenciones
/// (<c>WithholdingExempt</c>, los no contribuyentes del art. 23 ET) ni al autorretenedor; el componente se descuenta a la persona natural. (nuevo)
/// </summary>
public sealed class ReglasDeRetencion(ReglasDeParametrosDeCartera parametros, IValoresNacionales valores)
{
    /// <summary>Tipo de persona natural en <c>COR_People.PersonType</c>.</summary>
    public const string PersonaNatural = "01";

    /// <summary>La política a <paramref name="fecha"/> para la versión de la línea.</summary>
    public async Task<Result<PoliticaDeRetencion>> PoliticaAsync(DepositLineVersion version, DateOnly fecha, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(version);
        var tarifa = await TarifaAsync(version, fecha, ct);
        var umbral = await DecimalAsync(ParametrosDeCartera.RetencionUmbralDiarioUvt, fecha, ParameterScopeKind.None, 0, ct) ?? 0m;
        var alcance = Enum.TryParse<WithholdingThresholdScope>(await TextoAsync(ParametrosDeCartera.RetencionAlcanceDelUmbral, fecha, ct), out var a)
            ? a : WithholdingThresholdScope.ExceptTermDeposit;
        var componenteAplica = !string.Equals(await TextoAsync(ParametrosDeCartera.RetencionComponenteInflacionarioAplica, fecha, ct), "false",
            StringComparison.OrdinalIgnoreCase);
        var unidad = string.Equals(await TextoAsync(ParametrosDeCartera.RetencionRedondeo, fecha, ct), "Centavo", StringComparison.Ordinal) ? Domain.Lending.RedondeoDeCartera.Centavo : 1m;

        var clase = version.DepositClass;
        var aplicaUmbral = alcance == WithholdingThresholdScope.AllDepositClasses
                           || (alcance == WithholdingThresholdScope.ExceptTermDeposit && clase != DepositClass.TermDeposit);
        var uvt = 0m;
        if (aplicaUmbral && umbral > 0m)
        {
            var leida = await valores.LeerAsync(CodigosDeValorNacional.Uvt, string.Empty, fecha, ct: ct);
            if (leida.IsFailure || leida.Value.Valor is not { } valor)
                return Result.Failure<PoliticaDeRetencion>(new ErrorConDatos("NationalValues.Missing",
                    $"No hay UVT vigente el {ErroresDeCuentas.Fecha(fecha)}: la retención no se puede calcular.", new { code = CodigosDeValorNacional.Uvt, date = fecha }));
            uvt = valor;
        }
        decimal? componente = componenteAplica ? await ComponenteAsync(fecha, ct) : null;
        return Result.Success(new PoliticaDeRetencion(tarifa, umbral, uvt, alcance, componenteAplica, componente, unidad));
    }

    /// <summary>El perfil de retención del titular y si es persona natural (a quien se descuenta el componente inflacionario).</summary>
    public static (PerfilDeRetencion Perfil, bool PersonaNaturalNoObligada) PerfilDe(Person titular)
    {
        ArgumentNullException.ThrowIfNull(titular);
        // Respuesta 63 B del dueño (E250): el componente inflacionario se descuenta a la persona natural NO obligada a llevar contabilidad (ET arts. 38 a 41).
        var natural = !LectorDeCuentas.EsJuridica(titular) && !titular.IsObligatedToKeepBooks;
        if (titular.WithholdingExempt) return (PerfilDeRetencion.NonTaxpayer, natural);
        if (titular.IsSelfWithholder) return (PerfilDeRetencion.SelfWithholder, natural);
        return (string.IsNullOrWhiteSpace(titular.PersonType) ? PerfilDeRetencion.NoProfile : PerfilDeRetencion.Standard, natural);
    }

    /// <summary>
    /// La tarifa de la clase (FR-021): el parámetro que nombra la versión con el ámbito de su línea; el permanente o el contractual sin valor, la del ahorro
    /// a la vista; sin lectura, el defecto del catálogo.
    /// </summary>
    private async Task<decimal> TarifaAsync(DepositLineVersion version, DateOnly fecha, CancellationToken ct)
    {
        var clave = string.IsNullOrWhiteSpace(version.WithholdingRateParameterCode) ? ParametrosDeCartera.RetencionTarifaAhorro : version.WithholdingRateParameterCode;
        var propia = await DecimalAsync(clave, fecha, ParameterScopeKind.CreditLine, version.CreditLineId, ct);
        if (propia is { } t) return t;
        return await DecimalAsync(ParametrosDeCartera.RetencionTarifaAhorro, fecha, ParameterScopeKind.CreditLine, version.CreditLineId, ct) ?? 0m;
    }

    /// <summary>El último componente inflacionario publicado a la fecha (clave = año gravable): el del año anterior o, si falta, el de dos años antes.</summary>
    private async Task<decimal?> ComponenteAsync(DateOnly fecha, CancellationToken ct)
    {
        foreach (var anio in new[] { fecha.Year - 1, fecha.Year - 2 })
        {
            var leido = await valores.LeerAsync(CodigosDeValorNacional.ComponenteInflacionario, anio.ToString(CultureInfo.InvariantCulture), fecha, ct: ct);
            if (leido.IsSuccess && leido.Value.Valor is { } v) return v;
        }
        return null;
    }

    private async Task<decimal?> DecimalAsync(string clave, DateOnly fecha, ParameterScopeKind ambito, int ambitoId, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(clave, fecha, null, ambito, ambitoId, ct);
        if (leido.IsSuccess && leido.Value.Valor is decimal d) return d;
        var texto = leido.IsSuccess ? leido.Value.Texto : Defecto(clave);
        return decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    private async Task<string> TextoAsync(string clave, DateOnly fecha, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(clave, fecha, ct: ct);
        return leido.IsSuccess ? leido.Value.Texto : Defecto(clave);
    }

    private static string Defecto(string clave) =>
        ParametrosDeCartera.Definiciones.FirstOrDefault(d => d.Clave == clave)?.DefectoSeguro ?? string.Empty;
}
