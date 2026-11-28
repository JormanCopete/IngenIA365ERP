using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Accounting;

namespace IngenIA365ERP.Application.Implantacion.Cotejo;

/// <summary>
/// Lo común a las dos opciones contables (nuevo): el libro contabilizado al corte por el punto único de lectura
/// (<see cref="MovimientosContables"/>: alcance de sucursal, apertura como saldo inicial, el cierre del ejercicio del corte fuera y los de años
/// anteriores dentro), el plan y el saldo de una cuenta con el signo pedido.
/// </summary>
internal static class LibroAlCorte
{
    public static FiltrosDeInforme Filtros(DateOnly corte) => new() { From = new DateOnly(corte.Year, 1, 1), To = corte };

    /// <summary>El saldo final por naturaleza (positivo con su naturaleza) o débito menos crédito.</summary>
    public static decimal Saldo(SumasDeCuenta s, AccountNature naturaleza, bool debitoMenosCredito) =>
        debitoMenosCredito ? s.DebitoInicial - s.CreditoInicial + s.Debitos - s.Creditos : s.SaldoFinal(naturaleza);

    public static string NotaDeSigno(bool debitoMenosCredito) => debitoMenosCredito
        ? "Saldos como débito menos crédito: positivo débito, negativo crédito, en el ERP y en la referencia."
        : EncabezadoDeInforme.NotaDeSignos + " La referencia se lee igual.";

    public const string NotaDelCierre = "El comprobante de cierre del ejercicio del corte no entra (los saldos de resultados quedan como antes del cierre); "
        + "los cierres de años anteriores sí.";

    /// <summary>El grupo del resumen: la clase de la cuenta con su nombre («1 Activo»).</summary>
    public static string Clase(JerarquiaDelPlan plan, string codigo)
    {
        if (string.IsNullOrEmpty(codigo)) return MotorDeCotejo.SinGrupo;
        var clase = codigo[..1];
        return plan.PorCodigo(clase) is { } c ? $"{c.Code} {c.Name}" : $"Clase {clase}";
    }

    public static string CodigoDe(string? texto) => new((texto ?? string.Empty).Where(char.IsDigit).ToArray());
}

/// <summary>
/// Contabilidad, balance por cuenta (opción <c>cuentas</c>; nuevo). La referencia trae una fila por cuenta con su saldo al corte, del nivel que
/// quiera (clase, grupo, cuenta, subcuenta o auxiliar, o mezclados): cada código se compara con el saldo de su rama en el ERP. Las cuentas del
/// ERP con saldo que el archivo no trae se listan como «sólo en el ERP» en los mismos niveles (largos de código) que trae el archivo.
/// </summary>
public sealed class FuenteDeCuentas(IApplicationDbContext db, IUserBranchScope alcance, IDateTimeService clock, ICurrentUserService user) : IFuenteDeCotejo
{
    public const string Cuenta = "Cuenta";
    public const string Nombre = "Nombre";
    public const string Saldo = "Saldo";

    public string Opcion => OpcionesDeCotejo.Cuentas;
    public string Titulo => "Balance por cuenta";
    public string Permiso => OpcionesDeCotejo.PermisoContabilidad;

    public IReadOnlyList<ColumnaDeReferencia> Columnas { get; } =
    [
        new(Cuenta, true, ClaseDeColumnaDeReferencia.Texto, "El código de la cuenta del plan, de cualquier nivel.", "110505", "Código", "Codigo de la cuenta", "Código cuenta"),
        new(Nombre, false, ClaseDeColumnaDeReferencia.Texto, "El nombre de la cuenta (sólo se muestra).", "Caja general", "Nombre de la cuenta", "Descripción"),
        new(Saldo, true, ClaseDeColumnaDeReferencia.Importe, "El saldo final al corte.", "1500000", "Saldo final", "Saldo al corte", "Valor"),
    ];

    public IReadOnlyList<string> Instrucciones { get; } =
    [
        "Una fila por cuenta con su saldo final al corte. Puede traer cualquier nivel del plan, o varios: cada código se compara con la suma de su rama en el ERP.",
        "Por defecto el saldo va con el signo de la naturaleza de la cuenta (positivo con su naturaleza); si su archivo trae débito menos crédito, márquelo al cotejar.",
        "Un código que se repite en el archivo es un error: corrija el archivo para que cada cuenta venga una sola vez.",
    ];

