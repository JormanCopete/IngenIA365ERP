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
        ],
        [
            H(ReciboEmitidoV1.Type, "Recibo de caja", "RR", [RolesDeRecaudo.Caja, RolesDeRecaudo.PuenteDelModulo],
                RolesDeRecaudo.BancoRecaudo, RolesDeRecaudo.ChequesEnCanje, RolesDeRecaudo.MedioPorCobrar, RolesDeRecaudo.ConceptoVario),
            H(ReciboDeBancoEmitidoV1.Type, "Recibo de banco", "RB", [RolesDeRecaudo.PuenteDelModulo],
                RolesDeRecaudo.BancoRecaudo, RolesDeRecaudo.PartidasPorIdentificar, RolesDeRecaudo.ConceptoVario),
            H(ReciboDePagaduriaEmitidoV1.Type, "Recibo de pagaduría", "RB", [RolesDeRecaudo.SaldoDePagadurias, RolesDeRecaudo.PuenteDelModulo]),
            H(GiroDePagaduriaRecibidoV1.Type, "Giro de pagaduría recibido", "RB", [RolesDeRecaudo.SaldoDePagadurias], RolesDeRecaudo.BancoRecaudo, RolesDeRecaudo.Caja),
            H(PartidaPorIdentificarReconocidaV1.Type, "Partida por identificar reconocida", "RB", [RolesDeRecaudo.PartidasPorIdentificar], RolesDeRecaudo.BancoRecaudo),
            H(PartidaClasificadaV1.Type, "Partida clasificada", "RB", [RolesDeRecaudo.ConceptoVario], RolesDeRecaudo.PartidasPorIdentificar, RolesDeRecaudo.BancoRecaudo),
            H(PartidaDevueltaAlConsignanteV1.Type, "Partida devuelta al consignante", "RB", [RolesDeRecaudo.PartidasPorIdentificar, RolesDeRecaudo.PuenteDeDevolucion]),
            H(MovimientoDeCajaRegistradoV1.Type, "Movimiento de caja", "MC", [RolesDeRecaudo.Caja, RolesDeRecaudo.Boveda]),
            H(MedioReclasificadoV1.Type, "Medio reclasificado", "MC", [RolesDeRecaudo.Caja],
                RolesDeRecaudo.BancoRecaudo, RolesDeRecaudo.ChequesEnCanje, RolesDeRecaudo.MedioPorCobrar),
            H(DiferenciaDeArqueoAprobadaV1.Type, "Diferencia de arqueo aprobada", "MC", [RolesDeRecaudo.Caja],
                RolesDeRecaudo.CuentaPorCobrarCajero, RolesDeRecaudo.GastoFaltante, RolesDeRecaudo.SobrantesDeCaja),
            H(AjusteDeSesionRegistradoV1.Type, "Ajuste de sesión", "MC", [RolesDeRecaudo.Caja],
                RolesDeRecaudo.CuentaPorCobrarCajero, RolesDeRecaudo.GastoFaltante, RolesDeRecaudo.SobrantesDeCaja, RolesDeRecaudo.Boveda),
            H(SobranteLlevadoAIngresoV1.Type, "Sobrante llevado a ingreso", "MC", [RolesDeRecaudo.SobrantesDeCaja, RolesDeRecaudo.IngresoSobrantes]),
            H(DineroRetenidoEnCustodiaV1.Type, "Dinero retenido en custodia", "MC", [RolesDeRecaudo.Caja, RolesDeRecaudo.CustodiaPorLista]),
            H(CustodiaLiberadaV1.Type, "Custodia liberada", "MC", [RolesDeRecaudo.CustodiaPorLista], RolesDeRecaudo.Caja, RolesDeRecaudo.PuenteDeDevolucion),
            H(RemesaDespachadaV1.Type, "Remesa despachada", "MC", [RolesDeRecaudo.EfectivoEnTransito, RolesDeRecaudo.Boveda]),
            H(ReciboAnuladoV1.Type, "Recibo anulado", "AR", [RolesDeRecaudo.PuenteDelModulo],
                RolesDeRecaudo.Caja, RolesDeRecaudo.BancoRecaudo, RolesDeRecaudo.ChequesEnCanje, RolesDeRecaudo.MedioPorCobrar, RolesDeRecaudo.ConceptoVario,
                RolesDeRecaudo.SaldoAFavorDePagadores, RolesDeRecaudo.RecaudoPendienteDeAplicar),
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
        ],
        [
            H(PagoDeOrdenGiradoV1.Type, "Pago de orden girado", "TP", [RolesDeTesoreria.PuenteDeGiroDelOrigen], RolesDeTesoreria.Banco),
            H(ChequeGiradoEmitidoV1.Type, "Cheque girado emitido", "TP", [RolesDeTesoreria.PuenteDeGiroDelOrigen], RolesDeTesoreria.Banco),
            H(ChequeGiradoAnuladoV1.Type, "Cheque girado anulado", "TP", [RolesDeTesoreria.PuenteDeGiroDelOrigen], RolesDeTesoreria.Banco),
            H(GmfDePagoCausadoV1.Type, "GMF de un pago", "TP", [RolesDeTesoreria.GmfGasto], RolesDeTesoreria.GmfPorPagar, RolesDeTesoreria.Banco),
            H(RemesaConfirmadaV1.Type, "Remesa confirmada", "TP", [RolesDeTesoreria.EfectivoEnTransito], RolesDeTesoreria.Banco),
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
