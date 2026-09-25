using FluentAssertions;
using FluentValidation.TestHelper;
using IngenIA365ERP.Application.Core.People.Commands.CreatePerson;
using IngenIA365ERP.Application.Core.People.Commands.UpdatePerson;
using IngenIA365ERP.Application.Core.People.Contracts;
using IngenIA365ERP.Application.Core.People.Queries;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Core.People;

/// <summary>
/// Feature 012, T111 (T24, <c>contracts/api.md</c> §29 «Personas»; enmienda de la 008): el perfil tributario de la persona
/// —las seis marcas nuevas y las cuatro que ya existían en <c>COR_People</c>— se escribe sólo por <see cref="PersonInput"/>,
/// vuelve en <c>GET /api/core/people/{id}</c> y un <c>PUT</c> que no lo trae no lo borra (las marcas son anulables en el
/// contrato: nulo = no cambia).
/// </summary>
public class PerfilTributarioDePersonaTests
{
    private static PersonInput ConPerfilCompleto(PersonInput entrada) => entrada with
    {
        IsVatResponsible = true,
        IsSelfWithholder = true,
        IsVatWithholdingAgent = true,
        IsSimpleTaxRegime = true,
        IsIncomeTaxFiler = true,
        IsObligatedToInvoice = true,
        IsLargeContributor = true,
        WithholdingExempt = true,
        IcaWithholdingExempt = true,
        CiiuCode = "4711",
    };

    [Fact]
    public async Task Crear_escribe_las_diez_marcas_y_la_consulta_las_devuelve()
    {
        var d = new PersonasTestData();
        var alta = await new CreatePersonCommandHandler(d.Altas).Handle(
            Comando(ConPerfilCompleto(PersonasTestData.Entrada())), CancellationToken.None);

        alta.IsSuccess.Should().BeTrue(alta.Error.Message);
        var p = await d.Db.People.AsNoTracking().SingleAsync(x => x.PublicId == alta.Value);
        p.IsVatResponsible.Should().BeTrue();
        p.IsSelfWithholder.Should().BeTrue();
        p.IsVatWithholdingAgent.Should().BeTrue();
        p.IsSimpleTaxRegime.Should().BeTrue();
        p.IsIncomeTaxFiler.Should().BeTrue();
        p.IsObligatedToInvoice.Should().BeTrue();
        p.IsLargeContributor.Should().BeTrue();
        p.WithholdingExempt.Should().BeTrue();
        p.IcaWithholdingExempt.Should().BeTrue();
        p.CiiuCode.Should().Be("4711");

        var dto = (await new GetPersonByIdQueryHandler(d.Db).Handle(new GetPersonByIdQuery(alta.Value), CancellationToken.None)).Value;
        dto.IsVatResponsible.Should().BeTrue();
        dto.IsSelfWithholder.Should().BeTrue();
        dto.IsVatWithholdingAgent.Should().BeTrue();
        dto.IsSimpleTaxRegime.Should().BeTrue();
        dto.IsIncomeTaxFiler.Should().BeTrue();
        dto.IsObligatedToInvoice.Should().BeTrue();
        dto.IsLargeContributor.Should().BeTrue();
        dto.WithholdingExempt.Should().BeTrue();
        dto.IcaWithholdingExempt.Should().BeTrue();
        dto.CiiuCode.Should().Be("4711");
    }

    [Fact]
    public async Task Crear_sin_perfil_deja_todo_en_falso()
    {
        var d = new PersonasTestData();
        var alta = await new CreatePersonCommandHandler(d.Altas).Handle(Comando(PersonasTestData.Entrada()), CancellationToken.None);

        var p = await d.Db.People.AsNoTracking().SingleAsync(x => x.PublicId == alta.Value);
        p.IsVatResponsible.Should().BeFalse();
        p.IsObligatedToInvoice.Should().BeFalse();
        p.IsLargeContributor.Should().BeFalse();
        p.CiiuCode.Should().BeNull();
    }

