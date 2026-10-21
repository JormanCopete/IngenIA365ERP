using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Lending.Imports;

namespace IngenIA365ERP.Application.Tests.Lending.Imports;

/// <summary>
/// Feature 013, US8, T0717 (T82; plantillas.md §5.2, §5.3, §6, §0.3): las 17 partes <c>lending.migration.{parte}</c> y
/// <c>lending.provisional-collections</c> están en el catálogo de Cartera, cada una con una sola hoja <c>Datos</c> (admite <c>.csv</c>); sus
/// encabezados son <b>exactamente</b> los de la plantilla (lista dorada: la plantilla se congela antes de publicarse, cambiar una columna rompe esta
/// prueba) con su tipo de valor; ninguna crea personas; y el registro sirve la definición de cada parte. (nuevo)
/// </summary>
public class CatalogoDePlantillasDeMigracionTests
{
    /// <summary>La lista dorada de plantillas.md §5.3 y §6, en el orden de la plantilla.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Dorada = new Dictionary<string, string[]>
    {
        ["loans"] =
        [
            "numero", "deudor", "oficina", "linea", "asesor", "factura", "fechaDeDesembolso", "montoDesembolsado", "tipoDeTasa", "tasaCorrienteEA",
            "indice", "claveDelIndice", "margen", "tasaPiso", "tasaTecho", "mesesDeReajuste", "tipoDeTasaDeMora", "tasaDeMoraEA", "factorDeMora",
            "convencionDeDias", "equivalencia", "redondeoUnidad", "redondeoModo", "redondeoMomento", "sistemaDeAmortizacion", "prelacion", "periodicidad",
            "plazo", "preferenciaDeAbono", "formaDePago", "pagaduria", "idEmpleadoEnPagaduria", "fechaDeLibranza", "etapaDeCobranza", "enJuridico",
            "abogado", "notas",
        ],
        ["installments"] = ["numeroDeCredito", "cuota", "fechaDeVencimiento", "capital", "interesCorriente", "interesDeMora", "seguro", "otros", "interesProyectado"],
        ["balances"] = ["numeroDeCredito", "concepto", "valor"],
        ["risk-data"] =
        [
            "numeroDeCredito", "diasDeMora", "calificacion", "subcategoria", "causa", "deterioroCapital", "deterioroIntereses", "deterioroOtros",
            "deterioroGeneral", "fechaDeIngresoAE", "enSuspenso",
        ],
        ["restructurings"] = ["numeroDeCredito", "clase", "vecesReestructurado", "fechaUltima", "calificacionAlReestructurar", "cuotasConsecutivasPagadas", "creditoAnterior"],
        ["co-debtors"] = ["numeroDeCredito", "codeudor", "rol", "desde"],
        ["collaterals"] =
        [
            "numeroDeCredito", "tipo", "referencia", "valor", "fechaDeConstitucion", "vigenteHasta", "estado", "descripcion", "valorAvaluo", "fechaAvaluo",
            "poliza", "polizaVence", "aseguradora", "clasePagare", "canalDeCustodia", "idExternoPagare", "fondoDeGarantias", "cobertura", "comision",
        ],
        ["charged-off"] =
        [
            "numero", "deudor", "oficina", "linea", "fechaDeDesembolso", "fechaDeCastigo", "capitalCastigado", "interesesCastigados", "otrosCastigados",
            "soporteDelCastigo",
        ],
        ["sales"] =
        [
            "factura", "fechaDeVenta", "deudor", "valorOriginal", "cobrosExternos", "notasYAjustes", "saldoAlCorte", "resultado", "numeroDeCredito", "linea",
            "referenciaDeLosCobros",
        ],
        ["credit-limits"] = ["persona", "tipo", "linea", "valor", "vigenteDesde", "vigenteHasta", "motivo", "preferenciaDeAbono"],
        ["blocks"] = ["persona", "motivo", "numeroDeCredito", "desde", "hasta", "soporte", "aseguradora", "estadoDeLaReclamacion", "estadoDeLaInsolvencia"],
        ["authorizations"] = ["persona", "finalidad", "canal", "estado", "fecha", "versionDelTexto", "evidencia", "canalDeObtencion"],
        ["prior-notices"] =
        [
            "numeroDeCredito", "episodio", "comunicacionesExigidas", "fechaDeEnvio1", "canal1", "referencia1", "fechaDeEnvio2", "canal2", "referencia2",
        ],
        ["bureau-reports"] = ["numeroDeCredito", "central", "ultimoPeriodoReportado", "novedadReportada", "datoNegativo", "episodio", "conAvisoPrevio"],
        ["claims"] = ["numeroDeCredito", "central", "tipo", "fechaDeRadicacion", "prorrogado", "descripcion"],
        ["related-parties"] =
        [
            "persona", "fundamento", "personaRelacionada", "claseDeParentesco", "grado", "porcentajeDeAportes", "vigenteDesde", "vigenteHasta", "soporte",
        ],
        ["arrears-history"] = ["numeroDeCredito", "mes", "moraMaximaDias", "moraAlCierreDias"],
    };

    public static TheoryData<string> Partes => new(Dorada.Keys);

    [Fact]
    public void Son_diecisiete_partes_en_el_orden_de_carga()
    {
        DefinicionesDeMigracion.Todas.Select(p => p.Parte).Should().Equal(Dorada.Keys);
        DefinicionesDeMigracion.Todas.Where(p => p.Obligatoria).Select(p => p.Parte).Should().Equal("loans", "installments", "balances", "risk-data");
        DefinicionesDeMigracion.Todas.Select(p => p.Orden).Should().Equal(Enumerable.Range(1, 17));
    }

