using FluentAssertions;
using IngenIA365ERP.Application.Common.Taxation;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;

namespace IngenIA365ERP.Application.Tests.Common.Taxation;

/// <summary>
/// Feature 012, T103 (T23): la UVT tiene un solo lector, <see cref="LectorDeUvt"/>, que lee <c>LegalParameterCodes.Uvt</c>
/// de los parámetros legales de nómina vigentes a la fecha. Sin vigencia falla visible con <c>Taxation.Uvt.Missing</c>:
/// «No hay UVT vigente al {fecha}; regístrela en Parámetros legales».
/// </summary>
public class LectorDeUvtTests
{
    private static PayrollLegalParameter Uvt(decimal valor, DateTime desde, DateTime? hasta = null, bool borrada = false) => new()
    {
        Code = LegalParameterCodes.Uvt,
        Name = "Unidad de Valor Tributario",
        Kind = LegalParameterKind.Amount,
        Value = valor,
        ValidFrom = desde,
        ValidTo = hasta,
        Source = "Resolución DIAN",
        IsDeleted = borrada,
    };

    [Fact]
    public async Task Lee_la_UVT_vigente_a_la_fecha()
    {
        using var db = TestDbContextFactory.Create();
        db.PayrollLegalParameters.AddRange(
            Uvt(49_799m, new DateTime(2025, 1, 1), new DateTime(2025, 12, 31)),
            Uvt(52_374m, new DateTime(2026, 1, 1)),
            new PayrollLegalParameter { Code = LegalParameterCodes.Smmlv, Name = "SMMLV", Kind = LegalParameterKind.Amount, Value = 1m, ValidFrom = new DateTime(2026, 1, 1) });
        await db.SaveChangesAsync();

        var lector = new LectorDeUvt(db);

        var en2025 = await lector.LeerAsync(new DateOnly(2025, 12, 31));
        en2025.IsSuccess.Should().BeTrue();
        en2025.Value.Valor.Should().Be(49_799m);
        en2025.Value.VigenteDesde.Should().Be(new DateOnly(2025, 1, 1));

        var en2026 = await lector.LeerAsync(new DateOnly(2026, 3, 16));
        en2026.Value.Valor.Should().Be(52_374m);
        en2026.Value.Fuente.Should().Be("Resolución DIAN");
    }

    [Fact]
    public async Task Sin_vigencia_falla_con_Taxation_Uvt_Missing_y_el_mensaje_del_contrato()
    {
        using var db = TestDbContextFactory.Create();
        db.PayrollLegalParameters.Add(Uvt(52_374m, new DateTime(2026, 1, 1)));
        db.PayrollLegalParameters.Add(Uvt(47_065m, new DateTime(2024, 1, 1), new DateTime(2024, 12, 31), borrada: true));
        await db.SaveChangesAsync();

        var r = await new LectorDeUvt(db).LeerAsync(new DateOnly(2024, 6, 30));

        r.IsFailure.Should().BeTrue("la vigencia de 2024 está borrada");
        r.Error.Code.Should().Be("Taxation.Uvt.Missing");
        r.Error.Message.Should().Be("No hay UVT vigente al 30/06/2024; regístrela en Parámetros legales.");
    }

    [Fact]
    public async Task Una_UVT_sin_valor_tambien_es_falta_de_vigencia()
    {
        using var db = TestDbContextFactory.Create();
        var sinValor = Uvt(0m, new DateTime(2026, 1, 1));
        sinValor.Value = null;
        db.PayrollLegalParameters.Add(sinValor);
        await db.SaveChangesAsync();

        (await new LectorDeUvt(db).LeerAsync(new DateOnly(2026, 2, 1))).Error.Code.Should().Be(LectorDeUvt.CodigoFaltante);
    }
}
