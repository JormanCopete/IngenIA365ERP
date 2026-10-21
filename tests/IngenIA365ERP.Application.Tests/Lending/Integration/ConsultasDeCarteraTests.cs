using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Projections;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Integration;

/// <summary>
/// Feature 013, US6, T0567 (FR-058, FR-059, FR-060, C31, C102; interfaces.md §5.1): <see cref="ConsultasDeCartera"/>, la implementación real de
/// <c>IConsultasDeCartera</c>: el estado del deudor (activo, en mora con sus días, bloqueado con el motivo en español y sin detalle de listas, sin
/// relación), si admite la venta por estado y <c>Cupo.MoraMaximaParaVender</c>, el cupo total y el disponible (cupo − saldos vivos − reservas − aprobadas
/// sin desembolsar, como deudor y codeudor), las líneas comerciales con sus periodicidades de Comercial, las listas y la autorización <b>sin escribir</b>,
/// la lectura auditada que da la evidencia, <c>NotEnabled</c> antes de activar, y el estado de validación por el recibo (la tabla de §5.1).
/// </summary>
public sealed class ConsultasDeCarteraTests : IAsyncLifetime
{
    private VentasACreditoDePrueba _v = null!;

    public async Task InitializeAsync() => _v = await VentasACreditoDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _v.Dispose();
        return Task.CompletedTask;
    }

    private Task<EstadoCrediticioDto> ConsultarAsync(decimal monto = 300_000m, PaymentMeansClass clase = PaymentMeansClass.AssociateCredit,
        Domain.Entities.Core.Person? persona = null) =>
        _v.Consultas().EstadoCrediticioAsync(new ConsultaCrediticia((persona ?? _v.Ana).PublicId, clase, monto, VentasACreditoDePrueba.Hoy, "MOSTRADOR"), default);

    // ---------------------------------------------------------------------------------------------------------- estado --

    [Fact]
    public async Task Un_asociado_al_dia_esta_activo_admite_la_venta_y_ve_las_lineas_comerciales_con_sus_periodicidades()
    {
        var e = await ConsultarAsync();

        (e.Status, e.AllowsSale, e.LendingEnabled).Should().Be((EstadosCrediticios.Active, true, true));
        var linea = e.Lines.Should().ContainSingle().Subject;
        (linea.Code, linea.MaxTermMonths).Should().Be(("CONLIB", 60));
        linea.Periodicities.Should().Equal("MONTHLY");
    }

    [Fact]
    public async Task Un_cliente_no_ve_lineas_que_no_admiten_no_asociados()
    {
        var e = await ConsultarAsync(clase: PaymentMeansClass.CustomerCredit);

        e.Lines.Should().BeEmpty("la línea comercial de la prueba no admite no asociados");
    }

    [Fact]
    public async Task Sin_ser_asociado_ni_tener_cupo_no_tiene_relacion()
    {
        var e = await ConsultarAsync(persona: _v.O.Carla, clase: PaymentMeansClass.CustomerCredit);

        (e.Status, e.AllowsSale).Should().Be((EstadosCrediticios.NoRelationship, false));
    }

    [Fact]
    public async Task Bloqueado_por_un_bloqueo_vigente_con_el_motivo_en_espanol()
    {
        _v.Db.DebtorBlocks.Add(DebtorBlock.Nuevo(_v.Ana.Id, DebtorBlockReason.Garnishment, "manual:1", VentasACreditoDePrueba.Hoy.AddDays(-3), "Embargo judicial", null));
        _v.Db.SaveChanges();

        var e = await ConsultarAsync();

        (e.Status, e.AllowsSale).Should().Be((EstadosCrediticios.Blocked, false));
        e.BlockReason.Should().Be("Tiene un embargo registrado.");
    }

    [Fact]
    public async Task Una_coincidencia_en_listas_bloquea_sin_detalle_y_sin_escribir()
    {
        _v.Verificacion.CoincideAsync(_v.Ana.Id, Arg.Any<CancellationToken>()).Returns(true);

        var e = await ConsultarAsync();

        (e.Status, e.AllowsSale).Should().Be((EstadosCrediticios.Blocked, false));
        e.BlockReason.Should().Contain("oficial de cumplimiento").And.NotContain("OFAC");
        (await _v.Db.ScreeningResults.CountAsync()).Should().Be(0, "la consulta verifica listas sin escribir (C31)");
        (await _v.Db.DebtorBlocks.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Sin_autorizacion_de_tratamiento_para_credito_queda_bloqueado()
    {
        _v.O.QuitarAutorizacion(_v.Ana, DataAuthorizationPurpose.CreditProcessing);

        var e = await ConsultarAsync();

        (e.Status, e.AllowsSale).Should().Be((EstadosCrediticios.Blocked, false));
        e.BlockReason.Should().Contain("autorización");
    }

    [Fact]
    public async Task En_mora_dice_los_dias_y_admite_la_venta_segun_Cupo_MoraMaximaParaVender()
    {
        var credito = _v.O.Credito(_v.Ana, 1_000_000m, "CR-MORA");
        _v.Db.LoanDailyPositions.Add(new LoanDailyPosition
        {
            LoanId = credito.Id, PositionDate = VentasACreditoDePrueba.Hoy.AddDays(-1), Version = 1, RegisteredAt = _v.G.UtcNow, Status = LoanStatus.Active,
            BranchId = _v.O.Norte.Id, CreditLineId = 1, ModalityCode = "CONSUMO", Principal = 1_000_000m, DaysPastDue = 12, InputsSha256 = "x",
        });
        _v.Db.SaveChanges();

        var sinTolerancia = await ConsultarAsync();
        (sinTolerancia.Status, sinTolerancia.DaysPastDue, sinTolerancia.AllowsSale).Should().Be((EstadosCrediticios.InArrears, (int?)12, false),
            "con el defecto (0) en mora no se vende");

        await ParametroAsync(ParametrosDeCartera.CupoMoraMaximaParaVender, "15");
        (await ConsultarAsync()).AllowsSale.Should().BeTrue("12 días caben en los 15 que admite la cooperativa");
    }

    // ------------------------------------------------------------------------------------------------------------ cupo --

    [Fact]
    public async Task El_disponible_resta_saldos_vivos_reservas_y_lo_de_codeudor()
    {
        var rotativo = _v.Rotativo(_v.Ana, 5_000_000m);
        _v.O.Credito(_v.O.Beto, 700_000m, "CR-BETO", _v.Ana);
        _v.Db.LoanNumberReservations.Add(new LoanNumberReservation
        {
            Number = "CR-R1", PersonId = _v.Ana.Id, Amount = 300_000m, CreditLimitId = rotativo.Id, IdempotencyKey = "INV:r1", RequestSha256 = new string('a', 64),
            SalesDocumentPublicId = Guid.NewGuid(), PaymentPublicId = Guid.NewGuid(), Status = LoanNumberReservationStatus.Reserved, ReservedAt = _v.G.UtcNow,
            ReservedByUserId = _v.Cajera.Id, ExpiresOn = VentasACreditoDePrueba.Hoy.AddDays(3),
        });
        _v.Db.SaveChanges();

        var e = await ConsultarAsync();

        e.TotalQuota.Should().Be(5_000_000m);
        e.AvailableQuota.Should().Be(5_000_000m - 300_000m, "el rotativo cuenta las reservas vigentes; lo de codeudor pesa en el consolidado (FR-058)");
    }

    [Fact]
    public async Task El_consolidado_cuenta_lo_que_la_persona_debe_como_codeudora()
    {
        _v.Db.CreditLimits.Add(CreditLimit.NuevaVersion(_v.Ana.Id, CreditLimitKind.Consolidated, null, 5_000_000m, CreditLimitOrigin.Assigned,
            new DateOnly(2026, 1, 1), 1, _v.G.UtcNow.AddDays(-30), "Consolidado", null));
        _v.Db.SaveChanges();
        _v.O.Credito(_v.O.Beto, 700_000m, "CR-BETO", _v.Ana);

        var e = await ConsultarAsync();

        e.AvailableQuota.Should().Be(4_300_000m, "como deudor y como codeudor (FR-058)");
    }

    // --------------------------------------------------------------------------------------------------- evidencia --

    [Fact]
    public async Task Deja_la_lectura_auditada_y_su_EventId_es_la_evidencia()
    {
        var e = await ConsultarAsync();

        e.EvidenceId.Should().NotBeNullOrEmpty();
        var fila = await _v.Db.AuditOutbox.AsNoTracking().SingleAsync();
        fila.EventId.ToString().Should().Be(e.EvidenceId);
        (await _v.G.EventosAsync()).Should().Equal("Lending.CreditStatus.Queried");
    }

    [Fact]
    public async Task Antes_de_activar_responde_NotEnabled()
    {
        var setup = await _v.Db.LendingSetups.SingleAsync();
        setup.ActivationDate = null;
        await _v.Db.SaveChangesAsync();

        var e = await ConsultarAsync();

        (e.Status, e.LendingEnabled).Should().Be((EstadosCrediticios.NotEnabled, false));
    }

    // -------------------------------------------------------------------------------------------- estado de validación --

    [Fact]
    public async Task El_estado_de_validacion_sigue_la_tabla_del_recibo()
    {
        (await _v.Consultas().EstadoDeValidacionAsync(Guid.NewGuid(), default)).Status.Should().Be(EstadosDeValidacion.Pending, "sin recibo todavía");

        var casos = new (InboundMessageStatus Estado, DateTime? NoReprocesable, string Esperado)[]
        {
            (InboundMessageStatus.Processed, null, EstadosDeValidacion.Validated),
            (InboundMessageStatus.ConfirmedByException, null, EstadosDeValidacion.Validated),
            (InboundMessageStatus.ValidationFailed, null, EstadosDeValidacion.Failed),
            (InboundMessageStatus.NotIncorporated, null, EstadosDeValidacion.NotIncorporated),
            (InboundMessageStatus.Rejected, null, EstadosDeValidacion.Pending),
            (InboundMessageStatus.Rejected, _v.G.UtcNow, EstadosDeValidacion.NotIncorporated),
        };
        foreach (var (estado, noReprocesable, esperado) in casos)
        {
            var recibo = new LendingInboundMessage
            {
                MessagePublicId = Guid.NewGuid(), MessageType = "VentaACreditoRegistrada", MessageVersion = 2, OriginModule = "INV", OriginPublicId = Guid.NewGuid(),
                OriginEventKey = "Confirmation", ChainRootPublicId = Guid.NewGuid(), OperationDate = VentasACreditoDePrueba.Hoy, Status = estado,
                NotRetryableAt = noReprocesable, NotRetryableReason = noReprocesable is null ? null : "No se va a reprocesar",
                ExceptionReason = estado == InboundMessageStatus.ConfirmedByException ? "Cliente de confianza" : null,
                FirstReceivedAt = _v.G.UtcNow, LastProcessedAt = _v.G.UtcNow,
            };
            _v.Db.LendingInboundMessages.Add(recibo);
            await _v.Db.SaveChangesAsync();

            var r = await _v.Consultas().EstadoDeValidacionAsync(recibo.MessagePublicId, default);

            r.Status.Should().Be(esperado, $"{estado}{(noReprocesable is null ? string.Empty : " no reprocesable")}");
            if (estado == InboundMessageStatus.ConfirmedByException) r.Reason.Should().Contain("confirmada por excepción").And.Contain("Cliente de confianza");
            if (estado == InboundMessageStatus.Rejected && noReprocesable is null) r.Reason.Should().Contain("reproceso");
        }
    }

    private async Task ParametroAsync(string clave, string valor)
    {
        _v.Db.ParameterVersions.Add(new Domain.Entities.Parameters.ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = clave, ScopeKind = Domain.Enums.Parameters.ParameterScopeKind.None, Value = valor,
            ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba", CreatedAt = _v.G.UtcNow.AddDays(-10),
        });
        await _v.Db.SaveChangesAsync();
        _v.Db.ChangeTracker.Clear();
    }
}
