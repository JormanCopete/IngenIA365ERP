using System.Reflection;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Tests.Common.Integration;

/// <summary>
/// T014 (feature 012; decisiones-transversales T8; FR-072; contracts/mensajes.md §1, §3, §14): los contratos de mensaje.
/// Los cuatro ejemplos de §14 se construyen con los records y se serializan con <see cref="OpcionesDeMensajes"/>; el
/// resultado tiene que ser, byte a byte, el de <c>Casos/</c>. Esos archivos son los bloques JSON de §14 sin los
/// espacios fuera de las cadenas (la forma compacta en que se guarda <c>PayloadJson</c>): si una opción de
/// serialización, el orden de una propiedad o la escala de un decimal cambia, cambian los bytes de todo lo que se
/// emita desde entonces, y eso es un cambio de contrato que esta prueba delata.
/// </summary>
public class ContratosDeMensajesTests
{
    private static readonly Guid Venta = Guid.Parse("3f0c9d42-5a1b-4e7c-8d21-6b9f0e4a7c13");
    private static readonly Guid Sucursal = Guid.Parse("b7d1e2f3-0a4b-4c5d-8e6f-1a2b3c4d5e6f");
    private static readonly Guid Asociado = Guid.Parse("c1a2b3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d");
    private static readonly Guid Cajero = Guid.Parse("d4e5f6a7-b8c9-4d0e-9f1a-2b3c4d5e6f70");
    private static readonly Guid Supervisor = Guid.Parse("5e6f7a8b-9c0d-4e1f-a2b3-c4d5e6f7a8b9");
    private static readonly Guid Sesion = Guid.Parse("a9b8c7d6-e5f4-4a3b-9c2d-1e0f9a8b7c6d");
    private const string Cufe = "9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a9c1e4b7a";
    private static readonly DateOnly Fecha = new(2026, 11, 14);
    private static readonly DateTimeOffset Emitido = new(2026, 11, 15, 1, 12, 9, 418, TimeSpan.Zero);

    /// <summary>Los veinte tipos de §1, en su orden.</summary>
    private static readonly string[] LosVeinte =
    [
        "VentaFacturada", "CostoDeVentaReconocido", "CompraRecibida", "FacturaProveedorRegistrada",
        "AjusteInventarioAprobado", "TrasladoDespachado", "TrasladoRecibido", "DevolucionRegistrada",
        "DocumentoAnulado", "AjusteDeCostoReconocido", "NotaCreditoEmitida", "NotaDebitoEmitida",
        "GrupoContableReclasificado", "MovimientoDeCajaRegistrado", "DiferenciaDeArqueoAprobada",
        "SaldoInicialCargado", "PeriodoInventarioCerrado", "PeriodoInventarioReabierto",
        "VentaACreditoRegistrada", "AjusteDeVentaACredito",
    ];

    // ------------------------------------------------------------------------------ los cuatro de §14 --

    [Fact]
    public void VentaFacturada_de_14_1_sale_byte_a_byte() =>
        Serializar(Sobre("8a2e6f10-1c3d-4b5a-9e7f-0d1c2b3a4e51", VentaFacturadaV1.Type, "Confirmation", VentaFacturadaDelPos()))
            .Should().Be(Esperado("14.1-venta-facturada"));

    [Fact]
    public void CostoDeVentaReconocido_de_14_2_sale_byte_a_byte() =>
        Serializar(Sobre("8a2e6f10-1c3d-4b5a-9e7f-0d1c2b3a4e52", CostoDeVentaReconocidoV1.Type, "Confirmation", CostoDelPos()))
            .Should().Be(Esperado("14.2-costo-de-venta-reconocido"));

    [Fact]
    public void VentaACreditoRegistrada_de_14_3_sale_byte_a_byte() =>
        Serializar(Sobre("8a2e6f10-1c3d-4b5a-9e7f-0d1c2b3a4e53", VentaACreditoRegistradaV1.Type,
                "Confirmation:e1f2a3b4c5d64e7f8a9b0c1d2e3f4a03", CreditoDelPos()))
            .Should().Be(Esperado("14.3-venta-a-credito-registrada"));

