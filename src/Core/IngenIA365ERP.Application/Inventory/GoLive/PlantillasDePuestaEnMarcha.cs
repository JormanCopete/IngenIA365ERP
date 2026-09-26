using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Inventory.Imports;
using IngenIA365ERP.Domain.Entities.Inventory.GoLive;

namespace IngenIA365ERP.Application.Inventory.GoLive;

// Las plantillas 14 y 15 de la parametrización (feature 012, T308, T311; contracts/plantillas.md §14, §15): sus columnas. La
// misma definición arma el libro con su hoja «Instrucciones» y es lo que exigen ImportOpeningBalanceCommand e
// ImportLegacyFiguresCommand; CatalogoDePlantillas las publica con su clave. (nuevo)

/// <summary>
/// Plantilla 14 — saldo inicial (§14). Genera, por bodega, borradores <c>OpeningBalance</c> fechados en su corte; nunca
/// confirma. (nuevo)
/// </summary>
public static class PlantillaDeSaldoInicial
{
    public const string Clave = CatalogoDePlantillas.SaldoInicialClave;

    public const string Bodega = "bodega";
    public const string FechaDeCorte = "fechaDeCorte";
    public const string Producto = "producto";
    public const string Ubicacion = "ubicacion";
    public const string Cantidad = "cantidad";
    public const string CostoUnitario = "costoUnitario";
    public const string Lote = "lote";
    public const string Vencimiento = "vencimiento";
    public const string Serie = "serie";

    /// <summary>El largo del código de producto (§0.4: 20).</summary>
    public const int LargoDeProducto = 20;

    public static DefinicionDePlantilla Definicion { get; } = new(Clave, "Saldo inicial", ModuloDeAuditoria.Inventory,
    [
        new HojaDePlantilla("Datos",
        [
            new(Bodega, TipoDeValor.Codigo, Obligatoria: true, Largo: CodigoDeCatalogo.LargoCorto,
                Reglas: "bodega no activa y operativa (no de tránsito), dentro de su alcance", Ejemplo: "B01"),
            new(FechaDeCorte, TipoDeValor.Fecha, Obligatoria: true,
                Reglas: "la misma en todas las filas de una bodega; queda como su fecha de corte (la víspera de su activación)", Ejemplo: "AAAA-MM-DD"),
            new(Producto, TipoDeValor.Codigo, Obligatoria: true, Largo: LargoDeProducto,
                Reglas: "inventariable, con grupo contable y no bloqueado", Ejemplo: "ARZ-001"),
            new(Ubicacion, TipoDeValor.Codigo, Largo: CodigoDeCatalogo.LargoCorto, Reglas: "de la bodega; vacío = la ubicación por defecto", Ejemplo: "A-01"),
            new(Cantidad, TipoDeValor.Cantidad, Obligatoria: true,
                Reglas: "mayor que cero, en unidad base, con los decimales que admite la unidad", Ejemplo: "120"),
            new(CostoUnitario, TipoDeValor.Costo, Obligatoria: true, Reglas: "cero o más; entra al costo cargado (cero se admite con aviso)", Ejemplo: "1850,000000"),
            new(Lote, TipoDeValor.Texto, Largo: 30, Reglas: "se habilita con la entrega I6"),
            new(Vencimiento, TipoDeValor.Fecha, Reglas: "se habilita con la entrega I6"),
            new(Serie, TipoDeValor.Texto, Largo: 60, Reglas: "se habilita con la entrega I6"),
        ]),
    ]);
}

/// <summary>
/// Plantilla 15 — cifras de SOLIDO (§15). Escribe <c>INV_LegacyFigures</c>: sólo informativas, nunca kardex. (nuevo)
/// </summary>
public static class PlantillaDeCifrasDeSolido
{
    public const string Clave = CatalogoDePlantillas.CifrasDeSolidoClave;

    public const string Fecha = "fecha";
    public const string Bodega = "bodega";
    public const string Producto = "producto";
    public const string Cantidad = "cantidad";
    public const string Valor = "valor";
    public const string GrupoContable = "grupoContable";

    public static DefinicionDePlantilla Definicion { get; } = new(Clave, "Cifras de SOLIDO", ModuloDeAuditoria.Inventory,
    [
        new HojaDePlantilla("Datos",
        [
            new(Fecha, TipoDeValor.Fecha, Obligatoria: true, Reglas: "fecha de corte de la cifra; un archivo puede traer varias", Ejemplo: "AAAA-MM-DD"),
            new(Bodega, TipoDeValor.Texto, Obligatoria: true, Largo: LegacyFigure.LargoDelCodigoDeBodega,
                Reglas: "código de una bodega que ya existe en el ERP (activa o no)", Ejemplo: "B02"),
            new(Producto, TipoDeValor.Texto, Obligatoria: true, Largo: LegacyFigure.LargoDelCodigoDeProducto,
                Reglas: "tal como viene de SOLIDO; si no existe en el catálogo nuevo, la fila exige grupoContable y queda sin resolver", Ejemplo: "ARZ-001"),
            new(Cantidad, TipoDeValor.Cantidad, Obligatoria: true, Reglas: "en unidad base de SOLIDO; puede ser negativa", Ejemplo: "640"),
            new(Valor, TipoDeValor.Monto, Obligatoria: true, Reglas: "valor total de la existencia", Ejemplo: "1184000,00"),
            new(GrupoContable, TipoDeValor.Codigo, Largo: CodigoDeCatalogo.LargoCorto,
                Reglas: "obligatorio si el producto no existe; si existe y difiere del suyo, aviso", Ejemplo: "ABARROTES"),
        ]),
    ]);
}
