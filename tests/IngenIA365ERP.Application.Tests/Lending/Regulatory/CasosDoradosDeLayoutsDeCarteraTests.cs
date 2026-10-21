using System.Runtime.CompilerServices;
using FluentAssertions;
using IngenIA365ERP.Application.Lending.Regulatory;
using IngenIA365ERP.Domain.Common.Layouts;
using IngenIA365ERP.Domain.Entities.Lending.Documents;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Tests.Lending.Regulatory;

/// <summary>
/// T0991 (feature 013, K5, US16; quickstart §1.4; adaptadores-y-archivos.md §7.6; SC-019): los archivos dorados de los layouts de Cartera, comparados
/// <b>byte a byte</b> con <c>Casos/*.esperado.txt</c>: el formato de cartera del SICSES de un cierre de doce créditos de todas las modalidades de K1, y
/// el archivo de cada central sembrada (DATACREDITO y TRANSUNION) con titulares al día, en mora con aviso, castigado, cancelado, codeudor, con reclamo,
/// retiros (sin aviso y anulación desde el origen) y un excluido que no entra. Las filas salen de las mismas funciones puras que usan las proyecciones
/// (<see cref="FilasDelSicses"/>, <see cref="FilasDeCentral"/>) con el layout embebido vigente. Con <c>CARTERA_ESCRIBIR_ESPERADO=1</c> se reescriben
/// —sólo a propósito, tras cotejarlos a mano; molde <c>PILA_ESCRIBIR_ESPERADO</c>—; sin la variable nunca se escriben.
/// </summary>
public class CasosDoradosDeLayoutsDeCarteraTests
{
    private static readonly DateOnly Corte = new(2026, 12, 31);

    [Fact]
    public void El_formato_del_SICSES_de_doce_creditos_es_el_dorado()
    {
        var layout = CatalogoDeLayoutsDeCartera.Embebido.Vigente(RegulatoryFileKind.Sicses, null, Corte)!.Layout;
        var creditos = DoceCreditos();
        creditos.Select(c => c.Modalidad).Distinct().Should().BeEquivalentTo(
            ["CONSUMO", "CONSUMO_LIBRANZA", "CONSUMO_SIN_LIBRANZA", "COMERCIAL_PN", "COMERCIAL_PJ", "VIVIENDA", "MICROCREDITO"], "todas las modalidades de K1");

        var escritura = EscritorPorLayout.Escribir(layout, FilasDelSicses.Construir(creditos));

        escritura.Bloqueantes.Should().Be(0, string.Join(" | ", escritura.Inconsistencias.Select(i => i.Mensaje)));
        escritura.Lineas.Should().Be(12);
        EscritorPorLayout.Escribir(layout, FilasDelSicses.Construir(DoceCreditos())).Sha256.Should().Be(escritura.Sha256, "las mismas filas, los mismos bytes");
        Comparar("sicses-cartera-202612", escritura.Contenido!);
    }

    [Theory]
    [InlineData("DATACREDITO", "123456", 1 + 8 + 1)]
    [InlineData("TRANSUNION", "ABC123", 8)]
    public void El_archivo_de_cada_central_es_el_dorado(string central, string suscriptor, int lineas)
    {
        var layout = CatalogoDeLayoutsDeCartera.Embebido.Vigente(RegulatoryFileKind.CreditBureau, central, Corte)!.Layout;
        var filas = FilasDeCentral.Construir(layout, new DatosDelArchivoDeCentral(central, suscriptor, Corte), Titulares());

        var escritura = EscritorPorLayout.Escribir(layout, filas);

        escritura.Bloqueantes.Should().Be(0, string.Join(" | ", escritura.Inconsistencias.Select(i => i.Mensaje)));
        escritura.Lineas.Should().Be(lineas, "el excluido no entra; DATACREDITO lleva cabecera y totales");
        escritura.Texto.Should().NotContain("EXCLUIDO");
        Comparar($"central-{central.ToLowerInvariant()}-202612", escritura.Contenido!);
    }

