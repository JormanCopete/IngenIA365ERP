namespace IngenIA365ERP.Domain.Lending.SignatureDocuments;

/// <summary>Un campo que el sistema llena solo en los documentos para firma: su código, qué dice, su grupo y un ejemplo para la vista previa.</summary>
public sealed record CampoDelSistema(string Codigo, string Descripcion, string Grupo, string Ejemplo);

/// <summary>Una lista que se recorre con <c>{{#CADA LISTA}}</c>: sus campos por elemento y dos elementos de ejemplo.</summary>
public sealed record ListaDelSistema(
    string Codigo,
    string Descripcion,
    IReadOnlyList<CampoDelSistema> Campos,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Ejemplo);

/// <summary>
/// Los campos que el sistema llena solos en los documentos para firma (2026-11-18, pedido del dueño): la cooperativa, la fecha, la solicitud con sus
/// condiciones aprobadas, el pagaré, el deudor, los codeudores y la pagaduría. Cada cooperativa agrega los suyos (<c>LND_SignatureDocumentFields</c>).
/// Los códigos son fijos: una plantilla guardada los nombra, así que se agregan, no se renombran. (nuevo)
/// </summary>
public static class CamposDelSistema
{
    // Especiales: no son valores.
    /// <summary>Un espacio en blanco para llenar a mano (<c>{{BLANCO}}</c> o <c>{{BLANCO:40}}</c>, el largo en guiones bajos).</summary>
    public const string Blanco = "BLANCO";

    /// <summary>El lugar de las firmas del deudor y los codeudores, solo en su renglón.</summary>
    public const string Firmas = "FIRMAS";

    public const string CooperativaNombre = "COOPERATIVA_NOMBRE";
    public const string CooperativaNit = "COOPERATIVA_NIT";
    public const string Oficina = "OFICINA";
    public const string OficinaCiudad = "OFICINA_CIUDAD";

    public const string Fecha = "FECHA";
    public const string FechaLarga = "FECHA_LARGA";
    public const string FechaDia = "FECHA_DIA";
    public const string FechaDiaLetras = "FECHA_DIA_LETRAS";
    public const string FechaMes = "FECHA_MES";
    public const string FechaAnio = "FECHA_ANIO";

    public const string SolicitudNumero = "SOLICITUD_NUMERO";
    public const string LineaCodigo = "LINEA_CODIGO";
    public const string LineaNombre = "LINEA_NOMBRE";
    public const string Destino = "DESTINO";
    public const string Monto = "MONTO";
    public const string MontoLetras = "MONTO_LETRAS";
    public const string PlazoMeses = "PLAZO_MESES";
    public const string NumeroCuotas = "NUMERO_CUOTAS";
    public const string Periodicidad = "PERIODICIDAD";
    public const string SistemaAmortizacion = "SISTEMA_AMORTIZACION";
    public const string ValorCuota = "VALOR_CUOTA";
    public const string ValorCuotaLetras = "VALOR_CUOTA_LETRAS";
    public const string TasaEfectivaAnual = "TASA_EFECTIVA_ANUAL";
    public const string TasaMensual = "TASA_MENSUAL";
    public const string FechaPrimeraCuota = "FECHA_PRIMERA_CUOTA";
    public const string FechaUltimaCuota = "FECHA_ULTIMA_CUOTA";
    public const string FechaDesembolso = "FECHA_DESEMBOLSO";
    public const string CanalDePago = "CANAL_DE_PAGO";
    public const string EsLibranza = "ES_LIBRANZA";
    public const string PagareNumero = "PAGARE_NUMERO";

