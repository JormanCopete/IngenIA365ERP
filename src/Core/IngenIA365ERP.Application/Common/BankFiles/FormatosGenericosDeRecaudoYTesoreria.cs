namespace IngenIA365ERP.Application.Common.BankFiles;

/// <summary>
/// Los cuatro formatos genéricos de Recaudo y Tesorería (feature 015, E-06, T0024; FR-196, FR-197, FR-201, FR-213; archivos-y-plantillas.md §4):
/// sin banco, «pendientes de validar» con el dueño (P1, P6) y sembrados por <c>BankFileFormatsSeeder</c> con el mismo motor que el
/// <c>CSV-GENERICO</c> de la nómina. Cada cooperativa puede crear los suyos por banco. Son CSV con encabezados: la fila de encabezados es la
/// cabecera, hecha de constantes, que el escritor escribe y el lector salta.
///
/// <para>
/// Una desviación de §4: la respuesta de la pagaduría va en CSV y no en XLSX, porque el motor común sólo lee archivos planos; una hoja de
/// cálculo se guarda como CSV antes de cargarla.
/// </para>
/// </summary>
public static class FormatosGenericosDeRecaudoYTesoreria
{
    public const string Extracto = "EXTRACTO-GENERICO";
    public const string Pagos = "PAGOS-GENERICO";
    public const string RespuestaDePagos = "RESP-PAGOS-GENERICO";
    public const string RespuestaDePagaduria = "RESP-PAGADURIA-GEN";

    /// <summary>Lo que dice la nota de los cuatro hasta que el dueño los valide.</summary>
    public const string PendienteDeValidar = "Pendiente de validar con el dueño (feature 015, P1 y P6).";

    /// <summary>Los cuatro, por código.</summary>
    public static IReadOnlyList<(string Codigo, Func<BankFileFormatDefinition> Definicion)> Todos { get; } =
    [
        (Extracto, DefinicionDelExtracto),
        (Pagos, DefinicionDePagos),
        (RespuestaDePagos, DefinicionDeRespuestaDePagos),
        (RespuestaDePagaduria, DefinicionDeRespuestaDePagaduria),
    ];

    /// <summary>
    /// <c>EXTRACTO-GENERICO</c> (ámbito 7): fecha, valor con signo, referencia, documento y nombre del consignante, descripción y saldo. Huella de
    /// movimiento declarada en la nota: sin secuencia del banco, la combinación banco, cuenta, fecha, valor y referencia (FR-197).
    /// </summary>
    public static BankFileFormatDefinition DefinicionDelExtracto() => BankFileFormatDefinition.Parse("""
        { "code": "EXTRACTO-GENERICO", "name": "Extracto genérico (cualquier banco)", "scope": "BankStatement",
          "validFrom": "2026-01-01", "kind": "Delimited", "delimiter": ";", "quoteText": false, "encoding": "utf-8", "lineEnding": "CRLF",
          "uppercase": false, "stripAccents": false, "amountFormat": "Point2", "fileName": "EXTRACTO.csv", "contentType": "text/csv",
          "notes": "Huella del movimiento: sin secuencia del banco, (banco, cuenta, fecha, valor, referencia), con marca de posibles duplicados. Valor con signo: positivo es un abono. Pendiente de validar con el dueño (feature 015, P1 y P6).",
          "records": {
            "header": { "enabled": true, "fields": [
              { "order": 1, "name": "Fecha", "source": "Constant", "value": "fecha" },
              { "order": 2, "name": "Valor", "source": "Constant", "value": "valor" },
              { "order": 3, "name": "Referencia", "source": "Constant", "value": "referencia" },
              { "order": 4, "name": "Documento", "source": "Constant", "value": "documento" },
              { "order": 5, "name": "Consignante", "source": "Constant", "value": "consignante" },
              { "order": 6, "name": "Descripción", "source": "Constant", "value": "descripcion" },
              { "order": 7, "name": "Saldo", "source": "Constant", "value": "saldo" } ] },
            "detail": { "enabled": true, "fields": [
              { "order": 1, "name": "Fecha", "source": "MovementDate", "format": "yyyy-MM-dd", "required": true },
              { "order": 2, "name": "Valor", "source": "Amount", "required": true },
              { "order": 3, "name": "Referencia", "source": "MovementReference" },
              { "order": 4, "name": "Documento del consignante", "source": "DepositorTaxId" },
              { "order": 5, "name": "Nombre del consignante", "source": "DepositorName" },
              { "order": 6, "name": "Descripción", "source": "Description" },
              { "order": 7, "name": "Saldo", "source": "Balance" } ] },
            "trailer": { "enabled": false, "fields": [] } } }
        """)!;