    [Theory]
    [MemberData(nameof(Partes))]
    public void Cada_parte_tiene_una_hoja_Datos_con_los_encabezados_congelados(string parte)
    {
        var definicion = CatalogoDePlantillasDeCartera.DefinicionPorClave($"lending.migration.{parte}");

        definicion.Should().NotBeNull();
        definicion!.Clave.Should().Be($"lending.migration.{parte}");
        definicion.Modulo.Should().Be(ModuloDeAuditoria.Lending);
        definicion.EsDeUnaHoja.Should().BeTrue("una sola hoja admite .csv");
        definicion.Hojas[0].Nombre.Should().Be("Datos");
        definicion.Hojas[0].Columnas.Select(c => c.Nombre).Should().Equal(Dorada[parte]);
    }

    [Fact]
    public void Los_tipos_de_valor_son_los_de_la_plantilla()
    {
        string Tipo(string parte, string columna) => DefinicionesDeMigracion.Buscar(parte)!.Definicion.Hojas[0].Columna(columna)!.Tipo.ToString();

        Tipo("loans", "deudor").Should().Be(nameof(TipoDeValor.Persona));
        Tipo("loans", "oficina").Should().Be(nameof(TipoDeValor.Sucursal));
        Tipo("loans", "linea").Should().Be(nameof(TipoDeValor.Codigo));
        Tipo("loans", "montoDesembolsado").Should().Be(nameof(TipoDeValor.Monto));
        Tipo("loans", "tasaCorrienteEA").Should().Be(nameof(TipoDeValor.Porcentaje), "las tasas van en puntos");
        Tipo("loans", "tipoDeTasa").Should().Be(nameof(TipoDeValor.Enumeracion));
        Tipo("loans", "plazo").Should().Be(nameof(TipoDeValor.Entero));
        Tipo("loans", "enJuridico").Should().Be(nameof(TipoDeValor.SiNo));
        Tipo("installments", "fechaDeVencimiento").Should().Be(nameof(TipoDeValor.Fecha));
        Tipo("collaterals", "cobertura").Should().Be(nameof(TipoDeValor.Porcentaje));
        Tipo("bureau-reports", "ultimoPeriodoReportado").Should().Be(nameof(TipoDeValor.Texto), "el mes AAAA-MM es texto con patrón");

        // El número de crédito: texto de 20 con patrón; el mes: texto de 7.
        foreach (var parte in DefinicionesDeMigracion.Todas)
            foreach (var columna in parte.Definicion.Hojas[0].Columnas.Where(c => c.Nombre is "numero" or "numeroDeCredito" or "creditoAnterior"))
                (columna.Tipo, columna.Largo).Should().Be((TipoDeValor.Texto, 20), $"{parte.Parte}.{columna.Nombre} es un número de crédito");
        DefinicionesDeMigracion.HistorialDeMora.Hojas[0].Columna("mes")!.Largo.Should().Be(7);
    }

    [Fact]
    public void Ninguna_parte_crea_personas()
    {
        // Las personas se citan por documento (tipo Persona, que exige la persona viva en el maestro) y la instrucción lo dice.
        foreach (var parte in DefinicionesDeMigracion.Todas)
        {
            var personas = parte.Definicion.Hojas[0].Columnas.Where(c => c.Tipo == TipoDeValor.Persona).ToList();
            if (personas.Count == 0) continue;
            personas.Should().Contain(c => c.Reglas!.Contains("no crea personas"), $"{parte.Parte} cita personas del maestro");
            parte.Definicion.Hojas.Should().NotContain(h => h.Nombre == "Personas");
        }
    }

    [Fact]
    public void Los_cobros_provisionales_son_una_hoja_y_se_revisan_con_el_permiso_de_activar()
    {
        var registro = CatalogoDePlantillasDeCartera.Buscar("lending.provisional-collections")!;

        registro.Definicion.Should().BeSameAs(DefinicionesDeMigracion.CobrosProvisionales);
        registro.ImportPermission.Should().Be("Lending.Activation.Execute");
        DefinicionesDeMigracion.CobrosProvisionales.Hojas.Should().ContainSingle().Which.Nombre.Should().Be("Datos");
        DefinicionesDeMigracion.CobrosProvisionales.Hojas[0].Columnas.Select(c => c.Nombre).Should()
            .Equal("factura", "valor", "fechaValor", "referenciaContable", "observaciones");
        CatalogoDePlantillasDeCartera.Buscar("lending.migration")!.ImportPermission.Should().Be("Lending.Migration.Load");
    }

    [Fact]
    public async Task El_registro_sirve_la_definicion_de_cada_parte_y_una_parte_desconocida_no_existe()
    {
        var handler = new GetLendingTemplateQueryHandler();

        (await handler.Handle(new GetLendingTemplateQuery("lending.migration.installments"), default)).Value.Should().BeSameAs(DefinicionesDeMigracion.Cuotas);
        (await handler.Handle(new GetLendingTemplateQuery("lending.provisional-collections"), default)).IsSuccess.Should().BeTrue();
        (await handler.Handle(new GetLendingTemplateQuery("lending.migration.prestamos"), default)).Error.Code.Should().Be("Lending.Template.NotFound");
        DefinicionesDeMigracion.Buscar("LOANS")!.Parte.Should().Be("loans");
        DefinicionesDeMigracion.Buscar("lending.migration.risk-data")!.Definicion.Should().BeSameAs(DefinicionesDeMigracion.DatosDeRiesgo);
    }
}
