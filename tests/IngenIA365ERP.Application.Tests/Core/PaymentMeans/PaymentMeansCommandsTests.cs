using FluentAssertions;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.PaymentMeans;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using M = IngenIA365ERP.Application.Core.PaymentMeans.PlantillaDeMediosDePago;

namespace IngenIA365ERP.Application.Tests.Core.PaymentMeans;

/// <summary>
/// Feature 012, I3, T551 (contracts/api.md §22.1–§22.2; FR-096, FR-101): el catálogo de medios de pago de Core y sus franquicias,
/// adquirentes, datáfonos y denominaciones. Las combinaciones que un medio no admite responden <c>Core.PaymentMeans.Invalid</c>
/// con <c>data { field, rule }</c>; el <c>PUT</c> exige motivo; con pagos no cambian clase, red, adquirente ni arqueo
/// (<c>Core.PaymentMeans.InUse</c>, <c>data.payments</c>) ni se borra; código repetido es <c>Catalogo.CodigoDuplicado</c>; la tecla
/// rápida es única entre activos; el arqueo sale de la clase si no viene; franquicias, adquirentes y datáfonos en uso no se
/// borran. Y la plantilla 11 (T592) con las mismas reglas, todo o nada.
/// </summary>
public class PaymentMeansCommandsTests
{
    private static readonly DateOnly Hoy = new(2026, 9, 27);
    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();

    public PaymentMeansCommandsTests()
    {
        _reloj.HoyLocal.Returns(Hoy);
        _reloj.UtcNow.Returns(new DateTime(2026, 9, 27, 15, 0, 0, DateTimeKind.Utc));
    }

    private static PaymentMeansInput Efectivo(string codigo = "EFECTIVO") => new()
    {
        Code = codigo, Name = "Efectivo", Class = PaymentMeansClass.Cash, AllowsChange = true, ToleranceAmount = 2000m,
        DianPaymentMeansCode = "10", ValidFrom = Hoy,
    };

    private Task<Result<Guid>> CrearAsync(PaymentMeansInput input) =>
        new CreatePaymentMeansCommandHandler(_db, _reloj).Handle(new CreatePaymentMeansCommand(input), default);

    private Task<Result<PaymentMeansDto>> EditarAsync(Guid id, PaymentMeansInput input) =>
        new UpdatePaymentMeansCommandHandler(_db, _reloj).Handle(new UpdatePaymentMeansCommand(id, input, "ajuste de tolerancia"), default);

    private async Task<(CardNetwork Visa, CardNetwork Debito, CardAcquirer Redeban)> TarjetasAsync()
    {
        var visa = new CardNetwork { Code = "VISA", Name = "Visa", CardKind = CardKind.Credit };
        var debito = new CardNetwork { Code = "MAESTRO", Name = "Maestro", CardKind = CardKind.Debit };
        var redeban = new CardAcquirer { Code = "REDEBAN", Name = "Redeban" };
        _db.AddRange(visa, debito, redeban);
        await _db.SaveChangesAsync();
        return (visa, debito, redeban);
    }

    private async Task PagoAsync(int medioId, int? datafonoId = null)
    {
        _db.DocumentPayments.Add(new DocumentPayment
        {
            DocumentId = 900, LineNumber = 1, PaymentMeansId = medioId, Amount = 10_000m, MeansCode = "X", MeansName = "X",
            MeansClass = PaymentMeansClass.Cash, DianPaymentMeansCode = "10", CardTerminalId = datafonoId,
        });
        await _db.SaveChangesAsync();
    }

    private static ErrorConDatos ConDatos(Error e) => e.Should().BeOfType<ErrorConDatos>().Subject;

    private static (string Field, string Rule) CampoYRegla(Error e)
    {
        var data = ConDatos(e).Data;
        return ((string)data.GetType().GetProperty("field")!.GetValue(data)!, (string)data.GetType().GetProperty("rule")!.GetValue(data)!);
    }

    // ------------------------------------------------------------------------------------------ combinaciones --