    /// <summary><c>PAGOS-GENERICO</c> (ámbito 3): documento, nombre, banco (código ACH), tipo y número de cuenta, valor y concepto (FR-213).</summary>
    public static BankFileFormatDefinition DefinicionDePagos() => BankFileFormatDefinition.Parse("""
        { "code": "PAGOS-GENERICO", "name": "Pagos genérico de tesorería (cualquier banco)", "scope": "TreasuryPayments",
          "validFrom": "2026-01-01", "kind": "Delimited", "delimiter": ";", "quoteText": false, "encoding": "utf-8", "lineEnding": "CRLF",
          "uppercase": true, "stripAccents": true, "amountFormat": "Point2",
          "fileName": "PAGOS_{PaymentDate:yyyyMMdd}_{Sequence:000}.csv", "contentType": "text/csv",
          "notes": "Sin formato del banco, este archivo o la forma portal del banco. Pendiente de validar con el dueño (feature 015, P1 y P6).",
          "records": {
            "header": { "enabled": true, "fields": [
              { "order": 1, "name": "Documento", "source": "Constant", "value": "documento" },
              { "order": 2, "name": "Nombre", "source": "Constant", "value": "nombre" },
              { "order": 3, "name": "Banco", "source": "Constant", "value": "banco" },
              { "order": 4, "name": "Tipo de cuenta", "source": "Constant", "value": "tipo_cuenta" },
              { "order": 5, "name": "Cuenta", "source": "Constant", "value": "cuenta" },
              { "order": 6, "name": "Valor", "source": "Constant", "value": "valor" },
              { "order": 7, "name": "Concepto", "source": "Constant", "value": "concepto" } ] },
            "detail": { "enabled": true, "fields": [
              { "order": 1, "name": "Documento", "source": "PayeeDocument", "required": true },
              { "order": 2, "name": "Nombre", "source": "PayeeFullName", "length": 60 },
              { "order": 3, "name": "Banco destino (ACH)", "source": "PayeeBankCode", "required": true },
              { "order": 4, "name": "Tipo de cuenta", "source": "PayeeAccountType" },
              { "order": 5, "name": "Cuenta", "source": "PayeeAccountNumber", "required": true },
              { "order": 6, "name": "Valor", "source": "Amount", "required": true },
              { "order": 7, "name": "Concepto", "source": "Concept", "length": 40 } ] },
            "trailer": { "enabled": false, "fields": [] } } }
        """)!;

