using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Inventory.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Inventory.Lotes;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Tests.Accounting.Inventory;

/// <summary>
/// Feature 012, T454 (T513; contracts/contabilidad.md §5.3): los grupos de un lote resumido. Un grupo por fecha del comprobante,
/// tipo de documento, operación (con el tipo de documento decide el tipo de comprobante), sucursal y centro; las unidades de un
/// mismo evento no se parten; lo que va por documento y los informativos salen solos; una anulación, una nota o un ajuste de
/// costo caen en el grupo de <b>su</b> tipo y <b>su</b> fecha.
/// </summary>
public class AgrupadorDeResumidosTests
{
    private static readonly string Resumido = EscenarioContable.Horario("AJP", resumido: true);
    private static readonly Guid Lote = Guid.NewGuid();

    public static MensajeEntrante Entrante(MensajeDeUnidad m, string? horario, Guid? lote = null, int intentos = 0) => new(
        m.Sobre, m.Contenido, "{}", string.Empty, PrevalidationOutcome.Postable,
        new EntregaEntrante(IntegrationDestinations.Accounting, DeliveryMode.Batch, DeliveryStatus.InBatch, horario, null, lote ?? Lote, intentos, null));

    private static MensajeDeUnidad Ajuste(DateOnly fecha, string tipo = "AJP", Guid? sucursal = null, Guid? centro = null, string clase = "PositiveAdjustment",
        string operacion = "AjustePositivo", DocumentRefV1? relacionado = null)
    {
        var contenido = new AjusteInventarioAprobadoV1
        {
            Operation = operacion,
            Lines = [new CostLineV1 { AccountingGroupCode = "ABARROTES", WarehouseCode = "B01", Movement = KardexEntryKind.Entry, Cost = 100m, DocumentLines = [1] }],
        };
        var sobre = EscenarioContable.Sobre(AjusteInventarioAprobadoV1.Type, clase, tipo, "AJ-" + Guid.NewGuid().ToString("N")[..4], fecha, relacionado: relacionado)
            with { BranchPublicId = sucursal ?? EscenarioContable.Principal, CostCenterPublicId = centro };
        return new MensajeDeUnidad(sobre with { Payload = contenido }, contenido);
    }

    [Fact]
    public void Agrupa_por_fecha_tipo_sucursal_y_centro()
    {
        var d14 = new DateOnly(2026, 3, 14);
        var entregas = new[]
        {
            Ajuste(d14), Ajuste(d14), Ajuste(new DateOnly(2026, 3, 15)), Ajuste(d14, sucursal: EscenarioContable.Norte),
            Ajuste(d14, centro: EscenarioContable.CentroDeCosto), Ajuste(d14, tipo: "AJX"),
        }.Select(m => Entrante(m, Resumido)).ToList();

        var trabajos = AgrupadorDeResumidos.Planear(entregas, IntegrationDestinations.Accounting);

        trabajos.Should().HaveCount(5);
        trabajos.Should().OnlyContain(t => t.ClaveDeGrupo != null);
        trabajos[0].Unidades.Should().HaveCount(2, "dos ajustes del mismo día, tipo y sucursal van juntos");
        trabajos.Skip(1).Should().OnlyContain(t => t.Unidades.Count == 1);
        trabajos[0].Unidades.Should().OnlyContain(u => u.BatchPublicId == Lote && u.Destino == IntegrationDestinations.Accounting);
    }

    [Fact]
    public void Lo_que_va_por_documento_y_los_informativos_salen_solos()
    {
        var d14 = new DateOnly(2026, 3, 14);
        var saldo = new SaldoInicialCargadoV1();
        var informativo = new MensajeDeUnidad(EscenarioContable.Sobre(SaldoInicialCargadoV1.Type, "OpeningBalance", "SIN", "SI-1", kind: IntegrationMessageKind.Informational) with { Payload = saldo }, saldo);
        var entregas = new[]
        {
            Entrante(Ajuste(d14), EscenarioContable.Horario("AJP", resumido: false)),
            Entrante(Ajuste(d14), EscenarioContable.Horario("AJP", resumido: false)),
            Entrante(informativo, Resumido),
            Entrante(Ajuste(d14), null),
        };

        var trabajos = AgrupadorDeResumidos.Planear(entregas, IntegrationDestinations.Accounting);

        trabajos.Should().HaveCount(4).And.OnlyContain(t => t.ClaveDeGrupo == null && t.Unidades.Count == 1);
    }

    [Fact]
    public void Una_unidad_de_varios_mensajes_no_se_parte_y_lleva_los_intentos_leidos()
    {
        var unidad = EscenarioContable.Unidad(EscenarioContable.Caso("05-nota-credito-con-devolucion-cruza-con-la-venta"));
        var entregas = unidad.Select((m, i) => Entrante(m, EscenarioContable.Horario("NCV", resumido: true), intentos: i + 1)).ToList();

        var trabajos = AgrupadorDeResumidos.Planear(entregas, IntegrationDestinations.Accounting);

        var u = trabajos.Single().Unidades.Single();
        u.MessagePublicIds.Should().Equal(unidad.Select(m => m.Sobre.MessageId));
        u.IntentosLeidos.Should().Be(2);
        u.OriginEventKey.Should().Be("Confirmation");
    }

    [Fact]
    public void Una_anulacion_va_en_el_grupo_de_su_tipo_y_su_fecha()
    {
        var original = Ajuste(new DateOnly(2026, 3, 14));
        var anulada = new DocumentRefV1 { PublicId = original.Sobre.Origin.PublicId, DocumentClass = DocumentClass.PositiveAdjustment, Number = original.Sobre.Origin.Number };
        var contenido = new DocumentoAnuladoV1 { Reason = "error", VoidedDocument = anulada, VoidedDocumentTypeCode = "AJP" };
        var sobre = EscenarioContable.Sobre(DocumentoAnuladoV1.Type, "Voiding", "ANU", "AN-1", new DateOnly(2026, 3, 20), relacionado: anulada);
        var anulacion = new MensajeDeUnidad(sobre with { Payload = contenido }, contenido);

        var trabajos = AgrupadorDeResumidos.Planear([Entrante(original, Resumido), Entrante(anulacion, Resumido)], IntegrationDestinations.Accounting);

        trabajos.Should().HaveCount(2, "nunca se mezclan con los del original");
        AgrupadorDeResumidos.ClaveDe([Entrante(anulacion, Resumido)]).Should().StartWith("2026-03-20|ANU|");
    }

    [Fact]
    public void La_clave_del_ajuste_de_costo_usa_la_fecha_efectiva()
    {
        var unidad = EscenarioContable.Unidad(EscenarioContable.Caso("15-ajuste-de-costo-diferencia-de-precio"));
        AgrupadorDeResumidos.ClaveDe(unidad.Select(m => Entrante(m, Resumido)).ToList()).Should().StartWith("2026-03-12|FP|AjusteDeCosto|");
    }
}
