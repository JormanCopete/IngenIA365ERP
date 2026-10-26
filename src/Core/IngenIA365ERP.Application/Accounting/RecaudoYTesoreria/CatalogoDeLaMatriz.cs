using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;

namespace IngenIA365ERP.Application.Accounting.RecaudoYTesoreria;

/// <summary>Un rol de la matriz (contabilidad.md §5): si se resuelve por el vínculo de la cuenta propia (no admite regla), si es de resultado y qué dimensiones pesan. (nuevo)</summary>
public sealed record RolDeLaMatriz(string Codigo, string Nombre, bool PorVinculo, bool DeResultado, IReadOnlyList<string> Dimensiones);

/// <summary>Un hecho que pasa a Contabilidad (contabilidad.md §3, §4): su tipo sugerido de comprobante, los roles que siempre mueve y los que puede mover. (nuevo)</summary>
public sealed record HechoDeLaMatriz(string Tipo, string Nombre, string ComprobanteSugerido, IReadOnlyList<string> RolesObligatorios, IReadOnlyList<string> RolesPosibles);

/// <summary>
/// Una de las dos matrices (feature 015, T0257; contabilidad.md §3 a §6): el módulo de origen (<c>REC</c> o <c>TES</c>), el módulo de sus parámetros (<c>RCD</c> o
/// <c>TES</c>), sus roles y sus hechos, y los códigos de error y de permiso que llevan su nombre. Contabilidad no lee tablas de Recaudo ni de Tesorería: lo que sabe
/// de ellos está aquí, en el sobre y en el contenido de cada hecho. (nuevo)
/// </summary>
public sealed record ModuloDeLaMatriz(
    string Codigo,
    string Nombre,
    string ModuloDeParametros,
    string PrefijoDeErrores,
    string PermisoDeLaMatriz,
    IReadOnlyList<RolDeLaMatriz> Roles,
    IReadOnlyList<HechoDeLaMatriz> Hechos)
{
    public RolDeLaMatriz? Rol(string codigo) => Roles.FirstOrDefault(r => string.Equals(r.Codigo, codigo, StringComparison.Ordinal));

    public HechoDeLaMatriz? Hecho(string tipo) => Hechos.FirstOrDefault(h => string.Equals(h.Tipo, tipo, StringComparison.Ordinal));

    /// <summary>El origen del comprobante resumido (<c>AccountingOrigin.SourceType</c>).</summary>
    public string OrigenDelLote => Codigo == ModuloContable.Recaudo ? "ReceiptsPostingBatch" : "TreasuryPostingBatch";
}

/// <summary>Las dos matrices. (nuevo)</summary>
public static class MatricesDeRecaudoYTesoreria
{
    // Las dimensiones, con el nombre que llevan en el contenido del hecho y en la regla.
    public const string Oficina = "BranchCode";
    public const string Punto = "CollectionPointCode";
    public const string Caja = "CashBoxCode";
    public const string Medio = "MeansCode";
    public const string ClaseDeMedio = "MeansClass";
    public const string Concepto = "MiscConceptCode";
    public const string Modulo = "TargetModule";
    public const string Naturaleza = "TargetNature";
    public const string ClaseDeRecaudador = "CollectorClass";
    public const string CuentaPropia = "OwnBankAccountCode";
    public const string OrigenDeLaOrden = "OrderOrigin";
    public const string ClaseDeOrden = "OrderKind";
    public const string Causa = "Cause";

    /// <summary>Todas las dimensiones de una regla, en el orden de su clave.</summary>
    public static IReadOnlyList<string> Dimensiones { get; } =
        [Oficina, Punto, Caja, Medio, ClaseDeMedio, Concepto, Modulo, Naturaleza, ClaseDeRecaudador, CuentaPropia, OrigenDeLaOrden, ClaseDeOrden, Causa];

    private static RolDeLaMatriz R(string codigo, string nombre, params string[] dimensiones) => new(codigo, nombre, false, false, dimensiones);

    private static RolDeLaMatriz Resultado(string codigo, string nombre, params string[] dimensiones) => new(codigo, nombre, false, true, dimensiones);

    private static RolDeLaMatriz Vinculo(string codigo, string nombre) => new(codigo, nombre, true, false, []);

