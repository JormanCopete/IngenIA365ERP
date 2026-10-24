using FluentAssertions;
using IngenIA365ERP.Application.Accounting.RecaudoYTesoreria;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;

namespace IngenIA365ERP.Application.Tests.Accounting.Receipts;

/// <summary>
/// Feature 015, US8, T0249 (FR-143, FR-227): la plantilla lista filas sugeridas <b>sin cuenta</b> según la configuración real —una por caja en <c>Caja</c>, por
/// naturaleza en el puente del módulo, por concepto, por cuenta propia, por clase de orden; en los roles de resultado, una por hecho— y ninguna regla viene sembrada;
/// la completitud cuenta cada hecho y deja aparte los roles por vínculo. Prueba escrita, sin correr.
/// </summary>
public class PlantillaYCompletitudTests
{
    private static readonly IReadOnlyList<DimensionDeMatrizDto> ConfiguracionDeRecaudo =
    [
        new("BranchCode", "01", "Principal"),
        new("CollectionPointCode", "P1", "Taquilla", "01"),
        new("CashBoxCode", "C1", "Caja 1", "P1"),
        new("CashBoxCode", "C2", "Caja 2", "P1"),
        new("MeansCode", "EF", "Efectivo", "P1"),
        new("MiscConceptCode", "REI", "Reintegro"),
    ];

    [Fact]
    public void La_plantilla_sugiere_una_fila_por_caja_naturaleza_y_concepto_sin_cuenta()
    {
        var filas = PlantillaDeLaMatriz.Filas(MatricesDeRecaudoYTesoreria.Recaudo, ConfiguracionDeRecaudo);

        filas.Where(f => f.Role == RolesDeRecaudo.Caja).Select(f => f.Dimensions.CashBoxCode).Should().BeEquivalentTo(["C1", "C2"]);
        filas.Where(f => f.Role == RolesDeRecaudo.PuenteDelModulo).Select(f => f.Dimensions.TargetNature).Should().BeEquivalentTo(["Loan", "Deposit", "Contribution"]);
        filas.Where(f => f.Role == RolesDeRecaudo.ConceptoVario).Select(f => f.Dimensions.MiscConceptCode).Should().BeEquivalentTo(["REI"]);
        filas.Should().NotContain(f => f.Role == RolesDeRecaudo.BancoRecaudo, "el banco sale del vínculo");
        filas.Where(f => f.Role == RolesDeRecaudo.GastoFaltante).Should().OnlyContain(f => f.FactType != "*", "los roles de resultado van por hecho");
        filas.Where(f => f.Role != RolesDeRecaudo.GastoFaltante && f.Role != RolesDeRecaudo.IngresoSobrantes).Should().OnlyContain(f => f.FactType == "*");
        PlantillaDeLaMatriz.Tabla(MatricesDeRecaudoYTesoreria.Recaudo, filas).Filas.Should().OnlyContain(f => f.Valores[4] == null, "ninguna fila trae cuenta");
    }

    [Fact]
    public void En_Tesoreria_la_plantilla_va_por_clase_de_orden_y_cuenta_propia()
    {
        var filas = PlantillaDeLaMatriz.Filas(MatricesDeRecaudoYTesoreria.Tesoreria,
        [
            new("OwnBankAccountCode", "BCO1", "Cuenta corriente"),
            new("OrderOrigin", "LendingDisbursement", "LendingDisbursement"),
            new("OrderKind", "LoanDisbursement", "LoanDisbursement", "LendingDisbursement"),
            new("OrderKind", "PayerCreditBalanceRefund", "PayerCreditBalanceRefund", "Receipts"),
        ]);

        filas.Where(f => f.Role == RolesDeTesoreria.PuenteDeGiroDelOrigen).Select(f => f.Dimensions.OrderOrigin).Should().Contain("LendingDisbursement");
        filas.Where(f => f.Role == RolesDeTesoreria.PuenteDeDevolucion).Select(f => f.Dimensions.OrderKind).Should().Contain("PayerCreditBalanceRefund");
        filas.Where(f => f.Role == RolesDeTesoreria.EfectivoEnTransito).Should().NotBeEmpty();
    }

    [Fact]
    public void Cada_hecho_de_los_dos_modulos_tiene_su_tipo_de_comprobante_sugerido_y_sus_roles_en_la_matriz()
    {
        foreach (var modulo in MatricesDeRecaudoYTesoreria.Todas)
            foreach (var hecho in modulo.Hechos)
            {
                hecho.ComprobanteSugerido.Should().BeOneOf("RR", "RB", "MC", "AR", "TP");
                hecho.RolesPosibles.Should().OnlyContain(r => modulo.Rol(r) != null, $"{hecho.Tipo} sólo mueve roles de {modulo.Nombre}");
            }
    }
}
