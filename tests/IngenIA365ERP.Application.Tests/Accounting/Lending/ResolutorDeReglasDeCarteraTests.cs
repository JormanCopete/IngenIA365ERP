using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting.Lending;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using O = IngenIA365ERP.Application.Accounting.Lending.Reglas.OperacionesDeCartera;
using R = IngenIA365ERP.Application.Accounting.Lending.Reglas.RolesDeCuentaDeCartera;

namespace IngenIA365ERP.Application.Tests.Accounting.Lending;

/// <summary>
/// Feature 013, T0613 (T52, C77; contracts/contabilidad.md §2.1, §2.2, §3.3 y §3.4): la resolución de la matriz de Cartera. Las exigidas por
/// igualdad exacta y las opcionales por igualdad o <c>*</c>; pesos línea 512, empleado 256, gracia 128, sub-segmento 64, reestructurado 32,
/// por cuenta del asociado 16, sucursal 4 y hecho 2 sólo en los roles de resultado; el centro de costo se asigna a la línea y no se compara
/// ni pesa; el hecho <c>*</c> es obligatorio en saldo, orden, pasivo, puente, diferido y contingentes; la vigencia por la fecha contable del
/// hecho (también en un reverso); importe cero no pide regla; sin candidata, <c>Missing</c> con sus datos; dos candidatas nunca empatan. Y los
/// catálogos fijos de §2.1 y §2.2.
/// </summary>
public class ResolutorDeReglasDeCarteraTests
{
    private static readonly DateOnly Noviembre = new(2026, 11, 14);

    private static PeticionDeReglaDeCartera Capital(string categoria = "A", string? linea = "LIB01", string subsegmento = "ConLibranza", bool empleado = false,
        Guid? sucursal = null, DateOnly? fecha = null, decimal importe = 100m, string operacion = "PagoAplicado") =>
        new(operacion, R.Capital,
            new ValoresDeCartera(linea, "CONSUMO", subsegmento, categoria, "Principal", empleado, false, false, false, "Receipts", null, sucursal),
            fecha ?? Noviembre, importe, ["000123"]);

    private static async Task<MatrizVigenteDeCartera> CargarAsync(MatrizDePruebaDeCartera m, DateOnly? desde = null, DateOnly? hasta = null) =>
        await new ResolutorDeReglasDeCartera(m.D.Db).CargarAsync(desde ?? MatrizDePruebaDeCartera.Enero1, hasta ?? new DateOnly(2027, 12, 31), default);

    // ------------------------------------------------------------------------------------------ catálogos fijos --

    [Fact]
    public void Los_hechos_son_los_37_del_catalogo_de_mensajes_con_su_codigo_corto()
    {
        // K6b (T1056): suma la corrección monetaria UVR (CMU), de negocio; y el reparto de un pago corregido por la UVR (PRN, 2026-10-17).
        O.Todas.Should().HaveCount(37);
        O.Todas.Select(o => (o.Codigo, o.Corto, o.EsInformativo)).Should().Equal(
            CatalogoDeMensajesDeCarteraV1.HechosAContabilidad.Select(t => (t.Type, t.CodigoCorto!, t.Kind == IntegrationMessageKind.Informational)),
            "el código de operación de la matriz es el Type del mensaje (§2.1, C110)");
        O.Todas.Count(o => o.EsInformativo).Should().Be(6);
        O.DeNegocio.Should().HaveCount(31);
        O.Todas.SelectMany(o => o.Roles).Should().OnlyContain(r => R.Buscar(r) != null, "todo hecho nombra roles del catálogo");
        O.Todas.Where(o => o.EsInformativo).Should().OnlyContain(o => o.Roles.Count == 0 && o.Comprobante == null);
        O.Todas.Where(o => o.HeredaElModo).Select(o => o.Codigo).Should().BeEquivalentTo([DesembolsoReversadoV1.Type, PagoReversadoV1.Type, ReversoDeCierreV1.Type]);
    }

    [Fact]
    public void El_comprobante_y_el_modo_por_defecto_coinciden_con_la_semilla_de_los_tipos_de_hecho()
    {
        foreach (var o in O.Todas)
        {
            var semilla = LendingFactTypesSeeder.Datos[o.Codigo];
            o.Comprobante.Should().Be(o.EsInformativo ? null : semilla.Comprobante, o.Codigo);
            if (o.ModoPorDefecto is O.EnLinea or O.PorLotes or O.NoPasa)
                o.ModoPorDefecto.Should().Be(semilla.Modo, o.Codigo);
            if (o.ModoPorDefecto == O.PorLotes) o.DisparadorPorDefecto.Should().Be(semilla.Disparador, o.Codigo);
        }
    }

