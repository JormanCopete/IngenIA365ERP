using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Common.Parameters.AddParameterVersion;
using IngenIA365ERP.Application.Common.Parameters.GetParameterHistory;
using IngenIA365ERP.Application.Common.Parameters.ListParameters;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using IngenIA365ERP.Domain.Taxes;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.Parameters;

/// <summary>
/// T012 (feature 012; decisiones-transversales T21; FR-012; contracts/api.md §7): el alta de una vigencia de
/// parámetro —único escritor de <c>COR_ParameterVersions</c>— y las dos consultas de la pantalla. InMemory con
/// reloj fijo, permisos, resolutor de ámbito y reglas de módulo falsos.
/// </summary>
public class AddParameterVersionCommandTests
{
    private const string Inv = ParametrosDeInventario.Modulo;
    private const string StockNegativo = ParametrosDeInventario.ExistenciasStockNegativoPermitido;

    private static readonly DateOnly Hoy = new(2026, 9, 25);
    private static readonly Guid BodegaCentral = Guid.NewGuid();
    private static readonly Guid BodegaAjena = Guid.NewGuid();

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IPermissionChecker _permisos = Substitute.For<IPermissionChecker>();
    private readonly IResolutorDeAmbitoDeParametro _resolutor = Substitute.For<IResolutorDeAmbitoDeParametro>();
    private IReglasDeParametros _reglas = new ReglasDeParametrosVacias();

