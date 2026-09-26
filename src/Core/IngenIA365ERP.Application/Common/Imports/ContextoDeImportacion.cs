using IngenIA365ERP.Application.Common.Interfaces.Files;

namespace IngenIA365ERP.Application.Common.Imports;

/// <summary>
/// Lo que ve una plantilla mientras corre (feature 012, T156): sus hojas ya leídas y con encabezados comprobados, el
/// modo, el motivo, los permisos de quien importa y dónde dejar errores, avisos, el resultado de cada fila y lo propio
/// de la plantilla (<see cref="Extra"/>). Lo arma <see cref="EjecutorDeImportacion"/>; la plantilla no guarda: agrega o
/// modifica entidades en el contexto de datos, y el ejecutor decide si eso se guarda (apply sin errores) o se deshace
/// (review, o apply con errores). (nuevo)
/// </summary>
public sealed class ContextoDeImportacion
{
    private readonly List<ErrorDeFila> _errores = [];
    private readonly List<ErrorDeFila> _avisos = [];
    private readonly Dictionary<(string Hoja, int Fila), (string Clave, AccionDeImportacion Accion, IReadOnlyList<CampoCambiadoDto> Campos)> _resultados = [];
    private readonly HashSet<(string Hoja, int Fila)> _filasConError = [];
    private readonly Dictionary<string, List<string>> _mensajesPorFila = new(StringComparer.Ordinal);
    private readonly IReadOnlySet<string> _permisos;
    private readonly bool _todosLosPermisos;
    private readonly List<HojaDeImportacion> _hojas = [];

    internal ContextoDeImportacion(DefinicionDePlantilla plantilla, ModoDeImportacion modo, string? motivo,
        IReadOnlySet<string> permisos, bool todosLosPermisos)
    {
        Plantilla = plantilla;
        Modo = modo;
        Motivo = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
        _permisos = permisos;
        _todosLosPermisos = todosLosPermisos;
    }

    public DefinicionDePlantilla Plantilla { get; }

    public ModoDeImportacion Modo { get; }

    /// <summary>El motivo que vino con la importación (§0.5); nulo si no vino.</summary>
    public string? Motivo { get; }

    public IReadOnlyList<HojaDeImportacion> Hojas => _hojas;

