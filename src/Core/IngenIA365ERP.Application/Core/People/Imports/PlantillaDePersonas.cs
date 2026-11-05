using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Imports;

namespace IngenIA365ERP.Application.Core.People.Imports;

/// <summary>
/// La plantilla <c>core.people</c> (feature 016, US1; contracts/plantillas.md §2): personas naturales y jurídicas con las reglas
/// del alta unitaria de Maestros › Personas. La llave es el <b>usuario</b> que deriva la 017 de tipo y número
/// (<c>UsuarioDeDocumento</c>): CC 123 y CE 123 son dos personas. Crea o actualiza sólo lo que trae valor; nunca borra ni
/// escribe las marcas de asociado, empleado o vendedor. Las cuentas bancarias van por la plantilla de la 015 (D10). (nuevo)
/// </summary>
public static class PlantillaDePersonas
{
    public const string Clave = "core.people";

    public const string TipoDocumento = "tipoDocumento";
    public const string Documento = "documento";
    public const string DigitoVerificacion = "digitoVerificacion";
    public const string Naturaleza = "naturaleza";
    public const string PrimerNombre = "primerNombre";
    public const string OtrosNombres = "otrosNombres";
    public const string PrimerApellido = "primerApellido";
    public const string SegundoApellido = "segundoApellido";
    public const string RazonSocial = "razonSocial";
    public const string LugarExpedicion = "lugarExpedicion";
    public const string FechaExpedicion = "fechaExpedicion";
    public const string Correo = "correo";
    public const string Telefono = "telefono";
    public const string Telefono2 = "telefono2";
    public const string Celular = "celular";
    public const string Direccion = "direccion";
    public const string Ciudad = "ciudad";
    public const string FechaNacimiento = "fechaNacimiento";
    public const string Genero = "genero";
    public const string EstadoCivil = "estadoCivil";
    public const string NivelEducativo = "nivelEducativo";
    public const string EsProveedor = "esProveedor";
    public const string EsCliente = "esCliente";
    public const string ResponsableIva = "responsableIva";
    public const string Autorretenedor = "autorretenedor";
    public const string AgenteRetencionIva = "agenteRetencionIva";
    public const string RegimenSimple = "regimenSimple";
    public const string DeclaranteRenta = "declaranteRenta";
    public const string ObligadoFacturar = "obligadoFacturar";
    public const string ObligadoContabilidad = "obligadoContabilidad";
    public const string GranContribuyente = "granContribuyente";
    public const string ExentoRetencion = "exentoRetencion";
    public const string ExentoReteIca = "exentoReteIca";
    public const string Ciiu = "ciiu";

    /// <summary><c>COR_People.PersonType</c>: lo que escribe el formulario de persona.</summary>
    public const string TipoNatural = "01";
    public const string TipoJuridica = "02";

    /// <summary>Los catálogos cerrados del formulario de persona (<c>CatalogosDePersona</c> en Shared): código o nombre.</summary>
    public static readonly IReadOnlyDictionary<string, string> Generos = new Dictionary<string, string>
    {
        ["M"] = "Masculino", ["F"] = "Femenino", ["O"] = "Otro",
    };

    public static readonly IReadOnlyDictionary<string, string> EstadosCiviles = new Dictionary<string, string>
    {
        ["S"] = "Soltero", ["C"] = "Casado", ["U"] = "Unión libre", ["D"] = "Divorciado", ["V"] = "Viudo",
    };

    public static readonly IReadOnlyDictionary<string, string> NivelesEducativos = new Dictionary<string, string>
    {
        ["PR"] = "Primaria", ["BA"] = "Bachillerato", ["TE"] = "Técnico", ["TG"] = "Tecnológico", ["PG"] = "Profesional",
        ["ES"] = "Especialización", ["MA"] = "Maestría", ["DR"] = "Doctorado",
    };