    [Fact]
    public async Task Las_vueltas_son_solo_del_efectivo()
    {
        var (visa, _, redeban) = await TarjetasAsync();
        var r = await CrearAsync(Efectivo("VISARB") with
        {
            Class = PaymentMeansClass.CreditCard, CardNetworkPublicId = visa.PublicId, CardAcquirerPublicId = redeban.PublicId, DianPaymentMeansCode = "48",
        });

        r.Error.Code.Should().Be(PaymentMeansErrors.InvalidCode);
        CampoYRegla(r.Error).Should().Be(("allowsChange", ReglasDeMedioDePago.VueltasSoloEnEfectivo));
    }

    [Fact]
    public async Task Un_credito_no_se_arquea_fisicamente()
    {
        var r = await CrearAsync(Efectivo("CREDASO") with
        {
            Class = PaymentMeansClass.AssociateCredit, AllowsChange = false, CountMethod = CashCountMethod.PhysicalCount, DianPaymentMeansCode = "ZZZ",
            CreditDefaults = new CreditDefaultsInput(30, 1, 30),
        });

        CampoYRegla(r.Error).Should().Be(("countMethod", ReglasDeMedioDePago.CreditoSinArqueoFisico));
    }

    [Fact]
    public async Task El_efectivo_se_arquea()
    {
        var r = await CrearAsync(Efectivo() with { CountMethod = CashCountMethod.None });

        CampoYRegla(r.Error).Should().Be(("countMethod", ReglasDeMedioDePago.EfectivoSeArquea));
    }

    [Fact]
    public async Task Una_tarjeta_exige_red_y_adquirente_de_su_tipo()
    {
        var (visa, debito, redeban) = await TarjetasAsync();
        var tarjeta = Efectivo("VISARB") with { Class = PaymentMeansClass.CreditCard, AllowsChange = false, DianPaymentMeansCode = "48" };

        CampoYRegla((await CrearAsync(tarjeta with { CardAcquirerPublicId = redeban.PublicId })).Error)
            .Should().Be(("cardNetworkPublicId", ReglasDeMedioDePago.TarjetaExigeRed));
        CampoYRegla((await CrearAsync(tarjeta with { CardNetworkPublicId = visa.PublicId })).Error)
            .Should().Be(("cardAcquirerPublicId", ReglasDeMedioDePago.TarjetaExigeAdquirente));
        CampoYRegla((await CrearAsync(tarjeta with { CardNetworkPublicId = debito.PublicId, CardAcquirerPublicId = redeban.PublicId })).Error)
            .Should().Be(("cardNetworkPublicId", ReglasDeMedioDePago.TipoDeRedIncompatible), "una red débito no sirve para un medio de crédito");

        var ok = await CrearAsync(tarjeta with { CardNetworkPublicId = visa.PublicId, CardAcquirerPublicId = redeban.PublicId });
        ok.IsSuccess.Should().BeTrue(ok.IsFailure ? ok.Error.Message : string.Empty);
        _db.PaymentMeans.Single(m => m.Code == "VISARB").CountMethod.Should().Be(CashCountMethod.VoucherTotal, "una tarjeta se arquea por lote si no se dice otra cosa");
    }

    [Fact]
    public async Task Una_referencia_exigida_dice_cual_es()
    {
        var r = await CrearAsync(Efectivo("BONO") with { Class = PaymentMeansClass.Voucher, AllowsChange = false, RequiresReference = true, DianPaymentMeansCode = "71" });

        CampoYRegla(r.Error).Should().Be(("referenceKind", ReglasDeMedioDePago.ReferenciaExigeTipo));
    }

    [Fact]
    public async Task El_codigo_DIAN_sale_del_anexo_vigente()
    {
        var r = await CrearAsync(Efectivo() with { DianPaymentMeansCode = "Q9" });

        CampoYRegla(r.Error).Should().Be(("dianPaymentMeansCode", ReglasDeMedioDePago.CodigoDianDesconocido));
    }

    [Fact]
    public async Task El_arqueo_por_defecto_sale_de_la_clase_y_el_bono_es_de_numero_unico()
    {
        (await CrearAsync(Efectivo())).IsSuccess.Should().BeTrue();
        var bono = await CrearAsync(Efectivo("BONO") with
        {
            Class = PaymentMeansClass.Voucher, AllowsChange = false, RequiresReference = true, ReferenceKind = PaymentReferenceKind.VoucherNumber, DianPaymentMeansCode = "71",
        });
        bono.IsSuccess.Should().BeTrue(bono.IsFailure ? bono.Error.Message : string.Empty);

        _db.PaymentMeans.Single(m => m.Code == "EFECTIVO").CountMethod.Should().Be(CashCountMethod.PhysicalCount);
        var b = _db.PaymentMeans.Single(m => m.Code == "BONO");
        b.CountMethod.Should().Be(CashCountMethod.ByReference);
        b.UniqueReference.Should().BeTrue();
    }

