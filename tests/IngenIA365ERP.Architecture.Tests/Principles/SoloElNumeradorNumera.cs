using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, T002 (decisiones-transversales T16, §2.18), FR-038: los consecutivos son por tipo de
/// documento y prefijo y los asigna <b>un solo mecanismo</b>, el numerador, que incrementa la fila
/// de numeración (<c>INV_DocumentSequences.NextValue</c> o la resolución DIAN
/// <c>LastIssuedNumber</c>) dentro de la transacción de confirmación. Otro código que la toque
/// produce saltos o números repetidos.
///
/// <para>
/// La base de inventario (fase 3, T117) llenó <see cref="PropiedadesDeNumeracion"/> con <c>NextValue</c> y
/// <c>LastIssuedNumber</c> (la de I4 aún no existe: si alguien la escribe antes, falla igual) y agregó las reglas
/// del número del documento y del nombre <c>NextNumber</c>; <see cref="NumeradoresAutorizados"/> nombra los archivos
/// que sí pueden escribirlas.
/// </para>
/// </summary>
public class SoloElNumeradorNumera
{
    /// <summary>Propiedades de las filas de numeración. Las agrega el bloque que crea cada una.</summary>
    private static readonly string[] PropiedadesDeNumeracion =
    [
        // Base de inventario, fase 3 (T134, T139): el consecutivo de INV_DocumentSequences.
        "NextValue",
        // Entrega I4 (US8): COR_DianNumberingResolutions.
        "LastIssuedNumber",
        // Feature 015 (T0021; R-13): COR_Sequences, la numeración de Recaudo (RCD:{código}:{prefijo}) y Tesorería (TES:{código}).
        "NextSequence",
    ];

    /// <summary>
    /// Feature 015 (T0021; R-13; FR-007, FR-026): en Recaudo y en la Tesorería nueva el <c>Number</c> de un documento lo asignan sólo sus
    /// numeradores (los documentos lo reciben del valor que el numerador devuelve, en su inicializador o en la confirmación).
    /// </summary>
    [Fact]
    public void En_Recaudo_y_Tesoreria_solo_los_numeradores_asignan_numeros()
    {
        var root = RepoPath.FindRepoRoot();
        var escritura = new Regex(@"\.Number\s*(=(?!=)|\+=|-=|\+\+|--)", RegexOptions.Compiled);
        var infractores = RecaudoYTesoreria.ArchivosDeRecaudo().Concat(RecaudoYTesoreria.ArchivosDeTesoreria())
            .Where(f => Path.GetFileName(f) is not ("NumeradorDeRecaudo.cs" or "NumeradorDeTesoreria.cs"))
            .Where(f => escritura.IsMatch(FuenteSinComentarios.Leer(f)))
            .Select(f => Path.GetRelativePath(root, f))
            .ToList();
        Assert.True(infractores.Count == 0, "Número de Recaudo o Tesorería asignado fuera de su numerador (R-13):\n  " + string.Join("\n  ", infractores));
        Assert.True(File.Exists(Path.Combine(root, "src", "Core", "IngenIA365ERP.Application", "Receipts", "Common", "NumeradorDeRecaudo.cs")));
        Assert.True(File.Exists(Path.Combine(root, "src", "Core", "IngenIA365ERP.Application", "Treasury", "Common", "NumeradorDeTesoreria.cs")));
    }

    /// <summary>Nombres de archivo (sin ruta) que pueden escribirlas.</summary>
    private static readonly string[] NumeradoresAutorizados = ["Numerador.cs", "NumeradorFiscal.cs", "NumeradorDeCartera.cs",
        // Feature 015 (T0021; R-13): los únicos escritores de COR_Sequences para RCD: y TES:.
        "NumeradorDeRecaudo.cs", "NumeradorDeTesoreria.cs"];