    /// <summary>Lo propio de cada plantilla (§8, §14, §15 del contrato).</summary>
    public Dictionary<string, object?> Extra { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// El archivo crea o cierra una vigencia, o cambia algo que exige decir por qué (§0.5): la revisión lo informa
    /// (<see cref="ImportResultDto.RequiresReason"/>) y aplicar sin motivo es un error.
    /// </summary>
    public bool RequiereMotivo { get; private set; }

    public void PedirMotivo() => RequiereMotivo = true;

    /// <summary>La hoja por su nombre de la definición; vacía si es opcional y no vino.</summary>
    public HojaDeImportacion Hoja(string nombre) =>
        _hojas.FirstOrDefault(h => string.Equals(h.Nombre, nombre, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"La plantilla {Plantilla.Clave} no declara la hoja «{nombre}».", nameof(nombre));

    /// <summary>La única hoja de una plantilla de una sección.</summary>
    public HojaDeImportacion Datos => _hojas[0];

    public bool TienePermiso(string permiso) => _todosLosPermisos || _permisos.Contains(permiso);

    public int TotalDeErrores => _errores.Count;

    public bool HayErrores => _errores.Count > 0;

    // --------------------------------------------------------------- registro --

    public void Error(FilaDeImportacion fila, string columna, string codigo, string mensaje)
    {
        _errores.Add(new ErrorDeFila(fila.Numero, columna, codigo, mensaje, fila.Hoja.NombreEnErrores));
        _filasConError.Add((fila.Hoja.Nombre, fila.Numero));
        Mensaje(fila.Hoja.Nombre, fila.Numero, $"{columna}: {mensaje}");
    }

    /// <summary>Un error que no es de una fila (el motivo, una hoja entera): <c>Row = 0</c>.</summary>
    public void ErrorSinFila(string? hoja, string columna, string codigo, string mensaje) =>
        _errores.Add(new ErrorDeFila(0, columna, codigo, mensaje, hoja));

    public void Aviso(FilaDeImportacion fila, string columna, string codigo, string mensaje) =>
        _avisos.Add(new ErrorDeFila(fila.Numero, columna, codigo, mensaje, fila.Hoja.NombreEnErrores));

    internal void Aviso(ErrorDeFila aviso) => _avisos.Add(aviso);

    /// <summary>
    /// El resultado de una fila válida: su llave, si crea, actualiza o queda igual, y los campos que cambian. Una fila
    /// con errores no cuenta (su resultado en la revisión en Excel son sus errores).
    /// </summary>
    public void Registrar(FilaDeImportacion fila, string clave, AccionDeImportacion accion, IReadOnlyList<CampoCambiadoDto>? campos = null) =>
        _resultados[(fila.Hoja.Nombre, fila.Numero)] = (clave, accion, campos ?? []);

    internal bool FilaConErrores(FilaDeImportacion fila) => _filasConError.Contains((fila.Hoja.Nombre, fila.Numero));

    internal void AgregarHoja(HojaDeImportacion hoja) => _hojas.Add(hoja);

    private void Mensaje(string hoja, int fila, string mensaje)
    {
        var clave = $"{hoja}\n{fila}";
        if (!_mensajesPorFila.TryGetValue(clave, out var lista)) _mensajesPorFila[clave] = lista = [];
        lista.Add(mensaje);
    }

    // ------------------------------------------------------------- resultado --

    internal ImportResultDto Resultado(ArchivoDeImportacion archivo, bool aplicado)
    {
        var errores = _errores
            .OrderBy(e => e.Sheet is null ? -1 : Plantilla.Hojas.ToList().FindIndex(h => h.Nombre == e.Sheet))
            .ThenBy(e => e.Row).ThenBy(e => e.Column, StringComparer.Ordinal)
            .ToList();

        var hojas = new List<ResumenDeHojaDto>();
        var cambios = new List<CambioDeFilaDto>();
        var filas = new List<ResultadoDeFila>();
        var totalDeCambios = 0;
        foreach (var hoja in _hojas)
        {
            int creadas = 0, actualizadas = 0, iguales = 0;
            foreach (var fila in hoja.Filas)
            {
                var conError = _filasConError.Contains((hoja.Nombre, fila.Numero));
                AccionDeImportacion? accion = null;
                if (!conError && _resultados.TryGetValue((hoja.Nombre, fila.Numero), out var r))
                {
                    accion = r.Accion;
                    switch (r.Accion)
                    {
                        case AccionDeImportacion.Create: creadas++; break;
                        case AccionDeImportacion.Update: actualizadas++; break;
                        default: iguales++; break;
                    }
                    if (r.Accion != AccionDeImportacion.Unchanged)
                    {
                        totalDeCambios++;
                        if (cambios.Count < EjecutorDeImportacion.MaximoDeCambios)
                            cambios.Add(new CambioDeFilaDto(hoja.NombreEnErrores, fila.Numero, r.Clave, r.Accion, r.Campos));
                    }
                }
                filas.Add(new ResultadoDeFila(hoja.Nombre, fila.Numero, accion,
                    _mensajesPorFila.TryGetValue($"{hoja.Nombre}\n{fila.Numero}", out var m) ? m : []));
            }
            hojas.Add(new ResumenDeHojaDto(hoja.Nombre, hoja.Filas.Count, creadas, actualizadas, iguales));
        }

        return new ImportResultDto
        {
            Template = Plantilla.Clave,
            Mode = Modo,
            Valid = errores.Count == 0,
            Applied = aplicado,
            FileName = archivo.NombreArchivo,
            FileSha256 = archivo.Sha256,
            RequiresReason = RequiereMotivo,
            Sheets = hojas,
            Changes = cambios,
            ChangesTruncated = totalDeCambios > cambios.Count,
            Warnings = _avisos.OrderBy(a => a.Sheet, StringComparer.Ordinal).ThenBy(a => a.Row).ToList(),
            Errors = errores.Take(EjecutorDeImportacion.MaximoDeErrores).ToList(),
            TotalErrors = errores.Count,
            Extra = new Dictionary<string, object?>(Extra, StringComparer.Ordinal),
            Rows = filas,
        };
    }
}

/// <summary>Una hoja leída (nuevo): sus filas no vacías y el índice de cada columna de la definición.</summary>
public sealed class HojaDeImportacion
{
    private readonly TablaLeida _tabla;
    private readonly Dictionary<string, (int Fila, string Columna)> _claves = new(StringComparer.Ordinal);

    internal HojaDeImportacion(ContextoDeImportacion contexto, HojaDePlantilla definicion, TablaLeida tabla, bool deUnaHoja)
    {
        Contexto = contexto;
        Definicion = definicion;
        _tabla = tabla;
        NombreEnErrores = deUnaHoja ? null : definicion.Nombre;
        Filas = tabla.Filas.Where(f => !f.EstaVacia).Select(f => new FilaDeImportacion(this, f)).ToList();
    }

    public ContextoDeImportacion Contexto { get; }

    public HojaDePlantilla Definicion { get; }

    public string Nombre => Definicion.Nombre;

    /// <summary>La hoja como va en los errores: nula en las plantillas de una sola hoja.</summary>
    public string? NombreEnErrores { get; }

    public IReadOnlyList<FilaDeImportacion> Filas { get; }

    public ColumnaDePlantilla? Columna(string encabezado) => Definicion.Columna(encabezado);

    public int IndiceDe(string encabezado) => _tabla.IndiceDe(encabezado);

    /// <summary>
    /// Registra la llave de una fila; si ya vino en otra fila de la hoja, <c>Import.Row.Duplicate</c> nombrando la otra
    /// y devuelve <c>false</c>. Una llave vacía no se registra.
    /// </summary>
    public bool LlaveUnica(FilaDeImportacion fila, string? llave, string columna)
    {
        var normalizada = TablaLeida.Normalizar(llave);
        if (normalizada.Length == 0) return true;
        if (_claves.TryGetValue(normalizada, out var otra))
        {
            fila.Error(columna, ImportErrors.RowDuplicate, $"«{llave}» ya viene en la fila {otra.Fila}.");
            return false;
        }
        _claves[normalizada] = (fila.Numero, columna);
        return true;
    }
}
