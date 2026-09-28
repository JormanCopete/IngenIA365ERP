using System.Text.RegularExpressions;
using IngenIA365ERP.Application.ElectronicInvoicing;
using IngenIA365ERP.Architecture.Tests.Helpers;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, I4 (T752; decisiones-transversales §2.5): los enums de facturación electrónica que la API entrega como número y que las
/// pantallas mandan por nombre. Shared no referencia Domain, así que la etiqueta y el nombre de cada valor se fijan aquí contra los enums
/// reales, en el molde de <see cref="TextosDeVentasTests"/>: un valor nuevo sin su texto rompe aquí, no en la pantalla («Estado 8»), y un
/// nombre mal escrito rompe aquí, no en un 400 del servidor. (nuevo)
/// </summary>
public class TextosDeFacturacionElectronicaTests
{
    private static readonly string Fuente = File.ReadAllText(Path.Combine(RepoPath.FindRepoRoot(),
        "src", "Presentation", "IngenIA365ERP.Shared", "Services", "FacturacionElectronica", "TextosDeFacturacionElectronica.cs"));

    private static Dictionary<int, string> Pares(string diccionario)
    {
        var inicio = Fuente.IndexOf($" {diccionario} {{ get; }}", StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"TextosDeFacturacionElectronica ya no declara {diccionario}.");
        var fin = Fuente.IndexOf("};", inicio, StringComparison.Ordinal);
        return Regex.Matches(Fuente[inicio..fin], @"\[(\d+)\]\s*=\s*""([^""]+)""")
            .ToDictionary(m => int.Parse(m.Groups[1].Value), m => m.Groups[2].Value);
    }

    /// <summary>Cada valor tiene su etiqueta y su nombre, y el nombre es el del enum.</summary>
    private static void Cubre<TEnum>(string etiquetas, string nombres) where TEnum : struct, Enum
    {
        var esperados = Enum.GetValues<TEnum>().ToDictionary(v => Convert.ToInt32(v), v => v.ToString());
        Assert.Equal(esperados.Keys.OrderBy(k => k), Pares(etiquetas).Keys.OrderBy(k => k));
        Assert.Equal(esperados.OrderBy(p => p.Key), Pares(nombres).OrderBy(p => p.Key));
    }

    /// <summary>Sólo la etiqueta (enums que la pantalla muestra pero nunca manda).</summary>
    private static void Etiqueta<TEnum>(string etiquetas) where TEnum : struct, Enum =>
        Assert.Equal(Enum.GetValues<TEnum>().Select(v => Convert.ToInt32(v)).OrderBy(k => k), Pares(etiquetas).Keys.OrderBy(k => k));

    [Fact]
    public void Cada_modo_de_emision() => Cubre<EmissionMode>("Modos", "NombresDeModo");

    [Fact]
    public void Cada_ambiente() => Cubre<DianEnvironment>("Ambientes", "NombresDeAmbiente");

    [Fact]
    public void Cada_entrega_del_correo() => Cubre<EmailDeliveryBy>("EntregasDeCorreo", "NombresDeEntregaDeCorreo");

    [Fact]
    public void Cada_tipo_de_documento_electronico() => Cubre<ElectronicDocumentKind>("TiposDeDocumento", "NombresDeTipoDeDocumento");

    [Fact]
    public void Cada_estado_del_documento() => Cubre<ElectronicDocumentStatus>("Estados", "NombresDeEstado");

    [Fact]
    public void Cada_tipo_de_contingencia() => Cubre<ContingencyType>("TiposDeContingencia", "NombresDeTipoDeContingencia");

    [Fact]
    public void Cada_tipo_de_resolucion() => Cubre<ResolutionKind>("TiposDeResolucion", "NombresDeTipoDeResolucion");

    [Fact]
    public void Cada_resultado_del_canal() => Etiqueta<ChannelOutcome>("Resultados");

    [Fact]
    public void Cada_operacion_de_transmision() => Etiqueta<TransmissionOperation>("Operaciones");

    [Fact]
    public void Cada_motivo_de_version() => Etiqueta<DocumentVersionReason>("MotivosDeVersion");

    [Fact]
    public void Quien_rechaza() => Etiqueta<RejectedBy>("Rechazadores");

    [Fact]
    public void Cada_codigo_unico() => Etiqueta<UniqueCodeKind>("CodigosUnicos");

    [Fact]
    public void Cada_actor() => Etiqueta<ActorKind>("Actores");

    [Fact]
    public void Cada_veredicto_de_la_preparacion() => Etiqueta<VeredictoFiscal>("Veredictos");

    [Fact]
    public void Las_constantes_de_estado_son_las_del_enum()
    {
        Assert.Contains($"VeredictoBloqueado = {(int)VeredictoFiscal.Blocked};", Fuente, StringComparison.Ordinal);
        Assert.Contains($"VeredictoElectronico = {(int)VeredictoFiscal.Electronic};", Fuente, StringComparison.Ordinal);
        Assert.Contains($"EstadoRechazado = {(int)ElectronicDocumentStatus.Rejected};", Fuente, StringComparison.Ordinal);
    }
}
