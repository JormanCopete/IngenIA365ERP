namespace IngenIA365ERP.Domain.Payroll.Pila;

/// <summary>
/// El layout del archivo tipo 2 de la Resolución 2388 de 2016 como dato (feature 010, US5;
/// contracts/archivos.md §1.2): registros tipo 1 y 2 con cada campo, su posición, su largo, su
/// tipo y de dónde sale. Una versión nueva del anexo es un JSON nuevo con vigencia; las
/// generaciones históricas conservan la suya. Un layout con algún campo sin cotejar
/// (<see cref="PilaFieldLayout.Verified"/> en falso) no se marca vigente.
/// </summary>
public sealed record PilaLayout(
    string Code,
    string Version,
    string Source,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string Encoding,
    string LineEnding,
    IReadOnlyList<PilaRecordLayout> Records)
{
    public PilaRecordLayout Type1 => Records.First(r => r.Type == 1);
    public PilaRecordLayout Type2 => Records.First(r => r.Type == 2);

    /// <summary>Verdadero cuando todos los campos de los dos registros están cotejados contra el anexo.</summary>
    public bool IsVerified => Records.All(r => r.Fields.All(f => f.Verified));

    public bool IsValidAt(DateOnly date) => ValidFrom <= date && (ValidTo is null || ValidTo >= date);

    /// <summary>
    /// Comprueba que cada registro sea contiguo (sin huecos ni solapes), que empiece en 1 y que
    /// la suma de largos sea el largo declarado. Devuelve los problemas en texto llano.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var errores = new List<string>();
        foreach (var r in Records)
        {
            var esperado = 1;
            foreach (var f in r.Fields.OrderBy(f => f.Number))
            {
                if (f.Start != esperado)
                    errores.Add($"Registro {r.Type}, campo {f.Number} «{f.Name}»: empieza en {f.Start} y debía empezar en {esperado}.");
                if (f.Length <= 0)
                    errores.Add($"Registro {r.Type}, campo {f.Number} «{f.Name}»: largo inválido ({f.Length}).");
                if (f.Kind is not ("A" or "N"))
                    errores.Add($"Registro {r.Type}, campo {f.Number} «{f.Name}»: tipo «{f.Kind}» desconocido (A o N).");
                esperado = f.Start + f.Length;
            }
            var total = esperado - 1;
            if (total != r.Length)
                errores.Add($"Registro {r.Type}: los campos suman {total} posiciones y el registro declara {r.Length}.");
            var numeros = r.Fields.Select(f => f.Number).OrderBy(n => n).ToList();
            for (var i = 0; i < numeros.Count; i++)
                if (numeros[i] != i + 1) { errores.Add($"Registro {r.Type}: los campos no son consecutivos desde 1 (falta o sobra el {i + 1})."); break; }
        }
        return errores;
    }
}

public sealed record PilaRecordLayout(int Type, int Length, IReadOnlyList<PilaFieldLayout> Fields);

/// <param name="Kind"><c>N</c> numérico (derecha, ceros) o <c>A</c> alfanumérico (izquierda, espacios).</param>
/// <param name="Source"><c>Constant</c>, <c>Blank</c>, <c>Settings</c>, <c>Profile</c>, <c>Calculation</c>.</param>
/// <param name="Path">Qué propiedad del modelo puro llena el campo (documental y para la explicación).</param>
/// <param name="Format"><c>Integer</c>, <c>Rate7</c>, <c>Rate9</c>, <c>Date</c>, <c>Period</c>, <c>Flag</c>, <c>Text</c>.</param>
public sealed record PilaFieldLayout(
    int Number,
    string Name,
    int Start,
    int Length,
    string Kind,
    bool Required,
    string Source,
    string? Path,
    string? Constant,
    string Format,
    IReadOnlyList<string> Rules,
    bool Verified);