    // ------------------------------------------------------------------------------------------ código y tecla --

    [Fact]
    public async Task Un_codigo_repetido_es_codigo_duplicado()
    {
        await CrearAsync(Efectivo());

        var r = await CrearAsync(Efectivo("efectivo"));

        r.Error.Code.Should().Be("Catalogo.CodigoDuplicado");
    }

    [Fact]
    public async Task La_tecla_rapida_es_unica_entre_los_activos()
    {
        (await CrearAsync(Efectivo() with { QuickKey = "e" })).IsSuccess.Should().BeTrue();

        var otro = await CrearAsync(Efectivo("CAJAMENOR") with { QuickKey = "E" });
        var inactivo = await CrearAsync(Efectivo("VIEJO") with { QuickKey = "E", IsActive = false });

        CampoYRegla(otro.Error).Should().Be(("quickKey", ReglasDeMedioDePago.TeclaRapidaRepetida));
        inactivo.IsSuccess.Should().BeTrue("un medio inactivo no ocupa la tecla");
    }

    // ------------------------------------------------------------------------------------------ edición --

    [Fact]
    public async Task Editar_exige_motivo()
    {
        var sinMotivo = await new UpdatePaymentMeansCommandValidator().ValidateAsync(new UpdatePaymentMeansCommand(Guid.NewGuid(), Efectivo(), " "));

        sinMotivo.Errors.Should().Contain(e => e.PropertyName == "Reason");
    }

    [Fact]
    public async Task Con_pagos_no_cambian_la_clase_ni_el_arqueo_pero_si_la_tolerancia()
    {
        var id = (await CrearAsync(Efectivo())).Value;
        var medio = _db.PaymentMeans.Single(m => m.PublicId == id);
        await PagoAsync(medio.Id);

        var clase = await EditarAsync(id, Efectivo() with { Class = PaymentMeansClass.Check, AllowsChange = false });
        var arqueo = await EditarAsync(id, Efectivo() with { CountMethod = CashCountMethod.ByReference });
        var tolerancia = await EditarAsync(id, Efectivo() with { ToleranceAmount = 5000m, Name = "Efectivo caja" });

        clase.Error.Code.Should().Be(PaymentMeansErrors.InUseCode);
        ConDatos(clase.Error).Data.GetType().GetProperty("payments")!.GetValue(ConDatos(clase.Error).Data).Should().Be(1);
        arqueo.Error.Code.Should().Be(PaymentMeansErrors.InUseCode);
        tolerancia.IsSuccess.Should().BeTrue(tolerancia.IsFailure ? tolerancia.Error.Message : string.Empty);
        tolerancia.Value.ToleranceAmount.Should().Be(5000m);
        tolerancia.Value.Code.Should().Be("EFECTIVO");
    }

    [Fact]
    public async Task Con_pagos_no_cambian_la_red_ni_el_adquirente()
    {
        var (visa, _, redeban) = await TarjetasAsync();
        var otra = new CardAcquirer { Code = "CREDIBANCO", Name = "Credibanco" };
        _db.Add(otra);
        await _db.SaveChangesAsync();
        var tarjeta = Efectivo("VISARB") with
        {
            Class = PaymentMeansClass.CreditCard, AllowsChange = false, DianPaymentMeansCode = "48", CardNetworkPublicId = visa.PublicId, CardAcquirerPublicId = redeban.PublicId,
        };
        var id = (await CrearAsync(tarjeta)).Value;
        await PagoAsync(_db.PaymentMeans.Single(m => m.PublicId == id).Id);

        var r = await EditarAsync(id, tarjeta with { CardAcquirerPublicId = otra.PublicId });

        r.Error.Code.Should().Be(PaymentMeansErrors.InUseCode);
    }