    private static HechoDeLaMatriz H(string tipo, string nombre, string comprobante, string[] obligatorios, params string[] posibles) =>
        new(tipo, nombre, comprobante, obligatorios, obligatorios.Concat(posibles).Distinct(StringComparer.Ordinal).ToList());

    public static ModuloDeLaMatriz Recaudo { get; } = new(
        ModuloContable.Recaudo, "Recaudo", Domain.Receipts.CatalogoDeRecaudo.Modulo, "Accounting.ReceiptsRule", "Accounting.ReceiptsMatrix",
        [
            R(RolesDeRecaudo.Caja, "Caja", Caja, Punto, Oficina, Medio, ClaseDeMedio),
            R(RolesDeRecaudo.Boveda, "Bóveda", Oficina),
            R(RolesDeRecaudo.EfectivoEnTransito, "Efectivo en tránsito", Oficina, CuentaPropia),
            Vinculo(RolesDeRecaudo.BancoRecaudo, "Banco del recaudo (por el vínculo de la cuenta propia)"),
            R(RolesDeRecaudo.ChequesEnCanje, "Cheques en canje", Oficina),
            R(RolesDeRecaudo.MedioPorCobrar, "Medio por cobrar", Medio, ClaseDeMedio),
            R(RolesDeRecaudo.PuenteDelModulo, "Puente del módulo (recaudo por aplicar)", Modulo, Naturaleza, ClaseDeRecaudador, Oficina),
            R(RolesDeRecaudo.ConceptoVario, "Concepto vario", Concepto),
            R(RolesDeRecaudo.PartidasPorIdentificar, "Partidas por identificar", CuentaPropia),
            R(RolesDeRecaudo.SaldoAFavorDePagadores, "Saldos a favor de pagadores"),
            R(RolesDeRecaudo.SaldoDePagadurias, "Saldo de pagadurías"),
            R(RolesDeRecaudo.CuentaPorCobrarCajero, "Cuenta por cobrar al cajero"),
            R(RolesDeRecaudo.CuentaPorCobrarPagador, "Cuenta por cobrar al pagador"),
            Resultado(RolesDeRecaudo.GastoFaltante, "Gasto por faltante de caja", Oficina),
            R(RolesDeRecaudo.SobrantesDeCaja, "Sobrantes de caja", Oficina),
            Resultado(RolesDeRecaudo.IngresoSobrantes, "Ingreso por sobrantes", Oficina),
            R(RolesDeRecaudo.CustodiaPorLista, "Dinero en custodia por listas", Oficina),
            R(RolesDeRecaudo.PuenteDeDevolucion, "Puente de la devolución por orden de pago", ClaseDeOrden),
            // R1 (fase 25): el recaudo pendiente de aplicar, por módulo, con la persona como tercero.
            R(RolesDeRecaudo.RecaudoPendienteDeAplicar, "Recaudo pendiente de aplicar", Modulo),
            // R1 (fase 26): el GMF de los egresos de caja, que asume la cooperativa.
            Resultado(RolesDeRecaudo.GmfGasto, "Gasto por GMF de egresos de caja", Oficina),
            R(RolesDeRecaudo.GmfPorPagar, "GMF por pagar de egresos de caja"),
            // R1 (fase 30): las cuentas por cobrar comerciales que se cobran en el recibo, con tercero y factura.
            R(RolesDeRecaudo.CuentasPorCobrarComerciales, "Cuentas por cobrar comerciales"),
            // R2 (fase 38): la comisión de los convenios (de resultado), su IVA descontable y las retenciones a favor, por cuenta propia y con el aliado como tercero.
            Resultado(RolesDeRecaudoR2.ComisionesDeConvenio, "Comisiones de convenios de recaudo", CuentaPropia),
            R(RolesDeRecaudoR2.IvaDescontable, "IVA descontable de comisiones de convenios", CuentaPropia),
            R(RolesDeRecaudoR2.RetencionesAFavor, "Retenciones a favor practicadas por los convenios", CuentaPropia),
        ],
        [
            H(ReciboEmitidoV1.Type, "Recibo de caja", "RR", [RolesDeRecaudo.Caja, RolesDeRecaudo.PuenteDelModulo],
                RolesDeRecaudo.BancoRecaudo, RolesDeRecaudo.ChequesEnCanje, RolesDeRecaudo.MedioPorCobrar, RolesDeRecaudo.ConceptoVario,
                RolesDeRecaudo.CuentasPorCobrarComerciales),
            H(ReciboDeBancoEmitidoV1.Type, "Recibo de banco", "RB", [RolesDeRecaudo.PuenteDelModulo],
                RolesDeRecaudo.BancoRecaudo, RolesDeRecaudo.PartidasPorIdentificar, RolesDeRecaudo.ConceptoVario),
            H(ReciboDePagaduriaEmitidoV1.Type, "Recibo de pagaduría", "RB", [RolesDeRecaudo.SaldoDePagadurias, RolesDeRecaudo.PuenteDelModulo]),
            H(GiroDePagaduriaRecibidoV1.Type, "Giro de pagaduría recibido", "RB", [RolesDeRecaudo.SaldoDePagadurias], RolesDeRecaudo.BancoRecaudo, RolesDeRecaudo.Caja),
            H(PartidaPorIdentificarReconocidaV1.Type, "Partida por identificar reconocida", "RB", [RolesDeRecaudo.PartidasPorIdentificar], RolesDeRecaudo.BancoRecaudo),
            H(PartidaClasificadaV1.Type, "Partida clasificada", "RB", [RolesDeRecaudo.ConceptoVario], RolesDeRecaudo.PartidasPorIdentificar, RolesDeRecaudo.BancoRecaudo),
            H(PartidaDevueltaAlConsignanteV1.Type, "Partida devuelta al consignante", "RB", [RolesDeRecaudo.PartidasPorIdentificar, RolesDeRecaudo.PuenteDeDevolucion]),
            H(MovimientoDeCajaDeRecaudoRegistradoV1.Type, "Movimiento de caja", "MC", [RolesDeRecaudo.Caja, RolesDeRecaudo.Boveda]),
            H(MedioReclasificadoV1.Type, "Medio reclasificado", "MC", [RolesDeRecaudo.Caja],
                RolesDeRecaudo.BancoRecaudo, RolesDeRecaudo.ChequesEnCanje, RolesDeRecaudo.MedioPorCobrar),
            H(DiferenciaDeArqueoDeRecaudoAprobadaV1.Type, "Diferencia de arqueo aprobada", "MC", [RolesDeRecaudo.Caja],
                RolesDeRecaudo.CuentaPorCobrarCajero, RolesDeRecaudo.GastoFaltante, RolesDeRecaudo.SobrantesDeCaja),
            H(AjusteDeSesionRegistradoV1.Type, "Ajuste de sesión", "MC", [RolesDeRecaudo.Caja],
                RolesDeRecaudo.CuentaPorCobrarCajero, RolesDeRecaudo.GastoFaltante, RolesDeRecaudo.SobrantesDeCaja, RolesDeRecaudo.Boveda),
            H(SobranteLlevadoAIngresoV1.Type, "Sobrante llevado a ingreso", "MC", [RolesDeRecaudo.SobrantesDeCaja, RolesDeRecaudo.IngresoSobrantes]),
            H(DineroRetenidoEnCustodiaV1.Type, "Dinero retenido en custodia", "MC", [RolesDeRecaudo.Caja, RolesDeRecaudo.CustodiaPorLista]),
            H(CustodiaLiberadaV1.Type, "Custodia liberada", "MC", [RolesDeRecaudo.CustodiaPorLista], RolesDeRecaudo.Caja, RolesDeRecaudo.PuenteDeDevolucion),
            H(RemesaDespachadaV1.Type, "Remesa despachada", "MC", [RolesDeRecaudo.EfectivoEnTransito, RolesDeRecaudo.Boveda]),
            H(ReciboAnuladoV1.Type, "Recibo anulado", "AR", [RolesDeRecaudo.PuenteDelModulo],
                RolesDeRecaudo.Caja, RolesDeRecaudo.BancoRecaudo, RolesDeRecaudo.ChequesEnCanje, RolesDeRecaudo.MedioPorCobrar, RolesDeRecaudo.ConceptoVario,
                RolesDeRecaudo.SaldoAFavorDePagadores, RolesDeRecaudo.RecaudoPendienteDeAplicar, RolesDeRecaudo.CuentasPorCobrarComerciales),
            H(AplicacionReversadaPorModuloV1.Type, "Aplicación reversada por el módulo", "AR", [RolesDeRecaudo.PuenteDelModulo],
                RolesDeRecaudo.SaldoAFavorDePagadores, RolesDeRecaudo.Caja, RolesDeRecaudo.BancoRecaudo, RolesDeRecaudo.ChequesEnCanje, RolesDeRecaudo.MedioPorCobrar),
            H(ChequeRecibidoDevueltoV1.Type, "Cheque recibido devuelto", "AR", [RolesDeRecaudo.ChequesEnCanje],
                RolesDeRecaudo.PuenteDelModulo, RolesDeRecaudo.CuentaPorCobrarPagador),
            H(SaldoAFavorDePagadorResueltoV1.Type, "Saldo a favor de pagador resuelto", "AR", [RolesDeRecaudo.PuenteDeDevolucion],
                RolesDeRecaudo.SaldoAFavorDePagadores, RolesDeRecaudo.SaldoDePagadurias, RolesDeRecaudo.PuenteDelModulo),
            H(SaldoAFavorDeCarteraDevueltoV1.Type, "Saldo a favor de Cartera devuelto", "AR", [RolesDeRecaudo.PuenteDelModulo, RolesDeRecaudo.PuenteDeDevolucion]),
            H(DevolucionNoGiradaV1.Type, "Devolución no girada", "AR", [RolesDeRecaudo.PuenteDeDevolucion],
                RolesDeRecaudo.SaldoAFavorDePagadores, RolesDeRecaudo.SaldoDePagadurias, RolesDeRecaudo.PartidasPorIdentificar, RolesDeRecaudo.CustodiaPorLista,
                RolesDeRecaudo.PuenteDelModulo),
            // R1 (fase 25): el recaudo pendiente de aplicar (contabilidad.md §3), con el tipo de comprobante del recibo de caja.
            H(RecaudoPendienteReconocidoV1.Type, "Recaudo pendiente de aplicar reconocido", "RR", [RolesDeRecaudo.RecaudoPendienteDeAplicar],
                RolesDeRecaudo.Caja, RolesDeRecaudo.BancoRecaudo, RolesDeRecaudo.ChequesEnCanje, RolesDeRecaudo.MedioPorCobrar),
            H(RecaudoPendienteAplicadoV1.Type, "Recaudo pendiente aplicado", "RR", [RolesDeRecaudo.RecaudoPendienteDeAplicar, RolesDeRecaudo.PuenteDelModulo]),
            // R1 (fase 26): el egreso de caja, su GMF y su anulación (contabilidad.md §3, §8), con el tipo sugerido EG.
            H(EgresoDeCajaEntregadoV1.Type, "Egreso de caja", "EG", [RolesDeRecaudo.Caja],
                RolesDeRecaudo.PuenteDelModulo, RolesDeRecaudo.SaldoAFavorDePagadores, RolesDeRecaudo.ConceptoVario),
            H(GmfDeEgresoCausadoV1.Type, "GMF de egreso de caja", "EG", [RolesDeRecaudo.GmfGasto, RolesDeRecaudo.GmfPorPagar]),
            H(EgresoDeCajaAnuladoV1.Type, "Egreso de caja anulado", "AR", [RolesDeRecaudo.Caja],
                RolesDeRecaudo.PuenteDelModulo, RolesDeRecaudo.SaldoAFavorDePagadores, RolesDeRecaudo.ConceptoVario, RolesDeRecaudo.GmfGasto, RolesDeRecaudo.GmfPorPagar),
            // R2 (fase 38): la conciliación neta de un convenio (contabilidad.md §3), con el tipo de comprobante del recibo de banco.
            H(ComisionDeConvenioRegistradaV1.Type, "Comisión de convenio registrada", "RB", [RolesDeRecaudo.BancoRecaudo],
                RolesDeRecaudoR2.ComisionesDeConvenio, RolesDeRecaudoR2.IvaDescontable, RolesDeRecaudoR2.RetencionesAFavor),
        ]);

