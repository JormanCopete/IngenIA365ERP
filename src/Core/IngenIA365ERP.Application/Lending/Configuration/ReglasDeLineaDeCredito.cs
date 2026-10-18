using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Housing;
using IngenIA365ERP.Application.Saas.NationalValues;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Configuration;
using IngenIA365ERP.Domain.Lending.Housing;
using IngenIA365ERP.Domain.Lending.Rates;
using IngenIA365ERP.Domain.Lending.Schedules;

namespace IngenIA365ERP.Application.Lending.Configuration;

/// <summary>Un control de usura de la línea: a qué fecha, sobre qué campo (la tasa de la versión, una banda) y qué dijo el motor. (nuevo)</summary>
public sealed record UsuraVerificada(DateOnly Fecha, string Campo, ResultadoDeUsura Resultado);

/// <summary>
/// Lo que la línea pasó (feature 013, US1): la combinación del catálogo vigente y cada control de usura (por fecha y campo). La menor
/// holgura es la que muestra la lista de líneas. (nuevo)
/// </summary>
public sealed record LineaVerificada(CombinacionDeModalidad Combinacion, IReadOnlyList<UsuraVerificada> Controles)
{
    public decimal? MenorHolgura => Controles.Count == 0 ? null : Controles.Min(c => c.Resultado.Holgura);
}

/// <summary>
/// El plan tipo con que se controla la usura de una línea (feature 013, US1, T28, R36; data-model §1.3): la versión y <b>cada banda</b>
/// se proyectan con un plan tipo por banda de monto y plazo —el monto máximo del rango y los plazos mínimo y máximo, en cada periodicidad
/// admitida, con el sistema por defecto (o el primero que no sea pago único) y los seguros de la línea por su base y periodicidad— y
/// <see cref="ControlDeUsura"/> compara la efectiva con los cargos que computan contra la usura certificada. Puro: sin IO ni reloj; la
/// usura, el índice y la fecha llegan por parámetro. Lo usan <see cref="ReglasDeLineaDeCredito"/> y el efecto de la réplica. (nuevo)
/// </summary>
public static class PlanTipoDeLinea
{
    /// <summary>El campo de la tasa de la versión (o de su margen si es variable).</summary>
    public static string CampoDeLaVersion(BorradorDeVersionDeLinea b) => b.RateKind == RateKind.Variable ? "margin" : "annualRate";

    /// <summary>El campo de la tasa de una banda.</summary>
    public static string CampoDeLaBanda(BorradorDeVersionDeLinea b, BorradorDeBanda banda) =>
        $"rateBands[{banda.Order.ToString(CultureInfo.InvariantCulture)}].{(b.RateKind == RateKind.Variable ? "margin" : "annualRate")}";

    /// <summary>La efectiva anual de la versión o de una banda: la fija, o la variable con su margen, piso y techo (D10).</summary>
    public static decimal TasaEfectiva(BorradorDeVersionDeLinea b, BorradorDeBanda? banda, IndiceDeReferencia? indice)
    {
        if (b.RateKind == RateKind.Fixed) return banda?.AnnualRate ?? b.AnnualRate ?? 0m;
        if (indice is null) throw new ArgumentNullException(nameof(indice), "La tasa variable necesita el índice.");
        return TasaDelDia.TasaVariable(indice, banda?.Margin ?? b.Margin ?? 0m, b.RateFloor, b.RateCeiling);
    }

    /// <summary>Los períodos de un plazo en meses en la periodicidad: meses × 30 / días del período, por arriba, al menos uno.</summary>
    public static int Periodos(int meses, RepaymentPeriodicity periodicidad)
    {
        if (periodicidad == RepaymentPeriodicity.SinglePayment) return 1;
        var dias = meses * (int)RepaymentPeriodicity.Monthly;
        return Math.Max(1, (dias + (int)periodicidad - 1) / (int)periodicidad);
    }

