using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Sales.Payments;

namespace IngenIA365ERP.Domain.Tests.Sales.Payments;

/// <summary>
/// Feature 012, I3, T549 (T25; data-model §15 «Disponibilidad de medios»; contracts/api.md §22.3): un medio se ofrece si está
/// activo y vigente, aparece en los tres conjuntos —punto, canal y tipo de documento— o tiene su marca «todos», y su clase es
/// compatible con el caso. Un conjunto vacío <b>no</b> es «todos». Los créditos no se ofrecen al consumidor final ni a quien
/// no tiene <c>Inventory.Sales.SellOnCredit</c>. La salida va por <c>DisplayOrder</c> con su tecla rápida.
/// </summary>
public class DisponibilidadDeMedioTests
{
    private const int Punto1 = 1, Punto2 = 2, CanalPos = 10, CanalOficina = 11, TipoPos = 100, TipoFactura = 101;
    private static readonly DateOnly Hoy = new(2026, 10, 1);

    private static MedioOfrecible Medio(int id, string codigo, PaymentMeansClass clase = PaymentMeansClass.Cash, short orden = 1, string? tecla = null,
        bool activo = true, DateOnly? desde = null, DateOnly? hasta = null,
        bool todosLosPuntos = true, int[]? puntos = null, bool todosLosCanales = true, int[]? canales = null,
        bool todosLosTipos = true, int[]? tipos = null) =>
        new(id, codigo, codigo, clase, orden, tecla, activo, desde ?? new DateOnly(2026, 1, 1), hasta,
            todosLosPuntos, puntos ?? [], todosLosCanales, canales ?? [], todosLosTipos, tipos ?? []);

    private static CasoDeCobro Caso(int? punto = Punto1, int? canal = CanalPos, int tipo = TipoPos, bool consumidorFinal = false, bool credito = true) =>
        new(Hoy, punto, canal, tipo, consumidorFinal, credito);

    [Fact]
    public void La_marca_todos_ofrece_en_cualquier_punto_canal_y_tipo()
    {
        var efectivo = Medio(1, "EFECTIVO");
        DisponibilidadDeMedio.SeOfrece(efectivo, Caso()).Should().BeTrue();
        DisponibilidadDeMedio.SeOfrece(efectivo, Caso(Punto2, CanalOficina, TipoFactura)).Should().BeTrue();
    }

    [Fact]
    public void Un_conjunto_explicito_ofrece_solo_en_sus_miembros_de_cada_dimension()
    {
        var visa = Medio(2, "VISARB", PaymentMeansClass.CreditCard, todosLosPuntos: false, puntos: [Punto1],
            todosLosCanales: false, canales: [CanalPos], todosLosTipos: false, tipos: [TipoPos]);

        DisponibilidadDeMedio.SeOfrece(visa, Caso()).Should().BeTrue();
        DisponibilidadDeMedio.SeOfrece(visa, Caso(punto: Punto2)).Should().BeFalse("el punto 2 no está en su conjunto");
        DisponibilidadDeMedio.SeOfrece(visa, Caso(canal: CanalOficina)).Should().BeFalse("el canal de oficina no está");
        DisponibilidadDeMedio.SeOfrece(visa, Caso(tipo: TipoFactura)).Should().BeFalse("el tipo factura no está");
    }

    [Fact]
    public void Un_conjunto_vacio_no_es_todos()
    {
        var nadie = Medio(3, "NEQUI", PaymentMeansClass.Transfer, todosLosPuntos: false, puntos: []);
        DisponibilidadDeMedio.SeOfrece(nadie, Caso()).Should().BeFalse("sin la marca y sin puntos no se ofrece en ningún punto");

        var sinTipos = Medio(4, "CHEQUE", PaymentMeansClass.Check, todosLosTipos: false, tipos: []);
        DisponibilidadDeMedio.SeOfrece(sinTipos, Caso()).Should().BeFalse();
    }

    [Fact]
    public void Sin_punto_ni_canal_en_el_caso_esas_dimensiones_no_restringen()
    {
        // La factura de oficina no tiene punto: sólo la restringen el canal (si lo trae) y el tipo.
        var soloPuntoUno = Medio(5, "TRANSF", PaymentMeansClass.Transfer, todosLosPuntos: false, puntos: [Punto1]);
        DisponibilidadDeMedio.SeOfrece(soloPuntoUno, Caso(punto: null, canal: null, tipo: TipoFactura)).Should().BeTrue();
    }

    [Fact]
    public void Un_medio_inactivo_o_fuera_de_vigencia_no_se_ofrece()
    {
        DisponibilidadDeMedio.SeOfrece(Medio(6, "VIEJO", activo: false), Caso()).Should().BeFalse();
        DisponibilidadDeMedio.SeOfrece(Medio(7, "VENCIDO", hasta: new DateOnly(2026, 9, 30)), Caso()).Should().BeFalse();
        DisponibilidadDeMedio.SeOfrece(Medio(8, "FUTURO", desde: new DateOnly(2026, 10, 2)), Caso()).Should().BeFalse();
        DisponibilidadDeMedio.SeOfrece(Medio(9, "HOYVENCE", hasta: Hoy), Caso()).Should().BeTrue("la vigencia incluye su último día");
    }

    [Theory]
    [InlineData(PaymentMeansClass.AssociateCredit)]
    [InlineData(PaymentMeansClass.CustomerCredit)]
    public void Los_creditos_no_se_ofrecen_al_consumidor_final_ni_a_quien_no_puede_vender_a_credito(PaymentMeansClass clase)
    {
        var credito = Medio(10, "CRED", clase);
        DisponibilidadDeMedio.SeOfrece(credito, Caso()).Should().BeTrue();
        DisponibilidadDeMedio.SeOfrece(credito, Caso(consumidorFinal: true)).Should().BeFalse();
        DisponibilidadDeMedio.SeOfrece(credito, Caso(credito: false)).Should().BeFalse();
        DisponibilidadDeMedio.SeOfrece(Medio(11, "EFECTIVO"), Caso(consumidorFinal: true, credito: false)).Should().BeTrue();
    }

    [Fact]
    public void Los_ofrecidos_salen_por_orden_con_su_tecla_rapida()
    {
        var medios = new[]
        {
            Medio(1, "VISARB", PaymentMeansClass.CreditCard, orden: 3, tecla: "V"),
            Medio(2, "EFECTIVO", orden: 1, tecla: "E"),
            Medio(3, "VIEJO", orden: 0, activo: false),
            Medio(4, "BONO", PaymentMeansClass.Voucher, orden: 2, tecla: "B"),
            Medio(5, "CREDASOC", PaymentMeansClass.AssociateCredit, orden: 4),
        };

        var ofrecidos = DisponibilidadDeMedio.Ofrecidos(medios, Caso(consumidorFinal: true));

        ofrecidos.Select(m => m.Code).Should().Equal("EFECTIVO", "BONO", "VISARB");
        ofrecidos.Select(m => m.QuickKey).Should().Equal("E", "B", "V");
    }
}
