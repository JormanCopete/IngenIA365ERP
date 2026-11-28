using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.Associates.Services;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Reports;
using IngenIA365ERP.Application.Lending.Reports.Vistas;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Implantacion.Cotejo;

/// <summary>
/// Cartera, crédito por crédito (opción <c>creditos</c>; nuevo). Lo del ERP es la cartera viva al corte con el <b>mismo estado que el estado de
/// cuenta</b> (<see cref="CarteraAlCorte"/>: la reconstrucción del motor, o la posición diaria si no se pudo), con el alcance de Cartera de quien
/// coteja. El crédito se empareja por su número (el de la carga es el de origen), sin ceros a la izquierda ni separadores. Se comparan los
/// conceptos que traiga el archivo, los días de mora y la calificación; el documento del deudor también, para atrapar un número cruzado.
/// </summary>
public sealed class FuenteDeCreditos(IApplicationDbContext db, IAlcanceDeCartera alcance, CargadorDeReconstruccion cargador) : IFuenteDeCotejo
{
    public const string Credito = "Crédito";
    public const string Documento = "Documento del deudor";
    public const string Capital = "Capital";
    public const string InteresCorriente = "Interés corriente";
    public const string InteresDeMora = "Interés de mora";
    public const string Otros = "Otros";
    public const string DiasDeMora = "Días de mora";
    public const string Calificacion = "Calificación";

    public string Opcion => OpcionesDeCotejo.Creditos;
    public string Titulo => "Cartera por crédito";
    public string Permiso => OpcionesDeCotejo.PermisoCartera;

    public IReadOnlyList<ColumnaDeReferencia> Columnas { get; } =
    [
        new(Credito, true, ClaseDeColumnaDeReferencia.Texto, "El número del crédito.", "000123", "Número del crédito", "Pagaré", "Obligación", "Credito"),
        new(Documento, false, ClaseDeColumnaDeReferencia.Texto, "El documento del deudor.", "16123456", "Documento", "Cédula", "Identificación", "NIT"),
        new(Capital, true, ClaseDeColumnaDeReferencia.Importe, "El saldo de capital al corte.", "4500000", "Saldo capital", "Saldo de capital"),
        new(InteresCorriente, false, ClaseDeColumnaDeReferencia.Importe, "El interés corriente causado y no pagado (con el contingente).", "35000",
            "Intereses corrientes", "Interés", "Intereses"),
        new(InteresDeMora, false, ClaseDeColumnaDeReferencia.Importe, "El interés de mora causado y no pagado (con el contingente).", "1200", "Intereses de mora", "Mora"),
        new(Otros, false, ClaseDeColumnaDeReferencia.Importe, "Seguro, administración y otros cargos por cobrar.", "8000", "Otros conceptos", "Otros cargos"),
        new(DiasDeMora, false, ClaseDeColumnaDeReferencia.Entero, "Los días de mora al corte.", "15", "Días mora", "Altura de mora"),
        new(Calificacion, false, ClaseDeColumnaDeReferencia.Texto, "La calificación de riesgo (A a E).", "A", "Categoría", "Calificacion"),
    ];

    public IReadOnlyList<string> Instrucciones { get; } =
    [
        "Una fila por crédito vivo al corte. Sólo Crédito y Capital son obligatorias: las demás columnas que traiga se comparan; las que no traiga, no.",
        "El número se compara sin ceros a la izquierda, espacios ni guiones. Un crédito repetido en el archivo es un error.",
        "Lo del ERP es lo mismo que muestra el estado de cuenta al corte. La calificación se compara por la letra (A, B, C, D, E).",
    ];

    public async Task<Result<CotejoPreparado>> PrepararAsync(ArchivoDeReferencia archivo, ParametrosDeCotejo p, CancellationToken ct)
    {
        var errores = new List<ErrorDeFila>();
        var vistas = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var f in archivo.Filas)
        {
            var llave = LecturaDeReferencia.Llave(f.Texto(Credito));
            if (llave.Length == 0) errores.Add(new ErrorDeFila(f.Numero, Credito, "Implantacion.Cotejo.Vacio", "La fila no trae el número del crédito."));
            else if (!vistas.TryAdd(llave, f.Numero)) errores.Add(new ErrorDeFila(f.Numero, Credito, "Implantacion.Cotejo.Repetida", $"El crédito {f.Texto(Credito)} ya viene en la fila {vistas[llave]}."));
        }
        if (errores.Count > 0) return Result.Failure<CotejoPreparado>(LecturaDeReferencia.ArchivoInvalido(errores));

