using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;
using IngenIA365ERP.Domain.Sales.Payments;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, T003 y T560 (decisiones-transversales T25, §2.18), FR-096, FR-101: de un pago con tarjeta se guardan los
/// cuatro últimos dígitos (<c>Last4</c>), la franquicia y la autorización, nunca el número completo. Guardarlo pondría a la
/// cooperativa bajo PCI DSS por una columna que nadie usa.
///
/// <para>Tres comprobaciones:</para>
/// <list type="number">
/// <item>ninguna entidad, DTO, <c>*Input</c> ni record <c>*V1</c> de las carpetas de pagos (<see cref="CarpetasDePago"/>) declara
/// una propiedad —o un parámetro posicional de record— con nombre de número de tarjeta; además, cada entidad de
/// <see cref="EntidadesDePago"/> tiene que existir (si se renombra, se actualiza la lista);</item>
/// <item><c>DocumentPayment.Last4</c> es <c>char(4)</c> en las dos configuraciones EF (las instantáneas de SQL Server y
/// PostgreSQL): una columna más ancha dejaría caber el número;</item>
/// <item><see cref="ValidadorDePagos"/> rechaza con <c>Payments.CardNumberNotAllowed</c> todo campo con forma de número de
/// tarjeta, con o sin separadores, y deja pasar los cuatro dígitos.</item>
/// </list>
/// </summary>
public class LosPagosNoGuardanElNumeroDeTarjeta
{
    /// <summary>Nombres de tipo de las entidades que registran un pago o un medio (I3).</summary>
    private static readonly string[] EntidadesDePago =
    [
        "PaymentMeans",
        "CardNetwork",
        "CardAcquirer",
        "CardTerminal",
        "DocumentPayment",
        "VoucherRedemption",
        "CashMovementDetail",
        "CashCountLine",
        "CashCountTerminalBatch",
        "CashCountReferenceCheck",
    ];

    /// <summary>
    /// Carpetas (relativas a la raíz) donde vive lo que toca un pago: entidades, comandos y DTOs de Application, contratos de
    /// mensajes y los DTOs del cliente de ventas. Una que todavía no existe cuenta como vacía.
    /// </summary>
    private static readonly string[] CarpetasDePago =
    [
        Path.Combine("src", "Core", "IngenIA365ERP.Domain", "Entities", "Core", "Payments"),
        Path.Combine("src", "Core", "IngenIA365ERP.Domain", "Entities", "Inventory"),
        Path.Combine("src", "Core", "IngenIA365ERP.Domain", "Sales", "Payments"),
        Path.Combine("src", "Core", "IngenIA365ERP.Application", "Core", "PaymentMeans"),
        Path.Combine("src", "Core", "IngenIA365ERP.Application", "Inventory"),
        Path.Combine("src", "Core", "IngenIA365ERP.Application", "Common", "Integration", "Contracts", "Inventory"),
        Path.Combine("src", "Presentation", "IngenIA365ERP.Shared", "Services", "Ventas"),
    ];

    private const string NombresProhibidos = "CardNumber|CardPan|Pan|PrimaryAccountNumber|NumeroDeTarjeta|NumeroTarjeta|FullCardNumber";

