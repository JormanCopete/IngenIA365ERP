using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 015, US25, T0273b (E-04 a la 009; TR41; data-model §19): la conciliación bancaria es de Tesorería (<c>TES_Reconciliations</c>). Las tablas
/// <c>ACC_BankReconciliations</c>, <c>ACC_BankStatementLines</c> y <c>ACC_BankStatementColumnMaps</c> quedan sin uso hasta su retiro (migración destructiva aparte):
/// ningún tipo de <c>Application</c> ni de <c>API</c> nombra sus <c>DbSet</c> (<c>BankReconciliations</c>, <c>BankStatementLines</c>, <c>BankStatementColumnMaps</c>) para
/// agregar, modificar ni borrar; sólo los declara <c>IApplicationDbContext</c>. Hasta la fase 17 el único escritor era <c>ReopenPeriodCommandHandler</c>, que hoy llama a
/// <c>IConciliacionBancariaParaContabilidad</c>. Los de Tesorería (<c>TreasuryBankStatementLines</c>…) no cuentan. Prueba escrita, sin correr.
/// </summary>
public class NadieEscribeLaConciliacionBancariaDeContabilidad
{
    private static readonly string[] Carpetas = ["Core/IngenIA365ERP.Application", "Presentation/IngenIA365ERP.API"];

    /// <summary>Un <c>DbSet</c> de la conciliación de Contabilidad nombrado como miembro (<c>db.BankReconciliations</c>, <c>context.BankStatementLines</c>).</summary>
    internal static readonly Regex Toca = new(@"\.\s*(BankReconciliations|BankStatementLines|BankStatementColumnMaps)\b", RegexOptions.Compiled);

    private static bool Exento(string relativo) =>
        relativo.EndsWith("Common/Interfaces/IApplicationDbContext.cs", StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void Ningun_tipo_escribe_la_conciliacion_bancaria_de_Contabilidad()
    {
        var archivos = ReglaSobreFuente.ArchivosEn(Carpetas).ToList();
        Assert.NotEmpty(archivos);
        var infracciones = ReglaSobreFuente.Infracciones(archivos, [(Toca, "usa una tabla de la conciliación de Contabilidad (sin uso desde E-04)")], Exento);
        ReglaSobreFuente.SinInfracciones(infracciones,
            "La conciliación bancaria es de Tesorería (E-04, TR41): nadie escribe ACC_BankReconciliations, ACC_BankStatementLines ni ACC_BankStatementColumnMaps");
    }

    [Theory]
    [InlineData("foreach (var c in await db.BankReconciliations.Where(r => r.PeriodId == 1).ToListAsync(ct))", true)]
    [InlineData("db.BankStatementLines.Add(linea);", true)]
    [InlineData("context.BankStatementColumnMaps.Remove(m);", true)]
    [InlineData("await db.TreasuryBankStatementLines.AsNoTracking().ToListAsync(ct);", false)]
    [InlineData("var x = db.TreasuryReconciliations;", false)]
    public void El_detector_ve_a_quien_las_toca(string linea, bool toca) => Assert.Equal(toca, Toca.IsMatch(linea));

    [Fact]
    public void La_reapertura_del_periodo_usa_el_puerto_de_Tesoreria()
    {
        var archivo = ReglaSobreFuente.ArchivosNombrados(["Core/IngenIA365ERP.Application/Accounting/Periods/PeriodCommands.cs"]).Single();
        var fuente = FuenteSinComentarios.Leer(archivo);
        Assert.Contains("IConciliacionBancariaParaContabilidad", fuente, StringComparison.Ordinal);
        Assert.Contains("MarcarDesactualizadasAsync", fuente, StringComparison.Ordinal);
    }
}