    public const string DeudorNombre = "DEUDOR_NOMBRE";
    public const string DeudorTipoDocumento = "DEUDOR_TIPO_DOCUMENTO";
    public const string DeudorTipoDocumentoNombre = "DEUDOR_TIPO_DOCUMENTO_NOMBRE";
    public const string DeudorDocumento = "DEUDOR_DOCUMENTO";
    public const string DeudorExpedicion = "DEUDOR_EXPEDICION";
    public const string DeudorDireccion = "DEUDOR_DIRECCION";
    public const string DeudorCiudad = "DEUDOR_CIUDAD";
    public const string DeudorTelefono = "DEUDOR_TELEFONO";
    public const string DeudorCorreo = "DEUDOR_CORREO";

    public const string HayCodeudores = "HAY_CODEUDORES";
    public const string CodeudoresNombres = "CODEUDORES_NOMBRES";

    public const string PagaduriaNombre = "PAGADURIA_NOMBRE";
    public const string PagaduriaNit = "PAGADURIA_NIT";
    public const string PagaduriaCodigoEmpleado = "PAGADURIA_CODIGO_EMPLEADO";

    public const string Codeudores = "CODEUDORES";
    public const string CodeudorNombre = "CODEUDOR_NOMBRE";
    public const string CodeudorTipoDocumento = "CODEUDOR_TIPO_DOCUMENTO";
    public const string CodeudorDocumento = "CODEUDOR_DOCUMENTO";
    public const string CodeudorExpedicion = "CODEUDOR_EXPEDICION";
    public const string CodeudorDireccion = "CODEUDOR_DIRECCION";
    public const string CodeudorCiudad = "CODEUDOR_CIUDAD";
    public const string CodeudorTelefono = "CODEUDOR_TELEFONO";

    public const string Firmantes = "FIRMANTES";
    public const string FirmanteNombre = "FIRMANTE_NOMBRE";
    public const string FirmanteTipoDocumento = "FIRMANTE_TIPO_DOCUMENTO";
    public const string FirmanteDocumento = "FIRMANTE_DOCUMENTO";
    public const string FirmanteCalidad = "FIRMANTE_CALIDAD";

    /// <summary>Dentro de un <c>{{#CADA}}</c>: el número del elemento (1, 2, 3…).</summary>
    public const string Orden = "ORDEN";

    /// <summary>El sufijo que da en letras un campo propio en pesos.</summary>
    public const string SufijoLetras = "_LETRAS";

