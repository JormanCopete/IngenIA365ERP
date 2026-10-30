using FluentAssertions;
using IngenIA365ERP.Application.Receipts.Agreements;
using IngenIA365ERP.Application.Tests.Receipts;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Convenios;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Receipts.Agreements;

/// <summary>
/// Feature 015, R2, T0429 (FR-088 a FR-092, FR-197): la referencia de pago estable con su dígito de verificación (módulo 10 y módulo 11, determinista); la estructura
/// del convenio que la lee; el archivo del convenio con su huella (no se carga dos veces), su revisión previa, la identificación por referencia y la aplicación al cargar a
/// nombre de quien carga con un recibo por pago (canal <c>Agreement</c>); la referencia con dígito inválido o desconocida va a partidas; un pago ya cargado no se aplica
/// dos veces. Sin botón de pago en línea (C2 = A). Prueba escrita, sin correr. (nuevo)
/// </summary>
public class ConveniosDeRecaudoTests
{
    [Theory]
    [InlineData("7992739871", CheckDigitAlgorithm.Modulo10, "3")]
    [InlineData("100234", CheckDigitAlgorithm.Modulo10, "4")]
    [InlineData("12345", CheckDigitAlgorithm.Modulo11, "5")]
    public void El_digito_de_verificacion_es_determinista(string baseDeLaReferencia, CheckDigitAlgorithm algoritmo, string esperado)
    {
        DigitoDeVerificacion.Calcular(baseDeLaReferencia, algoritmo).Should().Be(esperado);
        DigitoDeVerificacion.Calcular(baseDeLaReferencia, algoritmo).Should().Be(DigitoDeVerificacion.Calcular(baseDeLaReferencia, algoritmo));
        DigitoDeVerificacion.EsValida(baseDeLaReferencia + esperado, algoritmo).Should().BeTrue();
    }

    [Fact]
    public void La_estructura_rechaza_el_digito_invalido_y_la_longitud_que_no_cabe()
    {
        var estructura = new EstructuraDeReferencia(8, CheckDigitAlgorithm.Modulo10);
        var buena = DigitoDeVerificacion.Componer("100234", CheckDigitAlgorithm.Modulo10, 8);

        estructura.Leer(buena).Valida.Should().BeTrue();
        estructura.Leer(buena[..^1] + (buena[^1] == '0' ? '1' : '0')).Motivo.Should().Be(MotivoDeReferenciaInvalida.DigitoInvalido);
        estructura.Leer("123456789012").Motivo.Should().Be(MotivoDeReferenciaInvalida.LongitudInvalida);
        estructura.Leer(" ").Motivo.Should().Be(MotivoDeReferenciaInvalida.Vacia);
    }

    [Fact]
    public void Una_base_alfanumerica_tambien_lleva_digito()
    {
        var referencia = DigitoDeVerificacion.Componer("CR-1002", CheckDigitAlgorithm.Modulo11);

        referencia.Should().StartWith("CR1002");
        DigitoDeVerificacion.EsValida(referencia, CheckDigitAlgorithm.Modulo11).Should().BeTrue();
    }