    private static void Comparar(string caso, byte[] contenido, [CallerFilePath] string fuente = "")
    {
        var ruta = Path.Combine(Path.GetDirectoryName(fuente)!, "Casos", caso + ".esperado.txt");
        // Regenerar a propósito (tras cotejar a mano): CARTERA_ESCRIBIR_ESPERADO=1 escribe el archivo junto al código fuente.
        if (Environment.GetEnvironmentVariable("CARTERA_ESCRIBIR_ESPERADO") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
            File.WriteAllBytes(ruta, contenido);
        }
        File.Exists(ruta).Should().BeTrue($"falta el archivo dorado {ruta}: regénerelo con CARTERA_ESCRIBIR_ESPERADO=1");
        contenido.Should().Equal(File.ReadAllBytes(ruta), $"el archivo debe ser byte a byte el de {Path.GetFileName(ruta)}");
    }

    // ---------------------------------------------------------------------------------------------------------- datos --

    private static CreditoDelSicses Credito(int n, string modalidad, string idType, string documento, RatingCategory cal, int dias, decimal capital,
        CollateralKind? garantia = null, decimal admisible = 0m, RatingSubcategory sub = RatingSubcategory.None, bool reestructurado = false,
        bool empleado = false, decimal tasa = 0.2682418m, RepaymentPeriodicity periodicidad = RepaymentPeriodicity.Monthly, bool conPago = true) => new()
    {
        LoanId = n, Numero = $"CR-{n:000000}", PersonId = 100 + n, TipoDeDocumento = idType, NumeroDeDocumento = documento, EsEmpleado = empleado,
        CodigoContable = modalidad.StartsWith("VIVIENDA", StringComparison.Ordinal) ? "14040501" : modalidad.StartsWith("COMERCIAL", StringComparison.Ordinal)
            ? "14600501" : "14110501",
        Modalidad = modalidad, Oficina = n % 2 == 0 ? "002" : "001", Linea = $"L{modalidad[..3]}", FechaDeDesembolso = new DateOnly(2025, 1 + n % 12, 10),
        FechaDeVencimiento = new DateOnly(2028, 1 + n % 12, 10), MontoDesembolsado = capital + 500_000m, TasaEfectivaAnual = tasa, Periodicidad = periodicidad,
        CuotasPactadas = 36, CuotasPagadas = 20 - n % 5, ValorDeLaCuota = decimal.Round((capital + 500_000m) / 30m, 2),
        FechaDelUltimoPago = conPago ? new DateOnly(2026, 12, 1 + n) : null, DiasDeMora = dias, Categoria = cal, Subcategoria = sub, Capital = capital,
        InteresCorriente = decimal.Round(capital / 100m, 2), InteresDeMora = dias > 0 ? 3_250.5m : 0m, InteresEnSuspenso = cal >= RatingCategory.C ? 8_100m : 0m,
        OtrosConceptos = 2_000m, DeterioroCapital = decimal.Round(capital * ((int)cal - 1) / 10m, 2), DeterioroIntereses = cal >= RatingCategory.C ? 3_250.5m : 0m,
        DeterioroOtros = 0m, Garantia = garantia, ValorAdmisibleDeGarantia = admisible, AportesDeducidos = 0m, Reestructurado = reestructurado,
        VecesReestructurado = reestructurado ? 1 : 0,
    };

    private static IReadOnlyList<CreditoDelSicses> DoceCreditos() =>
    [
        Credito(1, "CONSUMO", "C", "10203040", RatingCategory.A, 0, 3_000_000m),
        Credito(2, "CONSUMO_LIBRANZA", "C", "52123456", RatingCategory.A, 0, 8_500_000m, empleado: true),
        Credito(3, "CONSUMO_SIN_LIBRANZA", "CE", "E654321", RatingCategory.B, 45, 1_200_000m),
        Credito(4, "COMERCIAL_PN", "C", "79888777", RatingCategory.C, 75, 15_000_000m, CollateralKind.Pledge, 12_000_000m),
        Credito(5, "COMERCIAL_PJ", "N", "900123456", RatingCategory.A, 0, 120_000_000m, CollateralKind.Mortgage, 150_000_000m),
        Credito(6, "VIVIENDA", "C", "1020304050", RatingCategory.A, 0, 95_000_000m, CollateralKind.Mortgage, 140_000_000m, tasa: 0.12m),
        Credito(7, "MICROCREDITO", "C", "43210987", RatingCategory.D, 120, 2_400_000m, CollateralKind.GuaranteeFund, 1_200_000m, tasa: 0.4512345m),
        Credito(8, "CONSUMO", "TI", "1098765432", RatingCategory.E, 400, 650_000m, sub: RatingSubcategory.E2, conPago: false),
        Credito(9, "CONSUMO_LIBRANZA", "C", "31313131", RatingCategory.B, 35, 4_100_000m, CollateralKind.CoDebtor, reestructurado: true),
        Credito(10, "VIVIENDA", "PA", "AB1234567", RatingCategory.A, 0, 60_000_000m, CollateralKind.Mortgage, 90_000_000m, tasa: 0.11m),
        Credito(11, "COMERCIAL_PN", "C", "80555444", RatingCategory.E, 200, 7_000_000m, sub: RatingSubcategory.E1, periodicidad: RepaymentPeriodicity.Quarterly),
        Credito(12, "MICROCREDITO", "C", "1001002003", RatingCategory.A, 0, 900_000m, CollateralKind.Contributions, 300_000m, periodicidad: RepaymentPeriodicity.Biweekly),
    ];

