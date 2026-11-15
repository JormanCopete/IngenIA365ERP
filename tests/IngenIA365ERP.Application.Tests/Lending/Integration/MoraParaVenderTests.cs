using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Integration.Comercial;
using IngenIA365ERP.Application.Lending.Integration.Reservas;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Projections;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Integration;

/// <summary>
/// Decisión del dueño del 2026-11-12 («tres modos por empresa y línea»; «el bloqueo automático de 30 días sigue mandando»): la venta a crédito a un
/// deudor con mora. La regla pura (<see cref="ReglaDeMoraParaVender"/>) en su matriz —sin mora, dentro de la tolerancia, sobre la tolerancia con
/// cada política, desde el umbral del bloqueo automático y con un tope duro— y, sobre el escenario de ventas a crédito, la consulta y la reserva de
/// Cartera, que comparten la regla: la tolerancia y la política de la línea pisan las generales, la confirmación de quien cobra vale si vio al menos
/// los días de hoy, la aprobación vale sólo con su huella, y un bloqueo vigente manda sobre cualquier política.
/// </summary>
public sealed class MoraParaVenderTests : IAsyncLifetime
{
    private VentasACreditoDePrueba _v = null!;

    public async Task InitializeAsync()
    {
        _v = await VentasACreditoDePrueba.CrearAsync();
        _v.G.ActuarComo(_v.Cajera);
    }