    /// <summary>
    /// Feature 013 (T0144, C48): en Cartera el <c>Number</c> de toda tabla y el <c>NextValue</c> de <c>LND_NumberSequences</c> los
    /// escribe sólo <c>NumeradorDeCartera</c> (el de un hecho nace en su inicializador con el número que el numerador devolvió).
    /// </summary>
    [Fact]
    public void En_Cartera_solo_el_numerador_asigna_numeros()
    {
        var root = RepoPath.FindRepoRoot();
        var escritura = new Regex(@"\.Number\s*(=(?!=)|\+=|-=|\+\+|--)", RegexOptions.Compiled);
        var infractores = RepoPath.ProductionCSharpFiles()
            .Where(f => f.Replace(Path.DirectorySeparatorChar, '/').Contains("/Lending/", StringComparison.Ordinal))
            .Where(f => Path.GetFileName(f) != "NumeradorDeCartera.cs")
            .Where(f => escritura.IsMatch(FuenteSinComentarios.Leer(f)))
            .Select(f => Path.GetRelativePath(root, f))
            .ToList();

        Assert.True(infractores.Count == 0, "Número de Cartera asignado fuera de NumeradorDeCartera (C48):\n  " + string.Join("\n  ", infractores));
        Assert.True(File.Exists(Path.Combine(root, "src", "Core", "IngenIA365ERP.Application", "Lending", "Common", "NumeradorDeCartera.cs")));
    }

    /// <summary>
    /// Carpetas (fragmentos de ruta) del comercio donde <c>Number</c> es el número de un documento de inventario y sólo lo
    /// asigna el numerador. Fuera de ellas <c>Number</c> es de otros módulos (el comprobante contable lo numera su
    /// propio <c>AccountingPoster</c>, vigilado por <c>NingunModuloEscribeMovimientosFueraDelContrato</c>).
    /// </summary>
    private static readonly string[] CarpetasDelComercio =
    [
        "/Inventory/", "/Sales/", "/ElectronicInvoicing/", "/Purchasing/", "/Pos/",
    ];

    private static IEnumerable<string> ArchivosDelComercio() =>
        RepoPath.ProductionCSharpFiles()
            .Where(f => CarpetasDelComercio.Any(c => f.Replace(Path.DirectorySeparatorChar, '/').Contains(c, StringComparison.Ordinal)));