    public static ModuloDeLaMatriz Tesoreria { get; } = new(
        ModuloContable.Tesoreria, "Tesorería", Domain.Treasury.CatalogoDeTesoreria.Modulo, "Accounting.TreasuryRule", "Accounting.TreasuryMatrix",
        [
            Vinculo(RolesDeTesoreria.Banco, "Banco (por el vínculo de la cuenta propia)"),
            R(RolesDeTesoreria.PuenteDeGiroDelOrigen, "Puente de giro del origen", OrigenDeLaOrden, ClaseDeOrden),
            R(RolesDeTesoreria.PuenteDeDevolucion, "Puente de la devolución por orden de pago", ClaseDeOrden),
            Resultado(RolesDeTesoreria.GmfGasto, "Gasto por GMF", ClaseDeOrden, Causa),
            R(RolesDeTesoreria.GmfPorPagar, "GMF por pagar", ClaseDeOrden),
            R(RolesDeTesoreria.EfectivoEnTransito, "Efectivo en tránsito", Oficina, CuentaPropia),
            // T1 (fase 32): los fondos en tránsito de los traslados y el cierre del mes de los cheques girados, por cuenta propia.
            R(RolesDeTesoreriaT1.FondosEnTransito, "Fondos en tránsito entre cuentas propias", CuentaPropia),
            R(RolesDeTesoreriaT1.ChequesPorEntregar, "Cheques girados por entregar", CuentaPropia),
            R(RolesDeTesoreriaT1.CuentaPorPagarChequesNoCobrados, "Cuenta por pagar de cheques no cobrados", CuentaPropia),
            // T2 (fase 41): las notas bancarias, por cuenta propia y con el banco como tercero.
            Resultado(RolesDeTesoreriaT2.ComisionesBancarias, "Comisiones bancarias", CuentaPropia),
            Resultado(RolesDeTesoreriaT2.InteresesBancarios, "Intereses bancarios (en bruto)", CuentaPropia),
            Resultado(RolesDeTesoreriaT2.OtrosCargosBancarios, "Impuestos y otros cargos bancarios", CuentaPropia),
            R(RolesDeTesoreriaT2.IvaDescontable, "IVA descontable de comisiones bancarias", CuentaPropia),
            R(RolesDeTesoreriaT2.RetencionesAFavor, "Retenciones a favor practicadas por bancos y redes", CuentaPropia),
            // T2 (fase 44): la conciliación de adquirentes. MedioPorCobrar es el rol de Recaudo, parametrizado aparte en esta matriz (la contadora le pone la misma
            // cuenta); la comisión de la red es de resultado. Ambos por la cuenta propia, que es la dimensión que lleva el hecho.
            R(RolesDeRecaudo.MedioPorCobrar, "Medio por cobrar (lo liquidado por la red)", CuentaPropia),
            Resultado(RolesDeTesoreriaT2.ComisionesDeRed, "Comisiones de redes de tarjetas", CuentaPropia),
            // T2 (fase 42): las cuentas por pagar que Tesorería paga, con tercero y cruce.
            R(RolesDeTesoreriaT2.CuentasPorPagarProveedores, "Cuentas por pagar a proveedores (facturas de Comercial)"),
            R(RolesDeTesoreriaT2.CuentaPorPagarContable, "Cuentas por pagar contables que paga Tesorería"),
            // T1 (fase 34): la retención en la fuente de una orden manual, por clase de orden (el concepto del catálogo).
            R(RolesDeTesoreriaT1.RetencionEnLaFuentePorPagar, "Retención en la fuente por pagar de órdenes manuales", ClaseDeOrden),
        ],
        [
            H(PagoDeOrdenGiradoV1.Type, "Pago de orden girado", "TP", [RolesDeTesoreria.PuenteDeGiroDelOrigen], RolesDeTesoreria.Banco,
                RolesDeTesoreriaT1.RetencionEnLaFuentePorPagar),
            H(ChequeGiradoEmitidoV1.Type, "Cheque girado emitido", "TP", [RolesDeTesoreria.PuenteDeGiroDelOrigen], RolesDeTesoreria.Banco),
            H(ChequeGiradoAnuladoV1.Type, "Cheque girado anulado", "TP", [RolesDeTesoreria.PuenteDeGiroDelOrigen], RolesDeTesoreria.Banco),
            H(GmfDePagoCausadoV1.Type, "GMF de un pago", "TP", [RolesDeTesoreria.GmfGasto], RolesDeTesoreria.GmfPorPagar, RolesDeTesoreria.Banco),
            H(RemesaConfirmadaV1.Type, "Remesa confirmada", "TP", [RolesDeTesoreria.EfectivoEnTransito], RolesDeTesoreria.Banco),
            // T1 (fase 32; contabilidad.md §4, §8): los traslados y el cierre del mes de los cheques, con el tipo sugerido TB.
            H(TrasladoEntreCuentasEnviadoV1.Type, "Traslado entre cuentas propias enviado", "TB", [RolesDeTesoreriaT1.FondosEnTransito], RolesDeTesoreria.Banco),
            H(TrasladoConfirmadoV1.Type, "Traslado entre cuentas propias confirmado", "TB", [RolesDeTesoreriaT1.FondosEnTransito], RolesDeTesoreria.Banco),
            H(ChequesReclasificadosAlCierreV1.Type, "Cheques por entregar reclasificados al cierre", "TB", [RolesDeTesoreriaT1.ChequesPorEntregar], RolesDeTesoreria.Banco),
            H(ChequeNoCobradoACuentaPorPagarV1.Type, "Cheque no cobrado a cuenta por pagar", "TB", [RolesDeTesoreriaT1.CuentaPorPagarChequesNoCobrados],
                RolesDeTesoreria.Banco),
            // T2 (fase 41; contabilidad.md §4, §8): la nota bancaria, con el tipo sugerido TB.
            H(NotaBancariaRegistradaV1.Type, "Nota bancaria", "TB", [RolesDeTesoreria.Banco],
                RolesDeTesoreria.GmfGasto, RolesDeTesoreriaT2.ComisionesBancarias, RolesDeTesoreriaT2.IvaDescontable, RolesDeTesoreriaT2.InteresesBancarios,
                RolesDeTesoreriaT2.RetencionesAFavor, RolesDeTesoreriaT2.OtrosCargosBancarios),
            // T2 (fase 42; contabilidad.md §4): los pagos de T2 (TP) y el cobro tardío del cheque pasado a cuenta por pagar (TB).
            H(PagoDeProveedorGiradoV1.Type, "Pago a proveedor girado", "TP", [RolesDeTesoreriaT2.CuentasPorPagarProveedores, RolesDeTesoreria.Banco]),
            H(PagoDeCuentaPorPagarGiradoV1.Type, "Pago de cuenta por pagar girado", "TP", [RolesDeTesoreriaT2.CuentaPorPagarContable, RolesDeTesoreria.Banco]),
            H(ChequeNoCobradoPagadoV1.Type, "Cheque no cobrado pagado después", "TB", [RolesDeTesoreriaT1.CuentaPorPagarChequesNoCobrados, RolesDeTesoreria.Banco]),
            // T2 (fase 44; contabilidad.md §4): la conciliación de una red de tarjetas (el medio por cobrar es el mismo rol de Recaudo, en la matriz de Tesorería).
            H(ConciliacionDeAdquirenteRegistradaV1.Type, "Conciliación de adquirente", "TB", [RolesDeTesoreria.Banco, RolesDeRecaudo.MedioPorCobrar],
                RolesDeTesoreriaT2.ComisionesDeRed, RolesDeTesoreriaT2.IvaDescontable, RolesDeTesoreriaT2.RetencionesAFavor),
        ]);

    public static IReadOnlyList<ModuloDeLaMatriz> Todas { get; } = [Recaudo, Tesoreria];

    /// <summary>La matriz del módulo de origen (<c>REC</c> o <c>TES</c>); nula para otro.</summary>
    public static ModuloDeLaMatriz? De(string? modulo) => modulo switch
    {
        ModulosDeOrigen.Recaudo => Recaudo,
        ModulosDeOrigen.Tesoreria => Tesoreria,
        _ => null,
    };
}
