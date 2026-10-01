using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents.Numeracion;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Documents;

/// <summary>
/// Feature 012, T109 (T16, FR-038; data-model §5.9): el numerador toma la secuencia vigente del tipo a la fecha de
/// operación, la bloquea al final del cerrojo, copia su prefijo y su <c>NextValue</c> al documento y lo incrementa, todo
/// en el mismo guardado; sin secuencia vigente, <c>Inventory.Numbering.SequenceMissing</c>. Lo transaccional (el
/// bloqueo real, sin huecos ni repetidos entre dos confirmaciones) es de las e2e.
/// </summary>
public class NumeradorTests
{
    private static readonly DateOnly Hoy = new(2026, 9, 25);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly ICerrojoDeInventario _cerrojo = Substitute.For<ICerrojoDeInventario>();

    private InventoryDocumentType Tipo(string codigo = "AJP")
    {
        var tipo = new InventoryDocumentType { Code = codigo, Name = "Ajuste positivo", Class = DocumentClass.PositiveAdjustment };
        _db.InventoryDocumentTypes.Add(tipo);
        _db.SaveChanges();
        return tipo;
    }

    private DocumentSequence Secuencia(InventoryDocumentType tipo, string prefijo, long siguiente, DateOnly desde, DateOnly? hasta = null)
    {
        var s = new DocumentSequence { DocumentTypeId = tipo.Id, Prefix = prefijo, NextValue = siguiente, ValidFrom = desde, ValidTo = hasta };
        _db.DocumentSequences.Add(s);
        _db.SaveChanges();
        return s;
    }

    private static InventoryDocument Documento(InventoryDocumentType tipo, DateOnly fecha) =>
        new() { Class = tipo.Class, DocumentTypeId = tipo.Id, OperationDate = fecha };

    [Fact]
    public async Task Asigna_prefijo_y_numero_de_la_secuencia_vigente_e_incrementa_NextValue()
    {
        var tipo = Tipo();
        var secuencia = Secuencia(tipo, "AJ", 1500, new DateOnly(2026, 1, 1));
        var doc = Documento(tipo, Hoy);

        var r = await new Numerador(_db, _cerrojo).NumerarAsync(doc, tipo.Code);

        r.IsSuccess.Should().BeTrue();
        doc.Prefix.Should().Be("AJ");
        doc.Number.Should().Be(1500, "continúa la numeración con que se creó la secuencia");
        secuencia.NextValue.Should().Be(1501);
        await _cerrojo.Received(1).BloquearNumeracionAsync(secuencia.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Dos_documentos_seguidos_no_repiten_ni_saltan()
    {
        var tipo = Tipo();
        Secuencia(tipo, "", 1, new DateOnly(2026, 1, 1));
        var numerador = new Numerador(_db, _cerrojo);

        var a = Documento(tipo, Hoy);
        var b = Documento(tipo, Hoy);
        (await numerador.NumerarAsync(a, tipo.Code)).IsSuccess.Should().BeTrue();
        (await numerador.NumerarAsync(b, tipo.Code)).IsSuccess.Should().BeTrue();

        a.Number.Should().Be(1);
        b.Number.Should().Be(2);
        a.Prefix.Should().BeEmpty("el prefijo puede ir vacío");
    }

    [Fact]
    public async Task Usa_la_secuencia_vigente_a_la_fecha_de_operacion_no_la_de_hoy()
    {
        var tipo = Tipo();
        Secuencia(tipo, "A", 90, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30));
        var nueva = Secuencia(tipo, "B", 1, new DateOnly(2026, 7, 1));

        var deJunio = Documento(tipo, new DateOnly(2026, 6, 30));
        await new Numerador(_db, _cerrojo).NumerarAsync(deJunio, tipo.Code);
        deJunio.Prefix.Should().Be("A");
        deJunio.Number.Should().Be(90);

        var deJulio = Documento(tipo, new DateOnly(2026, 7, 1));
        await new Numerador(_db, _cerrojo).NumerarAsync(deJulio, tipo.Code);
        deJulio.Prefix.Should().Be("B");
        deJulio.Number.Should().Be(1);
        nueva.NextValue.Should().Be(2);
    }

    [Fact]
    public async Task No_toma_la_secuencia_de_otro_tipo()
    {
        var tipo = Tipo("AJP");
        var otro = Tipo("AJN");
        Secuencia(otro, "N", 5, new DateOnly(2026, 1, 1));

        var r = await new Numerador(_db, _cerrojo).NumerarAsync(Documento(tipo, Hoy), tipo.Code);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Inventory.Numbering.SequenceMissing");
    }

    [Fact]
    public async Task Sin_secuencia_vigente_falla_con_el_codigo_y_la_fecha_y_no_bloquea_nada()
    {
        var tipo = Tipo();
        Secuencia(tipo, "A", 1, new DateOnly(2026, 10, 1));
        var doc = Documento(tipo, Hoy);

        var r = await new Numerador(_db, _cerrojo).NumerarAsync(doc, tipo.Code);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Inventory.Numbering.SequenceMissing");
        r.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { documentTypeCode = "AJP", operationDate = Hoy });
        doc.Number.Should().BeNull();
        await _cerrojo.DidNotReceiveWithAnyArgs().BloquearNumeracionAsync(default, default);
    }

    [Fact]
    public async Task Un_documento_ya_numerado_no_se_vuelve_a_numerar()
    {
        var tipo = Tipo();
        Secuencia(tipo, "A", 1, new DateOnly(2026, 1, 1));
        var doc = Documento(tipo, Hoy);
        doc.Number = 7;

        var accion = () => new Numerador(_db, _cerrojo).NumerarAsync(doc, tipo.Code);

        await accion.Should().ThrowAsync<InvalidOperationException>();
    }
}