    [Fact]
    public async Task La_revision_previa_dice_que_se_aplica_y_que_va_a_partidas_sin_guardar()
    {
        var p = new ConveniosDePrueba();
        var hoy = EscenarioDeRecaudo.Hoy;
        var referenciaA = ConveniosDePrueba.Referencia(ConveniosDePrueba.NumeroA);
        var invalida = referenciaA[..^1] + (referenciaA[^1] == '9' ? '0' : (char)(referenciaA[^1] + 1));
        var archivo = ConveniosDePrueba.Archivo($"{hoy:yyyy-MM-dd};100000.00;{referenciaA};S1;;", $"{hoy:yyyy-MM-dd};50000.00;{invalida};S2;;");

        var r = await p.Cargar().Handle(new LoadCollectionFileCommand(p.Convenio.PublicId, "CONVENIO.csv", archivo, Preview: true) { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        r.Value.ToApply.Should().Be(1);
        r.Value.ToUnidentified.Should().Be(1);
        r.Value.Review.Single(l => l.Line == 2).Code.Should().Be(MotivosDeLineaDeConvenio.DigitoInvalido);
        (await p.B.K.C.E.Db.AgreementFiles.CountAsync()).Should().Be(0, "revisar no guarda");
    }

    [Fact]
    public async Task Cargar_aplica_cada_pago_con_su_recibo_y_lo_desconocido_va_a_partidas()
    {
        var p = new ConveniosDePrueba();
        var hoy = EscenarioDeRecaudo.Hoy;
        var archivo = ConveniosDePrueba.Archivo(
            $"{hoy:yyyy-MM-dd};100000.00;{ConveniosDePrueba.Referencia(ConveniosDePrueba.NumeroA)};S1;;",
            $"{hoy:yyyy-MM-dd};80000.00;{ConveniosDePrueba.Referencia(ConveniosDePrueba.NumeroB)};S2;;",
            $"{hoy:yyyy-MM-dd};30000.00;{ConveniosDePrueba.Referencia("999999")};S3;;");

        var r = await p.Cargar().Handle(new LoadCollectionFileCommand(p.Convenio.PublicId, "CONVENIO.csv", archivo) { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        r.Value.File!.Applied.Should().Be(2);
        r.Value.File.Unidentified.Should().Be(1);
        r.Value.File.Status.Should().Be(CollectionFileState.AppliedWithExceptions);
        (await p.B.K.C.E.Db.IssuedReceipts.CountAsync(x => x.Channel == ReceiptChannel.Agreement)).Should().Be(2, "un recibo por pago");
        (await p.B.K.C.E.Db.UnidentifiedItems.SingleAsync()).OriginKind.Should().Be(UnidentifiedItemOrigin.AgreementFile);
        (await p.B.K.C.E.Db.PaymentReferences.CountAsync()).Should().Be(2, "las referencias encontradas quedan registradas");
    }

    [Fact]
    public async Task El_mismo_archivo_no_se_carga_dos_veces_y_un_pago_ya_cargado_no_se_aplica_otra_vez()
    {
        var p = new ConveniosDePrueba();
        var hoy = EscenarioDeRecaudo.Hoy;
        var linea = $"{hoy:yyyy-MM-dd};100000.00;{ConveniosDePrueba.Referencia(ConveniosDePrueba.NumeroA)};S1;;";
        (await p.Cargar().Handle(new LoadCollectionFileCommand(p.Convenio.PublicId, "A.csv", ConveniosDePrueba.Archivo(linea)) { OperationKey = Guid.NewGuid() }, default))
            .IsSuccess.Should().BeTrue();

        var otraVez = await p.Cargar().Handle(new LoadCollectionFileCommand(p.Convenio.PublicId, "A.csv", ConveniosDePrueba.Archivo(linea)) { OperationKey = Guid.NewGuid() },
            default);
        var conElMismoPago = await p.Cargar().Handle(new LoadCollectionFileCommand(p.Convenio.PublicId, "B.csv", ConveniosDePrueba.Archivo(linea, ""))
            { OperationKey = Guid.NewGuid() }, default);

        otraVez.Error.Code.Should().Be("Receipts.AgreementFile.AlreadyLoaded");
        conElMismoPago.IsSuccess.Should().BeTrue(conElMismoPago.IsFailure ? conElMismoPago.Error.Code : null);
        conElMismoPago.Value.Omitted.Should().Be(1);
        (await p.B.K.C.E.Db.IssuedReceipts.CountAsync(x => x.Channel == ReceiptChannel.Agreement)).Should().Be(1);
    }

    [Fact]
    public async Task Los_totales_que_informa_el_aliado_tienen_que_cuadrar()
    {
        var p = new ConveniosDePrueba();
        var hoy = EscenarioDeRecaudo.Hoy;
        var archivo = ConveniosDePrueba.Archivo($"{hoy:yyyy-MM-dd};100000.00;{ConveniosDePrueba.Referencia(ConveniosDePrueba.NumeroA)};S1;;");

        var r = await p.Cargar().Handle(new LoadCollectionFileCommand(p.Convenio.PublicId, "C.csv", archivo, DeclaredLines: 1, DeclaredTotal: 90_000m)
            { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be("Receipts.AgreementFile.TotalsMismatch");
    }
}
