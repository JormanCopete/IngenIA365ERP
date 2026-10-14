using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Application.Lending.Segregation;
using IngenIA365ERP.Application.Tests.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Migration;

/// <summary>
/// Feature 013, US8, T0721 (FR-141, FR-143, D17, D29, T83; api.md §19.2), la aceptación: <see cref="AcceptMigrationDifferencesCommand"/> acepta una
/// lista de diferencias vigentes con motivo y un punto de sesión cerrado, aprobado y de sujeto <c>ActivationDifferences</c> de esa carga (si no,
/// <c>Lending.Session.ItemMismatch</c>); la aceptación es de escritura única; con el par <c>CargarMigracion × AceptarDiferencias</c> activo, quien
/// cargó no acepta (<c>Lending.Segregation.Conflict</c>); al aceptar todas, la carga queda cuadrada. <see cref="GetMigrationBalanceCheckQuery"/> sin
/// contabilidad iniciada responde <c>Lending.Accounting.NotStarted</c>. (nuevo)
/// </summary>
public sealed class ConciliacionYAceptacionDeMigracionTests : IAsyncLifetime
{
    private MigracionDePrueba _m = null!;

    public async Task InitializeAsync()
    {
        _m = await MigracionDePrueba.CrearAsync();
        await _m.CreditoCompletoAsync("CR-1");
        (await _m.AplicarAsync()).IsSuccess.Should().BeTrue("capital e interés quedan con diferencia: el doble de Contabilidad dice cero");
    }

    public Task DisposeAsync()
    {
        _m.Dispose();
        return Task.CompletedTask;
    }

    private Task<List<MigrationLoadDifference>> DiferenciasAsync() => _m.Db.MigrationLoadDifferences.AsNoTracking().OrderBy(d => d.Id).ToListAsync();

    private async Task<IngenIA365ERP.Application.Common.Models.Result<IReadOnlyList<MigrationDifferenceDto>>> AceptarAsync(IReadOnlyList<Guid> diferencias, Guid punto)
    {
        var r = await _m.Aceptador().Handle(new AcceptMigrationDifferencesCommand(_m.Carga.PublicId, diferencias, punto, "Diferencias por redondeo del sistema de origen")
            { OperationKey = Guid.NewGuid() }, default);
        _m.Db.ChangeTracker.Clear();
        return r;
    }

    [Fact]
    public async Task Acepta_con_motivo_y_punto_de_sesion_de_esa_carga_y_la_carga_queda_cuadrada()
    {
        var diferencias = await DiferenciasAsync();
        diferencias.Should().HaveCount(2);
        _m.G.ActuarComo(_m.Contadora);

        var r = await AceptarAsync(diferencias.Select(d => d.PublicId).ToList(), _m.Acta(_m.Carga.PublicId));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        (await DiferenciasAsync()).Should().AllSatisfy(d =>
        {
            d.AcceptedByUserId.Should().Be(_m.Contadora.Id);
            d.AcceptanceReason.Should().Be("Diferencias por redondeo del sistema de origen");
            d.SessionItemId.Should().NotBeNull();
        });
        (await _m.Db.MigrationLoads.AsNoTracking().SingleAsync()).IsBalanced.Should().BeTrue();
        (await _m.Db.CaseParticipations.AsNoTracking().Select(c => c.Action).ToListAsync()).Should().Contain(Domain.Lending.Segregation.AccionesDeSegregacion.AceptarDiferencias);

        var otraVez = await AceptarAsync([diferencias[0].PublicId], _m.Acta(_m.Carga.PublicId));
        otraVez.Error.Code.Should().Be(ErroresDeMigracion.CodigoDiferenciaInexistente, "la aceptación es de escritura única");
    }