        var ambito = await AmbitoDeInformeDeCartera.LeerAsync(db, alcance, new FiltrosDeInformeDeCartera { AsOf = p.Corte }, ct);
        var creditos = await CarteraAlCorte.LeerAsync(db, cargador, ambito, p.Corte, ct);

        var trae = (string c) => archivo.Trae(c);
        var definicion = new DefinicionDeCotejo("Cotejo de cartera por crédito", "Crédito", ["Deudor", "Número en el ERP"],
        [
            new MedidaDeCotejo(Documento, ClaseDeMedida.Texto),
            new MedidaDeCotejo(Capital, ClaseDeMedida.Moneda, p.Tolerancia),
            new MedidaDeCotejo(InteresCorriente, ClaseDeMedida.Moneda, p.Tolerancia),
            new MedidaDeCotejo(InteresDeMora, ClaseDeMedida.Moneda, p.Tolerancia),
            new MedidaDeCotejo(Otros, ClaseDeMedida.Moneda, p.Tolerancia),
            new MedidaDeCotejo(DiasDeMora, ClaseDeMedida.Entero),
            new MedidaDeCotejo(Calificacion, ClaseDeMedida.Texto),
        ], "Línea");

        var erp = new List<PartidaDeCotejo>();
        var yaVistas = new HashSet<string>(StringComparer.Ordinal);
        var repetidos = 0;
        foreach (var c in creditos)
        {
            var llave = LecturaDeReferencia.Llave(c.Credito.Number);
            if (!yaVistas.Add(llave)) { repetidos++; continue; }
            if (!vistas.ContainsKey(llave) && c.TotalAdeudado == 0m && c.AFavor == 0m) continue;
            var linea = string.IsNullOrWhiteSpace(c.LineaCodigo) ? c.LineaNombre : $"{c.LineaCodigo} {c.LineaNombre}";
            erp.Add(new PartidaDeCotejo(llave, c.Credito.Number, linea, [c.Deudor.Nombre, c.Credito.Number],
            [
                LecturaDeReferencia.Documento(c.Deudor.Documento),
                c.Capital,
                c.InteresCorriente,
                c.InteresDeMora,
                c.Seguro + c.Administracion + c.Otros,
                (decimal)c.DiasDeMora,
                c.Posicion?.DailyRating.ToString(),
            ], c.Error is null ? null : $"No se pudo recalcular al corte; sale con la posición diaria ({c.Error})."));
        }

        var referencia = archivo.Filas.Select(f => new PartidaDeCotejo(LecturaDeReferencia.Llave(f.Texto(Credito)), f.Texto(Credito), "(no está en el ERP)", [],
        [
            trae(Documento) ? LecturaDeReferencia.Documento(f.Texto(Documento)) : null,
            f.Importe(Capital),
            trae(InteresCorriente) ? f.Importe(InteresCorriente) : null,
            trae(InteresDeMora) ? f.Importe(InteresDeMora) : null,
            trae(Otros) ? f.Importe(Otros) : null,
            trae(DiasDeMora) ? f.Importe(DiasDeMora) : null,
            trae(Calificacion) ? Letra(f.Texto(Calificacion)) : null,
        ])).ToList();