    /// <summary>Una propiedad pública que guardaría el número de la tarjeta (PAN) completo.</summary>
    private static readonly Regex PropiedadDeTarjeta = new(
        $@"\bpublic\s+[\w?<>\[\],. ]+?\s+({NombresProhibidos})\s*[{{=;]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Un parámetro posicional de record (<c>record X(string CardNumber, …)</c>) con ese nombre.</summary>
    private static readonly Regex ParametroDeTarjeta = new(
        $@"[(,]\s*(\[[^\]]*\]\s*)?[\w?<>\[\].]+\s+({NombresProhibidos})\s*[,)=]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    [Fact]
    public void Ninguna_entidad_de_pago_guarda_el_numero_de_tarjeta()
    {
        var root = RepoPath.FindRepoRoot();
        var fuentes = RepoPath.ProductionCSharpFiles().Select(f => (Archivo: f, Texto: File.ReadAllText(f))).ToList();
        var infractores = new List<string>();

        foreach (var entidad in EntidadesDePago)
        {
            var declaracion = new Regex($@"\bclass\s+{Regex.Escape(entidad)}\b", RegexOptions.Compiled);
            var (archivo, texto) = fuentes.FirstOrDefault(f => declaracion.IsMatch(f.Texto)
                && f.Archivo.Contains(Path.Combine("IngenIA365ERP.Domain", "Entities"), StringComparison.Ordinal));

            if (archivo is null)
                infractores.Add($"{entidad}: no se encontró su declaración (si se renombró, actualizá EntidadesDePago)");
            else if (PropiedadDeTarjeta.IsMatch(texto))
                infractores.Add($"{Path.GetRelativePath(root, archivo)}: {entidad} guarda el número de la tarjeta");
        }

        Assert.True(infractores.Count == 0,
            "Entidades de pago que guardan el número de la tarjeta (T25; sólo Last4 y autorización):\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void Ningun_DTO_ni_entrada_de_pago_lleva_el_numero_de_tarjeta()
    {
        var root = RepoPath.FindRepoRoot();
        var infractores = new List<string>();
        var revisados = 0;

        foreach (var carpeta in CarpetasDePago.Select(c => Path.Combine(root, c)).Where(Directory.Exists))
        {
            foreach (var archivo in Directory.EnumerateFiles(carpeta, "*.cs", SearchOption.AllDirectories))
            {
                revisados++;
                var texto = FuenteSinComentarios.Leer(archivo);
                foreach (Match m in PropiedadDeTarjeta.Matches(texto).Concat(ParametroDeTarjeta.Matches(texto)))
                    infractores.Add($"{Path.GetRelativePath(root, archivo)}: «{m.Value.Trim()}»");
            }
        }

        Assert.True(revisados > 0, "No se encontró ningún archivo en las carpetas de pago: revisar CarpetasDePago.");
        Assert.True(infractores.Count == 0,
            "Tipos de pago con el número de la tarjeta (FR-101, T25; sólo Last4):\n  " + string.Join("\n  ", infractores));
    }

    [Theory]
    [InlineData("IngenIA365ERP.Persistence.Migrations.SqlServer", "nchar(4)")]
    [InlineData("IngenIA365ERP.Persistence.Migrations.PostgreSql", "character(4)")]
    public void Last4_es_char_4_en_las_dos_configuraciones(string proyecto, string tipo)
    {
        var root = RepoPath.FindRepoRoot();
        var instantanea = Path.Combine(root, "src", "Infrastructure", proyecto, "Application", "ApplicationDbContextModelSnapshot.cs");
        Assert.True(File.Exists(instantanea), $"No existe {instantanea}.");

        var texto = File.ReadAllText(instantanea);
        var inicio = texto.IndexOf("Entity(\"IngenIA365ERP.Domain.Entities.Inventory.Documents.DocumentPayment\"", StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"{proyecto}: la instantánea no tiene DocumentPayment.");
        var fin = texto.IndexOf("modelBuilder.Entity(", inicio + 1, StringComparison.Ordinal);
        var bloque = fin > 0 ? texto[inicio..fin] : texto[inicio..];

        var last4 = Regex.Match(bloque, @"Property<string>\(""Last4""\)(?<def>[^;]*);");
        Assert.True(last4.Success, $"{proyecto}: DocumentPayment no tiene Last4.");
        var definicion = last4.Groups["def"].Value;
        Assert.Contains(".HasMaxLength(4)", definicion);
        Assert.Contains($".HasColumnType(\"{tipo}\")", definicion);
        Assert.Contains(".IsFixedLength()", definicion);
    }

    [Theory]
    [InlineData("4111111111111111")]
    [InlineData("4111 1111 1111 1111")]
    [InlineData("4111-1111-1111-1111")]
    [InlineData("5500000000000004")]
    [InlineData("378282246310005")]
    public void El_validador_reconoce_un_numero_de_tarjeta(string texto) =>
        Assert.True(ValidadorDePagos.PareceNumeroDeTarjeta(texto), $"«{texto}» tiene forma de número de tarjeta.");

    [Theory]
    [InlineData("1234")]
    [InlineData("0042")]
    [InlineData("AUT-998877")]
    [InlineData("")]
    [InlineData(null)]
    public void El_validador_deja_pasar_los_cuatro_digitos_y_las_referencias(string? texto) =>
        Assert.False(ValidadorDePagos.PareceNumeroDeTarjeta(texto), $"«{texto}» no es un número de tarjeta.");

    [Fact]
    public void El_codigo_de_rechazo_es_el_canonico() =>
        Assert.Equal("Payments.CardNumberNotAllowed", ValidadorDePagos.CardNumberNotAllowed);
}
