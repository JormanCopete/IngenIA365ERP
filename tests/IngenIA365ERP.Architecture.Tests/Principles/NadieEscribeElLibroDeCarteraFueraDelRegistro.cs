using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013, FR-001 (DT §2.25, T23, C45): el libro, la marca de agua, las posiciones, las operaciones, las
/// versiones de condiciones y el plan sólo los escribe <c>RegistroDeCartera</c>.
///
/// <para>Llena en la fase Fundacional B (T0143): recorre todo Application, Infrastructure y Presentation —no sólo las carpetas de
/// Cartera, para que ningún módulo escriba el libro por un costado— y exime sólo <c>RegistroDeCartera</c>. Además de agregar filas,
/// vigila la creación de la fila de control y el avance de su marca de agua (<c>LastAccruedDate</c>, <c>LastRegisteredAt</c>,
/// <c>LastPositionDate</c>).</para>
/// </summary>
public class NadieEscribeElLibroDeCarteraFueraDelRegistro
{
    /// <summary>Carpetas de Cartera (relativas a <c>src/</c>).</summary>
    private static readonly string[] Carpetas = ["Core/IngenIA365ERP.Application", "Infrastructure", "Presentation"];

    /// <summary>Archivos exentos: <c>RegistroDeCartera</c> (rutas relativas a la raíz).</summary>
    private static readonly string[] Exentos = ["src/Core/IngenIA365ERP.Application/Lending/Ledger/RegistroDeCartera.cs"];

    private static readonly (Regex Patron, string Que)[] Prohibidos = [
        (new Regex(@"\b(LoanTransactions|LoanOperations|LoanDailyPositions|LoanTermsVersions|RepaymentScheduleVersions|RepaymentInstallments|LoanControls)\.(Add|AddRange|Update|UpdateRange|Attach)\(", RegexOptions.Compiled), "escribe el libro, sus versiones, sus proyecciones o la fila de control"),
        (new Regex(@"\.(LastAccruedDate|LastRegisteredAt|LastPositionDate)\s*(=(?!=)|\+=)", RegexOptions.Compiled), "mueve la marca de agua del crédito"),
        (new Regex(@"\bnew\s+(LoanTransaction|LoanOperation|LoanDailyPosition)\s*\(", RegexOptions.Compiled), "construye una fila del libro"),
    ];

    [Fact]
    public void Solo_el_registro_escribe_el_libro()
    {
        var infracciones = ReglaSobreFuente.Infracciones(
            ReglaSobreFuente.ArchivosEn(Carpetas), Prohibidos, relativo => Exentos.Contains(relativo));
        ReglaSobreFuente.SinInfracciones(infracciones, "Alguien escribe el libro de Cartera fuera de RegistroDeCartera (FR-001)");
    }

    /// <summary>
    /// US5 (T0526, C45): el contrato de obligaciones, la aplicación, el reverso, la resolución de saldos y el cambio de preferencia
    /// (<c>Application/Lending/Payments</c> y <c>Application/Lending/Integration</c>) están dentro del recorrido de la regla y escriben la operación,
    /// el libro y el plan por <c>RegistroDeCartera</c>.
    /// </summary>
    [Fact]
    public void Los_pagos_y_el_contrato_escriben_el_libro_por_el_registro()
    {
        var recorridos = ReglaSobreFuente.ArchivosEn(Carpetas).ToList();
        string[] escritores =
        [
            "Core/IngenIA365ERP.Application/Lending/Payments/AplicacionDePagos.cs",
            "Core/IngenIA365ERP.Application/Lending/Payments/ReversionDePago.cs",
            "Core/IngenIA365ERP.Application/Lending/Payments/ResolucionDeSaldoAFavor.cs",
            "Core/IngenIA365ERP.Application/Lending/Payments/Commands/ChangePrepaymentPreferenceCommand.cs",
        ];
        var nombrados = ReglaSobreFuente.ArchivosNombrados(escritores).ToList();
        Assert.Equal(escritores.Length, nombrados.Count);
        var infracciones = new List<string>();
        foreach (var archivo in nombrados)
        {
            if (!recorridos.Contains(archivo)) infracciones.Add($"{archivo}: fuera del recorrido de la regla");
            if (!FuenteSinComentarios.Leer(archivo).Contains("RegistroDeCartera", StringComparison.Ordinal)) infracciones.Add($"{archivo}: no escribe por RegistroDeCartera");
        }
        Assert.Contains(recorridos, a => a.EndsWith(Path.Combine("Integration", "ContratoDeObligacionesDeCartera.cs"), StringComparison.Ordinal));
        ReglaSobreFuente.SinInfracciones(infracciones, "Los pagos de Cartera no escriben el libro por RegistroDeCartera (C45)");
    }