    /// <summary><c>RESP-PAGOS-GENERICO</c> (ámbito 9; <c>RESPUESTA-PAGOS-GENERICO</c> en §4: el código del formato admite hasta 20 caracteres): línea, documento, cuenta, valor, resultado (<c>Pagado</c>/<c>Rechazado</c>), causal y fecha.</summary>
    public static BankFileFormatDefinition DefinicionDeRespuestaDePagos() => BankFileFormatDefinition.Parse("""
        { "code": "RESP-PAGOS-GENERICO", "name": "Respuesta genérica del banco a un archivo de pagos", "scope": "TreasuryPaymentResponse",
          "validFrom": "2026-01-01", "kind": "Delimited", "delimiter": ";", "quoteText": false, "encoding": "utf-8", "lineEnding": "CRLF",
          "uppercase": false, "stripAccents": false, "amountFormat": "Point2", "fileName": "RESPUESTA.csv", "contentType": "text/csv",
          "notes": "Resultado Pagado o Rechazado por línea. Pendiente de validar con el dueño (feature 015, P1 y P6).",
          "records": {
            "header": { "enabled": true, "fields": [
              { "order": 1, "name": "Línea", "source": "Constant", "value": "linea" },
              { "order": 2, "name": "Documento", "source": "Constant", "value": "documento" },
              { "order": 3, "name": "Cuenta", "source": "Constant", "value": "cuenta" },
              { "order": 4, "name": "Valor", "source": "Constant", "value": "valor" },
              { "order": 5, "name": "Resultado", "source": "Constant", "value": "resultado" },
              { "order": 6, "name": "Causal", "source": "Constant", "value": "causal" },
              { "order": 7, "name": "Fecha", "source": "Constant", "value": "fecha" } ] },
            "detail": { "enabled": true, "fields": [
              { "order": 1, "name": "Línea", "source": "LineNumber", "required": true },
              { "order": 2, "name": "Documento", "source": "BeneficiaryTaxId", "required": true },
              { "order": 3, "name": "Cuenta", "source": "AccountNumber" },
              { "order": 4, "name": "Valor", "source": "Amount", "required": true },
              { "order": 5, "name": "Resultado", "source": "Outcome", "required": true },
              { "order": 6, "name": "Causal", "source": "BankCause" },
              { "order": 7, "name": "Fecha", "source": "BankDate", "format": "yyyy-MM-dd" } ] },
            "trailer": { "enabled": false, "fields": [] } } }
        """)!;

    /// <summary>
    /// <c>RESP-PAGADURIA-GEN</c> (<c>RESPUESTA-PAGADURIA-GENERICA</c> en §4, acortado a 20 caracteres; ámbito 6, elegido por referencia desde el convenio): documento del empleado, código de la pagaduría
    /// (contrato v2) u obligación (v1), descontado, fecha y causal (FR-201).
    /// </summary>
    public static BankFileFormatDefinition DefinicionDeRespuestaDePagaduria() => BankFileFormatDefinition.Parse("""
        { "code": "RESP-PAGADURIA-GEN", "name": "Respuesta genérica de una pagaduría", "scope": "PayrollDeductionResult",
          "validFrom": "2026-01-01", "kind": "Delimited", "delimiter": ";", "quoteText": false, "encoding": "utf-8", "lineEnding": "CRLF",
          "uppercase": false, "stripAccents": false, "amountFormat": "Point2", "fileName": "RESPUESTA_PAGADURIA.csv", "contentType": "text/csv",
          "notes": "Código de la pagaduría (contrato v2) u obligación (v1); una hoja de cálculo se guarda como CSV antes de cargarla. Pendiente de validar con el dueño (feature 015, P1 y P6).",
          "records": {
            "header": { "enabled": true, "fields": [
              { "order": 1, "name": "Documento", "source": "Constant", "value": "documento" },
              { "order": 2, "name": "Código de la pagaduría", "source": "Constant", "value": "codigo_pagaduria" },
              { "order": 3, "name": "Obligación", "source": "Constant", "value": "obligacion" },
              { "order": 4, "name": "Descontado", "source": "Constant", "value": "descontado" },
              { "order": 5, "name": "Fecha", "source": "Constant", "value": "fecha" },
              { "order": 6, "name": "Causal", "source": "Constant", "value": "causal" } ] },
            "detail": { "enabled": true, "fields": [
              { "order": 1, "name": "Documento del empleado", "source": "PayeeDocument", "required": true },
              { "order": 2, "name": "Código de la pagaduría", "source": "PaymasterConceptCode" },
              { "order": 3, "name": "Obligación", "source": "LoanNumber" },
              { "order": 4, "name": "Descontado", "source": "DeductedAmount", "required": true },
              { "order": 5, "name": "Fecha del descuento", "source": "DeductionDate", "format": "yyyy-MM-dd" },
              { "order": 6, "name": "Causal", "source": "ResultCode" } ] },
            "trailer": { "enabled": false, "fields": [] } } }
        """)!;
}
