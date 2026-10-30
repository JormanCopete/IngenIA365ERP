using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Receipts.BankItems;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Tareas;
using IngenIA365ERP.Application.Tests.Receipts.BankItems;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Receipts.CashDesk;

/// <summary>
/// Feature 015, US2, T0236 (FR-186; US2-9): el recibo de contingencia se digita después con su talonario, su número además del consecutivo, la fecha del talonario y la imagen
/// obligatoria, por los contratos en forma no interactiva; un número fuera del rango o ya digitado se rechaza; el informe del talonario dice lo digitado, lo que falta por
/// digitar y lo que queda sin usar, y la tarea alerta <c>Recaudo.ContingenciaPorDigitar</c>. Prueba escrita, sin correr.
/// </summary>
public class ContingenciaDeCajaTests
{
    private readonly BancosDePrueba _b = new();
    private readonly ContingencyBook _talonario;

    public ContingenciaDeCajaTests()
    {
        var e = _b.K.C.E;
        e.Db.ReceiptDocumentTypes.Add(new ReceiptDocumentType { Code = "RT", Name = "Recibo de contingencia", Kind = ReceiptDocumentKind.ContingencyReceipt });
        _talonario = new ContingencyBook { CollectionPointId = _b.K.C.Punto.Id, FromNumber = 1001, ToNumber = 1050, RegisteredBy = "test" };
        e.Db.ContingencyBooks.Add(_talonario);
        e.Db.SaveChanges();
    }

    private Guid Imagen()
    {
        var a = new Attachment
        {
            OwnerEntityType = Application.Attachments.Common.AdjuntosDeModulo.ImagenDeContingencia, OwnerEntityPublicId = Guid.NewGuid(), FileName = "recibo.jpg",
            ContentType = "image/jpeg", SizeBytes = 10, Sha256Hex = new string('c', 64), StoragePath = "x", Format = FormatoDeAdjunto.Direct,
        };
        _b.K.C.E.Db.Attachments.Add(a);
        _b.K.C.E.Db.SaveChanges();
        return a.PublicId;
    }

    private Task<IngenIA365ERP.Application.Common.Models.Result<ReceiptDto>> DigitarAsync(int numero, Guid? imagen = null) =>
        new RegisterContingencyReceiptCommandHandler(_b.K.C.E.Db, _b.K.C.E.Alcance, _b.Aplicacion, _b.K.C.E.Reloj)
            .Handle(new RegisterContingencyReceiptCommand(_talonario.PublicId, numero, _b.K.Asociado.PublicId, EscenarioDeRecaudo.Hoy.AddDays(-2), [_b.CuotaA(80_000m)],
                imagen ?? Imagen()) { OperationKey = Guid.NewGuid() }, default);

    [Fact]
    public async Task Se_digita_con_su_numero_su_fecha_y_su_imagen()
    {
        var r = await DigitarAsync(1001);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        var recibo = await _b.K.C.E.Db.IssuedReceipts.SingleAsync();
        recibo.Channel.Should().Be(ReceiptChannel.Contingency);
        recibo.IsContingency.Should().BeTrue();
        recibo.ContingencyNumber.Should().Be(1001);
        recibo.ValueDate.Should().Be(EscenarioDeRecaudo.Hoy.AddDays(-2));
        recibo.ContingencyImageAttachmentPublicId.Should().NotBeNull();
        await _b.K.Obligaciones.Received().AplicarAsync(Arg.Is<IngenIA365ERP.Application.Common.Integration.Lending.SolicitudDeAplicacion>(s =>
            s.Form == IngenIA365ERP.Application.Common.Integration.Lending.FormaDeAplicacion.NonInteractive), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fuera_del_rango_ya_digitado_o_sin_imagen_se_rechaza()
    {
        (await DigitarAsync(999)).Error.Code.Should().Be(ErroresDeRecaudo.NumeroFueraDelTalonario(999).Code);
        (await DigitarAsync(1002)).IsSuccess.Should().BeTrue();
        (await DigitarAsync(1002)).Error.Code.Should().Be(ErroresDeRecaudo.NumeroDeTalonarioUsado(1002).Code);
        (await DigitarAsync(1003, Guid.NewGuid())).Error.Code.Should().Be(ErroresDeRecaudo.ImagenDeContingenciaRequerida().Code);
    }

    [Fact]
    public async Task El_informe_del_talonario_y_la_alerta_de_los_que_faltan_por_digitar()
    {
        await DigitarAsync(1001);
        await DigitarAsync(1004);

        var estado = await new GetContingencyBookStatusQueryHandler(_b.K.C.E.Db, _b.K.C.E.Alcance).Handle(new GetContingencyBookStatusQuery(_talonario.PublicId), default);
        estado.Value.Typed.Should().Equal(1001, 1004);
        estado.Value.Pending.Should().Equal(1002, 1003);
        estado.Value.Unused.Should().Be(46);

        var alertadas = await new AntiguedadDeRecaudo(_b.K.C.E.Db, _b.K.C.E.Parametros, _b.Movimientos, Substitute.For<IngenIA365ERP.Application.Common.Calendar.ICalendarioHabil>(), _b.K.C.E.Alertas,
            _b.K.C.E.Reloj).ContingenciaPorDigitarAsync(default);
        alertadas.Should().Be(1);
        await _b.K.C.E.Alertas.Received().LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.RecaudoContingenciaPorDigitar), Arg.Any<CancellationToken>());
    }
}