    [Fact]
    public void DiferenciaDeArqueoAprobada_de_14_4_sale_byte_a_byte()
    {
        var arqueo = Guid.Parse("7c8d9e0f-1a2b-4c3d-9e4f-5a6b7c8d9e0f");
        var cajeroPersona = Guid.Parse("2a3b4c5d-6e7f-4a8b-9c0d-1e2f3a4b5c6d");
        var sobre = new IntegrationEnvelopeV1
        {
            MessageId = Guid.Parse("8a2e6f10-1c3d-4b5a-9e7f-0d1c2b3a4e60"),
            Type = DiferenciaDeArqueoAprobadaV1.Type,
            Version = 1,
            Kind = IntegrationMessageKind.Business,
            OriginModule = "INV",
            OriginEventKey = "Confirmation",
            Origin = new MessageOriginV1
            {
                Kind = MessageOriginKind.Document, DocumentClass = DocumentClass.CashCountDifference, DocumentTypeCode = "ARQDIF",
                Number = "AD-0045", PublicId = arqueo, OperationDate = Fecha, FiscalUniqueCode = null,
            },
            Related = null,
            ChainRootPublicId = arqueo,
            BranchPublicId = Sucursal,
            CostCenterPublicId = null,
            WarehouseCode = null,
            PersonPublicId = cajeroPersona,
            Currency = "COP",
            ExchangeRate = 1m,
            OriginUser = new UserRefV1 { CentralUserId = Supervisor, Name = "Supervisor de ensayo" },
            EmittedAt = new DateTimeOffset(2026, 11, 15, 13, 5, 22, 107, TimeSpan.Zero),
            Payload = new DiferenciaDeArqueoAprobadaV1
            {
                PointOfSaleCode = "PTO01",
                CashRegisterCode = "CAJA03",
                CashSessionPublicId = Sesion,
                Cashier = new CashierV1 { CentralUserId = Cajero, PersonPublicId = cajeroPersona, Name = "Cajero de ensayo" },
                Approval = new ApprovalRefV1
                {
                    ApprovalRequestPublicId = Guid.Parse("4d5e6f7a-8b9c-4d0e-8f1a-2b3c4d5e6f7a"),
                    ApprovedBy = new UserRefV1 { CentralUserId = Supervisor, Name = "Supervisor de ensayo" },
                    Level = 1,
                    Method = ApprovalMethod.InPersonPasskey,
                    Reason = "Recontado dos veces; el faltante se confirma",
                    DecidedAt = new DateTimeOffset(2026, 11, 15, 13, 5, 20, TimeSpan.Zero),
                },
                Lines =
                [
                    new CashCountDifferenceLineV1
                    {
                        PaymentMeansCode = "EFECTIVO", PaymentMeansClass = PaymentMeansClass.Cash, CountMethod = CashCountMethod.PhysicalCount,
                        Expected = 1250000.00m, Counted = 1242000.00m, Difference = -8000.00m, ToleranceAmount = 5000.00m,
                        WithinTolerance = false, Treatment = CashDifferenceTreatment.ShortageToExpense,
                        Reason = "Faltante sin explicación al cierre del turno",
                    },
                    new CashCountDifferenceLineV1
                    {
                        PaymentMeansCode = "VISARB", PaymentMeansClass = PaymentMeansClass.CreditCard, CountMethod = CashCountMethod.VoucherTotal,
                        Expected = 820500.00m, Counted = 820770.00m, Difference = 270.00m, ToleranceAmount = 1000.00m,
                        WithinTolerance = true, Treatment = CashDifferenceTreatment.Surplus,
                        Reason = "Pago registrado por 49.250; el comprobante del datáfono dice 49.520",
                    },
                ],
            },
        };

        Serializar(sobre).Should().Be(Esperado("14.4-diferencia-de-arqueo-aprobada"));
    }

    // --------------------------------------------------------------------------------- las opciones --

