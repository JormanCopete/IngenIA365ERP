using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Deposits;

// Feature 014, US13 (T194, T195; contracts/documentos.md §2.3, §2.4, §4; FR-101, FR-104, FR-105, SC-016): lo puro de los documentos al titular de
// captaciones y aportes —el reparto por la proporción de cada titular, el certificado tributario del año y el de retención de un período, y qué
// documentos deja sin valor el reverso de una operación de cuenta—. Sin base, sin reloj y sin valores legales: todo llega por parámetro. (nuevo)

/// <summary>
/// La proporción de cada titular de una cuenta (data-model §3.2, E47): los cotitulares con la suya y el titular con el resto; si a algún cotitular le falta,
/// todos por partes iguales (Edge Cases de la spec) y el titular absorbe el redondeo a seis decimales. Sin cotitulares, el titular con 1. El titular va
/// primero. Es la misma regla con que la liquidación reparte los acumulados de retención (<c>LiquidacionDeIntereses.RepartoAsync</c>). (nuevo)
/// </summary>
public static class ProporcionesDeTitularidad
{
    public static IReadOnlyList<(int Persona, decimal Proporcion)> Calcular(int titular, IReadOnlyList<(int Persona, decimal? Proporcion)> cotitulares)
    {
        ArgumentNullException.ThrowIfNull(cotitulares);
        if (cotitulares.Count == 0) return [(titular, 1m)];
        var conProporcion = cotitulares.All(c => c.Proporcion is not null);
        var cada = conProporcion ? 0m : decimal.Round(1m / (cotitulares.Count + 1), 6, MidpointRounding.ToEven);
        var lista = cotitulares.Select(c => (c.Persona, conProporcion ? c.Proporcion!.Value : cada)).ToList();
        lista.Insert(0, (titular, 1m - lista.Sum(x => x.Item2)));
        return lista;
    }

    /// <summary>
    /// Reparte <paramref name="total"/> al centavo por las proporciones (la primera es la del titular, que absorbe el residuo): la suma de las partes es
    /// exactamente el total (SC-016).
    /// </summary>
    public static IReadOnlyDictionary<int, decimal> Repartir(decimal total, IReadOnlyList<(int Persona, decimal Proporcion)> proporciones)
    {
        ArgumentNullException.ThrowIfNull(proporciones);
        if (proporciones.Count == 0) throw new ArgumentException("Una cuenta tiene al menos a su titular.", nameof(proporciones));
        var partes = new Dictionary<int, decimal>();
        var resto = total;
        for (var i = proporciones.Count - 1; i >= 1; i--)
        {
            var valor = decimal.Round(total * proporciones[i].Proporcion, 2, MidpointRounding.AwayFromZero);
            partes[proporciones[i].Persona] = partes.GetValueOrDefault(proporciones[i].Persona) + valor;
            resto -= valor;
        }
        partes[proporciones[0].Persona] = partes.GetValueOrDefault(proporciones[0].Persona) + resto;
        return partes;
    }
}

/// <summary>Lo acumulado del año de una persona en una cuenta: rendimientos abonados, componente inflacionario, base y retención. (nuevo)</summary>
public sealed record AcumuladoTributario(int Persona, decimal Rendimientos, decimal ComponenteInflacionario, decimal BaseDeRetencion, decimal Retencion);

/// <summary>
/// Una cuenta tal como la ve el certificado tributario del año: su número y clase, si es de aportes, las proporciones de sus titulares al 31 de
/// diciembre, el saldo del titular a esa fecha (de toda la cuenta), el GMF cobrado en el año (de toda la cuenta; lo paga el titular de la cuenta y se
/// reparte por la proporción), los acumulados de retención del año por persona (ya repartidos al liquidar), el GMF del año que trajo la migración para la
/// persona y, en aportes, la revalorización abonada en el año. (nuevo)
/// </summary>
public sealed record CuentaParaElCertificadoTributario(
    string Numero,
    string Clase,
    bool EsAportes,
    IReadOnlyList<(int Persona, decimal Proporcion)> Proporciones,
    decimal SaldoAl31DeDiciembre,
    decimal GmfDelAño,
    IReadOnlyList<AcumuladoTributario> Acumulados,
    decimal GmfMigradoDeLaPersona = 0m,
    decimal RevalorizacionDelAño = 0m);

/// <summary>Una cuenta certificada para una persona: lo suyo por su proporción. (nuevo)</summary>
public sealed record CuentaCertificadaTributaria(
    string Numero,
    string Clase,
    bool EsAportes,
    decimal Proporcion,
    decimal SaldoAl31DeDiciembre,
    decimal Rendimientos,
    decimal ComponenteInflacionario,
    decimal BaseDeRetencion,
    decimal Retencion,
    decimal Gmf,
    decimal RevalorizacionDelAño);