        var notas = new List<string>
        {
            "Lo del ERP: la cartera viva al corte con el mismo estado que el estado de cuenta (intereses con su contingente; «Otros» = seguro + administración + otros cargos).",
            "Del ERP se listan los créditos vivos con saldo que el archivo no trae.",
        };
        if (!ambito.EsTotal) notas.Add("Su alcance de Cartera no es de toda la cooperativa: sólo entran los créditos de sus oficinas y líneas.");
        if (CarteraAlCorte.NotaDeErrores(creditos) is { } errorDeCalculo) notas.Add(errorDeCalculo);
        if (repetidos > 0) notas.Add($"{repetidos} crédito(s) del ERP comparten número con otro y no se cotejaron.");
        var faltan = new[] { InteresCorriente, InteresDeMora, Otros, DiasDeMora, Calificacion, Documento }.Where(c => !trae(c)).ToList();
        if (faltan.Count > 0) notas.Add($"El archivo no trae: {string.Join(", ", faltan)}; esas columnas no se comparan.");
        return Result.Success(new CotejoPreparado(definicion, erp, referencia, notas));
    }

    /// <summary>La letra de la calificación («B», «B1», «Categoría B» → «B»); lo que no es una letra de A a E queda como viene.</summary>
    private static string Letra(string texto)
    {
        var t = texto.Trim().ToUpperInvariant();
        if (t.StartsWith("CATEGORIA", StringComparison.Ordinal) || t.StartsWith("CATEGORÍA", StringComparison.Ordinal)) t = t[9..].Trim();
        return t.Length > 0 && t[0] is >= 'A' and <= 'E' && (t.Length == 1 || !char.IsLetter(t[1])) ? t[..1] : t;
    }
}

/// <summary>
/// Aportes y ahorros, cuenta por cuenta (opción <c>cuentas-de-asociados</c>; nuevo). El saldo del ERP es la suma del libro de cuentas con fecha
/// valor hasta el corte (lo mismo que la reconstrucción con todo lo registrado): capital, interés por pagar y bonificación menos cargos en las de
/// captación; las cuatro clases de aporte en las de aportes. La cuenta se empareja por su número de origen (el de la carga) o, si no lo tiene,
/// por el suyo. Con el alcance de Cartera de quien coteja.
/// </summary>
public sealed class FuenteDeCuentasDeAsociados(IApplicationDbContext db, IAlcanceDeCartera alcance) : IFuenteDeCotejo
{
    public const string Cuenta = "Cuenta";
    public const string Documento = "Documento del titular";
    public const string Saldo = "Saldo";

    public string Opcion => OpcionesDeCotejo.CuentasDeAsociados;
    public string Titulo => "Aportes y ahorros por cuenta";
    public string Permiso => OpcionesDeCotejo.PermisoCaptaciones;

    public IReadOnlyList<ColumnaDeReferencia> Columnas { get; } =
    [
        new(Cuenta, true, ClaseDeColumnaDeReferencia.Texto, "El número de la cuenta de ahorro, CDAT o aportes.", "AH-000456", "Número de cuenta", "Número", "Cuenta de ahorro"),
        new(Documento, false, ClaseDeColumnaDeReferencia.Texto, "El documento del titular.", "16123456", "Documento", "Cédula", "Identificación", "NIT"),
        new(Saldo, true, ClaseDeColumnaDeReferencia.Importe, "El saldo al corte (en captaciones con los intereses por pagar).", "820000", "Saldo final", "Saldo al corte", "Valor"),
    ];

    public IReadOnlyList<string> Instrucciones { get; } =
    [
        "Una fila por cuenta con su saldo al corte: ahorro a la vista, contractual, permanente, CDAT o aportes, en el mismo archivo o en varios.",
        "El número se compara sin ceros a la izquierda, espacios ni guiones, contra el número de origen de la cuenta (el de la carga) o, si no lo tiene, el del ERP.",
        "En captaciones el saldo del ERP es capital + interés por pagar + bonificación − cargos por cobrar; en aportes, la suma de sus clases.",
    ];

    public async Task<Result<CotejoPreparado>> PrepararAsync(ArchivoDeReferencia archivo, ParametrosDeCotejo p, CancellationToken ct)
    {
        var errores = new List<ErrorDeFila>();
        var vistas = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var f in archivo.Filas)
        {
            var llave = LecturaDeReferencia.Llave(f.Texto(Cuenta));
            if (llave.Length == 0) errores.Add(new ErrorDeFila(f.Numero, Cuenta, "Implantacion.Cotejo.Vacio", "La fila no trae el número de la cuenta."));
            else if (!vistas.TryAdd(llave, f.Numero)) errores.Add(new ErrorDeFila(f.Numero, Cuenta, "Implantacion.Cotejo.Repetida", $"La cuenta {f.Texto(Cuenta)} ya viene en la fila {vistas[llave]}."));
        }
        if (errores.Count > 0) return Result.Failure<CotejoPreparado>(LecturaDeReferencia.ArchivoInvalido(errores));

