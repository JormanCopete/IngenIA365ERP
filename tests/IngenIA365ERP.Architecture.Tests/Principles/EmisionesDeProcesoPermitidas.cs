using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013 (DT §2.25, T11, C15; T0037; contracts/mensajes.md §6): el proceso automático sólo origina los tipos marcados
/// <c>AdmiteProceso</c>, y esa marca es una <b>lista cerrada</b>: los masivos de la causación y del cierre, los hechos por crédito del
/// ciclo, los informativos de cierre, las respuestas a Comercial y, con A3, los tres contingentes. Todo tipo <c>INV</c>, <c>TES</c> y
/// <c>REC</c> queda en <c>false</c>: para Inventario el mensaje lo origina la persona (regla de la 012), y Tesorería y Recaudo emiten con
/// la persona que actúa. Agregar un tipo a la lista es una decisión del catálogo que se revisa aquí.
/// </summary>
public class EmisionesDeProcesoPermitidas
{
    /// <summary>Los tipos <c>CAR</c> con <c>AdmiteProceso = true</c> (§6; los tres últimos, contingentes de A3: se quitan si se rechaza A3).</summary>
    private static readonly string[] AdmitenProceso =
    [
        "InteresCausado", "MoraCausada", "SeguroCausado", "DeterioroCalculado", "CarteraReclasificadaPorCalificacion", "ReversoDeCierre",
        "CausacionSuspendida", "CausacionReanudada", "AnticipoAplicado", "DimensionContableCambiada",
        "AjusteDeCausacionPorFechaValor", "SaldoAFavorReconocido",
        "CierreDeCarteraRealizado", "CierreDeCarteraReabierto",
        "ObligacionCreada", "ObligacionRechazada",
        "CreditoAprobadoSinDesembolsar", "CupoRotativoOtorgado", "GarantiaConstituida",
        // K6b (T1056, E12): la corrección monetaria UVR la causa el ciclo diario, como el interés.
        "CorreccionMonetariaUvrCausada",
    ];

    /// <summary>El archivo donde se declara la marca de Cartera (relativo a <c>src/</c>).</summary>
    private static readonly string[] Archivos = ["Core/IngenIA365ERP.Application/Common/Integration/Contracts/Lending/CatalogoDeMensajesDeCarteraV1.cs"];

    [Fact]
    public void Solo_la_lista_cerrada_de_tipos_de_Cartera_admite_proceso()
    {
        var conMarca = CatalogoDeMensajes.Todos.Where(t => t.AdmiteProceso).Select(t => t.Type).Order(StringComparer.Ordinal).ToList();

        Assert.Equal(AdmitenProceso.Order(StringComparer.Ordinal), conMarca);
        Assert.All(CatalogoDeMensajes.Todos.Where(t => t.AdmiteProceso), t => Assert.Equal(ModulosDeOrigen.Cartera, t.OriginModule));
    }

    /// <summary>
    /// US4 (T0480): el ciclo diario emite por proceso la causación, la suspensión y su reanudación, el anticipo, la dimensión cambiada, el ajuste
    /// por fecha valor y el saldo a favor; lo que nace de una persona —el desembolso y el pago aplicado— nunca lo origina el proceso.
    /// </summary>
    [Fact]
    public void Los_hechos_del_ciclo_admiten_proceso_y_el_desembolso_y_el_pago_no()
    {
        string[] delCiclo =
        [
            "InteresCausado", "MoraCausada", "SeguroCausado", "CausacionSuspendida", "CausacionReanudada", "AnticipoAplicado",
            "DimensionContableCambiada", "AjusteDeCausacionPorFechaValor", "SaldoAFavorReconocido",
        ];
        foreach (var tipo in delCiclo)
            Assert.True(CatalogoDeMensajes.Todos.Single(t => t.Type == tipo).AdmiteProceso, $"{tipo} lo emite el ciclo diario: admite proceso (T11).");
        foreach (var tipo in new[] { "CreditoDesembolsado", "PagoAplicado" })
            Assert.False(CatalogoDeMensajes.Todos.Single(t => t.Type == tipo).AdmiteProceso, $"{tipo} lo origina una persona: no admite proceso (T11).");
    }

