namespace IngenIA365ERP.Application.Common.BankFiles;

/// <summary>
/// Los formatos de archivo de pagos de Tesorería de dos bancos (pedido del dueño del 2026-11-15, decisión «usar el formato público»):
/// Bancolombia PAB (pago a proveedores) y AV Villas (cargue masivo de pagos a terceros). Se siembran <b>inactivos</b> y «pendientes de
/// validar»: el lote sigue usando <c>PAGOS-GENERICO</c> hasta que alguien pruebe el archivo con el banco y active el formato en Maestros ›
/// Formatos bancarios. Las posiciones salen de la documentación pública que circula de cada banco, no del instructivo firmado por el
/// banco con la cooperativa; si el banco entrega otro, se corrige el formato por pantalla (o se crea la versión siguiente). El motor no sabe
/// de bancos: estos formatos son dato, como los demás (contracts/archivos.md §2.1). (nuevo)
/// </summary>
public static class FormatosDeBancosParaTesoreria
{
    public const string Bancolombia = "BANCOLOMBIA-PAB";
    public const string AvVillas = "AVVILLAS-PAGOS";

    /// <summary>Cómo se reconoce cada banco en <c>COR_Banks</c>: por el nombre (la ACH puede venir con 3 o 4 dígitos).</summary>
    public static readonly IReadOnlyList<(string Codigo, string NombreContiene, Func<BankFileFormatDefinition> Definicion)> Todos =
    [
        (Bancolombia, "BANCOLOMBIA", DefinicionBancolombia),
        (AvVillas, "VILLAS", DefinicionAvVillas),
    ];

    /// <summary>
    /// Bancolombia PAB, ancho fijo: registro de control «1» y un registro «6» por pago; sin totales al final (van en el control). Tipo de
    /// transacción 37 ahorros, 27 corriente; tipo de documento 1 CC, 2 CE, 3 NIT, 4 TI, 5 PA; valor con dos decimales implícitos.
    /// </summary>
    public static BankFileFormatDefinition DefinicionBancolombia() => BankFileFormatDefinition.Parse("""
        { "code": "BANCOLOMBIA-PAB", "name": "Bancolombia — pago a proveedores (PAB)", "scope": "TreasuryPayments",
          "validFrom": "2026-11-01", "kind": "FixedWidth", "encoding": "us-ascii", "lineEnding": "CRLF",
          "uppercase": true, "stripAccents": true, "amountFormat": "ImplicitCents", "isActive": false,
          "fileName": "PAB{PaymentDate:yyyyMMdd}{Sequence:00}.txt", "contentType": "text/plain",
          "notes": "PENDIENTE DE VALIDAR: armado con el formato público PAB de Bancolombia (2026-11-15). Antes de activarlo, cargue un archivo de prueba en la sucursal virtual empresas y compare con el instructivo que le entregue el banco.",
          "records": {
            "header": { "enabled": true, "fields": [
              { "order": 1, "name": "Tipo de registro", "source": "Constant", "value": "1", "length": 1 },
              { "order": 2, "name": "NIT pagador", "source": "CompanyNit", "length": 15, "align": "Right", "pad": "0", "required": true },
              { "order": 3, "name": "Aplicación", "source": "Constant", "value": "I", "length": 1 },
              { "order": 4, "name": "Relleno", "source": "Blank", "length": 15 },
              { "order": 5, "name": "Clase de transacción", "source": "Constant", "value": "220", "length": 3 },
              { "order": 6, "name": "Descripción", "source": "Constant", "value": "PAGOS", "length": 10 },
              { "order": 7, "name": "Fecha de transmisión", "source": "GenerationDate", "dataType": "Date", "format": "yyyyMMdd", "length": 8 },
              { "order": 8, "name": "Secuencia", "source": "Sequence", "dataType": "Integer", "format": "00", "length": 2, "align": "Right", "pad": "0" },
              { "order": 9, "name": "Fecha de aplicación", "source": "PaymentDate", "dataType": "Date", "format": "yyyyMMdd", "length": 8 },
              { "order": 10, "name": "Número de registros", "source": "LineCount", "dataType": "Integer", "length": 6, "align": "Right", "pad": "0" },
              { "order": 11, "name": "Sumatoria de débitos", "source": "Blank", "length": 17, "align": "Right", "pad": "0" },
              { "order": 12, "name": "Sumatoria de créditos", "source": "TotalAmount", "dataType": "Amount", "length": 17, "align": "Right", "pad": "0" },
              { "order": 13, "name": "Cuenta a debitar", "source": "SourceAccountNumber", "length": 11, "align": "Right", "pad": "0", "required": true },
              { "order": 14, "name": "Tipo de cuenta a debitar", "source": "SourceAccountType", "length": 1, "map": { "1": "S", "2": "D" } },
              { "order": 15, "name": "Relleno", "source": "Blank", "length": 149 } ] },
            "detail": { "enabled": true, "fields": [
              { "order": 1, "name": "Tipo de registro", "source": "Constant", "value": "6", "length": 1 },
              { "order": 2, "name": "Documento del beneficiario", "source": "PayeeDocument", "length": 15, "required": true },
              { "order": 3, "name": "Nombre del beneficiario", "source": "PayeeFullName", "length": 30 },
              { "order": 4, "name": "Banco destino", "source": "PayeeBankCode", "length": 9, "align": "Right", "pad": "0", "required": true },
              { "order": 5, "name": "Cuenta destino", "source": "PayeeAccountNumber", "length": 17, "required": true },
              { "order": 6, "name": "Indicador de lugar de pago", "source": "Constant", "value": "S", "length": 1 },
              { "order": 7, "name": "Tipo de transacción", "source": "PayeeAccountType", "length": 2,
                "map": { "Ahorros": "37", "A": "37", "1": "37", "Savings": "37", "Corriente": "27", "C": "27", "2": "27", "Checking": "27" } },
              { "order": 8, "name": "Valor", "source": "Amount", "dataType": "Amount", "length": 17, "align": "Right", "pad": "0", "required": true },
              { "order": 9, "name": "Fecha de aplicación", "source": "PaymentDate", "dataType": "Date", "format": "yyyyMMdd", "length": 8 },
              { "order": 10, "name": "Referencia", "source": "Concept", "length": 21 },
              { "order": 11, "name": "Tipo de documento", "source": "PayeeDocumentType", "length": 1,
                "map": { "C": "1", "E": "2", "N": "3", "T": "4", "P": "5" } },
              { "order": 12, "name": "Oficina de entrega", "source": "Blank", "length": 5, "align": "Right", "pad": "0" },
              { "order": 13, "name": "Fax", "source": "Blank", "length": 15 },
              { "order": 14, "name": "Correo", "source": "Blank", "length": 80 },
              { "order": 15, "name": "Identificación del autorizado", "source": "Blank", "length": 15 },
              { "order": 16, "name": "Relleno", "source": "Blank", "length": 27 } ] },
            "trailer": { "enabled": false, "fields": [] } } }
        """)!;

