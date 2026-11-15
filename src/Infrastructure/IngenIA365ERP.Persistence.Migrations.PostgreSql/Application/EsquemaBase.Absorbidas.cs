using IngenIA365ERP.Persistence.DbContext;
using IngenIA365ERP.Persistence.Initialization;

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application;

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
        "20260804192805_InitialSchema",
        "20260820025624_AddUserSettingsYPromoContenidos",
        "20260823000431_SacarTablasAdminDelModeloOperativo",
        "20260825144746_PuenteConIdentidadCentral",
        "20260905194621_NominaNovedadesYLiquidacion",
        "20260905211151_NominaTablasPorRangos",
        "20260905215607_NominaDetalleDeCorrida",
        "20260906151349_RetiroDeVoucherTypeIdSombraEnDocumentos",
        "20260912111205_CajasDeCompensacionYFondoDeCesantiasEnLaFicha",
        "20260912181648_CodigosAlfanumericosEnCatalogos",
        "20260912212920_PeriodicidadesReglasYDescarte",
        "20260913010749_IndicesDeLectura",
        "20260913015117_BaseLegal2026Corregida",
        "20260913151343_HorasMesBase2026",
        "20260913164246_NormaJulio2026ComoBase",
        "20260913222940_ReconciliarBanderasDerivadasDePersona",
        "20260913231156_UnaSolaFichaVivaPorPersona",
        "20260914001132_MotivoDeRetiroComoTexto",
        "20260915151632_ContabilidadNiif",
        "20260918225306_LongitudDeAuxiliarPorRango",
        "20260919033940_NombreDeCuentaHasta200",
        "20260919051423_RetencionPorPlanDeNomina",
        "20260921034933_NominaPrestacionesYDian",
        "20260921173504_SettlementDeductionsUnicosEntreVivas",
        "20260921203056_NominaDispersionBancaria",
        "20260921225646_NominaPilaYNominaElectronica",
        "20260923184607_AdjuntosDirectos",
        "20260925164923_RetiroDelInventarioHeredado",
        "20260925232507_PlataformaParaInventario",
        "20260926095622_InventarioComercialNucleo",
        "20260926160219_IntegracionContableDeInventario",
        "20260927160453_VentasYPuntoDeVenta",
        "20260928015129_DocumentosElectronicos",
        "20260928151250_ComprasYCosteoAvanzado",
        "20260929131054_ComercioAmpliado",
        "20261006075037_PlataformaParaCartera",
        "20261007010256_NominaRecaudaPorElContrato",
        "20261007164951_ComercioConCartera",
        "20261007172102_RetiroDeLaCarteraHeredada",
        "20261015040557_CarteraNucleo",
        "20261015040710_CarteraRiesgo",
        "20261015040801_IntegracionContableDeCartera",
        "20261015140615_CarteraLibranza",
        "20261015195513_CarteraNovedades",
        "20261016010144_CarteraCobranza",
        "20261016183603_CarteraCobranzaSalida",
        "20261017012810_CarteraInformes",
        "20261017031706_CarteraComprobanteDeCargue",
        "20261017042947_CarteraPerdidaEsperada",
        "20261017062235_CarteraVivienda",
        "20261018143710_RetiroDeCaptacionesHeredadas",
        "20261019104128_CaptacionesAportes",
        "20261020130623_ContractualDestinosAlVencer",
        "20261020175152_CaptacionesRespuestasDelDueno",
        "20261020181323_CaptacionesGmfEnEntregas",
        "20261020204151_CaptacionesRegiroDeOrdenes",
        "20261020213628_CaptacionesAsambleaRetirados",
        "20261020220510_CaptacionesRetencionDeAsamblea",
        "20261020223529_CaptacionesBaseDeRetornosPorPlantilla",
        "20261021220009_GruposDelAsociado",
        "20261021220901_RetiroDelCentroDeCostoDelAsociado",
        "20261021232738_CentroDeCostoDelEmpleado",
        "20261023145248_PlataformaParaRecaudoYTesoreria",
        "20261024113511_RecaudoNucleo",
        "20261024113647_TesoreriaNucleo",
        "20261024113827_IntegracionContableDeRecaudoYTesoreria",
        "20261024124206_TesoreriaConciliacionMinima",
        "20261024130418_RecaudoYTesoreriaPuestaEnMarcha",
        "20261024155953_CarteraYCaptacionesConPartidaCitada",
        "20261024185201_RecaudoCompleto",
        "20261024203607_TesoreriaCompleta",
        "20261026164905_RecaudoCanales",
        "20261026185302_TesoreriaAmpliada",
        "20261026192926_RetiroDeLaTesoreriaHeredada",
        "20261026210357_VersionDeClaveDeAnclaMasLarga",
        "20261027024928_RetiroDeLosDoblesDePrueba",
        "20261028003225_RecaudoYTesoreriaSegundaRonda",
        "20261028134606_UsuarioDeLaPersona",
        "20261030172409_ContabilidadSatelites",
        "20261107173231_LineaDeServicio",
        "20261108190612_CanalDigital",
        "20261108202613_PuntoDeVentaCentroDeCosto",
        "20261109122309_AgilidadDeTecladoYMediosUnificados",
        "20261109194738_Transporte",
        "20261112014020_EmisionAsincrona",
        "20261112165332_VentaACreditoConMora",
        "20261112193509_CuentaContablePorEntidadDeNomina",
        "20261113005937_CodificacionDeProductos",
        "20261114042152_SolicitudDesdeElCanal",
        "20261114154126_EquivalenciasDelProveedor",
    ];
}
