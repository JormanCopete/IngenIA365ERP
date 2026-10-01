using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.Application.ElectronicInvoicing.Numeracion;
using IngenIA365ERP.Application.ElectronicInvoicing.Settings;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Dian;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.ElectronicInvoicing.Settings;

/// <summary>
/// Feature 012, I4, T682 (api.md §24.1, §24.2, §24.3; contracts/dian.md §9, §10; data-model §27 duda 9): la configuración de emisión con
/// vigencia —cierra la anterior la víspera; rechaza canal desconocido, canal que no acepta el número del ERP, software propio sin
/// identificador, canal sin resolución asociada (<c>data.kinds[]</c>), simulado en producción y cruces—; la credencial derivada y su
/// verificación; las resoluciones —prefijo, rango, cruces, tipo respaldado, prefijo de notas, <c>InUse</c> con números emitidos— y su
/// asociación al canal con la clave técnica sólo en factura y enmascarada; el estado calculado y la preparación.
/// </summary>
public class ConfiguracionYResolucionesTests
{
    private static readonly DateOnly Enero = new(2026, 1, 1);

    private readonly EmisionDePrueba _e = new(new CanalDePrueba("SINNUMERO", CanalDePrueba.Completas(aceptaNumero: false)));

    // --------------------------------------------------------------------------------------- configuración --

    private ConfigureEmissionCommandHandler Configurar() => new(_e.Db, _e.Canales, _e.Tenant);

    private static ConfigureEmissionCommand Pedido(string canal = GuardiaDeEmisionFiscal.CanalSimulado, DianEnvironment ambiente = DianEnvironment.Testing,
        DateOnly? desde = null, EmissionMode modo = EmissionMode.TechnologyProvider, string? software = null) =>
        new(modo, canal, ambiente, software, null, EmailDeliveryBy.Erp, true, desde ?? Enero, "arranque", "900123456", "7", "Cooperativa",
            "Calle 1", "76001", "facturas@coop.co");

    [Fact]
    public async Task Configurar_crea_la_vigencia_con_la_clave_derivada_y_sin_verificar()
    {
        var r = await Configurar().Handle(Pedido(), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : "");
        var fila = await _e.Db.ElectronicEmissionSettings.SingleAsync();
        fila.CredentialKey.Should().Be($"{_e.Cooperativa:D}.SIMULADO.json");
        fila.CredentialVerifiedAt.Should().BeNull();
        fila.ChannelCode.Should().Be("SIMULADO");
        r.Value.ValidTo.Should().BeNull();
    }

    [Fact]
    public async Task Una_vigencia_nueva_cierra_la_anterior_la_vispera()
    {
        (await Configurar().Handle(Pedido(), default)).IsSuccess.Should().BeTrue();
        _e.Resolucion(ResolutionKind.Invoice, "FE", canal: "PROV", validaHasta: new DateOnly(2027, 12, 31));

        var r = await Configurar().Handle(Pedido("PROV", desde: new DateOnly(2026, 7, 1)), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : "");
        var filas = await _e.Db.ElectronicEmissionSettings.OrderBy(s => s.ValidFrom).ToListAsync();
        filas[0].ValidTo.Should().Be(new DateOnly(2026, 6, 30));
        filas[1].ValidTo.Should().BeNull();
        filas[1].IssuerMunicipalityDaneCode.Should().Be("76001");
    }

    [Fact]
    public async Task Rechaza_un_canal_desconocido()
    {
        var r = await Configurar().Handle(Pedido("NOEXISTE"), default);

        r.Error.Code.Should().Be(ErroresDeNumeracionYConfiguracion.ChannelUnknownCode);
    }

    [Fact]
    public async Task Rechaza_un_canal_que_no_acepta_el_numero_del_ERP()
    {
        var r = await Configurar().Handle(Pedido("SINNUMERO"), default);

        r.Error.Code.Should().Be(ErroresDeNumeracionYConfiguracion.ChannelRejectsErpNumberCode);
    }