/// <summary>El certificado tributario de una persona y un año: sus cuentas de captación, sus aportes (si el parámetro los incluye) y los totales. (nuevo)</summary>
public sealed record CertificadoTributarioDeCaptacionesArmado(
    short Año,
    IReadOnlyList<CuentaCertificadaTributaria> Captaciones,
    IReadOnlyList<CuentaCertificadaTributaria> Aportes)
{
    public decimal TotalSaldo => Captaciones.Sum(c => c.SaldoAl31DeDiciembre);
    public decimal TotalRendimientos => Captaciones.Sum(c => c.Rendimientos);
    public decimal TotalComponenteInflacionario => Captaciones.Sum(c => c.ComponenteInflacionario);
    public decimal TotalBaseDeRetencion => Captaciones.Sum(c => c.BaseDeRetencion);
    public decimal TotalRetencion => Captaciones.Sum(c => c.Retencion);
    public decimal TotalGmf => Captaciones.Sum(c => c.Gmf) + Aportes.Sum(c => c.Gmf);
    public decimal TotalAportes => Aportes.Sum(c => c.SaldoAl31DeDiciembre);
    public decimal TotalRevalorizacion => Aportes.Sum(c => c.RevalorizacionDelAño);

    /// <summary>¿No hay nada que certificar? (ninguna cuenta con saldo, rendimiento, retención ni GMF).</summary>
    public bool Vacio => Captaciones.Count == 0 && Aportes.Count == 0;
}

/// <summary>
/// El certificado tributario anual de captaciones de una persona (feature 014, US13, T195; contracts/documentos.md §2.3; FR-104; SC-016). Por cada cuenta
/// en que la persona es titular o cotitular al 31 de diciembre: el saldo a esa fecha y el GMF del año <b>por su proporción</b> (al centavo, el titular
/// absorbe el residuo: la suma de los certificados de todos los titulares es exactamente lo de la cuenta), y los rendimientos, el componente
/// inflacionario, la base y la retención que le tocaron en cada liquidación (los acumulados ya repartidos, migración incluida). Los aportes van aparte y
/// sólo si la cooperativa los incluye (<c>Documentos.CertificadoTributarioIncluyeAportes</c>). Una cuenta sin nada que decir no aparece. Motor puro. (nuevo)
/// </summary>
public static class CertificadoTributarioDeCaptaciones
{
    public static CertificadoTributarioDeCaptacionesArmado Armar(short año, int persona, IReadOnlyList<CuentaParaElCertificadoTributario> cuentas,
        bool incluyeAportes)
    {
        ArgumentNullException.ThrowIfNull(cuentas);
        var captaciones = new List<CuentaCertificadaTributaria>();
        var aportes = new List<CuentaCertificadaTributaria>();
        foreach (var c in cuentas)
        {
            if (c.EsAportes && !incluyeAportes) continue;
            var proporcion = c.Proporciones.Where(p => p.Persona == persona).Sum(p => p.Proporcion);
            var esTitular = c.Proporciones.Any(p => p.Persona == persona);
            var saldo = esTitular ? ProporcionesDeTitularidad.Repartir(c.SaldoAl31DeDiciembre, c.Proporciones).GetValueOrDefault(persona) : 0m;
            var gmf = (esTitular ? ProporcionesDeTitularidad.Repartir(c.GmfDelAño, c.Proporciones).GetValueOrDefault(persona) : 0m) + c.GmfMigradoDeLaPersona;
            var revalorizacion = esTitular ? ProporcionesDeTitularidad.Repartir(c.RevalorizacionDelAño, c.Proporciones).GetValueOrDefault(persona) : 0m;
            var suyos = c.Acumulados.Where(a => a.Persona == persona).ToList();
            var linea = new CuentaCertificadaTributaria(c.Numero, c.Clase, c.EsAportes, proporcion, saldo, suyos.Sum(a => a.Rendimientos),
                suyos.Sum(a => a.ComponenteInflacionario), suyos.Sum(a => a.BaseDeRetencion), suyos.Sum(a => a.Retencion), gmf, revalorizacion);
            if (linea is { SaldoAl31DeDiciembre: 0m, Rendimientos: 0m, Retencion: 0m, Gmf: 0m, ComponenteInflacionario: 0m, RevalorizacionDelAño: 0m }) continue;
            (c.EsAportes ? aportes : captaciones).Add(linea);
        }
        return new CertificadoTributarioDeCaptacionesArmado(año, captaciones.OrderBy(x => x.Numero, StringComparer.Ordinal).ToList(),
            aportes.OrderBy(x => x.Numero, StringComparer.Ordinal).ToList());
    }
}

/// <summary>Una retención practicada a la persona en el período: la clase de captación, la tarifa de la liquidación (si la hay) y sus valores. (nuevo)</summary>
public sealed record RetencionDelPeriodo(string Clase, decimal? Tarifa, decimal Rendimientos, decimal ComponenteInflacionario, decimal BaseDeRetencion,
    decimal Retencion);