    [Fact]
    public async Task Un_punto_que_no_es_de_esa_carga_o_no_esta_cerrado_responde_ItemMismatch()
    {
        var una = (await DiferenciasAsync())[0].PublicId;

        (await AceptarAsync([una], _m.Acta(Guid.NewGuid()))).Error.Code.Should().Be("Lending.Session.ItemMismatch");
        (await AceptarAsync([una], _m.Acta(_m.Carga.PublicId, estado: SessionStatus.Open))).Error.Code.Should().Be("Lending.Session.ItemMismatch");
        (await AceptarAsync([una], _m.Acta(_m.Carga.PublicId, resultado: CreditDecisionOutcome.Deny))).Error.Code.Should().Be("Lending.Session.ItemMismatch");
        (await DiferenciasAsync()).Should().OnlyContain(d => d.AcceptedAt == null);
    }

    [Fact]
    public async Task Con_el_par_de_segregacion_activo_quien_cargo_no_acepta()
    {
        _m.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = ParametrosDeCartera.SegregacionIncompatibilidades, ScopeKind = ParameterScopeKind.None,
            Value = "[{\"a\": \"CargarMigracion\", \"b\": \"AceptarDiferencias\", \"activo\": true}]", ValidFrom = new DateOnly(2026, 1, 1), Reason = "Política",
        });
        await _m.Db.SaveChangesAsync();
        _m.Db.ChangeTracker.Clear();
        var una = (await DiferenciasAsync())[0].PublicId;

        var r = await AceptarAsync([una], _m.Acta(_m.Carga.PublicId));

        r.Error.Code.Should().Be(RegistroDeIntervenciones.CodigoConflicto);
        _m.G.ActuarComo(_m.Contadora);
        (await AceptarAsync([una], _m.Acta(_m.Carga.PublicId))).IsSuccess.Should().BeTrue("otra persona sí acepta");
    }

    [Fact]
    public async Task El_cuadre_se_consulta_sin_datos_personales_y_sin_contabilidad_iniciada_no_hay_cuadre()
    {
        var r = await new GetMigrationBalanceCheckQueryHandler(_m.Db, _m.Contabilidad).Handle(new GetMigrationBalanceCheckQuery(_m.Carga.PublicId), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Rows.Should().HaveCount(2);
        r.Value.Differences.Should().HaveCount(2).And.OnlyContain(d => d.Kind == MigrationDifferenceKind.BalanceCheck);
        System.Text.Json.JsonSerializer.Serialize(r.Value).Should().NotContain("52000001").And.NotContain("Deudora", "el cuadre no trae documentos ni nombres (C85)");

        _m.ContabilidadIniciada = false;
        (await new GetMigrationBalanceCheckQueryHandler(_m.Db, _m.Contabilidad).Handle(new GetMigrationBalanceCheckQuery(_m.Carga.PublicId), default))
            .Error.Code.Should().Be(ErroresDeMigracion.CodigoSinContabilidad);
    }
}

/// <summary>
/// Feature 013, US8, T0721, la conciliación de ventas (FR-143, D29; data-model §21.5): <see cref="ConciliacionDeVentasDeMigracion"/> hace nacer la
/// diferencia <c>SaleNotMigrated</c> de cada venta no migrada con saldo; <see cref="MarkMigrationSaleCommand"/> marca desde la lista, con motivo y sólo
/// con la carga aplicada, una venta pendiente como <c>OutsideLendingD29</c> (nace su diferencia <c>SaleOutsideLending</c>, que se acepta venta por
/// venta) o <c>CancelledBeforeCutoff</c> (deja de tener diferencia); una que no está pendiente responde <c>Lending.MigrationLoad.SaleNotPending</c>;
/// los ajustes se cuentan hasta y después del corte; la consulta lista las cuatro clases. (nuevo)
/// </summary>
public sealed class ConciliacionDeVentasDeMigracionTests : IAsyncLifetime
{
    private static readonly DateOnly Corte = VentasACreditoDePrueba.Hoy.AddDays(-10);
    private VentasACreditoDePrueba _v = null!;
    private MigrationLoad _carga = null!;

