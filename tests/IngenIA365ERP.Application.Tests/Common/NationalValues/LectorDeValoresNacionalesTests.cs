using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Calendar;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Saas.NationalValues;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Core.NationalValues;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.NationalValues;

/// <summary>
/// T0025 (feature 013, T40, R5): los lectores únicos de la copia de la cooperativa. <see cref="IValoresNacionales"/> aplica la regla
/// bitemporal con <c>KnownAt</c> como R, falla con <c>NationalValues.Missing</c> y alerta el faltante, alerta el índice viejo y lee
/// una serie de un rango; <see cref="INormativaVigente"/> entrega el documento con su <c>PublicId</c> y su huella y lo memoriza;
/// <see cref="ICalendarioHabil"/> usa sólo los <c>FESTIVO</c> replicados; y el estado de revisión es derivado.
/// </summary>
public class LectorDeValoresNacionalesTests
{
    private static readonly DateTime PrimeraReplica = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SegundaReplica = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IAlertas _alertas = Substitute.For<IAlertas>();
    private readonly List<AlertaALevantar> _levantadas = [];

    public LectorDeValoresNacionalesTests()
    {
        _alertas.LevantarAsync(Arg.Do<AlertaALevantar>(_levantadas.Add), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AlertaLevantada(Guid.NewGuid(), DesenlaceDeAlerta.Levantada, 1, false)));
    }

    private LectorDeValoresNacionales Lector() => new(_db, _alertas);

    [Fact]
    public async Task Lee_con_la_regla_bitemporal_y_KnownAt_como_R()
    {
        var original = CopiasDePrueba.Copia(CodigosDeValorNacional.Usura, "CONSUMO_Y_ORDINARIO", new DateOnly(2026, 9, 1), 0.2650m, PrimeraReplica,
            hasta: new DateOnly(2026, 9, 30), unidad: "EA");
        await CopiasDePrueba.SembrarAsync(_db, PrimeraReplica, original);
        var correccion = CopiasDePrueba.Copia(CodigosDeValorNacional.Usura, "CONSUMO_Y_ORDINARIO", new DateOnly(2026, 9, 1), 0.2600m, SegundaReplica,
            hasta: new DateOnly(2026, 9, 30), corrige: original.SourcePublicId, unidad: "EA");
        await CopiasDePrueba.SembrarAsync(_db, SegundaReplica, correccion);

        var hoy = await Lector().LeerAsync(CodigosDeValorNacional.Usura, "CONSUMO_Y_ORDINARIO", new DateOnly(2026, 9, 15));
        var antes = await Lector().LeerAsync(CodigosDeValorNacional.Usura, "CONSUMO_Y_ORDINARIO", new DateOnly(2026, 9, 15), informacionHasta: PrimeraReplica.AddDays(10));
        var antesDeTodo = await Lector().LeerAsync(CodigosDeValorNacional.Usura, "CONSUMO_Y_ORDINARIO", new DateOnly(2026, 9, 15), informacionHasta: PrimeraReplica.AddMinutes(-1));

        hoy.Value.Valor.Should().Be(0.2600m, "la corrección reemplaza a la original");
        hoy.Value.PublicId.Should().Be(correccion.PublicId);
        hoy.Value.SourcePublicId.Should().Be(correccion.SourcePublicId);
        antes.Value.Valor.Should().Be(0.2650m, "una liquidación anterior a la segunda réplica no pudo conocer la corrección");
        antesDeTodo.Error.Code.Should().Be(ErroresDeValoresNacionales.MissingCode, "antes de la primera réplica no se sabía nada");
    }

    [Fact]
    public async Task Un_faltante_falla_con_Missing_y_alerta_Cartera_ValorNacionalFaltante()
    {
        var r = await Lector().LeerAsync(CodigosDeValorNacional.Usura, "MICROCREDITO", new DateOnly(2026, 11, 3));

        r.Error.Code.Should().Be(ErroresDeValoresNacionales.MissingCode);
        r.Error.Message.Should().Be("Falta USURA MICROCREDITO para el 2026-11-03. Pida al administrador de la plataforma registrarlo.");
        var alerta = _levantadas.Should().ContainSingle().Which;
        alerta.TypeCode.Should().Be(AlertasDeValoresNacionales.ValorNacionalFaltante);
        alerta.DedupKey.Should().Be("Cartera.ValorNacionalFaltante:USURA:MICROCREDITO:2026-11-03");
    }

    [Fact]
    public async Task Dentro_de_una_operacion_idempotente_el_faltante_se_encola_para_levantarse_al_terminar()
    {
        // Feature 013, US1-3 (T0344): la alerta guardada dentro de la transacción del comando se revertía con él.
        var diferidas = new AlertasDiferidas();
        diferidas.Abrir();

        var r = await new LectorDeValoresNacionales(_db, _alertas, diferidas: diferidas)
            .LeerAsync(CodigosDeValorNacional.Usura, "MICROCREDITO", new DateOnly(2026, 12, 1));

        r.Error.Code.Should().Be(ErroresDeValoresNacionales.MissingCode);
        _levantadas.Should().BeEmpty("no se guarda en la transacción de la operación");
        diferidas.Cerrar();
        diferidas.Tomar().Should().ContainSingle().Which.TypeCode.Should().Be(AlertasDeValoresNacionales.ValorNacionalFaltante);

        // Fuera de una operación (la cola inactiva) se levanta en el acto, como antes.
        await new LectorDeValoresNacionales(_db, _alertas, diferidas: diferidas)
            .LeerAsync(CodigosDeValorNacional.Usura, "MICROCREDITO", new DateOnly(2026, 12, 2));
        _levantadas.Should().ContainSingle();
    }

    [Fact]
    public async Task El_ultimo_indice_publicado_se_devuelve_y_si_es_viejo_alerta_IndiceDesactualizado()
    {
        await CopiasDePrueba.SembrarAsync(_db, PrimeraReplica,
            CopiasDePrueba.Copia(CodigosDeValorNacional.Ibr, "3M", new DateOnly(2026, 9, 1), 0.0890m, PrimeraReplica, unidad: "NominalMV"),
            CopiasDePrueba.Copia(CodigosDeValorNacional.Ibr, "3M", new DateOnly(2026, 9, 2), 0.0891m, PrimeraReplica, unidad: "NominalMV"));

        var alDia = await Lector().LeerUltimoPublicadoAsync(CodigosDeValorNacional.Ibr, "3M", new DateOnly(2026, 9, 4));
        _levantadas.Should().BeEmpty("dos días es normal");
        var viejo = await Lector().LeerUltimoPublicadoAsync(CodigosDeValorNacional.Ibr, "3M", new DateOnly(2026, 9, 20));

        (alDia.Value.Valor, alDia.Value.Desactualizado).Should().Be((0.0891m, false));
        (viejo.Value.Valor, viejo.Value.Desactualizado).Should().Be((0.0891m, true), "se devuelve igual, marcado");
        _levantadas.Should().ContainSingle(a => a.TypeCode == AlertasDeValoresNacionales.IndiceDesactualizado
                                              && a.DedupKey == "Cartera.IndiceDesactualizado:IBR:3M");
    }

    [Fact]
    public async Task La_serie_de_un_rango_trae_el_valor_de_cada_dia_y_los_huecos()
    {
        await CopiasDePrueba.SembrarAsync(_db, PrimeraReplica,
            CopiasDePrueba.Copia(CodigosDeValorNacional.Usura, "MICROCREDITO", new DateOnly(2026, 9, 1), 0.50m, PrimeraReplica, hasta: new DateOnly(2026, 9, 30)),
            CopiasDePrueba.Copia(CodigosDeValorNacional.Usura, "MICROCREDITO", new DateOnly(2026, 10, 1), 0.51m, PrimeraReplica, hasta: new DateOnly(2026, 10, 31)));

        var serie = await Lector().LeerSerieAsync(CodigosDeValorNacional.Usura, "MICROCREDITO", new DateOnly(2026, 9, 29), new DateOnly(2026, 11, 2));

        serie.Dias.Should().HaveCount(35);
        serie.En(new DateOnly(2026, 9, 30))!.Valor.Should().Be(0.50m);
        serie.En(new DateOnly(2026, 10, 1))!.Valor.Should().Be(0.51m);
        serie.Faltantes.Should().Equal(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 2));
        _levantadas.Should().BeEmpty("la serie no alerta: decide quien la recorre");
    }

    [Fact]
    public async Task El_estado_de_revision_es_derivado_de_la_revision_mas_reciente()
    {
        var copia = CopiasDePrueba.Copia(CodigosDeValorNacional.Smmlv, "", new DateOnly(2026, 1, 1), 1_750_905m, PrimeraReplica);
        await CopiasDePrueba.SembrarAsync(_db, PrimeraReplica, copia);

        (await Lector().LeerAsync(CodigosDeValorNacional.Smmlv, "", new DateOnly(2026, 6, 1))).Value.Estado.Should().Be(EstadoDeRevision.PendienteDeValidar);

        _db.NationalValueReviews.Add(Revision(copia.PublicId, NationalValueReviewDecision.Confirmed, PrimeraReplica.AddDays(1)));
        await _db.SaveChangesAsync();
        (await Lector().LeerAsync(CodigosDeValorNacional.Smmlv, "", new DateOnly(2026, 6, 1))).Value.Estado.Should().Be(EstadoDeRevision.Validada);

        _db.NationalValueReviews.Add(Revision(copia.PublicId, NationalValueReviewDecision.Objected, PrimeraReplica.AddDays(2), "No coincide con el decreto"));
        await _db.SaveChangesAsync();
        (await Lector().LeerAsync(CodigosDeValorNacional.Smmlv, "", new DateOnly(2026, 6, 1))).Value.Estado.Should().Be(EstadoDeRevision.Objetada);
    }

    [Fact]
    public async Task La_normativa_vigente_trae_PublicId_y_huella_y_se_memoriza_por_huella()
    {
        var contenido = ContenidoCanonico.Canonizar(PlatformNationalValuesSeeder.Contenido("catalogo-de-modalidades.json")
            .Replace("\"Consumo\"", $"\"Consumo {Guid.NewGuid():N}\""));
        var sha = ContenidoCanonico.Sha256(contenido);
        var replica = new NationalValueReplication { FromSequence = 1, ToSequence = 1, DocumentCount = 1, StartedAt = PrimeraReplica, FinishedAt = PrimeraReplica, ActorName = "p" };
        var documento = new RegulatoryDocument
        {
            Kind = nameof(RegulatoryDocumentKind.CatalogoDeModalidades), Code = "MODALIDADES-1", ValidFrom = new DateOnly(2026, 1, 1), Content = contenido,
            ContentSha256 = sha, SchemaVersion = 1, Source = "Propuesta", SourcePublicId = Guid.NewGuid(), SourceSequence = 1,
            CentralRegisteredAt = PrimeraReplica, KnownAt = PrimeraReplica,
        };
        replica.Documents.Add(documento);
        _db.NationalValueReplications.Add(replica);
        await _db.SaveChangesAsync();

        var lecturas = 0;
        ResultadoDeDocumento<CatalogoDeModalidades> Leer(string c, short v) { lecturas++; return CatalogoDeModalidades.Leer(c, v); }
        var lector = new LectorDeDocumentoNormativo(_db, _alertas);

        var a = await lector.LeerAsync(RegulatoryDocumentKind.CatalogoDeModalidades, new DateOnly(2026, 11, 1), Leer);
        var b = await lector.LeerAsync(RegulatoryDocumentKind.CatalogoDeModalidades, new DateOnly(2026, 12, 1), Leer);

        a.IsSuccess.Should().BeTrue(a.IsFailure ? a.Error.Message : string.Empty);
        (a.Value.PublicId, a.Value.SourcePublicId, a.Value.Sha256, a.Value.Codigo).Should().Be((documento.PublicId, documento.SourcePublicId, sha, "MODALIDADES-1"));
        a.Value.Documento.ClavesDeUsura.Should().Contain("CONSUMO_Y_ORDINARIO");
        b.Value.Documento.Should().BeSameAs(a.Value.Documento, "el contenido de una huella no cambia: se interpreta una sola vez");
        lecturas.Should().Be(1);

        var falta = await lector.TiposDeOrganizacionAsync(new DateOnly(2026, 11, 1));
        falta.Error.Code.Should().Be(ErroresDeValoresNacionales.MissingCode);
    }

    [Fact]
    public async Task El_calendario_habil_usa_solo_los_FESTIVO_replicados_nunca_los_festivos_manuales_de_Nomina()
    {
        await CopiasDePrueba.SembrarAsync(_db, PrimeraReplica,
            CopiasDePrueba.Copia(CodigosDeValorNacional.Festivo, "2026-12-08", new DateOnly(2026, 12, 8), null, PrimeraReplica, hasta: new DateOnly(2026, 12, 8)),
            CopiasDePrueba.Copia(CodigosDeValorNacional.Festivo, "2026-12-25", new DateOnly(2026, 12, 25), null, PrimeraReplica, hasta: new DateOnly(2026, 12, 25)));
        _db.Holidays.Add(new Holiday { Date = new DateOnly(2026, 12, 24), Name = "Día de la cooperativa", Origin = HolidayOrigin.Manual, Year = 2026, CreatedBy = "contadora" });
        await _db.SaveChangesAsync();

        var calendario = await new CalendarioDeDiasHabiles(_db).ObtenerAsync(new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31), DiasDeDescanso.SoloDomingo);

        calendario.EsHabil(new DateOnly(2026, 12, 8)).Should().BeFalse("Inmaculada Concepción, replicada");
        calendario.EsHabil(new DateOnly(2026, 12, 25)).Should().BeFalse();
        calendario.EsHabil(new DateOnly(2026, 12, 24)).Should().BeTrue("un festivo Manual de Nómina no es nacional (pregunta E5)");
        calendario.EsHabil(new DateOnly(2026, 12, 12)).Should().BeTrue("el sábado es hábil para el art. 829");
        calendario.SiguienteHabil(new DateOnly(2026, 12, 6)).Should().Be(new DateOnly(2026, 12, 7), "del domingo al lunes");
    }

    private static NationalValueReview Revision(Guid copia, NationalValueReviewDecision decision, DateTime cuando, string? nota = null) => new()
    {
        TargetKind = NationalValueReviewTarget.Value, TargetPublicId = copia, Decision = decision, Note = nota,
        ReviewedByUserId = 7, ReviewedByName = "Rafaela Lastra", ReviewedAt = cuando,
    };
}