    [Fact]
    public async Task Un_PUT_sin_el_perfil_no_lo_borra()
    {
        var d = new PersonasTestData();
        var alta = await new CreatePersonCommandHandler(d.Altas).Handle(Comando(ConPerfilCompleto(PersonasTestData.Entrada())), CancellationToken.None);

        // Un cliente viejo (o Empleados/Asociados de antes) manda la persona sin el perfil: nulo = no cambia.
        var r = await new UpdatePersonCommandHandler(d.Db, d.Clock, d.User, d.Personas).Handle(
            Actualizar(alta.Value, PersonasTestData.Entrada() with { Email = "nuevo@demo.co" }), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var p = await d.Db.People.AsNoTracking().SingleAsync(x => x.PublicId == alta.Value);
        p.Email.Should().Be("nuevo@demo.co");
        p.IsVatResponsible.Should().BeTrue();
        p.IsSelfWithholder.Should().BeTrue();
        p.IsVatWithholdingAgent.Should().BeTrue();
        p.IsSimpleTaxRegime.Should().BeTrue();
        p.IsIncomeTaxFiler.Should().BeTrue();
        p.IsObligatedToInvoice.Should().BeTrue();
        p.IsLargeContributor.Should().BeTrue();
        p.WithholdingExempt.Should().BeTrue();
        p.IcaWithholdingExempt.Should().BeTrue();
        p.CiiuCode.Should().Be("4711");
    }

    [Fact]
    public async Task Un_PUT_con_el_perfil_lo_cambia_y_el_CIIU_vacio_lo_quita()
    {
        var d = new PersonasTestData();
        var alta = await new CreatePersonCommandHandler(d.Altas).Handle(Comando(ConPerfilCompleto(PersonasTestData.Entrada())), CancellationToken.None);

        var r = await new UpdatePersonCommandHandler(d.Db, d.Clock, d.User, d.Personas).Handle(
            Actualizar(alta.Value, PersonasTestData.Entrada() with { IsVatResponsible = false, IsObligatedToInvoice = false, WithholdingExempt = false, CiiuCode = "" }),
            CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var p = await d.Db.People.AsNoTracking().SingleAsync(x => x.PublicId == alta.Value);
        p.IsVatResponsible.Should().BeFalse();
        p.IsObligatedToInvoice.Should().BeFalse();
        p.WithholdingExempt.Should().BeFalse();
        p.IsSelfWithholder.Should().BeTrue("no venía en el PUT");
        p.CiiuCode.Should().BeNull("el texto vacío quita el CIIU; el nulo lo conserva");
    }

    [Theory]
    [InlineData("47")]
    [InlineData("4711A")]
    [InlineData("47 11")]
    [InlineData("1234567")]
    [InlineData("*")]
    public void Un_CIIU_con_formato_invalido_lo_rechaza_el_validador(string ciiu)
    {
        var r = new PersonInputValidator().TestValidate(PersonasTestData.Entrada() with { CiiuCode = ciiu });

        r.ShouldHaveValidationErrorFor(x => x.CiiuCode);
    }

    [Theory]
    [InlineData("4711")]
    [InlineData("471101")]
    [InlineData("")]
    [InlineData(null)]
    public void Un_CIIU_de_cuatro_a_seis_digitos_o_vacio_pasa(string? ciiu)
    {
        var r = new PersonInputValidator().TestValidate(PersonasTestData.Entrada() with { CiiuCode = ciiu });

        r.ShouldNotHaveValidationErrorFor(x => x.CiiuCode);
    }

    [Fact]
    public void Las_diez_marcas_estan_en_el_contrato_y_vuelven_en_la_consulta()
    {
        string[] perfil =
        [
            nameof(PersonInput.IsVatResponsible), nameof(PersonInput.IsSelfWithholder), nameof(PersonInput.IsVatWithholdingAgent),
            nameof(PersonInput.IsSimpleTaxRegime), nameof(PersonInput.IsIncomeTaxFiler), nameof(PersonInput.IsObligatedToInvoice),
            nameof(PersonInput.IsLargeContributor), nameof(PersonInput.WithholdingExempt), nameof(PersonInput.IcaWithholdingExempt),
            nameof(PersonInput.CiiuCode),
        ];
        var leidas = typeof(PersonEditDto).GetProperties().Select(p => p.Name).ToHashSet();

        perfil.Should().OnlyContain(n => leidas.Contains(n));
    }

    internal static CreatePersonCommand Comando(PersonInput e) => new()
    {
        IdType = e.IdType, TaxId = e.TaxId, FirstName = e.FirstName, LastName = e.LastName, Email = e.Email, Mobile = e.Mobile,
        Gender = e.Gender, MaritalStatus = e.MaritalStatus,
        IsVatResponsible = e.IsVatResponsible, IsSelfWithholder = e.IsSelfWithholder, IsVatWithholdingAgent = e.IsVatWithholdingAgent,
        IsSimpleTaxRegime = e.IsSimpleTaxRegime, IsIncomeTaxFiler = e.IsIncomeTaxFiler, IsObligatedToInvoice = e.IsObligatedToInvoice,
        IsLargeContributor = e.IsLargeContributor, WithholdingExempt = e.WithholdingExempt, IcaWithholdingExempt = e.IcaWithholdingExempt,
        CiiuCode = e.CiiuCode,
    };

    private static UpdatePersonCommand Actualizar(Guid id, PersonInput e) => new()
    {
        PublicId = id,
        IdType = e.IdType, TaxId = e.TaxId, FirstName = e.FirstName, LastName = e.LastName, Email = e.Email, Mobile = e.Mobile,
        Gender = e.Gender, MaritalStatus = e.MaritalStatus,
        IsVatResponsible = e.IsVatResponsible, IsSelfWithholder = e.IsSelfWithholder, IsVatWithholdingAgent = e.IsVatWithholdingAgent,
        IsSimpleTaxRegime = e.IsSimpleTaxRegime, IsIncomeTaxFiler = e.IsIncomeTaxFiler, IsObligatedToInvoice = e.IsObligatedToInvoice,
        IsLargeContributor = e.IsLargeContributor, WithholdingExempt = e.WithholdingExempt, IcaWithholdingExempt = e.IcaWithholdingExempt,
        CiiuCode = e.CiiuCode,
    };
}
