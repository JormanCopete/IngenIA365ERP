using System.Reflection;
using System.Text.RegularExpressions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Architecture.Tests.Helpers;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013 (DT §2.25, T10, C117; T0037; contracts/mensajes.md §2.4): hay un solo catálogo de mensajes, el agregado
/// <c>CatalogoDeMensajes</c>, que une los tres de <c>Contracts/</c> (Comercial, Cartera y hacia Cartera). En él <c>(Type, Version)</c> es
/// único, cada record aparece una sola vez y se llama <c>{Type}V{Version}</c>, cada tipo declara su módulo de origen y destinos
/// conocidos, los hechos de Cartera a Contabilidad llevan su código corto único, y todo record de mensaje de <c>Contracts/</c> (con su
/// constante <c>Type</c>) está en el catálogo. Ningún módulo declara un catálogo propio fuera de esos tres.
/// </summary>
public class ElCatalogoDeMensajesEsUnico
{
    private const string EspacioDeContratos = "IngenIA365ERP.Application.Common.Integration.Contracts";

    /// <summary>Los únicos archivos que declaran catálogos de mensajes (relativos a la raíz, con barras normales).</summary>
    private static readonly string[] CatalogosPermitidos =
    [
        "src/Core/IngenIA365ERP.Application/Common/Integration/CatalogoDeMensajes.cs",
        "src/Core/IngenIA365ERP.Application/Common/Integration/Contracts/Inventory/CatalogoDeMensajesV1.cs",
        "src/Core/IngenIA365ERP.Application/Common/Integration/Contracts/Lending/CatalogoDeMensajesDeCarteraV1.cs",
        "src/Core/IngenIA365ERP.Application/Common/Integration/Contracts/Lending/CatalogoDeMensajesHaciaCarteraV1.cs",
        // Feature 015, US2 (T0091): los hechos de Recaudo a Contabilidad.
        "src/Core/IngenIA365ERP.Application/Common/Integration/Contracts/Receipts/CatalogoDeMensajesDeRecaudoV1.cs",
        // Feature 015, US10 (T0145): los hechos de Tesorería a Contabilidad.
        "src/Core/IngenIA365ERP.Application/Common/Integration/Contracts/Treasury/CatalogoDeMensajesDeTesoreriaV1.cs",
    ];

    private static readonly (Regex Patron, string Que)[] Prohibidos = [
        (new Regex(@"\bclass\s+CatalogoDeMensajes\w*", RegexOptions.Compiled), "declara un catálogo de mensajes propio"),
    ];