    [Fact]
    public void En_el_comercio_solo_el_numerador_asigna_el_numero_del_documento()
    {
        var root = RepoPath.FindRepoRoot();
        var escritura = new Regex(@"\.Number\s*(=(?!=)|\+=|-=|\+\+|--)", RegexOptions.Compiled);
        var infractores = ArchivosDelComercio()
            .Where(f => !NumeradoresAutorizados.Contains(Path.GetFileName(f), StringComparer.Ordinal))
            .Where(f => escritura.IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(root, f))
            .ToList();

        Assert.True(infractores.Count == 0,
            "Número de documento del comercio asignado fuera del numerador (FR-038, T16):\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void Ninguna_propiedad_del_comercio_se_llama_NextNumber()
    {
        var root = RepoPath.FindRepoRoot();
        var declaracion = new Regex(@"\bNextNumber\s*\{", RegexOptions.Compiled);
        var infractores = ArchivosDelComercio()
            .Where(f => declaracion.IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(root, f))
            .ToList();

        Assert.True(infractores.Count == 0,
            "Propiedades NextNumber en el comercio: se llaman NextValue o LastIssuedNumber (§2.1):\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void Solo_el_numerador_escribe_los_consecutivos()
    {
        var root = RepoPath.FindRepoRoot();
        var fuentes = RepoPath.ProductionCSharpFiles().Select(f => (Archivo: f, Texto: File.ReadAllText(f))).ToList();
        var infractores = new List<string>();

        foreach (var propiedad in PropiedadesDeNumeracion)
        {
            // Asignación, suma compuesta o incremento sobre la propiedad (no su declaración ni su lectura).
            var escritura = new Regex($@"\.{Regex.Escape(propiedad)}\s*(=(?!=)|\+=|-=|\+\+|--)|(\+\+|--)\s*\w+\.{Regex.Escape(propiedad)}\b", RegexOptions.Compiled);
            foreach (var (archivo, texto) in fuentes)
            {
                if (NumeradoresAutorizados.Contains(Path.GetFileName(archivo), StringComparer.Ordinal)) continue;
                if (escritura.IsMatch(texto))
                    infractores.Add($"{Path.GetRelativePath(root, archivo)}: escribe {propiedad} fuera del numerador");
            }
        }

        Assert.True(infractores.Count == 0,
            "Consecutivos escritos fuera del numerador (FR-038, T16):\n  " + string.Join("\n  ", infractores));
    }

    /// <summary>
    /// I4, T705 (FR-066): la vía del caso b que reusa el número del rechazado sin consumir la resolución la invoca sólo
    /// <c>ReplaceRejectedDocumentCommand</c> (además de su declaración en <c>NumeradorFiscal</c>).
    /// </summary>
    [Fact]
    public void LastIssuedNumber_solo_lo_escribe_el_NumeradorFiscal()
    {
        // T671 (data-model §18, §26): el consecutivo de una resolución DIAN lo escribe un único archivo —no basta el nombre: la ruta
        // completa—. La entidad lo inicializa en RangeFrom − 1 dentro de su propio setter (sin «.», así que no cuenta).
        var root = RepoPath.FindRepoRoot();
        const string autorizado = "src/Core/IngenIA365ERP.Application/ElectronicInvoicing/Numeracion/NumeradorFiscal.cs";
        var escritura = new Regex(@"\.LastIssuedNumber\s*(=(?!=)|\+=|-=|\+\+|--)|(\+\+|--)\s*\w+\.LastIssuedNumber\b", RegexOptions.Compiled);
        var escritores = RepoPath.ProductionCSharpFiles()
            .Where(f => escritura.IsMatch(FuenteSinComentarios.Leer(f)))
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .ToList();

        Assert.True(escritores.Count == 1 && escritores[0] == autorizado,
            "DianNumberingResolution.LastIssuedNumber lo escribe sólo NumeradorFiscal (T671). Lo escriben:\n  " + string.Join("\n  ", escritores));
    }

    /// <summary>
    /// I5, T773 (T802, T42f; dian.md §4.3): el consecutivo de un evento RADIAN (030 o 032) lo calcula sólo
    /// <c>NumeradorFiscal.NumerarEventoAsync</c> —el siguiente por (ambiente, prefijo del evento) bajo su cerrojo— y sólo lo pide
    /// <c>EmitRadianEventCommand</c>. Ningún otro archivo que conozca los eventos RADIAN saca el máximo de <c>Consecutive</c>: sería una
    /// segunda numeración con saltos o repetidos (el índice único del documento electrónico lo haría fallar tarde).
    /// </summary>
    [Fact]
    public void El_consecutivo_de_los_eventos_RADIAN_solo_lo_escribe_el_NumeradorFiscal()
    {
        var root = RepoPath.FindRepoRoot();
        var llamada = new Regex(@"\bNumerarEventoAsync\s*\(", RegexOptions.Compiled);
        var maximo = new Regex(@"\bMax(Async)?\s*\([^;]*\.Consecutive\b", RegexOptions.Compiled);
        var conoceEventos = new Regex(@"\bRadianEvent0(30|32)\b", RegexOptions.Compiled);
        var permitidosParaPedir = new[] { "NumeradorFiscal.cs", "EmitRadianEventCommand.cs" };
        var infractores = new List<string>();
        var pedidos = 0;

        foreach (var archivo in RepoPath.ProductionCSharpFiles())
        {
            var texto = FuenteSinComentarios.Leer(archivo);
            var nombre = Path.GetFileName(archivo);
            var relativo = Path.GetRelativePath(root, archivo);
            if (llamada.IsMatch(texto))
            {
                if (!permitidosParaPedir.Contains(nombre, StringComparer.Ordinal)) infractores.Add($"{relativo}: pide el número de un evento RADIAN");
                else if (nombre == "EmitRadianEventCommand.cs") pedidos++;
            }
            if (nombre != "NumeradorFiscal.cs" && conoceEventos.IsMatch(texto) && maximo.IsMatch(texto))
                infractores.Add($"{relativo}: calcula el siguiente consecutivo de un evento fuera del NumeradorFiscal");
        }

        Assert.True(pedidos == 1, "EmitRadianEventCommand debe pedir el número del evento a NumeradorFiscal.NumerarEventoAsync (T802).");
        Assert.True(infractores.Count == 0,
            "Consecutivos de eventos RADIAN fuera del NumeradorFiscal (FR-038, T16, T802):\n  " + string.Join("\n  ", infractores));
    }

    /// <summary>
    /// Feature 014 (T037, T032; data-model §0.4, §1.3 con research E03): las clases de número de captaciones —<c>MemberAccount</c> 16,
    /// <c>MemberAccountOperation</c> 17, <c>MembershipWithdrawal</c> 18, <c>AssemblyDistribution</c> 19 y <c>DeceasedHolderCase</c> 20— las numera sólo
    /// <c>NumeradorDeCartera</c>: él las nombra todas, busca los números ya usados de las cuentas y sus operaciones (y de las órdenes de giro de cuentas,
    /// que comparten consecutivo con las de desembolso), y ningún otro archivo de producción las pide a <c>AsignarAsync</c> directamente ni asigna el
    /// <c>Number</c> de una cuenta, una operación de cuenta o una orden de giro de cuentas.
    /// </summary>
    [Fact]
    public void Las_clases_de_numero_de_captaciones_las_numera_el_numerador()
    {
        var root = RepoPath.FindRepoRoot();
        var clases = new Dictionary<string, int>
        {
            ["MemberAccount"] = 16, ["MemberAccountOperation"] = 17, ["MembershipWithdrawal"] = 18, ["AssemblyDistribution"] = 19, ["DeceasedHolderCase"] = 20,
        };
        foreach (var (nombre, valor) in clases)
            Assert.Equal(valor, (int)Enum.Parse<IngenIA365ERP.Domain.Enums.Lending.LendingNumberKind>(nombre));

        var numerador = FuenteSinComentarios.Leer(Path.Combine(root, "src", "Core", "IngenIA365ERP.Application", "Lending", "Common", "NumeradorDeCartera.cs"));
        foreach (var nombre in clases.Keys)
            Assert.Contains($"LendingNumberKind.{nombre}", numerador, StringComparison.Ordinal);
        Assert.Matches(@"LendingNumberKind\.MemberAccount\s*=>\s*db\.MemberAccounts", numerador);
        Assert.Matches(@"LendingNumberKind\.MemberAccountOperation\s*=>\s*db\.MemberAccountOperations", numerador);
        Assert.Contains("db.AccountPayoutOrders", numerador, StringComparison.Ordinal);

        var pideDirecto = new Regex(@"\bAsignarAsync\(\s*LendingNumberKind\.(MemberAccount|MemberAccountOperation|MembershipWithdrawal|AssemblyDistribution|DeceasedHolderCase)\b",
            RegexOptions.Compiled);
        var infractores = RepoPath.ProductionCSharpFiles()
            .Where(f => Path.GetFileName(f) != "NumeradorDeCartera.cs")
            .Where(f => pideDirecto.IsMatch(FuenteSinComentarios.Leer(f)))
            .Select(f => Path.GetRelativePath(root, f))
            .ToList();
        Assert.True(infractores.Count == 0, "Números de captaciones pedidos fuera de los métodos del numerador (C48):\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void Solo_el_reemplazo_del_caso_b_reutiliza_un_numero_fiscal()
    {
        var root = RepoPath.FindRepoRoot();
        var llamada = new Regex(@"\bReutilizarNumeroParaReemplazo\s*\(", RegexOptions.Compiled);
        var permitidos = new[] { "NumeradorFiscal.cs", "ReplaceRejectedDocumentCommand.cs" };
        var infractores = RepoPath.ProductionCSharpFiles()
            .Where(f => !permitidos.Contains(Path.GetFileName(f), StringComparer.Ordinal))
            .Where(f => llamada.IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(root, f))
            .ToList();

        Assert.True(infractores.Count == 0,
            "ReutilizarNumeroParaReemplazo llamado fuera del caso b (FR-066, T705):\n  " + string.Join("\n  ", infractores));
    }
}
