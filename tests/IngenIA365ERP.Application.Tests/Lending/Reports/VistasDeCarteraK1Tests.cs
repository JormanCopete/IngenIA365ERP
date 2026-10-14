using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Application.Lending.Payments.Commands;
using IngenIA365ERP.Application.Lending.Payments.Queries;
using IngenIA365ERP.Application.Lending.Reports;
using IngenIA365ERP.Application.Lending.Reports.Vistas;
using IngenIA365ERP.Application.Tests.Lending.Payments;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Lending.Projections;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Reports;

/// <summary>
/// T0772 (feature 013, US15, K1; FR-133; contracts/api.md §17): las siete vistas de K1 sobre el escenario de <see cref="InformesDePrueba"/> (el
/// crédito de Ana sembrado por el registro del libro). <c>aging</c> con los rangos de <c>Informes.RangosDeEdad</c> o los de <c>ranges</c>, el saldo
/// de capital y la calificación informativa; <c>placement</c> por línea, oficina, asesor y mes con la tasa y el plazo ponderados; <c>receipts</c>
/// por línea, recaudador, modalidad y clase de medio con los reversos aparte; <c>transactions</c> por fecha valor, contable y de registro;
/// <c>usury</c> con sus dos secciones y <c>Informes.PuntosAlertaUsura</c>; <c>debtor-messages</c> con el motivo de exclusión o diferimiento;
/// <c>migration-reconciliation</c> con sus dos secciones; y las columnas ocultas presentes en la tabla y fuera de lo que se exporta.
/// </summary>
public class VistasDeCarteraK1Tests : IAsyncLifetime
{
    private static readonly DateOnly Hoy = InformesDePrueba.Hoy;

    private InformesDePrueba _e = null!;

