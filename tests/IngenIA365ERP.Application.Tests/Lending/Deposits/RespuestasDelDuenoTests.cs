using FluentAssertions;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Lending.Deposits.Documents;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Application.Lending.Deposits.TermDeposits;
using IngenIA365ERP.Application.Lending.Imports;
using IngenIA365ERP.Application.Lending.Payroll;
using IngenIA365ERP.Application.Tests.Lending.Sessions;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits;

/// <summary>
/// Las respuestas del dueño del 2026-10-21 (preguntas 55–106; research §27, E250 en adelante) que cambian lo construido: los lotes programados de
/// documentos (86), el pago de intereses del CDAT a la cuenta de un tercero (74), las plantillas de líneas (67), el archivo de pagaduría sin código de
/// concepto (59) y la fecha mínima de una versión de línea antes de activar (106). InMemory con hoy = 2026-12-01 (<see cref="GobiernoDePrueba"/>).
/// </summary>
public sealed class RespuestasDelDuenoTests : IDisposable
{
    private readonly GobiernoDePrueba _g = new();

    public void Dispose() => _g.Dispose();

    // ------------------------------------------------------------------------------------------------ 86 lotes programados --

    [Fact]
    public void El_parametro_de_lotes_programados_se_lee_y_lo_que_no_se_entiende_se_ignora()
    {
        var lotes = RunScheduledDocumentBatchesCommandHandler.Leer(
            "[{\"clase\":\"AccountStatement\",\"dia\":5},{\"clase\":\"DepositTaxCertificate\",\"mes\":3,\"dia\":1},{\"clase\":\"Otra\",\"dia\":2},{\"clase\":\"AccountStatement\",\"dia\":40}]");

        lotes.Should().BeEquivalentTo([new LoteProgramadoDeDocumentos("AccountStatement", 5, null), new LoteProgramadoDeDocumentos("DepositTaxCertificate", 1, 3)]);
        RunScheduledDocumentBatchesCommandHandler.Leer("no es json").Should().BeEmpty();
        RunScheduledDocumentBatchesCommandHandler.Leer("[]").Should().BeEmpty();
        RunScheduledDocumentBatchesCommandHandler.Leer(null).Should().BeEmpty();
    }

    [Fact]
    public void Un_lote_toca_su_dia_de_cada_mes_o_de_su_mes_y_el_31_cae_el_ultimo_dia_de_un_mes_corto()
    {
        var mensual = new LoteProgramadoDeDocumentos("AccountStatement", 5, null);
        var anual = new LoteProgramadoDeDocumentos("DepositTaxCertificate", 1, 3);
        var fin = new LoteProgramadoDeDocumentos("AccountStatement", 31, null);

        RunScheduledDocumentBatchesCommandHandler.Toca(mensual, new DateOnly(2027, 2, 5)).Should().BeTrue();
        RunScheduledDocumentBatchesCommandHandler.Toca(mensual, new DateOnly(2027, 2, 6)).Should().BeFalse();
        RunScheduledDocumentBatchesCommandHandler.Toca(anual, new DateOnly(2027, 3, 1)).Should().BeTrue();
        RunScheduledDocumentBatchesCommandHandler.Toca(anual, new DateOnly(2027, 4, 1)).Should().BeFalse();
        RunScheduledDocumentBatchesCommandHandler.Toca(fin, new DateOnly(2027, 2, 28)).Should().BeTrue();
        RunScheduledDocumentBatchesCommandHandler.Toca(fin, new DateOnly(2027, 2, 27)).Should().BeFalse();
    }