    [Fact]
    public async Task Prender_todos_con_un_conjunto_explicito_es_conflicto()
    {
        var id = (await CrearAsync(Efectivo() with { OfferedAtAllPoints = false })).Value;
        var medio = _db.PaymentMeans.Single(m => m.PublicId == id);
        _db.PaymentMeansPointsOfSale.Add(new PaymentMeansPointOfSale { PaymentMeansId = medio.Id, PointOfSaleId = 7 });
        await _db.SaveChangesAsync();

        var r = await EditarAsync(id, Efectivo() with { OfferedAtAllPoints = true });

        r.Error.Code.Should().Be("Inventory.PaymentMeans.AvailabilityConflict");
    }

    // ------------------------------------------------------------------------------------------ borrado --

    [Fact]
    public async Task Un_medio_con_pagos_no_se_borra_y_sin_pagos_si()
    {
        var conPagos = (await CrearAsync(Efectivo())).Value;
        var sinPagos = (await CrearAsync(Efectivo("CAJAMENOR"))).Value;
        await PagoAsync(_db.PaymentMeans.Single(m => m.PublicId == conPagos).Id);
        var borrar = new DeletePaymentMeansCommandHandler(_db, _reloj);

        (await borrar.Handle(new DeletePaymentMeansCommand(conPagos), default)).Error.Code.Should().Be(PaymentMeansErrors.InUseCode);
        (await borrar.Handle(new DeletePaymentMeansCommand(sinPagos), default)).IsSuccess.Should().BeTrue();
        _db.PaymentMeans.Single(m => m.PublicId == sinPagos).IsDeleted.Should().BeTrue();
    }

    // ------------------------------------------------------------------------------ franquicias, adquirentes, datáfonos --

    [Fact]
    public async Task Franquicias_adquirentes_y_datafonos_en_uso_no_se_borran()
    {
        var (visa, _, redeban) = await TarjetasAsync();
        await CrearAsync(Efectivo("VISARB") with
        {
            Class = PaymentMeansClass.CreditCard, AllowsChange = false, DianPaymentMeansCode = "48", CardNetworkPublicId = visa.PublicId, CardAcquirerPublicId = redeban.PublicId,
        });
        var ter = await new CreateCardTerminalCommandHandler(_db).Handle(new CreateCardTerminalCommand("ter001", redeban.PublicId, "SN-1"), default);
        ter.IsSuccess.Should().BeTrue(ter.IsFailure ? ter.Error.Message : string.Empty);
        await PagoAsync(_db.PaymentMeans.Single(m => m.Code == "VISARB").Id, _db.CardTerminals.Single().Id);

        var red = await new DeleteCardNetworkCommandHandler(_db, _reloj).Handle(new DeleteCardNetworkCommand(visa.PublicId), default);
        var adq = await new DeleteCardAcquirerCommandHandler(_db, _reloj).Handle(new DeleteCardAcquirerCommand(redeban.PublicId), default);
        var dat = await new DeleteCardTerminalCommandHandler(_db, _reloj).Handle(new DeleteCardTerminalCommand(ter.Value.CardTerminalPublicId), default);

        red.Error.Code.Should().Be(PaymentMeansErrors.CardNetworkInUseCode);
        adq.Error.Code.Should().Be(PaymentMeansErrors.CardAcquirerInUseCode);
        dat.Error.Code.Should().Be(PaymentMeansErrors.CardTerminalInUseCode);
    }

    [Fact]
    public async Task El_datafono_es_de_su_adquirente_con_codigo_unico_en_el()
    {
        var (_, _, redeban) = await TarjetasAsync();
        var crear = new CreateCardTerminalCommandHandler(_db);

        (await crear.Handle(new CreateCardTerminalCommand("TER001", redeban.PublicId), default)).Value.CardAcquirerCode.Should().Be("REDEBAN");
        (await crear.Handle(new CreateCardTerminalCommand("ter001", redeban.PublicId), default)).Error.Code.Should().Be("Catalogo.CodigoDuplicado");
        (await crear.Handle(new CreateCardTerminalCommand("TER002", Guid.NewGuid()), default)).Error.Code.Should().Be(PaymentMeansErrors.CardAcquirerNotFoundCode);
    }