    [Fact]
    public async Task Software_propio_exige_el_identificador()
    {
        var r = await Configurar().Handle(Pedido("PROV", modo: EmissionMode.OwnSoftware), default);

        r.Error.Code.Should().Be(ErroresDeNumeracionYConfiguracion.SoftwareIdRequiredCode);
    }

    [Fact]
    public async Task Rechaza_el_simulado_en_produccion()
    {
        var r = await Configurar().Handle(Pedido(ambiente: DianEnvironment.Production), default);

        r.Error.Code.Should().Be(ErroresDeNumeracionYConfiguracion.SimulatedInProductionCode);
    }

    [Fact]
    public async Task Exige_resolucion_asociada_al_canal_nuevo_por_cada_tipo_en_uso()
    {
        _e.Resolucion(ResolutionKind.Invoice, "SETP");
        _e.Resolucion(ResolutionKind.PosEquivalent, "POS");
        _e.Resolucion(ResolutionKind.SupportDocument, "DS", canal: "PROV");

        var r = await Configurar().Handle(Pedido("PROV"), default);

        r.Error.Code.Should().Be(ErroresDeNumeracionYConfiguracion.NoResolutionForChannelCode);
        var datos = ((ErrorConDatos)r.Error).Data;
        datos.GetType().GetProperty("kinds")!.GetValue(datos).Should().BeEquivalentTo(new[] { "Invoice", "PosEquivalent" });
    }

    [Fact]
    public async Task Dos_vigencias_no_se_cruzan()
    {
        (await Configurar().Handle(Pedido(desde: new DateOnly(2026, 7, 1)), default)).IsSuccess.Should().BeTrue();

        var r = await Configurar().Handle(Pedido(desde: new DateOnly(2026, 3, 1)), default);

        r.Error.Code.Should().Be(ErroresDeNumeracionYConfiguracion.SettingsOverlapsCode);
    }

    // -------------------------------------------------------------------------------------------- credencial --

    private VerifyChannelCredentialCommandHandler Verificar() => new(_e.Db, _e.Canales, _e.Credenciales, _e.Tenant, _e.Reloj);

    [Fact]
    public async Task Verificar_la_credencial_sella_la_fecha_si_el_canal_responde()
    {
        (await Configurar().Handle(Pedido(), default)).IsSuccess.Should().BeTrue();

        var r = await Verificar().Handle(new VerifyChannelCredentialCommand(null), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : "");
        r.Value.Verified.Should().BeTrue();
        (await _e.Db.ElectronicEmissionSettings.SingleAsync()).CredentialVerifiedAt.Should().Be(_e.Reloj.UtcNow);
        _e.Simulado.UltimoContexto!.TenantPublicId.Should().Be(_e.Cooperativa);
    }

