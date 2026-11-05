using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Core.Associates.Services;

namespace IngenIA365ERP.Application.Core.Associates.Imports;

/// <summary>
/// La plantilla <c>core.associates</c> (feature 016, US3; contracts/plantillas.md §3): la afiliación de personas que ya existen, por el
/// camino del alta unitaria. La llave es el usuario derivado de la persona viva; nunca crea personas ni borra. Un retirado sólo se
/// carga <b>al crear</b>, como estado histórico (research R9, D8): el retiro de un vigente nace en Cartera. (nuevo)
/// </summary>
public static class PlantillaDeAsociados
{
    public const string Clave = "core.associates";

    public const string TipoDocumento = "tipoDocumento";
    public const string Documento = "documento";
    public const string FechaAfiliacion = "fechaAfiliacion";
    public const string Oficina = "oficina";
    public const string Estado = "estado";
    public const string Grupo = "grupo";
    public const string Clase = "clase";
    public const string FechaRetiro = "fechaRetiro";
    public const string MotivoRetiro = "motivoRetiro";
    public const string FechaReingreso = "fechaReingreso";
    public const string PeriodicidadDescuento = "periodicidadDescuento";
    public const string FormaDescuento = "formaDescuento";
    public const string TasaAporte = "tasaAporte";
    public const string AporteComprometido = "aporteComprometido";
    public const string Comite = "comite";
    public const string Categoria = "categoria";

    /// <summary><c>COR_Associates.Status</c>.</summary>
    public static readonly IReadOnlyDictionary<string, string> Estados = new Dictionary<string, string>
    {
        ["vigente"] = AssociateRegistrar.EstadoVigente, ["activo"] = AssociateRegistrar.EstadoVigente, ["a"] = AssociateRegistrar.EstadoVigente,
        ["retirado"] = AssociateRegistrar.EstadoRetirado, ["r"] = AssociateRegistrar.EstadoRetirado,
    };

    /// <summary><c>COR_Associates.AssociateClass</c>, cuatro caracteres. Las tres clases son afiliación: dan fila de asociado.</summary>
    public static readonly IReadOnlyDictionary<string, string> Clases = new Dictionary<string, string>
    {
        ["asociado"] = "ASOC", ["asoc"] = "ASOC", ["afiliado"] = "AFIL", ["afil"] = "AFIL", ["empleado"] = "EMPL", ["empl"] = "EMPL",
    };

    /// <summary><c>COR_Associates.DeductionPeriod</c>, dos caracteres.</summary>
    public static readonly IReadOnlyDictionary<string, string> Periodicidades = new Dictionary<string, string>
    {
        ["mensual"] = "M", ["m"] = "M", ["quincenal"] = "Q", ["q"] = "Q",
    };

    /// <summary><c>COR_Associates.DeductionType</c>, dos caracteres.</summary>
    public static readonly IReadOnlyDictionary<string, string> Formas = new Dictionary<string, string>
    {
        ["caja"] = "C", ["c"] = "C", ["nomina"] = "N", ["n"] = "N",
    };

    public static DefinicionDePlantilla Definicion { get; } = new(Clave, "Asociados", ModuloDeAuditoria.Core,
    [
        new HojaDePlantilla("Datos",
        [
            new(TipoDocumento, TipoDeValor.Codigo, Obligatoria: true, Largo: 3,
                Reglas: "CC, CE, PPT, PEP, TI, PA, RC o NIT (también C, E, PT, NI…)", Ejemplo: "CC"),
            new(Documento, TipoDeValor.Persona, Obligatoria: true, Largo: 20,
                Reglas: "con el tipo, identifica la fila. La persona debe existir viva en Maestros › Personas: esta plantilla no crea personas",
                Ejemplo: "1010101010"),
            new(FechaAfiliacion, TipoDeValor.Fecha, Obligatoria: true, Reglas: "no puede ser futura", Ejemplo: "2015-03-01"),
            new(Oficina, TipoDeValor.Sucursal, Obligatoria: true, Largo: 80, Reglas: "código o nombre de la agencia de Maestros › Agencias", Ejemplo: "PRINCIPAL"),
            new(Estado, TipoDeValor.Enumeracion, Obligatoria: true,
                Reglas: "vigente (o activo) o retirado. Un asociado vigente que ya existe no se retira por archivo: se tramita en Cartera", Ejemplo: "vigente"),
            new(Grupo, TipoDeValor.Codigo, Largo: 80, Reglas: "código o nombre de un grupo activo de Maestros › Grupos del asociado"),
            new(Clase, TipoDeValor.Enumeracion, Reglas: "asociado, afiliado o empleado; las tres dan fila de asociado", Ejemplo: "asociado"),
            new(FechaRetiro, TipoDeValor.Fecha,
                Reglas: "obligatoria si está retirado; no futura. Sólo se guarda al crear. Un vigente con fecha de retiro lleva un reingreso igual o posterior"),
            new(MotivoRetiro, TipoDeValor.Codigo, Largo: 80, Reglas: "obligatorio si está retirado; código o nombre de Maestros › Motivos de retiro. Sólo se guarda al crear"),
            new(FechaReingreso, TipoDeValor.Fecha, Reglas: "no futura; en un retirado, anterior al retiro. Sólo se guarda al crear"),
            new(PeriodicidadDescuento, TipoDeValor.Enumeracion, Reglas: "mensual o quincenal", Ejemplo: "mensual"),
            new(FormaDescuento, TipoDeValor.Enumeracion, Reglas: "caja o nomina", Ejemplo: "caja"),
            new(TasaAporte, TipoDeValor.Cantidad, Reglas: "en puntos: 5 es 5 %. Vacía al crear = 0"),
            new(AporteComprometido, TipoDeValor.Monto, Reglas: "en pesos. Vacío al crear = 0"),
            new(Comite, TipoDeValor.Codigo, Largo: 80, Reglas: "código o nombre del comité"),
            new(Categoria, TipoDeValor.Texto, Largo: 2),
        ]),
    ]);
}