    public Task DisposeAsync()
    {
        _v.Dispose();
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------------------------------------- la regla pura --

    [Theory]
    // días, tolerancia, política, umbral del bloqueo automático → acción
    [InlineData(0, 0, "Bloquear", 30, AccionesPorMora.None)]
    [InlineData(0, 0, "Aprobacion", 30, AccionesPorMora.None)]
    [InlineData(10, 15, "Bloquear", 30, AccionesPorMora.None)]
    [InlineData(15, 15, "Advertir", 30, AccionesPorMora.None)]
    [InlineData(16, 15, "Bloquear", 30, AccionesPorMora.Blocked)]
    [InlineData(16, 15, "Advertir", 30, AccionesPorMora.Confirm)]
    [InlineData(16, 15, "Aprobacion", 30, AccionesPorMora.Approve)]
    [InlineData(5, 0, "", 30, AccionesPorMora.Blocked)]
    [InlineData(5, 0, "Otra", 30, AccionesPorMora.Blocked)]
    [InlineData(30, 0, "Advertir", 30, AccionesPorMora.Blocked)]
    [InlineData(45, 60, "Aprobacion", 30, AccionesPorMora.Blocked)]
    [InlineData(45, 0, "Aprobacion", 0, AccionesPorMora.Approve)]
    public void La_accion_sale_de_los_dias_la_tolerancia_la_politica_y_el_bloqueo_automatico(int dias, int tolerancia, string politica, int umbral,
        string esperada)
    {
        ReglaDeMoraParaVender.Accion(false, dias, tolerancia, politica, umbral).Should().Be(esperada);
    }

    [Theory]
    [InlineData("Bloquear")]
    [InlineData("Advertir")]
    [InlineData("Aprobacion")]
    public void Un_tope_duro_bloquea_con_cualquier_politica_y_aun_sin_mora(string politica)
    {
        ReglaDeMoraParaVender.Accion(true, 0, 0, politica, 30).Should().Be(AccionesPorMora.Blocked);
        ReglaDeMoraParaVender.Accion(true, 10, 15, politica, 30).Should().Be(AccionesPorMora.Blocked);
    }

    [Theory]
    // acción, días, confirmados, aprobada → procede
    [InlineData(AccionesPorMora.None, 0, null, false, true)]
    [InlineData(AccionesPorMora.Confirm, 20, null, false, false)]
    [InlineData(AccionesPorMora.Confirm, 20, 19, false, false)]
    [InlineData(AccionesPorMora.Confirm, 20, 20, false, true)]
    [InlineData(AccionesPorMora.Confirm, 20, null, true, true)]
    [InlineData(AccionesPorMora.Approve, 20, 20, false, false)]
    [InlineData(AccionesPorMora.Approve, 20, null, true, true)]
    [InlineData(AccionesPorMora.Blocked, 20, 20, true, false)]
    public void La_evidencia_alcanza_segun_la_accion(string accion, int dias, int? confirmados, bool aprobada, bool procede)
    {
        ReglaDeMoraParaVender.Permite(accion, dias, confirmados, aprobada).Should().Be(procede);
    }

    [Fact]
    public void La_huella_cambia_con_el_valor_la_linea_la_fecha_y_los_dias()
    {
        var persona = Guid.NewGuid();
        var dia = new DateOnly(2026, 11, 12);
        var base_ = ReglaDeMoraParaVender.Huella(persona, PaymentMeansClass.AssociateCredit, 100_000m, "conlib", dia, 40);

        ReglaDeMoraParaVender.Huella(persona, PaymentMeansClass.AssociateCredit, 100_000m, " CONLIB ", dia, 40).Should().Be(base_);
        ReglaDeMoraParaVender.Huella(persona, PaymentMeansClass.AssociateCredit, 100_001m, "CONLIB", dia, 40).Should().NotBe(base_);
        ReglaDeMoraParaVender.Huella(persona, PaymentMeansClass.AssociateCredit, 100_000m, "OTRA", dia, 40).Should().NotBe(base_);
        ReglaDeMoraParaVender.Huella(persona, PaymentMeansClass.AssociateCredit, 100_000m, "CONLIB", dia.AddDays(1), 40).Should().NotBe(base_);
        ReglaDeMoraParaVender.Huella(persona, PaymentMeansClass.AssociateCredit, 100_000m, "CONLIB", dia, 41).Should().NotBe(base_);
    }

    [Fact]
    public void La_politica_es_un_parametro_de_Cartera_por_empresa_y_linea_que_arranca_en_Bloquear()
    {
        var politica = Domain.Common.Parametros.CatalogoDeParametros.Buscar("LND", ParametrosDeCartera.CupoPoliticaDeMoraParaVender)!;
        politica.DefectoSeguro.Should().Be(PoliticasDeMoraParaVender.Bloquear);
        politica.AmbitosAdmitidos.Should().BeEquivalentTo([ParameterScopeKind.None, ParameterScopeKind.CreditLine]);
        politica.PendienteDeValidar.Should().NotBeNullOrWhiteSpace();
        var tolerancia = Domain.Common.Parametros.CatalogoDeParametros.Buscar("LND", ParametrosDeCartera.CupoMoraMaximaParaVender)!;
        tolerancia.AmbitosAdmitidos.Should().BeEquivalentTo([ParameterScopeKind.None, ParameterScopeKind.CreditLine]);
    }

    // ------------------------------------------------------------------------------------------------ la consulta --

    private Task<EstadoCrediticioDto> ConsultarAsync(string? linea = null, int? confirmados = null, Guid? aprobacion = null, decimal monto = 300_000m) =>
        _v.Consultas().EstadoCrediticioAsync(new ConsultaCrediticia(_v.Ana.PublicId, PaymentMeansClass.AssociateCredit, monto, VentasACreditoDePrueba.Hoy,
            "MOSTRADOR") { CreditLineCode = linea, ArrearsAcknowledgedDays = confirmados, ArrearsApprovalPublicId = aprobacion }, default);

    [Fact]
    public async Task Sin_parametros_sobre_la_tolerancia_rige_Bloquear_como_siempre()
    {
        Mora(12);

        var e = await ConsultarAsync();

        (e.Status, e.DaysPastDue, e.ArrearsAction, e.AllowsSale).Should().Be((EstadosCrediticios.InArrears, (int?)12, AccionesPorMora.Blocked, false));
        e.BlockReason.Should().Contain("12 días de mora");
    }

    [Fact]
    public async Task Dentro_de_la_tolerancia_se_vende_sin_mas_tramite()
    {
        Mora(12);
        await ParametroAsync(ParametrosDeCartera.CupoMoraMaximaParaVender, "15");

        var e = await ConsultarAsync();

        (e.ArrearsAction, e.AllowsSale, e.ArrearsToleranceDays).Should().Be((AccionesPorMora.None, true, (int?)15));
    }

    [Fact]
    public async Task Con_Advertir_procede_si_quien_cobra_confirmo_al_menos_los_dias_de_hoy()
    {
        Mora(12);
        await ParametroAsync(ParametrosDeCartera.CupoPoliticaDeMoraParaVender, PoliticasDeMoraParaVender.Advertir);

        var sin = await ConsultarAsync();
        (sin.ArrearsAction, sin.AllowsSale).Should().Be((AccionesPorMora.Confirm, false));
        (await ConsultarAsync(confirmados: 11)).AllowsSale.Should().BeFalse("vio menos días de los que hay");
        (await ConsultarAsync(confirmados: 12)).AllowsSale.Should().BeTrue();
    }

    [Fact]
    public async Task Con_Aprobacion_procede_solo_con_una_aprobada_de_la_misma_huella()
    {
        Mora(12);
        await ParametroAsync(ParametrosDeCartera.CupoPoliticaDeMoraParaVender, PoliticasDeMoraParaVender.Aprobacion);

        (await ConsultarAsync()).ArrearsAction.Should().Be(AccionesPorMora.Approve);
        var buena = await AprobacionAsync(ReglaDeMoraParaVender.Huella(_v.Ana.PublicId, PaymentMeansClass.AssociateCredit, 300_000m, null, VentasACreditoDePrueba.Hoy, 12));
        var deOtroValor = await AprobacionAsync(ReglaDeMoraParaVender.Huella(_v.Ana.PublicId, PaymentMeansClass.AssociateCredit, 999_000m, null, VentasACreditoDePrueba.Hoy, 12));
        var pendiente = await AprobacionAsync(ReglaDeMoraParaVender.Huella(_v.Ana.PublicId, PaymentMeansClass.AssociateCredit, 300_000m, null, VentasACreditoDePrueba.Hoy, 12),
            ApprovalRequestStatus.Pending);

        (await ConsultarAsync(aprobacion: buena)).AllowsSale.Should().BeTrue();
        (await ConsultarAsync(aprobacion: deOtroValor)).AllowsSale.Should().BeFalse("la huella es de otro valor");
        (await ConsultarAsync(aprobacion: pendiente)).AllowsSale.Should().BeFalse("todavía no la aprobaron");
        (await ConsultarAsync(confirmados: 12)).AllowsSale.Should().BeFalse("con «Aprobacion» la confirmación de quien cobra no alcanza");
    }

    [Fact]
    public async Task La_tolerancia_y_la_politica_de_la_linea_pisan_las_generales()
    {
        Mora(12);
        var linea = await _v.Db.CreditLines.AsNoTracking().Where(l => l.Code == "CONLIB").Select(l => l.Id).SingleAsync();
        await ParametroAsync(ParametrosDeCartera.CupoPoliticaDeMoraParaVender, PoliticasDeMoraParaVender.Advertir, linea);

        (await ConsultarAsync()).ArrearsAction.Should().Be(AccionesPorMora.Blocked, "sin línea rige la general (Bloquear)");
        (await ConsultarAsync("CONLIB")).ArrearsAction.Should().Be(AccionesPorMora.Confirm, "la línea dice Advertir");

        await ParametroAsync(ParametrosDeCartera.CupoMoraMaximaParaVender, "20", linea);
        var dentro = await ConsultarAsync("CONLIB");
        (dentro.ArrearsAction, dentro.AllowsSale, dentro.ArrearsToleranceDays).Should().Be((AccionesPorMora.None, true, (int?)20));
        (await ConsultarAsync()).ArrearsAction.Should().Be(AccionesPorMora.Blocked, "la tolerancia general sigue en 0");
    }

    [Fact]
    public async Task Desde_el_umbral_del_bloqueo_automatico_no_se_vende_con_ninguna_politica()
    {
        Mora(30);
        await ParametroAsync(ParametrosDeCartera.CupoPoliticaDeMoraParaVender, PoliticasDeMoraParaVender.Advertir);
        await ParametroAsync(ParametrosDeCartera.CupoMoraMaximaParaVender, "60");

        var e = await ConsultarAsync(confirmados: 30);

        (e.ArrearsAction, e.AllowsSale).Should().Be((AccionesPorMora.Blocked, false), "el bloqueo automático de 30 días manda aunque el ciclo no lo haya creado");
        e.BlockReason.Should().Contain("desde 30 días");
    }

    [Fact]
    public async Task Un_bloqueo_vigente_manda_sobre_cualquier_politica()
    {
        Mora(12);
        await ParametroAsync(ParametrosDeCartera.CupoPoliticaDeMoraParaVender, PoliticasDeMoraParaVender.Advertir);
        _v.Db.DebtorBlocks.Add(DebtorBlock.Nuevo(_v.Ana.Id, DebtorBlockReason.Arrears, "mora:1", VentasACreditoDePrueba.Hoy.AddDays(-1), null, null));
        _v.Db.SaveChanges();

        var e = await ConsultarAsync(confirmados: 12);

        (e.Status, e.ArrearsAction, e.AllowsSale).Should().Be((EstadosCrediticios.Blocked, AccionesPorMora.Blocked, false));
    }

    // ------------------------------------------------------------------------------------------------- la reserva --

    private SolicitudDeReserva Solicitud(int? confirmados = null, Guid? aprobacion = null, decimal monto = 300_000m)
    {
        var p = Guid.NewGuid();
        return new SolicitudDeReserva(SolicitudDeReservaValidator.ClaveDeReserva(p, monto), p, Guid.NewGuid(), _v.Ana.PublicId,
            PaymentMeansClass.AssociateCredit, monto, VentasACreditoDePrueba.Hoy, "CONLIB", "MOSTRADOR")
        {
            ArrearsAcknowledgedDays = confirmados,
            ArrearsApprovalPublicId = aprobacion,
        };
    }

    private async Task<ResultadoDeReservaDto> ReservarAsync(SolicitudDeReserva s)
    {
        var r = await _v.Reservas().ReservarAsync(s, default);
        await _v.Db.SaveChangesAsync();
        _v.Db.ChangeTracker.Clear();
        return r;
    }

    [Fact]
    public async Task La_reserva_exige_la_misma_evidencia_que_la_consulta()
    {
        Mora(12);
        await ParametroAsync(ParametrosDeCartera.CupoPoliticaDeMoraParaVender, PoliticasDeMoraParaVender.Advertir);

        var sin = await ReservarAsync(Solicitud());
        (sin.Status, sin.Code).Should().Be((EstadosDeReserva.Rejected, ErroresDeVentasACredito.CodigoReservaBloqueada), "la pantalla no se la puede saltar");
        sin.Message.Should().Contain("confirmar");

        var con = await ReservarAsync(Solicitud(confirmados: 12));
        con.Status.Should().Be(EstadosDeReserva.Reserved);
    }

    [Fact]
    public async Task La_reserva_con_Aprobacion_reserva_con_la_aprobada_de_su_linea()
    {
        Mora(12);
        await ParametroAsync(ParametrosDeCartera.CupoPoliticaDeMoraParaVender, PoliticasDeMoraParaVender.Aprobacion);
        var aprobada = await AprobacionAsync(ReglaDeMoraParaVender.Huella(_v.Ana.PublicId, PaymentMeansClass.AssociateCredit, 300_000m, "CONLIB",
            VentasACreditoDePrueba.Hoy, 12));

        (await ReservarAsync(Solicitud(confirmados: 12))).Status.Should().Be(EstadosDeReserva.Rejected);
        (await ReservarAsync(Solicitud(aprobacion: aprobada))).Status.Should().Be(EstadosDeReserva.Reserved);
    }

    // --------------------------------------------------------------------------------------------------- apoyo --

    /// <summary>Un crédito vivo de Ana con <paramref name="dias"/> días de mora en su posición de ayer.</summary>
    private void Mora(int dias)
    {
        var credito = _v.O.Credito(_v.Ana, 1_000_000m, $"CR-MORA-{dias}");
        _v.Db.LoanDailyPositions.Add(new LoanDailyPosition
        {
            LoanId = credito.Id, PositionDate = VentasACreditoDePrueba.Hoy.AddDays(-1), Version = 1, RegisteredAt = _v.G.UtcNow, Status = LoanStatus.Active,
            BranchId = _v.O.Norte.Id, CreditLineId = 1, ModalityCode = "CONSUMO", Principal = 1_000_000m, DaysPastDue = dias, InputsSha256 = "x",
        });
        _v.Db.SaveChanges();
        _v.Db.ChangeTracker.Clear();
    }

    private async Task ParametroAsync(string clave, string valor, int? linea = null)
    {
        _v.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = clave, ScopeKind = linea is null ? ParameterScopeKind.None : ParameterScopeKind.CreditLine,
            ScopeId = linea ?? 0, Value = valor, ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba", CreatedAt = _v.G.UtcNow.AddDays(-10),
        });
        await _v.Db.SaveChangesAsync();
        _v.Db.ChangeTracker.Clear();
    }

    private async Task<Guid> AprobacionAsync(string huella, ApprovalRequestStatus estado = ApprovalRequestStatus.Approved)
    {
        var solicitud = new ApprovalRequest
        {
            Subject = ApprovalSubjects.CreditInArrears, SourceType = ApprovalSourceTypes.SaleCreditInArrears, SourcePublicId = Guid.NewGuid(),
            SourceLabel = "Crédito con mora", Amount = 300_000m, OperationDate = VentasACreditoDePrueba.Hoy, CreatedByUserId = _v.Cajera.Id,
            RequestedByUserId = _v.Cajera.Id, Status = estado, CurrentLevel = 1, ContentSha256 = huella, RequestedAt = _v.G.UtcNow,
        };
        _v.Db.ApprovalRequests.Add(solicitud);
        await _v.Db.SaveChangesAsync();
        _v.Db.ChangeTracker.Clear();
        return solicitud.PublicId;
    }
}
