using IngenIA365ERP.Domain.Entities.Core;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Common.Taxation;
using IngenIA365ERP.Application.Core.Taxes;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Core.Taxes;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Taxes;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Core.Taxes;

/// <summary>
/// Feature 012, T104 (T22; contracts/api.md §30; data-model §17): los comandos del catálogo tributario de Core —alta y
/// edición de impuestos, tarifas y conceptos, cerrar una vigencia, marcarla revisada— con sus reglas (código repetido,
/// vigencias cruzadas, empate evidente, tarifa en vigencia, concepto en uso) y <see cref="LectorDeCatalogoTributario"/>,
/// que arma la foto del motor a una fecha. De paso, la semilla <see cref="TaxCatalogSeeder"/> (T168).
/// </summary>
public class TaxCatalogCommandsTests : IDisposable
{
    private static readonly DateOnly Hoy = new(2026, 3, 16);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();

    public TaxCatalogCommandsTests() => _reloj.HoyLocal.Returns(Hoy);

    public void Dispose() => _db.Dispose();

    // ------------------------------------------------------------------------------------------ ayudantes --

    private async Task<Guid> ImpuestoAsync(string codigo, TaxKind clase, TaxCalculationForm forma = TaxCalculationForm.PercentOfBase, Guid? sobre = null)
    {
        var r = await new CreateTaxDefinitionCommandHandler(_db).Handle(
            new CreateTaxDefinitionCommand(codigo, $"Impuesto {codigo}", clase, forma, sobre, false, "01", null, "alta"), default);
        r.IsSuccess.Should().BeTrue(r.Error.Message);
        return r.Value;
    }

    private async Task<Guid> ConceptoAsync(string codigo)
    {
        var r = await new CreateWithholdingConceptCommandHandler(_db).Handle(new CreateWithholdingConceptCommand(codigo, $"Concepto {codigo}", null, "alta"), default);
        r.IsSuccess.Should().BeTrue(r.Error.Message);
        return r.Value;
    }

    private static CreateTaxRateCommand Tarifa(Guid impuesto, string codigo, decimal? tarifa = 0.19m, DateOnly? desde = null, DateOnly? hasta = null,
        Guid? concepto = null, TaxRateConditionsDto? condiciones = null, short prioridad = 0, decimal? porUnidad = null, string? municipio = null,
        decimal? minimoUvt = null) =>
        new(impuesto, codigo, $"Tarifa {codigo}", tarifa, porUnidad, concepto, municipio, null, minimoUvt, null, condiciones,
            TaxAppliesTo.Both, prioridad, desde ?? new DateOnly(2026, 1, 1), hasta, "ET art. X", null, "alta");

    private Task<IngenIA365ERP.Application.Common.Models.Result<Guid>> CrearTarifaAsync(CreateTaxRateCommand c) =>
        new CreateTaxRateCommandHandler(_db).Handle(c, default);

    // ---------------------------------------------------------------------------------------------- impuestos --

