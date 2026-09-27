using System.Text.RegularExpressions;
using IngenIA365ERP.Application.Inventory.Pos;
using IngenIA365ERP.Architecture.Tests.Helpers;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, I3 (T633; decisiones-transversales §2.5): los enums de ventas, caja y medios de pago que la API entrega como número
/// y que las pantallas mandan por nombre. Shared no referencia Domain, así que la etiqueta y el nombre de cada valor se fijan aquí
/// contra los enums reales, en el molde de <see cref="TextosDeInventarioTests"/>: un valor nuevo sin su texto rompe aquí, no en la
/// pantalla («Clase 12»), y un nombre mal escrito rompe aquí, no en un 400 del servidor. (nuevo)
/// </summary>
public class TextosDeVentasTests
{
    private static readonly string Fuente = File.ReadAllText(Path.Combine(RepoPath.FindRepoRoot(),
        "src", "Presentation", "IngenIA365ERP.Shared", "Services", "Ventas", "TextosDeVentas.cs"));

    private static Dictionary<int, string> Pares(string diccionario)
    {
        var inicio = Fuente.IndexOf($" {diccionario} {{ get; }}", StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"TextosDeVentas ya no declara {diccionario}.");
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

    [Fact]
    public void Cada_clase_de_medio_de_pago() => Cubre<PaymentMeansClass>("ClasesDeMedio", "NombresDeClaseDeMedio");

    [Fact]
    public void Cada_tipo_de_tarjeta() => Cubre<CardKind>("TiposDeTarjeta", "NombresDeTipoDeTarjeta");

    [Fact]
    public void Cada_tipo_de_referencia() => Cubre<PaymentReferenceKind>("TiposDeReferencia", "NombresDeTipoDeReferencia");

    [Fact]
    public void Cada_forma_de_arqueo() => Cubre<CashCountMethod>("FormasDeArqueo", "NombresDeFormaDeArqueo");

    [Fact]
    public void Cada_tipo_de_denominacion() => Cubre<CashDenominationKind>("TiposDeDenominacion", "NombresDeTipoDeDenominacion");

    [Fact]
    public void Cada_rol_de_tipo_en_la_caja() => Cubre<CashRegisterDocumentRole>("RolesDeCaja", "NombresDeRolDeCaja");

    [Fact]
    public void Cada_estado_de_sesion() => Cubre<CashSessionStatus>("EstadosDeSesion", "NombresDeEstadoDeSesion");

    [Fact]
    public void Cada_clase_de_movimiento_de_caja() => Cubre<CashMovementKind>("ClasesDeMovimiento", "NombresDeClaseDeMovimiento");

    [Fact]
    public void Cada_destino_de_movimiento() => Cubre<CashMovementDestination>("DestinosDeMovimiento", "NombresDeDestinoDeMovimiento");

    [Fact]
    public void Cada_tratamiento_de_diferencia() => Cubre<CashDifferenceTreatment>("TratamientosDeDiferencia", "NombresDeTratamientoDeDiferencia");

    [Fact]
    public void Cada_estado_del_cierre_del_dia() => Cubre<DayCloseStatus>("EstadosDeCierreDelDia", "NombresDeEstadoDeCierreDelDia");

    [Fact]
    public void Cada_direccion_de_pago() => Cubre<PaymentDirection>("DireccionesDePago", "NombresDeDireccionDePago");

    [Fact]
    public void Cada_origen_de_credito() => Cubre<CreditOrigin>("OrigenesDeCredito", "NombresDeOrigenDeCredito");

    [Fact]
    public void Cada_estado_de_bono() => Cubre<VoucherRedemptionStatus>("EstadosDeBono", "NombresDeEstadoDeBono");

    [Fact]
    public void Cada_formato_de_impresion() => Cubre<CashRegisterPrintFormat>("FormatosDeImpresion", "NombresDeFormatoDeImpresion");

    [Fact]
    public void Las_constantes_nombran_el_valor_del_dominio()
    {
        Assert.Contains($"MedioEfectivo = {(int)PaymentMeansClass.Cash};", Fuente, StringComparison.Ordinal);
        Assert.Contains($"MedioTarjetaCredito = {(int)PaymentMeansClass.CreditCard};", Fuente, StringComparison.Ordinal);
        Assert.Contains($"MedioTarjetaDebito = {(int)PaymentMeansClass.DebitCard};", Fuente, StringComparison.Ordinal);
        Assert.Contains($"MedioCreditoAsociado = {(int)PaymentMeansClass.AssociateCredit};", Fuente, StringComparison.Ordinal);
        Assert.Contains($"MedioCreditoCliente = {(int)PaymentMeansClass.CustomerCredit};", Fuente, StringComparison.Ordinal);
        Assert.Contains($"MedioBono = {(int)PaymentMeansClass.Voucher};", Fuente, StringComparison.Ordinal);
        Assert.Contains($"ArqueoPorDenominaciones = {(int)CashCountMethod.PhysicalCount};", Fuente, StringComparison.Ordinal);
        Assert.Contains($"ArqueoPorLote = {(int)CashCountMethod.VoucherTotal};", Fuente, StringComparison.Ordinal);
        Assert.Contains($"ArqueoPorReferencias = {(int)CashCountMethod.ByReference};", Fuente, StringComparison.Ordinal);
        Assert.Contains($"SesionAbierta = {(int)CashSessionStatus.Open};", Fuente, StringComparison.Ordinal);
        Assert.Contains($"RolVentaPos = {(int)CashRegisterDocumentRole.PosSale};", Fuente, StringComparison.Ordinal);
        Assert.Contains($"RolFacturaAPedido = {(int)CashRegisterDocumentRole.InvoiceOnRequest};", Fuente, StringComparison.Ordinal);
        Assert.Contains($"GrupoDeVentas = {(int)DocumentClassGroup.Sales};", Fuente, StringComparison.Ordinal);
        Assert.Contains($"GrupoDeCaja = {(int)DocumentClassGroup.Cash};", Fuente, StringComparison.Ordinal);
        Assert.Contains($"ClasesDeVenta = [{(int)DocumentClass.SalesInvoice}, "
                        + $"{(int)DocumentClass.PosEquivalentDocument}, "
                        + $"{(int)DocumentClass.NonElectronicSalesReceipt}];", Fuente, StringComparison.Ordinal);
        Assert.Contains($"ClasesDeNota = [{(int)DocumentClass.NonElectronicSalesNote}, "
                        + $"{(int)DocumentClass.CreditNote}, "
                        + $"{(int)DocumentClass.PosAdjustmentNote}];", Fuente, StringComparison.Ordinal);
    }
}