    /// <summary>Los planes tipo de la versión (sin banda) y de cada banda, con el campo al que se atribuye cada uno.</summary>
    public static IReadOnlyList<(string Campo, EntradaDePlan Plan)> Planes(BorradorDeVersionDeLinea b, IndiceDeReferencia? indice, DateOnly fecha)
    {
        var planes = new List<(string, EntradaDePlan)>();
        planes.AddRange(PlanesDeUnRango(b, null, indice, fecha).Select(p => (CampoDeLaVersion(b), p)));
        foreach (var banda in b.RateBands.OrderBy(x => x.Order))
            planes.AddRange(PlanesDeUnRango(b, banda, indice, fecha).Select(p => (CampoDeLaBanda(b, banda), p)));
        return planes;
    }

    /// <summary>
    /// El plan de una consulta (api.md §3.1 <c>usury-check</c>): el monto, el número de cuotas y la periodicidad pedidos (o el máximo, el
    /// plazo máximo y la periodicidad por defecto), con la banda que casa por monto y plazo, si alguna.
    /// </summary>
    public static (string Campo, EntradaDePlan Plan, decimal Tasa) PlanDeConsulta(BorradorDeVersionDeLinea b, IndiceDeReferencia? indice, DateOnly fecha,
        decimal? monto, int? cuotas, RepaymentPeriodicity? periodicidad)
    {
        var p = periodicidad ?? b.DefaultPeriodicity;
        var m = monto ?? b.MaxAmount;
        var n = cuotas ?? Periodos(b.MaxTermMonths, p);
        var meses = p == RepaymentPeriodicity.SinglePayment ? b.MaxTermMonths : Math.Max(1, n * (int)p / (int)RepaymentPeriodicity.Monthly);
        var banda = b.RateBands.OrderBy(x => x.Order).FirstOrDefault(x => CreditLineRateBand.Nueva(1, x).Casa(m, meses, null, b.ForEmployees, null));
        var tasa = TasaEfectiva(b, banda, indice);
        var campo = banda is null ? CampoDeLaVersion(b) : CampoDeLaBanda(b, banda);
        return (campo, Entrada(b, m, n, meses, p, tasa, fecha), tasa);
    }

    /// <summary>
    /// El control de usura de la versión entera a una fecha: cada plan tipo de la versión y de cada banda contra la usura dada. Devuelve el
    /// peor resultado de cada campo (el de menor holgura).
    /// </summary>
    public static IReadOnlyList<UsuraVerificada> Controlar(BorradorDeVersionDeLinea b, IndiceDeReferencia? indice, UsuraCertificada usura, DateOnly fecha)
    {
        ArgumentNullException.ThrowIfNull(usura);
        var cargos = (IReadOnlyList<CargoDeUsura>)[];
        return Planes(b, indice, fecha)
            .Select(p => (p.Campo, Resultado: MotorDeCartera.ControlarUsura(MotorDeCartera.GenerarPlan(p.Plan), usura, cargos)))
            .GroupBy(x => x.Campo, StringComparer.Ordinal)
            .Select(g => new UsuraVerificada(fecha, g.Key, g.OrderBy(x => x.Resultado.Holgura ?? 0m).First().Resultado))
            .ToList();
    }

    private static IEnumerable<EntradaDePlan> PlanesDeUnRango(BorradorDeVersionDeLinea b, BorradorDeBanda? banda, IndiceDeReferencia? indice, DateOnly fecha)
    {
        var montoMaximo = Math.Min(banda?.MaxAmount ?? b.MaxAmount, b.MaxAmount);
        var montoMinimo = Math.Max(banda?.MinAmount ?? b.MinAmount, b.MinAmount);
        if (montoMaximo < montoMinimo || montoMaximo <= 0m) yield break;
        var mesesMinimo = Math.Max(banda?.MinTermMonths ?? b.MinTermMonths, b.MinTermMonths);
        var mesesMaximo = Math.Min(banda?.MaxTermMonths ?? b.MaxTermMonths, b.MaxTermMonths);
        if (mesesMaximo < mesesMinimo) yield break;

        var tasa = TasaEfectiva(b, banda, indice);
        var monto = decimal.Round(montoMaximo, 2);
        foreach (var periodicidad in b.Periodicities.Distinct())
        {
            if (Sistema(b, periodicidad) is null) continue;
            foreach (var meses in new[] { mesesMinimo, mesesMaximo }.Distinct())
                yield return Entrada(b, monto, Periodos(meses, periodicidad), meses, periodicidad, tasa, fecha);
        }
    }

