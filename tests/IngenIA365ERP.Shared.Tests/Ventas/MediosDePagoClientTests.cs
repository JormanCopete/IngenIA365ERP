using System.Net;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Core;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Ventas;
using IngenIA365ERP.Shared.Tests.Inventario;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Ventas;

/// <summary>
/// T633 (feature 012, I3; contracts/api.md §22.1–§22.3): el cliente del catálogo de medios de pago de Core. La clase, el tipo de
/// referencia y la forma de arqueo viajan por nombre; editar un medio lleva el motivo junto al medio (§22.1); franquicias,
/// adquirentes, datáfonos y denominaciones tienen su ruta; la disponibilidad vive en <c>/api/inventory</c>; las lecturas no llevan
/// clave y nada pone <c>Authorization</c>. (nuevo)
/// </summary>
public class MediosDePagoClientTests
{
    private readonly ServidorDeIntegracion _servidor = new();
    private readonly MediosDePagoClient _cliente;

    public MediosDePagoClientTests()
    {
        var (http, auth) = _servidor.ConSesion();
        _cliente = new MediosDePagoClient(http, auth);
    }

    [Fact]
    public async Task La_lista_filtra_por_clase_con_su_nombre_y_no_lleva_clave()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new[] { MedioJson() });

        var r = await _cliente.ListarAsync(clase: TextosDeVentas.MedioEfectivo, activos: true);

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.Single().Code.Should().Be("EFE");
        var vista = _servidor.Vistas.Single();
        vista.Ruta.Should().Be("/api/core/payment-means?class=Cash&active=true");
        vista.Clave.Should().BeNull();
        vista.Authorization.Should().BeNull();
    }

    [Fact]
    public async Task Crear_un_medio_manda_sus_enums_por_nombre_y_su_clave()
    {
        var id = Guid.NewGuid();
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.Created, new { paymentMeansPublicId = id });

        var r = await _cliente.GuardarAsync(null, MedioRequest(), motivo: null, new ClaveDeOperacion());

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value.Should().Be(id);
        var vista = _servidor.Vistas.Single();
        vista.Metodo.Should().Be("POST");
        vista.Ruta.Should().Be("/api/core/payment-means");
        vista.Clave.Should().NotBeNullOrWhiteSpace();
        vista.Cuerpo.Should().Contain("\"class\":\"CreditCard\"").And.Contain("\"referenceKind\":\"Approval\"").And.Contain("\"countMethod\":\"VoucherTotal\"");
    }

    [Fact]
    public async Task Editar_un_medio_lleva_el_motivo_en_el_mismo_cuerpo()
    {
        _servidor.Responder = _ => new HttpResponseMessage(HttpStatusCode.NoContent);
        var id = Guid.NewGuid();

        var r = await _cliente.GuardarAsync(id, MedioRequest(), "Sube la tolerancia", new ClaveDeOperacion());

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        var vista = _servidor.Vistas.Single();
        vista.Metodo.Should().Be("PUT");
        vista.Ruta.Should().Be($"/api/core/payment-means/{id}");
        vista.Cuerpo.Should().Contain("\"reason\":\"Sube la tolerancia\"").And.Contain("\"code\":\"VISA\"");
    }

    [Fact]
    public async Task Un_datafono_se_crea_en_su_ruta_con_su_adquirente()
    {
        var adquirente = Guid.NewGuid();
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.Created, new
        {
            cardTerminalPublicId = Guid.NewGuid(), code = "DAT1", cardAcquirerPublicId = adquirente, cardAcquirerCode = "CRB", serial = (string?)null,
            description = "Caja 1", isActive = true,
        });

        var r = await _cliente.GuardarDatafonoAsync(null, new DatafonoRequest("DAT1", adquirente, null, "Caja 1", true), new ClaveDeOperacion());

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.CardAcquirerCode.Should().Be("CRB");
        _servidor.Vistas.Single().Ruta.Should().Be("/api/core/card-terminals");
    }

    [Fact]
    public async Task Una_denominacion_manda_su_tipo_por_nombre()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.Created, new
        {
            cashDenominationPublicId = Guid.NewGuid(), currency = "COP", kind = 1, value = 100000m, displayOrder = 1, isActive = true,
            validFrom = "2026-01-01", validTo = (string?)null,
        });

        var r = await _cliente.GuardarDenominacionAsync(null, new DenominacionRequest(TextosDeVentas.NombreDeTipoDeDenominacion(1), 100000m,
            new DateOnly(2026, 1, 1), null, 1, true, null), new ClaveDeOperacion());

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        _servidor.Vistas.Single().Cuerpo.Should().Contain("\"kind\":\"Bill\"");
    }

    [Fact]
    public async Task La_disponibilidad_se_fija_en_el_modulo_comercial()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new
        {
            offeredAtAllPoints = false, pointOfSalePublicIds = new[] { Guid.NewGuid() }, offeredOnAllChannels = true,
            salesChannelPublicIds = Array.Empty<Guid>(), offeredForAllDocumentTypes = true, documentTypePublicIds = Array.Empty<Guid>(),
        });
        var id = Guid.NewGuid();

        var r = await _cliente.FijarDisponibilidadAsync(id, new DisponibilidadRequest([Guid.NewGuid()], [], []), new ClaveDeOperacion());

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.OfferedAtAllPoints.Should().BeFalse();
        var vista = _servidor.Vistas.Single();
        vista.Metodo.Should().Be("PUT");
        vista.Ruta.Should().Be($"/api/inventory/payment-means/{id}/availability");
    }

    private static MedioDePagoRequest MedioRequest() => new()
    {
        Code = "VISA", Name = "Visa crédito", Class = TextosDeVentas.NombreDeClaseDeMedio(TextosDeVentas.MedioTarjetaCredito),
        RequiresReference = true, ReferenceKind = TextosDeVentas.NombreDeTipoDeReferencia(1), CountMethod = TextosDeVentas.NombreDeFormaDeArqueo(TextosDeVentas.ArqueoPorLote),
        DianPaymentMeansCode = "48", ValidFrom = new DateOnly(2026, 1, 1),
    };

    private static object MedioJson() => new
    {
        paymentMeansPublicId = Guid.NewGuid(), code = "EFE", name = "Efectivo", displayOrder = 1, quickKey = "F1", @class = 1,
        cardNetworkPublicId = (Guid?)null, cardNetworkCode = (string?)null, cardAcquirerPublicId = (Guid?)null, cardAcquirerCode = (string?)null,
        bankPublicId = (Guid?)null, destinationAccountNumber = (string?)null, destinationAccountType = (byte?)null, requiresReference = false,
        referenceKind = (int?)null, referenceMinLength = (byte?)null, referenceMaxLength = (byte?)null, allowsChange = true, allowsPartial = true,
        uniqueReference = false, countMethod = 1, requiresTerminalBatchAtClose = false, toleranceAmount = 0m, expectedCommissionRate = (decimal?)null,
        expectedCommissionFixed = (decimal?)null, dianPaymentMeansCode = "10", creditDefaults = (object?)null, offeredAtAllPoints = true,
        offeredOnAllChannels = true, offeredForAllDocumentTypes = true, isActive = true, validFrom = "2026-01-01", validTo = (string?)null, notes = (string?)null,
    };
}
