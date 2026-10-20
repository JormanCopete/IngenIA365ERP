using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Liquidity;

/// <summary>Los códigos de error del fondo de liquidez (feature 014, US15, T216; api.md §15). (nuevo)</summary>
public static class ErroresDelFondoDeLiquidez
{
    public const string CodigoSinCategoria = "Lending.LiquidityFund.ProfileCategoryMissing";
    public const string CodigoReglasInvalidas = "Lending.LiquidityFund.RulesInvalid";
    public const string CodigoInvalido = "Lending.LiquidityFund.Invalid";
    public const string CodigoInexistente = "Lending.LiquidityFund.HoldingNotFound";
    public const string CodigoYaTerminada = "Lending.LiquidityFund.HoldingEnded";
    public const string CodigoSoporteRequerido = "Lending.LiquidityFund.SupportRequired";

    public static Error SinCategoria(DateOnly fecha) => new ErrorConDatos(CodigoSinCategoria,
        $"El perfil regulatorio que rige el {fecha:yyyy-MM-dd} no dice la categoría de la entidad para el fondo de liquidez: regístrela en el perfil "
        + "(básica, intermedia o plena).", new { date = fecha });

    public static Error ReglasInvalidas(string categoria) => new ErrorConDatos(CodigoReglasInvalidas,
        $"El parámetro {ParametrosDeCartera.FondoDeLiquidezPorcentajesPorCategoria} no trae reglas para la categoría «{categoria}» ni para «*», o su JSON está "
        + "mal formado.", new { category = categoria, parameter = ParametrosDeCartera.FondoDeLiquidezPorcentajesPorCategoria });

    public static Error Invalido(string campo, string mensaje) => new ErrorConDatos(CodigoInvalido, mensaje, new { field = campo });

    public static Error Inexistente() => new(CodigoInexistente, "La inversión del fondo de liquidez no existe.");

    public static Error YaTerminada(DateOnly dia) => new ErrorConDatos(CodigoYaTerminada,
        $"La inversión ya dejó de contar desde el {dia:yyyy-MM-dd}.", new { withdrawnOn = dia });

    public static Error SoporteRequerido() => new ErrorConDatos(CodigoSoporteRequerido,
        "Adjunte el soporte de la inversión (extracto, título o certificación) antes de registrarla.", new { field = "supportAttachmentPublicId" });
}

/// <summary>Qué tan lejos está el constituido del requerido. (nuevo)</summary>
public enum NivelDelFondoDeLiquidez
{
    Cubierto = 0,
    EnElMargen = 1,
    Insuficiente = 2,
}

/// <summary>Un depósito a la fecha con lo que el fondo y la medición del riesgo necesitan de él. (nuevo)</summary>
public sealed record DepositoALaFecha(
    int CuentaId,
    int PersonaId,
    int OficinaId,
    int LineaId,
    ClaseDeDepositoParaLiquidez Clase,
    decimal Capital,
    decimal InteresCausado,
    decimal InteresPorCausar,
    DateOnly? Vencimiento,
    int? DiasAlVencimiento,
    bool RedencionAnticipadaPactada,
    IReadOnlyList<(DateOnly Fecha, decimal Interes)> PagosDeIntereses)
{
    public DepositoParaFondo ParaFondo() => new(Clase, Capital, InteresCausado, InteresPorCausar, DiasAlVencimiento, RedencionAnticipadaPactada);
}

/// <summary>La posición del fondo a una fecha: la categoría, la regla, el cálculo, el constituido, la diferencia, el nivel y lo leído. (nuevo)</summary>
public sealed record PosicionDelFondoDeLiquidez(
    DateOnly Fecha,
    string Categoria,
    ReglaDeFondoDeLiquidez Regla,
    CalculoDelFondoDeLiquidez Calculo,
    decimal Constituido,
    decimal Diferencia,
    decimal Faltante,
    decimal Margen,
    NivelDelFondoDeLiquidez Nivel,
    string ParametrosJson,
    string Huella)
{
    /// <summary><c>General</c>, <c>PorMadurez</c> o <c>Transicion</c>.</summary>
    public string Etapa => Calculo.RegimenNuevoEnTransicion is not null ? "Transicion" : Calculo.Regimen.ToString();
}

/// <summary>Qué mide la medición de riesgo: el IRL (bandas cortas) o la brecha (bandas largas con la última abierta). (nuevo)</summary>
public enum MedidaDeRiesgoDeLiquidez
{
    Irl = 1,
    Brecha = 2,
}

/// <summary>Una medición del riesgo de liquidez con su umbral y los parámetros que rigieron. (nuevo)</summary>
public sealed record MedicionDeLiquidezALaFecha(
    DateOnly Fecha,
    MedidaDeRiesgoDeLiquidez Medida,
    MedicionDeRiesgoDeLiquidez Medicion,
    IReadOnlyList<ActivoLiquido> Activos,
    decimal Umbral,
    decimal UmbralDePlanDeAjuste,
    bool BajoElUmbral,
    IReadOnlyList<string> Supuestos);