    [Fact]
    public void Ningun_tipo_de_Inventario_Tesoreria_ni_Recaudo_admite_proceso()
    {
        var infractores = CatalogoDeMensajes.Todos
            .Where(t => (t.OriginModule is ModulosDeOrigen.Inventario or ModulosDeOrigen.Tesoreria or ModulosDeOrigen.Recaudo) && t.AdmiteProceso)
            .Select(t => $"{t.OriginModule} {t.Type}")
            .ToList();

        Assert.True(infractores.Count == 0, "Tipos que no son de Cartera marcados AdmiteProceso (T11):\n  " + string.Join("\n  ", infractores));
        Assert.Contains(CatalogoDeMensajes.Todos, t => t.OriginModule == ModulosDeOrigen.Inventario);
    }

    /// <summary>
    /// US6 (T0577; T11, mensajes.md §15.2): lo que nace del consumo de una venta o un ajuste de Comercial —<c>ObligacionCreada</c>,
    /// <c>ObligacionRechazada</c>, <c>CreditoDesembolsado</c> en especie y <c>CreditoAjustadoDesdeComercial</c>— lo emite el proceso de integración con
    /// <b>el usuario de Comercial</b> como usuario de origen: cada solicitud de emisión de esos archivos lleva <c>UsuarioDeOrigen</c>. Las dos respuestas
    /// admiten proceso; el desembolso y el ajuste no (los origina la persona que vendió, que viaja como usuario de origen).
    /// </summary>
    [Fact]
    public void Lo_que_nace_del_consumo_de_Comercial_lleva_el_usuario_de_origen()
    {
        Assert.True(CatalogoDeMensajes.Todos.Single(t => t.Type == "ObligacionCreada").AdmiteProceso);
        Assert.True(CatalogoDeMensajes.Todos.Single(t => t.Type == "ObligacionRechazada").AdmiteProceso);
        Assert.False(CatalogoDeMensajes.Todos.Single(t => t.Type == "CreditoAjustadoDesdeComercial").AdmiteProceso);
        string[] archivos =
        [
            "Core/IngenIA365ERP.Application/Lending/Integration/EmisionDeCartera.Comercial.cs",
            "Core/IngenIA365ERP.Application/Lending/Integration/Comercial/RespuestasAComercial.cs",
        ];
        var nombrados = ReglaSobreFuente.ArchivosNombrados(archivos).ToList();
        Assert.Equal(archivos.Length, nombrados.Count);
        var infracciones = new List<string>();
        foreach (var archivo in nombrados)
        {
            var texto = FuenteSinComentarios.Leer(archivo);
            var solicitudes = System.Text.RegularExpressions.Regex.Matches(texto, @"new SolicitudDeEmision\(").Count;
            var conUsuario = System.Text.RegularExpressions.Regex.Matches(texto, @"UsuarioDeOrigen:\s").Count;
            if (solicitudes == 0) infracciones.Add($"{Path.GetFileName(archivo)}: no emite");
            else if (conUsuario < solicitudes) infracciones.Add($"{Path.GetFileName(archivo)}: {solicitudes} emisiones y sólo {conUsuario} con el usuario de origen");
        }
        ReglaSobreFuente.SinInfracciones(infracciones, "Un hecho nacido del consumo de Comercial sale sin el usuario de Comercial (T11)");
    }

    [Fact]
    public void La_marca_se_declara_en_el_catalogo_de_Cartera()
    {
        var faltantes = ReglaSobreFuente.Faltantes(ReglaSobreFuente.ArchivosNombrados(Archivos), new(@"proceso:\s*true"));
        ReglaSobreFuente.SinInfracciones(faltantes, "El catálogo de Cartera no declara qué tipos admiten proceso (T11)");
    }
}