    [Fact]
    public void Los_acompanantes_y_el_principal_de_una_unidad_son_los_de_mensajes_7_2()
    {
        O.Buscar(PagoAplicadoV1.Type)!.Acompanantes.Should().BeEquivalentTo(
        [
            AnticipoRecibidoV1.Type, CobroDeInteresEnSuspensoV1.Type, RecuperacionDeCastigadaV1.Type, SaldoAFavorReconocidoV1.Type,
            AjusteDeCausacionPorFechaValorV1.Type,
        ]);
        O.Principal([AnticipoRecibidoV1.Type, PagoAplicadoV1.Type, CobroDeInteresEnSuspensoV1.Type, SaldoAFavorReconocidoV1.Type])
            .Should().Be(PagoAplicadoV1.Type, "el tipo de la unidad lo da el hecho principal (C01, C93)");
        O.Principal([SaldoAFavorReconocidoV1.Type, CreditoAjustadoDesdeComercialV1.Type]).Should().Be(CreditoAjustadoDesdeComercialV1.Type);
        O.Principal([InteresCausadoV1.Type]).Should().Be(InteresCausadoV1.Type);
        O.EsAcompanante(AnticipoRecibidoV1.Type).Should().BeTrue();
        O.EsAcompanante(PagoAplicadoV1.Type).Should().BeFalse();
    }

    [Fact]
    public void Los_roles_son_los_26_de_la_tabla_de_2_2()
    {
        R.Todos.Should().HaveCount(26);
        R.Buscar(R.Capital)!.Exigidas.Should().Equal(DimensionDeCartera.Modality, DimensionDeCartera.RiskCategory);
        R.Buscar(R.Capital)!.Opcionales.Should().Equal(DimensionDeCartera.CreditLine, DimensionDeCartera.IsEmployee, DimensionDeCartera.Subsegment,
            DimensionDeCartera.Restructured, DimensionDeCartera.Branch);
        R.Buscar(R.InteresPorCobrar)!.Conceptos.Should().Equal("CurrentInterest", "DefaultInterest");
        R.Buscar(R.RecaudoPorAplicar)!.Exigidas.Should().Equal(DimensionDeCartera.CollectorClass);
        R.Buscar(R.RecaudoPorAplicar)!.Tercero.Should().Be(TerceroNaturalDeCartera.Pagador);
        R.Buscar(R.SeguroPorPagar)!.Tercero.Should().Be(TerceroNaturalDeCartera.Aseguradora);
        R.Buscar(R.DesembolsoPorGirar)!.Tercero.Should().Be(TerceroNaturalDeCartera.Beneficiario);
        R.Buscar(R.PuenteComercial)!.Cruce.Should().Be(CruceDeRol.Factura);
        R.Buscar(R.CuentaPorCobrarComercial)!.Cruce.Should().Be(CruceDeRol.Factura, "los dos roles de Comercial cruzan contra la factura (C79)");
        R.Buscar(R.ContingenteAcreedora)!.Naturaleza.Should().Be(AccountNature.Credit, "C91: 91xx es acreedora contingente");
        R.Buscar(R.ContraContingenteAcreedora)!.Naturaleza.Should().Be(AccountNature.Debit);
        R.Todos.Where(r => r.AdmiteHecho).Select(r => r.Codigo).Should().BeEquivalentTo(
            [R.Ingreso, R.GastoDeterioro, R.RecuperacionDeterioro, R.GastoDeCastigoOCondonacion, R.IngresoPorRecuperacion],
            "el hecho sólo pesa en los roles de resultado (C77)");
        R.Todos.Where(r => !r.AdmiteHecho).Should().OnlyContain(r => !r.Opcionales.Contains(DimensionDeCartera.Operation));

        var (exigidas, _) = R.DimensionesDe(R.Buscar(R.GastoDeterioro)!, R.Individual);
        exigidas.Should().Contain([DimensionDeCartera.Reason, DimensionDeCartera.Modality, DimensionDeCartera.Concept]);
        R.DimensionesDe(R.Buscar(R.GastoDeterioro)!, R.General).Exigidas.Should().Equal(DimensionDeCartera.Reason);
    }