    [Fact]
    public async Task Alta_de_impuesto_con_el_codigo_en_mayusculas_y_la_retencion_derivada_de_la_clase()
    {
        var id = await ImpuestoAsync("rtf", TaxKind.ReteFuente);

        var impuesto = await _db.TaxDefinitions.SingleAsync(t => t.PublicId == id);
        impuesto.Code.Should().Be("RTF");
        impuesto.IsWithholding.Should().BeTrue("ReteFuente es una retención aunque no lo diga quien la crea");
        impuesto.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task PercentOfTax_exige_el_impuesto_base_en_el_validador_y_en_el_handler()
    {
        var sinBase = new CreateTaxDefinitionCommand("RIVA", "ReteIVA", TaxKind.ReteIva, TaxCalculationForm.PercentOfTax, null, false, "05", null, "alta");
        (await new CreateTaxDefinitionCommandValidator().ValidateAsync(sinBase)).Errors
            .Should().Contain(e => e.PropertyName == nameof(CreateTaxDefinitionCommand.TaxedOnTaxPublicId));
        var r = await new CreateTaxDefinitionCommandHandler(_db).Handle(sinBase, default);
        r.Error.Code.Should().Be("Validation.Invalid");

        var inexistente = await new CreateTaxDefinitionCommandHandler(_db).Handle(sinBase with { TaxedOnTaxPublicId = Guid.NewGuid() }, default);
        inexistente.Error.Code.Should().Be("Core.Tax.NotFound");

        var iva = await ImpuestoAsync("IVA", TaxKind.Iva);
        var ok = await new CreateTaxDefinitionCommandHandler(_db).Handle(sinBase with { TaxedOnTaxPublicId = iva }, default);
        ok.IsSuccess.Should().BeTrue();
        (await _db.TaxDefinitions.Include(t => t.TaxedOnDefinition).SingleAsync(t => t.Code == "RIVA")).TaxedOnDefinition!.Code.Should().Be("IVA");
    }

    [Fact]
    public async Task Codigo_de_impuesto_repetido_es_Catalogo_CodigoDuplicado()
    {
        await ImpuestoAsync("IVA", TaxKind.Iva);
        var r = await new CreateTaxDefinitionCommandHandler(_db).Handle(
            new CreateTaxDefinitionCommand("iva", "Otro", TaxKind.Iva, TaxCalculationForm.PercentOfBase, null, false, "01", null, "alta"), default);
        r.Error.Code.Should().Be("Catalogo.CodigoDuplicado");
        r.Error.Message.Should().Contain("Impuesto IVA");
    }

    [Fact]
    public async Task Todo_comando_del_catalogo_exige_motivo()
    {
        var sinMotivo = new CreateTaxDefinitionCommand("IVA", "IVA", TaxKind.Iva, TaxCalculationForm.PercentOfBase, null, false, "01", null, "");
        (await new CreateTaxDefinitionCommandValidator().ValidateAsync(sinMotivo)).Errors.Should().Contain(e => e.PropertyName == "Reason");
        (await new ReviewTaxRateCommandValidator().ValidateAsync(new ReviewTaxRateCommand(Guid.NewGuid(), " "))).IsValid.Should().BeFalse();
    }

    // ------------------------------------------------------------------------------------------------ tarifas --

    [Fact]
    public async Task Vigencias_del_mismo_codigo_que_se_cruzan_son_Core_TaxRate_Overlaps_con_la_otra_en_data()
    {
        var iva = await ImpuestoAsync("IVA", TaxKind.Iva);
        (await CrearTarifaAsync(Tarifa(iva, "IVA19", desde: new DateOnly(2017, 1, 1)))).IsSuccess.Should().BeTrue();

        var cruzada = await CrearTarifaAsync(Tarifa(iva, "IVA19", 0.20m, desde: new DateOnly(2027, 1, 1)));
        cruzada.Error.Code.Should().Be("Core.TaxRate.Overlaps");
        var datos = cruzada.Error.GetType().GetProperty("Data")!.GetValue(cruzada.Error)!;
        datos.GetType().GetProperty("validFrom")!.GetValue(datos).Should().Be(new DateOnly(2017, 1, 1));

        // Cerrada la anterior la víspera, la vigencia nueva entra como otra fila con el mismo código.
        var anterior = await _db.TaxRates.SingleAsync(t => t.Code == "IVA19");
        (await new CloseTaxRateCommandHandler(_db).Handle(new CloseTaxRateCommand(anterior.PublicId, new DateOnly(2026, 12, 31), "cambio de tarifa"), default))
            .IsSuccess.Should().BeTrue();
        (await CrearTarifaAsync(Tarifa(iva, "IVA19", 0.20m, desde: new DateOnly(2027, 1, 1)))).IsSuccess.Should().BeTrue();
        (await _db.TaxRates.CountAsync(t => t.Code == "IVA19")).Should().Be(2);
    }

    [Fact]
    public async Task El_codigo_de_una_tarifa_pertenece_a_un_solo_impuesto()
    {
        var iva = await ImpuestoAsync("IVA", TaxKind.Iva);
        var inc = await ImpuestoAsync("INC", TaxKind.Inc);
        await CrearTarifaAsync(Tarifa(iva, "T19"));
        (await CrearTarifaAsync(Tarifa(inc, "T19", desde: new DateOnly(2030, 1, 1)))).Error.Code.Should().Be("Catalogo.CodigoDuplicado");
    }

    [Fact]
    public async Task Empate_evidente_entre_retenciones_es_Core_TaxRate_Ambiguous_nombrando_las_dos()
    {
        var rtf = await ImpuestoAsync("RTF", TaxKind.ReteFuente);
        var compras = await ConceptoAsync("COMPRAS");
        var declarante = new TaxRateConditionsDto(SubjectIsIncomeTaxFiler: true);
        (await CrearTarifaAsync(Tarifa(rtf, "RF25", 0.025m, concepto: compras, condiciones: declarante))).IsSuccess.Should().BeTrue();

        var empate = await CrearTarifaAsync(Tarifa(rtf, "RF30", 0.03m, concepto: compras, condiciones: declarante));
        empate.Error.Code.Should().Be("Core.TaxRate.Ambiguous");
        empate.Error.Message.Should().Contain("RF30").And.Contain("RF25");

        // Con otra prioridad (o en vigencias que no se cruzan) ya no es un empate.
        (await CrearTarifaAsync(Tarifa(rtf, "RF30", 0.03m, concepto: compras, condiciones: declarante, prioridad: 1))).IsSuccess.Should().BeTrue();
        // En impuestos que el producto cita por código (IVA 19 y 5) no hay empate que mirar.
        var iva = await ImpuestoAsync("IVA", TaxKind.Iva);
        (await CrearTarifaAsync(Tarifa(iva, "IVA19"))).IsSuccess.Should().BeTrue();
        (await CrearTarifaAsync(Tarifa(iva, "IVA5", 0.05m))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Las_reglas_de_la_definicion_se_aplican_a_la_tarifa()
    {
        var rtf = await ImpuestoAsync("RTF", TaxKind.ReteFuente);
        (await CrearTarifaAsync(Tarifa(rtf, "RFX", 0.025m))).Error.Code.Should().Be("Validation.Invalid", "ReteFuente exige concepto");

        var bolsas = await ImpuestoAsync("BOLSAS", TaxKind.Inc, TaxCalculationForm.AmountPerUnit);
        (await CrearTarifaAsync(Tarifa(bolsas, "BOLSA", 0.19m))).Error.Code.Should().Be("Validation.Invalid", "por unidad exige valor por unidad");
        (await CrearTarifaAsync(Tarifa(bolsas, "BOLSA", null, porUnidad: 66m))).IsSuccess.Should().BeTrue();

        var iva = await ImpuestoAsync("IVA", TaxKind.Iva);
        (await CrearTarifaAsync(Tarifa(iva, "IVA19", condiciones: new TaxRateConditionsDto(SubjectIsIncomeTaxFiler: true))))
            .Error.Code.Should().Be("Validation.Invalid", "las condiciones sólo van en retenciones");

        var reteica = await ImpuestoAsync("RICA", TaxKind.ReteIca);
        (await CrearTarifaAsync(Tarifa(reteica, "RICAGEN", 0.0066m))).Error.Code.Should().Be("Validation.Invalid", "ReteICA exige municipio");
        // T176: el municipio se valida contra COR_Cities.DaneCode (DIVIPOLA), no sólo por su forma.
        (await CrearTarifaAsync(Tarifa(reteica, "RICAGEN", 0.0066m, municipio: "76001"))).Error.Code.Should().Be("Core.TaxRate.MunicipalityUnknown");
        _db.Cities.Add(new City
        {
            Name = "Cali", DaneCode = "76001", CreatedBy = "seed",
            Department = new Department { Code = "76", Name = "Valle del Cauca", CreatedBy = "seed", Country = new Country { Name = "Colombia", CreatedBy = "seed" } },
        });
        await _db.SaveChangesAsync();
        (await CrearTarifaAsync(Tarifa(reteica, "RICAGEN", 0.0066m, municipio: "76001"))).IsSuccess.Should().BeTrue();

        (await CrearTarifaAsync(Tarifa(rtf, "RFY", 0.025m, concepto: Guid.NewGuid()))).Error.Code.Should().Be("Core.WithholdingConcept.NotFound");
        (await CrearTarifaAsync(Tarifa(Guid.NewGuid(), "RFZ"))).Error.Code.Should().Be("Core.Tax.NotFound");
    }

    [Fact]
    public async Task El_validador_revisa_la_forma_de_la_tarifa()
    {
        var v = new CreateTaxRateCommandValidator();
        var baseCmd = Tarifa(Guid.NewGuid(), "IVA19");
        (await v.ValidateAsync(baseCmd)).IsValid.Should().BeTrue();
        (await v.ValidateAsync(baseCmd with { Rate = 19m })).IsValid.Should().BeFalse("la tarifa es fracción, no puntos");
        (await v.ValidateAsync(baseCmd with { MinimumBaseUvt = 27m, MinimumBasePesos = 1_000_000m })).IsValid.Should().BeFalse();
        (await v.ValidateAsync(baseCmd with { ValidTo = new DateOnly(2025, 1, 1) })).IsValid.Should().BeFalse();
        (await v.ValidateAsync(baseCmd with { ActivityCode = "4711" })).IsValid.Should().BeFalse("una actividad exige municipio");
        (await v.ValidateAsync(baseCmd with { ActivityCode = "4711", MunicipalityDaneCode = "76001" })).IsValid.Should().BeTrue();
        (await v.ValidateAsync(baseCmd with { LegalSource = "" })).IsValid.Should().BeFalse("toda tarifa cita su norma");
    }

    [Fact]
    public async Task Una_tarifa_en_vigencia_no_se_edita_y_una_futura_si()
    {
        var iva = await ImpuestoAsync("IVA", TaxKind.Iva);
        await CrearTarifaAsync(Tarifa(iva, "IVA19", desde: new DateOnly(2026, 1, 1)));
        await CrearTarifaAsync(Tarifa(iva, "IVA20", 0.20m, desde: new DateOnly(2027, 1, 1)));
        var vigente = await _db.TaxRates.SingleAsync(t => t.Code == "IVA19");
        var futura = await _db.TaxRates.SingleAsync(t => t.Code == "IVA20");

        var handler = new UpdateTaxRateCommandHandler(_db, _reloj);
        var enVigencia = await handler.Handle(Editar(vigente.PublicId, "IVA19", 0.18m, new DateOnly(2026, 1, 1)), default);
        enVigencia.Error.Code.Should().Be("Core.TaxRate.InEffect");

        (await handler.Handle(Editar(futura.PublicId, "IVA20", 0.21m, new DateOnly(2027, 1, 1)), default)).IsSuccess.Should().BeTrue();
        (await _db.TaxRates.SingleAsync(t => t.Code == "IVA20")).Rate.Should().Be(0.21m);

        (await handler.Handle(Editar(futura.PublicId, "OTRO", 0.21m, new DateOnly(2027, 1, 1)), default))
            .Error.Code.Should().Be("Validation.Invalid", "el código de una tarifa no cambia");
    }

    private static UpdateTaxRateCommand Editar(Guid id, string codigo, decimal tarifa, DateOnly desde) =>
        new(id, codigo, "Editada", tarifa, null, null, null, null, null, null, null, TaxAppliesTo.Both, 0, desde, null, "ET art. X", null, "corrección");

    [Fact]
    public async Task Cerrar_una_vigencia_antes_de_su_inicio_es_Validation_Invalid()
    {
        var iva = await ImpuestoAsync("IVA", TaxKind.Iva);
        await CrearTarifaAsync(Tarifa(iva, "IVA19", desde: new DateOnly(2026, 1, 1)));
        var tarifa = await _db.TaxRates.SingleAsync();

        var r = await new CloseTaxRateCommandHandler(_db).Handle(new CloseTaxRateCommand(tarifa.PublicId, new DateOnly(2025, 12, 31), "cierre"), default);
        r.Error.Code.Should().Be("Validation.Invalid");

        (await new CloseTaxRateCommandHandler(_db).Handle(new CloseTaxRateCommand(tarifa.PublicId, new DateOnly(2026, 1, 1), "cierre"), default))
            .IsSuccess.Should().BeTrue("puede cerrar el mismo día que empieza");
        (await new CloseTaxRateCommandHandler(_db).Handle(new CloseTaxRateCommand(Guid.NewGuid(), Hoy, "cierre"), default))
            .Error.Code.Should().Be("Core.TaxRate.NotFound");
    }

    [Fact]
    public async Task Marcar_revisada_baja_ReviewPending_sin_cambiar_la_tarifa()
    {
        var iva = await ImpuestoAsync("IVA", TaxKind.Iva);
        await CrearTarifaAsync(Tarifa(iva, "IVA19"));
        var tarifa = await _db.TaxRates.SingleAsync();
        tarifa.ReviewPending = true;
        await _db.SaveChangesAsync();

        var r = await new ReviewTaxRateCommandHandler(_db).Handle(new ReviewTaxRateCommand(tarifa.PublicId, "Validada por la contadora"), default);

        r.IsSuccess.Should().BeTrue();
        var leida = await _db.TaxRates.AsNoTracking().SingleAsync();
        leida.ReviewPending.Should().BeFalse();
        leida.Rate.Should().Be(0.19m);
        typeof(ReviewTaxRateCommand).Should().Implement<IngenIA365ERP.Application.Common.Behaviors.IConMotivo>("el motivo va a la auditoría");
    }

    // ---------------------------------------------------------------------------------------------- conceptos --

    [Fact]
    public async Task Un_concepto_que_usan_tarifas_vigentes_no_se_inactiva()
    {
        var rtf = await ImpuestoAsync("RTF", TaxKind.ReteFuente);
        var compras = await ConceptoAsync("COMPRAS");
        await CrearTarifaAsync(Tarifa(rtf, "RF25", 0.025m, concepto: compras));

        var handler = new UpdateWithholdingConceptCommandHandler(_db, _reloj);
        var r = await handler.Handle(new UpdateWithholdingConceptCommand(compras, "Compras", false, null, "ya no se usa"), default);
        r.Error.Code.Should().Be("Core.WithholdingConcept.InUse");
        r.Error.Message.Should().Contain("RF25");

        // Cerrada la vigencia antes de hoy, ya se puede inactivar.
        var tarifa = await _db.TaxRates.SingleAsync();
        await new CloseTaxRateCommandHandler(_db).Handle(new CloseTaxRateCommand(tarifa.PublicId, Hoy.AddDays(-1), "cierre"), default);
        (await handler.Handle(new UpdateWithholdingConceptCommand(compras, "Compras", false, null, "ya no se usa"), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Codigo_de_concepto_repetido_es_Catalogo_CodigoDuplicado_y_el_slug_lo_encuentra()
    {
        await ConceptoAsync("COMPRAS");
        (await new CreateWithholdingConceptCommandHandler(_db).Handle(new CreateWithholdingConceptCommand("compras", "Otra", null, "alta"), default))
            .Error.Code.Should().Be("Catalogo.CodigoDuplicado");

        var buscar = new BuscarCodigoDeCatalogoQueryHandler(_db);
        (await buscar.Handle(new BuscarCodigoDeCatalogoQuery("conceptos-de-retencion", "compras"), default)).Value.Existe.Should().BeTrue();
        var iva = await ImpuestoAsync("IVA", TaxKind.Iva);
        await CrearTarifaAsync(Tarifa(iva, "IVA19"));
        (await buscar.Handle(new BuscarCodigoDeCatalogoQuery("impuestos", "IVA"), default)).Value.Existe.Should().BeTrue();
        (await buscar.Handle(new BuscarCodigoDeCatalogoQuery("tarifas", "iva19"), default)).Value.Existe.Should().BeTrue();
        (await buscar.Handle(new BuscarCodigoDeCatalogoQuery("tarifas", "IVA20"), default)).Value.Existe.Should().BeFalse();
    }

    // ------------------------------------------------------------------------------------------------ consultas --

    [Fact]
    public async Task Las_consultas_devuelven_la_forma_del_contrato()
    {
        var iva = await ImpuestoAsync("IVA", TaxKind.Iva);
        await CrearTarifaAsync(Tarifa(iva, "IVA19", desde: new DateOnly(2017, 1, 1), hasta: new DateOnly(2025, 12, 31)));
        await CrearTarifaAsync(Tarifa(iva, "IVA19", 0.20m, desde: new DateOnly(2026, 1, 1)));

        var detalle = (await new GetTaxDefinitionQueryHandler(_db).Handle(new GetTaxDefinitionQuery(iva), default)).Value;
        detalle.Rates.Should().HaveCount(2);

        var vigentes = (await new ListTaxRatesQueryHandler(_db, _reloj).Handle(new ListTaxRatesQuery(OnlyCurrent: true), default)).Value;
        vigentes.Should().ContainSingle().Which.Rate.Should().Be(0.20m);
        var en2020 = (await new ListTaxRatesQueryHandler(_db, _reloj).Handle(new ListTaxRatesQuery(AsOf: new DateOnly(2020, 6, 1)), default)).Value;
        en2020.Should().ContainSingle().Which.Rate.Should().Be(0.19m);

        var una = vigentes.Single();
        var conOtras = (await new GetTaxRateQueryHandler(_db).Handle(new GetTaxRateQuery(una.TaxRatePublicId), default)).Value;
        conOtras.OtherVersions.Should().ContainSingle().Which.ValidTo.Should().Be(new DateOnly(2025, 12, 31));
        conOtras.TaxPublicId.Should().Be(iva);
    }

    // ----------------------------------------------------------------------------------------- la foto --

    [Fact]
    public async Task El_lector_arma_la_foto_a_una_fecha_con_la_UVT_y_los_parametros()
    {
        _db.PayrollLegalParameters.Add(new PayrollLegalParameter
        {
            Code = LegalParameterCodes.Uvt, Name = "UVT", Kind = LegalParameterKind.Amount, Value = 52_374m, ValidFrom = new DateTime(2026, 1, 1),
        });
        await _db.SaveChangesAsync();
        var iva = await ImpuestoAsync("IVA", TaxKind.Iva);
        var rtf = await ImpuestoAsync("RTF", TaxKind.ReteFuente);
        var compras = await ConceptoAsync("COMPRAS");
        await CrearTarifaAsync(Tarifa(iva, "IVA19", desde: new DateOnly(2017, 1, 1), hasta: new DateOnly(2025, 12, 31)));
        await CrearTarifaAsync(Tarifa(iva, "IVA19", 0.20m, desde: new DateOnly(2026, 1, 1)));
        await CrearTarifaAsync(Tarifa(rtf, "RF25", 0.025m, concepto: compras, condiciones: new TaxRateConditionsDto(SubjectIsIncomeTaxFiler: true), minimoUvt: 27m));

        var lector = new LectorDeCatalogoTributario(_db, new LectorDeUvt(_db), new LectorDeParametros(_db));
        var foto = await lector.FotoAsync(new DateOnly(2026, 3, 16));

        foto.IsSuccess.Should().BeTrue(foto.Error.Message);
        var f = foto.Value;
        f.Uvt.Should().Be(52_374m);
        f.RedondeoUvt.Should().Be(RedondeoUvt.Peso, "el defecto de Tributario.RedondeoUvtAPesos");
        f.DecimalesDeMonto.Should().Be(2, "Redondeo.Montos = Centavo por defecto");
        f.Cooperativa.IsVatResponsible.Should().BeTrue("Tributario.ResponsableIva por defecto");
        f.Cooperativa.EsAgenteDeRetencion.Should().BeTrue();
        f.Impuestos.Select(i => i.Code).Should().BeEquivalentTo(["IVA", "RTF"]);
        f.Tarifas.Should().HaveCount(2, "sólo las vigentes a la fecha");
        f.Tarifas.Single(t => t.Code == "IVA19").Rate.Should().Be(0.20m);
        f.Tarifas.Single(t => t.Code == "RF25").Condiciones.SubjectIsIncomeTaxFiler.Should().BeTrue();
        f.Conceptos.Should().ContainSingle(c => c.Code == "COMPRAS");

        // Y el motor corre sobre ella.
        var entrada = new EntradaTributaria
        {
            Fecha = new DateOnly(2026, 3, 16), Perspectiva = TaxAppliesTo.Purchases,
            Vendedor = new PerfilTributario { IsVatResponsible = true, IsIncomeTaxFiler = true }, Comprador = f.Cooperativa,
            Lineas = [new LineaTributaria(1, 2_000_000m, 1m, VatSaleTreatment.Taxed,
                [new ImpuestoDeLinea(f.Impuestos.Single(i => i.Code == "IVA").Id, "IVA19")], f.Conceptos.Single().Id)],
        };
        var resultado = MotorTributario.Calcular(f, entrada);
        resultado.Renglones.Select(r => (r.TaxRateCode, r.Amount)).Should().BeEquivalentTo([("IVA19", 400_000m), ("RF25", 50_000m)]);
    }

    [Fact]
    public async Task Sin_UVT_vigente_la_foto_falla_visible()
    {
        var lector = new LectorDeCatalogoTributario(_db, new LectorDeUvt(_db), new LectorDeParametros(_db));
        (await lector.FotoAsync(new DateOnly(2026, 3, 16))).Error.Code.Should().Be("Taxation.Uvt.Missing");
    }

    // ------------------------------------------------------------------------------------------ la semilla --

    [Fact]
    public async Task La_semilla_deja_las_tarifas_pendientes_de_validar_y_es_idempotente()
    {
        var insertadas = await TaxCatalogSeeder.AplicarAsync(_db, default);
        insertadas.Should().BeGreaterThan(0);
        _db.ChangeTracker.Clear();

        var tarifas = await _db.TaxRates.Include(t => t.TaxDefinition).Include(t => t.WithholdingConcept).ToListAsync();
        tarifas.Should().NotBeEmpty().And.OnlyContain(t => t.ReviewPending, "pendiente de validar por la contadora (A8)");
        tarifas.Should().OnlyContain(t => t.LegalSource.Length > 0, "toda tarifa cita su norma");
        tarifas.Select(t => t.TaxDefinition!.Kind).Should().NotContain(TaxKind.ReteIca, "ReteICA es municipal: no se siembra");
        tarifas.Where(t => t.TaxDefinition!.Kind == TaxKind.ReteFuente).Should().OnlyContain(t => t.WithholdingConcept != null);
        tarifas.Select(t => t.Code).Should().Contain(["IVA19", "IVA5", "IVAEXE", "IVAEXC", "INC8", "BOLSA", "RIVA15"]);
        tarifas.Single(t => t.Code == "BOLSA").AmountPerUnit.Should().BeGreaterThan(0);
        (await _db.TaxDefinitions.Include(d => d.TaxedOnDefinition).SingleAsync(d => d.Code == "RETEIVA")).TaxedOnDefinition!.Code.Should().Be("IVA");

        (await TaxCatalogSeeder.AplicarAsync(_db, default)).Should().Be(0, "idempotente por código y vigencia");
    }

    [Fact]
    public async Task La_semilla_no_pisa_lo_que_la_cooperativa_ya_cargo()
    {
        var iva = await ImpuestoAsync("IVA", TaxKind.Iva);
        await CrearTarifaAsync(Tarifa(iva, "IVA19", desde: new DateOnly(2020, 1, 1)));

        await TaxCatalogSeeder.AplicarAsync(_db, default);

        (await _db.TaxRates.Where(t => t.Code == "IVA19").ToListAsync()).Should().ContainSingle()
            .Which.ReviewPending.Should().BeFalse("la de la cooperativa se cruza con la sembrada: la sembrada no entra");
        (await _db.TaxDefinitions.SingleAsync(t => t.Code == "IVA")).Name.Should().Be("Impuesto IVA");
    }
}
