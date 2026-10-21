using FluentAssertions;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Core.BankFiles;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Core;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Core.BankFiles;

/// <summary>
/// Feature 013, K2, T0807 (T78, DT §2.25, adaptadores-y-archivos.md §6.1): los ámbitos de libranza (<c>PayrollDeductionRequest</c> = 5,
/// <c>PayrollDeductionResult</c> = 6) llevan la marca <see cref="SeleccionPorReferenciaAttribute"/>: cada convenio elige su formato por
/// referencia, así que dos genéricos del mismo ámbito con la misma vigencia se crean —uno por pagaduría—, mientras en
/// <c>PayrollDisbursement</c> siguen chocando (<c>Core.BankFileFormat.Overlaps</c>). El validador admite los orígenes de cada ámbito y no
/// otros, y exige que los formatos de libranza sean genéricos (sin banco).
/// </summary>
public class LosFormatosElegidosPorReferenciaNoCompitenPorVigencia
{
    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IDateTimeService _clock = Substitute.For<IDateTimeService>();
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();

    public LosFormatosElegidosPorReferenciaNoCompitenPorVigencia()
    {
        _clock.UtcNow.Returns(new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc));
        _user.UserName.Returns("formatos@demo");
    }

    private static BankFileFormatDefinition Definicion(string code, BankFileScope ambito, params (string Nombre, string Origen)[] detalle) => new()
    {
        Code = code, Name = $"Formato {code}", Scope = ambito.ToString(), ValidFrom = new DateOnly(2026, 12, 1),
        Kind = nameof(BankFileKind.Delimited), Delimiter = ";", Encoding = "utf-8", AmountFormat = nameof(BankFileAmountFormat.Point2),
        FileName = "ARCHIVO{GenerationDate:yyyyMMdd}.csv",
        Records = new()
        {
            Header = new() { Enabled = false },
            Detail = new() { Enabled = true, Fields = detalle.Select((d, i) => new BankFileFormatDefinition.BankFileFieldDefinition
            {
                Order = i + 1, Name = d.Nombre, Source = d.Origen,
            }).ToList() },
            Trailer = new() { Enabled = false },
        },
    };

    private static BankFileFormatDefinition Ida(string code) => Definicion(code, BankFileScope.PayrollDeductionRequest,
        ("Documento", "PayeeDocument"), ("Credito", "LoanNumber"), ("Valor", "Amount"), ("Novedad", "NoveltyKind"));

    private static BankFileFormatDefinition Vuelta(string code) => Definicion(code, BankFileScope.PayrollDeductionResult,
        ("Credito", "LoanNumber"), ("Codigo", "ResultCode"), ("Descontado", "DeductedAmount"), ("Fecha", "DeductionDate"));

    private static BankFileFormatDefinition Dispersion(string code) => Definicion(code, BankFileScope.PayrollDisbursement,
        ("Documento", "PayeeDocument"), ("Valor", "Amount"));

    private Task<IngenIA365ERP.Application.Common.Models.Result<Guid>> Crear(BankFileFormatDefinition def) =>
        new CreateBankFileFormatCommandHandler(_db, _clock, _user).Handle(new CreateBankFileFormatCommand(def), CancellationToken.None);

    [Fact]
    public void Los_ambitos_de_libranza_llevan_la_marca_y_los_demas_no()
    {
        SeleccionPorReferenciaAttribute.Tiene(BankFileScope.PayrollDeductionRequest).Should().BeTrue();
        SeleccionPorReferenciaAttribute.Tiene(BankFileScope.PayrollDeductionResult).Should().BeTrue();
        foreach (var otro in new[] { BankFileScope.PayrollDisbursement, BankFileScope.SeveranceDeposit, BankFileScope.SupplierPayments, BankFileScope.LoanDisbursement })
            SeleccionPorReferenciaAttribute.Tiene(otro).Should().BeFalse($"{otro} compite por vigencia");
        ((int)BankFileScope.PayrollDeductionRequest, (int)BankFileScope.PayrollDeductionResult).Should().Be((5, 6), "data-model §9.3");
    }

    [Fact]
    public async Task Dos_genericos_de_libranza_con_la_misma_vigencia_se_crean_y_en_dispersion_siguen_chocando()
    {
        (await Crear(Ida("PAG-A-IDA"))).IsSuccess.Should().BeTrue();
        (await Crear(Ida("PAG-B-IDA"))).IsSuccess.Should().BeTrue("una pagaduría no le quita el formato a otra");
        (await Crear(Vuelta("PAG-A-VUELTA"))).IsSuccess.Should().BeTrue();
        (await Crear(Vuelta("PAG-B-VUELTA"))).IsSuccess.Should().BeTrue();

        (await Crear(Dispersion("DISP-1"))).IsSuccess.Should().BeTrue();
        var choque = await Crear(Dispersion("DISP-2"));
        choque.IsFailure.Should().BeTrue();
        choque.Error.Code.Should().Be("Core.BankFileFormat.Overlaps");
    }

    [Fact]
    public async Task Editar_un_formato_de_libranza_tampoco_compite()
    {
        (await Crear(Ida("PAG-C-IDA"))).IsSuccess.Should().BeTrue();
        var segundo = await Crear(Ida("PAG-D-IDA"));
        var def = Ida("PAG-D-IDA");
        def.ValidFrom = new DateOnly(2026, 11, 1);

        var r = await new UpdateBankFileFormatCommandHandler(_db, _clock, _user).Handle(new UpdateBankFileFormatCommand(segundo.Value, def), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
    }

    [Fact]
    public void El_validador_admite_los_origenes_de_cada_ambito_y_no_otros()
    {
        BankFileFormatValidator.Validar(Ida("IDA")).Should().BeEmpty();
        BankFileFormatValidator.Validar(Vuelta("VUELTA")).Should().BeEmpty();

        BankFileFormatValidator.Validar(Definicion("DISP", BankFileScope.PayrollDisbursement, ("Documento", "PayeeDocument"), ("Credito", "LoanNumber")))
            .Should().ContainSingle(e => e.Field == "Credito" && e.Message.Contains("Cartera"), "LoanNumber no es de la dispersión de nómina");
        BankFileFormatValidator.Validar(Definicion("IDA2", BankFileScope.PayrollDeductionRequest, ("Credito", "LoanNumber"), ("Codigo", "ResultCode")))
            .Should().ContainSingle(e => e.Field == "Codigo" && e.Message.Contains("PayrollDeductionRequest"), "ResultCode es de la vuelta");
        BankFileFormatValidator.Validar(Definicion("VUELTA2", BankFileScope.PayrollDeductionResult, ("Credito", "LoanNumber"), ("Novedad", "NoveltyKind")))
            .Should().ContainSingle(e => e.Field == "Novedad", "NoveltyKind es de la ida");

        BankFileFormatValidator.OrigenesAdmitidos(BankFileScope.PayrollDeductionRequest)!.Should().Contain(
            [BankFieldSource.PayerEmployeeId, BankFieldSource.LoanNumber, BankFieldSource.InstallmentNumber, BankFieldSource.DueDate, BankFieldSource.Amount,
             BankFieldSource.PastDueAmount, BankFieldSource.NoveltyKind, BankFieldSource.PeriodYear, BankFieldSource.PeriodMonth, BankFieldSource.LibranzaDate]);
        BankFileFormatValidator.OrigenesAdmitidos(BankFileScope.PayrollDeductionResult)!.Should().Contain(
            [BankFieldSource.ResultCode, BankFieldSource.DeductedAmount, BankFieldSource.DeductionDate]).And.NotContain(BankFieldSource.Amount);
    }

    [Fact]
    public async Task Los_formatos_de_libranza_con_banco_se_rechazan()
    {
        var banco = new Bank { Name = "Banco de prueba", TransferCode = "0051", CreatedBy = "test" };
        _db.Banks.Add(banco);
        await _db.SaveChangesAsync();

        foreach (var def in new[] { Ida("IDA-BANCO"), Vuelta("VUELTA-BANCO") })
        {
            def.BankPublicId = banco.PublicId;
            BankFileFormatValidator.Validar(def).Should().ContainSingle(e => e.Field == "bankPublicId");
            (await Crear(def)).Error.Code.Should().Be("Core.BankFileFormat.Invalid");
        }

        var dispersion = Dispersion("DISP-BANCO");
        dispersion.BankPublicId = banco.PublicId;
        BankFileFormatValidator.Validar(dispersion).Should().BeEmpty("la dispersión sí va ligada a un banco");
    }

    [Fact]
    public void Los_origenes_de_libranza_tienen_su_tipo_por_defecto()
    {
        BankFileFormatDefinition.TipoPorDefecto(BankFieldSource.DeductedAmount).Should().Be(BankFieldDataType.Amount);
        BankFileFormatDefinition.TipoPorDefecto(BankFieldSource.PastDueAmount).Should().Be(BankFieldDataType.Amount);
        BankFileFormatDefinition.TipoPorDefecto(BankFieldSource.DeductionDate).Should().Be(BankFieldDataType.Date);
        BankFileFormatDefinition.TipoPorDefecto(BankFieldSource.DueDate).Should().Be(BankFieldDataType.Date);
        BankFileFormatDefinition.TipoPorDefecto(BankFieldSource.LibranzaDate).Should().Be(BankFieldDataType.Date);
        BankFileFormatDefinition.TipoPorDefecto(BankFieldSource.InstallmentNumber).Should().Be(BankFieldDataType.Integer);
        BankFileFormatDefinition.TipoPorDefecto(BankFieldSource.PeriodMonth).Should().Be(BankFieldDataType.Integer);
        BankFileFormatDefinition.TipoPorDefecto(BankFieldSource.ResultCode).Should().Be(BankFieldDataType.Text);
    }
}