    /// <summary>
    /// US8 (bloque 13C, T0728): la activación escribe los saldos iniciales de los créditos migrados (operación <c>OpeningBalance</c>, transacciones por
    /// concepto y cuota, plan y control) sólo por <c>RegistroDeCartera</c>; el nacimiento de los créditos y la activación están dentro del recorrido
    /// de la regla.
    /// </summary>
    [Fact]
    public void La_activacion_escribe_los_saldos_iniciales_por_el_registro()
    {
        var recorridos = ReglaSobreFuente.ArchivosEn(Carpetas).ToList();
        string[] escritores =
        [
            "Core/IngenIA365ERP.Application/Lending/Activation/NacimientoDeCreditosMigrados.cs",
            "Core/IngenIA365ERP.Application/Lending/Activation/ActivateLendingCommand.cs",
        ];
        var nombrados = ReglaSobreFuente.ArchivosNombrados(escritores).ToList();
        Assert.Equal(escritores.Length, nombrados.Count);
        var infracciones = new List<string>();
        foreach (var archivo in nombrados)
        {
            if (!recorridos.Contains(archivo)) infracciones.Add($"{archivo}: fuera del recorrido de la regla");
            if (!FuenteSinComentarios.Leer(archivo).Contains("RegistroDeCartera", StringComparison.Ordinal)) infracciones.Add($"{archivo}: no escribe por RegistroDeCartera");
        }
        ReglaSobreFuente.SinInfracciones(infracciones, "La activación no escribe los saldos iniciales por RegistroDeCartera (FR-001)");
    }

    /// <summary>
    /// K3 (US11, bloque B; T0868): la aplicación de una novedad (<c>TermsChange</c>, sus versiones de condiciones y plan, y el nacimiento del enlazado) y
    /// la de una condonación (<c>Waiver</c>) escriben la operación, el libro y el plan sólo por <c>RegistroDeCartera</c>.
    /// </summary>
    [Fact]
    public void Las_novedades_y_las_condonaciones_escriben_el_libro_por_el_registro()
    {
        var recorridos = ReglaSobreFuente.ArchivosEn(Carpetas).ToList();
        string[] escritores =
        [
            "Core/IngenIA365ERP.Application/Lending/Novelties/ApplyLoanNoveltyCommand.cs",
            "Core/IngenIA365ERP.Application/Lending/Waivers/ApplyWaiverCommand.cs",
        ];
        var nombrados = ReglaSobreFuente.ArchivosNombrados(escritores).ToList();
        Assert.Equal(escritores.Length, nombrados.Count);
        var infracciones = new List<string>();
        foreach (var archivo in nombrados)
        {
            if (!recorridos.Contains(archivo)) infracciones.Add($"{archivo}: fuera del recorrido de la regla");
            if (!FuenteSinComentarios.Leer(archivo).Contains("RegistroDeCartera", StringComparison.Ordinal)) infracciones.Add($"{archivo}: no escribe por RegistroDeCartera");
        }
        ReglaSobreFuente.SinInfracciones(infracciones, "Las novedades o las condonaciones no escriben el libro por RegistroDeCartera (FR-001)");
    }

    /// <summary>
    /// K3 (US14, bloque B; T0901): la aplicación del lote de castigo registra la operación <c>WriteOff</c> de cada crédito y el estado
    /// <c>WrittenOff</c> sólo por <c>RegistroDeCartera</c>, dentro del recorrido de la regla; la operación no deja filas propias en el libro (el castigo no
    /// cambia lo que se debe).
    /// </summary>
    [Fact]
    public void El_castigo_escribe_la_operacion_WriteOff_por_el_registro()
    {
        var recorridos = ReglaSobreFuente.ArchivosEn(Carpetas).ToList();
        var nombrados = ReglaSobreFuente.ArchivosNombrados(["Core/IngenIA365ERP.Application/Lending/WriteOffs/ApplyWriteOffBatchCommand.cs"]).ToList();
        Assert.Single(nombrados);
        var archivo = nombrados[0];
        Assert.Contains(archivo, recorridos);
        var fuente = FuenteSinComentarios.Leer(archivo);
        Assert.Contains("registro.RegistrarOperacion(", fuente);
        Assert.Contains("RegistroDeCartera.CambiarEstado(", fuente);
        Assert.DoesNotContain("RegistrarTransacciones(", fuente);
    }
}