    public AddParameterVersionCommandTests()
    {
        _permisos.HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var central = new AmbitoDeParametro(ParameterScopeKind.Warehouse, 7, BodegaCentral, "CEN", "Bodega central");
        _resolutor.ResolverAsync(ParameterScopeKind.Warehouse, BodegaCentral, Arg.Any<CancellationToken>())
            .Returns(Result.Success(central));
        _resolutor.ResolverAsync(ParameterScopeKind.Warehouse, BodegaAjena, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<AmbitoDeParametro>(new Error("Inventory.Warehouse.NotFound", "La bodega no existe.")));
        _resolutor.DescribirAsync(ParameterScopeKind.Warehouse, Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(new List<AmbitoDeParametro> { central });
    }

    private AddParameterVersionCommandHandler Handler() => new(_db, _permisos, _resolutor, _reglas);

    private static AddParameterVersionCommand Alta(string clave, string valor, DateOnly desde, string modulo = Inv,
        ParameterScopeKind ambito = ParameterScopeKind.None, Guid? entidad = null, string reason = "Cambio aprobado en comité",
        string? legalSource = null, string? chain = null) =>
        new(modulo, clave, ambito, entidad, chain, valor, desde, reason, legalSource) { OperationKey = Guid.NewGuid() };

    private Task<Result<AddParameterVersionResponse>> Enviar(AddParameterVersionCommand alta) => Handler().Handle(alta, CancellationToken.None);

    private static JsonElement Datos(Error error) =>
        JsonSerializer.SerializeToElement(error.Should().BeOfType<ErrorConDatos>().Subject.Data);

    private static string[] Admitidos(Error error) =>
        Datos(error).GetProperty("allowed").EnumerateArray().Select(e => e.GetString()!).ToArray();

    // ------------------------------------------------------------------------------------------ errores --

    [Fact]
    public async Task Una_clave_fuera_del_catalogo_es_KeyNotFound()
    {
        var r = await Enviar(Alta("Existencias.Inventada", "true", Hoy));

        r.Error.Code.Should().Be("Parameters.KeyNotFound");
        (await _db.ParameterVersions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Un_valor_no_admitido_es_ValueNotAllowed_con_lo_admitido()
    {
        var r = await Enviar(Alta(ParametrosDeInventario.RedondeoMontos, "Decena", Hoy));

        r.Error.Code.Should().Be("Parameters.ValueNotAllowed");
        Admitidos(r.Error).Should().Equal("Centavo", "Peso");
    }

    [Fact]
    public async Task Peps_es_ValueNotAllowed_mientras_no_este_disponible()
    {
        var r = await Enviar(Alta(ParametrosDeInventario.CosteoMetodo, "Peps", new DateOnly(2026, 10, 1)));

        r.Error.Code.Should().Be("Parameters.ValueNotAllowed");
        Admitidos(r.Error).Should().Equal("PromedioPonderado");
    }

    [Fact]
    public async Task Un_ambito_que_la_clave_no_admite_es_ScopeNotAllowed_con_lo_admitido()
    {
        var r = await Enviar(Alta(ParametrosDeInventario.RedondeoMontos, "Peso", Hoy, ambito: ParameterScopeKind.Warehouse, entidad: BodegaCentral));

        r.Error.Code.Should().Be("Parameters.ScopeNotAllowed");
        Admitidos(r.Error).Should().Equal("None");
    }

    [Fact]
    public async Task Una_entidad_de_ambito_fuera_de_alcance_responde_el_404_del_modulo()
    {
        var r = await Enviar(Alta(StockNegativo, "true", Hoy, ambito: ParameterScopeKind.Warehouse, entidad: BodegaAjena));

        r.Error.Code.Should().Be("Inventory.Warehouse.NotFound");
        (await _db.ParameterVersions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Sin_resolutor_de_modulo_ningun_ambito_se_resuelve()
    {
        var handler = new AddParameterVersionCommandHandler(_db, _permisos, new ResolutorDeAmbitoVacio(), _reglas);

        var r = await handler.Handle(Alta(StockNegativo, "true", Hoy, ambito: ParameterScopeKind.Warehouse, entidad: BodegaCentral), CancellationToken.None);

        r.Error.Code.Should().Be("Generic.NotFound");
    }

    [Theory]
    [InlineData(Inv, ParametrosDeInventario.CosteoMetodo, "PromedioPonderado", "Inventory.Costing.Manage")]
    [InlineData(Inv, ParametrosDeInventario.CosteoAmbito, "Bodega", "Inventory.Costing.Manage")]
    [InlineData(ParametrosTributarios.Modulo, ParametrosTributarios.GranContribuyente, "true", "Core.Taxes.Manage")]
    [InlineData(ParametrosDeFacturacionElectronica.Modulo, ParametrosDeFacturacionElectronica.EntregaCorreo, "Canal", "ElectronicInvoicing.Settings.Manage")]
    public async Task Sin_el_permiso_de_la_clave_es_PermissionRequired(string modulo, string clave, string valor, string permiso)
    {
        _permisos.HasPermissionAsync(permiso, Arg.Any<CancellationToken>()).Returns(false);

        var r = await Enviar(Alta(clave, valor, Hoy, modulo));

        r.Error.Code.Should().Be("Parameters.PermissionRequired");
        Datos(r.Error).GetProperty("permissionCode").GetString().Should().Be(permiso);
        (await _db.ParameterVersions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Con_el_permiso_de_la_clave_registra()
    {
        var r = await Enviar(Alta(ParametrosTributarios.GranContribuyente, "true", Hoy, ParametrosTributarios.Modulo));

        r.IsSuccess.Should().BeTrue();
        await _permisos.Received().HasPermissionAsync("Core.Taxes.Manage", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Una_clave_sin_permiso_propio_no_consulta_permisos()
    {
        (await Enviar(Alta(StockNegativo, "true", Hoy))).IsSuccess.Should().BeTrue();

        await _permisos.DidNotReceiveWithAnyArgs().HasPermissionAsync(default!, default);
    }

    [Fact]
    public void Sin_motivo_es_Validation_Invalid()
    {
        var validador = new AddParameterVersionCommandValidator();

        validador.Validate(Alta(StockNegativo, "true", Hoy, reason: "")).IsValid.Should().BeFalse();
        validador.Validate(Alta(StockNegativo, "true", Hoy, reason: "   ")).IsValid.Should().BeFalse();
        validador.Validate(Alta(StockNegativo, "true", Hoy)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void El_validador_exige_coherencia_del_ambito()
    {
        var validador = new AddParameterVersionCommandValidator();

        validador.Validate(Alta(StockNegativo, "true", Hoy, entidad: BodegaCentral)).IsValid.Should().BeFalse("el general no lleva entidad");
        validador.Validate(Alta(StockNegativo, "true", Hoy, ambito: ParameterScopeKind.Warehouse)).IsValid.Should().BeFalse("sin entidad");
        validador.Validate(Alta(StockNegativo, "true", Hoy, ambito: ParameterScopeKind.Warehouse, chain: "Sales"))
            .IsValid.Should().BeFalse("sólo el tipo de documento admite cadena");
        validador.Validate(Alta(ParametrosDeInventario.ContabilidadModoDePaso, "PorLotes", Hoy, ambito: ParameterScopeKind.DocumentType, chain: "Sales"))
            .IsValid.Should().BeTrue();
        validador.Validate(Alta(StockNegativo, "true", default)).IsValid.Should().BeFalse("sin fecha");
    }

    [Fact]
    public async Task Una_clave_que_exige_fuente_legal_sin_ella_es_Validation_Invalid()
    {
        var sin = await Enviar(Alta(ParametrosDeInventario.InformesTopeFaltantesPorcentaje, "0.03", Hoy));
        sin.Error.Code.Should().Be("Validation.Invalid");

        var con = await Enviar(Alta(ParametrosDeInventario.InformesTopeFaltantesPorcentaje, "0.03", Hoy, legalSource: "Acta 12 del consejo"));
        con.IsSuccess.Should().BeTrue();
        (await _db.ParameterVersions.SingleAsync()).LegalSource.Should().Be("Acta 12 del consejo");
    }

    // ------------------------------------------------------------------------------------------ vigencia --

    [Fact]
    public async Task La_primera_vigencia_se_guarda_con_su_texto_canonico_y_sin_cerrar_nada()
    {
        var r = await Enviar(Alta(StockNegativo, "TRUE", Hoy));

        r.IsSuccess.Should().BeTrue();
        r.Value.PreviousClosedOn.Should().BeNull();
        var guardada = await _db.ParameterVersions.SingleAsync();
        guardada.PublicId.Should().Be(r.Value.VersionPublicIds.Single());
        guardada.Value.Should().Be("true");
        guardada.Module.Should().Be(Inv);
        guardada.Key.Should().Be(StockNegativo);
        guardada.ScopeKind.Should().Be(ParameterScopeKind.None);
        guardada.ScopeId.Should().Be(0);
        guardada.ValidTo.Should().BeNull();
        guardada.Reason.Should().Be("Cambio aprobado en comité");
    }

    [Fact]
    public async Task La_nueva_cierra_la_anterior_la_vispera()
    {
        await Enviar(Alta(StockNegativo, "true", new DateOnly(2026, 1, 1)));

        var r = await Enviar(Alta(StockNegativo, "false", new DateOnly(2026, 10, 1)));

        r.IsSuccess.Should().BeTrue();
        r.Value.PreviousClosedOn.Should().Be(new DateOnly(2026, 9, 30));
        var vigencias = await _db.ParameterVersions.OrderBy(v => v.ValidFrom).ToListAsync();
        vigencias.Should().HaveCount(2);
        vigencias[0].ValidTo.Should().Be(new DateOnly(2026, 9, 30));
        vigencias[1].ValidTo.Should().BeNull();
    }

    [Fact]
    public async Task Una_vigencia_que_ya_termino_antes_no_se_toca()
    {
        _db.ParameterVersions.Add(new ParameterVersion
        {
            Module = Inv, Key = StockNegativo, Value = "true", ValidFrom = new DateOnly(2026, 1, 1), ValidTo = new DateOnly(2026, 3, 31), Reason = "x",
        });
        await _db.SaveChangesAsync();

        var r = await Enviar(Alta(StockNegativo, "false", new DateOnly(2026, 6, 1)));

        r.Value.PreviousClosedOn.Should().BeNull();
        (await _db.ParameterVersions.OrderBy(v => v.ValidFrom).FirstAsync()).ValidTo.Should().Be(new DateOnly(2026, 3, 31));
    }

    [Theory]
    [InlineData(2026, 10, 1)]
    [InlineData(2026, 9, 1)]
    public async Task Una_vigencia_que_empieza_ese_dia_o_despues_es_Overlaps(int anio, int mes, int dia)
    {
        await Enviar(Alta(StockNegativo, "true", new DateOnly(2026, 10, 1)));

        var r = await Enviar(Alta(StockNegativo, "false", new DateOnly(anio, mes, dia)));

        r.Error.Code.Should().Be("Parameters.Overlaps");
        Datos(r.Error).GetProperty("existingValidFrom").GetString().Should().Be("2026-10-01");
        (await _db.ParameterVersions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task El_general_y_la_excepcion_de_una_bodega_no_se_cruzan_entre_si()
    {
        await Enviar(Alta(StockNegativo, "false", new DateOnly(2026, 1, 1)));

        var r = await Enviar(Alta(StockNegativo, "true", new DateOnly(2026, 1, 1), ambito: ParameterScopeKind.Warehouse, entidad: BodegaCentral));

        r.IsSuccess.Should().BeTrue();
        r.Value.PreviousClosedOn.Should().BeNull();
        var excepcion = await _db.ParameterVersions.SingleAsync(v => v.ScopeKind == ParameterScopeKind.Warehouse);
        excepcion.ScopeId.Should().Be(7);
    }

    // ------------------------------------------------------------------------------ reglas del módulo --

    [Fact]
    public async Task Las_reglas_del_modulo_corren_antes_de_guardar_y_pueden_rechazar()
    {
        var reglas = Substitute.For<IReglasDeParametros>();
        reglas.EvaluarAsync(Arg.Any<AltaDeParametro>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<DecisionDeReglasDeParametro>(new Error("Parameters.RequiresPeriodStart", "Sólo al inicio de un período.")));
        _reglas = reglas;

        var r = await Enviar(Alta(ParametrosDeInventario.CosteoAmbito, "bodega", new DateOnly(2026, 10, 15)));

        r.Error.Code.Should().Be("Parameters.RequiresPeriodStart");
        (await _db.ParameterVersions.CountAsync()).Should().Be(0);
        await reglas.Received(1).EvaluarAsync(
            Arg.Is<AltaDeParametro>(a => a.Valor == "Bodega" && a.ValidFrom == new DateOnly(2026, 10, 15) && a.Definicion.Clave == ParametrosDeInventario.CosteoAmbito),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Una_cadena_escribe_una_vigencia_por_tipo_y_los_informa()
    {
        var compra = new AmbitoDeParametro(ParameterScopeKind.DocumentType, 11, Guid.NewGuid(), "FC", "Factura de compra");
        var recepcion = new AmbitoDeParametro(ParameterScopeKind.DocumentType, 12, Guid.NewGuid(), "RC", "Recepción");
        var reglas = Substitute.For<IReglasDeParametros>();
        reglas.EvaluarAsync(Arg.Any<AltaDeParametro>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new DecisionDeReglasDeParametro([compra, recepcion], [compra, recepcion])));
        _reglas = reglas;

        var r = await Enviar(Alta(ParametrosDeInventario.ContabilidadModoDePaso, "PorLotes", new DateOnly(2026, 10, 1),
            ambito: ParameterScopeKind.DocumentType, chain: "Purchases"));

        r.IsSuccess.Should().BeTrue();
        r.Value.VersionPublicIds.Should().HaveCount(2);
        r.Value.AffectedDocumentTypes!.Select(t => t.Code).Should().Equal("FC", "RC");
        (await _db.ParameterVersions.Select(v => v.ScopeId).OrderBy(i => i).ToListAsync()).Should().Equal(11, 12);
    }

    [Fact]
    public async Task Una_cadena_sin_reglas_del_modulo_que_la_resuelvan_es_Validation_Invalid()
    {
        var r = await Enviar(Alta(ParametrosDeInventario.ContabilidadModoDePaso, "PorLotes", Hoy, ambito: ParameterScopeKind.DocumentType, chain: "Sales"));

        r.Error.Code.Should().Be("Validation.Invalid");
    }

    // ------------------------------------------------------------------------------------- consultas --

    [Fact]
    public async Task La_lista_muestra_valor_actual_excepciones_y_programadas_a_la_fecha()
    {
        await Enviar(Alta(StockNegativo, "false", new DateOnly(2026, 1, 1)));
        await Enviar(Alta(StockNegativo, "true", new DateOnly(2026, 2, 1), ambito: ParameterScopeKind.Warehouse, entidad: BodegaCentral));
        await Enviar(Alta(StockNegativo, "true", new DateOnly(2026, 12, 1)));

        var reloj = Substitute.For<IDateTimeService>();
        reloj.HoyLocal.Returns(Hoy);
        var handler = new ListParametersQueryHandler(new LectorDeParametros(_db), _resolutor, reloj);

        var r = await handler.Handle(new ListParametersQuery(Inv, StockNegativo), CancellationToken.None);

        var dto = r.Value.Single();
        dto.Type.Should().Be("Bool");
        dto.DefaultValue.Should().Be("false");
        dto.AllowedScopes.Should().Equal(ParameterScopeKind.None, ParameterScopeKind.Warehouse);
        dto.Current.Source.Should().Be("Version");
        dto.Current.Value.Should().Be("false");
        dto.Current.ValidTo.Should().Be(new DateOnly(2026, 11, 30));
        dto.Overrides.Single().Scope!.Code.Should().Be("CEN");
        dto.Scheduled.Single().ValidFrom.Should().Be(new DateOnly(2026, 12, 1));

        var antes = await handler.Handle(new ListParametersQuery(Inv, StockNegativo, new DateOnly(2025, 6, 1)), CancellationToken.None);
        antes.Value.Single().Current.Source.Should().Be("Default");
    }

    [Fact]
    public async Task La_lista_sin_filtro_trae_todo_el_catalogo_y_una_clave_inexistente_es_KeyNotFound()
    {
        var reloj = Substitute.For<IDateTimeService>();
        reloj.HoyLocal.Returns(Hoy);
        var handler = new ListParametersQueryHandler(new LectorDeParametros(_db), _resolutor, reloj);

        (await handler.Handle(new ListParametersQuery(), CancellationToken.None)).Value.Should().HaveCount(57);
        (await handler.Handle(new ListParametersQuery(ParametrosTributarios.Modulo), CancellationToken.None)).Value.Should().HaveCount(6);
        (await handler.Handle(new ListParametersQuery(Inv, "No.Existe"), CancellationToken.None)).Error.Code.Should().Be("Parameters.KeyNotFound");
    }

    [Fact]
    public async Task El_historial_va_de_la_mas_reciente_a_la_mas_antigua_y_filtra_por_ambito()
    {
        await Enviar(Alta(StockNegativo, "false", new DateOnly(2026, 1, 1)));
        await Enviar(Alta(StockNegativo, "true", new DateOnly(2026, 6, 1)));
        await Enviar(Alta(StockNegativo, "true", new DateOnly(2026, 2, 1), ambito: ParameterScopeKind.Warehouse, entidad: BodegaCentral));
        var handler = new GetParameterHistoryQueryHandler(new LectorDeParametros(_db), _resolutor);

        var todo = await handler.Handle(new GetParameterHistoryQuery(Inv, StockNegativo), CancellationToken.None);
        todo.Value.Select(h => h.ValidFrom).Should().Equal(new DateOnly(2026, 6, 1), new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1));

        var bodega = await handler.Handle(new GetParameterHistoryQuery(Inv, StockNegativo, ParameterScopeKind.Warehouse, BodegaCentral), CancellationToken.None);
        bodega.Value.Single().Scope!.Name.Should().Be("Bodega central");

        var ajena = await handler.Handle(new GetParameterHistoryQuery(Inv, StockNegativo, ParameterScopeKind.Warehouse, BodegaAjena), CancellationToken.None);
        ajena.Error.Code.Should().Be("Inventory.Warehouse.NotFound");
    }
}