    public static IReadOnlyList<CampoDelSistema> Campos { get; } =
    [
        new(CooperativaNombre, "Nombre de la cooperativa", "Cooperativa", "COOPERATIVA DE EJEMPLO"),
        new(CooperativaNit, "NIT de la cooperativa con su dígito", "Cooperativa", "900123456-7"),
        new(Oficina, "Oficina (agencia) de la solicitud", "Cooperativa", "Principal"),
        new(OficinaCiudad, "Ciudad de la oficina de la solicitud", "Cooperativa", "Florida"),

        new(Fecha, "Fecha de generación (dd/mm/aaaa)", "Fecha", "18/11/2026"),
        new(FechaLarga, "Fecha de generación en palabras", "Fecha", "18 de noviembre de 2026"),
        new(FechaDia, "Día de la fecha de generación", "Fecha", "18"),
        new(FechaDiaLetras, "Día de la fecha de generación en letras", "Fecha", "dieciocho"),
        new(FechaMes, "Mes de la fecha de generación en letras", "Fecha", "noviembre"),
        new(FechaAnio, "Año de la fecha de generación", "Fecha", "2026"),

        new(SolicitudNumero, "Número (radicado) de la solicitud", "Crédito", "SC000123"),
        new(LineaCodigo, "Código de la línea de crédito", "Crédito", "EMPL15"),
        new(LineaNombre, "Nombre de la línea de crédito", "Crédito", "Crédito de consumo"),
        new(Destino, "Destino del crédito", "Crédito", "Libre inversión"),
        new(Monto, "Monto aprobado en pesos", "Crédito", "$ 5.000.000"),
        new(MontoLetras, "Monto aprobado en letras", "Crédito", "CINCO MILLONES DE PESOS M/CTE"),
        new(PlazoMeses, "Plazo en meses", "Crédito", "24"),
        new(NumeroCuotas, "Número de cuotas aprobadas", "Crédito", "24"),
        new(Periodicidad, "Periodicidad de las cuotas", "Crédito", "mensual"),
        new(SistemaAmortizacion, "Sistema de amortización", "Crédito", "cuota fija"),
        new(ValorCuota, "Valor de la cuota (la primera que amortiza)", "Crédito", "$ 264.380"),
        new(ValorCuotaLetras, "Valor de la cuota en letras", "Crédito", "DOSCIENTOS SESENTA Y CUATRO MIL TRESCIENTOS OCHENTA PESOS M/CTE"),
        new(TasaEfectivaAnual, "Tasa de interés remuneratorio efectiva anual", "Crédito", "24,00 %"),
        new(TasaMensual, "Tasa de interés remuneratorio mes vencido equivalente", "Crédito", "1,81 %"),
        new(FechaPrimeraCuota, "Fecha de la primera cuota", "Crédito", "30/12/2026"),
        new(FechaUltimaCuota, "Fecha de la última cuota", "Crédito", "30/11/2028"),
        new(FechaDesembolso, "Fecha prevista del desembolso", "Crédito", "20/11/2026"),
        new(CanalDePago, "Cómo se paga el crédito", "Crédito", "descuento por nómina"),
        new(EsLibranza, "«SI» si el crédito se paga por nómina (propia o de una pagaduría)", "Crédito", "SI"),
        new(PagareNumero, "Número del pagaré", "Crédito", "SC000123"),

        new(DeudorNombre, "Nombre completo del deudor", "Deudor", "MARÍA FERNANDA PÉREZ GÓMEZ"),
        new(DeudorTipoDocumento, "Sigla del documento del deudor", "Deudor", "C.C."),
        new(DeudorTipoDocumentoNombre, "Tipo de documento del deudor en palabras", "Deudor", "cédula de ciudadanía"),
        new(DeudorDocumento, "Número del documento del deudor", "Deudor", "31.234.567"),
        new(DeudorExpedicion, "Lugar de expedición del documento del deudor", "Deudor", "Cali"),
        new(DeudorDireccion, "Dirección del deudor", "Deudor", "Calle 10 # 5-20"),
        new(DeudorCiudad, "Ciudad del deudor", "Deudor", "Florida"),
        new(DeudorTelefono, "Teléfono o celular del deudor", "Deudor", "3001234567"),
        new(DeudorCorreo, "Correo del deudor", "Deudor", "maria@correo.com"),

        new(HayCodeudores, "«SI» si la solicitud tiene codeudores", "Codeudores", "SI"),
        new(CodeudoresNombres, "Nombres de los codeudores separados por coma", "Codeudores", "JUAN CARLOS RUIZ, ANA LÓPEZ"),

        new(PagaduriaNombre, "Empleador o pagaduría que descuenta (la cooperativa si es su nómina)", "Pagaduría", "ALCALDÍA DE FLORIDA"),
        new(PagaduriaNit, "NIT del empleador o pagaduría", "Pagaduría", "891380033-1"),
        new(PagaduriaCodigoEmpleado, "Identificación del empleado ante la pagaduría", "Pagaduría", "E-1045"),
    ];

    private static readonly IReadOnlyDictionary<string, string?> CodeudorUno = new Dictionary<string, string?>
    {
        [CodeudorNombre] = "JUAN CARLOS RUIZ", [CodeudorTipoDocumento] = "C.C.", [CodeudorDocumento] = "16.789.012",
        [CodeudorExpedicion] = "Palmira", [CodeudorDireccion] = "Carrera 3 # 8-15", [CodeudorCiudad] = "Florida",
        [CodeudorTelefono] = "3109876543", [Orden] = "1",
    };