    private static TitularDeCentral Titular(int n, string documento, string nombre, EstadoEnCentral estado, bool deudor = true, int dias = 0,
        bool reclamo = false, bool retiro = false, bool anulado = false, bool incluido = true, string? motivo = null, string idType = "C") => new()
    {
        LoanId = n, NumeroDeObligacion = $"CR-{n:000000}", PersonId = 500 + n * 2 + (deudor ? 0 : 1), EsDeudor = deudor, TipoDeDocumento = idType,
        NumeroDeDocumento = documento, Nombre = nombre, CodigoDeCiudad = "05001", Direccion = "Calle 10 # 20-30 Apto 201", Telefono = "3001234567",
        Correo = $"titular{n}@correo.test", FechaDeApertura = new DateOnly(2025, 3, n), FechaDeVencimiento = new DateOnly(2028, 3, n), Estado = estado,
        Reestructurado = n == 4, FechaDelEstadoDeOrigen = n == 4 ? new DateOnly(2026, 6, 30) : null, ValorInicial = 5_000_000m + n * 100_000m,
        SaldoTotal = estado is EstadoEnCentral.Cancelada or EstadoEnCentral.Retirado ? 0m : 3_000_000m + n * 10_000.25m,
        SaldoEnMora = dias > 0 ? 180_500.75m : 0m, ValorDeLaCuota = 210_450m, Periodicidad = RepaymentPeriodicity.Monthly, CuotasPagadas = 21, CuotasPactadas = 36,
        CuotasEnMora = dias > 0 ? 1 + dias / 30 : 0, DiasDeMora = dias, FechaLimiteDePago = estado == EstadoEnCentral.Cancelada ? null : new DateOnly(2027, 1, 10),
        FechaDelUltimoPago = new DateOnly(2026, 11, 28), ReclamoEnTramite = reclamo, RetiroSinAviso = retiro, AnuladoDesdeElOrigen = anulado,
        EsNegativo = estado is EstadoEnCentral.EnMora or EstadoEnCentral.Castigada, Incluido = incluido, MotivoDeExclusion = motivo,
    };

    private static IReadOnlyList<TitularDeCentral> Titulares() =>
    [
        Titular(1, "10203040", "María José Pérez Gómez", EstadoEnCentral.AlDia),
        Titular(1, "52123456", "Luis Ángel Muñoz", EstadoEnCentral.AlDia, deudor: false),
        Titular(2, "79888777", "Carlos Andrés Rodríguez", EstadoEnCentral.EnMora, dias: 65),
        Titular(3, "43210987", "Ana Lucía Ospina", EstadoEnCentral.Castigada, dias: 400),
        Titular(4, "31313131", "Jorge Iván Restrepo", EstadoEnCentral.Cancelada),
        Titular(5, "80555444", "Beatriz Elena Zuluaga", EstadoEnCentral.AlDia, reclamo: true),
        Titular(6, "1001002003", "Pedro Pablo Cárdenas", EstadoEnCentral.Retirado, retiro: true),
        Titular(7, "900123456", "Comercializadora del Valle SAS", EstadoEnCentral.Retirado, anulado: true, idType: "N"),
        Titular(8, "1122334455", "Excluido Sin Autorizacion", EstadoEnCentral.EnMora, dias: 40, incluido: false,
            motivo: MotivosDeExclusionDeCentral.NoReportAuthorization),
    ];
}

