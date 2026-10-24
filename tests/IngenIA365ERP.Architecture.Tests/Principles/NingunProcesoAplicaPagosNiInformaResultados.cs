using System.Text.RegularExpressions;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 015, T0003 (R-17, R-26; FR-145, FR-174; decisiones-transversales §2.5): las tareas programadas de Recaudo y Tesorería <b>sólo
/// proponen, preparan y alertan</b>. Ninguna llama a los contratos de Cartera y Captaciones (aplicar, reversar, consignar, retirar, resolver
/// un saldo a favor), ninguna registra un resultado de giro ni identifica una partida: todo eso es a nombre de una persona.
///
/// <para>
/// Árbol que recorre: los tipos que implementan <see cref="ITareaProgramada"/> en los espacios de los dos módulos, y el fuente de sus
/// carpetas. <see cref="Tareas"/> nace vacía en el Setup y la llena cada fase que crea una tarea (§2.5: <c>recaudo.lote-del-dia</c>,
/// <c>recaudo.cuadre-diario</c>, <c>recaudo.sesiones-olvidadas</c>, <c>recaudo.antiguedad</c>, <c>tesoreria.vigilancia</c>): una tarea que no
/// esté en la lista también falla, para que nadie agregue una sin pasar por esta regla.
/// </para>
/// </summary>
public class NingunProcesoAplicaPagosNiInformaResultados
{
    /// <summary>Los nombres de tipo de las tareas programadas de los dos módulos.</summary>
    private static readonly string[] Tareas =
    [
        "TareaDeSesionesOlvidadas", "TareaDeCuadreDiario", "TareaDeVigilanciaDeTesoreria",
    ];

    private static readonly Regex EscrituraProhibida = new(
        @"\.(AplicarAsync|ReversarAsync|ConsignarAsync|RetirarAsync|ResolverSaldoAFavorAsync|InformarAsync|MarcarIdentificacionAsync)\(|\bnew\s+(Register\w*Result\w*|Apply\w*|Identify\w*)Command\b",
        RegexOptions.Compiled);

    [Fact]
    public void Las_tareas_de_los_modulos_son_las_de_la_lista()
    {
        var encontradas = typeof(IngenIA365ERP.Application.DependencyInjection).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ITareaProgramada).IsAssignableFrom(t))
            .Where(t => RecaudoYTesoreria.EsDeRecaudo(t.Namespace) || RecaudoYTesoreria.EsDeTesoreria(t.Namespace))
            .Select(t => t.Name)
            .Order(StringComparer.Ordinal)
            .ToList();
        Assert.Equal(Tareas.Order(StringComparer.Ordinal).ToList(), encontradas);
    }

    [Fact]
    public void Ninguna_tarea_aplica_pagos_ni_informa_resultados()
    {
        var root = RepoPath.FindRepoRoot();
        var infracciones = RecaudoYTesoreria.ArchivosDeRecaudo().Concat(RecaudoYTesoreria.ArchivosDeTesoreria())
            .Where(a => FuenteSinComentarios.Leer(a).Contains("ITareaProgramada", StringComparison.Ordinal))
            .Where(a => EscrituraProhibida.IsMatch(FuenteSinComentarios.Leer(a)))
            .Select(a => ReglaSobreFuente.Relativo(root, a))
            .ToList();
        ReglaSobreFuente.SinInfracciones(infracciones, "Una tarea programada de Recaudo o Tesorería aplica pagos o informa resultados (FR-174)");
    }
}