    private static readonly HashSet<string> DestinosConocidos = typeof(IntegrationDestinations)
        .GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()!).ToHashSet();

    [Fact]
    public void Ningun_modulo_declara_un_catalogo_propio()
    {
        var infracciones = ReglaSobreFuente.Infracciones(
            ReglaSobreFuente.ArchivosEn(["Core", "Infrastructure", "Presentation"]), Prohibidos, relativo => CatalogosPermitidos.Contains(relativo));
        ReglaSobreFuente.SinInfracciones(infracciones, "Un módulo declara su propio catálogo de mensajes (T10)");
    }

    [Fact]
    public void Tipo_y_version_y_record_son_unicos_y_cada_tipo_declara_modulo_y_destinos()
    {
        var todos = CatalogoDeMensajes.Todos;
        var problemas = new List<string>();

        problemas.AddRange(todos.GroupBy(t => (t.Type, t.Version)).Where(g => g.Count() > 1).Select(g => $"{g.Key.Type} v{g.Key.Version} repetido"));
        problemas.AddRange(todos.GroupBy(t => t.Record).Where(g => g.Count() > 1).Select(g => $"{g.Key.Name} en {g.Count()} entradas"));
        foreach (var t in todos)
        {
            if (!ModulosDeOrigen.Todos.Contains(t.OriginModule)) problemas.Add($"{t.Type}: módulo «{t.OriginModule}» desconocido");
            if (t.Destinos.Count == 0) problemas.Add($"{t.Type}: sin destino");
            problemas.AddRange(t.Destinos.Where(d => !DestinosConocidos.Contains(d)).Select(d => $"{t.Type}: destino «{d}» desconocido"));
            if (t.Record.Name != $"{t.Type}V{t.Version}") problemas.Add($"{t.Record.Name} no se llama {t.Type}V{t.Version}");
            var constante = t.Record.GetField("Type", BindingFlags.Public | BindingFlags.Static);
            if (constante is not { IsLiteral: true } || (string?)constante.GetRawConstantValue() != t.Type)
                problemas.Add($"{t.Record.Name} no declara la constante Type = \"{t.Type}\"");
            if (!t.Record.Namespace!.StartsWith(EspacioDeContratos, StringComparison.Ordinal))
                problemas.Add($"{t.Record.Name} no vive en Contracts/");
        }

        Assert.True(problemas.Count == 0, "Catálogo de mensajes inconsistente (T10):\n  " + string.Join("\n  ", problemas));
    }

    [Fact]
    public void Todo_record_de_mensaje_de_Contracts_esta_en_el_catalogo()
    {
        var enElCatalogo = CatalogoDeMensajes.Todos.Select(t => t.Record).ToHashSet();
        var huerfanos = typeof(CatalogoDeMensajes).Assembly.GetTypes()
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith(EspacioDeContratos, StringComparison.Ordinal))
            .Where(t => Regex.IsMatch(t.Name, @"V\d+$"))
            .Where(t => t.GetField("Type", BindingFlags.Public | BindingFlags.Static) is { IsLiteral: true })
            .Where(t => !enElCatalogo.Contains(t))
            .Select(t => t.FullName!)
            .ToList();

        Assert.True(huerfanos.Count == 0, "Records de mensaje fuera del catálogo agregado:\n  " + string.Join("\n  ", huerfanos));
    }

    [Fact]
    public void El_catalogo_tiene_las_partes_de_la_013_y_los_hechos_de_Cartera_su_codigo_corto()
    {
        var todos = CatalogoDeMensajes.Todos;
        // Los veinte de la 012 y la v2 de VentaACreditoRegistrada (T0175, T18), que también es de Comercial; feature 014, US4 (T112): +
        // FacturaDeCargoRegistrada, que Comercial le informa a la Cartera.
        Assert.Equal(22, todos.Count(t => t.OriginModule == ModulosDeOrigen.Inventario));
        Assert.Equal(21, todos.Count(t => t.OriginModule == ModulosDeOrigen.Inventario && t.Version == 1));
        // K6b (T1056): + CorreccionMonetariaUvrCausada; decisión del dueño del 2026-10-17: + PagoRedistribuidoPorValorNacional.
        // Feature 014 (T048): + los 33 hechos de captaciones y aportes; T055: + la v2 de OrdenDeGiroEmitida; T098: + RetiroDeAsociadoAprobado;
        // US4 (T112): + ConsignacionReversadaPorCartera, CargoDeCaptacionCausado y CargoDeCaptacionReversado; T091 (D36): + la v2 de
        // DescuentosPorPagaduria (y, de Recaudo, la v2 de ResultadoDeDescuentos).
        Assert.Equal(82, todos.Count(t => t.OriginModule == ModulosDeOrigen.Cartera));
        // Feature 015, US10 y US11 (T0145, T0159): + GmfDePagoCausado (GMP) y PagoDeOrdenGirado (POG).
        // US19 (T0228): + ChequeGiradoEmitido (CHE), ChequeGiradoAnulado (CHA) y RemesaConfirmada (RMC).
        Assert.Equal(6, todos.Count(t => t.OriginModule == ModulosDeOrigen.Tesoreria));
        // Feature 015, US2 (T0091): + los cinco hechos de la caja a Contabilidad (MCR, MRC, DAA, ASR, SLI); US3 (T0116): + ReciboEmitido (RCE); US5 (T0178): +
        // ReciboAnulado (RAN), AplicacionReversadaPorModulo (ARM), SaldoAFavorDePagadorResuelto (SFP) y SaldoAFavorDeCarteraDevuelto (SFC).
        // US6 (T0196): + ReciboDeBancoEmitido (RBE), PartidaPorIdentificarReconocida (PIR), PartidaClasificada (PCL) y PartidaDevueltaAlConsignante (PDC).
        // US7 (T0209): + GiroDePagaduriaRecibido (GPR) y ReciboDePagaduriaEmitido (RPG); US15 (T0218): + ChequeRecibidoDevuelto (CRD); US19 (T0228): + RemesaDespachada (RMD).
        Assert.Equal(21, todos.Count(t => t.OriginModule == ModulosDeOrigen.Recaudo));

        var hechos = todos.Where(t => t.OriginModule == ModulosDeOrigen.Cartera && t.AdmiteDestino(IntegrationDestinations.Accounting)).ToList();
        Assert.Equal(70, hechos.Count);
        Assert.All(hechos, t => Assert.Matches("^[A-Z]{3,4}$", t.CodigoCorto ?? string.Empty));
        Assert.Equal(hechos.Count, hechos.Select(t => t.CodigoCorto).Distinct().Count());
        var hechosDeRecaudo = todos.Where(t => t.OriginModule == ModulosDeOrigen.Recaudo && t.AdmiteDestino(IntegrationDestinations.Accounting)).ToList();
        Assert.Equal(18, hechosDeRecaudo.Count);
        Assert.All(hechosDeRecaudo, t => Assert.Matches("^[A-Z]{3,4}$", t.CodigoCorto ?? string.Empty));
        var hechosDeTesoreria = todos.Where(t => t.OriginModule == ModulosDeOrigen.Tesoreria && t.AdmiteDestino(IntegrationDestinations.Accounting)).ToList();
        Assert.Equal(5, hechosDeTesoreria.Count);
        Assert.All(hechosDeTesoreria, t => Assert.Matches("^[A-Z]{3,4}$", t.CodigoCorto ?? string.Empty));
        var conCodigo = hechos.Concat(hechosDeRecaudo).Concat(hechosDeTesoreria).ToList();
        Assert.Equal(conCodigo.Count, conCodigo.Select(t => t.CodigoCorto).Distinct().Count());
        Assert.All(todos.Except(conCodigo), t => Assert.Null(t.CodigoCorto));
    }
}
