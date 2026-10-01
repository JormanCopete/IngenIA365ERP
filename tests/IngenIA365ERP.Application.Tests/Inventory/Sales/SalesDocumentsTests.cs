using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Pos;
using IngenIA365ERP.Application.Inventory.Pricing;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Sales;

/// <summary>
/// Feature 012, I3, T556 (contracts/api.md §18.1–§18.3; FR-057, FR-066, FR-097; US5-8): las ventas y notas de oficina por el ciclo
/// común. Una clase ajena a la ruta es <c>TypeNotForRoute</c>; la que el veredicto fiscal no admite, <c>FiscalClassMismatch</c> con
/// <c>verdict</c> y <c>allowedClasses</c>; un <c>expectedAmountDue</c> distinto, <c>TotalChanged</c>; la persona inactiva de contado sigue
/// la política; el vendedor sin rol vivo es <c>issue</c> en el borrador y error al confirmar; la nota va con la clase de su original, no
/// acredita más de lo que queda, reintegra por el mismo medio y en proporción (otro medio exige permiso), libera el bono y devuelve al
/// costo con que salió; sólo el comprobante y la nota no electrónicos se anulan con documento contrario.
/// </summary>
public class SalesDocumentsTests
{
    private static string Codigo<T>(Result<T> r) => r.IsFailure ? r.Error.Code : "OK";

    private static object? Datos(Error e) => (e as ErrorConDatos)?.Data;

    // -------------------------------------------------------------------------------------------- ruta y clase --