        var alcanceDeCartera = await alcance.ObtenerAsync(ct);
        var corte = p.Corte;
        var cuentas = await db.MemberAccounts.AsNoTracking()
            .Where(a => !a.IsDeleted && a.OpenedOn <= corte)
            .PorOficinaYLinea(alcanceDeCartera, a => a.BranchId, a => a.CreditLineId)
            .Select(a => new { a.Id, a.Number, a.SourceAccountNumber, a.Nature, a.CreditLineId, a.HolderPersonId })
            .ToListAsync(ct);
        var ids = cuentas.Select(c => c.Id).ToList();
        var sumas = await db.MemberAccountTransactions.AsNoTracking()
            .Where(t => t.ValueDate <= corte)
            .Where(t => ids.Contains(t.MemberAccountId))
            .GroupBy(t => new { t.MemberAccountId, t.Concept })
            .Select(g => new { g.Key.MemberAccountId, g.Key.Concept, Monto = g.Sum(t => t.Amount) })
            .ToListAsync(ct);
        var porCuenta = sumas.GroupBy(s => s.MemberAccountId).ToDictionary(g => g.Key, g => g.ToDictionary(x => x.Concept, x => x.Monto));
        var lineaIds = cuentas.Select(c => c.CreditLineId).Distinct().ToList();
        var lineas = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => lineaIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => $"{l.Code} {l.Name}", ct);
        var personas = await CarteraAlCorte.PersonasAsync(db, cuentas.Select(c => c.HolderPersonId).Distinct().ToList(), ct);

        var erp = new Dictionary<string, PartidaDeCotejo>(StringComparer.Ordinal);
        var repetidas = 0;
        foreach (var c in cuentas)
        {
            var conceptos = porCuenta.GetValueOrDefault(c.Id) ?? [];
            decimal S(MemberAccountConcept k) => conceptos.GetValueOrDefault(k);
            var saldo = c.Nature == LineNature.Contribution
                ? MemberAccountTransaction.ConceptosDeAportes.Sum(S)
                : S(MemberAccountConcept.Principal) + S(MemberAccountConcept.InterestPayable) + S(MemberAccountConcept.Bonus) - S(MemberAccountConcept.ChargeReceivable);
            var visible = string.IsNullOrWhiteSpace(c.SourceAccountNumber) ? c.Number : c.SourceAccountNumber!;
            var llave = LecturaDeReferencia.Llave(visible);
            if (!vistas.ContainsKey(llave) && saldo == 0m) continue;
            var titular = personas.GetValueOrDefault(c.HolderPersonId);
            var partida = new PartidaDeCotejo(llave, visible, lineas.GetValueOrDefault(c.CreditLineId) ?? string.Empty, [titular?.Nombre, c.Number],
                [LecturaDeReferencia.Documento(titular?.Documento), saldo]);
            if (!erp.TryAdd(llave, partida)) repetidas++;
        }

        var definicion = new DefinicionDeCotejo("Cotejo de aportes y ahorros por cuenta", "Cuenta", ["Titular", "Número en el ERP"],
            [new MedidaDeCotejo(Documento, ClaseDeMedida.Texto), new MedidaDeCotejo(Saldo, ClaseDeMedida.Moneda, p.Tolerancia)], "Línea");
        var referencia = archivo.Filas.Select(f => new PartidaDeCotejo(LecturaDeReferencia.Llave(f.Texto(Cuenta)), f.Texto(Cuenta), "(no está en el ERP)", [],
            [archivo.Trae(Documento) ? LecturaDeReferencia.Documento(f.Texto(Documento)) : null, f.Importe(Saldo)])).ToList();

        var notas = new List<string>
        {
            "Lo del ERP: la suma del libro de cuentas con fecha valor hasta el corte. Captación = capital + interés por pagar + bonificación − cargos por cobrar; aportes = sus cuatro clases.",
            "Del ERP se listan las cuentas con saldo que el archivo no trae.",
        };
        if (!alcanceDeCartera.TodasLasOficinas || !alcanceDeCartera.TodasLasLineas)
            notas.Add("Su alcance de Cartera no es de toda la cooperativa: sólo entran las cuentas de sus oficinas y líneas.");
        if (repetidas > 0) notas.Add($"{repetidas} cuenta(s) del ERP comparten número con otra y no se cotejaron.");
        return Result.Success(new CotejoPreparado(definicion, erp.Values.ToList(), referencia, notas));
    }
}

