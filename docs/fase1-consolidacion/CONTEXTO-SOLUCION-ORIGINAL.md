# CONTEXTO-SOLUCION-ORIGINAL.md
# Arquitectura del Sistema ERP SOLIDO y Proceso de Migracion

> Documento de referencia completo para la migracion a web (Blazor + SyncFusion).
> Generado: 2026-03-15

---

## 1. VISION GENERAL DEL SISTEMA

| Atributo | Valor |
|----------|-------|
| **Nombre** | SOLIDO - ERP Financiero para el Sector Solidario |
| **Empresa** | Informatica Creativa Ltda |
| **Tecnologia original** | VB.NET, .NET Framework 2.0/3.5, Windows Forms |
| **Solucion VB.NET** | `D:\OneDrive - INGENIA 365\PSNL\AplicacionesDesktop\SOLIDO\solido.sln` |
| **Proyecto formularios** | `SOLIDO` (WinForms exe) — 272 formularios, ~128K lineas |
| **Plugins originales** | `D:\...\SOLIDO\Plugins\` — 33 proyectos de clases VB.NET |
| **Proyecto consolidado** | `ERP.Core` (.NET 10, C# 13) — 231 archivos, ~149K lineas |
| **Base de datos** | Multi-motor: MySQL, SQL Server, Oracle, PostgreSQL, DB2 via ODBC |
| **Reportes** | Crystal Reports 10.5 (SAP) |
| **Tablas** | 292 tablas + 60 vistas |
| **Version ejecutable** | 2.1.0.0 |

### Proceso de migracion realizado

```
FASE 1: VB.NET → C# (33 proyectos individuales)
  Herramienta: Claude Code (IA)
  Resultado: 33 proyectos .CSharp en Plugins/

FASE 2: 33 proyectos C# → 1 proyecto ERP.Core
  Herramienta: Claude Code (IA)
  Resultado: ERP.Core con 15 modulos, 231 archivos .cs
  Resuelto: dependencias circulares, duplicados, namespaces

FASE 3 (PENDIENTE): Formularios SOLIDO → Blazor Web + SyncFusion
  Fuente: 272 formularios WinForms del proyecto SOLIDO
  Backend: ERP.Core como capa de logica de negocio