    [Fact]
    public async Task Una_clase_ajena_a_la_ruta_es_TypeNotForRoute()
    {
        var v = await VentasDePrueba.CrearAsync();

        var ajuste = await v.Guardar().Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Sales,
            v.Venta(lineas: [v.Linea(v.P1, 1m)]).ComoBorrador() with { DocumentTypePublicId = v.Tipo("AJN") }), default);
        var nota = await v.GuardarAsync(v.Venta("NV", lineas: [v.Linea(v.P1, 1m)]));

        Codigo(ajuste).Should().Be("Inventory.Document.TypeNotForRoute");
        Codigo(nota).Should().Be("Inventory.Document.TypeNotForRoute", "por /invoices sólo van la factura y el comprobante");
        Datos(nota.Error).Should().BeEquivalentTo(new { @class = "NonElectronicSalesNote", group = "Sales" });
    }

    [Fact]
    public async Task Una_clase_contraria_al_veredicto_fiscal_es_FiscalClassMismatch_y_obligada_sin_I4_NotReady()
    {
        var v = await VentasDePrueba.CrearAsync();
        var factura = await v.GuardarAsync(v.Venta("FV", pagos: [v.Pago(v.Efectivo, 2000m)], lineas: [v.Linea(v.P1, 1m)]));

        var r = await v.ConfirmarAsync(factura.Value.PublicId);

        Codigo(r).Should().Be(ErroresDeVentas.FiscalClassMismatchCode);
        Datos(r.Error).Should().BeEquivalentTo(new { verdict = "NonElectronic", allowedClasses = new[] { "NonElectronicSalesReceipt", "NonElectronicSalesNote" } });

        v.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeFacturacionElectronica.Modulo, Key = ParametrosDeFacturacionElectronica.ObligadaAFacturar, ScopeKind = ParameterScopeKind.None,
            Value = "true", ValidFrom = new DateOnly(2026, 9, 1), Reason = "obligada",
        });
        await v.Db.SaveChangesAsync();
        var comprobante = await v.GuardarAsync(v.Venta(pagos: [v.Pago(v.Efectivo, 2000m)], lineas: [v.Linea(v.P1, 1m)]));
        var bloqueada = await v.ConfirmarAsync(comprobante.Value.PublicId);

        Codigo(bloqueada).Should().Be(ErroresDeVentas.NotReadyCode, "obligada y sin I4 no confirma ninguna venta fiscal");
    }

    [Fact]
    public async Task Un_expectedAmountDue_distinto_es_TotalChanged()
    {
        var v = await VentasDePrueba.CrearAsync();
        var borrador = await v.GuardarAsync(v.Venta(pagos: [v.Pago(v.Efectivo, 4000m)], lineas: [v.Linea(v.P1, 2m)]));

        var r = await v.ConfirmarAsync(borrador.Value.PublicId, esperado: 3999m);
        var bien = await v.ConfirmarAsync(borrador.Value.PublicId, esperado: 4000m);

        Codigo(r).Should().Be(ErroresDelPos.TotalChangedCode);
        Datos(r.Error).Should().BeEquivalentTo(new { expectedAmountDue = 3999m, amountDue = 4000m });
        bien.IsSuccess.Should().BeTrue(bien.IsFailure ? bien.Error.Message : string.Empty);
        bien.Value.Status.Should().Be(DocumentStatus.Confirmed);
    }

    // ------------------------------------------------------------------------------------ contraparte y vendedor --

    [Fact]
    public async Task La_persona_inactiva_de_contado_sigue_Ventas_PersonaInactivaDeContado()
    {
        var v = await VentasDePrueba.CrearAsync();
        v.Ana.Status = "I";
        await v.Db.SaveChangesAsync();
        var permitida = await v.GuardarAsync(v.Venta(cliente: v.Ana, pagos: [v.Pago(v.Efectivo, 2000m)], lineas: [v.Linea(v.P1, 1m)]));
        var confirmada = await v.ConfirmarAsync(permitida.Value.PublicId);

        v.Parametro(ParametrosDeInventario.VentasPersonaInactivaDeContado, "Bloquear");
        var bloqueada = await v.GuardarAsync(v.Venta(cliente: v.Ana, pagos: [v.Pago(v.Efectivo, 2000m)], lineas: [v.Linea(v.P1, 1m)]));
        var rechazo = await v.ConfirmarAsync(bloqueada.Value.PublicId);

        confirmada.IsSuccess.Should().BeTrue("con «Permitir» se le vende de contado");
        bloqueada.Value.Warnings.Should().Contain(w => w.Code == ErroresDeVentas.PersonInactiveCode, "el borrador se guarda y lo avisa");
        Codigo(rechazo).Should().Be(ErroresDeVentas.PersonInactiveCode);
    }

    [Fact]
    public async Task El_vendedor_sin_rol_vivo_es_issue_en_el_borrador_y_error_al_confirmar_el_vivo_se_acepta()
    {
        var v = await VentasDePrueba.CrearAsync();
        var vivo = await v.GuardarAsync(v.Venta(vendedor: v.Luz.PublicId, pagos: [v.Pago(v.Efectivo, 2000m)], lineas: [v.Linea(v.P1, 1m)]));
        var aceptada = await v.ConfirmarAsync(vivo.Value.PublicId);

        v.Luz.IsDeleted = true;
        await v.Db.SaveChangesAsync();
        var retirado = await v.GuardarAsync(v.Venta(vendedor: v.Luz.PublicId, pagos: [v.Pago(v.Efectivo, 2000m)], lineas: [v.Linea(v.P1, 1m)]));
        var rechazo = await v.ConfirmarAsync(retirado.Value.PublicId);
        var sinFila = await v.GuardarAsync(v.Venta(vendedor: v.Ana.PublicId, pagos: [v.Pago(v.Efectivo, 2000m)], lineas: [v.Linea(v.P1, 1m)]));

        aceptada.IsSuccess.Should().BeTrue(aceptada.IsFailure ? aceptada.Error.Message : string.Empty);
        v.Documento(vivo.Value.PublicId).SalespersonId.Should().Be(v.Luz.Id);
        retirado.IsSuccess.Should().BeTrue("el borrador se guarda aunque tenga problemas de negocio");
        retirado.Value.Warnings.Should().Contain(w => w.Code == ErroresDelPos.SalespersonInvalidCode);
        Codigo(rechazo).Should().Be(ErroresDelPos.SalespersonInvalidCode);
        sinFila.Value.Warnings.Should().Contain(w => w.Code == ErroresDelPos.SalespersonInvalidCode, "una persona sin fila de vendedor tampoco vende");
    }

    // --------------------------------------------------------------------------------------------------- notas --

    [Fact]
    public async Task La_nota_con_clase_distinta_a_la_de_su_original_es_ClassMismatch()
    {
        var v = await VentasDePrueba.CrearAsync();
        var venta = await v.VentaConfirmadaAsync(lineas: [v.Linea(v.P1, 2m)]);

        var r = await v.NotaAsync(new CreditNoteDraftInput(venta.PublicId, "Error de digitación", true, false, [], [], DocumentTypePublicId: v.Tipo("NC")));

        Codigo(r).Should().Be(ErroresDeVentas.CreditNoteClassMismatchCode);
        Datos(r.Error).Should().BeEquivalentTo(new { originClass = "NonElectronicSalesReceipt", expectedClass = "NonElectronicSalesNote" });
    }

    [Fact]
    public async Task Acreditar_mas_de_lo_que_queda_es_ExceedsRemaining_con_data_lines()
    {
        var v = await VentasDePrueba.CrearAsync();
        var venta = await v.VentaConfirmadaAsync(lineas: [v.Linea(v.P1, 5m)]);
        var linea = venta.Lines.Single(l => !l.IsDeleted);

        var primera = await v.NotaAsync(new CreditNoteDraftInput(venta.PublicId, "Devuelve tres", false, true, [new CreditNoteLineInput(linea.PublicId, 3m)], []));
        (await v.ConfirmarAsync(primera.Value.PublicId)).IsSuccess.Should().BeTrue();
        var segunda = await v.NotaAsync(new CreditNoteDraftInput(venta.PublicId, "Devuelve tres más", false, true, [new CreditNoteLineInput(linea.PublicId, 3m)], []));

        Codigo(segunda).Should().Be(ErroresDeVentas.ExceedsRemainingCode);
        Datos(segunda.Error).Should().BeEquivalentTo(new { lines = new[] { new { OriginLinePublicId = linea.PublicId, RemainingQuantity = 2m, RemainingAmount = 4000m } } });
    }

    [Fact]
    public async Task El_reintegro_va_por_el_mismo_medio_y_en_proporcion_y_otro_medio_exige_permiso()
    {
        var v = await VentasDePrueba.CrearAsync();
        var venta = await v.VentaConfirmadaAsync(total => [v.Pago(v.Efectivo, 6000m), v.Pago(v.Transferencia, total - 6000m)], lineas: [v.Linea(v.P1, 5m)]);
        var linea = venta.Lines.Single(l => !l.IsDeleted);

        var nota = await v.NotaAsync(new CreditNoteDraftInput(venta.PublicId, "Descuento posterior", false, false, [new CreditNoteLineInput(linea.PublicId, Amount: 5000m)], []));

        nota.IsSuccess.Should().BeTrue(nota.IsFailure ? nota.Error.Message : string.Empty);
        var pagosDeLaVenta = v.Db.DocumentPayments.Where(p => p.DocumentId == venta.Id && !p.IsDeleted).ToDictionary(p => p.MeansCode);
        var reintegros = v.Db.DocumentPayments.Where(p => p.Document!.PublicId == nota.Value.PublicId && !p.IsDeleted).OrderBy(p => p.LineNumber).ToList();
        reintegros.Should().OnlyContain(r => r.Direction == PaymentDirection.Refunded);
        reintegros.Select(r => (r.MeansCode, r.Amount, r.RefundsPaymentId)).Should().Equal(
            ("EFECTIVO", 3000m, pagosDeLaVenta["EFECTIVO"].Id), ("TRANSF", 2000m, pagosDeLaVenta["TRANSF"].Id));

        v.K.Permisos.HasPermissionAsync(ReglasDeConfirmacionDeVenta.PermisoOtroMedio, Arg.Any<CancellationToken>()).Returns(false);
        var otroMedio = await v.NotaAsync(new CreditNoteDraftInput(venta.PublicId, "Por bono", false, false,
            [new CreditNoteLineInput(linea.PublicId, Amount: 1000m)], [new DocumentPaymentInput(v.Bono.PublicId, 1000m, Reference: "B-1")]));
        Codigo(otroMedio).Should().Be(ErroresDeVentas.RefundMeansNotAllowedCode);
    }

    [Fact]
    public async Task El_bono_reintegrado_pasa_a_Released_con_la_nota_que_lo_libero()
    {
        var v = await VentasDePrueba.CrearAsync();
        var venta = await v.VentaConfirmadaAsync(total => [v.Pago(v.Bono, total, "BN-0001")], lineas: [v.Linea(v.P1, 2m)]);
        var bono = v.Db.VoucherRedemptions.Single(r => r.DocumentId == venta.Id);
        bono.Status.Should().Be(VoucherRedemptionStatus.Active);

        var nota = await v.NotaAsync(new CreditNoteDraftInput(venta.PublicId, "Anula la venta", true, true, [], []));
        var r = await v.ConfirmarAsync(nota.Value.PublicId);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var liberado = v.Db.VoucherRedemptions.AsNoTracking().Single(x => x.Id == bono.Id);
        liberado.Status.Should().Be(VoucherRedemptionStatus.Released);
        liberado.ReleasedByDocumentId.Should().Be(v.Documento(nota.Value.PublicId).Id);
    }

    [Fact]
    public async Task La_devolucion_entra_al_costo_con_que_salio_aunque_el_promedio_cambio()
    {
        var v = await VentasDePrueba.CrearAsync();
        var venta = await v.VentaConfirmadaAsync(lineas: [v.Linea(v.P1, 2m)]);
        var linea = venta.Lines.Single(l => !l.IsDeleted);
        linea.UnitCost.Should().Be(1000m, "salió al promedio de 1.000");
        await v.K.EntradaAsync(v.P1, 98m, 3000m);

        var nota = await v.NotaAsync(new CreditNoteDraftInput(venta.PublicId, "Devuelve uno", false, true, [new CreditNoteLineInput(linea.PublicId, 1m)], []));
        var r = await v.ConfirmarAsync(nota.Value.PublicId);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var id = v.Documento(nota.Value.PublicId).Id;
        var entrada = v.Db.KardexEntries.Single(k => k.DocumentId == id);
        (entrada.Kind, entrada.QuantityBase, entrada.UnitCost).Should().Be((KardexEntryKind.Entry, 1m, 1000m));

        // Un solo vínculo por par de líneas (índice único (SourceLineId, TargetLineId) en la base, que InMemory no aplica): la nota con
        // devolución se enlaza sólo con NoteOf. Hasta el 2026-09-27 agregaba además ReturnOf y en la base respondía 500 (e2e T564).
        var pares = v.Db.DocumentLineLinks.Where(x => !x.IsDeleted && x.TargetLine!.DocumentId == id).Select(x => new { x.SourceLineId, x.TargetLineId }).ToList();
        pares.Should().OnlyHaveUniqueItems().And.ContainSingle();
    }

    [Fact]
    public async Task La_validacion_previa_evalua_los_sobres_con_un_numero_provisional_porque_el_numero_se_da_despues()
    {
        // La validación previa corre antes del cerrojo y del número (flujo canónico): con el número vacío, una cuenta que exige documento
        // cruce (el crédito provisional, FV + número) nunca era contabilizable y la última aprobación del crédito respondía NotPostable
        // (e2e T650, 2026-09-27). Lo evaluado lleva un número provisional; lo emitido, el definitivo.
        var v = await VentasDePrueba.CrearAsync();
        v.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeInventario.Modulo, Key = ParametrosDeInventario.ContabilidadModoDePaso, ScopeKind = ParameterScopeKind.None,
            Value = "EnLinea", ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba",
        });
        await v.Db.SaveChangesAsync();
        IReadOnlyList<IngenIA365ERP.Application.Common.Integration.Accounting.MensajeContableDto> evaluados = [];
        var validador = Substitute.For<IPasoDeValidacionPrevia>();
        validador.EvaluarAsync(default!, default!, default).ReturnsForAnyArgs(ci =>
        {
            evaluados = ci.ArgAt<IReadOnlyList<IngenIA365ERP.Application.Common.Integration.Accounting.MensajeContableDto>>(1);
            return Task.FromResult(Result.Success(new ResultadoDeValidacionPrevia(IngenIA365ERP.Domain.Enums.Integration.PrevalidationOutcome.Postable, [])));
        });
        var borrador = await v.GuardarAsync(v.Venta(lineas: v.Linea(v.P1, 1m)));
        var total = v.Documento(borrador.Value.PublicId).AmountDue;
        await v.GuardarAsync(v.Venta(pagos: [v.Pago(v.Efectivo, total)], lineas: v.Linea(v.P1, 1m)), borrador.Value.PublicId);

        var r = await new ConfirmInventoryDocumentCommandHandler(v.Db, v.Confirmacion(validador))
            .Handle(new ConfirmInventoryDocumentCommand(borrador.Value.PublicId, DocumentClassGroup.Sales), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        evaluados.Should().NotBeEmpty("con modo en línea se evalúa");
        evaluados.Should().OnlyContain(m => !string.IsNullOrWhiteSpace(m.Envelope.Origin.Number), "el número provisional llena el cruce");
    }

    // ----------------------------------------------------------------------------------------------- anulación --

    [Fact]
    public async Task Solo_se_anulan_con_documento_contrario_el_comprobante_y_la_nota_no_electronicos()
    {
        var v = await VentasDePrueba.CrearAsync();
        var venta = await v.VentaConfirmadaAsync(total => [v.Pago(v.Bono, total, "BN-0002")], lineas: [v.Linea(v.P1, 2m)]);

        var anulada = await v.Anular().Handle(new VoidInventoryDocumentCommand(venta.PublicId, DocumentClassGroup.Sales, "Venta duplicada"), default);

        anulada.IsSuccess.Should().BeTrue(anulada.IsFailure ? anulada.Error.Message : string.Empty);
        v.Db.VoucherRedemptions.AsNoTracking().Single(x => x.DocumentId == venta.Id).Status.Should().Be(VoucherRedemptionStatus.Released, "anular libera el bono");

        // Un fiscal electrónico emitido se corrige con su nota: se arma confirmado a mano (I4 no está).
        var tipo = v.K.Tipo("FV");
        var fiscal = new InventoryDocument
        {
            Class = DocumentClass.SalesInvoice, DocumentTypeId = tipo.Id, Prefix = "FE", Number = 1, OperationDate = Catalog.CatalogoDePrueba.Hoy,
            BranchId = v.K.Sucursal.Id, WarehouseId = v.K.Principal.Id, CreatedByUserId = Kardex.KardexDePrueba.Usuario,
        };
        fiscal.Confirmar(Kardex.KardexDePrueba.Usuario, DateTime.UtcNow);
        v.Db.InventoryDocuments.Add(fiscal);
        await v.Db.SaveChangesAsync();
        var corregir = await v.Anular().Handle(new VoidInventoryDocumentCommand(fiscal.PublicId, DocumentClassGroup.Sales, "Error"), default);

        Codigo(corregir).Should().Be(ErroresDeVentas.FiscalUseCorrectionCode);
        Datos(corregir.Error).Should().BeEquivalentTo(new { correctionClass = "CreditNote", route = ErroresDeVentas.RutaDeNotas, totalVoid = true });
    }

    // ---------------------------------------------------------------------------------------------- consultas --

    [Fact]
    public async Task La_consulta_de_la_venta_trae_pagos_mensajes_y_la_copia_fiscal_y_la_lista_filtra_por_persona()
    {
        var v = await VentasDePrueba.CrearAsync();
        var venta = await v.VentaConfirmadaAsync(total => [v.Pago(v.Efectivo, total, entregado: total + 1000m)], cliente: v.Ana, lineas: [v.Linea(v.P3, 1m)]);

        var detalle = await new GetSalesDocumentQueryHandler(v.Db, v.K.Vista(), v.K.Alcance).Handle(new GetSalesDocumentQuery(venta.PublicId), default);
        var lista = await new ListSalesDocumentsQueryHandler(v.Db, v.K.Alcance).Handle(new ListSalesDocumentsQuery(Person: v.Ana.PublicId), default);

        detalle.IsSuccess.Should().BeTrue(detalle.IsFailure ? detalle.Error.Message : string.Empty);
        var d = detalle.Value;
        d.Counterparty!.Name.Should().Be("Ana Pérez");
        d.Totals.TaxTotal.Should().Be(1900m);
        d.Lines.Single().Taxes.Should().ContainSingle(t => t.TaxRateCode == "IVA19" && t.Amount == 1900m);
        d.Payments.Single().Change.Should().Be(1000m);
        d.Messages.Select(m => m.Type).Should().Contain(["VentaFacturada", "CostoDeVentaReconocido"]);
        lista.Value.Items.Should().ContainSingle(x => x.DocumentPublicId == venta.PublicId && x.CounterpartyName == "Ana Pérez");
    }

    [Fact]
    public async Task Con_una_lista_que_incluye_impuestos_la_confirmacion_guarda_lo_que_la_persona_vio()
    {
        var v = await VentasDePrueba.CrearAsync();
        var cerrojo = Substitute.For<ICerrojoPorClave>();
        var deAna = (await new CreatePriceListCommandHandler(v.Db, cerrojo).Handle(new CreatePriceListCommand("ANA", "Ana con IVA", true,
            new PriceListScopeInput(PersonPublicId: v.Ana.PublicId), new DateOnly(2026, 1, 1), null, "Lista"), default)).Value;
        await new SetPriceListItemsCommandHandler(v.Db).Handle(new SetPriceListItemsCommand(deAna,
            [new PriceListItemInput(v.P3, v.Compras.C.Unidad("UND").PublicId, 999m)], "Precios"), default);

        // 999 con IVA: 839,50 + 159,51 = 999,01; el residuo de −0,01 va al bruto y el impuesto no se vuelve a calcular sobre él.
        var venta = await v.VentaConfirmadaAsync(cliente: v.Ana, lineas: [v.Linea(v.P3, 1m)]);

        venta.Total.Should().Be(999m, "la lista dice 999 con IVA");
        venta.AmountDue.Should().Be(999m);
        v.Db.DocumentTaxLines.Where(t => t.DocumentId == venta.Id).Sum(t => t.Amount).Should().Be(venta.TaxTotal);
    }

    // --------------------------------------------------------------------------------------- retiro gravado --

    [Fact]
    public async Task El_retiro_gravado_lleva_base_a_la_lista_general_e_IVA_y_sin_precio_dice_cual_falta()
    {
        var v = await VentasDePrueba.CrearAsync();
        v.K.Tipo("CI").IsTaxableWithdrawal = true;
        await v.Db.SaveChangesAsync();
        var confirmacion = v.Confirmacion();

        var guardado = await v.K.Guardar(v.Efectos()).Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Adjustments,
            v.K.Borrador("CI", centro: v.K.Centro.PublicId, lineas: [v.K.Linea(v.P3, 2m)])), default);
        var r = await confirmacion.ConfirmarAsync(new PedidoDeConfirmacion(guardado.Value.PublicId, DocumentClassGroup.Adjustments), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var contenido = v.ContenidoDe(guardado.Value.PublicId, "AjusteInventarioAprobado");
        contenido.Should().Contain("\"operation\":\"RetiroGravado\"").And.Contain("\"base\":20000").And.Contain("\"amount\":3800");

        var sinPrecio = await v.K.Guardar(v.Efectos()).Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Adjustments,
            v.K.Borrador("CI", centro: v.K.Centro.PublicId, lineas: [v.K.Linea(v.K.P2, 1m)])), default);
        await v.K.EntradaAsync(v.K.P2, 5m, 1000m);
        var falta = await v.Confirmacion().ConfirmarAsync(new PedidoDeConfirmacion(sinPrecio.Value.PublicId, DocumentClassGroup.Adjustments), default);
        Codigo(falta).Should().Be(ErroresDePrecios.PriceNotFoundCode);
        falta.Error.Message.Should().Contain("P2");
    }
}