    public async Task InitializeAsync()
    {
        _v = await VentasACreditoDePrueba.CrearAsync();
        _carga = new MigrationLoad { Number = 1, CutoffDate = Corte, Status = MigrationLoadStatus.Applied, CreatedByUserId = _v.Cajera.Id, CurrentCheckVersion = 1 };
        _v.Db.MigrationLoads.Add(_carga);
        _v.Db.SaveChanges();
        var loans = Parte("loans");
        var sales = Parte("sales");
        _v.Db.MigrationLoanRows.Add(new MigrationLoanRow { MigrationLoadPartId = loans.Id, RowNumber = 2, SourceLoanNumber = "S-1", PersonId = _v.Ana.Id,
            CreditLineCode = "CONLIB", InvoiceNumber = "PV01-MIG", DisbursementDate = Corte.AddDays(-20), OriginalAmount = 600_000m });
        _v.Db.MigrationSaleRows.Add(new MigrationSaleRow { MigrationLoadPartId = sales.Id, RowNumber = 2, InvoiceNumber = "PV01-CAN", PersonId = _v.Ana.Id,
            SaleDate = Corte.AddDays(-15), OriginalAmount = 600_000m, ExternalCollections = 600_000m, Outcome = SaleReconciliationOutcome.CancelledBeforeCutoff });
        _v.Db.SaveChanges();
        _v.Db.ChangeTracker.Clear();

        var migrada = _v.NuevaVenta(numero: "PV01-MIG", fecha: Corte.AddDays(-20));
        await _v.EmitirAsync(migrada, VentasACreditoDePrueba.ContenidoV2(migrada));
        foreach (var (numero, dias) in new[] { ("PV01-CAN", -15), ("PV01-SIN", -5), ("PV01-D29", -3), ("PV01-PAG", -2) })
        {
            var venta = _v.NuevaVenta(numero: numero, fecha: Corte.AddDays(dias));
            await _v.EmitirAsync(venta, VentasACreditoDePrueba.ContenidoV2(venta));
        }
        _v.G.ActuarComo(_v.Cajera);
    }

    public Task DisposeAsync()
    {
        _v.Dispose();
        return Task.CompletedTask;
    }

    private MigrationLoadPart Parte(string clave)
    {
        var p = new MigrationLoadPart { MigrationLoadId = _carga.Id, PartKey = clave, FileName = clave + ".xlsx", FileSha256 = Guid.NewGuid().ToString("N"),
            ImportedByUserId = _v.Cajera.Id, ImportedAt = _v.G.UtcNow, BatchNumber = 1, RowCount = 1 };
        _v.Db.MigrationLoadParts.Add(p);
        _v.Db.SaveChanges();
        return p;
    }

    private ConciliacionDeVentasDeMigracion Conciliacion() => new(_v.Db, new ConciliacionDeVentasDeLaCarga(_v.Db, new MensajesEntrantes(_v.Db)));

    private async Task<ConciliacionRegistrada> ConciliarAsync()
    {
        var carga = await _v.Db.MigrationLoads.SingleAsync();
        var r = await Conciliacion().ConciliarAsync(carga, 1, default);
        r.IsSuccess.Should().BeTrue();
        await _v.Db.SaveChangesAsync();
        _v.Db.ChangeTracker.Clear();
        return r.Value;
    }

    private async Task<IngenIA365ERP.Application.Common.Models.Result<MigrationSaleDto>> MarcarAsync(string factura, SaleReconciliationOutcome clase)
    {
        var mensaje = (await _v.Db.MigrationSaleReconciliations.AsNoTracking().FirstAsync(r => r.InvoiceNumber == factura)).MessagePublicId;
        var r = await new MarkMigrationSaleCommandHandler(_v.Db, _v.G.Actor, _v.G.Reloj)
            .Handle(new MarkMigrationSaleCommand(_carga.PublicId, mensaje, clase, "Revisada con Comercial") { OperationKey = Guid.NewGuid() }, default);
        _v.Db.ChangeTracker.Clear();
        return r;
    }