/// <summary>
/// Asociados (opción <c>asociados</c>; nuevo): que cada asociado del archivo exista en el ERP con el mismo estado al corte, y qué asociados del
/// ERP el archivo no trae. Se empareja por el documento (sin puntos ni ceros a la izquierda). Un asociado es «Retirado» al corte si su estado es
/// de retiro y la fecha de retiro no es posterior; los que ingresaron después del corte no entran. Con el alcance de oficinas de Cartera.
/// </summary>
public sealed class FuenteDeAsociados(IApplicationDbContext db, IAlcanceDeCartera alcance) : IFuenteDeCotejo
{
    public const string Documento = "Documento";
    public const string Nombre = "Nombre";
    public const string Estado = "Estado";

    private const string Activo = "Activo";
    private const string Retirado = "Retirado";

    public string Opcion => OpcionesDeCotejo.Asociados;
    public string Titulo => "Asociados";
    public string Permiso => OpcionesDeCotejo.PermisoAsociados;

    public IReadOnlyList<ColumnaDeReferencia> Columnas { get; } =
    [
        new(Documento, true, ClaseDeColumnaDeReferencia.Texto, "El documento del asociado.", "16123456", "Documento del asociado", "Cédula", "Identificación", "NIT"),
        new(Nombre, false, ClaseDeColumnaDeReferencia.Texto, "El nombre (sólo se muestra).", "María Pérez Gómez", "Nombre del asociado", "Nombres"),
        new(Estado, false, ClaseDeColumnaDeReferencia.Texto, "Activo o Retirado (también A/R, Vigente, Inactivo).", "Activo", "Estado del asociado"),
    ];

    public IReadOnlyList<string> Instrucciones { get; } =
    [
        "Una fila por asociado. Sin la columna Estado se coteja sólo que exista.",
        "Si el archivo no trae retirados, los retirados del ERP no se listan como «sólo en el ERP».",
        "Un documento repetido en el archivo es un error.",
    ];

    public async Task<Result<CotejoPreparado>> PrepararAsync(ArchivoDeReferencia archivo, ParametrosDeCotejo p, CancellationToken ct)
    {
        var errores = new List<ErrorDeFila>();
        var vistas = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var f in archivo.Filas)
        {
            var doc = LecturaDeReferencia.Documento(f.Texto(Documento));
            if (doc.Length == 0) errores.Add(new ErrorDeFila(f.Numero, Documento, "Implantacion.Cotejo.Vacio", "La fila no trae el documento."));
            else if (!vistas.TryAdd(doc, f.Numero)) errores.Add(new ErrorDeFila(f.Numero, Documento, "Implantacion.Cotejo.Repetida", $"El documento {f.Texto(Documento)} ya viene en la fila {vistas[doc]}."));
        }
        if (errores.Count > 0) return Result.Failure<CotejoPreparado>(LecturaDeReferencia.ArchivoInvalido(errores));

        var conEstado = archivo.Trae(Estado);
        var referencia = archivo.Filas.Select(f =>
        {
            var estado = conEstado ? EstadoDeReferencia(f.Texto(Estado)) : null;
            return new PartidaDeCotejo(LecturaDeReferencia.Documento(f.Texto(Documento)), f.Texto(Documento), estado ?? "(sin estado)",
                [string.IsNullOrWhiteSpace(f.Texto(Nombre)) ? null : f.Texto(Nombre).Trim()], [estado]);
        }).ToList();
        var traeRetirados = referencia.Any(r => r.Valores[0] as string == Retirado);

