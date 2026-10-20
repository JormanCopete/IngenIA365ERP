using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Deposits;

/// <summary>
/// Las operaciones de la Cartera que pueden ser hecho generador del GMF (feature 014, US4, T108; research R07, D32; FR-022, FR-052, FR-053). Son los
/// nombres que lleva el parámetro <c>Gmf.HechosGeneradores</c> en sus listas <c>gravados</c> y <c>exentos</c>: se guardan como texto, así que nunca
/// cambian. (nuevo)
/// </summary>
public static class HechosGeneradoresDeGmf
{
    /// <summary>Retiro de una cuenta de ahorro (por el contrato o por giro).</summary>
    public const string Retiro = "Withdrawal";

    /// <summary>
    /// El débito automático para pagar un crédito (decisión del dueño del 2026-10-18, D54): hecho propio para que cada cooperativa decida si grava; por
    /// defecto gravado como un retiro (pendiente de validar por la contadora). Hasta esa fecha se calculaba siempre como <see cref="Retiro"/>.
    /// </summary>
    public const string DebitoAutomatico = "DirectDebit";

    /// <summary>Traslado a una cuenta de otro titular.</summary>
    public const string TrasladoAOtroTitular = "TransferToOtherHolder";

    /// <summary>Traslado entre una cuenta de ahorro y un CDAT (en cualquier sentido), aunque sea del mismo titular (concepto DIAN 082568 de 2011).</summary>
    public const string TrasladoEntreAhorroYCdat = "TransferBetweenDemandAndTermDeposit";

    /// <summary>La redención de un CDAT abonada en una cuenta (Oficio DIAN 2787 de 2019): base capital más intereses.</summary>
    public const string RedencionDeCdatAbonada = "TermDepositRedemptionToAccount";

    /// <summary>La cancelación de una cuenta con entrega del saldo.</summary>
    public const string Cancelacion = "Cancellation";

    /// <summary>Traslado entre cuentas de ahorro del mismo titular (ET art. 879 num. 14): exento por defecto.</summary>
    public const string TrasladoEntreAhorrosDelMismoTitular = "TransferBetweenSameHolderSavings";

    /// <summary>
    /// La entrega de lo ahorrado a los beneficiarios por el fallecimiento del titular (respuesta 97 del dueño, 2026-10-21, E253): el GMF se cobra como en
    /// cualquier retiro, por defecto, y cada línea puede apagarlo (<c>GmfOnDeceasedAndUnclaimed</c>). Pendiente de validar por la contadora.
    /// </summary>
    public const string EntregaPorFallecimiento = "DeceasedDelivery";

    /// <summary>El pago de un saldo no reclamado al titular (respuesta 97, E253): como la entrega por fallecimiento.</summary>
    public const string PagoDeNoReclamado = "UnclaimedPayment";

    /// <summary>Los seis del defecto de D32, en su orden, el débito automático (D54) y las dos entregas de la respuesta 97 (E253).</summary>
    public static IReadOnlyList<string> Todos { get; } =
    [
        Retiro, TrasladoAOtroTitular, TrasladoEntreAhorroYCdat, RedencionDeCdatAbonada, Cancelacion, TrasladoEntreAhorrosDelMismoTitular, DebitoAutomatico,
        EntregaPorFallecimiento, PagoDeNoReclamado,
    ];

    /// <summary>¿Es una de las dos entregas cuyo GMF cada línea puede apagar (<c>GmfOnDeceasedAndUnclaimed</c>)?</summary>
    public static bool EsEntregaDeLaRespuesta97(string? hecho) => hecho is EntregaPorFallecimiento or PagoDeNoReclamado;

    public static bool Existe(string? hecho) => hecho is not null && Todos.Contains(hecho, StringComparer.Ordinal);

