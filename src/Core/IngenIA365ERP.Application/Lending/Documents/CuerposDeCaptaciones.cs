namespace IngenIA365ERP.Application.Lending.Documents;

// Feature 014, US13 (T195; contracts/documentos.md §2.1 a §2.5; FR-068, FR-101 a FR-105): los cuerpos de los documentos al titular de captaciones que
// faltaban —extracto, certificado de saldo, certificado tributario, certificado de retención, título del CDAT y respuesta al juzgado—. Como los de la 013,
// no calculan nada: todo lo que imprime <c>DocumentosDeCaptacionesReport</c> viene aquí. (nuevo)

/// <summary>Un titular de la cuenta impreso: nombre, documento, papel y proporción. (nuevo)</summary>
public sealed record TitularImpreso(string Nombre, string Documento, string Papel, decimal? Proporcion);

/// <summary>Un saldo por concepto de una cuenta impreso (capital, interés por pagar, aportes por clase…). (nuevo)</summary>
public sealed record ConceptoDeCuentaImpreso(string Concepto, decimal Valor);

/// <summary>Un movimiento del extracto por fecha valor: fecha contable, tipo en español, número, recaudador o canal, valor con signo y saldo. (nuevo)</summary>
public sealed record MovimientoDeExtractoImpreso(
    DateOnly FechaValor,
    DateOnly? FechaContable,
    string Tipo,
    string? Numero,
    string? Canal,
    decimal Valor,
    decimal Saldo,
    string? Reversa);

/// <summary>Los intereses del período: causado, liquidado (bruto) y ajuste de redondeo, con la tasa y el método de saldo. (nuevo)</summary>
public sealed record InteresesDelExtracto(decimal Causado, decimal Liquidado, decimal AjusteDeRedondeo, string? Tasa, string? MetodoDeSaldo);

/// <summary>La retención del período, o por qué no se retuvo. (nuevo)</summary>
public sealed record RetencionDelExtracto(decimal Base, decimal ComponenteInflacionario, string? Tarifa, decimal Retencion, string? SinRetencion);

/// <summary>El GMF del período: cobrado y lo exento usado. (nuevo)</summary>
public sealed record GmfDelExtracto(decimal Cobrado, decimal ExentoUsado, string Politica);

/// <summary>Los cargos del período: cobrados, pendientes y condonados. (nuevo)</summary>
public sealed record CargosDelExtracto(decimal Cobrados, decimal Pendientes, decimal Condonados);

/// <summary>El disponible desglosado (FR-057): saldo − bloqueos − embargos − canje − mínimo. (nuevo)</summary>
public sealed record DisponibleImpreso(decimal Saldo, decimal Bloqueado, decimal Embargado, decimal EnCanje, decimal Minimo, decimal Disponible);

/// <summary>Una cuota exigible de aportes en el período con su estado. (nuevo)</summary>
public sealed record CuotaDelExtractoImpresa(string Periodo, DateOnly Vence, decimal Valor, decimal Pendiente);

/// <summary>
/// El extracto de una cuenta (feature 014, US13; documentos.md §2.1; FR-102): la cuenta con sus titulares y oficina, el período, el saldo inicial por
/// concepto, los movimientos por fecha valor con los reversos enlazados, los intereses, la retención, el GMF, los cargos, el saldo final por concepto, el
/// disponible desglosado y, en aportes, las cuotas exigibles del período. (nuevo)
/// </summary>
public sealed record CuerpoDeExtractoDeCuenta(
    string NumeroDeCuenta,
    string Linea,
    string Clase,
    string Oficina,
    IReadOnlyList<TitularImpreso> Titulares,
    DateOnly Desde,
    DateOnly Hasta,
    decimal SaldoInicial,
    IReadOnlyList<ConceptoDeCuentaImpreso> SaldoInicialPorConcepto,
    IReadOnlyList<MovimientoDeExtractoImpreso> Movimientos,
    InteresesDelExtracto? Intereses,
    RetencionDelExtracto? Retencion,
    GmfDelExtracto? Gmf,
    CargosDelExtracto? Cargos,
    decimal SaldoFinal,
    IReadOnlyList<ConceptoDeCuentaImpreso> SaldoFinalPorConcepto,
    DisponibleImpreso Disponible,
    IReadOnlyList<CuotaDelExtractoImpresa> Cuotas) : CuerpoDeDocumentoDeCartera;

/// <summary>Un bloqueo o embargo vigente impreso en el certificado de saldo (sólo si el titular lo pide). (nuevo)</summary>
public sealed record RestriccionImpresa(string Clase, string Detalle, decimal Valor);

/// <summary>
/// El certificado de saldo de una cuenta a la fecha D (feature 014, US13; documentos.md §2.2; FR-103): estado, apertura, titulares, manejo, saldo por
/// concepto y del titular, bloqueos y embargos si se piden, con la leyenda «certifica el saldo registrado al {D} con la información disponible al {R}». (nuevo)
/// </summary>
public sealed record CuerpoDeCertificadoDeSaldoDeCuenta(
    string NumeroDeCuenta,
    string Linea,
    string Clase,
    string Estado,
    DateOnly Apertura,
    string Manejo,
    IReadOnlyList<TitularImpreso> Titulares,
    DateOnly Fecha,
    IReadOnlyList<ConceptoDeCuentaImpreso> PorConcepto,
    decimal SaldoDelTitular,
    IReadOnlyList<RestriccionImpresa>? Restricciones,
    string HuellaDelSaldo) : CuerpoDeDocumentoDeCartera
{
    public const string LeyendaDelCorte = "Certifica el saldo registrado al {0} con la información disponible al {1}.";
}