    private static readonly IReadOnlyDictionary<string, string?> CodeudorDos = new Dictionary<string, string?>
    {
        [CodeudorNombre] = "ANA LÓPEZ", [CodeudorTipoDocumento] = "C.C.", [CodeudorDocumento] = "29.456.789", [CodeudorExpedicion] = "Florida",
        [CodeudorDireccion] = "Calle 4 # 12-30", [CodeudorCiudad] = "Florida", [CodeudorTelefono] = "3157654321", [Orden] = "2",
    };

    public static IReadOnlyList<ListaDelSistema> Listas { get; } =
    [
        new(Codeudores, "Los codeudores de la solicitud",
        [
            new(CodeudorNombre, "Nombre completo del codeudor", "Codeudores", "JUAN CARLOS RUIZ"),
            new(CodeudorTipoDocumento, "Sigla del documento del codeudor", "Codeudores", "C.C."),
            new(CodeudorDocumento, "Número del documento del codeudor", "Codeudores", "16.789.012"),
            new(CodeudorExpedicion, "Lugar de expedición del documento del codeudor", "Codeudores", "Palmira"),
            new(CodeudorDireccion, "Dirección del codeudor", "Codeudores", "Carrera 3 # 8-15"),
            new(CodeudorCiudad, "Ciudad del codeudor", "Codeudores", "Florida"),
            new(CodeudorTelefono, "Teléfono o celular del codeudor", "Codeudores", "3109876543"),
            new(Orden, "Número del codeudor (1, 2…)", "Codeudores", "1"),
        ], [CodeudorUno, CodeudorDos]),
        new(Firmantes, "Quienes firman: el deudor y los codeudores",
        [
            new(FirmanteNombre, "Nombre completo de quien firma", "Firmantes", "MARÍA FERNANDA PÉREZ GÓMEZ"),
            new(FirmanteTipoDocumento, "Sigla del documento de quien firma", "Firmantes", "C.C."),
            new(FirmanteDocumento, "Número del documento de quien firma", "Firmantes", "31.234.567"),
            new(FirmanteCalidad, "Deudor o codeudor", "Firmantes", "Deudor"),
            new(Orden, "Número del firmante (1, 2…)", "Firmantes", "1"),
        ],
        [
            new Dictionary<string, string?> { [FirmanteNombre] = "MARÍA FERNANDA PÉREZ GÓMEZ", [FirmanteTipoDocumento] = "C.C.", [FirmanteDocumento] = "31.234.567", [FirmanteCalidad] = "Deudor", [Orden] = "1" },
            new Dictionary<string, string?> { [FirmanteNombre] = "JUAN CARLOS RUIZ", [FirmanteTipoDocumento] = "C.C.", [FirmanteDocumento] = "16.789.012", [FirmanteCalidad] = "Codeudor", [Orden] = "2" },
        ]),
    ];

    /// <summary>Los códigos reservados: los del sistema, sus listas, los de las listas y los especiales. Un campo propio no puede llamarse así.</summary>
    public static IReadOnlySet<string> Reservados { get; } = Campos.Select(c => c.Codigo)
        .Concat(Listas.Select(l => l.Codigo)).Concat(Listas.SelectMany(l => l.Campos.Select(c => c.Codigo)))
        .Append(Blanco).Append(Firmas).ToHashSet(StringComparer.Ordinal);

    /// <summary>Los datos de ejemplo de la vista previa: todos los campos del sistema con su ejemplo y las dos listas.</summary>
    public static DatosDePlantilla Ejemplo() => new(
        Campos.ToDictionary(c => c.Codigo, c => (string?)c.Ejemplo, StringComparer.Ordinal),
        Listas.ToDictionary(l => l.Codigo, l => l.Ejemplo, StringComparer.Ordinal));

    /// <summary>¿El texto es un código válido para un campo propio? Mayúscula inicial, después mayúsculas, números o guion bajo; de 2 a 40.</summary>
    public static bool CodigoValido(string? codigo) =>
        codigo is { Length: >= 2 and <= 40 } && codigo[0] is >= 'A' and <= 'Z' && codigo.All(c => c is (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_');
}
