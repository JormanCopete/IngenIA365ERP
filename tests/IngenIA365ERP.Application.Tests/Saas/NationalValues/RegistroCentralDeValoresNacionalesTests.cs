using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Saas.NationalValues;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Domain.Exceptions;
using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Saas.NationalValues;

/// <summary>
/// T0022 (feature 013, T37, T38; data-model parte 3 §3; contracts/api.md §21.5): el registro central de valores nacionales del
/// maestro —<see cref="RegisterNationalValueCommand"/>, <see cref="CorrectNationalValueCommand"/>,
/// <see cref="RegisterRegulatoryDocumentCommand"/>— sobre la base administrativa en memoria: numeración común sin huecos, sólo
/// inserción (la guarda de <c>AdminDbContext</c>, C68), corrección con motivo y el catálogo cerrado de códigos. También la semilla
/// central (<see cref="PlatformNationalValuesSeeder"/>, Order 6), que recibió los festivos de la semilla de Nómina (T93).
/// </summary>
public class RegistroCentralDeValoresNacionalesTests
{
    private static readonly DateOnly Enero2026 = new(2026, 1, 1);

    // ------------------------------------------------------------------------------------------- numeración --

    [Fact]
    public async Task Valores_y_documentos_comparten_una_Sequence_creciente_y_sin_huecos_del_contador()
    {
        using var e = new RegistroCentralDePrueba();

        (await e.RegistrarAsync(RegistroCentralDePrueba.Smmlv())).IsSuccess.Should().BeTrue();
        (await e.CatalogoDeModalidadesAsync()).IsSuccess.Should().BeTrue();
        (await e.RegistrarAsync(new RegisterNationalValueCommand(CodigosDeValorNacional.Uvt, null, Enero2026, null, null, 52_374m, "Pesos", "Resolución DIAN 000238")))
            .IsSuccess.Should().BeTrue();

        var valores = await e.Db.PlatformNationalValues.OrderBy(v => v.Sequence).Select(v => v.Sequence).ToListAsync();
        var documentos = await e.Db.PlatformRegulatoryDocuments.Select(d => d.Sequence).ToListAsync();
        valores.Concat(documentos).OrderBy(s => s).Should().Equal([1L, 2L, 3L], "una sola numeración para las dos tablas");
        documentos.Should().Equal([2L]);
        (await e.Db.PlatformNationalValueCounters.SingleAsync()).NextValue.Should().Be(4);
    }

    [Fact]
    public async Task Un_registro_rechazado_no_consume_numero()
    {
        using var e = new RegistroCentralDePrueba();
        (await e.RegistrarAsync(RegistroCentralDePrueba.Smmlv())).IsSuccess.Should().BeTrue();

        (await e.RegistrarAsync(RegistroCentralDePrueba.Smmlv(1_800_000m))).IsFailure.Should().BeTrue();
        (await e.RegistrarAsync(RegistroCentralDePrueba.Smmlv(desde: new DateOnly(2027, 1, 1)))).IsSuccess.Should().BeTrue();

        (await e.Db.PlatformNationalValues.OrderBy(v => v.Sequence).Select(v => v.Sequence).ToListAsync()).Should().Equal(1L, 2L);
    }

    [Fact]
    public async Task La_fila_registrada_lleva_al_maestro_y_el_instante_del_registro()
    {
        using var e = new RegistroCentralDePrueba();

        var id = (await e.RegistrarAsync(RegistroCentralDePrueba.Smmlv())).Value;

        var fila = await e.Db.PlatformNationalValues.SingleAsync(v => v.PublicId == id);
        fila.RegisteredByCentralUserId.Should().Be(RegistroCentralDePrueba.MaestroId);
        fila.RegisteredBy.Should().Be(RegistroCentralDePrueba.MaestroCorreo);
        fila.RegisteredAt.Should().Be(RegistroCentralDePrueba.Ahora);
        fila.Key.Should().BeEmpty("SMMLV no lleva clave");
        fila.Unit.Should().Be("Pesos");
    }

    [Fact]
    public async Task Sin_ser_maestro_no_se_registra()
    {
        using var e = new RegistroCentralDePrueba();
        e.Maestro.IsGlobalMasterAdmin.Returns(false);

        var r = await e.RegistrarAsync(RegistroCentralDePrueba.Smmlv());

        r.Error.Code.Should().Be("Saas.MasterOnly");
        (await e.Db.PlatformNationalValues.AnyAsync()).Should().BeFalse();
    }