    private static AmortizationSystem? Sistema(BorradorDeVersionDeLinea b, RepaymentPeriodicity periodicidad)
    {
        if (periodicidad == RepaymentPeriodicity.SinglePayment)
            return b.AmortizationSystems.Contains(AmortizationSystem.Bullet) ? AmortizationSystem.Bullet : null;
        if (b.DefaultAmortizationSystem != AmortizationSystem.Bullet) return b.DefaultAmortizationSystem;
        return b.AmortizationSystems.Where(s => s != AmortizationSystem.Bullet).Select(s => (AmortizationSystem?)s).FirstOrDefault();
    }

    private static EntradaDePlan Entrada(BorradorDeVersionDeLinea b, decimal monto, int periodos, int meses, RepaymentPeriodicity periodicidad,
        decimal tasa, DateOnly fecha)
    {
        var sistema = Sistema(b, periodicidad) ?? b.DefaultAmortizationSystem;
        var unico = periodicidad == RepaymentPeriodicity.SinglePayment;
        return new EntradaDePlan(monto, fecha, unico ? 1 : periodos, periodicidad, unico ? AmortizationSystem.Bullet : sistema, tasa, b.DayCount,
            b.RateEquivalence, new RedondeoDeCartera(b.RoundingUnit, b.RoundingMode, b.RoundingMoment))
        {
            PrimerVencimiento = unico ? fecha.AddMonths(Math.Max(1, meses)) : null,
            Seguros = b.Insurances.Select(s => new SeguroDelPlan(s.Code, s.Base, s.Rate, s.ChargePeriodicity,
                CreditLineInsurance.ComputaParaUsura(s.RetainedFraction, s.SelfInsured))).ToList(),
        };
    }
}

