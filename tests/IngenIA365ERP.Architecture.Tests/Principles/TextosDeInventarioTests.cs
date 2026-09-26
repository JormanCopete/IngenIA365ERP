using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012 (T183, T185; decisiones-transversales §2.5): la API entrega los enums de Inventario como número y la
/// pantalla los traduce con <c>Shared/Services/Inventario/TextosDeInventario</c>. Shared no referencia Domain, así que
/// nada impide que un número quede sin etiqueta o que una etiqueta nombre un número que ya no existe; esta prueba lee el
/// fuente y lo compara con los enums reales. Una clase nueva en <c>DocumentClass</c> sin su texto rompe aquí, no en la
/// pantalla («Clase 35»).
/// </summary>
public class TextosDeInventarioTests
{
    private static readonly string Fuente = File.ReadAllText(Path.Combine(RepoPath.FindRepoRoot(),
        "src", "Presentation", "IngenIA365ERP.Shared", "Services", "Inventario", "TextosDeInventario.cs"));

    private static IReadOnlyList<int> Claves(string diccionario)
    {
        var inicio = Fuente.IndexOf($" {diccionario} {{ get; }}", StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"TextosDeInventario ya no declara {diccionario}.");
        var fin = Fuente.IndexOf("};", inicio, StringComparison.Ordinal);
        return Regex.Matches(Fuente[inicio..fin], @"\[(\d+)\]\s*=").Select(m => int.Parse(m.Groups[1].Value)).ToList();
    }

    private static void Cubre<TEnum>(string diccionario) where TEnum : struct, Enum
    {
        var esperadas = Enum.GetValues<TEnum>().Select(v => Convert.ToInt32(v)).OrderBy(v => v).ToList();
        Assert.Equal(esperadas, Claves(diccionario).OrderBy(v => v).ToList());
    }

    [Fact]
    public void Cada_clase_de_documento_tiene_su_texto() => Cubre<DocumentClass>("Clases");

    [Fact]
    public void Cada_grupo_tiene_su_texto() => Cubre<DocumentClassGroup>("Grupos");

    [Fact]
    public void Cada_estado_tiene_su_texto() => Cubre<DocumentStatus>("Estados");

    [Fact]
    public void Cada_modo_de_paso_tiene_su_texto() => Cubre<PostingMode>("ModosDePaso");

    [Fact]
    public void Cada_forma_de_numerar_tiene_su_texto() => Cubre<NumberedBy>("Numeracion");

    [Fact]
    public void Cada_clase_de_producto_tiene_su_texto() => Cubre<ProductKind>("ClasesDeProducto");

    [Fact]
    public void Cada_estado_de_producto_tiene_su_texto() => Cubre<ProductStatus>("EstadosDeProducto");

    [Fact]
    public void Cada_tratamiento_de_IVA_tiene_su_texto() => Cubre<IngenIA365ERP.Domain.Enums.Core.VatSaleTreatment>("TratamientosDeIva");

    [Fact]
    public void Cada_uso_de_unidad_tiene_su_texto() => Cubre<ProductUnitUsage>("UsosDeUnidad");

    [Fact]
    public void Cada_comportamiento_de_bodega_tiene_su_texto() => Cubre<WarehouseBehavior>("ComportamientosDeBodega");

    [Fact]
    public void Cada_estado_de_activacion_tiene_su_texto() => Cubre<WarehouseActivationStatus>("ActivacionesDeBodega");

    [Fact]
    public void Cada_tipo_de_diferencia_de_traslado_tiene_su_texto() => Cubre<TransferDiscrepancyKind>("TiposDeDiferencia");

    [Fact]
    public void Cada_resolucion_de_diferencia_tiene_su_texto() => Cubre<TransferDiscrepancyResolution>("ResolucionesDeDiferencia");

    [Fact]
    public void Cada_tipo_de_conteo_tiene_su_texto() => Cubre<CountKind>("TiposDeConteo");

    [Fact]
    public void Cada_alcance_de_conteo_tiene_su_texto() => Cubre<CountScope>("AlcancesDeConteo");

    [Fact]
    public void Las_constantes_nombran_el_valor_del_dominio()
    {
        Assert.Contains($"DiferenciaFaltante = {(int)TransferDiscrepancyKind.Shortage};", Fuente, StringComparison.Ordinal);
        Assert.Contains($"ResolucionBajaDesdeTransito = {(int)TransferDiscrepancyResolution.WriteOffFromTransit};", Fuente, StringComparison.Ordinal);
        Assert.Contains($"ResolucionAjusteDeSobrante = {(int)TransferDiscrepancyResolution.SurplusAdjustment};", Fuente, StringComparison.Ordinal);
        Assert.Contains($"TratamientoGravado = {(int)IngenIA365ERP.Domain.Enums.Core.VatSaleTreatment.Taxed};", Fuente, StringComparison.Ordinal);
        Assert.Contains($"EstadoBloqueado = {(int)ProductStatus.Blocked};", Fuente, StringComparison.Ordinal);
        Assert.Contains($"ClasesDeProductoDeI1 = [{(int)ProductKind.Inventoriable}, {(int)ProductKind.Service}];", Fuente, StringComparison.Ordinal);
        Assert.Contains($"GrupoDeCompras = {(int)DocumentClassGroup.Purchases};", Fuente, StringComparison.Ordinal);
        Assert.Contains($"ClaseDeConsumoInterno = {(int)DocumentClass.InternalConsumption};", Fuente, StringComparison.Ordinal);
    }
}