/// <summary>Una cuenta del certificado tributario impresa: lo de la persona por su proporción. (nuevo)</summary>
public sealed record CuentaTributariaImpresa(
    string Numero,
    string Clase,
    decimal Proporcion,
    decimal SaldoAl31DeDiciembre,
    decimal Rendimientos,
    decimal ComponenteInflacionario,
    decimal Retencion,
    decimal Gmf,
    decimal Revalorizacion);

/// <summary>
/// El certificado tributario anual de captaciones (feature 014, US13; documentos.md §2.3; FR-104; SC-016): el agente retenedor, el año gravable, por cuenta
/// el saldo al 31 de diciembre, los rendimientos abonados, la retención, el componente inflacionario (no constitutivo de renta) y el GMF, por la proporción
/// de la persona; los totales, y los aportes aparte si la cooperativa los incluye. (nuevo)
/// </summary>
public sealed record CuerpoDeCertificadoTributarioDeCaptaciones(
    short Año,
    string AgenteRetenedor,
    string? NitDelAgente,
    IReadOnlyList<CuentaTributariaImpresa> Captaciones,
    IReadOnlyList<CuentaTributariaImpresa> Aportes,
    decimal TotalSaldo,
    decimal TotalRendimientos,
    decimal TotalComponenteInflacionario,
    decimal TotalRetencion,
    decimal TotalGmf,
    bool IncluyeAportes) : CuerpoDeDocumentoDeCartera
{
    public const string LeyendaDelComponente =
        "El componente inflacionario de los rendimientos financieros no constituye renta ni ganancia ocasional (ET arts. 38 a 41).";

    public const string LeyendaDeProporcion =
        "En las cuentas con varios titulares cada uno recibe su certificado por la proporción registrada; sin proporción, por partes iguales.";
}

/// <summary>Un renglón del certificado de retención impreso (concepto «rendimientos financieros» de una clase de captación, a una tarifa). (nuevo)</summary>
public sealed record RenglonDeRetencionImpreso(string Concepto, string? Tarifa, decimal Rendimientos, decimal ComponenteInflacionario, decimal Base,
    decimal Retencion);

/// <summary>
/// El certificado de retención en la fuente de un período (feature 014, US13; documentos.md §2.4; FR-105; ET art. 381; DUR 1625 de 2016 art.
/// 1.6.1.13.2.40): el agente retenedor con su NIT y dirección, el retenido, el período, el concepto, la base sometida, la tarifa, el valor retenido, el
/// componente inflacionario descontado y la ciudad de la consignación si se conoce. (nuevo)
/// </summary>
public sealed record CuerpoDeCertificadoDeRetencion(
    string AgenteRetenedor,
    string? NitDelAgente,
    string? DireccionDelAgente,
    string? Ciudad,
    DateOnly Desde,
    DateOnly Hasta,
    IReadOnlyList<RenglonDeRetencionImpreso> Renglones,
    decimal TotalBase,
    decimal TotalRetencion) : CuerpoDeDocumentoDeCartera;

/// <summary>
/// El título del CDAT (feature 014, US13; documentos.md §2.5; FR-068): número con su versión, cuenta, título anterior, titulares y manejo, las condiciones
/// selladas, las reglas del reglamento, la leyenda de retención y GMF y la del seguro de depósitos si la cooperativa está inscrita. Lleva destacada la
/// leyenda «CDAT — NO NEGOCIABLE». (nuevo)
/// </summary>
public sealed record CuerpoDeTituloDeCdat(
    string NumeroDelTitulo,
    string NumeroDeCuenta,
    string Linea,
    int Version,
    string? TituloAnterior,
    string Causa,
    IReadOnlyList<TitularImpreso> Titulares,
    string Manejo,
    decimal Capital,
    DateOnly Constitucion,
    DateOnly InicioDeLaVersion,
    int PlazoEnDias,
    DateOnly Vencimiento,
    decimal TasaEfectivaAnual,
    decimal? TasaNominal,
    decimal TasaDeLaTabla,
    decimal? MargenNegociado,
    string Periodicidad,
    string Modalidad,
    string DestinoDeLosIntereses,
    IReadOnlyList<string> Reglas,
    string? SeguroDeDepositos) : CuerpoDeDocumentoDeCartera
{
    public const string LeyendaNoNegociable = "CDAT — NO NEGOCIABLE";

    public const string LeyendaDeLegitimacion =
        "Título de legitimación; no es título valor (Código de Comercio, art. 645). Se entrega como constancia del depósito y no se endosa.";

    public const string LeyendaTributaria = "Los rendimientos están sujetos a retención en la fuente y el GMF según la norma vigente.";
}

/// <summary>
/// La respuesta al juzgado por un embargo (feature 014, US13; data-model §5.2.1; research E158, E176; FR-055): la autoridad, el oficio, el proceso, el
/// demandante, la clase de respuesta, el texto enviado, lo retenido y consignado a la fecha y la fecha de envío. (nuevo)
/// </summary>
public sealed record CuerpoDeRespuestaAlJuzgado(
    string Clase,
    string Autoridad,
    string Oficio,
    string? Proceso,
    string Demandante,
    DateOnly FechaDelOficio,
    DateOnly FechaDeEnvio,
    string Texto,
    decimal? Retenido,
    decimal? Consignado) : CuerpoDeDocumentoDeCartera;