    [Fact]
    public void Los_enums_salen_como_texto_y_los_nulos_se_escriben()
    {
        var json = Texto(new CostLineV1
        {
            AccountingGroupCode = "ASEO", WarehouseCode = "B01", WarehouseBehavior = WarehouseBehavior.Transit, Movement = KardexEntryKind.Exit,
        });

        json.Should().Contain("\"warehouseBehavior\":\"Transit\"")
            .And.Contain("\"movement\":\"Exit\"")
            .And.Contain("\"branchPublicId\":null");
    }

    [Fact]
    public void Los_decimales_conservan_su_escala_y_las_tarifas_van_como_fraccion()
    {
        var json = Texto(new TaxLineV1 { TaxCode = "ICA", Rate = 0.00966m, TaxableBase = 100000.00m, Amount = 966.00m, TaxableUnits = 20.0000m });

        json.Should().Contain("\"rate\":0.00966").And.Contain("\"taxableBase\":100000.00")
            .And.Contain("\"amount\":966.00").And.Contain("\"taxableUnits\":20.0000");
    }

    [Fact]
    public void Un_instante_sin_fraccion_sale_sin_punto_y_con_Z()
    {
        var json = Texto(new ApprovalRefV1 { DecidedAt = new DateTimeOffset(2026, 11, 14, 20, 11, 40, TimeSpan.FromHours(-5)) });

        json.Should().Contain("\"decidedAt\":\"2026-11-15T01:11:40Z\"", "los instantes van en UTC");
    }

    [Fact]
    public void La_constante_Type_no_viaja_en_el_contenido()
    {
        Texto(new CompraRecibidaV1()).Should().NotContain("\"type\"");
    }

    [Fact]
    public void El_contenido_se_lee_con_las_mismas_opciones()
    {
        var original = CreditoDelPos();

        var leido = JsonSerializer.Deserialize<VentaACreditoRegistradaV1>(OpcionesDeMensajes.Serializar(original), OpcionesDeMensajes.Opciones)!;

        leido.Origin.Should().Be(CreditOrigin.ProvisionalCredit);
        leido.Approval!.DecidedAt.Should().Be(original.Approval!.DecidedAt);
        leido.Terms.Should().Be(original.Terms);
        Texto(leido).Should().Be(Texto(original));
    }

    [Fact]
    public void Las_opciones_son_de_solo_lectura()
    {
        OpcionesDeMensajes.Opciones.IsReadOnly.Should().BeTrue();
    }

    // -------------------------------------------------------------------------------- los veinte tipos --

    [Fact]
    public void El_catalogo_tiene_los_veinte_tipos_de_1_en_su_orden_con_su_destino()
    {
        CatalogoDeMensajesV1.Todos.Select(t => t.Type).Should().Equal(LosVeinte);
        CatalogoDeMensajesV1.Todos.Should().OnlyContain(t => t.Version == 1);

        CatalogoDeMensajesV1.Todos.Where(t => t.Destination == IntegrationDestinations.Lending).Select(t => t.Type)
            .Should().Equal("VentaACreditoRegistrada", "AjusteDeVentaACredito");
        CatalogoDeMensajesV1.Todos.Where(t => t.Kind == IntegrationMessageKind.Informational).Select(t => t.Type)
            .Should().Equal("SaldoInicialCargado", "PeriodoInventarioCerrado", "PeriodoInventarioReabierto");
        CatalogoDeMensajesV1.Todos.Single(t => t.Kind is null).Type.Should().Be("DocumentoAnulado", "hereda el Kind de su original");
    }

    [Fact]
    public void Cada_tipo_es_un_record_V1_de_la_carpeta_con_su_constante_Type()
    {
        foreach (var tipo in CatalogoDeMensajesV1.Todos)
        {
            tipo.Record.Namespace.Should().Be("IngenIA365ERP.Application.Common.Integration.Contracts.Inventory");
            tipo.Record.Name.Should().Be($"{tipo.Type}V1");

            var constante = tipo.Record.GetField("Type", BindingFlags.Public | BindingFlags.Static);
            constante.Should().NotBeNull($"{tipo.Record.Name} declara su constante Type");
            constante!.IsLiteral.Should().BeTrue();
            constante.GetRawConstantValue().Should().Be(tipo.Type);
        }
    }