/// <summary>Los depósitos asegurados a una fecha: si la entidad está inscrita, el tope que rige y lo de cada ahorrador. (nuevo)</summary>
public sealed record AseguradosALaFecha(DateOnly Fecha, bool Inscrita, decimal? Tope, string? FuenteDelTope, IReadOnlyList<AseguradoDeUnAhorrador> Ahorradores,
    IReadOnlyList<DepositoALaFecha> Depositos);

/// <summary>
/// Lo que la Cartera sabe del fondo de liquidez, del riesgo de liquidez y de los depósitos asegurados (feature 014, US15, T216; FR-025, FR-109, FR-110; research
/// R25 y §24). Lee el libro de cuentas (la autoridad, nunca las posiciones), las condiciones selladas del CDAT y del contractual, las versiones de línea
/// vigentes, el perfil regulatorio, los parámetros de la entidad con su vigencia y los pisos legales (valores nacionales <c>LIMITE</c>), y deja el cálculo a
/// los motores puros <see cref="FondoDeLiquidez"/>, <see cref="RiesgoDeLiquidez"/> y <see cref="DepositosAsegurados"/>. No escribe nada. (nuevo)
/// </summary>
public sealed class FondoDeLiquidezDeCartera(
    IApplicationDbContext db,
    IPerfilRegulatorio perfil,
    ReglasDeParametrosDeCartera parametros,
    IValoresNacionales valores,
    IActivosLiquidosParaCartera? contabilidad = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Días que se usan como «fuera de todo plazo» para lo que sólo cae en la banda abierta de la brecha (la parte estable).</summary>
    public const int SinPlazo = int.MaxValue / 2;

    // --------------------------------------------------------------------------------------------------- fondo --

    public async Task<Result<PosicionDelFondoDeLiquidez>> PosicionAsync(DateOnly fecha, CancellationToken ct)
    {
        var vigente = await perfil.VigenteAsync(fecha, null, ct);
        if (vigente?.LiquidityFundCategory is not { Length: > 0 } categoria) return Result.Failure<PosicionDelFondoDeLiquidez>(ErroresDelFondoDeLiquidez.SinCategoria(fecha));
        var textoDeReglas = await TextoAsync(ParametrosDeCartera.FondoDeLiquidezPorcentajesPorCategoria, fecha, ct);
        var reglas = ReglasDelFondoDeLiquidez.Leer(textoDeReglas);
        var regla = reglas is null ? null : ReglasDelFondoDeLiquidez.Para(reglas, categoria);
        var general = reglas is null ? null : ReglasDelFondoDeLiquidez.Para(reglas, ReglasDelFondoDeLiquidez.Cualquiera) ?? regla;
        if (regla is null || general is null) return Result.Failure<PosicionDelFondoDeLiquidez>(ErroresDelFondoDeLiquidez.ReglasInvalidas(categoria));

        var pisos = new PisosDelFondoDeLiquidez(
            await LimiteAsync(CatalogoDeLimitesLegales.FondoLiquidezDepositos, fecha, ct),
            await LimiteAsync(CatalogoDeLimitesLegales.FondoLiquidezPermanenteSoloAlRetiro, fecha, ct),
            await LimiteAsync(CatalogoDeLimitesLegales.FondoLiquidezPlenaBandaMedia, fecha, ct),
            await LimiteAsync(CatalogoDeLimitesLegales.FondoLiquidezPlenaPermanenteSoloAlRetiro, fecha, ct));
        var depositos = await DepositosAsync(fecha, ct);
        var calculo = FondoDeLiquidez.Calcular(fecha, regla, general, pisos, depositos.Select(d => d.ParaFondo()).ToList());
        var constituido = await ConstituidoAsync(fecha, ct);
        var margen = await DecimalAsync(ParametrosDeCartera.FondoDeLiquidezMargenDeAlerta, fecha, ct) ?? 0m;
        var diferencia = constituido - calculo.Requerido;
        var nivel = diferencia < 0m ? NivelDelFondoDeLiquidez.Insuficiente
            : margen > 0m && constituido < calculo.Requerido * (1m + margen) ? NivelDelFondoDeLiquidez.EnElMargen
            : NivelDelFondoDeLiquidez.Cubierto;
        var parametrosJson = JsonSerializer.Serialize(new
        {
            category = categoria, rules = textoDeReglas, floors = pisos, margin = margen, profileVersion = vigente.VersionPublicId,
        }, Json);
        var huella = new HuellaDeCartera().Fecha("date", fecha).Texto("category", categoria).Texto("rules", textoDeReglas)
            .Decimal("required", calculo.Requerido, 2).Decimal("constituted", constituido, 2).Entero("accounts", depositos.Count).Calcular();
        return Result.Success(new PosicionDelFondoDeLiquidez(fecha, categoria, regla, calculo, constituido, diferencia, Math.Max(0m, -diferencia), margen, nivel,
            parametrosJson, huella));
    }

    /// <summary>
    /// Los activos líquidos que la entidad configuró leer de Contabilidad (respuesta 99 B del dueño, E250; parámetro <c>RiesgoDeLiquidez.ActivosLiquidosContables</c>):
    /// el saldo positivo de cada prefijo de cuenta con el descuento de su clase. Sin parámetro o sin el puerto, ninguno. Un prefijo sin saldo no entra.
    /// </summary>
    public async Task<(List<ActivoLiquido> Activos, string Resumen)> ActivosDeContabilidadAsync(DateOnly fecha, CancellationToken ct)
    {
        if (contabilidad is null) return ([], string.Empty);
        var reglas = LeerActivosContables(await TextoAsync(ParametrosDeCartera.RiesgoDeLiquidezActivosLiquidosContables, fecha, ct));
        if (reglas.Count == 0) return ([], string.Empty);
        var saldos = await contabilidad.SaldosPorPrefijosAsync(reglas.Select(r => r.Prefijo).ToList(), fecha, ct);
        if (saldos.IsFailure) return ([], string.Empty);
        var activos = new List<ActivoLiquido>();
        for (var i = 0; i < reglas.Count; i++)
            if (saldos.Value[i].Saldo > 0m)
                activos.Add(new ActivoLiquido($"Contabilidad {reglas[i].Prefijo}", saldos.Value[i].Saldo, reglas[i].Descuento));
        return (activos, string.Join(", ", reglas.Select(r => $"{r.Prefijo} (descuento {r.Descuento:P0})")));
    }

    /// <summary>Las reglas del parámetro (prefijo y descuento entre 0 y 1); lo que no se entiende se ignora.</summary>
    public static IReadOnlyList<(string Prefijo, decimal Descuento)> LeerActivosContables(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return [];
        try
        {
            using var doc = JsonDocument.Parse(texto);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return [];
            var lista = new List<(string, decimal)>();
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Object) continue;
                var prefijo = e.TryGetProperty("prefijo", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString()?.Trim() : null;
                var descuento = e.TryGetProperty("descuento", out var d) && d.TryGetDecimal(out var valor) ? valor : 0m;
                if (string.IsNullOrEmpty(prefijo) || !prefijo.All(char.IsLetterOrDigit) || descuento is < 0m or > 1m) continue;
                lista.Add((prefijo, descuento));
            }
            return lista;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>El constituido del día: las inversiones registradas vigentes en esa fecha.</summary>
    public async Task<decimal> ConstituidoAsync(DateOnly fecha, CancellationToken ct) =>
        await db.LiquidityFundHoldings.AsNoTracking()
            .Where(h => !h.IsDeleted && h.ConstitutedOn <= fecha && (h.WithdrawnOn == null || h.WithdrawnOn > fecha))
            .SumAsync(h => h.Amount, ct);

    // ------------------------------------------------------------------------------------------------ depósitos --

    /// <summary>
    /// Las cuentas de captación vivas a la fecha con su saldo del libro (capital; interés por pagar y bonificación como causado; lo no reclamado no es del
    /// titular), su clase para el fondo (el permanente por lo que diga su línea vigente: sólo retirable al desvincularse o con retiro parcial), el
    /// vencimiento de la última versión de condiciones del CDAT y del contractual, y el interés por causar del plan del CDAT.
    /// </summary>
    public async Task<IReadOnlyList<DepositoALaFecha>> DepositosAsync(DateOnly fecha, CancellationToken ct)
    {
        var cuentas = await db.MemberAccounts.AsNoTracking()
            .Where(a => a.Nature == LineNature.Deposit && a.OpenedOn <= fecha && (a.ClosedOn == null || a.ClosedOn > fecha))
            .Select(a => new { a.Id, a.HolderPersonId, a.BranchId, a.CreditLineId, a.DepositClass }).ToListAsync(ct);
        if (cuentas.Count == 0) return [];
        var saldos = (await (from t in db.MemberAccountTransactions.AsNoTracking()
                    join a in db.MemberAccounts.AsNoTracking() on t.MemberAccountId equals a.Id
                    where a.Nature == LineNature.Deposit && !t.IsDeleted && t.ValueDate <= fecha
                          && (t.Concept == MemberAccountConcept.Principal || t.Concept == MemberAccountConcept.InterestPayable
                              || t.Concept == MemberAccountConcept.Bonus)
                    group t by new { t.MemberAccountId, t.Concept } into g
                    select new { g.Key.MemberAccountId, g.Key.Concept, Total = g.Sum(x => x.Amount) }).ToListAsync(ct))
            .GroupBy(s => s.MemberAccountId).ToDictionary(g => g.Key, g => g.ToDictionary(x => x.Concept, x => x.Total));

        var lineas = cuentas.Select(c => c.CreditLineId).Distinct().ToList();
        var versiones = (await db.DepositLineVersions.AsNoTracking().Where(v => lineas.Contains(v.CreditLineId) && v.ValidFrom <= fecha)
                .Select(v => new { v.CreditLineId, v.VersionNumber, v.WithdrawableOnlyOnMembershipWithdrawal, v.EarlyRedemptionAllowed }).ToListAsync(ct))
            .GroupBy(v => v.CreditLineId).ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.VersionNumber).First());

        var conPlazo = cuentas.Where(c => c.DepositClass is DepositClass.TermDeposit or DepositClass.Contractual).Select(c => c.Id).ToList();
        var condiciones = (await db.MemberAccountTermsVersions.AsNoTracking()
                .Where(v => conPlazo.Contains(v.MemberAccountId) && v.EffectiveValueDate <= fecha)
                .Select(v => new { v.Id, v.MemberAccountId, v.VersionNumber, v.MaturityDate }).ToListAsync(ct))
            .GroupBy(v => v.MemberAccountId).ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.VersionNumber).First());
        var idsDeVersion = condiciones.Values.Select(v => v.Id).ToList();
        var pagos = (await db.TermDepositScheduleItems.AsNoTracking().Where(p => idsDeVersion.Contains(p.TermsVersionId) && p.PaymentDate > fecha)
                .Select(p => new { p.TermsVersionId, p.PaymentDate, p.Interest }).ToListAsync(ct))
            .GroupBy(p => p.TermsVersionId).ToDictionary(g => g.Key, g => g.OrderBy(p => p.PaymentDate).Select(p => (p.PaymentDate, p.Interest)).ToList());

        var resultado = new List<DepositoALaFecha>(cuentas.Count);
        foreach (var c in cuentas)
        {
            var s = saldos.GetValueOrDefault(c.Id);
            var capital = s?.GetValueOrDefault(MemberAccountConcept.Principal) ?? 0m;
            var causado = (s?.GetValueOrDefault(MemberAccountConcept.InterestPayable) ?? 0m) + (s?.GetValueOrDefault(MemberAccountConcept.Bonus) ?? 0m);
            if (capital == 0m && causado == 0m) continue;
            var linea = versiones.GetValueOrDefault(c.CreditLineId);
            var clase = c.DepositClass switch
            {
                DepositClass.TermDeposit => ClaseDeDepositoParaLiquidez.Cdat,
                DepositClass.Contractual => ClaseDeDepositoParaLiquidez.Contractual,
                DepositClass.Permanent => linea?.WithdrawableOnlyOnMembershipWithdrawal == true
                    ? ClaseDeDepositoParaLiquidez.PermanenteSoloAlRetirarse
                    : ClaseDeDepositoParaLiquidez.PermanenteConRetiroParcial,
                _ => ClaseDeDepositoParaLiquidez.ALaVista,
            };
            DateOnly? vence = null;
            IReadOnlyList<(DateOnly, decimal)> pagosDeIntereses = [];
            var porCausar = 0m;
            if (condiciones.TryGetValue(c.Id, out var v))
            {
                vence = v.MaturityDate;
                if (pagos.TryGetValue(v.Id, out var lista))
                {
                    pagosDeIntereses = lista;
                    porCausar = Math.Max(0m, lista.Sum(p => p.Interest) - Math.Max(0m, causado));
                }
            }
            resultado.Add(new DepositoALaFecha(c.Id, c.HolderPersonId, c.BranchId, c.CreditLineId, clase, capital, Math.Round(causado, 2, MidpointRounding.AwayFromZero),
                Math.Round(porCausar, 2, MidpointRounding.AwayFromZero), vence, vence is { } f ? f.DayNumber - fecha.DayNumber : null,
                clase is ClaseDeDepositoParaLiquidez.Cdat or ClaseDeDepositoParaLiquidez.Contractual && linea?.EarlyRedemptionAllowed == true, pagosDeIntereses));
        }
        return resultado;
    }

    // ------------------------------------------------------------------------------------------ riesgo de liquidez --

    /// <summary>
    /// La medición del riesgo de liquidez a la fecha (research §24): los activos líquidos netos son lo constituido del fondo con el descuento de su clase
    /// (<c>RiesgoDeLiquidez.DescuentosPorInstrumento</c>); las entradas, las cuotas de cartera por vencer de los créditos vivos (capital e interés de la versión
    /// vigente de cada cuota) descontadas por <c>RiesgoDeLiquidez.IndiceDeMora</c>; las salidas contractuales, el CDAT (los intereses en sus fechas de pago y
    /// el capital al vencimiento) y el contractual al vencimiento; las no contractuales, la fracción del ahorro a la vista en la primera banda y la fracción
    /// mensual del permanente y de los aportes. En la brecha, lo que queda del ahorro a la vista y del permanente y los aportes va a la banda abierta.
    /// </summary>
    public async Task<MedicionDeLiquidezALaFecha> RiesgoAsync(DateOnly fecha, MedidaDeRiesgoDeLiquidez medida, CancellationToken ct)
    {
        var bandas = Bandas(await TextoAsync(medida == MedidaDeRiesgoDeLiquidez.Irl
            ? ParametrosDeCartera.RiesgoDeLiquidezBandasIrl
            : ParametrosDeCartera.RiesgoDeLiquidezBandasDeBrecha, fecha, ct));
        var abierta = bandas.Contains(null);
        var horizonte = bandas.Where(b => b is not null).Select(b => b!.Value).DefaultIfEmpty(0).Max();
        var supuestos = new List<string>();

        var descuentos = Descuentos(await TextoAsync(ParametrosDeCartera.RiesgoDeLiquidezDescuentosPorInstrumento, fecha, ct));
        var inversiones = await db.LiquidityFundHoldings.AsNoTracking()
            .Where(h => !h.IsDeleted && h.ConstitutedOn <= fecha && (h.WithdrawnOn == null || h.WithdrawnOn > fecha))
            .Select(h => new { h.Kind, h.Reference, h.Amount }).ToListAsync(ct);
        var activos = inversiones.Select(h => new ActivoLiquido($"{h.Kind} {h.Reference}", h.Amount, descuentos.GetValueOrDefault(h.Kind.ToString(),
            descuentos.GetValueOrDefault(nameof(LiquidityHoldingKind.Other))))).ToList();
        var delaContabilidad = await ActivosDeContabilidadAsync(fecha, ct);
        activos.AddRange(delaContabilidad.Activos);
        supuestos.Add(delaContabilidad.Activos.Count == 0
            ? "Activos líquidos netos: el fondo de liquidez constituido con el descuento de cada clase de inversión; el disponible y las demás inversiones no se leen de "
              + "Contabilidad (parámetro RiesgoDeLiquidez.ActivosLiquidosContables vacío)."
            : "Activos líquidos netos: el fondo de liquidez constituido y, leídos de Contabilidad por los prefijos de la entidad, " + delaContabilidad.Resumen + ".");

        var flujos = new List<FlujoDeLiquidez>();
        var mora = await DecimalAsync(ParametrosDeCartera.RiesgoDeLiquidezIndiceDeMora, fecha, ct) ?? 0m;
        flujos.AddRange((await CuotasPorRecibirAsync(fecha, abierta ? null : horizonte, ct))
            .Select(c => new FlujoDeLiquidez(TipoDeFlujoDeLiquidez.Entrada, "Cuotas de cartera", c.Dias, Math.Round(c.Valor * (1m - mora), 2, MidpointRounding.AwayFromZero))));
        if (mora > 0m) supuestos.Add($"Entradas de cartera descontadas por el índice de mora de {mora:P2}.");

        var fraccionVista = await DecimalAsync(ParametrosDeCartera.RiesgoDeLiquidezFraccionDeRetiroALaVista, fecha, ct) ?? 0m;
        var depositosDelDia = await DepositosAsync(fecha, ct);
        var (fraccionDeVista, explicacionDelRetiro) = await FraccionDeRetiroALaVistaAsync(fecha, fraccionVista,
            depositosDelDia.Where(x => x.Clase == ClaseDeDepositoParaLiquidez.ALaVista).Sum(x => x.Capital + x.InteresCausado), ct);
        fraccionVista = fraccionDeVista;
        var fraccionAportes = await DecimalAsync(ParametrosDeCartera.RiesgoDeLiquidezFraccionMensualDeRetiroDeAportes, fecha, ct) ?? 0m;
        var depositos = depositosDelDia;
        foreach (var d in depositos)
        {
            switch (d.Clase)
            {
                case ClaseDeDepositoParaLiquidez.Cdat:
                case ClaseDeDepositoParaLiquidez.Contractual:
                    var dias = Math.Max(0, d.DiasAlVencimiento ?? 0);
                    if (d.PagosDeIntereses.Count > 0 && dias > 0)
                    {
                        foreach (var (f, interes) in d.PagosDeIntereses)
                            flujos.Add(new FlujoDeLiquidez(TipoDeFlujoDeLiquidez.SalidaContractual, "Intereses de CDAT", f.DayNumber - fecha.DayNumber, interes));
                        flujos.Add(new FlujoDeLiquidez(TipoDeFlujoDeLiquidez.SalidaContractual, "Capital de CDAT", dias, d.Capital));
                    }
                    else
                        flujos.Add(new FlujoDeLiquidez(TipoDeFlujoDeLiquidez.SalidaContractual,
                            d.Clase == ClaseDeDepositoParaLiquidez.Cdat ? "CDAT al vencimiento" : "Contractual al vencimiento", dias, d.Capital + d.InteresCausado));
                    break;
                case ClaseDeDepositoParaLiquidez.ALaVista:
                    var saldo = d.Capital + d.InteresCausado;
                    var volatil = Math.Round(saldo * fraccionVista, 2, MidpointRounding.AwayFromZero);
                    flujos.Add(new FlujoDeLiquidez(TipoDeFlujoDeLiquidez.SalidaNoContractual, "Ahorro a la vista", 1, volatil));
                    if (abierta) flujos.Add(new FlujoDeLiquidez(TipoDeFlujoDeLiquidez.SalidaNoContractual, "Ahorro a la vista (parte estable)", SinPlazo, saldo - volatil));
                    break;
                default:
                    flujos.AddRange(Mensuales("Ahorro permanente", d.Capital + d.InteresCausado, fraccionAportes, fecha, horizonte, abierta));
                    break;
            }
        }
        var aportes = await AportesAsync(fecha, ct);
        flujos.AddRange(Mensuales("Aportes sociales", aportes, fraccionAportes, fecha, horizonte, abierta));
        supuestos.Add(explicacionDelRetiro);
        supuestos.Add($"Salidas no contractuales: {fraccionVista:P2} del ahorro a la vista en la primera banda y {fraccionAportes:P2} al mes del ahorro permanente "
                      + "y de los aportes (parámetros de la entidad, pendientes de validar).");

        var medicion = RiesgoDeLiquidez.Medir(bandas, activos, flujos);
        var umbral = await DecimalAsync(ParametrosDeCartera.RiesgoDeLiquidezUmbralIrl, fecha, ct) ?? 1m;
        var plan = await DecimalAsync(ParametrosDeCartera.RiesgoDeLiquidezUmbralDePlanDeAjuste, fecha, ct) ?? umbral;
        var bajo = medida == MedidaDeRiesgoDeLiquidez.Irl && medicion.IrlMinimo is { } minimo && minimo < umbral;
        return new MedicionDeLiquidezALaFecha(fecha, medida, medicion, activos, umbral, plan, bajo, supuestos);
    }

    /// <summary>
    /// La fracción del ahorro a la vista que se espera retirar en la primera banda (respuesta 100 B del dueño, E250): con el método <c>Automatico</c> y la historia
    /// mínima del libro, el máximo retiro probable al nivel de confianza escalado a 30 días sobre el saldo a la vista; si no, la fracción fija. Devuelve la fracción y
    /// la frase del supuesto para el informe.
    /// </summary>
    public async Task<(decimal Fraccion, string Supuesto)> FraccionDeRetiroALaVistaAsync(DateOnly fecha, decimal fraccionFija, decimal saldoALaVista, CancellationToken ct)
    {
        var metodo = await TextoAsync(ParametrosDeCartera.RiesgoDeLiquidezRetirosALaVistaMetodo, fecha, ct);
        if (!string.Equals(metodo, "Automatico", StringComparison.Ordinal))
            return (fraccionFija, $"Retiro esperado del ahorro a la vista: fracción fija de {fraccionFija:P2} (método PorcentajeFijo).");
        var diasDeHistoria = (int)(await DecimalAsync(ParametrosDeCartera.RiesgoDeLiquidezDiasDeHistoriaDelRetiro, fecha, ct) ?? 365m);
        var confianza = await DecimalAsync(ParametrosDeCartera.RiesgoDeLiquidezConfianzaDelRetiro, fecha, ct) ?? 0m;
        if (confianza is <= 0m or >= 1m)
            return (fraccionFija, $"Retiro esperado del ahorro a la vista: fracción fija de {fraccionFija:P2}; el nivel de confianza del parámetro no es válido (va entre 0 y 1, sin incluirlos).");
        var desde = fecha.AddDays(-diasDeHistoria);
        var primero = await db.MemberAccountTransactions.AsNoTracking()
            .Where(t => !t.IsDeleted && t.Concept == MemberAccountConcept.Principal && db.MemberAccounts.Any(a => a.Id == t.MemberAccountId && a.DepositClass == DepositClass.Demand))
            .MinAsync(t => (DateOnly?)t.ValueDate, ct);
        if (primero is not { } inicio || inicio > desde)
            return (fraccionFija, $"Retiro esperado del ahorro a la vista: fracción fija de {fraccionFija:P2}; el libro aún no tiene {diasDeHistoria} días de historia para estimarlo con estadística.");

        var movimientos = await db.MemberAccountTransactions.AsNoTracking()
            .Where(t => !t.IsDeleted && t.Concept == MemberAccountConcept.Principal && t.ValueDate > desde && t.ValueDate <= fecha
                        && (t.Kind == MemberAccountTransactionKind.Deposit || t.Kind == MemberAccountTransactionKind.Withdrawal
                            || t.Kind == MemberAccountTransactionKind.TransferIn || t.Kind == MemberAccountTransactionKind.TransferOut
                            || t.Kind == MemberAccountTransactionKind.Refund)
                        && db.MemberAccounts.Any(a => a.Id == t.MemberAccountId && a.DepositClass == DepositClass.Demand))
            .GroupBy(t => t.ValueDate).Select(g => new { Dia = g.Key, Neto = g.Sum(x => x.Amount) }).ToListAsync(ct);
        var porDia = movimientos.ToDictionary(m => m.Dia, m => -m.Neto);
        var salidas = Enumerable.Range(1, diasDeHistoria).Select(i => porDia.GetValueOrDefault(desde.AddDays(i))).ToList();
        var esperado = RetiroEsperadoDeLaVista.Calcular(salidas, confianza, 30);
        var fraccion = RetiroEsperadoDeLaVista.Fraccion(esperado, saldoALaVista);
        return (fraccion, $"Retiro esperado del ahorro a la vista: {esperado:N0} (máximo retiro probable al {confianza:P0} de los últimos {diasDeHistoria} días, por la raíz de 30), "
                          + $"o {fraccion:P2} del saldo a la vista.");
    }

    private static IEnumerable<FlujoDeLiquidez> Mensuales(string concepto, decimal saldo, decimal fraccion, DateOnly fecha, int horizonte, bool abierta)
    {
        if (saldo <= 0m) yield break;
        var cuota = Math.Round(saldo * fraccion, 2, MidpointRounding.AwayFromZero);
        var usado = 0m;
        for (var m = 1; cuota > 0m && fecha.AddMonths(m).DayNumber - fecha.DayNumber <= horizonte && usado + cuota <= saldo; m++)
        {
            usado += cuota;
            yield return new FlujoDeLiquidez(TipoDeFlujoDeLiquidez.SalidaNoContractual, concepto, fecha.AddMonths(m).DayNumber - fecha.DayNumber, cuota);
        }
        if (abierta && saldo - usado > 0m)
            yield return new FlujoDeLiquidez(TipoDeFlujoDeLiquidez.SalidaNoContractual, $"{concepto} (parte estable)", SinPlazo, saldo - usado);
    }

    /// <summary>El saldo de aportes de todas las cuentas de aportes a la fecha (por el libro).</summary>
    private async Task<decimal> AportesAsync(DateOnly fecha, CancellationToken ct)
    {
        var conceptos = MemberAccountTransaction.ConceptosDeAportes.ToArray();
        return await (from t in db.MemberAccountTransactions.AsNoTracking()
                      join a in db.MemberAccounts.AsNoTracking() on t.MemberAccountId equals a.Id
                      where a.Nature == LineNature.Contribution && !t.IsDeleted && t.ValueDate <= fecha && conceptos.Contains(t.Concept)
                      select t.Amount).SumAsync(ct);
    }

    /// <summary>
    /// Las cuotas de cartera por vencer después de la fecha (hasta el horizonte, si lo hay) de los créditos vivos: de cada número de cuota, la de la versión
    /// de plan más reciente vigente a la fecha. Es el flujo contractual: no descuenta lo que ya se abonó a una cuota futura.
    /// </summary>
    private async Task<IReadOnlyList<(int Dias, decimal Valor)>> CuotasPorRecibirAsync(DateOnly fecha, int? horizonte, CancellationToken ct)
    {
        var hasta = horizonte is { } h ? fecha.AddDays(h) : DateOnly.MaxValue;
        var filas = await (from i in db.RepaymentInstallments.AsNoTracking()
                           join v in db.RepaymentScheduleVersions.AsNoTracking() on i.ScheduleVersionId equals v.Id
                           join l in db.Loans.AsNoTracking() on v.LoanId equals l.Id
                           where l.Status == LoanStatus.Active && v.EffectiveValueDate <= fecha && i.DueDate > fecha && i.DueDate <= hasta
                           select new { v.LoanId, i.InstallmentNumber, v.VersionNumber, i.DueDate, Valor = i.Principal + i.Interest }).ToListAsync(ct);
        return filas.GroupBy(f => (f.LoanId, f.InstallmentNumber)).Select(g => g.OrderByDescending(x => x.VersionNumber).First())
            .Select(f => (f.DueDate.DayNumber - fecha.DayNumber, f.Valor)).ToList();
    }

    // --------------------------------------------------------------------------------------- depósitos asegurados --

    /// <summary>
    /// Los depósitos asegurados a la fecha (FR-110): sólo si el perfil vigente dice que la entidad está inscrita en el seguro de depósitos; por ahorrador (el
    /// titular de cada cuenta de captación, nunca los aportes), hasta el tope vigente <c>TOPE_SEGURO_DEPOSITOS</c>. Sin tope registrado, la lista sale sin
    /// asegurar nada.
    /// </summary>
    public async Task<AseguradosALaFecha> AseguradosAsync(DateOnly fecha, CancellationToken ct)
    {
        var vigente = await perfil.VigenteAsync(fecha, null, ct);
        if (vigente is not { DepositInsuranceEnrolled: true }) return new AseguradosALaFecha(fecha, false, null, null, [], []);
        var tope = await valores.LeerAsync(CodigosDeValorNacional.TopeSeguroDepositos, string.Empty, fecha, null, ct);
        var depositos = await DepositosAsync(fecha, ct);
        // Respuesta 101 C del dueño (E250): el saldo de una cuenta conjunta se reparte entre sus titulares (por defecto en partes iguales) antes de aplicar el tope.
        var modo = await TextoAsync(ParametrosDeCartera.SeguroDeDepositosRepartoDeCotitulares, fecha, ct);
        var porAhorrador = (await RepartirEntreTitularesAsync(depositos, modo, fecha, ct)).OrderBy(a => a.PersonaId).ToList();
        var valorDelTope = tope is { IsSuccess: true, Value.Valor: { } t } ? t : (decimal?)null;
        return new AseguradosALaFecha(fecha, true, valorDelTope, tope.IsSuccess ? tope.Value.Fuente : null,
            DepositosAsegurados.Calcular(valorDelTope ?? 0m, porAhorrador), depositos);
    }

    /// <summary>
    /// El saldo de cada cuenta repartido entre su titular y sus cotitulares vigentes a <paramref name="fecha"/> según <paramref name="modo"/> (la suma de las partes
    /// de una cuenta es exactamente su saldo, el titular absorbe el residuo) y sumado por persona; las cuentas de cada una son las suyas, propias o conjuntas.
    /// </summary>
    public async Task<IReadOnlyList<DepositosDeUnAhorrador>> RepartirEntreTitularesAsync(IReadOnlyList<DepositoALaFecha> depositos, string modo, DateOnly fecha,
        CancellationToken ct)
    {
        var cuentas = depositos.Select(d => d.CuentaId).Distinct().ToList();
        var cotitulares = modo == "AlTitular" || cuentas.Count == 0
            ? new Dictionary<int, List<(int Persona, decimal? Share)>>()
            : (await db.MemberAccountParties.AsNoTracking()
                    .Where(x => cuentas.Contains(x.MemberAccountId) && !x.IsDeleted && x.Role == MemberAccountPartyRole.CoHolder && x.ValidFrom <= fecha
                                && (x.ValidTo == null || x.ValidTo >= fecha))
                    .OrderBy(x => x.Id).Select(x => new { x.MemberAccountId, x.PersonId, x.Share }).ToListAsync(ct))
                .GroupBy(x => x.MemberAccountId).ToDictionary(g => g.Key, g => g.Select(x => (x.PersonId, modo == "ProporcionRegistrada" ? x.Share : null)).ToList());
        var porPersona = new Dictionary<int, (HashSet<int> Cuentas, decimal Total)>();
        foreach (var d in depositos)
        {
            var saldo = d.Capital + d.InteresCausado;
            var partes = ProporcionesDeTitularidad.Repartir(saldo,
                ProporcionesDeTitularidad.Calcular(d.PersonaId, cotitulares.GetValueOrDefault(d.CuentaId) ?? []));
            foreach (var (persona, valor) in partes)
            {
                (HashSet<int> Cuentas, decimal Total) actual = porPersona.TryGetValue(persona, out var x) ? x : (new HashSet<int>(), 0m);
                actual.Cuentas.Add(d.CuentaId);
                porPersona[persona] = (actual.Cuentas, actual.Total + valor);
            }
        }
        return porPersona.Select(p => new DepositosDeUnAhorrador(p.Key, p.Value.Cuentas.Count, p.Value.Total)).ToList();
    }

    // ------------------------------------------------------------------------------------------------- lectura --

    /// <summary>El texto de un parámetro a la fecha; sin lectura (entrega sin desplegar, sin fila), el defecto del catálogo.</summary>
    public async Task<string> TextoAsync(string clave, DateOnly fecha, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(clave, fecha, ct: ct);
        return leido.IsSuccess ? leido.Value.Texto : ParametrosDeCartera.Definiciones.First(d => d.Clave == clave).DefectoSeguro;
    }

    /// <summary>Un parámetro decimal a la fecha (con sus límites legales); vacío, nulo.</summary>
    public async Task<decimal?> DecimalAsync(string clave, DateOnly fecha, CancellationToken ct)
    {
        var texto = await TextoAsync(clave, fecha, ct);
        return decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var valor) ? valor : null;
    }

    private async Task<decimal?> LimiteAsync(string codigo, DateOnly fecha, CancellationToken ct)
    {
        var leido = await valores.LeerAsync(CodigosDeValorNacional.Limite, codigo, fecha, null, ct);
        return leido.IsSuccess ? leido.Value.Valor : null;
    }

    /// <summary>Los topes de las bandas del parámetro (JSON de enteros y, en la brecha, un <c>null</c> final para la banda abierta).</summary>
    public static IReadOnlyList<int?> Bandas(string texto)
    {
        try
        {
            var bandas = JsonSerializer.Deserialize<List<int?>>(texto) ?? [];
            return bandas.Count == 0 ? [null] : bandas;
        }
        catch (JsonException)
        {
            return [null];
        }
    }

    private static Dictionary<string, decimal> Descuentos(string texto)
    {
        try
        {
            return new Dictionary<string, decimal>(JsonSerializer.Deserialize<Dictionary<string, decimal>>(texto) ?? [], StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
