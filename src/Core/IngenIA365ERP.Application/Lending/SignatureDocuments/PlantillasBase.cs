using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.SignatureDocuments;

/// <summary>Una plantilla base: lo que se copia a la cooperativa al cargarlas.</summary>
public sealed record PlantillaBase(
    SignatureDocumentKind Clase,
    string Codigo,
    string Nombre,
    string Texto,
    IReadOnlyList<RepaymentChannel> Canales,
    int Orden,
    bool Huella,
    bool FirmanCodeudores);

/// <summary>Un campo propio base.</summary>
public sealed record CampoBase(
    string Codigo,
    string Nombre,
    SignatureFieldKind Tipo,
    SignatureFieldSource Origen,
    string? Valor,
    string Ayuda,
    int Orden);

/// <summary>
/// Las plantillas base de los documentos para firma (2026-11-18, pedido del dueño): el pagaré con espacios en blanco, su carta de instrucciones y la
/// libranza, escritos sobre el Código de Comercio (arts. 621, 622, 709 a 711), la Ley 1527 de 2012 y el art. 142 de la Ley 79 de 1988 (ver
/// <c>docs/manual/cartera-documentos-para-firma.md</c>). Son un punto de partida: <b>no están validadas por un abogado</b> —se cargan sin validar y
/// cada cooperativa las ajusta y las valida— y cargarlas no pisa nada: sólo agrega lo que la cooperativa no tiene por código. (nuevo)
/// </summary>
public static class PlantillasBase
{
    public const string CodigoPagare = "PAGARE";
    public const string CodigoCarta = "CARTA-INSTR";
    public const string CodigoLibranza = "LIBRANZA";

    public const string CiudadDePago = "CIUDAD_PAGO";
    public const string DiasDeMoraParaLlenar = "DIAS_MORA_LLENADO";
    public const string RegistroRuneol = "REGISTRO_RUNEOL";

    public static IReadOnlyList<CampoBase> Campos { get; } =
    [
        new(CiudadDePago, "Ciudad donde se paga el pagaré", SignatureFieldKind.Text, SignatureFieldSource.Constant, null,
            "El lugar de cumplimiento del pagaré (C. de Co. art. 621): normalmente el domicilio principal de la cooperativa.", 1),
        new(DiasDeMoraParaLlenar, "Días de mora para llenar el pagaré", SignatureFieldKind.Number, SignatureFieldSource.Constant, null,
            "Desde cuántos días de mora la carta de instrucciones permite llenar el pagaré. Lo fija la política de cartera de la cooperativa.", 2),
        new(RegistroRuneol, "Código de la cooperativa en el RUNEOL", SignatureFieldKind.Text, SignatureFieldSource.Constant, null,
            "El código de inscripción como entidad operadora de libranza en el Registro Único Nacional de Entidades Operadoras de Libranza. "
            + "Si está vacío, la libranza no lo menciona.", 3),
    ];

    public static IReadOnlyList<PlantillaBase> Plantillas { get; } =
    [
        new(SignatureDocumentKind.PromissoryNote, CodigoPagare, "Pagaré con espacios en blanco", Pagare, [], 1, Huella: true, FirmanCodeudores: true),
        new(SignatureDocumentKind.InstructionLetter, CodigoCarta, "Carta de instrucciones del pagaré", Carta, [], 1, Huella: true, FirmanCodeudores: true),
        new(SignatureDocumentKind.PayrollDeductionAuthorization, CodigoLibranza, "Libranza (autorización de descuento por nómina)", Libranza,
            [RepaymentChannel.OwnPayroll, RepaymentChannel.PaymasterDeduction], 1, Huella: true, FirmanCodeudores: false),
    ];