        var alcanceDeCartera = await alcance.ObtenerAsync(ct);
        var corte = p.Corte;
        var filas = await (from a in db.Associates.AsNoTracking()
                           join per in db.People.AsNoTracking().IgnoreQueryFilters() on a.PersonId equals per.Id
                           where !a.IsDeleted && (a.JoinDate == null || a.JoinDate <= corte)
                           select new { a.PersonId, a.BranchId, a.Status, a.WithdrawalDate, per.TaxId })
            .ToListAsync(ct);
        filas = filas.Where(x => alcanceDeCartera.TodasLasOficinas || (x.BranchId is { } o && alcanceDeCartera.IncluyeOficina(o))).ToList();
        var personas = await CarteraAlCorte.PersonasAsync(db, filas.Select(x => x.PersonId).Distinct().ToList(), ct);

        var erp = new Dictionary<string, (PartidaDeCotejo Partida, int Veces)>(StringComparer.Ordinal);
        var retiradosFuera = 0;
        foreach (var x in filas)
        {
            var doc = LecturaDeReferencia.Documento(x.TaxId);
            if (doc.Length == 0) continue;
            var retirado = AssociateRegistrar.EsRetirado(x.Status) && (x.WithdrawalDate is null || x.WithdrawalDate <= corte);
            var estado = retirado ? Retirado : Activo;
            if (retirado && !traeRetirados && !vistas.ContainsKey(doc)) { retiradosFuera++; continue; }
            var nombre = personas.GetValueOrDefault(x.PersonId)?.Nombre;
            if (erp.TryGetValue(doc, out var ya))
            {
                // Dos fichas con el mismo documento (CC y CE, o una ficha repetida): manda la activa y se avisa.
                var mejor = (ya.Partida.Valores[0] as string) == Activo ? ya.Partida : new PartidaDeCotejo(doc, x.TaxId, estado, [nombre], [estado]);
                erp[doc] = (mejor with { Nota = $"El documento está en {ya.Veces + 1} fichas de asociado del ERP." }, ya.Veces + 1);
                continue;
            }
            erp[doc] = (new PartidaDeCotejo(doc, x.TaxId, estado, [nombre], [estado]), 1);
        }

        var definicion = new DefinicionDeCotejo("Cotejo de asociados", "Documento", ["Nombre"], [new MedidaDeCotejo(Estado, ClaseDeMedida.Texto)], "Estado");
        var notas = new List<string>
        {
            "Retirado al corte = estado de retiro con fecha de retiro hasta el corte. Los asociados que ingresaron después del corte no entran.",
        };
        if (!conEstado) notas.Add("El archivo no trae la columna Estado: sólo se coteja que cada asociado exista.");
        if (retiradosFuera > 0) notas.Add($"El archivo no trae retirados: {retiradosFuera:N0} retirado(s) del ERP no se listan.");
        if (!alcanceDeCartera.TodasLasOficinas) notas.Add("Su alcance no es de toda la cooperativa: sólo entran los asociados de sus oficinas.");
        // Sin la columna Estado, el ERP tampoco compara estado (la medida va nula en la referencia): el grupo del resumen sigue siendo el del ERP.
        return Result.Success(new CotejoPreparado(definicion, erp.Values.Select(v => v.Partida).ToList(), referencia, notas));
    }

    /// <summary>El estado de la referencia llevado a Activo o Retirado; lo que no se reconoce queda como viene y difiere.</summary>
    private static string? EstadoDeReferencia(string texto)
    {
        var t = texto.Trim().ToUpperInvariant();
        if (t.Length == 0) return string.Empty;
        if (t is "A" or "V" or "1" || t.StartsWith("ACT", StringComparison.Ordinal) || t.StartsWith("VIG", StringComparison.Ordinal) || t.StartsWith("HAB", StringComparison.Ordinal))
            return Activo;
        if (t is "R" or "I" or "0" || t.StartsWith("RET", StringComparison.Ordinal) || t.StartsWith("INA", StringComparison.Ordinal) || t.StartsWith("DES", StringComparison.Ordinal))
            return Retirado;
        return texto.Trim();
    }
}
