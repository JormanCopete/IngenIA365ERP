using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 015, T0001 (desviación III heredada de la T101 de la 013, aprobada por el dueño el 2026-10-23; R-05, R-37; FR-003;
/// contracts/interfaces.md, preámbulo): los puertos entre Recaudo y Tesorería se llaman directo dentro de la unidad de trabajo del que llama,
/// fuera de MediatR; por eso cada implementación de un puerto de <b>escritura</b> (<c>IMovimientosBancariosParaRecaudo.MarcarIdentificacionAsync</c>,
/// <c>IRemesasDeTesoreria.RegistrarAsync</c>, <c>IOrdenesDePagoDeTesoreria.EmitirAsync</c>, <c>IPartidasDesdeConciliacion.CrearAsync</c>,
/// <c>IResultadoDeOrdenesDeRecaudo.InformarAsync</c>, <c>ICuentasBancariasDePersona</c> no: es lectura) ejecuta su validador y escribe su
/// evento. Molde: <see cref="LosPuertosDeCarteraValidanYAuditanSusEscrituras"/>.
///
/// <para>
/// La lista <see cref="Archivos"/> nace vacía en el Setup y la llenan las fases que implementan los puertos (fase 7: cuentas, órdenes y
/// remesas de Tesorería; fase 8: el resultado de las órdenes de Recaudo; fase 11: movimientos bancarios; fase 17: partidas desde la
/// conciliación). Cada archivo nombrado tiene que existir.
/// </para>
/// </summary>
public class LosPuertosDeRecaudoYTesoreriaValidanYAuditanSusEscrituras
{
    /// <summary>Implementaciones de puertos de escritura (relativas a <c>src/</c>).</summary>
    private static readonly string[] Archivos =
    [
        // Fase 10 (T0177): la orden de pago que pide Recaudo y lo que Recaudo hace con su resultado.
        "Core/IngenIA365ERP.Application/Treasury/Orders/OrdenesDePagoDeTesoreria.cs",
        "Core/IngenIA365ERP.Application/Receipts/CreditBalances/CreditBalanceCommands.cs",
        // Fase 11 (T0193): el extracto que Tesorería ofrece a Recaudo.
        "Core/IngenIA365ERP.Application/Treasury/Statements/MovimientosBancariosParaRecaudo.cs",
        // Fase 14 (T0227): la remesa que Tesorería ofrece a Recaudo.
        "Core/IngenIA365ERP.Application/Treasury/Remittances/RemesasDeTesoreria.cs",
    ];

    private static readonly Regex Valida = new(@"\bValidateAsync\(|\bValidar\w*\(", RegexOptions.Compiled);

    private static readonly Regex Audita = new(@"\bAudit\w*\(|\bAuditar\w*\(|\b(IntentoAsync|HechoAsync|RegistrarAsync)\(", RegexOptions.Compiled);

    [Fact]
    public void Cada_puerto_de_escritura_valida_y_audita()
    {
        var archivos = ReglaSobreFuente.ArchivosNombrados(Archivos).ToList();
        Assert.Equal(Archivos.Length, archivos.Count);
        var faltantes = ReglaSobreFuente.Faltantes(archivos, Valida).Select(a => a + ": no valida")
            .Concat(ReglaSobreFuente.Faltantes(archivos, Audita).Select(a => a + ": no audita"))
            .ToList();
        ReglaSobreFuente.SinInfracciones(faltantes, "Un puerto de Recaudo o Tesorería escribe sin validar o sin auditar (desviación III, T101)");
    }
}
