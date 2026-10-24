using System.Reflection;
using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 015, T0001 (R-22; FR-004; contracts/mensajes.md; contracts/contabilidad.md §1): los hechos que Recaudo y Tesorería emiten a
/// Contabilidad llevan dimensiones por código (punto, caja, medio, concepto, cuenta propia por su <c>PublicId</c>) y <b>nunca una cuenta
/// contable</b>: la cuenta la decide la matriz (<c>ACC_ReceiptsPostingRules</c>, <c>ACC_TreasuryPostingRules</c>). La cuenta bancaria propia
/// (<c>OwnBankAccount*</c>) y la del beneficiario (<c>BankAccount*</c>, <c>PersonBankAccount*</c>) no son cuentas contables. Molde:
/// <see cref="LosHechosDeCarteraNoLlevanCuentas"/>.
///
/// <para>
/// Árbol que recorre: <see cref="Carpetas"/>, los contratos de mensaje de los dos módulos. La lista nace vacía en el Setup y la llena la
/// fase 16 (US8) con <c>Application/Common/Integration/Contracts/{Receipts,Treasury}</c> cuando nazcan los hechos; con la lista vacía no hay
/// nada que violar.
/// </para>
/// </summary>
public class LosHechosDeRecaudoYTesoreriaNoLlevanCuentas
{
    /// <summary>Carpetas de contratos de mensaje de Recaudo y Tesorería (relativas a <c>src/</c>).</summary>
    private static readonly string[] Carpetas =
    [
        // Feature 015, US2 (T0091): los hechos de la caja.
        "Core/IngenIA365ERP.Application/Common/Integration/Contracts/Receipts",
        // Fase 16 (T0254): los hechos de Tesorería.
        "Core/IngenIA365ERP.Application/Common/Integration/Contracts/Treasury",
    ];

    /// <summary>Propiedades con <c>Account</c> en el nombre que no son cuentas contables.</summary>
    private static readonly Regex QueNoSonCuentas = new(@"^(OwnBankAccount\w*|PersonBankAccount\w*|BankAccount\w*|DestinationAccount\w*)$", RegexOptions.Compiled);

    private static readonly Regex NombreDeCuenta = new(
        @"^(Account\w*|Puc\w*|Cuenta\w*|\w*(Ledger|Accounting|Chart)Account\w*|\w*AccountCode|\w*AccountId)$", RegexOptions.Compiled);

    [Fact]
    public void Ningun_record_de_los_contratos_tiene_propiedades_de_cuenta()
    {
        var root = RepoPath.FindRepoRoot();
        var archivos = ReglaSobreFuente.ArchivosEn(Carpetas).ToList();
        var espacios = archivos
            .Select(a => Regex.Match(File.ReadAllText(a), @"^namespace\s+([\w.]+)\s*;", RegexOptions.Multiline))
            .Where(m => m.Success).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

        var infracciones = typeof(IngenIA365ERP.Application.DependencyInjection).Assembly.GetTypes()
            .Where(t => t.Namespace is not null && espacios.Contains(t.Namespace) && t.IsClass && !t.IsAbstract)
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => (Tipo: t, Propiedad: p)))
            .Where(x => NombreDeCuenta.IsMatch(x.Propiedad.Name) && !QueNoSonCuentas.IsMatch(x.Propiedad.Name))
            .Select(x => $"{x.Tipo.Name}.{x.Propiedad.Name}")
            .ToList();

        Assert.True(Carpetas.Length == 0 || archivos.Count > 0, "La lista nombra carpetas sin contratos: si se movieron, actualizá Carpetas.");
        ReglaSobreFuente.SinInfracciones(infracciones,
            $"Un hecho de Recaudo o Tesorería lleva una cuenta contable (FR-004); revisados {archivos.Count} archivos bajo {root}");
    }
}
