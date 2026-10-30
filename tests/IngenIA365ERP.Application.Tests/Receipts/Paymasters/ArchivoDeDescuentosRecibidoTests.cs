using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Paymasters;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Receipts.Paymasters;

/// <summary>
/// Feature 015, US7, T0201 (R-16; FR-075; mensajes.md §3.2): el consumo de <c>DescuentosPorPagaduria</c> v1 y v2 con recibo único; las líneas estructuradas por crédito
/// y por cuota de cuenta; una versión que reemplaza a otra no enviada la deja <c>Superseded</c>; si ya se envió, se rechaza con
/// <c>Receipts.Paymaster.SupersedesSentFile</c> y alerta; la vista por pagaduría y período respeta el alcance. Prueba escrita, sin correr.
/// </summary>
public class ArchivoDeDescuentosRecibidoTests
{
    private readonly PagaduriasDePrueba _p = new();

    [Fact]
    public async Task La_v1_guarda_el_archivo_y_sus_lineas_con_recibo_unico()
    {
        var id = _p.Mensaje(_p.V1(3), 1, Guid.NewGuid());

        var r = await _p.Consumir().Handle(new ConsumePayrollDeductionFileCommand([id]), default);
        var repetido = await _p.Consumir().Handle(new ConsumePayrollDeductionFileCommand([id]), default);

        r.Value.Should().BeOfType<ResultadoDeConsumo.Processed>();
        repetido.Value.Should().BeOfType<ResultadoDeConsumo.AlreadyProcessed>();
        var archivo = await _p.B.K.C.E.Db.ReceiptsPayrollDeductionFiles.SingleAsync();
        archivo.MessageVersion.Should().Be(1);
        archivo.Status.Should().Be(PayrollDeductionFileState.Received);
        archivo.PaymasterPersonId.Should().Be(_p.Pagaduria.Id);
        archivo.LineCount.Should().Be(3);
        (await _p.B.K.C.E.Db.ReceiptsPayrollDeductionFileLines.CountAsync(l => l.PayrollDeductionFileId == archivo.Id && l.Kind == PayrollDeductionLineKind.Loan)).Should().Be(3);
        (await _p.B.K.C.E.Db.ReceiptsInboundMessages.CountAsync(m => m.MessagePublicId == id)).Should().Be(1);
    }

    [Fact]
    public async Task La_v2_distingue_creditos_y_cuotas_de_cuentas_con_el_codigo_de_la_pagaduria()
    {
        var archivo = await _p.RecibirAsync(_p.V2(), 2);

        archivo.FileGroupCode.Should().Be("G1");
        var lineas = await _p.B.K.C.E.Db.ReceiptsPayrollDeductionFileLines.Where(l => l.PayrollDeductionFileId == archivo.Id).OrderBy(l => l.Id).ToListAsync();
        lineas.Select(l => l.Kind).Should().Equal(PayrollDeductionLineKind.Loan, PayrollDeductionLineKind.AccountInstallment);
        lineas[1].ProductPublicId.Should().Be(_p.B.K.Aportes);
        lineas[1].InstallmentPublicId.Should().NotBeNull();
        lineas.Select(l => l.PaymasterConceptCode).Should().Equal("120", "130");
    }

    [Fact]
    public async Task Una_version_que_reemplaza_a_otra_no_enviada_la_deja_reemplazada()
    {
        var idViejo = _p.Mensaje(_p.V1(2), 1, Guid.NewGuid());
        await _p.Consumir().Handle(new ConsumePayrollDeductionFileCommand([idViejo]), default);
        var viejo = await _p.B.K.C.E.Db.ReceiptsPayrollDeductionFiles.SingleAsync();

        await _p.RecibirAsync(_p.V1(2, reemplaza: new SupersededDeductionFileV1 { FilePublicId = viejo.FilePublicId, FileVersion = 1, MessageId = idViejo }, version: 2), 1);

        (await _p.B.K.C.E.Db.ReceiptsPayrollDeductionFiles.SingleAsync(f => f.Id == viejo.Id)).Status.Should().Be(PayrollDeductionFileState.Superseded);
        (await _p.B.K.C.E.Db.ReceiptsPayrollDeductionFiles.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Si_la_anterior_ya_se_envio_la_nueva_se_rechaza_y_alerta()
    {
        var idViejo = _p.Mensaje(_p.V1(2), 1, Guid.NewGuid());
        await _p.Consumir().Handle(new ConsumePayrollDeductionFileCommand([idViejo]), default);
        var viejo = await _p.B.K.C.E.Db.ReceiptsPayrollDeductionFiles.SingleAsync();
        (await _p.ConfirmarEnvio().Handle(new ConfirmPayrollFileSentCommand(viejo.PublicId, PayrollFileChannel.Portal) { OperationKey = Guid.NewGuid() }, default))
            .IsSuccess.Should().BeTrue();

        var id = _p.Mensaje(_p.V1(2, reemplaza: new SupersededDeductionFileV1 { FilePublicId = viejo.FilePublicId, FileVersion = 1, MessageId = idViejo }, version: 2), 1,
            Guid.NewGuid());
        var r = await _p.Consumir().Handle(new ConsumePayrollDeductionFileCommand([id]), default);

        r.Value.Should().BeOfType<ResultadoDeConsumo.Rejected>().Which.Code.Should().Be(ErroresDeRecaudo.CodigoReemplazaArchivoEnviado);
        (await _p.B.K.C.E.Db.ReceiptsPayrollDeductionFiles.CountAsync()).Should().Be(1);
        await _p.B.K.C.E.Alertas.Received().LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.RecaudoNoAplicadoDePagaduria), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task La_vista_por_pagaduria_y_periodo_respeta_el_alcance()
    {
        var archivo = await _p.RecibirAsync(_p.V1(2), 1);

        var lista = await new ListPayrollDeductionFilesQueryHandler(_p.B.K.C.E.Db, _p.B.K.C.E.Alcance)
            .Handle(new ListPayrollDeductionFilesQuery(_p.Pagaduria.PublicId, archivo.PeriodYear, archivo.PeriodMonth), default);
        lista.Value.Should().ContainSingle().Which.Amount.Should().Be(200_000m);
        var detalle = await new GetPayrollDeductionFileQueryHandler(_p.B.K.C.E.Db, _p.B.K.C.E.Alcance).Handle(new GetPayrollDeductionFileQuery(archivo.PublicId), default);
        detalle.Value.Lines.Should().HaveCount(2);

        _p.B.K.C.E.Alcance.Valor = IngenIA365ERP.Application.Common.Scope.AlcanceDeRecaudo.Vacio;
        (await new GetPayrollDeductionFileQueryHandler(_p.B.K.C.E.Db, _p.B.K.C.E.Alcance).Handle(new GetPayrollDeductionFileQuery(archivo.PublicId), default))
            .Error.Code.Should().Be(ErroresDeRecaudo.CodigoNoEncontrado);
    }
}
