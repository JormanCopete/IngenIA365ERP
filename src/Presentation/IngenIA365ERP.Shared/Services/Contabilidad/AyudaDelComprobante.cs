namespace IngenIA365ERP.Shared.Services.Contabilidad;

/// <summary>
/// Los nombres y la ayuda (F1) de cada campo de la ventana de comprobantes. Antes vivían en la página y F2 los mostraba en un diálogo;
/// desde el 2026-11-16 F1 es la ayuda (el globo de teclado.js lee <c>data-ayuda</c>) y F2 busca, como en Comercial y Recaudo.
/// </summary>
public static class AyudaDelComprobante
{
    public static string Nombre(string campo) => campo switch
    {
        "VoucherType" => "Tipo de comprobante", "Date" => "Fecha", "Description" => "Descripción", "Account" => "Cuenta", "Person" => "Tercero",
        "CrossDocument" => "Documento cruce", "CostCenter" => "Centro de costo", "Branch" => "Sucursal", "Amount" => "Débito / crédito",
        "TaxBase" => "Base gravable", "Detail" => "Detalle", _ => campo,
    };

    public static string Texto(string campo) => campo switch
    {
        "VoucherType" => "Sólo los tipos de uso manual: los de módulo (nómina, cartera…) los genera cada módulo al aprobar su operación. F2 despliega la lista.",
        "Date" => "Debe caer en un mes abierto y no ser posterior a hoy. Un mes cerrado se reabre en Períodos, con motivo.",
        "Description" => "Qué registra el comprobante; se imprime como concepto y aparece en la lista.",
        "Account" => "Código o nombre: sólo cuentas de movimiento, activas y habilitadas para Contabilidad. Lo que la cuenta exige se habilita al elegirla. F2 busca.",
        "Person" => "Obligatorio cuando la cuenta lo exige (cuentas por cobrar, por pagar, retenciones…). Documento o nombre; F2 busca y, si no existe, se puede crear.",
        "CrossDocument" => "Tipo y número del documento que soporta el movimiento (factura, recibo…). Obligatorio si la cuenta lo exige. F2 busca el tipo.",
        "CostCenter" => "Sólo se admite en cuentas que lo manejan; en las demás queda deshabilitado. Si hay uno solo, se pone solo. F2 busca.",
        "Branch" => "Se propone la de su oficina (o la principal) y la línea nueva repite la anterior. Si la cuenta «exige sucursal», hay que elegirla. F2 busca.",
        "Amount" => "Una línea lleva débito o crédito, nunca ambos ni cero. F6 pone en la línea activa lo que falta para cuadrar.",
        "TaxBase" => "Base sobre la que se calcula el impuesto; el valor de la línea se compara con base × tarifa vigente, con la tolerancia configurada.",
        "Detail" => "Detalle de la línea. Enter pasa a la línea siguiente; en la última abre una nueva que repite tercero, centro y sucursal.",
        _ => "Elija un campo para ver su ayuda.",
    };
}