    [Fact]
    public async Task Una_denominacion_nueva_cierra_la_vigencia_de_la_retirada()
    {
        var vieja = new CashDenomination { Kind = CashDenominationKind.Bill, Value = 1000m, ValidFrom = new DateOnly(2020, 1, 1), IsActive = true };
        _db.CashDenominations.Add(vieja);
        await _db.SaveChangesAsync();

        var r = await new CreateCashDenominationCommandHandler(_db).Handle(
            new CreateCashDenominationCommand(CashDenominationKind.Coin, 1000m, new DateOnly(2026, 10, 1), RetiresPublicId: vieja.PublicId), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        vieja.ValidTo.Should().Be(new DateOnly(2026, 9, 30));
        (await new CreateCashDenominationCommandHandler(_db).Handle(
            new CreateCashDenominationCommand(CashDenominationKind.Coin, 1000m, Hoy), default)).Error.Code.Should().Be("Catalogo.CodigoDuplicado");
    }

    // ------------------------------------------------------------------------------------------ plantilla 11 --

    private readonly ITabularFileReader _lector = Substitute.For<ITabularFileReader>();
    private readonly Dictionary<string, TablaLeida> _hojas = new(StringComparer.OrdinalIgnoreCase);

    private void Hoja(string nombre, string[] encabezados, params string?[][] filas) =>
        _hojas[nombre] = new TablaLeida(encabezados, filas.Select((f, i) => new FilaLeida(i + 2, f)).ToList(), "xlsx");

    private async Task<Result<ImportResultDto>> ImportarAsync(ModoDeImportacion modo, string motivo = "", bool conDisponibilidad = true)
    {
        _lector.ListarHojasAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Result.Success<IReadOnlyList<string>>(_hojas.Keys.ToList())));
        _lector.LeerHojaAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(_hojas.TryGetValue(ci.ArgAt<string?>(2) ?? "?", out var t)
                ? Result.Success(t)
                : Result.Failure<TablaLeida>(ArchivosTabulares.HojaFaltante(ci.ArgAt<string?>(2) ?? "?"))));
        var permisos = Substitute.For<ICurrentUserPermissions>();
        permisos.EsMaestroGlobal.Returns(conDisponibilidad);
        permisos.ListAsync(Arg.Any<CancellationToken>()).Returns(["Core.PaymentMeans.Manage"]);
        var ejecutor = new EjecutorDeImportacion(_db, _lector, permisos,
            new ServiceCollection().AddSingleton(Substitute.For<IAuditService>()).BuildServiceProvider());
        var r = await new ImportPaymentMeansCommandHandler(_db, ejecutor, _reloj)
            .Handle(new ImportPaymentMeansCommand(modo, new ArchivoDeImportacion("medios.xlsx", [1, 2, 3]), motivo), default);
        _db.DescartarCambios();
        return r;
    }

    private static readonly string[] EncabezadosMedios =
        [M.Codigo, M.Nombre, M.Clase, M.Franquicia, M.Adquirente, M.Arqueo, M.Tolerancia, M.CodigoDian, M.Puntos, M.VigenteDesde];

    [Fact]
    public async Task La_plantilla_11_crea_franquicias_adquirentes_datafonos_y_medios_con_su_disponibilidad()
    {
        var punto = new PointOfSale { Code = "PV01", Name = "Florida", BranchId = 1, SalesChannelId = 1, DefaultWarehouseId = 1 };
        var caja = new CashRegister { Code = "CJ01", Name = "Caja 1", PointOfSale = punto, WarehouseId = 1 };
        _db.AddRange(punto, caja);
        await _db.SaveChangesAsync();
        Hoja(M.HojaFranquicias, [M.Codigo, M.Nombre, M.TipoDeTarjeta], ["VISA", "Visa", "Both"]);
        Hoja(M.HojaAdquirentes, [M.Codigo, M.Nombre], ["REDEBAN", "Redeban"]);
        Hoja(M.HojaDatafonos, [M.Codigo, M.Adquirente, M.CajaPorDefecto], ["TER001", "REDEBAN", "CJ01"]);
        Hoja(M.HojaMedios, EncabezadosMedios,
            ["EFECTIVO", "Efectivo", "Cash", null, null, "PhysicalCount", "2000", "10", "*", "2026-09-01"],
            ["VISARB", "Visa Redeban", "CreditCard", "VISA", "REDEBAN", "VoucherTotal", "0", "48", "PV01", "2026-09-01"]);

        var revision = await ImportarAsync(ModoDeImportacion.Review);
        revision.Value.Valid.Should().BeTrue(string.Join(" | ", revision.Value.Errors.Select(e => $"{e.Sheet} {e.Row} {e.Column} {e.Code} {e.Message}")));
        revision.Value.RequiresReason.Should().BeTrue("crear un medio crea su vigencia");
        _db.PaymentMeans.Should().BeEmpty("la revisión no guarda");

        var r = await ImportarAsync(ModoDeImportacion.Apply, "carga inicial");

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        _db.CardNetworks.Single().Code.Should().Be("VISA");
        var ter = _db.CardTerminals.Single();
        _db.CashRegisters.Single().DefaultCardTerminalId.Should().Be(ter.Id, "la caja propone el datáfono nuevo");
        var visa = _db.PaymentMeans.Single(m => m.Code == "VISARB");
        visa.OfferedAtAllPointsOfSale.Should().BeFalse();
        _db.PaymentMeansPointsOfSale.Single(x => x.PaymentMeansId == visa.Id).PointOfSaleId.Should().Be(punto.Id);
        _db.PaymentMeans.Single(m => m.Code == "EFECTIVO").OfferedAtAllPointsOfSale.Should().BeTrue();
    }

    [Fact]
    public async Task La_plantilla_11_aplica_las_reglas_del_alta_y_no_guarda_nada_con_un_error()
    {
        Hoja(M.HojaMedios, EncabezadosMedios,
            ["EFECTIVO", "Efectivo", "Cash", null, null, "PhysicalCount", "2000", "10", "*", "2026-09-01"],
            ["VISARB", "Visa Redeban", "CreditCard", null, null, null, "0", "48", "*", "2026-09-01"]);

        var r = await ImportarAsync(ModoDeImportacion.Apply, "carga inicial");

        r.Error.Code.Should().Be(ImportErrors.InvalidCode);
        var cuerpo = (ImportResultDto)ConDatos(r.Error).Data;
        cuerpo.Errors.Should().ContainSingle(e => e.Row == 3 && e.Column == M.Franquicia && e.Code == PaymentMeansErrors.InvalidCode);
        _db.PaymentMeans.Should().BeEmpty("todo o nada");
    }

    [Fact]
    public async Task Las_columnas_de_disponibilidad_exigen_su_permiso()
    {
        Hoja(M.HojaMedios, EncabezadosMedios, ["EFECTIVO", "Efectivo", "Cash", null, null, null, "0", "10", "*", "2026-09-01"]);

        var r = await ImportarAsync(ModoDeImportacion.Review, conDisponibilidad: false);

        r.Value.Errors.Should().ContainSingle(e => e.Column == M.Puntos && e.Code == ImportErrors.CellPermissionRequired);
    }

    [Fact]
    public async Task La_plantilla_11_admite_columnas_de_credito_solo_en_clases_de_credito_y_sin_arqueo()
    {
        // I3, T660 (plantillas.md §11): el error cae en la fila y la columna que sobra.
        string[] encabezados = [M.Codigo, M.Nombre, M.Clase, M.Arqueo, M.CodigoDian, M.PlazoDias, M.Cuotas, M.Periodicidad, M.LineaSugerida, M.VigenteDesde];
        Hoja(M.HojaMedios, encabezados,
            ["CREDASOC", "Crédito asociados", "AssociateCredit", "None", "1", "30", "6", "Mensual", "CONSUMO", "2026-09-01"],
            ["EFECTIVO", "Efectivo", "Cash", "PhysicalCount", "10", null, null, null, "CONSUMO", "2026-09-01"],
            ["CREDCLI", "Crédito comercial", "CustomerCredit", "ByReference", "1", "30", "1", null, null, "2026-09-01"]);

        var r = await ImportarAsync(ModoDeImportacion.Review);

        r.Value.Valid.Should().BeFalse();
        r.Value.Errors.Should().ContainSingle(e => e.Row == 3 && e.Column == M.LineaSugerida && e.Code == PaymentMeansErrors.InvalidCode);
        r.Value.Errors.Should().ContainSingle(e => e.Row == 4 && e.Column == M.Arqueo && e.Code == PaymentMeansErrors.InvalidCode);
        r.Value.Errors.Should().NotContain(e => e.Row == 2, "el crédito con sus condiciones y sin arqueo es válido");
    }
}
