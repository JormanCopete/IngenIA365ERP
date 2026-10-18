using System.Text;
using FluentAssertions;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Core.BankFiles;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Enums.Core;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Core.BankFiles;

/// <summary>
/// Feature 013, K2, T0832: la vista previa del formato de vuelta en <c>/maestros/formatos-bancarios</c> lee un archivo de prueba con
/// <see cref="FlatFileReader"/> sin guardar nada (<see cref="ReadBankFileSampleQuery"/>): las filas con sus valores por origen ya tipados (montos y
/// fechas en forma invariante) y los errores con su línea y campo; un formato que no existe responde <c>Core.BankFileFormat.NotFound</c>. (nuevo)
/// </summary>
public class LecturaDePruebaDeUnFormatoTests
{
    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();

    private async Task<Guid> VueltaAsync()
    {
        var clock = Substitute.For<IDateTimeService>();
        clock.UtcNow.Returns(new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc));
        var user = Substitute.For<ICurrentUserService>();
        user.UserName.Returns("formatos@demo");
        var def = new BankFileFormatDefinition
        {
            Code = "PAGVUE", Name = "Respuesta de la pagaduría", Scope = nameof(BankFileScope.PayrollDeductionResult), ValidFrom = new DateOnly(2026, 12, 1),
            Kind = nameof(BankFileKind.Delimited), Delimiter = ";", Encoding = "utf-8", AmountFormat = nameof(BankFileAmountFormat.Point2),
            FileName = "RESP{GenerationDate:yyyyMMdd}.csv",
            Records = new()
            {
                Header = new() { Enabled = false },
                Detail = new()
                {
                    Enabled = true,
                    Fields = [
                        new() { Order = 1, Name = "Credito", Source = "LoanNumber", Required = true },
                        new() { Order = 2, Name = "Codigo", Source = "ResultCode", Required = true },
                        new() { Order = 3, Name = "Descontado", Source = "DeductedAmount" },
                        new() { Order = 4, Name = "Fecha", Source = "DeductionDate" },
                    ],
                },
                Trailer = new() { Enabled = false },
            },
        };
        var r = await new CreateBankFileFormatCommandHandler(_db, clock, user).Handle(new CreateBankFileFormatCommand(def), CancellationToken.None);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        return r.Value;
    }

    [Fact]
    public async Task Lee_las_filas_tipadas_y_los_errores_sin_guardar_nada()
    {
        var formato = await VueltaAsync();
        var formatos = _db.BankFileFormats.Count();
        var contenido = Encoding.UTF8.GetBytes("CR-001;00;125000.50;20270130\nCR-002;07;0.00;\nCR-003;00;no-es-monto;20270130\n");

        var r = await new ReadBankFileSampleQueryHandler(_db).Handle(new ReadBankFileSampleQuery(formato, contenido), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        r.Value.DetailLineCount.Should().Be(3);
        var primera = r.Value.Rows.Should().Contain(f => f.LineNumber == 1).Subject;
        primera.Values["LoanNumber"].Should().Be("CR-001");
        primera.Values["ResultCode"].Should().Be("00");
        primera.Values["DeductedAmount"].Should().Be("125000.50");
        primera.Values["DeductionDate"].Should().Be("2027-01-30");
        r.Value.Errors.Should().ContainSingle(e => e.LineNumber == 3).Which.Field.Should().NotBeNullOrEmpty();
        _db.BankFileFormats.Count().Should().Be(formatos, "leer no guarda nada");
    }

    [Fact]
    public async Task Un_formato_que_no_existe_es_el_404_de_los_formatos()
    {
        var r = await new ReadBankFileSampleQueryHandler(_db).Handle(new ReadBankFileSampleQuery(Guid.NewGuid(), [1]), CancellationToken.None);
        r.IsFailure.Should().BeTrue();
        r.Error.Should().Be(BankFileFormatErrors.NotFound);
    }
}