    // ---------------------------------------------------------------------------------- duplicado y corrección --

    [Fact]
    public async Task La_misma_llave_vigente_se_rechaza_con_AlreadyRegistered_porque_va_como_correccion()
    {
        using var e = new RegistroCentralDePrueba();
        await e.RegistrarAsync(RegistroCentralDePrueba.Smmlv());

        var r = await e.RegistrarAsync(RegistroCentralDePrueba.Smmlv(1_800_000m));

        r.Error.Code.Should().Be(ErroresDeValoresNacionales.AlreadyRegisteredCode);
        r.Error.Message.Should().Contain("SMMLV").And.Contain("2026-01-01").And.Contain("corrección");
    }

    [Fact]
    public void Una_correccion_sin_motivo_no_pasa_la_validacion()
    {
        var v = new CorrectNationalValueCommandValidator();

        v.Validate(new CorrectNationalValueCommand(Guid.NewGuid(), 1m, null, null, "Decreto", "")).Errors
            .Should().ContainSingle(x => x.PropertyName == nameof(CorrectNationalValueCommand.Reason));
        v.Validate(new CorrectNationalValueCommand(Guid.NewGuid(), 1m, null, null, "Decreto", "El decreto corrigió el valor")).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task La_correccion_de_una_correccion_reemplaza_a_la_correccion_y_una_fila_se_reemplaza_una_sola_vez()
    {
        using var e = new RegistroCentralDePrueba();
        var original = (await e.RegistrarAsync(RegistroCentralDePrueba.Smmlv(1_700_000m))).Value;

        var primera = await e.CorregirAsync(new CorrectNationalValueCommand(original, 1_750_000m, null, null, "Decreto 1469 de 2025", "Error de digitación"));
        primera.IsSuccess.Should().BeTrue();
        var segunda = await e.CorregirAsync(new CorrectNationalValueCommand(primera.Value, 1_750_905m, null, null, "Decreto 1469 de 2025", "El valor exacto"));
        segunda.IsSuccess.Should().BeTrue();

        var otraVez = await e.CorregirAsync(new CorrectNationalValueCommand(original, 1_800_000m, null, null, "Decreto", "Otra"));
        otraVez.Error.Code.Should().Be(ErroresDeValoresNacionales.AlreadyCorrectedCode, "el original ya lo reemplazó la primera corrección");

        var filas = await e.Db.PlatformNationalValues.ToListAsync();
        filas.Should().HaveCount(3);
        filas.Single(f => f.PublicId == primera.Value).SupersedesPublicId.Should().Be(original);
        filas.Single(f => f.PublicId == segunda.Value).SupersedesPublicId.Should().Be(primera.Value);
        filas.Single(f => f.PublicId == segunda.Value).Reason.Should().Be("El valor exacto");
        filas.Where(f => f.SupersedesPublicId is not null).Select(f => f.SupersedesPublicId).Should().OnlyHaveUniqueItems();

        var vigente = VigenciaBitemporal.Vigente(filas, new DateOnly(2026, 6, 1), DateTime.MaxValue);
        vigente!.Value.Should().Be(1_750_905m, "manda la última corrección");
    }

    [Fact]
    public async Task Corregir_un_valor_que_no_existe_responde_NotFound()
    {
        using var e = new RegistroCentralDePrueba();

        var r = await e.CorregirAsync(new CorrectNationalValueCommand(Guid.NewGuid(), 1m, null, null, "Decreto", "Motivo"));

        r.Error.Code.Should().Be(ErroresDeValoresNacionales.NotFoundCode);
    }

    // ------------------------------------------------------------------------------------- reglas del código --

    [Fact]
    public async Task Un_festivo_rige_solo_su_fecha_ValidFrom_igual_a_ValidTo_igual_a_la_clave()
    {
        using var e = new RegistroCentralDePrueba();

        var bien = await e.RegistrarAsync(new RegisterNationalValueCommand(CodigosDeValorNacional.Festivo, "2026-12-08", new DateOnly(2026, 12, 8), null, null,
            null, null, "Ley 51 de 1983", "Inmaculada Concepción"));
        var mal = await e.RegistrarAsync(new RegisterNationalValueCommand(CodigosDeValorNacional.Festivo, "2026-12-25", new DateOnly(2026, 12, 1), null, null,
            null, null, "Ley 51 de 1983"));

        bien.IsSuccess.Should().BeTrue();
        var fila = await e.Db.PlatformNationalValues.SingleAsync();
        (fila.ValidFrom, fila.ValidTo, fila.Value, fila.Unit, fila.Label).Should()
            .Be((new DateOnly(2026, 12, 8), (DateOnly?)new DateOnly(2026, 12, 8), (decimal?)null, (string?)null, "Inmaculada Concepción"));
        mal.Error.Code.Should().Be(ErroresDeValoresNacionales.KeyInvalidCode);
    }

    [Fact]
    public async Task Codigo_desconocido_clave_invalida_y_codigo_no_disponible_tienen_su_error()
    {
        using var e = new RegistroCentralDePrueba();
        await e.CatalogoDeModalidadesAsync();

        (await e.RegistrarAsync(new RegisterNationalValueCommand("DTF", null, Enero2026, null, null, 0.1m, "EA", "BanRep")))
            .Error.Code.Should().Be(ErroresDeValoresNacionales.CodeUnknownCode);
        (await e.RegistrarAsync(new RegisterNationalValueCommand(CodigosDeValorNacional.Uvr, null, Enero2026, null, null, 400m, "Pesos", "BanRep")))
            .Error.Code.Should().Be(ErroresDeValoresNacionales.CodeNotAvailableCode, "UVR llega con K6b");
        (await e.RegistrarAsync(new RegisterNationalValueCommand(CodigosDeValorNacional.TopeTasaVivienda, null, Enero2026, null, null, 0.1m, "EA", "BanRep")))
            .Error.Code.Should().Be(ErroresDeValoresNacionales.CodeNotAvailableCode);

        var usuraMala = await e.RegistrarAsync(new RegisterNationalValueCommand(CodigosDeValorNacional.Usura, "TARJETAS", new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 30), null, 0.265m, "EA", "Superfinanciera"));
        usuraMala.Error.Code.Should().Be(ErroresDeValoresNacionales.KeyInvalidCode, "la clave de USURA es una modalidad certificada del catálogo vigente");
        var datos = ((ErrorConDatos)usuraMala.Error).Data;
        datos.GetType().GetProperty("allowedKeys")!.GetValue(datos).Should().BeAssignableTo<IEnumerable<string>>()
            .Which.Should().Contain("CONSUMO_Y_ORDINARIO");

        (await e.RegistrarAsync(new RegisterNationalValueCommand(CodigosDeValorNacional.Usura, "CONSUMO_Y_ORDINARIO", new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 30), null, 0.265m, "EA", "Superfinanciera"))).IsSuccess.Should().BeTrue();