    /// <summary>
    /// El hecho de un traslado entre dos cuentas de captación (FR-052): entre ahorro y CDAT (en cualquier sentido) gravado por su propia regla; si no,
    /// según el titular: el mismo titular es el exento del art. 879 num. 14; otro titular, el gravado.
    /// </summary>
    public static string DeTraslado(DepositClass? origen, DepositClass? destino, bool mismoTitular)
    {
        var conCdat = (origen == DepositClass.TermDeposit) != (destino == DepositClass.TermDeposit) && origen is not null && destino is not null;
        if (conCdat) return TrasladoEntreAhorroYCdat;
        return mismoTitular ? TrasladoEntreAhorrosDelMismoTitular : TrasladoAOtroTitular;
    }
}

/// <summary>
/// La política del GMF que rige en una fecha (research R07; los parámetros <c>Gmf.*</c> con vigencia y la UVT del año): tarifa (fracción; cero = el
/// impuesto no se calcula desde esa vigencia), tope mensual exento en UVT, la UVT, la política de exención y los hechos gravados y exentos. (nuevo)
/// </summary>
public sealed record PoliticaDeGmf(
    decimal Tarifa,
    decimal TopeExencionUvt,
    decimal Uvt,
    GmfExemptionPolicy Politica,
    IReadOnlyCollection<string> Gravados,
    IReadOnlyCollection<string> Exentos)
{
    /// <summary>El tope mensual exento en pesos: <c>TopeExencionUvt × UVT</c>, a dos decimales.</summary>
    public decimal TopeEnPesos => Math.Round(TopeExencionUvt * Uvt, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// ¿Se redondea el GMF al peso? Por defecto sí (<c>Gmf.Redondeo = Peso</c>); con <c>Centavo</c>, a dos decimales. Decisión del dueño del 2026-10-18
    /// (D54): defecto configurable por cooperativa, pendiente de validar por la contadora.
    /// </summary>
    public bool AlPeso { get; init; } = true;

    /// <summary>¿Se calcula el GMF? (con tarifa cero o política «no aplica» no, desde esa vigencia, FR-022).</summary>
    public bool Activo => Tarifa > 0m && Politica != GmfExemptionPolicy.NotApplicable;

    /// <summary>¿Es <paramref name="hecho"/> generador? Gravado y no exento (un hecho que el parámetro no nombra no genera).</summary>
    public bool Grava(string hecho) => Gravados.Contains(hecho, StringComparer.Ordinal) && !Exentos.Contains(hecho, StringComparer.Ordinal);

    /// <summary>
    /// Lee <c>Gmf.HechosGeneradores</c> (<c>{ "gravados": [...], "exentos": [...] }</c>, D32). Un JSON ilegible o con un hecho desconocido es un error
    /// del dato, no un «nada se grava»: devuelve el problema y quien llama no calcula (falla cerrado).
    /// </summary>
    public static (IReadOnlyList<string> Gravados, IReadOnlyList<string> Exentos, string? Problema) LeerHechos(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return ([], [], "Gmf.HechosGeneradores está vacío.");
        try
        {
            using var doc = JsonDocument.Parse(json);
            var gravados = Lista(doc.RootElement, "gravados");
            var exentos = Lista(doc.RootElement, "exentos");
            var desconocido = gravados.Concat(exentos).FirstOrDefault(h => !HechosGeneradoresDeGmf.Existe(h));
            return desconocido is null
                ? (gravados, exentos, null)
                : ([], [], $"«{desconocido}» no es un hecho generador de la Cartera ({string.Join(", ", HechosGeneradoresDeGmf.Todos)}).");
        }
        catch (JsonException ex)
        {
            return ([], [], $"Gmf.HechosGeneradores no es un JSON válido: {ex.Message}");
        }
    }

    private static List<string> Lista(JsonElement raiz, string nombre)
    {
        if (raiz.ValueKind != JsonValueKind.Object) return [];
        foreach (var p in raiz.EnumerateObject())
            if (string.Equals(p.Name, nombre, StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.Array)
                return p.Value.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!.Trim()).ToList();
        return [];
    }
}

/// <summary>Una política con la fecha desde la que rige (para elegir la de una fecha). (nuevo)</summary>
public sealed record VigenciaDeGmf(DateOnly Desde, PoliticaDeGmf Politica);

/// <summary>
/// Una disposición que puede causar GMF (FR-053): el hecho generador, el monto dispuesto, si la cuenta debitada está sujeta (la versión de su línea dice
/// <c>IsGmfSubject</c>), si es la cuenta marcada exenta del titular (política <c>SingleMarkedAccount</c>) y lo exento ya usado en el mes por la persona
/// (Σ de <c>LND_GmfMonthlyUsage</c> leído bajo el cerrojo de la persona). (nuevo)
/// </summary>
public sealed record DisposicionDeGmf(
    string HechoGenerador,
    decimal Monto,
    bool CuentaSujeta,
    bool EsLaCuentaMarcada,
    decimal ExentoUsadoEnElMes);

/// <summary>
/// El GMF de una disposición (FR-022): la base, lo exento usado antes, lo exento que aplica esta vez, la base gravada, la tarifa, el valor (al peso), la
/// política, el hecho, el tope y la explicación en español. <see cref="ExentoUsadoDespues"/> es el nuevo acumulado del mes. (nuevo)
/// </summary>
public sealed record GmfCalculado(
    decimal Base,
    decimal ExentoUsadoAntes,
    decimal ExentoAplicado,
    decimal BaseGravada,
    decimal Tarifa,
    decimal Valor,
    GmfExemptionPolicy Politica,
    string HechoGenerador,
    decimal Tope,
    string Explicacion)
{
    public decimal ExentoUsadoDespues => ExentoUsadoAntes + ExentoAplicado;

    /// <summary>¿Hay algo que escribir? (un valor, o exención usada que cuenta para el tope del mes).</summary>
    public bool DejaHuella => Valor > 0m || ExentoAplicado > 0m;
}

/// <summary>
/// El motor del gravamen a los movimientos financieros (feature 014, US4, T108; research R07, D32; FR-022, FR-036, FR-053, FR-066). Puro: recibe la
/// política vigente (<see cref="PoliticaDeGmf"/>, de los parámetros <c>Gmf.*</c> y la UVT) y la disposición, y devuelve el GMF y el nuevo acumulado
/// exento; no lee la base ni el reloj y no tiene valores legales (tarifa, tope y UVT llegan por parámetro). Es, con <c>RetencionSobreRendimientos</c>,
/// el único que calcula lo que va en las filas <c>GmfCharged</c> (<c>LaRetencionYElGmfSoloLosCalculanSusMotores</c>).
///
/// <list type="number">
/// <item>Con tarifa cero o política <c>NotApplicable</c> desde una fecha, no hay GMF ni uso del tope (lo anterior se conserva).</item>
/// <item>Un hecho que no es generador (exento por el parámetro o que el parámetro no nombra) y una cuenta no sujeta no causan nada.</item>
/// <item>La exención mensual (ET art. 879 num. 1): con <c>SingleMarkedAccount</c> sólo en la cuenta marcada; con <c>PerHolderLimit</c> en toda cuenta de
/// ahorro de la persona, sumando sus disposiciones del mes. Aplica hasta el tope (<c>TopeExencionUvt × UVT</c>) menos lo ya usado.</item>
/// <item>El valor es la base gravada por la tarifa, <b>al peso</b> (mitad hacia arriba): 1.669.100 × 0,004 = 6.676,4 → 6.676; o al centavo si la
/// cooperativa lo fija (<c>Gmf.Redondeo</c>, D54).</item>
/// </list>
/// El reverso no se calcula: espeja las filas y la disposición del mes (<see cref="UsoTrasReverso"/>). (nuevo)
/// </summary>
public static class GravamenMovimientosFinancieros
{
    /// <summary>¿El cálculo cobra GMF? Sólo entonces se escribe la pareja <c>GmfCharged</c> (−capital, +GMF) en el libro.</summary>
    public static bool SeCobra([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] GmfCalculado? gmf) => gmf is { Valor: > 0m };

    /// <summary>La política que rige en <paramref name="fecha"/>: la de mayor «desde» que no la pasa; nula si ninguna rige todavía.</summary>
    public static PoliticaDeGmf? Vigente(IEnumerable<VigenciaDeGmf> vigencias, DateOnly fecha)
    {
        ArgumentNullException.ThrowIfNull(vigencias);
        return vigencias.Where(v => v.Desde <= fecha).OrderByDescending(v => v.Desde).Select(v => v.Politica).FirstOrDefault();
    }

    /// <summary>El GMF de la disposición con la política que rige (ver la clase).</summary>
    public static GmfCalculado Calcular(PoliticaDeGmf politica, DisposicionDeGmf d)
    {
        ArgumentNullException.ThrowIfNull(politica);
        ArgumentNullException.ThrowIfNull(d);
        if (d.Monto < 0m) throw new ArgumentOutOfRangeException(nameof(d), d.Monto, "Lo dispuesto no es negativo.");
        if (d.ExentoUsadoEnElMes < 0m) throw new ArgumentOutOfRangeException(nameof(d), d.ExentoUsadoEnElMes, "Lo exento usado no es negativo.");
        var tope = politica.TopeEnPesos;
        GmfCalculado Nada(string motivo) => new(d.Monto, d.ExentoUsadoEnElMes, 0m, 0m, politica.Tarifa, 0m, politica.Politica, d.HechoGenerador, tope, motivo);

        if (!politica.Activo)
            return Nada(politica.Politica == GmfExemptionPolicy.NotApplicable
                ? "La política de exención es «no aplica»: no hay GMF."
                : "La tarifa del GMF es cero: no hay GMF.");
        if (d.Monto == 0m) return Nada("No hay valor dispuesto.");
        if (!politica.Grava(d.HechoGenerador))
            return Nada($"«{d.HechoGenerador}» no es hecho generador del GMF según Gmf.HechosGeneradores.");
        if (!d.CuentaSujeta) return Nada("La línea de la cuenta no está sujeta al GMF.");

        // La exención mensual: en la cuenta marcada (una por persona) o en toda cuenta del titular (tope por titular).
        var conExencion = politica.Politica switch
        {
            GmfExemptionPolicy.SingleMarkedAccount => d.EsLaCuentaMarcada,
            GmfExemptionPolicy.PerHolderLimit => true,
            _ => false,
        };
        var disponible = conExencion ? Math.Max(0m, tope - d.ExentoUsadoEnElMes) : 0m;
        var exento = Math.Min(d.Monto, disponible);
        var gravada = d.Monto - exento;
        var valor = Math.Round(gravada * politica.Tarifa, politica.AlPeso ? 0 : 2, MidpointRounding.AwayFromZero);

        var texto = conExencion
            ? string.Create(CultureInfo.InvariantCulture,
                $"Tope exento del mes {tope:N2} ({politica.TopeExencionUvt:0.####} UVT de {politica.Uvt:N2}); usado antes {d.ExentoUsadoEnElMes:N2}, exento ahora {exento:N2}; base gravada {gravada:N2} × {politica.Tarifa:0.######} = {(politica.AlPeso ? valor.ToString("N0", CultureInfo.InvariantCulture) : valor.ToString("N2", CultureInfo.InvariantCulture))}.")
            : string.Create(CultureInfo.InvariantCulture,
                $"Sin exención en esta cuenta ({politica.Politica}); base gravada {gravada:N2} × {politica.Tarifa:0.######} = {(politica.AlPeso ? valor.ToString("N0", CultureInfo.InvariantCulture) : valor.ToString("N2", CultureInfo.InvariantCulture))}.");
        return new GmfCalculado(d.Monto, d.ExentoUsadoEnElMes, exento, gravada, politica.Tarifa, valor, politica.Politica, d.HechoGenerador, tope, texto);
    }

    /// <summary>
    /// Lo exento usado en el mes después de reversar una disposición: el reverso devuelve al tope lo que la original usó (FR-002), sin bajar de cero.
    /// </summary>
    public static decimal UsoTrasReverso(decimal usadoEnElMes, decimal exentoDeLaOriginal) => Math.Max(0m, usadoEnElMes - exentoDeLaOriginal);
}
