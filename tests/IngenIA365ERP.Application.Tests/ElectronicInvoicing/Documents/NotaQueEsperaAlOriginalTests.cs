using FluentAssertions;
using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Application.ElectronicInvoicing.Documents;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.ElectronicInvoicing.Documents;

/// <summary>
/// Feature 012, I4, T738 (FR-066; contracts/dian.md §4.1): la nota sobre un original en contingencia se registra sin el código único del
/// original y espera a que se valide; entonces nace de nuevo (versión <c>ReferenceCompleted</c>) con la referencia completa y la misma huella
/// económica, y se transmite esa. Sin eso la reconstrucción no coincidiría con lo registrado y la nota nunca saldría.
/// </summary>
public class NotaQueEsperaAlOriginalTests
{
    private static EntradaDeDocumentoElectronico Nota(string numeroDelOriginal) => Entradas.Factura() with
    {
        DocumentPublicId = Guid.NewGuid(),
        DocumentClass = "CreditNote",
        DocumentTypeCode = "NC",
        DocumentNumber = "NC1",
        Kind = ElectronicDocumentKind.CreditNote,
        Correccion = new CorreccionDeEntrada(Guid.NewGuid(), numeroDelOriginal, Entradas.Fecha, "2"),
    };

    [Fact]
    public async Task Validado_el_original_la_nota_nace_de_nuevo_con_su_codigo_y_se_transmite()
    {
        var s = new EscenarioDeEmision();
        var original = await s.FacturaAsync();
        original.Status = ElectronicDocumentStatus.DianContingency;
        await s.E.Db.SaveChangesAsync();

        var entrada = Nota(original.Number);
        var sinCodigo = ConstructorDelCanonico.Construir(entrada, new ContextoDelCanonico { Configuracion = s.Configuracion, Prefijo = "NC", Consecutivo = 1 });
        sinCodigo.IsSuccess.Should().BeTrue(sinCodigo.IsFailure ? sinCodigo.Error.Message : null);
        var registro = await s.Registro().RegistrarAsync(new PedidoDeRegistroElectronico("INV", entrada.DocumentPublicId, "NC", s.Configuracion,
            sinCodigo.Value, null, null, Corrige: original, EsperaA: original), default);
        await s.E.Db.SaveChangesAsync();
        var nota = registro.Value;

        // Mientras el original no está validado, la nota no se transmite.
        var espera = await s.Emitir().Handle(new EmitElectronicDocumentCommand(nota.PublicId, RetryNow: true), default);
        espera.IsSuccess.Should().BeTrue();
        s.Canal.Llamadas.Should().BeEmpty();

        // El original se valida con su CUFE; la reconstrucción de la nota ya lo lleva.
        original.Status = ElectronicDocumentStatus.Validated;
        original.UniqueCode = new string('f', 96);
        await s.E.Db.SaveChangesAsync();
        var conCodigo = ConstructorDelCanonico.Construir(entrada, new ContextoDelCanonico
        {
            Configuracion = s.Configuracion, Prefijo = "NC", Consecutivo = 1,
            Corregido = new DocumentoCorregidoCanonico(original.Number, original.UniqueCode, original.IssueDate, null),
        });
        conCodigo.Value.CanonicalSha256.Should().NotBe(sinCodigo.Value.CanonicalSha256);
        conCodigo.Value.EconomicFingerprint.Should().Be(sinCodigo.Value.EconomicFingerprint);
        s.Reconstruccion.PorNumero[nota.Number] = conCodigo.Value;
        s.Canal.Emisiones.Enqueue(CanalGuionado.Respuesta(ChannelOutcome.Validated));
        s.Adelantar();

        var r = await s.Emitir().Handle(new EmitElectronicDocumentCommand(nota.PublicId, RetryNow: true), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        var versiones = await s.E.Db.ElectronicDocumentVersions.Where(v => v.ElectronicDocumentId == nota.Id).OrderBy(v => v.VersionNumber).ToListAsync();
        versiones.Should().HaveCount(2);
        versiones[1].Reason.Should().Be(DocumentVersionReason.ReferenceCompleted);
        versiones[1].CanonicalSha256.Should().Be(conCodigo.Value.CanonicalSha256);
        s.Canal.Llamadas.Should().ContainSingle(l => l.Operacion == "Emitir").Which.Clave.Should().EndWith(":v2");
        (await s.E.Db.ElectronicDocuments.SingleAsync(d => d.Id == nota.Id)).Status.Should().Be(ElectronicDocumentStatus.Validated);
    }
}