    [Fact]
    public void De_concepto_a_rol_como_la_tabla_de_2_2()
    {
        R.RolDelConcepto("Principal").Should().Be(R.Capital);
        R.RolDelConcepto("DefaultInterest").Should().Be(R.InteresPorCobrar);
        R.RolDelConcepto("CurrentInterestInSuspense").Should().Be(R.SuspensoEnOrden);
        R.RolDelConcepto("CurrentInterestInSuspense", castigado: true).Should().Be(R.CastigadaEnOrden, "C81: el suspenso de un castigado va a control");
        R.RolDelConcepto("CollectionFee").Should().Be(R.CargoPorCobrar);
        R.RolDelConcepto("CollectionFee", porCuentaDelAsociado: true).Should().Be(R.PagoPorCuentaDelAsociado);
        R.RolDelConcepto("Advance").Should().Be(R.Anticipo);
        R.RolDelConcepto("CreditBalance").Should().Be(R.SaldoAFavor);
        R.ContraDeOrden(R.SuspensoEnOrden).Should().Be(R.ContraOrdenContingente);
        R.ContraDeOrden(R.CastigadaEnOrden).Should().Be(R.ContraOrdenControl);
    }

    [Fact]
    public void Dos_candidatas_nunca_empatan_porque_los_pesos_son_potencias_de_dos()
    {
        int[] pesos =
        [
            LendingPostingRule.PesoLinea, LendingPostingRule.PesoEmpleado, LendingPostingRule.PesoGracia, LendingPostingRule.PesoSubsegmento,
            LendingPostingRule.PesoReestructurado, LendingPostingRule.PesoPorCuentaDelAsociado, LendingPostingRule.PesoSucursal, LendingPostingRule.PesoHecho,
        ];
        pesos.Should().Equal(512, 256, 128, 64, 32, 16, 4, 2);
        var sumas = Enumerable.Range(0, 1 << pesos.Length)
            .Select(mascara => pesos.Where((_, i) => (mascara & (1 << i)) != 0).Sum())
            .ToList();
        sumas.Should().OnlyHaveUniqueItems("cada combinación de opcionales fijadas tiene un peso propio (§3.3, paso 4)");
    }

    // --------------------------------------------------------------------------------------------- resolución --

    [Fact]
    public async Task Las_exigidas_se_comparan_por_igualdad_exacta()
    {
        var m = new MatrizDePruebaDeCartera();
        m.Regla(R.Capital, m.CapitalA, modalidad: "CONSUMO", categoria: "B");

        var r = (await CargarAsync(m)).Resolver(Capital(categoria: "A"));

        r.IsFailure.Should().BeTrue("una regla de otra categoría nunca sirve");
        r.Error.Code.Should().Be("Accounting.LendingRule.Missing");
    }

    [Fact]
    public async Task Gana_la_de_mayor_peso_y_las_opcionales_casan_con_comodin()
    {
        var m = new MatrizDePruebaDeCartera();
        var general = m.Regla(R.Capital, m.CapitalConsumoA, modalidad: "CONSUMO", categoria: "A");
        var libranza = m.Regla(R.Capital, m.CapitalA, modalidad: "CONSUMO", subsegmento: "ConLibranza", categoria: "A");
        var empleados = m.Regla(R.Capital, m.CapitalEmpleadosA, modalidad: "CONSUMO", categoria: "A", empleado: true);
        var linea = m.Regla(R.Capital, m.CapitalLinea, modalidad: "CONSUMO", categoria: "A", linea: "LIB01");
        var matriz = await CargarAsync(m);

        matriz.Resolver(Capital(linea: "COM01", subsegmento: "SinLibranza")).Value!.Regla.Id.Should().Be(general.Id, "sin libranza cae en el comodín");
        matriz.Resolver(Capital(linea: "COM02")).Value!.Regla.Id.Should().Be(libranza.Id, "sub-segmento 64 le gana a 0");
        matriz.Resolver(Capital(linea: "COM02", empleado: true)).Value!.Regla.Id.Should().Be(empleados.Id,
            "empleado 256 le gana a sub-segmento 64: en el CUIF «créditos a empleados» le gana a la libranza (§24, ejemplo 12)");
        matriz.Resolver(Capital(empleado: true)).Value!.Regla.Id.Should().Be(linea.Id, "línea 512 le gana a empleado 256");
        empleados.SpecificityWeight.Should().Be(256);
        libranza.SpecificityWeight.Should().Be(64);
        general.SpecificityWeight.Should().Be(0);
    }