/// <summary>Un renglón del certificado de retención: el concepto (rendimientos financieros de una clase) a una tarifa. (nuevo)</summary>
public sealed record RenglonDeRetencion(string Clase, decimal? Tarifa, decimal Rendimientos, decimal ComponenteInflacionario, decimal BaseDeRetencion,
    decimal Retencion);

/// <summary>
/// El certificado de retención en la fuente de un período (feature 014, US13, T195; contracts/documentos.md §2.4; FR-105; ET art. 381, DUR 1625 de 2016
/// art. 1.6.1.13.2.40): agrupa lo retenido a la persona por clase de captación y tarifa —el concepto es «rendimientos financieros»—, con la base sometida a
/// retención, el componente inflacionario descontado y el valor retenido. Motor puro. (nuevo)
/// </summary>
public static class CertificadoDeRetencionDeCaptaciones
{
    public static IReadOnlyList<RenglonDeRetencion> Agrupar(IEnumerable<RetencionDelPeriodo> retenciones)
    {
        ArgumentNullException.ThrowIfNull(retenciones);
        return retenciones
            .GroupBy(r => (r.Clase, r.Tarifa))
            .Select(g => new RenglonDeRetencion(g.Key.Clase, g.Key.Tarifa, g.Sum(r => r.Rendimientos), g.Sum(r => r.ComponenteInflacionario),
                g.Sum(r => r.BaseDeRetencion), g.Sum(r => r.Retencion)))
            .Where(r => r.Rendimientos != 0m || r.Retencion != 0m)
            .OrderBy(r => r.Clase, StringComparer.Ordinal).ThenBy(r => r.Tarifa)
            .ToList();
    }
}

/// <summary>Un documento que cubre una cuenta tal como lo mira la anulación por reverso: su clase, sus fechas y la operación que lo selló (títulos). (nuevo)</summary>
public sealed record DocumentoDeCuentaQueCubre(
    IssuedDocumentKind Clase,
    DateOnly? Corte,
    DateOnly? Desde,
    DateOnly? Hasta,
    short? Año,
    long? OperacionQueLoSello);

/// <summary>La operación de cuenta reversada como la mira la anulación: su Id, su fecha valor, si pagó cuotas y si movió lo tributario. (nuevo)</summary>
public sealed record OperacionDeCuentaReversada(long Id, DateOnly FechaValor, bool PagoCuotas, bool Tributaria);

/// <summary>
/// Qué documento de cuenta deja sin valor el reverso de una operación (feature 014, US13, T194; contracts/documentos.md §4; FR-101). Quien llama ya filtró
/// los documentos que cubren alguna cuenta de la operación, que la vieron (<c>InformationCutoff</c> ≥ su registro) y que no están anulados. Por clase:
/// <list type="bullet">
/// <item>extracto: la operación tiene fecha valor hasta el fin del período (las anteriores cambian el saldo inicial; las del período, los movimientos);</item>
/// <item>certificado de saldo, constancia de aportes, liquidación del retiro: fecha valor hasta su corte;</item>
/// <item>certificado tributario: fecha valor hasta el 31 de diciembre del año (cambia el saldo certificado o lo tributario del año);</item>
/// <item>certificado de retención: una operación tributaria (liquidación, retención, GMF, bonificación) con fecha valor en el período;</item>
/// <item>título del CDAT: la operación que lo selló (la constitución, la prórroga o la renovación);</item>
/// <item>paz y salvo de aportes: una operación que pagó cuotas;</item>
/// <item>la respuesta al juzgado no se anula por reverso (es lo que se le dijo al juzgado ese día).</item>
/// </list>
/// Motor puro. (nuevo)
/// </summary>
public static class VigenciaDeDocumentosDeCuentas
{
    public static bool Anula(DocumentoDeCuentaQueCubre documento, OperacionDeCuentaReversada operacion)
    {
        ArgumentNullException.ThrowIfNull(documento);
        ArgumentNullException.ThrowIfNull(operacion);
        return documento.Clase switch
        {
            IssuedDocumentKind.AccountStatement => documento.Hasta is not { } hasta || operacion.FechaValor <= hasta,
            IssuedDocumentKind.DepositTaxCertificate => documento.Año is not { } año || operacion.FechaValor <= new DateOnly(año, 12, 31),
            IssuedDocumentKind.WithholdingCertificate => operacion.Tributaria
                                                         && (documento.Desde is not { } d || operacion.FechaValor >= d)
                                                         && (documento.Hasta is not { } h || operacion.FechaValor <= h),
            IssuedDocumentKind.TermDepositTitle => documento.OperacionQueLoSello == operacion.Id,
            IssuedDocumentKind.ContributionClearance => operacion.PagoCuotas,
            IssuedDocumentKind.GarnishmentResponse => false,
            _ => documento.Corte is not { } corte || operacion.FechaValor <= corte,
        };
    }
}