        (await e.RegistrarAsync(new RegisterNationalValueCommand(CodigosDeValorNacional.Limite, "AVISO_PREVIO_DIAS", Enero2026, null, null, 20m, "Horas", "Ley 2157")))
            .Error.Code.Should().Be(ErroresDeValoresNacionales.UnitInvalidCode, "la unidad del límite la fija el catálogo");
    }

    [Fact]
    public async Task Sin_catalogo_de_modalidades_vigente_la_usura_no_tiene_clave_valida()
    {
        using var e = new RegistroCentralDePrueba();

        var r = await e.RegistrarAsync(new RegisterNationalValueCommand(CodigosDeValorNacional.Usura, "CONSUMO_Y_ORDINARIO", new DateOnly(2026, 11, 1),
            null, null, 0.265m, "EA", "Superfinanciera"));

        r.Error.Code.Should().Be(ErroresDeValoresNacionales.KeyInvalidCode);
        r.Error.Message.Should().Contain("catálogo de modalidades");
    }

    // ------------------------------------------------------------------------------------------- documentos --

    [Fact]
    public async Task Un_documento_invalido_responde_Document_Invalid_con_la_ruta_de_cada_error()
    {
        using var e = new RegistroCentralDePrueba();

        var r = await e.RegistrarDocumentoAsync(new RegisterRegulatoryDocumentCommand(nameof(RegulatoryDocumentKind.CatalogoDeModalidades),
            "MALO", Enero2026, null, 1, """{ "modalities": [ { "name": "Sin código" } ] }""", false, "Prueba"));

        r.Error.Code.Should().Be(ErroresDeValoresNacionales.DocumentInvalidCode);
        var datos = ((ErrorConDatos)r.Error).Data;
        var errores = (System.Collections.IEnumerable)datos.GetType().GetProperty("errors")!.GetValue(datos)!;
        errores.Cast<object>().Should().NotBeEmpty().And.AllSatisfy(x =>
        {
            x.GetType().GetProperty("path")!.GetValue(x).Should().BeOfType<string>().Which.Should().StartWith("$");
            x.GetType().GetProperty("message")!.GetValue(x).Should().BeOfType<string>().Which.Should().NotBeEmpty();
        });
        (await e.Db.PlatformRegulatoryDocuments.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task El_Anexo_2_no_se_admite_antes_de_K6a_ni_una_clase_sin_validador()
    {
        using var e = new RegistroCentralDePrueba();

        (await e.RegistrarDocumentoAsync(new RegisterRegulatoryDocumentCommand(nameof(RegulatoryDocumentKind.SarcAnexo2), "ANEXO2", Enero2026, null, 1, "{}", false, "CBCF")))
            .Error.Code.Should().Be(ErroresDeValoresNacionales.DocumentKindNotAvailableCode);
        (await e.RegistrarDocumentoAsync(new RegisterRegulatoryDocumentCommand(nameof(RegulatoryDocumentKind.SarcAnexo1), "ANEXO1", Enero2026, null, 1, "{}", false, "CBCF")))
            .Error.Code.Should().Be(ErroresDeValoresNacionales.DocumentInvalidCode, "desde la fase 3 Cartera registra el validador del Anexo 1 (T0204)");
        var sinCartera = new RegisterRegulatoryDocumentCommandHandler(e.Db, e.Maestro, e.Reloj,
            [new ValidadorDeCatalogoDeModalidades(), new ValidadorDeTiposDeOrganizacion()]);
        (await sinCartera.Handle(new RegisterRegulatoryDocumentCommand(nameof(RegulatoryDocumentKind.SarcAnexo1), "ANEXO1", Enero2026, null, 1, "{}", false, "CBCF"), CancellationToken.None))
            .Error.Code.Should().Be(ErroresDeValoresNacionales.DocumentKindNotAvailableCode, "una clase sin validador registrado no se admite");
    }

    [Fact]
    public async Task La_huella_se_recalcula_sobre_el_contenido_canonico()
    {
        using var e = new RegistroCentralDePrueba();
        var contenido = PlatformNationalValuesSeeder.Contenido("tipos-de-organizacion.json");

        var id = (await e.RegistrarDocumentoAsync(new RegisterRegulatoryDocumentCommand(nameof(RegulatoryDocumentKind.TiposDeOrganizacion), "TIPOS-1",
            Enero2026, null, 1, contenido, false, "Propuesta E4"))).Value;

        var fila = await e.Db.PlatformRegulatoryDocuments.SingleAsync(d => d.PublicId == id);
        var canonico = ContenidoCanonico.Canonizar(contenido);
        fila.Content.Should().Be(canonico, "se guarda la forma canónica, sin comentarios ni espacios");
        fila.ContentSha256.Should().Be(ContenidoCanonico.Sha256(canonico)).And.HaveLength(64).And.MatchRegex("^[0-9a-f]{64}$");
        ContenidoCanonico.Sha256(ContenidoCanonico.Canonizar(contenido.Replace("\n", "\n\n  "))).Should().Be(fila.ContentSha256,
            "dos contenidos que sólo difieren en espacios dan la misma huella");

        (await e.RegistrarDocumentoAsync(new RegisterRegulatoryDocumentCommand(nameof(RegulatoryDocumentKind.TiposDeOrganizacion), "TIPOS-1",
            Enero2026, null, 1, contenido, false, "Propuesta E4"))).Error.Code.Should().Be(ErroresDeValoresNacionales.DocumentAlreadyRegisteredCode);
    }

    // ---------------------------------------------------------------------------------------- inmutabilidad --

    [Fact]
    public async Task AdminDbContext_rechaza_modificar_o_borrar_una_fila_del_registro_central()
    {
        var opciones = new DbContextOptionsBuilder<AdminDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        await using var db = new AdminDbContext(opciones);
        var fila = new PlatformNationalValue { Id = 1, Code = "SMMLV", Key = "", ValidFrom = Enero2026, Value = 1m, Source = "x", Sequence = 1, RegisteredBy = "m" };
        db.Attach(fila);
        db.Entry(fila).State.Should().Be(EntityState.Unchanged, "una fila ya guardada");

        db.Entry(fila).Property(nameof(PlatformNationalValue.Source)).CurrentValue = "otra";
        var modificar = async () => await db.SaveChangesAsync();
        (await modificar.Should().ThrowAsync<ImmutableEntityModifiedException>()).Which.Property.Should().Be(nameof(PlatformNationalValue.Source));

        db.Entry(fila).State = EntityState.Deleted;
        await ((Func<Task>)(async () => await db.SaveChangesAsync())).Should().ThrowAsync<ImmutableEntityModifiedException>();
    }

    [Fact]
    public void Ninguna_entidad_ADM_anterior_es_un_hecho_por_accidente()
    {
        var hechos = typeof(PlatformNationalValue).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(PlatformNationalValue).Namespace && typeof(IHechoInmutable).IsAssignableFrom(t))
            .Select(t => t.Name)
            .ToList();

        hechos.Should().BeEquivalentTo([nameof(PlatformNationalValue), nameof(PlatformRegulatoryDocument)],
            "C68: la guarda de AdminDbContext sólo debe morder a los hechos del registro central");
    }

    // ----------------------------------------------------------------------------------------- semilla central --

    [Fact]
    public async Task La_semilla_central_siembra_documentos_valores_limites_y_festivos_una_sola_vez()
    {
        using var e = new RegistroCentralDePrueba();

        var primera = await PlatformNationalValuesSeeder.AplicarAsync(e.Db, e.Validadores, RegistroCentralDePrueba.Ahora, NullLogger.Instance, CancellationToken.None);
        var segunda = await PlatformNationalValuesSeeder.AplicarAsync(e.Db, e.Validadores, RegistroCentralDePrueba.Ahora, NullLogger.Instance, CancellationToken.None);

        segunda.Should().Be(0, "idempotente por (código, clave, desde) y por código de documento; nunca actualiza");
        var valores = await e.Db.PlatformNationalValues.ToListAsync();
        var documentos = await e.Db.PlatformRegulatoryDocuments.ToListAsync();
        primera.Should().Be(valores.Count + documentos.Count);

        documentos.Select(d => d.Kind).Should().BeEquivalentTo([nameof(RegulatoryDocumentKind.CatalogoDeModalidades), nameof(RegulatoryDocumentKind.TiposDeOrganizacion),
            // Feature 013, fase 3 (T0204): el Anexo 1 y la regla de pérdida esperada, sin verificar (E8).
            nameof(RegulatoryDocumentKind.SarcAnexo1), nameof(RegulatoryDocumentKind.ReglaDePerdidaEsperada)]);
        documentos.Should().OnlyContain(d => !d.Verified, "versión inicial pendiente de cotejo (E4, E8, E10)");
        valores.Single(v => v.Code == CodigosDeValorNacional.Smmlv).Value.Should().Be(1_750_905m);
        valores.Single(v => v.Code == CodigosDeValorNacional.Uvt).Value.Should().Be(52_374m);
        valores.Where(v => v.Code == CodigosDeValorNacional.Limite).Select(v => v.Key).Should()
            .BeEquivalentTo(CatalogoDeLimitesLegales.Todos.Select(l => l.Codigo).Where(c => c != CatalogoDeLimitesLegales.LibranzaUmbralMoraPagaduria),
                "todos los límites con valor semilla; el umbral de mora de pagaduría se coteja en la CBCF antes de K2");

        // Los festivos que sembraba Nómina (HolidaysSeeder, retirado en la feature 013): 18 por año, 2026–2028.
        var festivos = valores.Where(v => v.Code == CodigosDeValorNacional.Festivo).ToList();
        festivos.Should().HaveCount(3 * 18);
        festivos.Should().OnlyContain(f => f.ValidTo == f.ValidFrom && f.Key == f.ValidFrom.ToString("yyyy-MM-dd") && f.Value == null);
        festivos.Should().Contain(f => f.ValidFrom == new DateOnly(2026, 1, 12), "Reyes cae martes 6 y se corre al lunes 12");
        festivos.Should().Contain(f => f.ValidFrom == new DateOnly(2026, 4, 3), "Viernes Santo");
        festivos.Should().Contain(f => f.ValidFrom == new DateOnly(2026, 11, 2), "Todos los Santos cae domingo y se corre al lunes 2");

        valores.Concat<object>(documentos).Should().NotBeEmpty();
        var secuencias = valores.Select(v => v.Sequence).Concat(documentos.Select(d => d.Sequence)).OrderBy(s => s).ToList();
        secuencias.Should().Equal(Enumerable.Range(1, secuencias.Count).Select(i => (long)i), "numera con el contador común, sin huecos");
        documentos.Max(d => d.Sequence).Should().BeLessThan(valores.Min(v => v.Sequence), "el catálogo de modalidades va antes que lo que lo cita");
    }
}