    /// <summary>
    /// AV Villas, cargue masivo de pagos a terceros por el portal empresarial: delimitado por punto y coma, una línea por pago, sin
    /// encabezado ni totales. Es la forma de cargue que el portal acepta en la documentación pública; el layout de ancho fijo de AV Villas
    /// Empresas, si el banco lo exige, se carga sobre este mismo código.
    /// </summary>
    public static BankFileFormatDefinition DefinicionAvVillas() => BankFileFormatDefinition.Parse("""
        { "code": "AVVILLAS-PAGOS", "name": "Banco AV Villas — pagos a terceros (cargue masivo)", "scope": "TreasuryPayments",
          "validFrom": "2026-11-01", "kind": "Delimited", "delimiter": ";", "quoteText": false, "encoding": "us-ascii", "lineEnding": "CRLF",
          "uppercase": true, "stripAccents": true, "amountFormat": "Integer", "isActive": false,
          "fileName": "PAGOS_AVVILLAS_{PaymentDate:yyyyMMdd}_{Sequence:000}.txt", "contentType": "text/plain",
          "notes": "PENDIENTE DE VALIDAR: armado con el cargue masivo público de AV Villas (2026-11-15), no con el instructivo del banco. Antes de activarlo, haga un cargue de prueba en el portal empresarial; si el banco exige otro layout, corríjalo aquí.",
          "records": {
            "header": { "enabled": false, "fields": [] },
            "detail": { "enabled": true, "fields": [
              { "order": 1, "name": "Tipo de documento", "source": "PayeeDocumentType", "map": { "C": "CC", "E": "CE", "N": "NIT", "T": "TI", "P": "PA" } },
              { "order": 2, "name": "Documento", "source": "PayeeDocument", "required": true },
              { "order": 3, "name": "Nombre", "source": "PayeeFullName", "length": 40 },
              { "order": 4, "name": "Banco destino (ACH)", "source": "PayeeBankCode", "required": true },
              { "order": 5, "name": "Tipo de cuenta", "source": "PayeeAccountType",
                "map": { "Ahorros": "AH", "A": "AH", "1": "AH", "Savings": "AH", "Corriente": "CC", "C": "CC", "2": "CC", "Checking": "CC" } },
              { "order": 6, "name": "Cuenta", "source": "PayeeAccountNumber", "required": true },
              { "order": 7, "name": "Valor", "source": "Amount", "dataType": "Amount", "required": true },
              { "order": 8, "name": "Referencia", "source": "Concept", "length": 30 } ] },
            "trailer": { "enabled": false, "fields": [] } } }
        """)!;
}