    [Fact]
    public async Task Clave_alterada_en_la_base_o_archivo_ausente_responde_CredentialMismatch()
    {
        (await Configurar().Handle(Pedido(), default)).IsSuccess.Should().BeTrue();
        var fila = await _e.Db.ElectronicEmissionSettings.SingleAsync();
        fila.CredentialKey = "otra-cooperativa.SIMULADO.json";
        await _e.Db.SaveChangesAsync();

        (await Verificar().Handle(new VerifyChannelCredentialCommand(null), default)).Error.Code
            .Should().Be(ErroresDeNumeracionYConfiguracion.CredentialMismatchCode);

        fila.CredentialKey = $"{_e.Cooperativa:D}.SIMULADO.json";
        await _e.Db.SaveChangesAsync();
        _e.Credenciales.ResolverAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<CredencialesDeCanal>("x", "no está el archivo"));
        (await Verificar().Handle(new VerifyChannelCredentialCommand(null), default)).Error.Code
            .Should().Be(ErroresDeNumeracionYConfiguracion.CredentialMismatchCode);
        fila.CredentialVerifiedAt.Should().BeNull();
    }

    [Fact]
    public async Task La_consulta_de_configuracion_muestra_la_vigente_los_canales_y_la_credencial()
    {
        (await Configurar().Handle(Pedido(), default)).IsSuccess.Should().BeTrue();

        var r = await new GetEmissionSettingsQueryHandler(_e.Db, _e.Canales, _e.Credenciales, _e.Reloj).Handle(new GetEmissionSettingsQuery(), default);

        r.Value.Current!.ChannelCode.Should().Be("SIMULADO");
        r.Value.AvailableChannels.Select(c => c.ChannelCode).Should().Contain(["SIMULADO", "PROV"]);
        r.Value.AvailableChannels.Single(c => c.ChannelCode == "SIMULADO").Capabilities.DocumentKinds.Should().Contain("Invoice");
        r.Value.Credential.Should().Be(new CredencialDelCanalDto($"{_e.Cooperativa:D}.SIMULADO.json", true, null));
    }

    // ------------------------------------------------------------------------------------------ resoluciones --

    private RegisterNumberingResolutionCommandHandler Registrar() => new(_e.Db, new PrefijosDeInventario(_e.Db), _e.Reloj);

    private static RegisterNumberingResolutionCommand Resolucion(ResolutionKind tipo = ResolutionKind.Invoice, string prefijo = "SETP",
        long desde = 1, long hasta = 1000, ResolutionKind? respalda = null, string numero = "18764000001", DateOnly? validaDesde = null,
        DateOnly? validaHasta = null) =>
        new(tipo, respalda, numero, new DateOnly(2025, 12, 20), prefijo, desde, hasta, validaDesde ?? Enero, validaHasta ?? new DateOnly(2027, 12, 31),
            DianEnvironment.Testing, "resolución nueva");

    [Fact]
    public async Task Registrar_deja_el_numerador_en_RangeFrom_menos_uno()
    {
        var r = await Registrar().Handle(Resolucion(prefijo: "setp", desde: 990000000, hasta: 995000000), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : "");
        r.Value.Prefix.Should().Be("SETP");
        r.Value.LastIssuedNumber.Should().BeNull("nada emitido");
        (await _e.Db.DianNumberingResolutions.SingleAsync()).LastIssuedNumber.Should().Be(989999999);
        r.Value.Status.Should().Be("Active");
    }

    [Theory]
    [InlineData("AB-1")]
    [InlineData("ABCDE")]
    [InlineData("A B")]
    public async Task Prefijo_invalido(string prefijo)
    {
        (await Registrar().Handle(Resolucion(prefijo: prefijo), default)).Error.Code.Should().Be(ErroresDeNumeracionYConfiguracion.PrefixInvalidCode);
    }

    [Theory]
    [InlineData(10, 5)]
    [InlineData(0, 5)]
    public async Task Rango_invalido(long desde, long hasta)
    {
        (await Registrar().Handle(Resolucion(desde: desde, hasta: hasta), default)).Error.Code.Should().Be(ErroresDeNumeracionYConfiguracion.RangeInvalidCode);
    }

    [Fact]
    public async Task Mismo_tipo_prefijo_y_ambiente_con_rangos_o_vigencias_cruzados_Overlaps()
    {
        (await Registrar().Handle(Resolucion(desde: 1, hasta: 1000, validaHasta: new DateOnly(2026, 6, 30)), default)).IsSuccess.Should().BeTrue();

        (await Registrar().Handle(Resolucion(desde: 500, hasta: 2000, numero: "2", validaDesde: new DateOnly(2026, 7, 1)), default))
            .Error.Code.Should().Be(ErroresDeNumeracionYConfiguracion.ResolutionOverlapsCode, "rangos cruzados");
        (await Registrar().Handle(Resolucion(desde: 1001, hasta: 2000, numero: "3", validaDesde: new DateOnly(2026, 6, 1)), default))
            .Error.Code.Should().Be(ErroresDeNumeracionYConfiguracion.ResolutionOverlapsCode, "vigencias cruzadas");
        (await Registrar().Handle(Resolucion(desde: 1001, hasta: 2000, numero: "4", validaDesde: new DateOnly(2026, 7, 1)), default))
            .IsSuccess.Should().BeTrue("la renovación sigue el rango y empieza al vencer la anterior");
    }

    [Fact]
    public async Task Contingencia_sin_tipo_respaldado_BackedKindRequired()
    {
        (await Registrar().Handle(Resolucion(ResolutionKind.Contingency, "CFE"), default)).Error.Code
            .Should().Be(ErroresDeNumeracionYConfiguracion.BackedKindRequiredCode);
        (await Registrar().Handle(Resolucion(ResolutionKind.Contingency, "CFE", respalda: ResolutionKind.Invoice), default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Un_prefijo_de_notas_no_puede_ser_el_de_una_resolucion_y_al_reves()
    {
        var nota = new InventoryDocumentType { Code = "NC", Name = "Nota crédito", Class = DocumentClass.CreditNote };
        nota.Sequences.Add(new DocumentSequence { DocumentType = nota, Prefix = "NC", NextValue = 1, ValidFrom = Enero });
        _e.Db.InventoryDocumentTypes.Add(nota);
        await _e.Db.SaveChangesAsync();

        (await Registrar().Handle(Resolucion(prefijo: "NC"), default)).Error.Code.Should().Be(ErroresDeNumeracionYConfiguracion.PrefixInUseCode);

        (await Registrar().Handle(Resolucion(prefijo: "SETP"), default)).IsSuccess.Should().BeTrue();
        (await PrefijosDeInventario.ValidarPrefijoDeNotaAsync(_e.Db, DocumentClass.DebitNote, "SETP", default)).Error.Code
            .Should().Be(ErroresDeNumeracionYConfiguracion.PrefixInUseCode);
        (await PrefijosDeInventario.ValidarPrefijoDeNotaAsync(_e.Db, DocumentClass.PositiveAdjustment, "SETP", default)).IsSuccess
            .Should().BeTrue("sólo las notas electrónicas comparten la numeración con las resoluciones");
    }

    [Fact]
    public async Task Corregir_con_numeros_emitidos_responde_InUse_salvo_retirarla()
    {
        var registrada = (await Registrar().Handle(Resolucion(desde: 1, hasta: 100), default)).Value;
        var fila = await _e.Db.DianNumberingResolutions.SingleAsync();
        await new NumeradorFiscal(_e.Db, Substitute.For<IngenIA365ERP.Application.Inventory.Common.ICerrojoDeInventario>())
            .NumerarAsync(new(ElectronicDocumentKind.Invoice, "SETP", DianEnvironment.Testing, "SIMULADO", null, EmisionDePrueba.Hoy));
        fila.LastIssuedNumber.Should().Be(0, "no está asociada todavía: no numera");
        fila.Channels.Add(new() { Resolution = fila, ChannelCode = "SIMULADO", ValidFrom = Enero });
        await _e.Db.SaveChangesAsync();
        (await new NumeradorFiscal(_e.Db, Substitute.For<IngenIA365ERP.Application.Inventory.Common.ICerrojoDeInventario>())
            .NumerarAsync(new(ElectronicDocumentKind.Invoice, "SETP", DianEnvironment.Testing, "SIMULADO", null, EmisionDePrueba.Hoy))).IsSuccess.Should().BeTrue();

        var actualizar = new UpdateNumberingResolutionCommandHandler(_e.Db, new PrefijosDeInventario(_e.Db), _e.Reloj);
        UpdateNumberingResolutionCommand Cambio(long hasta, DateOnly validaHasta) => new(registrada.ResolutionPublicId, ResolutionKind.Invoice, null,
            "18764000001", new DateOnly(2025, 12, 20), "SETP", 1, hasta, Enero, validaHasta, DianEnvironment.Testing, "corrección");

        (await actualizar.Handle(Cambio(200, new DateOnly(2027, 12, 31)), default)).Error.Code.Should().Be(ErroresDeNumeracionYConfiguracion.ResolutionInUseCode);
        (await actualizar.Handle(Cambio(100, new DateOnly(2028, 12, 31)), default)).Error.Code.Should().Be(ErroresDeNumeracionYConfiguracion.ResolutionInUseCode,
            "la fecha final sólo va hacia atrás");
        var retirada = await actualizar.Handle(Cambio(100, new DateOnly(2026, 12, 31)), default);
        retirada.IsSuccess.Should().BeTrue();
        retirada.Value.ValidTo.Should().Be(new DateOnly(2026, 12, 31));
    }

    [Fact]
    public async Task Corregir_sin_numeros_emitidos_mueve_el_rango_y_el_numerador()
    {
        var registrada = (await Registrar().Handle(Resolucion(desde: 1, hasta: 100), default)).Value;
        var actualizar = new UpdateNumberingResolutionCommandHandler(_e.Db, new PrefijosDeInventario(_e.Db), _e.Reloj);

        var r = await actualizar.Handle(new(registrada.ResolutionPublicId, ResolutionKind.Invoice, null, "18764000001", new DateOnly(2025, 12, 20),
            "SETP", 101, 500, Enero, new DateOnly(2027, 12, 31), DianEnvironment.Testing, "digitado mal"), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : "");
        (await _e.Db.DianNumberingResolutions.SingleAsync()).LastIssuedNumber.Should().Be(100);
    }

    // ------------------------------------------------------------------------------------ asociación al canal --

    private LinkResolutionToChannelCommandHandler Asociar() => new(_e.Db, _e.Canales, _e.Credenciales, _e.Tenant, _e.Reloj);

    [Fact]
    public async Task La_clave_tecnica_solo_en_factura_y_enmascarada()
    {
        var factura = (await Registrar().Handle(Resolucion(), default)).Value;
        var pos = (await Registrar().Handle(Resolucion(ResolutionKind.PosEquivalent, "POS", numero: "9"), default)).Value;

        (await Asociar().Handle(new(pos.ResolutionPublicId, "SIMULADO", null, Enero, "clave-secreta-ab12", false, "asociar"), default))
            .Error.Code.Should().Be(ErroresDeNumeracionYConfiguracion.TechnicalKeyNotAllowedCode);

        var r = await Asociar().Handle(new(factura.ResolutionPublicId, "simulado", null, Enero, "fc8eac422eba16e22ffd8c6f94b3f40a6e38162c-ab12", false, "asociar"), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : "");
        r.Value.Channels.Single().TechnicalKeyMasked.Should().Be("••••ab12");
        r.Value.Channels.Single().ChannelCode.Should().Be("SIMULADO");
        (await _e.Db.DianResolutionChannels.SingleAsync()).TechnicalKey.Should().EndWith("ab12", "se guarda entera; sólo la respuesta se enmascara");
    }

    [Fact]
    public async Task Asociar_a_otro_canal_cierra_la_asociacion_anterior_la_vispera_y_no_se_cruza()
    {
        var factura = (await Registrar().Handle(Resolucion(), default)).Value;
        (await Asociar().Handle(new(factura.ResolutionPublicId, "SIMULADO", null, Enero, null, false, "arranque"), default)).IsSuccess.Should().BeTrue();

        var r = await Asociar().Handle(new(factura.ResolutionPublicId, "PROV", null, new DateOnly(2026, 8, 1), null, false, "proveedor"), default);

        r.Value.Channels.Should().HaveCount(2);
        r.Value.Channels[0].ValidTo.Should().Be(new DateOnly(2026, 7, 31));
        (await Asociar().Handle(new(factura.ResolutionPublicId, "SIMULADO", null, new DateOnly(2026, 8, 1), null, false, "otra"), default))
            .Error.Code.Should().Be(ErroresDeNumeracionYConfiguracion.ResolutionOverlapsCode);
        (await Asociar().Handle(new(factura.ResolutionPublicId, "NOEXISTE", null, new DateOnly(2026, 9, 1), null, false, "x"), default))
            .Error.Code.Should().Be(ErroresDeNumeracionYConfiguracion.ChannelUnknownCode);
    }

    [Fact]
    public async Task Traer_la_clave_del_canal_solo_propone_la_de_esa_resolucion()
    {
        var factura = (await Registrar().Handle(Resolucion(), default)).Value;
        _e.Simulado.RespuestaDeRangos = ResultadoDeCanal.Sin(ChannelOutcome.Validated, 0,
            RangosDelCanal.Mensaje("OTRO", "1", "no-es-esta"), RangosDelCanal.Mensaje("SETP", "18764000001", "clave-del-canal-9f3c"));

        var r = await Asociar().Handle(new(factura.ResolutionPublicId, "SIMULADO", null, Enero, null, true, "traer clave"), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : "");
        r.Value.Channels.Single().TechnicalKeyMasked.Should().Be("••••9f3c");
        (await _e.Db.DianNumberingResolutions.CountAsync()).Should().Be(1, "consultar rangos nunca crea resoluciones");
    }

    // ------------------------------------------------------------------------------------ estado y preparación --

    [Fact]
    public async Task El_estado_se_calcula_a_la_fecha()
    {
        _e.Resolucion(ResolutionKind.Invoice, "ACT", desde: 1, hasta: 10);
        _e.Resolucion(ResolutionKind.Invoice, "VEN", validaHasta: new DateOnly(2026, 11, 30));
        _e.Resolucion(ResolutionKind.Invoice, "FUT", validaDesde: new DateOnly(2027, 1, 1), validaHasta: new DateOnly(2027, 12, 31));

        var r = await new ListNumberingResolutionsQueryHandler(_e.Db, _e.Reloj).Handle(new ListNumberingResolutionsQuery(), default);

        r.Value.Single(x => x.Prefix == "ACT").Status.Should().Be("Active");
        r.Value.Single(x => x.Prefix == "ACT").DaysToExpire.Should().Be(26);
        r.Value.Single(x => x.Prefix == "VEN").Status.Should().Be("Expired");
        r.Value.Single(x => x.Prefix == "FUT").Status.Should().Be("NotYetValid");
        (await new ListNumberingResolutionsQueryHandler(_e.Db, _e.Reloj).Handle(new ListNumberingResolutionsQuery(Status: "Expired"), default))
            .Value.Should().ContainSingle();
    }

    [Fact]
    public async Task La_preparacion_sin_configuracion_lista_lo_que_falta()
    {
        _e.Obligada(true);
        var handler = new GetDianReadinessQueryHandler(_e.Db, _e.Guardia(), _e.Lector(), _e.Reloj);

        var r = await handler.Handle(new GetDianReadinessQuery(), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Obligated.Should().BeTrue();
        r.Value.Verdict.Should().Be(VeredictoFiscal.Blocked);
        r.Value.Missing.Should().Contain(m => m.Code == GuardiaDeEmisionFiscal.NoSettingsCode && m.WhoFixes.Page == "/admin/facturacion-electronica");
        r.Value.Channel.Should().BeNull();
    }

    [Fact]
    public async Task La_preparacion_lista_para_facturar_responde_Electronic()
    {
        _e.Obligada(true);
        _e.Configuracion();
        _e.Resolucion(ResolutionKind.Invoice, "SETP", desde: 1, hasta: 10);
        var tipo = new InventoryDocumentType { Code = "FV", Name = "Factura", Class = DocumentClass.SalesInvoice, FiscalPrefix = "SETP", IsActive = true };
        _e.Db.InventoryDocumentTypes.Add(tipo);
        await _e.Db.SaveChangesAsync();
        var handler = new GetDianReadinessQueryHandler(_e.Db, _e.Guardia(), _e.Lector(), _e.Reloj);

        var r = await handler.Handle(new GetDianReadinessQuery(DocumentType: tipo.PublicId), default);

        r.Value.Verdict.Should().Be(VeredictoFiscal.Electronic);
        r.Value.Missing.Should().BeEmpty();
        r.Value.Channel!.ChannelCode.Should().Be("SIMULADO");
        r.Value.Resolutions.Single().Status.Should().Be("Active");
    }
}