```

---

## 2. ARQUITECTURA ORIGINAL (VB.NET)

### Diagrama de la solucion solido.sln

```
solido.sln
├── SOLIDO (WinForms .exe)
│   ├── Aplicacion/          → Inicio, principal, ayuda
│   ├── Modulos/             → Logica compartida (7K lineas)
│   ├── Formularios/         → 272 WinForms organizados por modulo
│   │   ├── cop/             → 103 forms (Cartera)
│   │   ├── Nom/             → 51 forms (Nomina)
│   │   ├── Inv/             → 38 forms (Inventario)
│   │   ├── Cnt/             → 27 forms (Contabilidad)
│   │   ├── Productivo/      → 17 forms (Grupos productivos)
│   │   ├── Sys/             → 12 forms (Sistema)
│   │   ├── Rec/             → 6 forms (Recaudos)
│   │   ├── Clientes/        → 5 forms (Clientes)
│   │   ├── Deposito/        → 5 forms (CDTs)
│   │   ├── Tes/             → 2 forms (Tesoreria)
│   │   ├── Tarjeta Debito/  → 2 forms (Debito)
│   │   ├── Ser/             → 2 forms (Servicios)
│   │   ├── Cdts/            → 1 form (CDTs)
│   │   └── tarcre/          → 1 form (Tarjeta credito)
│   └── Resources/           → Imagenes, iconos
│
├── ConectBd (Plugin)         → Conexion ODBC
├── MsgConfig (Plugin)        → Parametros del sistema
├── MsgSas (Plugin)           → Utilidades UI
├── SasToolBar (Plugin)       → Toolbar personalizado
├── msgcop (Plugin)           → Cartera financiera (40K lineas)
├── msgliqcre (Plugin)        → Liquidacion creditos (28K lineas)
├── msgcnt (Plugin)           → Contabilidad (7K lineas)
├── msgnom (Plugin)           → Nomina (14K lineas)
├── msginv (Plugin)           → Inventario (12K lineas)
├── ... (28 plugins mas)
└── Nitgen (Plugin)           → Biometria
```

### Como SOLIDO referencia los plugins

El proyecto SOLIDO tiene un modulo global (`Modulos/solido.vb`) que instancia TODOS los plugins:
```vb
Public myconnect As New OdbcConnection
Public msgcop As New msgcop.Clscartera
Public msgcnt As New msgcnt.ClsContabilidad
Public msgliqcre As New msgliqcre.ClsLiqcreditos
Public msginv As New msginv.msginv
' ... 30+ instancias
```

Los formularios acceden a los plugins via estas variables globales:
```vb
' En cualquier formulario:
solido.msgcop.BuscaAsociado(codigoter, myconnect, ...)
solido.msgcnt.GrabaMovimiento(comprobante, ...)
```

### Patrones de acceso a datos

| Patron | Uso |
|--------|-----|
| **ODBC directo** | Todas las consultas via `ClsConect.ExecuteQueryconec()` |
| **DataSet/DataTable** | Resultado de queries, binding a DataGridView |
| **String SQL dinamico** | SQL construido con concatenacion de strings |
| **Stored procedures** | Para operaciones criticas (cierre, traslados) |
| **Crystal Reports** | Reportes .rpt desde ruta de red compartida |

---

## 3. ARQUITECTURA MIGRADA (C# - ERP.Core)

### Estructura actual

```
ERP.Core/ (.NET 10, C# 13, WinForms)
├── Compartido/               → Infraestructura transversal (42 archivos, 11K lineas)
│   ├── Datos/                → ClsConect (conexion ODBC)
│   ├── Controles/            → SasToolBar, Texbox*, CalendarGrid
│   ├── Utilidades/           → Ayuda, Numeros_A_Letras, GenerarCodBarras
│   ├── Configuracion/        → ParamSys, VarIni, forms de config
│   ├── Reportes/             → reporte (stub), config_report
│   ├── Forms/                → FormAyuda, FrmFormPago, imprimir
│   └── Interfaces/           → IReportService, IExcelExportService
├── CarteraFinanciera/        → Modulo mas grande (110 archivos, 85K lineas)
│   ├── Services/Cartera/     → Clscartera (13 partials)
│   ├── Services/Creditos/    → ClsLiqcreditos (4 partials)
│   ├── Services/Depositos/   → ClsDepositos
│   ├── Services/Debitos/     → ClsMsgDeb
│   ├── Services/TarjetaCred/ → Clstarjcredito
│   ├── Models/               → ParamCop
│   ├── Reportes/             → ImpreDoc, ClsImpCert, clscircular
│   └── Forms/                → 25+ formularios
├── Contabilidad/             → 15 archivos, 9K lineas
├── Inventario/               → 20 archivos, 16K lineas
├── Nomina/                   → 22 archivos, 18K lineas
├── CDT/                      → 3 archivos
├── Tesoreria/                → 5 archivos
├── Creditos/Reportes/        → clsimpsol
├── Seguridad/                → Biometria + forms login
├── Recaudos/                 → msgrecaudos
├── Reportes/                 → mscext01, mscext02
└── Produccion/               → ProcesoProd
```

### Decisiones arquitectonicas

| Decision | Justificacion |
|----------|---------------|
| **Cluster circular → CarteraFinanciera** | 7 proyectos con dependencias bidireccionales consolidados en un modulo |
| **Clases compartidas → Compartido/** | ConectBd, MsgSas, SasToolBar usados por 15+ modulos |
| **Crystal Reports → #if CRYSTAL_LEGACY** | DLLs Crystal 10.5 incompatibles con .NET 10 |
| **Excel COM → #if EXCEL_LEGACY** | COM Interop no disponible en .NET 10 |
| **Nitgen SDK → #if NITGEN_SDK** | DLL biometrica sin version .NET 10 |
| **IReportService** | Interfaz para reemplazo futuro de Crystal |
| **IExcelExportService** | Interfaz para reemplazo futuro de Excel COM |
| **Stub de reporte** | Clase stub fuera del #if permite compilacion sin Crystal |

---

## 4. MODULOS FUNCIONALES DEL ERP

### Modulo: CarteraFinanciera (cop)

- **Proposito**: Gestion de prestamos, obligaciones, cobro de cartera, reestructuracion de deudas, circulares, SIPLA/antilavado
- **Plugins originales**: msgcop, msgliqcre, msgdep, MsgDeb, msgcre, MsgImpCop, msccircular, MscImpCert, MscAplNom
- **Formularios en SOLIDO**: 103 forms en `Formularios/cop/`
- **Clases en ERP.Core**: Clscartera (13 partials), ClsLiqcreditos (4 partials), ClsDepositos, ClsMsgDeb, Clstarjcredito, clscircular, ImpreDoc, ParamCop
- **Tablas principales**: cop_maecar, cop_movimto, cop_cuopen, cop_cuoant, cop_solcre, cop_extras, cop_copmora, cop_garantia
- **Reportes Crystal**: cop_repor_circularcobro, cop_rcodeudas, cop_rpagototaldeuda, cop_rordencomercio, cop_recibocaja
- **Flujos principales**:
  1. Solicitud de credito → evaluacion capacidad de pago → aprobacion → desembolso → tabla de amortizacion
  2. Recaudo de cuotas → aplicacion a capital/interes/mora → actualizacion saldos → contabilizacion
  3. Reestructuracion de deuda → nueva tabla → ajuste contable

### Modulo: Contabilidad (cnt)

- **Proposito**: Asientos contables, mayor, balance, conciliacion bancaria, cierre de periodo, medios magneticos
- **Plugins originales**: msgcnt, MsgImpCnt
- **Formularios en SOLIDO**: 27 forms en `Formularios/Cnt/`
- **Clases en ERP.Core**: ClsContabilidad, ClsContabilidad.Promedios, ImpreDoc (cnt), ParamCnt
- **Tablas principales**: cnt_maecuen, cnt_movimto, cnt_docmto, cnt_docaux, cnt_nit, cnt_tercero, cnt_concibanca
- **Reportes Crystal**: Comprobantes, auxiliares de cuenta, balance de prueba, certificados retencion
- **Flujos principales**:
  1. Registro de comprobante → partida doble → cuadre documento → cierre
  2. Conciliacion bancaria → cruce de movimientos → ajustes
  3. Generacion medios magneticos DIAN

### Modulo: Inventario (inv)

- **Proposito**: Productos, bodegas, facturacion POS, cierre de caja, tickets termicos
- **Plugins originales**: msginv, msginvconfig, msgtiket, msgcli
- **Formularios en SOLIDO**: 38 forms en `Formularios/Inv/`
- **Clases en ERP.Core**: msginv (5 partials), ClsInvConfig, clsmsgtiket, Clsmsgcli
- **Tablas principales**: inv_productos, inv_movtos, inv_ctrlinv, inv_bodegas, inv_precios, inv_facturas
- **Flujos principales**:
  1. Facturacion → calculo IVA/descuentos → impresion ticket → cierre caja
  2. Ingreso mercancia → actualizacion costo promedio → kardex
  3. Inventario fisico → ajuste → contabilizacion

### Modulo: Nomina (nom)

- **Proposito**: Liquidacion nomina, prestaciones sociales, cesantias, vacaciones, planilla unica, contabilizacion
- **Plugins originales**: msgnom, msgnomconfig, msgimp, mscpagonomi, arnomgen
- **Formularios en SOLIDO**: 51 forms en `Formularios/Nom/`
- **Clases en ERP.Core**: msgnom (8 partials), msgnomconfig, clsmsgimp, Clspagonomi, inicio
- **Tablas principales**: nom_empleados, nom_liqplan, nom_cptos, nom_maeliqemp, nom_cesantias, nom_ausentismos
- **Flujos principales**:
  1. Liquidacion quincenal/mensual → deducciones → neto a pagar → plano bancario
  2. Liquidacion prestaciones → cesantias + intereses + prima + vacaciones
  3. Planilla unica → aportes salud, pension, ARP, caja

### Modulo: CDT (Certificados de Deposito a Termino)

- **Proposito**: Apertura, renovacion, liquidacion de CDTs
- **Plugins originales**: MsgCdats, mscimpcdat
- **Formularios en SOLIDO**: 1 form en `Formularios/Cdts/` + 5 en `Formularios/Deposito/`
- **Clases en ERP.Core**: ClsMsgCdats, clsimpcdats, ParamCdt
- **Tablas principales**: cdt_maecdats, cdt_novcdats, cdt_parame58, cdt_tasasplazos

### Modulo: Tesoreria (tes)

- **Proposito**: Pagos a proveedores, gestion de liquidez, formas de pago
- **Plugins originales**: msgtes
- **Formularios en SOLIDO**: 2 forms en `Formularios/Tes/`
- **Clases en ERP.Core**: clstesoreria, VarIni (tes), ParamTes, FrmPagoFact
- **Tablas principales**: tes_factura, sys_forpago

### Modulo: Seguridad

- **Proposito**: Login, autenticacion biometrica, firmas digitales, bloqueo de empresas
- **Plugins originales**: Nitgen, MsgConfig (parcial)
- **Formularios en SOLIDO**: 12 forms en `Formularios/Sys/`
- **Clases en ERP.Core**: ClsNitgen, NetBioApi, FrmLogeo, sys_ffirmas, frmempbloq

### Modulo: Recaudos

- **Proposito**: Conceptos de recaudo, facturacion de servicios
- **Plugins originales**: MsgRecaudos
- **Formularios en SOLIDO**: 6 forms en `Formularios/Rec/`
- **Clases en ERP.Core**: msgrecaudos

### Modulo: Produccion

- **Proposito**: Grupos productivos, asociados por empresa, seguimiento
- **Plugins originales**: mscProcesoProd
- **Formularios en SOLIDO**: 17 forms en `Formularios/Productivo/`
- **Clases en ERP.Core**: ProcesoProd
- **Tablas principales**: gp_EmpresaProd, gp_SocioDistibuEmp, gp_Capacitaciones

---

## 5. MODELO DE DATOS

### Resumen por modulo

| Prefijo | Modulo | Tablas | Vistas | Tablas clave |
|---------|--------|--------|--------|-------------|
| sys_ | Sistema | 26 | 0 | sys_compania, sys_maenit, sys_sasusu |
| cop_ | Cartera | 85+ | 25+ | cop_maecar, cop_movimto, cop_cuopen |
| cnt_ | Contabilidad | 50+ | 15+ | cnt_maecuen, cnt_movimto, cnt_docmto |
| nom_ | Nomina | 45+ | 10+ | nom_empleados, nom_liqplan, nom_cptos |
| inv_ | Inventario | 35+ | 10+ | inv_productos, inv_movtos, inv_ctrlinv |
| dep_ | Depositos | 11 | 0 | dep_maeahor, dep_ahor29, dep_firmas |
| cdt_ | CDT | 5 | 1 | cdt_maecdats, cdt_novcdats, cdt_parame58 |
| gp_ | Produccion | 17 | 0 | gp_EmpresaProd, gp_SocioDistibuEmp |
| rec_ | Recaudos | 4 | 0 | rec_comceptos, rec_pagocomceptos |
| cli_ | Clientes | 4 | 0 | cli_maecli, cli_docfact |
| deb_ | Debito | 4 | 0 | deb_maetarj, deb_movto |
| cre_ | Creditos | 1 | 0 | cre_parame01 |
| tes_ | Tesoreria | 1 | 0 | tes_factura |
| **TOTAL** | | **292** | **60+** | |

### Motor de base de datos

El sistema soporta 5 motores via ODBC:

| Motor | Connection String Pattern |
|-------|--------------------------|
| MySQL | `Driver={driver};UID={user};DATABASE={db};PASSWORD={pass};PORT={port};SERVER={server}` |
| SQL Server | `Driver={driver};Server={server};Database={db};Uid={user};Pwd={pass}` |
| Oracle | `DRIVER={driver};SERVER={server};uid={user};Pwd={pass};dbq={db}` |
| PostgreSQL | `Driver={driver};Server={server};Database={db};Uid={user};Pwd={pass}` |
| DB2 | `DSN={dsn};Pwd={pass}` |

---

## 6. REGLAS DE NEGOCIO CRITICAS

### Contabilidad
- **Partida doble**: Todo movimiento debe tener debito = credito
- **Periodos**: No se puede grabar en periodo cerrado
- **Numeracion**: Comprobantes con consecutivo automatico por tipo
- **3x1000**: Calculo automatico del gravamen al movimiento financiero
- **Cierre anual**: Traslado de saldos de cuentas, terceros y documentos

### Cartera y creditos
- **Causacion de intereses**: Por periodicidad (mensual, quincenal, semanal)
- **Mora**: Calculo automatico con dias de gracia configurables
- **Capacidad de pago**: Evaluacion antes de aprobar credito
- **SIPLA**: Control antilavado con limites diarios y mensuales
- **Reestructuracion**: Genera nueva tabla de amortizacion
- **Codeudores**: Maximo configurable por obligacion

### Nomina
- **Retencion en la fuente**: Tabla progresiva UVT
- **Prestaciones sociales**: Cesantias, intereses, prima, vacaciones
- **Planilla unica**: Aportes a salud, pension, ARP, caja compensacion
- **Planos bancarios**: Generacion de archivos planos para pago por banco

### Inventario
- **Costo promedio ponderado**: Recalculo automatico en cada compra
- **IVA**: Multiples tasas por producto
- **Cierre de caja**: Cuadre de efectivo, cheques, tarjetas, titulos
- **Facturacion electronica**: Resolucion DIAN con prefijo y numeracion

---

## 7. MAPA DE FORMULARIOS DEL PROYECTO SOLIDO

### Distribucion por modulo

| Modulo | Forms | % | Complejidad promedio |
|--------|-------|---|---------------------|
| Cartera (cop) | 103 | 38% | Alta |
| Nomina (Nom) | 51 | 19% | Media-Alta |
| Inventario (Inv) | 38 | 14% | Media |
| Contabilidad (Cnt) | 27 | 10% | Alta |
| Productivo | 17 | 6% | Media |
| Sistema (Sys) | 12 | 4% | Baja-Media |
| Recaudos (Rec) | 6 | 2% | Baja |
| Deposito/CDT | 6 | 2% | Media |
| Clientes | 5 | 2% | Media |
| Otros (Tes, Deb, Ser) | 7 | 3% | Baja |
| **TOTAL** | **272** | 100% | |

### Formulario principal

`principal.vb` — 89KB, ~45K lineas. Contiene:
- Menu principal con navegacion a todos los modulos
- Autenticacion de usuario
- Inicializacion de plugins
- Gestion de ventanas MDI

---

## 8. MAPEO PARA MIGRACION A WEB (Blazor + SyncFusion)

### Mapeo de controles WinForms → SyncFusion Blazor

| WinForms | SyncFusion Blazor | Notas |
|----------|-------------------|-------|
| DataGridView | SfGrid | Paginacion server-side recomendada |
| TextBox | SfTextBox | Agregar validacion Blazor |
| ComboBox | SfDropDownList | Carga lazy para listas grandes |
| DateTimePicker | SfDatePicker | Formato configurable |
| NumericUpDown | SfNumericTextBox | |
| CheckBox | SfCheckBox | |
| RadioButton | SfRadioButton | |
| TabControl | SfTab | |
| TreeView | SfTreeView | |
| ProgressBar | SfProgressBar | |
| MenuStrip | SfMenu | |
| ToolStrip | SfToolbar | |
| MaskedTextBox | SfMaskedTextBox | |
| RichTextBox | SfRichTextEditor | |
| PrintDocument | SfPdfViewer | Reemplazar impresion directa |
| CrystalReportViewer | SfPdfViewer + RDLC | Motor de reportes nuevo |
| SasToolBar (custom) | SfToolbar | Recrear con botones estandar |
| TexboxSoloNumeros | SfNumericTextBox | |
| TexboxDecimal | SfNumericTextBox (Format="N2") | |
| CalendarGrid | SfDatePicker en SfGrid | |

### Mapeo de formularios → componentes Blazor (top 20)

| Formulario SOLIDO | Componente Blazor | Controles SyncFusion | API Endpoint |
|---|---|---|---|
| principal.vb | MainLayout.razor | SfMenu, SfSidebar | - |
| cop_fmaecli01.vb | Pages/Cartera/Asociado.razor | SfGrid, SfDialog, SfTab | GET/POST /api/cartera/asociados |
| cop_fgenmovto01.vb | Pages/Cartera/Movimientos.razor | SfGrid, SfNumericTextBox | POST /api/cartera/movimientos |
| cop_fsolcre01.vb | Pages/Cartera/SolicitudCredito.razor | SfTab, SfGrid, SfDialog | POST /api/cartera/solicitudes |
| cnt_fmovcont01.vb | Pages/Contabilidad/Comprobante.razor | SfGrid, SfDropDownList | POST /api/contabilidad/comprobantes |
| cnt_fbalance01.vb | Pages/Contabilidad/Balance.razor | SfGrid, SfChart | GET /api/contabilidad/balance |
| inv_frmfactura01.vb | Pages/Inventario/Factura.razor | SfGrid, SfNumericTextBox | POST /api/inventario/facturas |
| inv_frmproducto01.vb | Pages/Inventario/Producto.razor | SfGrid, SfDialog | GET/POST /api/inventario/productos |
| nom_frmliquida01.vb | Pages/Nomina/Liquidacion.razor | SfGrid, SfProgressBar | POST /api/nomina/liquidacion |
| nom_frmempleado01.vb | Pages/Nomina/Empleado.razor | SfTab, SfGrid | GET/POST /api/nomina/empleados |

---

## 9. ORDEN SUGERIDO DE MIGRACION A WEB

### Fase 1 — Quick Wins (4-6 semanas)
Formularios maestros simples del proyecto SOLIDO.
Objetivo: tener la aplicacion web funcional con CRUD basico.

| Prioridad | Formularios SOLIDO | Modulo | Estimacion |
|-----------|-------------------|--------|------------|
| 1 | Sys: Login, Menu principal | Seguridad | 1 semana |
| 2 | Sys: Usuarios, Parametros | Seguridad | 1 semana |
| 3 | Cnt: Plan de cuentas, Terceros | Contabilidad | 1 semana |
| 4 | Cop: Asociados (maestro) | Cartera | 1 semana |
| 5 | Inv: Productos, Bodegas | Inventario | 1 semana |
| 6 | Nom: Empleados (maestro) | Nomina | 1 semana |

### Fase 2 — Core (8-12 semanas)
Formularios de transacciones principales.

| Prioridad | Formularios SOLIDO | Modulo |
|-----------|-------------------|--------|
| 7 | Cnt: Comprobantes contables | Contabilidad |
| 8 | Cop: Solicitud de credito | Cartera |
| 9 | Cop: Movimientos de cartera | Cartera |
| 10 | Cop: Pagos de deuda | Cartera |
| 11 | Inv: Facturacion | Inventario |
| 12 | Nom: Liquidacion nomina | Nomina |
| 13 | Tes: Pago de facturas | Tesoreria |
| 14 | Dep: Cuentas de ahorro | Ahorros |

### Fase 3 — Procesos (6-8 semanas)
Procesos batch y logica compleja.

| Prioridad | Formularios SOLIDO | Modulo |
|-----------|-------------------|--------|
| 15 | Cop: Causacion de intereses | Cartera |
| 16 | Cnt: Cierre de periodo | Contabilidad |
| 17 | Nom: Prestaciones sociales | Nomina |
| 18 | Nom: Planilla unica | Nomina |
| 19 | Cop: Reestructuracion | Cartera |
| 20 | Inv: Cierre de caja | Inventario |

### Fase 4 — Reportes (4-6 semanas)
Migrar de Crystal Reports a nuevo motor.

| Prioridad | Reportes | Motor sugerido |
|-----------|----------|----------------|
| 21 | Extractos de cuenta | RDLC / FastReport |
| 22 | Comprobantes contables | RDLC |
| 23 | Certificados de retencion | RDLC |
| 24 | Tickets POS | PrintDocument → PDF |
| 25 | Circulares de cobro | RDLC |

---

## 10. NOTAS TECNICAS PARA LA MIGRACION WEB

### Patrones de SOLIDO que necesitan atencion en Blazor

| Patron WinForms | Problema en Web | Solucion Blazor |
|-----------------|-----------------|-----------------|
| Variables globales (`solido.msgcop`) | No existe estado global en web | Inyeccion de dependencias (DI) |
| MDI Forms (ventanas hijas) | No aplica en web | Tabs o rutas SPA |
| ShowDialog() modal | Bloqueante, no existe en web | SfDialog con async/await |
| DataGridView editable | Estado local complejo | SfGrid con EditMode.Dialog |
| Crystal ReportViewer | No compatible | SfPdfViewer + exportar a PDF |
| COM Interop (Excel) | No existe en servidor | ClosedXML o EPPlus |
| PrintDocument | Impresion directa | Generar PDF + descargar |
| Timer para polling | Thread blocking | SignalR o polling JS |
| Process.Start() | No existe en servidor | Descargas de archivo |
| Clipboard | No existe en servidor | JS Interop |

### Logica que esta DENTRO de los formularios de SOLIDO

El proyecto SOLIDO tiene ~7,238 lineas en `Modulos/` que contienen logica de negocio
que NO fue migrada a ERP.Core porque esta en el proyecto de formularios, no en los plugins:

| Archivo en Modulos/ | Lineas | Contenido | Accion requerida |
|---------------------|--------|-----------|-----------------|
| cop_mgrabamovto.vb | ~3,000 | Motor de grabacion de movimientos cartera | Extraer a ERP.Core.CarteraFinanciera.Services |
| copvar.vb | ~2,800 | Variables y helpers de cartera | Extraer a ERP.Core.CarteraFinanciera.Helpers |
| contable.vb | ~2,300 | Operaciones contables | Extraer a ERP.Core.Contabilidad.Services |
| cop_cuadredoc.vb | ~1,200 | Cuadre de documentos | Extraer a ERP.Core.CarteraFinanciera.Services |
| ImportacionCnt.vb | ~900 | Importacion de datos contables | Extraer a ERP.Core.Contabilidad.Services |
| calculos_financieros.vb | ~500 | Calculos financieros | Extraer a ERP.Core.Compartido.Utilidades |

**IMPORTANTE**: Antes de crear componentes Blazor, migrar estas 7K lineas a ERP.Core.

### Recomendaciones SyncFusion

| Componente | Recomendacion |
|------------|---------------|
| **SfGrid** | Usar `AllowPaging`, `AllowFiltering`, `AllowSorting` server-side para tablas grandes |
| **SfDialog** | Usar para reemplazar ShowDialog() — configurar `IsModal=true` |
| **SfTab** | Para formularios con multiples secciones (solicitud credito tiene 10+ tabs) |
| **SfDropDownList** | Con `AllowFiltering` para listas de asociados/terceros (14K+ registros) |
| **SfNumericTextBox** | Configurar `Format`, `Decimals`, `Min`, `Max` por campo |
| **SfDatePicker** | Configurar formato segun `PstForFec` del sistema |
| **SfPdfViewer** | Para reemplazar Crystal ReportViewer |
| **SfScheduler** | Para calendario de pagos y vencimientos |
| **SfChart** | Para dashboards financieros (balances, indicadores) |
| **SfDashboardLayout** | Para pantalla principal con KPIs |

### Consideraciones de rendimiento

1. **Consultas SQL**: Migrar de string concatenation a parametros para prevenir SQL injection
2. **DataSets grandes**: Implementar paginacion server-side (cop_maecar puede tener 100K+ registros)
3. **Reportes**: Generar PDF asincronamente, no bloquear el UI thread
4. **Conexion ODBC**: Considerar migrar a driver nativo (MySqlConnector, Npgsql, etc.)
5. **Archivos planos**: Los procesos de generacion de planos bancarios deben ser background jobs
