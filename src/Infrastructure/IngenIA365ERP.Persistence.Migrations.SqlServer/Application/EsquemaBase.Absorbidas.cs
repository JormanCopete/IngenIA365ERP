using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.Initialization;

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application;

/// <summary>
/// Las 90 migraciones del árbol operativo que <see cref="EsquemaBase"/> absorbió (consolidación del 2026-11-15).
/// Sus archivos salieron del compilado y siguen en git; esta lista es lo que queda de ellas en el código, y la usa
/// <see cref="ConsolidacionDeMigraciones"/> para reconocer una base vieja (las tiene todas: se marca la base) y para
/// sellar una nueva (se le anotan después de aplicar la base). Se generó con
/// <c>tools/scripts/consolidar-migraciones/generar_absorbidas.py</c>; no se edita a mano.
/// </summary>
public sealed class MigracionesAbsorbidasPorEsquemaBase : MigracionesAbsorbidas
{
    public override Type Contexto => typeof(ApplicationDbContext);

    public override Type MigracionBase => typeof(EsquemaBase);

    public override IReadOnlyList<string> Absorbidas { get; } =
    [
        "20260804192755_InitialSchema",
        "20260820025612_AddUserSettingsYPromoContenidos",
        "20260823000420_SacarTablasAdminDelModeloOperativo",
        "20260825144733_PuenteConIdentidadCentral",
        "20260905194555_NominaNovedadesYLiquidacion",
        "20260905211124_NominaTablasPorRangos",
        "20260905215541_NominaDetalleDeCorrida",
        "20260906151337_RetiroDeVoucherTypeIdSombraEnDocumentos",
        "20260912111151_CajasDeCompensacionYFondoDeCesantiasEnLaFicha",
        "20260912181635_CodigosAlfanumericosEnCatalogos",
        "20260912212906_PeriodicidadesReglasYDescarte",
        "20260913010735_IndicesDeLectura",
        "20260913015103_BaseLegal2026Corregida",
        "20260913151329_HorasMesBase2026",
        "20260913164232_NormaJulio2026ComoBase",
        "20260913222928_ReconciliarBanderasDerivadasDePersona",
        "20260913231143_UnaSolaFichaVivaPorPersona",
        "20260914001117_MotivoDeRetiroComoTexto",
        "20260915151603_ContabilidadNiif",
        "20260918225236_LongitudDeAuxiliarPorRango",
        "20260919033910_NombreDeCuentaHasta200",
        "20260919051352_RetencionPorPlanDeNomina",
        "20260921034851_NominaPrestacionesYDian",
        "20260921173423_SettlementDeductionsUnicosEntreVivas",
        "20260921203039_NominaDispersionBancaria",
        "20260921225628_NominaPilaYNominaElectronica",
        "20260923184507_AdjuntosDirectos",
        "20260925164847_RetiroDelInventarioHeredado",
        "20260925232439_PlataformaParaInventario",
        "20260926095603_InventarioComercialNucleo",
        "20260926160153_IntegracionContableDeInventario",
        "20260927160424_VentasYPuntoDeVenta",
        "20260928015144_DocumentosElectronicos",
        "20260928151213_ComprasYCosteoAvanzado",
        "20260929131016_ComercioAmpliado",
        "20261006075000_PlataformaParaCartera",
        "20261007010209_NominaRecaudaPorElContrato",
        "20261007164913_ComercioConCartera",
        "20261007172025_RetiroDeLaCarteraHeredada",
        "20261015040531_CarteraNucleo",
        "20261015040644_CarteraRiesgo",
        "20261015040735_IntegracionContableDeCartera",
        "20261015140547_CarteraLibranza",
        "20261015195430_CarteraNovedades",
        "20261016010113_CarteraCobranza",
        "20261016183514_CarteraCobranzaSalida",
        "20261017012740_CarteraInformes",
        "20261017031635_CarteraComprobanteDeCargue",
        "20261017042918_CarteraPerdidaEsperada",
        "20261017062205_CarteraVivienda",
        "20261018143627_RetiroDeCaptacionesHeredadas",
        "20261019104056_CaptacionesAportes",
        "20261020130545_ContractualDestinosAlVencer",
        "20261020175119_CaptacionesRespuestasDelDueno",
        "20261020181249_CaptacionesGmfEnEntregas",
        "20261020204112_CaptacionesRegiroDeOrdenes",
        "20261020213550_CaptacionesAsambleaRetirados",
        "20261020220430_CaptacionesRetencionDeAsamblea",
        "20261020223449_CaptacionesBaseDeRetornosPorPlantilla",
        "20261021215931_GruposDelAsociado",
        "20261021220819_RetiroDelCentroDeCostoDelAsociado",
        "20261021232658_CentroDeCostoDelEmpleado",
        "20261023145208_PlataformaParaRecaudoYTesoreria",
        "20261024113421_RecaudoNucleo",
        "20261024113559_TesoreriaNucleo",
        "20261024113733_IntegracionContableDeRecaudoYTesoreria",
        "20261024124117_TesoreriaConciliacionMinima",
        "20261024130329_RecaudoYTesoreriaPuestaEnMarcha",
        "20261024155853_CarteraYCaptacionesConPartidaCitada",
        "20261024185110_RecaudoCompleto",
        "20261024203551_TesoreriaCompleta",
        "20261026164807_RecaudoCanales",
        "20261026185203_TesoreriaAmpliada",
        "20261026192828_RetiroDeLaTesoreriaHeredada",
        "20261026210304_VersionDeClaveDeAnclaMasLarga",
        "20261027024843_RetiroDeLosDoblesDePrueba",
        "20261028003139_RecaudoYTesoreriaSegundaRonda",
        "20261028134527_UsuarioDeLaPersona",
        "20261030172235_ContabilidadSatelites",
        "20261107173127_LineaDeServicio",
        "20261108190547_CanalDigital",
        "20261108202413_PuntoDeVentaCentroDeCosto",
        "20261109122253_AgilidadDeTecladoYMediosUnificados",
        "20261109194646_Transporte",
        "20261112013915_EmisionAsincrona",
        "20261112165231_VentaACreditoConMora",
        "20261112193403_CuentaContablePorEntidadDeNomina",
        "20261113005831_CodificacionDeProductos",
        "20261114042207_SolicitudDesdeElCanal",
        "20261114154055_EquivalenciasDelProveedor",
    ];
}