    [Fact]
    public void El_parametro_de_lotes_programados_nace_vacio_y_pendiente_de_validar()
    {
        var d = ParametrosDeCartera.Definiciones.Single(x => x.Clave == ParametrosDeCartera.DocumentosLotesProgramados);
        d.DefectoSeguro.Should().Be("[]");
        d.PendienteDeValidar.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void La_tarea_de_lotes_corre_una_vez_al_dia_desde_las_seis()
    {
        var tarea = new TareaDeLotesDeDocumentos();
        var seisYMedia = new DateTimeOffset(2027, 3, 1, 6, 30, 0, TimeSpan.FromHours(-5));

        tarea.DebeCorrer(seisYMedia, null).Should().BeTrue();
        tarea.DebeCorrer(seisYMedia, seisYMedia).Should().BeFalse();
        tarea.DebeCorrer(seisYMedia.AddHours(-2), null).Should().BeFalse();
    }

    // ------------------------------------------------------------------------------------------ 74 intereses a un tercero --

    [Fact]
    public async Task Pagar_a_la_cuenta_del_mismo_titular_no_pide_nada_y_a_otra_persona_exige_permiso_e_instruccion()
    {
        var permisos = Substitute.For<IngenIA365ERP.Application.Common.Interfaces.Security.ICurrentUserPermissions>();
        permisos.ListAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyCollection<string>>([]));

        (await DestinosDeCdat.ValidarTerceroAsync(_g.Db, permisos, 7, 7, null, default)).IsSuccess.Should().BeTrue();

        var sinPermiso = await DestinosDeCdat.ValidarTerceroAsync(_g.Db, permisos, 8, 7, Guid.NewGuid(), default);
        sinPermiso.Error.Code.Should().Be(ErroresDeCdat.CodigoTerceroSinPermiso);

        permisos.ListAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyCollection<string>>([DestinosDeCdat.PermisoDePagoATercero]));
        var sinInstruccion = await DestinosDeCdat.ValidarTerceroAsync(_g.Db, permisos, 8, 7, null, default);
        sinInstruccion.Error.Code.Should().Be(ErroresDeCdat.CodigoTerceroSinInstruccion);
        var inexistente = await DestinosDeCdat.ValidarTerceroAsync(_g.Db, permisos, 8, 7, Guid.NewGuid(), default);
        inexistente.Error.Code.Should().Be(ErroresDeCdat.CodigoTerceroSinInstruccion);
    }

    /// <summary>
    /// Revisión a mano del 2026-10-27 (feature 015, E250 de la 014): el giro de los intereses a otra persona pide lo mismo que la cuenta de otra persona —el permiso y la
    /// instrucción—; al titular, nada.
    /// </summary>
    [Fact]
    public async Task El_giro_de_los_intereses_a_otra_persona_exige_permiso_e_instruccion()
    {
        var titular = new IngenIA365ERP.Domain.Entities.Core.Person { FirstName = "Ana", LastName = "Titular", TaxId = "1001", IdType = "CC" };
        var tercero = new IngenIA365ERP.Domain.Entities.Core.Person { FirstName = "Beto", LastName = "Tercero", TaxId = "1002", IdType = "CC" };
        _g.Db.People.AddRange(titular, tercero);
        await _g.Db.SaveChangesAsync();
        var permisos = Substitute.For<IngenIA365ERP.Application.Common.Interfaces.Security.ICurrentUserPermissions>();
        permisos.ListAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyCollection<string>>([]));

        var alTitular = await DestinosDeCdat.ValidarAsync(_g.Db, null!, null!, new TermDepositDestinationInput(TermDepositDestinationKind.PaymentOrder,
            PayeePersonPublicId: titular.PublicId), titular.Id, default, permisos);
        alTitular.IsSuccess.Should().BeTrue(alTitular.IsFailure ? alTitular.Error.Code : null);
        var aOtra = await DestinosDeCdat.ValidarAsync(_g.Db, null!, null!, new TermDepositDestinationInput(TermDepositDestinationKind.PaymentOrder,
            PayeePersonPublicId: tercero.PublicId), titular.Id, default, permisos);
        aOtra.Error.Code.Should().Be(ErroresDeCdat.CodigoTerceroSinPermiso);
        var capital = await DestinosDeCdat.ValidarAsync(_g.Db, null!, null!, new TermDepositDestinationInput(TermDepositDestinationKind.PaymentOrder,
            PayeePersonPublicId: tercero.PublicId), titular.Id, default, permisos, soloTitular: true);
        capital.IsFailure.Should().BeTrue("el capital se gira sólo al titular");
    }

    [Fact]
    public async Task Sin_el_servicio_de_permisos_un_tercero_nunca_pasa()
    {
        var r = await DestinosDeCdat.ValidarTerceroAsync(_g.Db, null, 8, 7, Guid.NewGuid(), default);

        r.Error.Code.Should().Be(ErroresDeCdat.CodigoTerceroSinPermiso);
    }

    // ------------------------------------------------------------------------------------------------ 67 plantillas de líneas --

    [Fact]
    public void Las_plantillas_de_lineas_de_captacion_y_aportes_estan_en_el_registro_con_sus_permisos()
    {
        var captacion = CatalogoDePlantillasDeCartera.Todas.Single(p => p.Key == PlantillasDeLineasDeCaptacion.CaptacionClave);
        var aportes = CatalogoDePlantillasDeCartera.Todas.Single(p => p.Key == PlantillasDeLineasDeCaptacion.AportesClave);

        captacion.Sheets.Should().Equal("Lineas", "Tramos");
        captacion.ImportPermission.Should().Be(PermisosDeLineas.AdministrarCaptacion);
        captacion.DownloadPermission.Should().Be(PermisosDeLineas.VerCaptacion);
        aportes.ImportPermission.Should().Be(PermisosDeLineas.AdministrarAportes);
        captacion.Definicion.Should().BeSameAs(PlantillasDeLineasDeCaptacion.Captacion);
        aportes.Definicion.Should().BeSameAs(PlantillasDeLineasDeCaptacion.Aportes);
    }

    [Fact]
    public void Las_columnas_de_las_plantillas_de_lineas_no_se_repiten_en_su_hoja_y_las_llaves_son_obligatorias()
    {
        foreach (var definicion in new[] { PlantillasDeLineasDeCaptacion.Captacion, PlantillasDeLineasDeCaptacion.Aportes })
            foreach (var hoja in definicion.Hojas)
            {
                hoja.Columnas.Select(c => c.Nombre).Should().OnlyHaveUniqueItems($"la hoja {hoja.Nombre} de {definicion.Clave}");
                hoja.Columnas.Single(c => c.Nombre == PlantillasDeLineasDeCaptacion.C.Codigo).Obligatoria.Should().BeTrue();
                hoja.Columnas.Single(c => c.Nombre == PlantillasDeLineasDeCaptacion.C.VigenteDesde).Obligatoria.Should().BeTrue();
            }
    }

    // --------------------------------------------------------------------------------------------- 59 línea sin código --

    [Fact]
    public void El_error_de_una_linea_de_cuenta_sin_codigo_de_pagaduria_dice_cuales_son_y_donde_corregirlas()
    {
        var e = ErroresDeLibranza.LineaDeCuentaSinCodigo("CONV1", ["APORTES", "AHORRO"]);

        e.Code.Should().Be("Lending.PayrollFile.AccountLineWithoutPaymasterCode");
        e.Message.Should().Contain("APORTES, AHORRO").And.Contain("Grupos y equivalencias").And.Contain("CONV1");
    }

    // ------------------------------------------------------------------------------------ 63 obligado a llevar contabilidad --

    [Fact]
    public void El_componente_inflacionario_se_descuenta_a_la_persona_natural_que_no_esta_obligada_a_llevar_contabilidad()
    {
        var natural = new IngenIA365ERP.Domain.Entities.Core.Person { PersonType = "N" };
        var obligada = new IngenIA365ERP.Domain.Entities.Core.Person { PersonType = "N", IsObligatedToKeepBooks = true };

        IngenIA365ERP.Application.Lending.Deposits.Accrual.ReglasDeRetencion.PerfilDe(natural).PersonaNaturalNoObligada.Should().BeTrue();
        IngenIA365ERP.Application.Lending.Deposits.Accrual.ReglasDeRetencion.PerfilDe(obligada).PersonaNaturalNoObligada.Should().BeFalse();
    }

    // --------------------------------------------------------------------------------------- 106 fecha mínima de la versión --

    [Fact]
    public async Task La_fecha_minima_de_una_version_es_hoy_una_vez_activada_la_naturaleza_y_sin_limite_antes()
    {
        var hoy = GobiernoDePrueba.Hoy;
        (await IngenIA365ERP.Application.Lending.Deposits.Activation.ActivacionesDeCaptacion.FechaMinimaDeVersionAsync(_g.Db, LineNature.Deposit, hoy, default))
            .Should().Be(DateOnly.MinValue);

        _g.Db.LendingSetups.Add(new LendingSetup { Id = LendingSetup.IdDeLaFilaUnica, ActivationDate = hoy.AddMonths(-3), DepositsActivationDate = hoy.AddMonths(-1) });
        await _g.Db.SaveChangesAsync();

        (await IngenIA365ERP.Application.Lending.Deposits.Activation.ActivacionesDeCaptacion.FechaMinimaDeVersionAsync(_g.Db, LineNature.Deposit, hoy, default))
            .Should().Be(hoy);
        (await IngenIA365ERP.Application.Lending.Deposits.Activation.ActivacionesDeCaptacion.FechaMinimaDeVersionAsync(_g.Db, LineNature.Contribution, hoy, default))
            .Should().Be(DateOnly.MinValue, "los aportes todavía no se activaron");
    }
}