    public static DefinicionDePlantilla Definicion { get; } = new(Clave, "Personas y terceros", ModuloDeAuditoria.Core,
    [
        new HojaDePlantilla("Datos",
        [
            new(TipoDocumento, TipoDeValor.Codigo, Obligatoria: true, Largo: 3,
                Reglas: "CC, CE, PPT, PEP, TI, PA, RC o NIT (también C, E, PT, NI…)", Ejemplo: "CC"),
            new(Documento, TipoDeValor.Persona, Obligatoria: true, Largo: 20,
                Reglas: "sin dígito de verificación; puntos, espacios y guiones se quitan. Con el tipo, identifica la fila", Ejemplo: "1010101010"),
            new(DigitoVerificacion, TipoDeValor.Entero, Largo: 1, Reglas: "del NIT; si no corresponde al número se avisa"),
            new(Naturaleza, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "natural o juridica", Ejemplo: "natural"),
            new(PrimerNombre, TipoDeValor.Texto, Largo: 150, Reglas: "obligatorio en persona natural"),
            new(OtrosNombres, TipoDeValor.Texto, Largo: 150),
            new(PrimerApellido, TipoDeValor.Texto, Largo: 150, Reglas: "obligatorio en persona natural"),
            new(SegundoApellido, TipoDeValor.Texto, Largo: 150),
            new(RazonSocial, TipoDeValor.Texto, Largo: 150,
                Reglas: "obligatoria en persona jurídica; si la jurídica no trae nombres, nombres y apellidos toman la razón social"),
            new(LugarExpedicion, TipoDeValor.Texto, Largo: 40),
            new(FechaExpedicion, TipoDeValor.Fecha),
            new(Correo, TipoDeValor.Texto, Largo: 120),
            new(Telefono, TipoDeValor.Texto, Largo: 40),
            new(Telefono2, TipoDeValor.Texto, Largo: 40),
            new(Celular, TipoDeValor.Texto, Largo: 30),
            new(Direccion, TipoDeValor.Texto, Largo: 120),
            new(Ciudad, TipoDeValor.Codigo, Largo: 60, Reglas: "código DIVIPOLA (DANE), código de la cooperativa o nombre", Ejemplo: "76001"),
            new(FechaNacimiento, TipoDeValor.Fecha, Reglas: "anterior a hoy"),
            new(Genero, TipoDeValor.Texto, Largo: 20, Reglas: "M, F, O o su nombre"),
            new(EstadoCivil, TipoDeValor.Texto, Largo: 20, Reglas: "S, C, U, D, V o su nombre"),
            new(NivelEducativo, TipoDeValor.Texto, Largo: 20, Reglas: "PR, BA, TE, TG, PG, ES, MA, DR o su nombre"),
            new(EsProveedor, TipoDeValor.SiNoIndiferente, Reglas: "vacío = no cambia"),
            new(EsCliente, TipoDeValor.SiNoIndiferente, Reglas: "vacío = no cambia"),
            new(ResponsableIva, TipoDeValor.SiNoIndiferente, Reglas: "perfil tributario; vacío = no cambia"),
            new(Autorretenedor, TipoDeValor.SiNoIndiferente),
            new(AgenteRetencionIva, TipoDeValor.SiNoIndiferente),
            new(RegimenSimple, TipoDeValor.SiNoIndiferente),
            new(DeclaranteRenta, TipoDeValor.SiNoIndiferente),
            new(ObligadoFacturar, TipoDeValor.SiNoIndiferente),
            new(ObligadoContabilidad, TipoDeValor.SiNoIndiferente),
            new(GranContribuyente, TipoDeValor.SiNoIndiferente),
            new(ExentoRetencion, TipoDeValor.SiNoIndiferente),
            new(ExentoReteIca, TipoDeValor.SiNoIndiferente),
            new(Ciiu, TipoDeValor.Texto, Largo: 6, Reglas: "actividad económica, 4 a 6 dígitos"),
        ]),
    ]);

    /// <summary>
    /// El dígito de verificación de un NIT por el algoritmo de la DIAN (pesos primos de derecha a izquierda, módulo 11).
    /// Nulo si el número no es sólo dígitos o pasa de 15.
    /// </summary>
    public static int? DigitoDeVerificacionDian(string numero)
    {
        int[] pesos = [3, 7, 13, 17, 19, 23, 29, 37, 41, 43, 47, 53, 59, 67, 71];
        if (numero.Length is 0 or > 15 || !numero.All(char.IsAsciiDigit)) return null;
        var suma = 0;
        for (var i = 0; i < numero.Length; i++)
            suma += (numero[numero.Length - 1 - i] - '0') * pesos[i];
        var residuo = suma % 11;
        return residuo < 2 ? residuo : 11 - residuo;
    }
}
