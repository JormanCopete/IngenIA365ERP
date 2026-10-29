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
        // K6b (decisión del dueño del 2026-10-17): el reparto de un pago corregido por la UVR lo emite la corrida de corrección.
        "PagoRedistribuidoPorValorNacional",
        // Feature 014 (T048; mensajes.md §1.1 de la 014, columna «Proceso»): los de captaciones que emiten el ciclo diario y sus procesos.
        "RetiroDeCuentaAplicado", "GmfRetenido", "InteresDeCaptacionCausado", "AjusteDeCausacionDeCaptacion", "InteresDeCaptacionLiquidado",
        "RetencionPracticada", "InteresesDeCdatPagados", "CdatProrrogado", "CdatCancelado", "PenalidadAplicada", "CaptacionReclasificada",
        "BonificacionReconocida", "CargoDeCuentaCausado", "DevolucionOrdenada", "SaldoTrasladadoANoReclamados",
        // E243: el vencimiento del ahorro contractual cumplido traslada a la cuenta a la vista por el proceso nocturno.
        "TrasladoEntreCuentasAplicado",
        // US4 (T116): el cargo periódico gravado lo causa el ciclo diario y pide la factura a Comercial en la misma unidad.
        "CargoDeCaptacionCausado",
        // Feature 014 (T055): la orden de giro v2 de captaciones (la v1 de los desembolsos sigue sin proceso).
        "OrdenDeGiroEmitida",
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

    /// <summary>
    /// Feature 014, US4 (T119; mensajes.md §1.4): el <c>PagoAplicado</c> sigue sin admitir proceso, salvo el del débito automático —recaudador
    /// <c>OwnLoanPayoff</c> con la clave <c>CAR:debito-automatico:…</c>—, que lo aplica el ciclo diario.
    /// </summary>
    [Fact]
    public void El_pago_aplicado_admite_proceso_solo_en_el_debito_automatico()
    {
        var pago = CatalogoDeMensajes.Todos.Single(t => t.Type == "PagoAplicado");
        var debito = new IngenIA365ERP.Application.Common.Integration.Contracts.Lending.PagoAplicadoV1
        {
            Collector = new IngenIA365ERP.Application.Common.Integration.Contracts.Lending.CollectorRefV1
            {
                CollectorCode = "CAR", CollectorClass = "OwnLoanPayoff", IdempotencyKey = "CAR:debito-automatico:0123456789abcdef0123456789abcdef:20261201",
            },
        };
        var cruce = debito with { Collector = debito.Collector with { IdempotencyKey = "CAR:0123456789abcdef0123456789abcdef:0123456789abcdef0123456789abcdef" } };
        var nomina = debito with { Collector = debito.Collector with { CollectorCode = "NOM", CollectorClass = "Payroll" } };

        Assert.False(pago.AdmiteProceso);
        Assert.True(pago.AdmiteProcesoPara(debito));
        Assert.False(pago.AdmiteProcesoPara(cruce));
        Assert.False(pago.AdmiteProcesoPara(nomina));
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

    /// <summary>
    /// Feature 015, US11 (T0154; R-18; FR-118, FR-174): <c>ResultadoDelGiro</c> (TES → Cartera y Captaciones) no admite proceso y su único emisor,
    /// <c>RegistroDeResultadosDelGiro</c>, lo emite siempre con la persona que lo registra (o, en el «anulada», la del origen) como usuario de origen.
    /// </summary>
    [Fact]
    public void El_resultado_del_giro_sale_a_nombre_de_una_persona()
    {
        Assert.False(CatalogoDeMensajes.Todos.Single(t => t.Type == "ResultadoDelGiro").AdmiteProceso);
        var archivo = Path.Combine(RepoPath.FindRepoRoot(), "src", "Core", "IngenIA365ERP.Application", "Treasury", "Results", "RegistroDeResultadosDelGiro.cs");
        var texto = FuenteSinComentarios.Leer(archivo);
        var solicitudes = System.Text.RegularExpressions.Regex.Matches(texto, @"new SolicitudDeEmision\(").Count;
        Assert.True(solicitudes > 0, "RegistroDeResultadosDelGiro emite el resultado del giro.");
        Assert.Equal(solicitudes, System.Text.RegularExpressions.Regex.Matches(texto, @"UsuarioDeOrigen:\s").Count);
    }
}