    [Fact]
    public async Task Las_no_migradas_con_saldo_hacen_nacer_su_diferencia_y_se_marcan_desde_la_lista()
    {
        var conciliada = await ConciliarAsync();

        (conciliada.Conteos.Migradas, conciliada.Conteos.CanceladasAntesDelCorte, conciliada.Conteos.NoMigradasConSaldo).Should().Be((1, 1, 3));
        var diferencias = await _v.Db.MigrationLoadDifferences.AsNoTracking().ToListAsync();
        diferencias.Should().HaveCount(3).And.OnlyContain(d => d.Kind == MigrationDifferenceKind.SaleNotMigrated && d.CheckVersion == 1);
        (await _v.Db.MigrationSaleReconciliations.AsNoTracking().Where(r => r.Outcome == SaleReconciliationOutcome.NotMigratedWithBalance).ToListAsync())
            .Should().OnlyContain(r => r.DifferenceId != null);

        var fuera = await MarcarAsync("PV01-D29", SaleReconciliationOutcome.OutsideLendingD29);
        var pagada = await MarcarAsync("PV01-PAG", SaleReconciliationOutcome.CancelledBeforeCutoff);

        fuera.IsSuccess.Should().BeTrue(fuera.IsFailure ? fuera.Error.Message : string.Empty);
        (fuera.Value.Classification, fuera.Value.MarkedFromList, fuera.Value.MarkReason).Should()
            .Be((SaleReconciliationOutcome.OutsideLendingD29, true, "Revisada con Comercial"));
        pagada.Value.DifferencePublicId.Should().BeNull("la marcada cancelada deja de tener diferencia");
        var carga = await _v.Db.MigrationLoads.AsNoTracking().SingleAsync();
        var vigentes = await ConciliacionDeVentasDeMigracion.VigentesAsync(_v.Db, carga, 1, default);
        vigentes.Select(d => d.Kind).Should().BeEquivalentTo(new[] { MigrationDifferenceKind.SaleNotMigrated, MigrationDifferenceKind.SaleOutsideLending });
        carga.SalesReconciled.Should().BeFalse();
        carga.SalesReconciliationSha256.Should().HaveLength(64);

        var lista = await new GetMigrationSalesReconciliationQueryHandler(_v.Db).Handle(new GetMigrationSalesReconciliationQuery(_carga.PublicId), default);
        lista.Value.Items.Select(i => i.Classification).Should().BeEquivalentTo(new[]
        {
            SaleReconciliationOutcome.Migrated, SaleReconciliationOutcome.CancelledBeforeCutoff, SaleReconciliationOutcome.NotMigratedWithBalance,
            SaleReconciliationOutcome.OutsideLendingD29, SaleReconciliationOutcome.CancelledBeforeCutoff,
        });
    }

    [Fact]
    public async Task Una_venta_que_no_esta_pendiente_o_una_carga_no_aplicada_responde_SaleNotPending()
    {
        await ConciliarAsync();

        (await MarcarAsync("PV01-MIG", SaleReconciliationOutcome.OutsideLendingD29)).Error.Code.Should().Be(ErroresDeMigracion.CodigoVentaNoPendiente);
        (await MarcarAsync("PV01-CAN", SaleReconciliationOutcome.OutsideLendingD29)).Error.Code.Should().Be(ErroresDeMigracion.CodigoVentaNoPendiente);

        var carga = await _v.Db.MigrationLoads.SingleAsync();
        carga.Status = MigrationLoadStatus.Replaced;
        await _v.Db.SaveChangesAsync();
        _v.Db.ChangeTracker.Clear();
        (await MarcarAsync("PV01-SIN", SaleReconciliationOutcome.OutsideLendingD29)).Error.Code.Should().Be(ErroresDeMigracion.CodigoVentaNoPendiente);
    }

    [Fact]
    public void La_marca_exige_motivo_y_una_de_las_dos_clases()
    {
        var validador = new MarkMigrationSaleCommandValidator();

        validador.Validate(new MarkMigrationSaleCommand(Guid.NewGuid(), Guid.NewGuid(), SaleReconciliationOutcome.Migrated, "motivo")).IsValid.Should().BeFalse();
        validador.Validate(new MarkMigrationSaleCommand(Guid.NewGuid(), Guid.NewGuid(), SaleReconciliationOutcome.OutsideLendingD29, "")).IsValid.Should().BeFalse();
        validador.Validate(new MarkMigrationSaleCommand(Guid.NewGuid(), Guid.NewGuid(), SaleReconciliationOutcome.OutsideLendingD29, "Venta de servicios"))
            .IsValid.Should().BeTrue();
    }
}