    public async Task<Result<CotejoPreparado>> PrepararAsync(ArchivoDeReferencia archivo, ParametrosDeCotejo p, CancellationToken ct)
    {
        var errores = new List<ErrorDeFila>();
        var vistas = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var f in archivo.Filas)
        {
            var codigo = LibroAlCorte.CodigoDe(f.Texto(Cuenta));
            if (codigo.Length == 0) errores.Add(new ErrorDeFila(f.Numero, Cuenta, "Implantacion.Cotejo.CuentaInvalida", $"«{f.Texto(Cuenta)}» no es un código de cuenta."));
            else if (!vistas.TryAdd(codigo, f.Numero)) errores.Add(new ErrorDeFila(f.Numero, Cuenta, "Implantacion.Cotejo.Repetida", $"La cuenta {codigo} ya viene en la fila {vistas[codigo]}."));
        }
        if (errores.Count > 0) return Result.Failure<CotejoPreparado>(LecturaDeReferencia.ArchivoInvalido(errores));

        var ctx = await MovimientosContables.PrepararAsync(db, alcance, clock, LibroAlCorte.Filtros(p.Corte), ct);
        if (ctx.IsFailure) return Result.Failure<CotejoPreparado>(ctx.Error);
        var plan = await JerarquiaDelPlan.CargarAsync(db, ct);
        var agregadas = plan.Agregar(await LibrosQueries.SumasPorCuentaAsync(db, ctx.Value, ct));

        var definicion = new DefinicionDeCotejo("Cotejo del balance por cuenta", "Cuenta", ["Nombre"],
            [new MedidaDeCotejo("Saldo", ClaseDeMedida.Moneda, p.Tolerancia)], "Clase", OrdenDeCodigo: true);

        var referencia = archivo.Filas.Select(f =>
        {
            var codigo = LibroAlCorte.CodigoDe(f.Texto(Cuenta));
            return new PartidaDeCotejo(codigo, codigo, LibroAlCorte.Clase(plan, codigo), [NullSiVacio(f.Texto(Nombre))], [f.Importe(Saldo)]);
        }).ToList();

        var largos = vistas.Keys.Select(c => c.Length).ToHashSet();
        var erp = new List<PartidaDeCotejo>();
        foreach (var cuenta in plan.Cuentas)
        {
            var enArchivo = vistas.ContainsKey(cuenta.Code);
            if (!enArchivo && !largos.Contains(cuenta.Code.Length)) continue;
            var saldo = agregadas.TryGetValue(cuenta.Id, out var s) ? LibroAlCorte.Saldo(s, cuenta.Nature, p.SignoDebitoMenosCredito) : 0m;
            if (!enArchivo && saldo == 0m) continue;
            erp.Add(new PartidaDeCotejo(cuenta.Code, cuenta.Code, LibroAlCorte.Clase(plan, cuenta.Code), [cuenta.Name], [saldo]));
        }
        // Un código del archivo que no está en el plan queda «sólo en la referencia»; se dice por qué.
        var faltantes = vistas.Keys.Where(c => plan.PorCodigo(c) is null).ToHashSet(StringComparer.Ordinal);
        if (faltantes.Count > 0)
            referencia = referencia.Select(r => faltantes.Contains(r.Llave) ? r with { Nota = "La cuenta no existe en el plan del ERP." } : r).ToList();