    [Fact]
    public async Task La_sucursal_pesa_4_y_se_compara_por_su_Id()
    {
        var m = new MatrizDePruebaDeCartera();
        m.Regla(R.RecaudoPorAplicar, m.Puente, clase: "Receipts");
        var norte = m.Regla(R.RecaudoPorAplicar, m.CapitalA, clase: "Receipts", sucursal: m.D.Norte.Id);
        var matriz = await CargarAsync(m);
        PeticionDeReglaDeCartera Recaudo(Guid sucursal) => new(PagoAplicadoV1.Type, R.RecaudoPorAplicar,
            new ValoresDeCartera(CollectorClass: "Receipts", BranchPublicId: sucursal), Noviembre, 700_000m, ["000123"]);

        var enNorte = matriz.Resolver(Recaudo(m.D.Norte.PublicId)).Value!;
        enNorte.Regla.Id.Should().Be(norte.Id);
        enNorte.BranchId.Should().Be(m.D.Norte.Id, "la sucursal llega por PublicId y se traduce a Id");
        matriz.Resolver(Recaudo(m.D.Principal.PublicId)).Value!.Cuenta.Code.Should().Be("11059501");
        norte.SpecificityWeight.Should().Be(4);
    }

    [Fact]
    public async Task El_centro_de_costo_se_asigna_a_la_linea_y_no_se_compara_ni_pesa()
    {
        var m = new MatrizDePruebaDeCartera();
        var conCentro = m.Regla(R.Ingreso, m.Ingreso, modalidad: "CONSUMO", concepto: "CurrentInterest", centro: m.D.Centro.Id);
        var matriz = await CargarAsync(m);

        var r = matriz.Resolver(new PeticionDeReglaDeCartera(InteresCausadoV1.Type, R.Ingreso,
            new ValoresDeCartera(ModalityCode: "CONSUMO", ConceptCode: "CurrentInterest", BranchPublicId: m.D.Principal.PublicId), Noviembre, 6452.48m, []));

        r.Value!.Regla.Id.Should().Be(conCentro.Id, "Cartera no conoce centros: el hecho no lo trae y la regla igual casa");
        r.Value.CostCenterId.Should().Be(m.D.Centro.Id, "el centro de la regla ganadora va a la línea");
        conCentro.SpecificityWeight.Should().Be(0, "el centro no pesa (C77)");
        conCentro.DimensionKey.Should().NotContain(m.D.Centro.Id.ToString() + "|", "el centro no entra en la clave");
        conCentro.DimensionKey.Should().Be("*|Ingreso|*|CONSUMO|*|*|CurrentInterest|*|*|*|*|*|*|*");
    }

    [Fact]
    public async Task El_hecho_pesa_2_solo_en_los_roles_de_resultado()
    {
        var m = new MatrizDePruebaDeCartera();
        var general = m.Regla(R.Ingreso, m.Ingreso, modalidad: "CONSUMO", concepto: "CurrentInterest");
        var delAjuste = m.Regla(R.Ingreso, m.IngresoDelAjuste, CreditoAjustadoDesdeComercialV1.Type, modalidad: "CONSUMO", concepto: "CurrentInterest");
        var matriz = await CargarAsync(m);
        PeticionDeReglaDeCartera Ingreso(string hecho) => new(hecho, R.Ingreso,
            new ValoresDeCartera(ModalityCode: "CONSUMO", ConceptCode: "CurrentInterest"), Noviembre, 50m, []);

        delAjuste.SpecificityWeight.Should().Be(2);
        matriz.Resolver(Ingreso(CreditoAjustadoDesdeComercialV1.Type)).Value!.Regla.Id.Should().Be(delAjuste.Id, "el ingreso que se reversa en un ajuste de Comercial");
        matriz.Resolver(Ingreso(InteresCausadoV1.Type)).Value!.Regla.Id.Should().Be(general.Id);

        // En un rol de saldo el hecho va en «*»: guardar una regla con el hecho fijado se rechaza.
        var forma = ReglasDeLaMatrizDeCartera.Forma(PagoAplicadoV1.Type, R.Capital, null,
            ReglasDeLaMatrizDeCartera.Presentes(PagoAplicadoV1.Type, null, "CONSUMO", null, "A", null, null, null, null, null, null, null, false));
        forma.Select(e => e.Code).Should().Equal("Accounting.LendingRule.DimensionNotAllowed");
        foreach (var rol in new[] { R.SuspensoEnOrden, R.Anticipo, R.SeguroPorPagar, R.RecaudoPorAplicar, R.IngresoDiferido, R.ContingenteAcreedora })
            R.Buscar(rol)!.AdmiteHecho.Should().BeFalse($"{rol}: hecho * obligatorio en saldo, orden, pasivo, puente, diferido y contingentes");
    }