    [Fact]
    public void No_hay_records_V1_de_mensaje_fuera_del_catalogo()
    {
        var conConstante = typeof(VentaFacturadaV1).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(VentaFacturadaV1).Namespace && t.Name.EndsWith("V1", StringComparison.Ordinal))
            .Where(t => t.GetField("Type", BindingFlags.Public | BindingFlags.Static) is { IsLiteral: true })
            .Select(t => t.Name)
            .OrderBy(n => n);

        conConstante.Should().Equal(LosVeinte.Select(t => $"{t}V1").OrderBy(n => n));
    }

    // ------------------------------------------------------------------------------------- ejemplos --

    private static IntegrationEnvelopeV1 Sobre(string messageId, string type, string clave, object payload) => new()
    {
        MessageId = Guid.Parse(messageId),
        Type = type,
        Version = 1,
        Kind = IntegrationMessageKind.Business,
        OriginModule = "INV",
        OriginEventKey = clave,
        Origin = new MessageOriginV1
        {
            Kind = MessageOriginKind.Document, DocumentClass = DocumentClass.PosEquivalentDocument, DocumentTypeCode = "DEPOS",
            Number = "PV01-1532", PublicId = Venta, OperationDate = Fecha, FiscalUniqueCode = Cufe,
        },
        Related = null,
        ChainRootPublicId = Venta,
        BranchPublicId = Sucursal,
        CostCenterPublicId = null,
        WarehouseCode = "B01PV",
        PersonPublicId = Asociado,
        Currency = "COP",
        ExchangeRate = 1m,
        OriginUser = new UserRefV1 { CentralUserId = Cajero, Name = "Cajero de ensayo" },
        EmittedAt = Emitido,
        Payload = payload,
    };

    private static VentaFacturadaV1 VentaFacturadaDelPos() => new()
    {
        SalesChannelCode = "MOSTRADOR",
        PointOfSaleCode = "PTO01",
        CashRegisterCode = "CAJA03",
        CashSessionPublicId = Sesion,
        DerivedFrom = [],
        Lines =
        [
            new SalesAmountLineV1 { AccountingGroupCode = "ABARROTES", WarehouseCode = "B01PV", GrossAmount = 100000.00m, DiscountAmount = 5000.00m, NetAmount = 95000.00m, DocumentLines = [1, 2, 4] },
            new SalesAmountLineV1 { AccountingGroupCode = "ASEO", WarehouseCode = "B01PV", GrossAmount = 50000.00m, DiscountAmount = 0.00m, NetAmount = 50000.00m, DocumentLines = [3, 5] },
        ],
        Taxes =
        [
            new TaxLineV1 { TaxCode = "IVA", TaxKind = TaxKind.Iva, TaxRateCode = "IVA05", Rate = 0.05m, Treatment = TaxTreatment.Generated, TaxableBase = 95000.00m, Amount = 4750.00m, DocumentLines = [1, 2, 4] },
            new TaxLineV1 { TaxCode = "IVA", TaxKind = TaxKind.Iva, TaxRateCode = "IVA19", Rate = 0.19m, Treatment = TaxTreatment.Generated, TaxableBase = 50000.00m, Amount = 9500.00m, DocumentLines = [3, 5] },
        ],
        Payments =
        [
            new PaymentLineV1 { PaymentPublicId = Guid.Parse("e1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a01"), LineNumber = 1, PaymentMeansCode = "EFECTIVO", PaymentMeansClass = PaymentMeansClass.Cash, Direction = PaymentDirection.Received, Amount = 60000.00m },
            new PaymentLineV1
            {
                PaymentPublicId = Guid.Parse("e1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a02"), LineNumber = 2, PaymentMeansCode = "VISARB", PaymentMeansClass = PaymentMeansClass.CreditCard,
                Direction = PaymentDirection.Received, Amount = 49250.00m, Reference = "482913", ThirdPartyPersonPublicId = Guid.Parse("f0e1d2c3-b4a5-4968-8776-655443322110"),
                CardNetworkCode = "VISA", CardAcquirerCode = "REDEBAN", CardTerminalCode = "T0457",
            },
            new PaymentLineV1
            {
                PaymentPublicId = Guid.Parse("e1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a03"), LineNumber = 3, PaymentMeansCode = "CREDASOC", PaymentMeansClass = PaymentMeansClass.AssociateCredit,
                Direction = PaymentDirection.Received, Amount = 50000.00m, ThirdPartyPersonPublicId = Asociado, PendingValidation = true,
            },
        ],
        Totals = new SalesTotalsV1 { Subtotal = 150000.00m, DiscountTotal = 5000.00m, TaxTotal = 14250.00m, WithholdingTotal = 0.00m, Total = 159250.00m, AmountDue = 159250.00m },
    };

    private static CostoDeVentaReconocidoV1 CostoDelPos() => new()
    {
        PointOfSaleCode = "PTO01",
        CashSessionPublicId = Sesion,
        Lines =
        [
            new CostLineV1 { AccountingGroupCode = "ABARROTES", WarehouseCode = "B01PV", WarehouseBehavior = WarehouseBehavior.Operational, Movement = KardexEntryKind.Exit, QuantityBase = 20.0000m, Cost = 72400.00m, DocumentLines = [1, 2, 4] },
            new CostLineV1 { AccountingGroupCode = "ASEO", WarehouseCode = "B01PV", WarehouseBehavior = WarehouseBehavior.Operational, Movement = KardexEntryKind.Exit, QuantityBase = 5.0000m, Cost = 31180.50m, DocumentLines = [3, 5] },
        ],
    };

    private static VentaACreditoRegistradaV1 CreditoDelPos() => new()
    {
        ThirdPartyKind = "Associate",
        Person = new PartySnapshotV1 { TaxIdType = "CC", TaxId = "1000000001", Name = "Asociado de ensayo" },
        CreditPayment = new CreditPaymentV1
        {
            PaymentPublicId = Guid.Parse("e1f2a3b4-c5d6-4e7f-8a9b-0c1d2e3f4a03"), LineNumber = 3, PaymentMeansCode = "CREDASOC",
            PaymentMeansClass = PaymentMeansClass.AssociateCredit, Amount = 50000.00m,
        },
        DocumentTotal = 159250.00m,
        AmountDue = 159250.00m,
        Terms = new CreditTermsV1
        {
            TermUnit = "Months", Term = 3, Installments = 3, Periodicity = "Monthly",
            FirstDueDate = new DateOnly(2026, 12, 14), FinalDueDate = new DateOnly(2027, 2, 14),
        },
        CreditLineCode = null,
        SuggestedCreditLineCode = "CONSUMOALM",
        PendingValidation = true,
        Origin = CreditOrigin.ProvisionalCredit,
        Approval = new ApprovalRefV1
        {
            ApprovalRequestPublicId = Guid.Parse("0b1c2d3e-4f5a-4b6c-8d7e-9f0a1b2c3d4e"),
            ApprovedBy = new UserRefV1 { CentralUserId = Supervisor, Name = "Supervisor de ensayo" },
            Level = 1,
            Method = ApprovalMethod.OwnSession,
            Reason = "Crédito provisional mientras la integración con Cartera está pendiente",
            DecidedAt = new DateTimeOffset(2026, 11, 15, 1, 11, 40, TimeSpan.Zero),
        },
        ConsultationEvidence = null,
        AccountsReceivableRecordedBy = "Contabilidad",
        PointOfSaleCode = "PTO01",
        SalesChannelCode = "MOSTRADOR",
    };

    // --------------------------------------------------------------------------------------- ayudas --

    private static string Serializar(IntegrationEnvelopeV1 sobre) => Encoding.UTF8.GetString(OpcionesDeMensajes.Serializar(sobre));

    private static string Texto(object contenido) => Encoding.UTF8.GetString(OpcionesDeMensajes.Serializar(contenido));

    private static string Esperado(string caso) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Common", "Integration", "Casos", $"{caso}.json"), new UTF8Encoding(false));
}