        var notas = new List<string>(await EncabezadoDeInforme.NotasAsync(db, user, clock, ctx.Value.Filtros, ctx.Value.PeriodoTexto, ctx.Value, ct))
        {
            LibroAlCorte.NotaDeSigno(p.SignoDebitoMenosCredito),
            LibroAlCorte.NotaDelCierre,
            $"Del ERP se listan las cuentas con saldo de los niveles que trae el archivo (códigos de {string.Join(", ", largos.Order())} dígitos).",
        };
        if (faltantes.Count > 0) notas.Add($"{faltantes.Count} código(s) del archivo no existen en el plan del ERP.");
        return Result.Success(new CotejoPreparado(definicion, erp, referencia, notas));
    }

    private static string? NullSiVacio(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

/// <summary>
/// Contabilidad, saldos por tercero (opción <c>terceros</c>; nuevo). La referencia trae cuenta, documento del tercero y saldo: cada cuenta (de
/// cualquier nivel) se compara por tercero con la suma de su rama en el ERP. Si el archivo repite cuenta y tercero (por ejemplo, una fila por
/// documento cruce), se suman. Del ERP se listan los terceros con saldo en las cuentas que trae el archivo, incluido «sin tercero».
/// </summary>
public sealed class FuenteDeTerceros(IApplicationDbContext db, IUserBranchScope alcance, IDateTimeService clock, ICurrentUserService user) : IFuenteDeCotejo
{
    public const string Cuenta = "Cuenta";
    public const string Documento = "Documento del tercero";
    public const string NombreDelTercero = "Nombre del tercero";
    public const string Saldo = "Saldo";

    private const string SinTercero = "(sin tercero)";

    public string Opcion => OpcionesDeCotejo.Terceros;
    public string Titulo => "Saldos por tercero";
    public string Permiso => OpcionesDeCotejo.PermisoContabilidad;

    public IReadOnlyList<ColumnaDeReferencia> Columnas { get; } =
    [
        new(Cuenta, true, ClaseDeColumnaDeReferencia.Texto, "El código de la cuenta del plan, de cualquier nivel.", "13050501", "Código", "Codigo de la cuenta"),
        new(Documento, true, ClaseDeColumnaDeReferencia.Texto, "El documento (NIT con o sin dígito de verificación). Vacío = sin tercero.", "900123456-7",
            "Documento", "NIT", "Identificación", "Nit del tercero", "Tercero"),
        new(NombreDelTercero, false, ClaseDeColumnaDeReferencia.Texto, "El nombre (sólo se muestra).", "Comercializadora del Valle SAS", "Nombre", "Razón social"),
        new(Saldo, true, ClaseDeColumnaDeReferencia.Importe, "El saldo final al corte de esa cuenta con ese tercero.", "250000", "Saldo final", "Valor"),
    ];

    public IReadOnlyList<string> Instrucciones { get; } =
    [
        "Una fila por cuenta y tercero con el saldo al corte. Si su archivo trae una fila por documento cruce, súbalo así: las filas de la misma cuenta y tercero se suman.",
        "El documento se compara sin puntos, espacios, ceros a la izquierda ni dígito de verificación.",
        "Conviene cotejar primero el balance por cuenta: aquí sólo entran las cuentas que trae el archivo.",
    ];

    public async Task<Result<CotejoPreparado>> PrepararAsync(ArchivoDeReferencia archivo, ParametrosDeCotejo p, CancellationToken ct)
    {
        var errores = new List<ErrorDeFila>();
        foreach (var f in archivo.Filas)
            if (LibroAlCorte.CodigoDe(f.Texto(Cuenta)).Length == 0)
                errores.Add(new ErrorDeFila(f.Numero, Cuenta, "Implantacion.Cotejo.CuentaInvalida", $"«{f.Texto(Cuenta)}» no es un código de cuenta."));
        if (errores.Count > 0) return Result.Failure<CotejoPreparado>(LecturaDeReferencia.ArchivoInvalido(errores));

        var ctx = await MovimientosContables.PrepararAsync(db, alcance, clock, LibroAlCorte.Filtros(p.Corte), ct);
        if (ctx.IsFailure) return Result.Failure<CotejoPreparado>(ctx.Error);
        var plan = await JerarquiaDelPlan.CargarAsync(db, ct);
        var porTercero = await LibrosQueries.SumasPorCuentaYTerceroAsync(db, ctx.Value, ct);
        var personas = await LibrosQueries.TercerosAsync(db, porTercero.Keys.Select(k => k.PersonaId).OfType<int>().Distinct().ToList(), ct);

        // La referencia, sumada por cuenta y documento.
        var referencia = archivo.Filas
            .GroupBy(f => (Codigo: LibroAlCorte.CodigoDe(f.Texto(Cuenta)), Doc: LecturaDeReferencia.Documento(f.Texto(Documento))))
            .Select(g =>
            {
                var primera = g.First();
                var visible = g.Key.Doc.Length == 0 ? SinTercero : primera.Texto(Documento);
                var nota = g.Count() > 1 ? $"{g.Count()} filas del archivo sumadas." : null;
                return new PartidaDeCotejo(Llave(g.Key.Codigo, g.Key.Doc), visible, Grupo(plan, g.Key.Codigo),
                    [g.Key.Codigo, NullSiVacio(primera.Texto(NombreDelTercero))], [g.Sum(f => f.Importe(Saldo) ?? 0m)], nota);
            }).ToList();

        // El ERP: por cada código del archivo, la suma de su rama por tercero (documento normalizado: dos personas con el mismo número se suman).
        var codigos = referencia.Select(r => r.Atributos[0]!).Distinct(StringComparer.Ordinal).ToList();
        var erp = new Dictionary<string, (string Codigo, string Doc, string Visible, string? Nombre, decimal Saldo, HashSet<int> Personas)>(StringComparer.Ordinal);
        foreach (var ((cuentaId, personaId), sumas) in porTercero)
        {
            var cuenta = plan.PorId(cuentaId);
            if (cuenta is null) continue;
            foreach (var codigo in codigos.Where(c => cuenta.Code.StartsWith(c, StringComparison.Ordinal)))
            {
                var naturaleza = plan.PorCodigo(codigo)?.Nature ?? JerarquiaDelPlan.NaturalezaDeClase(codigo);
                var saldo = LibroAlCorte.Saldo(sumas, naturaleza, p.SignoDebitoMenosCredito);
                var persona = personaId is { } pid && personas.TryGetValue(pid, out var t) ? t : default;
                var doc = personaId is null ? string.Empty : LecturaDeReferencia.Documento(persona.TaxId);
                var llave = Llave(codigo, doc);
                var actual = erp.GetValueOrDefault(llave, (codigo, doc, personaId is null ? SinTercero : persona.TaxId ?? string.Empty, personaId is null ? null : persona.Nombre, 0m, []));
                actual.Saldo += saldo;
                if (personaId is { } id2) actual.Personas.Add(id2);
                erp[llave] = actual;
            }
        }
        var enArchivo = referencia.Select(r => r.Llave).ToHashSet(StringComparer.Ordinal);
        var partidasErp = erp.Where(x => x.Value.Saldo != 0m || enArchivo.Contains(x.Key))
            .Select(x => new PartidaDeCotejo(x.Key, x.Value.Visible, Grupo(plan, x.Value.Codigo), [x.Value.Codigo, x.Value.Nombre], [x.Value.Saldo],
                x.Value.Personas.Count > 1 ? $"El documento es de {x.Value.Personas.Count} personas del ERP; se suman." : null))
            .ToList();

        var definicion = new DefinicionDeCotejo("Cotejo de saldos por tercero", "Documento del tercero", ["Cuenta", "Tercero"],
            [new MedidaDeCotejo("Saldo", ClaseDeMedida.Moneda, p.Tolerancia)], "Cuenta");
        var notas = new List<string>(await EncabezadoDeInforme.NotasAsync(db, user, clock, ctx.Value.Filtros, ctx.Value.PeriodoTexto, ctx.Value, ct))
        {
            LibroAlCorte.NotaDeSigno(p.SignoDebitoMenosCredito),
            LibroAlCorte.NotaDelCierre,
            $"Se cotejan las {codigos.Count:N0} cuenta(s) que trae el archivo; cada una suma su rama del plan, abierta por tercero.",
        };
        return Result.Success(new CotejoPreparado(definicion, partidasErp, referencia, notas));
    }

    private static string Llave(string codigo, string documento) => $"{codigo}|{documento}";

    private static string Grupo(JerarquiaDelPlan plan, string codigo) =>
        plan.PorCodigo(codigo) is { } c ? $"{c.Code} {c.Name}" : $"{codigo} (no está en el plan)";

    private static string? NullSiVacio(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