    private const string Pagare = """
        # PAGARÉ No. {{PAGARE_NUMERO}}

        = Valor: ${{BLANCO:20}}

        Nosotros, **{{DEUDOR_NOMBRE:MAYUSCULAS}}**, mayor de edad, identificado(a) con {{DEUDOR_TIPO_DOCUMENTO}} No. {{DEUDOR_DOCUMENTO}} expedida en {{DEUDOR_EXPEDICION}}, domiciliado(a) en {{DEUDOR_CIUDAD}}, en calidad de deudor(a){{#SI HAY_CODEUDORES}}, y {{CODEUDORES_NOMBRES}}, identificados como aparece al pie de nuestras firmas, en calidad de codeudores{{/SI}}, declaramos:

        **PRIMERA. Promesa de pago.** Que por virtud del presente título valor pagaremos incondicional, solidaria e indivisiblemente a la orden de **{{COOPERATIVA_NOMBRE}}**, NIT {{COOPERATIVA_NIT}}, o de quien represente sus derechos o sea su legítimo tenedor, en la ciudad de {{CIUDAD_PAGO}}, la suma de {{BLANCO:45}} pesos moneda legal colombiana (${{BLANCO:15}}).

        **SEGUNDA. Vencimiento.** Que pagaremos la suma indicada el día {{BLANCO:4}} del mes de {{BLANCO:14}} del año {{BLANCO:6}}.

        **TERCERA. Intereses.** Que sobre el capital reconoceremos intereses remuneratorios a la tasa del {{BLANCO:8}} por ciento efectivo anual y, en caso de mora, intereses moratorios a la tasa máxima legal permitida, sin exceder el límite de usura, desde el día siguiente al vencimiento y hasta el pago total.

        **CUARTA. Cláusula aceleratoria.** Que el tenedor podrá declarar vencido el plazo y exigir de inmediato el pago total de la obligación, con sus intereses, en cualquiera de estos casos: a) mora en el pago de una o más cuotas de capital o de intereses de cualquier obligación a nuestro cargo y a favor de {{COOPERATIVA_NOMBRE}}; b) si cualquiera de los firmantes es embargado o perseguido judicialmente; c) si cualquiera de los firmantes inicia un proceso de insolvencia, reorganización o liquidación; d) si se comprueba falsedad o inexactitud en la información o en los documentos presentados para obtener el crédito; e) si el deudor pierde la calidad de asociado o, siendo el crédito pagadero por libranza, se retira de su empleo y no paga directamente las cuotas; f) la muerte del deudor, caso en el cual el tenedor podrá exigir el pago a los herederos y a los codeudores.

        **QUINTA. Gastos.** Que serán de nuestro cargo los gastos de cobranza judicial y extrajudicial, incluidos los honorarios de abogado en los términos que la ley permita, y el impuesto de timbre si se causare.

        **SEXTA. Renuncias.** Que renunciamos a la presentación para el pago, al aviso de rechazo y al protesto, y a los requerimientos judiciales y extrajudiciales para la constitución en mora.

        **SÉPTIMA. Espacios en blanco.** Que este pagaré se otorga con espacios en blanco, que el tenedor podrá llenar sin previo aviso conforme a la carta de instrucciones que suscribimos en esta misma fecha y al artículo 622 del Código de Comercio.

        **OCTAVA. Prórrogas y garantías.** Que aceptamos desde ahora cualquier prórroga, ampliación o modificación del plazo que el tenedor conceda a cualquiera de los firmantes, sin que se requiera nuestro consentimiento, y que la solidaridad subsiste en ese caso. Esta obligación se respalda además con las garantías que se hayan constituido a favor de {{COOPERATIVA_NOMBRE}}.

        Para constancia se firma en {{OFICINA_CIUDAD}}, el {{FECHA_LARGA}}.

        {{FIRMAS}}
        """;