    [Fact]
    public async Task La_vigencia_se_mira_en_la_fecha_contable_del_hecho_tambien_en_un_reverso()
    {
        var m = new MatrizDePruebaDeCartera();
        var vieja = m.Regla(R.Capital, m.CapitalA, modalidad: "CONSUMO", categoria: "A", hasta: new DateOnly(2026, 11, 30));
        var nueva = m.Regla(R.Capital, m.CapitalConsumoA, modalidad: "CONSUMO", categoria: "A", desde: new DateOnly(2026, 12, 1));
        var matriz = await CargarAsync(m);

        matriz.Resolver(Capital(fecha: new DateOnly(2026, 11, 20))).Value!.Regla.Id.Should().Be(vieja.Id);
        matriz.Resolver(Capital(fecha: new DateOnly(2026, 12, 1), operacion: PagoReversadoV1.Type)).Value!.Regla.Id
            .Should().Be(nueva.Id, "un reverso fechado en diciembre resuelve con la matriz de su fecha, no con la del pago de noviembre (T55)");
        (await CargarAsync(m, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31))).Resolver(Capital(fecha: new DateOnly(2025, 12, 31)))
            .Error.Code.Should().Be("Accounting.LendingRule.Missing", "antes de toda vigencia no hay regla");
    }

    [Fact]
    public async Task Un_importe_cero_no_pide_regla()
    {
        var m = new MatrizDePruebaDeCartera();

        var r = (await CargarAsync(m)).Resolver(Capital(importe: 0m));

        r.IsSuccess.Should().BeTrue();
        r.Value.Should().BeNull("un pago sin mora no pide regla de mora");
    }

    [Fact]
    public async Task Sin_candidata_responde_Missing_con_hecho_rol_valores_fecha_y_creditos()
    {
        var m = new MatrizDePruebaDeCartera();

        var r = (await CargarAsync(m)).Resolver(Capital(categoria: "E1", sucursal: m.D.Principal.PublicId));

        r.Error.Code.Should().Be("Accounting.LendingRule.Missing");
        var datos = ((ErrorConDatos)r.Error).Data;
        Propiedad(datos, "operation").Should().Be("PagoAplicado");
        Propiedad(datos, "role").Should().Be("Capital");
        Propiedad(datos, "date").Should().Be(Noviembre);
        ((IReadOnlyList<string>)Propiedad(datos, "loans")!).Should().Equal("000123");
        var valores = Propiedad(datos, "values")!;
        Propiedad(valores, "modalityCode").Should().Be("CONSUMO");
        Propiedad(valores, "riskCategory").Should().Be("E", "las subcategorías van a su letra (§3.4)");
        Propiedad(valores, "branch").Should().Be(m.D.Principal.PublicId);
        r.Error.Message.Should().Contain("Contabilidad › Cartera › Matriz");
    }

    [Fact]
    public async Task La_subcategoria_casa_con_la_regla_de_su_letra()
    {
        var m = new MatrizDePruebaDeCartera();
        var e = m.Regla(R.Capital, m.CapitalA, modalidad: "CONSUMO", categoria: "E2");

        e.RiskCategory.Should().Be("E", "se guarda por su letra");
        (await CargarAsync(m)).Resolver(Capital(categoria: "E1")).Value!.Regla.Id.Should().Be(e.Id);
    }

    [Fact]
    public async Task Las_candidatas_de_una_combinacion_dicen_cual_caso()
    {
        var m = new MatrizDePruebaDeCartera();
        var general = m.Regla(R.Capital, m.CapitalConsumoA, modalidad: "CONSUMO", categoria: "A");
        var empleados = m.Regla(R.Capital, m.CapitalEmpleadosA, modalidad: "CONSUMO", categoria: "A", empleado: true);

        var candidatas = (await CargarAsync(m)).CandidatasDe(Capital());

        candidatas.Should().HaveCount(2);
        candidatas[0].Regla.Id.Should().Be(general.Id);
        candidatas[0].Coincide.Should().BeTrue();
        candidatas.Single(c => c.Regla.Id == empleados.Id).Coincide.Should().BeFalse("la deudora no es empleada");
    }

    private static object? Propiedad(object objeto, string nombre) => objeto.GetType().GetProperty(nombre)!.GetValue(objeto);
}