/// <summary>
/// El único sitio de las reglas de una versión de línea de crédito que comparten el alta, la versión nueva, el puntaje, la reapertura y
/// la importación (feature 013, US1, T0352; api.md §3.1; data-model §1.2–§1.7; T28, T44, D18, D20, FR-018–FR-021):
///
/// <list type="number">
/// <item>la forma de la versión con sus hijas (<see cref="CreditLineVersion.Validar"/>: retroactiva, precisión, D20, bandas, mínimos,
/// listas);</item>
/// <item>la modalidad y el sub-segmento contra el <c>CatalogoDeModalidades</c> vigente en <c>ValidFrom</c> (<c>ModalityUnknown</c>; sin
/// documento, <c>NationalValues.Missing</c>); vivienda o UVR, hasta K6b, <c>HousingNotAvailable</c> (D18); desde K6b (T1055), las reglas de la
/// Ley 546 de 1999 (<see cref="ReglasDeVivienda.ValidarLinea"/>: tasa fija, tope de vivienda con su fuente, plazo entre los límites, UVR sólo si el
/// catálogo la admite), y la usura del plan tipo se controla contra el tope de vivienda;</item>
/// <item>los no asociados contra el perfil regulatorio vigente (<c>NonAssociatesNotAllowed</c>);</item>
/// <item>la usura: la versión y cada banda pasan <see cref="ControlDeUsura"/> sobre el plan tipo (<see cref="PlanTipoDeLinea"/>) con la
/// usura certificada que el catálogo asigna a la combinación, a <c>ValidFrom</c> y a hoy (<c>Lending.Usury.Exceeded</c> con la usura,
/// la modalidad y la diferencia; sin usura o sin índice del día, <c>NationalValues.Missing</c>, que el lector alerta). La mora fija
/// tampoco pasa la usura del día.</item>
/// </list>
/// No escribe nada. (nuevo)
/// </summary>
public sealed class ReglasDeLineaDeCredito(INormativaVigente normativa, IValoresNacionales valores, IPerfilRegulatorio perfil,
    EntregaDeCartera? entrega = null)
{
    /// <summary>
    /// Verifica una versión completa. <paramref name="hoy"/> es la fecha local; <paramref name="admitirDesde"/> (sólo la plantilla, en
    /// una línea sin solicitudes ni créditos) admite una versión que empieza antes de hoy (plantillas.md §1).
    /// </summary>
    public async Task<Result<LineaVerificada>> VerificarAsync(BorradorDeVersionDeLinea b, DateOnly hoy, CancellationToken ct, bool admitirDesde = false)
    {
        ArgumentNullException.ThrowIfNull(b);
        var referencia = admitirDesde && b.ValidFrom < hoy ? b.ValidFrom : hoy;
        if (CreditLineVersion.Validar(b, referencia) is { } forma) return Result.Failure<LineaVerificada>(ErroresDeLineasDeCredito.De(forma));

        var combinacion = await CombinacionAsync(b.ModalityCode, b.SubsegmentCode, b.ValidFrom, ct);
        if (combinacion.IsFailure) return Result.Failure<LineaVerificada>(combinacion.Error);

        // K6b (T1055): las reglas de vivienda de la Ley 546 a ValidFrom y a hoy; una línea en UVR sólo donde el catálogo la admite y en un solo tramo.
        if (EsVivienda(b.ModalityCode.Trim().ToUpperInvariant(), combinacion.Value))
        {
            foreach (var fecha in new[] { b.ValidFrom, hoy }.Where(f => f >= referencia).Distinct().OrderBy(f => f))
            {
                var vivienda = await ViviendaAsync(b, combinacion.Value, fecha, ct);
                if (vivienda.IsFailure) return Result.Failure<LineaVerificada>(vivienda.Error);
            }
        }
        else if (b.DenominationUnit == DenominationUnit.Uvr && !combinacion.Value.AdmitsUvr)
        {
            return Result.Failure<LineaVerificada>(LectorDeVivienda.Error(ProblemaDeCartera.Con(ReglasDeVivienda.CodigoUvrNoAdmitida,
                "La combinación de modalidad y sub-segmento no admite UVR en el catálogo de modalidades vigente: la línea va en pesos.", "field",
                "denominationUnit")));
        }
        if (b.DenominationUnit == DenominationUnit.Uvr && b.AllowsTranches)
            return Result.Failure<LineaVerificada>(ErroresDeLineasDeCredito.Invalido("allowsTranches",
                "Una línea en UVR se desembolsa en un solo tramo: desmarque «admite tramos» o pacte la línea en pesos."));

        if (b.AllowsNonAssociates)
        {
            var vigente = await perfil.VigenteAsync(b.ValidFrom, null, ct);
            if (!PermiteNoAsociados(vigente))
                return Result.Failure<LineaVerificada>(ErroresDeLineasDeCredito.NoAsociadosNoPermitidos(vigente?.OrganizationTypeCode));
        }

        var controles = new List<UsuraVerificada>();
        foreach (var fecha in new[] { b.ValidFrom, hoy }.Where(f => f >= referencia).Distinct().OrderBy(f => f))
        {
            var control = await ControlarAsync(b, combinacion.Value, fecha, ct);
            if (control.IsFailure) return Result.Failure<LineaVerificada>(control.Error);
            controles.AddRange(control.Value);
        }
        return Result.Success(new LineaVerificada(combinacion.Value, controles));
    }

    /// <summary>
    /// Sólo la usura de una versión ya registrada a una fecha (la reapertura revalida la del día): la combinación del catálogo vigente a
    /// esa fecha y cada plan tipo. Una modalidad que salió del catálogo responde <c>ModalityUnknown</c>.
    /// </summary>
    public async Task<Result<LineaVerificada>> ControlarUsuraAsync(BorradorDeVersionDeLinea b, DateOnly fecha, CancellationToken ct)
    {
        var combinacion = await CombinacionAsync(b.ModalityCode, b.SubsegmentCode, fecha, ct);
        if (combinacion.IsFailure) return Result.Failure<LineaVerificada>(combinacion.Error);
        var control = await ControlarAsync(b, combinacion.Value, fecha, ct);
        return control.IsFailure
            ? Result.Failure<LineaVerificada>(control.Error)
            : Result.Success(new LineaVerificada(combinacion.Value, control.Value));
    }

    /// <summary>
    /// La consulta de usura de una versión (api.md §3.1 <c>usury-check</c>): un plan con el monto, las cuotas y la periodicidad pedidos.
    /// No escribe. Sin usura del día, <c>NationalValues.Missing</c>.
    /// </summary>
    public async Task<Result<(UsuraVerificada Control, decimal Tasa, CombinacionDeModalidad Combinacion)>> ConsultarAsync(BorradorDeVersionDeLinea b,
        DateOnly fecha, decimal? monto, int? cuotas, RepaymentPeriodicity? periodicidad, CancellationToken ct)
    {
        var combinacion = await CombinacionAsync(b.ModalityCode, b.SubsegmentCode, fecha, ct);
        if (combinacion.IsFailure) return Result.Failure<(UsuraVerificada, decimal, CombinacionDeModalidad)>(combinacion.Error);
        var usura = await UsuraAsync(combinacion.Value, fecha, ct);
        if (usura.IsFailure) return Result.Failure<(UsuraVerificada, decimal, CombinacionDeModalidad)>(usura.Error);
        var indice = await IndiceAsync(b, fecha, ct);
        if (indice.IsFailure) return Result.Failure<(UsuraVerificada, decimal, CombinacionDeModalidad)>(indice.Error);

        var (campo, plan, tasa) = PlanTipoDeLinea.PlanDeConsulta(b, indice.Value, fecha, monto, cuotas, periodicidad);
        var resultado = MotorDeCartera.ControlarUsura(MotorDeCartera.GenerarPlan(plan), usura.Value);
        return Result.Success((new UsuraVerificada(fecha, campo, resultado), tasa, combinacion.Value));
    }

    /// <summary>
    /// La menor holgura de usura del plan tipo de una versión a la fecha (la lista de líneas): negativa si la supera; nula si falta el
    /// catálogo, la usura o el índice (sin error: la lista no se cae por una línea).
    /// </summary>
    public async Task<decimal?> HolguraAsync(BorradorDeVersionDeLinea b, DateOnly fecha, CancellationToken ct)
    {
        var combinacion = await CombinacionAsync(b.ModalityCode, b.SubsegmentCode, fecha, ct);
        if (combinacion.IsFailure) return null;
        var usura = await UsuraAsync(combinacion.Value, fecha, ct);
        if (usura.IsFailure) return null;
        var indice = await IndiceAsync(b, fecha, ct);
        if (indice.IsFailure) return null;
        var controles = PlanTipoDeLinea.Controlar(b, indice.Value, usura.Value, fecha);
        return controles.Count == 0 ? null : controles.Min(c => c.Resultado.Holgura) is { } h ? ErroresDeLineasDeCredito.Tasa(h) : null;
    }

    /// <summary>
    /// ¿El perfil regulatorio vigente deja prestar a no asociados? (FR-018, falla cerrado.) La actividad financiera de ahorro y crédito
    /// se ejerce sólo con asociados (Ley 454 de 1998, art. 39) y los fondos de empleados sólo prestan a sus asociados: se admite cuando hay
    /// perfil, sin actividad financiera autorizada y sin categoría de fondo de empleados. <b>Pendiente de validar por la contadora.</b>
    /// </summary>
    public static bool PermiteNoAsociados(PerfilRegulatorioDto? perfilVigente) =>
        perfilVigente is { FinancialActivityAuthorized: false, EmployeeFundCategory: null };

    /// <summary>¿La combinación es de vivienda o UVR? (D18: no se admite hasta K6b; desde K6b pasa las reglas de la Ley 546.)</summary>
    public static bool EsVivienda(string modalidad, CombinacionDeModalidad? combinacion) =>
        string.Equals(modalidad, ModalidadDeVivienda, StringComparison.OrdinalIgnoreCase)
        || combinacion is { HousingRateCap: true } or { AdmitsUvr: true } or { UsuryKey: null };

    /// <summary>El código nacional de la modalidad de vivienda.</summary>
    public const string ModalidadDeVivienda = "VIVIENDA";

    /// <summary>El índice de la tasa variable leído como «último publicado» a la fecha; nulo si la línea es de tasa fija.</summary>
    public async Task<Result<IndiceDeReferencia?>> IndiceAsync(BorradorDeVersionDeLinea b, DateOnly fecha, CancellationToken ct)
    {
        if (b.RateKind != RateKind.Variable) return Result.Success<IndiceDeReferencia?>(null);
        var leido = await valores.LeerUltimoPublicadoAsync(b.IndexCode!.Trim().ToUpperInvariant(), b.IndexKey!.Trim(), fecha, null, ct);
        return leido.IsFailure ? Result.Failure<IndiceDeReferencia?>(leido.Error) : Result.Success<IndiceDeReferencia?>(Indice(leido.Value));
    }

    /// <summary>El índice como lo recibe el motor: un valor nominal mes vencido lleva sus doce períodos (D10).</summary>
    public static IndiceDeReferencia Indice(ValorNacionalVigente v) =>
        new(v.Valor ?? 0m, string.Equals(v.Unidad, nameof(UnidadDeValorNacional.NominalMV), StringComparison.Ordinal) ? 12 : null);

    /// <summary>
    /// K6b (T1054, T1055): la UVR publicada <b>para</b> la fecha (la de un día rige ese día); sin ella, <c>NationalValues.Missing</c>. La usan la
    /// simulación y el desembolso de una línea en UVR para pasar el monto a UVR.
    /// </summary>
    public async Task<Result<decimal>> UvrAsync(DateOnly fecha, CancellationToken ct)
    {
        var leida = await valores.LeerAsync(CodigosDeValorNacional.Uvr, string.Empty, fecha, null, ct);
        if (leida.IsFailure) return Result.Failure<decimal>(leida.Error);
        return leida.Value.ValidFrom == fecha && leida.Value.Valor is { } uvr && uvr > 0m
            ? Result.Success(uvr)
            : Result.Failure<decimal>(ErroresDeValoresNacionales.Faltante(CodigosDeValorNacional.Uvr, string.Empty, fecha));
    }

    /// <summary>
    /// K6b (T1055): las reglas de vivienda de una versión a una fecha —los límites de la Ley 546 y el tope de vivienda vigentes, con su fuente— por
    /// <see cref="ReglasDeVivienda.ValidarLinea"/>; el primer problema como error del contrato.
    /// </summary>
    public async Task<Result> ViviendaAsync(BorradorDeVersionDeLinea b, CombinacionDeModalidad combinacion, DateOnly fecha, CancellationToken ct)
    {
        var lector = new LectorDeVivienda(valores);
        var limites = await lector.LimitesAsync(fecha, ct);
        if (limites.IsFailure) return Result.Failure(limites.Error);
        TopeDeTasaDeVivienda? tope = null;
        if (combinacion.HousingRateCap)
        {
            var leido = await lector.TopeAsync(fecha, ct);
            if (leido.IsFailure) return Result.Failure(leido.Error);
            tope = leido.Value;
        }
        decimal? usura = null;
        if (combinacion.UsuryKey is { } clave)
        {
            var leida = await valores.LeerAsync(CodigosDeValorNacional.Usura, clave, fecha, null, ct);
            if (leida.IsFailure) return Result.Failure(leida.Error);
            usura = leida.Value.Valor;
        }
        var tasas = b.RateBands.Select(x => x.AnnualRate).Append(b.AnnualRate).OfType<decimal>().ToList();
        var problemas = ReglasDeVivienda.ValidarLinea(new LineaDeVivienda(b.RateKind, tasas.Count == 0 ? null : tasas.Max(), b.MinTermMonths, b.MaxTermMonths,
            b.DenominationUnit, combinacion.AdmitsUvr), tope, usura, limites.Value);
        return problemas.Count == 0 ? Result.Success() : Result.Failure(LectorDeVivienda.Error(problemas[0]));
    }

    /// <summary>
    /// La usura certificada de la combinación a la fecha (<c>USURA</c> con la clave que asigna el catálogo). K6b (T1055): una combinación con tope de
    /// vivienda controla el plan tipo contra el <c>TOPE_TASA_VIVIENDA</c> vigente (rige el menor si también tuviera usura).
    /// </summary>
    public async Task<Result<UsuraCertificada>> UsuraAsync(CombinacionDeModalidad combinacion, DateOnly fecha, CancellationToken ct)
    {
        if (combinacion.UsuryKey is null && combinacion.HousingRateCap)
        {
            var tope = await new LectorDeVivienda(valores).TopeAsync(fecha, ct);
            return tope.IsFailure
                ? Result.Failure<UsuraCertificada>(tope.Error)
                : Result.Success(new UsuraCertificada(tope.Value.Tasa, tope.Value.PublicId, CodigosDeValorNacional.TopeTasaVivienda, tope.Value.VigenteDesde));
        }
        var leida = await valores.LeerAsync(CodigosDeValorNacional.Usura, combinacion.UsuryKey!, fecha, null, ct);
        return leida.IsFailure
            ? Result.Failure<UsuraCertificada>(leida.Error)
            : Result.Success(new UsuraCertificada(leida.Value.Valor ?? 0m, leida.Value.PublicId, leida.Value.Clave, leida.Value.ValidFrom));
    }

    /// <summary>La combinación modalidad + sub-segmento en el catálogo vigente a la fecha; vivienda se rechaza hasta K6b.</summary>
    public async Task<Result<CombinacionDeModalidad>> CombinacionAsync(string modalidad, string? subsegmento, DateOnly fecha, CancellationToken ct)
    {
        var codigo = modalidad.Trim().ToUpperInvariant();
        var sub = string.IsNullOrWhiteSpace(subsegmento) ? null : subsegmento.Trim();
        var catalogo = await normativa.CatalogoDeModalidadesAsync(fecha, null, ct);
        if (catalogo.IsFailure) return Result.Failure<CombinacionDeModalidad>(catalogo.Error);

        var combinacion = catalogo.Value.Documento.Buscar(codigo, sub);
        var modalidadDelCatalogo = catalogo.Value.Documento.Modalidades.FirstOrDefault(m => m.Code == codigo);
        // D18: hasta K6b la vivienda y la UVR no se admiten; desde K6b (T1055) siguen las reglas de la Ley 546 (ViviendaAsync).
        if (EsVivienda(codigo, combinacion) && !LectorDeVivienda.Disponible(entrega))
            return Result.Failure<CombinacionDeModalidad>(ErroresDeLineasDeCredito.ViviendaNoDisponible(codigo));
        if (combinacion is null) return Result.Failure<CombinacionDeModalidad>(ErroresDeLineasDeCredito.ModalidadDesconocida(codigo, sub, fecha));
        if (modalidadDelCatalogo?.RetiredFrom is { } retirada && retirada <= fecha)
            return Result.Failure<CombinacionDeModalidad>(ErroresDeLineasDeCredito.ModalidadDesconocida(codigo, sub, fecha, retirada: true));
        return Result.Success(combinacion);
    }

    private async Task<Result<IReadOnlyList<UsuraVerificada>>> ControlarAsync(BorradorDeVersionDeLinea b, CombinacionDeModalidad combinacion,
        DateOnly fecha, CancellationToken ct)
    {
        var usura = await UsuraAsync(combinacion, fecha, ct);
        if (usura.IsFailure) return Result.Failure<IReadOnlyList<UsuraVerificada>>(usura.Error);
        var indice = await IndiceAsync(b, fecha, ct);
        if (indice.IsFailure) return Result.Failure<IReadOnlyList<UsuraVerificada>>(indice.Error);

        var controles = PlanTipoDeLinea.Controlar(b, indice.Value, usura.Value, fecha);
        var excedido = controles.Where(c => !c.Resultado.Pasa).OrderBy(c => c.Resultado.Holgura ?? 0m).FirstOrDefault();
        if (excedido is not null)
            return Result.Failure<IReadOnlyList<UsuraVerificada>>(
                ErroresDeLineasDeCredito.UsuraExcedida(excedido.Campo, fecha, excedido.Resultado, combinacion.ModalityCode));
        if (b.DefaultRateKind == DefaultRateKind.Fixed && b.DefaultRate is { } mora && mora > usura.Value.Tasa)
            return Result.Failure<IReadOnlyList<UsuraVerificada>>(ErroresDeLineasDeCredito.MoraSobreLaUsura(
                mora, usura.Value.Tasa, usura.Value.PublicId, usura.Value.Clave, combinacion.ModalityCode, fecha));
        return Result.Success(controles);
    }
}