    private const string Carta = """
        # CARTA DE INSTRUCCIONES

        = Para llenar el pagaré No. {{PAGARE_NUMERO}}

        {{OFICINA_CIUDAD}}, {{FECHA_LARGA}}

        Señores
        **{{COOPERATIVA_NOMBRE}}**
        NIT {{COOPERATIVA_NIT}}

        Nosotros, **{{DEUDOR_NOMBRE:MAYUSCULAS}}**, identificado(a) con {{DEUDOR_TIPO_DOCUMENTO}} No. {{DEUDOR_DOCUMENTO}}{{#SI HAY_CODEUDORES}}, y {{CODEUDORES_NOMBRES}}{{/SI}}, de conformidad con el artículo 622 del Código de Comercio, los autorizamos de manera expresa e irrevocable para que, sin previo aviso, llenen los espacios en blanco del pagaré No. {{PAGARE_NUMERO}}, que hemos otorgado a su favor en esta misma fecha, conforme a las siguientes instrucciones:

        - **Cuantía:** será igual al valor de todas las obligaciones exigibles que a la fecha en que se llene tengamos a favor de {{COOPERATIVA_NOMBRE}}, por capital, intereses remuneratorios y moratorios, seguros, primas, comisiones y demás cargos pactados, y los gastos de cobranza; entre ellas, el crédito de la solicitud No. {{SOLICITUD_NUMERO}} por {{MONTO}} ({{MONTO_LETRAS}}).
        - **Fecha de vencimiento:** será la del día en que el pagaré se llene.
        - **Tasa de interés:** los intereses remuneratorios a la tasa pactada para cada obligación, que para el crédito de la solicitud No. {{SOLICITUD_NUMERO}} es del {{TASA_EFECTIVA_ANUAL}} efectivo anual; los moratorios, a la tasa máxima legal permitida.
        - **Lugar de pago:** la ciudad de {{CIUDAD_PAGO}}.

        El pagaré podrá llenarse cuando ocurra cualquiera de estos eventos: a) mora de más de {{DIAS_MORA_LLENADO}} días en el pago de cualquier cuota de capital o de intereses de cualquiera de nuestras obligaciones; b) cualquiera de los casos de la cláusula aceleratoria del pagaré; c) el retiro o la exclusión del deudor como asociado de {{COOPERATIVA_NOMBRE}} quedando obligaciones a su cargo.

        Declaramos que recibimos copia de esta carta de instrucciones y del pagaré con espacios en blanco al que se refiere.

        {{FIRMAS}}
        """;

    private const string Libranza = """
        # AUTORIZACIÓN DE DESCUENTO POR LIBRANZA

        = Ley 1527 de 2012 y artículo 142 de la Ley 79 de 1988

        {{OFICINA_CIUDAD}}, {{FECHA_LARGA}}

        Señores
        **{{PAGADURIA_NOMBRE}}**
        NIT {{PAGADURIA_NIT}}
        Pagaduría o área de nómina

        Yo, **{{DEUDOR_NOMBRE:MAYUSCULAS}}**, identificado(a) con {{DEUDOR_TIPO_DOCUMENTO}} No. {{DEUDOR_DOCUMENTO}}{{#SI PAGADURIA_CODIGO_EMPLEADO}}, código de empleado {{PAGADURIA_CODIGO_EMPLEADO}}{{/SI}}, en ejercicio del derecho que me confiere la Ley 1527 de 2012, autorizo de manera expresa e irrevocable a la entidad pagadora para que descuente de mi salario, honorarios o pensión las sumas que se indican a continuación y las gire a favor de **{{COOPERATIVA_NOMBRE}}**, NIT {{COOPERATIVA_NIT}}{{#SI REGISTRO_RUNEOL}}, entidad operadora de libranza inscrita en el RUNEOL con el código {{REGISTRO_RUNEOL}}{{/SI}}:

        - **Obligación:** crédito de la solicitud No. {{SOLICITUD_NUMERO}}, línea {{LINEA_NOMBRE}}, respaldado con el pagaré No. {{PAGARE_NUMERO}}.
        - **Valor del crédito:** {{MONTO}} ({{MONTO_LETRAS}}).
        - **Valor de cada cuota:** {{VALOR_CUOTA}} ({{VALOR_CUOTA_LETRAS}}), con periodicidad {{PERIODICIDAD}}.
        - **Número de cuotas:** {{NUMERO_CUOTAS}}, desde el {{FECHA_PRIMERA_CUOTA}} hasta el pago total de la obligación.
        - **Tasa de interés remuneratorio:** {{TASA_EFECTIVA_ANUAL}} efectivo anual.

        Autorizo además que, si me retiro o me desvinculan antes del pago total, se descuente el saldo pendiente de las sumas que me correspondan en la liquidación final de salarios y prestaciones sociales, en los términos y con los límites que la ley permita, y que, si cambio de empleador o de entidad pagadora, esta autorización se presente ante el nuevo para que continúe el descuento.

        Declaro que conozco las condiciones del crédito, que esta autorización no puede revocarse unilateralmente mientras exista saldo a mi cargo y que, de acuerdo con la ley, el descuento no puede llevarme a recibir menos del cincuenta por ciento (50 %) del neto de mi salario o pensión después de los descuentos de ley.

        {{FIRMAS}}

        ## Constancia de recibo de la entidad pagadora

        Fecha de recibo: {{BLANCO:25}}

        Nombre y cargo de quien recibe: {{BLANCO:40}}

        Firma y sello: {{BLANCO:40}}
        """;
}