    public async Task InitializeAsync() => _e = await InformesDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _e.Dispose();
        return Task.CompletedTask;
    }

    private static FiltrosDeInformeDeCartera Filtros(DateOnly? desde = null, DateOnly? hasta = null, DateOnly? corte = null) =>
        new() { From = desde, To = hasta, AsOf = corte };

    private static object? V(TablaExportable t, FilaExportable f, string columna) => InformesDePrueba.Valor(t, f, columna);

    // ---------------------------------------------------------------------------------------------------------------- aging --

    [Fact]
    public async Task Aging_agrupa_por_los_rangos_de_la_cooperativa_con_saldo_y_calificacion_informativa()
    {
        var t = InformesDePrueba.Tabla(await _e.Edades().Handle(new AgingReportQuery(Filtros()), default));

        var fila = t.Filas.Should().ContainSingle().Subject;
        V(t, fila, "Crédito").Should().Be(_e.Credito.Number);
        V(t, fila, "Saldo de capital").Should().Be(3_000_000m);
        V(t, fila, "Días de mora").Should().Be(0);
        V(t, fila, "Rango").Should().Be("Al día");
        fila.Seccion.Should().Be("Al día");
        ((string)V(t, fila, "Calificación")!).Should().StartWith("A", "al día la calificación informativa es A, del mismo motor del cierre");
        V(t, fila, "_credito").Should().Be(_e.Credito.PublicId.ToString());
        V(t, fila, "_persona").Should().Be(_e.D.O.Ana.PublicId.ToString());
        t.Totales!.Valores[InformesDePrueba.Indice(t, "Saldo de capital")].Should().Be(3_000_000m);
        t.Notas.Should().Contain(n => n.Contains("1 a 30 · 31 a 60 · 61 a 90 · 91 a 180 · 181 a 360 · Más de 360") && n.Contains("Informes.RangosDeEdad"));
    }

    [Fact]
    public async Task Aging_toma_la_mora_al_corte_y_admite_rangos_propios()
    {
        var vencimiento = (await _e.Db.RepaymentInstallments.AsNoTracking().OrderBy(i => i.Id).FirstAsync()).DueDate;
        var corte = vencimiento.AddDays(45);
        await _e.P.CausarHastaAsync(corte);

        var porDefecto = InformesDePrueba.Tabla(await _e.Edades().Handle(new AgingReportQuery(Filtros(corte: corte)), default));
        V(porDefecto, porDefecto.Filas.Single(), "Días de mora").Should().Be(45);
        V(porDefecto, porDefecto.Filas.Single(), "Rango").Should().Be("31 a 60");
        ((string)V(porDefecto, porDefecto.Filas.Single(), "Calificación")!).Should().StartWith("B", "45 días en consumo es B en el Anexo 1");

        await _e.ParametroAsync(ParametrosDeCartera.InformesRangosDeEdad, "60,120");
        var configurados = InformesDePrueba.Tabla(await _e.Edades().Handle(new AgingReportQuery(Filtros(corte: corte)), default));
        V(configurados, configurados.Filas.Single(), "Rango").Should().Be("1 a 60", "los rangos vigentes de Informes.RangosDeEdad");

        var propios = InformesDePrueba.Tabla(await _e.Edades().Handle(new AgingReportQuery(Filtros(corte: corte), "10,20"), default));
        V(propios, propios.Filas.Single(), "Rango").Should().Be("Más de 20");

        var antes = InformesDePrueba.Tabla(await _e.Edades().Handle(new AgingReportQuery(Filtros(corte: vencimiento.AddDays(-1))), default));
        V(antes, antes.Filas.Single(), "Días de mora").Should().Be(0, "la posición vigente a ese corte todavía estaba al día");
    }

    // ------------------------------------------------------------------------------------------------------------ placement --

    [Fact]
    public async Task Placement_abre_por_linea_oficina_asesor_y_mes_con_tasa_y_plazo_ponderados()
    {
        var linea = await _e.Db.CreditLines.AsNoTracking().SingleAsync(l => l.Id == _e.Credito.CreditLineId);
        var oficina = await _e.Db.Branches.AsNoTracking().SingleAsync(b => b.Id == _e.Credito.BranchId);

        var porLinea = InformesDePrueba.Tabla(await _e.Colocacion().Handle(new PlacementReportQuery(Filtros()), default));
        var fila = porLinea.Filas.Should().ContainSingle().Subject;
        V(porLinea, fila, "Dimensión").Should().Be($"{linea.Code} · {linea.Name}");
        V(porLinea, fila, "Créditos").Should().Be(1);
        V(porLinea, fila, "Monto desembolsado").Should().Be(3_000_000m);
        V(porLinea, fila, "Tasa promedio").Should().Be(20m, "20 % EA pactada");
        V(porLinea, fila, "Plazo promedio").Should().Be(24m);
        porLinea.Columnas.Should().NotContain(c => c.EsOculta, "agregada: sin datos del deudor");

        var porOficina = InformesDePrueba.Tabla(await _e.Colocacion().Handle(new PlacementReportQuery(Filtros(), "office"), default));
        V(porOficina, porOficina.Filas.Single(), "Dimensión").Should().Be(oficina.Name);

        var porAsesor = InformesDePrueba.Tabla(await _e.Colocacion().Handle(new PlacementReportQuery(Filtros(), "advisor"), default));
        ((string)V(porAsesor, porAsesor.Filas.Single(), "Dimensión")!).Should().NotBe(PlacementReportQueryHandler.SinAsesor, "la solicitud la radicó la asesora");

        var porMes = InformesDePrueba.Tabla(await _e.Colocacion().Handle(new PlacementReportQuery(Filtros(), "month"), default));
        V(porMes, porMes.Filas.Single(), "Dimensión").Should().Be("2026-12");

        var otroMes = InformesDePrueba.Tabla(await _e.Colocacion().Handle(new PlacementReportQuery(Filtros(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30))), default));
        otroMes.Filas.Should().BeEmpty();
    }

    // ------------------------------------------------------------------------------------------------------------- receipts --

    [Fact]
    public async Task Receipts_abre_por_linea_recaudador_modalidad_y_clase_de_medio_con_los_reversos_aparte()
    {
        var (primero, _) = await PagosEnPantalla.DosPagosAsync(_e.P);
        var huella = (await _e.P.VistaPrevia().Handle(new PreviewLoanPaymentReversalQuery(_e.Credito.PublicId, primero), default)).Value.PreviewSha256;
        var reverso = await _e.P.Reversar().Handle(new ReverseLoanPaymentCommand(_e.Credito.PublicId, primero, "Cheque devuelto", huella)
            { OperationKey = Guid.NewGuid() }, default);
        reverso.IsSuccess.Should().BeTrue(reverso.IsFailure ? reverso.Error.Message : string.Empty);
        _e.Db.ChangeTracker.Clear();
        var rango = Filtros(Hoy, _e.G.HoyLocal.AddDays(1));

        var porLinea = InformesDePrueba.Tabla(await _e.Recaudo().Handle(new ReceiptsReportQuery(rango), default));
        var fila = porLinea.Filas.Should().ContainSingle().Subject;
        V(porLinea, fila, "Pagos").Should().Be(2);
        V(porLinea, fila, "Total").Should().Be(300_000m);
        ((decimal)V(porLinea, fila, "Capital")! + (decimal)V(porLinea, fila, "Intereses")! + (decimal)V(porLinea, fila, "Otros")!).Should().Be(300_000m);
        ((decimal)V(porLinea, fila, "Intereses")!).Should().BePositive();
        V(porLinea, fila, "Reversos").Should().Be(100_000m, "el reverso va aparte; el pago reversado sigue contando");

        var porRecaudador = InformesDePrueba.Tabla(await _e.Recaudo().Handle(new ReceiptsReportQuery(rango, "collector"), default));
        V(porRecaudador, porRecaudador.Filas.Single(), "Dimensión").Should().Be(ClavesDeRecaudador.Prueba);

        var porModalidad = InformesDePrueba.Tabla(await _e.Recaudo().Handle(new ReceiptsReportQuery(rango, "modality"), default));
        V(porModalidad, porModalidad.Filas.Single(), "Dimensión").Should().Be("Valor libre");

        var porMedio = InformesDePrueba.Tabla(await _e.Recaudo().Handle(new ReceiptsReportQuery(rango, "meansClass"), default));
        porMedio.Filas.Should().ContainSingle();

        var deOtro = InformesDePrueba.Tabla(await _e.Recaudo().Handle(new ReceiptsReportQuery(new FiltrosDeInformeDeCartera
            { From = rango.From, To = rango.To, Collector = "NOM" }), default));
        deOtro.Filas.Should().BeEmpty("sólo los del recaudador pedido");
    }

    // --------------------------------------------------------------------------------------------------------- transactions --

    [Fact]
    public async Task Transactions_filtra_por_fecha_valor_contable_o_de_registro_con_su_operacion()
    {
        var porValor = InformesDePrueba.Tabla(await _e.Movimientos().Handle(new TransactionsReportQuery(Filtros(Hoy, Hoy)), default));
        porValor.Filas.Should().NotBeEmpty();
        var desembolso = porValor.Filas.First(f => (string)V(porValor, f, "Tipo")! == "Desembolso");
        V(porValor, desembolso, "Crédito").Should().Be(_e.Credito.Number);
        V(porValor, desembolso, "Concepto").Should().Be("Capital");
        ((string)V(porValor, desembolso, "Operación")!).Should().NotBeEmpty();
        V(porValor, desembolso, "_credito").Should().Be(_e.Credito.PublicId.ToString());
        V(porValor, desembolso, "_operacion").Should().NotBeNull();

        var porRegistro = InformesDePrueba.Tabla(await _e.Movimientos().Handle(new TransactionsReportQuery(Filtros(Hoy, Hoy), DateKind: "registered"), default));
        porRegistro.Filas.Should().HaveCount(porValor.Filas.Count);
        var porContable = InformesDePrueba.Tabla(await _e.Movimientos().Handle(new TransactionsReportQuery(Filtros(Hoy, Hoy), DateKind: "accounting"), default));
        porContable.Subtitulo.Should().Contain("fecha contable");

        var soloDesembolso = InformesDePrueba.Tabla(await _e.Movimientos().Handle(new TransactionsReportQuery(Filtros(Hoy, Hoy), _e.Credito.PublicId, "Disbursement"), default));
        soloDesembolso.Filas.Should().OnlyContain(f => (string)V(soloDesembolso, f, "Tipo")! == "Desembolso");
        var otroCredito = InformesDePrueba.Tabla(await _e.Movimientos().Handle(new TransactionsReportQuery(Filtros(Hoy, Hoy), Guid.NewGuid()), default));
        otroCredito.Filas.Should().BeEmpty();
        (await _e.Movimientos().Handle(new TransactionsReportQuery(Filtros(), Kind: "Pago"), default)).Error.Code
            .Should().Be(FiltrosDeInformeDeCartera.FiltroInvalidoCodigo);
    }

    // ---------------------------------------------------------------------------------------------------------------- usury --

    [Fact]
    public async Task Usury_lista_los_creditos_topados_y_las_lineas_a_menos_de_los_puntos_de_alerta()
    {
        // Una posición topada del crédito de Ana hoy (la versión siguiente del día): la proyección como la dejaría el ciclo diario.
        var vigente = await _e.Db.LoanDailyPositions.AsNoTracking().Where(p => p.LoanId == _e.Credito.Id)
            .OrderByDescending(p => p.PositionDate).ThenByDescending(p => p.Version).FirstAsync();
        _e.Db.LoanDailyPositions.Add(new LoanDailyPosition
        {
            LoanId = _e.Credito.Id, PositionDate = vigente.PositionDate, Version = vigente.Version + 1, RegisteredAt = _e.G.UtcNow,
            Status = vigente.Status, BranchId = vigente.BranchId, CreditLineId = vigente.CreditLineId, ModalityCode = vigente.ModalityCode,
            SubsegmentCode = vigente.SubsegmentCode, Principal = vigente.Principal, PayoffAmount = vigente.PayoffAmount, UsuryCapped = true,
            AppliedAnnualRate = 0.25m, InputsSha256 = vigente.InputsSha256,
        });
        await _e.Db.SaveChangesAsync();
        _e.Db.ChangeTracker.Clear();

        var porDefecto = InformesDePrueba.Tabla(await _e.Usura().Handle(new UsuryReportQuery(Filtros()), default));
        var topado = porDefecto.Filas.Should().ContainSingle(f => f.Seccion == UsuryReportQueryHandler.SeccionDeCreditos).Subject;
        V(porDefecto, topado, "Crédito").Should().Be(_e.Credito.Number);
        V(porDefecto, topado, "Tasa").Should().Be(20m);
        V(porDefecto, topado, "Usura").Should().Be(25m);
        V(porDefecto, topado, "Días topados").Should().Be(1);
        V(porDefecto, topado, "_credito").Should().Be(_e.Credito.PublicId.ToString());
        porDefecto.Filas.Should().NotContain(f => f.Seccion == UsuryReportQueryHandler.SeccionDeLineas,
            "la línea está a unos 5 puntos de la usura y la alerta por defecto (Informes.PuntosAlertaUsura) es de 2");
        porDefecto.Notas.Should().Contain(n => n.Contains("Informes.PuntosAlertaUsura"));

        var conSeis = InformesDePrueba.Tabla(await _e.Usura().Handle(new UsuryReportQuery(Filtros(), "6"), default));
        var linea = conSeis.Filas.Should().ContainSingle(f => f.Seccion == UsuryReportQueryHandler.SeccionDeLineas).Subject;
        V(conSeis, linea, "Usura").Should().Be(25m);
        ((decimal)V(conSeis, linea, "Margen")!).Should().BeInRange(0m, 6m);
        V(conSeis, linea, "Crédito").Should().BeNull();

        await _e.ParametroAsync(ParametrosDeCartera.InformesPuntosAlertaUsura, "7");
        var configurado = InformesDePrueba.Tabla(await _e.Usura().Handle(new UsuryReportQuery(Filtros()), default));
        configurado.Filas.Should().Contain(f => f.Seccion == UsuryReportQueryHandler.SeccionDeLineas, "los puntos vigentes de la cooperativa");
    }

    // ------------------------------------------------------------------------------------------------------- debtor-messages --

    [Fact]
    public async Task DebtorMessages_trae_el_motivo_de_la_exclusion_y_filtra_por_su_estado()
    {
        var mensaje = new DebtorMessage
        {
            PersonId = _e.D.O.Ana.Id, RecipientRole = ContactRole.Debtor, LoanId = _e.Credito.Id, Purpose = DebtorMessagePurpose.Collection,
            Channel = MessageChannel.Sms, ChannelCode = "SMS", RenderedBody = "Su cuota está vencida.", BodySha256 = new string('a', 64),
            IdempotencyKey = $"prueba:{Guid.NewGuid():N}", OriginKind = "Manual", RequestedAt = _e.G.UtcNow, RequestedByKind = ActorKind.Process,
        };
        _e.Db.DebtorMessages.Add(mensaje);
        await _e.Db.SaveChangesAsync();
        _e.Db.DebtorMessageEvents.Add(new DebtorMessageEvent
        {
            DebtorMessageId = mensaje.Id, Sequence = 1, Kind = DebtorMessageEventKind.Excluded, OccurredAt = _e.G.UtcNow,
            ReasonText = "El titular no autorizó el canal SMS.", ActorKind = ActorKind.Process,
        });
        await _e.Db.SaveChangesAsync();
        _e.Db.ChangeTracker.Clear();

        var t = InformesDePrueba.Tabla(await _e.Mensajes().Handle(new DebtorMessagesReportQuery(Filtros()), default));
        var fila = t.Filas.Should().ContainSingle().Subject;
        V(t, fila, "Crédito").Should().Be(_e.Credito.Number);
        V(t, fila, "Canal").Should().Be("SMS");
        V(t, fila, "Propósito").Should().Be("Cobranza");
        V(t, fila, "Estado").Should().Be("Excluido");
        ((string)V(t, fila, "Motivo de exclusión o diferimiento")!).Should().Contain("no autorizó el canal");
        V(t, fila, "_mensaje").Should().Be(mensaje.PublicId.ToString());

        InformesDePrueba.Tabla(await _e.Mensajes().Handle(new DebtorMessagesReportQuery(Filtros(), Status: "Sent"), default)).Filas.Should().BeEmpty();
        InformesDePrueba.Tabla(await _e.Mensajes().Handle(new DebtorMessagesReportQuery(Filtros(), Channel: "Email"), default)).Filas.Should().BeEmpty();
        (await _e.Mensajes().Handle(new DebtorMessagesReportQuery(Filtros(), Purpose: "Cobro"), default)).Error.Code
            .Should().Be(FiltrosDeInformeDeCartera.FiltroInvalidoCodigo);
        _e.G.AlcanceActual = InformesDePrueba.OtraOficina(_e.Credito.BranchId);
        InformesDePrueba.Tabla(await _e.Mensajes().Handle(new DebtorMessagesReportQuery(Filtros()), default)).Filas.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------------------------- migration-reconciliation --

    [Fact]
    public async Task MigrationReconciliation_trae_el_cuadre_y_las_ventas_con_su_aceptacion_y_exige_la_carga()
    {
        var filas = new List<FilaDeCuadreDto>
        {
            new("k1", "Capital", "Capital", "CONSUMO", "SinLibranza", "A", [new CuentaDelCuadreDto("14420501", "Consumo sin libranza")], 5_000_000m, 4_990_000m,
                10_000m, false),
        };
        var carga = new MigrationLoad
        {
            Number = 7, CutoffDate = new DateOnly(2026, 11, 30), Status = MigrationLoadStatus.Applied, CreatedByUserId = 1, CurrentCheckVersion = 1,
            BalanceCheckJson = JsonSerializer.Serialize(filas, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
        };
        _e.Db.MigrationLoads.Add(carga);
        await _e.Db.SaveChangesAsync();
        var diferencia = new MigrationLoadDifference
        {
            MigrationLoadId = carga.Id, CheckVersion = 1, Kind = MigrationDifferenceKind.SaleNotMigrated, DimensionKey = "venta:FV-10",
            InvoiceNumber = "FV-10", LendingAmount = 0m, AccountingAmount = 120_000m, Difference = -120_000m,
        };
        _e.Db.MigrationLoadDifferences.Add(diferencia);
        await _e.Db.SaveChangesAsync();
        _e.Db.MigrationSaleReconciliations.Add(new MigrationSaleReconciliation
        {
            MigrationLoadId = carga.Id, CheckVersion = 1, MessagePublicId = Guid.NewGuid(), SaleDocumentPublicId = Guid.NewGuid(), InvoiceNumber = "FV-10",
            CreditPaymentPublicId = Guid.NewGuid(), SaleDate = new DateOnly(2026, 11, 20), Amount = 120_000m, PersonId = _e.D.O.Ana.Id,
            Outcome = SaleReconciliationOutcome.NotMigratedWithBalance, DifferenceId = diferencia.Id,
        });
        await _e.Db.SaveChangesAsync();
        _e.Db.ChangeTracker.Clear();

        var t = InformesDePrueba.Tabla(await _e.Migracion().Handle(new MigrationReconciliationReportQuery(Filtros(), carga.PublicId), default));
        var cuadre = t.Filas.Should().ContainSingle(f => f.Seccion == MigrationReconciliationReportQueryHandler.SeccionDeCuadre).Subject;
        V(t, cuadre, "Concepto").Should().Be("Capital");
        V(t, cuadre, "Diferencia").Should().Be(10_000m);
        ((string)V(t, cuadre, "Conjunto de cuentas")!).Should().Contain("14420501");
        V(t, cuadre, "Aceptación").Should().Be("Pendiente");
        var venta = t.Filas.Should().ContainSingle(f => f.Seccion == MigrationReconciliationReportQueryHandler.SeccionDeVentas).Subject;
        V(t, venta, "Factura").Should().Be("FV-10");
        V(t, venta, "Clasificación").Should().Be("No migrada con saldo");
        V(t, venta, "Saldo").Should().Be(120_000m);
        V(t, venta, "Aceptación").Should().Be("Pendiente");
        V(t, venta, "_persona").Should().Be(_e.D.O.Ana.PublicId.ToString());

        var sinCarga = await _e.Migracion().Handle(new MigrationReconciliationReportQuery(Filtros()), default);
        sinCarga.Error.Code.Should().Be(FiltrosDeInformeDeCartera.FiltroInvalidoCodigo, "load es obligatorio");
        (await _e.Migracion().Handle(new MigrationReconciliationReportQuery(Filtros(), Guid.NewGuid()), default)).Error.Code
            .Should().Be("Lending.MigrationLoad.NotFound");

        _e.G.AlcanceActual = InformesDePrueba.SoloLaOficina(_e.Credito.BranchId);
        var parcial = InformesDePrueba.Tabla(await _e.Migracion().Handle(new MigrationReconciliationReportQuery(Filtros(), carga.PublicId), default));
        parcial.Filas.Should().BeEmpty("la carga es de toda la cooperativa: un alcance parcial no la ve");
        parcial.Notas.Should().Contain(MigrationReconciliationReportQueryHandler.NotaDeAlcance);
    }

    // ---------------------------------------------------------------------------------------------------- columnas ocultas --

    [Fact]
    public void Las_columnas_ocultas_viajan_en_la_tabla_y_no_se_exportan()
    {
        var ocultas = new Dictionary<string, IReadOnlyList<ColumnaExportable>>
        {
            ["aging"] = AgingReportQueryHandler.Columnas,
            ["transactions"] = TransactionsReportQueryHandler.Columnas,
            ["usury"] = UsuryReportQueryHandler.Columnas,
            ["debtor-messages"] = DebtorMessagesReportQueryHandler.Columnas,
            ["migration-reconciliation"] = MigrationReconciliationReportQueryHandler.Columnas,
        };
        ocultas["aging"].Where(c => c.EsOculta).Select(c => c.Clave).Should().Equal("_credito", "_persona");
        ocultas["transactions"].Where(c => c.EsOculta).Select(c => c.Clave).Should().Equal("_credito", "_operacion", "_mensaje");
        ocultas["usury"].Where(c => c.EsOculta).Select(c => c.Clave).Should().Equal("_credito");
        ocultas["debtor-messages"].Where(c => c.EsOculta).Select(c => c.Clave).Should().Equal("_credito", "_mensaje");
        ocultas["migration-reconciliation"].Where(c => c.EsOculta).Select(c => c.Clave).Should().Equal("_persona");

        foreach (var (vista, columnas) in ocultas)
        {
            var fila = new FilaExportable(columnas.Select(c => (object?)c.Nombre).ToList());
            var exportada = new TablaExportable(vista, string.Empty, columnas, [fila], null, []).SinOcultas();
            exportada.Columnas.Should().NotContain(c => c.Nombre.StartsWith('_'), $"{vista}: los exportadores omiten las ocultas (FR-133)");
            exportada.Filas.Single().Valores.Should().HaveCount(exportada.Columnas.Count);
        }
    }

    [Fact]
    public void Las_siete_vistas_de_K1_se_declaran_con_sus_datos_del_deudor()
    {
        var vistas = new[]
        {
            AgingReportQueryHandler.Vista, PlacementReportQueryHandler.Vista, ReceiptsReportQueryHandler.Vista, TransactionsReportQueryHandler.Vista,
            UsuryReportQueryHandler.Vista, DebtorMessagesReportQueryHandler.Vista, MigrationReconciliationReportQueryHandler.Vista,
        };

        vistas.Select(v => v.Key).Should().Equal("aging", "placement", "receipts", "transactions", "usury", "debtor-messages", "migration-reconciliation");
        vistas.Should().OnlyContain(v => v.AvailableFrom == Domain.Lending.EntregaDeCartera.K1);
        vistas.Where(v => v.PersonalData).Select(v => v.Key).Should().BeEquivalentTo("aging", "transactions", "usury", "debtor-messages", "migration-reconciliation");
        vistas.Where(v => v.PersonalData).Should().OnlyContain(v => v.RequiredPermission == "Lending.Loans.Read");
        vistas.Where(v => !v.PersonalData).Should().OnlyContain(v => v.RequiredPermission == null);
    }
}
